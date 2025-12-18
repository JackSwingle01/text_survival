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
    public List<string> CollectionLog { get; } = [];  // Descriptions of collected resources for end summary
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
        int remainingTime = GetRemainingTimeInPhase();
        var tickResult = GameEventRegistry.RunTicks(ctx, remainingTime);

        IncrementTime(tickResult.MinutesElapsed);
        AddSegmentUpdate(tickResult.MinutesElapsed);

        if (CurrentPhase == ExpeditionPhase.Working)
        {
            DoWork(tickResult.MinutesElapsed, ctx);
        }
        return new SegmentResult(tickResult.MinutesElapsed, tickResult.TriggeredEvent);
    }

    private int GetRemainingTimeInPhase()
    {
        if (CurrentPhase == ExpeditionPhase.Working)
            return WorkTimeMinutes - MinutesSpentAtLocation;
        else
            return TimeToTraverseLocation() - MinutesSpentAtLocation;
    }

    public void DoWork(int minutes, GameContext ctx)
    {
        if (Type == ExpeditionType.Forage)
        {
            DoForageWork(minutes, ctx);
        }
        else if (Type == ExpeditionType.Gather)
        {
            DoHarvestWork(minutes, ctx);
        }
        // Note: Hunt type is handled by ExpeditionRunner.RunHuntingWorkPhase()
        // which runs interactively instead of through this automated work phase
    }

    private void DoForageWork(int minutes, GameContext ctx)
    {
        var feature = CurrentLocation.GetFeature<ForageFeature>() ?? throw new InvalidOperationException("Can't forage here.");
        var found = feature.Forage(minutes / 60.0);

        if (!found.IsEmpty)
        {
            // Add directly to player inventory
            ctx.Inventory.Add(found);

            // Log descriptions for end summary
            CollectionLog.AddRange(found.Descriptions);

            string timeText = minutes == 60 ? "1 hour" : $"{minutes} minutes";
            AddLog($"You spent {timeText} searching and found: {string.Join(", ", found.Descriptions)}");
        }
        else
        {
            string timeText = minutes == 60 ? "1 hour" : $"{minutes} minutes";
            AddLog($"You spent {timeText} searching but found nothing.");
        }
    }

    private void DoHarvestWork(int minutes, GameContext ctx)
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

        var found = feature.Harvest(minutes);
        if (!found.IsEmpty)
        {
            // Add directly to player inventory
            ctx.Inventory.Add(found);

            // Log descriptions for end summary
            CollectionLog.AddRange(found.Descriptions);

            AddLog($"You spent {minutes} minutes harvesting and gathered: {string.Join(", ", found.Descriptions)}");
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