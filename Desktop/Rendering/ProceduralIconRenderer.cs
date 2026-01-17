using Raylib_cs;
using System.Numerics;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Renders icons using procedural Raylib primitives.
/// </summary>
public class ProceduralIconRenderer : IIconRenderer
{
    public Color GetIconColor(string iconName)
    {
        return iconName switch
        {
            "fire" => UIColors.FireOrange,
            "embers" => new Color(160, 96, 48, 255),       // Amber
            "water" => new Color(144, 208, 224, 255),     // Light blue
            "trap_caught" => new Color(100, 220, 100, 255), // Green
            "trap" => new Color(180, 160, 100, 255),       // Tan
            "burrow" => new Color(140, 120, 80, 255),      // Brown
            "shelter" => new Color(180, 140, 80, 255),     // Warm brown
            "snow_shelter" => new Color(200, 220, 240, 255), // Ice blue
            "ice" => new Color(180, 210, 230, 255),        // Light ice blue
            "cache" => new Color(160, 140, 100, 255),      // Tan/brown
            "carcass" => new Color(180, 100, 100, 255),    // Meat red
            "construction" => new Color(150, 150, 150, 255), // Gray
            "bed" => new Color(140, 120, 180, 255),        // Soft purple
            "harvest" => new Color(120, 180, 100, 255),    // Plant green
            "danger" => UIColors.Danger,                    // Red/yellow
            "body" => new Color(150, 130, 130, 255),       // Muted gray-brown
            "loot" => new Color(200, 180, 100, 255),       // Gold
            "ready" => new Color(100, 200, 100, 255),      // Green
            "curing" => new Color(180, 160, 120, 255),     // Tan
            "predator" => new Color(200, 160, 120, 255),   // Warm tan
            "prey" => new Color(160, 180, 140, 255),       // Soft green
            "tracks" => new Color(140, 120, 100, 255),     // Brown
            "forest" => new Color(80, 140, 80, 255),       // Forest green
            "rocks" => new Color(140, 140, 140, 255),      // Gray
            "trail" => new Color(120, 100, 80, 255),       // Brown
            "overlook" => new Color(160, 160, 170, 255),   // Gray-blue
            "thicket" => new Color(90, 130, 70, 255),      // Dark green
            "bones" => new Color(220, 215, 200, 255),      // Off-white
            "grass" => new Color(140, 170, 100, 255),      // Yellow-green
            "nest" => new Color(160, 130, 90, 255),        // Brown
            "webs" => new Color(180, 180, 190, 255),       // Light gray
            "wind_shelter" => new Color(150, 180, 200, 255), // Blue-gray
            _ => new Color(200, 200, 200, 255)             // Default gray
        };
    }

    public void DrawIcon(string iconName, float x, float y, float size, Color tint, bool hasGlow = false)
    {
        float centerX = x + size / 2;
        float centerY = y + size / 2;

        // Draw glow if requested
        if (hasGlow)
        {
            var glowColor = new Color(tint.R, tint.G, tint.B, (byte)40);
            float glowRadius = size * 0.6f;
            Raylib.DrawCircle((int)centerX, (int)centerY, glowRadius, glowColor);
        }

        // Draw icon based on type
        switch (iconName)
        {
            case "fire":
                DrawFire(centerX, centerY, size, tint);
                break;
            case "embers":
                DrawEmbers(centerX, centerY, size, tint);
                break;
            case "water":
                DrawWater(centerX, centerY, size, tint);
                break;
            case "trap_caught":
                DrawTrapCaught(centerX, centerY, size, tint);
                break;
            case "trap":
                DrawTrap(centerX, centerY, size, tint);
                break;
            case "burrow":
                DrawBurrow(centerX, centerY, size, tint);
                break;
            case "shelter":
                DrawShelter(centerX, centerY, size, tint);
                break;
            case "snow_shelter":
                DrawSnowShelter(centerX, centerY, size, tint);
                break;
            case "ice":
                DrawIce(centerX, centerY, size, tint);
                break;
            case "cache":
                DrawCache(centerX, centerY, size, tint);
                break;
            case "carcass":
                DrawCarcass(centerX, centerY, size, tint);
                break;
            case "construction":
                DrawConstruction(centerX, centerY, size, tint);
                break;
            case "bed":
                DrawBed(centerX, centerY, size, tint);
                break;
            case "harvest":
                DrawHarvest(centerX, centerY, size, tint);
                break;
            case "danger":
                DrawDanger(centerX, centerY, size, tint);
                break;
            case "body":
                DrawBody(centerX, centerY, size, tint);
                break;
            case "loot":
                DrawLoot(centerX, centerY, size, tint);
                break;
            case "ready":
                DrawReady(centerX, centerY, size, tint);
                break;
            case "curing":
                DrawCuring(centerX, centerY, size, tint);
                break;
            case "predator":
                DrawPredator(centerX, centerY, size, tint);
                break;
            case "prey":
                DrawPrey(centerX, centerY, size, tint);
                break;
            case "tracks":
                DrawTracks(centerX, centerY, size, tint);
                break;
            case "forest":
                DrawForest(centerX, centerY, size, tint);
                break;
            case "rocks":
                DrawRocks(centerX, centerY, size, tint);
                break;
            case "trail":
                DrawTrail(centerX, centerY, size, tint);
                break;
            case "overlook":
                DrawOverlook(centerX, centerY, size, tint);
                break;
            case "thicket":
                DrawThicket(centerX, centerY, size, tint);
                break;
            case "bones":
                DrawBones(centerX, centerY, size, tint);
                break;
            case "grass":
                DrawGrass(centerX, centerY, size, tint);
                break;
            case "nest":
                DrawNest(centerX, centerY, size, tint);
                break;
            case "webs":
                DrawWebs(centerX, centerY, size, tint);
                break;
            case "wind_shelter":
                DrawWindShelter(centerX, centerY, size, tint);
                break;
            default:
                DrawDefault(centerX, centerY, size, tint);
                break;
        }
    }

