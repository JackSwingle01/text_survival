using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.UI;

namespace text_survival.Desktop.UI;

/// <summary>
/// The recent narrative, kept on screen. Toasts say what just happened and then vanish;
/// this is where the player looks to reconstruct why - the trail of causes behind a
/// wound, a lost fire, or an animal that found them.
/// </summary>
public static class JournalPanel
{
    private const int VisibleLines = 8;
    private const float PanelWidth = 280f;
    private const float PanelHeight = 170f;

    private static readonly Vector4 ColorNormal = new(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Vector4 ColorSuccess = new(0.4f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 ColorWarning = new(1f, 0.8f, 0.3f, 1f);
    private static readonly Vector4 ColorDanger = new(1f, 0.35f, 0.35f, 1f);
    private static readonly Vector4 ColorSystem = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 ColorDiscovery = new(0.6f, 0.8f, 1f, 1f);
    private static readonly Vector4 ColorTimestamp = new(0.5f, 0.5f, 0.5f, 1f);

    public static void Render(GameContext ctx)
    {
        var entries = ctx.Log.Recent(VisibleLines);
        if (entries.Count == 0)
            return;

        float screenHeight = ImGui.GetIO().DisplaySize.Y;
        ImGui.SetNextWindowPos(new Vector2(10, screenHeight - PanelHeight - 10), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(PanelWidth, PanelHeight), ImGuiCond.Always);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                                 ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;

        if (ImGui.Begin("Journal", flags))
        {
            foreach (var (text, level, timestamp) in entries)
            {
                if (!string.IsNullOrEmpty(timestamp))
                {
                    UiText.Colored(ColorTimestamp, timestamp);
                    ImGui.SameLine();
                }

                ImGui.PushTextWrapPos(0);
                UiText.Colored(ColorFor(level), text);
                ImGui.PopTextWrapPos();
            }

            // Keep the newest line in view as entries arrive.
            ImGui.SetScrollHereY(1.0f);
        }
        ImGui.End();
    }

    private static Vector4 ColorFor(LogLevel level) => level switch
    {
        LogLevel.Success => ColorSuccess,
        LogLevel.Warning => ColorWarning,
        LogLevel.Danger => ColorDanger,
        LogLevel.System => ColorSystem,
        LogLevel.Discovery => ColorDiscovery,
        _ => ColorNormal
    };
}
