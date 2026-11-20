namespace Survival;

public enum PitType { Open, Mound, Stone }

/// Derived from FireState, never stored.
public enum FirePhase { Cold, Igniting, Building, Roaring, Steady, Dying, Embers }

/// Temperatures in °F, as tuned in the old game.
public readonly record struct FuelProps(double PeakTempF, double BurnRateKgPerHour, double MinFireTempF, double IgnitionBonusPct);

/// A fire pit on a tile, or a torch carried by an agent (CarriedBy set; the two-mass fields are then unused).
public sealed class FireState
{
    public required TileId Tile { get; set; }
    public required long LastUpdatedMinute { get; set; }
    public PitType Pit { get; init; }
    // Two-mass model: fuel waiting to catch, and fuel burning. Masses are the sums. Sorted, so the
    // sums add up in one order however the fuels came and went (see World.Tiles).
    public SortedDictionary<Resource, double> UnburnedKg { get; init; } = [];
    public SortedDictionary<Resource, double> BurningKg { get; init; } = [];
    public bool HasEmbers { get; set; }
    public double EmberDurationHours { get; set; }
    public double EmberHoursRemaining { get; set; }
    public double EmberStartTempF { get; set; }
    public double LastBurningTempF { get; set; }
    public double BurnedSinceLitKg { get; set; }
    public double CharcoalKg { get; set; }
    public AgentId? CarriedBy { get; set; }
    public double TorchMinutesLeft { get; set; }
}

public static class Fire
{
    public static readonly IReadOnlyDictionary<Resource, FuelProps> Props = new Dictionary<Resource, FuelProps>
    {
        // ponytail: table in code until phase 4 moves content to data files
        [Resource.Tinder] = new(450, 3.0, 0, 0.15),
        [Resource.BirchBark] = new(500, 2.5, 0, 0.25),
        [Resource.Amadou] = new(490, 2.6, 0, 0.20),
        [Resource.Usnea] = new(480, 2.8, 0, 0.18),
        [Resource.Chaga] = new(470, 2.2, 0, 0.15),
        [Resource.Kindling] = new(600, 1.5, 0, 0),
        [Resource.Pine] = new(800, 1.2, 150, 0.05),
        [Resource.Birch] = new(850, 0.9, 250, 0),
        [Resource.Oak] = new(950, 0.6, 500, 0),
        [Resource.Bone] = new(650, 0.5, 600, 0),
        [Resource.Peat] = new(700, 0.8, 400, 0),
        // Old FireHandler fed charcoal to the fire as kindling.
        [Resource.Charcoal] = new(600, 1.5, 0, 0),
    };

    /// Which resources burn: the ones with fuel properties.
    public static bool IsFuel(Resource r) => Props.ContainsKey(r);

    const double BaseCatchRateKgPerMinute = 0.05;
    const double CharcoalYieldPct = 0.04;
    public const double TorchBurnMinutes = 60;

    // ---- pit properties ----
    public static double MaxFuelKg(PitType pit) => pit switch { PitType.Mound => 15, PitType.Stone => 18, _ => 12 };
    public static double WindProtectionLevel(PitType pit) => pit switch { PitType.Mound => 0.4, PitType.Stone => 0.7, _ => 0 };
    static double FuelEfficiencyFactor(PitType pit) => pit == PitType.Stone ? 1.15 : 1.0;

    // ---- derived state ----
    public static double UnburnedKg(FireState f) => f.UnburnedKg.Values.Sum();
    public static double BurningKg(FireState f) => f.BurningKg.Values.Sum();
    public static double TotalKg(FireState f) => UnburnedKg(f) + BurningKg(f);
    public static bool IsActive(FireState f) => f.CarriedBy != null ? f.TorchMinutesLeft > 0 : BurningKg(f) > 0;

    public static FirePhase Phase(FireState f)
    {
        double burning = BurningKg(f);
        if (f.HasEmbers) return FirePhase.Embers;
        if (burning <= 0) return FirePhase.Cold;
        if (burning < 0.5) return FirePhase.Igniting;
        if (UnburnedKg(f) > burning * 0.5) return FirePhase.Building;
        if (burning > 4.0) return FirePhase.Roaring;
        if (burning > 1.5) return FirePhase.Steady;
        return FirePhase.Dying;
    }

