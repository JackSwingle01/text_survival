namespace Survival.Tests;

/// Sight rays (port of the old Perception.Sight): a budget spent crossing each tile, absorbed at
/// max(1, 1/visibility²) per tile-length; 11 tiles over open ground, up to 19 from a vantage; cut
/// by the weather and by the eyes; mountains block what lies behind them but are seen themselves.
public class SightTests
{
    static readonly WeatherSample Clear = new(0, WeatherCondition.Clear, -5, 0.1, 0, 0.1, WindDirection.North);

    /// A plain of this size, the camp and the pass's exit tucked in the top corners out of every ray.
    static World Plain(int width = 40, int height = 31, Action<Terrain[,], Cave[,], int?[,]>? shape = null, WeatherSample? sky = null)
    {
        var terrain = new Terrain[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++) terrain[x, y] = Terrain.Plain;
        var caves = new Cave[width, height];
        var ids = new int?[width, height];
        shape?.Invoke(terrain, caves, ids);
        var map = new GeneratedMap(width, height, terrain, caves, ids, [], [], new(0, 0), new() { [new(width - 1, 0)] = 5 });
        return new World { Seed = 1, GeneratedMap = map, FixedWeather = sky ?? Clear };
    }

    static readonly TileId Centre = new(15, 15);

    static bool Sees(World w, TileId from, int dx, int dy, double sight = 1) => Sight.Visible(w, from, sight).Contains(new TileId(from.X + dx, from.Y + dy));

    [Fact]
    public void OverOpenGround_SightReachesElevenTiles()
    {
        var w = Plain();
        Assert.True(Sees(w, Centre, 11, 0));
        Assert.False(Sees(w, Centre, 12, 0));
        Assert.True(Sees(w, Centre, 7, 8));   // 10.6 tiles off
        Assert.False(Sees(w, Centre, 8, 8));  // 11.3
        Assert.True(Sees(w, Centre, 0, 0));
    }

    /// Forest (visibility 0.5) costs four tiles of budget for every tile of it crossed.
    [Fact]
    public void Forest_AbsorbsFourTimesTheBudget_AndIsSeenItself()
    {
        var w = Plain(shape: (t, _, _) => { for (int x = 16; x < 40; x++) t[x, 15] = Terrain.Forest; });
        Assert.True(Sees(w, Centre, 1, 0));
        Assert.True(Sees(w, Centre, 3, 0));  // half a plain tile and two of forest: 8.5 of 11
        Assert.False(Sees(w, Centre, 4, 0)); // a third forest tile: 12.5
    }

    [Fact]
    public void AMountain_IsSeen_ButHidesWhatIsBehindIt()
    {
        var w = Plain(shape: (t, _, _) => t[17, 15] = Terrain.Mountain);
        Assert.True(Sees(w, Centre, 2, 0));
        Assert.False(Sees(w, Centre, 3, 0));
        Assert.True(Sees(w, Centre, 3, 1)); // off the line behind it
    }

    /// A diagonal does not slip through the seam between two opaque tiles.
    [Fact]
    public void ADiagonal_CannotPeekBetweenTwoMountains()
    {
        var w = Plain(shape: (t, _, _) => { t[16, 15] = Terrain.Mountain; t[15, 16] = Terrain.Mountain; });
        Assert.False(Sees(w, Centre, 2, 2));
        Assert.True(Sees(w, Centre, -2, -2));
    }

    /// Hills (visibility 1.5) give a vantage: (1.5 - 1.3) x 20 = 4 more tiles; an overlook up to 8 more.
    [Fact]
    public void FromTheHills_SightReachesFarther()
    {
        var w = Plain(shape: (t, _, _) => t[15, 15] = Terrain.Hills);
        Assert.Equal(15, Sight.RangeTiles(Map.Of(w), Centre));
        Assert.True(Sees(w, Centre, 15, 0));
        Assert.False(Sees(w, Centre, 16, 0));
        Assert.Equal(19, Sight.VantageRangeTiles(1.8)); // capped at 8 more
        Assert.Equal(11, Sight.VantageRangeTiles(1.2));
    }

    [Fact]
    public void Weather_AndBadEyes_CutTheBudget()
    {
        var blizzard = Plain(sky: Clear with { Rolled = WeatherCondition.Blizzard, TemperatureC = -15 });
        Assert.Equal(0.3, Weather.At(blizzard, 0).VisibilityFactor);
        Assert.True(Sees(blizzard, Centre, 3, 0));  // 11 x 0.3 = 3.3
        Assert.False(Sees(blizzard, Centre, 4, 0));

        var w = Plain();
        Assert.True(Sees(w, Centre, 6, 0, sight: 0.5));   // round(5.5) = 6
        Assert.False(Sees(w, Centre, 7, 0, sight: 0.5));
        Assert.Equal([Centre], Sight.Visible(w, Centre, sightCapacity: 0));
    }

    /// From outside, a cave's floor is hidden; inside, sight has six tiles of budget along its own floor.
    [Fact]
    public void ACaveFloor_IsHiddenFromOutside_AndSightInsideIsShort()
    {
        var w = Plain(shape: (t, c, ids) =>
        {
            for (int x = 16; x <= 25; x++) { t[x, 15] = Terrain.Rock; c[x, 15] = x == 16 ? Cave.Entrance : Cave.Floor; ids[x, 15] = 3; }
        });
        Assert.True(Sees(w, Centre, 1, 0));  // the mouth
        Assert.False(Sees(w, Centre, 2, 0)); // the floor beyond it
        var inside = new TileId(18, 15);
        Assert.Equal(6, Sight.RangeTiles(Map.Of(w), inside));
        // A cave floor's visibility is 0.7: about two tiles of budget a tile, so six reach three tiles on.
        Assert.True(Sees(w, inside, 3, 0));
        Assert.False(Sees(w, inside, 4, 0));
        Assert.False(Sees(w, inside, 0, 1)); // out through the rock
        Assert.True(Sees(w, new TileId(16, 15), 1, 0)); // from its mouth, in
    }

    [Fact]
    public void Visible_IsInAFixedOrder_WithNoRepeats()
    {
        var w = Plain();
        var seen = Sight.Visible(w, Centre);
        Assert.Equal(seen, Sight.Visible(w, Centre));
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(seen.OrderBy(t => t.X).ThenBy(t => t.Y), seen);
    }
}
