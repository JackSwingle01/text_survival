namespace Survival;

/// Flesh, bones, gone; a dead person is a body, a skeleton, then scattered bones (emergent-world-design §5).
public enum RemainsStage { Flesh, Skeletal, Bones, Gone }

/// Stage is derived, so a read changes nothing. World changes are settled only at the top of each
/// clock hour (Hour), which every minute lived passes through, so they happen at the same minute however
/// the clock is stepped and whoever looks.
/// Cold all but stops the clock (DecayFactor 0.1 below 0 °F).
// A grave (not built yet) is exempt: Hour skips it once burial exists.
public static class Remains
{
    /// Effective hours to bones for a 70 kg body: ten days at a thaw. Three would leave scavengers nothing.
    public const double SkeletalHours70Kg = 240;
    /// However stripped, the last of the flesh takes this share of the full time.
    public const double StrippedFloorPct = 0.1;
    public const double ScatterHours = 240;
    /// About sixty days at a thaw, two hundred and forty in a hard frost.
    public const double GoneHours = 1440;

    public static double SkeletalHours(double bodyKg) => SkeletalHours70Kg * Math.Cbrt(Math.Max(1, bodyKg) / 70);

    public static double EffectiveHours(World world, TileId tile, long fromMinute, long toMinute)
    {
        if (toMinute <= fromMinute) return 0;
        double delta = Map.TemperatureDeltaF(Map.Of(world), tile);
        var cache = world.DecayCache ??= [];
        if (!cache.TryGetValue(delta, out var timeline)) cache[delta] = timeline = new DecayTimeline(delta, Ground.HistoryStartMinute(world));
        return timeline.Between(world, fromMinute, toMinute);
    }

    /// Eating speeds the rot; cutting does not, because the flesh left rots no faster for what a butcher took.
    public static double FleshLeftPct(Carcass c)
    {
        double uneaten = Carcasses.FleshKg(c) + Carcasses.CutFleshKg(c);
        return c.FleshAtDeathKg <= 0 ? (uneaten > 0 ? 1 : 0) : Math.Clamp(uneaten / c.FleshAtDeathKg, 0, 1);
    }

    public static RemainsStage StageOf(World world, TileId tile, Carcass c)
    {
        double hours = Carcasses.EffectiveHoursNow(world, tile, c);
        if (hours >= GoneHours) return RemainsStage.Gone;
        double left = FleshLeftPct(c);
        if (left <= 0 || !Carcasses.HasSoftTissue(c) || hours >= SkeletalHours(c.BodyWeightKg) * Math.Max(StrippedFloorPct, left))
            return RemainsStage.Bones;
        return RemainsStage.Flesh;
    }

    public static double MeatKg(World world, TileId tile, Carcass c) =>
        StageOf(world, tile, c) == RemainsStage.Flesh ? Carcasses.MeatKg(c) : 0;

    /// By the carcass ledger's rule (Carcasses.Create).
    public static double FleshKg(BodyState body) => body.MuscleKg * 0.65 + body.FatKg * 0.6;

    public static double HoursDead(World world, Agent dead) =>
        dead.Body.DiedMinute is long died ? EffectiveHours(world, dead.Tile, died, world.Minute) : 0;

    public static RemainsStage StageOf(World world, Agent dead)
    {
        double hours = HoursDead(world, dead);
        double skeletal = SkeletalHours(dead.Body.WeightKg) * Math.Max(StrippedFloorPct, 1 - dead.Body.PickedKg / Math.Max(1e-9, FleshKg(dead.Body)));
        if (hours >= skeletal + ScatterHours) return RemainsStage.Bones;
        return hours >= skeletal ? RemainsStage.Skeletal : RemainsStage.Flesh;
    }

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

    /// At the top of every clock hour. A scattered person leaves the world's agents; their gear lies
    /// beside the bones as a pile, and a torch they carried goes out. The player's body stays (Map.Outcome reads it).
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
        if (!Items.IsEmpty(kit))
            Items.AddAll(Items.PileAt(world, dead.Tile, new Cause(dead.Id, $"what {Cause.Describe(world, dead.Id)} carried, beside {bones.Animal.ToLowerInvariant()} bones")), kit);
        Agents.Remove(world, dead.Id);
    }
}

/// Prefix sums of rot over clock hours for one tile's air, sampled at the top of each hour as
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

    public static double TempF(World world, long minute, double deltaF) => Exposure.SiteAirF(Weather.At(world, FloorHour(minute) * 60), deltaF);
}
