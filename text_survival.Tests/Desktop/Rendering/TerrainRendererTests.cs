using Raylib_cs;
using text_survival.Desktop.Rendering;
using text_survival.Environments.Grid;

namespace text_survival.Tests.Desktop.Rendering;

public class TerrainRendererTests
{
    private static Dictionary<TerrainType, TerrainRenderer.Art[]> FlatArt() =>
        Enum.GetValues<TerrainType>().ToDictionary(t => t, t => new[]
        {
            new TerrainRenderer.Art(1, 1, [new Color((byte)((int)t*25), (byte)(200-(int)t*20), (byte)((int)t*20), (byte)255)])
        });

    [Fact]
    public void OneSourceSpansFourCellsIncludingNegativeCoordinates()
    {
        var colors = Enumerable.Range(0, 96 * 96).Select(i => new Color((byte)(i % 96), (byte)(i / 96), (byte)0, (byte)255)).ToArray();
        var art = new Dictionary<TerrainType, TerrainRenderer.Art[]> { [TerrainType.Forest] = [new(96, 96, colors)] };
        var neighborhood = TerrainRenderer.Neighborhood.Create(TerrainType.Forest);
        foreach (int y in new[] { -2, -1, 0, 1, 2 })
            foreach (int x in new[] { -2, -1, 0, 1, 2 })
            {
                var tile = TerrainRenderer.Rasterize(x, y, neighborhood, art);
                Assert.Equal((byte)(((x % 2 + 2) % 2) * 48), tile[0].R);
                Assert.Equal((byte)(((y % 2 + 2) % 2) * 48), tile[0].G);
                Assert.Equal((byte)(tile[0].R + 47), tile[^1].R);
            }
    }

    [Fact]
    public void UnknownNeighborsDoNotAlterTerrainAndInteriorStaysIntact()
    {
        var art = FlatArt();
        var plain = TerrainRenderer.Neighborhood.Create(TerrainType.Plain);
        var unknown = TerrainRenderer.Neighborhood.Create(TerrainType.Plain, (_, _) => null);
        Assert.Equal(plain, unknown);
        var mixed = TerrainRenderer.Neighborhood.Create(TerrainType.Plain, (_, _) => TerrainType.DeepWater);
        var pixels = TerrainRenderer.Rasterize(3, 5, mixed, art);
        var plainColor = art[TerrainType.Plain][0].Pixels[0];
        var waterColor = art[TerrainType.DeepWater][0].Pixels[0];
        Assert.Equal(plainColor, pixels[24 * 48 + 24]);
        Assert.Contains(pixels.Take(48), p => p.Equals(waterColor));
        Assert.All(pixels, p => Assert.True(p.Equals(plainColor) || p.Equals(waterColor)));
        Assert.Equal(pixels, TerrainRenderer.Rasterize(3, 5, mixed, art));
    }

    [Fact]
    public void BothSidesOfBoundaryConvergeAndCornersIncludeDiagonalTerrain()
    {
        var art = FlatArt();
        var forest = art[TerrainType.Forest][0].Pixels[0];
        var water = art[TerrainType.DeepWater][0].Pixels[0];
        var left = TerrainRenderer.Neighborhood.Create(TerrainType.Forest,
            (dx, _) => dx == 1 ? TerrainType.DeepWater : TerrainType.Forest);
        var right = TerrainRenderer.Neighborhood.Create(TerrainType.DeepWater,
            (dx, _) => dx == -1 ? TerrainType.Forest : TerrainType.DeepWater);
        var a = TerrainRenderer.Rasterize(-1, 0, left, art);
        var b = TerrainRenderer.Rasterize(0, 0, right, art);
        // The pixels either side of the boundary are a similar mix of both terrains,
        // so the edge is a scatter, not a step.
        double aWater = Enumerable.Range(0, 48).Count(y => a[y * 48 + 47].Equals(water)) / 48.0;
        double bWater = Enumerable.Range(0, 48).Count(y => b[y * 48].Equals(water)) / 48.0;
        Assert.InRange(aWater, 0.2, 0.8);
        Assert.InRange(bWater, 0.2, 0.8);
        Assert.InRange(Math.Abs(aWater - bWater), 0, 0.3);

        var corner = TerrainRenderer.Neighborhood.Create(TerrainType.Forest,
            (dx, dy) => dx == 1 && dy == 1 ? TerrainType.DeepWater : TerrainType.Forest);
        // A diagonal-only neighbour shows through some corners; how much depends on
        // where the edge wobble falls, so look across a run of tiles.
        var tiles = Enumerable.Range(0, 8).Select(x => TerrainRenderer.Rasterize(x, 0, corner, art)).ToArray();
        Assert.Contains(tiles, t => Enumerable.Range(36, 12)
            .Any(y => Enumerable.Range(36, 12).Any(x => t[y * 48 + x].Equals(water))));
        Assert.All(tiles, t => Assert.Equal(forest, t[24 * 48 + 24]));
    }
}
