using System.Numerics;
using text_survival.Desktop.Rendering;

namespace text_survival.Tests.Desktop.Rendering;

public class CameraTests
{
    [Fact]
    public void ManualPanStaysIndependentOfPlayerUntilRecentered()
    {
        var camera = new Camera(10, 10);
        camera.Pan(new Vector2(3, -2), 40, 40, immediate: true);
        camera.TrackPlayer(new Vector2(20, 20));
        camera.Update(1);
        Assert.False(camera.IsFollowingPlayer);
        Assert.Equal(new Vector2(13, 8), camera.Center);

        camera.Follow(new Vector2(20, 20));
        camera.Update(1);
        Assert.True(camera.IsFollowingPlayer);
        Assert.Equal(new Vector2(20, 20), camera.Center);
    }

    [Fact]
    public void PanningClampsToAllMapEdges()
    {
        var camera = new Camera(5, 5);
        camera.Pan(new Vector2(-100, 100), 20, 30, immediate: true);
        Assert.Equal(new Vector2(0, 29), camera.Center);
        camera.Pan(new Vector2(100, -100), 20, 30, immediate: true);
        Assert.Equal(new Vector2(19, 0), camera.Center);
    }

    [Fact]
    public void FractionalPanPreservesPickingAndViewportCoverage()
    {
        var camera = new Camera(12, 12);
        camera.Pan(new Vector2(0.37f, -0.29f), 30, 30, immediate: true);
        Assert.Equal((13, 11), camera.ScreenToWorld(camera.GetTileCenter(13, 11)));
        Assert.Null(camera.ScreenToWorld(new Vector2(camera.ScreenOffsetX - 1, camera.ScreenOffsetY)));
        var tiles = camera.GetVisibleTiles().ToHashSet();
        for (int x = camera.ScreenOffsetX; x < camera.ScreenOffsetX + camera.GridWidth; x += 9)
        for (int y = camera.ScreenOffsetY; y < camera.ScreenOffsetY + camera.GridHeight; y += 9)
        {
            var picked = camera.ScreenToWorld(new Vector2(x, y));
            if (picked.HasValue) Assert.Contains(picked.Value, tiles);
        }
    }
}
