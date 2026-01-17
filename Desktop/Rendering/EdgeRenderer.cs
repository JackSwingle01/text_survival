using Raylib_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Renders edges between tiles (rivers, cliffs, trails).
/// </summary>
public static class EdgeRenderer
{
    // Edge colors
    private static readonly Color RiverColor = new(100, 140, 170, 180);
    private static readonly Color CliffColor = new(80, 70, 60, 200);
    private static readonly Color TrailColor = new(140, 130, 110, 120);
    private static readonly Color HazardColor = new(180, 100, 60, 150);

    /// <summary>
    /// Render all edges between visible tiles.
    /// </summary>
    public static void RenderEdges(GameContext ctx, Camera camera, float timeFactor)
    {
        var map = ctx.Map;

        foreach (var (worldX, worldY) in camera.GetVisibleTiles())
        {
            if (!map.IsValidPosition(worldX, worldY))
                continue;

            // Check each edge direction
            RenderEdge(ctx, camera, worldX, worldY, Direction.North, timeFactor);
            RenderEdge(ctx, camera, worldX, worldY, Direction.East, timeFactor);
            RenderEdge(ctx, camera, worldX, worldY, Direction.South, timeFactor);
            RenderEdge(ctx, camera, worldX, worldY, Direction.West, timeFactor);
        }
    }

    /// <summary>
    /// Render an edge from one tile in a given direction.
    /// </summary>
    private static void RenderEdge(GameContext ctx, Camera camera, int x, int y, Direction dir, float timeFactor)
    {
        var map = ctx.Map;

        // Get neighbor position
        int nx = x + dir switch { Direction.East => 1, Direction.West => -1, _ => 0 };
        int ny = y + dir switch { Direction.South => 1, Direction.North => -1, _ => 0 };

        // Check if both tiles are visible
        if (!map.IsValidPosition(nx, ny))
            return;

        var vis1 = map.GetVisibility(x, y);
        var vis2 = map.GetVisibility(nx, ny);
        if (vis1 == Environments.Grid.TileVisibility.Unexplored || vis2 == Environments.Grid.TileVisibility.Unexplored)
            return;

        // Get locations
        var loc1 = map.GetLocationAt(x, y);
        var loc2 = map.GetLocationAt(nx, ny);
        if (loc1 == null || loc2 == null)
            return;

        // Calculate edge position
        var pos1 = camera.WorldToScreen(x, y);
        var pos2 = camera.WorldToScreen(nx, ny);
        float tileSize = camera.TileSize;

        // Edge midpoint and orientation
        float edgeX, edgeY;
        bool isHorizontal = dir == Direction.East || dir == Direction.West;

        if (isHorizontal)
        {
            edgeX = (pos1.X + pos2.X + tileSize) / 2;
            edgeY = pos1.Y + tileSize / 2;
        }
        else
        {
            edgeX = pos1.X + tileSize / 2;
            edgeY = (pos1.Y + pos2.Y + tileSize) / 2;
        }

        // Check for river
        if (HasRiver(loc1, loc2))
        {
            DrawRiver(edgeX, edgeY, tileSize, isHorizontal, x, y);
        }

        // Check for cliff/climb
        bool hasCliff = HasCliff(loc1, loc2, dir);
        if (hasCliff)
        {
            DrawCliff(edgeX, edgeY, tileSize, isHorizontal, dir);
        }

        // Check for trail
        if (HasTrail(loc1, loc2))
        {
            DrawTrail(edgeX, edgeY, tileSize, isHorizontal, x, y);
        }
    }

    /// <summary>
    /// Check if there's a river between two locations.
    /// </summary>
    private static bool HasRiver(Environments.Location loc1, Environments.Location loc2)
    {
        // River exists when one terrain is water and the other isn't
        bool is1Water = loc1.Terrain.ToString().Contains("Water");
        bool is2Water = loc2.Terrain.ToString().Contains("Water");
        return is1Water != is2Water;
    }

    /// <summary>
    /// Check if there's a cliff/climb between two locations.
    /// </summary>
    private static bool HasCliff(Environments.Location loc1, Environments.Location loc2, Direction dir)
    {
        // Cliff exists between different elevation terrains
        var terrain1 = loc1.Terrain.ToString();
        var terrain2 = loc2.Terrain.ToString();

        bool is1High = terrain1 == "Mountain" || terrain1 == "Hills" || terrain1 == "Rock";
        bool is2High = terrain2 == "Mountain" || terrain2 == "Hills" || terrain2 == "Rock";

        // Only show cliff if one is high and other is low
        if (is1High == is2High)
            return false;

        // Show cliff on the high side going down
        return is1High;
    }

