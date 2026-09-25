using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using System.Numerics;

namespace text_survival.Desktop.UI;

public static class HudWidgets
{
    public static readonly Vector4 Heading = new(.45f, .7f, .9f, 1);
    public static readonly Vector4 Warning = UiStyle.Caution;
    public const ImGuiWindowFlags PanelFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove |
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoBringToFrontOnFocus;

    public static void Begin(string id, HudRect rect)
    {
        ImGui.SetNextWindowPos(rect.Position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(rect.Size, ImGuiCond.Always);
        ImGui.Begin(id, PanelFlags);
    }

    public static void Section(string title)
    {
        ImGui.Spacing();
        UiText.Colored(Heading, title.ToUpperInvariant());
        ImGui.Separator();
    }

    public static bool Action(HudAction action, bool enabled = true)
    {
        // Global commands remain owned by MapInputRouter; this renders the same key hint.
        var control = action.Control with { Label = action.Control.DisplayLabel, Shortcut = null };
        return UiControls.Button(control, enabled, new Vector2(-1, 0));
    }

    public static void VitalBar(float fraction, Vector4 color, string label, bool warning)
    {
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, UiStyle.BarFill(color));
        ImGui.ProgressBar(fraction, new Vector2(-1, UiStyle.BarHeight), label);
        ImGui.PopStyleColor();
        if (warning && GameGui.Capture == null)
            ImGuiNET.ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(),
                ImGui.GetItemRectMin() + ImGui.GetItemRectSize(), ImGui.GetColorU32(color), 0, ImDrawFlags.None, 2);
    }

    public static string Duration(int minutes) => minutes >= 60
        ? (minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes / 60}h {minutes % 60}m") : $"{minutes}m";
}
