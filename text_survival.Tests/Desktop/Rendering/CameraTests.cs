using System.Numerics;
using text_survival.Desktop.Rendering;

namespace text_survival.Tests.Desktop.Rendering;

public class CameraTests
{
    [Theory]
    [InlineData(900, 320)]
    [InlineData(320, 900)]
    [InlineData(703, 701)]
    public void RectangularViewportFillsBothAxesThroughPanZoomAndResize(int width, int height)
    {
        var camera = new Camera(20, 20);
        camera.Pan(new Vector2(.37f, -.29f), 40, 40, immediate: true);
        foreach (int zoom in new[] { 0, 2, -4 })
        {
            camera.Zoom(zoom);
            foreach (int inset in new[] { 0, 73 })
            {
                camera.ConfigureForViewport(300, 100, width - inset, height - inset);
                Assert.Equal(width - inset, camera.GridWidth);
                Assert.Equal(height - inset, camera.GridHeight);
                Assert.Equal(300, camera.ScreenOffsetX);
                Assert.Equal(100, camera.ScreenOffsetY);
                Assert.Equal(new Vector2(300 + camera.GridWidth / 2f, 100 + camera.GridHeight / 2f),
                    camera.GetTileCenter(camera.Center.X, camera.Center.Y));
                var visible = camera.GetVisibleTiles().ToHashSet();
                foreach (float x in new[] { 300.5f, 300 + camera.GridWidth / 2f, 300 + camera.GridWidth - .5f })
                foreach (float y in new[] { 100.5f, 100 + camera.GridHeight / 2f, 100 + camera.GridHeight - .5f })
                {
                    var point = new Vector2(x, y);
                    Assert.True(camera.ContainsScreenPoint(point));
                    var tile = camera.ScreenToWorld(point);
                    Assert.NotNull(tile);
                    Assert.Contains(tile.Value, visible);
                    var origin = camera.WorldToScreen(tile.Value.x, tile.Value.y);
                    Assert.InRange(point.X - origin.X, 0, camera.TileSize);
                    Assert.InRange(point.Y - origin.Y, 0, camera.TileSize);
                }
                Assert.Null(camera.ScreenToWorld(new Vector2(300 + camera.GridWidth, 100)));
                Assert.Null(camera.ScreenToWorld(new Vector2(300, 100 + camera.GridHeight)));
            }
        }
    }

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
    [Theory]
    [InlineData(2, 3)]
    [InlineData(-4, 15)]
    public void ZoomKeepsViewportBoundedAndPickingAccurate(int steps, int expectedSize)
    {
        var camera = new Camera(12, 12);
        camera.ConfigureForViewport(300, 50, 700, 700);
        camera.Pan(new Vector2(.37f, -.29f), 40, 40, immediate: true);
        var center = camera.Center;
        Assert.True(camera.Zoom(steps));
        Assert.Equal(expectedSize, camera.ViewSize);
        Assert.Equal(center, camera.Center);
        Assert.False(camera.IsFollowingPlayer);
        Assert.InRange(camera.ScreenOffsetX + camera.GridWidth, 990, 1000);
        Assert.Equal((12, 12), camera.ScreenToWorld(camera.GetTileCenter(12, 12)));
        var visible = camera.GetVisibleTiles().ToHashSet();
        for (int x = camera.ScreenOffsetX; x < camera.ScreenOffsetX + camera.GridWidth; x += 9)
        for (int y = camera.ScreenOffsetY; y < camera.ScreenOffsetY + camera.GridHeight; y += 9)
            Assert.Contains(camera.ScreenToWorld(new Vector2(x, y))!.Value, visible);
        camera.ConfigureForViewport(300, 50, 500, 500);
        Assert.Equal(expectedSize, camera.ViewSize);
        Assert.True(camera.ResetZoom());
        Assert.Equal(7, camera.ViewSize);
    }

    [Fact]
    public void TrackpadZoomAccumulatesAndReversesAtLimitsWithoutBreakingFollow()
    {
        var camera = new Camera();
        Assert.False(camera.ZoomWheel(.5f));
        Assert.True(camera.ZoomWheel(.5f));
        Assert.Equal(5, camera.ViewSize);
        camera.Zoom(int.MaxValue);
        Assert.Equal(3, camera.ViewSize);
        Assert.False(camera.ZoomWheel(6));
        Assert.True(camera.ZoomWheel(-1));
        Assert.Equal(5, camera.ViewSize);
        camera.Zoom(int.MinValue);
        Assert.Equal(31, camera.ViewSize);
        Assert.True(camera.IsFollowingPlayer);
        camera.ResetZoom();
        Assert.Equal(7, camera.ViewSize);
    }
}
