using Raylib_cs;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Renders icons using textures from assets/icons/, falling back to procedural rendering
/// when a texture doesn't exist.
/// </summary>
public class TextureIconRenderer : IIconRenderer, IDisposable
{
    private readonly ProceduralIconRenderer _fallback;
    private readonly Dictionary<string, Texture2D> _textures = new();
    private bool _disposed;

    public TextureIconRenderer(ProceduralIconRenderer fallback)
    {
        _fallback = fallback;
        LoadTextures();
    }

    private void LoadTextures()
    {
        string assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icons");

        // Also check relative to working directory (for development)
        if (!Directory.Exists(assetsPath))
        {
            assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", "icons");
        }

        if (!Directory.Exists(assetsPath))
            return;

        foreach (string filePath in Directory.GetFiles(assetsPath, "*.png"))
        {
            string iconName = Path.GetFileNameWithoutExtension(filePath);
            Texture2D texture = Raylib.LoadTexture(filePath);

            if (texture.Id != 0)
            {
                _textures[iconName] = texture;
            }
        }
    }

    public Color GetIconColor(string iconName)
    {
        // Colors are always defined in procedural renderer
        return _fallback.GetIconColor(iconName);
    }

    public void DrawIcon(string iconName, float x, float y, float size, Color tint, bool hasGlow = false)
    {
        if (_textures.TryGetValue(iconName, out Texture2D texture))
        {
            DrawTextureIcon(texture, x, y, size, tint, hasGlow);
        }
        else
        {
            _fallback.DrawIcon(iconName, x, y, size, tint, hasGlow);
        }
    }

    private static void DrawTextureIcon(Texture2D texture, float x, float y, float size, Color tint, bool hasGlow)
    {
        float centerX = x + size / 2;
        float centerY = y + size / 2;

        // Source rectangle (entire texture)
        var source = new Rectangle(0, 0, texture.Width, texture.Height);

        // Draw glow if requested (larger, semi-transparent version behind)
        if (hasGlow)
        {
            float glowSize = size * 1.4f;
            float glowX = centerX - glowSize / 2;
            float glowY = centerY - glowSize / 2;
            var glowDest = new Rectangle(glowX, glowY, glowSize, glowSize);
            var glowTint = new Color(tint.R, tint.G, tint.B, (byte)60);

            Raylib.DrawTexturePro(texture, source, glowDest, System.Numerics.Vector2.Zero, 0f, glowTint);
        }

        // Draw main icon
        var dest = new Rectangle(x, y, size, size);
        Raylib.DrawTexturePro(texture, source, dest, System.Numerics.Vector2.Zero, 0f, tint);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        foreach (var texture in _textures.Values)
        {
            Raylib.UnloadTexture(texture);
        }
        _textures.Clear();

        _disposed = true;
    }

    ~TextureIconRenderer()
    {
        Dispose(false);
    }
}
