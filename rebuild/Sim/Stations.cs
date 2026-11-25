namespace Survival;

public enum StationKind { Fire, Rack }

public enum StationDriver { Heat, Drying }

/// GLOSSARY: Transformation. Keeps the very thing put on (a piece, or an amount), so taking it back returns it as it was.
public sealed class Transformation
{
    public required string Recipe { get; init; }
    public required Resource Input { get; init; }
    public required double InputKg { get; init; }
    public required AgentId SetBy { get; init; }
    public double DonePct { get; set; }
}

public sealed class StationState
{
    public List<Transformation> Working { get; init; } = [];
    public Inventory Ready { get; set; } = new();
    /// The places finished amounts (dried berries) hold: amounts merge in Ready, and each still takes its place.
    public int ReadyAmounts { get; set; }
}

/// Never wears, never removed.
public sealed class RackState
{
    public required TileId Tile { get; init; }
    public StationState Work { get; init; } = new();
}

public sealed record TransformationFinished(long Minute, TileId Tile, LocationFeature Station, AgentId SetBy, string Recipe, Resource Made, double Kg)
    : WorldEvent(Minute, Tile);

/// Why a station stopped working: the fire went out (drying's reasons come with the rack).
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StopReason>))]
public enum StopReason { FireOut }

public sealed record TransformationStopped(long Minute, TileId Tile, FireId Fire, IReadOnlyList<Resource> Left, StopReason Why)
    : WorldEvent(Minute, Tile);

public enum Slowed { FireLow, Embers, FireOut }

/// FinishMinute null: not while things stay as they are. Why null: at the recipe's own time.
public sealed record StationProgress(long? FinishMinute, Slowed? Why);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StationMark>))]
public enum StationMark { Working, Ready }

/// Fires and curing racks. A fire is asked only for its temperature. Every station with work lives each world minute
/// (Scheduler.Minute), so a finish's minute does not depend on who watches or how the world steps.
public static class Stations
{
    /// A good fire: the smallest steady fire of the coolest wood (pine, 800 °F at 0.85 of its size) is above it.
    public const double ReferenceTempF = 650;
    /// Embers: low but above zero, below the weakest flame (sticks at half its size, 300 °F, 0.46).
    public const double EmberRatePct = 0.25;
    /// Full rate whatever the weather (#187); the weather and a fire beside it set it in #190.
    public const double DryingRatePct = 1;
    public const int RackRoom = 4;
    public const int HandlingMinutes = 1;
    /// A hair of slack: fifteen fifteenths are not exactly 1 in floating point.
    const double DoneSlack = 1e-9;

    public const string NoFire = "no fire here", NoRack = "no such rack here";

    public static bool Handles(Activity a) => a is PutOn or TakeFinished or TakeBack;

    /// A station-worked recipe's one input: one piece of it, or up to InputCapKg of it (the loader's rule).
    public static Resource InputOf(Recipe r) => r.Materials[0].AnyOf[0];

    public static double InputCapKg(Recipe r) => r.Materials[0].Kg;

    public static string Word(StationKind kind) => kind switch { StationKind.Fire => "fire", StationKind.Rack => "rack" };

    public static string Word(LocationFeature station) => Word(KindOf(station));

    static StationKind KindOf(LocationFeature station) =>
        station.Kind == LocationFeatureKind.Fire ? StationKind.Fire : station.Kind == LocationFeatureKind.Rack ? StationKind.Rack
        : throw new ArgumentException($"{station} is not a station", nameof(station));

    public static IEnumerable<Recipe> RecipesOf(StationKind kind) => Recipes.All.Where(r => r.Station == kind);

    public static IEnumerable<Recipe> FireRecipes => RecipesOf(StationKind.Fire);

    public static StationState At(World world, FireId pit) => world.Stations.GetValueOrDefault(pit) ?? new StationState();

    public static int Room(World world, FireId pit) => Fire.Room(world.Fires[pit].Pit);

    /// Finished results take a place too: there is no queue.
    public static int Used(StationState s) => s.Working.Count + s.Ready.Pieces.Values.Sum(p => p.Count) + s.ReadyAmounts;

    internal static RackId Build(World world, TileId tile)
    {
        var id = new RackId(world.NextRackId++);
        world.Racks[id] = new RackState { Tile = tile };
        return id;
    }

    public static IEnumerable<RackId> RacksAt(World world, TileId tile) => world.Racks.Where(kv => kv.Value.Tile == tile).Select(kv => kv.Key);

    /// State is a fresh one for a fire with nothing on it.
    sealed record Site(LocationFeature Feature, StationState State, int Room)
    {
        public StationKind Kind => KindOf(Feature);
        public FireId? Pit => Kind == StationKind.Fire ? new FireId(Feature.Index) : null;
    }

    static RackId? RackOf(Activity a) => a switch { PutOn p => p.Rack, TakeFinished t => t.Rack, TakeBack b => b.Rack, _ => null };

