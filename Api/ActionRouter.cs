using text_survival.Actions;
using text_survival.Web.Dto;

namespace text_survival.Api;

/// <summary>
/// Central router for all game actions. Dispatches based on current pending activity phase.
/// This consolidates all 21 controllers into a single dispatcher.
/// </summary>
public static class ActionRouter
{
    /// <summary>
    /// Process a player action based on current game state.
    /// </summary>
    /// <param name="ctx">Current game context</param>
    /// <param name="choiceId">The action/choice identifier</param>
    /// <returns>GameResponse with updated state</returns>
    public static GameResponse ProcessAction(GameContext ctx, string choiceId)
    {
        if (ctx.PendingActivity == null)
            return ProcessIdleAction(ctx, choiceId);

        return ctx.PendingActivity.Phase switch
        {
            // Event phases
            ActivityPhase.EventPending => EventActions.ProcessChoice(ctx, choiceId),
            ActivityPhase.EventOutcomeShown => EventActions.ProcessContinue(ctx),

            // Hunt phases
            ActivityPhase.HuntSighting => HuntActions.ProcessChoice(ctx, choiceId),
            ActivityPhase.HuntActive => HuntActions.ProcessChoice(ctx, choiceId),
            ActivityPhase.HuntResult => HuntActions.ProcessContinue(ctx),

            // Encounter phases
            ActivityPhase.EncounterActive => EncounterActions.ProcessChoice(ctx, choiceId),
            ActivityPhase.EncounterOutcome => EncounterActions.ProcessContinue(ctx),

            // Combat phases
            ActivityPhase.CombatIntro => CombatActions.ProcessContinue(ctx),
            ActivityPhase.CombatPlayerTurn => CombatActions.ProcessChoice(ctx, choiceId),
            ActivityPhase.CombatPlayerAction => CombatActions.ProcessContinue(ctx),
            ActivityPhase.CombatAnimalTurn => CombatActions.ProcessContinue(ctx),
            ActivityPhase.CombatResult => CombatActions.ProcessContinue(ctx),

            // Travel phases
            ActivityPhase.TravelHazardPending => TravelActions.ProcessHazardChoice(ctx, choiceId),
            ActivityPhase.TravelImpairmentWarning => TravelActions.ProcessImpairmentChoice(ctx, choiceId),
            ActivityPhase.TravelBlocked => TravelActions.ProcessBlockedContinue(ctx),
            ActivityPhase.TravelInterrupted => TravelActions.ProcessInterruptedChoice(ctx, choiceId),

            ActivityPhase.None => ProcessIdleAction(ctx, choiceId),
            _ => GameResponse.Error($"Unhandled activity phase: {ctx.PendingActivity.Phase}")
        };
    }

    /// <summary>
    /// Process actions when no pending activity - player is in idle/camp state.
    /// </summary>
    private static GameResponse ProcessIdleAction(GameContext ctx, string choiceId)
    {
        // Parse choice ID to determine action category
        var parts = choiceId.Split('_');
        var category = parts[0].ToLowerInvariant();

        return category switch
        {
            // Navigation
            "move" => IdleActions.ProcessMove(ctx, choiceId),

            // Camp activities
            "fire" => IdleActions.ProcessFire(ctx, choiceId),
            "inventory" => IdleActions.ProcessInventory(ctx, choiceId),
            "storage" => IdleActions.ProcessStorage(ctx, choiceId),
            "crafting" => IdleActions.ProcessCrafting(ctx, choiceId),
            "eat" => IdleActions.ProcessEat(ctx, choiceId),
            "sleep" => IdleActions.ProcessSleep(ctx, choiceId),
            "wait" => IdleActions.ProcessWait(ctx, choiceId),

            // Work activities
            "forage" => IdleActions.ProcessForage(ctx, choiceId),
            "hunt" => IdleActions.ProcessHunt(ctx, choiceId),
            "chop" => IdleActions.ProcessChop(ctx, choiceId),
            "harvest" => IdleActions.ProcessHarvest(ctx, choiceId),
            "butcher" => IdleActions.ProcessButcher(ctx, choiceId),

            // Torch
            "torch" => IdleActions.ProcessTorch(ctx, choiceId),

            // Continue/dismiss overlays
            "continue" => IdleActions.ProcessContinue(ctx),
            "dismiss" => IdleActions.ProcessDismiss(ctx),

            _ => GameResponse.Error($"Unknown action: {choiceId}")
        };
    }
}
