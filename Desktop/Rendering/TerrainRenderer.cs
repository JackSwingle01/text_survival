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
        var treeColor = new Color(15, 35, 25, 178);  // Adjusted to match canvas rgba(15,35,25,0.7)

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

        // Snow on some trees (white highlights) - adjusted to match canvas rgba(255,255,255,0.15)
        var snowColor = new Color(255, 255, 255, 38);
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

        // Central snow patch (irregular blob instead of perfect circle)
        var snowColor = new Color(255, 255, 255, 65);
        float snowRadius = size * 0.12f;
        Vector2[] blobPoints = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            float angle = i * MathF.PI * 2 / 8;
            float radiusVariation = snowRadius * (0.8f + RenderUtils.SeededRandom(worldX, worldY, i + 60) * 0.4f);
            blobPoints[i] = new Vector2(
                centerX + MathF.Cos(angle) * radiusVariation,
                centerY + MathF.Sin(angle) * radiusVariation
            );
        }
        RenderUtils.DrawPolygon(blobPoints, snowColor);

        // Ice shimmer patches (rotated ellipses, semi-transparent)
        var shimmerColor = new Color(200, 220, 240, 30);
        for (int i = 0; i < 3; i++)
        {
            float sx = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 70) * 0.6f);
            float sy = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 71) * 0.6f);
            float radiusX = size * (0.06f + RenderUtils.SeededRandom(worldX, worldY, i + 72) * 0.04f);
            float radiusY = radiusX * 0.6f;
            float rotation = RenderUtils.SeededRandom(worldX, worldY, i + 73) * MathF.PI;
            RenderUtils.DrawRotatedEllipse(sx, sy, radiusX, radiusY, rotation, shimmerColor);
        }

        // Radiating cracks (jagged multi-segment lines)
        var crackColor = isDeep
            ? new Color(30, 55, 80, 115)
            : new Color(50, 80, 110, 90);

        int crackCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 30) * 2);
        for (int i = 0; i < crackCount; i++)
        {
            float baseAngle = RenderUtils.SeededRandom(worldX, worldY, i + 31) * MathF.PI * 2;
            Vector2 prevPoint = new(centerX, centerY);

            // Multi-segment crack (3 segments)
            for (int j = 0; j < 3; j++)
            {
                float angleVariation = (RenderUtils.SeededRandom(worldX, worldY, i * 10 + j + 80) - 0.5f) * 0.4f;
                float segmentAngle = baseAngle + angleVariation;
                float segmentLen = size * (0.08f + RenderUtils.SeededRandom(worldX, worldY, i * 10 + j + 81) * 0.08f);

                Vector2 nextPoint = new(
                    prevPoint.X + MathF.Cos(segmentAngle) * segmentLen,
                    prevPoint.Y + MathF.Sin(segmentAngle) * segmentLen
                );

                Raylib.DrawLineEx(prevPoint, nextPoint, Math.Max(1, size * 0.01f), crackColor);
                prevPoint = nextPoint;
            }
        }
    }

    /// <summary>
    /// Plain - tundra with lichen, snow drifts, and sparse vegetation.
    /// </summary>
    private static void RenderPlain(float px, float py, float size, int worldX, int worldY)
    {
        // Lichen patches - clustered for more natural appearance
        var lichenColors = new[]
        {
            new Color(100, 100, 70, 90),  // Adjusted to match canvas
            new Color(120, 110, 80, 85),
            new Color(90, 95, 65, 95)
        };

        int clusterCount = 1 + (int)(RenderUtils.SeededRandom(worldX, worldY, 1) * 2);  // 1-2 clusters
        for (int c = 0; c < clusterCount; c++)
        {
            float clusterX = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, c * 20) * 0.6f);
            float clusterY = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, c * 20 + 1) * 0.6f);

            // 4-8 spots per cluster
            int spotCount = 4 + (int)(RenderUtils.SeededRandom(worldX, worldY, c * 20 + 2) * 5);
            for (int i = 0; i < spotCount; i++)
            {
                float lx = clusterX + (RenderUtils.SeededRandom(worldX, worldY, c * 100 + i * 3) - 0.5f) * size * 0.15f;
                float ly = clusterY + (RenderUtils.SeededRandom(worldX, worldY, c * 100 + i * 3 + 1) - 0.5f) * size * 0.15f;
                float lr = size * (0.015f + RenderUtils.SeededRandom(worldX, worldY, c * 100 + i * 3 + 2) * 0.025f);
                var color = lichenColors[i % lichenColors.Length];
                Raylib.DrawCircle((int)lx, (int)ly, lr, color);
            }
        }

        // Curved snow drifts (bezier curves)
        var driftColor = new Color(255, 255, 255, 25);
        int driftCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 20) * 2);
        for (int i = 0; i < driftCount; i++)
        {
            float startX = px + size * RenderUtils.SeededRandom(worldX, worldY, i + 21) * 0.3f;
            float startY = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 22) * 0.6f);
            float endX = startX + size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, i + 23) * 0.4f);
            float endY = startY + (RenderUtils.SeededRandom(worldX, worldY, i + 24) - 0.5f) * size * 0.1f;

            // Control points for curve
            float midX = (startX + endX) / 2;
            float controlY = startY - size * 0.05f;

            Vector2 p0 = new(startX, startY);
            Vector2 p1 = new(midX, controlY);
            Vector2 p2 = new(midX, controlY);
            Vector2 p3 = new(endX, endY);

            RenderUtils.DrawBezierCurve(p0, p1, p2, p3, driftColor, Math.Max(1, (int)(size * 0.012f)), 8);
        }

        // Tussock grass with radiating blades
        var grassColor = new Color(140, 120, 80, 128);  // Adjusted to match canvas
        int grassCount = 3 + (int)(RenderUtils.SeededRandom(worldX, worldY, 30) * 3);
        for (int i = 0; i < grassCount; i++)
        {
            float gx = px + size * (0.15f + RenderUtils.SeededRandom(worldX, worldY, i + 31) * 0.7f);
            float gy = py + size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, i + 32) * 0.6f);
            float gh = size * (0.03f + RenderUtils.SeededRandom(worldX, worldY, i + 33) * 0.02f);

            // 5 blades radiating from center
            for (int j = 0; j < 5; j++)
            {
                float bladeAngle = (j - 2) * 0.3f;  // Spread blades
                float bladeEndX = gx + MathF.Sin(bladeAngle) * gh * 0.3f;
                float bladeEndY = gy - gh * MathF.Cos(bladeAngle);
                Raylib.DrawLineEx(new Vector2(gx, gy), new Vector2(bladeEndX, bladeEndY), 1, grassColor);
            }
        }

        // Low shrubs with branching twigs
        var shrubColor = new Color(60, 50, 40, 100);
        int shrubCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 40) * 2);
        for (int i = 0; i < shrubCount; i++)
        {
            float sx = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 41) * 0.6f);
            float sy = py + size * (0.4f + RenderUtils.SeededRandom(worldX, worldY, i + 42) * 0.4f);
            float sh = size * 0.04f;

            // Main stem
            Raylib.DrawLineEx(new Vector2(sx, sy), new Vector2(sx, sy - sh), 1.5f, shrubColor);

            // 4 alternating branches
            for (int j = 0; j < 4; j++)
            {
                float branchY = sy - sh * (0.3f + j * 0.2f);
                float branchAngle = (j % 2 == 0 ? -0.6f : 0.6f);
                float branchLen = sh * 0.4f;
                float branchEndX = sx + MathF.Sin(branchAngle) * branchLen;
                float branchEndY = branchY - MathF.Cos(branchAngle) * branchLen * 0.3f;
                Raylib.DrawLineEx(new Vector2(sx, branchY), new Vector2(branchEndX, branchEndY), 1, shrubColor);
            }
        }

        // Snow sparkles (small bright squares)
        var sparkleColor = new Color(255, 255, 255, 180);
        int sparkleCount = 3 + (int)(RenderUtils.SeededRandom(worldX, worldY, 50) * 4);
        for (int i = 0; i < sparkleCount; i++)
        {
            float sparkleX = px + size * RenderUtils.SeededRandom(worldX, worldY, i + 51) * 1.0f;
            float sparkleY = py + size * RenderUtils.SeededRandom(worldX, worldY, i + 52) * 1.0f;
            int sparkleSize = (int)(size * 0.008f);
            Raylib.DrawRectangle((int)sparkleX, (int)sparkleY, Math.Max(1, sparkleSize), Math.Max(1, sparkleSize), sparkleColor);
        }
    }

    /// <summary>
    /// Clearing - open area with dirt patches and edge trees.
    /// </summary>
    private static void RenderClearing(float px, float py, float size, int worldX, int worldY)
    {
        // Dirt patches (rotated ellipses for more natural shapes)
        var dirtColor = new Color(100, 90, 80, 40);
        int patchCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 1) * 2);
        for (int i = 0; i < patchCount; i++)
        {
            float dx = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i * 2) * 0.6f);
            float dy = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i * 2 + 1) * 0.6f);
            float drX = size * (0.08f + RenderUtils.SeededRandom(worldX, worldY, i + 10) * 0.06f);
            float drY = drX * (0.6f + RenderUtils.SeededRandom(worldX, worldY, i + 11) * 0.4f);
            float rotation = RenderUtils.SeededRandom(worldX, worldY, i + 12) * MathF.PI;
            RenderUtils.DrawRotatedEllipse(dx, dy, drX, drY, rotation, dirtColor);
        }

        // Stumps with growth rings
        var stumpColor = new Color(60, 50, 40, 80);
        var ringColor = new Color(50, 40, 30, 100);
        int stumpCount = 1 + (int)(RenderUtils.SeededRandom(worldX, worldY, 20) * 2);
        for (int i = 0; i < stumpCount; i++)
        {
            float sx = px + size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, i + 21) * 0.4f);
            float sy = py + size * (0.4f + RenderUtils.SeededRandom(worldX, worldY, i + 22) * 0.3f);
            float sr = size * (0.025f + RenderUtils.SeededRandom(worldX, worldY, i + 23) * 0.02f);

            // Outer stump
            Raylib.DrawCircle((int)sx, (int)sy, sr, stumpColor);

            // Inner growth ring
            Raylib.DrawCircle((int)sx, (int)sy, sr * 0.6f, ringColor);

            // Ring detail lines
            Raylib.DrawRing(new Vector2(sx, sy), sr * 0.4f, sr * 0.45f, 0, 360, 16, ringColor);
        }

        // Fallen branches (angled lines with varied rotation)
        var branchColor = new Color(70, 60, 50, 90);
        int branchCount = 3 + (int)(RenderUtils.SeededRandom(worldX, worldY, 30) * 3);
        for (int i = 0; i < branchCount; i++)
        {
            float bx = px + size * (0.15f + RenderUtils.SeededRandom(worldX, worldY, i + 31) * 0.7f);
            float by = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 32) * 0.6f);
            float bLen = size * (0.05f + RenderUtils.SeededRandom(worldX, worldY, i + 33) * 0.08f);
            float bAngle = RenderUtils.SeededRandom(worldX, worldY, i + 34) * MathF.PI * 2;

            float endX = bx + MathF.Cos(bAngle) * bLen;
            float endY = by + MathF.Sin(bAngle) * bLen;

            Raylib.DrawLineEx(new Vector2(bx, by), new Vector2(endX, endY), Math.Max(1.5f, size * 0.01f), branchColor);

            // Small side twigs
            float twigX = bx + MathF.Cos(bAngle) * bLen * 0.5f;
            float twigY = by + MathF.Sin(bAngle) * bLen * 0.5f;
            float twigAngle = bAngle + MathF.PI / 2;
            float twigLen = bLen * 0.2f;
            Raylib.DrawLineEx(
                new Vector2(twigX, twigY),
                new Vector2(twigX + MathF.Cos(twigAngle) * twigLen, twigY + MathF.Sin(twigAngle) * twigLen),
                1,
                branchColor
            );
        }

        // Edge tree hints (partial triangles at borders)
        var treeColor = new Color(15, 35, 25, 120);
        var treeEdgeColor = new Color(15, 35, 25, 80);

        // Left edge trees
        for (int i = 0; i < 2; i++)
        {
            float tx = px + size * 0.05f;
            float ty = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 40) * 0.6f);
            float th = size * (0.08f + RenderUtils.SeededRandom(worldX, worldY, i + 41) * 0.04f);
            float tw = th * 0.5f;

            Vector2 p1 = new(tx, ty - th);
            Vector2 p2 = new(tx - tw / 2, ty);
            Vector2 p3 = new(tx + tw / 2, ty);
            Raylib.DrawTriangle(p1, p2, p3, treeColor);
        }

        // Right edge trees (partial, cut off by border)
        for (int i = 0; i < 2; i++)
        {
            float tx = px + size * 0.95f;
            float ty = py + size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, i + 50) * 0.4f);
            float th = size * 0.08f;
            float tw = th * 0.5f;

            // Draw partial triangle (appears cut off by edge)
            Vector2 p1 = new(tx, ty - th * 0.7f);
            Vector2 p2 = new(tx - tw * 0.3f, ty);
            Vector2 p3 = new(px + size, ty - th * 0.3f);  // Extends past border
            Raylib.DrawTriangle(p1, p2, p3, treeEdgeColor);
        }
    }

    /// <summary>
    /// Hills - rolling terrain with snow caps.
    /// </summary>
    private static void RenderHills(float px, float py, float size, int worldX, int worldY)
    {
        var hillColor = new Color(90, 105, 115, 80);
        var hillShadowColor = new Color(70, 85, 95, 60);
        var rockColor = new Color(80, 85, 90, 70);
        var grassColor = new Color(100, 110, 90, 60);

        // Draw 3 overlapping quadratic mounds (back-to-front for depth)
        int hillCount = 3;
        for (int i = hillCount - 1; i >= 0; i--)  // Back-to-front
        {
            float hx = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i * 3) * 0.6f);
            float baseY = py + size * (0.6f + RenderUtils.SeededRandom(worldX, worldY, i * 3 + 1) * 0.2f);
            float hw = size * (0.35f + RenderUtils.SeededRandom(worldX, worldY, i * 3 + 2) * 0.25f);
            float peakY = baseY - size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 10) * 0.15f);

            // Shadow underneath mound
            RenderUtils.DrawQuadraticMound(
                hx - hw / 2 + size * 0.01f,
                hx + hw / 2 + size * 0.01f,
                baseY + size * 0.015f,
                peakY + size * 0.015f,
                hillShadowColor
            );

            // Main mound
            RenderUtils.DrawQuadraticMound(hx - hw / 2, hx + hw / 2, baseY, peakY, hillColor);

            // Exposed rock patches on slopes (rotated ellipses)
            if (RenderUtils.SeededRandom(worldX, worldY, i + 20) > 0.5f)
            {
                float rockX = hx + (RenderUtils.SeededRandom(worldX, worldY, i + 21) - 0.5f) * hw * 0.5f;
                float rockY = (peakY + baseY) / 2 + (RenderUtils.SeededRandom(worldX, worldY, i + 22) - 0.5f) * size * 0.1f;
                float rockW = size * (0.03f + RenderUtils.SeededRandom(worldX, worldY, i + 23) * 0.03f);
                float rockH = rockW * 0.6f;
                float rockRotation = RenderUtils.SeededRandom(worldX, worldY, i + 24) * MathF.PI * 0.5f;
                RenderUtils.DrawRotatedEllipse(rockX, rockY, rockW, rockH, rockRotation, rockColor);
            }

            // Grass tufts on lower slopes
            int tufts = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, i + 30) * 3);
            for (int j = 0; j < tufts; j++)
            {
                float tuftX = hx + (RenderUtils.SeededRandom(worldX, worldY, i * 10 + j * 2 + 31) - 0.5f) * hw * 0.7f;
                float tuftY = baseY - size * (0.05f + RenderUtils.SeededRandom(worldX, worldY, i * 10 + j * 2 + 32) * 0.1f);
                float tuftH = size * 0.02f;

                // 3 blades per tuft
                for (int b = 0; b < 3; b++)
                {
                    float bladeAngle = (b - 1) * 0.3f;
                    float bladeEndX = tuftX + MathF.Sin(bladeAngle) * tuftH * 0.4f;
                    float bladeEndY = tuftY - tuftH;
                    Raylib.DrawLineEx(new Vector2(tuftX, tuftY), new Vector2(bladeEndX, bladeEndY), 1, grassColor);
                }
            }
        }

        // Curved contour hints (bezier approximation)
        var contourColor = new Color(70, 85, 95, 50);
        for (int i = 0; i < 2; i++)
        {
            float startX = px + size * 0.1f;
            float y = py + size * (0.65f + i * 0.15f);
            float endX = px + size * (0.5f + RenderUtils.SeededRandom(worldX, worldY, i + 40) * 0.4f);
            float controlY = y - size * (0.02f + RenderUtils.SeededRandom(worldX, worldY, i + 41) * 0.02f);

            Vector2 p0 = new(startX, y);
            Vector2 p1 = new((startX + endX) / 2, controlY);
            Vector2 p2 = new((startX + endX) / 2, controlY);
            Vector2 p3 = new(endX, y + (RenderUtils.SeededRandom(worldX, worldY, i + 42) - 0.5f) * size * 0.03f);

            RenderUtils.DrawBezierCurve(p0, p1, p2, p3, contourColor, 1, 8);
        }
    }

    /// <summary>
    /// Rock/Mountain - angular boulders and peaks.
    /// </summary>
    private static void RenderRock(float px, float py, float size, int worldX, int worldY, bool isMountain)
    {
        var rockBaseColor = isMountain
            ? new Color(50, 50, 55, 120)
            : new Color(80, 80, 85, 100);
        var rockSideColor = isMountain
            ? new Color(60, 60, 65, 110)
            : new Color(90, 90, 95, 95);
        var rockTopColor = isMountain
            ? new Color(70, 70, 75, 100)
            : new Color(100, 100, 105, 90);
        var shadowColor = new Color(30, 30, 35, 100);
        var highlightColor = new Color(255, 255, 255, 40);

        // Draw 3-4 multi-layer boulders
        int boulderCount = 3 + (int)(RenderUtils.SeededRandom(worldX, worldY, 1) * 2);
        for (int i = 0; i < boulderCount; i++)
        {
            float bx = px + size * (0.15f + RenderUtils.SeededRandom(worldX, worldY, i * 4) * 0.7f);
            float by = py + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i * 4 + 1) * 0.6f);
            float bw = size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 4 + 2) * 0.1f);
            float bh = size * (0.08f + RenderUtils.SeededRandom(worldX, worldY, i * 4 + 3) * 0.08f);

            // Shadow layer (underneath, offset slightly)
            Vector2 shadowP1 = new(bx - bw / 2 + size * 0.01f, by + size * 0.01f);
            Vector2 shadowP2 = new(bx + bw / 2 + size * 0.01f, by + size * 0.01f);
            Vector2 shadowP3 = new(bx + bw / 3 + size * 0.01f, by - bh * 0.5f + size * 0.01f);
            Vector2 shadowP4 = new(bx - bw / 3 + size * 0.01f, by - bh * 0.5f + size * 0.01f);
            RenderUtils.DrawQuadrilateral(shadowP1, shadowP2, shadowP3, shadowP4, shadowColor);

            // Base/side layer (darker face)
            Vector2 sideP1 = new(bx - bw / 2, by);
            Vector2 sideP2 = new(bx + bw / 2, by);
            Vector2 sideP3 = new(bx + bw / 3, by - bh * 0.7f);
            Vector2 sideP4 = new(bx - bw / 3, by - bh * 0.7f);
            RenderUtils.DrawQuadrilateral(sideP1, sideP2, sideP3, sideP4, rockBaseColor);

            // Top face (lighter, angled)
            Vector2 topP1 = new(bx - bw / 3, by - bh * 0.7f);
            Vector2 topP2 = new(bx + bw / 3, by - bh * 0.7f);
            Vector2 topP3 = new(bx + bw * 0.2f, by - bh);
            Vector2 topP4 = new(bx - bw * 0.2f, by - bh);
            RenderUtils.DrawQuadrilateral(topP1, topP2, topP3, topP4, rockTopColor);

            // Highlight edge on top
            Raylib.DrawLineEx(topP4, topP3, Math.Max(1, size * 0.008f), highlightColor);

            // Snow cap on top (if significant boulder)
            if (bw > size * 0.12f)
            {
                var snowColor = new Color(255, 255, 255, 50);
                float snowW = bw * 0.15f;
                Raylib.DrawEllipse((int)bx, (int)(by - bh * 0.9f), snowW, snowW * 0.5f, snowColor);
            }
        }

        // Cracks between boulders (dark irregular lines)
        var crackColor = new Color(20, 20, 25, 120);
        int crackCount = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, 60) * 2);
        for (int i = 0; i < crackCount; i++)
        {
            float cx1 = px + size * (0.2f + RenderUtils.SeededRandom(worldX, worldY, i + 61) * 0.6f);
            float cy1 = py + size * (0.3f + RenderUtils.SeededRandom(worldX, worldY, i + 62) * 0.5f);
            float cx2 = cx1 + (RenderUtils.SeededRandom(worldX, worldY, i + 63) - 0.5f) * size * 0.15f;
            float cy2 = cy1 + size * (0.05f + RenderUtils.SeededRandom(worldX, worldY, i + 64) * 0.1f);
            Raylib.DrawLineEx(new Vector2(cx1, cy1), new Vector2(cx2, cy2), Math.Max(1, size * 0.008f), crackColor);
        }

        // Gravel scatter at base (small circles)
        var gravelColor = new Color(60, 60, 65, 70);
        int gravelCount = 5 + (int)(RenderUtils.SeededRandom(worldX, worldY, 70) * 4);
        for (int i = 0; i < gravelCount; i++)
        {
            float gx = px + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i + 71) * 0.8f);
            float gy = py + size * (0.7f + RenderUtils.SeededRandom(worldX, worldY, i + 72) * 0.25f);
            float gr = size * (0.008f + RenderUtils.SeededRandom(worldX, worldY, i + 73) * 0.012f);
            Raylib.DrawCircle((int)gx, (int)gy, gr, gravelColor);
        }

        // Mountain peak silhouettes (enhanced)
        if (isMountain)
        {
            var peakColor = new Color(35, 35, 40, 150);
            var peakShadow = new Color(25, 25, 30, 120);

            for (int i = 0; i < 2; i++)
            {
                float peakX = px + size * (0.3f + i * 0.4f);
                float peakY = py + size * 0.7f;
                float peakH = size * (0.25f + RenderUtils.SeededRandom(worldX, worldY, i + 50) * 0.15f);
                float peakW = peakH * 0.8f;

                // Shadow side of peak
                Vector2 shadowPeak1 = new(peakX + size * 0.005f, peakY - peakH + size * 0.005f);
                Vector2 shadowPeak2 = new(peakX - peakW / 2 + size * 0.005f, peakY + size * 0.005f);
                Vector2 shadowPeak3 = new(peakX + peakW / 2 + size * 0.005f, peakY + size * 0.005f);
                Raylib.DrawTriangle(shadowPeak1, shadowPeak2, shadowPeak3, peakShadow);

                // Main peak
                Vector2 p1 = new(peakX, peakY - peakH);
                Vector2 p2 = new(peakX - peakW / 2, peakY);
                Vector2 p3 = new(peakX + peakW / 2, peakY);
                Raylib.DrawTriangle(p1, p2, p3, peakColor);

                // Ridge detail (jagged edge)
                Raylib.DrawLineEx(p2, p1, Math.Max(1, size * 0.008f), highlightColor);

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
        // Ice patches (varied shapes using rotated ellipses)
        var iceColor = new Color(150, 170, 180, 40);
        var iceHighlight = new Color(200, 220, 230, 25);
        int patchCount = 3 + (int)(RenderUtils.SeededRandom(worldX, worldY, 1) * 2);
        for (int i = 0; i < patchCount; i++)
        {
            float ix = px + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 2) * 0.8f);
            float iy = py + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i * 2 + 1) * 0.8f);
            float irX = size * (0.05f + RenderUtils.SeededRandom(worldX, worldY, i + 10) * 0.04f);
            float irY = irX * (0.6f + RenderUtils.SeededRandom(worldX, worldY, i + 11) * 0.4f);
            float rotation = RenderUtils.SeededRandom(worldX, worldY, i + 12) * MathF.PI;

            RenderUtils.DrawRotatedEllipse(ix, iy, irX, irY, rotation, iceColor);

            // Highlight on ice patch
            RenderUtils.DrawRotatedEllipse(ix - irX * 0.2f, iy - irY * 0.2f, irX * 0.4f, irY * 0.3f, rotation, iceHighlight);
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
                float reedAngle = (RenderUtils.SeededRandom(worldX, worldY, i * 3 + j + 31) - 0.5f) * 0.15f;  // Slight bend

                // Reed stem (slightly angled)
                float endX = rx + MathF.Sin(reedAngle) * rh;
                float endY = cy - rh;
                Raylib.DrawLineEx(new Vector2(rx, cy), new Vector2(endX, endY), 1.5f, reedColor);

                // Cattail head (vertical ellipse for sausage shape)
                float headWidth = size * 0.008f;
                float headHeight = size * 0.018f;
                Raylib.DrawEllipse((int)endX, (int)(endY - headHeight / 2), headWidth, headHeight, headColor);
            }
        }

        // Dead reed patches (more varied angles and clustering)
        var deadReedColor = new Color(80, 70, 60, 50);
        var deadReedDark = new Color(60, 55, 45, 60);
        int deadPatchCount = 3 + (int)(RenderUtils.SeededRandom(worldX, worldY, 40) * 3);
        for (int i = 0; i < deadPatchCount; i++)
        {
            float patchX = px + size * (0.1f + RenderUtils.SeededRandom(worldX, worldY, i + 41) * 0.8f);
            float patchY = py + size * (0.6f + RenderUtils.SeededRandom(worldX, worldY, i + 42) * 0.3f);

            // 2-4 reeds per patch
            int reedsInPatch = 2 + (int)(RenderUtils.SeededRandom(worldX, worldY, i + 43) * 3);
            for (int j = 0; j < reedsInPatch; j++)
            {
                float reedX = patchX + (RenderUtils.SeededRandom(worldX, worldY, i * 10 + j + 50) - 0.5f) * size * 0.08f;
                float reedY = patchY + (RenderUtils.SeededRandom(worldX, worldY, i * 10 + j + 51) - 0.5f) * size * 0.04f;
                float reedLen = size * (0.04f + RenderUtils.SeededRandom(worldX, worldY, i * 10 + j + 52) * 0.06f);
                float reedAngle = RenderUtils.SeededRandom(worldX, worldY, i * 10 + j + 53) * MathF.PI * 0.5f - MathF.PI * 0.25f;

                float endX = reedX + MathF.Cos(reedAngle) * reedLen;
                float endY = reedY + MathF.Sin(reedAngle) * reedLen;

                var color = j % 2 == 0 ? deadReedColor : deadReedDark;
                Raylib.DrawLineEx(new Vector2(reedX, reedY), new Vector2(endX, endY), 1, color);
            }
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
