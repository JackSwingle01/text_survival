using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Desktop;
using text_survival.Desktop.Dto;
using text_survival.UI;

namespace text_survival.Combat;

/// <summary>
/// Result of a player action attempt. ActionTaken=false means invalid action (turn not consumed).
/// </summary>
public record PlayerActionResult(bool ActionTaken, string? Narrative);

/// <summary>
/// Runs fights. Player fights get the turn loop and the UI; fights with no player run headless.
/// Every fight is built by <see cref="CombatScenario.Create"/> and ends in <see cref="CombatAftermath"/>.
/// </summary>
public static class CombatOrchestrator
{
    private const int MOVE_DIST = 3;
    private const int HUNT_START_DISTANCE_M = 34;
    private const int MAX_PACK_MEMBERS = 3;
    private static readonly Random _rng = new();

    #region Entry Points

    /// <summary>
    /// The player stalks prey: player Engaged, prey Unaware, opened at stalking distance.
    /// </summary>
    public static CombatResult RunHunt(GameContext ctx, Animal prey)
    {
        var scenario = CombatScenario.Create(
            PlayerSide(ctx, prey), AnimalSide(ctx, prey), ctx.CurrentLocation, HUNT_START_DISTANCE_M,
            AwarenessState.Engaged, AwarenessState.Unaware, ctx.player);

        GameDisplay.AddNarrative(ctx, $"You begin stalking the {prey.Name.ToLower()}...");

        return RunWithPlayer(ctx, scenario, ActivityType.Hunting, result => result switch
        {
            CombatResult.Victory => "You bring down your prey!",
            CombatResult.Defeat => "You have been killed.",
            CombatResult.Fled => "You retreat from the hunt.",
            CombatResult.AnimalFled => "The prey escapes!",
            _ => "The hunt ends."
        });
    }

    /// <summary>
    /// A predator comes for the player: both sides Engaged, opened at the encounter's distance.
    /// </summary>
    /// <param name="engageChance">The boldness (0-1) that brought the predator here; seeds its morale.</param>
    public static CombatResult RunEncounter(GameContext ctx, Animal predator, int startDistanceM, double engageChance)
    {
        var scenario = CombatScenario.Create(
            PlayerSide(ctx, predator), AnimalSide(ctx, predator), ctx.CurrentLocation, startDistanceM,
            AwarenessState.Engaged, AwarenessState.Engaged, ctx.player);

        foreach (var unit in scenario.Team2)
            unit.BoldnessModifier += engageChance - 0.5;

        GameDisplay.AddWarning(ctx, $"A {predator.Name.ToLower()} attacks!");

        return RunWithPlayer(ctx, scenario, ActivityType.Fighting, result => result switch
        {
            CombatResult.Victory => "You are victorious!",
            CombatResult.Defeat => "You have been killed.",
            CombatResult.Fled => "You escape!",
            CombatResult.AnimalFled => "Your enemies flee!",
            _ => "The encounter ends."
        });
    }

    /// <summary>
    /// A fight with no player in it: NPCs defending themselves, a pack pulling down prey.
    /// Same grid, same AI, same aftermath; nobody watches.
    /// </summary>
    public static CombatResult ResolveHeadless(
        GameContext ctx,
        IReadOnlyList<Actor> teamA,
        IReadOnlyList<Actor> teamB,
        Location where,
        int startDistanceM,
        AwarenessState teamAAwareness,
        AwarenessState teamBAwareness)
    {
        var scenario = CombatScenario.Create(teamA, teamB, where, startDistanceM, teamAAwareness, teamBAwareness);
        var result = scenario.ResolveHeadless();
        CombatAftermath.Apply(ctx, scenario, result, where);
        return result;
    }

    /// <summary>The player and any NPC here who decides to help against this enemy.</summary>
    private static List<Actor> PlayerSide(GameContext ctx, Animal enemy)
    {
        var side = new List<Actor> { ctx.player };
        var npcsHere = ctx.GetNPCsAt(ctx.Map?.CurrentPosition ?? new GridPosition(0, 0));
        side.AddRange(npcsHere.Where(npc => npc.DecideToHelpInCombat(ctx.player, enemy)));
        return side;
    }

    /// <summary>The animal plus a random handful of its living herd mates.</summary>
    public static List<Actor> AnimalSide(GameContext ctx, Animal lead, int maxExtra = MAX_PACK_MEMBERS)
    {
        var side = new List<Actor> { lead };
        var herd = ctx.Herds.ContainingAnimal(lead);
        if (herd != null)
        {
            int maxPack = Math.Min(herd.Members.Count - 1, maxExtra);
            int packSize = maxPack > 0 ? _rng.Next(0, maxPack + 1) : 0;
            side.AddRange(herd.Members
                .Where(a => a != lead && a.IsAlive)
                .OrderBy(_ => _rng.Next())
                .Take(packSize));
        }
        return side;
    }

    #endregion

    #region Player Loop

