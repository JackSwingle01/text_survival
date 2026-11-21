namespace Survival;

/// Old ButcheringMode: quick and messy, careful, or slow and thorough.
public enum ButcherMode { QuickStrip, Careful, FullProcessing }

/// Old ButcheringModeConfig (Content/butchering.json). Yield factors per resource; anything not named takes 1.
public sealed record ButcherModeProps(double TimeFactor, IReadOnlyDictionary<Resource, double> YieldFactor, double ScentIncrease, double BloodySeverity);

/// A dead animal on a tile, as a tile delta (old CarcassFeature). Its yields are what is left of it.
public sealed class Carcass
{
    public required int Id { get; init; }
    public required string Animal { get; init; }
    public required double BodyWeightKg { get; init; }
    public required long DiedMinute { get; init; }
    /// Who killed it and how ("a wolf pack", "the player's wooden spear", "its wounds").
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Cause? Cause { get; init; }
    /// Temperature-weighted hours since death, as of LastUpdatedMinute (old EffectiveHoursSinceDeath).
    public double EffectiveHours { get; set; }
    public required long LastUpdatedMinute { get; set; }
    public double LastKnownTempF { get; set; } = 32;
    /// Kg of RawMeat, Bone, Hide, Sinew, RawFat, Feather, Ivory and MammothHide left on it.
    public Dictionary<Resource, double> Remaining { get; init; } = [];
    /// The ledger's meat and fat when it died (Carcasses.FleshKg): how stripped it is now is the share
    /// of this left, and a stripped carcass goes to bones sooner (Remains).
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double FleshAtDeathKg { get; init; }
    public ButcherMode? Mode { get; set; }
    public double ScentBonus { get; set; }
    /// Minutes of the setup done, before the mode's time factor. Setup is paid once per carcass.
    public double SetupMinutes { get; set; }
    /// Kg cut off Remaining so far, by resource, before the yield factors.
    public SortedDictionary<Resource, double> CutKg { get; init; } = [];
    /// Kg that cutting has yielded so far, by resource: what the scrap minimum and meat floor judge.
    public SortedDictionary<Resource, double> YieldKg { get; init; } = [];
    /// Kg cut and yielded but not yet carried off: under the minimum so far, or no room in the pack.
    public SortedDictionary<Resource, double> Pending { get; init; } = [];
}

/// Port of old CarcassFeature: decay by temperature, butchering yields and times.
public static class Carcasses
{
    const double BaseButcheringMinutes = 30.0, MinutesPerKgYield = 2.75;

    /// Old ButcheringModeConfig, by mode (Content/butchering.json): yield factors relative to a careful
    /// butcher's yield. Old game: the carcass's ledger was the careful yield, so a full cut gave 107% of
    /// the kilograms it took off the carcass (bug, fixed): the ledger now holds what full processing
    /// recovers (Create scales it by ThoroughFactor), and every mode recovers its old yield as a share of
    /// it, never more than was cut.
    static readonly IReadOnlyDictionary<ButcherMode, ButcherModeProps> Rows = Content.Tables.Butchering;

    /// How much more of a resource full processing recovers than careful work: the ledger's scale.
    public static double ThoroughFactor(Resource r) => Rows[ButcherMode.FullProcessing].YieldFactor.GetValueOrDefault(r, 1);

    static IReadOnlyDictionary<Resource, double> Recovered(IReadOnlyDictionary<Resource, double> careful) =>
        Enum.GetValues<Resource>().Where(r => careful.ContainsKey(r) || Rows[ButcherMode.FullProcessing].YieldFactor.ContainsKey(r))
            .ToDictionary(r => r, r => careful.GetValueOrDefault(r, 1) / ThoroughFactor(r)).AsReadOnly();

    static readonly IReadOnlyDictionary<ButcherMode, ButcherModeProps> Modes =
        Enum.GetValues<ButcherMode>().ToDictionary(m => m, m => Rows[m] with { YieldFactor = Recovered(Rows[m].YieldFactor) }).AsReadOnly();

    /// YieldFactor is the share of each resource's ledger the mode recovers, at most 1; anything not named takes 1.
    public static ButcherModeProps ModeProps(ButcherMode mode) =>
        Modes.TryGetValue(mode, out var props) ? props : throw new ArgumentOutOfRangeException(nameof(mode), mode, null);

