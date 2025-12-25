// using text_survival.Actors.Player;
// using text_survival.Environments;

// namespace text_survival.Actions.Expeditions;

// public enum ExpeditionState { Traveling, Working }

// public class Expedition
// {
//     // Player reference - serialized with ReferenceHandler.Preserve handling circular refs
//     [System.Text.Json.Serialization.JsonInclude]
//     private Player _player = null!;

//     // Travel tracking
//     public Stack<Location> TravelHistory { get; set; } = new();

//     // Constructor for normal use
//     public Expedition(Location startLocation, Player player)
//     {
//         _player = player;
//         TravelHistory = new Stack<Location>([startLocation]);
//     }

//     // Parameterless constructor for deserialization
//     [System.Text.Json.Serialization.JsonConstructor]
//     public Expedition()
//     {
//     }

//     [System.Text.Json.Serialization.JsonIgnore]
//     public Location CurrentLocation => TravelHistory.Peek();

//     [System.Text.Json.Serialization.JsonIgnore]
//     public bool IsAtCamp => TravelHistory.Count == 1;

//     // State
//     public ExpeditionState State { get; set; } = ExpeditionState.Traveling;

//     // Time tracking
//     public int MinutesElapsedTotal { get; private set; } = 0;

//     // Logs
//     public List<string> CollectionLog { get; set; } = [];
//     private List<string> _eventsLog { get; set; } = [];

//     public void MoveTo(Location location, int travelTimeMinutes)
//     {
//         // If moving to previous location, pop instead of push (backtracking)
//         if (location == TravelHistory.ElementAtOrDefault(1))
//         {
//             TravelHistory.Pop();
//         }
//         else
//         {
//             TravelHistory.Push(location);
//         }
//         MinutesElapsedTotal += travelTimeMinutes;
//     }

//     public void AddTime(int minutes)
//     {
//         MinutesElapsedTotal += minutes;
//     }

//     public int GetEstimatedReturnTime(Inventory? inventory = null)
//     {
//         return TravelProcessor.GetPathMinutes(TravelHistory.ToList(), _player, inventory);
//     }

//     public void AddLog(string log)
//     {
//         if (!string.IsNullOrEmpty(log))
//         {
//             _eventsLog.Add(log);
//         }
//     }

//     public List<string> FlushLogs()
//     {
//         var logs = _eventsLog.ToList();
//         _eventsLog.Clear();
//         return logs;
//     }

//     public string GetStateDisplayName()
//     {
//         return State switch
//         {
//             ExpeditionState.Traveling => $"traveling near {CurrentLocation.Name}",
//             ExpeditionState.Working => $"working at {CurrentLocation.Name}",
//             _ => "unknown"
//         };
//     }

// }
