using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Unified work execution for all locations (camp or expedition).
/// Returns WorkResult - caller handles logging and expedition tracking.
/// </summary>
public class WorkRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private bool PlayerDied => !_ctx.player.IsAlive;

    /// <summary>
    /// Check if location is too dark to work. Returns true if work is blocked.
    /// </summary>
    private bool CheckDarknessBlocking(Location location)
    {
        if (!location.IsDark)
            return false;

        // Check for active light source
        var heatSource = location.GetFeature<HeatSourceFeature>();
        if (heatSource != null && heatSource.IsActive)
            return false;

        GameDisplay.AddWarning(_ctx, "It's too dark to work here. You need a light source.");
        GameDisplay.Render(_ctx, statusText: "Darkness.");
        Input.WaitForKey(_ctx);
        return true;
    }

    public WorkResult DoForage(Location location)
    {
        if (CheckDarknessBlocking(location))
            return WorkResult.Empty(0);
        var feature = location.GetFeature<ForageFeature>();
        if (feature == null)
        {
            GameDisplay.AddNarrative(_ctx, "There's nothing to forage here.");
            return WorkResult.Empty(0);
        }

        GameDisplay.Render(_ctx, statusText: "Planning.");
        var workTimeChoice = new Choice<int>("How long should you forage?");
        workTimeChoice.AddOption("Quick gather - 15 min", 15);
        workTimeChoice.AddOption("Standard search - 30 min", 30);
        workTimeChoice.AddOption("Thorough search - 60 min", 60);
        int workTime = workTimeChoice.GetPlayerChoice(_ctx);

        // Movement impairment slows foraging (+20%)
        var capacities = _ctx.player.GetCapacities();
        if (AbilityCalculator.IsMovingImpaired(capacities.Moving))
        {
            workTime = (int)(workTime * 1.20);
            GameDisplay.AddWarning(_ctx, "Your limited movement slows the work.");
        }

        // Breathing impairment slows foraging (+15%)
        if (AbilityCalculator.IsBreathingImpaired(capacities.Breathing))
        {
            workTime = (int)(workTime * 1.15);
            GameDisplay.AddWarning(_ctx, "Your labored breathing slows the work.");
        }

        GameDisplay.AddNarrative(_ctx, "You search the area for resources...");

        var (died, actualWorkTime) = RunWorkWithContinuePrompt(location, workTime, ActivityType.Foraging, "foraging");
        if (died)
            return WorkResult.Died(actualWorkTime);

        var found = feature.Forage(actualWorkTime / 60.0);

        // Perception impairment reduces forage yield (-15%)
        var perception = AbilityCalculator.CalculatePerception(
            _ctx.player.Body, _ctx.player.EffectRegistry.GetCapacityModifiers());
        if (AbilityCalculator.IsPerceptionImpaired(perception))
        {
            found.ApplyForageMultiplier(0.85);
            GameDisplay.AddWarning(_ctx, "Your foggy senses cause you to miss some resources.");
        }

        _ctx.Inventory.Add(found);

        var collected = new List<string>();
        string quality = feature.GetQualityDescription();

        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative(_ctx, "You find nothing.");
            GameDisplay.AddNarrative(_ctx, GetForageFailureMessage(quality));
        }
        else
        {
            GameDisplay.AddNarrative(_ctx, "You found:");
            var grouped = found.Descriptions.GroupBy(d => d).Select(g => (g.Key, g.Count()));
            foreach (var (desc, count) in grouped)
            {
                string line = count > 1 ? $"{desc} ({count})" : desc;
                GameDisplay.AddNarrative(_ctx, $"  - {line}");
            }
            collected.AddRange(found.Descriptions);
            if (quality == "sparse" || quality == "picked over")
                GameDisplay.AddNarrative(_ctx, "Resources here are getting scarce.");
        }

        GameDisplay.Render(_ctx, statusText: "Thinking.");
        Input.WaitForKey(_ctx);

        // Check weight limit and force drop if needed
        ForceDropIfOverweight();

        return new WorkResult(collected, null, actualWorkTime, false);
    }

    public WorkResult DoHarvest(Location location)
    {
        if (CheckDarknessBlocking(location))
            return WorkResult.Empty(0);

        var harvestables = location
            .Features.OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources())
            .ToList();

        if (harvestables.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, "There's nothing to harvest here.");
            return WorkResult.Empty(0);
        }

        HarvestableFeature target;
        if (harvestables.Count == 1)
        {
            target = harvestables[0];
        }
        else
        {
            GameDisplay.Render(_ctx, statusText: "Planning.");
            var harvestChoice = new Choice<HarvestableFeature>("What do you want to harvest?");
            foreach (var h in harvestables)
            {
                harvestChoice.AddOption($"{h.DisplayName} - {h.GetStatusDescription()}", h);
            }
            target = harvestChoice.GetPlayerChoice(_ctx);
        }

        GameDisplay.Render(_ctx, statusText: "Planning.");
        var workTimeChoice = new Choice<int>($"How long should you harvest {target.DisplayName}?");
        workTimeChoice.AddOption("Quick work - 15 min", 15);
        workTimeChoice.AddOption("Standard work - 30 min", 30);
        workTimeChoice.AddOption("Thorough work - 60 min", 60);
        int workTime = workTimeChoice.GetPlayerChoice(_ctx);

        var (died, actualWorkTime) = RunWorkWithContinuePrompt(location, workTime, ActivityType.Foraging, "harvesting");
        if (died)
            return WorkResult.Died(actualWorkTime);

        var found = target.Harvest(actualWorkTime);
        _ctx.Inventory.Add(found);

        var collected = new List<string>();

        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative(_ctx, "You didn't get anything.");
        }
        else
        {
            foreach (var desc in found.Descriptions)
            {
                GameDisplay.AddNarrative(_ctx, $"You harvested {desc}");
                collected.Add(desc);
            }
        }

        GameDisplay.AddNarrative(_ctx, $"{target.DisplayName}: {target.GetStatusDescription()}");
        GameDisplay.Render(_ctx, statusText: "Thinking.");
        Input.WaitForKey(_ctx);

        // Check weight limit and force drop if needed
        ForceDropIfOverweight();

        return new WorkResult(collected, null, actualWorkTime, false);
    }

    public WorkResult DoExplore(Location location)
    {
        if (CheckDarknessBlocking(location))
            return WorkResult.Empty(0);

        if (!_ctx.Zone.HasUnrevealedLocations())
        {
            GameDisplay.AddNarrative(_ctx, "You've explored everything reachable from here.");
            return WorkResult.Empty(0);
        }

        double successChance = CalculateExploreChance(location);

        GameDisplay.Render(_ctx, statusText: "Planning.");
        var timeChoice = new Choice<int>(
            $"How thoroughly should you scout? ({successChance:P0} chance to find something)"
        );
        timeChoice.AddOption("Quick scout - 15 min", 15);
        timeChoice.AddOption("Standard scout - 30 min (+10%)", 30);
        timeChoice.AddOption("Thorough scout - 60 min (+20%)", 60);
        timeChoice.AddOption("Cancel", 0);
        int exploreTime = timeChoice.GetPlayerChoice(_ctx);

        if (exploreTime == 0)
            return WorkResult.Empty(0);

        // Breathing impairment slows exploration (+15%)
        var breathing = _ctx.player.GetCapacities().Breathing;
        if (AbilityCalculator.IsBreathingImpaired(breathing))
        {
            exploreTime = (int)(exploreTime * 1.15);
            GameDisplay.AddWarning(_ctx, "Your labored breathing slows the scouting.");
        }

        double timeBonus = exploreTime switch
        {
            30 => 0.10,
            60 => 0.20,
            _ => 0.0,
        };
        double finalChance = Math.Min(0.95, successChance + timeBonus);

        GameDisplay.AddNarrative(_ctx, "You scout the area, looking for new paths...");

        // Scouting takes you away from fire - use 0.0 proximity regardless of location
        var (died, actualExploreTime) = RunWorkWithContinuePrompt(location, exploreTime, ActivityType.Exploring, "exploring");
        if (died)
            return WorkResult.Died(actualExploreTime);

        Location? discovered = null;

        if (Utils.RandDouble(0, 1) <= finalChance)
        {
            var newLocation = _ctx.Zone.RevealRandomLocation(location);

            if (newLocation != null)
            {
                GameDisplay.AddSuccess(_ctx, $"You discovered a new area: {newLocation.Name}!");
                if (!string.IsNullOrEmpty(newLocation.Tags))
                    GameDisplay.AddNarrative(_ctx, newLocation.Tags);
                discovered = newLocation;
            }
            else
            {
                GameDisplay.AddNarrative(_ctx, "You scouted the area but found no new paths.");
            }
        }
        else
        {
            GameDisplay.AddNarrative(_ctx, "You searched the area but couldn't find any new paths.");
        }

        GameDisplay.Render(_ctx, statusText: "Thinking.");
        Input.WaitForKey(_ctx);

        return new WorkResult([], discovered, actualExploreTime, false);
    }

    // === PROGRESS AND TIME PASSAGE ===

    /// <summary>
    /// Runs work with progress bar and event checks, prompting to continue after events.
    /// Returns (died, actualMinutesWorked).
    /// </summary>
    private (bool died, int minutesWorked) RunWorkWithContinuePrompt(
        Location location, int workMinutes, ActivityType activity, string activityName)
    {
        int totalElapsed = 0;

        while (totalElapsed < workMinutes)
        {
            int remaining = workMinutes - totalElapsed;
            var (died, segmentElapsed) = RunWorkSegment(location, remaining, activity);
            totalElapsed += segmentElapsed;

            if (died)
                return (true, totalElapsed);

            // Check if an event interrupted us
            if (_ctx.EventOccurredLastUpdate && totalElapsed < workMinutes)
            {
                int remainingAfterEvent = workMinutes - totalElapsed;
                GameDisplay.Render(_ctx, statusText: "Interrupted");

                var choice = new Choice<bool>($"Continue {activityName}? ({remainingAfterEvent} min remaining)");
                choice.AddOption("Continue", true);
                choice.AddOption("Stop", false);

                if (!choice.GetPlayerChoice(_ctx))
                    break;
            }
        }

        return (false, totalElapsed);
    }

    /// <summary>
    /// Runs a single segment of work until completion, death, or event interruption.
    /// Returns (died, elapsedMinutes).
    /// </summary>
    private (bool died, int elapsed) RunWorkSegment(Location location, int workMinutes, ActivityType activity)
    {
        int elapsed = 0;

        while (elapsed < workMinutes)
        {
            GameDisplay.Render(
                _ctx,
                addSeparator: false,
                statusText: location.Name,
                progress: elapsed,
                progressTotal: workMinutes
            );

            int min = _ctx.Update(1, activity);
            elapsed += min;

            if (PlayerDied)
                return (true, elapsed);

            // Break on event so caller can prompt for continuation
            if (_ctx.EventOccurredLastUpdate)
                break;

            Thread.Sleep(100);
        }

        return (false, elapsed);
    }

    // === TRAPPING ===

    /// <summary>
    /// Set a snare at this location. Requires AnimalTerritoryFeature.
    /// </summary>
    public WorkResult DoSetTrap(Location location)
    {
        if (CheckDarknessBlocking(location))
            return WorkResult.Empty(0);

        // Validate location has animal territory
        var territory = location.GetFeature<AnimalTerritoryFeature>();
        if (territory == null)
        {
            GameDisplay.AddNarrative(_ctx, "No game trails here. Snares need animal territory.");
            return WorkResult.Empty(0);
        }

        // Get available snares from inventory
        var snares = _ctx.Inventory.Tools.Where(t => t.Type == ToolType.Snare && t.Works).ToList();
        if (snares.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, "You don't have any snares to set.");
            return WorkResult.Empty(0);
        }

        // Select which snare to use
        Tool selectedSnare;
        if (snares.Count == 1)
        {
            selectedSnare = snares[0];
        }
        else
        {
            GameDisplay.Render(_ctx, statusText: "Planning.");
            var snareChoice = new Choice<Tool>("Which snare do you want to set?");
            foreach (var snare in snares)
            {
                string durability = snare.Durability > 0 ? $"{snare.Durability} uses" : "unlimited";
                snareChoice.AddOption($"{snare.Name} ({durability})", snare);
            }
            selectedSnare = snareChoice.GetPlayerChoice(_ctx);
        }

        // Ask about bait
        GameDisplay.Render(_ctx, statusText: "Planning.");
        var baitChoice = new Choice<BaitType>("Do you want to bait the snare?");
        baitChoice.AddOption("No bait", BaitType.None);

        if (_ctx.Inventory.RawMeat.Count > 0 || _ctx.Inventory.CookedMeat.Count > 0)
            baitChoice.AddOption("Use meat (strong attraction, decays faster)", BaitType.Meat);
        if (_ctx.Inventory.Berries.Count > 0)
            baitChoice.AddOption("Use berries (moderate attraction)", BaitType.Berries);

        BaitType bait = baitChoice.GetPlayerChoice(_ctx);

        // Consume bait
        if (bait == BaitType.Meat)
        {
            if (_ctx.Inventory.RawMeat.Count > 0)
                _ctx.Inventory.RawMeat.RemoveAt(0);
            else
                _ctx.Inventory.CookedMeat.RemoveAt(0);
        }
        else if (bait == BaitType.Berries)
        {
            _ctx.Inventory.Berries.RemoveAt(0);
        }

        // Setting time
        int workTime = 10;

        // Manipulation impairment increases time and injury risk
        var capacities = _ctx.player.GetCapacities();
        double injuryChance = 0.05; // Base 5% injury chance

        if (AbilityCalculator.IsManipulationImpaired(capacities.Manipulation))
        {
            workTime = (int)(workTime * 1.25);
            injuryChance += 0.10 * (1.0 - capacities.Manipulation);
            GameDisplay.AddWarning(_ctx, "Your clumsy hands make setting the snare difficult.");
        }

        GameDisplay.AddNarrative(_ctx, "You find a promising game trail and set the snare...");

        var (died, actualWorkTime) = RunWorkWithContinuePrompt(location, workTime, ActivityType.Foraging, "setting trap");
        if (died)
            return WorkResult.Died(actualWorkTime);

        // Check for trap injury
        if (Utils.DetermineSuccess(injuryChance))
        {
            GameDisplay.AddWarning(_ctx, "The snare mechanism snaps unexpectedly!");
            _ctx.player.Body.Damage(new Bodies.DamageInfo(3, Bodies.DamageType.Sharp, "snare mechanism"));
            GameDisplay.AddNarrative(_ctx, "You cut your fingers on the trap mechanism.");
        }

        // Get or create SnareLineFeature at this location
        var snareLine = location.GetFeature<SnareLineFeature>();
        if (snareLine == null)
        {
            snareLine = new SnareLineFeature(territory);
            location.AddFeature(snareLine);
        }

        // Place the snare
        bool reinforced = selectedSnare.Name.Contains("Reinforced");
        if (bait != BaitType.None)
            snareLine.PlaceSnareWithBait(selectedSnare.Durability, bait, reinforced);
        else
            snareLine.PlaceSnare(selectedSnare.Durability, reinforced);

        // Remove snare from inventory
        _ctx.Inventory.Tools.Remove(selectedSnare);

        string baitMsg = bait != BaitType.None ? $" baited with {bait.ToString().ToLower()}" : "";
        GameDisplay.AddSuccess(_ctx, $"Snare set{baitMsg}. Check back later.");

        GameDisplay.Render(_ctx, statusText: "Done.");
        Input.WaitForKey(_ctx);

        return new WorkResult([$"Set {selectedSnare.Name}"], null, actualWorkTime, false);
    }

    /// <summary>
    /// Check all snares at this location.
    /// </summary>
    public WorkResult DoCheckTraps(Location location)
    {
        if (CheckDarknessBlocking(location))
            return WorkResult.Empty(0);

        var snareLine = location.GetFeature<SnareLineFeature>();
        if (snareLine == null || snareLine.SnareCount == 0)
        {
            GameDisplay.AddNarrative(_ctx, "No snares set here.");
            return WorkResult.Empty(0);
        }

        int workTime = 5 + (snareLine.SnareCount * 3); // Base time + per-snare check time

        // Manipulation impairment increases time and injury risk
        var capacities = _ctx.player.GetCapacities();
        double injuryChance = 0.03; // Lower base since just checking

        if (AbilityCalculator.IsManipulationImpaired(capacities.Manipulation))
        {
            workTime = (int)(workTime * 1.20);
            injuryChance += 0.08 * (1.0 - capacities.Manipulation);
        }

        GameDisplay.AddNarrative(_ctx, "You check your snare line...");

        var (died, actualWorkTime) = RunWorkWithContinuePrompt(location, workTime, ActivityType.Foraging, "checking traps");
        if (died)
            return WorkResult.Died(actualWorkTime);

        // Check for injury while handling traps
        if (Utils.DetermineSuccess(injuryChance))
        {
            GameDisplay.AddWarning(_ctx, "A snare catches your hand!");
            _ctx.player.Body.Damage(new Bodies.DamageInfo(2, Bodies.DamageType.Sharp, "snare"));
        }

        // Collect results
        var results = snareLine.CheckAllSnares();
        var collected = new List<string>();

        foreach (var result in results)
        {
            if (result.WasDestroyed)
            {
                GameDisplay.AddWarning(_ctx, "One snare was destroyed - torn apart by something large.");
            }
            else if (result.WasStolen)
            {
                GameDisplay.AddNarrative(_ctx, $"Something got here first. Only scraps of {result.AnimalType} remain.");
                // Add partial remains (bones)
                _ctx.Inventory.Bone.Add(0.1);
                collected.Add($"Scraps ({result.AnimalType})");
            }
            else if (result.AnimalType != null)
            {
                GameDisplay.AddSuccess(_ctx, $"Catch! A {result.AnimalType} ({result.WeightKg:F1}kg).");
                // Add raw meat based on weight
                _ctx.Inventory.RawMeat.Add(result.WeightKg * 0.5); // ~50% edible
                _ctx.Inventory.Bone.Add(result.WeightKg * 0.1);
                if (result.WeightKg > 3)
                    _ctx.Inventory.Hide.Add(result.WeightKg * 0.15);
                collected.Add($"{result.AnimalType} ({result.WeightKg:F1}kg)");
            }
        }

        if (collected.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, "Nothing caught yet. The snares are still set.");
        }

        // Report remaining snares
        int remaining = snareLine.SnareCount;
        if (remaining > 0)
            GameDisplay.AddNarrative(_ctx, $"{remaining} snare(s) still active.");
        else
            GameDisplay.AddNarrative(_ctx, "No snares remain at this location.");

        GameDisplay.Render(_ctx, statusText: "Done.");
        Input.WaitForKey(_ctx);

        // Check weight limit
        ForceDropIfOverweight();

        return new WorkResult(collected, null, actualWorkTime, false);
    }

    // === WORK OPTIONS (used by GameRunner and ExpeditionRunner) ===

    /// <summary>
    /// Check if any work is available at a location.
    /// </summary>
    public static bool HasWorkOptions(GameContext ctx, Location location, bool includeHunt = false)
    {
        if (location.HasFeature<ForageFeature>())
            return true;

        var harvestables = location
            .Features.OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources());
        if (harvestables.Any())
            return true;

        if (includeHunt && location.HasFeature<AnimalTerritoryFeature>())
            return true;

        // Trapping options
        if (CanSetTrap(ctx, location) || CanCheckTraps(location))
            return true;

        if (ctx.Zone.HasUnrevealedLocations())
            return true;

        return false;
    }

    /// <summary>
    /// Check if player can set a trap at this location.
    /// </summary>
    private static bool CanSetTrap(GameContext ctx, Location location)
    {
        return location.HasFeature<AnimalTerritoryFeature>() &&
               ctx.Inventory.Tools.Any(t => t.Type == ToolType.Snare && t.Works);
    }

    /// <summary>
    /// Check if there are traps to check at this location.
    /// </summary>
    private static bool CanCheckTraps(Location location)
    {
        var snareLine = location.GetFeature<SnareLineFeature>();
        return snareLine != null && snareLine.SnareCount > 0;
    }

    /// <summary>
    /// Get work options menu for a location. Returns null if no options available.
    /// </summary>
    public static Choice<string>? GetWorkOptions(
        GameContext ctx,
        Location location,
        bool includeHunt = false
    )
    {
        var choice = new Choice<string>("What work do you want to do?");
        bool hasOptions = false;

        if (location.HasFeature<ForageFeature>())
        {
            var forage = location.GetFeature<ForageFeature>()!;
            choice.AddOption($"Forage for resources ({forage.GetQualityDescription()})", "forage");
            hasOptions = true;
        }

        var harvestables = location
            .Features.OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources());
        if (harvestables.Any())
        {
            choice.AddOption("Harvest resources", "harvest");
            hasOptions = true;
        }

        if (includeHunt && location.HasFeature<AnimalTerritoryFeature>())
        {
            var territory = location.GetFeature<AnimalTerritoryFeature>()!;
            choice.AddOption($"Hunt ({territory.GetQualityDescription()})", "hunt");
            hasOptions = true;
        }

        // Trapping options
        if (CanSetTrap(ctx, location))
        {
            var snareCount = ctx.Inventory.Tools.Count(t => t.Type == ToolType.Snare && t.Works);
            choice.AddOption($"Set snare ({snareCount} available)", "set_trap");
            hasOptions = true;
        }

        if (CanCheckTraps(location))
        {
            var snareLine = location.GetFeature<SnareLineFeature>()!;
            choice.AddOption($"Check traps ({snareLine.GetDescription()})", "check_traps");
            hasOptions = true;
        }

        if (ctx.Zone.HasUnrevealedLocations())
        {
            choice.AddOption("Explore the area (discover new locations)", "explore");
            hasOptions = true;
        }

        choice.AddOption("Cancel", "cancel");
        return hasOptions ? choice : null;
    }

    /// <summary>
    /// Get labels for available work types (for menu display).
    /// </summary>
    public static List<string> GetWorkLabels(GameContext ctx, Location location)
    {
        var labels = new List<string>();

        if (location.HasFeature<ForageFeature>())
            labels.Add("Forage");

        if (ctx.Zone.HasUnrevealedLocations())
            labels.Add("Scout");

        return labels;
    }

    // === HELPERS ===

    /// <summary>
    /// Calculate chance to discover a new location.
    /// Decreases exponentially with existing connections.
    /// High visibility locations are better for scouting.
    /// </summary>
    public static double CalculateExploreChance(Location location)
    {
        int connections = location.Connections.Count;
        double baseChance = 0.90;
        double decayFactor = 0.55;
        double chance = baseChance * Math.Pow(decayFactor, connections);

        // High visibility improves scouting (up to +20% at visibility 2.0)
        chance += location.VisibilityFactor * 0.10;

        return Math.Min(0.95, chance);
    }

    public static string GetForageFailureMessage(string quality)
    {
        string[] messages = quality switch
        {
            "abundant" =>
            [
                "Fresh snow. Everything's buried.",
                "What you spot is rotten through.",
                "Frozen solid to the ground. Can't pry it loose.",
                "A sound nearby. You wait it out, lose your momentum.",
                "Ice crust over everything. Takes too long to break through.",
            ],
            "decent" =>
            [
                "Hollow log, empty inside. Wasted time.",
                "Wind-scoured ground. Bare rock in every crevice.",
                "Drifts deeper than they looked. Hard to search properly.",
                "What you find crumbles apart in your hands.",
                "Steep terrain. You cover less ground than planned.",
            ],
            "sparse" =>
            [
                "Slim pickings. Most of it's already gone.",
                "Traces of what was here. Nearly spent.",
                "Hardly anything left. You'd need luck.",
                "Almost picked clean. Time to look elsewhere.",
                "Scraps and remnants. This place won't last.",
            ],
            _ =>
            [
                "Stripped bare.",
                "It's gone. All of it.",
                "You're wasting time here.",
                "Barren.",
                "Move on.",
            ],
        };

        return messages[Random.Shared.Next(messages.Length)];
    }

    /// <summary>
    /// Check if player is over carry capacity and force them to drop items.
    /// </summary>
    private void ForceDropIfOverweight()
    {
        var inv = _ctx.Inventory;

        if (inv.RemainingCapacityKg >= 0)
            return;

        GameDisplay.ClearNarrative(_ctx);
        GameDisplay.AddWarning(_ctx,
            $"You're carrying too much! ({inv.CurrentWeightKg:F1}/{inv.MaxWeightKg:F0} kg)"
        );
        GameDisplay.AddNarrative(_ctx, "You must drop some items.");
        GameDisplay.Render(_ctx, statusText: "Overburdened.");
        Input.WaitForKey(_ctx);

        // Create a dummy "drop target" that just discards items
        var dropTarget = new Items.Inventory { MaxWeightKg = -1 };

        while (inv.RemainingCapacityKg < 0)
        {
            var items = inv.GetTransferableItems(dropTarget);
            if (items.Count == 0)
                break;

            var options = items.Select(i => $"{i.Description}").ToList();

            GameDisplay.ClearNarrative(_ctx);
            GameDisplay.AddWarning(_ctx,
                $"Over capacity by {-inv.RemainingCapacityKg:F1} kg. Drop something."
            );
            GameDisplay.Render(_ctx, statusText: "Overburdened.");

            string selected = Input.Select(_ctx, "Drop which item?", options);
            int idx = options.IndexOf(selected);

            items[idx].TransferTo();
            GameDisplay.AddNarrative(_ctx, $"Dropped {items[idx].Description}");
        }

        GameDisplay.AddNarrative(_ctx, "You adjust your load and continue.");
        GameDisplay.Render(_ctx, statusText: "Relieved.");
        Input.WaitForKey(_ctx);
    }
}