    /// Old CarcassFeature.InitializeYieldsFromAnimal, from the body that died. Phase 3b kills call this.
    public static Carcass Create(World world, TileId tile, string animal, BodyState body, long diedMinute,
        bool isBird = false, bool isMegafauna = false, double harvestedPct = 0, IReadOnlyDictionary<Resource, double>? special = null,
        Cause? cause = null)
    {
        var y = new Dictionary<Resource, double>();
        bool specialHide = special?.ContainsKey(Resource.MammothHide) == true;
        foreach (var (r, kg) in special ?? new Dictionary<Resource, double>()) y[r] = kg;
        y[Resource.RawMeat] = body.MuscleKg * 0.65;
        y[Resource.RawFat] = body.FatKg * 0.6;
        y[Resource.Bone] = (body.WeightKg - body.MuscleKg - body.FatKg) * 0.25;
        y[Resource.Sinew] = body.WeightKg * 0.05;
        if (isBird) y[Resource.Feather] = body.WeightKg * 0.08;
        else if (!specialHide) y[Resource.Hide] = body.WeightKg * 0.10;
        if (isMegafauna)
        {
            y[Resource.RawMeat] = Math.Min(y[Resource.RawMeat], 80);
            y[Resource.RawFat] = Math.Min(y[Resource.RawFat], 15);
            y[Resource.Bone] = Math.Min(y[Resource.Bone], 8);
            if (y.ContainsKey(Resource.Hide)) y[Resource.Hide] = Math.Min(y[Resource.Hide], 12);
        }
        if (harvestedPct > 0)
        {
            double left = 1 - harvestedPct;
            y[Resource.RawMeat] *= left;
            y[Resource.RawFat] *= left;
            y[Resource.Bone] *= Math.Max(0.7, left);
            if (y.ContainsKey(Resource.Hide)) y[Resource.Hide] *= Math.Max(0.5, left);
            y[Resource.Sinew] *= Math.Max(0.8, left);
        }
        foreach (var r in y.Keys.ToList()) y[r] *= ThoroughFactor(r);
        var c = new Carcass
        {
            Id = world.NextCarcassId++, Animal = animal, BodyWeightKg = body.WeightKg,
            DiedMinute = diedMinute, LastUpdatedMinute = diedMinute, Remaining = y, Cause = cause,
            FleshAtDeathKg = y[Resource.RawMeat] + y[Resource.RawFat],
        };
        return Put(world, tile, c);
    }

    /// Bones and nothing else, already this far gone (effective hours of rot since death): what a
    /// scattered skeleton leaves (Remains). Old bones carry the cause their owner died of.
    public static Carcass Bones(World world, TileId tile, string animal, double bodyWeightKg, double boneKg, long diedMinute, Cause cause, double effectiveHours) =>
        Put(world, tile, new Carcass
        {
            Id = world.NextCarcassId++, Animal = animal, BodyWeightKg = bodyWeightKg, DiedMinute = diedMinute, Cause = cause,
            EffectiveHours = effectiveHours, LastUpdatedMinute = world.Minute, Remaining = new() { [Resource.Bone] = boneKg },
        });

    static Carcass Put(World world, TileId tile, Carcass c)
    {
        if (!world.Tiles.TryGetValue(tile, out var state)) world.Tiles[tile] = state = new TileState();
        (state.Carcasses ??= []).Add(c);
        world.CarcassTiles?.Add(tile);
        return c;
    }

    /// The tiles that hold a carcass, in tile order: an index over World.Tiles, built on the first
    /// question after a load and kept by Put and Remove, the only ways a carcass comes or goes. What
    /// looks for meat across many tiles (Fauna.MeatSensed, Remains.Hour) walks this, not the map.
    public static IReadOnlyCollection<TileId> TilesWith(World world) =>
        world.CarcassTiles ??= [.. world.Tiles.Where(t => t.Value.Carcasses != null).Select(t => t.Key)];

    /// Everything that rots: what is left of a carcass once its flesh is gone is bone (and ivory).
    static readonly Resource[] SoftTissue = [Resource.RawMeat, Resource.RawFat, Resource.Hide, Resource.Sinew, Resource.Feather, Resource.MammothHide];

    /// The ledger's meat and fat left on it.
    public static double FleshKg(Carcass c) => c.Remaining.GetValueOrDefault(Resource.RawMeat) + c.Remaining.GetValueOrDefault(Resource.RawFat);

