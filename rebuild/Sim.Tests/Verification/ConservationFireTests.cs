using CsCheck;
using Survival;
using static Survival.Tests.Verification.ConservationWorlds;

namespace Survival.Tests.Verification;

/// Law 3: fire fuel. Every kilogram put on a pit is, at every minute, either still on it (unburned
/// or burning) or burned; nothing appears in catch-up whatever the granularity:
///
///   Σ added = Fire.TotalKg + burned,   burned = BurnedSinceLitKg + Σ what each dead fire turned to charcoal from
///   CharcoalKg = 4% (CharcoalYieldPct) of every burned mass a dead fire settled
///
/// Burned is the fire's own tally (Fire.Consume adds to BurnedSinceLitKg); mass that leaves the
/// pit without being tallied is the leak this law looks for, a last gram included. Temperatures stay
/// in 0..the hottest fuel's peak at the largest size factor. Each fuel burns at its own rate, and more
/// fuel shortens the burn only by speeding the fire up, by no more than its size can.
public class ConservationFireTests
{
    static readonly Resource[] FuelKinds = [.. Fire.Props.Keys.Order()];
    const double CharcoalYieldPct = 0.04; // Fire.CharcoalYieldPct
    static readonly double HottestF = Fire.Props.Values.Max(p => p.PeakTempF) * 1.1; // ActiveTemperatureF's largest sizeFactor

    public sealed record Addition(int AtMinute, int Fuel, double Kg);

    public sealed record FireScene(int Pit, double[] BurningKg, double[] UnburnedKg, Addition[] Additions, int Minutes, int[] Chunks);

    static Gen<double[]> Mix(int zeroWeight) =>
        Gen.Frequency((zeroWeight, Gen.Const(0.0)), (1, Gen.Double[0.001, 4])).Array[FuelKinds.Length];

    static readonly Gen<FireScene> Scenes = Gen.Select(
        Gen.Int[0, 2], Mix(3), Mix(4),
        Gen.Select(Gen.Int[0, 600], Gen.Int[0, FuelKinds.Length - 1], Gen.Double[0.001, 6], (m, f, kg) => new Addition(m, f, kg)).Array[0, 12],
        Gen.Int[1, 900], Gen.Int[1, 120].Array[1, 30],
        (pit, b, u, adds, min, chunks) => new FireScene(pit, b, u, adds, min, chunks));

    static FireState Light(FireScene s, TileId tile, long minute)
    {
        var f = new FireState { Tile = tile, LastUpdatedMinute = minute, Pit = (PitType)s.Pit };
        for (int i = 0; i < FuelKinds.Length; i++)
        {
            if (s.BurningKg[i] > 0) f.BurningKg[FuelKinds[i]] = s.BurningKg[i];
            if (s.UnburnedKg[i] > 0) f.UnburnedKg[FuelKinds[i]] = s.UnburnedKg[i];
        }
        return f;
    }

    sealed class Tally
    {
        public double Added, Burned, Settled;
    }

    /// Stacks on the fire, burning and waiting, to see which ones a minute removed.
    static HashSet<(bool Burning, Resource Fuel)> Stacks(FireState f) =>
        [.. f.BurningKg.Keys.Select(r => (true, r)), .. f.UnburnedKg.Keys.Select(r => (false, r))];

    /// Fire.Transfer and Fire.Consume take a stack under a gram (0.001 kg) with the rest of it: that
    /// mass must have gone somewhere counted, like any other.
    static void Vanished(FireState f, double vanished, HashSet<(bool, Resource)> stacksBefore, string at)
    {
        int dropped = stacksBefore.Except(Stacks(f)).Count();
        Assert.True(Math.Abs(vanished) <= 1e-12, $"{at}: {vanished:R} kg left the pit uncounted, {dropped} stacks gone ({Show(Snapshot(f))})");
    }

    static void AddCounted(FireState f, Tally t, Resource fuel, double kg, string at)
    {
        double before = Fire.TotalKg(f);
        var stacks = Stacks(f);
        double added = Fire.AddFuel(f, fuel, kg);
        t.Added += added;
        Vanished(f, before + added - Fire.TotalKg(f), stacks, at);
    }

