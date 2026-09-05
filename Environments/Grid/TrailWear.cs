namespace text_survival.Environments.Grid;

/// <summary>
/// How far a route has been beaten in. Derived from the wear scalar, never stored -
/// there is one number per edge and everything else reads off it.
/// </summary>
public enum TrailTier
{
    /// <summary>Untrodden ground.</summary>
    None,

    /// <summary>Flattened grass, a line you can pick out. Visible, but no faster yet.</summary>
    Trace,

    /// <summary>Bare earth where feet have killed the vegetation.</summary>
    Path,

    /// <summary>A proper game trail - packed, clear of brush, obvious from a distance.</summary>
    Trail
}

/// <summary>
/// Desire paths. Every crossing of an edge adds wear; time and weather take it back.
/// Cross the same ground often enough and it beats itself into a trail that is
/// genuinely faster to walk - and stops being one if you abandon it for a season.
///
/// Wear is the only stored state. Tier and traversal bonus are derived from it, so
/// there is no promotion bookkeeping and no way for the two to disagree. Deliberate
/// player trails (<see cref="EdgeType.TrailMarker"/>, <see cref="EdgeType.CutTrail"/>)
/// remain separate authored edges - this is the emergent sibling of those, not a
/// replacement.
///
/// Keys are canonical edge keys produced by <see cref="GameMap"/>, so wear is shared
/// by both directions of travel.
/// </summary>
public class TrailWear
{
    // Wear needed to reach each tier. One adult human crossing adds 1, so these read
    // roughly as net crossings: a route walked once a day is a Trace in three days and
    // a Path in nine. A dozen caribou add twelve at a go, which is the point.
    public const double TraceAt = 5;
    public const double PathAt = 14;
    public const double TrailAt = 35;

    /// <summary>Wear stops accumulating here, so an abandoned trail fades in a bounded time.</summary>
    public const double MaxWear = 50;

    /// <summary>
    /// Wear lost per minute to the ground settling and growing back. This is the
    /// mechanism that really reclaims a route, and it runs whatever the sky is doing:
    /// about 0.44 wear a day, so a Path nobody walks is gone in about a month.
    /// </summary>
    private const double SettlingPerMinute = 0.000306;

    /// <summary>
    /// Extra wear lost per unit of world erosion, as snow fills the trench in. Weather
    /// is deliberately the smaller term: a beaten path is compacted ground, and burying
    /// it is not the same as undoing it. A continuous blizzard roughly triples the rate
    /// rather than the sixteenfold swing raw erosion would give.
    /// </summary>
    private const double BuryingPerErosionUnit = 0.0000837;

    /// <summary>Below this, drop the entry rather than keep a rounding error forever.</summary>
    private const double MinimumWear = 0.5;

    private readonly Dictionary<(GridPosition Position, Direction Direction), double> _wear = new();

    /// <summary>Add the wear of one crossing. <paramref name="depth"/> is the mover's weight on the ground.</summary>
    public void Add((GridPosition, Direction) edge, double depth)
    {
        _wear.TryGetValue(edge, out double current);
        _wear[edge] = Math.Min(MaxWear, current + depth);
    }

    public double WearAt((GridPosition, Direction) edge) =>
        _wear.TryGetValue(edge, out double wear) ? wear : 0;

    public TrailTier TierAt((GridPosition, Direction) edge) => TierFor(WearAt(edge));

    public static TrailTier TierFor(double wear) => wear switch
    {
        >= TrailAt => TrailTier.Trail,
        >= PathAt => TrailTier.Path,
        >= TraceAt => TrailTier.Trace,
        _ => TrailTier.None
    };

    /// <summary>
    /// Minutes saved crossing this edge. A trace is only visible; you have to beat a
    /// route properly before it saves you anything.
    /// </summary>
    public int TraversalModifierMinutes((GridPosition, Direction) edge) => TierAt(edge) switch
    {
        TrailTier.Trail => -2,
        TrailTier.Path => -1,
        _ => 0
    };

    /// <summary>
    /// Give ground back to the wild: settling and regrowth over time, plus a smaller
    /// contribution from the same erosion that erases footprints. The two systems read
    /// one weather but run on very different clocks.
    /// </summary>
    public void Decay(int minutes, double erosionUnits)
    {
        if (minutes <= 0 || _wear.Count == 0) return;

        double loss = minutes * SettlingPerMinute + erosionUnits * BuryingPerErosionUnit;
        List<(GridPosition, Direction)>? reclaimed = null;

        foreach (var edge in _wear.Keys.ToList())
        {
            double wear = _wear[edge] - loss;
            if (wear < MinimumWear)
                (reclaimed ??= []).Add(edge);
            else
                _wear[edge] = wear;
        }

        foreach (var edge in reclaimed ?? [])
            _wear.Remove(edge);
    }

    /// <summary>Flat serialization form, matching how GameMap stores locations and edges.</summary>
    public class WearData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public Direction Direction { get; set; }
        public double Wear { get; set; }

        public WearData() { }

        public WearData(GridPosition position, Direction direction, double wear)
        {
            X = position.X;
            Y = position.Y;
            Direction = direction;
            Wear = wear;
        }
    }

    public List<WearData> Wear
    {
        get => _wear.Select(kvp => new WearData(kvp.Key.Position, kvp.Key.Direction, kvp.Value)).ToList();
        set
        {
            _wear.Clear();
            foreach (var data in value ?? [])
                _wear[(new GridPosition(data.X, data.Y), data.Direction)] = data.Wear;
        }
    }
}
