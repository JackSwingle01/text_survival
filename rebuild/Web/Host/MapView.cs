using System.Text;

namespace Survival.Web;

/// MapDto, the map the page's renderer draws, from one of two sides. Remembered: a person's memory, what
/// the player's page shows (the map never reads the truth about a place out of sight: clean-room
/// decision 7). Truth: every tile explored and in sight, what observer mode shows. Both sides answer
/// the same per-tile rules here (terrain codes, ground, frozen water, edges, names), so anything the map
/// gains reaches both.
static class MapView
{
    /// The terrain codes of MapDto.Known: one character each.
    public const string TerrainCodes = "FCPHWMRXD"; // Forest Clearing Plain Hills Water Marsh Rock Mountain DeepWater
    public const char CaveFloorCode = 'c', CaveMouthCode = 'e', UnknownCode = '.';

    /// Tracks are read within this many tiles of the player, in sight: nobody reads prints from far off.
    public const int TrackReadTiles = 3;

    /// A tile the person has seen, or stands on.
    public static bool Explored(World world, Agent person, TileId t) =>
        t == person.Tile || Memories.Of(person).Explored is { } e && Map.Of(world).InBounds(t) && e.Contains(t);

    /// The map as this person knows it.
    public static MapDto Remembered(World world, Agent person)
    {
        var m = Map.Of(world);
        var memory = Memories.Of(person);
        bool explored(TileId t) => Explored(world, person, t);
        var visible = Sight.InView(world, person); // the view the person's looks already cast
        var inSight = visible.ToHashSet();

        var places = new List<PlaceMarkDto>();
        // The fire underfoot is the one the fire panel shows (FireHere): memory catches up with it only on
        // the next minute awake, so on waking it would still hold the fire slept beside.
        var pitIdHere = Fire.PitAt(world, person.Tile);
        var pitHere = pitIdHere is FireId pid ? world.Fires[pid] : null;
        var stationHere = pitIdHere is FireId sid ? Stations.MarkAt(world, sid) : null;
        foreach (var (t, mem) in memory.Places)
        {
            bool underfoot = t == person.Tile;
            var fire = underfoot ? (pitHere is { } ph ? Fire.Phase(ph) : null) : mem.Fire;
            var station = underfoot ? stationHere : mem.Station;
            bool notable = fire != null || station != null || mem.Shelter != null || mem.Carcasses is { Count: > 0 } || mem.Pile is { Count: > 0 } || mem.Danger != Danger.None;
            if (!notable) continue;
            // The fuel was reckoned beside the fire; age it from then. A fire seen burning since keeps that reckoning.
            double? hoursLeft = underfoot ? (pitHere != null && Fire.IsActive(pitHere) ? Math.Round(Fire.FuelHours(pitHere), 1) : null)
                : mem.FireHours is double h && mem.VisitedMinute is long v ? Math.Max(0, Math.Round(h - (world.Minute - v) / 60.0, 1)) : null;
            places.Add(new PlaceMarkDto(t.X, t.Y, underfoot ? world.Minute : mem.SeenMinute, fire is { } f ? Names.Title(f) : null, hoursLeft, mem.Shelter,
                mem.Carcasses is { Count: > 0 }, mem.Pile is { Count: > 0 }, mem.Danger.HasFlag(Danger.Den), mem.Danger.HasFlag(Danger.Hurt), station is { } sm ? Words.Code(sm) : null));
        }
        var sightings = memory.Sightings.Where(s => s.Key != person.Id)
            .Select(s => new SightingDto(s.Value.Tile.X, s.Value.Tile.Y, Words.SpeciesIcon(s.Value.Species), s.Value.Count, s.Value.Minute)).ToList();

        var tracks = new List<TrackDto>();
        foreach (var t in visible)
            if (Math.Abs(t.X - person.Tile.X) <= TrackReadTiles && Math.Abs(t.Y - person.Tile.Y) <= TrackReadTiles)
                tracks.AddRange(TracksOn(world, t));

        // In sight, the ground as it is; out of sight, as the person last saw it.
        RememberedGround seen(TileId t) => memory.Explored?.At(t) ?? throw new InvalidOperationException($"Tile {t} is out of sight and was never seen");
        long minuteOf(TileId t) => inSight.Contains(t) ? world.Minute : seen(t).SeenMinute;
        SeenSurface groundOf(TileId t) => inSight.Contains(t) ? SeenSurface.Of(Ground.Within(world, t)) : seen(t).Surface;
        var trails = Memories.Trails(world, person, inSight).Select(t => Trail(t.Edge, t.Tier)).ToList();
        return Build(world, explored, minuteOf, groundOf, [.. visible.Select(t => t.Y * m.Width + t.X).Order()], places, sightings, tracks, trails);
    }

