namespace Survival;

/// How far a carcass or a dead body has gone (emergent-world-design §5): flesh, then bones once the
/// flesh is stripped or rotted away, then gone. A dead person is a body, a skeleton, then scattered
/// bones beside what they carried.
public enum RemainsStage { Flesh, Skeletal, Bones, Gone }

/// The body and carcass pipeline (emergent-world-design §5). Everything is derived from the rot clock
/// (DecayTimeline: Carcasses.DecayFactor at each clock hour's air) and from how much flesh is left, so
/// a read changes nothing. What a stage change does to the world (the flesh rotting off the ledger, the
/// bones taken away, a skeleton scattering) is settled at the top of every clock hour (Hour), which
/// every minute the world lives passes through, so it happens at the same minute however the clock is
/// stepped and whoever looks.
/// Cold all but stops the clock (DecayFactor 0.1 below 0 °F): a body kept frozen by the local air
/// lasts ten times as long (the frozen traveller). Scavengers speed it by eating: a carcass is
/// stripped through Carcasses.EatMeat, a body through Pick.
// A grave (not built yet) is exempt: Hour skips it once burial exists.
public static class Remains
{
    /// Effective hours to bones for a 70 kg body left whole: ten days at a cool thaw, five in summer
    /// heat, a hundred deep frozen. Carrion that lasts only three days leaves the scavengers nothing
    /// (judgment). Larger animals take longer, with the cube root of their mass (a mammoth about four
    /// times a man).
    public const double SkeletalHours70Kg = 240;
    /// However stripped, the last of the flesh takes this share of the full time (judgment).
    public const double StrippedFloorPct = 0.1;
    /// A skeleton lies whole this many effective hours before it scatters into bones (judgment).
    public const double ScatterHours = 240;
    /// Bones weather away this many effective hours after death: about sixty days at a thaw, two
    /// hundred and forty in a hard frost (judgment).
    public const double GoneHours = 1440;

    /// Effective hours to bones for a whole body of this mass.
    public static double SkeletalHours(double bodyKg) => SkeletalHours70Kg * Math.Cbrt(Math.Max(1, bodyKg) / 70);

    /// Temperature-weighted hours of rot on this tile between two minutes (Carcasses.DecayFactor at
    /// each clock hour's surface air, as Carcasses.CatchUp charges them).
    public static double EffectiveHours(World world, TileId tile, long fromMinute, long toMinute)
    {
        if (toMinute <= fromMinute) return 0;
        double delta = Map.TemperatureDeltaF(Map.Of(world), tile);
        var cache = world.DecayCache ??= [];
        if (!cache.TryGetValue(delta, out var timeline)) cache[delta] = timeline = new DecayTimeline(delta, Ground.HistoryStartMinute(world));
        return timeline.Between(world, fromMinute, toMinute);
    }

    // ---- carcasses ----

    /// How much of the flesh it died with nothing has eaten, 0-1: what is still on it and what was cut
    /// off it. Eating strips a carcass for the rot; cutting does not, because the flesh still on it
    /// rots no faster for what a butcher took away.
    public static double FleshLeftPct(Carcass c)
    {
        double uneaten = Carcasses.FleshKg(c) + Carcasses.CutFleshKg(c);
        return c.FleshAtDeathKg <= 0 ? (uneaten > 0 ? 1 : 0) : Math.Clamp(uneaten / c.FleshAtDeathKg, 0, 1);
    }

    /// Bones once its time is up, sooner the more of it was eaten; bones at once when it was eaten to
    /// nothing, or once nothing that rots is left on it (cut clean and carried off).
    public static RemainsStage StageOf(World world, TileId tile, Carcass c)
    {
        double hours = Carcasses.EffectiveHoursNow(world, tile, c);
        if (hours >= GoneHours) return RemainsStage.Gone;
        double left = FleshLeftPct(c);
        if (left <= 0 || !Carcasses.HasSoftTissue(c) || hours >= SkeletalHours(c.BodyWeightKg) * Math.Max(StrippedFloorPct, left))
            return RemainsStage.Bones;
        return RemainsStage.Flesh;
    }

    /// Meat an animal could eat off it now: none once it is bones.
    public static double MeatKg(World world, TileId tile, Carcass c) =>
        StageOf(world, tile, c) == RemainsStage.Flesh ? Carcasses.MeatKg(c) : 0;

    // ---- dead people ----

    /// The flesh a dead person had, by the carcass ledger's rule (Carcasses.Create: meat and fat).
    public static double FleshKg(BodyState body) => body.MuscleKg * 0.65 + body.FatKg * 0.6;

    /// Effective hours since a dead person died, where they lie.
    public static double HoursDead(World world, Agent dead) =>
        dead.Body.DiedMinute is long died ? EffectiveHours(world, dead.Tile, died, world.Minute) : 0;

