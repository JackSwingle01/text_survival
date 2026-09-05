using text_survival.Environments;
using text_survival.Environments.Factories;
using text_survival.Environments.Grid;
using text_survival.Actors.Animals;

namespace text_survival.Tests.Environments.Grid;

/// <summary>
/// Footprints and the desire paths that grow out of them. Both are driven by the same
/// movement seam and the same erosion, on very different clocks.
/// </summary>
public class TrackRegistryTests
{
    private static Weather CalmCold() => new()
    {
        PrecipitationPct = 0,
        WindSpeedPct = 0,
        BaseTemperature = -10  // Well below the thaw threshold.
    };

    private static Weather Blizzard() => new()
    {
        PrecipitationPct = 0.9,
        WindSpeedPct = 0.8,
        BaseTemperature = -15
    };

    [Fact]
    public void Stamp_MarksBothTilesWithHeading()
    {
        var tracks = new TrackRegistry();
        var from = new GridPosition(4, 5);
        var to = new GridPosition(4, 4);  // North

        tracks.Stamp(from, to, TrackMaker.Human);

        Assert.Single(tracks.At(from));
        Assert.Single(tracks.At(to));
        Assert.Equal(Direction.North, tracks.At(from)[0].Track.Heading);
        Assert.Equal(Direction.North, tracks.At(to)[0].Track.Heading);
    }

    [Fact]
    public void Stamp_SameTileTwice_RefreshesRatherThanAccumulates()
    {
        var tracks = new TrackRegistry();
        var a = new GridPosition(1, 1);
        var b = new GridPosition(2, 1);

        tracks.Stamp(a, b, TrackMaker.Paw);
        tracks.Advance(600, CalmCold());
        double afterWait = tracks.At(a)[0].Freshness;

        tracks.Stamp(a, b, TrackMaker.Paw);

        Assert.Single(tracks.At(a));
        Assert.Equal(1.0, tracks.At(a)[0].Freshness, 3);
        Assert.True(afterWait < 1.0, "the first pass should have aged before the second");
    }

    [Fact]
    public void Stamp_SamePositionForDifferentMakers_KeepsBoth()
    {
        var tracks = new TrackRegistry();
        var a = new GridPosition(3, 3);
        var b = new GridPosition(3, 4);

        tracks.Stamp(a, b, TrackMaker.Paw);
        tracks.Stamp(a, b, TrackMaker.Hoof);

        Assert.Equal(2, tracks.At(a).Count);
    }

    [Fact]
    public void Stamp_NoMove_RecordsNothing()
    {
        var tracks = new TrackRegistry();
        var a = new GridPosition(2, 2);

        tracks.Stamp(a, a, TrackMaker.Human);

        Assert.Empty(tracks.At(a));
    }

    [Fact]
    public void Freshness_FadesFasterInBadWeather()
    {
        var calm = new TrackRegistry();
        var storm = new TrackRegistry();
        var a = new GridPosition(0, 1);
        var b = new GridPosition(0, 0);

        calm.Stamp(a, b, TrackMaker.Hoof);
        storm.Stamp(a, b, TrackMaker.Hoof);

        calm.Advance(240, CalmCold());
        storm.Advance(240, Blizzard());

        Assert.True(storm.At(a)[0].Freshness < calm.At(a)[0].Freshness,
            "a blizzard should bury prints far faster than still cold air");
    }

    [Fact]
    public void Freshness_WeatherHistoryCountsWithoutBeingStored()
    {
        // A storm that blew through while the player was elsewhere still has to have
        // erased the prints - that is the whole point of a shared accumulator.
        var tracks = new TrackRegistry();
        var a = new GridPosition(5, 5);
        var b = new GridPosition(5, 6);

        tracks.Stamp(a, b, TrackMaker.Human);
        tracks.Advance(60, CalmCold());
        Assert.True(tracks.At(a)[0].Freshness > 0.9);

        tracks.Advance(600, Blizzard());
        Assert.Empty(tracks.At(a));
    }

