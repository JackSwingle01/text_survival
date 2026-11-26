namespace Survival;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StationKind>))]
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

/// GLOSSARY: Transformation, one with no input (melting snow): each full share of the work adds its result.
public sealed class Ongoing
{
    public required string Recipe { get; init; }
    public required AgentId SetBy { get; init; }
    /// Of the unit under way.
    public double DonePct { get; set; }
}

public sealed class StationState
{
    public List<Transformation> Working { get; init; } = [];
    public List<Ongoing> Ongoing { get; init; } = [];
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

public enum Slowed { FireLow, Embers, FireOut, Snowing, Raining }

/// What most speeds drying past the recipe's own time.
public enum Sped { FireBeside, WarmAir, Wind }

/// FinishMinute null: not while things stay as they are. Why null: at the recipe's own time or faster, Faster saying why.
public sealed record StationProgress(long? FinishMinute, Slowed? Why, Sped? Faster = null);

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
    /// Drying: each DryingSpanF above the reference (−10 °C, where winter's and spring's skies overlap), fire heat
    /// included, adds 1, so a steady fire doubles it there; a gale adds a little.
    public const double DryingReferenceAirF = 14, DryingSpanF = 36, GaleDryingDelta = 0.2, MaxDryingFactor = 2;
    public const int RackRoom = 4;
    public const int HandlingMinutes = 1;
    /// A hair of slack: fifteen fifteenths are not exactly 1 in floating point.
    const double DoneSlack = 1e-9;

    public const string NoFire = "no fire here", NoRack = "no such rack here";

    public static bool Handles(Activity a) => a is PutOn or TakeFinished or TakeBack or StopWork;

    /// A station-worked recipe's one input: one piece of it, or up to InputCapKg of it (the loader's rule).
    public static Resource InputOf(Recipe r) => r.Materials[0].AnyOf[0];

    public static double InputCapKg(Recipe r) => r.Materials[0].Kg;

    /// Takes nothing: set going, it works until its Result.CapKg waits at the station (the loader's rule).
    public static bool FromNothing(Recipe r) => r.Materials.Count == 0;
    public static bool FromNothing(string recipe) => FromNothing(Recipes.Get(recipe));
    static string Lower(string recipe) => Recipes.Get(recipe).Name.ToLowerInvariant();
    public static string StopLabel(string recipe) => $"Stop: {Lower(recipe)}";

    public static string Word(StationKind kind) => kind switch { StationKind.Fire => "fire", StationKind.Rack => "rack" };

    public static string Word(LocationFeature station) => Word(KindOf(station));

    static StationKind KindOf(LocationFeature station) =>
        station.Kind == LocationFeatureKind.Fire ? StationKind.Fire : station.Kind == LocationFeatureKind.Rack ? StationKind.Rack
        : throw new ArgumentException($"{station} is not a station", nameof(station));

    public static IEnumerable<Recipe> RecipesOf(StationKind kind) => Recipes.All.Where(r => r.Station == kind);

    public static IEnumerable<Recipe> FireRecipes => RecipesOf(StationKind.Fire);

    public static StationState At(World world, FireId pit) => world.Stations.GetValueOrDefault(pit) ?? new StationState();

    public static int Room(World world, FireId pit) => Fire.Room(world.Fires[pit].Pit);

    /// Finished results take a place too: there is no queue. What a recipe with no input made waits in its own place.
    public static int Used(StationState s) => s.Working.Count + s.Ongoing.Count + s.Ready.Pieces.Values.Sum(p => p.Count) + s.ReadyAmounts;

    static bool Empty(StationState s) => s.Working.Count == 0 && s.Ongoing.Count == 0 && !Items.HasResources(s.Ready);

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

