namespace text_survival.Environments.Features;

/// <summary>
/// State of a placed fishing net.
/// </summary>
public enum NetState
{
    Empty,        // Just set, no fish yet
    Soaking,      // Collecting fish over time
    CatchReady,   // Has fish waiting to be collected
    Lost,         // Lost to current or ice
    Stolen        // Catch stolen by predator
}

/// <summary>
/// Represents a fishing net placed in water.
/// Passively catches fish over time.
/// </summary>
public class PlacedNet
{
    public NetState State { get; private set; } = NetState.Empty;
    public int SoakDurationMinutes { get; private set; }
    public List<double> CaughtFishWeights { get; private set; } = [];
    public int DurabilityRemaining { get; private set; }
    public int MinutesSinceCatch { get; private set; }

    // Constants
    private const int MinSoakMinutes = 240;      // 4 hours minimum before catching
    private const int MaxSoakMinutes = 720;      // 12 hours for max catch
    private const double BaseChancePerHour = 0.15;
    private const double MaxFishWeight = 2.0;
    private const double MinFishWeight = 0.4;
    private const int MinFishCount = 2;
    private const int MaxFishCount = 5;
    private const double SpoilageThresholdMinutes = 1080;  // 18 hours
    private const double CurrentLossChancePerHour = 0.01;  // 5% over ~5 hours
    private const double PredatorTheftChancePerHour = 0.02; // When stalked

    public PlacedNet(int durability)
    {
        DurabilityRemaining = durability;
    }

    /// <summary>
    /// Check if net can still be used.
    /// </summary>
    public bool IsUsable => State != NetState.Lost && DurabilityRemaining > 0;

    /// <summary>
    /// Check if net has fish ready to collect.
    /// </summary>
    public bool HasCatch => State == NetState.CatchReady && CaughtFishWeights.Count > 0;

    /// <summary>
    /// Total weight of caught fish.
    /// </summary>
    public double TotalCatchWeight => CaughtFishWeights.Sum();

    /// <summary>
    /// Update net state for elapsed time.
    /// </summary>
    public List<string> Update(int minutes, double fishAbundance, bool isFlowingWater, bool stalkedTensionActive)
    {
        var messages = new List<string>();

        if (!IsUsable) return messages;

        SoakDurationMinutes += minutes;
        double hours = minutes / 60.0;

        switch (State)
        {
            case NetState.Empty:
            case NetState.Soaking:
                UpdateSoakingState(minutes, fishAbundance, isFlowingWater, messages);
                break;

            case NetState.CatchReady:
                UpdateCatchReadyState(minutes, stalkedTensionActive, messages);
                break;
        }

        return messages;
    }

    private void UpdateSoakingState(int minutes, double fishAbundance, bool isFlowingWater, List<string> messages)
    {
        State = NetState.Soaking;
        double hours = minutes / 60.0;

        // Risk: Lost to current in flowing water
        if (isFlowingWater)
        {
            double lossChance = CurrentLossChancePerHour * hours;
            if (Utils.DetermineSuccess(lossChance))
            {
                State = NetState.Lost;
                return;
            }
        }

        // Check for fish catch after minimum soak time
        if (SoakDurationMinutes >= MinSoakMinutes)
        {
            // Calculate catch based on soak time and abundance
            double soakProgress = Math.Min(1.0, (SoakDurationMinutes - MinSoakMinutes) / (double)(MaxSoakMinutes - MinSoakMinutes));
            double catchChance = BaseChancePerHour * hours * fishAbundance * (0.5 + soakProgress);

            if (Utils.DetermineSuccess(catchChance) || SoakDurationMinutes >= MaxSoakMinutes)
            {
                // Generate catch
                int fishCount = MinFishCount + Random.Shared.Next(MaxFishCount - MinFishCount + 1);
                fishCount = (int)(fishCount * fishAbundance);
                fishCount = Math.Max(MinFishCount, fishCount);

                CaughtFishWeights = [];
                for (int i = 0; i < fishCount; i++)
                {
                    double weight = MinFishWeight + Random.Shared.NextDouble() * (MaxFishWeight - MinFishWeight);
                    CaughtFishWeights.Add(weight);
                }

                State = NetState.CatchReady;
                MinutesSinceCatch = 0;
                ConsumeDurability();
            }
        }
    }

    private void UpdateCatchReadyState(int minutes, bool stalkedTensionActive, List<string> messages)
    {
        MinutesSinceCatch += minutes;
        double hours = minutes / 60.0;

        // Risk: Predator theft when stalked
        if (stalkedTensionActive)
        {
            double theftChance = PredatorTheftChancePerHour * hours;
            if (Utils.DetermineSuccess(theftChance))
            {
                State = NetState.Stolen;
                CaughtFishWeights = [];
                return;
            }
        }

        // Risk: Fish spoilage (reduce catch quality over time)
        if (MinutesSinceCatch >= SpoilageThresholdMinutes)
        {
            // Fish start spoiling - lose one fish every 2 hours past threshold
            double hoursPastThreshold = (MinutesSinceCatch - SpoilageThresholdMinutes) / 60.0;
            int fishToLose = (int)(hoursPastThreshold / 2);
            while (fishToLose > 0 && CaughtFishWeights.Count > 0)
            {
                CaughtFishWeights.RemoveAt(CaughtFishWeights.Count - 1);
                fishToLose--;
            }

            if (CaughtFishWeights.Count == 0)
            {
                State = NetState.Empty;
                SoakDurationMinutes = 0;
            }
        }
    }

    /// <summary>
    /// Collect the catch, clearing the net.
    /// Returns list of fish weights.
    /// </summary>
    public List<double> CollectCatch()
    {
        if (State != NetState.CatchReady)
            return [];

        var result = CaughtFishWeights.ToList();

        // Reset state
        CaughtFishWeights = [];
        MinutesSinceCatch = 0;
        SoakDurationMinutes = 0;
        State = NetState.Empty;

        return result;
    }

    /// <summary>
    /// Mark net as lost (ice froze over, current swept away).
    /// </summary>
    public void MarkLost()
    {
        State = NetState.Lost;
        DurabilityRemaining = 0;
    }

    /// <summary>
    /// Mark catch as stolen by predator.
    /// </summary>
    public void MarkStolen()
    {
        State = NetState.Stolen;
        CaughtFishWeights = [];
    }

    private void ConsumeDurability()
    {
        if (DurabilityRemaining > 0)
            DurabilityRemaining--;
    }

    /// <summary>
    /// Get description of net's current state.
    /// </summary>
    public string GetStatusDescription()
    {
        return State switch
        {
            NetState.CatchReady => $"Catch ready! ({CaughtFishWeights.Count} fish, {TotalCatchWeight:F1}kg)",
            NetState.Stolen => "Plundered by predator",
            NetState.Lost => "Lost",
            NetState.Soaking when SoakDurationMinutes < MinSoakMinutes =>
                $"Soaking ({SoakDurationMinutes / 60.0:F1}h, needs {MinSoakMinutes / 60.0:F0}h min)",
            NetState.Soaking => $"Soaking ({SoakDurationMinutes / 60.0:F1}h)",
            _ => $"Set ({SoakDurationMinutes / 60.0:F1}h)"
        };
    }
}
