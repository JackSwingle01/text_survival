namespace Survival.Tests;

/// Footprints and desire paths. Ported from the old TrackAndTrailTests (TrackRegistryTests,
/// TrackTrafficTests, GroundMemoryIntegrationTests, TrailWearTests), on a world clock instead of
/// an accumulator: World.Minute moves and erosion is derived from the sky.
public class TracksTests
{
    static readonly WeatherSample CalmCold = new(0, WeatherCondition.Clear, -10, 0, 0, 0, WindDirection.North);
    static readonly WeatherSample Blizzard = new(0, WeatherCondition.Blizzard, -15, 0.8, 0.9, 1, WindDirection.North);
    static readonly WeatherSample SteadySnow = new(0, WeatherCondition.HeavySnow, -10, 0.5, 0.5, 1, WindDirection.North);

    static World Ground(WeatherSample? sky = null, int seed = 17) => new() { Seed = seed, FixedWeather = sky ?? CalmCold };

    static readonly TileId A = new(3, 3), B = new(4, 3);

    static TrackSign? Sign(World w, TileId t, TrackMaker maker = TrackMaker.Human) =>
        Tracks.Read(w, t).Where(s => s.Maker == maker).Cast<TrackSign?>().FirstOrDefault();

    static double FreshnessOf(World w, TileId t, TrackMaker maker = TrackMaker.Human) => Sign(w, t, maker)?.FreshnessPct ?? 0;
    static int TrafficOf(World w, TileId t, TrackMaker maker = TrackMaker.Human) => Sign(w, t, maker)?.Traffic ?? 0;

    // ---- footprints (old TrackRegistryTests) ----

    [Fact]
    public void Stamp_MarksBothTilesWithHeading()
    {
        var w = Ground();
        TileId from = new(4, 5), to = new(4, 4); // north
        Tracks.RecordMove(w, from, to, TrackMaker.Human);
        Assert.Equal(Direction.North, Assert.Single(Tracks.Read(w, from)).Heading);
        Assert.Equal(Direction.North, Assert.Single(Tracks.Read(w, to)).Heading);
    }

    [Fact]
    public void Stamp_SameTileTwice_RefreshesRatherThanDuplicates()
    {
        var w = Ground();
        Tracks.RecordMove(w, A, B, TrackMaker.Paw);
        w.Minute += 600;
        double afterWait = FreshnessOf(w, A, TrackMaker.Paw);
        Tracks.RecordMove(w, A, B, TrackMaker.Paw);
        Assert.Equal(1.0, Assert.Single(Tracks.Read(w, A)).FreshnessPct, 3);
        Assert.True(afterWait < 1.0, "the first pass should have aged before the second");
    }

    [Fact]
    public void Stamp_DifferentMakers_KeepBoth()
    {
        var w = Ground();
        Tracks.RecordMove(w, A, B, TrackMaker.Paw);
        Tracks.RecordMove(w, A, B, TrackMaker.Hoof);
        Assert.Equal(2, Tracks.Read(w, A).Count);
    }

    [Fact]
    public void Stamp_NoMove_RecordsNothing()
    {
        var w = Ground();
        Tracks.RecordMove(w, A, A, TrackMaker.Human);
        Assert.Empty(Tracks.Read(w, A));
        Assert.Empty(w.Tiles);
    }

    [Fact]
    public void Freshness_FadesFasterInBadWeather()
    {
        var calm = Ground(CalmCold);
        var storm = Ground(Blizzard);
        Tracks.RecordMove(calm, A, B, TrackMaker.Hoof);
        Tracks.RecordMove(storm, A, B, TrackMaker.Hoof);
        calm.Minute = storm.Minute = 240;
        Assert.True(FreshnessOf(storm, A, TrackMaker.Hoof) < FreshnessOf(calm, A, TrackMaker.Hoof));
    }

    [Fact]
    public void Freshness_WeatherWhileAwayCounts_WithoutAnythingTicking()
    {
        // A storm that blew through while nobody looked still erased the prints: seed 2048's
        // weather is asked only at the reading, never minute by minute.
        var w = Ground(sky: null, seed: 2048);
        Tracks.RecordMove(w, A, B, TrackMaker.Human);
        w.Minute = 60;
        Assert.True(FreshnessOf(w, A) > 0.9);
        w.Minute = 10 * 1440;
        Assert.Empty(Tracks.Read(w, A));
    }

