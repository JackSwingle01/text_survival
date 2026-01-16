using text_survival.Actions;
using text_survival.UI;

namespace text_survival.Api;

/// <summary>
/// Handles event-related actions.
/// </summary>
public static class EventActions
{
    /// <summary>
    /// Process a player's choice in a pending event.
    /// </summary>
    public static GameResponse ProcessChoice(GameContext ctx, string choiceId)
    {
        if (ctx.PendingActivity?.Phase != ActivityPhase.EventPending)
            return GameResponse.Error("No pending event");

        if (ctx.PendingActivity.EventSource == null)
            return GameResponse.Error("Event source not available - may have been lost during save/load");

        var choice = ctx.PendingActivity.EventSource.FindChoiceById(ctx, choiceId);
        if (choice == null)
            return GameResponse.Error($"Invalid choice: {choiceId}");

        var outcome = choice.DetermineResult();
        var outcomeData = GameEventRegistry.HandleOutcome(ctx, outcome);

        ctx.PendingActivity.SelectedChoiceId = choiceId;
        ctx.PendingActivity.Outcome = new EventOutcomeSnapshot(
            Description: outcome.Message,
            AbortsAction: outcome.AbortsAction
        );
        ctx.PendingActivity.Phase = ActivityPhase.EventOutcomeShown;

        if (outcome.ChainEvent != null)
        {
            var chainedEvent = outcome.ChainEvent(ctx);
            ctx.PendingActivity.EventSource = chainedEvent;
        }

        if (outcome.SpawnEncounter != null)
        {
            ctx.QueueEncounter(outcome.SpawnEncounter);
        }

        return GameResponse.Success(ctx);
    }

    /// <summary>
    /// Process continue/dismiss after viewing event outcome.
    /// </summary>
    public static GameResponse ProcessContinue(GameContext ctx)
    {
        if (ctx.PendingActivity?.Phase != ActivityPhase.EventOutcomeShown)
            return GameResponse.Error("No event outcome to continue from");

        // Check if there's a chained event
        if (ctx.PendingActivity.EventSource != null)
        {
            ctx.PendingActivity.Event = ctx.PendingActivity.EventSource.ToSnapshot(ctx);
            ctx.PendingActivity.Phase = ActivityPhase.EventPending;
            ctx.PendingActivity.Outcome = null;
            ctx.PendingActivity.SelectedChoiceId = null;
        }
        else
        {
            // Clear pending activity
            ctx.PendingActivity = null;
        }

        // Check for queued encounters
        ctx.HandlePendingEncounter();

        return GameResponse.Success(ctx);
    }
}