    /// Combustion temperature, not heat felt.
    public static double TemperatureF(FireState f)
    {
        if (f.HasEmbers) return EmberTemperatureF(f);
        return BurningKg(f) > 0 ? ActiveTemperatureF(f) : 0;
    }

    static double ActiveTemperatureF(FireState f)
    {
        double burning = BurningKg(f);
        if (burning <= 0) return 0;
        double sizeFactor = burning < 0.5 ? 0.5 : burning < 1.0 ? 0.7 : burning < 2.0 ? 0.85 : burning < 4.0 ? 1.0 : 1.1;
        return Weighted(f, p => p.PeakTempF, fallback: 450) * sizeFactor;
    }

    static double EmberTemperatureF(FireState f)
    {
        if (f.EmberDurationHours <= 0) return Math.Max(200, f.EmberStartTempF * 0.3);
        return f.EmberStartTempF * Math.Sqrt(f.EmberHoursRemaining / f.EmberDurationHours); // cools slowly, then faster
    }

    static double Weighted(FireState f, Func<FuelProps, double> prop, double fallback)
    {
        double mass = 0, sum = 0;
        foreach (var (fuel, kg) in f.BurningKg) { sum += prop(Props[fuel]) * kg; mass += kg; }
        return mass > 0 ? sum / mass : fallback;
    }

    static double BurnSizeFactor(double burningKg) => burningKg < 1.5 ? 0.9 : burningKg < 4.0 ? 1.0 : burningKg < 7.0 ? 1.1 : 1.2;

    /// Before the stone pit's efficiency, matching the old display figure.
    public static double BurnRateKgPerHour(FireState f)
    {
        double burning = BurningKg(f);
        return burning > 0 ? Weighted(f, p => p.BurnRateKgPerHour, fallback: 1.0) * BurnSizeFactor(burning) : 0;
    }

    public static double BurningHoursRemaining(FireState f)
    {
        double rate = BurnRateKgPerHour(f);
        return rate > 0 ? BurningKg(f) / rate : 0;
    }

    /// Radiant heat in °F added to the ambient temperature. Embers radiate too.
    public static double HeatOutputF(FireState f, double ambientF)
    {
        if (f.CarriedBy != null)
            return f.TorchMinutesLeft > 0 ? 3.0 + 2.0 * (f.TorchMinutesLeft / TorchBurnMinutes) : 0;
        double differentialF = TemperatureF(f) - ambientF;
        if (differentialF <= 0) return 0;
        return differentialF / 50.0 * Math.Sqrt(Math.Max(BurningKg(f), 0.5));
    }

    // ---- world queries ----

    /// Everything fire does for one agent this minute, in °F (the old SurvivalContext and
    /// Location.GetTemperature rules). AirHeatF warms the tile's air, embers included, damped
    /// because open air carries heat away. PitHeatF is the radiant heat of flames on the tile,
    /// felt against that warmed air, before the activity's proximity. TorchHeatF warms only
    /// the carrier. A pit's walls cut the wind only while it burns.
    public readonly record struct Warmth(double AirHeatF, double PitHeatF, double TorchHeatF, double WindProtectionLevel);

    public static Warmth WarmthFor(World world, AgentId who)
    {
        var tile = world.Agent(who).Tile;
        // ponytail: tile insulation is 0 until phase 2 brings shelters; then factor = Clamp(insulation + 0.4, 0.4, 1).
        const double OpenAirHeatPct = 0.4;
        double torchHeatF = 0;
        var pits = new List<FireState>();
        foreach (var (id, f) in world.Fires)
        {
            if (f.CarriedBy == who) { RequireCurrent(world, id, f); torchHeatF += HeatOutputF(f, 0); }
            else if (f.CarriedBy == null && f.Tile == tile) { RequireCurrent(world, id, f); pits.Add(f); }
        }
        var burning = pits.Where(IsActive).ToList();
        double windProtection = burning.Count == 0 ? 0 : burning.Max(f => WindProtectionLevel(f.Pit));
        // Heat is felt against the air as weather leaves it, wind chill included (old Location.GetTemperatureBreakdown).
        double ambientF = Weather.AirAtF(world, tile, Weather.At(world, world.Minute), windProtection);
        double airHeatF = pits.Sum(f => HeatOutputF(f, ambientF) * OpenAirHeatPct);
        double pitHeatF = burning.Sum(f => HeatOutputF(f, ambientF + airHeatF));
        return new Warmth(airHeatF, pitHeatF, torchHeatF, windProtection);
    }