    [Fact]
    public void Freshness_DeeperTracksOutlastLighterOnes()
    {
        var w = Ground();
        TileId light = new(1, 0), heavy = new(3, 0);
        Tracks.RecordMove(w, light, new(1, 1), TrackMaker.Paw, individualDepth: 0.5);
        Tracks.RecordMove(w, heavy, new(3, 1), TrackMaker.Hoof, individualDepth: 2.5);
        w.Minute = 600;
        Assert.True(FreshnessOf(w, heavy, TrackMaker.Hoof) > FreshnessOf(w, light, TrackMaker.Paw));
    }

    [Fact]
    public void DeadTracks_AreNotReadable_AndArePrunedOnTheNextCrossing()
    {
        var w = Ground();
        Tracks.RecordMove(w, A, B, TrackMaker.Human);
        w.Minute = 100_000;
        Assert.Empty(Tracks.Read(w, A));
        Assert.Equal(0, FreshnessOf(w, A));
        Tracks.RecordMove(w, new(50, 50), new(51, 50), TrackMaker.Human);
        Assert.False(w.Tiles.ContainsKey(A)); // nothing else on it: the tile is pruned whole
        Assert.DoesNotContain(new EdgeId(A, B), w.Trails.Keys);
    }

    [Fact]
    public void DeadSign_PassesNothingOn()
    {
        // old game: a dead but not-yet-pruned sign still handed on its heaviest walker, so how deep
        // the next print was depended on when the prune last ran (bug, fixed).
        var w = Ground();
        Tracks.RecordMove(w, A, B, TrackMaker.Hoof, individualDepth: 3.0); // lifespan 2880*3*(0.6+0.4)
        w.Minute = (long)(Tracks.BaseLifespanUnits * 3 / 0.5) + 1;  // calm cold: 0.5 units a minute
        Assert.Empty(Tracks.Read(w, A));
        Assert.True(w.Tiles[A].Tracks!.ContainsKey(TrackMaker.Hoof)); // dead but unswept, as the old prune window left it
        Tracks.RecordMove(w, A, B, TrackMaker.Hoof, individualDepth: 0.5);
        Assert.Equal(0.5, Sign(w, A, TrackMaker.Hoof)!.Value.Depth);
    }

    // ---- traffic (old TrackTrafficTests) ----

    [Fact]
    public void Traffic_CountsAWholeHerdFromOnePassage()
    {
        var w = Ground();
        Tracks.RecordMove(w, A, B, TrackMaker.Hoof, individuals: 12, individualDepth: 1.3);
        Assert.Equal(12, TrafficOf(w, A, TrackMaker.Hoof));
    }

    [Fact]
    public void Traffic_AccumulatesAcrossPassages()
    {
        var w = Ground();
        Tracks.RecordMove(w, A, B, TrackMaker.Human);
        Assert.Equal(1, TrafficOf(w, A));
        Tracks.RecordMove(w, A, B, TrackMaker.Human);
        Tracks.RecordMove(w, A, B, TrackMaker.Human);
        Assert.Equal(3, TrafficOf(w, A));
    }

    [Fact]
    public void Traffic_SummarizesDirectionAsANetVector()
    {
        var w = Ground();
        for (int i = 0; i < 6; i++) Tracks.RecordMove(w, A, B, TrackMaker.Human);
        for (int i = 0; i < 4; i++) Tracks.RecordMove(w, B, A, TrackMaker.Human);
        var t = w.Tiles[A].Tracks![TrackMaker.Human];
        Assert.Equal(2, t.DirectionX);
        Assert.Equal(10, t.EastWestTraffic);
        Assert.Equal(Direction.East, Sign(w, A)!.Value.Heading);
    }

    [Fact]
    public void Traffic_OldTrafficFadesBeforeNewIsAdded()
    {
        var w = Ground();
        for (int i = 0; i < 10; i++) Tracks.RecordMove(w, A, B, TrackMaker.Human);
        Assert.Equal(10, TrafficOf(w, A));
        w.Minute = 4000;
        Tracks.RecordMove(w, A, B, TrackMaker.Human);
        int after = TrafficOf(w, A);
        Assert.InRange(after, 1, 9);
    }

