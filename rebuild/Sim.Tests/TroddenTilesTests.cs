namespace Survival.Tests;

/// Trodden tiles through the world: time spent on a tile, not passing through it, packs its snow (Tracks.RecordActivity)
/// and prints it; the tile's ground within it reads the packing (Ground.Within), and the worked ground shows.
public class TroddenTilesTests
{
    // A day of heavy snow at -10 °C lays about 40 cm of loose snow.
    static readonly WeatherSample HeavySnow = new(0, WeatherCondition.HeavySnow, -10, 0.2, 0.6, 1, WindDirection.North);
    static readonly WeatherSample CalmCold = new(0, WeatherCondition.Clear, -15, 0, 0, 0, WindDirection.North);
    static readonly TileId Forest = new(2, 33), Rock = new(2, 34); // neighbours on seed 17's map

    static World World(WeatherSample sky)
    {
        var w = new World { Seed = 17, GeneratedMap = TestWorlds.Map(17), FixedWeather = sky };
        w.Agents[AgentId.Player] = new Agent { Id = AgentId.Player, Tile = new TileId(2, 30) };
        return w;
    }

    /// Each animal is set to do the activity for the minutes given: its mind takes over when it ends or is cut short.
    static void SetDoing(World w, Animals herd, Func<AgentId, Activity> activity, int minutes)
    {
        foreach (var a in herd.In(w)) a.Doing = new Doing { Plain = activity(a.Id), MinutesLeft = minutes };
    }

    static void Start(World w, Animals herd, Func<AgentId, Activity> activity, int minutes)
    {
        foreach (var a in herd.In(w))
        {
            Assert.Null(Sim.Start(w, activity(a.Id), minutes, [], out var doing));
            a.Doing = doing;
        }
    }

    [Fact]
    public void AHerdBeddedAllDay_TreadsItsTile_ACrossingAddsNone()
    {
        var w = World(CalmCold);
        w.Minute = 1; // just after a lot: the herd leaves before the next one
        var crossers = Animals.Spawn(w, Species.Caribou, Forest, 10);
        Start(w, crossers, id => new Travel(id, Direction.South, Pace.Quick), 120);
        Sim.Advance(w, 14);
        Assert.All(crossers.In(w), a => Assert.Equal(Rock, a.Tile));
        Assert.NotEmpty(Tracks.Read(w, Forest)); // the crossing printed the tile it left
        Assert.Equal(0, Tracks.TroddenAt(w, Forest));
        foreach (var id in crossers.Ids) Agents.Remove(w, id);

        var bedded = Animals.Spawn(w, Species.Caribou, Forest, 10);
        SetDoing(w, bedded, id => new Rest(id), 1440);
        Sim.Advance(w, 1440);
        double trodden = Tracks.TroddenAt(w, Forest);
        Assert.True(trodden >= Tracks.TroddenTileAt, $"bedded all day: {trodden:0.00}");
        Assert.True(trodden < 1);
    }

    [Fact]
    public void AGrazedTile_ShowsMorePrints_ThanOnePassedThrough()
    {
        var w = World(CalmCold);
        w.Minute = 1;
        var crossers = Animals.Spawn(w, Species.Caribou, Forest, 10);
        Start(w, crossers, id => new Travel(id, Direction.South, Pace.Quick), 120);
        Sim.Advance(w, 14);
        foreach (var id in crossers.Ids) Agents.Remove(w, id);
        int passedThrough = Tracks.Read(w, Forest).Single(s => s.Maker == TrackMaker.Hoof).Traffic;

        var grazers = Animals.Spawn(w, Species.Caribou, Rock, 10);
        SetDoing(w, grazers, id => new Graze(id), 8 * 60);
        Sim.Advance(w, 8 * 60);
        foreach (var id in grazers.Ids) Agents.Remove(w, id);
        var grazed = Tracks.Read(w, Rock).Single(s => s.Maker == TrackMaker.Hoof);
        var crossed = Tracks.Read(w, Forest).Single(s => s.Maker == TrackMaker.Hoof);
        Assert.True(grazed.Traffic > crossed.Traffic, $"grazed {grazed.Traffic} vs passed through {crossed.Traffic} (was {passedThrough})");
    }

    [Fact]
    public void APersonRestingHalfADay_TreadsTheirTile_Through_SimStep()
    {
        var w = World(CalmCold);
        var start = w.Agent(AgentId.Player).Tile;
        Assert.Equal(720, Sim.Step(w, new Rest(AgentId.Player), 720).MinutesElapsed);
        double trodden = Tracks.TroddenAt(w, start);
        // One person on a 100 m tile treads it about as fast as a yard settles: a trace, never trodden ground.
        Assert.InRange(trodden, 0.01, Tracks.TroddenTileAt / 4);
        Assert.Contains(Tracks.Read(w, start), s => s.Maker == TrackMaker.Human && s.Pattern == TrafficPattern.Mixed);
    }

