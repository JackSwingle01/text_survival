using System.Text;
using System.Text.Json;

namespace Survival.Web;

/// Observer mode (GLOSSARY), development only: a world with no player in it, drawn as it truly is.
/// New, then Advance at whatever rate the page runs; Spawn puts a thing on a tile; Inspect describes
/// a tile as a person standing there would find it. JSON in and out like GameHost, and apart from it:
/// nothing here reads anyone's Memory, and nothing the player's boundary does reads this.
///
/// The player is taken out of the world, so Sim.Advance runs NPCs and herds on their own minds. What
/// looks the player up (Map.Outcome, Verbs.At, ViewBuilder) is never called on this world.
public sealed class Observer
{
    World? _world;
    long? _groundSentMinute;
    readonly List<ObserverLogDto> _log = [];

    public const int MaxLog = 200, LogShown = 60;

    /// What the spawn tool can put on a tile: one row per kind, so a herd or an item is a new row. Each
    /// makes its thing on a passable tile; the tile is checked before it is asked.
    sealed record Spawnable(string Id, string Label, Action<World, TileId> Make);

    static readonly Spawnable[] Spawnables =
    [
        new("npc", "Person (NPC)", (w, t) => Npc.Spawn(w, t)),
    ];

    World W => _world ?? throw new InvalidOperationException("No world: call New first");

    /// Read access for tests; the page never sees it.
    internal World World => W;

