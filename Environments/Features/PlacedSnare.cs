namespace text_survival.Environments.Features;

/// <summary>
/// Type of bait used in a snare.
/// </summary>
public enum BaitType
{
    None,
    Meat,
    Berries
}

/// <summary>
/// State of a placed snare.
/// </summary>
public enum SnareState
{
    Empty,      // Set and waiting
    CatchReady, // Has caught something
    Stolen,     // Catch was stolen by scavengers
    Destroyed   // Destroyed by predator
}

/// <summary>
/// Represents a snare placed at a location.
/// Tracks time since placement, bait status, catch status, and durability.
/// </summary>
public class PlacedSnare
{
    public SnareState State { get; private set; } = SnareState.Empty;
    public int MinutesSet { get; private set; }
    public BaitType Bait { get; private set; } = BaitType.None;
    public double BaitFreshness { get; private set; } = 1.0;
    public string? CaughtAnimalType { get; private set; }
    public double CaughtAnimalWeightKg { get; private set; }
    public int MinutesSinceCatch { get; private set; }
    public int DurabilityRemaining { get; private set; }
    public bool IsReinforced { get; }

    // Constants
    private const double BaseChancePerHour = 0.03;
    private const double MaxTimeBonus = 1.0;
    private const double HoursToMaxBonus = 6.0;
    private const double BaitDecayPerHour = 0.10;
    private const double ScavengerChancePerHour = 0.08;
    private const int ScavengerThresholdMinutes = 180; // 3 hours

    public PlacedSnare(int durability, bool reinforced = false)
    {
        DurabilityRemaining = durability;
        IsReinforced = reinforced;
    }

    /// <summary>
    /// Add bait to the snare.
    /// </summary>
    public void AddBait(BaitType bait)
    {
        Bait = bait;
        BaitFreshness = 1.0;
    }

    /// <summary>
    /// Check if snare has caught something.
    /// </summary>
    public bool HasCatch => State == SnareState.CatchReady;

    /// <summary>
    /// Check if snare is usable (not destroyed and has durability).
    /// </summary>
    public bool IsUsable => State != SnareState.Destroyed && DurabilityRemaining > 0;

    /// <summary>
    /// Check if snare is baited.
    /// </summary>
    public bool IsBaited => Bait != BaitType.None && BaitFreshness > 0;

    /// <summary>
    /// Update snare state for elapsed time.
    /// Returns messages for any state changes.
    /// </summary>
    public List<string> Update(int minutes, double gameDensity, List<(string type, double weightKg)> smallGame)
    {
        var messages = new List<string>();
        MinutesSet += minutes;

        // Process based on current state
        switch (State)
        {
            case SnareState.Empty:
                UpdateEmptyState(minutes, gameDensity, smallGame, messages);
                break;
            case SnareState.CatchReady:
                UpdateCatchReadyState(minutes, messages);
                break;
        }

        return messages;
    }

    private void UpdateEmptyState(int minutes, double gameDensity, List<(string type, double weightKg)> smallGame, List<string> messages)
    {
        // Decay bait
        if (Bait != BaitType.None)
        {
            double decayAmount = (minutes / 60.0) * BaitDecayPerHour;
            BaitFreshness = Math.Max(0, BaitFreshness - decayAmount);

            if (BaitFreshness <= 0)
            {
                Bait = BaitType.None;
            }
        }

        // Check for catch (only if there are small game in the area)
        if (smallGame.Count > 0)
        {
            double catchChance = CalculateCatchChance(minutes, gameDensity);

            if (Utils.DetermineSuccess(catchChance))
            {
                // Select a random small game animal
                var (animalType, weightKg) = SelectRandomAnimal(smallGame);
                CaughtAnimalType = animalType;
                CaughtAnimalWeightKg = weightKg;
                State = SnareState.CatchReady;
                MinutesSinceCatch = 0;
                ConsumeDurability();
            }
        }
    }

    private void UpdateCatchReadyState(int minutes, List<string> messages)
    {
        MinutesSinceCatch += minutes;

        // Check for scavenger theft after threshold
        if (MinutesSinceCatch >= ScavengerThresholdMinutes)
        {
            double theftChance = (minutes / 60.0) * ScavengerChancePerHour;

            if (Utils.DetermineSuccess(theftChance))
            {
                State = SnareState.Stolen;
            }
        }
    }

