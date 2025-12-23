using text_survival.Actors.Animals;
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

        GameDisplay.AddNarrative(_ctx, "Where do you want to go?");
        GameDisplay.Render(_ctx, statusText: "Planning.");

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
            GameDisplay.Render(_ctx, statusText: "Surveying.");

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
                string action = actionChoice.GetPlayerChoice(_ctx);

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
        GameDisplay.AddNarrative(_ctx, "You made it back to camp.");
        DisplayExpeditionSummary(expedition);
        GameDisplay.Render(_ctx, statusText: "Arrived.");
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
            GameDisplay.AddNarrative(_ctx, "There's nowhere to go from here.");
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

        var destination = choice.GetPlayerChoice(_ctx);
        if (destination == null) return false;

        // Travel with progress bar
        int travelTime = TravelProcessor.GetTraversalMinutes(destination, _ctx.player, _ctx.Inventory);
        expedition.State = ExpeditionState.Traveling;

        bool died = RunTravelWithProgress(expedition, destination, travelTime);
        if (died) return false;

        expedition.MoveTo(destination, travelTime);
        destination.Explore();

        GameDisplay.AddNarrative(_ctx, $"You arrive at {expedition.CurrentLocation.Name}.");
        if (!string.IsNullOrEmpty(expedition.CurrentLocation.Tags))
            GameDisplay.AddNarrative(_ctx, expedition.CurrentLocation.Tags);

        return true;
    }

    private void ReturnToCamp(Expedition expedition)
    {
        GameDisplay.AddNarrative(_ctx, "You head back toward camp...");

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

        string workType = workChoice.GetPlayerChoice(_ctx);
        expedition.State = ExpeditionState.Working;

        var work = new WorkRunner(_ctx);
        WorkResult? result = null;

        switch (workType)
        {
            case "forage":
                result = work.DoForage(expedition.CurrentLocation);
                break;
            case "harvest":
                result = work.DoHarvest(expedition.CurrentLocation);
                break;
            case "hunt":
                DoHuntWork(expedition);
                break;
            case "explore":
                result = work.DoExplore(expedition.CurrentLocation);
                break;
            case "set_trap":
                result = work.DoSetTrap(expedition.CurrentLocation);
                break;
            case "check_traps":
                result = work.DoCheckTraps(expedition.CurrentLocation);
                break;
            case "cancel":
                break;
        }

        // Log results to expedition
        if (result != null)
        {
            expedition.CollectionLog.AddRange(result.CollectedItems);
            expedition.AddTime(result.MinutesElapsed);
            if (result.DiscoveredLocation != null)
                expedition.AddLog($"Discovered: {result.DiscoveredLocation.Name}");
        }

        expedition.State = ExpeditionState.Traveling;
    }

    private bool HasWorkOptions(Location location) =>
        WorkRunner.HasWorkOptions(_ctx, location, includeHunt: true);

    private Choice<string>? GetWorkOptions(Location location) =>
        WorkRunner.GetWorkOptions(_ctx, location, includeHunt: true);

    // --- Hunting (stays here - interactive, not time-passage based) ---

    private void DoHuntWork(Expedition expedition)
    {
        var feature = expedition.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        if (feature == null)
        {
            GameDisplay.AddNarrative(_ctx, "There's no game to be found here.");
            return;
        }

        while (!PlayerDied)
        {
            GameDisplay.AddNarrative(_ctx, $"\nYou scan the area for signs of game...");
            GameDisplay.AddNarrative(_ctx, $"Game density: {feature.GetQualityDescription()}");
            GameDisplay.Render(_ctx, statusText: "Scanning.");

            var choice = new Choice<string>("What do you do?");
            choice.AddOption("Search for game (~15 min)", "search");
            choice.AddOption("Done hunting", "done");

            string action = choice.GetPlayerChoice(_ctx);
            if (action == "done")
                break;

            int searchTime = 15;
            GameDisplay.UpdateAndRenderProgress(_ctx, "Searching for game...", searchTime, ActivityType.Hunting);
            expedition.AddTime(searchTime);

            if (PlayerDied) break;

            Animal? found = feature.SearchForGame(searchTime);
            if (found == null)
            {
                GameDisplay.AddNarrative(_ctx, "You find no game. The area seems quiet.");
                continue;
            }

            GameDisplay.AddNarrative(_ctx, $"You spot {found.GetTraitDescription()}.");
            GameDisplay.AddNarrative(_ctx, $"It's {found.GetActivityDescription()}.");
            GameDisplay.Render(_ctx, statusText: "Watching.");

            var huntChoice = new Choice<bool>("Do you want to stalk it?");
            huntChoice.AddOption($"Stalk the {found.Name}", true);
            huntChoice.AddOption("Let it go", false);

            if (!huntChoice.GetPlayerChoice(_ctx))
                continue;

            int huntMinutes = RunSingleHunt(found, expedition.CurrentLocation, expedition);
            if (huntMinutes > 0)
            {
                GameDisplay.UpdateAndRenderProgress(_ctx, "Stalking...", huntMinutes, ActivityType.Hunting);
            }
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
        _ctx.player.stealthManager.StartHunting(target, _ctx);
        int minutesSpent = 0;

        // Auto-equip spear if available
        var spear = _ctx.Inventory.GetOrEquipWeapon(_ctx, ToolType.Spear);
        bool hasSpear = spear != null;
        bool hasStones = _ctx.Inventory.StoneCount > 0;

        if (!hasSpear && !hasStones)
        {
            GameDisplay.AddWarning(_ctx, "You don't have a ranged weapon so you need to get very close. This will be difficult.");
        }

        while (_ctx.player.stealthManager.IsHunting && _ctx.player.stealthManager.IsTargetValid(_ctx))
        {
            GameDisplay.AddNarrative(_ctx, $"\nDistance: {target.DistanceFromPlayer:F0}m | {target.GetActivityDescription()}");
            GameDisplay.Render(_ctx, statusText: "Stalking.");

            var choice = new Choice<string>("What do you do?");
            choice.AddOption("Approach carefully", "approach");
            choice.AddOption("Wait and watch", "wait");
            choice.AddOption("Assess target", "assess");

            // Check throw options (spear already equipped at hunt start if available)
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

            if (hasStones && target.Size == AnimalSize.Small && target.DistanceFromPlayer <= 15)
            {
                double hitChance = CalculateStoneHitChance(target);
                choice.AddOption($"Throw stone ({hitChance:P0} hit) [{_ctx.Inventory.StoneCount} left]", "throw_stone");
            }

            choice.AddOption("Give up this hunt", "stop");

            string action = choice.GetPlayerChoice(_ctx);

            switch (action)
            {
                case "approach":
                    bool success = _ctx.player.stealthManager.AttemptApproach(location, _ctx);
                    minutesSpent += 7;

                    if (success)
                    {
                        _ctx.player.Skills.GetSkill("Hunting").GainExperience(1);

                        if (target.DistanceFromPlayer <= 5 && target.State != AnimalState.Detected)
                        {
                            GameDisplay.AddNarrative(_ctx, "You're close enough to strike!");
                            GameDisplay.Render(_ctx, statusText: "Poised.");

                            var attackChoice = new Choice<bool>("Attack?");
                            attackChoice.AddOption("Strike now!", true);
                            attackChoice.AddOption("Back off", false);

                            if (attackChoice.GetPlayerChoice(_ctx))
                            {
                                PerformKill(target, expedition);
                            }
                        }
                    }
                    else if (target.IsEngaged)
                    {
                        // Prey turned aggressive — handle as predator encounter
                        var outcome = EncounterRunner.HandlePredatorEncounter(target, _ctx);

                        // Caller handles StealthManager cleanup based on outcome
                        switch (outcome)
                        {
                            case EncounterOutcome.PredatorRetreated:
                                _ctx.player.stealthManager.StopHunting(_ctx, $"The {target.Name} retreated.");
                                break;
                            case EncounterOutcome.PlayerEscaped:
                                _ctx.player.stealthManager.StopHunting(_ctx, "You escaped.");
                                break;
                            case EncounterOutcome.CombatVictory:
                                _ctx.player.stealthManager.StopHunting(_ctx, $"You killed the {target.Name}.");
                                break;
                            case EncounterOutcome.PlayerDied:
                                // No cleanup needed
                                break;
                        }
                        break; // Exit hunt loop after encounter
                    }
                    break;

                case "assess":
                    _ctx.player.stealthManager.AssessTarget(_ctx);
                    minutesSpent += 2;
                    Input.WaitForKey(_ctx);
                    break;

                case "wait":
                    int waitTime = Utils.RandInt(5, 10);
                    GameDisplay.AddNarrative(_ctx, $"You wait and watch for {waitTime} minutes...");
                    minutesSpent += waitTime;

                    if (target.CheckActivityChange(waitTime, out var newActivity) && newActivity.HasValue)
                    {
                        GameDisplay.AddNarrative(_ctx, $"The {target.Name} shifts—now {target.GetActivityDescription()}.");
                    }
                    else
                    {
                        GameDisplay.AddNarrative(_ctx, $"The {target.Name} continues {target.GetActivityDescription()}.");
                    }
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
                    _ctx.player.stealthManager.StopHunting(_ctx, "You abandon the hunt.");
                    break;
            }
        }

        return minutesSpent;
    }

    private void PerformKill(Animal target, Expedition expedition)
    {
        GameDisplay.AddNarrative(_ctx, $"You strike! The {target.Name} falls.");

        target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, "stealth kill", "Heart"));
        _ctx.player.stealthManager.StopHunting(_ctx);

        var loot = ButcherRunner.ButcherAnimal(target, _ctx);
        _ctx.Inventory.Add(loot);

        _ctx.player.Skills.GetSkill("Hunting").GainExperience(5);
        GameDisplay.AddNarrative(_ctx, $"You butcher the {target.Name} and collect {loot.TotalWeightKg:F1}kg of meat.");

        foreach (var desc in loot.Descriptions)
            expedition.CollectionLog.Add(desc);

        Input.WaitForKey(_ctx);
    }

    #region Ranged Hunting Helpers

    private void PerformRangedAttack(Animal target, Expedition expedition, bool isSpear)
    {
        var weapon = _ctx.Inventory.Weapon;

        // Calculate manipulation penalty for thrown accuracy
        var manipulation = _ctx.player.GetCapacities().Manipulation;
        double manipPenalty = Bodies.AbilityCalculator.IsManipulationImpaired(manipulation) ? 0.15 : 0.0;

        // Calculate hit chance
        bool applySmallPenalty = isSpear && target.Size == AnimalSize.Small;
        double hitChance = HuntingCalculator.CalculateThrownAccuracy(
            target.DistanceFromPlayer,
            isSpear ? GetSpearRange(weapon!) : 12,
            isSpear ? GetSpearBaseAccuracy(weapon!) : 0.65,
            applySmallPenalty,
            manipPenalty
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
            GameDisplay.AddNarrative(_ctx, $"Your {weaponName} strikes true! The {target.Name} falls.");
            target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, "thrown weapon", "Heart"));

            // Butcher
            var loot = ButcherRunner.ButcherAnimal(target, _ctx);
            _ctx.Inventory.Add(loot);

            GameDisplay.AddNarrative(_ctx, $"You butcher the {target.Name} and collect {loot.TotalWeightKg:F1}kg of meat.");
            foreach (var desc in loot.Descriptions)
                expedition.CollectionLog.Add(desc);

            _ctx.player.stealthManager.StopHunting(_ctx, "Hunt successful.");
            Input.WaitForKey(_ctx);
        }
        else
        {
            // Miss - but check for glancing hit (wound)
            // Close misses (within 15% of hit threshold) can become wounds
            double roll = Utils.RandDouble(0, 1);
            bool isGlancingHit = roll < hitChance + (hitChance * 0.3) && isSpear; // Only spears can wound

            string weaponName = isSpear ? weapon!.Name : "stone";

            if (isGlancingHit)
            {
                // Glancing hit - animal wounded but escapes
                // Wound severity determines tracking success (arterial = high, muscle = low)
                double woundSeverity = Utils.RandDouble(0.3, 0.8);
                string woundDesc = woundSeverity > 0.6
                    ? "Bright red arterial spray, pumping with each heartbeat"
                    : "Dark blood, muscle wound — the animal barely slowed";

                GameDisplay.AddNarrative(_ctx, $"Your {weaponName} grazes the {target.Name}!");
                GameDisplay.AddNarrative(_ctx, woundDesc);
                GameDisplay.AddNarrative(_ctx, $"The {target.Name} bolts, leaving blood on the snow.");

                // Create WoundedPrey tension - entry point to Blood Trail arc
                var tension = Tensions.ActiveTension.WoundedPrey(
                    woundSeverity,
                    target.Name,
                    expedition.CurrentLocation
                );
                _ctx.Tensions.AddTension(tension);

                GameDisplay.AddNarrative(_ctx, "You could follow the blood trail...");
            }
            else
            {
                // Clean miss
                GameDisplay.AddNarrative(_ctx, $"Your {weaponName} misses! The {target.Name} bolts.");
            }

            if (isSpear)
            {
                // Spear recovery: spend time searching
                GameDisplay.AddNarrative(_ctx, "You spend a few minutes searching for your spear...");
                _ctx.Update(3, ActivityType.Hunting);
                expedition.AddTime(3);
            }

            // Animal flees
            _ctx.player.stealthManager.StopHunting(_ctx, $"The {target.Name} escaped.");
            Input.WaitForKey(_ctx);
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
        var manipulation = _ctx.player.GetCapacities().Manipulation;
        double manipPenalty = Bodies.AbilityCalculator.IsManipulationImpaired(manipulation) ? 0.15 : 0.0;

        bool applySmallPenalty = target.Size == AnimalSize.Small;
        return HuntingCalculator.CalculateThrownAccuracy(
            target.DistanceFromPlayer,
            GetSpearRange(spear),
            GetSpearBaseAccuracy(spear),
            applySmallPenalty,
            manipPenalty
        );
    }

    private double CalculateStoneHitChance(Animal target)
    {
        var manipulation = _ctx.player.GetCapacities().Manipulation;
        double manipPenalty = Bodies.AbilityCalculator.IsManipulationImpaired(manipulation) ? 0.15 : 0.0;

        // Stones don't get small target penalty (they're designed for small game)
        return HuntingCalculator.CalculateThrownAccuracy(
            target.DistanceFromPlayer,
            12,
            0.65,
            targetIsSmall: false,
            manipPenalty
        );
    }

    #endregion
    // --- Progress Bar Helpers ---

    /// <summary>
    /// Runs travel with a progress bar. Returns true if player died during travel.
    /// </summary>
    private bool RunTravelWithProgress(Expedition expedition, Location destination, int totalTime)
    {
        int elapsed = 0;
        bool died = false;
        string statusText = $"Traveling to {destination.Name}...";

        while (elapsed < totalTime && !died)
        {
            GameDisplay.Render(_ctx,
                addSeparator: false,
                statusText: statusText,
                progress: elapsed,
                progressTotal: totalTime);

            // Use the new activity-based Update with event checking
            elapsed += _ctx.Update(1, ActivityType.Traveling);

            if (PlayerDied)
            {
                died = true;
                break;
            }

            Thread.Sleep(100);
        }

        return died;
    }

    private void DisplayExpeditionSummary(Expedition expedition)
    {
        if (expedition.CollectionLog.Count > 0)
        {
            GameDisplay.AddNarrative(_ctx, "\n--- Expedition Summary ---");
            GameDisplay.AddNarrative(_ctx, $"Time away: {expedition.MinutesElapsedTotal} minutes");
            GameDisplay.AddNarrative(_ctx, "Collected:");
            foreach (var item in expedition.CollectionLog)
            {
                GameDisplay.AddNarrative(_ctx, $"  - {item}");
            }
        }

        var logs = expedition.FlushLogs();
        foreach (var log in logs)
        {
            GameDisplay.AddNarrative(_ctx, log);
        }

        Input.WaitForKey(_ctx);
    }


}