    // === Icon Drawing Methods ===

    private static void DrawFire(float cx, float cy, float size, Color tint)
    {
        float s = size * 0.35f;
        float baseY = cy + s * 0.5f;

        // Back flame tongue (left, shorter)
        var backColor = new Color((byte)(tint.R * 0.8f), (byte)(tint.G * 0.6f), (byte)(tint.B * 0.4f), tint.A);
        Raylib.DrawTriangle(
            new Vector2(cx - s * 0.3f, cy - s * 0.5f),
            new Vector2(cx - s * 0.5f, baseY),
            new Vector2(cx - s * 0.1f, baseY),
            backColor);

        // Back flame tongue (right, shorter)
        Raylib.DrawTriangle(
            new Vector2(cx + s * 0.35f, cy - s * 0.4f),
            new Vector2(cx + s * 0.1f, baseY),
            new Vector2(cx + s * 0.55f, baseY),
            backColor);

        // Main center flame (tallest, orange)
        Raylib.DrawTriangle(
            new Vector2(cx, cy - s),
            new Vector2(cx - s * 0.4f, baseY),
            new Vector2(cx + s * 0.4f, baseY),
            tint);

        // Inner bright core (yellow-white)
        var coreColor = new Color((byte)255, (byte)230, (byte)120, tint.A);
        float coreY = cy + s * 0.3f;
        Raylib.DrawTriangle(
            new Vector2(cx, cy - s * 0.4f),
            new Vector2(cx - s * 0.2f, coreY),
            new Vector2(cx + s * 0.2f, coreY),
            coreColor);

        // Hottest center (white-yellow)
        var hotColor = new Color((byte)255, (byte)250, (byte)200, tint.A);
        Raylib.DrawTriangle(
            new Vector2(cx, cy - s * 0.1f),
            new Vector2(cx - s * 0.1f, cy + s * 0.15f),
            new Vector2(cx + s * 0.1f, cy + s * 0.15f),
            hotColor);
    }

    private static void DrawEmbers(float cx, float cy, float size, Color tint)
    {
        float s = size * 0.12f;

        // Dim outer glow
        var glowColor = new Color(tint.R, (byte)(tint.G * 0.5f), (byte)(tint.B * 0.3f), (byte)60);
        Raylib.DrawCircle((int)cx, (int)cy, s * 3f, glowColor);

        // Bright center embers
        var brightColor = new Color((byte)255, (byte)180, (byte)80, tint.A);
        Raylib.DrawCircle((int)cx, (int)cy, s * 1.1f, brightColor);
        Raylib.DrawCircle((int)(cx - s * 1.8f), (int)(cy + s * 0.8f), s * 0.8f, tint);

        // Medium embers
        Raylib.DrawCircle((int)(cx + s * 1.6f), (int)(cy + s * 0.3f), s * 0.7f, tint);
        Raylib.DrawCircle((int)(cx + s * 0.5f), (int)(cy - s * 1.2f), s * 0.6f, tint);

        // Small dim embers
        var dimColor = new Color((byte)(tint.R * 0.7f), (byte)(tint.G * 0.5f), (byte)(tint.B * 0.5f), tint.A);
        Raylib.DrawCircle((int)(cx - s * 0.8f), (int)(cy - s * 0.8f), s * 0.4f, dimColor);
        Raylib.DrawCircle((int)(cx + s * 2.2f), (int)(cy - s * 0.5f), s * 0.35f, dimColor);
    }