    /// One minute of Fire.Tick with the ledger kept around it.
    static void TickCounted(FireState f, Tally t, long minute, string where)
    {
        double before = Fire.TotalKg(f), bsl = f.BurnedSinceLitKg, charcoal = f.CharcoalKg;
        var stacks = Stacks(f);
        Fire.Tick(f, new FireId(0), minute, []);
        double settled = f.CharcoalKg > charcoal ? bsl : 0; // a dead fire settles all it had burned
        double counted = f.BurnedSinceLitKg - bsl + settled;
        t.Burned += counted;
        t.Settled += settled;
        Vanished(f, before - Fire.TotalKg(f) - counted, stacks, $"{where}, minute {minute}");
        double temp = Fire.TemperatureF(f);
        Assert.True(temp >= 0 && temp <= HottestF + 1e-9, $"{where}, minute {minute}: {temp:R} °F");
    }

    static SortedDictionary<Resource, double> Snapshot(FireState f)
    {
        var d = new SortedDictionary<Resource, double>();
        foreach (var (r, kg) in f.BurningKg) AddTo(d, r, kg);
        foreach (var (r, kg) in f.UnburnedKg) AddTo(d, r, kg);
        return d;
    }

    static void Ledger(FireScene s)
    {
        var f = Light(s, default, 0);
        var t = new Tally { Added = Fire.TotalKg(f) };
        var adds = s.Additions.OrderBy(a => a.AtMinute).ToList();
        for (long m = 1; m <= s.Minutes; m++)
        {
            foreach (var a in adds.Where(a => a.AtMinute == m - 1)) AddCounted(f, t, FuelKinds[a.Fuel], a.Kg, $"adding at minute {m - 1}");
            TickCounted(f, t, m, "per-minute");
            Assert.True(Math.Abs(t.Added - Fire.TotalKg(f) - t.Burned) <= 1e-9,
                $"minute {m}: added {t.Added:R} = on the pit {Fire.TotalKg(f):R} + burned {t.Burned:R}?");
            Assert.True(Math.Abs(f.CharcoalKg - t.Settled * CharcoalYieldPct) <= 1e-12, $"minute {m}: charcoal {f.CharcoalKg:R}, 4% of {t.Settled:R}");
        }
    }

    /// Fire.Transfer and Fire.Consume once deleted a stack under a gram uncounted (fixed): no dust sink.
    [Fact]
    public void Fire_FuelAdded_IsOnThePitOrBurned() => Scenes.Check(Ledger, iter: 150);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Fire_FuelAdded_IsOnThePitOrBurned_Many() => Scenes.Check(Ledger, iter: 5000);

    static readonly ThreadLocal<World> FireWorld = new(() => Bare(5));

    /// The world catches a pit up whenever someone looks (Fire.CatchUp): looking every minute, every
    /// so often, or once at the end leaves the same fire.
    static void Granularity(FireScene s)
    {
        string Run(IEnumerable<int> chunks)
        {
            var w = FireWorld.Value!; // a pit needs no more of the world than its clock
            w.Fires.Clear();
            w.Minute = 0;
            var id = new FireId(w.NextFireId++);
            w.Fires[id] = Light(s, Camp(w), 0);
            var adds = s.Additions.OrderBy(a => a.AtMinute).ToList();
            void AddAt(long minute)
            {
                foreach (var a in adds.Where(a => a.AtMinute == minute))
                    if (w.Fires.TryGetValue(id, out var pit)) Fire.AddFuel(pit, FuelKinds[a.Fuel], a.Kg);
            }
            AddAt(0);
            long at = 0;
            using var steps = chunks.GetEnumerator();
            while (at < s.Minutes)
            {
                long next = Math.Min(s.Minutes, at + (steps.MoveNext() ? steps.Current : s.Minutes));
                // A log goes on at its minute, however often anyone looks.
                if (adds.FirstOrDefault(a => a.AtMinute > at && a.AtMinute < next) is { } early) next = early.AtMinute;
                w.Minute = next;
                if (w.Fires.ContainsKey(id)) Fire.CatchUp(w, id, []);
                AddAt(next);
                at = next;
            }
            var f = w.Fires[id];
            return $"{Show(Snapshot(f))} | burned {f.BurnedSinceLitKg:R} | charcoal {f.CharcoalKg:R} | embers {f.HasEmbers} {f.EmberHoursRemaining:R}";
        }
        Assert.Equal(Run(Enumerable.Repeat(1, s.Minutes)), Run(s.Chunks));
    }

    [Fact]
    public void Fire_CatchUp_IsTheSameAtAnyGranularity() => Scenes.Check(Granularity, iter: 60);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Fire_CatchUp_IsTheSameAtAnyGranularity_Many() => Scenes.Check(Granularity, iter: 2000);

