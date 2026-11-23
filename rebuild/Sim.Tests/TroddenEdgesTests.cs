namespace Survival.Tests;

/// Trodden edges through the world: crossings pack an edge's snow by each walker's weight, and later crossings of it
/// read that packing in the ground (Ground.Underfoot at an edge's half), so a trail is worth most in deep loose snow.
public class TroddenEdgesTests
{
    // A day of heavy snow at -10 °C lays about 40 cm of loose snow; above freezing nothing lies.
    static readonly WeatherSample HeavySnow = new(0, WeatherCondition.HeavySnow, -10, 0.2, 0.6, 1, WindDirection.North);
    static readonly WeatherSample Thaw = new(0, WeatherCondition.Clear, 8, 0, 0, 0, WindDirection.North);
    static readonly TileId Forest = new(2, 33), Rock = new(2, 34); // neighbours on seed 17's map

    static World World(WeatherSample sky)
    {
        var w = new World { Seed = 17, GeneratedMap = Map.Generate(17), FixedWeather = sky };
        w.Agents[AgentId.Player] = new Agent { Id = AgentId.Player, Tile = Forest };
        return w;
    }

    /// One animal of the species walks the edge back and forth, crossings times, each a completed crossing.
    static void Walk(World w, Species species, int crossings)
    {
        var animal = w.Agent(Agents.Spawn(w, species, Forest));
        for (int i = 0; i < crossings; i++)
        {
            animal.Crossing = new Crossing { From = animal.Tile, Dir = animal.Tile == Forest ? Direction.South : Direction.North, Pace = Pace.Quick, TotalMinutes = 1 };
            Traversal.Advance(w, animal, []);
        }
        Agents.Remove(w, animal.Id);
    }

    /// The minutes the player takes to walk from Forest to Rock, through Sim.Step.
    static int PersonCrosses(World w)
    {
        long start = w.Minute;
        Assert.Equal("arrived", Sim.Step(w, new Travel(AgentId.Player, Direction.South, Pace.Quick), 120).InterruptedReason);
        return (int)(w.Minute - start);
    }

    /// The minutes the crossing an animal begins from Forest to Rock is set to take.
    static int AnimalCrosses(World w, Species species)
    {
        var id = Agents.Spawn(w, species, Forest);
        int minutes = Traversal.Begin(w, new Travel(id, Direction.South, Pace.Quick)).TotalMinutes;
        Agents.Remove(w, id);
        return minutes;
    }

    /// Two worlds alike in all but the edge: in one, caribou have walked it forty times.
    static (World Trodden, World Bare) Twins(WeatherSample sky, long minute)
    {
        var (trodden, bare) = (World(sky), World(sky));
        trodden.Minute = bare.Minute = minute;
        Walk(trodden, Species.Caribou, 40);
        return (trodden, bare);
    }

    [Fact]
    public void InDeepLooseSnow_ATroddenEdge_IsQuicker_ForAPersonAndForAnAnimal()
    {
        var (trodden, bare) = Twins(HeavySnow, 1440);
        Assert.True(Ground.Underfoot(bare, Rock).SnowDepthM > 0.25, $"snow {Ground.Underfoot(bare, Rock).SnowDepthM:0.00} m");
        int wolfBare = AnimalCrosses(bare, Species.Wolf), wolfTrodden = AnimalCrosses(trodden, Species.Wolf);
        int personBare = PersonCrosses(bare), personTrodden = PersonCrosses(trodden);
        Assert.True(personTrodden < personBare, $"person: trodden {personTrodden} vs bare {personBare}");
        Assert.True(wolfTrodden < wolfBare, $"wolf: trodden {wolfTrodden} vs bare {wolfBare}");
        // Worth a lot: a fifth or more of the crossing.
        Assert.True(personTrodden <= personBare * 0.8, $"person: trodden {personTrodden} vs bare {personBare}");
    }

    [Fact]
    public void OnBareGround_TheSameTroddenEdge_SavesLittle()
    {
        var (trodden, bare) = Twins(Thaw, 1440);
        Assert.Equal(0, Ground.Underfoot(bare, Rock).SnowDepthM);
        Assert.Equal(TrailTier.Trail, Tracks.TierOf(trodden, Forest, Rock));
        Assert.InRange(PersonCrosses(bare) - PersonCrosses(trodden), 0, 1);
    }

