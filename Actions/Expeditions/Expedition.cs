using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions.Expeditions;

public enum ExpeditionPhase { NotStarted = 0, TravelingOut = 1, Working = 2, TravelingBack = 3, Completed = 4 }

public enum ExpeditionType { Forage, Hunt, Explore, Gather }

public class Expedition(List<Location> path, int destinationIndex, Player player,
                        ExpeditionType type, int workTimeMinutes)
{
    public ExpeditionType Type { get; } = type;
    private readonly Player Player = player; // just for reading player stats for traversal
    /// <summary>
    /// Path is: start -> t1 -> ... -> tn -> dest -> tn -> ... t1 -> start
    /// i.e. include the start and 
    /// </summary>
    public List<Location> Path { get; } = path;
    public int CurrentIndex { get; private set; } = 0;
    public int DestinationIndex { get; private set; } = destinationIndex;

    // time tracking 
    public int WorkTimeMinutes { get; private set; } = workTimeMinutes;
    public int MinutesElapsedTotal { get; private set; }
    public int MinutesSpentAtLocation { get; private set; }

    // derived properties
    public ExpeditionPhase CurrentPhase
    {
        get
        {
            if (CurrentIndex == 0) return ExpeditionPhase.NotStarted;
            else if (CurrentIndex > 0 && CurrentIndex < DestinationIndex) return ExpeditionPhase.TravelingOut;
            else if (CurrentIndex == DestinationIndex) return ExpeditionPhase.Working;
            else if (CurrentIndex > DestinationIndex && CurrentIndex < Path.Count - 1) return ExpeditionPhase.TravelingBack;
            else if (CurrentIndex == Path.Count - 1) return ExpeditionPhase.Completed;
            else throw new InvalidDataException("Current Index is Invalid");
        }
    }
    public Location CurrentLocation => Path[CurrentIndex];
    public Location Destination => Path[DestinationIndex];
    public bool IsComplete => CurrentPhase == ExpeditionPhase.Completed;


    // results
    public List<Item> LootCollected { get; } = [];
    private List<string> EventsLog { get; } = [];


    // methods
    public void IncrementTime(int minutes)
    {
        MinutesElapsedTotal += minutes;
        MinutesSpentAtLocation += minutes;
    }

    public bool ReadyToAdvanceLocation()
    {
        if (CurrentPhase == ExpeditionPhase.NotStarted) return true;
        else if (CurrentPhase == ExpeditionPhase.TravelingOut || CurrentPhase == ExpeditionPhase.TravelingBack)
        {
            return MinutesSpentAtLocation >= TimeToTraverseLocation();
        }
        else if (CurrentPhase == ExpeditionPhase.Working) return MinutesSpentAtLocation >= WorkTimeMinutes;
        else if (CurrentPhase == ExpeditionPhase.Completed) return false;
        return false;
    }
    private int TimeToTraverseLocation()
    {
        return TravelProcessor.GetTraversalMinutes(CurrentLocation, player);
    }

    public void AdvancePath()
    {
        CurrentIndex++;
        MinutesSpentAtLocation = 0;
    }

    public void CancelExpedition()
    {
        // get the distance back
        CurrentIndex = DestinationIndex + Math.Abs(DestinationIndex - CurrentIndex);
        if (CurrentIndex == DestinationIndex)
        {
            AdvancePath();
        }
        MinutesSpentAtLocation = GetProgressBackForLocation();
    }

    private int GetProgressBackForLocation()
    {
        if (CurrentPhase == ExpeditionPhase.TravelingOut)
            return TimeToTraverseLocation() - MinutesSpentAtLocation; // return the way we came
        else if (CurrentPhase == ExpeditionPhase.TravelingBack)
            return MinutesSpentAtLocation; // already heading back
        else
            return 0;
    }

    // public string GetSummaryNotes()
    // {
    //     string notes = "";
    //     if (DetectionRisk > .2)
    //     {
    //         notes += "High detection risk. ";
    //     }
    //     if (ExposureFactor > .8)
    //     {
    //         notes += "The route is exposed to the weather.";
    //     }
    //     return notes;
    // }

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
            ExpeditionPhase.TravelingOut => $"traveling to {Path[DestinationIndex].Name}",
            ExpeditionPhase.Working => Type switch
            {
                ExpeditionType.Forage => "foraging",
                ExpeditionType.Hunt => "hunting",
                ExpeditionType.Explore => "exploring",
                ExpeditionType.Gather => "gathering",
                _ => "working"
            },
            ExpeditionPhase.TravelingBack => $"returning to {Path.Last().Name}",
            ExpeditionPhase.Completed => "completed",
            _ => "unknown"
        };
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
        while (!ReadyToAdvanceLocation())
        {
            IncrementTime(1);
            t++;

            // check for events, encounters, etc.
            evt = GameEventRegistry.GetEventOnTick(ctx);

            if (evt is not null || ReadyToAdvanceLocation())
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
        var feature = CurrentLocation.GetFeature<ForageFeature>() ?? throw new InvalidOperationException("Can't forage here.");
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
        var feature = CurrentLocation.Features
                .OfType<HarvestableFeature>()
                .FirstOrDefault(f => f.IsDiscovered && f.HasAvailableResources());

        if (feature is null)
        {
            AddLog("There's nothing left to harvest here.");
            AdvancePath();
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
        MinutesElapsedTotal += minutes;
    }
}