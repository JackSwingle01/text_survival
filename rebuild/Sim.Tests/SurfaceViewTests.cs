namespace Survival.Tests;

/// Ground.SurfaceNow: a tile's ground as a view draws it (snow, crust, mud, standing water, ice),
/// read without writing anything.
public class SurfaceViewTests
{
    /// The run opens on 1 July: the June pre-roll snows now and then, and none of it is lying on the
    /// valley floor by day 1. Only the coldest rock (the high pass) keeps any.
    [Fact]
    public void OnDayOne_AlmostNoTileHasSnowLying()
    {
        var w = World.New(1);
        var m = Map.Of(w);
        var passable = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .Where(t => m.TerrainAt(t).IsPassable()).ToList();
        var snowy = passable.Where(t => Ground.SurfaceNow(w, t).SnowDepthM > 0.02).ToList();
        Assert.True(snowy.Count < passable.Count / 100, $"{snowy.Count} of {passable.Count} tiles have snow lying in July");
        Assert.All(snowy, t => Assert.Equal(Terrain.Rock, m.TerrainAt(t)));
    }

    [Fact]
    public void SnowOnATile_IsReported_AndLookingWritesNothing()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        double already = Ground.SurfaceNow(w, camp).SnowDepthM; // seed 17's camp has a dusting
        Ground.Write(w, camp, (g, _) => g.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = 0.4, CompactionPct = 0.3, FrozenFractionPct = 1 }));
        string before = Save.ToJson(w);
        var s = Ground.SurfaceNow(w, camp);
        Assert.Equal(before, Save.ToJson(w));
        Assert.Equal(already + 0.4, s.SnowDepthM, 6);
        Assert.Equal((0.0, 0.0), (s.MudLevel, s.WaterDepthM)); // under snow there is no mud to see
    }

    [Fact]
    public void FrozenStandingWater_IsIce()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        Ground.Write(w, camp, (g, _) => g.Layers.Insert(1, new SurfaceLayer { Material = SurfaceMaterial.Water, ThicknessM = 0.05, WaterSaturationPct = 1, FrozenFractionPct = 1 }));
        var s = Ground.SurfaceNow(w, camp);
        Assert.True(s.Iced);
        Assert.True(s.WaterDepthM > 0.05); // ice stands taller than the water it froze from
    }
}
