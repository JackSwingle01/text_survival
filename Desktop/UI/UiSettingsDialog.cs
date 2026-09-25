using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using text_survival.Desktop.Input;

namespace text_survival.Desktop.UI;

/// <summary>First consumer of the shared focusable controls; changes apply between frames.</summary>
public sealed class UiSettingsDialog
{
    public static bool Requested { get; set; }
    private bool _focused;
    private UiScale _selected = UiStyle.Settings.Scale;
    public bool Render(bool interactive)
    {
        float width = Math.Min(Raylib.GetScreenWidth() - 32, 550 * UiStyle.Scale);
        float height = Math.Min(Raylib.GetScreenHeight() - 32, 480 * UiStyle.Scale);
        ImGui.SetNextWindowPos(new(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f), ImGuiCond.Always, new(.5f, .5f));
        ImGui.SetNextWindowSize(new(width, height), ImGuiCond.Always);
        ImGui.Begin("UI size & keyboard", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        UiText.Wrapped("Choose a comfortable reading size. Text, spacing and shared controls scale together.");
        ImGui.Spacing();
        foreach (var scale in Enum.GetValues<UiScale>())
        {
            bool selected = _selected == scale;
            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, UiStyle.Selection);
            if (UiControls.Button(new("scale-" + scale, scale.ToString(), selected ? "confirm" : "text-size"), interactive, new(-1, 0))) _selected = scale;
            if (selected) ImGui.PopStyleColor();
        }
        ImGui.Spacing();
        var apply = new UiControl("apply-size", "Apply size", "confirm", DisabledReason:
            _selected == UiStyle.Settings.Scale ? "This size is already applied. Choose another size first." : null);
        if (interactive && !_focused) { ImGui.SetKeyboardFocusHere(); _focused = true; }
        if (UiControls.Button(apply, interactive, new(-1, 0), showDisabledReason: false)) UiStyle.ChooseScale(_selected);
        if (interactive) ImGui.SetItemDefaultFocus(); // Starts on a harmless, focusable explanation.
        ImGui.BeginChild("size-feedback", new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 2));
        UiText.Wrapped(UiStyle.SaveError ?? apply.DisabledReason ?? "Choose Apply size to use this preference.");
        ImGui.EndChild();
        ImGui.Separator();
        UiText.Wrapped("Tab / Shift+Tab: move focus\nArrows: navigate lists\nEnter: activate focused control\nEsc: close or return\nF6: switch HUD panels\nCtrl/Cmd+F: recipe search\nF4: cycle forage focus");
        ImGui.Spacing();
        bool close = UiControls.Button(new("close-settings", "Close", "cancel", KeyboardKey.Escape), interactive, new(-1, 0));
        ImGui.End();
        return close;
    }
}
