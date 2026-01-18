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
/// Orchestrates combat encounters with turn loop and IO.
/// CombatScenario handles state/rules, this handles the player-facing loop.
/// </summary>
public static class CombatOrchestrator
{
    private const int MOVE_DIST = 3;
    private static readonly Random _rng = new();

    /// <summary>
    /// Main entry point for player combat.
    /// </summary>
    public static CombatResult RunCombat(GameContext ctx, Animal enemy)
    {
        // === SETUP ===
        var (scenario, playerUnit) = SetupCombat(ctx, enemy);
        ctx.ActiveCombat = scenario;  // Switch to combat mode

        // Show intro message
        GameDisplay.AddWarning(ctx, $"A {enemy.Name.ToLower()} attacks!");

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
                var narrative = ExecutePlayerChoice(scenario, playerUnit, action);

                // Show result
                if (!string.IsNullOrEmpty(narrative))
                {
                    GameDisplay.AddNarrative(ctx, narrative);
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

    private static GridPosition StartPosition(bool isPlayerTeam, int index)
    {
        // Player team: bottom quarter (y ≈ 4), clustered 1-2m apart
        // Enemy team: top quarter (y ≈ 21), clustered 1-2m apart
        int baseY = isPlayerTeam ? 4 : 21;
        int xOffset = (index % 3) * 2;  // 0, 2, 4
        int yOffset = index / 3;         // stack rows if > 3
        return new GridPosition(12 + xOffset, baseY + yOffset);
    }

    #endregion

    #region Player Actions

    private static string ExecutePlayerChoice(CombatScenario scenario, Unit playerUnit, string choice)
    {
        // Handle click-to-move (move_to_X_Y format)
        if (choice.StartsWith("move_to_"))
        {
            var parts = choice.Split('_');
            if (parts.Length == 4 && int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int y))
            {
                return ExecuteMoveTo(scenario, playerUnit, new GridPosition(x, y));
            }
            return "";
        }

        var nearest = scenario.GetNearestEnemy(playerUnit);
        if (nearest == null) return "";

        if (!Enum.TryParse<CombatActions>(choice, true, out var action))
            return "";

        return action switch
        {
            CombatActions.Advance => ExecuteAdvance(scenario, playerUnit, nearest),
            CombatActions.Retreat => ExecuteRetreat(scenario, playerUnit, nearest),
            CombatActions.Attack => ExecuteAttack(scenario, playerUnit, nearest),
            CombatActions.Throw => ExecuteThrow(scenario, playerUnit, nearest),
            CombatActions.Dodge => ExecuteDodge(scenario, playerUnit),
            CombatActions.Block => ExecuteBlock(scenario, playerUnit),
            CombatActions.Shove => ExecuteShove(scenario, playerUnit, nearest),
            CombatActions.Intimidate => ExecuteIntimidate(scenario, playerUnit),
            CombatActions.Flee => ExecuteFlee(scenario, playerUnit),
            _ => ""
        };
    }

    private static string ExecuteMoveTo(CombatScenario scenario, Unit playerUnit, GridPosition dest)
    {
        // Calculate distance to destination
        double distance = Math.Sqrt(
            Math.Pow(dest.X - playerUnit.Position.X, 2) +
            Math.Pow(dest.Y - playerUnit.Position.Y, 2));

        // Validate: within movement range (max 3m - same as MOVE_DIST)
        if (distance > MOVE_DIST)
        {
            return "Too far to move there.";
        }

        // Can't move to current position
        if (distance == 0)
        {
            return "";
        }

        // Check if destination is occupied
        if (scenario.Units.Any(u => u.actor.IsAlive && u.Position.X == dest.X && u.Position.Y == dest.Y))
        {
            return "That space is occupied.";
        }

        // Valid move - execute it
        scenario.Move(playerUnit, dest);
        return "You move.";
    }

    private static string ExecuteAdvance(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        var dest = playerUnit.Position.MoveToward(nearest.Position, MOVE_DIST);
        scenario.Move(playerUnit, dest);
        return "You advance.";
    }

    private static string ExecuteRetreat(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        var dest = playerUnit.Position.MoveAway(nearest.Position, MOVE_DIST);
        scenario.Move(playerUnit, dest);
        return "You back away.";
    }

    private static string ExecuteAttack(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        var result = scenario.ExecuteAction(CombatActions.Attack, playerUnit, nearest);
        if (result != null)
        {
            return CombatNarrator.DescribeAttack(playerUnit.actor, nearest.actor, result);
        }
        return $"You attack the {nearest.actor.Name}!";
    }

    private static string ExecuteThrow(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        var result = scenario.ExecuteAction(CombatActions.Throw, playerUnit, nearest);
        if (result != null)
        {
            return CombatNarrator.DescribeAttack(playerUnit.actor, nearest.actor, result);
        }
        return $"You throw your weapon at the {nearest.actor.Name}!";
    }

    private static string ExecuteDodge(CombatScenario scenario, Unit playerUnit)
    {
        scenario.ExecuteAction(CombatActions.Dodge, playerUnit, null);
        return "You ready to dodge.";
    }

    private static string ExecuteBlock(CombatScenario scenario, Unit playerUnit)
    {
        scenario.ExecuteAction(CombatActions.Block, playerUnit, null);
        return "You raise your guard.";
    }

    private static string ExecuteShove(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        scenario.ExecuteAction(CombatActions.Shove, playerUnit, nearest);
        return $"You shove the {nearest.actor.Name}!";
    }

    private static string ExecuteIntimidate(CombatScenario scenario, Unit playerUnit)
    {
        scenario.ExecuteAction(CombatActions.Intimidate, playerUnit, null);
        return CombatNarrator.DescribeIntimidate(playerUnit.actor, isPlayer: true);
    }

    private static string ExecuteFlee(CombatScenario scenario, Unit playerUnit)
    {
        if (!CombatScenario.CanFlee(playerUnit.Position))
            return "You're too far from the edge to flee.";

        scenario.ExecuteFlee(playerUnit);
        return "You sprint for the edge!";
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
    }

    #endregion
}
