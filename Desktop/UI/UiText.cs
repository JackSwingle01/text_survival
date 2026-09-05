using ImGuiNET;
using System.Numerics;

namespace text_survival.Desktop.UI;

/// <summary>
/// Drop-in replacements for ImGui's Text/TextColored/TextDisabled/TextWrapped/SetTooltip.
/// Those native calls are printf-style formatters, so any dynamic string containing a raw
/// '%' (an item name, an event message, a computed percentage) gets misread as a format
/// specifier and drops or mangles output. These route through TextUnformatted instead,
/// which never interprets '%'.
/// </summary>
public static class UiText
{
    public static void Text(string text) => ImGui.TextUnformatted(text);

    public static void Colored(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    public static void Disabled(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    public static void Wrapped(string text)
    {
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    public static void Tooltip(string text)
    {
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(text);
        ImGui.EndTooltip();
    }
}