    // ---- more fuel never shortens the burn ----

    public sealed record MoreScene(FireScene Base, int Fuel, double Kg);

    static readonly Gen<MoreScene> MoreScenes = Gen.Select(Scenes, Gen.Int[0, FuelKinds.Length - 1], Gen.Double[0.001, 6],
        (b, f, kg) => new MoreScene(b, f, kg));

    static FireState Copy(FireState f) => new()
    {
        Tile = f.Tile, LastUpdatedMinute = f.LastUpdatedMinute, Pit = f.Pit, UnburnedKg = new(f.UnburnedKg), BurningKg = new(f.BurningKg),
        HasEmbers = f.HasEmbers, EmberDurationHours = f.EmberDurationHours, EmberHoursRemaining = f.EmberHoursRemaining,
        EmberStartTempF = f.EmberStartTempF, LastBurningTempF = f.LastBurningTempF, BurnedSinceLitKg = f.BurnedSinceLitKg, CharcoalKg = f.CharcoalKg,
    };

    /// Minutes of flame, and minutes until the last ember is cold, left as it is.
    static (int Flame, int Warm) Burn(FireState f)
    {
        var x = Copy(f);
        int flame = 0, warm = 0;
        while (Fire.IsActive(x) || x.HasEmbers)
        {
            if (Fire.IsActive(x)) flame++;
            warm++;
            Fire.Tick(x, new FireId(0), warm, []);
            if (warm > 100_000) throw new InvalidOperationException("a fire that never goes out");
        }
        return (flame, warm);
    }

    /// More fuel never shortens the flames. The embers after them start from whichever fuel burned
    /// last (Fire.ToEmbers), so they may lose a little: at most 1% of the whole burn (120k samples
    /// lost at most 0.4%). Old game: tinder dragged oak down to the mixed mean rate, a 31% loss
    /// (2 kg of oak in an open pit, 217 min alone, 149 with 0.5 kg of tinder), now caught.
    const double EmbersKeptPct = 0.99;

    static void MoreFuel(MoreScene s, double warmKeptPct)
    {
        var f = Light(s.Base, default, 0);
        if (!Fire.IsActive(f)) return; // a cold pit: added fuel only waits for a light
        var more = Copy(f);
        var fuel = FuelKinds[s.Fuel];
        if (Fire.AddFuel(more, fuel, s.Kg) <= 0) return;
        var (flame, warm) = Burn(f);
        var (flameMore, warmMore) = Burn(more);
        Assert.True(flameMore >= flame, $"{s.Kg:R} kg more {fuel} on {Show(Snapshot(f))} ({(PitType)s.Base.Pit}): flames for {flameMore} min, without it {flame} min");
        // The burn is counted in whole minutes.
        Assert.True(warmMore >= Math.Floor(warm * warmKeptPct), $"{s.Kg:R} kg more {fuel} on {Show(Snapshot(f))} ({(PitType)s.Base.Pit}): warm for {warmMore} min, without it {warm} min");
    }

    [Fact]
    public void Fire_MoreFuel_NeverShortensTheFlames() => MoreScenes.Check(s => MoreFuel(s, EmbersKeptPct), iter: 150);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Fire_MoreFuel_NeverShortensTheFlames_Many() => MoreScenes.Check(s => MoreFuel(s, EmbersKeptPct), iter: 5000);

    /// The old bug's own case: tinder on burning oak takes the larger share and goes first; the oak burns on after it.
    [Fact]
    public void Fire_TinderOnOak_LeavesTheOakItsTime()
    {
        var oak = new FireState { Tile = default, LastUpdatedMinute = 0, Pit = PitType.Open, BurningKg = { [Resource.Oak] = 2 } };
        var withTinder = Copy(oak);
        Fire.AddFuel(withTinder, Resource.Tinder, 0.5);
        var (alone, _) = Burn(oak);
        var (mixed, _) = Burn(withTinder);
        Assert.Equal(217, alone);
        Assert.True(mixed >= alone, $"oak alone flames {alone} min, with tinder {mixed} min");
    }

    // ---- how a mix burns: the fire's burn, shared by table rate times mass ----

    public sealed record MixScene(int Pit, double[] BurningKg);

    static readonly Gen<MixScene> Mixes = Gen.Select(Gen.Int[0, 2], Mix(2), (pit, b) => new MixScene(pit, b));