    /// <summary>
    /// Calculate catch chance for elapsed time.
    /// </summary>
    private double CalculateCatchChance(int minutes, double gameDensity)
    {
        double hoursSet = MinutesSet / 60.0;
        double hoursElapsed = minutes / 60.0;

        // Time bonus peaks at 6 hours
        double timeBonus = Math.Min(MaxTimeBonus, hoursSet / HoursToMaxBonus);

        // Bait multiplier: 1.5-2.5x with fresh bait
        double baitMult = Bait != BaitType.None ? (1.5 + BaitFreshness * 1.0) : 1.0;

        // Final chance per hour
        double chancePerHour = BaseChancePerHour * timeBonus * gameDensity * baitMult;

        // Convert to chance for elapsed time
        return chancePerHour * hoursElapsed;
    }

    /// <summary>
    /// Select a random small game animal weighted by spawn probability.
    /// </summary>
    private static (string type, double weightKg) SelectRandomAnimal(List<(string type, double weightKg)> animals)
    {
        if (animals.Count == 0)
            return ("rabbit", 2.0);

        return animals[Random.Shared.Next(animals.Count)];
    }

    /// <summary>
    /// Collect the catch, clearing the snare.
    /// Returns the animal type and weight.
    /// </summary>
    public (string? type, double weightKg) CollectCatch()
    {
        if (State != SnareState.CatchReady)
            return (null, 0);

        var result = (CaughtAnimalType, CaughtAnimalWeightKg);

        // Reset state
        CaughtAnimalType = null;
        CaughtAnimalWeightKg = 0;
        MinutesSinceCatch = 0;
        State = SnareState.Empty;

        return result;
    }

    /// <summary>
    /// Get scavenged remains if catch was stolen.
    /// </summary>
    public (string? type, double weightKg) GetStolenRemains()
    {
        if (State != SnareState.Stolen)
            return (null, 0);

        // Return partial remains (bones/scraps)
        var result = (CaughtAnimalType, CaughtAnimalWeightKg * 0.2);

        // Reset
        CaughtAnimalType = null;
        CaughtAnimalWeightKg = 0;
        State = SnareState.Empty;

        return result;
    }

    /// <summary>
    /// Mark snare as destroyed by predator.
    /// </summary>
    public void Destroy()
    {
        State = SnareState.Destroyed;
        DurabilityRemaining = 0;
    }

    /// <summary>
    /// Consume one use of durability.
    /// </summary>
    private void ConsumeDurability()
    {
        if (DurabilityRemaining > 0)
            DurabilityRemaining--;
    }

    /// <summary>
    /// Get a description of the snare's current state.
    /// </summary>
    public string GetStatusDescription()
    {
        return State switch
        {
            SnareState.CatchReady => $"Catch! ({CaughtAnimalType})",
            SnareState.Stolen => "Plundered (scraps remain)",
            SnareState.Destroyed => "Destroyed",
            _ when IsBaited => $"Set (baited, {BaitFreshness:P0} fresh)",
            _ => $"Set ({MinutesSet / 60.0:F1}h)"
        };
    }

    #region Save/Load Support

    /// <summary>
    /// Restore snare state from save data.
    /// </summary>
    internal void RestoreState(
        SnareState state,
        int minutesSet,
        BaitType bait,
        double baitFreshness,
        string? caughtAnimalType,
        double caughtAnimalWeight,
        int minutesSinceCatch,
        int durability)
    {
        State = state;
        MinutesSet = minutesSet;
        Bait = bait;
        BaitFreshness = baitFreshness;
        CaughtAnimalType = caughtAnimalType;
        CaughtAnimalWeightKg = caughtAnimalWeight;
        MinutesSinceCatch = minutesSinceCatch;
        DurabilityRemaining = durability;
    }

    /// <summary>
    /// Create a snare with restored state (for save/load).
    /// </summary>
    internal static PlacedSnare CreateRestored(int durability, bool reinforced) => new(durability, reinforced);

    #endregion
}