    [Fact]
    public void Traffic_FadesAsTheSignAges()
    {
        var w = Ground();
        Tracks.RecordMove(w, A, B, TrackMaker.Hoof, individuals: 12, individualDepth: 1.3);
        w.Minute = 3000;
        Assert.InRange(TrafficOf(w, A, TrackMaker.Hoof), 1, 11);
    }

    [Fact]
    public void Depth_AHerdsChurnOutlastsALoneWalker()
    {
        var w = Ground();
        TileId lone = new(6, 6), herd = new(8, 6);
        Tracks.RecordMove(w, lone, new(6, 7), TrackMaker.Hoof, individuals: 1, individualDepth: 1.3);
        Tracks.RecordMove(w, herd, new(8, 7), TrackMaker.Hoof, individuals: 12, individualDepth: 1.3);
        w.Minute = 3000;
        Assert.True(FreshnessOf(w, herd, TrackMaker.Hoof) > FreshnessOf(w, lone, TrackMaker.Hoof));
    }

    // ---- the seam (old GroundMemoryIntegrationTests) ----

    [Fact]
    public void RecordMove_FeedsBothTracksAndTrailWear()
    {
        var w = Ground();
        Tracks.RecordMove(w, A, B, TrackMaker.Human);
        Assert.Single(Tracks.Read(w, A));
        Assert.Single(Tracks.Read(w, B));
        Assert.Equal(1.0, Tracks.WearAt(w, A, B));
        Assert.Equal(TrailTier.None, Tracks.TierOf(w, A, B)); // one crossing is not a path
    }

    [Fact]
    public void Wear_IsSharedByBothDirectionsOfTravel()
    {
        var w = Ground();
        for (int i = 0; i < 3; i++)
        {
            Tracks.RecordMove(w, A, B, TrackMaker.Human);
            Tracks.RecordMove(w, B, A, TrackMaker.Human);
        }
        Assert.Equal(TrailTier.Trace, Tracks.TierOf(w, A, B));
        Assert.Equal(TrailTier.Trace, Tracks.TierOf(w, B, A));
        Assert.Single(w.Trails);
    }

    [Fact]
    public void NonAdjacentHop_MarksBothEnds_WearsNoEdge()
    {
        var w = Ground();
        TileId far = new(7, 3);
        for (int i = 0; i < 100; i++) Tracks.RecordMove(w, A, far, TrackMaker.Hoof);
        Assert.Single(Tracks.Read(w, A));
        Assert.Single(Tracks.Read(w, far));
        Assert.Empty(w.Trails);
        Assert.Equal(0, Tracks.TrailMinutes(w, A, B));
    }

    [Fact]
    public void AHerdBeatsInARouteALoneAnimalNeverWould()
    {
        var byHerd = Ground();
        var byLoner = Ground();
        double caribou = Math.Clamp(Math.Sqrt(120 / 70.0), 0.4, 3.0); // old AnimalType.Caribou.IndividualTrackDepth
        Tracks.RecordMove(byHerd, A, B, TrackMaker.Hoof, individuals: 12, individualDepth: caribou);
        Tracks.RecordMove(byLoner, A, B, TrackMaker.Hoof, individuals: 1, individualDepth: caribou);
        Assert.Equal(TrailTier.Path, Tracks.TierOf(byHerd, A, B));
        Assert.Equal(TrailTier.None, Tracks.TierOf(byLoner, A, B));
    }

    [Fact]
    public void Wear_IsLinearInHeadCount()
    {
        var together = Ground();
        var separately = Ground();
        Tracks.RecordMove(together, A, B, TrackMaker.Paw, individuals: 5, individualDepth: 0.8);
        for (int i = 0; i < 5; i++) Tracks.RecordMove(separately, A, B, TrackMaker.Paw, individuals: 1, individualDepth: 0.8);
        Assert.Equal(Tracks.WearAt(together, A, B), Tracks.WearAt(separately, A, B), 12);
    }