    static FireState Burning(int pit, IEnumerable<KeyValuePair<Resource, double>> kg)
    {
        var f = new FireState { Tile = default, LastUpdatedMinute = 0, Pit = (PitType)pit };
        foreach (var (r, m) in kg) if (m > 0) f.BurningKg[r] = m;
        return f;
    }

    /// (a) The old game's burn for one fuel, restated: the size factor's rate over the pit's
    /// efficiency, the stack scaled down by that fraction, a last gram counted burned. A minute that
    /// takes the fire down past a size band's edge (1.5, 4 or 7 kg) burns at the band it leaves until it
    /// reaches the edge and at the next for the rest; the old game billed it all at the band it left.
    static (double Left, double Burned) OldMinute(Resource fuel, double kg, PitType pit)
    {
        static double Size(double kg) => kg < 1.5 ? 0.9 : kg < 4.0 ? 1.0 : kg < 7.0 ? 1.1 : 1.2;
        double efficiency = pit == PitType.Stone ? 1.15 : 1.0;
        double rate = Fire.Props[fuel].BurnRateKgPerHour; // one fuel's mean is its own rate exactly
        double PerMinute(double atKg) => rate * Size(atKg) / efficiency / 60.0;
        double consumed = PerMinute(kg);
        double edge = new[] { 7.0, 4.0, 1.5 }.FirstOrDefault(e => kg >= e && kg - consumed < e);
        if (edge > 0) consumed = kg - edge + (1 - (kg - edge) / PerMinute(kg)) * PerMinute(edge - 1e-9);
        consumed = Math.Min(kg, consumed);
        double left = kg * (1.0 - consumed / kg);
        return left < 0.001 ? (0, kg) : (left, consumed);
    }

    public sealed record SingleScene(int Pit, int Fuel, double Kg, int Minutes);

    static readonly Gen<SingleScene> Singles = Gen.Select(Gen.Int[0, 2], Gen.Int[0, FuelKinds.Length - 1],
        Gen.Frequency((3, Gen.Double[0.001, 12]), (1, Gen.Double[0.001, 0.01])), Gen.Int[1, 600], (pit, fuel, kg, min) => new SingleScene(pit, fuel, kg, min));

    static void SingleFuel(SingleScene s)
    {
        var fuel = FuelKinds[s.Fuel];
        var f = Burning(s.Pit, [new(fuel, s.Kg)]);
        double kg = s.Kg, burned = 0;
        for (int m = 1; m <= s.Minutes && kg > 0; m++)
        {
            var (left, b) = OldMinute(fuel, kg, (PitType)s.Pit);
            Fire.Tick(f, new FireId(0), m, []);
            kg = left; burned += b;
            Assert.True(Math.Abs(Fire.BurningKg(f) - kg) <= 1e-12, $"{fuel} {(PitType)s.Pit}, minute {m}: {Fire.BurningKg(f):R} kg burning, the old model {kg:R}");
            if (Fire.IsActive(f)) Assert.True(Math.Abs(f.BurnedSinceLitKg - burned) <= 1e-12, $"{fuel}, minute {m}: burned {f.BurnedSinceLitKg:R}, the old model {burned:R}");
        }
    }

    [Fact]
    public void Fire_OneFuel_BurnsAsTheOldGameDid() => Singles.Check(SingleFuel, iter: 400);

    /// A gram more Amadou on 5 kg of it in a mound flamed 117 min against 118 without it: the gram kept
    /// the fire over 4 kg for one more minute, billed in full at the bigger band's rate, and it fell
    /// behind (CsCheck seed 9E0gmYdnroU5, Fire_MoreOfTheSameFuel_NeverShortensTheBurn_Many).
    [Fact]
    public void Fire_AGramMore_AcrossASizeBand_NeverShortensTheBurn() =>
        MoreFuel(new MoreScene(new FireScene(1, [.. FuelKinds.Select(r => r == Resource.Amadou ? 5.0 : 0)], new double[FuelKinds.Length], [], 1, [1]),
            Array.IndexOf(FuelKinds, Resource.Amadou), 0.0011233327397750922), 1.0);

