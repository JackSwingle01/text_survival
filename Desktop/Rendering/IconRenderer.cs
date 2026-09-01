using Raylib_cs;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Draws feature icons on the map from the pixel art in assets/icons/.
/// The icon name a feature exposes as its MapIcon is the file's basename, so authoring
/// a new icon is dropping a PNG in - no code change, and no second place to update.
/// </summary>
public class IconRenderer : IDisposable
{
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly HashSet<string> _reportedMissing = [];
    private bool _disposed;

    public IconRenderer(string assetsPath)
    {
        if (!Directory.Exists(assetsPath))
            throw new DirectoryNotFoundException($"Icon assets directory not found: {assetsPath}");

        foreach (string filePath in Directory.GetFiles(assetsPath, "*.png"))
        {
            Texture2D texture = Raylib.LoadTexture(filePath);
            if (texture.Id == 0)
            {
                Console.Error.WriteLine($"IconRenderer: failed to load '{filePath}'.");
                continue;
            }

            // Pixel art must not be smoothed when scaled up.
            Raylib.SetTextureFilter(texture, TextureFilter.Point);
            _textures[Path.GetFileNameWithoutExtension(filePath)] = texture;
        }
    }

    /// <summary>
    /// Draw an icon in the given square. A glow color draws a larger, faint copy behind it -
    /// the caller decides what glows and in what color, because the reason (a lit fire, a
    /// snare with a catch) is what the color means.
    /// </summary>
    public void DrawIcon(string iconName, float x, float y, float size, Color? glow = null)
    {
        if (!_textures.TryGetValue(iconName, out Texture2D texture))
        {
            if (_reportedMissing.Add(iconName))
                Console.Error.WriteLine($"IconRenderer: no art for icon '{iconName}' - nothing drawn.");
            return;
        }

        var source = new Rectangle(0, 0, texture.Width, texture.Height);

        if (glow.HasValue)
        {
            float glowSize = size * 1.4f;
            var glowDest = new Rectangle(
                x + size / 2 - glowSize / 2,
                y + size / 2 - glowSize / 2,
                glowSize, glowSize);
            var glowTint = new Color(glow.Value.R, glow.Value.G, glow.Value.B, (byte)60);
            Raylib.DrawTexturePro(texture, source, glowDest, System.Numerics.Vector2.Zero, 0f, glowTint);
        }

        Raylib.DrawTexturePro(texture, source, new Rectangle(x, y, size, size),
            System.Numerics.Vector2.Zero, 0f, Color.White);
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var texture in _textures.Values)
            Raylib.UnloadTexture(texture);
        _textures.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