    [Fact]
    public void AMammothsCrossing_PacksAnEdgeMoreThanAFoxs()
    {
        var (byMammoth, byFox) = (World(Thaw), World(Thaw));
        Walk(byMammoth, Species.Mammoth, 1);
        Walk(byFox, Species.Fox, 1);
        double mammoth = Tracks.TroddenAt(byMammoth, Forest, Rock), fox = Tracks.TroddenAt(byFox, Forest, Rock);
        Assert.True(mammoth > 2 * fox, $"mammoth {mammoth:0.000} vs fox {fox:0.000}");
        Assert.True(fox > 0);
    }

    [Fact]
    public void AFlyer_PacksNothing()
    {
        var w = World(Thaw);
        Walk(w, Species.Raven, 10);
        Assert.Empty(w.Trails);
        Assert.Empty(Tracks.Read(w, Rock));
    }

    [Fact]
    public void ATrailLeftAlone_FadesOverAboutAMonth()
    {
        var w = World(Thaw);
        for (int i = 0; i < 32; i++) Tracks.RecordMove(w, Forest, Rock, TrackMaker.Human);
        Assert.Equal(TrailTier.Trail, Tracks.TierOf(w, Forest, Rock));
        w.Minute = 14 * 1440;
        Assert.NotEqual(TrailTier.None, Tracks.TierOf(w, Forest, Rock));
        w.Minute = 30 * 1440;
        Assert.Equal(0, Tracks.TroddenAt(w, Forest, Rock));
        Tracks.RecordMove(w, new TileId(50, 50), new TileId(50, 51), TrackMaker.Human); // the hourly sweep
        Assert.False(w.Trails.ContainsKey(new EdgeId(Forest, Rock)), "a faded edge leaves the save");
    }

    [Fact]
    public void ATrailBuriedByFreshSnow_ComesBackAfterOneCrossing()
    {
        var w = World(HeavySnow);
        w.Minute = 1440;
        Walk(w, Species.Caribou, 40);
        double packed = Ground.Underfoot(w, Rock, Forest).TroddenLevel;
        double amount = Tracks.TroddenAt(w, Forest, Rock);
        w.Minute = 3 * 1440; // two more days of heavy snow on it
        double buried = Ground.Underfoot(w, Rock, Forest).TroddenLevel;
        Assert.Equal(amount - 2 / 35.0, Tracks.TroddenAt(w, Forest, Rock), 6); // the amount only settles
        Assert.True(buried < packed / 2, $"buried {buried:0.00} vs packed {packed:0.00}");
        Walk(w, Species.Caribou, 1);
        Assert.True(Ground.Underfoot(w, Rock, Forest).TroddenLevel > packed - 0.1, "one crossing packs it again");
    }

    [Fact]
    public void TheTrailLine_IsTheMinutesSavedAgainstTheBareTiles()
    {
        var (trodden, bare) = Twins(HeavySnow, 1440);
        var body = trodden.Agent(AgentId.Player).Body;
        var sky = Weather.At(trodden, trodden.Minute);
        var withTrail = Trip.Terms(trodden, body, Forest, Rock, Pace.Quick, sky);
        var without = Trip.Terms(bare, body, Forest, Rock, Pace.Quick, sky);
        Assert.Equal(0, without.TrailMinutes);
        Assert.True(withTrail.TrailMinutes < 0);
        Assert.Equal(withTrail.TotalMinutes - without.TotalMinutes, withTrail.TrailMinutes);
        Assert.Equal(withTrail.TotalMinutes, withTrail.Terms.Sum(t => t.Minutes));
    }

    [Fact]
    public void TheTroddenOverlay_PacksTheSnow_ButKeepsItsDepth_SoSearchAndWaterReadTheSame()
    {
        var (trodden, bare) = Twins(HeavySnow, 1440);
        Surface tile = Ground.Underfoot(bare, Rock), edge = Ground.Underfoot(trodden, Rock, Forest);
        Assert.True(edge.SnowPackingLevel > tile.SnowPackingLevel);
        Assert.True(edge.TraversalFactor < tile.TraversalFactor);
        Assert.True(edge.HazardDelta > tile.HazardDelta, "packed snow is a little slick");
        Assert.True(edge.HazardDelta <= tile.HazardDelta + 0.05);
        Assert.Equal(tile.SnowDepthM, edge.SnowDepthM);
        Assert.Equal(tile.SearchFactor(0, 0.3), edge.SearchFactor(0, 0.3));
        Assert.Equal(tile.DrinkableWaterM, edge.DrinkableWaterM);
        Assert.Equal(tile, Ground.Underfoot(trodden, Rock)); // the tile itself reads no edge
    }
}
