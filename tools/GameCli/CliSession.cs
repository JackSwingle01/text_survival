using System.Text.Json;
using text_survival.Actions;
using text_survival.Core;
using text_survival.Desktop.UI;
using text_survival.Environments.Grid;
using text_survival.Persistence;
using text_survival.UI;

namespace text_survival.GameCli;

/// <summary>One persistent game and its suspended async call stack. No work runs while reading input.</summary>
public sealed class CliSession : IDisposable
{
    private readonly FrameScheduler _scheduler = new();
    private readonly SynchronizationContext? _previous;
    private Task<bool> _game;
    public GameContext Context { get; private set; }
    public CliUi Ui { get; private set; }
    public bool Exiting { get; private set; }
    public bool IsBusy => !_game.IsCompleted && !Ui.Waiting;
    public CliSession(GameContext context, string sessionId)
    {
        CliLaunchOptions.ValidateSession(sessionId);
        _previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(_scheduler);
        Context = context; Context.SessionId = sessionId;
        Ui = new(Context); Context.Ui = Ui;
        _game = new GameRunner(Context).RunAsync();
        Advance();
    }
    public void Dispose() => SynchronizationContext.SetSynchronizationContext(_previous);
    private void Advance()
    {
        // Bounded pumping also lets an incapacitated run report progress without hanging the caller.
        for (int i = 0; i < 5000 && !Ui.Waiting && !_game.IsCompleted; i++)
        { Ui.Tick(); _scheduler.Pump(); }
        if (_game.IsFaulted) _game.GetAwaiter().GetResult();
        if (_game.IsCompletedSuccessfully && _game.Result)
        {
            string sessionId = Context.SessionId!;
            Context = GameContext.CreateNewGame(); Context.SessionId = sessionId;
            Ui = new(Context); Context.Ui = Ui;
            _game = new GameRunner(Context).RunAsync();
        }
        Ui.Render(); // Initializes action callbacks and default screen selections.
    }
    // ValueTuple members are fields, which the protocol serializer intentionally omits.
    private object[] LogEntries(int count) => Context.Log.Entries.TakeLast(count)
        .Select(entry => (object)new { entry.Text, entry.Level, entry.Timestamp }).ToArray();

