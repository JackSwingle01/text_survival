namespace Survival;

public enum PitType { Open, Mound, Stone }

/// Derived from FireState, never stored.
public enum FirePhase { Cold, Igniting, Building, Roaring, Steady, Dying, Embers }

/// Temperatures in °F, as tuned in the old game. One row of Content/fire.json fuels. A class, not a struct:
/// the reader builds a struct row from its parameterless constructor and would take a missing field as 0.
public sealed record FuelProps(double PeakTempF, double BurnRateKgPerHour, double MinFireTempF, double IgnitionBonusPct);

/// One row of Content/fire.json pits: what a pit holds, how much wind it keeps off, how long fuel lasts in it.
public sealed record PitProps(double MaxFuelKg, double WindProtectionLevel, double FuelEfficiencyFactor);

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
    /// Who struck this fire, until it is sure to take a log or goes out: their Firecraft for lighting it
    /// is paid then (Fire.PayLighter), so a fire that never got past its kindling teaches only a miss's worth.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public AgentId? LitBy { get; set; }
    /// A pit: who made it, by lighting the first fire in it. Null on a carried torch.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Cause? Cause { get; init; }
}

public static class Fire
{
    /// What burns and how (Content/fire.json).
    public static readonly IReadOnlyDictionary<Resource, FuelProps> Props = Content.Tables.Fuels;
    static readonly IReadOnlyDictionary<PitType, PitProps> Pits = Content.Tables.Pits;

    /// Which resources burn: the ones with fuel properties.
    public static bool IsFuel(Resource r) => Props.ContainsKey(r);

    const double BaseCatchRateKgPerMinute = 0.05;
    const double CharcoalYieldPct = 0.04;
    public const double TorchBurnMinutes = 60;

    // ---- pit properties ----
    public static double MaxFuelKg(PitType pit) => Pits[pit].MaxFuelKg;
    public static double WindProtectionLevel(PitType pit) => Pits[pit].WindProtectionLevel;
    static double FuelEfficiencyFactor(PitType pit) => Pits[pit].FuelEfficiencyFactor;

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

    /// How hot the hottest flame in the fire is: what new fuel must touch to catch. A log catches off
    /// the burning oak beside it, not off the fire's average, so tinder thrown on a hot fire never
    /// stops it taking bone. Embers are as hot as they are. Old game: fuel caught only off the mass-weighted
    /// mean, so tinder cooled a fire below what its oak could light (bug, fixed).
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

    static double Weighted(FireState f, Func<FuelProps, double> prop, double fallback)
    {
        double mass = 0, sum = 0;
        foreach (var (fuel, kg) in f.BurningKg) { sum += prop(Props[fuel]) * kg; mass += kg; }
        return mass > 0 ? sum / mass : fallback;
    }

    static double BurnSizeFactor(double burningKg) => burningKg < 1.5 ? 0.9 : burningKg < 4.0 ? 1.0 : burningKg < 7.0 ? 1.1 : 1.2;

    /// The whole fire's burn, kg an hour, before the stone pit's efficiency: the burning fuels'
    /// mass-weighted mean table rate at the fire's size, as the old game had it.
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

    /// A fire's burn rate over its fuels' table rates at a working size (BurnSizeFactor runs 0.9-1.2):
    /// what someone judging how long wood will last reckons with.
    public const double ReckonedBurnSizeFactor = 1.1;

    /// Hours of fire in this much of a fuel, as someone judging a woodpile reckons it.
    // Ignores a stone pit's efficiency and that a small fire burns slower; both under 15%.
    public static double FuelHours(Resource fuel, double kg) => kg / (Props[fuel].BurnRateKgPerHour * ReckonedBurnSizeFactor);

    /// Hours of fire in everything on the pit, burning or waiting to catch, reckoned as FuelHours.
    public static double FuelHours(FireState f) => f.UnburnedKg.Concat(f.BurningKg).Sum(x => FuelHours(x.Key, x.Value));

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
    /// ShelterHeatF is the fire's heat a shelter holds in beyond the open air's share: warm air, but
    /// air someone too hot can step out of, so it is withheld with the radiant heat once they sweat.
    public readonly record struct Warmth(double AirHeatF, double PitHeatF, double TorchHeatF, double WindProtectionLevel, double ShelterHeatF = 0);

    const double OpenAirHeatPct = 0.4;