    public static RemainsStage StageOf(World world, Agent dead)
    {
        double hours = HoursDead(world, dead);
        double skeletal = SkeletalHours(dead.Body.WeightKg) * Math.Max(StrippedFloorPct, 1 - dead.Body.PickedKg / Math.Max(1e-9, FleshKg(dead.Body)));
        if (hours >= skeletal + ScatterHours) return RemainsStage.Bones;
        return hours >= skeletal ? RemainsStage.Skeletal : RemainsStage.Flesh;
    }

    /// Scavengers tear up to kg off a dead body; returns what they got. None off a skeleton.
    public static double Pick(World world, AgentId deadId, double kg)
    {
        if (kg < 0) throw new ArgumentOutOfRangeException(nameof(kg), kg, "Cannot pick a negative amount");
        var dead = world.Agent(deadId);
        if (dead.Mind is not PersonMind || Sim.IsAlive(dead)) throw new InvalidOperationException($"{deadId} is not a dead person");
        if (StageOf(world, dead) != RemainsStage.Flesh) return 0;
        double bite = Math.Min(kg, Math.Max(0, FleshKg(dead.Body) - dead.Body.PickedKg));
        dead.Body.PickedKg += bite;
        return bite;
    }

    // ---- the hourly settling ----

    /// At the top of every clock hour: flesh past its time rots off the ledger, bones past theirs
    /// leave, and a skeleton past its time scatters: the person leaves the world's agents, their
    /// bones lie as remains with the cause they died of, and what they carried and wore lies beside
    /// them as a stash. The player's body stays (the run is over; Map.Outcome reads it). A torch the
    /// dead were carrying goes out.
    public static void Hour(World world)
    {
        if (world.Minute % 60 != 0) return;
        foreach (var tile in Carcasses.TilesWith(world).ToList())
            foreach (var c in world.Tiles[tile].Carcasses!.ToList())
                switch (StageOf(world, tile, c))
                {
                    case RemainsStage.Gone: Carcasses.Remove(world, tile, c); break;
                    case RemainsStage.Bones when Carcasses.HasUncutSoftTissue(c): Carcasses.RotAway(c); break;
                }
        foreach (var dead in world.Agents.Values.Where(a => a.Id != AgentId.Player && a.Mind is PersonMind && !Sim.IsAlive(a)).ToList())
            if (Combat.EncounterOf(world, dead.Id) == null && StageOf(world, dead) == RemainsStage.Bones) Scatter(world, dead);
        Fire.PutOutDroppedTorches(world);
    }

    static void Scatter(World world, Agent dead)
    {
        var body = dead.Body;
        var cause = body.DeathCause ?? new Cause(null, "its injuries");
        var bones = Carcasses.Bones(world, dead.Tile, "Human", body.WeightKg, (body.WeightKg - body.MuscleKg - body.FatKg) * 0.25,
            body.DiedMinute ?? world.Minute, cause, HoursDead(world, dead));
        var kit = new Inventory();
        if (dead.Mind is PersonMind person) Items.AddAll(kit, person.Pack);
        kit.Gear.AddRange(body.Worn);
        if (kit.Kg.Count > 0 || kit.Gear.Count > 0)
            Items.AddAll(Items.StashAt(world, dead.Tile, new Cause(dead.Id, $"what {Cause.Describe(world, dead.Id)} carried, beside {bones.Animal.ToLowerInvariant()} bones")), kit);
        Agents.Remove(world, dead.Id);
    }
}

/// Prefix sums of rot (Carcasses.DecayFactor) over clock hours, for one tile's air: the seed's sky
/// plus the tile's own cold (Map.TemperatureDeltaF), sampled at the top of each hour as
/// Carcasses.CatchUp samples it. Pure derived data, rebuilt from the seed after a load.
internal sealed class DecayTimeline(double deltaF, long historyStartMinute)
{
    readonly long firstHour = FloorHour(historyStartMinute);
    // before[i]: effective hours over clock hours [firstHour, firstHour + i); factors[i]: hour firstHour + i's.
    readonly List<double> before = [0];
    readonly List<double> factors = [];

    static long FloorHour(long minute) => minute >= 0 ? minute / 60 : -((-minute + 59) / 60);

    public double Between(World world, long from, long to) => At(world, to) - At(world, from);

    double At(World world, long minute)
    {
        long hour = FloorHour(minute);
        int i = checked((int)(hour - firstHour));
        if (i < 0) throw new ArgumentOutOfRangeException(nameof(minute), minute, "Rot starts with the weather's history");
        while (factors.Count <= i)
        {
            double tempF = Exposure.SiteAirF(Weather.At(world, (firstHour + factors.Count) * 60), deltaF);
            factors.Add(Carcasses.DecayFactor(tempF));
            before.Add(before[^1] + factors[^1]);
        }
        return before[i] + (minute - hour * 60) / 60.0 * factors[i];
    }

    /// The air at the top of the clock hour holding this minute, as Carcasses.CatchUp last read it.
    public static double TempF(World world, long minute, double deltaF) => Exposure.SiteAirF(Weather.At(world, FloorHour(minute) * 60), deltaF);
}
