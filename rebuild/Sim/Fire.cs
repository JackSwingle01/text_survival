namespace Survival;

public enum PitType { Open, Mound, Stone }

/// Derived from FireState, never stored.
public enum FirePhase { Cold, Igniting, Building, Roaring, Steady, Dying, Embers }

/// One row of Content/fire.json fuels, temperatures in °F. A class, not a struct:
/// the reader builds a struct row from its parameterless constructor and would take a missing field as 0.
public sealed record FuelProps(double PeakTempF, double BurnRateKgPerHour, double MinFireTempF, double IgnitionBonusPct);

/// What a fire forecast is asked (Fire.Forecast): the pit as it is now (null: none yet) and the type a new one would
/// have, the fuel on offer (pieces included), the starter a strike would use (null: none), and the hours wanted.
public sealed record FireAsk(FireState? Pit, PitType NewPit, Inventory Fuel, ToolType? Starter, double HoursWanted, int FirecraftLevel = 0, double DexterityPct = 1.0);

/// Bridge: fuel on offer never gets hot enough to catch. Hours: everything catches, and it is not enough.
public enum FireNeedKind { Starter, Tinder, Bridge, Hours }

/// What a fire forecast says must be added: any one of AnyOf, Kg of the first (as Items.AddKg puts it in pieces).
public sealed record FireNeed(FireNeedKind Kind, IReadOnlyList<Resource> AnyOf, double Kg);

public readonly record struct FuelToAdd(Resource Fuel, double Kg);

/// The strike a fire forecast assumes for a cold pit: TinderKg of Tinder and up to KindlingKg of sticks.
public sealed record FireStrike(Resource Tinder, double TinderKg, double KindlingKg);

/// StrikePct is 1 when the pit burns or glows. LeastHeatF: the least Fire.HeatOutputF over the hours, against 0 °F air.
/// Missing is empty when the hours reach those wanted. Next is null for a cold pit, which has the Strike assumed.
public sealed record FireForecast(double StrikePct, double Hours, double LeastHeatF, IReadOnlyList<FireNeed> Missing, FuelToAdd? Next, FireStrike? Strike);

public sealed record PitProps(int Room, double MaxFuelKg, double WindProtectionLevel, double FuelEfficiencyFactor);

/// What flame does to an animal's nerve (Content/fire.json deterrence, read by Combat.FireDeterrence).
public sealed record DeterrenceProps(double FireFear, double TorchIntensityPct, double FullIntensityHeatF, double FullWithinM);

/// A fire pit on a tile, or a torch carried by an agent (CarriedBy set; the two-mass fields are then unused).
public sealed class FireState
{
    public required TileId Tile { get; set; }
    public required long LastUpdatedMinute { get; set; }
    public PitType Pit { get; set; }
    // Two masses: fuel waiting to catch, and fuel burning. Sorted, so the sums add up in one order however
    // the fuels came and went (see World.Tiles).
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
    /// Who struck this fire, until it is sure to take a log or goes out; Fire.PayLighter pays their Firecraft then.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public AgentId? LitBy { get; set; }
    /// A pit: who made it, by lighting the first fire in it or digging it. Null on a carried torch.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Cause? Cause { get; init; }
}

public static class Fire
{
    public static readonly IReadOnlyDictionary<Resource, FuelProps> Props = Content.Tables.Fuels;
    static readonly IReadOnlyDictionary<PitType, PitProps> Pits = Content.Tables.Pits;
    public static readonly DeterrenceProps Deterrence = Content.Tables.Deterrence;

    public static bool IsFuel(Resource r) => Props.ContainsKey(r);

    const double BaseCatchRateKgPerMinute = 0.05;
    const double CharcoalYieldPct = 0.04;
    const double TorchFadeMinutes = 60;

    public static int Room(PitType pit) => Pits[pit].Room;
    public static double MaxFuelKg(PitType pit) => Pits[pit].MaxFuelKg;
    public static double WindProtectionLevel(PitType pit) => Pits[pit].WindProtectionLevel;
    static double FuelEfficiencyFactor(PitType pit) => Pits[pit].FuelEfficiencyFactor;

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

    /// Whether a fire seen in this phase is burning (IsActive, for a pit): flames, not embers.
    public static bool Burns(FirePhase? phase) => phase is not (null or FirePhase.Cold or FirePhase.Embers);

    /// Combustion temperature, not heat felt.
    public static double TemperatureF(FireState f)
    {
        if (f.HasEmbers) return EmberTemperatureF(f);
        return BurningKg(f) > 0 ? ActiveTemperatureF(f) : 0;
    }

    static double ActiveTemperatureF(FireState f)
    {
        double burning = BurningKg(f);
        return burning > 0 ? Weighted(f, p => p.PeakTempF, fallback: 450) * TempSizeFactor(burning) : 0;
    }

    static double TempSizeFactor(double burningKg) =>
        burningKg < 0.5 ? 0.5 : burningKg < 1.0 ? 0.7 : burningKg < 2.0 ? 0.85 : burningKg < 4.0 ? 1.0 : 1.1;

    /// The hottest flame, not the mass-weighted mean: new fuel catches off the burning oak beside it, so
    /// tinder thrown on a hot fire never stops it taking bone.
    public static double CatchTemperatureF(FireState f)
    {
        if (f.HasEmbers) return EmberTemperatureF(f);
        double burning = BurningKg(f);
        return burning > 0 ? f.BurningKg.Where(x => x.Value > 0).Max(x => Props[x.Key].PeakTempF) * TempSizeFactor(burning) : 0;
    }

    static double EmberTemperatureF(FireState f)
    {
        if (f.EmberDurationHours <= 0) return Math.Max(200, f.EmberStartTempF * 0.3);
        return f.EmberStartTempF * Math.Sqrt(f.EmberHoursRemaining / f.EmberDurationHours); // cools slowly, then faster
    }

