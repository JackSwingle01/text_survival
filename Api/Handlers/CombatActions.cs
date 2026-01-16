using text_survival.Actions;

namespace text_survival.Api;

/// <summary>
/// Handles combat-related actions.
/// Combat is complex - for now, abort on load per the plan.
/// </summary>
public static class CombatActions
{
    /// <summary>
    /// Process a player's choice during combat.
    /// </summary>
    public static GameResponse ProcessChoice(GameContext ctx, string choiceId)
    {
        if (ctx.PendingActivity?.Phase != ActivityPhase.CombatPlayerTurn)
            return GameResponse.Error("Not player's turn in combat");

        if (ctx.PendingActivity.CombatScenario == null)
        {
            // Combat was interrupted by save/load - abort gracefully
            return AbortCombat(ctx, "Combat was interrupted.");
        }

        // TODO: Migrate combat choice logic from CombatController
        // For now, return error indicating not yet implemented
        return GameResponse.Error($"Combat action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>
    /// Process continue during combat phases.
    /// </summary>
    public static GameResponse ProcessContinue(GameContext ctx)
    {
        if (ctx.PendingActivity == null)
            return GameResponse.Error("No pending combat activity");

        // Handle different combat phases
        switch (ctx.PendingActivity.Phase)
        {
            case ActivityPhase.CombatIntro:
            case ActivityPhase.CombatPlayerAction:
            case ActivityPhase.CombatAnimalTurn:
                // Advance to next phase
                if (ctx.PendingActivity.CombatScenario == null)
                {
                    return AbortCombat(ctx, "Combat was interrupted.");
                }
                // TODO: Implement phase transitions
                return GameResponse.Error("Combat phase transition not yet implemented");

            case ActivityPhase.CombatResult:
                // Combat is over, clear pending activity
                ctx.PendingActivity = null;
                return GameResponse.Success(ctx);

            default:
                return GameResponse.Error($"Invalid combat phase: {ctx.PendingActivity.Phase}");
        }
    }

    /// <summary>
    /// Gracefully abort combat that was interrupted (e.g., by save/load).
    /// </summary>
    private static GameResponse AbortCombat(GameContext ctx, string message)
    {
        ctx.PendingActivity!.Phase = ActivityPhase.CombatResult;
        ctx.PendingActivity.CombatNarrative = message + " The predator retreats.";
        ctx.PendingActivity.Combat = new CombatSnapshot(
            AnimalType: "Unknown",
            Zone: 3,
            AnimalHealth: 0,
            PlayerHealth: ctx.player.Vitality,
            AnimalState: "fled",
            AvailableActions: new List<string>(),
            DistanceMeters: 30,
            ZoneName: "far",
            PlayerEnergy: 1.0,
            Narrative: message + " The predator retreats."
        );
        return GameResponse.Success(ctx);
    }
}
