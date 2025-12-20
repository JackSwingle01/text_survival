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
            GameDisplay.Render(_ctx);
            Output.ProgressSimple("Searching for game...", searchTime);
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
            if (huntMinutes > 0)
            {
                GameDisplay.Render(_ctx);
                Output.ProgressSimple("Stalking...", huntMinutes);
            }
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
                        // Prey turned aggressive — handle as predator encounter
                        var outcome = HandlePredatorEncounter(target, expedition);

                        // Caller handles StealthManager cleanup based on outcome
                        switch (outcome)
                        {
                            case EncounterOutcome.PredatorRetreated:
                                _ctx.player.stealthManager.StopHunting($"The {target.Name} retreated.");
                                break;
                            case EncounterOutcome.PlayerEscaped:
                                _ctx.player.stealthManager.StopHunting("You escaped.");
                                break;
                            case EncounterOutcome.CombatVictory:
                                _ctx.player.stealthManager.StopHunting($"You killed the {target.Name}.");
                                break;
                            case EncounterOutcome.PlayerDied:
                                // No cleanup needed
                                break;
                        }
                        break; // Exit hunt loop after encounter
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

    #region Predator Encounter

    /// <summary>
    /// Handles a predator encounter. Returns outcome for caller to handle cleanup.
    /// Does NOT call StealthManager - caller is responsible for that.
    /// Expedition is nullable for reuse from events/travel.
    /// </summary>
    private EncounterOutcome HandlePredatorEncounter(Animal predator, Expedition? expedition = null)
    {
        // Initialize boldness from observable context
        predator.EncounterBoldness = predator.CalculateBoldness(_ctx.player, _ctx.Inventory);

        while (predator.IsAlive && _ctx.player.IsAlive)
        {
            // Display current state
            string boldnessDesc = predator.EncounterBoldness >= 0.7 ? "aggressive"
                : predator.EncounterBoldness > 0.3 ? "wary" : "hesitant";

            GameDisplay.AddNarrative($"\nThe {predator.Name} is {predator.DistanceFromPlayer:F0}m away, looking {boldnessDesc}.");

            // Show observable factors
            bool hasMeat = _ctx.Inventory.HasMeat;
            if (hasMeat)
                GameDisplay.AddNarrative("It's eyeing the meat you're carrying.");
            if (_ctx.player.Vitality < 0.7)
                GameDisplay.AddNarrative("It seems to sense your weakness.");

            GameDisplay.Render(_ctx);

            // Player options
            var choice = new Choice<string>("What do you do?");
            choice.AddOption("Stand your ground", "stand");
            choice.AddOption("Back away slowly", "back");
            choice.AddOption("Run", "run");
            if (predator.DistanceFromPlayer <= 20)
                choice.AddOption("Fight", "fight");
            if (hasMeat)
                choice.AddOption("Drop the meat", "drop_meat");

            string action = choice.GetPlayerChoice();

            switch (action)
            {
                case "stand":
                    GameDisplay.AddNarrative("You hold your position, facing the predator.");
                    predator.DistanceFromPlayer -= 10; // Predator closes
                    predator.EncounterBoldness -= 0.10; // But loses confidence

                    if (predator.EncounterBoldness < 0.3)
                    {
                        GameDisplay.AddNarrative($"The {predator.Name} hesitates... then slinks away.");
                        return EncounterOutcome.PredatorRetreated;
                    }
                    GameDisplay.AddNarrative($"The {predator.Name} moves closer, but seems less certain.");
                    break;

                case "back":
                    GameDisplay.AddNarrative("You slowly back away, keeping eyes on the predator.");
                    predator.DistanceFromPlayer += 5; // You gain distance
                    predator.EncounterBoldness += 0.05; // But it gets bolder
                    GameDisplay.AddNarrative($"Distance: {predator.DistanceFromPlayer:F0}m");
                    break;

                case "run":
                    var (escaped, narrative) = HuntingCalculator.CalculatePursuitOutcome(
                        _ctx.player, predator, predator.DistanceFromPlayer);
                    GameDisplay.AddNarrative(narrative);
                    if (escaped)
                    {
                        return EncounterOutcome.PlayerEscaped;
                    }
                    // Caught — forced combat
                    predator.DistanceFromPlayer = 5;
                    return RunPredatorCombat(predator, expedition);

                case "fight":
                    return RunPredatorCombat(predator, expedition);

                case "drop_meat":
                    double meatDropped = _ctx.Inventory.DropAllMeat();
                    GameDisplay.AddNarrative($"You drop {meatDropped:F1}kg of meat and back away.");
                    GameDisplay.AddNarrative($"The {predator.Name} goes for the meat. You slip away.");
                    return EncounterOutcome.PlayerEscaped;
            }

            // Boldness ceiling: very bold predator closes regardless of player action
            if (predator.EncounterBoldness >= 0.7)
            {
                GameDisplay.AddNarrative($"The {predator.Name} grows impatient and closes in.");
                predator.DistanceFromPlayer -= 10;
            }

            // Check if predator reaches attack range
            if (predator.DistanceFromPlayer <= 5)
            {
                GameDisplay.AddNarrative($"The {predator.Name} charges!");
                return RunPredatorCombat(predator, expedition);
            }

            _ctx.Update(1); // 1 minute per turn
            expedition?.AddTime(1); // Only if expedition context exists
            Input.WaitForKey();
        }

        return _ctx.player.IsAlive ? EncounterOutcome.PredatorRetreated : EncounterOutcome.PlayerDied;
    }

    private EncounterOutcome RunPredatorCombat(Animal predator, Expedition? expedition = null)
    {
        GameDisplay.AddNarrative($"Combat with {predator.Name}!");

        while (predator.IsAlive && _ctx.player.IsAlive)
        {
            GameDisplay.AddNarrative($"\nYou: {_ctx.player.Vitality:P0} | {predator.Name}: {predator.Vitality:P0}");
            GameDisplay.Render(_ctx);

            var choice = new Choice<string>("Your move:");
            choice.AddOption("Attack", "attack");
            choice.GetPlayerChoice();

            // Player attacks
            _ctx.player.Attack(predator);
            _ctx.Update(1);
            expedition?.AddTime(1);

            if (!predator.IsAlive) break;

            // Predator attacks
            predator.Attack(_ctx.player);
        }

        if (!_ctx.player.IsAlive)
        {
            return EncounterOutcome.PlayerDied;
        }

        // Victory
        GameDisplay.AddNarrative($"The {predator.Name} falls!");

        var butcherChoice = new Choice<bool>("Butcher the carcass?");
        butcherChoice.AddOption("Yes", true);
        butcherChoice.AddOption("No", false);

        if (butcherChoice.GetPlayerChoice())
        {
            var loot = ButcheringProcessor.Butcher(predator);
            _ctx.Inventory.Add(loot);
            GameDisplay.AddNarrative($"You collect {loot.TotalWeightKg:F1}kg of resources.");
            foreach (var desc in loot.Descriptions)
                expedition?.CollectionLog.Add(desc);
            _ctx.Update(10);
            expedition?.AddTime(10);
        }

        Input.WaitForKey();
        return EncounterOutcome.CombatVictory;
    }

    #endregion

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
                GameEventRegistry.HandleEvent(_ctx, triggeredEvent);
                triggeredEvent = null;
            }
        }

        return died;
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
}