    static Site? SiteOf(World world, TileId tile, RackId? rack)
    {
        if (rack is RackId r)
            return world.Racks.TryGetValue(r, out var rs) && rs.Tile == tile ? new(new(LocationFeatureKind.Rack, tile, r.Value), rs.Work, RackRoom) : null;
        return Fire.PitAt(world, tile) is FireId id ? new(new(LocationFeatureKind.Fire, tile, id.Value), At(world, id), Room(world, id)) : null;
    }

    /// One minute of work at this rate: whether it is done. The world's minute and the forecast both take it.
    static bool Advance(ref double donePct, double ratePct, Recipe r)
    {
        donePct += ratePct / r.Minutes;
        return donePct >= 1 - DoneSlack;
    }

    /// GLOSSARY: Forecast. A piece done this far, by the fire's own forecast of its burn; null if it goes cold first.
    /// The same for everyone. The minute now has already been worked.
    public static long? FinishMinute(World world, FireId pit, Recipe r, double donePct)
    {
        var fire = world.Fires[pit];
        if (Fire.IsActive(fire) || fire.HasEmbers) Fire.RequireCurrent(world, pit, fire); // a cold fire has no burn to lag behind
        if (r.Station != StationKind.Fire) throw new ArgumentException($"{r.Id} is not worked at a fire", nameof(r));
        foreach (var ahead in Fire.Ahead(fire))
            if (Advance(ref donePct, HeatRatePct(ahead), r)) return ahead.LastUpdatedMinute;
        return null;
    }

    /// GLOSSARY: Forecast. On a rack, at the drying rate, which stays as it is.
    static long RackFinishMinute(World world, Recipe r, double donePct)
    {
        if (r.Station != StationKind.Rack) throw new ArgumentException($"{r.Id} is not worked on a rack", nameof(r));
        for (long minute = world.Minute + 1; ; minute++)
            if (Advance(ref donePct, DryingRatePct, r)) return minute;
    }

    static long? Forecast(World world, Site site, Transformation t) => site.Pit is FireId pit
        ? FinishMinute(world, pit, Recipes.Get(t.Recipe), t.DonePct)
        : RackFinishMinute(world, Recipes.Get(t.Recipe), t.DonePct);

    public static Slowed? SlowedBy(FireState fire) =>
        fire.HasEmbers ? Slowed.Embers : !Fire.IsActive(fire) ? Slowed.FireOut : HeatRatePct(fire) < 1 ? Slowed.FireLow : null;

    public static StationProgress? Progress(World world, AgentId who, TakeBack a) =>
        SiteOf(world, world.Agent(who).Tile, a.Rack) is { } site && Piece(site.State, a) is { } t
            ? new(Forecast(world, site, t), site.Pit is FireId pit ? SlowedBy(world.Fires[pit]) : null) : null;

    public static StationMark? MarkAt(World world, FireId pit) => Mark(world.Stations.GetValueOrDefault(pit));

    /// Every station on the tile, a fire and its racks: Ready wins, then Working.
    public static StationMark? MarkAt(World world, TileId tile)
    {
        var marks = RacksAt(world, tile).Select(id => Mark(world.Racks[id].Work)).ToList();
        if (Fire.PitAt(world, tile) is FireId pit) marks.Add(MarkAt(world, pit));
        return marks.Contains(StationMark.Ready) ? StationMark.Ready : marks.Contains(StationMark.Working) ? StationMark.Working : null;
    }

    /// Ready wins: something finished and not taken; else something working; else nothing.
    public static StationMark? Mark(StationState? s) =>
        s == null ? null : Items.HasResources(s.Ready) ? StationMark.Ready : s.Working.Count > 0 ? StationMark.Working : null;

    /// 1 is the recipe's stated time.
    public static double HeatRatePct(FireState fire)
    {
        if (fire.HasEmbers) return EmberRatePct;
        if (!Fire.IsActive(fire)) return 0;
        return Math.Min(1, Fire.TemperatureF(fire) / ReferenceTempF);
    }