    private static void DrawWater(float cx, float cy, float size, Color tint)
    {
        // Blue teardrop
        float s = size * 0.3f;
        // Bottom circle
        Raylib.DrawCircle((int)cx, (int)(cy + s * 0.3f), s * 0.7f, tint);
        // Top triangle to make teardrop
        var v1 = new Vector2(cx, cy - s);
        var v2 = new Vector2(cx - s * 0.5f, cy + s * 0.1f);
        var v3 = new Vector2(cx + s * 0.5f, cy + s * 0.1f);
        Raylib.DrawTriangle(v1, v3, v2, tint);
    }

    private static void DrawTrapCaught(float cx, float cy, float size, Color tint)
    {
        float s = size * 0.25f;
        float loopY = cy - s * 0.3f;

        // Filled snare loop (caught = full)
        Raylib.DrawCircle((int)cx, (int)loopY, s * 0.9f, tint);

        // Trigger stick
        var stickColor = new Color((byte)(tint.R * 0.7f), (byte)(tint.G * 0.7f), (byte)(tint.B * 0.5f), tint.A);
        Raylib.DrawLineEx(
            new Vector2(cx, loopY + s * 0.9f),
            new Vector2(cx, cy + s * 1.0f),
            2, stickColor);

        // Checkmark overlay (white)
        var checkColor = new Color((byte)255, (byte)255, (byte)255, tint.A);
        float cs = s * 0.6f;
        Raylib.DrawLineEx(
            new Vector2(cx - cs * 0.5f, loopY),
            new Vector2(cx - cs * 0.1f, loopY + cs * 0.4f),
            2.5f, checkColor);
        Raylib.DrawLineEx(
            new Vector2(cx - cs * 0.1f, loopY + cs * 0.4f),
            new Vector2(cx + cs * 0.5f, loopY - cs * 0.4f),
            2.5f, checkColor);
    }

    private static void DrawTrap(float cx, float cy, float size, Color tint)
    {
        // Snare loop shape
        float s = size * 0.25f;
        float loopY = cy - s * 0.2f;

        // Snare loop (noose shape)
        Raylib.DrawCircleLines((int)cx, (int)loopY, s, tint);
        Raylib.DrawCircleLines((int)cx, (int)loopY, s - 1, tint);

        // Trigger stick (vertical line down from loop)
        var stickColor = new Color((byte)(tint.R * 0.7f), (byte)(tint.G * 0.6f), (byte)(tint.B * 0.5f), tint.A);
        Raylib.DrawLineEx(
            new Vector2(cx, loopY + s),
            new Vector2(cx, cy + s * 1.2f),
            2, stickColor);

        // Small anchor mark at bottom
        Raylib.DrawLineEx(
            new Vector2(cx - s * 0.3f, cy + s * 1.2f),
            new Vector2(cx + s * 0.3f, cy + s * 1.2f),
            2, stickColor);
    }

    private static void DrawBurrow(float cx, float cy, float size, Color tint)
    {
        // Brown circle with darker hole in center
        float s = size * 0.25f;
        Raylib.DrawCircle((int)cx, (int)cy, s, tint);
        var holeColor = new Color((byte)(tint.R / 2), (byte)(tint.G / 2), (byte)(tint.B / 2), tint.A);
        Raylib.DrawCircle((int)cx, (int)cy, s * 0.5f, holeColor);
    }

    private static void DrawShelter(float cx, float cy, float size, Color tint)
    {
        // Brown house (triangle roof + square base)
        float s = size * 0.3f;
        // Square base
        Raylib.DrawRectangle((int)(cx - s * 0.7f), (int)cy, (int)(s * 1.4f), (int)(s * 0.9f), tint);
        // Triangle roof
        var v1 = new Vector2(cx, cy - s * 0.6f);
        var v2 = new Vector2(cx - s * 0.9f, cy + s * 0.1f);
        var v3 = new Vector2(cx + s * 0.9f, cy + s * 0.1f);
        Raylib.DrawTriangle(v1, v3, v2, tint);
    }

