using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Environments.Surface;

namespace text_survival.Tests.Environments.Surface;

/// <summary>
/// Where the ground meets the rest of the game: travel, footing, wetness, and the tick.
/// </summary>
public class SurfaceIntegrationTests
{
    private static SurfaceWeather HeavySnow => new(15, 0.2, 0, 0.005 / 60, 0);

    private static Location Tile(TerrainType terrain, Weather weather) =>
        new(terrain.ToString(), "", weather,
            terrainHazardLevel: terrain.BaseHazardLevel(),
            windFactor: terrain.BaseWindFactor())
        { Terrain = terrain };

    [Fact]
    public void DeepSnowSlowsACrossingOnceAndOnlyOnce()
    {
        var ctx = GameContext.CreateNewGame(seed: 7);
        var map = ctx.Map!;
        var here = ctx.CurrentLocation;
        var there = map.GetTravelOptions().First();

        int bare = TravelProcessor.GetTraversalMinutes(here, there, ctx.player, ctx.Inventory, map);

        here.Surface.Advance(60 * 24, HeavySnow);
        there.Surface.Advance(60 * 24, HeavySnow);

        int snowy = TravelProcessor.GetTraversalMinutes(here, there, ctx.player, ctx.Inventory, map);

        Assert.True(snowy > bare, $"deep snow should cost time: {bare} -> {snowy}");

        // The excess is the surface factor on the two segments and nothing else. Folded into
        // the hazard that scales segment time as well, this would overshoot badly.
        double expected = TravelProcessor.CalculateSegmentTime(here, ctx.player, ctx.Inventory)
                          * (here.Surface.TraversalFactor - 1)
                        + TravelProcessor.CalculateSegmentTime(there, ctx.player, ctx.Inventory)
                          * (there.Surface.TraversalFactor - 1);

        Assert.InRange(snowy - bare, expected - 1.5, expected + 1.5);
    }

    [Fact]
    public void ATrailBonusDoesNotQuietlyDiscountABlizzard()
    {
        var ctx = GameContext.CreateNewGame(seed: 11);
        var map = ctx.Map!;
        var here = ctx.CurrentLocation;
        var there = map.GetTravelOptions().First();

        here.Surface.Advance(60 * 24, HeavySnow);
        there.Surface.Advance(60 * 24, HeavySnow);

        int untrodden = TravelProcessor.GetTraversalMinutes(here, there, ctx.player, ctx.Inventory, map);

        // Beat the route in properly.
        for (int i = 0; i < 60; i++)
            map.RecordMove(map.GetPosition(here), map.GetPosition(there), TrackMaker.Human);
        Assert.Equal(TrailTier.Trail, map.GetTrailTier(map.GetPosition(here), map.GetPosition(there)));

        int beaten = TravelProcessor.GetTraversalMinutes(here, there, ctx.player, ctx.Inventory, map);

        int saved = untrodden - beaten;
        Assert.InRange(saved, 0, 2);
    }

    [Fact]
    public void SurfaceIceAddsFootingHazard_ButNotOverALakesOwnIce()
    {
        var weather = new Weather(-10, GameContext.StartTime);

        var ground = Tile(TerrainType.Marsh, weather);
        double bare = ground.GetEffectiveTerrainHazard();
        ground.Surface.AddWater(0.08);
        ground.Surface.Advance(60 * 12, new SurfaceWeather(10, 0.1, 0, 0, 0));
        Assert.True(ground.GetEffectiveTerrainHazard() > bare, "verglas should be a hazard");

        var lake = Tile(TerrainType.Water, weather);
        lake.Features.Add(new WaterFeature());
        lake.Surface.AddWater(0.08);
        lake.Surface.Advance(60 * 12, new SurfaceWeather(10, 0.1, 0, 0, 0));

        Assert.Equal(0, lake.GetSurfaceHazardDelta());
    }

    [Fact]
    public void WadingWetsYou_AndBeddingKeepsYouOffIt()
    {
        var ctx = GameContext.CreateNewGame(seed: 21);
        var here = ctx.CurrentLocation;
        here.Surface.AddWater(0.3);

        var walking = SurvivalContext.GetSurvivalContext(
            ctx.player, ctx.Inventory, ActivityType.Traveling, GameContext.TimeOfDay.Noon);
        Assert.True(walking.GroundContactWettingPct > 0.05,
            $"wading should soak you fast, got {walking.GroundContactWettingPct}");
        Assert.Equal(0, walking.GroundContactProtectionLevel);

        foreach (var existing in here.Features.OfType<BeddingFeature>().ToList())
            here.RemoveFeature(existing);

        var lying = SurvivalContext.GetSurvivalContext(
            ctx.player, ctx.Inventory, ActivityType.Sleeping, GameContext.TimeOfDay.Night);
        Assert.Equal(0, lying.GroundContactProtectionLevel);
        Assert.True(lying.GroundContactWettingPct > 0.05, "lying in a puddle soaks you too");

        here.AddFeature(BeddingFeature.CreatePaddedBedding());
        var bedded = SurvivalContext.GetSurvivalContext(
            ctx.player, ctx.Inventory, ActivityType.Sleeping, GameContext.TimeOfDay.Night);
        Assert.True(bedded.GroundContactProtectionLevel > 0.5);
    }