    [Fact]
    public void Freshness_DeeperTracksOutlastLighterOnes()
    {
        var tracks = new TrackRegistry();
        var light = new GridPosition(1, 0);
        var heavy = new GridPosition(3, 0);

        tracks.Stamp(light, new GridPosition(1, 1), TrackMaker.Paw, individualDepth: 0.5);
        tracks.Stamp(heavy, new GridPosition(3, 1), TrackMaker.Hoof, individualDepth: 2.5);

        tracks.Advance(600, CalmCold());

        Assert.True(tracks.At(heavy)[0].Freshness > tracks.At(light)[0].Freshness);
    }

    [Fact]
    public void At_DeadTracksAreNotReadable()
    {
        var tracks = new TrackRegistry();
        var a = new GridPosition(7, 7);

        tracks.Stamp(a, new GridPosition(7, 8), TrackMaker.Human);
        tracks.Advance(100_000, CalmCold());

        Assert.Empty(tracks.At(a));
        Assert.Equal(0, tracks.FreshnessOf(a, TrackMaker.Human));
    }

    [Fact]
    public void Marks_RoundTripsThroughSerializationForm()
    {
        var tracks = new TrackRegistry();
        tracks.Stamp(new GridPosition(2, 3), new GridPosition(3, 3), TrackMaker.Paw, individualDepth: 1.7);
        tracks.Advance(120, CalmCold());

        var restored = new TrackRegistry { Erosion = tracks.Erosion, Marks = tracks.Marks };

        var original = tracks.At(new GridPosition(2, 3))[0];
        var copy = restored.At(new GridPosition(2, 3))[0];

        Assert.Equal(original.Track.Maker, copy.Track.Maker);
        Assert.Equal(original.Track.Heading, copy.Track.Heading);
        Assert.Equal(original.Track.Traffic, copy.Track.Traffic);
        Assert.Equal(original.Track.Depth, copy.Track.Depth);
        Assert.Equal(original.Freshness, copy.Freshness, 6);
    }
}


/// <summary>
/// How much came through, not just that something did. Traffic accumulates across
/// passages and fades in between, so it answers "one animal or a dozen?".
/// </summary>
public class TrackTrafficTests
{
    private static Weather CalmCold() => new()
    {
        PrecipitationPct = 0,
        WindSpeedPct = 0,
        BaseTemperature = -10
    };

    private static readonly GridPosition A = new(2, 2);
    private static readonly GridPosition B = new(3, 2);

    [Fact]
    public void TrafficOf_CountsAWholeHerdFromOnePassage()
    {
        var tracks = new TrackRegistry();
        tracks.Stamp(A, B, TrackMaker.Hoof, individuals: 12, individualDepth: 1.3);

        Assert.Equal(12, tracks.TrafficOf(A, TrackMaker.Hoof));
    }

    [Fact]
    public void TrafficOf_AccumulatesAcrossPassages()
    {
        var tracks = new TrackRegistry();

        tracks.Stamp(A, B, TrackMaker.Human);
        Assert.Equal(1, tracks.TrafficOf(A, TrackMaker.Human));

        tracks.Stamp(A, B, TrackMaker.Human);
        tracks.Stamp(A, B, TrackMaker.Human);
        Assert.Equal(3, tracks.TrafficOf(A, TrackMaker.Human));
    }

    [Fact]
    public void TrafficOf_OldTrafficFadesBeforeNewIsAdded()
    {
        var tracks = new TrackRegistry();

        for (int i = 0; i < 10; i++) tracks.Stamp(A, B, TrackMaker.Human);
        Assert.Equal(10, tracks.TrafficOf(A, TrackMaker.Human));

        // Leave it long enough for most of the sign to go, then walk through once more.
        tracks.Advance(4000, CalmCold());
        tracks.Stamp(A, B, TrackMaker.Human);

        int after = tracks.TrafficOf(A, TrackMaker.Human);
        Assert.True(after < 10, $"faded traffic should not carry forward whole, got {after}");
        Assert.True(after >= 1, "the fresh passage should still count");
    }

    [Fact]
    public void TrafficOf_FadesAsTheSignAges()
    {
        var tracks = new TrackRegistry();
        tracks.Stamp(A, B, TrackMaker.Hoof, individuals: 12, individualDepth: 1.3);

        int fresh = tracks.TrafficOf(A, TrackMaker.Hoof);
        tracks.Advance(3000, CalmCold());
        int later = tracks.TrafficOf(A, TrackMaker.Hoof);

        Assert.Equal(12, fresh);
        Assert.True(later < fresh, "a week-old herd trail should read as quieter than a fresh one");
    }

