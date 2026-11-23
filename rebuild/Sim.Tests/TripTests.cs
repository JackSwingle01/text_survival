namespace Survival.Tests;

/// The trip preview (Trip): a crossing's minutes as the terms that make them, adding up to exactly
/// what the crossing will take (Traversal.CrossingMinutes), read without changing the world.
public class TripTests
{
    static readonly AgentId P = AgentId.Player;

    static int Sum(TripTime t) => t.Terms.Sum(x => x.Minutes);

    /// Across the valley's ground, both paces, a heavy load and a lame body, under the seeded sky:
    /// the terms add up to the total, and the total is the crossing's own.
    [Fact]
    public void TheTerms_AddUpToTheCrossingsOwnMinutes_Everywhere()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        var lame = BodyState.BaselineHuman();
        Body.Damage(lame, new DamageInfo(0.5, DamageType.Blunt, BodyTarget.LeftLeg), Rng.ForSystem(1, "lame"));
        int checkedCrossings = 0;
        for (int minute = 0; minute < 3 * 1440; minute += 601)
        {
            w.Minute = minute;
            var sky = Weather.At(w, minute);
            for (int x = 0; x < m.Width; x += 3)
                for (int y = 0; y < m.Height; y += 3)
                {
                    var from = new TileId(x, y);
                    if (!m.TerrainAt(from).IsPassable()) continue;
                    foreach (var dir in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
                    {
                        var to = Map.Step(from, dir);
                        if (Map.Blocked(m, from, to) != null) continue;
                        foreach (var (body, load) in new[] { (BodyState.BaselineHuman(), 0.0), (lame, 1.2) })
                            foreach (var pace in new[] { Pace.Quick, Pace.Careful })
                            {
                                var preview = Trip.Terms(w, body, from, to, pace, sky, load);
                                Assert.Equal(preview.TotalMinutes, Sum(preview));
                                Assert.Equal(Traversal.CrossingMinutes(w, body, from, to, pace, sky, load), preview.TotalMinutes);
                                checkedCrossings++;
                            }
                    }
                }
        }
        Assert.True(checkedCrossings > 5000, $"{checkedCrossings} crossings");
    }

    [Fact]
    public void ThePreview_ChangesNothing()
    {
        var w = World.New(17);
        Sim.Step(w, new Wait(P), 1);
        string before = Save.ToJson(w);
        foreach (var dir in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
            Trip.Time(w, P, dir, Pace.Careful);
        Assert.Equal(before, Save.ToJson(w));
    }

    /// Each thing that slows a walk shows as its own term: the ground's shape, a careful pace, a load,
    /// snow, and a beaten path that saves time.
    [Fact]
    public void EachSlowing_ShowsAsItsOwnTerm()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        var calm = new WeatherSample(0, WeatherCondition.Clear, -10, 0, 0, 0, WindDirection.North);
        var body = BodyState.BaselineHuman();
        var (from, to) = (new TileId(2, 33), new TileId(2, 34)); // Forest -> Rock (TravelTests)
        var plain = Trip.Terms(w, body, from, to, Pace.Quick, calm);
        Assert.Equal(Map.BaseTraversalMinutes(m, from) + Map.BaseTraversalMinutes(m, to), plain.WalkingMinutes);
        Assert.True(plain.TerrainMinutes > 0);
        Assert.Equal(0, plain.CarefulMinutes);
        Assert.True(Trip.Terms(w, body, from, to, Pace.Careful, calm).CarefulMinutes > 0);
        Assert.True(Trip.Terms(w, body, from, to, Pace.Quick, calm, encumbrancePct: 1.5).LoadMinutes > 0);

        TestKit.EditGround(w, to, (g, _) => g.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = 0.6, CompactionPct = 0.1, FrozenFractionPct = 1 }));
        Assert.True(Trip.Terms(w, body, from, to, Pace.Quick, calm).GroundMinutes > 0);
        Assert.Equal(Traversal.CrossingMinutes(w, body, from, to, Pace.Careful, calm), Trip.Terms(w, body, from, to, Pace.Careful, calm).TotalMinutes);

        for (int i = 0; i < 40; i++) Tracks.RecordMove(w, from, to, TrackMaker.Human);
        Assert.True(Trip.Terms(w, body, from, to, Pace.Quick, calm).TrailMinutes < 0);
        Assert.Equal(Traversal.CrossingMinutes(w, body, from, to, Pace.Quick, calm), Trip.Terms(w, body, from, to, Pace.Quick, calm).TotalMinutes);
    }
}
