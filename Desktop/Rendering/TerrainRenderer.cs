using Raylib_cs;
using System.Numerics;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Renders procedural terrain textures for each tile type.
/// Ported from JavaScript TerrainRenderer.js.
/// </summary>
public static class TerrainRenderer
{
    /// <summary>
    /// Render terrain texture for a tile.
    /// </summary>
    public static void RenderTexture(string terrain, float px, float py, float size, int worldX, int worldY, float timeFactor = 0.5f)
    {
        switch (terrain)
        {
            case "Forest":
                RenderForest(px, py, size, worldX, worldY);
                break;
            case "Water":
                RenderWater(px, py, size, worldX, worldY, isDeep: false);
                break;
            case "DeepWater":
                RenderWater(px, py, size, worldX, worldY, isDeep: true);
                break;
            case "Plain":
                RenderPlain(px, py, size, worldX, worldY);
                break;
            case "Clearing":
                RenderClearing(px, py, size, worldX, worldY);
                break;
            case "Hills":
                RenderHills(px, py, size, worldX, worldY);
                break;
            case "Rock":
                RenderRock(px, py, size, worldX, worldY, isMountain: false);
                break;
            case "Mountain":
                RenderRock(px, py, size, worldX, worldY, isMountain: true);
                break;
            case "Marsh":
                RenderMarsh(px, py, size, worldX, worldY);
                break;
        }
    }