    public static Refusal? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        if (Items.PackOf(agent) is not Inventory pack) return new("carries nothing to put on or take");
        var rack = RackOf(activity);
        if (SiteOf(world, agent.Tile, rack) is not { } site)
            return rack == null ? new(NoFire, new MissingState(Lack.BurningFire)) : new(NoRack);
        if (Access.Refusal(world, activity.Who, site.Feature) is { } barred) return barred;
        var s = site.State;
        string word = Word(site.Kind);
        switch (activity)
        {
            case PutOn a:
                {
                    var r = Recipes.Get(a.Recipe);
                    if (r.Station != site.Kind) throw new ArgumentException($"{a.Recipe} is not worked at a {word}", nameof(activity));
                    var input = InputOf(r);
                    if (Items.IsDiscrete(input) ? Items.Count(pack, input) == 0 : Items.Have(pack, input) <= Items.SliverKg)
                        return new($"no {Names.Of(input)} to put on", Items.IsDiscrete(input)
                            ? new MissingResource([input], Items.Resources[input].MinPieceKg, 1) : new MissingResource([input], InputCapKg(r), 0));
                    if (Used(s) >= site.Room)
                        return new($"the {word} has room for {site.Room}, and all of it is taken", new MissingState(Lack.StationRoom, site.Room + 1, Used(s)));
                    return null;
                }
            case TakeFinished:
                return Items.HasResources(s.Ready) ? null : new($"nothing at the {word} is done");
            case TakeBack a:
                return Piece(s, a) != null ? null : new("nothing there to take back");
            default:
                throw new NotSupportedException($"{activity.GetType().Name} is not a station activity");
        }
    }

    internal static string? Act(World world, Activity activity, List<WorldEvent> events)
    {
        if (Refusal(world, activity) is { } refused) throw new InvalidOperationException($"Stations.Act on a refused activity: {refused}");
        var agent = world.Agent(activity.Who);
        var pack = Items.Pack(world, activity.Who);
        var site = SiteOf(world, agent.Tile, RackOf(activity))!;
        var s = site.State;
        if (site.Pit is FireId id && !world.Stations.ContainsKey(id)) world.Stations[id] = s;
        switch (activity)
        {
            case PutOn a:
                {
                    var r = Recipes.Get(a.Recipe);
                    var input = InputOf(r);
                    double kg = Items.IsDiscrete(input) ? Items.TakePieces(pack, input, 1).Single() : Items.Take(pack, input, InputCapKg(r));
                    s.Working.Add(new Transformation { Recipe = a.Recipe, Input = input, InputKg = kg, SetBy = agent.Id });
                    break;
                }
            case TakeFinished:
                {
                    var taken = s.Ready;
                    s.Ready = new Inventory();
                    s.ReadyAmounts = 0;
                    Items.Receive(world, agent, taken, events);
                    events.Add(new Gathered(world.Minute, agent.Id, agent.Tile, Items.Describe(taken)));
                    break;
                }
            case TakeBack a:
                {
                    var t = Piece(s, a)!;
                    s.Working.Remove(t);
                    var back = new Inventory();
                    AddByForm(back, t.Input, t.InputKg);
                    Items.Receive(world, agent, back, events);
                    break;
                }
        }
        if (site.Pit is FireId emptied && Used(s) == 0) world.Stations.Remove(emptied);
        return null;
    }

    static void AddByForm(Inventory inv, Resource r, double kg)
    {
        if (Items.IsDiscrete(r)) Items.AddPiece(inv, r, kg);
        else Items.Add(inv, r, kg);
    }

    static Transformation? Piece(StationState s, TakeBack a) => s.Working.FirstOrDefault(t => t.Input == a.Input && t.InputKg == a.Kg);

    /// The fire is caught up to the minute before its heat is read; a rack dries at its rate. Cooking pays the setter, if alive.
    internal static void Minute(World world, List<WorldEvent> events)
    {
        foreach (var (id, s) in world.Stations)
        {
            if (s.Working.Count == 0) continue;
            // Only this catches a working fire up, so heat at the last minute and none at this one is the stop.
            bool wasWorking = HeatRatePct(world.Fires[id]) > 0;
            Fire.CatchUp(world, id, events);
            var fire = world.Fires[id];
            double rate = HeatRatePct(fire);
            if (rate <= 0)
            {
                if (wasWorking) events.Add(new TransformationStopped(world.Minute, fire.Tile, id, [.. s.Working.Select(t => t.Input)], StopReason.FireOut));
                continue;
            }
            Work(world, s, new LocationFeature(LocationFeatureKind.Fire, fire.Tile, id.Value), rate, events);
        }
        foreach (var (id, rack) in world.Racks)
            if (rack.Work.Working.Count > 0) Work(world, rack.Work, new LocationFeature(LocationFeatureKind.Rack, rack.Tile, id.Value), DryingRatePct, events);
    }

    static void Work(World world, StationState s, LocationFeature at, double rate, List<WorldEvent> events)
    {
        foreach (var t in s.Working.ToList())
        {
            var r = Recipes.Get(t.Recipe);
            double done = t.DonePct;
            bool finished = Advance(ref done, rate, r);
            t.DonePct = done;
            if (!finished) continue;
            s.Working.Remove(t);
            var made = r.Result.Resource!.Value;
            double kg = t.InputKg * r.Result.SharePct;
            AddByForm(s.Ready, made, kg);
            if (!Items.IsDiscrete(made)) s.ReadyAmounts++;
            events.Add(new TransformationFinished(world.Minute, at.Tile, at, t.SetBy, t.Recipe, made, kg));
            bool cooked = r.Driver == StationDriver.Heat && Items.Resources[made].Category == ResourceCategory.Food;
            if (cooked && world.Agents.TryGetValue(t.SetBy, out var setter) && Sim.IsAlive(setter))
                Skills.Award(world, setter, Skill.Cooking, kg * Skills.CookXpPerKg, events);
        }
    }
}
