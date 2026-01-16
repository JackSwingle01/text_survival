using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Desktop;
using DesktopIO = text_survival.Desktop.DesktopIO;
using text_survival.Desktop.Dto;

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
    /// Main entry point for player combat. Matches old CombatRunner signature.
    /// </summary>
    public static CombatResult RunCombat(GameContext ctx, Animal enemy)
    {
        // === SETUP ===
        var (scenario, playerUnit) = SetupCombat(ctx, enemy);

        // Show intro
        var introDto = BuildCombatDto(ctx, scenario, playerUnit, CombatPhase.Intro,
            narrative: $"A {enemy.Name.ToLower()} attacks!");
        DesktopIO.RenderCombat(ctx, introDto);
        DesktopIO.WaitForCombatContinue(ctx);

        // === MAIN LOOP ===
        while (!scenario.IsOver)
        {
            // 1. Player choice
            var choiceDto = BuildCombatDto(ctx, scenario, playerUnit, CombatPhase.PlayerChoice);
            string choice = DesktopIO.WaitForCombatChoice(ctx, choiceDto);

            // 2. Execute player action
            var narrative = ExecutePlayerChoice(scenario, playerUnit, choice);

            // 3. Show player action result
            if (!string.IsNullOrEmpty(narrative))
            {
                var actionDto = BuildCombatDto(ctx, scenario, playerUnit, CombatPhase.PlayerAction,
                    narrative: narrative);
                DesktopIO.RenderCombat(ctx, actionDto);
                DesktopIO.WaitForCombatContinue(ctx);
            }

            if (scenario.IsOver) break;

            // 4. AI turns - process each unit individually with 1-second auto-advance
            foreach (var unit in scenario.Units.Where(u => u != playerUnit).ToList())
            {
                var aiNarrative = scenario.ProcessSingleAITurn(unit);
                if (aiNarrative != null)
                {
                    var aiDto = BuildCombatDto(ctx, scenario, playerUnit, CombatPhase.AnimalAction,
                        narrative: aiNarrative);
                    // Set auto-advance so frontend responds after 1 second
                    aiDto = aiDto with { AutoAdvanceMs = 1000 };
                    DesktopIO.RenderCombat(ctx, aiDto);
                    DesktopIO.WaitForCombatContinue(ctx);
                }
                if (scenario.IsOver) break;
            }
        }

        // === CLEANUP ===
        var result = DetermineResult(scenario, playerUnit);
        var outcomeDto = BuildOutcomeDto(ctx, scenario, playerUnit, result);
        DesktopIO.RenderCombat(ctx, outcomeDto);
        DesktopIO.WaitForCombatContinue(ctx);
        DesktopIO.ClearCombat(ctx);  // Clear combat mode - return to travel mode

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
            _ => ""
        };
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
        scenario.ExecuteAction(CombatActions.Attack, playerUnit, nearest);
        return $"You attack the {nearest.actor.Name}!";
    }

    private static string ExecuteThrow(CombatScenario scenario, Unit playerUnit, Unit nearest)
    {
        scenario.ExecuteAction(CombatActions.Throw, playerUnit, nearest);
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
        return "You shout and make yourself large!";
    }

    private static List<CombatActionDto> GetAvailableActions(CombatScenario scenario, Unit playerUnit, GameContext ctx)
    {
        var actions = new List<CombatActionDto>();
        var nearest = scenario.GetNearestEnemy(playerUnit);
        if (nearest == null) return actions;

        double distance = playerUnit.Position.DistanceTo(nearest.Position);
        var zone = CombatScenario.GetZone(distance);
        bool hasWeapon = ctx.Inventory.Weapon != null;

        switch (zone)
        {
            case Zone.close:
                actions.Add(MakeAction(CombatActions.Attack));
                actions.Add(MakeAction(CombatActions.Block));
                actions.Add(MakeAction(CombatActions.Shove));
                actions.Add(MakeAction(CombatActions.Retreat));
                break;

            case Zone.near:
                actions.Add(MakeAction(CombatActions.Attack));
                actions.Add(MakeAction(CombatActions.Dodge));
                actions.Add(MakeAction(CombatActions.Block));
                actions.Add(MakeAction(CombatActions.Advance));
                actions.Add(MakeAction(CombatActions.Retreat));
                break;

            case Zone.mid:
                if (hasWeapon)
                    actions.Add(MakeAction(CombatActions.Throw));
                actions.Add(MakeAction(CombatActions.Intimidate));
                actions.Add(MakeAction(CombatActions.Advance));
                actions.Add(MakeAction(CombatActions.Retreat));
                break;

            case Zone.far:
                actions.Add(MakeAction(CombatActions.Intimidate));
                actions.Add(MakeAction(CombatActions.Advance));
                actions.Add(MakeAction(CombatActions.Retreat));
                break;
        }

        return actions;
    }

    private static CombatActionDto MakeAction(CombatActions action)
    {
        string label = action switch
        {
            CombatActions.Throw => "Throw Weapon",
            _ => action.ToString()
        };
        return new CombatActionDto(action.ToString().ToLowerInvariant(), label, null, true, null, null);
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

    #region DTO Building

    private static CombatDto BuildCombatDto(
        GameContext ctx,
        CombatScenario scenario,
        Unit playerUnit,
        CombatPhase phase,
        string? narrative = null)
    {
        var nearest = scenario.GetNearestEnemy(playerUnit);
        double distance = nearest != null ? playerUnit.Position.DistanceTo(nearest.Position) : 99;
        var zone = CombatScenario.GetZone(distance);

        var actions = phase == CombatPhase.PlayerChoice
            ? GetAvailableActions(scenario, playerUnit, ctx)
            : new List<CombatActionDto>();

        var grid = BuildGridDto(ctx, scenario);

        return new CombatDto(
            DistanceZone: zone.ToString(),
            DistanceMeters: distance,
            PreviousDistanceMeters: null,
            PlayerVitality: ctx.player.Vitality,
            PlayerEnergy: ctx.player.Body.Energy / 480.0,
            PlayerBraced: false,
            Phase: phase,
            NarrativeMessage: narrative,
            Actions: actions,
            ThreatFactors: new List<ThreatFactorDto>(),
            Outcome: null,
            Grid: grid
        );
    }

    private static CombatDto BuildOutcomeDto(
        GameContext ctx,
        CombatScenario scenario,
        Unit playerUnit,
        CombatResult result)
    {
        var outcomeType = result switch
        {
            CombatResult.Victory => "victory",
            CombatResult.Defeat => "defeat",
            CombatResult.Fled => "fled",
            CombatResult.AnimalFled => "animal_fled",
            _ => "ended"
        };

        var message = result switch
        {
            CombatResult.Victory => "You are victorious!",
            CombatResult.Defeat => "You have been killed.",
            CombatResult.Fled => "You escape!",
            CombatResult.AnimalFled => "Your enemies flee!",
            _ => "The encounter ends."
        };

        var rewards = result == CombatResult.Victory
            ? scenario.Team2.Where(u => !u.actor.IsAlive)
                .Select(u => $"{u.actor.Name} carcass")
                .ToList()
            : null;

        var outcome = new CombatOutcomeDto(outcomeType, message, rewards);

        var dto = BuildCombatDto(ctx, scenario, playerUnit, CombatPhase.Outcome);
        // Return new DTO with outcome attached
        return dto with { Outcome = outcome, NarrativeMessage = message };
    }

    private static CombatGridDto BuildGridDto(GameContext ctx, CombatScenario scenario)
    {
        var units = new List<CombatUnitDto>();

        foreach (var unit in scenario.Units)
        {
            string team = scenario.Team1.Contains(unit) ? "ally" : "enemy";
            if (unit == scenario.Player) team = "player";

            // Get icon based on actor type
            string icon = GetActorIcon(unit.actor, team);

            units.Add(new CombatUnitDto(
                Id: unit.GetHashCode().ToString(),
                Name: unit.actor.Name,
                Team: team,
                Position: new CombatGridPositionDto(unit.Position.X, unit.Position.Y),
                Vitality: unit.actor.Vitality,
                HealthDescription: GetHealthDescription(unit.actor.Vitality),
                Threat: unit.Threat,
                Boldness: unit.Boldness,
                Aggression: unit.Aggression,
                BoldnessDescriptor: GetBoldnessDescriptor(unit.Boldness),
                Icon: icon
            ));
        }

        return new CombatGridDto(
            GridSize: 25,
            CellSizeMeters: 1.0,
            Units: units,
            Terrain: ctx.CurrentLocation?.Terrain.ToString(),
            LocationX: ctx.Map?.CurrentPosition.X,
            LocationY: ctx.Map?.CurrentPosition.Y
        );
    }

    private static string GetActorIcon(Actor actor, string team)
    {
        // Player gets person icon
        if (team == "player") return "👤";

        // NPCs (allies) get person icon
        if (actor is NPC) return "🧑";

        // Animals get their type-specific emoji
        if (actor is Animal)
        {
            var animalType = AnimalTypes.Parse(actor.Name);
            if (animalType.HasValue)
                return animalType.Value.Emoji();
        }

        // Fallback
        return "🐾";
    }

    private static string GetBoldnessDescriptor(double boldness)
    {
        return boldness switch
        {
            >= 0.7 => "aggressive",
            >= 0.5 => "bold",
            >= 0.3 => "wary",
            _ => "cautious"
        };
    }

    private static string GetHealthDescription(double vitality)
    {
        return vitality switch
        {
            >= 0.9 => "healthy",
            >= 0.7 => "wounded",
            >= 0.5 => "badly hurt",
            >= 0.3 => "staggering",
            _ => "near death"
        };
    }

    #endregion
}
