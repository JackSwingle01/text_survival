using Raylib_cs;
using System.Numerics;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Renders visual effects: snow particles, vignette, night overlay.
/// </summary>
public class EffectsRenderer
{
    private readonly List<SnowParticle> _snowParticles = new();
    private const int SnowParticleCount = 30;
    private readonly Random _random = new();

    public EffectsRenderer()
    {
        InitSnowParticles();
    }

    /// <summary>
    /// Initialize snow particles.
    /// </summary>
    private void InitSnowParticles()
    {
        _snowParticles.Clear();
        for (int i = 0; i < SnowParticleCount; i++)
        {
            _snowParticles.Add(new SnowParticle
            {
                X = (float)_random.NextDouble(),
                Y = (float)_random.NextDouble(),
                Speed = 0.02f + (float)_random.NextDouble() * 0.03f,
                Drift = ((float)_random.NextDouble() - 0.5f) * 0.01f,
                Size = 2 + (float)_random.NextDouble() * 2,
                Alpha = 0.3f + (float)_random.NextDouble() * 0.4f
            });
        }
    }

    /// <summary>
    /// Update particle positions.
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (var particle in _snowParticles)
        {
            // Fall down
            particle.Y += particle.Speed * deltaTime * 30;

            // Drift sideways
            particle.X += particle.Drift * deltaTime * 30;

            // Wrap around
            if (particle.Y > 1.1f)
            {
                particle.Y = -0.1f;
                particle.X = (float)_random.NextDouble();
            }
            if (particle.X < -0.1f) particle.X = 1.1f;
            if (particle.X > 1.1f) particle.X = -0.1f;
        }
    }

    /// <summary>
    /// Render snow particles.
    /// </summary>
    public void RenderSnow(float offsetX, float offsetY, float width, float height)
    {
        foreach (var particle in _snowParticles)
        {
            float px = offsetX + particle.X * width;
            float py = offsetY + particle.Y * height;

            var color = new Color((byte)255, (byte)255, (byte)255, (byte)(particle.Alpha * 255));
            Raylib.DrawCircle((int)px, (int)py, particle.Size, color);
        }
    }

    /// <summary>
    /// Render vignette effect (darkening at edges).
    /// </summary>
    public void RenderVignette(float offsetX, float offsetY, float width, float height)
    {
        // Draw gradient overlay at edges
        float vignetteWidth = width * 0.15f;
        float vignetteHeight = height * 0.15f;

        // Top edge
        DrawGradientRect(
            offsetX, offsetY,
            width, vignetteHeight,
            new Color(0, 0, 0, 80),
            new Color(0, 0, 0, 0),
            vertical: true);

        // Bottom edge
        DrawGradientRect(
            offsetX, offsetY + height - vignetteHeight,
            width, vignetteHeight,
            new Color(0, 0, 0, 0),
            new Color(0, 0, 0, 80),
            vertical: true);

        // Left edge
        DrawGradientRect(
            offsetX, offsetY,
            vignetteWidth, height,
            new Color(0, 0, 0, 80),
            new Color(0, 0, 0, 0),
            vertical: false);

        // Right edge
        DrawGradientRect(
            offsetX + width - vignetteWidth, offsetY,
            vignetteWidth, height,
            new Color(0, 0, 0, 0),
            new Color(0, 0, 0, 80),
            vertical: false);

        // Corners (darker)
        float cornerSize = Math.Min(vignetteWidth, vignetteHeight);
        DrawCornerVignette(offsetX, offsetY, cornerSize, topLeft: true);
        DrawCornerVignette(offsetX + width - cornerSize, offsetY, cornerSize, topLeft: false);
        DrawCornerVignette(offsetX, offsetY + height - cornerSize, cornerSize, topLeft: true, flipY: true);
        DrawCornerVignette(offsetX + width - cornerSize, offsetY + height - cornerSize, cornerSize, topLeft: false, flipY: true);
    }

    /// <summary>
    /// Render night overlay (darker at night).
    /// </summary>
    public void RenderNightOverlay(float offsetX, float offsetY, float width, float height, float timeFactor)
    {
        // timeFactor: 0 = midnight (darkest), 1 = noon (no overlay)
        // Calculate darkness level
        float darkness = (1 - timeFactor) * 0.6f; // Max 60% dark at midnight

        if (darkness > 0.01f)
        {
            var nightColor = new Color((byte)10, (byte)15, (byte)30, (byte)(darkness * 255));
            Raylib.DrawRectangle((int)offsetX, (int)offsetY, (int)width, (int)height, nightColor);
        }
    }

    /// <summary>
    /// Draw a gradient rectangle.
    /// </summary>
    private static void DrawGradientRect(float x, float y, float w, float h, Color startColor, Color endColor, bool vertical)
    {
        if (vertical)
        {
            Raylib.DrawRectangleGradientV((int)x, (int)y, (int)w, (int)h, startColor, endColor);
        }
        else
        {
            Raylib.DrawRectangleGradientH((int)x, (int)y, (int)w, (int)h, startColor, endColor);
        }
    }

    /// <summary>
    /// Draw a corner vignette.
    /// </summary>
    private static void DrawCornerVignette(float x, float y, float size, bool topLeft, bool flipY = false)
    {
        // Draw as series of fading rectangles
        int steps = 5;
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            float alpha = 40 * (1 - t);

            float rectSize = size * (1 - t * 0.5f);
            float rectX = topLeft ? x : x + size - rectSize;
            float rectY = flipY ? y + size - rectSize : y;

            var color = new Color((byte)0, (byte)0, (byte)0, (byte)alpha);
            Raylib.DrawRectangle((int)rectX, (int)rectY, (int)rectSize, (int)rectSize, color);
        }
    }

    /// <summary>
    /// Snow particle data.
    /// </summary>
    private class SnowParticle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Speed { get; set; }
        public float Drift { get; set; }
        public float Size { get; set; }
        public float Alpha { get; set; }
    }
}
