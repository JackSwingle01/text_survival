using ImGuiNET;
using System.Numerics;

namespace text_survival.Desktop.UI;

/// <summary>
/// Types of toast notifications with different visual styling.
/// </summary>
public enum ToastType
{
    Info,
    Success,
    Warning,
    Danger
}

/// <summary>
/// A single toast notification.
/// </summary>
internal class Toast
{
    public string Message { get; init; } = "";
    public ToastType Type { get; init; }
    public float Duration { get; init; }
    public float TimeRemaining { get; set; }
}

/// <summary>
/// Manages floating toast notifications that appear in the top-right corner.
/// Auto-dismisses after configurable duration with fade-out animation.
/// </summary>
public static class ToastManager
{
    private static readonly List<Toast> _toasts = new();
    private const float FadeOutDuration = 0.5f;
    private const int MaxToasts = 5;

    // Colors for different toast types
    private static readonly Vector4 ColorInfo = new(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Vector4 ColorSuccess = new(0.3f, 0.8f, 0.3f, 1f);
    private static readonly Vector4 ColorWarning = new(1f, 0.8f, 0.3f, 1f);
    private static readonly Vector4 ColorDanger = new(1f, 0.3f, 0.3f, 1f);

    /// <summary>
    /// Show a toast notification.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="type">The type of toast (affects color).</param>
    /// <param name="duration">How long to display (seconds).</param>
    public static void Show(string message, ToastType type = ToastType.Info, float duration = 4f)
    {
        // Remove oldest if at max
        while (_toasts.Count >= MaxToasts)
        {
            _toasts.RemoveAt(0);
        }

        _toasts.Add(new Toast
        {
            Message = message,
            Type = type,
            Duration = duration,
            TimeRemaining = duration
        });
    }

    /// <summary>
    /// Render all active toasts. Call each frame.
    /// </summary>
    /// <param name="deltaTime">Time since last frame in seconds.</param>
    public static void Render(float deltaTime)
    {
        // Update timers and remove expired toasts
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            _toasts[i].TimeRemaining -= deltaTime;
            if (_toasts[i].TimeRemaining <= 0)
            {
                _toasts.RemoveAt(i);
            }
        }

        if (_toasts.Count == 0) return;

        // Position in top-right corner
        var io = ImGui.GetIO();
        float startX = io.DisplaySize.X - 310;
        float startY = 60;
        float spacing = 5;

        for (int i = 0; i < _toasts.Count; i++)
        {
            var toast = _toasts[i];

            // Calculate alpha (fade out in final 0.5 seconds)
            float alpha = toast.TimeRemaining < FadeOutDuration
                ? toast.TimeRemaining / FadeOutDuration
                : 1f;

            // Get color based on type
            Vector4 baseColor = toast.Type switch
            {
                ToastType.Success => ColorSuccess,
                ToastType.Warning => ColorWarning,
                ToastType.Danger => ColorDanger,
                _ => ColorInfo
            };

            // Apply alpha
            var color = new Vector4(baseColor.X, baseColor.Y, baseColor.Z, baseColor.W * alpha);
            var bgColor = new Vector4(0.15f, 0.15f, 0.15f, 0.9f * alpha);

            // Position this toast
            float yOffset = i * (30 + spacing);
            ImGui.SetNextWindowPos(new Vector2(startX, startY + yOffset), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(300, 0), ImGuiCond.Always);

            // Style the window
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 8));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, bgColor);
            ImGui.PushStyleColor(ImGuiCol.Border, color);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                                      ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse |
                                      ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing |
                                      ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNav;

            if (ImGui.Begin($"##Toast{i}", flags))
            {
                ImGui.TextColored(color, toast.Message);
            }
            ImGui.End();

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
        }
    }

    /// <summary>
    /// Clear all active toasts.
    /// </summary>
    public static void Clear()
    {
        _toasts.Clear();
    }
}