    private static void DrawSnowShelter(float cx, float cy, float size, Color tint)
    {
        // Light blue dome
        float s = size * 0.35f;
        // Half circle (dome)
        for (int i = 0; i < 180; i += 5)
        {
            float angle1 = i * Raylib.DEG2RAD;
            float angle2 = (i + 5) * Raylib.DEG2RAD;
            var v1 = new Vector2(cx, cy + s * 0.3f);
            var v2 = new Vector2(cx + MathF.Cos(angle1 + MathF.PI) * s, cy + s * 0.3f + MathF.Sin(angle1 + MathF.PI) * s * 0.7f);
            var v3 = new Vector2(cx + MathF.Cos(angle2 + MathF.PI) * s, cy + s * 0.3f + MathF.Sin(angle2 + MathF.PI) * s * 0.7f);
            Raylib.DrawTriangle(v1, v2, v3, tint);
        }
    }

    private static void DrawIce(float cx, float cy, float size, Color tint)
    {
        // Light blue crystal (hexagon-ish)
        float s = size * 0.25f;
        // Draw a simple diamond/crystal shape
        var v1 = new Vector2(cx, cy - s);           // Top
        var v2 = new Vector2(cx + s * 0.7f, cy);    // Right
        var v3 = new Vector2(cx, cy + s);           // Bottom
        var v4 = new Vector2(cx - s * 0.7f, cy);    // Left
        Raylib.DrawTriangle(v1, v2, v4, tint);
        Raylib.DrawTriangle(v4, v2, v3, tint);
    }

    private static void DrawCache(float cx, float cy, float size, Color tint)
    {
        // Brown box
        float s = size * 0.25f;
        Raylib.DrawRectangle((int)(cx - s), (int)(cy - s * 0.7f), (int)(s * 2), (int)(s * 1.5f), tint);
        // Lid line
        var lidColor = new Color((byte)(tint.R * 0.7f), (byte)(tint.G * 0.7f), (byte)(tint.B * 0.7f), tint.A);
        Raylib.DrawLineEx(
            new Vector2(cx - s, cy - s * 0.3f),
            new Vector2(cx + s, cy - s * 0.3f),
            2, lidColor);
    }

    private static void DrawCarcass(float cx, float cy, float size, Color tint)
    {
        // Red meat shape (oval with bone)
        float s = size * 0.3f;
        Raylib.DrawEllipse((int)cx, (int)cy, s, s * 0.6f, tint);
        // Bone sticking out
        var boneColor = new Color((byte)220, (byte)215, (byte)200, tint.A);
        Raylib.DrawLineEx(
            new Vector2(cx + s * 0.5f, cy),
            new Vector2(cx + s * 1.1f, cy - s * 0.3f),
            2, boneColor);
    }

    private static void DrawConstruction(float cx, float cy, float size, Color tint)
    {
        // Gray hammer/wrench shape
        float s = size * 0.25f;
        // Handle
        Raylib.DrawRectangle((int)(cx - s * 0.2f), (int)(cy - s * 0.2f), (int)(s * 0.4f), (int)(s * 1.2f), tint);
        // Head
        Raylib.DrawRectangle((int)(cx - s * 0.6f), (int)(cy - s * 0.6f), (int)(s * 1.2f), (int)(s * 0.5f), tint);
    }

    private static void DrawBed(float cx, float cy, float size, Color tint)
    {
        // Purple rectangle (bed)
        float s = size * 0.35f;
        Raylib.DrawRectangle((int)(cx - s), (int)(cy - s * 0.3f), (int)(s * 2), (int)(s * 0.8f), tint);
        // Pillow
        var pillowColor = new Color((byte)Math.Min(255, tint.R + 40), (byte)Math.Min(255, tint.G + 40), (byte)Math.Min(255, tint.B + 40), tint.A);
        Raylib.DrawRectangle((int)(cx - s * 0.8f), (int)(cy - s * 0.25f), (int)(s * 0.5f), (int)(s * 0.5f), pillowColor);
    }

    private static void DrawHarvest(float cx, float cy, float size, Color tint)
    {
        // Green leaf
        float s = size * 0.3f;
        // Leaf shape using two arcs/triangles
        var v1 = new Vector2(cx, cy - s);
        var v2 = new Vector2(cx - s * 0.6f, cy + s * 0.3f);
        var v3 = new Vector2(cx, cy + s * 0.5f);
        var v4 = new Vector2(cx + s * 0.6f, cy + s * 0.3f);
        Raylib.DrawTriangle(v1, v3, v2, tint);
        Raylib.DrawTriangle(v1, v4, v3, tint);
        // Stem
        var stemColor = new Color((byte)(tint.R * 0.7f), (byte)(tint.G * 0.7f), (byte)(tint.B * 0.7f), tint.A);
        Raylib.DrawLineEx(new Vector2(cx, cy), new Vector2(cx, cy + s * 0.8f), 2, stemColor);
    }

