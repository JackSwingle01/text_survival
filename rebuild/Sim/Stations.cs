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

public sealed record TransformationFinished(long Minute, TileId Tile, FireId Station, AgentId SetBy, string Recipe, Resource Made, double Kg)
    : WorldEvent(Minute, Tile);

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
    const double DoneSlackPct = 1e-9;

    public const string NoFire = "no fire here";
    public const string NothingReady = "nothing at the fire is done";

    public static bool Handles(Activity a) => a is PutOn or TakeFinished or TakeBack;

    public static string Word(StationKind kind) => kind switch { StationKind.Fire => "fire" };

    public static IEnumerable<Recipe> FireRecipes => Recipes.All.Where(r => r.Station == StationKind.Fire);

    public static StationState At(World world, FireId pit) => world.Stations.GetValueOrDefault(pit) ?? new StationState();

    public static int Room(World world, FireId pit) => Fire.Room(world.Fires[pit].Pit);

    /// Finished results take a place too: there is no queue.
    public static int Used(StationState s) => s.Working.Count + s.Ready.Pieces.Values.Sum(p => p.Count);

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
                    var input = r.Materials[0].AnyOf[0];
                    if (Items.Count(pack, input) == 0)
                        return new($"no {Names.Of(input)} to put on", new MissingResource([input], Items.Resources[input].MinPieceKg, 1));
                    int room = Room(world, id);
                    if (Used(s) >= room) return new($"the fire has room for {room}, and all of it is taken", new MissingState(Lack.StationRoom, room + 1, Used(s)));
                    return null;
                }
            case TakeFinished:
                return Items.HasResources(s.Ready) ? null : new(NothingReady);
            case TakeBack a:
                return a.Slot >= 0 && a.Slot < s.Working.Count ? null : new("nothing there to take back");
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
                    var input = Recipes.Get(a.Recipe).Materials[0].AnyOf[0];
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
                    var t = s.Working[a.Slot];
                    s.Working.RemoveAt(a.Slot);
                    var back = new Inventory();
                    Items.AddPiece(back, t.Input, t.InputKg);
                    Items.Receive(world, agent, back, events);
                    break;
                }
        }
        if (Used(s) == 0) world.Stations.Remove(id);
        return null;
    }

    /// The fire is caught up to the minute before its heat is read. Cooking pays the setter, if alive.
    internal static void Minute(World world, List<WorldEvent> events)
    {
        if (world.Stations.Count == 0) return;
        foreach (var (id, s) in world.Stations)
        {
            if (s.Working.Count == 0) continue;
            Fire.CatchUp(world, id, events);
            var fire = world.Fires[id];
            double rate = HeatRatePct(fire);
            if (rate <= 0) continue;
            foreach (var t in s.Working.ToList())
            {
                var r = Recipes.Get(t.Recipe);
                t.DonePct += rate / r.Minutes;
                if (t.DonePct < 1 - DoneSlackPct) continue;
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