    public object Look() => new {
        session = Context.SessionId, screen = _game.IsCompleted ? "ended" : Ui.Screen,
        status = Observations.Summary(Context), view = Ui.Render(),
        progress = Ui.Progress.Select(p => new { p.Kind, p.Status, p.Progress, p.SimulatedMinutes, p.TotalMinutes, p.Sections }),
        completed = Ui.CompletedProgress.ToArray(), log = LogEntries(8)
    };
    public object Execute(string command, string? input = null)
    {
        var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new ArgumentException("Empty command. Use help.");
        string rest = command.Trim().Contains(' ') ? command.Trim()[(command.Trim().IndexOf(' ') + 1)..] : "";
        switch (parts[0].ToLowerInvariant())
        {
            case "help": return Help;
            case "look": return Look();
            case "status": return Observations.Status(Context);
            case "weather": return Observations.Weather(Context);
            case "log": return LogEntries(parts.Length == 1 ? 30 : Integer(parts[1], 1, 10000));
            case "map": return Observations.Map(Context, parts.Length == 1 || parts[1] == "all" ? 3 : Integer(parts[1], 0, 1000), parts.Length > 1 && parts[1] == "all");
            case "inspect":
                if (parts.Length < 2) throw new ArgumentException("Usage: inspect x,y [x,y ...]");
                return parts.Skip(1).Select(ParsePosition).Select(p => Observations.Inspect(Context, p)).ToArray();
            case "combat": return Observations.Combat(Context);
            case "unit": return parts.Skip(1).Select(s => Observations.Unit(Context, Integer(s, 0, int.MaxValue))).ToArray();
            case "actions": return HudActions.Build(Context).Select(a => new { a.Id, a.Label, a.Enabled, a.DisabledReason,
                availableNow = Ui.Screen == "map" && a.Enabled }).ToArray();
            case "save":
                if (Ui.Screen != "map") throw new ArgumentException("Save at a map decision so no pending action or choice is lost.");
                var (success, error) = SaveManager.Save(Context);
                if (!success) throw new InvalidOperationException(error);
                return new { saved = Context.SessionId };
            case "quit": Exiting = true; return new { ended = true, saved = false };
        }
        Ui.CompletedProgress.Clear();
        switch (parts[0].ToLowerInvariant())
        {
            case "click": case "choose":
                var frame = Ui.Render();
                var matches = frame.Controls.Where(c => c.Id == rest || c.Label == rest).ToArray();
                if (matches.Length != 1) throw new ArgumentException(matches.Length == 0 ? "Unknown control. Use an ID or exact label from look." : "Ambiguous label; use the full control ID.");
                if (!matches[0].Enabled) throw new ArgumentException("That control is disabled.");
                Ui.Render(matches[0].Id, input); break;
            case "set":
                if (input == null) throw new ArgumentException("Use JSON: {\"command\":\"set CONTROL_ID\",\"value\":\"text\"}");
                var fields = Ui.Render().Controls.Where(c => c.Id == rest || c.Label == rest).ToArray();
                if (fields.Length != 1 || fields[0].Kind != "text") throw new ArgumentException("Unknown or ambiguous text field; use its ID.");
                var field = fields[0];
                Ui.Render(field.Id, input); break;
            case "close": Ui.Close(); break;
            case "act":
                var action = HudActions.Build(Context).SingleOrDefault(a => a.Id.Equals(rest, StringComparison.OrdinalIgnoreCase));
                if (action == null) throw new ArgumentException("Unknown action. Use actions.");
                if (!action.Enabled) throw new ArgumentException(action.DisabledReason);
                Ui.Act(action.Payload); break;
            case "travel":
                if (parts.Length is < 2 or > 3) throw new ArgumentException("Usage: travel x,y [quick|careful]");
                var destination = ParsePosition(parts[1]);
                var travel = TravelInspection.Build(Context, (destination.X, destination.Y));
                if (travel.Actions.Count == 0) throw new ArgumentException(travel.Reason);
                string? pace = parts.Length == 3 ? parts[2] : null;
                if (pace != null && pace is not "quick" and not "careful") throw new ArgumentException("Pace must be quick or careful.");
                Ui.Act(new PlayerAction.Travel(destination.X, destination.Y, pace)); break;
            case "move":
                if (parts.Length != 2) throw new ArgumentException("Usage: move x,y (combat coordinates)");
                var target = ParsePosition(parts[1]);
                if (target.X < 0 || target.Y < 0 || target.X >= text_survival.Combat.CombatScenario.MAP_SIZE || target.Y >= text_survival.Combat.CombatScenario.MAP_SIZE)
                    throw new ArgumentException("Combat coordinate is out of bounds.");
                Ui.MoveCombat(new(null, target)); break;
            case "new": Ui.Act(new PlayerAction.NewGame()); break;
            case "step": if (Ui.Waiting) throw new ArgumentException("A decision is waiting; choose a control."); break;
            default: throw new ArgumentException("Unknown command. Use help.");
        }
        Advance(); return Look();
    }
    private static int Integer(string text, int min, int max) => int.TryParse(text, out int value) && value >= min && value <= max
        ? value : throw new ArgumentException($"Expected an integer between {min} and {max}.");
    private static GridPosition ParsePosition(string text)
    {
        var xy = text.Split(',');
        if (xy.Length != 2 || !int.TryParse(xy[0], out int x) || !int.TryParse(xy[1], out int y)) throw new ArgumentException("Coordinates must be x,y.");
        return new(x, y);
    }
    public static object Help => new {
        agentMode = "start [--seed N] [--session cli_NAME] [--load cli_NAME]; send SESSION \"command\" [--value TEXT] [--request ID] [--revision N]; poll SESSION REQUEST_ID [RUN]; peek SESSION; stop SESSION. Each agent command exits without waiting for gameplay. interactive [options] opts into blocking stdin/stdout mode. start/send/stop return receipts; poll returns results; peek returns the last snapshot and lifecycle state.",
        commands = new[] { "look", "status", "weather", "log [count]", "map [radius|all]", "inspect x,y [x,y ...]", "actions", "act ACTION_ID",
            "travel x,y [quick|careful]", "click CONTROL_ID_OR_EXACT_LABEL", "choose CONTROL_ID_OR_EXACT_LABEL", "set CONTROL_ID (JSON value required)",
            "close", "combat", "unit ID [ID ...]", "move x,y", "save", "new", "step", "quit" },
        protocol = "One command per line, plain text or JSON {command,value}. One JSON response per line. Use control IDs from the latest look; labels must match exactly and be unambiguous. set changes a text field; click OK commits it.",
        observations = "Read-only commands cost no game time. Trees, tabs, combo options, and tooltips are expanded in text. Map and combat coordinates are separate. Game time advances through real actions; events wait for explicit choices.",
        saves = "Only isolated cli_ sessions are written. save requires a map decision. quit and EOF do not save. Restart with --load cli_NAME. Visual camera, zoom, and audio controls are desktop-only."
    };
}