    /// The map as it truly is: every tile explored and in sight, every print readable, every worn route.
    /// No places and no sightings: an observer's places and actors come with its view (Observer), which
    /// changes by the minute while this map goes with the ground.
    public static MapDto Truth(World world)
    {
        var m = Map.Of(world);
        var tracks = new List<TrackDto>();
        foreach (var (t, s) in world.Tiles)
            if (s.Tracks != null) tracks.AddRange(TracksOn(world, t));
        var trails = new List<TrailDto>();
        foreach (var edge in world.Trails.Keys) // edge order: Trails is sorted
            if (Tracks.TierOf(world, edge.A, edge.B) is var tier and not TrailTier.None) trails.Add(Trail(edge, tier));
        return Build(world, _ => true, _ => world.Minute, t => SeenSurface.Of(Ground.Within(world, t)), [.. Enumerable.Range(0, m.Width * m.Height)],
            [], [], tracks, trails);
    }

    /// The per-tile rules both sides share. explored: whether the tile is on the map at all; minuteOf: the
    /// minute it is drawn as of (now, or when last seen); groundOf: its ground as of then, asked only of
    /// passable tiles.
    static MapDto Build(World world, Func<TileId, bool> explored, Func<TileId, long> minuteOf, Func<TileId, SeenSurface> groundOf, IReadOnlyList<int> visible,
        List<PlaceMarkDto> places, List<SightingDto> sightings, List<TrackDto> tracks, List<TrailDto> trails)
    {
        var m = Map.Of(world);
        var known = new StringBuilder(m.Width * m.Height);
        var surface = new List<SurfaceDto>();
        var frozen = new List<int>();
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++)
            {
                var t = new TileId(x, y);
                if (!explored(t)) { known.Append(UnknownCode); continue; }
                known.Append(TerrainCode(m, t));
                if (m.TerrainAt(t) is Terrain.Water or Terrain.DeepWater && m.CaveAt(t) == Cave.None && Map.IsFrozen(m, t, minuteOf(t)))
                    frozen.Add(y * m.Width + x);
                if (m.TerrainAt(t).IsPassable() && SurfaceOf(groundOf(t), y * m.Width + x) is { } s) surface.Add(s);
            }

        var named = new List<NamedDto>();
        foreach (var (t, site) in m.Sites) if (explored(t)) named.Add(new NamedDto(t.X, t.Y, site.Name));
        foreach (var (t, i) in m.PassStageIndex) if (explored(t)) named.Add(new NamedDto(t.X, t.Y, Map.PassStages[i].Name));
        named.Add(new NamedDto(m.Camp.X, m.Camp.Y, Map.Describe(m, m.Camp)));

        var edges = new List<EdgeDto>();
        foreach (var ((a, b), e) in m.Edges)
            if (e is EdgeType.Ravine or EdgeType.Cliff && (explored(a) || explored(b)))
                edges.Add(new EdgeDto(a.X, a.Y, b.X, b.Y, Words.Code(e)));
        edges.Sort((p, q) => (p.Ay, p.Ax, p.By, p.Bx).CompareTo((q.Ay, q.Ax, q.By, q.Bx)));

        return new MapDto(m.Width, m.Height, known.ToString(), visible, m.Camp.X, m.Camp.Y,
            places, sightings, tracks, named, surface, frozen, edges, trails);
    }

    static IEnumerable<TrackDto> TracksOn(World world, TileId t) => Tracks.Read(world, t).Select(s =>
        new TrackDto(t.X, t.Y, Names.Of(s.Maker), Narration.Reading(s), s.AgeMinutes, s.Traffic, Words.Code(s.Heading), Words.Code(s.Pattern),
            Math.Round(s.FreshnessPct, 2)));

    static TrailDto Trail(EdgeId e, TrailTier tier) => new(e.A.X, e.A.Y, e.B.X, e.B.Y, Words.Code(tier));

    /// A tile's code in MapDto.Known: a cave's floor or mouth, else its terrain.
    static char TerrainCode(GeneratedMap m, TileId t) =>
        m.CaveAt(t) == Cave.Floor ? CaveFloorCode : m.CaveAt(t) == Cave.Entrance ? CaveMouthCode : TerrainCodes[(int)m.TerrainAt(t)];

    /// A tile's ground as the page draws it, or null for bare, dry, untrodden ground (left out of MapDto.Surface).
    static SurfaceDto? SurfaceOf(SeenSurface g, int index)
    {
        double snowCm = Math.Round(g.SnowDepthM * 100, 1), waterCm = Math.Round(g.WaterDepthM * 100, 1), mud = Math.Round(g.MudLevel, 2);
        double trodden = g.TroddenLevel >= Tracks.TroddenTileAt ? Math.Round(g.TroddenLevel, 2) : 0;
        return snowCm > 0 || waterCm > 0 || mud > 0 || trodden > 0 ? new SurfaceDto(index, snowCm, g.Crusted, mud, waterCm, g.Iced, trodden) : null;
    }
}