    [Fact]
    public void TrailMinutes_RewardOnlyRealPaths()
    {
        var w = Ground();
        Assert.Equal(0, Tracks.TrailMinutes(w, A, B));
        for (int i = 0; i < 6; i++) Tracks.RecordMove(w, A, B, TrackMaker.Human); // a trace
        Assert.Equal(0, Tracks.TrailMinutes(w, A, B));
        for (int i = 0; i < 8; i++) Tracks.RecordMove(w, A, B, TrackMaker.Human); // a path
        Assert.Equal(-1, Tracks.TrailMinutes(w, A, B));
        for (int i = 0; i < 21; i++) Tracks.RecordMove(w, A, B, TrackMaker.Human); // a trail
        Assert.Equal(-2, Tracks.TrailMinutes(w, A, B));
    }

    [Fact]
    public void Footprints_AgeFasterThanTrails()
    {
        var w = Ground(SteadySnow);
        for (int i = 0; i < 20; i++) Tracks.RecordMove(w, A, B, TrackMaker.Human);
        Assert.Equal(TrailTier.Path, Tracks.TierOf(w, A, B));
        w.Minute = 3 * 1440; // three days of steady snow
        Assert.Empty(Tracks.Read(w, A));
        Assert.Equal(TrailTier.Path, Tracks.TierOf(w, A, B));
    }

    // ---- desire paths (old TrailWearTests) ----

    [Fact]
    public void TierFor_DerivesFromWearAlone()
    {
        Assert.Equal(TrailTier.None, Tracks.TierFor(0));
        Assert.Equal(TrailTier.None, Tracks.TierFor(Tracks.TraceAt - 1));
        Assert.Equal(TrailTier.Trace, Tracks.TierFor(Tracks.TraceAt));
        Assert.Equal(TrailTier.Path, Tracks.TierFor(Tracks.PathAt));
        Assert.Equal(TrailTier.Trail, Tracks.TierFor(Tracks.TrailAt));
    }

    [Fact]
    public void HeavierMovers_WearGroundFaster()
    {
        var light = Ground();
        var heavy = Ground();
        for (int i = 0; i < 5; i++)
        {
            Tracks.RecordMove(light, A, B, TrackMaker.Paw, individualDepth: 0.76); // a wolf
            Tracks.RecordMove(heavy, A, B, TrackMaker.Hoof, individualDepth: 3.0); // a mammoth
        }
        Assert.Equal(TrailTier.Path, Tracks.TierOf(heavy, A, B));  // 15
        Assert.Equal(TrailTier.None, Tracks.TierOf(light, A, B));  // 3.8
    }

    [Fact]
    public void Wear_IsCapped()
    {
        var w = Ground();
        for (int i = 0; i < 500; i++) Tracks.RecordMove(w, A, B, TrackMaker.Hoof, individualDepth: 3.0);
        Assert.Equal(Tracks.MaxWear, Tracks.WearAt(w, A, B));
    }

    [Fact]
    public void Decay_ReclaimsAbandonedRoutes()
    {
        var w = Ground();
        for (int i = 0; i < 14; i++) Tracks.RecordMove(w, A, B, TrackMaker.Human);
        Assert.Equal(TrailTier.Path, Tracks.TierOf(w, A, B));
        w.Minute = 35 * 1440; // five weeks of still cold, walked by nobody
        Assert.Equal(0, Tracks.WearAt(w, A, B));
        Tracks.RecordMove(w, new(50, 50), new(50, 51), TrackMaker.Human);
        Assert.False(w.Trails.ContainsKey(new EdgeId(A, B)), "a reclaimed route is pruned from the save");
    }

    [Fact]
    public void Decay_BadWeatherBuriesAPathWithoutUndoingIt()
    {
        var calm = Ground(CalmCold);
        var storm = Ground(Blizzard);
        for (int i = 0; i < 20; i++) { Tracks.RecordMove(calm, A, B, TrackMaker.Human); Tracks.RecordMove(storm, A, B, TrackMaker.Human); }
        calm.Minute = storm.Minute = 5 * 1440;
        Assert.True(Tracks.WearAt(storm, A, B) < Tracks.WearAt(calm, A, B));
        Assert.Equal(TrailTier.Path, Tracks.TierOf(calm, A, B));
        Assert.Equal(TrailTier.Trace, Tracks.TierOf(storm, A, B)); // loses its bonus, not its existence
    }