    /// The ledger's meat and fat cut off it so far (CutKg, before the yield factors).
    public static double CutFleshKg(Carcass c) => c.CutKg.GetValueOrDefault(Resource.RawMeat) + c.CutKg.GetValueOrDefault(Resource.RawFat);

    /// Anything on it that rots, uncut or cut and waiting.
    public static bool HasSoftTissue(Carcass c) => HasUncutSoftTissue(c) || SoftTissue.Any(r => c.Pending.GetValueOrDefault(r) > 0);

    public static bool HasUncutSoftTissue(Carcass c) => SoftTissue.Any(r => c.Remaining.GetValueOrDefault(r) > 0);

    /// The flesh has rotted off (Remains.Hour): every uncut soft tissue goes. Cut pieces waiting on it
    /// are already someone's yield, charged their decay when they were cut (PutBack), so they stay
    /// until the carcass itself is gone. Old (this rebuild): they rotted with it, while YieldKg still
    /// counted them (bug, fixed).
    public static void RotAway(Carcass c)
    {
        foreach (var r in SoftTissue)
            if (c.Remaining.ContainsKey(r)) c.Remaining[r] = 0;
    }

    /// Takes a carcass off its tile; a tile left with nothing on it leaves World.Tiles.
    public static void Remove(World world, TileId tile, Carcass c)
    {
        var state = world.Tiles.GetValueOrDefault(tile);
        if (state?.Carcasses?.Remove(c) != true) throw new InvalidOperationException($"Carcass {c.Id} is not on {tile}");
        if (state.Carcasses.Count > 0) return;
        state.Carcasses = null;
        world.CarcassTiles?.Remove(tile);
        Map.Tidy(world, tile);
    }

    public static Carcass? Find(World world, TileId tile, int id) =>
        world.Tiles.TryGetValue(tile, out var t) ? t.Carcasses?.FirstOrDefault(c => c.Id == id) : null;

    // ---- decay ----

    /// Old GetDecayMultiplier: cold all but stops the clock.
    public static double DecayFactor(double temperatureF) => temperatureF switch
    {
        < 0 => 0.1,
        < 15 => 0.25,
        < 32 => 0.5,
        < 50 => 1.0,
        _ => 2.0,
    };

    /// Old DecayLevel: 0 fresh, 1 spoiled.
    public static double DecayLevel(double effectiveHours) => effectiveHours switch
    {
        <= 4 => effectiveHours * 0.05,
        <= 12 => 0.2 + (effectiveHours - 4) * 0.0375,
        <= 24 => 0.5 + (effectiveHours - 12) * 0.025,
        _ => Math.Min(1.0, 0.8 + (effectiveHours - 24) * 0.02),
    };

    public static double DecayLevel(Carcass c) => DecayLevel(c.EffectiveHours);

    /// How far gone it is now, without settling the decay owed: reading it changes nothing.
    public static double DecayLevelNow(World world, TileId tile, Carcass c) => DecayLevel(EffectiveHoursNow(world, tile, c));

    /// Temperature-weighted hours since death as of now, without settling them (Remains.EffectiveHours).
    public static double EffectiveHoursNow(World world, TileId tile, Carcass c) =>
        c.EffectiveHours + Remains.EffectiveHours(world, tile, c.LastUpdatedMinute, world.Minute);

    public static double RawHours(World world, Carcass c) => (world.Minute - c.DiedMinute) / 60.0;
    public static bool IsFrozen(World world, Carcass c) => c.LastKnownTempF < 15 && RawHours(world, c) > 2;

    /// Old ScentIntensity: fresh blood carries furthest.
    public static double Scent(World world, Carcass c)
    {
        double h = RawHours(world, c);
        double baseScent = h < 2 ? 0.8 : h < 6 ? 0.6 : h < 12 ? 0.4 : h < 24 ? 0.2 : 0.1;
        return Math.Min(1.0, baseScent + c.ScentBonus);
    }