    private static void DrawDanger(float cx, float cy, float size, Color tint)
    {
        // Yellow triangle with !
        float s = size * 0.35f;
        var v1 = new Vector2(cx, cy - s);
        var v2 = new Vector2(cx - s * 0.9f, cy + s * 0.6f);
        var v3 = new Vector2(cx + s * 0.9f, cy + s * 0.6f);
        Raylib.DrawTriangle(v1, v3, v2, tint);
        // Exclamation mark
        var markColor = new Color((byte)40, (byte)40, (byte)40, tint.A);
        Raylib.DrawRectangle((int)(cx - s * 0.1f), (int)(cy - s * 0.4f), (int)(s * 0.2f), (int)(s * 0.5f), markColor);
        Raylib.DrawCircle((int)cx, (int)(cy + s * 0.25f), s * 0.1f, markColor);
    }

    private static void DrawBody(float cx, float cy, float size, Color tint)
    {
        // Gray humanoid (circle head + rectangle body)
        float s = size * 0.2f;
        // Head
        Raylib.DrawCircle((int)cx, (int)(cy - s * 0.8f), s * 0.6f, tint);
        // Body
        Raylib.DrawRectangle((int)(cx - s * 0.5f), (int)(cy - s * 0.3f), (int)(s * 1), (int)(s * 1.2f), tint);
    }

    private static void DrawLoot(float cx, float cy, float size, Color tint)
    {
        // Gold sparkle/star
        float s = size * 0.25f;
        // 4-pointed star
        DrawStar(cx, cy, s, 4, tint);
    }

    private static void DrawReady(float cx, float cy, float size, Color tint)
    {
        // Green double-check
        float s = size * 0.25f;
        // First checkmark
        Raylib.DrawLineEx(
            new Vector2(cx - s * 0.8f, cy),
            new Vector2(cx - s * 0.3f, cy + s * 0.5f),
            2, tint);
        Raylib.DrawLineEx(
            new Vector2(cx - s * 0.3f, cy + s * 0.5f),
            new Vector2(cx + s * 0.2f, cy - s * 0.5f),
            2, tint);
        // Second checkmark (offset)
        Raylib.DrawLineEx(
            new Vector2(cx - s * 0.3f, cy),
            new Vector2(cx + s * 0.2f, cy + s * 0.5f),
            2, tint);
        Raylib.DrawLineEx(
            new Vector2(cx + s * 0.2f, cy + s * 0.5f),
            new Vector2(cx + s * 0.7f, cy - s * 0.5f),
            2, tint);
    }

    private static void DrawCuring(float cx, float cy, float size, Color tint)
    {
        // Tan hourglass
        float s = size * 0.25f;
        // Top triangle
        var v1 = new Vector2(cx - s * 0.7f, cy - s);
        var v2 = new Vector2(cx + s * 0.7f, cy - s);
        var v3 = new Vector2(cx, cy);
        Raylib.DrawTriangle(v1, v2, v3, tint);
        // Bottom triangle
        var v4 = new Vector2(cx - s * 0.7f, cy + s);
        var v5 = new Vector2(cx + s * 0.7f, cy + s);
        Raylib.DrawTriangle(v3, v5, v4, tint);
    }

    private static void DrawPredator(float cx, float cy, float size, Color tint)
    {
        float s = size * 0.16f;

        // Main pad (slightly heart-shaped)
        Raylib.DrawEllipse((int)cx, (int)(cy + s * 0.6f), s * 1.3f, s * 1.0f, tint);

        // Toe pads (4 toes)
        Raylib.DrawEllipse((int)(cx - s * 0.9f), (int)(cy - s * 0.4f), s * 0.5f, s * 0.6f, tint);
        Raylib.DrawEllipse((int)(cx + s * 0.9f), (int)(cy - s * 0.4f), s * 0.5f, s * 0.6f, tint);
        Raylib.DrawEllipse((int)(cx - s * 0.35f), (int)(cy - s * 0.9f), s * 0.45f, s * 0.55f, tint);
        Raylib.DrawEllipse((int)(cx + s * 0.35f), (int)(cy - s * 0.9f), s * 0.45f, s * 0.55f, tint);

        // Claw marks (small triangles above toe pads)
        var clawColor = new Color((byte)(tint.R * 0.6f), (byte)(tint.G * 0.5f), (byte)(tint.B * 0.4f), tint.A);
        float clawLen = s * 0.35f;
        // Outer claws
        Raylib.DrawLineEx(new Vector2(cx - s * 0.9f, cy - s * 0.9f), new Vector2(cx - s * 1.0f, cy - s * 1.3f), 1.5f, clawColor);
        Raylib.DrawLineEx(new Vector2(cx + s * 0.9f, cy - s * 0.9f), new Vector2(cx + s * 1.0f, cy - s * 1.3f), 1.5f, clawColor);
        // Inner claws
        Raylib.DrawLineEx(new Vector2(cx - s * 0.35f, cy - s * 1.3f), new Vector2(cx - s * 0.35f, cy - s * 1.65f), 1.5f, clawColor);
        Raylib.DrawLineEx(new Vector2(cx + s * 0.35f, cy - s * 1.3f), new Vector2(cx + s * 0.35f, cy - s * 1.65f), 1.5f, clawColor);
    }

