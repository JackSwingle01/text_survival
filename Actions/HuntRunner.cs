using text_survival.Actions.Expeditions;
using text_survival.Actions.Handlers;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

/// <summary>
/// Result of an interactive hunt.
/// </summary>
public enum HuntOutcome
{
    Success,        // Kill + butcher completed
    PreyFled,       // Animal escaped
    PlayerAbandoned,// Player gave up
    TurnedCombat,   // Prey became aggressive, encounter ran
    PlayerDied      // Player died during hunt
}

/// <summary>
/// Handles the interactive hunting phase after an animal is found.
/// This is the approach/throw/kill loop - separate from the time-based search.
/// </summary>
public static class HuntRunner
{
    /// <summary>
    /// Run an interactive hunt against a found animal.
    /// Prompts player to stalk, then runs the approach/attack loop.
    /// </summary>
    /// <returns>Outcome of the hunt and minutes elapsed</returns>
    public static (HuntOutcome outcome, int minutesElapsed) Run(Animal target, Location location, GameContext ctx)
    {
        GameDisplay.Render(ctx, statusText: "Watching.");

        // Prompt to stalk
        var huntChoice = new Choice<bool>("Do you want to stalk it?");
        huntChoice.AddOption($"Stalk the {target.Name}", true);
        huntChoice.AddOption("Let it go", false);

        if (!huntChoice.GetPlayerChoice(ctx))
        {
            return (HuntOutcome.PlayerAbandoned, 0);
        }

        // Run the interactive hunt loop
        int minutesSpent = RunHuntLoop(target, location, ctx);

        if (!ctx.player.IsAlive)
            return (HuntOutcome.PlayerDied, minutesSpent);

        if (!target.IsAlive)
        {
            // Record the successful hunt in the territory
            var territory = location.GetFeature<AnimalTerritoryFeature>();
            territory?.RecordSuccessfulHunt();
            return (HuntOutcome.Success, minutesSpent);
        }

        if (target.IsEngaged)
            return (HuntOutcome.TurnedCombat, minutesSpent);

        return (HuntOutcome.PreyFled, minutesSpent);
    }

    private static int RunHuntLoop(Animal target, Location location, GameContext ctx)
    {
        ctx.player.stealthManager.StartHunting(target, ctx);
        int minutesSpent = 0;

        // Auto-equip spear if available
        var spear = ctx.Inventory.GetOrEquipWeapon(ctx, ToolType.Spear);
        bool hasSpear = spear != null;
        bool hasStones = ctx.Inventory.Count(Resource.Stone) > 0;

        if (!hasSpear && !hasStones)
        {
            GameDisplay.AddWarning(ctx, "You don't have a ranged weapon so you need to get very close. This will be difficult.");
        }

        while (ctx.player.stealthManager.IsHunting && ctx.player.stealthManager.IsTargetValid(ctx))
        {
            GameDisplay.AddNarrative(ctx, $"\nDistance: {target.DistanceFromPlayer:F0}m | {target.GetActivityDescription()}");
            GameDisplay.Render(ctx, statusText: "Stalking.");

            var choice = new Choice<string>("What do you do?");
            choice.AddOption("Approach carefully", "approach");
            choice.AddOption("Wait and watch", "wait");
            choice.AddOption("Assess target", "assess");

            // Check throw options (spear already equipped at hunt start if available)
            hasSpear = ctx.Inventory.Weapon?.ToolType == ToolType.Spear;
            hasStones = ctx.Inventory.Count(Resource.Stone) > 0;

            if (hasSpear && ctx.Inventory.Weapon != null)
            {
                double spearRange = HuntHandler.GetSpearRange(ctx.Inventory.Weapon);
                if (target.DistanceFromPlayer <= spearRange)
                {
                    double hitChance = HuntHandler.CalculateSpearHitChance(ctx.Inventory.Weapon, target, ctx);
                    choice.AddOption($"Throw {ctx.Inventory.Weapon.Name} ({hitChance:P0} hit)", "throw_spear");
                }
            }

            if (hasStones && target.Size == AnimalSize.Small && target.DistanceFromPlayer <= 15)
            {
                double hitChance = HuntHandler.CalculateStoneHitChance(target, ctx);
                choice.AddOption($"Throw stone ({hitChance:P0} hit) [{ctx.Inventory.Count(Resource.Stone)} left]", "throw_stone");
            }

            choice.AddOption("Give up this hunt", "stop");

            string action = choice.GetPlayerChoice(ctx);

            switch (action)
            {
                case "approach":
                    bool success = ctx.player.stealthManager.AttemptApproach(location, ctx);
                    minutesSpent += 7;

                    if (success)
                    {
                        ctx.player.Skills.GetSkill("Hunting").GainExperience(1);

                        if (target.DistanceFromPlayer <= 5 && target.State != AnimalState.Detected)
                        {
                            GameDisplay.AddNarrative(ctx, "You're close enough to strike!");
                            GameDisplay.Render(ctx, statusText: "Poised.");

                            var attackChoice = new Choice<bool>("Attack?");
                            attackChoice.AddOption("Strike now!", true);
                            attackChoice.AddOption("Back off", false);

                            if (attackChoice.GetPlayerChoice(ctx))
                            {
                                PerformKill(target, ctx);
                            }
                        }
                    }
                    else if (target.IsEngaged)
                    {
                        // Prey turned aggressive - handle as predator encounter
                        var outcome = EncounterRunner.HandlePredatorEncounter(target, ctx);

                        switch (outcome)
                        {
                            case EncounterOutcome.PredatorRetreated:
                                ctx.player.stealthManager.StopHunting(ctx, $"The {target.Name} retreated.");
                                break;
                            case EncounterOutcome.PlayerEscaped:
                                ctx.player.stealthManager.StopHunting(ctx, "You escaped.");
                                break;
                            case EncounterOutcome.CombatVictory:
                                ctx.player.stealthManager.StopHunting(ctx, $"You killed the {target.Name}.");
                                break;
                            case EncounterOutcome.PlayerDied:
                                // No cleanup needed
                                break;
                        }
                        break; // Exit hunt loop after encounter
                    }
                    break;

                case "assess":
                    ctx.player.stealthManager.AssessTarget(ctx);
                    minutesSpent += 2;
                    break;

                case "wait":
                    int waitTime = Utils.RandInt(5, 10);
                    GameDisplay.AddNarrative(ctx, $"You wait and watch for {waitTime} minutes...");
                    minutesSpent += waitTime;

                    if (target.CheckActivityChange(waitTime, out var newActivity) && newActivity.HasValue)
                    {
                        GameDisplay.AddNarrative(ctx, $"The {target.Name} shifts—now {target.GetActivityDescription()}.");
                    }
                    else
                    {
                        GameDisplay.AddNarrative(ctx, $"The {target.Name} continues {target.GetActivityDescription()}.");
                    }
                    break;

                case "throw_spear":
                    PerformRangedAttack(target, ctx, isSpear: true);
                    minutesSpent += 2;
                    break;

                case "throw_stone":
                    PerformRangedAttack(target, ctx, isSpear: false);
                    minutesSpent += 2;
                    break;

                case "stop":
                    ctx.player.stealthManager.StopHunting(ctx, "You abandon the hunt.");
                    break;
            }
        }

        return minutesSpent;
    }

