using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival.Actions.Expeditions;

public enum ExpeditionState { Traveling, Working }

public class Expedition(Location startLocation, Player player)
{
    private readonly Player _player = player;

    // Travel tracking
    public Stack<Location> TravelHistory { get; } = new Stack<Location>([startLocation]);
    public Location CurrentLocation => TravelHistory.Peek();
    public bool IsAtCamp => TravelHistory.Count == 1;

    // State
    public ExpeditionState State { get; set; } = ExpeditionState.Traveling;

    // Time tracking
    public int MinutesElapsedTotal { get; private set; } = 0;

    // Logs
    public List<string> CollectionLog { get; } = [];
    private List<string> _eventsLog = [];

    public void MoveTo(Location location, int travelTimeMinutes)
    {
        // If moving to previous location, pop instead of push (backtracking)
        if (location == TravelHistory.ElementAtOrDefault(1))
        {
            TravelHistory.Pop();
        }
        else
        {
            TravelHistory.Push(location);
        }
        MinutesElapsedTotal += travelTimeMinutes;
    }

    public void AddTime(int minutes)
    {
        MinutesElapsedTotal += minutes;
    }

    public int GetEstimatedReturnTime(Inventory? inventory = null)
    {
        return TravelProcessor.GetPathMinutes(TravelHistory.ToList(), _player, inventory);
    }

    public void AddLog(string log)
    {
        if (!string.IsNullOrEmpty(log))
        {
            _eventsLog.Add(log);
        }
    }

    public List<string> FlushLogs()
    {
        var logs = _eventsLog.ToList();
        _eventsLog.Clear();
        return logs;
    }

    public string GetStateDisplayName()
    {
        return State switch
        {
            ExpeditionState.Traveling => $"traveling near {CurrentLocation.Name}",
            ExpeditionState.Working => $"working at {CurrentLocation.Name}",
            _ => "unknown"
        };
    }
}
