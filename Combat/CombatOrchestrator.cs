using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Desktop;
using text_survival.UI;

namespace text_survival.Combat;

/// <summary>
/// Result of a player action attempt. ActionTaken=false means invalid action (turn not consumed).
/// </summary>
public record PlayerActionResult(bool ActionTaken, string? Narrative);

/// <summary>
/// Configuration for stealth-based combat (hunts and predator encounters).
/// </summary>
public record StealthCombatConfig(
    AwarenessState PlayerAwareness,    // Engaged (hunt) or Unaware (encounter)
    AwarenessState TargetAwareness,    // Unaware (hunt) or Alert/Engaged (encounter)
    ActivityType ActivityType,          // Hunting, Encounter, Fighting
    string? IntroMessage,               // "You begin stalking..." or null for encounters
    bool EnableBackgroundPhase          // True for encounters (predator stalks before player aware)
);

/// <summary>
/// Orchestrates combat encounters with turn loop and IO.
/// CombatScenario handles state/rules, this handles the player-facing loop.
/// </summary>
public static class CombatOrchestrator
{
    private const int MOVE_DIST = 3;
    private static readonly Random _rng = new();

    #region Stealth Combat Configs

    /// <summary>Hunt config: player is engaged, prey starts unaware.</summary>
    public static StealthCombatConfig HuntConfig => new(
        PlayerAwareness: AwarenessState.Engaged,
        TargetAwareness: AwarenessState.Unaware,
        ActivityType: ActivityType.Hunting,
        IntroMessage: null,  // Handled by pre-approach phase in HuntRunner
        EnableBackgroundPhase: false
    );

    /// <summary>Encounter config: player starts unaware, predator is alert or engaged.</summary>
    public static StealthCombatConfig EncounterConfig(bool predatorIsAlert) => new(
        PlayerAwareness: AwarenessState.Unaware,
        TargetAwareness: predatorIsAlert ? AwarenessState.Alert : AwarenessState.Engaged,
        ActivityType: ActivityType.Encounter,
        IntroMessage: null,
        EnableBackgroundPhase: true
    );

    #endregion

    /// <summary>
    /// Main entry point for player combat.
    /// </summary>
    public static CombatResult RunCombat(GameContext ctx, Animal enemy)
    {
        // === SETUP ===
        var (scenario, playerUnit) = SetupCombat(ctx, enemy);
        scenario.Location = ctx.CurrentLocation; // Set location for detection modifiers
        ctx.ActiveCombat = scenario;  // Switch to combat mode

        // Show intro message
        GameDisplay.AddWarning(ctx, $"A {enemy.Name.ToLower()} attacks!");

        // Get hunting skill for detection checks
        int huntingSkill = ctx.player.Skills.GetSkill("Hunting")?.Level ?? 0;

        // === MAIN LOOP ===
        while (!scenario.IsOver && !Raylib_cs.Raylib.WindowShouldClose())
        {
            // Render and wait for player input
            var response = DesktopIO.RenderGridAndWaitForInput(ctx);

            if (response.Type == "action" && response.Action != null && response.Action.StartsWith("combat:"))
            {
                // Extract action (remove "combat:" prefix)
                string action = response.Action.Substring(7);

                // Execute player action
                var actionResult = ExecutePlayerChoice(scenario, playerUnit, action, ctx);

                // Handle invalid action with feedback
                if (!actionResult.ActionTaken)
                {
                    if (!string.IsNullOrEmpty(actionResult.Narrative))
                    {
                        BlockingDialog.ShowMessageAndWait(ctx, "Invalid Action", actionResult.Narrative);
                    }
                    continue;  // Go back to waiting for input
                }

                // Show result narrative for successful actions
                if (!string.IsNullOrEmpty(actionResult.Narrative))
                {
                    GameDisplay.AddNarrative(ctx, actionResult.Narrative);
                }

                // Action was valid - advance to AI turns
                // Time cost: 1 minute per action (survival pressure during combat)
                ctx.Update(1, ActivityType.Fighting);

                // Run detection checks (only affects enemies that are Unaware/Alert)
                var awarenessChanges = scenario.RunDetectionChecks(playerUnit, huntingSkill);
                foreach (var (unit, oldState, newState) in awarenessChanges)
                {
                    string detectionMsg = newState switch
                    {
                        AwarenessState.Alert when oldState == AwarenessState.Unaware =>
                            $"The {unit.actor.Name.ToLower()} becomes alert - it senses something!",
                        AwarenessState.Engaged =>
                            $"The {unit.actor.Name.ToLower()} spots you!",
                        _ => null
                    };
                    if (detectionMsg != null)
                    {
                        GameDisplay.AddWarning(ctx, detectionMsg);
                    }
                }

                if (scenario.IsOver) break;

                // AI turns - executed one at a time with rendering between
                scenario.ResetAITurns(playerUnit);
                DesktopIO.RunAITurnsWithAnimation(ctx, scenario, playerUnit);
            }
        }

        // === CLEANUP ===
        ctx.ActiveCombat = null;  // Exit combat mode

        var result = DetermineResult(scenario, playerUnit);

        // Show result message
        string resultMessage = result switch
        {
            CombatResult.Victory => "You are victorious!",
            CombatResult.Defeat => "You have been killed.",
            CombatResult.Fled => "You escape!",
            CombatResult.AnimalFled => "Your enemies flee!",
            _ => "The encounter ends."
        };
        GameDisplay.AddSuccess(ctx, resultMessage);

        HandlePostCombat(ctx, scenario, result);
        return result;
    }