    [Fact]
    public void Decay_TrailsOutliveFootprintsByAWideMargin()
    {
        var w = Ground();
        for (int i = 0; i < 60; i++) Tracks.RecordMove(w, A, B, TrackMaker.Human);
        w.Minute = (long)(Tracks.BaseLifespanUnits * 3.0 / 0.5); // outlasts the deepest print the game can leave
        Assert.Equal(TrailTier.Trail, Tracks.TierOf(w, A, B));
    }

    // ---- erosion is derived, not accumulated ----

    [Theory]
    [InlineData(17)]
    [InlineData(42)]
    [InlineData(2048)]
    public void Erosion_DerivedEqualsThePerMinuteSum_Over30SeededDays(int seed)
    {
        var w = new World { Seed = seed };
        double sum = 0;
        for (long t = 1; t <= 30 * 1440; t++)
        {
            sum += Tracks.ErosionPerMinute(Weather.At(w, t));
            if (t % 101 == 0) Assert.Equal(sum, Tracks.ErosionAt(w, t), 1e-6);
        }
        Assert.Equal(sum, Tracks.ErosionAt(w, 30 * 1440), 1e-6);
        // Asked in any order, from a fresh cache, it answers the same.
        var fresh = new World { Seed = seed };
        Assert.Equal(Tracks.ErosionAt(w, 12345), Tracks.ErosionAt(fresh, 12345));
        Assert.Equal(0, Tracks.ErosionAt(fresh, 0));
    }

    [Fact]
    public void ErosionRate_IsTheOldFormula()
    {
        Assert.Equal(0.5, Tracks.ErosionPerMinute(CalmCold));
        Assert.Equal(0.5 + 0.9 * 6 + 0.8 * 3, Tracks.ErosionPerMinute(Blizzard), 12);
        Assert.Equal(2.5, Tracks.ErosionPerMinute(CalmCold with { TemperatureC = 2 })); // 35.6 °F thaws
        Assert.Equal(0.5, Tracks.ErosionPerMinute(CalmCold with { TemperatureC = 1 })); // 33.8 °F does not
    }

    // ---- wiring: travel, saves ----

    static World Walker(WeatherSample? sky = null)
    {
        var map = Map.Generate(17);
        var w = new World { Seed = 17, GeneratedMap = map, FixedWeather = sky ?? CalmCold with { TemperatureC = 5 } };
        w.Agents[AgentId.Player] = new Agent { Id = AgentId.Player, Tile = new TileId(2, 33) }; // Forest, Rock to the south: 7 minutes
        return w;
    }

    [Fact]
    public void Walking_LeavesPrintsOnBothTiles_AndWearsTheEdge()
    {
        var w = Walker();
        var r = Sim.Step(w, new Travel(AgentId.Player, Direction.South, Pace.Quick), 60);
        Assert.Equal("arrived", r.InterruptedReason);
        TileId forest = new(2, 33), rock = new(2, 34);
        var sign = Assert.Single(Tracks.Read(w, rock));
        Assert.Equal(TrackMaker.Human, sign.Maker);
        Assert.Equal(Direction.South, sign.Heading);
        Assert.Single(Tracks.Read(w, forest));
        Assert.Equal(1.0, Tracks.WearAt(w, forest, rock));
    }

    [Fact]
    public void WalkingARouteOften_MakesItFaster()
    {
        var w = Walker();
        TileId forest = new(2, 33), rock = new(2, 34);
        var body = w.Agent(AgentId.Player).Body;
        int fresh = Traversal.CrossingMinutes(w, body, forest, rock, Pace.Quick, CalmCold);
        Assert.Equal(6, fresh); // the 68 kg man; the old 75 kg traveller took 7
        for (int i = 0; i < 40; i++)
        {
            var dir = w.Agent(AgentId.Player).Tile == forest ? Direction.South : Direction.North;
            Assert.Equal("arrived", Sim.Step(w, new Travel(AgentId.Player, dir, Pace.Careful), 60).InterruptedReason);
        }
        Assert.Equal(TrailTier.Trail, Tracks.TierOf(w, forest, rock));
        Assert.Equal(-2, Traversal.EdgeMinutes(w, rock, forest));
        // Today's ground (Ground) may slow both; the beaten route is still two minutes quicker than bare.
        int beaten = Traversal.CrossingMinutes(w, body, forest, rock, Pace.Quick, CalmCold);
        w.Trails.Remove(new EdgeId(forest, rock));
        Assert.Equal(Traversal.CrossingMinutes(w, body, forest, rock, Pace.Quick, CalmCold) - 2, beaten);
    }