    [Fact]
    public void Advance_AndStep_TreadAlike()
    {
        var (stepped, advanced) = (World(CalmCold), World(CalmCold));
        var tile = stepped.Agent(AgentId.Player).Tile;
        Sim.Step(stepped, new Wait(AgentId.Player), 120);
        Sim.Advance(advanced, 120);
        Assert.Equal(stepped.Tiles[tile].Trodden, advanced.Tiles[tile].Trodden);
        Assert.True(advanced.Tiles[tile].Trodden!.Amount > 0);
    }

    [Fact]
    public void AYard_SettlesSoonerThanATrail()
    {
        var w = World(CalmCold);
        var herd = Animals.Spawn(w, Species.Caribou, Forest, 20);
        SetDoing(w, herd, id => new Rest(id), 2 * 1440);
        Sim.Advance(w, 2 * 1440);
        foreach (var id in herd.Ids) Agents.Remove(w, id);
        Assert.True(Tracks.TroddenAt(w, Forest) > 0.6, $"{Tracks.TroddenAt(w, Forest):0.00}");
        w.Minute += 7 * 1440;
        Assert.True(Tracks.TroddenAt(w, Forest) > 0);
        w.Minute += 7 * 1440;
        Assert.Equal(0, Tracks.TroddenAt(w, Forest)); // a fortnight: the yard is gone, where a trail lasts five weeks
    }

    [Fact]
    public void ARavenFlyingOver_LeavesNothing_OneFeeding_LeavesBirdPrintsThatFadeWithinHours()
    {
        var w = KillChainTests.Bare(-15);
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        var c = KillChainTests.Caribou(w, tile);
        var start = new TileId(tile.X, tile.Y + 5);
        var raven = Animals.Spawn(w, Species.Raven, start, 1);
        TestKit.SetHunger(w.Agent(raven), 1);
        for (int i = 0; i < 6 * 60 && w.Agent(raven).Tile != tile; i++) Sim.Advance(w, 1);
        Assert.Equal(tile, w.Agent(raven).Tile);
        // Over every tile it flew: no prints, nothing trodden.
        for (int y = start.Y - 1; y > tile.Y; y--)
        {
            Assert.Empty(Tracks.Read(w, new TileId(tile.X, y)));
            Assert.Equal(0, Tracks.TroddenAt(w, new TileId(tile.X, y)));
        }
        double meat = Carcasses.MeatKg(c);
        Sim.Advance(w, 60);
        Assert.True(Carcasses.MeatKg(c) < meat, "the raven ate nothing");
        Assert.Contains(Tracks.Read(w, tile), s => s.Maker == TrackMaker.Bird);
        Assert.Empty(w.Trails);

        Agents.Remove(w, raven);
        Sim.Advance(w, 18 * 60);
        Assert.DoesNotContain(Tracks.Read(w, tile), s => s.Maker == TrackMaker.Bird);
    }

    // ---- the ground's interface ----

    [Fact]
    public void WithinATroddenTile_FootingChanges_SearchAndWaterDoNot()
    {
        var w = World(HeavySnow);
        w.Minute = 1440;
        w.Tiles[Forest] = new TileState { Trodden = new TileTrodden(0.8, w.Minute) };
        var bare = Ground.Underfoot(w, Forest);
        var within = Ground.Within(w, Forest);
        Assert.True(bare.SnowDepthM > 0.15, $"snow {bare.SnowDepthM:0.00} m");
        Assert.True(within.HazardDelta > bare.HazardDelta, $"footing: {within.HazardDelta} vs {bare.HazardDelta}");
        Assert.True(within.SnowPackingLevel > bare.SnowPackingLevel);
        Assert.Equal(bare.SnowDepthM, within.SnowDepthM);
        Assert.Equal(bare.SearchFactor(0, 0.3), within.SearchFactor(0, 0.3));
        Assert.Equal(bare.DrinkableWaterM, within.DrinkableWaterM);
        // A crossing reads the tile, not the time spent on it.
        Assert.Equal(bare, Ground.Underfoot(w, Forest, null));
        Assert.Equal(Ground.Underfoot(w, Rock), Ground.Within(w, Rock)); // untrodden: the tile itself
    }

    [Fact]
    public void SnowFallenSinceTheLastStamp_BuriesATroddenTile_WithoutLoweringItsAmount()
    {
        var w = World(HeavySnow);
        w.Minute = 1440;
        w.Tiles[Forest] = new TileState { Trodden = new TileTrodden(0.8, w.Minute) };
        double fresh = Ground.Within(w, Forest).TroddenLevel;
        w.Minute += 1440;
        Assert.True(Ground.Within(w, Forest).TroddenLevel < fresh * 0.6, $"{Ground.Within(w, Forest).TroddenLevel:0.00} vs {fresh:0.00}");
        Assert.True(Tracks.TroddenAt(w, Forest) > 0.7);
    }
}
