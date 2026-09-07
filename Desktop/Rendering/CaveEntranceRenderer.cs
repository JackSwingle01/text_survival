using System.Numerics;
using Raylib_cs;

namespace text_survival.Desktop.Rendering;

/// <summary>A native pixel-art stone brow, anchored to the outdoor/cave boundary.</summary>
internal static class CaveEntranceRenderer
{
    private static Texture2D _texture;

    public static void Load(string path)
    {
        Unload();
        _texture = Raylib.LoadTexture(Path.Combine(path, "cave_entrance.png"));
        if (_texture.Id == 0) throw new IOException("Missing cave entrance art.");
        Raylib.SetTextureFilter(_texture, TextureFilter.Point);
    }

    public static void Unload()
    {
        if (_texture.Id != 0) Raylib.UnloadTexture(_texture);
        _texture = default;
    }

    public static void Draw(Vector2 boundary, Vector2 inward, float pitch, float timeFactor)
    {
        // Authored facing down (outdoors), with its brow above the threshold.
        // Rotate that inward normal (0,-1) to the cave's actual direction.
        float angle = MathF.Atan2(inward.X, -inward.Y) * 180 / MathF.PI;
        float scale = pitch * .9f / 48;
        byte light = (byte)(255 * (.4f + Math.Clamp(timeFactor, 0, 1) * .6f));
        Raylib.DrawTexturePro(_texture, new Rectangle(0, 0, _texture.Width, _texture.Height),
            new Rectangle(boundary.X, boundary.Y, 48 * scale, 24 * scale),
            new Vector2(24 * scale, 17 * scale), angle, new Color(light, light, light, (byte)255));
    }
}
