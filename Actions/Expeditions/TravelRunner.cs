using text_survival.Actors.Animals;
using text_survival.Actions.Handlers;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

public class TravelRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private bool PlayerDied => !_ctx.player.IsAlive;


    /// <summary>
    /// Shows travel options and moves to selected destination.
    /// Loops until player chooses to stop traveling.
    /// </summary>
    public void DoTravel()
    {
        while (true)
        {
            var connections = _ctx.CurrentLocation.GetConnections(_ctx);
            if (connections.Count == 0)
            {
                GameDisplay.AddNarrative(_ctx, "You don't know where to go from here. You need to explore first.");
                return;
            }

            // Show travel options
            var choice = new Choice<Location?>("Where do you go?");

            foreach (var con in connections)
            {
                string lbl;
                if (con.Explored)
                {
                    int minutes = TravelProcessor.GetTraversalMinutes(con, _ctx.player, _ctx.Inventory);
                    // Show name with tags for explored locations
                    string tags = !string.IsNullOrEmpty(con.Tags) ? $" {con.Tags}" : "";
                    lbl = $"{con.Name}{tags} (~{minutes} min)";
                }
                else
                {
                    lbl = con.GetUnexploredHint(_ctx.player);
                }

                if (con == _ctx.Camp)
                    lbl += " - Camp";

                // todo - add backtracking?
                // if (con == expedition.TravelHistory.ElementAtOrDefault(1))
                //     lbl += " (backtrack)";

                choice.AddOption(lbl, con);
            }

            choice.AddOption("Done", null);

            var destination = choice.GetPlayerChoice(_ctx);
            if (destination == null) break;

            bool success = TravelToLocation(destination);
            if (!success) return; // Player died, exit entirely
        }
    }

    /// <summary>
    /// Travels to the specified destination, handling hazardous terrain and progress.
    /// Returns true if travel succeeded, false if player died.
    /// </summary>
    internal bool TravelToLocation(Location destination)
    {
        int travelTime;
        bool quickTravel = false;
        double injuryRisk = 0;

        if (TravelProcessor.IsHazardousTerrain(destination))
        {
            int quickTime = TravelProcessor.GetTraversalMinutes(destination, _ctx.player, _ctx.Inventory);
            int carefulTime = TravelProcessor.GetCarefulTraversalMinutes(destination, _ctx.player, _ctx.Inventory);
            injuryRisk = TravelProcessor.GetInjuryRisk(destination, _ctx.player, _ctx.Weather);

            GameDisplay.AddNarrative(_ctx, "The terrain ahead looks treacherous.");

            var speedChoice = new Choice<bool>("How do you proceed?");
            speedChoice.AddOption($"Careful (~{carefulTime} min) - Safe passage", false);
            speedChoice.AddOption($"Quick (~{quickTime} min) - {injuryRisk:P0} injury risk", true);

            quickTravel = speedChoice.GetPlayerChoice(_ctx);
            travelTime = quickTravel ? quickTime : carefulTime;
        }
        else
        {
            travelTime = TravelProcessor.GetTraversalMinutes(destination, _ctx.player, _ctx.Inventory);
        }

        bool died = RunTravelWithProgress(travelTime);
        if (died) return false;

        // Check for injury if quick travel through hazardous terrain
        if (quickTravel && injuryRisk > 0)
        {
            if (Utils.RandDouble(0, 1) < injuryRisk)
            {
                TravelHandler.ApplyTravelInjury(_ctx, destination);
            }
        }

        bool firstVisit = !destination.Explored;
        _ctx.CurrentLocation = destination;
        destination.Explore();

        // Check for victory
        if (_ctx.IsWinLocation(destination))
        {
            HandleVictory();
            return true;
        }

        GameDisplay.AddNarrative(_ctx, $"You arrive at {destination.Name}.");

        // Show discovery text on first visit
        if (firstVisit && !string.IsNullOrEmpty(destination.DiscoveryText))
        {
            GameDisplay.AddNarrative(_ctx, destination.DiscoveryText);
        }

        return true;
    }






    // --- Hunting (stays here - interactive, not time-passage based) ---

    public void DoHuntWork()
    {
        var feature = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
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

            int huntMinutes = RunSingleHunt(found, ctx.CurrentLocation);
            if (huntMinutes > 0)
            {
                GameDisplay.UpdateAndRenderProgress(_ctx, "Stalking...", huntMinutes, ActivityType.Hunting);
            }

            if (PlayerDied) break;

            if (!found.IsAlive)
            {
                feature.RecordSuccessfulHunt();
            }
        }
    }

    private int RunSingleHunt(Animal target, Location location)
    {
        _ctx.player.stealthManager.StartHunting(target, _ctx);
        int minutesSpent = 0;

        // Auto-equip spear if available
        var spear = _ctx.Inventory.GetOrEquipWeapon(_ctx, ToolType.Spear);
        bool hasSpear = spear != null;
        bool hasStones = _ctx.Inventory.Count(Resource.Stone) > 0;

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
            hasStones = _ctx.Inventory.Count(Resource.Stone) > 0;

            if (hasSpear && _ctx.Inventory.Weapon != null)
            {
                double spearRange = HuntHandler.GetSpearRange(_ctx.Inventory.Weapon);
                if (target.DistanceFromPlayer <= spearRange)
                {
                    double hitChance = HuntHandler.CalculateSpearHitChance(_ctx.Inventory.Weapon, target, _ctx);
                    choice.AddOption($"Throw {_ctx.Inventory.Weapon.Name} ({hitChance:P0} hit)", "throw_spear");
                }
            }

            if (hasStones && target.Size == AnimalSize.Small && target.DistanceFromPlayer <= 15)
            {
                double hitChance = HuntHandler.CalculateStoneHitChance(target, _ctx);
                choice.AddOption($"Throw stone ({hitChance:P0} hit) [{_ctx.Inventory.Count(Resource.Stone)} left]", "throw_stone");
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
                                PerformKill(target);
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
                    PerformRangedAttack(target, isSpear: true);
                    minutesSpent += 2;
                    break;

                case "throw_stone":
                    PerformRangedAttack(target, isSpear: false);
                    minutesSpent += 2;
                    break;

                case "stop":
                    _ctx.player.stealthManager.StopHunting(_ctx, "You abandon the hunt.");
                    break;
            }
        }

        return minutesSpent;
    }

    private void PerformKill(Animal target)
    {
        GameDisplay.AddNarrative(_ctx, $"You strike! The {target.Name} falls.");

        target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, "stealth kill", "Heart"));
        _ctx.player.stealthManager.StopHunting(_ctx);

        var loot = ButcherRunner.ButcherAnimal(target, _ctx);
        _ctx.Inventory.Combine(loot);

        _ctx.player.Skills.GetSkill("Hunting").GainExperience(5);
        GameDisplay.AddNarrative(_ctx, $"You butcher the {target.Name} and collect {loot.CurrentWeightKg:F1}kg of meat.");

        Input.WaitForKey(_ctx);
    }

    #region Ranged Hunting Helpers

    private void PerformRangedAttack(Animal target, bool isSpear)
    {
        var weapon = _ctx.Inventory.Weapon;

        // Calculate hit chance using HuntHandler
        double hitChance = HuntHandler.CalculateThrownAccuracy(_ctx, target, isSpear, weapon);

        // Consume stone immediately (it's thrown either way)
        if (!isSpear)
        {
            _ctx.Inventory.Pop(Resource.Stone);
        }

        bool hit = Utils.RandDouble(0, 1) < hitChance;

        if (hit)
        {
            // Kill
            string weaponName = isSpear && weapon != null ? weapon.Name : "stone";
            GameDisplay.AddNarrative(_ctx, $"Your {weaponName} strikes true! The {target.Name} falls.");
            target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, "thrown weapon", "Heart"));

            // Butcher
            var loot = ButcherRunner.ButcherAnimal(target, _ctx);
            _ctx.Inventory.Combine(loot);

            GameDisplay.AddNarrative(_ctx, $"You butcher the {target.Name} and collect {loot.CurrentWeightKg:F1}kg of meat.");

            _ctx.player.stealthManager.StopHunting(_ctx, "Hunt successful.");
            Input.WaitForKey(_ctx);
        }
        else
        {
            // Miss - but check for glancing hit (wound)
            // Close misses (within 15% of hit threshold) can become wounds
            double roll = Utils.RandDouble(0, 1);
            bool isGlancingHit = roll < hitChance + (hitChance * 0.3) && isSpear; // Only spears can wound

            string weaponName = isSpear && weapon != null ? weapon.Name : "stone";

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
                    ctx.CurrentLocation
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
            }

            // Animal flees
            _ctx.player.stealthManager.StopHunting(_ctx, $"The {target.Name} escaped.");
            Input.WaitForKey(_ctx);
        }
    }

    #endregion
    // --- Progress Bar Helpers ---

    /// <summary>
    /// Runs travel with a progress bar. Returns true if player died during travel.
    /// </summary>
    private bool RunTravelWithProgress(int totalTime)
    {
        int elapsed = 0;
        bool died = false;
        string statusText = $"Traveling...";

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

    private void HandleVictory()
    {
        _ctx.TriggerVictory();

        GameDisplay.ClearNarrative(_ctx);
        GameDisplay.AddSuccess(_ctx, "You made it.");
        GameDisplay.AddNarrative(_ctx, "");
        GameDisplay.AddNarrative(_ctx, "The pass is behind you now.");
        GameDisplay.AddNarrative(_ctx, "Below, the far valley stretches green and sheltered.");
        GameDisplay.AddNarrative(_ctx, "Smoke rises from distant fires. Your tribe is there.");
        GameDisplay.AddNarrative(_ctx, "");
        GameDisplay.AddNarrative(_ctx, "You survived.");
        GameDisplay.AddNarrative(_ctx, "");
        GameDisplay.AddNarrative(_ctx, $"Days survived: {_ctx.DaysSurvived}");
        GameDisplay.AddNarrative(_ctx, $"Season: {_ctx.Weather.GetSeasonLabel()}");
        GameDisplay.Render(_ctx, statusText: "Victory!");

        Input.WaitForKey(_ctx);
    }

}
