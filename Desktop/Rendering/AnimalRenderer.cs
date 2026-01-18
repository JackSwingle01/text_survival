using Raylib_cs;
using System.Numerics;
using text_survival.Actors.Animals;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Renders procedural animal sprites for herd visualization.
/// Minimalist ice age fauna designs - all animals face left.
/// </summary>
public static class AnimalRenderer
{
    // Largest sprite spans ~80 units (bison width, megaloceros height)
    private const float NormalizationSize = 80f;

    /// <summary>
    /// Draw an animal sprite at the specified position.
    /// </summary>
    /// <param name="sizePixels">Target size in pixels (largest dimension)</param>
    public static void DrawAnimal(AnimalType type, float x, float y, float sizePixels)
    {
        float scale = sizePixels / NormalizationSize;
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

    private static void DrawCircle(float cx, float cy, float r, Color color)
    {
        Raylib.DrawCircle((int)cx, (int)cy, r, color);
    }

    private static void DrawRotatedEllipse(float cx, float cy, float rx, float ry, float angleRadians, Color color)
    {
        // Approximate rotated ellipse using a polygon
        int segments = 20;

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
            float rx1 = x1 * MathF.Cos(angleRadians) - y1 * MathF.Sin(angleRadians);
            float ry1 = x1 * MathF.Sin(angleRadians) + y1 * MathF.Cos(angleRadians);
            float rx2 = x2 * MathF.Cos(angleRadians) - y2 * MathF.Sin(angleRadians);
            float ry2 = x2 * MathF.Sin(angleRadians) + y2 * MathF.Cos(angleRadians);

            Raylib.DrawTriangle(
                new Vector2(cx, cy),
                new Vector2(cx + rx1, cy + ry1),
                new Vector2(cx + rx2, cy + ry2),
                color);
        }
    }

    // === Animal Drawing Functions ===
    // All animals face LEFT (head/snout on negative X side)