    #region Setup

    private static (CombatScenario scenario, Unit playerUnit) SetupCombat(GameContext ctx, Animal enemy)
    {
        // Player team
        var playerUnit = new Unit(ctx.player, StartPosition(true, 0));
        var team1 = new List<Unit> { playerUnit };

        // Find allied NPCs who will help
        var npcsHere = ctx.GetNPCsAt(ctx.Map?.CurrentPosition ?? new GridPosition(0, 0));
        var allies = npcsHere.Where(npc => npc.DecideToHelpInCombat(ctx.player, enemy)).ToList();
        foreach (var npc in allies)
        {
            team1.Add(new Unit(npc, StartPosition(true, team1.Count)));
        }

        // Enemy team
        var enemyUnit = new Unit(enemy, StartPosition(false, 0));
        var team2 = new List<Unit> { enemyUnit };

        // Random pack members from herd (0-3 extra)
        var herd = ctx.Herds.GetHerdContaining(enemy);
        if (herd != null)
        {
            int maxPack = Math.Min(herd.Members.Count - 1, 3);
            int packSize = maxPack > 0 ? _rng.Next(0, maxPack + 1) : 0;
            var packMembers = herd.Members
                .Where(a => a != enemy && a.IsAlive)
                .OrderBy(_ => _rng.Next())
                .Take(packSize);
            foreach (var animal in packMembers)
            {
                team2.Add(new Unit(animal, StartPosition(false, team2.Count)));
            }
        }

        return (new CombatScenario(team1, team2, playerUnit), playerUnit);
    }

    private static GridPosition StartPosition(bool isPlayerTeam, int index, int gridSize = CombatScenario.MAP_SIZE)
    {
        // Player team: bottom quarter, clustered 1-2m apart
        // Enemy team: top quarter, clustered 1-2m apart
        int centerX = gridSize / 2;
        int playerBaseY = gridSize / 6;        // ~8 for 50x50
        int enemyBaseY = gridSize - gridSize / 6; // ~42 for 50x50
        int baseY = isPlayerTeam ? playerBaseY : enemyBaseY;
        int xOffset = (index % 3) * 2;  // 0, 2, 4
        int yOffset = index / 3;         // stack rows if > 3
        return new GridPosition(centerX + xOffset, baseY + yOffset);
    }

    /// <summary>
    /// Sets awareness state for an entire team.
    /// </summary>
    public static void SetTeamAwareness(IEnumerable<Unit> team, AwarenessState awareness)
    {
        foreach (var unit in team)
        {
            unit.Awareness = awareness;
        }
    }

    /// <summary>
    /// Configure combat for a hunt scenario: player engaged, enemies unaware.
    /// Enemies start far away (~40m from player).
    /// </summary>
    public static void SetupHunt(CombatScenario scenario)
    {
        SetTeamAwareness(scenario.Team1, AwarenessState.Engaged);
        SetTeamAwareness(scenario.Team2, AwarenessState.Unaware);
    }