    /// 3 g more pine on 2 kg of it in a stone pit stayed warm 145 min against 146 without it: the
    /// mean peak of one fuel came out a rounding error off its own peak, the embers' length sits on
    /// a whole minute, and the error picked the side (CsCheck seed cHFSEvDRgWP1,
    /// Fire_MoreOfTheSameFuel_NeverShortensTheBurn_Many).
    [Fact]
    public void Fire_AGramMore_OfOneFuel_LeavesTheEmbersTheirTime() =>
        MoreFuel(new MoreScene(new FireScene(2, [.. FuelKinds.Select(r => r == Resource.Pine ? 2.0 : 0)], new double[FuelKinds.Length], [], 1, [1]),
            Array.IndexOf(FuelKinds, Resource.Pine), 0.0033670508448069274), 1.0);

    /// One minute of a mix with nothing waiting: what each fuel lost, or null where a stack burned out
    /// or the fire burned everything (the share then is the stack, not the rate).
    static Dictionary<Resource, double>? LostInAMinute(FireState f)
    {
        var before = new Dictionary<Resource, double>(f.BurningKg);
        double total = Fire.BurningKg(f);
        Fire.Tick(f, new FireId(0), 1, []);
        if (f.BurningKg.Count != before.Count || Fire.BurningKg(f) <= 0) return null;
        return before.ToDictionary(x => x.Key, x => x.Value - f.BurningKg[x.Key]);
    }

    /// (b) A mix burns no faster than its fastest fuel and no slower than its slowest would, alone,
    /// at the same burning mass in the same pit.
    static void Between(MixScene s)
    {
        var kg = FuelKinds.Select((r, i) => new KeyValuePair<Resource, double>(r, s.BurningKg[i])).Where(x => x.Value > 0).ToList();
        if (kg.Count == 0) return;
        double total = kg.Sum(x => x.Value);
        var lost = LostInAMinute(Burning(s.Pit, kg));
        if (lost == null) return;
        var pure = kg.Select(x => total - Fire.BurningKg(Tap(Burning(s.Pit, [new(x.Key, total)])))).ToList();
        double mixed = lost.Values.Sum();
        Assert.True(mixed >= pure.Min() - 1e-12 && mixed <= pure.Max() + 1e-12,
            $"{Show(new SortedDictionary<Resource, double>(kg.ToDictionary(x => x.Key, x => x.Value)))} ({(PitType)s.Pit}) lost {mixed:R} kg, pure fires {pure.Min():R}..{pure.Max():R}");
    }

    static FireState Tap(FireState f) { Fire.Tick(f, new FireId(0), 1, []); return f; }

    [Fact]
    public void Fire_AMix_BurnsBetweenItsFuels() => Mixes.Check(Between, iter: 400);

    /// (c) Within a mix, a faster fuel loses a larger share of itself each minute than a slower one.
    static void FasterGoesFirst(MixScene s)
    {
        var kg = FuelKinds.Select((r, i) => new KeyValuePair<Resource, double>(r, s.BurningKg[i])).Where(x => x.Value > 0).ToList();
        if (LostInAMinute(Burning(s.Pit, kg)) is not { } lost) return;
        foreach (var (fast, fastKg) in kg)
            foreach (var (slow, slowKg) in kg)
            {
                if (Fire.Props[fast].BurnRateKgPerHour <= Fire.Props[slow].BurnRateKgPerHour) continue;
                Assert.True(lost[fast] / fastKg > lost[slow] / slowKg,
                    $"{fast} lost {lost[fast] / fastKg:P3} of itself, {slow} {lost[slow] / slowKg:P3} ({(PitType)s.Pit})");
            }
    }

    [Fact]
    public void Fire_InAMix_TheFasterFuelGoesFirst() => Mixes.Check(FasterGoesFirst, iter: 400);

    /// More of the one fuel already burning, nothing waiting: never shorter at all.
    static readonly Gen<MoreScene> SameFuel = Gen.Select(Gen.Int[0, 2], Gen.Int[0, FuelKinds.Length - 1], Gen.Double[0.05, 8], Gen.Double[0.001, 6],
        (pit, fuel, kg, more) => new MoreScene(
            new FireScene(pit, [.. FuelKinds.Select((_, i) => i == fuel ? kg : 0.0)], new double[FuelKinds.Length], [], 1, [1]), fuel, more));

    [Fact]
    public void Fire_MoreOfTheSameFuel_NeverShortensTheBurn() => SameFuel.Check(s => MoreFuel(s, 1.0), iter: 300);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Fire_MoreOfTheSameFuel_NeverShortensTheBurn_Many() => SameFuel.Check(s => MoreFuel(s, 1.0), iter: 10000);

}
