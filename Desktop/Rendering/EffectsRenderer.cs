using Raylib_cs;
using System.Numerics;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Renders visual effects: snow particles, vignette, night overlay.
/// </summary>
public class EffectsRenderer
{
    private readonly List<SnowParticle> _snowParticles = new();
    private const int BaseSnowParticleCount = 10;
    private const int MaxSnowParticleCount = 60;
    private readonly Random _random = new();

    // Current weather parameters
    private double _currentPrecipitation = 0.3;
    private double _currentWindSpeed = 5;

    public EffectsRenderer()
    {
        InitSnowParticles();
    }

    /// <summary>
    /// Update weather parameters to scale snow effects.
    /// </summary>
    /// <param name="precipitation">Precipitation level (0-1)</param>
    /// <param name="windSpeed">Wind speed in mph</param>
    public void UpdateWeather(double precipitation, double windSpeed)
    {
        _currentPrecipitation = precipitation;
        _currentWindSpeed = windSpeed;

        // Adjust particle count based on precipitation
        int targetCount = (int)(BaseSnowParticleCount + precipitation * (MaxSnowParticleCount - BaseSnowParticleCount));
        targetCount = Math.Clamp(targetCount, BaseSnowParticleCount, MaxSnowParticleCount);

        // Add or remove particles to match target
        while (_snowParticles.Count < targetCount)
        {
            _snowParticles.Add(CreateParticle());
        }
        while (_snowParticles.Count > targetCount)
        {
            _snowParticles.RemoveAt(_snowParticles.Count - 1);
        }
    }

    private SnowParticle CreateParticle()
    {
        // Base speed scaled by precipitation (heavier snow = faster fall)
        float baseSpeed = 0.02f + (float)(_currentPrecipitation * 0.03f);
        // Drift scaled by wind
        float baseDrift = (float)(_currentWindSpeed * 0.002f);

        return new SnowParticle
        {
            X = (float)_random.NextDouble(),
            Y = (float)_random.NextDouble(),
            Speed = baseSpeed + (float)_random.NextDouble() * 0.02f,
            Drift = baseDrift + ((float)_random.NextDouble() - 0.3f) * 0.01f,  // Bias drift in wind direction
            Size = 2 + (float)_random.NextDouble() * 2,
            Alpha = 0.3f + (float)(_currentPrecipitation * 0.4f)
        };
    }

    /// <summary>
    /// Initialize snow particles.
    /// </summary>
    private void InitSnowParticles()
    {
        _snowParticles.Clear();
        for (int i = 0; i < BaseSnowParticleCount; i++)
        {
            _snowParticles.Add(CreateParticle());
        }
    }

    /// <summary>
    /// Update particle positions.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Scale speed and drift by current weather
        float windFactor = 1 + (float)(_currentWindSpeed * 0.05f);
        float precipFactor = 1 + (float)(_currentPrecipitation * 0.5f);

        foreach (var particle in _snowParticles)
        {
            // Fall down (faster in heavier precipitation)
            particle.Y += particle.Speed * deltaTime * 30 * precipFactor;

            // Drift sideways (more with stronger wind)
            particle.X += particle.Drift * deltaTime * 30 * windFactor;

            // Wrap around
            if (particle.Y > 1.1f)
            {
                particle.Y = -0.1f;
                particle.X = (float)_random.NextDouble();
                // Refresh particle properties on respawn
                particle.Speed = 0.02f + (float)(_currentPrecipitation * 0.03f) + (float)_random.NextDouble() * 0.02f;
                particle.Drift = (float)(_currentWindSpeed * 0.002f) + ((float)_random.NextDouble() - 0.3f) * 0.01f;
                particle.Alpha = 0.3f + (float)(_currentPrecipitation * 0.4f);
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
