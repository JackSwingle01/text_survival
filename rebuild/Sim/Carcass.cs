namespace Survival;

public enum ButcherMode { QuickStrip, Careful, FullProcessing }

/// Authored in Content/butchering.json. Yield factors per resource; anything not named takes 1.
public sealed record ButcherModeProps(double TimeFactor, IReadOnlyDictionary<Resource, double> YieldFactor, double ScentIncrease, double BloodySeverity);

/// A dead animal on a tile, as a tile delta. Its yields are what is left of it.
public sealed class Carcass
{
    public required int Id { get; init; }
    public required string Animal { get; init; }
    public required double BodyWeightKg { get; init; }
    public required long DiedMinute { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Cause? Cause { get; init; }
    /// Temperature-weighted, as of LastUpdatedMinute.
    public double EffectiveHours { get; set; }
    public required long LastUpdatedMinute { get; set; }
    public double LastKnownTempF { get; set; } = 32;
    public Dictionary<Resource, double> Remaining { get; init; } = [];
    /// Meat and fat when it died: how stripped it is now is the share of this left, and a stripped carcass goes to bones sooner.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double FleshAtDeathKg { get; init; }
    public ButcherMode? Mode { get; set; }
    public double ScentBonus { get; set; }
    /// Minutes of the setup done, before the mode's time factor. Setup is paid once per carcass.
    public double SetupMinutes { get; set; }
    public SortedDictionary<Resource, double> CutKg { get; init; } = [];
    /// Kg yielded so far: what the scrap minimum and meat floor judge.
    public SortedDictionary<Resource, double> YieldKg { get; init; } = [];
    /// Kg yielded but not yet carried off: under the minimum so far, or no room in the pack.
    public SortedDictionary<Resource, double> Pending { get; init; } = [];
}

public static class Carcasses
{
    const double BaseButcheringMinutes = 30.0, MinutesPerKgYield = 2.75;

    /// Yield factors relative to a careful butcher's. The ledger holds what full processing recovers (Create scales
    /// it by ThoroughFactor); every mode recovers a share of it, never more than was cut.
    static readonly IReadOnlyDictionary<ButcherMode, ButcherModeProps> Rows = Content.Tables.Butchering;

    public static double ThoroughFactor(Resource r) => Rows[ButcherMode.FullProcessing].YieldFactor.GetValueOrDefault(r, 1);

    static IReadOnlyDictionary<Resource, double> Recovered(IReadOnlyDictionary<Resource, double> careful) =>
        Enum.GetValues<Resource>().Where(r => careful.ContainsKey(r) || Rows[ButcherMode.FullProcessing].YieldFactor.ContainsKey(r))
            .ToDictionary(r => r, r => careful.GetValueOrDefault(r, 1) / ThoroughFactor(r)).AsReadOnly();

    static readonly IReadOnlyDictionary<ButcherMode, ButcherModeProps> Modes =
        Enum.GetValues<ButcherMode>().ToDictionary(m => m, m => Rows[m] with { YieldFactor = Recovered(Rows[m].YieldFactor) }).AsReadOnly();

    public static ButcherModeProps ModeProps(ButcherMode mode) =>
        Modes.TryGetValue(mode, out var props) ? props : throw new ArgumentOutOfRangeException(nameof(mode), mode, null);

    /// The meat a carcass holds of its body's muscle.
    public const double MeatOfMusclePct = 0.65;

    public static Carcass Create(World world, TileId tile, string animal, BodyState body, long diedMinute,
        bool isBird = false, bool isMegafauna = false, double harvestedPct = 0, IReadOnlyDictionary<Resource, double>? special = null,
        Cause? cause = null)
    {
        var y = new Dictionary<Resource, double>();
        bool specialHide = special?.ContainsKey(Resource.MammothHide) == true;
        foreach (var (r, kg) in special ?? new Dictionary<Resource, double>()) y[r] = kg;
        y[Resource.RawMeat] = body.MuscleKg * MeatOfMusclePct;
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
            Id = world.NextCarcassId++,
            Animal = animal,
            BodyWeightKg = body.WeightKg,
            DiedMinute = diedMinute,
            LastUpdatedMinute = diedMinute,
            Remaining = y,
            Cause = cause,
            FleshAtDeathKg = y[Resource.RawMeat] + y[Resource.RawFat],
        };
        return Put(world, tile, c);
    }

    /// Bones only, already effectiveHours rotted: what a scattered skeleton leaves. Old bones keep the cause of death.
    public static Carcass Bones(World world, TileId tile, string animal, double bodyWeightKg, double boneKg, long diedMinute, Cause cause, double effectiveHours) =>
        Put(world, tile, new Carcass
        {
            Id = world.NextCarcassId++,
            Animal = animal,
            BodyWeightKg = bodyWeightKg,
            DiedMinute = diedMinute,
            Cause = cause,
            EffectiveHours = effectiveHours,
            LastUpdatedMinute = world.Minute,
            Remaining = new() { [Resource.Bone] = boneKg },
        });