    private static void DrawCaribou(float x, float y, float scale)
    {
        var body = new Color(139, 115, 85, 255);
        var bodyLight = new Color(160, 128, 96, 255);
        var antler = new Color(90, 74, 58, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // Antlers (behind head)
        DrawRotatedEllipse(x - 20 * s, y - 38 * s, 2 * s, 14 * s, -0.4f, antler);
        DrawRotatedEllipse(x - 12 * s, y - 40 * s, 2 * s, 14 * s, 0.4f, antler);
        DrawRotatedEllipse(x - 28 * s, y - 44 * s, 2 * s, 8 * s, -0.9f, antler);
        DrawRotatedEllipse(x - 6 * s, y - 46 * s, 2 * s, 8 * s, 0.9f, antler);
        DrawRotatedEllipse(x - 24 * s, y - 50 * s, 1.5f * s, 5 * s, -0.5f, antler);
        DrawRotatedEllipse(x - 10 * s, y - 52 * s, 1.5f * s, 5 * s, 0.5f, antler);

        // Body
        DrawEllipse(x + 8 * s, y - 12 * s, 18 * s, 12 * s, body);
        DrawEllipse(x + 2 * s, y - 14 * s, 8 * s, 6 * s, bodyLight);

        // Legs
        DrawEllipse(x - 2 * s, y + 2 * s, 3 * s, 10 * s, body);
        DrawEllipse(x + 18 * s, y + 2 * s, 3 * s, 10 * s, body);

        // Neck
        DrawRotatedEllipse(x - 8 * s, y - 20 * s, 6 * s, 12 * s, -0.3f, body);

        // Head
        DrawEllipse(x - 16 * s, y - 28 * s, 7 * s, 6 * s, body);

        // Snout
        DrawEllipse(x - 22 * s, y - 26 * s, 5 * s, 3 * s, bodyLight);

        // Eye
        DrawCircle(x - 14 * s, y - 29 * s, 1.5f * s, eye);
    }

    private static void DrawRabbit(float x, float y, float scale)
    {
        var fur = new Color(212, 204, 196, 255);
        var furLight = new Color(232, 224, 216, 255);
        var earInner = new Color(196, 168, 152, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // Tail (behind body)
        DrawCircle(x + 12 * s, y - 6 * s, 3 * s, furLight);

        // Body
        DrawEllipse(x + 4 * s, y - 8 * s, 10 * s, 7 * s, fur);
        DrawEllipse(x + 2 * s, y - 10 * s, 5 * s, 4 * s, furLight);

        // Back legs (hunched)
        DrawEllipse(x + 10 * s, y - 2 * s, 5 * s, 5 * s, fur);

        // Front legs
        DrawEllipse(x - 4 * s, y - 2 * s, 2 * s, 6 * s, fur);

        // Ears (behind head)
        DrawRotatedEllipse(x - 8 * s, y - 26 * s, 3 * s, 10 * s, -0.3f, fur);
        DrawRotatedEllipse(x - 2 * s, y - 28 * s, 3 * s, 10 * s, 0.2f, fur);
        DrawRotatedEllipse(x - 8 * s, y - 26 * s, 1.5f * s, 7 * s, -0.3f, earInner);
        DrawRotatedEllipse(x - 2 * s, y - 28 * s, 1.5f * s, 7 * s, 0.2f, earInner);

        // Head
        DrawCircle(x - 4 * s, y - 14 * s, 6 * s, fur);

        // Snout
        DrawEllipse(x - 10 * s, y - 12 * s, 4 * s, 3 * s, furLight);

        // Eye
        DrawCircle(x - 6 * s, y - 15 * s, 1.5f * s, eye);
    }

    private static void DrawPtarmigan(float x, float y, float scale)
    {
        var feather = new Color(232, 228, 220, 255);
        var featherDark = new Color(196, 192, 184, 255);
        var beak = new Color(74, 74, 58, 255);
        var eye = new Color(42, 42, 42, 255);
        var feet = new Color(138, 122, 90, 255);

        float s = scale;

        // Body
        DrawEllipse(x, y - 8 * s, 12 * s, 8 * s, feather);
        DrawEllipse(x + 4 * s, y - 10 * s, 6 * s, 5 * s, featherDark);

        // Tail
        DrawRotatedEllipse(x + 14 * s, y - 6 * s, 6 * s, 3 * s, 0.3f, featherDark);

        // Wing detail
        DrawRotatedEllipse(x + 2 * s, y - 8 * s, 8 * s, 4 * s, 0.2f, featherDark);

        // Feet
        DrawEllipse(x - 4 * s, y + 2 * s, 2 * s, 4 * s, feet);
        DrawEllipse(x + 2 * s, y + 2 * s, 2 * s, 4 * s, feet);

        // Head
        DrawCircle(x - 8 * s, y - 12 * s, 5 * s, feather);

        // Beak
        DrawRotatedEllipse(x - 14 * s, y - 11 * s, 4 * s, 2 * s, 0, beak);

        // Eye
        DrawCircle(x - 7 * s, y - 13 * s, 1.2f * s, eye);
    }

    private static void DrawFox(float x, float y, float scale)
    {
        var fur = new Color(196, 122, 74, 255);
        var furLight = new Color(212, 160, 112, 255);
        var white = new Color(232, 224, 216, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // Tail
        DrawRotatedEllipse(x + 18 * s, y - 8 * s, 12 * s, 5 * s, 0.4f, fur);
        DrawRotatedEllipse(x + 24 * s, y - 4 * s, 4 * s, 3 * s, 0.4f, white);

        // Body
        DrawEllipse(x, y - 10 * s, 14 * s, 8 * s, fur);
        DrawEllipse(x - 4 * s, y - 12 * s, 6 * s, 4 * s, furLight);

        // Legs
        DrawEllipse(x - 8 * s, y + 1 * s, 2.5f * s, 8 * s, fur);
        DrawEllipse(x + 6 * s, y + 1 * s, 2.5f * s, 8 * s, fur);

        // Ears
        DrawRotatedEllipse(x - 18 * s, y - 26 * s, 3 * s, 6 * s, -0.4f, fur);
        DrawRotatedEllipse(x - 12 * s, y - 26 * s, 3 * s, 6 * s, 0.4f, fur);

        // Head
        DrawEllipse(x - 15 * s, y - 16 * s, 8 * s, 6 * s, fur);

        // Snout
        DrawEllipse(x - 22 * s, y - 14 * s, 5 * s, 3 * s, white);

        // Eye
        DrawCircle(x - 14 * s, y - 17 * s, 1.5f * s, eye);
    }

    private static void DrawWolf(float x, float y, float scale)
    {
        var fur = new Color(106, 106, 106, 255);
        var furLight = new Color(138, 138, 138, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // Tail (behind, curving up)
        DrawRotatedEllipse(x + 22 * s, y - 16 * s, 4 * s, 12 * s, 0.6f, fur);

        // Body
        DrawEllipse(x + 6 * s, y - 12 * s, 18 * s, 10 * s, fur);
        DrawEllipse(x + 2 * s, y - 14 * s, 8 * s, 5 * s, furLight);

        // Legs
        DrawEllipse(x - 4 * s, y + 2 * s, 3 * s, 12 * s, fur);
        DrawEllipse(x + 16 * s, y + 2 * s, 3 * s, 12 * s, fur);

        // Neck
        DrawRotatedEllipse(x - 10 * s, y - 18 * s, 7 * s, 10 * s, -0.3f, fur);

        // Ears
        DrawRotatedEllipse(x - 22 * s, y - 32 * s, 3 * s, 6 * s, -0.3f, fur);
        DrawRotatedEllipse(x - 14 * s, y - 34 * s, 3 * s, 6 * s, 0.3f, fur);

        // Head
        DrawEllipse(x - 18 * s, y - 24 * s, 8 * s, 7 * s, fur);

        // Snout
        DrawEllipse(x - 26 * s, y - 22 * s, 6 * s, 4 * s, furLight);

        // Eye
        DrawCircle(x - 16 * s, y - 26 * s, 2 * s, eye);
    }

    private static void DrawBear(float x, float y, float scale)
    {
        var fur = new Color(90, 74, 58, 255);
        var furLight = new Color(106, 90, 74, 255);
        var snout = new Color(122, 106, 90, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // Body (big and round)
        DrawEllipse(x + 6 * s, y - 14 * s, 22 * s, 14 * s, fur);
        DrawEllipse(x + 2 * s, y - 16 * s, 10 * s, 7 * s, furLight);

        // Legs
        DrawEllipse(x - 8 * s, y + 4 * s, 5 * s, 12 * s, fur);
        DrawEllipse(x + 18 * s, y + 4 * s, 5 * s, 12 * s, fur);

        // Neck/shoulder
        DrawEllipse(x - 12 * s, y - 18 * s, 10 * s, 10 * s, fur);

        // Ears
        DrawCircle(x - 24 * s, y - 32 * s, 4 * s, fur);
        DrawCircle(x - 14 * s, y - 34 * s, 4 * s, fur);

        // Head
        DrawCircle(x - 18 * s, y - 24 * s, 10 * s, fur);

        // Snout
        DrawEllipse(x - 26 * s, y - 22 * s, 6 * s, 4 * s, snout);

        // Eye
        DrawCircle(x - 14 * s, y - 26 * s, 2 * s, eye);
    }

    private static void DrawCaveBear(float x, float y, float scale)
    {
        var fur = new Color(74, 58, 42, 255);
        var furLight = new Color(90, 74, 58, 255);
        var snout = new Color(106, 90, 74, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // Shoulder hump
        DrawEllipse(x + 2 * s, y - 28 * s, 14 * s, 12 * s, fur);

        // Body (massive)
        DrawEllipse(x + 8 * s, y - 14 * s, 26 * s, 16 * s, fur);
        DrawEllipse(x + 4 * s, y - 18 * s, 12 * s, 8 * s, furLight);

        // Legs (thick)
        DrawEllipse(x - 10 * s, y + 6 * s, 6 * s, 14 * s, fur);
        DrawEllipse(x + 22 * s, y + 6 * s, 6 * s, 14 * s, fur);

        // Neck
        DrawEllipse(x - 14 * s, y - 22 * s, 12 * s, 12 * s, fur);

        // Ears
        DrawCircle(x - 28 * s, y - 40 * s, 5 * s, fur);
        DrawCircle(x - 16 * s, y - 42 * s, 5 * s, fur);

        // Head
        DrawCircle(x - 22 * s, y - 30 * s, 12 * s, fur);

        // Snout
        DrawEllipse(x - 32 * s, y - 28 * s, 8 * s, 5 * s, snout);

        // Eye
        DrawCircle(x - 18 * s, y - 32 * s, 2.5f * s, eye);
    }

    private static void DrawHyena(float x, float y, float scale)
    {
        var fur = new Color(138, 122, 90, 255);
        var furDark = new Color(106, 90, 74, 255);
        var spots = new Color(90, 74, 58, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // Tail (short, behind)
        DrawRotatedEllipse(x + 20 * s, y - 14 * s, 3 * s, 8 * s, 0.8f, fur);

        // Body (sloped - higher at shoulders)
        DrawRotatedEllipse(x + 8 * s, y - 10 * s, 18 * s, 10 * s, 0.15f, fur);

        // Spots on body
        DrawCircle(x + 12 * s, y - 12 * s, 3 * s, spots);
        DrawCircle(x + 4 * s, y - 8 * s, 2 * s, spots);
        DrawCircle(x - 2 * s, y - 14 * s, 2.5f * s, spots);

        // Legs (front taller than back)
        DrawEllipse(x - 4 * s, y + 2 * s, 3 * s, 12 * s, fur);
        DrawEllipse(x + 14 * s, y + 4 * s, 3 * s, 10 * s, fur);

        // Mane/shoulders
        DrawEllipse(x - 6 * s, y - 18 * s, 8 * s, 10 * s, furDark);

        // Ears (rounded)
        DrawCircle(x - 20 * s, y - 28 * s, 4 * s, fur);
        DrawCircle(x - 12 * s, y - 30 * s, 4 * s, fur);

        // Head
        DrawEllipse(x - 16 * s, y - 20 * s, 8 * s, 7 * s, fur);

        // Snout
        DrawEllipse(x - 24 * s, y - 18 * s, 6 * s, 4 * s, furDark);

        // Eye
        DrawCircle(x - 14 * s, y - 22 * s, 2 * s, eye);
    }

    private static void DrawMammoth(float x, float y, float scale)
    {
        var fur = new Color(90, 74, 58, 255);
        var furLight = new Color(106, 90, 74, 255);
        var furDark = new Color(74, 58, 42, 255);
        var tusk = new Color(212, 204, 196, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // Shaggy fur (behind body)
        DrawEllipse(x + 8 * s, y - 2 * s, 26 * s, 8 * s, furDark);

        // Body (massive dome)
        DrawEllipse(x + 8 * s, y - 18 * s, 28 * s, 20 * s, fur);
        DrawEllipse(x + 4 * s, y - 22 * s, 14 * s, 10 * s, furLight);

        // Legs (column-like)
        DrawEllipse(x - 12 * s, y + 8 * s, 6 * s, 16 * s, fur);
        DrawEllipse(x + 24 * s, y + 8 * s, 6 * s, 16 * s, fur);

        // Head (connected to body)
        DrawEllipse(x - 20 * s, y - 20 * s, 14 * s, 14 * s, fur);

        // Trunk (series of connected ellipses going down then curling)
        DrawRotatedEllipse(x - 30 * s, y - 12 * s, 5 * s, 10 * s, -0.2f, furLight);
        DrawRotatedEllipse(x - 34 * s, y - 2 * s, 4 * s, 8 * s, -0.1f, furLight);
        DrawRotatedEllipse(x - 36 * s, y + 8 * s, 4 * s, 6 * s, 0.3f, furLight);
        DrawEllipse(x - 34 * s, y + 14 * s, 4 * s, 4 * s, furLight);

        // Tusks (curving forward and up)
        DrawRotatedEllipse(x - 30 * s, y - 8 * s, 3 * s, 14 * s, -0.5f, tusk);
        DrawRotatedEllipse(x - 38 * s, y - 14 * s, 3 * s, 10 * s, -1.2f, tusk);

        // Ear (small flap)
        DrawEllipse(x - 10 * s, y - 26 * s, 5 * s, 7 * s, furDark);

        // Eye
        DrawCircle(x - 18 * s, y - 24 * s, 2.5f * s, eye);
    }

    private static void DrawSaberTooth(float x, float y, float scale)
    {
        var fur = new Color(201, 160, 80, 255);
        var furLight = new Color(217, 184, 104, 255);
        var furDark = new Color(160, 128, 48, 255);
        var fang = new Color(232, 228, 220, 255);
        var nose = new Color(90, 74, 58, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // Tail (behind, hanging down from rear)
        DrawRotatedEllipse(x + 26 * s, y - 6 * s, 4 * s, 14 * s, 0.3f, fur);
        DrawCircle(x + 30 * s, y + 6 * s, 3 * s, furDark);

        // Body (muscular cat)
        DrawEllipse(x + 8 * s, y - 12 * s, 20 * s, 12 * s, fur);
        DrawEllipse(x + 4 * s, y - 14 * s, 10 * s, 6 * s, furLight);

        // Back legs
        DrawEllipse(x + 18 * s, y + 2 * s, 5 * s, 12 * s, fur);

        // Front legs (powerful)
        DrawEllipse(x - 8 * s, y + 2 * s, 4 * s, 12 * s, fur);

        // Chest/shoulders
        DrawEllipse(x - 10 * s, y - 14 * s, 10 * s, 10 * s, fur);

        // Neck
        DrawEllipse(x - 16 * s, y - 20 * s, 8 * s, 10 * s, fur);

        // Ears (rounded cat ears)
        DrawCircle(x - 28 * s, y - 36 * s, 4 * s, fur);
        DrawCircle(x - 18 * s, y - 38 * s, 4 * s, fur);

        // Head (broad, cat-like)
        DrawCircle(x - 22 * s, y - 28 * s, 10 * s, fur);

        // Muzzle
        DrawEllipse(x - 30 * s, y - 24 * s, 6 * s, 5 * s, furLight);

        // Nose
        DrawCircle(x - 32 * s, y - 26 * s, 2 * s, nose);

        // FANGS (long sabertooth canines)
        DrawRotatedEllipse(x - 32 * s, y - 14 * s, 2 * s, 10 * s, 0.1f, fang);
        DrawRotatedEllipse(x - 26 * s, y - 14 * s, 2 * s, 10 * s, -0.1f, fang);

        // Eye
        DrawCircle(x - 20 * s, y - 30 * s, 2.5f * s, eye);
    }

    private static void DrawMegaloceros(float x, float y, float scale)
    {
        var body = new Color(122, 106, 90, 255);
        var bodyLight = new Color(138, 122, 106, 255);
        var antler = new Color(74, 58, 42, 255);
        var eye = new Color(42, 42, 42, 255);

        float s = scale;

        // MASSIVE palmate antlers (behind head)
        // Left antler
        DrawRotatedEllipse(x - 24 * s, y - 48 * s, 4 * s, 18 * s, -0.5f, antler);
        DrawRotatedEllipse(x - 34 * s, y - 56 * s, 3 * s, 14 * s, -0.9f, antler);
        DrawRotatedEllipse(x - 16 * s, y - 58 * s, 3 * s, 12 * s, 0.1f, antler);
        DrawRotatedEllipse(x - 40 * s, y - 50 * s, 2 * s, 8 * s, -1.2f, antler);
        // Right antler
        DrawRotatedEllipse(x - 6 * s, y - 50 * s, 4 * s, 18 * s, 0.5f, antler);
        DrawRotatedEllipse(x + 4 * s, y - 58 * s, 3 * s, 14 * s, 0.9f, antler);
        DrawRotatedEllipse(x - 12 * s, y - 60 * s, 3 * s, 12 * s, -0.1f, antler);
        DrawRotatedEllipse(x + 10 * s, y - 52 * s, 2 * s, 8 * s, 1.2f, antler);

        // Body
        DrawEllipse(x + 10 * s, y - 14 * s, 22 * s, 14 * s, body);
        DrawEllipse(x + 6 * s, y - 16 * s, 10 * s, 7 * s, bodyLight);

        // Legs
        DrawEllipse(x - 4 * s, y + 4 * s, 3 * s, 14 * s, body);
        DrawEllipse(x + 22 * s, y + 4 * s, 3 * s, 14 * s, body);

        // Neck (long, upright)
        DrawRotatedEllipse(x - 8 * s, y - 26 * s, 6 * s, 14 * s, -0.2f, body);

        // Head
        DrawEllipse(x - 14 * s, y - 38 * s, 7 * s, 6 * s, body);

        // Snout
        DrawEllipse(x - 20 * s, y - 36 * s, 5 * s, 3 * s, bodyLight);

        // Eye
        DrawCircle(x - 12 * s, y - 39 * s, 2 * s, eye);
    }

    private static void DrawBison(float x, float y, float scale)
    {
        var fur = new Color(74, 58, 42, 255);
        var furLight = new Color(90, 74, 58, 255);
        var mane = new Color(58, 42, 26, 255);
        var horn = new Color(42, 42, 42, 255);
        var eye = new Color(26, 26, 26, 255);

        float s = scale;

        // Body (huge)
        DrawEllipse(x + 12 * s, y - 10 * s, 26 * s, 14 * s, fur);
        DrawEllipse(x + 8 * s, y - 12 * s, 12 * s, 7 * s, furLight);

        // Shoulder hump (massive)
        DrawEllipse(x - 6 * s, y - 22 * s, 18 * s, 16 * s, mane);

        // Legs
        DrawEllipse(x - 14 * s, y + 6 * s, 5 * s, 14 * s, fur);
        DrawEllipse(x + 26 * s, y + 6 * s, 5 * s, 14 * s, fur);

        // Neck (thick, going down)
        DrawRotatedEllipse(x - 18 * s, y - 12 * s, 10 * s, 14 * s, 0.4f, mane);

        // Horns (on lowered head)
        DrawRotatedEllipse(x - 34 * s, y - 18 * s, 3 * s, 8 * s, -0.8f, horn);
        DrawRotatedEllipse(x - 22 * s, y - 20 * s, 3 * s, 8 * s, 0.8f, horn);

        // Head (low, grazing position)
        DrawEllipse(x - 28 * s, y - 8 * s, 10 * s, 8 * s, fur);

        // Beard
        DrawEllipse(x - 34 * s, y - 2 * s, 5 * s, 8 * s, mane);

        // Snout
        DrawEllipse(x - 36 * s, y - 6 * s, 6 * s, 4 * s, furLight);

        // Eye
        DrawCircle(x - 24 * s, y - 10 * s, 2 * s, eye);
    }

    private static void DrawRat(float x, float y, float scale)
    {
        var fur = new Color(106, 90, 74, 255);
        var furLight = new Color(122, 106, 90, 255);
        var ear = new Color(138, 106, 90, 255);
        var tail = new Color(90, 74, 58, 255);
        var eye = new Color(26, 26, 26, 255);
        var whisker = new Color(74, 74, 74, 255);

        float s = scale;

        // Tail
        DrawRotatedEllipse(x + 16 * s, y - 4 * s, 14 * s, 2 * s, 0.2f, tail);

        // Body
        DrawEllipse(x, y - 6 * s, 10 * s, 6 * s, fur);
        DrawEllipse(x - 3 * s, y - 7 * s, 5 * s, 3 * s, furLight);

        // Ears
        DrawCircle(x - 10 * s, y - 16 * s, 4 * s, ear);
        DrawCircle(x - 4 * s, y - 17 * s, 4 * s, ear);

        // Head
        DrawEllipse(x - 8 * s, y - 10 * s, 6 * s, 5 * s, fur);

        // Snout
        DrawEllipse(x - 14 * s, y - 9 * s, 4 * s, 2.5f * s, furLight);

        // Eye
        DrawCircle(x - 7 * s, y - 11 * s, 1.5f * s, eye);

        // Whiskers
        float whiskerThickness = 0.5f * s;
        Raylib.DrawLineEx(new Vector2(x - 16 * s, y - 10 * s), new Vector2(x - 20 * s, y - 12 * s), whiskerThickness, whisker);
        Raylib.DrawLineEx(new Vector2(x - 16 * s, y - 9 * s), new Vector2(x - 20 * s, y - 9 * s), whiskerThickness, whisker);
        Raylib.DrawLineEx(new Vector2(x - 16 * s, y - 8 * s), new Vector2(x - 20 * s, y - 6 * s), whiskerThickness, whisker);
    }

    private static void DrawDefault(float x, float y, float scale)
    {
        // Generic animal silhouette
        var color = new Color(120, 120, 120, 255);
        DrawEllipse(x, y, scale * 6, scale * 4, color);
        DrawCircle(x - scale * 5, y - scale, scale * 2.5f, color);
    }
}
