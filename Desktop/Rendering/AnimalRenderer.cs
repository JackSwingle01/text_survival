using Raylib_cs;
using System.Numerics;
using text_survival.Actors.Animals;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Renders procedural animal sprites for herd visualization.
/// </summary>
public static class AnimalRenderer
{
    /// <summary>
    /// Draw an animal sprite at the specified position.
    /// </summary>
    public static void DrawAnimal(AnimalType type, float x, float y, float scale)
    {
        switch (type)
        {
            case AnimalType.Caribou:
                DrawCaribou(x, y, scale);
                break;
            case AnimalType.Rabbit:
                DrawRabbit(x, y, scale);
                break;
            case AnimalType.Ptarmigan:
                DrawPtarmigan(x, y, scale);
                break;
            case AnimalType.Fox:
                DrawFox(x, y, scale);
                break;
            case AnimalType.Wolf:
                DrawWolf(x, y, scale);
                break;
            case AnimalType.Bear:
                DrawBear(x, y, scale);
                break;
            case AnimalType.CaveBear:
                DrawCaveBear(x, y, scale);
                break;
            case AnimalType.Hyena:
                DrawHyena(x, y, scale);
                break;
            case AnimalType.Mammoth:
                DrawMammoth(x, y, scale);
                break;
            case AnimalType.SaberTooth:
                DrawSaberTooth(x, y, scale);
                break;
            case AnimalType.Megaloceros:
                DrawMegaloceros(x, y, scale);
                break;
            case AnimalType.Bison:
                DrawBison(x, y, scale);
                break;
            case AnimalType.Rat:
                DrawRat(x, y, scale);
                break;
            default:
                DrawDefault(x, y, scale);
                break;
        }
    }

    // === Helper Methods ===

    private static void DrawEllipse(float cx, float cy, float rx, float ry, Color color)
    {
        Raylib.DrawEllipse((int)cx, (int)cy, rx, ry, color);
    }

    private static void DrawRotatedEllipse(float cx, float cy, float rx, float ry, float angleDegrees, Color color)
    {
        // Approximate rotated ellipse using a polygon
        int segments = 20;
        float angleRad = angleDegrees * Raylib.DEG2RAD;

        for (int i = 0; i < segments; i++)
        {
            float t1 = (i * 2 * MathF.PI) / segments;
            float t2 = ((i + 1) * 2 * MathF.PI) / segments;

            // Point on ellipse
            float x1 = rx * MathF.Cos(t1);
            float y1 = ry * MathF.Sin(t1);
            float x2 = rx * MathF.Cos(t2);
            float y2 = ry * MathF.Sin(t2);

            // Rotate
            float rx1 = x1 * MathF.Cos(angleRad) - y1 * MathF.Sin(angleRad);
            float ry1 = x1 * MathF.Sin(angleRad) + y1 * MathF.Cos(angleRad);
            float rx2 = x2 * MathF.Cos(angleRad) - y2 * MathF.Sin(angleRad);
            float ry2 = x2 * MathF.Sin(angleRad) + y2 * MathF.Cos(angleRad);

            Raylib.DrawTriangle(
                new Vector2(cx, cy),
                new Vector2(cx + rx1, cy + ry1),
                new Vector2(cx + rx2, cy + ry2),
                color);
        }
    }

    // === Animal Drawing Functions ===

    private static void DrawCaribou(float x, float y, float scale)
    {
        // Brown deer with antlers
        var bodyColor = new Color(139, 90, 60, 255);
        var antlerColor = new Color(101, 67, 33, 255);
        var legColor = new Color(120, 80, 55, 255);

        // Body
        DrawEllipse(x, y, scale * 7, scale * 5, bodyColor);

        // Head
        DrawEllipse(x + scale * 8, y - scale * 2, scale * 4, scale * 3, bodyColor);

        // Legs (simple lines)
        Raylib.DrawLineEx(new Vector2(x - scale * 3, y + scale * 5), new Vector2(x - scale * 3, y + scale * 10), scale * 1.5f, legColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 3, y + scale * 5), new Vector2(x + scale * 3, y + scale * 10), scale * 1.5f, legColor);