    /// One minute of work at this rate (1 the recipe's time): whether it is done. The world's minute and the forecast both take it.
    static bool Advance(ref double donePct, double rate, Recipe r)
    {
        donePct += rate / r.Minutes;
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

    /// GLOSSARY: Forecast. On a rack under the sky and shelter as they are now, beside the pit as its own forecast burns
    /// it down; null if it will not finish. The minute now has already been worked.
    static long? RackFinishMinute(World world, TileId tile, Recipe r, double donePct)
    {
        if (r.Station != StationKind.Rack) throw new ArgumentException($"{r.Id} is not worked on a rack", nameof(r));
        var shelter = Shelters.CoverNow(world, tile, stationary: true);
        FireState? pit = null;
        IEnumerator<FireState>? ahead = null;
        if (Fire.PitAt(world, tile) is FireId id)
        {
            pit = world.Fires[id];
            if (Fire.IsActive(pit) || pit.HasEmbers) Fire.RequireCurrent(world, id, pit);
            ahead = Fire.Ahead(pit).GetEnumerator();
        }
        for (long minute = world.Minute + 1; ; minute++)
        {
            bool burning = ahead?.MoveNext() == true;
            if (burning) pit = ahead!.Current; // the last copy stays: the cold pit the real minute reads
            double rate = DryingFactor(Exposure.Standing(world, tile, world.Minute, pit, shelter));
            if (!burning && rate <= 0) return null; // nothing left to change
            if (Advance(ref donePct, rate, r)) return minute;
        }
    }

    static long? Forecast(World world, Site site, Transformation t) => site.Pit is FireId pit
        ? FinishMinute(world, pit, Recipes.Get(t.Recipe), t.DonePct)
        : RackFinishMinute(world, site.Feature.Tile, Recipes.Get(t.Recipe), t.DonePct);

    public static long? Forecast(World world, FireId pit, Transformation t) => FinishMinute(world, pit, Recipes.Get(t.Recipe), t.DonePct);

    public static long? Forecast(World world, FireId pit, Ongoing o)
    {
        var r = Recipes.Get(o.Recipe);
        return Items.Have(At(world, pit).Ready, r.Result.Resource!.Value) >= r.Result.CapKg - DoneSlack ? null : FinishMinute(world, pit, r, o.DonePct);
    }

    public static Slowed? SlowedBy(FireState fire) =>
        fire.HasEmbers ? Slowed.Embers : !Fire.IsActive(fire) ? Slowed.FireOut : HeatRatePct(fire) < 1 ? Slowed.FireLow : null;

    public static StationProgress? Progress(World world, AgentId who, TakeBack a)
    {
        if (SiteOf(world, world.Agent(who).Tile, a.Rack) is not { } site || Piece(site.State, a) is not { } t) return null;
        if (site.Pit is FireId pit) return new(Forecast(world, site, t), SlowedBy(world.Fires[pit]));
        var tile = site.Feature.Tile;
        var now = Exposure.Standing(world, tile, world.Minute, Fire.CurrentPitAt(world, tile), Shelters.CoverNow(world, tile, stationary: true));
        return new(Forecast(world, site, t), SlowedBy(now), SpedBy(now));
    }

    public static Slowed? SlowedBy(StandingExposure e) => e.Falling switch
    {
        Precipitation.None => null,
        Precipitation.Rain => Slowed.Raining,
        Precipitation.Snow or Precipitation.Blizzard => Slowed.Snowing,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e.Falling, "no words for it"),
    };

    public static Sped? SpedBy(StandingExposure e)
    {
        if (DryingFactor(e) <= 1) return null;
        var (air, fire, wind) = DryingDeltas(e);
        return fire >= air && fire >= wind ? Sped.FireBeside : air >= wind ? Sped.WarmAir : Sped.Wind;
    }

    /// 1 is the recipe's stated time: a dry, still day at or below the reference air. Rain or snow on it stops it.
    public static double DryingFactor(StandingExposure e)
    {
        if (e.Falling != Precipitation.None) return 0;
        var (air, fire, wind) = DryingDeltas(e);
        return Math.Min(MaxDryingFactor, 1 + air + fire + wind);
    }

    static (double Air, double Fire, double Wind) DryingDeltas(StandingExposure e)
    {
        double air = WarmDelta(e.AirF);
        return (air, WarmDelta(e.AirF + e.FireHeatF) - air, e.WindLevel * GaleDryingDelta);
    }

