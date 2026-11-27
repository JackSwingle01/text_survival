using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Survival;

namespace WebSpike;

/// The whole boundary between a UI and the game. Strings and bytes in, JSON views out: no Sim type
/// crosses it, so the same five calls can sit behind JSExport (in-browser wasm) or HTTP (a server).
public sealed class GameHost
{
    public const int Radius = 7; // tiles drawn around the player: a 15x15 window

    World? _world;
    readonly List<string> _log = [];

    World W => _world ?? throw new InvalidOperationException("No game: call NewGame or Load first");

    public string NewGame(int seed)
    {
        _world = World.New(seed);
        _log.Clear();
        _log.Add($"Seed {seed}. You are alone at the camp.");
        return View();
    }

    /// One player action, as JSON: {"kind":"wait"|"rest"|"sleep","minutes":60} or {"kind":"walk","dir":"N"}.
    public string Act(string actionJson)
    {
        var a = JsonSerializer.Deserialize(actionJson, HostJson.Default.ActionDto)
                ?? throw new JsonException("No action");
        var (activity, minutes) = a.Kind switch
        {
            "wait" => ((Activity)new Wait(AgentId.Player), a.Minutes ?? 60),
            "rest" => (new Rest(AgentId.Player), a.Minutes ?? 60),
            "sleep" => (new Sleep(AgentId.Player), a.Minutes ?? 480),
            "walk" => (new Travel(AgentId.Player, Heading(a.Dir)), 24 * 60), // a crossing stops itself on arrival
            _ => throw new ArgumentException($"Unknown action kind '{a.Kind}'"),
        };
        var result = Sim.Step(W, activity, minutes);
        _log.Add($"{a.Kind}{(a.Dir != null ? " " + a.Dir : "")}: {result.MinutesElapsed} min" +
                 (result.InterruptedReason != null ? $" ({result.InterruptedReason})" : ""));
        foreach (var e in result.Events.TakeLast(5)) _log.Add($"  {e.GetType().Name}");
        while (_log.Count > 30) _log.RemoveAt(0);
        return View();
    }

    public string View() => JsonSerializer.Serialize(BuildView(), HostJson.Default.ViewDto);

    public byte[] Save() => Encoding.UTF8.GetBytes(Survival.Save.ToJson(W));

    public string Load(byte[] bytes)
    {
        _world = Survival.Save.FromJson(Encoding.UTF8.GetString(bytes));
        _log.Add("Loaded.");
        return View();
    }

    static Direction Heading(string? d) => d switch
    {
        "N" => Direction.North,
        "E" => Direction.East,
        "S" => Direction.South,
        "W" => Direction.West,
        _ => throw new ArgumentException($"No direction '{d}'"),
    };

    ViewDto BuildView()
    {
        var w = W;
        var map = Map.Of(w);
        var p = w.Agent(AgentId.Player);
        var b = p.Body;
        var sky = Weather.At(w, w.Minute);
        int mod = Weather.MinuteOfDay(w.Minute);

        var tiles = new List<string>();
        for (int dy = -Radius; dy <= Radius; dy++)
            for (int dx = -Radius; dx <= Radius; dx++)
            {
                var t = new TileId(p.Tile.X + dx, p.Tile.Y + dy);
                tiles.Add(!map.InBounds(t) ? "" : map.CaveAt(t) != Cave.None ? "cavefloor" : map.TerrainAt(t).ToString().ToLowerInvariant());
            }

        var marks = new List<MarkDto>();
        foreach (var agent in w.Agents.Values)
        {
            if (agent.Id == AgentId.Player) continue;
            int dx = agent.Tile.X - p.Tile.X, dy = agent.Tile.Y - p.Tile.Y;
            if (Math.Abs(dx) <= Radius && Math.Abs(dy) <= Radius && Sim.IsAlive(agent))
                marks.Add(new MarkDto(dx, dy, agent.Species == Species.Human ? "npc" : "animal:" + agent.Species.ToString().ToLowerInvariant()));
        }
        foreach (var f in w.Fires.Values)
        {
            if (f.CarriedBy != null) continue;
            var ft = f.Tile;
            int dx = ft.X - p.Tile.X, dy = ft.Y - p.Tile.Y;
            if (Math.Abs(dx) <= Radius && Math.Abs(dy) <= Radius)
                marks.Add(new MarkDto(dx, dy, Fire.Burns(Fire.Phase(f)) ? "fire" : "embers"));
        }
        {
            int dx = map.Camp.X - p.Tile.X, dy = map.Camp.Y - p.Tile.Y;
            if (Math.Abs(dx) <= Radius && Math.Abs(dy) <= Radius) marks.Add(new MarkDto(dx, dy, "shelter"));
        }

        return new ViewDto(
            Clock: new ClockDto(Weather.DayNumber(w.Minute), $"{mod / 60}:{mod % 60:00}", Weather.SeasonAt(w.Minute).ToString(),
                sky.Condition.ToString(), Math.Round(sky.TemperatureF), Math.Round(Exposure.OpenAirF(w, p.Tile, sky)), Math.Round(sky.WindMph)),
            Vitals: new VitalsDto(
                Warmth: Math.Clamp((b.CoreTempF - 90.0) / (Body.BaseCoreTempF - 90.0), 0, 1),
                Food: Math.Clamp(b.CaloriesPct, 0, 1), Water: Math.Clamp(b.HydrationPct, 0, 1), Rest: Math.Clamp(b.EnergyPct, 0, 1),
                CoreTempF: Math.Round(b.CoreTempF, 1), Effects: [.. Deprivations.Of(b).Select(d => d.Kind.ToString()), .. b.Effects.Select(e => e.Kind.ToString())]),
            Place: Map.Describe(map, p.Tile),
            Radius: Radius, X: p.Tile.X, Y: p.Tile.Y,
            Tiles: tiles,
            Marks: marks,
            Actions:
            [
                new("Wait 60 min", """{"kind":"wait","minutes":60}"""),
                new("Rest 60 min", """{"kind":"rest","minutes":60}"""),
                new("Walk N", """{"kind":"walk","dir":"N"}"""),
                new("Walk S", """{"kind":"walk","dir":"S"}"""),
                new("Walk E", """{"kind":"walk","dir":"E"}"""),
                new("Walk W", """{"kind":"walk","dir":"W"}"""),
            ],
            Log: [.. _log],
            Outcome: Map.Outcome(w).ToString());
    }
}

public sealed record ActionDto(string Kind, int? Minutes = null, string? Dir = null);
public sealed record ClockDto(int Day, string Time, string Season, string Sky, double AirF, double FeelsF, double WindMph);
public sealed record VitalsDto(double Warmth, double Food, double Water, double Rest, double CoreTempF, List<string> Effects);
public sealed record MarkDto(int Dx, int Dy, string Kind);
public sealed record ActionOption(string Label, string Json);
public sealed record ViewDto(ClockDto Clock, VitalsDto Vitals, string Place, int Radius, int X, int Y, List<string> Tiles,
    List<MarkDto> Marks, List<ActionOption> Actions, List<string> Log, string Outcome);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ActionDto))]
[JsonSerializable(typeof(ViewDto))]
[JsonSerializable(typeof(BenchResult))]
public partial class HostJson : JsonSerializerContext;