    /// <summary>
    /// Check if there's a trail between two locations.
    /// </summary>
    private static bool HasTrail(Environments.Location loc1, Environments.Location loc2)
    {
        // For now, no trail system - can be expanded later
        return false;
    }

    /// <summary>
    /// Draw a river edge.
    /// </summary>
    private static void DrawRiver(float cx, float cy, float tileSize, bool isHorizontal, int worldX, int worldY)
    {
        float length = tileSize * 0.8f;
        float width = tileSize * 0.1f;

        // Wavy line using sine wave
        int segments = 12;
        float waveAmp = tileSize * 0.03f;

        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float wave = MathF.Sin(t * MathF.PI * 3 + RenderUtils.SeededRandom(worldX, worldY, 100)) * waveAmp;

            if (isHorizontal)
            {
                points[i] = new Vector2(
                    cx - length / 2 + t * length,
                    cy + wave);
            }
            else
            {
                points[i] = new Vector2(
                    cx + wave,
                    cy - length / 2 + t * length);
            }
        }

        // Draw river line
        for (int i = 0; i < segments; i++)
        {
            Raylib.DrawLineEx(points[i], points[i + 1], width, RiverColor);
        }

        // Ice shimmer highlights
        var shimmerColor = new Color(200, 220, 240, 60);
        for (int i = 0; i < 2; i++)
        {
            float t = 0.3f + i * 0.4f;
            int idx = (int)(t * segments);
            if (idx < segments)
            {
                Raylib.DrawCircle((int)points[idx].X, (int)points[idx].Y, width * 0.3f, shimmerColor);
            }
        }
    }

    /// <summary>
    /// Draw a cliff edge.
    /// </summary>
    private static void DrawCliff(float cx, float cy, float tileSize, bool isHorizontal, Direction dir)
    {
        float length = tileSize * 0.6f;
        float height = tileSize * 0.08f;

        Vector2 start, end;
        if (isHorizontal)
        {
            start = new Vector2(cx, cy - length / 2);
            end = new Vector2(cx, cy + length / 2);
        }
        else
        {
            start = new Vector2(cx - length / 2, cy);
            end = new Vector2(cx + length / 2, cy);
        }

        // Draw cliff shadow
        var shadowColor = new Color(40, 35, 30, 150);
        Vector2 shadowOffset = new(2, 2);
        Raylib.DrawLineEx(start + shadowOffset, end + shadowOffset, height, shadowColor);

        // Draw cliff line
        Raylib.DrawLineEx(start, end, height, CliffColor);

        // Draw hazard stripes
        var stripeColor = new Color(200, 150, 100, 100);
        int stripeCount = 3;
        for (int i = 0; i < stripeCount; i++)
        {
            float t = (i + 0.5f) / stripeCount;
            Vector2 pos = Vector2.Lerp(start, end, t);

            // Small diagonal lines to indicate hazard
            float stripeLen = tileSize * 0.02f;
            Raylib.DrawLine(
                (int)(pos.X - stripeLen), (int)(pos.Y - stripeLen),
                (int)(pos.X + stripeLen), (int)(pos.Y + stripeLen),
                stripeColor);
        }
    }

    /// <summary>
    /// Draw a trail edge.
    /// </summary>
    private static void DrawTrail(float cx, float cy, float tileSize, bool isHorizontal, int worldX, int worldY)
    {
        float length = tileSize * 0.7f;
        float width = tileSize * 0.04f;

        Vector2 start, end;
        if (isHorizontal)
        {
            start = new Vector2(cx - length / 2, cy);
            end = new Vector2(cx + length / 2, cy);
        }
        else
        {
            start = new Vector2(cx, cy - length / 2);
            end = new Vector2(cx, cy + length / 2);
        }

        // Draw worn path
        Raylib.DrawLineEx(start, end, width, TrailColor);

        // Footprint marks
        var footprintColor = new Color(100, 90, 80, 80);
        for (int i = 0; i < 3; i++)
        {
            float t = 0.2f + i * 0.3f;
            Vector2 pos = Vector2.Lerp(start, end, t);
            Raylib.DrawCircle((int)pos.X, (int)pos.Y, width * 0.3f, footprintColor);
        }
    }

    /// <summary>
    /// Direction enum for edges.
    /// </summary>
    private enum Direction
    {
        North,
        East,
        South,
        West
    }
}