    /// Ages the carcass to world.Minute, each clock hour at the tile's air at the top of that hour (the
    /// rot timeline, Remains.EffectiveHours), so how often anyone looks cannot change how fast it rots. The air is what reaches the surface
    /// (Exposure.Surface): old game: decay read the air a person feels, wind chill and all, so a windy
    /// thaw kept meat as if it were freezing (bug, fixed). The old reading also included a fire on the
    /// tile; this one does not.
    public static void CatchUp(World world, TileId tile, Carcass c)
    {
        if (c.LastUpdatedMinute >= world.Minute) return;
        double deltaF = Map.TemperatureDeltaF(Map.Of(world), tile);
        c.LastKnownTempF = DecayTimeline.TempF(world, world.Minute - 1, deltaF); // the last hour charged
        c.EffectiveHours = EffectiveHoursNow(world, tile, c);
        c.LastUpdatedMinute = world.Minute;
    }

    /// An animal eats raw meat straight off the carcass, uncut: up to kg of it. Returns the kg eaten.
    /// An animal tears off what a careful butcher would cut, not what full processing scrapes from the
    /// bone, so it eats in careful kilograms: each one takes ThoroughFactor of the ledger. Scaling the
    /// ledger up to full processing's changed nothing about how long a carcass feeds a pack, or what a
    /// kilo a wolf eats costs whoever butchers what is left.
    public static double EatMeat(Carcass c, double kg)
    {
        if (kg < 0) throw new ArgumentOutOfRangeException(nameof(kg), kg, "Cannot eat a negative amount");
        double meat = MeatKg(c), bite = Math.Min(meat, kg);
        if (bite > 0) c.Remaining[Resource.RawMeat] = bite >= meat ? 0 : c.Remaining[Resource.RawMeat] - bite * ThoroughFactor(Resource.RawMeat);
        return bite;
    }

    /// The raw meat left on the carcass for an animal to eat, in careful kilograms (EatMeat).
    public static double MeatKg(Carcass c) => c.Remaining.GetValueOrDefault(Resource.RawMeat) / ThoroughFactor(Resource.RawMeat);

    // ---- butchering ----

    /// Kg still uncut on the carcass.
    public static double TotalRemainingKg(Carcass c) => c.Remaining.Values.Sum();
    public static bool AllCut(Carcass c) => TotalRemainingKg(c) < 0.01;
    /// Nothing left on it: all cut, and every cut piece carried off.
    public static bool IsButchered(Carcass c) => AllCut(c) && c.Pending.Count == 0;

    /// Old GetRemainingMinutes(mode): setup plus 2.75 minutes a kilo, scaled by the mode. The setup
    /// is charged once per carcass, not once per session (Prepare).
    public static int RemainingMinutes(Carcass c, ButcherMode mode) =>
        (int)Math.Ceiling((Math.Max(0, BaseButcheringMinutes - c.SetupMinutes) + WorkKg(c) * MinutesPerKgYield) * ModeProps(mode).TimeFactor);

    /// The kilograms the butchering time is charged on: the ledger at a careful butcher's yield, as the
    /// old game charged it, so scaling the ledger up to full processing's (ThoroughFactor) took no longer.
    static double WorkKg(Carcass c) => c.Remaining.Sum(x => x.Value / ThoroughFactor(x.Key));

    /// Effective minutes of butchering go to the setup first, until it is done; returns what is left for cutting.
    public static double Prepare(Carcass c, double minutes, ButcherMode mode)
    {
        double tf = ModeProps(mode).TimeFactor;
        double setup = Math.Min(minutes, Math.Max(0, BaseButcheringMinutes - c.SetupMinutes) * tf);
        c.SetupMinutes = BaseButcheringMinutes - c.SetupMinutes - setup / tf < 1e-9 ? BaseButcheringMinutes : c.SetupMinutes + setup / tf;
        return minutes - setup;
    }

