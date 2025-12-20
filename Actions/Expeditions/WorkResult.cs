using text_survival.Environments;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Result of a work activity (forage, harvest, explore).
/// WorkRunner returns this; caller handles logging and expedition tracking.
/// </summary>
public record WorkResult(
    List<string> CollectedItems,
    Location? DiscoveredLocation,
    int MinutesElapsed,
    bool PlayerDied
)
{
    public static WorkResult Empty(int minutes, bool died = false) =>
        new([], null, minutes, died);

    public static WorkResult Died(int minutes) =>
        new([], null, minutes, true);
}
