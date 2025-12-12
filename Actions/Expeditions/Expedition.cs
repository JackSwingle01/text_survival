

using System.Security.Cryptography.X509Certificates;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival.Actions.Expeditions;

public enum ExpeditionPhase { NotStarted = 0, TravelingOut = 1, Working = 2, TravelingBack = 3, Completed = 4 }

public enum ExpeditionType { Forage, Hunt, Explore, Gather }

public class Expedition(Location startLocation, Location endLocation, ExpeditionType type,
                        int travelTimeMinutes, int workTimeMinutes, int timeVarianceMinutes,
                        double exposureFactor, double detectionRisk)
{
    public Location startLocation { get; } = startLocation;
    public Location endLocation { get; } = endLocation;
    public ExpeditionType Type { get; } = type;

    public int TravelTimeMinutes { get; private set; } = travelTimeMinutes;
    public int WorkTimeMinutes { get; } = workTimeMinutes;
    public int TimeVarianceMinutes { get; } = timeVarianceMinutes;

    public int TotalEstimatedTimeMinutes => TravelTimeMinutes * 2 + WorkTimeMinutes;

    public double ExposureFactor { get; } = exposureFactor; // 0-1
    public double DetectionRisk { get; } = detectionRisk; // 0-1

    public ExpeditionPhase CurrentPhase { get; private set; }
    public int MinutesElapsedPhase { get; private set; }
    public int MinutesElapsedTotal { get; private set; }

    public List<Item> LootCollected { get; } = [];
    private List<string> EventsLog { get; } = [];
    public bool IsComplete => CurrentPhase == ExpeditionPhase.Completed;

    public void IncrementTime(int minutes)
    {
        MinutesElapsedPhase += minutes;
        MinutesElapsedTotal += minutes;
    }

    public bool IsPhaseComplete()
    {
        return CurrentPhase switch
        {
            ExpeditionPhase.TravelingOut => MinutesElapsedPhase >= TravelTimeMinutes,
            ExpeditionPhase.Working => MinutesElapsedPhase >= WorkTimeMinutes,
            ExpeditionPhase.TravelingBack => MinutesElapsedPhase >= TravelTimeMinutes,
            _ => false,
        };
    }

    public void AdvancePhase()
    {
        if (IsComplete)
            throw new InvalidOperationException("Expedition is already completed.");

        int nextPhase = (int)CurrentPhase + 1;

        if (nextPhase > (int)ExpeditionPhase.Completed)
            throw new InvalidOperationException("No further phases available.");

        CurrentPhase = (ExpeditionPhase)nextPhase;
        MinutesElapsedPhase = 0;
    }

    public void CancelExpedition()
    {
        TravelTimeMinutes = GetMinutesBack(); ;
        CurrentPhase = ExpeditionPhase.TravelingBack;
        MinutesElapsedPhase = 0;
    }

    public int GetMinutesBack()
    {
        if (CurrentPhase == ExpeditionPhase.TravelingOut)
            return MinutesElapsedPhase; // return the way we came
        else if (CurrentPhase == ExpeditionPhase.Working)
            return TravelTimeMinutes; // full trip back
        else if (CurrentPhase == ExpeditionPhase.TravelingBack)
            return MinutesElapsedPhase; // already heading back
        else
            throw new InvalidOperationException("Expedition is already completed.");
    }

    public string DangerLevel()
    {
        double dangerLevel = DetectionRisk + ExposureFactor * .5;
        if (dangerLevel < .2)
            return "Low";
        else if (dangerLevel < .5)
            return "Moderate";
        else
            return "High";
    }

    public string GetSummaryNotes()
    {
        string notes = "";
        if (detectionRisk > .2)
        {
            notes += "High detection risk. ";
        }
        if (ExposureFactor > .8)
        {
            notes += "The route is exposed to the weather.";
        }
        return notes;
    }

    public string WorkTimeWithVariance => $"{WorkTimeMinutes - TimeVarianceMinutes}-{WorkTimeMinutes + TimeVarianceMinutes}";
    public void AddLog(string log)
    {
        if (!string.IsNullOrEmpty(log))
        {
            EventsLog.Add(log);
        }
    }
    public List<string> GetFlushLogs()
    {
        List<string> logs = EventsLog.ToList();
        EventsLog.Clear();
        return logs;
    }

    public string GetPhaseDisplayName(ExpeditionPhase? phase = null)
    {
        phase ??= CurrentPhase;
        return phase switch
        {
            ExpeditionPhase.NotStarted => "Preparing",
            ExpeditionPhase.TravelingOut => $"Traveling to {endLocation.Name}",
            ExpeditionPhase.Working => Type switch
            {
                ExpeditionType.Forage => "Foraging",
                ExpeditionType.Hunt => "Hunting",
                ExpeditionType.Explore => "Exploring",
                ExpeditionType.Gather => "Gathering",
                _ => "Working"
            },
            ExpeditionPhase.TravelingBack => $"Returning to {startLocation.Name}",
            ExpeditionPhase.Completed => "Completed",
            _ => "Unknown"
        };
    }
}