    /// cover: the shelter around someone keeping still. Its insulation keeps the fire's heat in.
    /// The fire's part of the agent's exposure (Exposure.At), which measures it against the air there.
    public static Warmth WarmthFor(World world, AgentId who, ShelterCover cover = default) =>
        Exposure.At(world, world.Agent(who).Tile, Cover.In(cover, who)).Warmth;

    /// The fires reaching something on a tile: the pits on it, the heat of the holder's torches, and
    /// how much wind the walls of the burning pits keep off (only while they burn). All caught up.
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
        var burning = pits.Where(IsActive).ToList();
        return new FiresAround(pits, torchHeatF, burning.Count == 0 ? 0 : burning.Max(f => WindProtectionLevel(f.Pit)));
    }

    /// The fires' heat, felt against the air as the weather and any shelter leave it, wind chill
    /// included (old Location.GetTemperatureBreakdown); insulation is what of the shelter's is left.
    internal static Warmth WarmthAgainst(FiresAround fires, double ambientF, double insulation)
    {
        // Open air carries 60% of the heat away; a shelter's insulation keeps more of it in (old: the
        // fire's share was Clamp(insulation + 0.4, 0.4, 1)).
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

    /// The pit on a tile as it is now. It must have been caught up, as anything read off a fire must be.
    public static FireState? CurrentPitAt(World world, TileId tile)
    {
        if (PitAt(world, tile) is not FireId id) return null;
        var f = world.Fires[id];
        RequireCurrent(world, id, f);
        return f;
    }

    static void RequireCurrent(World world, FireId id, FireState f)
    {
        if (f.LastUpdatedMinute != world.Minute)
            throw new InvalidOperationException($"Fire {id.Value} is at minute {f.LastUpdatedMinute}, world at {world.Minute}: catch it up first");
    }

    // ---- fuel ----

    public static bool CanAddFuel(FireState f, Resource fuel) =>
        TotalKg(f) < MaxFuelKg(f.Pit) && CatchTemperatureF(f) >= Props[fuel].MinFireTempF;

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

    /// Under this, a stack is dust: it goes with the rest of its fuel, never off the ledger.
    const double DustKg = 0.001;

    /// Catches kg of a fuel. A last gram left waiting catches with it. Old game: it was deleted (bug, fixed).
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

    /// Torches whose carrier has died or left the world go out where they fell (Remains.Hour): no one
    /// carries them on, and nothing else would ever catch them up or take them away.
    internal static void PutOutDroppedTorches(World world)
    {
        foreach (var (id, f) in world.Fires.ToList())
            if (f.CarriedBy is AgentId carrier && !(world.Agents.TryGetValue(carrier, out var a) && Sim.IsAlive(a)))
                world.Fires.Remove(id);
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

    /// The fire's burn this minute (BurnRateKgPerHour), shared out among the fuels by their table rates
    /// times their masses, all read off the fire as it was at the start of the minute: a fast fuel goes
    /// first and a slow one after it, and one fuel alone burns exactly as it always did. Old game: every
    /// fuel lost the same fraction of itself, so tinder dragged an oak log down with it, and 2 kg of oak
    /// with 0.5 kg of tinder flamed 149 min against 217 alone; now 226 (bug, fixed). A last gram burns
    /// with its stack; old game: it was deleted uncounted, and made no charcoal (bug, fixed).
    static void Consume(FireState f)
    {
        double burning = BurningKg(f);
        f.LastBurningTempF = ActiveTemperatureF(f); // before consumption: the embers start from it
        double consumed = Math.Min(burning, BurnRateKgPerHour(f) / FuelEfficiencyFactor(f.Pit) / 60.0);
        double weight = f.BurningKg.Sum(x => Props[x.Key].BurnRateKgPerHour * x.Value);
        var shares = f.BurningKg.ToDictionary(x => x.Key, x => consumed * (Props[x.Key].BurnRateKgPerHour * x.Value / weight));
        foreach (var (fuel, share) in shares)
        {
            double kg = f.BurningKg[fuel];
            double burned = Math.Min(kg, share);
            double left = kg * (1.0 - burned / kg); // the old arithmetic, so one fuel burns bit for bit as it did
            if (left < DustKg) { burned = kg; f.BurningKg.Remove(fuel); }
            else f.BurningKg[fuel] = left;
            f.BurnedSinceLitKg += burned;
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

    // ---- activities ----

    /// Fire-start odds as their terms (old FireHandler.CalculateFireChance, in its order): the tool's
    /// base, the skill, the tinder, -20% with consciousness impaired, and up to -50% for clumsy
    /// hands; clamped to 5-95%. Dexterity folds in manipulation, wetness, darkness and vitality;
    /// handsWhy names which of those are costing it, for the sheet.
    public static Odds StartOdds(ToolType tool, Resource tinder, int skillLevel = 0, bool consciousnessImpaired = false, double dexterityPct = 1.0, string handsWhy = "")
    {
        double basePct = tool switch
        {
            ToolType.FireStriker => 0.90,
            ToolType.BowDrill => 0.50,
            ToolType.HandDrill => 0.35,
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Not a fire starter"),
        };
        return new Odds(tool.ToString(), basePct, Odds.Moving(
            OddsTerm.Add("Firecraft", Skills.FirecraftPct(skillLevel)),
            OddsTerm.Add(tinder.ToString(), Props[tinder].IgnitionBonusPct),
            OddsTerm.Add("Dazed", consciousnessImpaired ? -0.2 : 0),
            OddsTerm.Add(handsWhy.Length > 0 ? $"Hands ({handsWhy})" : "Hands", -(1.0 - dexterityPct) * 0.5)), MinPct: 0.05, MaxPct: 0.95);
    }

    public static double StartChancePct(ToolType tool, Resource tinder, int skillLevel = 0, bool consciousnessImpaired = false, double dexterityPct = 1.0) =>
        StartOdds(tool, tinder, skillLevel, consciousnessImpaired, dexterityPct).TotalPct;

    /// This person's odds of lighting a fire here, now: their skill, their head, their hands, the dark.
    /// Fires around the agent must be caught up (the light they work by).
    public static Odds StartOdds(World world, Agent agent, ToolType tool, Resource tinder)
    {
        var body = agent.Body;
        var context = AbilityContextFor(world, agent);
        return StartOdds(tool, tinder, Skills.Level(agent, Skill.Firecraft),
            consciousnessImpaired: Body.GetCapacities(body).Consciousness < 0.5,
            dexterityPct: Body.Dexterity(body, context), handsWhy: string.Join(", ", Body.DexterityCosts(body, context)));
    }

    /// Why this fire activity cannot be done right now, or null. Checked before any time passes
    /// or anything changes, so a refusal costs nothing. Fires around the agent must be caught up.
    /// Everything put on a fire comes out of the agent's pack.
    public static string? Refusal(World world, Activity activity)
    {
        var tile = world.Agent(activity.Who).Tile;
        var pack = Items.Pack(world, activity.Who);
        var pitId = PitAt(world, tile);
        var pit = pitId is FireId id ? world.Fires[id] : null;
        // The pit is the thing reached: asked first, once it is there.
        string? Reach() => Access.Refusal(world, activity.Who, new ThingRef(ThingKind.Fire, tile, pitId!.Value.Value));
        switch (activity)
        {
            case StartFire a:
                if (!IsFuel(a.Tinder) || Props[a.Tinder].MinFireTempF != 0) throw new ArgumentException($"{a.Tinder} cannot light from cold", nameof(activity));
                if (a.TinderKg <= 0 || a.KindlingKg < 0) throw new ArgumentOutOfRangeException(nameof(activity), "Tinder must be positive and kindling not negative");
                if (pit != null)
                {
                    if (Reach() is string barred) return barred;
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
                if (Reach() is string fuelBarred) return fuelBarred;
                if (Items.Have(pack, a.Fuel) <= 0) return $"no {a.Fuel} to add";
                if (CanAddFuel(pit, a.Fuel)) return null;
                return TotalKg(pit) >= MaxFuelKg(pit.Pit) ? "the fire is full" : $"the fire is not hot enough for {a.Fuel}";
            case LightTorch a:
                if (!HasLight(world, a.Who)) return "no flame to light a torch from";
                return Items.Tool(pack, ToolType.Torch) == null ? "no torch to light" : null;
            case GatherCharcoal:
                if (pit == null) return "no charcoal here";
                return Reach() ?? (pit.CharcoalKg > 0 ? null : "no charcoal here");
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
        // Old FireHandler passed the Firecraft level; the port had dropped it (bug, fixed).
        double chance = StartOdds(world, agent, a.Tool, a.Tinder).TotalPct;
        double tinderKg = Items.Take(pack, a.Tinder, a.TinderKg);
        var tool = Items.Tool(pack, a.Tool)!;
        Items.Use(tool);
        if (world.Rng("fire", a.Who).NextDouble() >= chance)
        {
            events.Add(new FireStartFailed(world.Minute, a.Who, tile, chance));
            Skills.Award(world, agent, Skill.Firecraft, Skills.FireFailureXp, events);
            return "fire did not catch";
        }
        var id = PitAt(world, tile) ?? NewFire(world, new FireState
        {
            Tile = tile, LastUpdatedMinute = world.Minute, Pit = a.Pit,
            Cause = new Cause(a.Who, $"lit by {Cause.Describe(world, a.Who)} with a {Items.Def(tool).Name.ToLowerInvariant()}"),
        });
        var f = world.Fires[id];
        // What the pit has no room for stays in the pack, as Feed does. Old game: it popped the
        // kindling and tinder and the pit's capacity cap destroyed what did not fit (bug, fixed).
        Items.Add(pack, a.Tinder, tinderKg - AddFuel(f, a.Tinder, tinderKg));
        if (a.KindlingKg > 0) PutOn(f, pack, Resource.Kindling, a.KindlingKg);
        IgniteAll(f);
        events.Add(new FireLit(world.Minute, id, tile, a.Who));
        // A strike that catches is worth a miss now; the rest when the fire takes wood (PayLighter). Old game:
        // a full success for any flame, so tinder and a pinch of kindling, with no fuel to follow, farmed levels (bug, fixed).
        Skills.Award(world, agent, Skill.Firecraft, Skills.FireFailureXp, events);
        f.LitBy = a.Who;
        PayLighter(world, f, events); // struck onto logs already in the pit
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
        double added = PutOn(f, pack, a.Fuel, a.Kg);
        events.Add(new FuelAdded(world.Minute, id, tile, a.Who, a.Fuel, added));
        if (added > 0) PayLighter(world, f, events);
        if (wasEmbers && !f.HasEmbers) events.Add(new FireLit(world.Minute, id, tile, a.Who));
        return null;
    }

    /// Up to kg from the pack onto the pit, as much as it has room for; returns what left the pack.
    /// A sliver Items.Take takes with the rest goes on the fire with it, never nowhere.
    static double PutOn(FireState f, Inventory pack, Resource fuel, double kg)
    {
        double offered = Math.Min(kg, Items.Have(pack, fuel));
        double added = offered > 0 ? AddFuel(f, fuel, offered) : 0;
        if (added <= 0) return 0;
        double taken = Items.Take(pack, fuel, added);
        if (taken > added) f.UnburnedKg[fuel] = f.UnburnedKg.GetValueOrDefault(fuel) + taken - added;
        return taken;
    }

    /// The rest of a strike's Firecraft, to whoever struck it, once the fire is sure to take a log: it has
    /// caught, and it was their doing. Decided at the action that makes it so (a strike onto logs, a log
    /// onto a strike), never by the catch-up that sees the log take, which runs whenever someone looks.
    static void PayLighter(World world, FireState f, List<WorldEvent> events)
    {
        if (f.LitBy is not AgentId lighter || !WillTakeALog(f)) return;
        f.LitBy = null;
        if (world.Agents.TryGetValue(lighter, out var who))
            Skills.Award(world, who, Skill.Firecraft, Skills.FireSuccessXp - Skills.FireFailureXp, events);
    }

    /// Whether, left as it is, the fire will have a log burning before it goes out: a copy burned forward
    /// a minute at a time. The burn has no weather and no dice in it, and fuel put on later only helps
    /// a log catch, so the answer at the action is the answer the catch-up will find.
    static bool WillTakeALog(FireState f)
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

    /// Hours the fire will go on burning, left as it is: a copy burned forward a minute at a time, as
    /// WillTakeALog. Unlike FuelHours, logs on a fire too cool to take them count for nothing.
    public static double HoursItWillBurn(FireState f)
    {
        var copy = BurnCopy(f);
        int minutes = 0;
        while (BurningKg(copy) > 0)
        {
            Catch(copy);
            Consume(copy);
            minutes++;
        }
        return minutes / 60.0;
    }

    static FireState BurnCopy(FireState f) => new()
    {
        Tile = f.Tile, LastUpdatedMinute = f.LastUpdatedMinute, Pit = f.Pit,
        UnburnedKg = new(f.UnburnedKg), BurningKg = new(f.BurningKg), HasEmbers = f.HasEmbers,
        EmberDurationHours = f.EmberDurationHours, EmberHoursRemaining = f.EmberHoursRemaining, EmberStartTempF = f.EmberStartTempF,
    };

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
