using text_survival.Actors.Animals;
using text_survival.Environments;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Result of a work activity (forage, harvest, explore, hunt search).
/// WorkRunner returns this; caller handles logging and continuation.
/// </summary>
public record WorkResult(
    List<string> CollectedItems,
    Location? DiscoveredLocation,
    int MinutesElapsed,
    bool PlayerDied,
    Animal? FoundAnimal = null,  // Set by HuntStrategy when animal found - caller runs interactive hunt
    Herd? FoundHerd = null       // Set when animal came from persistent herd (for kill/wound tracking)
)
{
    public static WorkResult Empty(int minutes, bool died = false) =>
        new([], null, minutes, died);

    public static WorkResult Died(int minutes) =>
        new([], null, minutes, true);
}