    [Fact]
    public void Depth_HeavyTrafficOutlastsALoneWalker()
    {
        var tracks = new TrackRegistry();
        var lone = new GridPosition(6, 6);
        var herd = new GridPosition(8, 6);

        tracks.Stamp(lone, new GridPosition(6, 7), TrackMaker.Hoof, individuals: 1, individualDepth: 1.3);
        tracks.Stamp(herd, new GridPosition(8, 7), TrackMaker.Hoof, individuals: 12, individualDepth: 1.3);

        tracks.Advance(3000, CalmCold());

        Assert.True(tracks.At(herd)[0].Freshness > tracks.At(lone)[0].Freshness,
            "ground churned by a herd should stay readable longer");
    }
}

/// <summary>
/// The map-level seam: one completed move feeds both systems, and a beaten path
/// actually reaches travel time.
/// </summary>
public class GroundMemoryIntegrationTests
{
    private static GameMap CreateTestMap(int size = 10)
    {
        var map = new GameMap(size, size);
        var weather = new Weather();

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.SetLocation(x, y, LocationFactory.MakeTerrainLocation(TerrainType.Plain, weather));

        return map;
    }

    private static readonly GridPosition A = new(3, 3);
    private static readonly GridPosition B = new(4, 3);

    [Fact]
    public void RecordMove_FeedsBothTracksAndTrailWear()
    {
        var map = CreateTestMap();

        map.RecordMove(A, B, TrackMaker.Human);

        Assert.Single(map.Tracks.At(A));
        Assert.Single(map.Tracks.At(B));
        Assert.True(map.Tracks.FreshnessOf(B, TrackMaker.Human) > 0);
        Assert.Equal(TrailTier.None, map.GetTrailTier(A, B));  // one crossing is not a path
    }

    [Fact]
    public void RecordMove_WearIsSharedByBothDirectionsOfTravel()
    {
        var map = CreateTestMap();

        // Walking there and back is two crossings of one route, not one each of two.
        for (int i = 0; i < 3; i++)
        {
            map.RecordMove(A, B, TrackMaker.Human);
            map.RecordMove(B, A, TrackMaker.Human);
        }

        Assert.Equal(TrailTier.Trace, map.GetTrailTier(A, B));
        Assert.Equal(map.GetTrailTier(A, B), map.GetTrailTier(B, A));
    }

    [Fact]
    public void RecordMove_NonAdjacentHopWearsNoEdge()
    {
        var map = CreateTestMap();
        var far = new GridPosition(7, 3);  // a herd hopping across its territory

        for (int i = 0; i < 100; i++) map.RecordMove(A, far, TrackMaker.Hoof);

        // Both ends are marked, but no single edge between them was walked.
        Assert.Single(map.Tracks.At(A));
        Assert.Single(map.Tracks.At(far));
        Assert.Equal(TrailTier.None, map.GetTrailTier(A, B));
        Assert.Equal(0, map.GetEdgeTraversalModifier(A, B));
    }

    [Fact]
    public void RecordMove_AHerdBeatsInARouteALoneAnimalNeverWould()
    {
        var byHerd = CreateTestMap();
        var byLoner = CreateTestMap();

        // Caribou: ~120kg, so a little over one human's worth of ground pressure each.
        double caribou = AnimalType.Caribou.IndividualTrackDepth();

        byHerd.RecordMove(A, B, TrackMaker.Hoof, individuals: 12, individualDepth: caribou);
        byLoner.RecordMove(A, B, TrackMaker.Hoof, individuals: 1, individualDepth: caribou);

        // One passage of a dozen animals is already a path; one animal is nothing.
        Assert.Equal(TrailTier.Path, byHerd.GetTrailTier(A, B));
        Assert.Equal(TrailTier.None, byLoner.GetTrailTier(A, B));
    }

