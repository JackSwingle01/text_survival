using text_survival.Actors.NPCs;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

public class ExpeditionRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private bool PlayerDied => !_ctx.player.IsAlive;

    /// <summary>
    /// Main entry point - starts a flexible expedition from camp
    /// Player travels to other locations, works, then returns
    /// </summary>
    public void Run()
    {
        var expedition = new Expedition(_ctx.CurrentLocation, _ctx.player);
        _ctx.Expedition = expedition;

        GameDisplay.AddNarrative("Where do you want to go?");
        GameDisplay.Render(_ctx);

        // First, pick a destination from camp
        bool wentSomewhere = DoTravel(expedition);
        if (!wentSomewhere)
        {
            // Player cancelled travel, didn't actually leave
            _ctx.Expedition = null;
            return;
        }

        // Now we're away from camp - main loop
        bool stayOut = true;
        while (stayOut && !PlayerDied)
        {
            GameDisplay.Render(_ctx);

            var actionChoice = new Choice<string>("What do you do?");

            // Work options (not at camp - camp work is separate)
            if (!expedition.IsAtCamp && HasWorkOptions(expedition.CurrentLocation))
                actionChoice.AddOption("Work here", "work");

            // Travel options
            actionChoice.AddOption("Travel to another location", "travel");

            // Return to camp option
            if (!expedition.IsAtCamp)
                actionChoice.AddOption("Return to camp", "return");
            else
                stayOut = false; // Already back at camp, exit loop

            if (stayOut)
            {
                string action = actionChoice.GetPlayerChoice();

                if (action == "work")
                {
                    DoWork(expedition);
                }
                else if (action == "travel")
                {
                    DoTravel(expedition);
                }
                else // return
                {
                    ReturnToCamp(expedition);
                    stayOut = false;
                }
            }
        }

        // Expedition ended
        _ctx.Expedition = null;

        if (PlayerDied) return;

        // Back at camp
        GameDisplay.AddNarrative("You made it back to camp.");
        DisplayExpeditionSummary(expedition);
        GameDisplay.Render(_ctx);
    }

    /// <summary>
    /// Shows travel options and moves to selected destination.
    /// Returns true if travel occurred, false if cancelled.
    /// </summary>
    private bool DoTravel(Expedition expedition)
    {
        var connections = expedition.CurrentLocation.Connections;
        if (connections.Count == 0)
        {
            GameDisplay.AddNarrative("There's nowhere to go from here.");
            return false;
        }

        // Show travel options
        var choice = new Choice<Location?>("Where do you go?");

        foreach (var con in connections)
        {
            string lbl;
            if (con.Explored)
            {
                int minutes = TravelProcessor.GetTraversalMinutes(con, _ctx.player, _ctx.Inventory);
                lbl = $"{con.Name} (~{minutes} min)";
            }
            else
            {
                lbl = con.GetUnexploredHint(_ctx.player);
            }

            if (con == _ctx.Camp.Location)
                lbl += " - Camp";
            if (con == expedition.TravelHistory.ElementAtOrDefault(1))
                lbl += " (backtrack)";

            choice.AddOption(lbl, con);
        }

        choice.AddOption("Cancel", null);

        var destination = choice.GetPlayerChoice();
        if (destination == null) return false;

        // Travel with progress bar
        int travelTime = TravelProcessor.GetTraversalMinutes(destination, _ctx.player, _ctx.Inventory);
        expedition.State = ExpeditionState.Traveling;

        bool died = RunTravelWithProgress(expedition, destination, travelTime);
        if (died) return false;

        expedition.MoveTo(destination, travelTime);
        destination.Explore();

        GameDisplay.AddNarrative($"You arrive at {expedition.CurrentLocation.Name}.");
        if (!string.IsNullOrEmpty(expedition.CurrentLocation.Description))
            GameDisplay.AddNarrative(expedition.CurrentLocation.Description);

        return true;
    }

    private void ReturnToCamp(Expedition expedition)
    {
        GameDisplay.AddNarrative("You head back toward camp...");

        // Walk back through travel history
        while (!expedition.IsAtCamp && !PlayerDied)
        {
            // Next location is always the second item in the stack (backtrack)
            var nextLocation = expedition.TravelHistory.ElementAtOrDefault(1);
            if (nextLocation == null) break;

            int travelTime = TravelProcessor.GetTraversalMinutes(nextLocation, _ctx.player, _ctx.Inventory);
            expedition.State = ExpeditionState.Traveling;

            bool died = RunTravelWithProgress(expedition, nextLocation, travelTime);
            if (died) break;

            expedition.MoveTo(nextLocation, travelTime);
        }
    }

    private void DoWork(Expedition expedition)
    {
        var workChoice = GetWorkOptions(expedition.CurrentLocation);
        if (workChoice == null) return;

        string workType = workChoice.GetPlayerChoice();
        expedition.State = ExpeditionState.Working;

        switch (workType)
        {
            case "forage":
                DoForageWork(expedition);
                break;
            case "harvest":
                DoHarvestWork(expedition);
                break;
            case "hunt":
                DoHuntWork(expedition);
                break;
            case "explore":
                DoExploreWork(expedition);
                break;
            case "cancel":
                break;
        }

        expedition.State = ExpeditionState.Traveling;
    }

    private bool HasWorkOptions(Location location)
    {
        if (location.HasFeature<ForageFeature>())
            return true;

        var harvestables = location.Features
            .OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources());
        if (harvestables.Any())
            return true;

        if (location.HasFeature<AnimalTerritoryFeature>())
            return true;

        // Can explore if there are unrevealed locations in the zone
        if (_ctx.Zone.HasUnrevealedLocations())
            return true;

        return false;
    }

    private Choice<string>? GetWorkOptions(Location location)
    {
        var choice = new Choice<string>("What work do you want to do?");
        bool hasOptions = false;

        if (location.HasFeature<ForageFeature>())
        {
            var forage = location.GetFeature<ForageFeature>()!;
            choice.AddOption($"Forage for resources ({forage.GetQualityDescription()})", "forage");
            hasOptions = true;
        }

        var harvestables = location.Features
            .OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources());
        if (harvestables.Any())
        {
            choice.AddOption("Harvest resources", "harvest");
            hasOptions = true;
        }

        if (location.HasFeature<AnimalTerritoryFeature>())
        {
            var territory = location.GetFeature<AnimalTerritoryFeature>()!;
            choice.AddOption($"Hunt ({territory.GetQualityDescription()})", "hunt");
            hasOptions = true;
        }

        if (_ctx.Zone.HasUnrevealedLocations())
        {
            choice.AddOption("Explore the area (discover new locations)", "explore");
            hasOptions = true;
        }

        choice.AddOption("Cancel", "cancel");
        return hasOptions ? choice : null;
    }

    // --- Work Methods ---

    private void DoForageWork(Expedition expedition)
    {
        GameDisplay.Render(_ctx);
        var workTimeChoice = new Choice<int>("How long should you forage?");
        workTimeChoice.AddOption("Quick gather - 15 min", 15);
        workTimeChoice.AddOption("Standard search - 30 min", 30);
        workTimeChoice.AddOption("Thorough search - 60 min", 60);
        int workTime = workTimeChoice.GetPlayerChoice();

        bool died = RunWorkWithProgress(expedition, workTime, "Foraging");
        if (died) return;

        var feature = expedition.CurrentLocation.GetFeature<ForageFeature>()!;
        var found = feature.Forage(workTime / 60.0);
        _ctx.Inventory.Add(found);

        string quality = feature.GetQualityDescription();
        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative(GetForageFailureMessage(quality));
        }
        else
        {
            foreach (var desc in found.Descriptions)
            {
                GameDisplay.AddNarrative($"You found {desc}");
                expedition.CollectionLog.Add(desc);
            }
            if (quality == "sparse" || quality == "picked over")
                GameDisplay.AddNarrative("Resources here are getting scarce.");
        }
        GameDisplay.Render(_ctx);
        Input.WaitForKey();
    }

    private void DoHarvestWork(Expedition expedition)
    {
        var harvestables = expedition.CurrentLocation.Features
            .OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources())
            .ToList();

        if (harvestables.Count == 0)
        {
            GameDisplay.AddNarrative("There's nothing to harvest here.");
            return;
        }

        HarvestableFeature target;
        if (harvestables.Count == 1)
        {
            target = harvestables[0];
        }
        else
        {
            GameDisplay.Render(_ctx);
            var harvestChoice = new Choice<HarvestableFeature>("What do you want to harvest?");
            foreach (var h in harvestables)
            {
                harvestChoice.AddOption($"{h.DisplayName} - {h.GetStatusDescription()}", h);
            }
            target = harvestChoice.GetPlayerChoice();
        }

        GameDisplay.Render(_ctx);
        var workTimeChoice = new Choice<int>($"How long should you harvest {target.DisplayName}?");
        workTimeChoice.AddOption("Quick work - 15 min", 15);
        workTimeChoice.AddOption("Standard work - 30 min", 30);
        workTimeChoice.AddOption("Thorough work - 60 min", 60);
        int workTime = workTimeChoice.GetPlayerChoice();

        bool died = RunWorkWithProgress(expedition, workTime, "Harvesting");
        if (died) return;

        var found = target.Harvest(workTime);
        _ctx.Inventory.Add(found);

        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative("You didn't get anything.");
        }
        else
        {
            foreach (var desc in found.Descriptions)
            {
                GameDisplay.AddNarrative($"You harvested {desc}");
                expedition.CollectionLog.Add(desc);
            }
        }

        GameDisplay.AddNarrative($"{target.DisplayName}: {target.GetStatusDescription()}");
        GameDisplay.Render(_ctx);
        Input.WaitForKey();
    }

    private void DoHuntWork(Expedition expedition)
    {
        var feature = expedition.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        if (feature == null)
        {
            GameDisplay.AddNarrative("There's no game to be found here.");
            return;
        }

        while (!PlayerDied)
        {
            GameDisplay.AddNarrative($"\nYou scan the area for signs of game...");
            GameDisplay.AddNarrative($"Game density: {feature.GetQualityDescription()}");
            GameDisplay.Render(_ctx);

            var choice = new Choice<string>("What do you do?");
            choice.AddOption("Search for game (~15 min)", "search");
            choice.AddOption("Done hunting", "done");

            string action = choice.GetPlayerChoice();
            if (action == "done")
                break;

            int searchTime = 15;
            _ctx.Update(searchTime);
            expedition.AddTime(searchTime);

            if (PlayerDied) break;

            Animal? found = feature.SearchForGame(searchTime);
            if (found == null)
            {
                GameDisplay.AddNarrative("You find no game. The area seems quiet.");
                continue;
            }

            GameDisplay.AddNarrative($"You spot a {found.Name}!");
            GameDisplay.Render(_ctx);

            var huntChoice = new Choice<bool>("Do you want to stalk it?");
            huntChoice.AddOption($"Stalk the {found.Name}", true);
            huntChoice.AddOption("Let it go", false);

            if (!huntChoice.GetPlayerChoice())
                continue;

            int huntMinutes = RunSingleHunt(found, expedition.CurrentLocation, expedition);
            _ctx.Update(huntMinutes);
            expedition.AddTime(huntMinutes);

            if (PlayerDied) break;

            if (!found.IsAlive)
            {
                feature.RecordSuccessfulHunt();
            }
        }
    }

    private int RunSingleHunt(Animal target, Location location, Expedition expedition)
    {
        _ctx.player.stealthManager.StartHunting(target);
        int minutesSpent = 0;

        bool hasSpear = _ctx.Inventory.Weapon?.Type == ToolType.Spear;
        bool hasStones = _ctx.Inventory.StoneCount > 0;

        if (!hasSpear && !hasStones)
        {
            GameDisplay.AddWarning("You don't have a ranged weapon so you need to get very close. This will be difficult.");
        }

        while (_ctx.player.stealthManager.IsHunting && _ctx.player.stealthManager.IsTargetValid())
        {
            GameDisplay.AddNarrative($"\nDistance: {target.DistanceFromPlayer:F0}m | State: {target.State}");
            GameDisplay.Render(_ctx);

            var choice = new Choice<string>("What do you do?");
            choice.AddOption("Approach carefully", "approach");
            choice.AddOption("Assess target", "assess");

            // Add throw options if player has ranged weapons and is in range
            hasSpear = _ctx.Inventory.Weapon?.Type == ToolType.Spear;
            hasStones = _ctx.Inventory.StoneCount > 0;

            if (hasSpear)
            {
                double spearRange = GetSpearRange(_ctx.Inventory.Weapon!);
                if (target.DistanceFromPlayer <= spearRange)
                {
                    double hitChance = CalculateSpearHitChance(_ctx.Inventory.Weapon!, target);
                    choice.AddOption($"Throw {_ctx.Inventory.Weapon!.Name} ({hitChance:P0} hit)", "throw_spear");
                }
            }

            if (hasStones && target.Size == AnimalSize.Small && target.DistanceFromPlayer <= 12)
            {
                double hitChance = CalculateStoneHitChance(target);
                choice.AddOption($"Throw stone ({hitChance:P0} hit) [{_ctx.Inventory.StoneCount} left]", "throw_stone");
            }

            choice.AddOption("Give up this hunt", "stop");

            string action = choice.GetPlayerChoice();

            switch (action)
            {
                case "approach":
                    bool success = _ctx.player.stealthManager.AttemptApproach(location);
                    minutesSpent += 7;

                    if (success)
                    {
                        _ctx.player.Skills.GetSkill("Hunting").GainExperience(1);

                        if (target.DistanceFromPlayer <= 5 && target.State != AnimalState.Detected)
                        {
                            GameDisplay.AddNarrative("You're close enough to strike!");
                            GameDisplay.Render(_ctx);

                            var attackChoice = new Choice<bool>("Attack?");
                            attackChoice.AddOption("Strike now!", true);
                            attackChoice.AddOption("Back off", false);

                            if (attackChoice.GetPlayerChoice())
                            {
                                PerformKill(target, expedition);
                            }
                        }
                    }
                    else if (target.IsEngaged)
                    {
                        // TODO: combat
                    }
                    break;

                case "assess":
                    _ctx.player.stealthManager.AssessTarget();
                    minutesSpent += 2;
                    Input.WaitForKey();
                    break;

                case "throw_spear":
                    PerformRangedAttack(target, expedition, isSpear: true);
                    minutesSpent += 2;
                    break;

                case "throw_stone":
                    PerformRangedAttack(target, expedition, isSpear: false);
                    minutesSpent += 2;
                    break;

                case "stop":
                    _ctx.player.stealthManager.StopHunting("You abandon the hunt.");
                    break;
            }
        }

        return minutesSpent;
    }

    private void PerformKill(Animal target, Expedition expedition)
    {
        GameDisplay.AddNarrative($"You strike! The {target.Name} falls.");

        target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, "stealth kill", "Heart"));
        _ctx.player.stealthManager.StopHunting();

        var loot = ButcheringProcessor.Butcher(target);
        _ctx.Inventory.Add(loot);

        _ctx.player.Skills.GetSkill("Hunting").GainExperience(5);
        GameDisplay.AddNarrative($"You butcher the {target.Name} and collect {loot.TotalWeightKg:F1}kg of meat.");

        foreach (var desc in loot.Descriptions)
            expedition.CollectionLog.Add(desc);

        Input.WaitForKey();
    }

    #region Ranged Hunting Helpers

    private void PerformRangedAttack(Animal target, Expedition expedition, bool isSpear)
    {
        var weapon = _ctx.Inventory.Weapon;

        // Calculate hit chance
        bool applySmallPenalty = isSpear && target.Size == AnimalSize.Small;
        double hitChance = HuntingCalculator.CalculateThrownAccuracy(
            target.DistanceFromPlayer,
            isSpear ? GetSpearRange(weapon!) : 12,
            isSpear ? GetSpearBaseAccuracy(weapon!) : 0.65,
            applySmallPenalty
        );

        // Consume stone immediately (it's thrown either way)
        if (!isSpear)
        {
            _ctx.Inventory.TakeStone();
        }

        bool hit = Utils.RandDouble(0, 1) < hitChance;

        if (hit)
        {
            // Kill
            string weaponName = isSpear ? weapon!.Name : "stone";
            GameDisplay.AddNarrative($"Your {weaponName} strikes true! The {target.Name} falls.");
            target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, "thrown weapon", "Heart"));

            // Butcher
            var loot = ButcheringProcessor.Butcher(target);
            _ctx.Inventory.Add(loot);

            GameDisplay.AddNarrative($"You butcher the {target.Name} and collect {loot.TotalWeightKg:F1}kg of meat.");
            foreach (var desc in loot.Descriptions)
                expedition.CollectionLog.Add(desc);

            _ctx.player.stealthManager.StopHunting("Hunt successful.");
            Input.WaitForKey();
        }
        else
        {
            // Miss
            string weaponName = isSpear ? weapon!.Name : "stone";
            GameDisplay.AddNarrative($"Your {weaponName} misses! The {target.Name} bolts.");

            if (isSpear)
            {
                // Spear recovery: spend time searching
                GameDisplay.AddNarrative("You spend a few minutes searching for your spear...");
                _ctx.Update(3);
                expedition.AddTime(3);
            }

            // Animal flees
            _ctx.player.stealthManager.StopHunting($"The {target.Name} escaped.");
            Input.WaitForKey();
        }
    }

    private static double GetSpearRange(Tool spear)
    {
        // Stone-tipped spears have longer range
        return spear.Name.Contains("Stone") ? 25 : 20;
    }

    private static double GetSpearBaseAccuracy(Tool spear)
    {
        // Stone-tipped spears are more accurate
        return spear.Name.Contains("Stone") ? 0.75 : 0.70;
    }

    private double CalculateSpearHitChance(Tool spear, Animal target)
    {
        bool applySmallPenalty = target.Size == AnimalSize.Small;
        return HuntingCalculator.CalculateThrownAccuracy(
            target.DistanceFromPlayer,
            GetSpearRange(spear),
            GetSpearBaseAccuracy(spear),
            applySmallPenalty
        );
    }

    private double CalculateStoneHitChance(Animal target)
    {
        // Stones don't get small target penalty (they're designed for small game)
        return HuntingCalculator.CalculateThrownAccuracy(
            target.DistanceFromPlayer,
            12,
            0.65,
            targetIsSmall: false
        );
    }

    #endregion

    private void DoExploreWork(Expedition expedition)
    {
        // Check if there are unrevealed locations
        if (!_ctx.Zone.HasUnrevealedLocations())
        {
            GameDisplay.AddNarrative("You've explored everything reachable from here.");
            return;
        }

        var location = expedition.CurrentLocation;
        double successChance = CalculateExploreChance(location);

        GameDisplay.Render(_ctx);
        var timeChoice = new Choice<int>($"How thoroughly should you scout? ({successChance:P0} chance to find something)");
        timeChoice.AddOption("Quick scout - 15 min", 15);
        timeChoice.AddOption("Standard scout - 30 min (+10%)", 30);
        timeChoice.AddOption("Thorough scout - 60 min (+20%)", 60);
        int exploreTime = timeChoice.GetPlayerChoice();

        // Longer scouting improves chances
        double timeBonus = exploreTime switch
        {
            30 => 0.10,
            60 => 0.20,
            _ => 0.0
        };
        double finalChance = Math.Min(0.95, successChance + timeBonus);

        bool died = RunWorkWithProgress(expedition, exploreTime, "Exploring");
        if (died) return;

        // Roll for success
        if (Utils.RandDouble(0, 1) <= finalChance)
        {
            var newLocation = _ctx.Zone.RevealRandomLocation(location);

            if (newLocation != null)
            {
                GameDisplay.AddSuccess($"You discovered a new area: {newLocation.Name}!");
                if (!string.IsNullOrEmpty(newLocation.Description))
                    GameDisplay.AddNarrative(newLocation.Description);

                expedition.AddLog($"Discovered: {newLocation.Name}");
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

        GameDisplay.Render(_ctx);
        Input.WaitForKey();
    }

    /// <summary>
    /// Calculate chance to discover a new location.
    /// Decreases exponentially with existing connections.
    /// </summary>
    private static double CalculateExploreChance(Location location)
    {
        int connections = location.Connections.Count;
        double baseChance = 0.90;
        double decayFactor = 0.55; // Each connection multiplies chance by this

        return baseChance * Math.Pow(decayFactor, connections);
    }

    // --- Progress Bar Helpers ---

    /// <summary>
    /// Runs travel with a progress bar. Returns true if player died during travel.
    /// </summary>
    private bool RunTravelWithProgress(Expedition expedition, Location destination, int totalTime)
    {
        int elapsed = 0;
        GameEvent? triggeredEvent = null;
        bool died = false;

        while (elapsed < totalTime && !died)
        {
            Output.Progress($"Traveling to {destination.Name}...", totalTime, task =>
            {
                task?.Increment(elapsed);

                while (elapsed < totalTime && triggeredEvent == null && !died)
                {
                    var tickResult = GameEventRegistry.RunTicks(_ctx, 1);
                    _ctx.Update(tickResult.MinutesElapsed);
                    elapsed += tickResult.MinutesElapsed;
                    task?.Increment(tickResult.MinutesElapsed);

                    if (PlayerDied)
                    {
                        died = true;
                        break;
                    }

                    if (tickResult.TriggeredEvent != null)
                        triggeredEvent = tickResult.TriggeredEvent;

                    Thread.Sleep(100);
                }
            });

            if (died) break;

            if (triggeredEvent != null)
            {
                HandleEvent(triggeredEvent);
                triggeredEvent = null;
            }
        }

        return died;
    }

    /// <summary>
    /// Runs work with a progress bar. Returns true if player died during work.
    /// </summary>
    private bool RunWorkWithProgress(Expedition expedition, int workMinutes, string workType)
    {
        int elapsed = 0;
        GameEvent? triggeredEvent = null;
        bool died = false;

        while (elapsed < workMinutes && !died)
        {
            Output.Progress($"{workType} at {expedition.CurrentLocation.Name}...", workMinutes, task =>
            {
                task?.Increment(elapsed);

                while (elapsed < workMinutes && triggeredEvent == null && !died)
                {
                    var tickResult = GameEventRegistry.RunTicks(_ctx, 1);
                    _ctx.Update(tickResult.MinutesElapsed);
                    elapsed += tickResult.MinutesElapsed;
                    expedition.AddTime(tickResult.MinutesElapsed);
                    task?.Increment(tickResult.MinutesElapsed);

                    if (PlayerDied)
                    {
                        died = true;
                        break;
                    }

                    if (tickResult.TriggeredEvent != null)
                        triggeredEvent = tickResult.TriggeredEvent;

                    Thread.Sleep(100);
                }
            });

            if (died) break;

            if (triggeredEvent != null)
            {
                HandleEvent(triggeredEvent);
                triggeredEvent = null;
            }
        }

        return died;
    }

    private void HandleEvent(GameEvent evt)
    {
        GameDisplay.AddNarrative("EVENT:");
        GameDisplay.AddNarrative($"** {evt.Name} **");
        GameDisplay.AddNarrative(evt.Description + "\n");
        GameDisplay.Render(_ctx);

        var choice = evt.Choices.GetPlayerChoice();
        GameDisplay.AddNarrative(choice.Description + "\n");
        Input.WaitForKey();
        Output.ProgressSimple(">", 10);

        var outcome = choice.DetermineResult();
        HandleOutcome(outcome);
        GameDisplay.Render(_ctx);
        Input.WaitForKey();
    }

    private void HandleOutcome(EventResult outcome)
    {
        GameDisplay.AddNarrative("OUTCOME:");
        GameDisplay.AddNarrative(outcome.Message);

        if (outcome.TimeAddedMinutes != 0)
        {
            GameDisplay.AddNarrative($"(+{outcome.TimeAddedMinutes} minutes)");
            _ctx.Update(outcome.TimeAddedMinutes);
        }

        if (outcome.NewEffect is not null)
        {
            _ctx.player.EffectRegistry.AddEffect(outcome.NewEffect);
        }

        if (outcome.RewardPool != RewardPool.None)
        {
            var resources = RewardGenerator.Generate(outcome.RewardPool);
            if (!resources.IsEmpty)
            {
                _ctx.Inventory.Add(resources);
                foreach (var desc in resources.Descriptions)
                {
                    GameDisplay.AddNarrative($"You found {desc}");
                }
            }
        }
    }

    private void DisplayExpeditionSummary(Expedition expedition)
    {
        if (expedition.CollectionLog.Count > 0)
        {
            GameDisplay.AddNarrative("\n--- Expedition Summary ---");
            GameDisplay.AddNarrative($"Time away: {expedition.MinutesElapsedTotal} minutes");
            GameDisplay.AddNarrative("Collected:");
            foreach (var item in expedition.CollectionLog)
            {
                GameDisplay.AddNarrative($"  - {item}");
            }
        }

        var logs = expedition.FlushLogs();
        foreach (var log in logs)
        {
            GameDisplay.AddNarrative(log);
        }

        Input.WaitForKey();
    }

    private static string GetForageFailureMessage(string quality)
    {
        string[] messages = quality switch
        {
            "abundant" => [
                "You find plenty, but it's all frozen solid or rotted through. The area is rich - just not this haul.",
                "Fresh snow buries everything. You dig, but there's more here than you had time to uncover.",
                "A rich area, but everything usable is just out of reach. A longer search would help.",
                "You find things, but they crumble apart - frozen and brittle. Plenty more here though.",
                "Ice coats everything. Resources are visible beneath but locked away. The area is clearly bountiful."
            ],
            "decent" => [
                "You find a few scraps, but nothing worth keeping. The area still has potential.",
                "You turn up a few things, but nothing quite usable. There's more here with patience.",
                "Resources here take more effort to find. A more thorough search might turn something up.",
                "You turn up some possibilities, but nothing usable. More thorough searching might help.",
                "A modest area. You didn't find much this time, but it's not exhausted."
            ],
            "sparse" => [
                "Slim pickings. Most of what was here has already been taken.",
                "You find traces of what this place once offered. It's nearly spent.",
                "Hardly anything left. You'd need luck to find something useful here.",
                "The area is almost picked clean. Time to look elsewhere.",
                "Scraps and remnants. This place won't sustain you much longer."
            ],
            _ => [
                "Nothing. This place has been stripped bare.",
                "You search thoroughly and find nothing. Whatever was here is gone.",
                "Completely exhausted. You're wasting time here.",
                "Barren. Not a single useful thing remains.",
                "Empty. There's nothing left to find."
            ]
        };

        return messages[Random.Shared.Next(messages.Length)];
    }
}
