using ImGuiNET;
using System.Numerics;

namespace text_survival.Desktop.UI;

public static class HudWidgets
{
    public static readonly Vector4 Heading = new(.45f, .7f, .9f, 1);
    public static readonly Vector4 Warning = new(1, .7f, .3f, 1);
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
        ImGui.PushID(action.Id);
        ImGui.BeginDisabled(!enabled || !action.Enabled);
        string label = action.Label + (action.Shortcut.HasValue ? " " + Input.HotkeyRegistry.GetTip(action.Shortcut.Value) : "");
        // Wrap labels instead of clipping durations and resource counts off the end.
        float width = ImGui.GetContentRegionAvail().X;
        var size = ImGui.CalcTextSize(label, false, Math.Max(1, width - 16));
        bool clicked = ImGui.Button("##action", new Vector2(width, Math.Max(ImGui.GetFrameHeight(), size.Y + 10)));
        var min = ImGui.GetItemRectMin();
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), min + new Vector2(8, 5),
            ImGui.GetColorU32(ImGuiCol.Text), label, Math.Max(1, width - 16));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && action.DisabledReason != null)
            UiText.Tooltip(action.DisabledReason);
        ImGui.PopID();
        return clicked;
    }

    public static string Duration(int minutes) => minutes >= 60
        ? (minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes / 60}h {minutes % 60}m") : $"{minutes}m";
}