    /// <summary>
    /// Forest - evergreen trees with snow.
    /// </summary>
    private static void RenderForest(float px, float py, float size, int worldX, int worldY)
    {
        var treeColor = new Color(15, 35, 25, 180);

        // Generate 5-7 trees at seeded random positions
        int treeCount = 5 + (int)(RenderUtils.SeededRandom(worldX, worldY, 100) * 3);
        for (int i = 0; i < treeCount; i++)
        {
            float tx = px + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 10) * 0.8f);
            float ty = py + size * (0.15f + RenderUtils.SeededRandom(worldX, worldY, i * 10 + 1) * 0.7f);
            float treeHeight = size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 10 + 2) * 0.067f);
            float treeWidth = treeHeight * 0.6f;

            // Draw triangle tree (evergreen shape)
            Vector2 p1 = new(tx, ty - treeHeight);
            Vector2 p2 = new(tx - treeWidth / 2, ty);
            Vector2 p3 = new(tx + treeWidth / 2, ty);
            Raylib.DrawTriangle(p1, p2, p3, treeColor);

            // Second layer (slightly smaller, overlapping)
            Vector2 p4 = new(tx, ty - treeHeight * 0.85f);
            Vector2 p5 = new(tx - treeWidth * 0.35f, ty - treeHeight * 0.2f);
            Vector2 p6 = new(tx + treeWidth * 0.35f, ty - treeHeight * 0.2f);
            Raylib.DrawTriangle(p4, p5, p6, treeColor);
        }

        // Snow on some trees (white highlights)
        var snowColor = new Color(255, 255, 255, 40);
        for (int i = 0; i < 3; i++)
        {
            float sx = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 50) * 0.6f);
            float sy = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 51) * 0.4f);
            Raylib.DrawRectangle(
                (int)(sx - size * 0.025f),
                (int)sy,
                (int)(size * 0.05f),
                (int)(size * 0.017f),
                snowColor);
        }
    }

    /// <summary>
    /// Water/Ice - frozen surface with concentric pressure rings.
    /// </summary>
    private static void RenderWater(float px, float py, float size, int worldX, int worldY, bool isDeep)
    {
        float centerX = px + size * (0.4f + RenderUtils.SeededRandom(worldX, worldY, 1) * 0.2f);
        float centerY = py + size * (0.4f + RenderUtils.SeededRandom(worldX, worldY, 2) * 0.2f);

        // Concentric pressure rings
        var ringColor = isDeep
            ? new Color(40, 70, 100, 100)
            : new Color(60, 100, 130, 90);

        for (int i = 0; i < 3; i++)
        {
            float radius = size * (0.15f + i * 0.12f);
            float startAngle = RenderUtils.SeededRandom(worldX, worldY, i + 10) * MathF.PI * 0.5f;
            float arcLength = MathF.PI * (0.6f + RenderUtils.SeededRandom(worldX, worldY, i + 11) * 0.8f);

            // Draw arc as line segments
            DrawArc(centerX, centerY, radius, startAngle, startAngle + arcLength, ringColor, Math.Max(1, (int)(size * 0.017f)));
        }

        // Central snow patch
        var snowColor = new Color(255, 255, 255, 65);
        float snowRadius = size * 0.12f;
        Raylib.DrawCircle((int)centerX, (int)centerY, snowRadius, snowColor);

        // Radiating cracks
        var crackColor = isDeep
            ? new Color(30, 55, 80, 115)
            : new Color(50, 80, 110, 90);

        int crackCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 30) * 2);
        for (int i = 0; i < crackCount; i++)
        {
            float angle = RenderUtils.SeededRandom(worldX, worldY, i + 31) * MathF.PI * 2;
            float len = size * (0.25f + RenderUtils.SeededRandom(worldX, worldY, i + 32) * 0.2f);
            float endX = centerX + MathF.Cos(angle) * len;
            float endY = centerY + MathF.Sin(angle) * len;
            Raylib.DrawLine((int)centerX, (int)centerY, (int)endX, (int)endY, crackColor);
        }
    }

    /// <summary>
    /// Plain - tundra with lichen, snow drifts, and sparse vegetation.
    /// </summary>
    private static void RenderPlain(float px, float py, float size, int worldX, int worldY)
    {
        // Lichen patches (small colored dots)
        var lichenColors = new[]
        {
            new Color(140, 150, 130, 60),
            new Color(160, 140, 120, 50),
            new Color(120, 130, 100, 55)
        };

        int patchCount = 4 + (int)(RenderUtils.SeededRandom(worldX, worldY, 1) * 4);
        for (int i = 0; i < patchCount; i++)
        {
            float lx = px + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 3) * 0.8f);
            float ly = py + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 3 + 1) * 0.8f);
            float lr = size * (0.02f + RenderUtils.SeededRandom(worldX, worldY, i * 3 + 2) * 0.03f);
            var color = lichenColors[i % lichenColors.Length];
            Raylib.DrawCircle((int)lx, (int)ly, lr, color);
        }

        // Snow drift lines
        var driftColor = new Color(255, 255, 255, 25);
        int driftCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 20) * 2);
        for (int i = 0; i < driftCount; i++)
        {
            float y = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 21) * 0.6f);
            float x1 = px + size * RenderUtils.SeededRandom(worldX, worldY, i + 22) * 0.3f;
            float x2 = px + size * (0.4f + RenderUtils.SeededRandom(worldX, worldY, i + 23) * 0.5f);
            Raylib.DrawLine((int)x1, (int)y, (int)x2, (int)y, driftColor);
        }

        // Sparse grass tufts
        var grassColor = new Color(80, 90, 70, 70);
        int grassCount = 3 + (int)(RenderUtils.SeededRandom(worldX, worldY, 30) * 3);
        for (int i = 0; i < grassCount; i++)
        {
            float gx = px + size * (0.15f + RenderUtils.SeededRandom(worldX, worldY, i + 31) * 0.7f);
            float gy = py + size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, i + 32) * 0.6f);
            float gh = size * (0.03f + RenderUtils.SeededRandom(worldX, worldY, i + 33) * 0.02f);

            // Draw grass as small vertical lines
            for (int j = 0; j < 3; j++)
            {
                float offset = (j - 1) * size * 0.01f;
                Raylib.DrawLine((int)(gx + offset), (int)gy, (int)(gx + offset), (int)(gy - gh), grassColor);
            }
        }
    }

    /// <summary>
    /// Clearing - open area with dirt patches and edge trees.
    /// </summary>
    private static void RenderClearing(float px, float py, float size, int worldX, int worldY)
    {
        // Dirt patches
        var dirtColor = new Color(100, 90, 80, 40);
        int patchCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 1) * 2);
        for (int i = 0; i < patchCount; i++)
        {
            float dx = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i * 2) * 0.6f);
            float dy = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i * 2 + 1) * 0.6f);
            float dr = size * (0.08f + RenderUtils.SeededRandom(worldX, worldY, i + 10) * 0.06f);
            Raylib.DrawCircle((int)dx, (int)dy, dr, dirtColor);
        }

        // Stumps
        var stumpColor = new Color(60, 50, 40, 80);
        if (RenderUtils.SeededRandom(worldX, worldY, 20) > 0.5f)
        {
            float sx = px + size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, 21) * 0.4f);
            float sy = py + size * (0.4f + RenderUtils.SeededRandom(worldX, worldY, 22) * 0.3f);
            Raylib.DrawCircle((int)sx, (int)sy, size * 0.03f, stumpColor);
        }

        // Edge trees (small, at borders)
        var treeColor = new Color(15, 35, 25, 120);
        for (int i = 0; i < 2; i++)
        {
            float tx = px + (i == 0 ? size * 0.05f : size * 0.9f);
            float ty = py + size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, i + 30) * 0.4f);
            float th = size * 0.08f;
            float tw = th * 0.5f;

            Vector2 p1 = new(tx, ty - th);
            Vector2 p2 = new(tx - tw / 2, ty);
            Vector2 p3 = new(tx + tw / 2, ty);
            Raylib.DrawTriangle(p1, p2, p3, treeColor);
        }
    }

    /// <summary>
    /// Hills - rolling terrain with snow caps.
    /// </summary>
    private static void RenderHills(float px, float py, float size, int worldX, int worldY)
    {
        // Draw 2-3 overlapping hill mounds
        var hillColor = new Color(90, 105, 115, 80);
        var snowCapColor = new Color(255, 255, 255, 50);

        int hillCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 1) * 2);
        for (int i = 0; i < hillCount; i++)
        {
            float hx = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i * 3) * 0.6f);
            float hy = py + size * (0.5f + RenderUtils.SeededRandom(worldX, worldY, i * 3 + 1) * 0.3f);
            float hw = size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, i * 3 + 2) * 0.2f);
            float hh = size * (0.15f + RenderUtils.SeededRandom(worldX, worldY, i + 10) * 0.1f);

            // Draw hill as ellipse
            Raylib.DrawEllipse((int)hx, (int)hy, hw / 2, hh, hillColor);

            // Snow cap on top
            Raylib.DrawEllipse((int)hx, (int)(hy - hh * 0.7f), hw * 0.3f, hh * 0.3f, snowCapColor);
        }

        // Contour lines
        var contourColor = new Color(70, 85, 95, 50);
        for (int i = 0; i < 2; i++)
        {
            float y = py + size * (0.6f + i * 0.15f);
            float x1 = px + size * 0.1f;
            float x2 = px + size * (0.5f + RenderUtils.SeededRandom(worldX, worldY, i + 20) * 0.4f);
            Raylib.DrawLine((int)x1, (int)y, (int)x2, (int)y, contourColor);
        }
    }

    /// <summary>
    /// Rock/Mountain - angular boulders and peaks.
    /// </summary>
    private static void RenderRock(float px, float py, float size, int worldX, int worldY, bool isMountain)
    {
        var rockColor = isMountain
            ? new Color(50, 50, 55, 120)
            : new Color(80, 80, 85, 100);
        var shadowColor = new Color(30, 30, 35, 80);
        var highlightColor = new Color(255, 255, 255, 30);

        // Draw 3-4 angular boulders
        int boulderCount = 3 + (int)(RenderUtils.SeededRandom(worldX, worldY, 1) * 2);
        for (int i = 0; i < boulderCount; i++)
        {
            float bx = px + size * (0.15f + RenderUtils.SeededRandom(worldX, worldY, i * 4) * 0.7f);
            float by = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i * 4 + 1) * 0.6f);
            float bw = size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 4 + 2) * 0.1f);
            float bh = size * (0.08f + RenderUtils.SeededRandom(worldX, worldY, i * 4 + 3) * 0.08f);

            // Draw boulder as irregular polygon (simplified to rectangle)
            Raylib.DrawRectangle((int)(bx - bw / 2), (int)(by - bh), (int)bw, (int)bh, rockColor);

            // Shadow
            Raylib.DrawRectangle((int)(bx - bw / 2 + 2), (int)by, (int)bw, (int)(bh * 0.2f), shadowColor);

            // Highlight
            Raylib.DrawLine((int)(bx - bw / 2), (int)(by - bh), (int)(bx + bw / 3), (int)(by - bh), highlightColor);
        }

        // Mountain peak silhouettes
        if (isMountain)
        {
            var peakColor = new Color(35, 35, 40, 150);
            for (int i = 0; i < 2; i++)
            {
                float peakX = px + size * (0.3f + i * 0.4f);
                float peakY = py + size * 0.7f;
                float peakH = size * (0.25f + RenderUtils.SeededRandom(worldX, worldY, i + 50) * 0.15f);
                float peakW = peakH * 0.8f;

                Vector2 p1 = new(peakX, peakY - peakH);
                Vector2 p2 = new(peakX - peakW / 2, peakY);
                Vector2 p3 = new(peakX + peakW / 2, peakY);
                Raylib.DrawTriangle(p1, p2, p3, peakColor);

                // Snow cap
                var snowColor = new Color(255, 255, 255, 60);
                float capH = peakH * 0.3f;
                Vector2 s1 = new(peakX, peakY - peakH);
                Vector2 s2 = new(peakX - peakW * 0.2f, peakY - peakH + capH);
                Vector2 s3 = new(peakX + peakW * 0.2f, peakY - peakH + capH);
                Raylib.DrawTriangle(s1, s2, s3, snowColor);
            }
        }
    }

    /// <summary>
    /// Marsh - frozen wetland with cattails and dead reeds.
    /// </summary>
    private static void RenderMarsh(float px, float py, float size, int worldX, int worldY)
    {
        // Ice patches
        var iceColor = new Color(150, 170, 180, 40);
        int patchCount = 3 + (int)(RenderUtils.SeededRandom(worldX, worldY, 1) * 2);
        for (int i = 0; i < patchCount; i++)
        {
            float ix = px + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 2) * 0.8f);
            float iy = py + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 2 + 1) * 0.8f);
            float ir = size * (0.05f + RenderUtils.SeededRandom(worldX, worldY, i + 10) * 0.04f);
            Raylib.DrawCircle((int)ix, (int)iy, ir, iceColor);
        }

        // Cattail clusters
        var reedColor = new Color(70, 80, 60, 100);
        var headColor = new Color(50, 40, 30, 120);
        int clusterCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 20) * 2);
        for (int i = 0; i < clusterCount; i++)
        {
            float cx = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 21) * 0.6f);
            float cy = py + size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, i + 22) * 0.5f);

            // Draw 2-3 reeds per cluster
            for (int j = 0; j < 3; j++)
            {
                float rx = cx + (j - 1) * size * 0.02f;
                float rh = size * (0.06f + RenderUtils.SeededRandom(worldX, worldY, i * 3 + j + 30) * 0.03f);
                Raylib.DrawLine((int)rx, (int)cy, (int)rx, (int)(cy - rh), reedColor);

                // Cattail head
                Raylib.DrawRectangle((int)(rx - 1), (int)(cy - rh - size * 0.015f), 3, (int)(size * 0.015f), headColor);
            }
        }

        // Dead reed patches (horizontal lines)
        var deadReedColor = new Color(80, 70, 60, 50);
        for (int i = 0; i < 2; i++)
        {
            float y = py + size * (0.7f + RenderUtils.SeededRandom(worldX, worldY, i + 40) * 0.2f);
            float x1 = px + size * RenderUtils.SeededRandom(worldX, worldY, i + 41) * 0.3f;
            float x2 = x1 + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i + 42) * 0.15f);
            Raylib.DrawLine((int)x1, (int)y, (int)x2, (int)y, deadReedColor);
        }
    }

    /// <summary>
    /// Helper to draw an arc (partial circle).
    /// </summary>
    private static void DrawArc(float cx, float cy, float radius, float startAngle, float endAngle, Color color, int thickness)
    {
        int segments = 16;
        float angleStep = (endAngle - startAngle) / segments;

        for (int i = 0; i < segments; i++)
        {
            float a1 = startAngle + i * angleStep;
            float a2 = startAngle + (i + 1) * angleStep;

            float x1 = cx + MathF.Cos(a1) * radius;
            float y1 = cy + MathF.Sin(a1) * radius;
            float x2 = cx + MathF.Cos(a2) * radius;
            float y2 = cy + MathF.Sin(a2) * radius;

            Raylib.DrawLineEx(new Vector2(x1, y1), new Vector2(x2, y2), thickness, color);
        }
    }
}
