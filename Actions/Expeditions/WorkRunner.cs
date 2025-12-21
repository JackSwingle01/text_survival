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

    public WorkResult DoForage(Location location)
    {
        var feature = location.GetFeature<ForageFeature>();
        if (feature == null)
        {
            GameDisplay.AddNarrative("There's nothing to forage here.");
            return WorkResult.Empty(0);
        }

        GameDisplay.Render(_ctx, statusText: "Planning.");
        var workTimeChoice = new Choice<int>("How long should you forage?");
        workTimeChoice.AddOption("Quick gather - 15 min", 15);
        workTimeChoice.AddOption("Standard search - 30 min", 30);
        workTimeChoice.AddOption("Thorough search - 60 min", 60);
        int workTime = workTimeChoice.GetPlayerChoice();

        // Movement impairment slows foraging (+20%)
        var capacities = _ctx.player.GetCapacities();
        if (AbilityCalculator.IsMovingImpaired(capacities.Moving))
        {
            workTime = (int)(workTime * 1.20);
            GameDisplay.AddWarning("Your limited movement slows the work.");
        }

        // Breathing impairment slows foraging (+15%)
        if (AbilityCalculator.IsBreathingImpaired(capacities.Breathing))
        {
            workTime = (int)(workTime * 1.15);
            GameDisplay.AddWarning("Your labored breathing slows the work.");
        }

        GameDisplay.AddNarrative("You search the area for resources...");

        bool died = RunWorkWithProgress(location, workTime, ActivityType.Foraging);
        if (died)
            return WorkResult.Died(workTime);

        var found = feature.Forage(workTime / 60.0);

        // Perception impairment reduces forage yield (-15%)
        var perception = AbilityCalculator.CalculatePerception(
            _ctx.player.Body, _ctx.player.EffectRegistry.GetCapacityModifiers());
        if (AbilityCalculator.IsPerceptionImpaired(perception))
        {
            found.ApplyForageMultiplier(0.85);
            GameDisplay.AddWarning("Your foggy senses cause you to miss some resources.");
        }

        _ctx.Inventory.Add(found);

        var collected = new List<string>();
        string quality = feature.GetQualityDescription();

        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative("You find nothing.");
            GameDisplay.AddNarrative(GetForageFailureMessage(quality));
        }
        else
        {
            GameDisplay.AddNarrative("You found:");
            var grouped = found.Descriptions.GroupBy(d => d).Select(g => (g.Key, g.Count()));
            foreach (var (desc, count) in grouped)
            {
                string line = count > 1 ? $"{desc} ({count})" : desc;
                GameDisplay.AddNarrative($"  - {line}");
            }
            collected.AddRange(found.Descriptions);
            if (quality == "sparse" || quality == "picked over")
                GameDisplay.AddNarrative("Resources here are getting scarce.");
        }

        GameDisplay.Render(_ctx, statusText: "Thinking.");
        Input.WaitForKey();

        // Check weight limit and force drop if needed
        ForceDropIfOverweight();

        return new WorkResult(collected, null, workTime, false);
    }

    public WorkResult DoHarvest(Location location)
    {
        var harvestables = location
            .Features.OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources())
            .ToList();

        if (harvestables.Count == 0)
        {
            GameDisplay.AddNarrative("There's nothing to harvest here.");
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
            target = harvestChoice.GetPlayerChoice();
        }

        GameDisplay.Render(_ctx, statusText: "Planning.");
        var workTimeChoice = new Choice<int>($"How long should you harvest {target.DisplayName}?");
        workTimeChoice.AddOption("Quick work - 15 min", 15);
        workTimeChoice.AddOption("Standard work - 30 min", 30);
        workTimeChoice.AddOption("Thorough work - 60 min", 60);
        int workTime = workTimeChoice.GetPlayerChoice();

        bool died = RunWorkWithProgress(location, workTime, ActivityType.Foraging);
        if (died)
            return WorkResult.Died(workTime);

        var found = target.Harvest(workTime);
        _ctx.Inventory.Add(found);

        var collected = new List<string>();

        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative("You didn't get anything.");
        }
        else
        {
            foreach (var desc in found.Descriptions)
            {
                GameDisplay.AddNarrative($"You harvested {desc}");
                collected.Add(desc);
            }
        }

        GameDisplay.AddNarrative($"{target.DisplayName}: {target.GetStatusDescription()}");
        GameDisplay.Render(_ctx, statusText: "Thinking.");
        Input.WaitForKey();

        // Check weight limit and force drop if needed
        ForceDropIfOverweight();

        return new WorkResult(collected, null, workTime, false);
    }

    public WorkResult DoExplore(Location location)
    {
        if (!_ctx.Zone.HasUnrevealedLocations())
        {
            GameDisplay.AddNarrative("You've explored everything reachable from here.");
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
        int exploreTime = timeChoice.GetPlayerChoice();

        // Breathing impairment slows exploration (+15%)
        var breathing = _ctx.player.GetCapacities().Breathing;
        if (AbilityCalculator.IsBreathingImpaired(breathing))
        {
            exploreTime = (int)(exploreTime * 1.15);
            GameDisplay.AddWarning("Your labored breathing slows the scouting.");
        }

        double timeBonus = exploreTime switch
        {
            30 => 0.10,
            60 => 0.20,
            _ => 0.0,
        };
        double finalChance = Math.Min(0.95, successChance + timeBonus);

        GameDisplay.AddNarrative("You scout the area, looking for new paths...");

        // Scouting takes you away from fire - use 0.0 proximity regardless of location
        bool died = RunWorkWithProgress(location, exploreTime, ActivityType.Exploring);
        if (died)
            return WorkResult.Died(exploreTime);

        Location? discovered = null;

        if (Utils.RandDouble(0, 1) <= finalChance)
        {
            var newLocation = _ctx.Zone.RevealRandomLocation(location);

            if (newLocation != null)
            {
                GameDisplay.AddSuccess($"You discovered a new area: {newLocation.Name}!");
                if (!string.IsNullOrEmpty(newLocation.Description))
                    GameDisplay.AddNarrative(newLocation.Description);
                discovered = newLocation;
            }
            else
            {
                GameDisplay.AddNarrative("You scouted the area but found no new paths.");
            }
        }
        else
        {
            GameDisplay.AddNarrative("You searched the area but couldn't find any new paths.");
        }

        GameDisplay.Render(_ctx, statusText: "Thinking.");
        Input.WaitForKey();

        return new WorkResult([], discovered, exploreTime, false);
    }

    // === PROGRESS AND TIME PASSAGE ===

    /// <summary>
    /// Runs work with progress bar and event checks.
    /// Returns true if player died during work.
    /// </summary>
    private bool RunWorkWithProgress(Location location, int workMinutes, ActivityType activity)
    {
        int elapsed = 0;
        bool died = false;

        while (elapsed < workMinutes && !died)
        {
            GameDisplay.Render(
                _ctx,
                addSeparator: false,
                statusText: location.Name,
                progress: elapsed,
                progressTotal: workMinutes
            );

            // Use the new activity-based Update with event checking
            int min = _ctx.Update(1, activity);
            elapsed += min;

            if (PlayerDied)
            {
                died = true;
                break;
            }

            Thread.Sleep(100);
        }

        return died;
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

        if (ctx.Zone.HasUnrevealedLocations())
            return true;

        return false;
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
    /// </summary>
    public static double CalculateExploreChance(Location location)
    {
        int connections = location.Connections.Count;
        double baseChance = 0.90;
        double decayFactor = 0.55;
        return baseChance * Math.Pow(decayFactor, connections);
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

        GameDisplay.ClearNarrative();
        GameDisplay.AddWarning(
            $"You're carrying too much! ({inv.CurrentWeightKg:F1}/{inv.MaxWeightKg:F0} kg)"
        );
        GameDisplay.AddNarrative("You must drop some items.");
        GameDisplay.Render(_ctx, statusText: "Overburdened.");
        Input.WaitForKey();

        // Create a dummy "drop target" that just discards items
        var dropTarget = new Items.Inventory { MaxWeightKg = -1 };

        while (inv.RemainingCapacityKg < 0)
        {
            var items = inv.GetTransferableItems(dropTarget);
            if (items.Count == 0)
                break;

            var options = items.Select(i => $"{i.Description}").ToList();

            GameDisplay.ClearNarrative();
            GameDisplay.AddWarning(
                $"Over capacity by {-inv.RemainingCapacityKg:F1} kg. Drop something."
            );
            GameDisplay.Render(_ctx, statusText: "Overburdened.");

            string selected = Input.Select("Drop which item?", options);
            int idx = options.IndexOf(selected);

            items[idx].TransferTo();
            GameDisplay.AddNarrative($"Dropped {items[idx].Description}");
        }

        GameDisplay.AddNarrative("You adjust your load and continue.");
        GameDisplay.Render(_ctx, statusText: "Relieved.");
        Input.WaitForKey();
    }
}