    /// From the lowest fuel up, so equal values average exactly: an ulp can tip a whole ember minute.
    static double Weighted(FireState f, Func<FuelProps, double> prop, double fallback)
    {
        if (f.BurningKg.Count == 0) return fallback;
        double lowest = f.BurningKg.Keys.Min(fuel => prop(Props[fuel]));
        double mass = 0, sum = 0;
        foreach (var (fuel, kg) in f.BurningKg) { sum += (prop(Props[fuel]) - lowest) * kg; mass += kg; }
        return mass > 0 ? lowest + sum / mass : fallback;
    }

    static readonly (double FromKg, double Factor)[] BurnSizeBands = [(7.0, 1.2), (4.0, 1.1), (1.5, 1.0), (0, 0.9)];

    static double BurnSizeFactor(double burningKg) => BurnSizeBands.First(b => burningKg >= b.FromKg).Factor;

    /// Kg an hour before the stone pit's efficiency: mass-weighted mean table rate at the fire's size.
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

    /// What someone judging how long wood will last reckons with (BurnSizeFactor runs 0.9-1.2).
    public const double ReckonedBurnSizeFactor = 1.1;

    // Reckoned as a woodpile is judged: ignores a stone pit's efficiency and a small fire's slower burn; both under 15%.
    public static double FuelHours(Resource fuel, double kg) => kg / (Props[fuel].BurnRateKgPerHour * ReckonedBurnSizeFactor);

    public static double FuelHours(FireState f) => f.UnburnedKg.Concat(f.BurningKg).Sum(x => FuelHours(x.Key, x.Value));

    /// Radiant heat in °F added to the ambient temperature. Embers radiate too.
    public static double HeatOutputF(FireState f, double ambientF)
    {
        if (f.CarriedBy != null)
            return f.TorchMinutesLeft > 0 ? 3.0 + 2.0 * Math.Min(1, f.TorchMinutesLeft / TorchFadeMinutes) : 0;
        double differentialF = TemperatureF(f) - ambientF;
        if (differentialF <= 0) return 0;
        return differentialF / 50.0 * Math.Sqrt(Math.Max(BurningKg(f), 0.5));
    }

    /// For what fears flame: measured against 0 °F air, so it is the flame's own, not the weather's. Embers give none.
    public static double IntensityPct(FireState f) =>
        !IsActive(f) || f.HasEmbers ? 0
        : f.CarriedBy != null ? Deterrence.TorchIntensityPct
        : Math.Min(1, HeatOutputF(f, ambientF: 0) / Deterrence.FullIntensityHeatF);

    /// A burning pit (Holder null) or torch on a tile, and how fiercely it burns.
    public readonly record struct Flame(AgentId? Holder, double IntensityPct);

    public static List<Flame> FlamesOn(World world, TileId tile)
    {
        var flames = new List<Flame>();
        foreach (var (id, f) in world.Fires)
        {
            if (!OnTile(world, f, tile)) continue;
            RequireCurrent(world, id, f);
            if (IntensityPct(f) is double pct and > 0) flames.Add(new Flame(f.CarriedBy, pct));
        }
        return flames;
    }

    /// Every pit in the world brought up to the clock. A pit lives only when someone near it catches it
    /// up (CatchUpAround, CatchUpOn); a world watched with nobody in it (Observer mode) needs them all
    /// current to show them. Path-independent, like every catch-up: it changes no outcome.
    public static void CatchUpAll(World world, List<WorldEvent> events)
    {
        foreach (var (id, f) in world.Fires.ToList())
            if (f.CarriedBy == null) CatchUp(world, id, events); // a torch goes with its carrier
    }

    /// FlamesOn requires this first.
    public static void CatchUpOn(World world, TileId tile, List<WorldEvent> events)
    {
        foreach (var (id, f) in world.Fires.ToList())
            if (OnTile(world, f, tile)) CatchUp(world, id, events);
    }

    /// A torch's own Tile is only as fresh as its last catch-up, so it is placed by its living carrier.
    static bool OnTile(World world, FireState f, TileId tile) =>
        f.CarriedBy is AgentId carrier ? world.Agents.TryGetValue(carrier, out var a) && a.Tile == tile && Sim.IsAlive(a) : f.Tile == tile;

    /// What fire does for one agent, in °F. AirHeatF warms the tile's air (embers included), damped because open air
    /// carries heat away. PitHeatF is flames' radiant heat felt against that warmed air, before activity proximity.
    /// TorchHeatF warms only the carrier. ShelterHeatF is the heat a shelter holds in beyond the open air's share;
    /// withheld with the radiant heat as someone warms to a sweat, since they can step out of hot air.
    public readonly record struct Warmth(double AirHeatF, double PitHeatF, double TorchHeatF, double WindProtectionLevel, double ShelterHeatF = 0);

    const double OpenAirHeatPct = 0.4;

    /// cover: the shelter around someone keeping still; its insulation keeps the fire's heat in.
    public static Warmth WarmthFor(World world, AgentId who, ShelterCover cover = default) =>
        Exposure.At(world, world.Agent(who).Tile, Cover.In(cover, who)).Warmth;

    /// Walls keep wind off only while the pit burns. All caught up.
    internal readonly record struct FiresAround(List<FireState> Pits, double TorchHeatF, double WallsLevel);

    internal static FiresAround Around(World world, TileId tile, AgentId? holder)
    {
        double torchHeatF = 0;
        var pits = new List<FireState>();
        foreach (var (id, f) in world.Fires)
        {
            if (holder != null && f.CarriedBy == holder) { RequireCurrent(world, id, f); torchHeatF += HeatOutputF(f, 0); }
            else if (f.CarriedBy == null && f.Tile == tile) { RequireCurrent(world, id, f); pits.Add(f); }
        }
        return Of(pits, torchHeatF);
    }

    /// pit: as it is at the minute asked about (a forecast's copy, or caught up); no torch.
    internal static FiresAround Beside(FireState? pit) => Of(pit == null ? [] : [pit], 0);

    static FiresAround Of(List<FireState> pits, double torchHeatF)
    {
        var burning = pits.Where(IsActive).ToList();
        return new FiresAround(pits, torchHeatF, burning.Count == 0 ? 0 : burning.Max(f => WindProtectionLevel(f.Pit)));
    }

