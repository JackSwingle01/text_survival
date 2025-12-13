using text_survival.Environments;
using text_survival.Environments.Features;
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

    public int TravelOutTimeMinutes { get; private set; } = travelTimeMinutes;
    public int TravelBackTimeMinutes { get; private set; } = travelTimeMinutes;
    public int WorkTimeMinutes { get; private set; } = workTimeMinutes;
    public int TimeVarianceMinutes { get; } = timeVarianceMinutes;

    public int TotalEstimatedTimeMinutes => TravelOutTimeMinutes + TravelBackTimeMinutes + WorkTimeMinutes;

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
            ExpeditionPhase.TravelingOut => MinutesElapsedPhase >= TravelOutTimeMinutes,
            ExpeditionPhase.Working => MinutesElapsedPhase >= WorkTimeMinutes,
            ExpeditionPhase.TravelingBack => MinutesElapsedPhase >= TravelBackTimeMinutes,
            _ => true,
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
        TravelBackTimeMinutes = GetMinutesBack(); ;
        CurrentPhase = ExpeditionPhase.TravelingBack;
        MinutesElapsedPhase = 0;
    }

    public int GetMinutesBack()
    {
        if (CurrentPhase == ExpeditionPhase.TravelingOut)
            return MinutesElapsedPhase; // return the way we came
        else if (CurrentPhase == ExpeditionPhase.Working)
            return TravelBackTimeMinutes; // full trip back
        else if (CurrentPhase == ExpeditionPhase.TravelingBack)
            return TravelBackTimeMinutes - MinutesElapsedPhase; // already heading back
        else
            throw new InvalidOperationException("Expedition is already completed.");
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
            ExpeditionPhase.NotStarted => "preparing",
            ExpeditionPhase.TravelingOut => $"traveling to {endLocation.Name}",
            ExpeditionPhase.Working => Type switch
            {
                ExpeditionType.Forage => "foraging",
                ExpeditionType.Hunt => "hunting",
                ExpeditionType.Explore => "exploring",
                ExpeditionType.Gather => "gathering",
                _ => "working"
            },
            ExpeditionPhase.TravelingBack => $"returning to {startLocation.Name}",
            ExpeditionPhase.Completed => "completed",
            _ => "unknown"
        };
    }

    public void CompleteWorkPhaseEarly()
    {
        if (CurrentPhase == ExpeditionPhase.Working)
        {
            AdvancePhase();
        }
    }

    public void AddWorkTimeMinutes(int minutes)
    {
        WorkTimeMinutes += minutes;
    }

    public class SegmentResult(int timeElapsed, GameEvent? gameEvent)
    {
        public int TimeElapsed { get; set; } = timeElapsed;
        public GameEvent? Event { get; set; } = gameEvent;
    }

    public SegmentResult RunExpeditionPhase(GameContext ctx)
    {
        int t = 0;
        GameEvent? evt = null;
        while (!IsPhaseComplete())
        {
            IncrementTime(1);
            t++;

            // check for events, encounters, etc.
            evt = GameEventRegistry.GetEventOnTick(ctx);

            if (evt is not null || IsPhaseComplete())
            {
                break;
            }
        }

        AddSegmentUpdate(t);
        if (CurrentPhase == ExpeditionPhase.Working)
        {
            DoWork(t);
        }
        return new SegmentResult(t, evt);
    }

    private void DoWork(int minutes)
    {
        if (Type == ExpeditionType.Forage)
        {
            DoForageWork(minutes);
        }
        if (Type == ExpeditionType.Gather)
        {
            DoHarvestWork(minutes);
        }
    }
    private void DoForageWork(int minutes)
    {
        Location location = endLocation;
        var feature = location.GetFeature<ForageFeature>() ?? throw new InvalidOperationException("Can't forage here.");
        var items = feature.Forage(minutes / 60.0);
        LootCollected.AddRange(items);
        if (items.Count > 0)
        {
            var groupedItems = items
                .GroupBy(item => item.Name)
                .Select(group => $"{group.Key} ({group.Count()})")
                .ToList();

            string timeText = minutes == 60 ? "1 hour" : $"{minutes} minutes";
            AddLog($"You spent {timeText} searching and found: {string.Join(", ", groupedItems)}");
        }
        else
        {
            string timeText = minutes == 60 ? "1 hour" : $"{minutes} minutes";
            AddLog($"You spent {timeText} searching but found nothing.");
        }
    }

    private void DoHarvestWork(int minutes)
    {
        var feature = endLocation.Features
                .OfType<HarvestableFeature>()
                .FirstOrDefault(f => f.IsDiscovered && f.HasAvailableResources());

        if (feature is null)
        {
            AddLog("There's nothing left to harvest here.");
            CompleteWorkPhaseEarly();
            return;
        }

        var items = feature.Harvest(minutes);
        if (items.Count > 0)
        {
            LootCollected.AddRange(items);

            var grouped = items.GroupBy(i => i.Name)
                .Select(g => $"{g.Key} ({g.Count()})");
            AddLog($"You spent {minutes} minutes harvesting and gathered: {string.Join(", ", grouped)}");
            if (feature.GetTotalMinutesToHarvest() > 0)
            {
                AddLog($"The {feature.DisplayName} is now {feature.GetStatusDescription()} and has {feature.GetTotalMinutesToHarvest()} minutes left of harvesting until depleted.");
            }
            else
            {
                AddLog($"The {feature.DisplayName} has been depleted.");
            }
        }
        else
        {
            AddLog($"You make steady progress on harvesting.");
        }
    }

    private void AddSegmentUpdate(int minutesPassed)
    {
        string action = CurrentPhase switch
        {
            ExpeditionPhase.TravelingOut => "walk",
            ExpeditionPhase.Working => "work",
            ExpeditionPhase.TravelingBack => "walk",
            _ => "unknown"
        };
        AddLog($"You {action} for {minutesPassed} minutes.");
    }

    /// <summary>
    /// Adds time that should be counted as a delay. Increments the time in phase but also the time remaining the same amount.
    /// </summary>
    /// <param name="minutes"></param>
    public void AddDelayTime(int minutes)
    {
        IncrementTime(minutes);
        if (CurrentPhase == ExpeditionPhase.TravelingOut)
        {
            TravelOutTimeMinutes += minutes;
        }
        else if (CurrentPhase == ExpeditionPhase.Working)
        {
            WorkTimeMinutes += minutes;
        }
        else if (CurrentPhase == ExpeditionPhase.TravelingBack)
        {
            TravelBackTimeMinutes += minutes;
        }

    }
}