    /// <summary>
    /// Configure combat for a predator encounter: player unaware, enemies alert/engaged.
    /// </summary>
    public static void SetupPredatorEncounter(CombatScenario scenario, bool predatorIsAlert = false)
    {
        SetTeamAwareness(scenario.Team1, AwarenessState.Unaware);
        SetTeamAwareness(scenario.Team2, predatorIsAlert ? AwarenessState.Alert : AwarenessState.Engaged);
    }

    /// <summary>
    /// Configure combat for mutual encounter: both sides engaged (current default behavior).
    /// </summary>
    public static void SetupMutualEncounter(CombatScenario scenario)
    {
        SetTeamAwareness(scenario.Team1, AwarenessState.Engaged);
        SetTeamAwareness(scenario.Team2, AwarenessState.Engaged);
    }

    /// <summary>
    /// Run combat as a hunt scenario: player approaches unaware prey.
    /// </summary>
    public static CombatResult RunHunt(GameContext ctx, Animal prey)
    {
        var (scenario, playerUnit) = SetupHuntCombat(ctx, prey);
        scenario.Location = ctx.CurrentLocation;
        ctx.ActiveCombat = scenario;

        GameDisplay.AddNarrative(ctx, $"You begin stalking the {prey.Name.ToLower()}...");

        var result = RunStealthCombat(ctx, scenario, playerUnit, HuntConfig);

        ctx.ActiveCombat = null;

        string resultMessage = result switch
        {
            CombatResult.Victory => "You bring down your prey!",
            CombatResult.Defeat => "You have been killed.",
            CombatResult.Fled => "You retreat from the hunt.",
            CombatResult.AnimalFled => "The prey escapes!",
            _ => "The hunt ends."
        };
        GameDisplay.AddSuccess(ctx, resultMessage);

        HandlePostCombat(ctx, scenario, result);
        return result;
    }

    /// <summary>
    /// Setup combat scenario for hunting - enemies start far away (~40m).
    /// </summary>
    private static (CombatScenario scenario, Unit playerUnit) SetupHuntCombat(GameContext ctx, Animal prey)
    {
        // Derive positions from grid size for consistency
        int playerY = MAP_SIZE / 6;                 // ~8 for 50x50
        int preyY = MAP_SIZE - MAP_SIZE / 6;        // ~42 for 50x50

        // Player starts at bottom center
        var playerUnit = new Unit(ctx.player, new GridPosition(MAP_SIZE / 2, playerY));
        var team1 = new List<Unit> { playerUnit };

        // Prey starts far away at top (~40m away)
        var preyUnit = new Unit(prey, new GridPosition(MAP_SIZE / 2, preyY));
        var team2 = new List<Unit> { preyUnit };

        // Include any pack members from herd
        var herd = ctx.Herds.GetHerdContaining(prey);
        if (herd != null)
        {
            int maxPack = Math.Min(herd.Members.Count - 1, 2);
            int packSize = maxPack > 0 ? _rng.Next(0, maxPack + 1) : 0;
            var packMembers = herd.Members
                .Where(a => a != prey && a.IsAlive)
                .OrderBy(_ => _rng.Next())
                .Take(packSize);
            int index = 1;
            foreach (var animal in packMembers)
            {
                int xOffset = (index % 3) * 2 - 2;
                team2.Add(new Unit(animal, new GridPosition(MAP_SIZE / 2 + xOffset, preyY + index / 3)));
                index++;
            }
        }

        return (new CombatScenario(team1, team2, playerUnit, ctx.CurrentLocation), playerUnit);
    }

    private const int MAP_SIZE = CombatScenario.MAP_SIZE;

    /// <summary>
    /// Run combat as a predator encounter: player starts Unaware, predator approaches.
    /// Combat overlay only appears when player detects predator or is attacked.
    /// </summary>
    public static CombatResult RunPredatorEncounter(GameContext ctx, Animal predator, bool predatorIsAlert = false)
    {
        var (scenario, playerUnit) = SetupEncounterCombat(ctx, predator);
        scenario.Location = ctx.CurrentLocation;
        ctx.ActiveCombat = scenario;

        var result = RunStealthCombat(ctx, scenario, playerUnit, EncounterConfig(predatorIsAlert));

        ctx.ActiveCombat = null;

        string resultMessage = result switch
        {
            CombatResult.Victory => "You kill the predator!",
            CombatResult.Defeat => "You have been killed.",
            CombatResult.Fled => "You escape!",
            CombatResult.AnimalFled => "The predator flees!",
            _ => "The encounter ends."
        };
        GameDisplay.AddSuccess(ctx, resultMessage);

        HandlePostCombat(ctx, scenario, result);
        return result;
    }