    private static CombatResult RunWithPlayer(GameContext ctx, CombatScenario scenario, ActivityType activity, Func<CombatResult, string> describe)
    {
        var playerUnit = scenario.Player!;
        ctx.ActiveCombat = scenario;
        int huntingSkill = ctx.player.Skills.GetSkill("Hunting")?.Level ?? 0;

        while (!scenario.IsOver && scenario.Units.Contains(playerUnit) && !Raylib_cs.Raylib.WindowShouldClose())
        {
            RunCombatTurn(ctx, scenario, playerUnit, huntingSkill, activity);
        }

        ctx.ActiveCombat = null;

        var result = scenario.DetermineResult();
        GameDisplay.AddSuccess(ctx, describe(result));
        CombatAftermath.Apply(ctx, scenario, result, ctx.CurrentLocation);
        return result;
    }

    /// <summary>
    /// Processes one player turn + AI responses.
    /// </summary>
    private static void RunCombatTurn(
        GameContext ctx,
        CombatScenario scenario,
        Unit playerUnit,
        int huntingSkill,
        ActivityType activityType)
    {
        var input = DesktopIO.WaitForCombatAction(ctx);
        if (input == null) return; // Window closed - the outer loop stops.

        PlayerActionResult actionResult;
        if (input.MoveTarget != null)
            actionResult = ExecuteMoveTo(scenario, playerUnit, input.MoveTarget.Value);
        else if (input.Action != null)
            actionResult = ExecutePlayerChoice(scenario, playerUnit, input.Action.Value, ctx);
        else
            throw new InvalidOperationException("Combat input carried neither an action nor a move target.");

        if (!string.IsNullOrEmpty(actionResult.Narrative))
            GameDisplay.AddNarrative(ctx, actionResult.Narrative);

        if (!actionResult.ActionTaken) return;

        ctx.Update(1, activityType);
        ProcessDetectionChanges(ctx, scenario, playerUnit, huntingSkill);

        if (!scenario.IsOver)
        {
            scenario.ResetAITurns(playerUnit);
            DesktopIO.RunAITurnsWithAnimation(ctx, scenario, playerUnit);
        }
    }

    /// <summary>
    /// Detection check + message display (shared logic).
    /// </summary>
    private static void ProcessDetectionChanges(
        GameContext ctx,
        CombatScenario scenario,
        Unit playerUnit,
        int huntingSkill)
    {
        var awarenessChanges = scenario.RunDetectionChecks(playerUnit, huntingSkill);
        foreach (var (unit, oldState, newState) in awarenessChanges)
        {
            string? detectionMsg = newState switch
            {
                AwarenessState.Alert when oldState == AwarenessState.Unaware =>
                    $"The {unit.actor.Name.ToLower()} becomes alert - it senses something!",
                AwarenessState.Engaged =>
                    $"The {unit.actor.Name.ToLower()} spots you!",
                _ => null
            };
            if (detectionMsg != null)
                GameDisplay.AddWarning(ctx, detectionMsg);
        }
    }

    #endregion

    #region Player Actions

    private static PlayerActionResult ExecutePlayerChoice(CombatScenario scenario, Unit playerUnit, CombatActions action, GameContext ctx)
    {
        var nearest = scenario.GetNearestEnemy(playerUnit);
        if (nearest == null) return new PlayerActionResult(false, null);

        return action switch
        {
            CombatActions.Advance => ExecuteAdvance(scenario, playerUnit, nearest),
            CombatActions.Retreat => ExecuteRetreat(scenario, playerUnit, nearest),
            CombatActions.Attack => ExecuteAttack(scenario, playerUnit, nearest),
            CombatActions.Throw => ExecuteThrow(scenario, playerUnit, nearest),
            CombatActions.ThrowStone => ExecuteThrowStone(scenario, playerUnit, nearest, ctx),
            CombatActions.Dodge => ExecuteDodge(scenario, playerUnit),
            CombatActions.Block => ExecuteBlock(scenario, playerUnit),
            CombatActions.Shove => ExecuteShove(scenario, playerUnit, nearest),
            CombatActions.Intimidate => ExecuteIntimidate(scenario, playerUnit),
            CombatActions.Flee => ExecuteFlee(scenario, playerUnit),
            CombatActions.Assess => ExecuteAssess(scenario, playerUnit, nearest, ctx),
            CombatActions.Wait => ExecuteWait(scenario, playerUnit, ctx),
            _ => new PlayerActionResult(false, null)
        };
    }

    private static PlayerActionResult ExecuteMoveTo(CombatScenario scenario, Unit playerUnit, GridPosition dest)
    {
        // Calculate distance to destination
        double distance = Math.Sqrt(
            Math.Pow(dest.X - playerUnit.Position.X, 2) +
            Math.Pow(dest.Y - playerUnit.Position.Y, 2));

        // Validate: within movement range (max 3m - same as MOVE_DIST)
        if (distance > MOVE_DIST)
            return new PlayerActionResult(false, "That's too far to move in one action.");

        // Can't move to current position
        if (distance == 0)
            return new PlayerActionResult(false, null);

        // Valid move - execute it (Move() handles collision resolution)
        scenario.Move(playerUnit, dest);
        return new PlayerActionResult(true, "You move.");
    }