    /// Light enough to work by: flames on the tile, or a lit torch in hand. Embers are not light.
    public static bool HasLight(World world, AgentId who)
    {
        var tile = world.Agent(who).Tile;
        bool lit = false;
        foreach (var (id, f) in world.Fires)
        {
            if (f.CarriedBy != who && (f.CarriedBy != null || f.Tile != tile)) continue;
            RequireCurrent(world, id, f);
            lit |= IsActive(f);
        }
        return lit;
    }

    /// Old AbilityContext.FromFullContext: the load, the dark, and whether a flame lights the work.
    /// Here, beside HasLight, so work and fire both read it without depending on each other.
    /// Fires around the agent must be caught up.
    public static AbilityContext AbilityContextFor(World world, Agent agent) =>
        new(Items.EncumbrancePct(agent), Weather.DarknessLevel(world, agent.Tile), HasLight(world, agent.Id));

    public static FireId? PitAt(World world, TileId tile)
    {
        foreach (var (id, f) in world.Fires)
            if (f.CarriedBy == null && f.Tile == tile) return id;
        return null;
    }

    static void RequireCurrent(World world, FireId id, FireState f)
    {
        if (f.LastUpdatedMinute != world.Minute)
            throw new InvalidOperationException($"Fire {id.Value} is at minute {f.LastUpdatedMinute}, world at {world.Minute}: catch it up first");
    }

    // ---- fuel ----

    public static bool CanAddFuel(FireState f, Resource fuel) =>
        TotalKg(f) < MaxFuelKg(f.Pit) && TemperatureF(f) >= Props[fuel].MinFireTempF;

    /// Fuel goes on the unburned pile and catches over time. Hot embers relight at once.
    /// Returns the kg actually added (capped by pit capacity), or 0 when refused.
    public static double AddFuel(FireState f, Resource fuel, double kg)
    {
        if (kg <= 0) throw new ArgumentOutOfRangeException(nameof(kg), kg, "Fuel mass must be positive");
        if (!CanAddFuel(f, fuel)) return 0;
        double added = Math.Min(kg, MaxFuelKg(f.Pit) - TotalKg(f));
        f.UnburnedKg[fuel] = f.UnburnedKg.GetValueOrDefault(fuel) + added;

        if (f.HasEmbers && EmberTemperatureF(f) >= Props[fuel].MinFireTempF)
        {
            Transfer(f, fuel, added);
            f.HasEmbers = false;
            f.EmberHoursRemaining = 0;
            f.EmberDurationHours = 0;
        }
        return added;
    }

    /// Lights every unburned fuel that can catch from cold (tinder and kindling).
    public static void IgniteAll(FireState f)
    {
        foreach (var (fuel, kg) in f.UnburnedKg.ToList())
            if (Props[fuel].MinFireTempF == 0 && kg > 0) Transfer(f, fuel, kg);
    }

    static void Transfer(FireState f, Resource fuel, double kg)
    {
        double left = f.UnburnedKg.GetValueOrDefault(fuel) - kg;
        if (left <= 0.001) f.UnburnedKg.Remove(fuel); else f.UnburnedKg[fuel] = left;
        f.BurningKg[fuel] = f.BurningKg.GetValueOrDefault(fuel) + kg;
    }

    public static double CollectCharcoal(FireState f)
    {
        double kg = f.CharcoalKg;
        f.CharcoalKg = 0;
        return kg;
    }

    // ---- time ----