    [Fact]
    public void DryOrFrozenGroundTransfersFarLessThanWetGround()
    {
        var weather = new Weather(-10, GameContext.StartTime);

        var wet = Tile(TerrainType.Marsh, weather);
        wet.Surface.Advance(60 * 6, new SurfaceWeather(45, 0, 0, 0, 10e-3 / 60));

        var frozen = Tile(TerrainType.Marsh, weather);
        frozen.Surface.Advance(60 * 6, new SurfaceWeather(45, 0, 0, 0, 10e-3 / 60));
        frozen.Surface.Advance(60 * 24 * 4, new SurfaceWeather(-5, 0.2, 0, 0, 0));

        Assert.True(
            wet.Surface.GetContactWettingRate(SurfaceContact.Walking)
            > frozen.Surface.GetContactWettingRate(SurfaceContact.Walking) * 3,
            "frozen ground has no liquid to give");
    }

    [Fact]
    public void ARoofDoesNotDryThePuddleYouAreStandingIn()
    {
        var weather = new Weather(-10, GameContext.StartTime);
        var sheltered = new Location("Rock Overhang", "", weather,
            overheadCoverLevel: 1.0)
        { Terrain = TerrainType.Marsh };

        sheltered.Surface.AddWater(0.2);
        sheltered.Surface.Advance(60 * 2, sheltered.GetSurfaceWeather());

        Assert.True(sheltered.Surface.GetContactWettingRate(SurfaceContact.Walking) > 0.05,
            "the roof keeps the rain off you, not the water off the floor");
    }

    [Fact]
    public void EveryTileGetsEveryMinuteExactlyOnce_ForegroundChangesIncluded()
    {
        var ctx = GameContext.CreateNewGame(seed: 33);
        var map = ctx.Map!;
        var weather = ctx.Weather;

        var a = map.CurrentLocation;
        var b = map.GetTravelOptions().First(l => l.HasFeature<ForageFeature>());
        var bystander = map.AllLocations.First(l =>
            l != a && l != b && l.IsPassable && l.HasFeature<ForageFeature>());

        // A depleted forage patch counts elapsed time directly, so it works as a stopwatch.
        var clocks = new[] { a, b, bystander }
            .Select(l => l.GetFeature<ForageFeature>()!)
            .ToList();
        foreach (var clock in clocks) clock.Deplete(1);
        var start = clocks.Select(c => c.HoursSinceLastForage).ToList();

        // Whole batches, so nothing is owed at the end. The foreground switches halfway,
        // which is the case that can silently lose or double a tile's time.
        for (int i = 0; i < 45; i++) map.AdvanceWorld(1, weather, a);
        for (int i = 0; i < 45; i++) map.AdvanceWorld(1, weather, b);

        Assert.Equal(0, map.PendingBackgroundMinutes);
        for (int i = 0; i < clocks.Count; i++)
            Assert.Equal(90 / 60.0, clocks[i].HoursSinceLastForage - start[i], 6);
    }

    [Fact]
    public void ForageRegrowsOnPlainTerrainToo_NotJustOnNamedLocations()
    {
        var ctx = GameContext.CreateNewGame(seed: 44);
        var map = ctx.Map!;

        var terrainTile = map.AllLocations
            .First(l => l.IsTerrainOnly && l.IsPassable && l.HasFeature<ForageFeature>()
                        && l != map.CurrentLocation);

        var forage = terrainTile.GetFeature<ForageFeature>()!;
        forage.Deplete(20);
        double depleted = forage.CurrentDensity;
        Assert.True(forage.IsDepleted());

        // Terrain-only tiles were never ticked before, so a plain foraged flat stayed flat
        // for the rest of the run.
        for (int i = 0; i < 60 * 24 * 14; i++)
            map.AdvanceWorld(1, ctx.Weather, map.CurrentLocation);

        Assert.True(forage.CurrentDensity > depleted,
            $"forage should recover: {depleted:F3} -> {forage.CurrentDensity:F3}");
    }

    [Fact]
    public void FootprintsAgeOnTheWorldClock()
    {
        var ctx = GameContext.CreateNewGame(seed: 55);
        var map = ctx.Map!;

        double before = map.Tracks.Erosion;
        for (int i = 0; i < 600; i++)
            map.AdvanceWorld(1, ctx.Weather, map.CurrentLocation);

        Assert.True(map.Tracks.Erosion > before,
            "the ground's clock has to actually run; for a while nothing called it at all");
    }
}