    [Fact]
    public void ATileWithNothingLeftOnIt_LeavesTheSave()
    {
        var w = new World { Seed = 17, FixedWeather = CalmCold };
        TileId a = new(4, 4), b = new(5, 4), c = new(10, 10), d = new(11, 10);
        Tracks.RecordMove(w, a, b, TrackMaker.Human);
        Assert.True(w.Tiles.ContainsKey(a));
        w.Minute = 10_000; // calm cold: one person's prints are gone after 5760 minutes
        Tracks.RecordMove(w, c, d, TrackMaker.Human);
        Assert.False(w.Tiles.ContainsKey(a), "a clean tile is generation again: nothing to save");
        Assert.False(w.Tiles.ContainsKey(b));
        // Anything else on it keeps it, prints or not.
        Items.StashAt(w, c);
        w.Minute = 20_000;
        Tracks.RecordMove(w, a, b, TrackMaker.Human);
        Assert.True(w.Tiles.ContainsKey(c));
        Assert.Null(w.Tiles[c].Tracks);
    }

    [Fact]
    public void AHerdAgent_StampsAllItsMembers()
    {
        var w = Walker();
        var herd = w.Agent(AgentId.Player);
        herd.Members.AddRange([BodyState.BaselineHuman(), BodyState.BaselineHuman()]);
        herd.Crossing = new Crossing { From = herd.Tile, Dir = Direction.South, Pace = Pace.Quick, TotalMinutes = 1 };
        Traversal.Advance(w, herd, []);
        Assert.Equal(3, Sign(w, new(2, 34))!.Value.Traffic);
        Assert.Equal(3.0, Tracks.WearAt(w, new(2, 33), new(2, 34)));
    }

    [Fact]
    public void SaveMidRun_RoundTripsTracksAndWear_AndSavesNoErosion()
    {
        var w = new World { Seed = 17 };
        for (int i = 0; i < 30; i++) { w.Minute = i * 97; Tracks.RecordMove(w, A, B, TrackMaker.Human); Tracks.RecordMove(w, B, new(4, 4), TrackMaker.Paw, 5, 0.76); }
        w.Minute += 500;
        string json = Save.ToJson(w);
        Assert.DoesNotContain("rosion", json);
        var loaded = Save.FromJson(json);
        Assert.Equal(json, Save.ToJson(loaded));
        Assert.Equal(Tracks.Read(w, B), Tracks.Read(loaded, B));
        Assert.Equal(Tracks.WearAt(w, A, B), Tracks.WearAt(loaded, A, B));
        Assert.Equal(Tracks.TierOf(w, B, new(4, 4)), Tracks.TierOf(loaded, B, new(4, 4)));
    }
}

/// Exact outputs of the old TrackRegistry and TrailWear, fed minute by minute the same sky the
/// rebuild derives (golden harness: old classes advanced per minute beside Tracks; max |old - new|
/// 1.3e-11, erosion over 30 seeded days within 3.3e-8 of ~1.8e5).
public class TracksGoldenTests
{
    const int Digits = 6;
    static readonly WeatherSample CalmCold = new(0, WeatherCondition.Clear, -10, 0, 0, 0, WindDirection.North);
    static readonly WeatherSample Blizzard = new(0, WeatherCondition.Blizzard, -15, 0.8, 0.9, 1, WindDirection.North);
    static World Ground(WeatherSample? sky) => new() { Seed = 17, FixedWeather = sky };
    static WeatherSample? Sky(string name) => name switch { "calm" => CalmCold, "blizzard" => Blizzard, _ => null };

    [Theory]
    // Re-captured after the weather pre-roll and phase fix moved every seed's sky (old accumulator
    // fed the rebuild's sky, tmp harness p2fix/golden-tracks); the derivation still matches it to 1e-7.
    [InlineData(17, 137185.90247788394)]
    [InlineData(42, 161908.29485801823)]
    [InlineData(2048, 163690.55805103504)]
    public void Erosion_After30SeededDays_MatchesTheOldAccumulator(int seed, double old) =>
        Assert.Equal(old, Tracks.ErosionAt(new World { Seed = seed }, 30 * 1440), 1e-6);

