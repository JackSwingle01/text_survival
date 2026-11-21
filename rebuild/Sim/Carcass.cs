namespace Survival;

/// Old ButcheringMode: quick and messy, careful, or slow and thorough.
public enum ButcherMode { QuickStrip, Careful, FullProcessing }

/// Old ButcheringModeConfig. Yield factors per resource; anything not named takes 1.
public sealed record ButcherModeProps(double TimeFactor, IReadOnlyDictionary<Resource, double> YieldFactor, double ScentIncrease, double BloodySeverity);

/// A dead animal on a tile, as a tile delta (old CarcassFeature). Its yields are what is left of it.
public sealed class Carcass
{
    public required int Id { get; init; }
    public required string Animal { get; init; }
    public required double BodyWeightKg { get; init; }
    public required long DiedMinute { get; init; }
    /// Temperature-weighted hours since death, as of LastUpdatedMinute (old EffectiveHoursSinceDeath).
    public double EffectiveHours { get; set; }
    public required long LastUpdatedMinute { get; set; }
    public double LastKnownTempF { get; set; } = 32;
    /// Kg of RawMeat, Bone, Hide, Sinew, RawFat, Feather, Ivory and MammothHide left on it.
    public Dictionary<Resource, double> Remaining { get; init; } = [];
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

    public static ButcherModeProps ModeProps(ButcherMode mode) => mode switch
    {
        ButcherMode.QuickStrip => new(0.5, new Dictionary<Resource, double>
        {
            [Resource.RawMeat] = 0.8, [Resource.Hide] = 0, [Resource.Bone] = 0.5, [Resource.Sinew] = 0, [Resource.RawFat] = 0.5, [Resource.Feather] = 0,
        }, 0.2, 0.3),
        ButcherMode.Careful => new(1.0, new Dictionary<Resource, double>(), 0.1, 0.15),
        ButcherMode.FullProcessing => new(1.5, new Dictionary<Resource, double>
        {
            [Resource.RawMeat] = 1.1, [Resource.Sinew] = 1.2, [Resource.RawFat] = 1.1, [Resource.Feather] = 1.2,
        }, 0.05, 0.1),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// Old CarcassFeature.InitializeYieldsFromAnimal, from the body that died. Phase 3b kills call this.
    public static Carcass Create(World world, TileId tile, string animal, BodyState body, long diedMinute,
        bool isBird = false, bool isMegafauna = false, double harvestedPct = 0, IReadOnlyDictionary<Resource, double>? special = null)
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
        var c = new Carcass
        {
            Id = world.NextCarcassId++, Animal = animal, BodyWeightKg = body.WeightKg,
            DiedMinute = diedMinute, LastUpdatedMinute = diedMinute, Remaining = y,
        };
        if (!world.Tiles.TryGetValue(tile, out var state)) world.Tiles[tile] = state = new TileState();
        (state.Carcasses ??= []).Add(c);
        return c;
    }

    /// Takes a carcass off its tile; a tile left with nothing on it leaves World.Tiles.
    public static void Remove(World world, TileId tile, Carcass c)
    {
        var state = world.Tiles.GetValueOrDefault(tile);
        if (state?.Carcasses?.Remove(c) != true) throw new InvalidOperationException($"Carcass {c.Id} is not on {tile}");
        if (state.Carcasses.Count > 0) return;
        state.Carcasses = null;
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
    public static double RawHours(World world, Carcass c) => (world.Minute - c.DiedMinute) / 60.0;
    public static bool IsFrozen(World world, Carcass c) => c.LastKnownTempF < 15 && RawHours(world, c) > 2;

    /// Old ScentIntensity: fresh blood carries furthest.
    public static double Scent(World world, Carcass c)
    {
        double h = RawHours(world, c);
        double baseScent = h < 2 ? 0.8 : h < 6 ? 0.6 : h < 12 ? 0.4 : h < 24 ? 0.2 : 0.1;
        return Math.Min(1.0, baseScent + c.ScentBonus);
    }

    /// Ages the carcass to world.Minute in clock-hour chunks, each at the tile's air at the top of its hour,
    /// so how often anyone looks cannot change how fast it rots.
    // ponytail: the old Location.GetTemperature it read included a fire on the tile; the air here does not.
    public static void CatchUp(World world, TileId tile, Carcass c)
    {
        while (c.LastUpdatedMinute < world.Minute)
        {
            long next = Math.Min(world.Minute, (c.LastUpdatedMinute / 60 + 1) * 60);
            double tempF = Weather.AirAtF(world, tile, Weather.At(world, c.LastUpdatedMinute / 60 * 60), 0);
            c.LastKnownTempF = tempF;
            c.EffectiveHours += (next - c.LastUpdatedMinute) / 60.0 * DecayFactor(tempF);
            c.LastUpdatedMinute = next;
        }
    }

    /// An animal eats raw meat straight off the carcass, uncut: up to kg of it. Returns the kg eaten.
    public static double EatMeat(Carcass c, double kg)
    {
        if (kg < 0) throw new ArgumentOutOfRangeException(nameof(kg), kg, "Cannot eat a negative amount");
        double meat = c.Remaining.GetValueOrDefault(Resource.RawMeat), bite = Math.Min(meat, kg);
        if (bite > 0) c.Remaining[Resource.RawMeat] = meat - bite;
        return bite;
    }

    // ---- butchering ----

    /// Kg still uncut on the carcass.
    public static double TotalRemainingKg(Carcass c) => c.Remaining.Values.Sum();
    public static bool AllCut(Carcass c) => TotalRemainingKg(c) < 0.01;
    /// Nothing left on it: all cut, and every cut piece carried off.
    public static bool IsButchered(Carcass c) => AllCut(c) && c.Pending.Count == 0;

    /// Old GetRemainingMinutes(mode): setup plus 2.75 minutes a kilo, scaled by the mode. The setup
    /// is charged once per carcass, not once per session (Prepare).
    public static int RemainingMinutes(Carcass c, ButcherMode mode) =>
        (int)Math.Ceiling((Math.Max(0, BaseButcheringMinutes - c.SetupMinutes) + TotalRemainingKg(c) * MinutesPerKgYield) * ModeProps(mode).TimeFactor);

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
    public static void Cut(Carcass c, double minutes, bool hasCuttingTool, bool handsImpaired, ButcherMode mode)
    {
        if (minutes <= 0 || AllCut(c)) return;
        var props = ModeProps(mode);
        double fraction = Math.Min(1.0, minutes / (TotalRemainingKg(c) * MinutesPerKgYield * props.TimeFactor));
        double yieldFactor = handsImpaired ? 0.8 : 1.0;
        double meatDecay = 1.0 - DecayLevel(c) * 0.8;
        foreach (var r in c.Remaining.Keys.ToList())
        {
            // Trophy materials are not torn off by hand: they wait for a blade.
            if (!hasCuttingTool && r is Resource.Ivory or Resource.MammothHide) continue;
            double take = c.Remaining[r] * fraction;
            double kg = !hasCuttingTool
                ? r switch { Resource.RawMeat => take * 0.5 * yieldFactor * meatDecay, Resource.Bone => take * yieldFactor * 0.67, _ => 0 }
                : take * yieldFactor * props.YieldFactor.GetValueOrDefault(r, 1) * (r == Resource.RawMeat ? meatDecay : 1);
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
    // ponytail: waiting cut meat does not rot further; decay was charged when it was cut.
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