    /// <summary>
    /// Setup combat scenario for predator encounter - predator starts closer than hunt.
    /// </summary>
    private static (CombatScenario scenario, Unit playerUnit) SetupEncounterCombat(GameContext ctx, Animal predator)
    {
        // Derive positions from grid size - closer than hunt (~20-25m vs ~40m)
        int playerY = MAP_SIZE / 4;                 // ~12 for 50x50
        int predatorY = MAP_SIZE / 2 + MAP_SIZE / 5; // ~35 for 50x50 (~23m away)

        // Player at center-bottom
        var playerUnit = new Unit(ctx.player, new GridPosition(MAP_SIZE / 2, playerY));
        var team1 = new List<Unit> { playerUnit };

        // Find allied NPCs who will help
        var npcsHere = ctx.GetNPCsAt(ctx.Map?.CurrentPosition ?? new GridPosition(0, 0));
        var allies = npcsHere.Where(npc => npc.DecideToHelpInCombat(ctx.player, predator)).ToList();
        foreach (var npc in allies)
        {
            team1.Add(new Unit(npc, new GridPosition(MAP_SIZE / 2 + team1.Count * 2, playerY)));
        }

        // Predator starts at medium distance (~20-25m away)
        var predatorUnit = new Unit(predator, new GridPosition(MAP_SIZE / 2, predatorY));
        var team2 = new List<Unit> { predatorUnit };

        // Add pack members if applicable
        var herd = ctx.Herds.GetHerdContaining(predator);
        if (herd != null)
        {
            int maxPack = Math.Min(herd.Members.Count - 1, 2);
            int packSize = maxPack > 0 ? _rng.Next(0, maxPack + 1) : 0;
            var packMembers = herd.Members
                .Where(a => a != predator && a.IsAlive)
                .OrderBy(_ => _rng.Next())
                .Take(packSize);
            int index = 1;
            foreach (var animal in packMembers)
            {
                int xOffset = (index % 3) * 2 - 2;
                team2.Add(new Unit(animal, new GridPosition(MAP_SIZE / 2 + xOffset, predatorY + index / 3)));
                index++;
            }
        }

        return (new CombatScenario(team1, team2, playerUnit, ctx.CurrentLocation), playerUnit);
    }

    #endregion

    #region Unified Stealth Combat

    /// <summary>
    /// Unified stealth combat loop for both hunts and predator encounters.
    /// Eliminates duplication between RunHunt and RunPredatorEncounter.
    /// </summary>
    private static CombatResult RunStealthCombat(
        GameContext ctx,
        CombatScenario scenario,
        Unit playerUnit,
        StealthCombatConfig config)
    {
        // 1. Apply awareness states
        SetTeamAwareness(scenario.Team1, config.PlayerAwareness);
        SetTeamAwareness(scenario.Team2, config.TargetAwareness);

        // 2. Show intro message if provided
        if (config.IntroMessage != null)
            GameDisplay.AddNarrative(ctx, config.IntroMessage);

        // 3. Get hunting skill once
        int huntingSkill = ctx.player.Skills.GetSkill("Hunting")?.Level ?? 0;

        // 4. Main loop
        while (!scenario.IsOver && !Raylib_cs.Raylib.WindowShouldClose())
        {
            // 4a. Background phase for encounters (player unaware)
            if (config.EnableBackgroundPhase && playerUnit.Awareness == AwarenessState.Unaware)
            {
                RunBackgroundPhase(ctx, scenario, playerUnit, huntingSkill);
                continue;
            }

            // 4b. Normal combat turn
            RunCombatTurn(ctx, scenario, playerUnit, huntingSkill, config.ActivityType);
        }

        return DetermineResult(scenario, playerUnit);
    }