    private static void DrawPrey(float cx, float cy, float size, Color tint)
    {
        float s = size * 0.22f;

        // Body (horizontal oval, sitting pose)
        Raylib.DrawEllipse((int)(cx + s * 0.2f), (int)(cy + s * 0.4f), s * 1.0f, s * 0.7f, tint);

        // Haunches (back bump)
        Raylib.DrawCircle((int)(cx + s * 0.9f), (int)(cy + s * 0.2f), s * 0.5f, tint);

        // Head (round, forward)
        Raylib.DrawCircle((int)(cx - s * 0.6f), (int)(cy + s * 0.1f), s * 0.55f, tint);

        // Ears (tall, distinctive)
        var earColor = tint;  // Same color
        Raylib.DrawEllipse((int)(cx - s * 0.9f), (int)(cy - s * 0.8f), s * 0.22f, s * 0.6f, earColor);
        Raylib.DrawEllipse((int)(cx - s * 0.45f), (int)(cy - s * 0.75f), s * 0.22f, s * 0.55f, earColor);

        // Inner ear detail (lighter)
        var innerEar = new Color((byte)Math.Min(255, tint.R + 30), (byte)Math.Min(255, tint.G + 30), (byte)Math.Min(255, tint.B + 20), tint.A);
        Raylib.DrawEllipse((int)(cx - s * 0.9f), (int)(cy - s * 0.75f), s * 0.1f, s * 0.35f, innerEar);
        Raylib.DrawEllipse((int)(cx - s * 0.45f), (int)(cy - s * 0.7f), s * 0.1f, s * 0.3f, innerEar);

        // Tiny tail
        Raylib.DrawCircle((int)(cx + s * 1.3f), (int)(cy + s * 0.3f), s * 0.2f, tint);
    }

    private static void DrawTracks(float cx, float cy, float size, Color tint)
    {
        // Brown paw prints (smaller, trail-like)
        float s = size * 0.1f;
        // First print
        Raylib.DrawCircle((int)(cx - s * 1.5f), (int)(cy + s), s, tint);
        Raylib.DrawCircle((int)(cx - s * 2.2f), (int)(cy - s * 0.5f), s * 0.5f, tint);
        // Second print
        Raylib.DrawCircle((int)(cx + s * 1.5f), (int)(cy - s), s, tint);
        Raylib.DrawCircle((int)(cx + s * 2.2f), (int)(cy + s * 0.5f), s * 0.5f, tint);
    }

    private static void DrawForest(float cx, float cy, float size, Color tint)
    {
        // Green tree (triangle on stick)
        float s = size * 0.3f;
        // Trunk
        var trunkColor = new Color((byte)100, (byte)70, (byte)40, tint.A);
        Raylib.DrawRectangle((int)(cx - s * 0.15f), (int)(cy + s * 0.2f), (int)(s * 0.3f), (int)(s * 0.6f), trunkColor);
        // Canopy
        var v1 = new Vector2(cx, cy - s);
        var v2 = new Vector2(cx - s * 0.8f, cy + s * 0.3f);
        var v3 = new Vector2(cx + s * 0.8f, cy + s * 0.3f);
        Raylib.DrawTriangle(v1, v3, v2, tint);
    }

