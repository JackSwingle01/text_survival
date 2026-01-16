using text_survival.Actions;

namespace text_survival.Api;

/// <summary>
/// Extension methods for converting GameEvent to serializable snapshots.
/// </summary>
public static class EventExtensions
{
    /// <summary>
    /// Generate a deterministic ID for a choice based on its label and index.
    /// </summary>
    public static string GenerateChoiceId(string label, int index) =>
        $"choice_{index}";

    /// <summary>
    /// Create a serializable snapshot of the event for the pending activity state.
    /// </summary>
    public static EventSnapshot ToSnapshot(this GameEvent evt, GameContext ctx)
    {
        var availableChoices = evt.GetAvailableChoices(ctx);

        return new EventSnapshot(
            Id: evt.Name,
            Description: evt.Description,
            Choices: availableChoices.Select((c, i) => new ChoiceSnapshot(
                Id: GenerateChoiceId(c.Label, i),
                Text: c.Label
            )).ToList()
        );
    }

    /// <summary>
    /// Find the choice matching the given choice ID.
    /// Returns null if no match found.
    /// </summary>
    public static EventChoice? FindChoiceById(this GameEvent evt, GameContext ctx, string choiceId)
    {
        var availableChoices = evt.GetAvailableChoices(ctx);

        for (int i = 0; i < availableChoices.Count; i++)
        {
            if (GenerateChoiceId(availableChoices[i].Label, i) == choiceId)
            {
                return availableChoices[i];
            }
        }

        return null;
    }
}