    private static PlayerActionResult ExecuteAdvance(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        var dest = playerUnit.Position.MoveToward(nearest.Position, MOVE_DIST);
        scenario.Move(playerUnit, dest);
        return new PlayerActionResult(true, "You advance.");
    }

    private static PlayerActionResult ExecuteRetreat(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        var dest = playerUnit.Position.MoveAway(nearest.Position, MOVE_DIST);
        scenario.Move(playerUnit, dest);
        return new PlayerActionResult(true, "You back away.");
    }

    private static PlayerActionResult ExecuteAttack(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        var result = scenario.ExecuteAction(CombatActions.Attack, playerUnit, nearest);
        var narrative = result != null
            ? CombatNarrator.DescribeAttack(playerUnit.actor, nearest.actor, result)
            : $"You attack the {nearest.actor.Name}!";
        return new PlayerActionResult(true, narrative);
    }

    private static PlayerActionResult ExecuteThrow(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        var result = scenario.ExecuteAction(CombatActions.Throw, playerUnit, nearest);
        var narrative = result != null
            ? CombatNarrator.DescribeAttack(playerUnit.actor, nearest.actor, result)
            : $"You throw your weapon at the {nearest.actor.Name}!";
        return new PlayerActionResult(true, narrative);
    }

    private static PlayerActionResult ExecuteThrowStone(CombatScenario scenario, Unit playerUnit, Unit nearest, GameContext ctx)
    {
        // Check if player has stones
        if (ctx.Inventory.Count(Resource.Stone) <= 0)
        {
            return new PlayerActionResult(false, null);
        }

        // Consume the stone
        ctx.Inventory.Pop(Resource.Stone);

        var result = scenario.ExecuteAction(CombatActions.ThrowStone, playerUnit, nearest);
        var narrative = result != null && result.Hit
            ? $"Your stone strikes the {nearest.actor.Name.ToLower()}!"
            : $"Your stone misses the {nearest.actor.Name.ToLower()}.";
        return new PlayerActionResult(true, narrative);
    }

    private static PlayerActionResult ExecuteDodge(CombatScenario scenario, Unit playerUnit)
    {
        scenario.ExecuteAction(CombatActions.Dodge, playerUnit, null);
        return new PlayerActionResult(true, "You ready to dodge.");
    }

    private static PlayerActionResult ExecuteBlock(CombatScenario scenario, Unit playerUnit)
    {
        scenario.ExecuteAction(CombatActions.Block, playerUnit, null);
        return new PlayerActionResult(true, "You raise your guard.");
    }

    private static PlayerActionResult ExecuteShove(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        var (success, dodged) = scenario.Shove(playerUnit, nearest);
        var narrative = CombatNarrator.DescribeShove(playerUnit.actor, nearest.actor, success, dodged);
        return new PlayerActionResult(true, narrative);
    }

    private static PlayerActionResult ExecuteIntimidate(CombatScenario scenario, Unit playerUnit)
    {
        scenario.ExecuteAction(CombatActions.Intimidate, playerUnit, null);
        return new PlayerActionResult(true, CombatNarrator.DescribeIntimidate(playerUnit.actor, isPlayer: true));
    }

    private static PlayerActionResult ExecuteFlee(CombatScenario scenario, Unit playerUnit)
    {
        if (!CombatScenario.CanFlee(playerUnit.Position))
            return new PlayerActionResult(false, null);

        scenario.ExecuteFlee(playerUnit);
        return new PlayerActionResult(true, "You sprint for the edge!");
    }

    private static PlayerActionResult ExecuteAssess(CombatScenario scenario, Unit playerUnit, Unit target, GameContext ctx)
    {
        int huntingSkill = ctx.player.Skills.GetSkill("Hunting")?.Level ?? 0;
        double detection = scenario.CalculateDetectionRisk(playerUnit, target, huntingSkill);
        double distance = playerUnit.Position.DistanceTo(target.Position);

        string awareness = target.Awareness == AwarenessState.Unaware ? "unaware" : "alert";
        string narrative = $"The {target.actor.Name.ToLower()} is {awareness}, {distance:F0}m away. Detection risk: {detection:P0}";
        return new PlayerActionResult(true, narrative);
    }

    private static PlayerActionResult ExecuteWait(CombatScenario scenario, Unit playerUnit, GameContext ctx)
    {
        // Wait 5-10 min, animal may change activity
        int waitTimeMinutes = Utils.RandInt(5, 10);
        ctx.Update(waitTimeMinutes, ActivityType.Hunting);

        // Check each unaware/alert enemy for activity change
        var messages = new List<string>();
        foreach (var enemy in playerUnit.enemies.Where(e => e.Awareness != AwarenessState.Engaged))
        {
            if (enemy.actor is Animal animal && animal.CheckActivityChange(waitTimeMinutes, out var newActivity) && newActivity.HasValue)
            {
                messages.Add($"The {animal.Name.ToLower()} shifts—now {animal.GetActivityDescription()}.");
            }
        }

        string narrative = messages.Count > 0
            ? string.Join(" ", messages)
            : "You wait and watch.";
        return new PlayerActionResult(true, narrative);
    }

    #endregion
}