    private static void DrawRocks(float cx, float cy, float size, Color tint)
    {
        // Gray rock pile (overlapping circles/ovals)
        float s = size * 0.15f;
        var darkerTint = new Color((byte)(tint.R * 0.8f), (byte)(tint.G * 0.8f), (byte)(tint.B * 0.8f), tint.A);
        Raylib.DrawEllipse((int)(cx - s), (int)(cy + s * 0.5f), s * 1.2f, s * 0.8f, tint);
        Raylib.DrawEllipse((int)(cx + s * 0.8f), (int)(cy + s * 0.3f), s, s * 0.7f, darkerTint);
        Raylib.DrawEllipse((int)cx, (int)(cy - s * 0.3f), s * 0.9f, s * 0.6f, tint);
    }

    private static void DrawTrail(float cx, float cy, float size, Color tint)
    {
        // Brown dotted line
        float s = size * 0.3f;
        float dotSize = size * 0.08f;
        Raylib.DrawCircle((int)(cx - s), (int)cy, dotSize, tint);
        Raylib.DrawCircle((int)(cx - s * 0.33f), (int)(cy - s * 0.2f), dotSize, tint);
        Raylib.DrawCircle((int)(cx + s * 0.33f), (int)(cy + s * 0.2f), dotSize, tint);
        Raylib.DrawCircle((int)(cx + s), (int)cy, dotSize, tint);
    }

    private static void DrawOverlook(float cx, float cy, float size, Color tint)
    {
        // Gray mountain peak
        float s = size * 0.35f;
        var v1 = new Vector2(cx, cy - s);
        var v2 = new Vector2(cx - s, cy + s * 0.6f);
        var v3 = new Vector2(cx + s, cy + s * 0.6f);
        Raylib.DrawTriangle(v1, v3, v2, tint);
        // Snow cap
        var snowColor = new Color((byte)240, (byte)245, (byte)250, tint.A);
        var sv1 = new Vector2(cx, cy - s);
        var sv2 = new Vector2(cx - s * 0.3f, cy - s * 0.4f);
        var sv3 = new Vector2(cx + s * 0.3f, cy - s * 0.4f);
        Raylib.DrawTriangle(sv1, sv3, sv2, snowColor);
    }

    private static void DrawThicket(float cx, float cy, float size, Color tint)
    {
        float s = size * 0.14f;

        // Shadow/darker base layer
        var shadowColor = new Color((byte)(tint.R * 0.6f), (byte)(tint.G * 0.65f), (byte)(tint.B * 0.5f), tint.A);
        Raylib.DrawEllipse((int)cx, (int)(cy + s * 0.8f), s * 1.8f, s * 0.7f, shadowColor);

        // Main bush body (varied sizes for organic look)
        Raylib.DrawCircle((int)cx, (int)cy, s * 1.1f, tint);
        Raylib.DrawCircle((int)(cx - s * 1.1f), (int)(cy - s * 0.2f), s * 0.9f, tint);
        Raylib.DrawCircle((int)(cx + s * 1.1f), (int)(cy - s * 0.15f), s * 0.85f, tint);

        // Top foliage bumps (lighter)
        var topColor = new Color((byte)Math.Min(255, tint.R + 15), (byte)Math.Min(255, tint.G + 20), (byte)Math.Min(255, tint.B + 10), tint.A);
        Raylib.DrawCircle((int)(cx - s * 0.5f), (int)(cy - s * 0.9f), s * 0.7f, topColor);
        Raylib.DrawCircle((int)(cx + s * 0.4f), (int)(cy - s * 0.85f), s * 0.65f, topColor);
        Raylib.DrawCircle((int)cx, (int)(cy - s * 0.6f), s * 0.6f, topColor);
    }

    private static void DrawBones(float cx, float cy, float size, Color tint)
    {
        // White crossed bones
        float s = size * 0.3f;
        float thickness = size * 0.06f;
        // First bone
        Raylib.DrawLineEx(
            new Vector2(cx - s, cy - s * 0.6f),
            new Vector2(cx + s, cy + s * 0.6f),
            thickness, tint);
        // Second bone
        Raylib.DrawLineEx(
            new Vector2(cx + s, cy - s * 0.6f),
            new Vector2(cx - s, cy + s * 0.6f),
            thickness, tint);
        // Bone ends (circles)
        float endSize = size * 0.08f;
        Raylib.DrawCircle((int)(cx - s), (int)(cy - s * 0.6f), endSize, tint);
        Raylib.DrawCircle((int)(cx + s), (int)(cy + s * 0.6f), endSize, tint);
        Raylib.DrawCircle((int)(cx + s), (int)(cy - s * 0.6f), endSize, tint);
        Raylib.DrawCircle((int)(cx - s), (int)(cy + s * 0.6f), endSize, tint);
    }

