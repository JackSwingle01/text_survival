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
        var colors = Enumerable.Range(0, 96*96).Select(i => new Color((byte)(i%96), (byte)(i/96), (byte)0, (byte)255)).ToArray();
        var art = new Dictionary<TerrainType, TerrainRenderer.Art[]> { [TerrainType.Forest] = [new(96, 96, colors)] };
        var neighborhood = TerrainRenderer.Neighborhood.Create(TerrainType.Forest);
        foreach (int y in new[] { -2, -1, 0, 1, 2 })
        foreach (int x in new[] { -2, -1, 0, 1, 2 })
        {
            var tile = TerrainRenderer.Rasterize(x, y, neighborhood, art);
            Assert.Equal((byte)(((x%2+2)%2)*48), tile[0].R);
            Assert.Equal((byte)(((y%2+2)%2)*48), tile[0].G);
            Assert.Equal((byte)(tile[0].R+47), tile[^1].R);
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
        Assert.Equal(art[TerrainType.Plain][0].Pixels[0], pixels[24*48+24]);
        Assert.NotEqual(pixels[24*48+24], pixels[0]);
        Assert.Equal(pixels, TerrainRenderer.Rasterize(3, 5, mixed, art));
    }

    [Fact]
    public void BothSidesOfBoundaryConvergeAndCornersIncludeDiagonalTerrain()
    {
        var art = FlatArt();
        var left = TerrainRenderer.Neighborhood.Create(TerrainType.Forest,
            (dx, _) => dx == 1 ? TerrainType.DeepWater : TerrainType.Forest);
        var right = TerrainRenderer.Neighborhood.Create(TerrainType.DeepWater,
            (dx, _) => dx == -1 ? TerrainType.Forest : TerrainType.DeepWater);
        var a = TerrainRenderer.Rasterize(-1, 0, left, art);
        var b = TerrainRenderer.Rasterize(0, 0, right, art);
        for (int y = 0; y < 48; y++)
            Assert.InRange(Math.Abs(a[y*48+47].R-b[y*48].R), 0, 30);

        var corner = TerrainRenderer.Neighborhood.Create(TerrainType.Forest,
            (dx, dy) => dx == 1 && dy == 1 ? TerrainType.DeepWater : TerrainType.Forest);
        var pixels = TerrainRenderer.Rasterize(0, 0, corner, art);
        Assert.True(pixels[^1].R > 10);
        Assert.Equal(art[TerrainType.Forest][0].Pixels[0], pixels[24*48+24]);
    }
}