    /// Felt against the air as weather and shelter leave it, wind chill included.
    internal static Warmth WarmthAgainst(FiresAround fires, double ambientF, double insulation)
    {
        // Open air carries 60% of the heat away; a shelter's insulation keeps more of it in.
        double heldPct = Math.Clamp(insulation + OpenAirHeatPct, OpenAirHeatPct, 1.0);
        double heatF = fires.Pits.Sum(f => HeatOutputF(f, ambientF));
        double airHeatF = heatF * OpenAirHeatPct, shelterHeatF = heatF * heldPct - airHeatF;
        double pitHeatF = fires.Pits.Where(IsActive).Sum(f => HeatOutputF(f, ambientF + airHeatF + shelterHeatF));
        return new Warmth(airHeatF, pitHeatF, fires.TorchHeatF, fires.WallsLevel, shelterHeatF);
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

    /// The pit on a tile as it is now. It must have been caught up, as anything read off a fire must be.
    public static FireState? CurrentPitAt(World world, TileId tile)
    {
        if (PitAt(world, tile) is not FireId id) return null;
        var f = world.Fires[id];
        RequireCurrent(world, id, f);
        return f;
    }

    internal static void RequireCurrent(World world, FireId id, FireState f)
    {
        if (f.LastUpdatedMinute != world.Minute)
            throw new InvalidOperationException($"Fire {id.Value} is at minute {f.LastUpdatedMinute}, world at {world.Minute}: catch it up first");
    }

    public static bool CanAddFuel(FireState f, Resource fuel) =>
        TotalKg(f) < MaxFuelKg(f.Pit) && CatchTemperatureF(f) >= Props[fuel].MinFireTempF;

    /// Fuel catches over time; hot embers relight at once. Returns kg added (capped by pit capacity), 0 when refused.
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

    public static void IgniteAll(FireState f)
    {
        foreach (var (fuel, kg) in f.UnburnedKg.ToList())
            if (Props[fuel].MinFireTempF == 0 && kg > 0) Transfer(f, fuel, kg);
    }

    /// Under this, a stack is dust: it goes with the rest of its fuel, never off the ledger.
    const double DustKg = 0.001;

    /// A last gram left waiting catches with it, never deleted.
    static void Transfer(FireState f, Resource fuel, double kg)
    {
        double left = f.UnburnedKg.GetValueOrDefault(fuel) - kg;
        if (left <= DustKg) { f.UnburnedKg.Remove(fuel); kg += Math.Max(0, left); }
        else f.UnburnedKg[fuel] = left;
        f.BurningKg[fuel] = f.BurningKg.GetValueOrDefault(fuel) + kg;
    }

    public static double CollectCharcoal(FireState f)
    {
        double kg = f.CharcoalKg;
        f.CharcoalKg = 0;
        return kg;
    }

    /// A minute at a time, emitting what happened when it happened.
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

    /// Torches whose carrier died or left go out where they fell (Remains.Hour); nothing else would ever catch them up.
    internal static void PutOutDroppedTorches(World world)
    {
        foreach (var (id, f) in world.Fires.ToList())
            if (f.CarriedBy is AgentId carrier && !(world.Agents.TryGetValue(carrier, out var a) && Sim.IsAlive(a)))
                world.Fires.Remove(id);
    }

    public static void CatchUpAround(World world, Agent agent, List<WorldEvent> events)
    {
        foreach (var (id, f) in world.Fires.ToList())
            if (f.CarriedBy == agent.Id || (f.CarriedBy == null && f.Tile == agent.Tile))
                CatchUp(world, id, events);
    }

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

    static void Catch(FireState f)
    {
        double burning = BurningKg(f);
        if (f.UnburnedKg.Count == 0 || burning <= 0) return;
        double tempFactor = Math.Max(0.5, TemperatureF(f) / 400.0);
        double catchF = CatchTemperatureF(f);
        double massFactor = 1.0 + burning * 0.15;
        foreach (var (fuel, kg) in f.UnburnedKg.ToList())
        {
            var p = Props[fuel];
            if (kg <= 0 || catchF < p.MinFireTempF) continue;
            double catching = Math.Min(kg, BaseCatchRateKgPerMinute * tempFactor * massFactor * p.BurnRateKgPerHour);
            if (catching > 0) Transfer(f, fuel, catching);
        }
    }

    /// Shares the minute's burn (BurnRateKgPerHour) among fuels by table rate times mass, read at the start of the
    /// minute: a fast fuel goes first, so tinder does not drag an oak log down with it. A last gram burns with its stack.
    static void Consume(FireState f)
    {
        double burning = BurningKg(f);
        f.LastBurningTempF = ActiveTemperatureF(f); // before consumption: the embers start from it
        double consumed = MinuteBurnKg(burning, Weighted(f, p => p.BurnRateKgPerHour, fallback: 1.0), FuelEfficiencyFactor(f.Pit));
        double weight = f.BurningKg.Sum(x => Props[x.Key].BurnRateKgPerHour * x.Value);
        var shares = f.BurningKg.ToDictionary(x => x.Key, x => consumed * (Props[x.Key].BurnRateKgPerHour * x.Value / weight));
        foreach (var (fuel, share) in shares)
        {
            double kg = f.BurningKg[fuel];
            double burned = Math.Min(kg, share);
            double left = kg * (1.0 - burned / kg); // a fraction of the stack, not kg - burned: one fuel alone burns bit for bit as a fraction rule
            if (left < DustKg) { burned = kg; f.BurningKg.Remove(fuel); }
            else f.BurningKg[fuel] = left;
            f.BurnedSinceLitKg += burned;
        }
    }

    /// A minute crossing a size band burns at the bigger band's rate only until the edge, then the smaller's:
    /// billing it all at the bigger rate let a fire with a gram more fuel go out a minute sooner.
    static double MinuteBurnKg(double burningKg, double meanRateKgPerHour, double efficiency)
    {
        double kg = burningKg, minutes = 1.0, burned = 0;
        int band = Array.FindIndex(BurnSizeBands, b => kg >= b.FromKg);
        while (true)
        {
            double perMinuteKg = meanRateKgPerHour * BurnSizeBands[band].Factor / efficiency / 60.0;
            double toEdgeKg = kg - BurnSizeBands[band].FromKg;
            if (band == BurnSizeBands.Length - 1 || perMinuteKg * minutes <= toEdgeKg)
                return Math.Min(burningKg, burned + perMinuteKg * minutes);
            burned += toEdgeKg;
            minutes -= toEdgeKg / perMinuteKg;
            kg = BurnSizeBands[band].FromKg;
            band++;
        }
    }

    static void ToEmbers(FireState f, FireId id, long minute, List<WorldEvent> events)
    {
        f.LitBy = null; // out before it took a log: nothing more to learn from it
        f.EmberStartTempF = f.LastBurningTempF;
        f.HasEmbers = true;
        f.EmberDurationHours = Math.Max(0.25, f.EmberStartTempF / 600.0 * 0.5);
        f.EmberHoursRemaining = f.EmberDurationHours;
        f.BurningKg.Clear();
        events.Add(new FireBurnedToEmbers(minute, id, f.Tile));
    }

    /// Odds as terms: tool base, skill, tinder, up to -50% clumsy hands (a foggy head among them); clamped 5-95%.
    /// handsWhy names what is costing dexterity, for the sheet.
    public static Odds StartOdds(ToolType tool, Resource tinder, int skillLevel = 0, double dexterityPct = 1.0, string handsWhy = "")
    {
        double basePct = tool switch
        {
            ToolType.FireStriker => 0.90,
            ToolType.BowDrill => 0.50,
            ToolType.HandDrill => 0.35,
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Not a fire starter"),
        };
        return new Odds(Names.Title(tool), basePct, Odds.Moving(
            OddsTerm.Add("Firecraft", Skills.FirecraftPct(skillLevel)),
            OddsTerm.Add(Names.Title(tinder), Props[tinder].IgnitionBonusPct),
            OddsTerm.Add(handsWhy.Length > 0 ? $"Hands ({handsWhy})" : "Hands", -(1.0 - dexterityPct) * 0.5)), MinPct: 0.05, MaxPct: 0.95);
    }

    public static double StartChancePct(ToolType tool, Resource tinder, int skillLevel = 0, double dexterityPct = 1.0) =>
        StartOdds(tool, tinder, skillLevel, dexterityPct).TotalPct;

    /// Fires around the agent must be caught up (the light they work by).
    public static Odds StartOdds(World world, Agent agent, ToolType tool, Resource tinder)
    {
        var body = agent.Body;
        var context = AbilityContextFor(world, agent);
        return StartOdds(tool, tinder, Skills.Level(agent, Skill.Firecraft),
            dexterityPct: Body.Dexterity(body, context), handsWhy: string.Join(", ", Body.DexterityCosts(body, context)));
    }

    /// Checked before any time passes, so a refusal costs nothing. Fires around the agent must be caught up.
    public static Refusal? Refusal(World world, Activity activity)
    {
        var tile = world.Agent(activity.Who).Tile;
        var pack = Items.Pack(world, activity.Who);
        var pitId = PitAt(world, tile);
        var pit = pitId is FireId id ? world.Fires[id] : null;
        // The pit is the thing reached: asked first, once it is there.
        Refusal? Reach() => Access.Refusal(world, activity.Who, new LocationFeature(LocationFeatureKind.Fire, tile, pitId!.Value.Value));
        switch (activity)
        {
            case StartFire a:
                if (!IsFuel(a.Tinder) || Props[a.Tinder].MinFireTempF != 0) throw new ArgumentException($"{a.Tinder} cannot light from cold", nameof(activity));
                if (a.TinderKg <= 0 || a.KindlingKg < 0) throw new ArgumentOutOfRangeException(nameof(activity), "Tinder must be positive and kindling not negative");
                if (pit != null)
                {
                    if (Reach() is { } barred) return barred;
                    if (!CanAddFuel(pit, a.Tinder)) return Full(pit, a.TinderKg);
                    if (Items.LightestKg(pack, a.Tinder) is double tinderPieceKg && tinderPieceKg > RoomKg(pit) + 1e-9)
                        return new($"the fire has no room for a whole {Names.PieceWord(a.Tinder)}", new MissingState(Lack.FireRoomKg, tinderPieceKg, RoomKg(pit)));
                    if (IsActive(pit)) return new("the fire is already burning");
                }
                if (Items.Tool(pack, a.Tool) == null) return new($"no working {Names.Of(a.Tool)}", new MissingTool(a.Tool));
                if (Items.Have(pack, a.Tinder) < a.TinderKg - 1e-9)
                    return new($"not enough {Names.Of(a.Tinder)}", new MissingResource(a.Tinder, a.TinderKg - Items.Have(pack, a.Tinder)));
                if (Items.Have(pack, Resource.Stick) < a.KindlingKg - 1e-9)
                    return new("not enough sticks", new MissingResource(Resource.Stick, a.KindlingKg - Items.Have(pack, Resource.Stick)));
                return null;
            case AddFuel a:
                if (!IsFuel(a.Fuel)) return new($"{Names.Cap(Names.Of(a.Fuel))} does not burn");
                if (a.Kg <= 0) throw new ArgumentOutOfRangeException(nameof(activity), a.Kg, "Fuel mass must be positive");
                if (pit == null) return new("no fire here", new MissingState(Lack.BurningFire));
                if (Reach() is { } fuelBarred) return fuelBarred;
                if (Items.Have(pack, a.Fuel) <= 0) return new($"no {Names.Of(a.Fuel)} to add", new MissingResource(a.Fuel, a.Kg));
                // A piece goes on whole or not at all: a nearly full fire refuses one that won't fit.
                if (Items.LightestKg(pack, a.Fuel) is double pieceKg && pieceKg > RoomKg(pit) + 1e-9)
                    return new($"the fire has no room for a whole {Names.PieceWord(a.Fuel)}", new MissingState(Lack.FireRoomKg, pieceKg, RoomKg(pit)));
                double topUpKg = Math.Min(a.Kg, Items.Have(pack, a.Fuel));
                if (!Items.IsDiscrete(a.Fuel) && IsSliver(pit, topUpKg)) return Full(pit, Math.Min(topUpKg, SliverKg));
                if (CanAddFuel(pit, a.Fuel)) return null;
                return new($"the fire is not hot enough for {Names.Of(a.Fuel)}",
                    new MissingState(Lack.FireHeatF, Props[a.Fuel].MinFireTempF, CatchTemperatureF(pit)));
            case LightTorch a:
                if (!HasLight(world, a.Who)) return new("no flame to light a torch from", new MissingState(Lack.Light));
                return Items.Tool(pack, ToolType.Torch) == null ? new("no torch to light", new MissingTool(ToolType.Torch)) : null;
            case GatherCharcoal:
                if (pit == null) return new("no charcoal here");
                return Reach() ?? (pit.CharcoalKg > 0 ? null : new("no charcoal here"));
            default:
                throw new NotSupportedException($"{activity.GetType().Name} is not a fire activity");
        }
    }

    /// Less room than this, when the top-up offered is bigger, is a sliver: too little to matter, so the pit counts as full.
    public const double SliverKg = 0.25;

    static bool IsSliver(FireState pit, double offeredKg) => RoomKg(pit) < Math.Min(offeredKg, SliverKg);

    static double RoomKg(FireState f) => Math.Max(0, MaxFuelKg(f.Pit) - TotalKg(f));

    static Refusal Full(FireState pit, double needKg) => new("the fire is full", new MissingState(Lack.FireRoomKg, needKg, RoomKg(pit)));

    /// A strike made and failed: the attempt is over, and cost its minute.
    public const string DidNotCatch = "fire did not catch";

    /// Returns DidNotCatch when the ignition roll fails: the attempt still happened and cost its minute.
    public static string? Act(World world, Activity activity, List<WorldEvent> events)
    {
        if (Refusal(world, activity) is { } refused) throw new InvalidOperationException($"Fire.Act on a refused activity: {refused}");
        return activity switch
        {
            StartFire a => Start(world, a, events),
            AddFuel a => Feed(world, a, events),
            LightTorch a => Torch(world, a, events),
            GatherCharcoal a => Collect(world, a, events),
            _ => throw new NotSupportedException($"{activity.GetType().Name} is not a fire activity"),
        };
    }

    /// The tinder is spent on every attempt, the kindling only when it catches.
    static string? Start(World world, StartFire a, List<WorldEvent> events)
    {
        var agent = world.Agent(a.Who);
        var tile = agent.Tile;
        var pack = Items.Pack(world, a.Who);
        double chance = StartOdds(world, agent, a.Tool, a.Tinder).TotalPct;
        // A discrete tinder (chaga) is spent in whole pieces, no more than the pit has room for.
        double roomKg = PitAt(world, tile) is FireId there ? RoomKg(world.Fires[there]) : MaxFuelKg(a.Pit);
        double tinderKg = TakeTinder(pack, a.Tinder, a.TinderKg, roomKg);
        var tool = Items.Tool(pack, a.Tool)!;
        Items.Use(tool);
        if (world.Rng("fire", a.Who).NextDouble() >= chance)
        {
            events.Add(new FireStartFailed(world.Minute, a.Who, tile, chance));
            Skills.Award(world, agent, Skill.Firecraft, Skills.FireFailureXp, events);
            return DidNotCatch;
        }
        var id = PitAt(world, tile) ?? NewFire(world, new FireState
        {
            Tile = tile,
            LastUpdatedMinute = world.Minute,
            Pit = a.Pit,
            Cause = new Cause(a.Who, $"lit by {Cause.Describe(world, a.Who)} with a {Items.Def(tool).Name.ToLowerInvariant()}"),
        });
        var f = world.Fires[id];
        Kindle(f, pack, a.Tinder, tinderKg, a.KindlingKg);
        events.Add(new FireLit(world.Minute, id, tile, a.Who));
        // A catching strike pays a miss's worth now, the rest when the fire takes wood (PayLighter), so tinder with no fuel to follow cannot farm levels.
        Skills.Award(world, agent, Skill.Firecraft, Skills.FireFailureXp, events);
        f.LitBy = a.Who;
        PayLighter(world, f, events); // struck onto logs already in the pit
        return null;
    }

    /// A caught strike: the tinder already taken from the pack, then up to kindlingKg of sticks, and all of it alight.
    /// What the pit has no room for stays in the pack, as Feed does; a piece of tinder goes in whole.
    static void Kindle(FireState f, Inventory pack, Resource tinder, double tinderKg, double kindlingKg)
    {
        double tinderIn = AddFuel(f, tinder, tinderKg);
        if (Items.IsDiscrete(tinder)) f.UnburnedKg[tinder] = f.UnburnedKg.GetValueOrDefault(tinder) + tinderKg - tinderIn;
        else Items.Add(pack, tinder, tinderKg - tinderIn);
        if (kindlingKg > 0) PutOn(f, pack, Resource.Stick, kindlingKg);
        IgniteAll(f);
    }

    /// The tinder a strike spends, whether or not it catches: whole pieces of a discrete one, no more than the pit has room for.
    static double TakeTinder(Inventory pack, Resource tinder, double kg, double roomKg) =>
        Items.Move(pack, new Inventory(), tinder, kg, Items.IsDiscrete(tinder) ? roomKg : double.PositiveInfinity);

    /// At most what the pack holds goes on, and what the pit has no room for stays in the pack.
    static string? Feed(World world, AddFuel a, List<WorldEvent> events)
    {
        var tile = world.Agent(a.Who).Tile;
        var pack = Items.Pack(world, a.Who);
        var id = PitAt(world, tile)!.Value;
        var f = world.Fires[id];
        bool wasEmbers = f.HasEmbers;
        int had = Items.Count(pack, a.Fuel);
        double added = PutOn(f, pack, a.Fuel, a.Kg);
        events.Add(new FuelAdded(world.Minute, id, tile, a.Who, a.Fuel, added, had - Items.Count(pack, a.Fuel)));
        if (added > 0) PayLighter(world, f, events);
        if (wasEmbers && !f.HasEmbers) events.Add(new FireLit(world.Minute, id, tile, a.Who));
        return null;
    }

    /// Returns what left the pack; a sliver Items.Take takes with the rest goes on the fire, never nowhere. A discrete
    /// fuel goes on in whole pieces (Items.Move), as many as fit the pit's room.
    static double PutOn(FireState f, Inventory pack, Resource fuel, double kg)
    {
        if (Items.IsDiscrete(fuel))
        {
            if (!CanAddFuel(f, fuel)) return 0;
            double moved = Items.Move(pack, new Inventory(), fuel, kg, RoomKg(f));
            if (moved <= 0) return 0;
            double inPit = AddFuel(f, fuel, moved);
            f.UnburnedKg[fuel] = f.UnburnedKg.GetValueOrDefault(fuel) + moved - inPit; // rounding at the brim
            return moved;
        }
        double offered = Math.Min(kg, Items.Have(pack, fuel));
        double added = offered > 0 ? AddFuel(f, fuel, offered) : 0;
        if (added <= 0) return 0;
        double taken = Items.Take(pack, fuel, added);
        if (taken > added) f.UnburnedKg[fuel] = f.UnburnedKg.GetValueOrDefault(fuel) + taken - added;
        return taken;
    }

    /// Decided at the action that makes the fire sure to take a log (a strike onto logs, a log onto a strike),
    /// never by the catch-up that sees the log take, which runs whenever someone looks.
    static void PayLighter(World world, FireState f, List<WorldEvent> events)
    {
        if (f.LitBy is not AgentId lighter || !SureToTakeFuelNeedingHeat(f)) return;
        f.LitBy = null;
        if (world.Agents.TryGetValue(lighter, out var who))
            Skills.Award(world, who, Skill.Firecraft, Skills.FireSuccessXp - Skills.FireFailureXp, events);
    }

    /// Burns a copy forward. The burn has no weather or dice and later fuel only helps, so the answer at the
    /// action is the answer the catch-up will find.
    static bool SureToTakeFuelNeedingHeat(FireState f)
    {
        static bool LogBurning(FireState x) => x.BurningKg.Keys.Any(r => Props[r].MinFireTempF > 0);
        if (LogBurning(f)) return true;
        if (!f.UnburnedKg.Keys.Any(r => Props[r].MinFireTempF > 0)) return false;
        var copy = BurnCopy(f);
        while (BurningKg(copy) > 0)
        {
            Catch(copy);
            if (LogBurning(copy)) return true;
            Consume(copy);
        }
        return false;
    }

    /// A forecast: whether this fuel, put on now, catches. What a burning or glowing pit accepts catches the next minute.
    public static bool WillCatch(FireState f, Resource fuel, double kg) => kg > 0 && (IsActive(f) || f.HasEmbers) && CanAddFuel(f, fuel);

    /// Unlike FuelHours, logs on a fire too cool to take them count for nothing.
    public static double HoursItWillBurn(FireState f) => MinutesItWillBurn(f) / 60.0;

    public static int MinutesItWillBurn(FireState f)
    {
        var copy = BurnCopy(f);
        int minutes = 0;
        while (BurningKg(copy) > 0)
        {
            Catch(copy);
            Consume(copy);
            minutes++;
        }
        return minutes;
    }

    /// A forecast of its burn untended: one copy, yielded after each minute's tick until cold; read it before the next.
    public static IEnumerable<FireState> Ahead(FireState f)
    {
        if (f.CarriedBy != null) throw new ArgumentException("A carried torch has no burn to forecast here", nameof(f));
        var copy = BurnCopy(f);
        while (IsActive(copy) || copy.HasEmbers)
        {
            copy.LastUpdatedMinute++;
            Tick(copy, default, copy.LastUpdatedMinute, []);
            yield return copy;
        }
    }

    /// A forecast: the minutes of light to work by here (HasLight) if nobody tends it. Fires around who must be caught up.
    public static int MinutesOfLightLeft(World world, AgentId who)
    {
        var tile = world.Agent(who).Tile;
        int minutes = 0;
        foreach (var (id, f) in world.Fires)
        {
            if (f.CarriedBy != who && (f.CarriedBy != null || f.Tile != tile)) continue;
            RequireCurrent(world, id, f);
            if (IsActive(f)) minutes = Math.Max(minutes, f.CarriedBy != null ? (int)Math.Ceiling(f.TorchMinutesLeft) : MinutesItWillBurn(f));
        }
        return minutes;
    }

    /// A forecast of a single strike now, assuming nobody adds fuel; fires around the agent caught up. What fuel
    /// then does is Forecast's.
    public sealed record LightForecast(double StrikePct, double FlameHours);

    public static LightForecast ForecastLight(World world, StartFire a)
    {
        var agent = world.Agent(a.Who);
        if (Refusal(world, a) != null) return new LightForecast(0, 0);
        double strike = StartOdds(world, agent, a.Tool, a.Tinder).TotalPct;
        return new LightForecast(strike, HoursItWillBurn(IfLit(world, a)));
    }

    static FireState IfLit(World world, StartFire a)
    {
        var tile = world.Agent(a.Who).Tile;
        var lit = PitAt(world, tile) is FireId id ? BurnCopy(world.Fires[id]) : new FireState { Tile = tile, LastUpdatedMinute = world.Minute, Pit = a.Pit };
        var pack = Items.CopyResources(Items.Pack(world, a.Who));
        Kindle(lit, pack, a.Tinder, TakeTinder(pack, a.Tinder, a.TinderKg, RoomKg(lit)), a.KindlingKg);
        return lit;
    }

    public static bool CanStrike(Inventory pack) => Starters.Any(t => Items.Tool(pack, t) != null);

    public static readonly IReadOnlyList<ToolType> Starters = [ToolType.FireStriker, ToolType.BowDrill, ToolType.HandDrill];

    public static readonly IReadOnlyList<Resource> Tinders =
        [.. new[] { Resource.ResinTinder, Resource.Amadou, Resource.BirchBark, Resource.Usnea, Resource.Tinder, Resource.Chaga }.OrderByDescending(r => Props[r].IgnitionBonusPct)];

    /// The surest catch the pack holds enough of; plain tinder, the one a person plans to find, if none.
    public static Resource TinderToStrike(Inventory pack, double kg) => Tinders.FirstOrDefault(r => Items.Have(pack, r) >= kg, Resource.Tinder);

    /// The one fire forecast: what fire can be had, if whoever asks strikes a cold pit and then puts on NextFuel each
    /// minute. Needs no body and no mind: a strike's odds are read from the level and dexterity given.
    public static FireForecast Forecast(FireAsk ask)
    {
        bool burns = ask.Pit is { } pit && (IsActive(pit) || pit.HasEmbers);
        if (!burns && ask.Starter == null) return new(0, 0, 0, [new FireNeed(FireNeedKind.Starter, [], 0)], null, null);
        if (!burns && ask.Pit != null && TotalKg(ask.Pit) >= MaxFuelKg(ask.Pit.Pit)) return new(0, 0, 0, [], null, null); // no room to strike in
        var strike = burns ? null : StrikeFrom(ask.Fuel);
        var burned = burns || strike != null ? Burn(ask.Pit, ask.NewPit, ask.Fuel, strike) : Burned.None;
        double strikePct = burns ? 1 : strike != null ? StartOdds(ask.Starter!.Value, strike.Tinder, ask.FirecraftLevel, ask.DexterityPct).TotalPct : 0;
        var missing = new List<FireNeed>();
        if (burned.Hours < ask.HoursWanted)
        {
            var offer = ask.Fuel;
            var toSearch = burned;
            if (!burns && strike == null)
            {
                offer = Items.CopyResources(ask.Fuel);
                Items.AddKg(offer, Tinders[0], Verbs.TinderKg);
                missing.Add(new FireNeed(FireNeedKind.Tinder, Tinders, Verbs.TinderKg));
                strike = StrikeFrom(offer);
                toSearch = Burn(ask.Pit, ask.NewPit, offer, strike);
            }
            if (toSearch.Hours < ask.HoursWanted && FuelNeed(ask, offer, strike, toSearch) is { } need) missing.Add(need);
        }
        return new FireForecast(strikePct, burned.Hours, burned.LeastHeatF, missing, burns ? burned.First : null, burns ? null : StrikeFrom(ask.Fuel));
    }

    const double MostFuelSearchedKg = 200;

    /// The surest tinder on offer and a strike's kindling, as Verbs offers them. Null without a strike's worth of tinder.
    static FireStrike? StrikeFrom(Inventory fuel) =>
        Tinders.Where(r => Items.Have(fuel, r) >= Verbs.TinderKg - 1e-9).Cast<Resource?>().FirstOrDefault() is Resource tinder
            ? new FireStrike(tinder, Verbs.TinderKg, Items.WouldMoveKg(fuel, Resource.Stick, Verbs.KindlingKg))
            : null;

    /// One entry at most, weighed so that adding it alone reaches the hours wanted.
    static FireNeed? FuelNeed(FireAsk ask, Inventory offer, FireStrike? strike, Burned burned)
    {
        if (burned.Stranded.Count > 0)
        {
            var target = burned.Stranded.OrderBy(r => Props[r].MinFireTempF).ThenBy(r => r).First();
            var bridges = ByHoursPerKg(r => !burned.Stranded.Contains(r) && Props[r].MinFireTempF <= burned.HeatReachedF && Props[r].PeakTempF >= Props[target].MinFireTempF);
            if (Weighed(ask, offer, strike, burned, bridges, target) is { } bridge) return bridge with { Kind = FireNeedKind.Bridge };
        }
        var more = ByHoursPerKg(r => !burned.Stranded.Contains(r) && Props[r].MinFireTempF <= burned.HeatReachedF);
        return Weighed(ask, offer, strike, burned, more, mustTake: null);
    }

    static List<Resource> ByHoursPerKg(Func<Resource, bool> which) =>
        [.. Props.Keys.Where(which).OrderBy(r => Props[r].BurnRateKgPerHour).ThenBy(r => r)];

    static FireNeed? Weighed(FireAsk ask, Inventory offer, FireStrike? strike, Burned burned, List<Resource> candidates, Resource? mustTake)
    {
        foreach (var fuel in candidates)
        {
            double shortHours = Math.Max(ask.HoursWanted - burned.Hours, 1.0 / 60);
            double kg = Math.Max(Items.IsDiscrete(fuel) ? Items.Resources[fuel].MinPieceKg : 0.01, shortHours * Props[fuel].BurnRateKgPerHour * ReckonedBurnSizeFactor);
            for (; kg <= MostFuelSearchedKg; kg *= 1.5)
            {
                var more = Items.CopyResources(offer);
                double added = Items.AddKg(more, fuel, kg);
                var tried = Burn(ask.Pit, ask.NewPit, more, strike, ask.HoursWanted, mustTake);
                if (tried.Hours >= ask.HoursWanted && (mustTake is not Resource m || !tried.Stranded.Contains(m)))
                    return new FireNeed(FireNeedKind.Hours, [fuel, .. candidates.Where(c => c != fuel)], added);
            }
        }
        return null;
    }

    /// One burn of a copy. Stranded: fuel on offer or in the pit that never caught. First: put on before the first minute.
    sealed record Burned(double Hours, double LeastHeatF, double HeatReachedF, IReadOnlySet<Resource> Stranded, FuelToAdd? First)
    {
        public static readonly Burned None = new(0, 0, 0, new HashSet<Resource>(), null);
    }

    static Burned Burn(FireState? pit, PitType newPit, Inventory fuel, FireStrike? strike, double stopAtHours = double.PositiveInfinity, Resource? mustTake = null)
    {
        var offer = Items.CopyResources(fuel);
        var f = pit != null ? BurnCopy(pit) : new FireState { Tile = default, LastUpdatedMinute = 0, Pit = newPit };
        FuelToAdd? first = null;
        if (strike != null) Kindle(f, offer, strike.Tinder, TakeTinder(offer, strike.Tinder, strike.TinderKg, RoomKg(f)), strike.KindlingKg);
        else first = Tend(f, offer);
        int minutes = 0;
        double least = double.PositiveInfinity, reached = CatchTemperatureF(f);
        while (IsActive(f))
        {
            if (minutes >= stopAtHours * 60 && (mustTake is not Resource m || Items.Have(offer, m) <= DustKg && !f.UnburnedKg.ContainsKey(m))) break;
            minutes++;
            least = Math.Min(least, HeatOutputF(f, ambientF: 0));
            f.LastUpdatedMinute++;
            Tick(f, default, f.LastUpdatedMinute, []);
            reached = Math.Max(reached, CatchTemperatureF(f));
            Tend(f, offer);
        }
        var stranded = Items.Totals(offer).Where(x => IsFuel(x.Resource) && x.Kg > DustKg).Select(x => x.Resource)
            .Concat(f.UnburnedKg.Where(x => x.Value > DustKg).Select(x => x.Key)).ToHashSet();
        return new Burned(minutes / 60.0, minutes > 0 ? least : 0, reached, stranded, first);
    }

    static FuelToAdd? Tend(FireState f, Inventory offer)
    {
        if (NextFuel(f, offer) is not { } next) return null;
        PutOn(f, offer, next.Fuel, next.Kg);
        return next;
    }

    /// The tending a forecast assumes, and its Next: of the fuel on offer, the first in the order it catches (least heat
    /// needed, then the slowest burning) that the fire takes now and has room for, as much as fits. Null for a cold pit.
    internal static FuelToAdd? NextFuel(FireState f, Inventory fuel)
    {
        if (!IsActive(f) && !f.HasEmbers) return null;
        var order = Items.Totals(fuel).Where(x => IsFuel(x.Resource) && x.Kg > DustKg)
            .OrderByDescending(x => Props[x.Resource].PeakTempF).ThenBy(x => Props[x.Resource].BurnRateKgPerHour).ThenBy(x => x.Resource);
        foreach (var (r, kg) in order)
        {
            if (!CanAddFuel(f, r)) continue;
            double room = RoomKg(f);
            if (Items.IsDiscrete(r) ? Items.LightestKg(fuel, r) > room + 1e-9 : IsSliver(f, kg)) continue;
            double put = Items.IsDiscrete(r) ? Items.WouldMoveKg(fuel, r, kg, room) : Math.Min(kg, room);
            if (put > 0) return new FuelToAdd(r, put);
        }
        return null;
    }

    static FireState BurnCopy(FireState f) => new()
    {
        Tile = f.Tile,
        LastUpdatedMinute = f.LastUpdatedMinute,
        Pit = f.Pit,
        UnburnedKg = new(f.UnburnedKg),
        BurningKg = new(f.BurningKg),
        HasEmbers = f.HasEmbers,
        EmberDurationHours = f.EmberDurationHours,
        EmberHoursRemaining = f.EmberHoursRemaining,
        EmberStartTempF = f.EmberStartTempF,
    };

    /// Lit from flames on the tile or the torch already in hand (which it replaces); uses up the first torch in the pack.
    static string? Torch(World world, LightTorch a, List<WorldEvent> events)
    {
        var tile = world.Agent(a.Who).Tile;
        var pack = Items.Pack(world, a.Who);
        var torch = Items.Tool(pack, ToolType.Torch)!;
        pack.Gear.Remove(torch);
        foreach (var (old, f) in world.Fires.ToList())
            if (f.CarriedBy == a.Who) world.Fires.Remove(old);
        var id = NewFire(world, new FireState { Tile = tile, LastUpdatedMinute = world.Minute, CarriedBy = a.Who, TorchMinutesLeft = Items.Def(torch).BurnMinutes });
        events.Add(new FireLit(world.Minute, id, tile, a.Who));
        return null;
    }

    static string? Collect(World world, GatherCharcoal a, List<WorldEvent> events)
    {
        var agent = world.Agent(a.Who);
        var gained = new Inventory();
        Items.Add(gained, Resource.Charcoal, CollectCharcoal(world.Fires[PitAt(world, agent.Tile)!.Value]));
        Items.Receive(world, agent, gained, events);
        events.Add(new Gathered(world.Minute, a.Who, agent.Tile, Items.Describe(gained)));
        return null;
    }

    /// Refused where the pit here is already as good, burns, or is someone else's.
    public static Refusal? PitRefusal(World world, Agent agent, PitType dug)
    {
        if (PitAt(world, agent.Tile) is not FireId id) return null;
        var pit = world.Fires[id];
        if (Access.Refusal(world, agent.Id, new LocationFeature(LocationFeatureKind.Fire, agent.Tile, id.Value)) is { } barred) return barred;
        if (pit.Pit >= dug) return new($"the fire pit here is already as good ({Names.Of(pit.Pit)})");
        return IsActive(pit) ? new("put the fire out first") : null;
    }

    internal static void Dig(World world, Agent agent, PitType dug)
    {
        if (PitAt(world, agent.Tile) is FireId id) world.Fires[id].Pit = dug;
        else NewFire(world, new FireState
        {
            Tile = agent.Tile,
            LastUpdatedMinute = world.Minute,
            Pit = dug,
            Cause = new Cause(agent.Id, $"dug by {Cause.Describe(world, agent.Id)}"),
        });
    }

    static FireId NewFire(World world, FireState f)
    {
        var id = new FireId(world.NextFireId++);
        world.Fires[id] = f;
        return id;
    }
}