    private static void DrawGrass(float cx, float cy, float size, Color tint)
    {
        // Green blades
        float s = size * 0.25f;
        float thickness = size * 0.04f;
        // Multiple grass blades
        Raylib.DrawLineEx(new Vector2(cx - s * 0.5f, cy + s * 0.5f), new Vector2(cx - s * 0.7f, cy - s), thickness, tint);
        Raylib.DrawLineEx(new Vector2(cx, cy + s * 0.5f), new Vector2(cx - s * 0.1f, cy - s * 1.1f), thickness, tint);
        Raylib.DrawLineEx(new Vector2(cx + s * 0.5f, cy + s * 0.5f), new Vector2(cx + s * 0.6f, cy - s * 0.9f), thickness, tint);
    }

    private static void DrawNest(float cx, float cy, float size, Color tint)
    {
        // Brown bowl + eggs
        float s = size * 0.25f;
        // Nest bowl (half ellipse)
        Raylib.DrawEllipse((int)cx, (int)(cy + s * 0.2f), s * 1.2f, s * 0.6f, tint);
        // Inner darker area
        var innerColor = new Color((byte)(tint.R * 0.6f), (byte)(tint.G * 0.6f), (byte)(tint.B * 0.6f), tint.A);
        Raylib.DrawEllipse((int)cx, (int)cy, s * 0.8f, s * 0.35f, innerColor);
        // Eggs
        var eggColor = new Color((byte)230, (byte)225, (byte)215, tint.A);
        Raylib.DrawEllipse((int)(cx - s * 0.3f), (int)(cy - s * 0.1f), s * 0.25f, s * 0.18f, eggColor);
        Raylib.DrawEllipse((int)(cx + s * 0.3f), (int)(cy - s * 0.1f), s * 0.25f, s * 0.18f, eggColor);
    }

    private static void DrawWebs(float cx, float cy, float size, Color tint)
    {
        // Gray web pattern
        float s = size * 0.3f;
        // Radial lines
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathF.PI / 3;
            Raylib.DrawLineEx(
                new Vector2(cx, cy),
                new Vector2(cx + MathF.Cos(angle) * s, cy + MathF.Sin(angle) * s),
                1, tint);
        }
        // Concentric rings (simplified)
        Raylib.DrawCircleLines((int)cx, (int)cy, s * 0.5f, tint);
        Raylib.DrawCircleLines((int)cx, (int)cy, s * 0.8f, tint);
    }

    private static void DrawWindShelter(float cx, float cy, float size, Color tint)
    {
        // Blue wind lines
        float s = size * 0.3f;
        float thickness = size * 0.04f;
        // Curved wind lines (simplified as wavy lines)
        Raylib.DrawLineEx(new Vector2(cx - s, cy - s * 0.5f), new Vector2(cx + s, cy - s * 0.5f), thickness, tint);
        Raylib.DrawLineEx(new Vector2(cx - s * 0.8f, cy), new Vector2(cx + s * 0.8f, cy), thickness, tint);
        Raylib.DrawLineEx(new Vector2(cx - s * 0.6f, cy + s * 0.5f), new Vector2(cx + s * 0.6f, cy + s * 0.5f), thickness, tint);
    }

    private static void DrawDefault(float cx, float cy, float size, Color tint)
    {
        // Gray ? in circle
        float s = size * 0.25f;
        Raylib.DrawCircleLines((int)cx, (int)cy, s, tint);
        // Question mark
        int fontSize = (int)(s * 1.5f);
        int textWidth = Raylib.MeasureText("?", fontSize);
        Raylib.DrawText("?", (int)(cx - textWidth / 2), (int)(cy - fontSize / 2), fontSize, tint);
    }

    private static void DrawStar(float cx, float cy, float size, int points, Color color)
    {
        // Draw a simple star shape
        float innerRadius = size * 0.4f;
        float outerRadius = size;

        for (int i = 0; i < points * 2; i++)
        {
            float radius1 = (i % 2 == 0) ? outerRadius : innerRadius;
            float radius2 = ((i + 1) % 2 == 0) ? outerRadius : innerRadius;
            float angle1 = (i * MathF.PI / points) - MathF.PI / 2;
            float angle2 = ((i + 1) * MathF.PI / points) - MathF.PI / 2;

            var v1 = new Vector2(cx, cy);
            var v2 = new Vector2(cx + MathF.Cos(angle1) * radius1, cy + MathF.Sin(angle1) * radius1);
            var v3 = new Vector2(cx + MathF.Cos(angle2) * radius2, cy + MathF.Sin(angle2) * radius2);

            Raylib.DrawTriangle(v1, v3, v2, color);
        }
    }
}