    static double WarmDelta(double airF) => Math.Max(0, airF - DryingReferenceAirF) / DryingSpanF;

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
                    if (!FromNothing(r) && InputOf(r) is var input
                        && (Items.IsDiscrete(input) ? Items.Count(pack, input) == 0 : Items.Have(pack, input) <= Items.SliverKg))
                        return new($"no {Names.Of(input)} to put on", Items.IsDiscrete(input)
                            ? new MissingResource([input], Items.Resources[input].MinPieceKg, 1) : new MissingResource([input], InputCapKg(r), 0));
                    if (Used(s) >= site.Room)
                        return new($"the {word} has room for {site.Room}, and all of it is taken", new MissingState(Lack.StationRoom, site.Room + 1, Used(s)));
                    if (s.Ongoing.Any(o => o.Recipe == a.Recipe)) return new($"the {word} is already set to {Lower(a.Recipe)}");
                    return null;
                }
            case TakeFinished:
                return Items.HasResources(s.Ready) ? null : new($"nothing at the {word} is done");
            case TakeBack a:
                return Piece(s, a) != null ? null : new("nothing there to take back");
            case StopWork a:
                return s.Ongoing.Any(o => o.Recipe == a.Recipe) ? null : new($"the fire is not set to {Lower(a.Recipe)}");
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
            case PutOn a when FromNothing(a.Recipe):
                s.Ongoing.Add(new Ongoing { Recipe = a.Recipe, SetBy = agent.Id });
                break;
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
            case StopWork a:
                s.Ongoing.RemoveAll(o => o.Recipe == a.Recipe);
                break;
        }
        if (site.Pit is FireId emptied && Empty(s)) world.Stations.Remove(emptied);
        return null;
    }

    static void AddByForm(Inventory inv, Resource r, double kg)
    {
        if (Items.IsDiscrete(r)) Items.AddPiece(inv, r, kg);
        else Items.Add(inv, r, kg);
    }

    static Transformation? Piece(StationState s, TakeBack a) => s.Working.FirstOrDefault(t => t.Input == a.Input && t.InputKg == a.Kg);

    static IEnumerable<(LocationFeature Feature, StationState State)> On(World world, TileId tile)
    {
        foreach (var (id, s) in world.Stations)
            if (world.Fires[id].Tile == tile) yield return (new(LocationFeatureKind.Fire, tile, id.Value), s);
        foreach (var (id, rack) in world.Racks)
            if (rack.Tile == tile && !Empty(rack.Work)) yield return (new(LocationFeatureKind.Rack, tile, id.Value), rack.Work);
    }

    /// GLOSSARY: Exposed. The tile's pile and what sits at its stations, an input as itself. Only read: it may be the pile.
    public static Inventory? ExposedFood(World world, TileId tile)
    {
        var pile = world.Tiles.GetValueOrDefault(tile)?.Pile;
        if (world.Stations.Count == 0 && world.Racks.Count == 0) return pile;
        Inventory? all = null;
        foreach (var (_, s) in On(world, tile))
        {
            if (all == null)
            {
                all = new Inventory();
                if (pile != null) Items.AddAll(all, pile);
            }
            foreach (var t in s.Working) AddByForm(all, t.Input, t.InputKg);
            Items.AddAll(all, s.Ready);
        }
        return all ?? pile;
    }

    /// Tiles whose stations hold anything, and no pile (Items.PileTilesOf has those).
    public static IEnumerable<TileId> FoodTiles(World world)
    {
        var seen = new HashSet<TileId>();
        foreach (var (id, _) in world.Stations)
            if (world.Fires[id].Tile is var t && world.Tiles.GetValueOrDefault(t)?.Pile == null && seen.Add(t)) yield return t;
        foreach (var rack in world.Racks.Values)
            if (!Empty(rack.Work) && world.Tiles.GetValueOrDefault(rack.Tile)?.Pile == null && seen.Add(rack.Tile)) yield return rack.Tile;
    }

    /// The station an animal reaches its food at, for its access check: the first on the tile holding any of it.
    public static LocationFeature? FeatureWith(World world, TileId tile, IReadOnlyList<Resource> food)
    {
        foreach (var (feature, s) in On(world, tile))
            if (s.Working.Exists(t => food.Contains(t.Input)) || food.Any(r => Items.Have(s.Ready, r) > 0)) return feature;
        return null;
    }

    /// An animal's bite past the pile (Items.TakeFromPile), as a pile's is torn: a piece being worked keeps working, smaller;
    /// a finished amount eaten away frees its places.
    internal static List<(StationKind At, double Kg)> Eat(World world, TileId tile, Resource r, double kg)
    {
        var took = new List<(StationKind, double)>();
        foreach (var (feature, s) in On(world, tile).ToList())
        {
            if (kg <= Items.SliverKg) break;
            double moved = Items.Bite(s.Ready, new Inventory(), r, kg);
            if (!Items.IsDiscrete(r) && moved > 0 && !HoldsAnAmount(s)) s.ReadyAmounts = 0;
            for (int i = 0; i < s.Working.Count && kg - moved > Items.SliverKg; i++)
            {
                var t = s.Working[i];
                if (t.Input != r) continue;
                double bite = Math.Min(t.InputKg, kg - moved), left = t.InputKg - bite;
                bool whole = Items.IsDiscrete(r) ? !Items.InRange(r, left) : left <= Items.SliverKg;
                moved += whole ? t.InputKg : bite;
                if (whole) s.Working.RemoveAt(i--);
                else s.Working[i] = new Transformation { Recipe = t.Recipe, Input = r, InputKg = left, SetBy = t.SetBy, DonePct = t.DonePct };
            }
            if (moved <= 0) continue;
            took.Add((KindOf(feature), moved));
            kg -= moved;
            if (feature.Kind == LocationFeatureKind.Fire && Empty(s)) world.Stations.Remove(new FireId(feature.Index));
        }
        return took;
    }

    /// A finished amount made from an input (dried berries, tallow); a melt's water holds no place.
    static bool HoldsAnAmount(StationState s) =>
        Items.Totals(s.Ready).Any(x => !Items.IsDiscrete(x.Resource) && Recipes.All.Any(rc => !FromNothing(rc) && rc.Result.Resource == x.Resource));

    /// A fire, or the pit beside a rack, is caught up to the minute before its heat is read. Cooking pays the setter, if alive.
    internal static void Minute(World world, List<WorldEvent> events)
    {
        foreach (var (id, s) in world.Stations)
        {
            if (s.Working.Count == 0 && s.Ongoing.Count == 0) continue;
            // Only this catches a working fire up, so heat at the last minute and none at this one is the stop.
            bool wasWorking = HeatRatePct(world.Fires[id]) > 0;
            Fire.CatchUp(world, id, events);
            var fire = world.Fires[id];
            double rate = HeatRatePct(fire);
            if (rate <= 0)
            {
                if (wasWorking && s.Working.Count > 0) events.Add(new TransformationStopped(world.Minute, fire.Tile, id, [.. s.Working.Select(t => t.Input)], StopReason.FireOut));
                continue;
            }
            Work(world, s, new LocationFeature(LocationFeatureKind.Fire, fire.Tile, id.Value), rate, events);
        }
        foreach (var (id, rack) in world.Racks)
            if (rack.Work.Working.Count > 0)
                Work(world, rack.Work, new LocationFeature(LocationFeatureKind.Rack, rack.Tile, id.Value), DryingFactor(RackExposure(world, rack.Tile, events)), events);
    }

    static StandingExposure RackExposure(World world, TileId tile, List<WorldEvent> events)
    {
        FireState? pit = null;
        if (Fire.PitAt(world, tile) is FireId id) // the pit only: catching up a torch passing could spend it
        {
            Fire.CatchUp(world, id, events);
            pit = world.Fires[id];
        }
        return Exposure.Standing(world, tile, world.Minute, pit, Shelters.CoverFor(world, tile, stationary: true, events));
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
        foreach (var o in s.Ongoing)
        {
            var r = Recipes.Get(o.Recipe);
            var made = r.Result.Resource!.Value;
            if (Items.Have(s.Ready, made) >= r.Result.CapKg - DoneSlack) continue; // stopped while its cap waits
            o.DonePct += rate / r.Minutes;
            if (o.DonePct < 1 - DoneSlack) continue;
            o.DonePct = Math.Max(0, o.DonePct - 1);
            Items.Add(s.Ready, made, r.Result.Kg);
            events.Add(new TransformationFinished(world.Minute, at.Tile, at, o.SetBy, o.Recipe, made, r.Result.Kg));
        }
    }
}