    [Fact]
    public void RecordMove_WearIsLinearInHeadCount()
    {
        var together = CreateTestMap();
        var separately = CreateTestMap();

        together.RecordMove(A, B, TrackMaker.Paw, individuals: 5, individualDepth: 0.8);
        for (int i = 0; i < 5; i++)
            separately.RecordMove(A, B, TrackMaker.Paw, individuals: 1, individualDepth: 0.8);

        Assert.Equal(together.GetTrailTier(A, B), separately.GetTrailTier(A, B));
    }

    [Fact]
    public void GetEdgeTraversalModifier_IncludesTheBeatenPath()
    {
        var map = CreateTestMap();

        Assert.Equal(0, map.GetEdgeTraversalModifier(A, B));

        for (int i = 0; i < 14; i++) map.RecordMove(A, B, TrackMaker.Human);
        Assert.Equal(-1, map.GetEdgeTraversalModifier(A, B));

        for (int i = 0; i < 21; i++) map.RecordMove(A, B, TrackMaker.Human);
        Assert.Equal(-2, map.GetEdgeTraversalModifier(A, B));
    }

    [Fact]
    public void GetEdgeTraversalModifier_StacksWithAuthoredEdges()
    {
        var map = CreateTestMap();

        map.AddEdge(A, B, new TileEdge(EdgeType.River));   // +4 to ford
        for (int i = 0; i < 50; i++) map.RecordMove(A, B, TrackMaker.Human);

        // A well-worn crossing is still a river crossing, just a better-known one.
        Assert.Equal(2, map.GetEdgeTraversalModifier(A, B));
    }

    [Fact]
    public void MoveTo_LeavesThePlayersOwnTracks()
    {
        var map = CreateTestMap();
        map.CurrentPosition = A;

        map.MoveTo(map.GetLocationAt(B)!);

        Assert.True(map.Tracks.FreshnessOf(A, TrackMaker.Human) > 0);
        Assert.True(map.Tracks.FreshnessOf(B, TrackMaker.Human) > 0);
    }

    [Fact]
    public void AdvanceGround_AgesFootprintsFasterThanTrails()
    {
        var map = CreateTestMap();
        var weather = new Weather { PrecipitationPct = 0.5, WindSpeedPct = 0.5, BaseTemperature = -10 };

        for (int i = 0; i < 20; i++) map.RecordMove(A, B, TrackMaker.Human);
        Assert.Equal(TrailTier.Path, map.GetTrailTier(A, B));

        map.AdvanceGround(60 * 24 * 3, weather);   // three days of steady snow

        Assert.Empty(map.Tracks.At(A));                       // prints long gone
        Assert.Equal(TrailTier.Path, map.GetTrailTier(A, B)); // the route remains
    }
}

public class TrailWearTests
{
    private static readonly (GridPosition, Direction) Edge = (new GridPosition(4, 4), Direction.East);

    /// <summary>Erosion units for a stretch of dead calm, cold weather.</summary>
    private static double CalmErosion(int minutes) => 0.5 * minutes;

    /// <summary>Erosion units for an unbroken blizzard.</summary>
    private static double BlizzardErosion(int minutes) => 8.3 * minutes;

    [Fact]
    public void TierFor_DerivesFromWearAlone()
    {
        Assert.Equal(TrailTier.None, TrailWear.TierFor(0));
        Assert.Equal(TrailTier.None, TrailWear.TierFor(TrailWear.TraceAt - 1));
        Assert.Equal(TrailTier.Trace, TrailWear.TierFor(TrailWear.TraceAt));
        Assert.Equal(TrailTier.Path, TrailWear.TierFor(TrailWear.PathAt));
        Assert.Equal(TrailTier.Trail, TrailWear.TierFor(TrailWear.TrailAt));
    }

    [Fact]
    public void Add_RepeatedCrossingsBeatARouteIn()
    {
        var trails = new TrailWear();

        for (int i = 0; i < 4; i++) trails.Add(Edge, 1.0);
        Assert.Equal(TrailTier.None, trails.TierAt(Edge));

        trails.Add(Edge, 1.0);
        Assert.Equal(TrailTier.Trace, trails.TierAt(Edge));

        for (int i = 0; i < 30; i++) trails.Add(Edge, 1.0);
        Assert.Equal(TrailTier.Trail, trails.TierAt(Edge));
    }

