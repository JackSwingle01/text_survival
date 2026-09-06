using System.Numerics;

namespace text_survival.Desktop.UI;

public readonly record struct HudRect(float X, float Y, float Width, float Height)
{
    public Vector2 Position => new(X, Y);
    public Vector2 Size => new(Width, Height);
    public bool Contains(Vector2 point) => point.X >= X && point.Y >= Y && point.X < X + Width && point.Y < Y + Height;
}

/// <summary>One layout calculation for both ImGui panels and the Raylib map, in logical pixels.</summary>
public sealed record HudLayout(HudRect Top, HudRect Survivor, HudRect Map, HudRect Inspector, HudRect Events)
{
    public static HudLayout Calculate(float width, float height, float fontSize, bool historyExpanded)
    {
        float scale = Math.Max(1, fontSize / 16.25f);
        float gap = 6;
        float top = Math.Max(42, fontSize + 24);
        float left = Math.Min(290 * scale, width * .25f);
        float right = Math.Min(320 * scale, width * .27f);
        float body = Math.Max(1, height - top - gap * 2);
        float center = Math.Max(1, width - left - right - gap * 4);
        float log = historyExpanded ? Math.Min(body * .45f, 340 * scale) : (height < 800 ? 76 : 112) * scale;
        log = Math.Min(log, body * .45f);
        return new(new(0, 0, width, top), new(gap, top + gap, left, body),
            new(left + gap * 2, top + gap, center, Math.Max(1, body - log - gap)),
            new(width - right - gap, top + gap, right, body),
            new(left + gap * 2, height - gap - log, center, log));
    }
}