    /// A new world from this seed with the player taken out. Returns what never changes (ObserverMapDto).
    public string New(int seed)
    {
        _world = World.New(seed);
        _world.Agents.Remove(AgentId.Player);
        _groundSentMinute = null;
        _log.Clear();
        var m = Map.Of(_world);
        var terrain = new StringBuilder(m.Width * m.Height);
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++) terrain.Append(ViewBuilder.TerrainCode(m, new TileId(x, y)));
        var named = new List<NamedDto>();
        foreach (var (t, site) in m.Sites) named.Add(new NamedDto(t.X, t.Y, site.Name));
        foreach (var (t, i) in m.PassStageIndex) named.Add(new NamedDto(t.X, t.Y, Map.PassStages[i].Name));
        named.Add(new NamedDto(m.Camp.X, m.Camp.Y, Map.Describe(m, m.Camp)));
        return JsonSerializer.Serialize(new ObserverMapDto(m.Width, m.Height, terrain.ToString(), m.Camp.X, m.Camp.Y, named,
            [.. Spawnables.Select(s => new SpawnableDto(s.Id, s.Label))]),
            HostJson.Default.ObserverMapDto);
    }

    public string View()
    {
        var w = W;
        return JsonSerializer.Serialize(new ObserverViewDto(w.Minute, ViewBuilder.ClockOf(w, Weather.At(w, w.Minute)), Actors(), Places(),
            GroundIfChanged(), [.. _log.TakeLast(LogShown)]), HostJson.Default.ObserverViewDto);
    }

    /// Runs the world on for this many minutes with nobody acting for the player (Sim.Advance), then
    /// brings every pit up to the clock: a fire lives only when someone near it catches it up, and
    /// here nobody may be.
    public string Advance(int minutes)
    {
        var w = W;
        var events = Sim.Advance(w, minutes);
        Fire.CatchUpAll(w, events);
        foreach (var e in events)
            if (Journal.Files(e) && Narration.Of(w, e) is { } line) _log.Add(new ObserverLogDto(e.Tile.X, e.Tile.Y, Words.Log(line)));
        if (_log.Count > MaxLog) _log.RemoveRange(0, _log.Count - MaxLog);
        return View();
    }

    /// Puts one of Spawnables on the tile. Unknown kinds, tiles off the map and ground nothing can
    /// stand on are refused.
    public string Spawn(string kind, int x, int y)
    {
        var w = W;
        var m = Map.Of(w);
        var what = Spawnables.FirstOrDefault(s => s.Id == kind)
            ?? throw new ArgumentException($"Nothing to spawn called '{kind}'; there are {string.Join(", ", Spawnables.Select(s => s.Id))}");
        var t = new TileId(x, y);
        if (!m.InBounds(t)) throw new ArgumentException($"Tile {x},{y} is off the map");
        if (!m.TerrainAt(t).IsPassable()) throw new ArgumentException($"Nothing can stand on {Names.Of(m.TerrainAt(t))} at {x},{y}");
        what.Make(w, t);
        return View();
    }

    /// A tile as a person standing on it would find it, and who is there.
    public string Inspect(int x, int y)
    {
        var w = W;
        var m = Map.Of(w);
        var t = new TileId(x, y);
        if (!m.InBounds(t)) throw new ArgumentException($"Tile {x},{y} is off the map");
        var features = LocationFeatures.At(w, t).Select(f => new TileFeatureDto(f.Kind.ToString().ToLowerInvariant(), Verbs.NameOf(w, f),
            f.Kind == LocationFeatureKind.Pool && !w.Agents.Values.Any(a => a.Mind is PersonMind && Pools.Knows(w, a.Id, new PoolId(t, f.Index))))).ToList();
        var actors = w.Agents.Values.Where(a => a.Tile == t && Shown(a))
            .Select(a => new ActorDetailDto(a.Id.Value, Names.Cap(Cause.Describe(w, a.Id)), Doing(a), Insides(a))).ToList();
        var fire = Fire.PitAt(w, t) is FireId id ? ViewBuilder.FireOf(w.Fires[id]) : null;
        return JsonSerializer.Serialize(new TileDto(x, y, Map.Describe(m, t), Names.Title(m.TerrainAt(t)), Map.DiscoveryText(w, t), fire, features, actors),
            HostJson.Default.TileDto);
    }

    /// What is going on inside an actor, in lines: a person's stats by tier and effects; a herd's state and hunger.
    List<string> Insides(Agent a)
    {
        var w = W;
        var lines = new List<string>();
        if (a.Mind is HerdMind h)
        {
            lines.Add($"State: {Words.Of(h.State)} for {Ago(w.Minute - h.StateSinceMinute)}");
            lines.Add($"Members: {a.Members.Count(Body.IsAlive)} alive of {a.Members.Count}");
            lines.Add($"Fear: {h.Fear:P0}");
            if (h.LastFedMinute > HerdMind.Never) lines.Add($"Last fed: {Ago(w.Minute - h.LastFedMinute)} ago");
            lines.Add($"Territory: {h.Territory.Count} tiles");
            if (h.Foray is { } f) lines.Add($"Ranging to {f.X},{f.Y}");
            return lines;
        }
        var b = a.Body;
        var tiers = Tiers.Of(w, a.Id);
        lines.Add($"Warmth: {Words.Of(tiers.Warmth)} · body {b.CoreTempF:0.0}°F");
        lines.Add($"Food: {Words.Of(tiers.Food)} · {b.CaloriesKcal:0} kcal · fat {b.FatKg:0.0} kg");
        lines.Add($"Water: {Words.Of(tiers.Water)} · {b.HydrationPct:P0}");
        lines.Add($"Rest: {Words.Of(tiers.Rest)} · {b.EnergyPct:P0}");
        foreach (var e in b.Effects.Where(e => e.Severity > 0.01)) lines.Add($"{Names.Title(e.Kind)} {e.Severity:P0}");
        lines.Add($"Carrying {Items.CarriedKg(a):0.0} kg");
        if (a.Mind is NpcMind n) lines.Add($"Camp: {n.Camp.X},{n.Camp.Y}");
        return lines;
    }

    static string Ago(long minutes) => minutes < 60 ? $"{minutes} min" : minutes < 2880 ? $"{minutes / 60} h" : $"{minutes / 1440} d";

    // ---- the view ----

    List<ActorDto> Actors()
    {
        var w = W;
        var list = new List<ActorDto>();
        foreach (var (id, a) in w.Agents)
        {
            if (!Shown(a)) continue;
            var to = a.Crossing is { } c ? Map.Step(c.From, c.Dir) : (TileId?)null;
            list.Add(new ActorDto(id.Value, a.Mind is HerdMind ? "herd" : "npc", Words.SpeciesIcon(a.Species), Names.Cap(Cause.Describe(w, id)),
                a.Tile.X, a.Tile.Y, a.Members.Count, Doing(a), (a.Mind as HerdMind)?.State is HerdState s ? Words.Of(s) : null,
                a.Mind is HerdMind && Fauna.IsPredator(a.Species), to?.X, to?.Y, a.Crossing is { } cr ? Math.Round((double)cr.ElapsedMinutes / cr.TotalMinutes, 3) : null));
        }
        return list;
    }

    /// An actor on the map: a living person, or a herd with members left. A dead person is a body, listed on its tile.
    static bool Shown(Agent a) => a.Members.Count > 0 && (a.Mind is HerdMind || Sim.IsAlive(a));

    /// An activity in words: "travel north", "gather", or "deciding" between activities.
    static string Doing(Agent a) => Sim.ActivityOf(a) switch
    {
        null => "deciding",
        Travel t => $"travel {Names.Of(t.Dir)}",
        var act => string.Concat(act.GetType().Name.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString())),
    };

    List<PlaceMarkDto> Places()
    {
        var w = W;
        var marks = new SortedDictionary<TileId, PlaceMarkDto>();
        PlaceMarkDto At(TileId t) => marks.TryGetValue(t, out var p) ? p : new PlaceMarkDto(t.X, t.Y, w.Minute, null, null, null, false, false, false, false);
        foreach (var (_, f) in w.Fires)
            if (f.CarriedBy == null) // a torch goes with its carrier
                marks[f.Tile] = At(f.Tile) with { Fire = Names.Title(Fire.Phase(f)), FireHoursLeft = Fire.IsActive(f) ? Math.Round(Fire.FuelHours(f), 1) : null };
        foreach (var (t, s) in w.Shelters) marks[t] = At(t) with { Shelter = s.Name };
        foreach (var (t, s) in w.Tiles)
        {
            if (s.Carcasses is { Count: > 0 }) marks[t] = At(t) with { Carcass = true };
            if (s.Stash is { } st && (st.Kg.Count > 0 || st.Gear.Count > 0)) marks[t] = At(t) with { Stash = true };
        }
        return [.. marks.Values];
    }

    /// The whole map's ground, when Ground has stepped since the last view sent it.
    ObserverGroundDto? GroundIfChanged()
    {
        var w = W;
        long step = w.Minute / Ground.StepMinutes * Ground.StepMinutes;
        if (_groundSentMinute == step) return null;
        _groundSentMinute = step;
        var m = Map.Of(w);
        var surface = new List<SurfaceDto>();
        var frozen = new List<int>();
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++)
            {
                var t = new TileId(x, y);
                int i = y * m.Width + x;
                if (ViewBuilder.FrozenWater(m, t, w.Minute)) frozen.Add(i);
                if (m.TerrainAt(t).IsPassable() && ViewBuilder.SurfaceOf(Ground.SurfaceNow(w, t), i) is { } s) surface.Add(s);
            }
        return new ObserverGroundDto(w.Minute, surface, frozen);
    }
}