    [Fact]
    public void Add_HeavierMoversWearGroundFaster()
    {
        var light = new TrailWear();
        var heavy = new TrailWear();

        for (int i = 0; i < 5; i++)
        {
            light.Add(Edge, 0.76);   // a wolf
            heavy.Add(Edge, 3.0);    // a mammoth
        }

        Assert.True(heavy.WearAt(Edge) > light.WearAt(Edge));
        Assert.Equal(TrailTier.Path, heavy.TierAt(Edge));   // 5 x 3.0 = 15
        Assert.Equal(TrailTier.None, light.TierAt(Edge));   // 5 x 0.76 = 3.8
    }

    [Fact]
    public void Add_WearIsCapped()
    {
        var trails = new TrailWear();
        for (int i = 0; i < 500; i++) trails.Add(Edge, 3.0);

        Assert.Equal(TrailWear.MaxWear, trails.WearAt(Edge));
    }

    [Fact]
    public void TraversalModifier_OnlyRewardsRealPaths()
    {
        var trails = new TrailWear();

        for (int i = 0; i < 6; i++) trails.Add(Edge, 1.0);    // a trace
        Assert.Equal(0, trails.TraversalModifierMinutes(Edge));

        for (int i = 0; i < 8; i++) trails.Add(Edge, 1.0);    // a path
        Assert.Equal(-1, trails.TraversalModifierMinutes(Edge));

        for (int i = 0; i < 21; i++) trails.Add(Edge, 1.0);   // a trail
        Assert.Equal(-2, trails.TraversalModifierMinutes(Edge));
    }

    [Fact]
    public void Decay_ReclaimsAbandonedRoutes()
    {
        var trails = new TrailWear();
        for (int i = 0; i < 14; i++) trails.Add(Edge, 1.0);
        Assert.Equal(TrailTier.Path, trails.TierAt(Edge));

        // Five weeks of still, cold weather, walked by nobody.
        int minutes = 60 * 24 * 35;
        trails.Decay(minutes, CalmErosion(minutes));

        Assert.Equal(TrailTier.None, trails.TierAt(Edge));
        Assert.Equal(0, trails.WearAt(Edge));
    }

    [Fact]
    public void Decay_BadWeatherBuriesAPathWithoutUndoingIt()
    {
        var calm = new TrailWear();
        var storm = new TrailWear();
        for (int i = 0; i < 20; i++) { calm.Add(Edge, 1.0); storm.Add(Edge, 1.0); }

        int minutes = 60 * 24 * 5;  // five days
        calm.Decay(minutes, CalmErosion(minutes));
        storm.Decay(minutes, BlizzardErosion(minutes));

        Assert.True(storm.WearAt(Edge) < calm.WearAt(Edge), "snow should bury a path faster");
        Assert.Equal(TrailTier.Path, calm.TierAt(Edge));

        // Five unbroken days of blizzard costs the route its speed bonus but not its
        // existence - the ground underneath is still trodden, and walking it again
        // brings it back far faster than making it took.
        Assert.Equal(TrailTier.Trace, storm.TierAt(Edge));
    }

    [Fact]
    public void Decay_TrailsOutliveFootprintsByAWideMargin()
    {
        var trails = new TrailWear();
        for (int i = 0; i < 60; i++) trails.Add(Edge, 1.0);   // a well-established trail
        Assert.Equal(TrailTier.Trail, trails.TierAt(Edge));

        // Long enough to wipe out even the deepest footprint the game can leave.
        double erosionUnits = TrackRegistry.BaseLifespanUnits * 3.0;
        trails.Decay((int)(erosionUnits / 0.5), erosionUnits);

        Assert.Equal(TrailTier.Trail, trails.TierAt(Edge));
    }

    [Fact]
    public void Wear_RoundTripsThroughSerializationForm()
    {
        var trails = new TrailWear();
        for (int i = 0; i < 20; i++) trails.Add(Edge, 1.0);

        var restored = new TrailWear { Wear = trails.Wear };

        Assert.Equal(trails.WearAt(Edge), restored.WearAt(Edge));
        Assert.Equal(TrailTier.Path, restored.TierAt(Edge));
    }
}
