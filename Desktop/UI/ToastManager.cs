using ImGuiNET;
using System.Numerics;
using text_survival.UI;

namespace text_survival.Desktop.UI;

/// <summary>
/// Draws the toasts in <see cref="ToastFeed"/> at the top-centre of the screen, fading
/// each one out as it expires.
/// </summary>
public static class ToastManager
{
    private const float FadeOutDuration = 0.5f;
    // Colors for different toast types
    private static readonly Vector4 ColorInfo = new(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Vector4 ColorSuccess = new(0.3f, 0.8f, 0.3f, 1f);
    private static readonly Vector4 ColorWarning = new(1f, 0.8f, 0.3f, 1f);
    private static readonly Vector4 ColorDanger = new(1f, 0.3f, 0.3f, 1f);

    /// <summary>
    /// Tick the feed down and draw what is left. Once per frame.
    /// </summary>
    public static void Render(float deltaTime)
    {
        ToastFeed.Tick(deltaTime);

        var toasts = ToastFeed.Active;
        if (toasts.Count == 0) return;

        // Position at top-center of screen (avoids overlap with side panels)
        var io = ImGui.GetIO();
        float startX = (io.DisplaySize.X - 300) / 2;
        float startY = 10;
        float spacing = 5;
        float currentY = startY;

        for (int i = 0; i < toasts.Count; i++)
        {
            var toast = toasts[i];

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
            ImGui.SetNextWindowPos(new Vector2(startX, currentY), ImGuiCond.Always);
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
                ImGui.PushStyleColor(ImGuiCol.Text, color);
                UiText.Wrapped(toast.Message);
                ImGui.PopStyleColor();
            }
            currentY += ImGui.GetWindowSize().Y + spacing;
            ImGui.End();

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
        }
    }
}