    /// Advances one fire to world.Minute, a minute at a time, emitting what happened when it happened.
    public static void CatchUp(World world, FireId id, List<WorldEvent> events)
    {
        var f = world.Fires[id];
        if (f.CarriedBy is AgentId carrier) f.Tile = world.Agent(carrier).Tile;
        while (f.LastUpdatedMinute < world.Minute)
        {
            if (!IsActive(f) && !f.HasEmbers) { f.LastUpdatedMinute = world.Minute; break; } // cold: nothing to do
            f.LastUpdatedMinute++;
            Tick(f, id, f.LastUpdatedMinute, events);
        }
        if (f.CarriedBy != null && f.TorchMinutesLeft <= 0) world.Fires.Remove(id); // a spent torch is ash
    }

    /// Catches up the pits on the agent's tile and the torches it carries.
    public static void CatchUpAround(World world, Agent agent, List<WorldEvent> events)
    {
        foreach (var (id, f) in world.Fires.ToList())
            if (f.CarriedBy == agent.Id || (f.CarriedBy == null && f.Tile == agent.Tile))
                CatchUp(world, id, events);
    }

    /// One minute of burning.
    public static void Tick(FireState f, FireId id, long minute, List<WorldEvent> events)
    {
        if (f.CarriedBy != null)
        {
            f.TorchMinutesLeft = Math.Max(0, f.TorchMinutesLeft - 1);
            if (f.TorchMinutesLeft == 0) events.Add(new FireDied(minute, id, f.Tile));
            return;
        }
        if (BurningKg(f) > 0)
        {
            Catch(f);
            Consume(f);
            if (BurningKg(f) <= 0) ToEmbers(f, id, minute, events);
        }
        else if (f.HasEmbers)
        {
            f.EmberHoursRemaining = Math.Max(0, f.EmberHoursRemaining - 1 / 60.0);
            if (f.EmberHoursRemaining <= 0)
            {
                if (f.BurnedSinceLitKg > 0.1)
                {
                    f.CharcoalKg += f.BurnedSinceLitKg * CharcoalYieldPct;
                    f.BurnedSinceLitKg = 0;
                }
                f.HasEmbers = false;
                f.EmberDurationHours = 0;
                f.EmberStartTempF = 0;
                events.Add(new FireDied(minute, id, f.Tile));
            }
        }
    }

    /// Unburned fuel catches faster in a hot, big fire; fast-burning fuel catches fast.
    static void Catch(FireState f)
    {
        double burning = BurningKg(f);
        if (f.UnburnedKg.Count == 0 || burning <= 0) return;
        double tempF = TemperatureF(f);
        double tempFactor = Math.Max(0.5, tempF / 400.0);
        double massFactor = 1.0 + burning * 0.15;
        foreach (var (fuel, kg) in f.UnburnedKg.ToList())
        {
            var p = Props[fuel];
            if (kg <= 0 || tempF < p.MinFireTempF) continue;
            double catching = Math.Min(kg, BaseCatchRateKgPerMinute * tempFactor * massFactor * p.BurnRateKgPerHour);
            if (catching > 0) Transfer(f, fuel, catching);
        }
    }

    static void Consume(FireState f)
    {
        double burning = BurningKg(f);
        f.LastBurningTempF = ActiveTemperatureF(f); // before consumption: the embers start from it
        double consumed = Math.Min(burning, BurnRateKgPerHour(f) / FuelEfficiencyFactor(f.Pit) / 60.0);
        double keepPct = 1.0 - consumed / burning;
        foreach (var fuel in f.BurningKg.Keys.ToList())
        {
            double left = f.BurningKg[fuel] * keepPct;
            if (left < 0.001) f.BurningKg.Remove(fuel); else f.BurningKg[fuel] = left;
        }
        f.BurnedSinceLitKg += consumed;
    }

    static void ToEmbers(FireState f, FireId id, long minute, List<WorldEvent> events)
    {
        f.EmberStartTempF = f.LastBurningTempF;
        f.HasEmbers = true;
        f.EmberDurationHours = Math.Max(0.25, f.EmberStartTempF / 600.0 * 0.5);
        f.EmberHoursRemaining = f.EmberDurationHours;
        f.BurningKg.Clear();
        events.Add(new FireBurnedToEmbers(minute, id, f.Tile));
    }