    static Carcass Put(World world, TileId tile, Carcass c)
    {
        if (!world.Tiles.TryGetValue(tile, out var state)) world.Tiles[tile] = state = new TileState();
        (state.Carcasses ??= []).Add(c);
        world.CarcassTiles?.Add(tile);
        return c;
    }

    /// Index over World.Tiles, built on first use after a load and kept by Put and Remove, the only ways a carcass comes or goes.
    /// What looks for meat across many tiles walks this, not the map.
    public static IReadOnlyCollection<TileId> TilesWith(World world) =>
        world.CarcassTiles ??= [.. world.Tiles.Where(t => t.Value.Carcasses != null).Select(t => t.Key)];

    /// Everything that rots; what is left once it is gone is bone (and ivory).
    static readonly Resource[] SoftTissue = [Resource.RawMeat, Resource.RawFat, Resource.Hide, Resource.Sinew, Resource.Feather, Resource.MammothHide];

    public static double FleshKg(Carcass c) => c.Remaining.GetValueOrDefault(Resource.RawMeat) + c.Remaining.GetValueOrDefault(Resource.RawFat);

    public static double CutFleshKg(Carcass c) => c.CutKg.GetValueOrDefault(Resource.RawMeat) + c.CutKg.GetValueOrDefault(Resource.RawFat);

    public static bool HasSoftTissue(Carcass c) => HasUncutSoftTissue(c) || SoftTissue.Any(r => c.Pending.GetValueOrDefault(r) > 0);

    public static bool HasUncutSoftTissue(Carcass c) => SoftTissue.Any(r => c.Remaining.GetValueOrDefault(r) > 0);

    /// Cut pieces waiting are already someone's yield, charged their decay when cut, so they stay until the carcass is gone.
    public static void RotAway(Carcass c)
    {
        foreach (var r in SoftTissue)
            if (c.Remaining.ContainsKey(r)) c.Remaining[r] = 0;
    }

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

    public static double DecayFactor(double temperatureF) => temperatureF switch
    {
        < 0 => 0.1,
        < 15 => 0.25,
        < 32 => 0.5,
        < 50 => 1.0,
        _ => 2.0,
    };

    /// 0 fresh, 1 spoiled.
    public static double DecayLevel(double effectiveHours) => effectiveHours switch
    {
        <= 4 => effectiveHours * 0.05,
        <= 12 => 0.2 + (effectiveHours - 4) * 0.0375,
        <= 24 => 0.5 + (effectiveHours - 12) * 0.025,
        _ => Math.Min(1.0, 0.8 + (effectiveHours - 24) * 0.02),
    };

    public static double DecayLevel(Carcass c) => DecayLevel(c.EffectiveHours);

    /// Without settling the decay owed: reading it changes nothing.
    public static double DecayLevelNow(World world, TileId tile, Carcass c) => DecayLevel(EffectiveHoursNow(world, tile, c));

    public static double EffectiveHoursNow(World world, TileId tile, Carcass c) =>
        c.EffectiveHours + Remains.EffectiveHours(world, tile, c.LastUpdatedMinute, world.Minute);

    /// How long a carcass found frozen through has lain: long enough to freeze, and the cold has kept it.
    public const int FrozenThroughHours = 24;

    /// Frozen through at today's air, its hours dead kept by the cold: only the last minute of them rots it.
    public static void FrozenThrough(World world, TileId tile, Carcass c)
    {
        c.LastUpdatedMinute = Math.Max(0, world.Minute - 1);
        CatchUp(world, tile, c);
    }

    public static double RawHours(World world, Carcass c) => (world.Minute - c.DiedMinute) / 60.0;
    public static bool IsFrozen(World world, Carcass c) => c.LastKnownTempF < 15 && RawHours(world, c) > 2;

    /// Fresh blood carries furthest.
    public static double Scent(World world, Carcass c)
    {
        double h = RawHours(world, c);
        double baseScent = h < 2 ? 0.8 : h < 6 ? 0.6 : h < 12 ? 0.4 : h < 24 ? 0.2 : 0.1;
        return Math.Min(1.0, baseScent + c.ScentBonus);
    }

    /// Each clock hour ages at the tile's air at the top of that hour, so how often anyone looks cannot change how fast it rots.
    /// Surface air, not what a person feels: wind chill does not keep meat, nor does a fire on the tile.
    public static void CatchUp(World world, TileId tile, Carcass c)
    {
        if (c.LastUpdatedMinute >= world.Minute) return;
        double deltaF = Map.TemperatureDeltaF(Map.Of(world), tile);
        c.LastKnownTempF = DecayTimeline.TempF(world, world.Minute - 1, deltaF);
        c.EffectiveHours = EffectiveHoursNow(world, tile, c);
        c.LastUpdatedMinute = world.Minute;
    }

