namespace Survival;

public enum StationKind { Fire }

public enum StationDriver { Heat }

/// GLOSSARY: Transformation. Keeps the very piece put on, so taking it back returns it as it was.
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
    public Inventory Ready { get; init; } = new();
}

public sealed record TransformationFinished(long Minute, TileId Tile, FireId Fire, AgentId SetBy, string Recipe, Resource Made, double Kg)
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

/// The making system's stations. A fire is asked only for its temperature. Every station with work on it lives
/// each world minute (Scheduler.Minute), so a finish's minute does not depend on who watches or how the world steps.
public static class Stations
{
    /// A good fire: the smallest steady fire of the coolest log (pine, 800 °F at 0.85 of its size) is above it.
    public const double ReferenceTempF = 650;
    /// Embers: low but above zero, below the weakest flame (kindling at half its size, 300 °F, 0.46).
    public const double EmberRatePct = 0.25;
    public const int HandlingMinutes = 1;
    /// A hair of slack: fifteen fifteenths are not exactly 1 in floating point.
    const double DoneSlack = 1e-9;

    public const string NoFire = "no fire here";
    public const string NothingReady = "nothing at the fire is done";

    public static bool Handles(Activity a) => a is PutOn or TakeFinished or TakeBack;

    /// A station-worked recipe's one input, one piece of it (the loader's rule).
    public static Resource InputOf(Recipe r) => r.Materials[0].AnyOf[0];

    public static string Word(StationKind kind) => kind switch { StationKind.Fire => "fire" };

    public static IEnumerable<Recipe> FireRecipes => Recipes.All.Where(r => r.Station == StationKind.Fire);

    public static StationState At(World world, FireId pit) => world.Stations.GetValueOrDefault(pit) ?? new StationState();

    public static int Room(World world, FireId pit) => Fire.Room(world.Fires[pit].Pit);

    /// Finished results take a place too: there is no queue.
    public static int Used(StationState s) => s.Working.Count + s.Ready.Pieces.Values.Sum(p => p.Count);

    /// One minute of work at this rate: whether the piece is done. The world's minute and the forecast both take it.
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

    public static long? Forecast(World world, FireId pit, Transformation t) => FinishMinute(world, pit, Recipes.Get(t.Recipe), t.DonePct);

    public static Slowed? SlowedBy(FireState fire) =>
        fire.HasEmbers ? Slowed.Embers : !Fire.IsActive(fire) ? Slowed.FireOut : HeatRatePct(fire) < 1 ? Slowed.FireLow : null;

    public static StationProgress Progress(World world, FireId pit, Transformation t) =>
        new(Forecast(world, pit, t), SlowedBy(world.Fires[pit]));

    public static Transformation? Working(World world, AgentId who, TakeBack a) =>
        Fire.PitAt(world, world.Agent(who).Tile) is FireId id ? Piece(At(world, id), a) : null;

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
        if (Fire.PitAt(world, agent.Tile) is not FireId id) return new(NoFire, new MissingState(Lack.BurningFire));
        if (Access.Refusal(world, activity.Who, new LocationFeature(LocationFeatureKind.Fire, agent.Tile, id.Value)) is { } barred) return barred;
        var s = At(world, id);
        switch (activity)
        {
            case PutOn a:
                {
                    var r = Recipes.Get(a.Recipe);
                    if (r.Station != StationKind.Fire) throw new ArgumentException($"{a.Recipe} is not worked at a fire", nameof(activity));
                    var input = InputOf(r);
                    if (Items.Count(pack, input) == 0)
                        return new($"no {Names.Of(input)} to put on", new MissingResource([input], Items.Resources[input].MinPieceKg, 1));
                    int room = Room(world, id);
                    if (Used(s) >= room) return new($"the fire has room for {room}, and all of it is taken", new MissingState(Lack.StationRoom, room + 1, Used(s)));
                    return null;
                }
            case TakeFinished:
                return Items.HasResources(s.Ready) ? null : new(NothingReady);
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
        var id = Fire.PitAt(world, agent.Tile)!.Value;
        if (!world.Stations.TryGetValue(id, out var s)) world.Stations[id] = s = new StationState();
        switch (activity)
        {
            case PutOn a:
                {
                    var input = InputOf(Recipes.Get(a.Recipe));
                    double kg = Items.TakePieces(pack, input, 1).Single();
                    s.Working.Add(new Transformation { Recipe = a.Recipe, Input = input, InputKg = kg, SetBy = agent.Id });
                    break;
                }
            case TakeFinished:
                {
                    var taken = s.Ready;
                    world.Stations[id] = s = new StationState { Working = s.Working };
                    Items.Receive(world, agent, taken, events);
                    events.Add(new Gathered(world.Minute, agent.Id, agent.Tile, Items.Describe(taken)));
                    break;
                }
            case TakeBack a:
                {
                    var t = Piece(s, a)!;
                    s.Working.Remove(t);
                    var back = new Inventory();
                    Items.AddPiece(back, t.Input, t.InputKg);
                    Items.Receive(world, agent, back, events);
                    break;
                }
        }
        if (Used(s) == 0) world.Stations.Remove(id);
        return null;
    }

    static Transformation? Piece(StationState s, TakeBack a) => s.Working.FirstOrDefault(t => t.Input == a.Input && t.InputKg == a.Kg);

    /// The fire is caught up to the minute before its heat is read. Cooking pays the setter, if alive.
    internal static void Minute(World world, List<WorldEvent> events)
    {
        if (world.Stations.Count == 0) return;
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
                Items.AddPiece(s.Ready, made, kg);
                events.Add(new TransformationFinished(world.Minute, fire.Tile, id, t.SetBy, t.Recipe, made, kg));
                bool cooked = r.Driver == StationDriver.Heat && Items.Resources[made].Category == ResourceCategory.Food;
                if (cooked && world.Agents.TryGetValue(t.SetBy, out var setter) && Sim.IsAlive(setter))
                    Skills.Award(world, setter, Skill.Cooking, kg * Skills.CookXpPerKg, events);
            }
        }
    }
}