    // One person's prints: the minute they are gone, and freshness at 1 h, 6 h, 1 day, 2 days.
    // The overview's "~4 days calm, ~6 h blizzard" is 96.0 h and 5.8 h.
    [Theory]
    [InlineData("calm", 5760, 0.9895833333333334, 0.9375, 0.75, 0.5)]
    [InlineData("blizzard", 347, 0.8270833333333332, 0, 0, 0)]
    [InlineData("seed17", 715, 0.9386756453640829, 0.5072435461692317, 0, 0)] // re-captured: seed 17's sky moved
    public void OnePersonsPrints_FadeAsOld(string sky, int diesAt, double h1, double h6, double d1, double d2)
    {
        var w = Ground(Sky(sky));
        Tracks.RecordMove(w, new(4, 5), new(4, 4), TrackMaker.Human);
        double At(long m) { w.Minute = m; var s = Tracks.Read(w, new(4, 5)); return s.Count == 0 ? 0 : s[0].FreshnessPct; }
        Assert.Equal(h1, At(60), Digits);
        Assert.Equal(h6, At(360), Digits);
        Assert.Equal(d1, At(1440), Digits);
        Assert.Equal(d2, At(2880), Digits);
        Assert.True(At(diesAt - 1) > 0);
        Assert.Equal(0, At(diesAt));
    }

    static readonly Dictionary<string, ((int Minute, int Dx, int Dy, int Count)[] Moves, int ReadAt)> Scripts = new()
    {
        ["6E 4W"] = ([.. Enumerable.Range(0, 6).Select(i => (i, 1, 0, 1)), .. Enumerable.Range(6, 4).Select(i => (i, -1, 0, 1))], 10),
        ["herd 12 north"] = ([(0, 0, -1, 12)], 0),
        ["herd 12 north, 3000 later"] = ([(0, 0, -1, 12)], 3000),
        ["5N 5S"] = ([.. Enumerable.Range(0, 10).Select(i => (i, 0, i % 2 == 0 ? -1 : 1, 1))], 20),
        ["3N 3E"] = ([(0, 0, -1, 1), (1, 1, 0, 1), (2, 0, -1, 1), (3, 1, 0, 1), (4, 0, -1, 1), (5, 1, 0, 1)], 30),
        ["10E then 1 later"] = ([.. Enumerable.Range(0, 10).Select(i => (i, 1, 0, 1)), (4010, 1, 0, 1)], 4010),
        ["8N 2S over 2 days"] = ([.. Enumerable.Range(0, 8).Select(i => (i * 300, 0, -1, 1)), (2500, 0, 1, 1), (2800, 0, 1, 1)], 2900),
    };

    // Old TrafficOf and LocationInspector.DirectionSummary, calm cold, every move out of (10,10).
    [Theory]
    [InlineData("6E 4W", 10, TrafficPattern.EastWest, Direction.East, 0.9999068906629283, 1.8645939985303577)]
    [InlineData("herd 12 north", 12, TrafficPattern.Heading, Direction.North, 1, 1.985640646055102)]
    [InlineData("herd 12 north, 3000 later", 9, TrafficPattern.Heading, Direction.North, 0.737700104816005, 1.985640646055102)]
    [InlineData("5N 5S", 10, TrafficPattern.NorthSouth, Direction.South, 0.9989757972922109, 1.8645939985303577)]
    [InlineData("3N 3E", 6, TrafficPattern.Mixed, Direction.East, 0.9972523567674932, 1.5796365868861404)]
    [InlineData("10E then 1 later", 7, TrafficPattern.Heading, Direction.East, 1, 1.6786324792909282)]
    [InlineData("8N 2S over 2 days", 8, TrafficPattern.Heading, Direction.North, 0.9901775000282217, 1.7674839563239981)]
    public void ScriptedPassages_ReadAsOld(string script, int traffic, TrafficPattern pattern, Direction heading, double freshness, double depth)
    {
        var (moves, readAt) = Scripts[script];
        var w = Ground(CalmCold);
        TileId at = new(10, 10);
        foreach (var (minute, dx, dy, count) in moves)
        {
            w.Minute = minute;
            Tracks.RecordMove(w, at, new(at.X + dx, at.Y + dy), TrackMaker.Human, count);
        }
        w.Minute = readAt;
        var s = Assert.Single(Tracks.Read(w, at));
        Assert.Equal(traffic, s.Traffic);
        Assert.Equal(pattern, s.Pattern);
        Assert.Equal(heading, s.Heading);
        Assert.Equal(freshness, s.FreshnessPct, Digits);
        Assert.Equal(depth, s.Depth, Digits);
    }

