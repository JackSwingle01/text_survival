using System.Numerics;
using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using text_survival.Desktop.Input;
using Raylib_cs;

namespace text_survival.Desktop.UI;

public sealed record UiControl(string Id, string Label, string Icon, KeyboardKey? Shortcut = null,
    string? DisabledReason = null, UiTone Tone = UiTone.Normal)
{
    public bool Enabled => DisabledReason == null;
    public string DisplayLabel => Shortcut is { } key ? $"{Label} [{KeyLabel(key)}]" : Label;
    public static string KeyLabel(KeyboardKey key) => key switch {
        KeyboardKey.Escape => "Esc", KeyboardKey.Space => "Space", _ => key.ToString() };
    public bool CanActivate(bool scopeEnabled, bool requested) => Enabled && scopeEnabled && requested;
}

public static class UiControls
{
    public static bool InputIntSubmitted(string label, ref int value)
    {
        ImGui.InputInt(label, ref value);
        return GameGui.Capture == null && (ImGuiNET.ImGui.IsItemActive() || ImGuiNET.ImGui.IsItemFocused()) &&
            UiNavigation.Key(ImGuiKey.Enter, whileEditing: true);
    }

    /// <summary>Unavailable actions stay focusable so their reason is available without a mouse.</summary>
    public static bool Button(UiControl control, bool scopeEnabled = true, Vector2 size = default, bool showDisabledReason = true)
    {
        bool available = control.Enabled;
        ImGui.BeginDisabled(!scopeEnabled || GameGui.Capture != null && !available);
        ImGui.PushStyleColor(ImGuiCol.Text, available ? UiStyle.Color(control.Tone) : UiStyle.Supporting);
        bool clicked = UiIcons.Button(control.Icon, control.DisplayLabel, control.Id, size);
        if (!available && GameGui.Capture == null)
        {
            var min = ImGui.GetItemRectMin();
            var max = min + ImGui.GetItemRectSize();
            ImGuiNET.ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(UiStyle.Supporting));
            UiIcons.Draw("blocked", max - new Vector2(18, 18), 16);
        }
        bool focused = ImGui.IsItemFocused();
        bool hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
        ImGui.PopStyleColor();
        ImGui.EndDisabled();
        if (showDisabledReason && control.DisabledReason is { } reason && (focused || hovered))
        {
            if (focused && GameGui.Capture == null)
            {
                var viewport = ImGuiNET.ImGui.GetIO().DisplaySize;
                float width = Math.Min(viewport.X - 16, ImGui.GetFontSize() * 32);
                float height = ImGui.CalcTextSize(reason, false, width).Y + 24;
                var anchor = ImGui.GetItemRectMin();
                float below = anchor.Y + ImGui.GetItemRectSize().Y + 4;
                ImGui.SetNextWindowPos(new Vector2(Math.Clamp(anchor.X, 0, Math.Max(0, viewport.X - width - 16)),
                    Math.Max(0, below + height < viewport.Y ? below : anchor.Y - height)));
            }
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32);
            UiText.Text(reason);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
        bool shortcut = GameGui.Capture == null && control.Shortcut is { } key && UiInput.Pressed(key) &&
            !ImGui.WantTextInput && !ImGui.AnyPopupOpen;
        return control.CanActivate(scopeEnabled, clicked || shortcut);
    }
}
