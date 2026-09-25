using ImGuiNET;
using text_survival.Actions;
using text_survival.Desktop.Audio;
using text_survival.Desktop.Input;
using text_survival.Desktop.Rendering;
using text_survival.UI;

namespace text_survival.Desktop.UI;

public static class TopBar
{
    public static PlayerAction? Render(GameContext ctx, HudRect rect, IReadOnlyList<HudAction> actions, bool interactive, WorldRenderer world)
    {
        PlayerAction? result = null;
        HudWidgets.Begin("##TopBar", rect);
        int day = (ctx.GameTime - GameContext.StartTime).Days + 1;
        var weather = ctx.CurrentLocation.Weather;
        string clock = $"Day {day}  {ctx.GameTime:h:mm tt}";
        UiIcons.Label("clock", clock);
        ImGui.SameLine();
        UiText.Disabled("·");
        ImGui.SameLine();
        double temp = ctx.CurrentLocation.GetTemperatureBreakdown(ctx.CurrentActivity).BaseTemp;
        UiIcons.Label("temperature", $"{weather.GetSeasonLabel()} · {weather.GetConditionLabel()} · {temp:F0}°F");
        ImGui.SameLine();
        UiText.Disabled("·");
        ImGui.SameLine();
        UiIcons.Label("wind", $"{weather.WindSpeedMPH:F0} mph {weather.CurrentWindDirection}");
        ImGui.SameLine();
        UiText.Disabled("·");
        ImGui.SameLine();
        UiIcons.Label("precipitation", weather.PrecipitationPct < 0.1 ? "None" : weather.PrecipitationPct < 0.3 ? "Light"
            : weather.PrecipitationPct < 0.6 ? "Moderate" : "Heavy");
        if (weather.GetFrontLabel() is { Length: > 0 } front)
        {
            ImGui.SameLine();
            UiText.Disabled("·");
            ImGui.SameLine();
            UiText.Disabled(front);
        }
        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(.1f, .16f, .21f, 1));
        foreach (var action in actions.Where(a => a.Group == HudActionGroup.Navigation))
        {
            ImGui.BeginDisabled(!interactive);
            if (UiIcons.Button(action.Icon, action.Control.DisplayLabel, action.Id)) result = action.Payload;
            ImGui.EndDisabled();
            ImGui.SameLine();
        }
        if (interactive && Input.UiInput.Ready && !ImGui.GetIO().WantTextInput &&
            !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId) && Input.UiInput.Pressed(Raylib_cs.KeyboardKey.F1)) UiSettingsDialog.Requested = true;
        if (UiIcons.Button("help", "More / Help", "hud-more-button")) ImGui.OpenPopup("hud-more");
        if (ImGui.BeginPopup("hud-more"))
        {
            bool Menu(string icon, string label, string id, bool enabled = true)
            {
                bool clicked = UiControls.Button(new(id, label, icon), enabled, new(-1, 0));
                if (clicked) ImGui.CloseCurrentPopup();
                return clicked;
            }
            if (Menu("text-size", "UI size & keyboard [F1]", "settings", interactive)) UiSettingsDialog.Requested = true;
            if (Menu(AudioManager.IsMuted ? "mute" : "sound", AudioManager.IsMuted ? "Unmute music" : "Mute music", "sound")) AudioManager.ToggleMute();
            if (Menu("recenter", $"Follow player {HotkeyRegistry.GetTip(HotkeyAction.FollowPlayer)}", "recenter", interactive)) world.RecenterOnPlayer(ctx);
            if (Menu("restart", "New game...", "restart", interactive)) result = new PlayerAction.NewGame();
            ImGui.Separator();
            UiText.Text("CONTROLS");
            UiText.Text("F1: UI settings · F6: switch HUD panel");
            UiText.Text("Tab / Shift+Tab: focus · Arrows: navigate · Enter: activate");
            UiText.Text("Esc: return · Ctrl/Cmd+F: recipe search · F4: forage focus");
            UiText.Text(ctx.ActiveCombat == null ? "WASD: travel · X: inspect · Enter: destination choices" : "WASD: preview · Enter: move · Q/E: target · X: inspect");
            UiText.Text("Arrows / middle drag: pan · G: follow");
            UiText.Text("Map wheel / trackpad: zoom");
            UiText.Text("Ctrl or Cmd +/-: zoom · 0: reset (with modifier)");
            UiText.Text("History: F6 to Events · F2 expand · F3 read · F4 latest");
            UiText.Text("Esc: close dialog / clear selection");
            if (ctx.ActiveCombat == null) foreach (var key in Enum.GetValues<HotkeyAction>()) UiText.Text($"{HotkeyRegistry.GetTip(key)}  {key}");
            ImGui.EndPopup();
        }
        ImGui.PopStyleColor();
        ImGui.End();
        return result;
    }
}
