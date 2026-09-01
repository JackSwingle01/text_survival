namespace PixelArtCli;

using Rgba = (byte R, byte G, byte B, byte A);

/// <summary>
/// A raster buffer with drawing primitives, used both as the final output image
/// and as a part's local scratch canvas during composition. Drawing is clipped
/// silently to bounds (standard raster-graphics behavior, not an error).
/// </summary>
public class Canvas(int width, int height)
{
    public readonly int Width = width;
    public readonly int Height = height;
    private readonly byte[] _rgba = new byte[width * height * 4];

    public byte[] Rgba => _rgba;

    public Rgba GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return (0, 0, 0, 0);
        int i = (y * Width + x) * 4;
        return (_rgba[i], _rgba[i + 1], _rgba[i + 2], _rgba[i + 3]);
    }

    public void SetPixel(int x, int y, Rgba color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        int i = (y * Width + x) * 4;
        _rgba[i] = color.R;
        _rgba[i + 1] = color.G;
        _rgba[i + 2] = color.B;
        _rgba[i + 3] = color.A;
    }

    public void Pixel(int x, int y, Rgba color) => SetPixel(x, y, color);

    public void Rect(int x, int y, int w, int h, Rgba color)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                SetPixel(xx, yy, color);
    }

    public void Line(int x0, int y0, int x1, int y1, Rgba color)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int x = x0, y = y0;
        while (true)
        {
            SetPixel(x, y, color);
            if (x == x1 && y == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }

    /// <summary>Filled circle.</summary>
    public void Circle(int cx, int cy, int r, Rgba color)
    {
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r * r)
                    SetPixel(x, y, color);
            }
    }

    /// <summary>Flood fill the contiguous same-color region starting at (x,y).</summary>
    public void Fill(int x, int y, Rgba color)
    {
        Rgba target = GetPixel(x, y);
        if (target == color) return;

        var stack = new Stack<(int X, int Y)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            if (cx < 0 || cx >= Width || cy < 0 || cy >= Height) continue;
            if (GetPixel(cx, cy) != target) continue;

            SetPixel(cx, cy, color);
            stack.Push((cx + 1, cy));
            stack.Push((cx - 1, cy));
            stack.Push((cx, cy + 1));
            stack.Push((cx, cy - 1));
        }
    }

    /// <summary>Mirror the left half onto the right half (reflects around the vertical centerline).</summary>
    public void MirrorX()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width / 2; x++)
                SetPixel(Width - 1 - x, y, GetPixel(x, y));
    }

    /// <summary>Mirror the top half onto the bottom half (reflects around the horizontal centerline).</summary>
    public void MirrorY()
    {
        for (int y = 0; y < Height / 2; y++)
            for (int x = 0; x < Width; x++)
                SetPixel(x, Height - 1 - y, GetPixel(x, y));
    }

    /// <summary>Stamp another canvas onto this one at (atX, atY), skipping the source's transparent pixels.</summary>
    public void BlitFrom(Canvas src, int atX, int atY)
    {
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
            {
                var p = src.GetPixel(x, y);
                if (p.A == 0) continue;
                SetPixel(atX + x, atY + y, p);
            }
    }

    /// <summary>Bounding box of non-transparent pixels, or null if the canvas is empty.</summary>
    public (int MinX, int MinY, int MaxX, int MaxY)? BoundingBox()
    {
        int minX = Width, minY = Height, maxX = -1, maxY = -1;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (GetPixel(x, y).A != 0)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        return maxX < 0 ? null : (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Count 8-connected components of non-transparent pixels. Returns the count and,
    /// for components after the first (i.e. the "stray islands"), their bounding boxes.
    /// </summary>
    public (int Count, List<(int MinX, int MinY, int MaxX, int MaxY)> ExtraComponents) CountConnectedComponents()
    {
        var visited = new bool[Width, Height];
        var extras = new List<(int, int, int, int)>();
        int count = 0;

        for (int y0 = 0; y0 < Height; y0++)
        {
            for (int x0 = 0; x0 < Width; x0++)
            {
                if (visited[x0, y0] || GetPixel(x0, y0).A == 0) continue;

                count++;
                int minX = x0, minY = y0, maxX = x0, maxY = y0;
                var stack = new Stack<(int, int)>();
                stack.Push((x0, y0));
                visited[x0, y0] = true;

                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop();
                    minX = Math.Min(minX, cx); maxX = Math.Max(maxX, cx);
                    minY = Math.Min(minY, cy); maxY = Math.Max(maxY, cy);

                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                            if (visited[nx, ny] || GetPixel(nx, ny).A == 0) continue;
                            visited[nx, ny] = true;
                            stack.Push((nx, ny));
                        }
                }

                if (count > 1)
                    extras.Add((minX, minY, maxX, maxY));
            }
        }

        return (count, extras);
    }

    /// <summary>
    /// Whether any non-transparent pixel of `a` (placed at aX,aY) is 8-adjacent to any
    /// non-transparent pixel of `b` (placed at bX,bY). Used to assert two parts actually connect.
    /// </summary>
    public static bool AreTouching(Canvas a, int aX, int aY, Canvas b, int bX, int bY)
    {
        var aPixels = new List<(int X, int Y)>();
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
                if (a.GetPixel(x, y).A != 0)
                    aPixels.Add((aX + x, aY + y));

        var bSet = new HashSet<(int, int)>();
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                if (b.GetPixel(x, y).A != 0)
                    bSet.Add((bX + x, bY + y));

        foreach (var (x, y) in aPixels)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (bSet.Contains((x + dx, y + dy)))
                        return true;

        return false;
    }
}