    /// Effective minutes of cutting: that share of everything left comes off the carcass (CutKg)
    /// and yields (YieldKg, Pending) by mode, steady hands and (meat only) decay. Without a blade you
    /// tear off half the meat and some bone and ruin the rest. Kilograms removed are linear in
    /// minutes, so cutting a minute at a time takes what one long cut does. Once it is all cut,
    /// Settle applies the meat floor and drops what was too little to keep.
    /// skillFactor is the butcher's practice (Skills.ButcherYieldFactor): it wins back what clumsy hands
    /// and spoiling meat would lose, never more than the method recovers clean, so never more than the cut.
    public static void Cut(Carcass c, double minutes, bool hasCuttingTool, bool handsImpaired, ButcherMode mode, double skillFactor = 1.0)
    {
        if (minutes <= 0 || AllCut(c)) return;
        var props = ModeProps(mode);
        double fraction = Math.Min(1.0, minutes / (WorkKg(c) * MinutesPerKgYield * props.TimeFactor));
        double handsPct = handsImpaired ? 0.8 : 1.0;
        double meatDecayPct = 1.0 - DecayLevel(c) * 0.8;
        foreach (var r in c.Remaining.Keys.ToList())
        {
            // Trophy materials are not torn off by hand: they wait for a blade.
            if (!hasCuttingTool && r is Resource.Ivory or Resource.MammothHide) continue;
            double take = c.Remaining[r] * fraction;
            // What the method recovers from this cut with steady hands and fresh meat (old ButcheringModeConfig,
            // and the old bare-handed tear: half the meat and two thirds of the bone)...
            double clean = !hasCuttingTool
                ? r switch { Resource.RawMeat => take / ThoroughFactor(r) * 0.5, Resource.Bone => take * 0.67, _ => 0 }
                : take * props.YieldFactor.GetValueOrDefault(r, 1);
            // ...and what the hands and the rot cost of it, some of which practice wins back.
            double penaltyPct = handsPct * (r == Resource.RawMeat ? meatDecayPct : 1);
            double kg = Math.Min(clean, clean * penaltyPct * skillFactor);
            c.CutKg[r] = c.CutKg.GetValueOrDefault(r) + take;
            c.Remaining[r] = Math.Max(0, c.Remaining[r] - take);
            if (kg <= 0) continue;
            c.YieldKg[r] = c.YieldKg.GetValueOrDefault(r) + kg;
            c.Pending[r] = c.Pending.GetValueOrDefault(r) + kg;
        }
        if (AllCut(c)) Settle(c);
    }

    /// The whole carcass is cut: the old meat floor, and scraps too small to keep dropped. Both are
    /// judged on everything the carcass gave, never on one session's share of it, so how the work
    /// was split cannot change what it yields.
    static void Settle(Carcass c)
    {
        // Old floor: a little meat even from a ptarmigan, if there was that much to begin with.
        double meat = c.YieldKg.GetValueOrDefault(Resource.RawMeat);
        if (meat > 0 && meat < 0.1 && c.CutKg.GetValueOrDefault(Resource.RawMeat) >= 0.1)
        {
            c.Pending[Resource.RawMeat] = c.Pending.GetValueOrDefault(Resource.RawMeat) + 0.1 - meat;
            c.YieldKg[Resource.RawMeat] = 0.1;
        }
        foreach (var (r, kg) in c.YieldKg.ToList())
        {
            if (kg >= MinimumKg(r)) continue;
            c.YieldKg.Remove(r);
            c.Pending.Remove(r);
        }
    }

    /// What is cut and may go in a pack now: a resource waits on the carcass until the carcass has
    /// given a keepable piece of it (or Settle dropped it). Taken out of Pending; what the pack cannot
    /// hold goes back with PutBack.
    public static Inventory TakeReady(Carcass c)
    {
        var ready = new Inventory();
        foreach (var (r, kg) in c.Pending.ToList())
        {
            if (c.YieldKg[r] < MinimumKg(r)) continue;
            Items.Add(ready, r, kg);
            c.Pending.Remove(r);
        }
        return ready;
    }

    /// Cut pieces a full pack could not take wait on the carcass, already yielded: the next session
    /// carries them without cutting them again. Old game: RestoreYields put the yield back as uncut
    /// carcass and scaled it by mode, decay and hands a second time (bug, fixed).
    // Waiting cut meat does not rot further; decay was charged when it was cut.
    public static void PutBack(Carcass c, Inventory leftovers)
    {
        foreach (var (r, kg) in leftovers.Kg) c.Pending[r] = c.Pending.GetValueOrDefault(r) + kg;
    }

    /// Old Add* helpers dropped pieces too small to keep.
    static double MinimumKg(Resource r) => r switch
    {
        Resource.RawMeat or Resource.Bone => 0.05,
        Resource.Hide => 0.1 + 1e-12,
        Resource.Feather => 0.02 + 1e-12,
        Resource.Sinew => 0.05 + 1e-12,
        Resource.RawFat => 0.1,
        _ => 1e-12,
    };
}