    // ---- activities ----

    /// Fire-start odds. Dexterity folds in manipulation, wetness, darkness and vitality.
    public static double StartChancePct(ToolType tool, Resource tinder, int skillLevel = 0, bool consciousnessImpaired = false, double dexterityPct = 1.0)
    {
        double chance = tool switch
        {
            ToolType.FireStriker => 0.90,
            ToolType.BowDrill => 0.50,
            ToolType.HandDrill => 0.35,
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Not a fire starter"),
        };
        chance += skillLevel * 0.1 + Props[tinder].IgnitionBonusPct;
        if (consciousnessImpaired) chance -= 0.2;
        chance -= (1.0 - dexterityPct) * 0.5;
        return Math.Clamp(chance, 0.05, 0.95);
    }

    /// Why this fire activity cannot be done right now, or null. Checked before any time passes
    /// or anything changes, so a refusal costs nothing. Fires around the agent must be caught up.
    /// Everything put on a fire comes out of the agent's pack.
    public static string? Refusal(World world, Activity activity)
    {
        var tile = world.Agent(activity.Who).Tile;
        var pack = Items.Pack(world, activity.Who);
        var pit = PitAt(world, tile) is FireId id ? world.Fires[id] : null;
        switch (activity)
        {
            case StartFire a:
                if (!IsFuel(a.Tinder) || Props[a.Tinder].MinFireTempF != 0) throw new ArgumentException($"{a.Tinder} cannot light from cold", nameof(activity));
                if (a.TinderKg <= 0 || a.KindlingKg < 0) throw new ArgumentOutOfRangeException(nameof(activity), "Tinder must be positive and kindling not negative");
                if (pit != null)
                {
                    if (pit.Pit != a.Pit) return $"the pit here is {pit.Pit}, not {a.Pit}";
                    if (!CanAddFuel(pit, a.Tinder)) return "the fire is full";
                    if (IsActive(pit)) return "the fire is already burning";
                }
                if (Items.Tool(pack, a.Tool) == null) return $"no working {a.Tool}";
                if (Items.Have(pack, a.Tinder) < a.TinderKg - 1e-9) return $"not enough {a.Tinder}";
                if (Items.Have(pack, Resource.Kindling) < a.KindlingKg - 1e-9) return "not enough kindling";
                return null;
            case AddFuel a:
                if (!IsFuel(a.Fuel)) return $"{a.Fuel} does not burn";
                if (a.Kg <= 0) throw new ArgumentOutOfRangeException(nameof(activity), a.Kg, "Fuel mass must be positive");
                if (pit == null) return "no fire here";
                if (Items.Have(pack, a.Fuel) <= 0) return $"no {a.Fuel} to add";
                if (CanAddFuel(pit, a.Fuel)) return null;
                return TotalKg(pit) >= MaxFuelKg(pit.Pit) ? "the fire is full" : $"the fire is not hot enough for {a.Fuel}";
            case LightTorch a:
                if (!HasLight(world, a.Who)) return "no flame to light a torch from";
                return Items.Tool(pack, ToolType.Torch) == null ? "no torch to light" : null;
            case GatherCharcoal:
                return pit is { CharcoalKg: > 0 } ? null : "no charcoal here";
            default:
                throw new NotSupportedException($"{activity.GetType().Name} is not a fire activity");
        }
    }

    /// Does the one-shot part of a fire activity Refusal allowed. Returns "fire did not catch"
    /// when the ignition roll fails: the attempt still happened and cost its minute.
    public static string? Act(World world, Activity activity, List<WorldEvent> events)
    {
        if (Refusal(world, activity) is string refused) throw new InvalidOperationException($"Fire.Act on a refused activity: {refused}");
        return activity switch
        {
            StartFire a => Start(world, a, events),
            AddFuel a => Feed(world, a, events),
            LightTorch a => Torch(world, a, events),
            GatherCharcoal a => Collect(world, a, events),
            _ => throw new NotSupportedException($"{activity.GetType().Name} is not a fire activity"),
        };
    }

