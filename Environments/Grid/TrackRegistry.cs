namespace text_survival.Environments.Grid;

/// <summary>
/// The kind of print left on the ground. Three, by shape rather than by identity: the
/// player's own boots and an NPC's are the same mark, and the player is trusted to
/// remember where they have been. What a print actually tells you is whether the thing
/// that passed walks on two feet, on pads, or on hooves.
/// </summary>
public enum TrackMaker
{
    Human,
    Paw,
    Hoof
}

/// <summary>
/// The sign left on one tile by one kind of traveller.
///
/// <see cref="Traffic"/> is how much has come through - individuals, accumulated across
/// passages and faded by weather in between, so three passes by one person and one pass
/// by three people read alike, which is also true on the ground.
/// <see cref="StampedErosion"/> is the world erosion accumulator's value when the sign
/// was last added to, which is what makes age weather-aware without storing any weather
/// history. See <see cref="TrackRegistry"/>.
/// </summary>
public readonly record struct Track(
    TrackMaker Maker,
    Direction Heading,
    double StampedErosion,
    double Traffic,
    double HeaviestIndividual)
{
    /// <summary>
    /// How deeply the ground is marked, and so how long the sign lasts. Linear in the
    /// weight of the heaviest thing through, sublinear in how many came - a mammoth
    /// leaves a deeper print than a fox, and a herd churns more than one animal, but
    /// twelve caribou do not last twelve times as long as one.
    /// </summary>
    public double Depth =>
        Math.Clamp(HeaviestIndividual * (0.6 + 0.4 * Math.Sqrt(Math.Max(1, Traffic))), 0.5, 3.0);
}

/// <summary>
/// Footprints left across the map, and the weather that erases them.
///
/// Rather than ageing every track every minute, the world keeps a single monotonic
/// erosion accumulator that advances faster in snow and wind. A track records the
/// accumulator's value when it was stamped, so its age is one subtraction - and a
/// blizzard that blew through while the player was elsewhere is already accounted
/// for, because it advanced the same counter. One addition per minute, whatever the
/// size of the map.
/// </summary>
public class TrackRegistry
{
    /// <summary>Erosion units a Depth-1 track survives. Tuned against the rates below.</summary>
    public const double BaseLifespanUnits = 2880;

    // Erosion per minute. Roughly: 4 days in dead calm cold, 17 hours in light snow,
    // 6 hours in a blizzard, for a single human's prints.
    private const double StillAirUnits = 0.5;
    private const double PrecipitationUnits = 6.0;
    private const double WindUnits = 3.0;
    private const double ThawUnits = 2.0;
    private const double ThawAboveF = 34;

    /// <summary>Sweep dead entries roughly this often, in erosion units.</summary>
    private const double PruneIntervalUnits = 240;

    /// <summary>Hard ceiling so a long run cannot grow the save without bound.</summary>
    private const int MaxTracks = 4000;

    private readonly Dictionary<(GridPosition Position, TrackMaker Maker), Track> _tracks = new();
    private double _lastPruneErosion;

    /// <summary>
    /// Monotonic world erosion. Only ever increases; a track's age is the difference
    /// between this and its stamp.
    /// </summary>
    public double Erosion { get; set; }

    /// <summary>
    /// Something moved between two adjacent tiles. Stamps both the ground departed from
    /// and the ground arrived on.
    /// </summary>
    /// <param name="individuals">How many came through - one walker, or a whole herd.</param>
    /// <param name="individualDepth">
    /// How heavily one of them marks the ground, with an adult human at 1.0.
    /// </param>
    public void Stamp(GridPosition from, GridPosition to, TrackMaker maker,
        double individuals = 1, double individualDepth = 1.0)
    {
        if (from == to) return;

        Direction heading = HeadingOf(from, to);

        // Both tiles: the ground departed from and the ground arrived on. A trail is
        // the chain of these, which is what makes a heading worth storing at all.
        StampTile(from, maker, heading, individuals, individualDepth);
        StampTile(to, maker, heading, individuals, individualDepth);
    }

    private void StampTile(GridPosition position, TrackMaker maker, Direction heading,
        double individuals, double individualDepth)
    {
        double traffic = individuals;
        double heaviest = individualDepth;

        if (_tracks.TryGetValue((position, maker), out Track existing))
        {
            // Whatever the weather has left of the earlier sign carries over and the new
            // passage adds to it, so a route in daily use reads as busier than one
            // crossed once - and traffic can never pile up forever, because what is
            // carried has already faded.
            traffic += existing.Traffic * Freshness(existing);
            heaviest = Math.Max(heaviest, existing.HeaviestIndividual);
        }

        _tracks[(position, maker)] = new Track(maker, heading, Erosion, traffic, heaviest);
    }