    private static void PerformKill(Animal target, GameContext ctx)
    {
        GameDisplay.AddNarrative(ctx, $"You strike! The {target.Name} falls.");

        target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, BodyTarget.Heart));
        ctx.player.stealthManager.StopHunting(ctx);

        var loot = ButcherRunner.ButcherAnimal(target, ctx);

        ctx.player.Skills.GetSkill("Hunting").GainExperience(5);
        GameDisplay.AddNarrative(ctx, $"You butcher the {target.Name} and collect {loot.CurrentWeightKg:F1}kg of meat.");
        InventoryCapacityHelper.CombineAndReport(ctx, loot);
    }

    private static void PerformRangedAttack(Animal target, GameContext ctx, bool isSpear)
    {
        var weapon = ctx.Inventory.Weapon;

        // Calculate hit chance using HuntHandler
        double hitChance = HuntHandler.CalculateThrownAccuracy(ctx, target, isSpear, weapon);

        // Consume stone immediately (it's thrown either way)
        if (!isSpear)
        {
            ctx.Inventory.Pop(Resource.Stone);
        }

        bool hit = Utils.RandDouble(0, 1) < hitChance;

        if (hit)
        {
            // Kill
            string weaponName = isSpear && weapon != null ? weapon.Name : "stone";
            GameDisplay.AddNarrative(ctx, $"Your {weaponName} strikes true! The {target.Name} falls.");
            target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, BodyTarget.Heart));

            // Butcher
            var loot = ButcherRunner.ButcherAnimal(target, ctx);

            GameDisplay.AddNarrative(ctx, $"You butcher the {target.Name} and collect {loot.CurrentWeightKg:F1}kg of meat.");
            InventoryCapacityHelper.CombineAndReport(ctx, loot);

            ctx.player.stealthManager.StopHunting(ctx, "Hunt successful.");
        }
        else
        {
            // Miss - but check for glancing hit (wound)
            double roll = Utils.RandDouble(0, 1);
            bool isGlancingHit = roll < hitChance + (hitChance * 0.3) && isSpear; // Only spears can wound

            string weaponName = isSpear && weapon != null ? weapon.Name : "stone";

            if (isGlancingHit)
            {
                // Glancing hit - animal wounded but escapes
                double woundSeverity = Utils.RandDouble(0.3, 0.8);
                string woundDesc = woundSeverity > 0.6
                    ? "Bright red arterial spray, pumping with each heartbeat"
                    : "Dark blood, muscle wound — the animal barely slowed";

                GameDisplay.AddNarrative(ctx, $"Your {weaponName} grazes the {target.Name}!");
                GameDisplay.AddNarrative(ctx, woundDesc);
                GameDisplay.AddNarrative(ctx, $"The {target.Name} bolts, leaving blood on the snow.");

                // Create WoundedPrey tension - entry point to Blood Trail arc
                var tension = Tensions.ActiveTension.WoundedPrey(
                    woundSeverity,
                    target.Name,
                    ctx.CurrentLocation
                );
                ctx.Tensions.AddTension(tension);

                GameDisplay.AddNarrative(ctx, "You could follow the blood trail...");
            }
            else
            {
                // Clean miss
                GameDisplay.AddNarrative(ctx, $"Your {weaponName} misses! The {target.Name} bolts.");
            }

            if (isSpear)
            {
                // Spear recovery: spend time searching
                GameDisplay.AddNarrative(ctx, "You spend a few minutes searching for your spear...");
                ctx.Update(3, ActivityType.Hunting);
            }

            // Animal flees
            ctx.player.stealthManager.StopHunting(ctx, $"The {target.Name} escaped.");
        }
    }
}