    // Trail wear under seed-17 weather for 60 days: who crosses how often, and the minute each
    // tier is first reached (-1 never). The overview's herd timelines (caribou Path in ~8 days,
    // a lone bear never) come from how often real herds cross an edge, which arrives with phase 3.
    [Theory]
    [InlineData("human daily", 1, 1.0, 1440, -1, -1, -1, 1.0)]
    // Re-captured after the weather pre-roll and phase fix moved seed 17's sky (p2fix/golden-tracks).
    [InlineData("human twice a day", 1, 1.0, 720, 5760, 16560, 42480, 50)]
    [InlineData("12 caribou every 4 days", 12, 1.3093073414159542, 5760, 9120, 5760, 17280, 50)]
    [InlineData("wolf pack of 5 every 2 days", 5, 0.7559289460184544, 2880, 5760, 17280, 46080, 50)]
    [InlineData("wolf pack of 5 every 3 days", 5, 0.7559289460184544, 4320, 8640, 43200, -1, 27.729504770880077)]
    [InlineData("lone bear daily", 1, 1.889822365046136, 1440, 5760, 18720, 46080, 50)]
    public void TrailTimelines_MatchOld(string who, double n, double depth, int every, int trace, int path, int trail, double day60)
    {
        _ = who;
        var w = Ground(null);
        TileId a = new(4, 4), b = new(5, 4);
        var first = new Dictionary<TrailTier, int>();
        for (int t = 1; t <= 60 * 1440; t++)
        {
            w.Minute = t;
            if (t % every == 0) Tracks.RecordMove(w, a, b, TrackMaker.Hoof, n, depth);
            if (t % every != 0 && t % 24 != 0) continue; // tiers only change on a crossing or by decay; sample decay often enough to catch it
            first.TryAdd(Tracks.TierOf(w, a, b), t);
        }
        Assert.Equal(trace, first.GetValueOrDefault(TrailTier.Trace, -1));
        Assert.Equal(path, first.GetValueOrDefault(TrailTier.Path, -1));
        Assert.Equal(trail, first.GetValueOrDefault(TrailTier.Trail, -1));
        Assert.Equal(day60, Tracks.WearAt(w, a, b), Digits);
    }

    [Fact]
    public void AbandonedPath_IsGoneInAboutAMonth_AsOld()
    {
        var w = Ground(CalmCold);
        TileId a = new(4, 4), b = new(5, 4);
        for (int i = 0; i < 14; i++) Tracks.RecordMove(w, a, b, TrackMaker.Human);
        w.Minute = 38809;
        Assert.True(Tracks.WearAt(w, a, b) > 0);
        w.Minute = 38810; // day 26.95
        Assert.Equal(0, Tracks.WearAt(w, a, b));
    }

    [Theory]
    [InlineData("blizzard", 5996, 12.794888000003084, TrailTier.Trace)]
    [InlineData("calm", -1, 17.495479999994927, TrailTier.Path)]
    public void FiveDays_OnATwentyCrossingRoute_AsOld(string sky, int losesBonusAt, double endWear, TrailTier endTier)
    {
        var w = Ground(Sky(sky));
        TileId a = new(4, 4), b = new(5, 4);
        for (int i = 0; i < 20; i++) Tracks.RecordMove(w, a, b, TrackMaker.Human);
        int lost = -1;
        for (int t = 1; t <= 5 * 1440 && lost < 0; t++) { w.Minute = t; if (Tracks.TrailMinutes(w, a, b) == 0) lost = t; }
        Assert.Equal(losesBonusAt, lost);
        w.Minute = 5 * 1440;
        Assert.Equal(endWear, Tracks.WearAt(w, a, b), Digits);
        Assert.Equal(endTier, Tracks.TierOf(w, a, b));
    }
}