    /// <summary>
    /// Processes background phase when player is unaware (predator stalking).
    /// </summary>
    private static void RunBackgroundPhase(
        GameContext ctx,
        CombatScenario scenario,
        Unit playerUnit,
        int huntingSkill)
    {
        // Run predator's turn (they're stalking/approaching)
        scenario.ResetAITurns(playerUnit);
        while (scenario.HasRemainingAITurns(playerUnit))
        {
            scenario.RunNextAITurn(playerUnit);
            if (scenario.IsOver) break;

            // Check if player detected the predator (reverse detection check)
            var nearestEnemy = scenario.GetNearestEnemy(playerUnit);
            if (nearestEnemy != null)
            {
                double distance = playerUnit.Position.DistanceTo(nearestEnemy.Position);
                double detectionChance = HuntingCalculator.CalculateDetectionChance(
                    distance,
                    AwarenessState.Alert, // Player is alert to danger in general
                    huntingSkill,
                    0
                );

                // Location visibility helps player spot predator
                if (scenario.Location != null)
                {
                    double visibilityNormalized = scenario.Location.VisibilityFactor / 2.0;
                    detectionChance *= (1.0 + visibilityNormalized * 0.3);
                }

                if (Utils.DetermineSuccess(detectionChance))
                {
                    playerUnit.Awareness = AwarenessState.Engaged;
                    GameDisplay.AddWarning(ctx, $"You spot a {nearestEnemy.actor.Name.ToLower()} stalking you!");
                }
            }
        }

        // Time passes during stalking
        ctx.Update(1, ActivityType.Encounter);
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
        var response = DesktopIO.RenderGridAndWaitForInput(ctx);

        if (response.Type != "action" || response.Action == null || !response.Action.StartsWith("combat:"))
            return;

        string action = response.Action.Substring(7);
        var actionResult = ExecutePlayerChoice(scenario, playerUnit, action, ctx);

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

    private static PlayerActionResult ExecutePlayerChoice(CombatScenario scenario, Unit playerUnit, string choice, GameContext ctx)
    {
        // Handle click-to-move (move_to_X_Y format)
        if (choice.StartsWith("move_to_"))
        {
            var parts = choice.Split('_');
            if (parts.Length == 4 && int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int y))
            {
                return ExecuteMoveTo(scenario, playerUnit, new GridPosition(x, y));
            }
            return new PlayerActionResult(false, null);
        }

        var nearest = scenario.GetNearestEnemy(playerUnit);
        if (nearest == null) return new PlayerActionResult(false, null);

        if (!Enum.TryParse<CombatActions>(choice, true, out var action))
            return new PlayerActionResult(false, null);

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

    #region Result & Cleanup

    private static CombatResult DetermineResult(CombatScenario scenario, Unit player)
    {
        if (!player.actor.IsAlive) return CombatResult.Defeat;
        if (scenario.Team2.All(u => !u.actor.IsAlive)) return CombatResult.Victory;
        if (!scenario.Units.Contains(player)) return CombatResult.Fled;
        return CombatResult.AnimalFled;  // enemies fled
    }

    private static void HandlePostCombat(GameContext ctx, CombatScenario scenario, CombatResult result)
    {
        // Create carcasses for dead enemies
        if (result == CombatResult.Victory)
        {
            foreach (var unit in scenario.Team2)
            {
                if (!unit.actor.IsAlive && unit.actor is Animal animal)
                {
                    var carcass = new CarcassFeature(animal);
                    ctx.CurrentLocation.AddFeature(carcass);
                }
            }
        }

        // Dead ally NPCs become bodies
        foreach (var unit in scenario.Team1)
        {
            if (!unit.actor.IsAlive && unit.actor is NPC npc)
            {
                var cause = NPCBodyFeature.DetermineDeathCause(npc);
                var body = new NPCBodyFeature(npc.Name, cause, ctx.GameTime, npc.Inventory ?? new Inventory());
                ctx.CurrentLocation.AddFeature(body);
                ctx.NPCs.Remove(npc);
            }
        }

        // Record that team members fought together (relationship memory)
        if (result == CombatResult.Victory || result == CombatResult.AnimalFled)
        {
            var team = scenario.Team1.Select(u => u.actor);
            RelationshipEvents.FoughtTogether(team);
        }
    }

    #endregion
}