        // Antlers
        Raylib.DrawLineEx(new Vector2(x + scale * 7, y - scale * 5), new Vector2(x + scale * 5, y - scale * 10), scale, antlerColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 9, y - scale * 5), new Vector2(x + scale * 11, y - scale * 10), scale, antlerColor);
        // Antler branches
        Raylib.DrawLineEx(new Vector2(x + scale * 5.5f, y - scale * 8), new Vector2(x + scale * 3, y - scale * 9), scale * 0.8f, antlerColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 10.5f, y - scale * 8), new Vector2(x + scale * 13, y - scale * 9), scale * 0.8f, antlerColor);
    }

    private static void DrawRabbit(float x, float y, float scale)
    {
        // Gray rabbit with long ears
        var bodyColor = new Color(180, 180, 180, 255);
        var earColor = new Color(160, 160, 160, 255);
        var tailColor = new Color(220, 220, 220, 255);

        // Body
        DrawEllipse(x, y, scale * 6, scale * 5, bodyColor);

        // Head
        DrawEllipse(x + scale * 5, y - scale, scale * 4, scale * 3.5f, bodyColor);

        // Long ears
        DrawEllipse(x + scale * 4, y - scale * 7, scale * 1.5f, scale * 5, earColor);
        DrawEllipse(x + scale * 7, y - scale * 7, scale * 1.5f, scale * 5, earColor);

        // Fluffy tail
        Raylib.DrawCircle((int)(x - scale * 5), (int)y, scale * 2.5f, tailColor);
    }

    private static void DrawPtarmigan(float x, float y, float scale)
    {
        // White bird
        var bodyColor = new Color(240, 240, 245, 255);
        var beakColor = new Color(80, 80, 80, 255);
        var eyeColor = new Color(40, 40, 40, 255);

        // Body
        DrawEllipse(x, y, scale * 5, scale * 4, bodyColor);

        // Head
        Raylib.DrawCircle((int)(x + scale * 4), (int)(y - scale * 2), scale * 3, bodyColor);

        // Wing
        DrawRotatedEllipse(x - scale * 2, y, scale * 4, scale * 2, 30, new Color(230, 230, 235, 255));

        // Beak
        Raylib.DrawTriangle(
            new Vector2(x + scale * 7, y - scale * 2),
            new Vector2(x + scale * 5, y - scale * 3),
            new Vector2(x + scale * 5, y - scale * 1),
            beakColor);

        // Eye
        Raylib.DrawCircle((int)(x + scale * 4), (int)(y - scale * 2.5f), scale * 0.8f, eyeColor);
    }

    private static void DrawFox(float x, float y, float scale)
    {
        // Orange fox with bushy tail
        var bodyColor = new Color(218, 107, 44, 255);
        var tailColor = new Color(200, 90, 30, 255);
        var earColor = new Color(190, 95, 40, 255);
        var chestColor = new Color(240, 230, 220, 255);

        // Body
        DrawEllipse(x, y, scale * 6, scale * 4, bodyColor);

        // Head
        DrawEllipse(x + scale * 6, y - scale, scale * 3.5f, scale * 3, bodyColor);

        // Chest/belly (white)
        DrawEllipse(x + scale, y + scale, scale * 3, scale * 2, chestColor);

        // Pointed ears
        Raylib.DrawTriangle(
            new Vector2(x + scale * 5, y - scale * 5),
            new Vector2(x + scale * 4, y - scale * 2),
            new Vector2(x + scale * 6, y - scale * 2),
            earColor);
        Raylib.DrawTriangle(
            new Vector2(x + scale * 8, y - scale * 5),
            new Vector2(x + scale * 7, y - scale * 2),
            new Vector2(x + scale * 9, y - scale * 2),
            earColor);

        // Bushy tail
        DrawEllipse(x - scale * 7, y + scale, scale * 6, scale * 3, tailColor);
        // Tail tip (white)
        Raylib.DrawCircle((int)(x - scale * 11), (int)(y + scale), scale * 2, chestColor);
    }

    private static void DrawWolf(float x, float y, float scale)
    {
        // Gray wolf
        var bodyColor = new Color(120, 120, 130, 255);
        var earColor = new Color(100, 100, 110, 255);
        var eyeColor = new Color(200, 180, 100, 255);

        // Body
        DrawEllipse(x, y, scale * 8, scale * 5, bodyColor);

        // Head
        DrawEllipse(x + scale * 8, y - scale, scale * 5, scale * 4, bodyColor);

        // Legs
        Raylib.DrawLineEx(new Vector2(x - scale * 4, y + scale * 5), new Vector2(x - scale * 4, y + scale * 11), scale * 2, bodyColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 4, y + scale * 5), new Vector2(x + scale * 4, y + scale * 11), scale * 2, bodyColor);

        // Pointed ears
        Raylib.DrawTriangle(
            new Vector2(x + scale * 7, y - scale * 6),
            new Vector2(x + scale * 6, y - scale * 3),
            new Vector2(x + scale * 8, y - scale * 3),
            earColor);
        Raylib.DrawTriangle(
            new Vector2(x + scale * 10, y - scale * 6),
            new Vector2(x + scale * 9, y - scale * 3),
            new Vector2(x + scale * 11, y - scale * 3),
            earColor);

        // Eyes (yellow)
        Raylib.DrawCircle((int)(x + scale * 9), (int)(y - scale * 2), scale * 0.8f, eyeColor);
    }

    private static void DrawBear(float x, float y, float scale)
    {
        // Dark brown bear
        var bodyColor = new Color(80, 50, 30, 255);
        var earColor = new Color(70, 45, 25, 255);

        // Body (stocky)
        DrawEllipse(x, y, scale * 9, scale * 7, bodyColor);

        // Head
        Raylib.DrawCircle((int)(x + scale * 8), (int)(y - scale * 3), scale * 5, bodyColor);

        // Rounded ears
        Raylib.DrawCircle((int)(x + scale * 5), (int)(y - scale * 7), scale * 2.5f, earColor);
        Raylib.DrawCircle((int)(x + scale * 11), (int)(y - scale * 7), scale * 2.5f, earColor);

        // Legs (thick)
        Raylib.DrawLineEx(new Vector2(x - scale * 5, y + scale * 7), new Vector2(x - scale * 5, y + scale * 12), scale * 3, bodyColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 5, y + scale * 7), new Vector2(x + scale * 5, y + scale * 12), scale * 3, bodyColor);
    }

    private static void DrawCaveBear(float x, float y, float scale)
    {
        // Darker, larger bear with shoulder hump
        var bodyColor = new Color(50, 35, 25, 255);
        var earColor = new Color(45, 30, 20, 255);
        var humpColor = new Color(60, 40, 28, 255);

        // Body (larger, stockier)
        DrawEllipse(x, y, scale * 11, scale * 8, bodyColor);

        // Shoulder hump
        DrawEllipse(x - scale * 2, y - scale * 4, scale * 6, scale * 5, humpColor);

        // Head
        Raylib.DrawCircle((int)(x + scale * 9), (int)(y - scale * 2), scale * 6, bodyColor);

        // Rounded ears
        Raylib.DrawCircle((int)(x + scale * 6), (int)(y - scale * 7), scale * 2.5f, earColor);
        Raylib.DrawCircle((int)(x + scale * 12), (int)(y - scale * 7), scale * 2.5f, earColor);

        // Thick legs
        Raylib.DrawLineEx(new Vector2(x - scale * 6, y + scale * 8), new Vector2(x - scale * 6, y + scale * 14), scale * 3.5f, bodyColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 6, y + scale * 8), new Vector2(x + scale * 6, y + scale * 14), scale * 3.5f, bodyColor);
    }

    private static void DrawHyena(float x, float y, float scale)
    {
        // Tan with spots, sloped back
        var bodyColor = new Color(180, 150, 110, 255);
        var spotColor = new Color(100, 80, 60, 255);
        var earColor = new Color(160, 130, 100, 255);

        // Body (sloped - higher at front)
        DrawRotatedEllipse(x, y, scale * 8, scale * 5, -15, bodyColor);

        // Head
        DrawEllipse(x + scale * 7, y - scale * 3, scale * 4, scale * 3.5f, bodyColor);

        // Rounded ears
        Raylib.DrawCircle((int)(x + scale * 6), (int)(y - scale * 6), scale * 2, earColor);
        Raylib.DrawCircle((int)(x + scale * 9), (int)(y - scale * 6), scale * 2, earColor);

        // Spots
        Raylib.DrawCircle((int)(x - scale * 2), (int)(y - scale), scale * 1.5f, spotColor);
        Raylib.DrawCircle((int)(x + scale * 2), (int)(y + scale), scale * 1.5f, spotColor);
        Raylib.DrawCircle((int)(x - scale * 4), (int)(y + scale * 2), scale * 1.2f, spotColor);

        // Legs
        Raylib.DrawLineEx(new Vector2(x - scale * 3, y + scale * 5), new Vector2(x - scale * 3, y + scale * 10), scale * 2, bodyColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 4, y + scale * 5), new Vector2(x + scale * 4, y + scale * 10), scale * 2, bodyColor);
    }

    private static void DrawMammoth(float x, float y, float scale)
    {
        // Massive with shaggy fur, curved tusks, trunk
        var bodyColor = new Color(90, 70, 50, 255);
        var furColor = new Color(110, 85, 60, 255);
        var tuskColor = new Color(230, 220, 200, 255);

        // Massive body
        DrawEllipse(x, y, scale * 14, scale * 11, bodyColor);

        // Shaggy fur texture
        DrawEllipse(x - scale * 2, y - scale * 5, scale * 10, scale * 7, furColor);
        DrawEllipse(x + scale * 3, y + scale * 6, scale * 8, scale * 4, furColor);

        // Head
        DrawEllipse(x + scale * 12, y - scale * 4, scale * 7, scale * 6, bodyColor);

        // Curved tusks
        Raylib.DrawLineEx(new Vector2(x + scale * 10, y + scale * 2), new Vector2(x + scale * 8, y + scale * 10), scale * 2, tuskColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 14, y + scale * 2), new Vector2(x + scale * 16, y + scale * 10), scale * 2, tuskColor);
        // Curve tips inward
        Raylib.DrawLineEx(new Vector2(x + scale * 8, y + scale * 10), new Vector2(x + scale * 9, y + scale * 12), scale * 1.5f, tuskColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 16, y + scale * 10), new Vector2(x + scale * 15, y + scale * 12), scale * 1.5f, tuskColor);

        // Trunk
        Raylib.DrawLineEx(new Vector2(x + scale * 12, y + scale * 2), new Vector2(x + scale * 11, y + scale * 9), scale * 2.5f, bodyColor);
    }

    private static void DrawSaberTooth(float x, float y, float scale)
    {
        // Tan/orange with massive fangs
        var bodyColor = new Color(210, 160, 100, 255);
        var fangColor = new Color(240, 235, 220, 255);
        var earColor = new Color(190, 145, 90, 255);

        // Muscular body
        DrawEllipse(x, y, scale * 9, scale * 6, bodyColor);

        // Head (large)
        DrawEllipse(x + scale * 9, y - scale * 2, scale * 6, scale * 5, bodyColor);

        // Ears
        Raylib.DrawTriangle(
            new Vector2(x + scale * 8, y - scale * 7),
            new Vector2(x + scale * 7, y - scale * 4),
            new Vector2(x + scale * 9, y - scale * 4),
            earColor);
        Raylib.DrawTriangle(
            new Vector2(x + scale * 12, y - scale * 7),
            new Vector2(x + scale * 11, y - scale * 4),
            new Vector2(x + scale * 13, y - scale * 4),
            earColor);

        // Massive fangs
        Raylib.DrawLineEx(new Vector2(x + scale * 10, y + scale * 2), new Vector2(x + scale * 9, y + scale * 8), scale * 1.5f, fangColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 12, y + scale * 2), new Vector2(x + scale * 13, y + scale * 8), scale * 1.5f, fangColor);

        // Legs
        Raylib.DrawLineEx(new Vector2(x - scale * 4, y + scale * 6), new Vector2(x - scale * 4, y + scale * 12), scale * 2.5f, bodyColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 4, y + scale * 6), new Vector2(x + scale * 4, y + scale * 12), scale * 2.5f, bodyColor);
    }

    private static void DrawMegaloceros(float x, float y, float scale)
    {
        // Huge palmate antlers, elk-like
        var bodyColor = new Color(130, 95, 65, 255);
        var antlerColor = new Color(95, 70, 45, 255);
        var legColor = new Color(115, 85, 60, 255);

        // Body
        DrawEllipse(x, y, scale * 8, scale * 6, bodyColor);

        // Head
        DrawEllipse(x + scale * 9, y - scale * 2, scale * 4.5f, scale * 3.5f, bodyColor);

        // Legs
        Raylib.DrawLineEx(new Vector2(x - scale * 4, y + scale * 6), new Vector2(x - scale * 4, y + scale * 12), scale * 2, legColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 4, y + scale * 6), new Vector2(x + scale * 4, y + scale * 12), scale * 2, legColor);

        // Massive palmate antlers (palm-like)
        // Main beams
        Raylib.DrawLineEx(new Vector2(x + scale * 7, y - scale * 6), new Vector2(x + scale * 3, y - scale * 14), scale * 1.2f, antlerColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 11, y - scale * 6), new Vector2(x + scale * 15, y - scale * 14), scale * 1.2f, antlerColor);
        // Palm fans
        DrawRotatedEllipse(x + scale * 3, y - scale * 15, scale * 5, scale * 3, 20, antlerColor);
        DrawRotatedEllipse(x + scale * 15, y - scale * 15, scale * 5, scale * 3, -20, antlerColor);
    }

    private static void DrawBison(float x, float y, float scale)
    {
        // Dark fur, shoulder hump, horns, beard
        var bodyColor = new Color(70, 50, 35, 255);
        var humpColor = new Color(85, 60, 40, 255);
        var hornColor = new Color(50, 40, 30, 255);
        var beardColor = new Color(60, 45, 30, 255);

        // Body
        DrawEllipse(x, y, scale * 10, scale * 7, bodyColor);

        // Shoulder hump
        DrawEllipse(x - scale * 3, y - scale * 5, scale * 7, scale * 6, humpColor);

        // Head (small relative to body)
        DrawEllipse(x + scale * 9, y - scale, scale * 5, scale * 4, bodyColor);

        // Horns (curved)
        Raylib.DrawLineEx(new Vector2(x + scale * 8, y - scale * 4), new Vector2(x + scale * 6, y - scale * 7), scale * 1.2f, hornColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 6, y - scale * 7), new Vector2(x + scale * 7, y - scale * 8), scale * 1f, hornColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 11, y - scale * 4), new Vector2(x + scale * 13, y - scale * 7), scale * 1.2f, hornColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 13, y - scale * 7), new Vector2(x + scale * 12, y - scale * 8), scale * 1f, hornColor);

        // Beard
        DrawEllipse(x + scale * 9, y + scale * 3, scale * 3, scale * 2.5f, beardColor);

        // Legs (thick)
        Raylib.DrawLineEx(new Vector2(x - scale * 5, y + scale * 7), new Vector2(x - scale * 5, y + scale * 12), scale * 2.5f, bodyColor);
        Raylib.DrawLineEx(new Vector2(x + scale * 5, y + scale * 7), new Vector2(x + scale * 5, y + scale * 12), scale * 2.5f, bodyColor);
    }

    private static void DrawRat(float x, float y, float scale)
    {
        // Small, long tail, large ears
        var bodyColor = new Color(110, 90, 80, 255);
        var earColor = new Color(140, 120, 110, 255);
        var tailColor = new Color(100, 80, 70, 255);

        // Body (small)
        DrawEllipse(x, y, scale * 4, scale * 3, bodyColor);

        // Head
        Raylib.DrawCircle((int)(x + scale * 3.5f), (int)(y - scale * 0.5f), scale * 2, bodyColor);

        // Large ears
        Raylib.DrawCircle((int)(x + scale * 3), (int)(y - scale * 3), scale * 1.8f, earColor);
        Raylib.DrawCircle((int)(x + scale * 5), (int)(y - scale * 3), scale * 1.8f, earColor);

        // Long thin tail
        Raylib.DrawLineEx(new Vector2(x - scale * 4, y), new Vector2(x - scale * 8, y + scale * 2), scale * 0.8f, tailColor);
    }

    private static void DrawDefault(float x, float y, float scale)
    {
        // Generic animal silhouette
        var color = new Color(120, 120, 120, 255);
        DrawEllipse(x, y, scale * 6, scale * 4, color);
        Raylib.DrawCircle((int)(x + scale * 5), (int)(y - scale), scale * 2.5f, color);
    }
}