    /// Old FireHandler.AttemptStartFire: the tinder is spent on every attempt, the kindling only
    /// when it catches. Dexterity carries hands, wetness, darkness and vitality.
    static string? Start(World world, StartFire a, List<WorldEvent> events)
    {
        var agent = world.Agent(a.Who);
        var tile = agent.Tile;
        var pack = Items.Pack(world, a.Who);
        var body = agent.Body;
        double chance = StartChancePct(a.Tool, a.Tinder,
            consciousnessImpaired: Body.GetCapacities(body).Consciousness < 0.5,
            dexterityPct: Body.Dexterity(body, AbilityContextFor(world, agent)));
        Items.Take(pack, a.Tinder, a.TinderKg);
        Items.Use(Items.Tool(pack, a.Tool)!);
        if (world.Rng("fire").NextDouble() >= chance)
        {
            events.Add(new FireStartFailed(world.Minute, a.Who, tile, chance));
            return "fire did not catch";
        }
        var id = PitAt(world, tile) ?? NewFire(world, new FireState { Tile = tile, LastUpdatedMinute = world.Minute, Pit = a.Pit });
        var f = world.Fires[id];
        // What the pit has no room for stays in the pack, as Feed does. Old game: it popped the
        // kindling and tinder and the pit's capacity cap destroyed what did not fit (bug, fixed).
        Items.Add(pack, a.Tinder, a.TinderKg - AddFuel(f, a.Tinder, a.TinderKg));
        if (a.KindlingKg > 0) Items.Take(pack, Resource.Kindling, AddFuel(f, Resource.Kindling, a.KindlingKg));
        IgniteAll(f);
        events.Add(new FireLit(world.Minute, id, tile, a.Who));
        return null;
    }

    /// At most what the pack holds goes on, and what the pit has no room for stays in the pack.
    static string? Feed(World world, AddFuel a, List<WorldEvent> events)
    {
        var tile = world.Agent(a.Who).Tile;
        var pack = Items.Pack(world, a.Who);
        var id = PitAt(world, tile)!.Value;
        var f = world.Fires[id];
        bool wasEmbers = f.HasEmbers;
        double added = AddFuel(f, a.Fuel, Math.Min(a.Kg, Items.Have(pack, a.Fuel)));
        Items.Take(pack, a.Fuel, added);
        events.Add(new FuelAdded(world.Minute, id, a.Who, a.Fuel, added));
        if (wasEmbers && !f.HasEmbers) events.Add(new FireLit(world.Minute, id, tile, a.Who));
        return null;
    }

    /// From flames on the tile, or from the torch already in hand (which it replaces). Uses up a torch from the pack.
    static string? Torch(World world, LightTorch a, List<WorldEvent> events)
    {
        var tile = world.Agent(a.Who).Tile;
        var pack = Items.Pack(world, a.Who);
        pack.Gear.Remove(Items.Tool(pack, ToolType.Torch)!);
        foreach (var (old, f) in world.Fires.ToList())
            if (f.CarriedBy == a.Who) world.Fires.Remove(old);
        var id = NewFire(world, new FireState { Tile = tile, LastUpdatedMinute = world.Minute, CarriedBy = a.Who, TorchMinutesLeft = TorchBurnMinutes });
        events.Add(new FireLit(world.Minute, id, tile, a.Who));
        return null;
    }

    /// The charcoal a dead fire left behind goes into the pack (old ember collection).
    static string? Collect(World world, GatherCharcoal a, List<WorldEvent> events)
    {
        var agent = world.Agent(a.Who);
        var gained = new Inventory();
        Items.Add(gained, Resource.Charcoal, CollectCharcoal(world.Fires[PitAt(world, agent.Tile)!.Value]));
        Items.Receive(world, agent, gained, events);
        events.Add(new Gathered(world.Minute, a.Who, agent.Tile, Items.Describe(gained)));
        return null;
    }

    static FireId NewFire(World world, FireState f)
    {
        var id = new FireId(world.NextFireId++);
        world.Fires[id] = f;
        return id;
    }
}