    /// <summary>
    /// Advance the erosion accumulator for elapsed game time. Called once per tick from
    /// the simulation, after weather has updated. Returns the units added, which trail
    /// wear decays against so both run off one reading of the weather.
    /// </summary>
    public double Advance(int minutes, Weather weather)
    {
        double units = ErosionPerMinute(weather) * minutes;
        Erosion += units;

        if (Erosion - _lastPruneErosion >= PruneIntervalUnits)
            Prune();

        return units;
    }

    /// <summary>
    /// How fast the ground is being wiped clean right now. Falling snow buries prints,
    /// wind drifts them over, a thaw slumps their edges, and even still air fills them
    /// slowly.
    /// </summary>
    public static double ErosionPerMinute(Weather weather) =>
        StillAirUnits
        + weather.PrecipitationPct * PrecipitationUnits
        + weather.WindSpeedPct * WindUnits
        + (weather.TemperatureInFahrenheit > ThawAboveF ? ThawUnits : 0);

    /// <summary>How much of a track is left to read, 1 = just made, 0 = gone.</summary>
    public double Freshness(Track track) =>
        Math.Clamp(1 - (Erosion - track.StampedErosion) / (BaseLifespanUnits * track.Depth), 0, 1);

    /// <summary>
    /// Readable tracks on a tile, freshest first. Empty when the ground is clean.
    /// </summary>
    public IReadOnlyList<(Track Track, double Freshness)> At(GridPosition position)
    {
        List<(Track, double)>? found = null;

        foreach (TrackMaker maker in Enum.GetValues<TrackMaker>())
        {
            if (!_tracks.TryGetValue((position, maker), out Track track)) continue;

            double freshness = Freshness(track);
            if (freshness <= 0) continue;

            (found ??= []).Add((track, freshness));
        }

        if (found == null) return [];

        found.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return found;
    }

    /// <summary>The freshest track of a given kind on a tile, 0 if there is none.</summary>
    public double FreshnessOf(GridPosition position, TrackMaker maker) =>
        _tracks.TryGetValue((position, maker), out Track track) ? Freshness(track) : 0;

    /// <summary>
    /// How many came through, as the player would count it. Faded by however long the
    /// sign has been sitting there, so a busy route read a week later reads as quieter.
    /// </summary>
    public int TrafficOf(GridPosition position, TrackMaker maker)
    {
        if (!_tracks.TryGetValue((position, maker), out Track track)) return 0;

        double readable = track.Traffic * Freshness(track);
        return readable <= 0 ? 0 : Math.Max(1, (int)Math.Round(readable));
    }

    /// <summary>Which way something was heading when it crossed this tile.</summary>
    private static Direction HeadingOf(GridPosition from, GridPosition to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }

    /// <summary>
    /// Drop tracks the weather has finished with. Erosion only ever rises, so "dead"
    /// is a single comparison and this never has to look at the weather.
    /// </summary>
    private void Prune()
    {
        _lastPruneErosion = Erosion;

        var dead = _tracks.Where(kvp => Freshness(kvp.Value) <= 0).Select(kvp => kvp.Key).ToList();
        foreach (var key in dead)
            _tracks.Remove(key);

        if (_tracks.Count <= MaxTracks) return;

        // Should not happen with the lifespans above, but a save file is forever.
        var oldest = _tracks
            .OrderBy(kvp => kvp.Value.StampedErosion)
            .Take(_tracks.Count - MaxTracks)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in oldest)
            _tracks.Remove(key);
    }

    /// <summary>Flat serialization form, matching how GameMap stores locations and edges.</summary>
    public class TrackData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public TrackMaker Maker { get; set; }
        public Direction Heading { get; set; }
        public double StampedErosion { get; set; }
        public double Traffic { get; set; }
        public double HeaviestIndividual { get; set; }

        public TrackData() { }

        public TrackData(GridPosition position, Track track)
        {
            X = position.X;
            Y = position.Y;
            Maker = track.Maker;
            Heading = track.Heading;
            StampedErosion = track.StampedErosion;
            Traffic = track.Traffic;
            HeaviestIndividual = track.HeaviestIndividual;
        }
    }

    public List<TrackData> Marks
    {
        get => _tracks.Select(kvp => new TrackData(kvp.Key.Position, kvp.Value)).ToList();
        set
        {
            _tracks.Clear();
            foreach (var data in value ?? [])
            {
                var position = new GridPosition(data.X, data.Y);
                _tracks[(position, data.Maker)] = new Track(
                    data.Maker, data.Heading, data.StampedErosion, data.Traffic, data.HeaviestIndividual);
            }
        }
    }
}
