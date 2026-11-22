using CsCheck;
using Survival;
using static Survival.Tests.Verification.ConservationWorlds;

namespace Survival.Tests.Verification;

/// Law 4: the ground's water ledger (GroundState). At every moment, under any weather and any
/// chunking of time:
///
///   WaterAddedM = StoredWaterM + WaterDrainedM + WaterEvaporatedM + WaterOverflowedM
///
/// and every sink only grows. Driven two ways: the state-level API (Advance, AddWater, AddSnow,
/// Compact) over generated weather, and the world's lazy catch-up (Ground.CatchUp) under the seed's
/// own sky, looked at whenever.
public class ConservationGroundTests
{
    static readonly SubstrateProfile[] Substrates =
        [SubstrateProfile.Loam, SubstrateProfile.ForestFloor, SubstrateProfile.Slope, SubstrateProfile.Rock, SubstrateProfile.Peat, SubstrateProfile.IceSheet];

    public enum GroundOp { Advance, AddWater, AddSnow, Compact }

    public sealed record Op(GroundOp Kind, int Minutes, SurfaceWeather Weather, double Amount);

    /// Weather as the ground can meet it: -40 to 70 °F, calm to a gale, night to full sun, and snowfall
    /// and rain from nothing to a cloudburst (about 12 mm an hour).
    static readonly Gen<SurfaceWeather> Weathers = Gen.Select(
        Gen.Double[-40, 70], Gen.Double[0, 1], Gen.Double[0, 1],
        Gen.Frequency((2, Gen.Const(0.0)), (1, Gen.Double[0, 2e-4])),
        Gen.Frequency((2, Gen.Const(0.0)), (1, Gen.Double[0, 2e-4])),
        (t, wind, sun, snow, rain) => new SurfaceWeather(t, wind, sun, snow, rain));

    static readonly Gen<Op> Ops = Gen.Select(
        Gen.Frequency((6, Gen.Const(GroundOp.Advance)), (1, Gen.Const(GroundOp.AddWater)), (1, Gen.Const(GroundOp.AddSnow)), (1, Gen.Const(GroundOp.Compact))),
        Gen.Frequency((2, Gen.Int[1, 15]), (2, Gen.Int[15, 240]), (1, Gen.Int[240, 2880])),
        Weathers, Gen.Double[0, 0.08],
        (k, m, w, a) => new Op(k, m, w, a));

    public sealed record Scene(int Substrate, double FrozenPct, Op[] Ops);

    static readonly Gen<Scene> Scenes = Gen.Select(Gen.Int[0, Substrates.Length - 1], Gen.Double[0, 1], Ops.Array[1, 40],
        (s, f, ops) => new Scene(s, f, ops));

    static double Accounted(GroundState g, SubstrateProfile sub) =>
        Ground.StoredWaterM(g, sub) + g.WaterDrainedM + g.WaterEvaporatedM + g.WaterOverflowedM;

    /// The old GroundTests tolerance was 1e-6 m; this is tighter, so drift shows long before it matters.
    const double ToleranceM = 1e-9;

    static void Balanced(GroundState g, SubstrateProfile sub, string at)
    {
        double leak = g.WaterAddedM - Accounted(g, sub);
        Assert.True(Math.Abs(leak) <= ToleranceM,
            $"{at}: added {g.WaterAddedM:R} m, held {Ground.StoredWaterM(g, sub):R} + drained {g.WaterDrainedM:R} + evaporated {g.WaterEvaporatedM:R} + overflowed {g.WaterOverflowedM:R}: off by {leak:R} m");
    }

    static void StateLevel(Scene s)
    {
        var sub = Substrates[s.Substrate];
        var g = Ground.Baseline(sub, s.FrozenPct);
        Balanced(g, sub, "baseline");
        for (int i = 0; i < s.Ops.Length; i++)
        {
            var op = s.Ops[i];
            var (added, drained, evaporated, overflowed) = (g.WaterAddedM, g.WaterDrainedM, g.WaterEvaporatedM, g.WaterOverflowedM);
            switch (op.Kind)
            {
                case GroundOp.Advance: Ground.Advance(g, sub, op.Minutes, op.Weather); break;
                case GroundOp.AddWater: Ground.AddWater(g, sub, op.Amount); break;
                case GroundOp.AddSnow: Ground.AddSnow(g, sub, op.Amount); break;
                case GroundOp.Compact: Ground.Compact(g, sub, op.Amount * 12); break;
            }
            string at = $"op {i} {op.Kind} on {s.Substrate}";
            Balanced(g, sub, at);
            Assert.True(g.WaterAddedM >= added && g.WaterDrainedM >= drained && g.WaterEvaporatedM >= evaporated && g.WaterOverflowedM >= overflowed,
                $"{at}: a ledger column went down");
            Assert.True(Ground.StoredWaterM(g, sub) >= -ToleranceM, $"{at}: negative water held");
        }
    }

    [Fact]
    public void Ground_WaterIn_IsHeldOrGone() => Scenes.Check(StateLevel, iter: 150);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Ground_WaterIn_IsHeldOrGone_Many() => Scenes.Check(StateLevel, iter: 5000);

    /// Seeds that caught the ledger losing water below NegligibleM, a trace at a time, until the
    /// sum passed the tolerance: settling saturated snow squeezed out sub-nanometre drops that
    /// were dropped rather than passed down. One per substrate it showed on.
    [Theory]
    [InlineData("9Tq5kYPuyrg1")] // lake ice: dense wet snow settling away, 7.8e-9 m lost
    [InlineData("56EoPwFat2J1")] // rock
    [InlineData("1qUzTAbM0Qe1")] // loam
    [InlineData("eyMB1PE73A91")] // slope
    public void Ground_WaterIn_IsHeldOrGone_FoundSeeds(string seed) => Scenes.Sample(StateLevel, seed: seed, iter: 1, threads: 1);

    /// The world's tiles, looked at whenever: an observed tile carried on from where it was left, an
    /// unobserved one from its class's cache. The ledger holds on every one.
    public sealed record WorldScene(int[] Tiles, int[] LookMinutes);

    static readonly Gen<WorldScene> WorldScenes = Gen.Select(Gen.Int[0, int.MaxValue].Array[1, 4], Gen.Int[1, 3 * 1440].Array[1, 8],
        (t, m) => new WorldScene(t, m));


    static void WorldLevel(WorldScene s)
    {
        var w = Seeded();
        var m = Map.Of(w);
        var tiles = s.Tiles.Select(i => new TileId(i % m.Width, i / m.Width % m.Height)).ToList();
        foreach (var dt in s.LookMinutes)
        {
            w.Minute += dt;
            foreach (var t in tiles)
            {
                var g = Ground.CatchUp(w, t);
                Balanced(g, Ground.Substrate(m, t), $"{t} at minute {w.Minute}");
            }
        }
    }

    [Fact]
    public void Ground_CatchUp_KeepsTheLedger() => WorldScenes.Check(WorldLevel, iter: 6);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Ground_CatchUp_KeepsTheLedger_Many() => WorldScenes.Check(WorldLevel, iter: 150);
}