    /// An animal tears off what a careful butcher would cut, so it eats in careful kilograms (each takes ThoroughFactor
    /// of the ledger), and the ledger's scale cannot change how long a carcass feeds a pack.
    public static double EatMeat(Carcass c, double kg)
    {
        if (kg < 0) throw new ArgumentOutOfRangeException(nameof(kg), kg, "Cannot eat a negative amount");
        double meat = MeatKg(c), bite = Math.Min(meat, kg);
        if (bite > 0) c.Remaining[Resource.RawMeat] = bite >= meat ? 0 : c.Remaining[Resource.RawMeat] - bite * ThoroughFactor(Resource.RawMeat);
        return bite;
    }

    public static double MeatKg(Carcass c) => c.Remaining.GetValueOrDefault(Resource.RawMeat) / ThoroughFactor(Resource.RawMeat);

    public static double TotalRemainingKg(Carcass c) => c.Remaining.Values.Sum();
    public static bool AllCut(Carcass c) => TotalRemainingKg(c) < 0.01;

    public static bool CanCut(Resource r, bool hasCuttingTool) => hasCuttingTool || r is not (Resource.Ivory or Resource.MammothHide);

    public static bool AllCut(Carcass c, bool hasCuttingTool) =>
        c.Remaining.Where(x => CanCut(x.Key, hasCuttingTool)).Sum(x => x.Value) < 0.01;
    public static bool IsButchered(Carcass c) => AllCut(c) && c.Pending.Count == 0;

    /// The setup is charged once per carcass, not once per session (Prepare).
    public static int RemainingMinutes(Carcass c, ButcherMode mode, bool hasCuttingTool = true) =>
        (int)Math.Ceiling((Math.Max(0, BaseButcheringMinutes - c.SetupMinutes) + WorkKg(c, hasCuttingTool) * MinutesPerKgYield) * ModeProps(mode).TimeFactor);

    /// Charged at a careful butcher's yield, so the ledger's scale (ThoroughFactor) takes no longer.
    static double WorkKg(Carcass c, bool hasCuttingTool) =>
        c.Remaining.Where(x => CanCut(x.Key, hasCuttingTool)).Sum(x => x.Value / ThoroughFactor(x.Key));

    public static double Prepare(Carcass c, double minutes, ButcherMode mode)
    {
        double tf = ModeProps(mode).TimeFactor;
        double setup = Math.Min(minutes, Math.Max(0, BaseButcheringMinutes - c.SetupMinutes) * tf);
        c.SetupMinutes = BaseButcheringMinutes - c.SetupMinutes - setup / tf < 1e-9 ? BaseButcheringMinutes : c.SetupMinutes + setup / tf;
        return minutes - setup;
    }

    /// Kilograms removed are linear in minutes, so cutting a minute at a time takes what one long cut does.
    /// skillFactor wins back what clumsy hands and spoiling meat would lose, never more than the method recovers clean.
    public static void Cut(Carcass c, double minutes, bool hasCuttingTool, bool handsImpaired, ButcherMode mode, double skillFactor = 1.0)
    {
        if (minutes <= 0 || AllCut(c, hasCuttingTool)) return;
        var props = ModeProps(mode);
        double fraction = Math.Min(1.0, minutes / (WorkKg(c, hasCuttingTool) * MinutesPerKgYield * props.TimeFactor));
        double handsPct = handsImpaired ? 0.8 : 1.0;
        double meatDecayPct = 1.0 - DecayLevel(c) * 0.8;
        foreach (var r in c.Remaining.Keys.ToList())
        {
            if (!CanCut(r, hasCuttingTool)) continue;
            double take = c.Remaining[r] * fraction;
            // Steady hands and fresh meat, less the bare-handed tear: a blade never wastes more than hands in the same mode.
            double clean = take * props.YieldFactor.GetValueOrDefault(r, 1) * (hasCuttingTool ? 1 : BareHandsPct(r));
            double penaltyPct = handsPct * (r == Resource.RawMeat ? meatDecayPct : 1);
            double kg = Math.Min(clean, clean * penaltyPct * skillFactor);
            c.CutKg[r] = c.CutKg.GetValueOrDefault(r) + take;
            c.Remaining[r] = Math.Max(0, c.Remaining[r] - take);
            if (kg <= 0) continue;
            c.YieldKg[r] = c.YieldKg.GetValueOrDefault(r) + kg;
            c.Pending[r] = c.Pending.GetValueOrDefault(r) + kg;
        }
        if (AllCut(c, hasCuttingTool)) Settle(c);
    }

    static double BareHandsPct(Resource r) => r switch { Resource.RawMeat => 0.5, Resource.Bone => 0.67, _ => 0 };

    /// Judged on everything the carcass gave, never one session's share, so how the work was split cannot change the yield.
    static void Settle(Carcass c)
    {
        // A little meat even from a ptarmigan, if there was that much to begin with.
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

    /// A resource waits on the carcass until it has given a keepable piece (or Settle dropped it). What the pack
    /// cannot hold goes back with PutBack.
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

    /// Already yielded: the next session carries them without cutting or scaling them again. Waiting meat does not rot further.
    public static void PutBack(Carcass c, Inventory leftovers)
    {
        foreach (var (r, kg) in leftovers.Kg) c.Pending[r] = c.Pending.GetValueOrDefault(r) + kg;
    }

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
