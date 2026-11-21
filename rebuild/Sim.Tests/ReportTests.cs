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
        if (Shelters.At(w, tile, []) == null) Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
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

    /// The fuel floor is a sum of hours of fire, so it fails where wood is thin (open steppe, bare
    /// rock), and forage counts toward it: a treeless region that passes on its deadfall fails once
    /// that is picked over, and passes again when it has grown back.
    [Fact]
    public void Fuel_floor_fails_where_wood_is_thin_and_counts_forage()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        IEnumerable<TileId> Tiles(RegionRow r) =>
            from x in Enumerable.Range(r.X * Report.RegionTiles, Report.RegionTiles)
            from y in Enumerable.Range(r.Y * Report.RegionTiles, Report.RegionTiles)
            let t = new TileId(x, y)
            where m.InBounds(t) && m.TerrainAt(t).IsPassable()
            select t;
        var d = Report.Distribution(w);
        Assert.Contains(d.Regions, r => !r.Fuel);
        var region = d.Regions.First(r => r.Fuel && Tiles(r).All(t => !Pools.At(w, t).Any(p => p is TreePool)));
        RegionRow Now() => Report.Distribution(w).Regions.Single(r => (r.X, r.Y) == (region.X, region.Y));

        var rng = w.Rng("test");
        foreach (var t in Tiles(region))
            for (int i = 0; i < Pools.At(w, t).Count; i++)
                if (Pools.At(w, t)[i] is ForagePool) Pools.Forage(w, new PoolId(t, i), 500, rng);
        Assert.False(Now().Fuel, $"region {region.X},{region.Y} picked clean still has {Now().FuelFireHours:0.0} h of fire");
        w.Minute += (long)(Pools.ForageRespawnHours * 60);
        Assert.True(Now().Fuel);
    }

    /// The fuel floor is about surviving, and a player starts with no axe: standing trees are felled
    /// fuel, counted apart, and a region that would pass only on its trees fails (third review: the
    /// floor counted every tree's fire whether or not anyone could fell it).
    [Fact]
    public void Fuel_floor_counts_what_hands_gather_and_trees_apart()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        var forest = (from x in Enumerable.Range(0, m.Width) from y in Enumerable.Range(0, m.Height)
                      let t = new TileId(x, y)
                      where Pools.At(w, t).Any(p => p is TreePool) && Pools.At(w, t).Any(p => p is ForagePool)
                      select t).First();
        var (byHand, withAxe) = Report.FuelFireHoursPerWorkHour(w, forest);
        Assert.True(withAxe > Report.MinFuelFireHours / 10, $"a stand of trees gave {withAxe:0.0} h of felled fire an hour");
        Assert.True(byHand > 0 && byHand < withAxe, $"by hand {byHand:0.00} h, felled {withAxe:0.0} h");

        var d = Report.Distribution(w);
        var onTrees = d.Regions.Where(r => r.FuelFireHours < Report.MinFuelFireHours && r.FuelFireHours + r.FelledFireHours >= Report.MinFuelFireHours).ToList();
        Assert.NotEmpty(onTrees);
        Assert.All(onTrees, r => Assert.False(r.Fuel, $"region {r.X},{r.Y} passes the floor on trees no one can fell bare-handed"));
        Assert.All(d.Regions, r => Assert.Equal(r.FuelFireHours >= Report.MinFuelFireHours, r.Fuel));
    }

    /// What the CLI's look prints reads the world and writes nothing (it once settled a snow
    /// shelter's melt and a carcass's decay, so "look; save" saved a different world from "save").
    [Fact]
    public void Look_changes_nothing()
    {
        var w = World.New(17);
        w.FixedWeather = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
        var m = Map.Of(w);
        var tile = (from x in Enumerable.Range(0, m.Width) from y in Enumerable.Range(0, m.Height)
                    let t = new TileId(x, y)
                    where m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && m.SiteAt(t) == null && t != m.Camp
                    select t).First();
        w.Agent(AgentId.Player).Tile = tile;
        Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        Carcasses.Create(w, tile, "Caribou", SpeciesTable.NewBody(Species.Caribou, w.Rng("test")), w.Minute);
        w.Minute += 6 * 60;
        string before = Save.ToJson(w);
        var lines = Report.Look(w, AgentId.Player);
        Assert.Equal(before, Save.ToJson(w));
        Assert.Contains(lines, l => l.StartsWith("shelter: Snow shelter", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("carcass", StringComparison.Ordinal) && !l.Contains("decay 0%"));
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
