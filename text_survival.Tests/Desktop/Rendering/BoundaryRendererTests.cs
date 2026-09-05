using System.Numerics;
using text_survival.Desktop.Rendering;

namespace text_survival.Tests.Desktop.Rendering;

public class BoundaryRendererTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BoundariesShareCornersAndWidthsAcrossDirections(bool river)
    {
        const float pitch = 102;
        for (int x = -3; x <= 3; x++)
        for (int y = -3; y <= 3; y++)
        {
            EdgeRenderer.Boundary Build(int bx, int by, bool vertical) =>
                EdgeRenderer.BuildBoundary(bx, by, vertical, new Vector2(bx, by) * pitch,
                    pitch, river, Vector2.UnitY);
            var horizontal = Build(x, y, false);
            var vertical = Build(x, y, true);
            var next = Build(x + 1, y, false);
            var corner = Build(x + 1, y, true);
            var below = Build(x, y + 1, true);
            Same(horizontal, 0, vertical, 0);
            Same(horizontal, horizontal.Points.Length - 1, next, 0);
            Same(horizontal, horizontal.Points.Length - 1, corner, 0);
            Same(vertical, vertical.Points.Length - 1, below, 0);
        }
    }

    [Fact]
    public void RoundedRiverBendKeepsBothOuterEndsAndJoinsWithoutGaps()
    {
        var a = EdgeRenderer.BuildBoundary(0, 0, false, Vector2.Zero, 102, true, Vector2.Zero);
        var b = EdgeRenderer.BuildBoundary(1, 0, true, new Vector2(102, 0), 102, true, Vector2.Zero);
        var rounded = EdgeRenderer.RoundRiverBends([a, b]);
        Assert.Equal(3, rounded.Count);
        var bend = rounded[0];
        var trimmedA = rounded[1];
        var trimmedB = rounded[2];
        Same(a, 0, trimmedA, 0);
        Same(b, b.Points.Length - 1, trimmedB, trimmedB.Points.Length - 1);
        Same(trimmedA, trimmedA.Points.Length - 1, bend, 0);
        Same(trimmedB, 0, bend, bend.Points.Length - 1);
        // The rounded channel cuts inside the corner instead of keeping the elbow.
        Assert.True(Vector2.Distance(bend.Points[bend.Points.Length / 2], a.Points[^1]) > 3);
    }

    [Fact]
    public void BoundaryDetailMovesWithCameraWithoutChangingShape()
    {
        var pan = new Vector2(31.25f, -76.5f);
        var a = EdgeRenderer.BuildBoundary(3, 5, true, Vector2.Zero, 102, true, Vector2.Zero);
        var b = EdgeRenderer.BuildBoundary(3, 5, true, pan, 102, true, Vector2.Zero);
        Assert.Equal(a.Seed, b.Seed);
        Assert.Equal(a.Widths, b.Widths);
        for (int i = 0; i < a.Points.Length; i++)
            Assert.True(Vector2.Distance(a.Points[i] + pan, b.Points[i]) < 0.001f);
    }

    private static void Same(EdgeRenderer.Boundary a, int ai, EdgeRenderer.Boundary b, int bi)
    {
        Assert.True(Vector2.Distance(a.Points[ai], b.Points[bi]) < 0.001f);
        Assert.Equal(a.Widths[ai], b.Widths[bi], 3);
    }
}
