using Survival;
using Xunit;

public class ReportTests
{
    /// A populated world a couple of days on, with one of each thing the player can leave behind.
    static World Lived(int seed = 17)
    {
        var w = TestKit.Supply(World.New(seed));
        var camp = Map.Of(w).Camp;
        for (int i = 0; i < 10 && Fire.PitAt(w, camp) == null; i++)
            Sim.Step(w, new StartFire(AgentId.Player, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        Sim.Step(w, new Stash(AgentId.Player, Resource.Kindling, 1), 1);
        Sim.Step(w, new Gather(AgentId.Player, 0), 60);
        Carcasses.Create(w, camp, "Caribou", SpeciesTable.NewBody(Species.Caribou, w.Rng("test")), w.Minute);
        Sim.Advance(w, 2 * 1440);
        return w;
    }

    [Fact]
    public void Dump_is_deterministic()
    {
        Assert.Equal(Report.DumpText(Lived()), Report.DumpText(Lived()));
    }

    [Fact]
    public void Dump_covers_every_kind_present_and_distribution_counts_every_row()
    {
        var w = Lived();
        var finds = Report.Finds(w);
        var kinds = finds.Select(f => f.Kind).ToHashSet();
        foreach (var kind in new[] { "carcass", "stash", "fire pit", "shelter", "bedding", "place", "stock", "worked forage", "tracks", "herd" })
            Assert.Contains(kind, kinds);
        Assert.Equal(w.Agents.Count - 1, finds.Count(f => f.Category == FindCategory.Agent || f.Kind == "body"));

        var d = Report.Distribution(w, finds);
        Assert.Equal(finds.Count, d.Total);
        Assert.Equal(finds.Count, d.Regions.Sum(r => r.Total));
    }

    /// The report only reads: carcass decay and snow melt are caught up on copies, so the save
    /// is the same byte for byte before and after every report.
    [Fact]
    public void Reports_change_nothing_in_the_world()
    {
        var w = Lived();
        var m = Map.Of(w);
        var tile = Map.Step(m.Camp, Direction.East);
        if (Shelters.At(w, tile) == null) Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        Sim.Advance(w, 5 * 60); // hours of melt and decay owed, unsettled
        string before = Save.ToJson(w);
        var finds = Report.Finds(w);
        Report.DumpText(w, finds);
        Report.DistributionText(w, Report.Distribution(w, finds));
        Report.DumpText(w);
        Assert.Equal(before, Save.ToJson(w));
    }

    /// A snow shelter thawed to nothing is gone from the report and from the shelter floor, though
    /// the world settles that only when something next looks.
    [Fact]
    public void A_melted_snow_shelter_is_not_reported()
    {
        World Thawed(bool shelter, out TileId tile)
        {
            var w = World.New(17);
            w.FixedWeather = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
            var m = Map.Of(w);
            tile = (from x in Enumerable.Range(0, m.Width) from y in Enumerable.Range(0, m.Height)
                    let t = new TileId(x, y)
                    where m.TerrainAt(t) == Terrain.Plain && Manhattan(t, m.Camp) > 30 && m.CaveAt(t) == Cave.None && m.StageAt(t) == null && m.SiteAt(t) == null
                    select t).First();
            if (shelter) Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
            w.Minute = 3 * 1440;
            return w;
        }
        static int Manhattan(TileId a, TileId b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        var w = Thawed(true, out var tile);
        Assert.True(w.Shelters.ContainsKey(tile)); // not yet settled
        var finds = Report.Finds(w);
        Assert.DoesNotContain(finds, f => f.Tile == tile && f.Kind == "shelter");
        RegionRow Region(World x) => Report.Distribution(x).Regions.Single(r => r.X == tile.X / Report.RegionTiles && r.Y == tile.Y / Report.RegionTiles);
        Assert.Equal(Region(Thawed(false, out _)).Shelter, Region(w).Shelter);
        Assert.True(w.Shelters.ContainsKey(tile)); // and the report settled nothing
    }

    /// Sticks and tinder lying in the forage are fuel: a region with them passes the fuel floor.
    [Fact]
    public void Forage_fuel_counts_toward_the_fuel_floor()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        var d = Report.Distribution(w);
        foreach (var r in d.Regions.Where(r => !r.Fuel))
            for (int x = r.X * Report.RegionTiles; x < Math.Min(m.Width, (r.X + 1) * Report.RegionTiles); x++)
                for (int y = r.Y * Report.RegionTiles; y < Math.Min(m.Height, (r.Y + 1) * Report.RegionTiles); y++)
                    Assert.DoesNotContain(Pools.At(w, new TileId(x, y)), p => p is ForagePool f && f.Items.Any(i => Fire.IsFuel(i.Resource)));
    }

    [Fact]
    public void Curve_has_one_point_per_chunk()
    {
        var w = World.New(17);
        var curve = Report.Curve(w, 4, 2);
        Assert.Equal(2, curve.Count);
        Assert.Equal([2, 4], curve.Select(p => p.Day));
        Assert.Throws<ArgumentException>(() => Report.Curve(w, 5, 2));
    }
}
