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
        bool compact = rect.Width < 1500 * ImGui.GetFontSize() / 16.25f;
        UiText.Text(clock + "  ");
        ImGui.SameLine();
        string summary = compact ? $"{ctx.CurrentLocation.GetTemperatureBreakdown(ctx.CurrentActivity).BaseTemp:F0}°F"
            : $"{weather.GetConditionLabel()} · {ctx.CurrentLocation.GetTemperatureBreakdown(ctx.CurrentActivity).BaseTemp:F0}°F";
        if (ImGui.Button(summary + " v###weather")) ImGui.OpenPopup("weather-details");
        if (ImGui.BeginPopup("weather-details"))
        {
            UiText.Text($"{weather.GetSeasonLabel()} · {weather.GetConditionLabel()}");
            UiText.Text($"Wind {weather.WindSpeedMPH:F0} mph · {weather.CurrentWindDirection}");
            UiText.Text($"Precipitation {weather.PrecipitationPct:P0}");
            UiText.Text(weather.GetFrontLabel());
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(.1f, .16f, .21f, 1));
        foreach (var action in actions.Where(a => a.Group == HudActionGroup.Navigation))
        {
            ImGui.BeginDisabled(!interactive);
            if (ImGui.Button(action.Label + " " + HotkeyRegistry.GetTip(action.Shortcut!.Value))) result = action.Payload;
            ImGui.EndDisabled();
            ImGui.SameLine();
        }
        if (ImGui.Button("More / Help")) ImGui.OpenPopup("hud-more");
        if (ImGui.BeginPopup("hud-more"))
        {
            if (ImGui.MenuItem(AudioManager.IsMuted ? "Unmute music" : "Mute music")) AudioManager.ToggleMute();
            if (ImGui.MenuItem($"Follow player {HotkeyRegistry.GetTip(HotkeyAction.FollowPlayer)}", "", world.Camera.IsFollowingPlayer, interactive)) world.RecenterOnPlayer(ctx);
            ImGui.Separator();
            UiText.Text("CONTROLS");
            UiText.Text("WASD: move · Click: inspect tile");
            UiText.Text("Arrows / middle drag: pan · G: follow");
            UiText.Text("Map wheel / trackpad: zoom");
            UiText.Text("Ctrl or Cmd +/-: zoom · 0: reset (with modifier)");
            UiText.Text("Esc: close dialog / clear selection");
            foreach (var key in Enum.GetValues<HotkeyAction>()) UiText.Text($"{HotkeyRegistry.GetTip(key)}  {key}");
            ImGui.EndPopup();
        }
        ImGui.PopStyleColor();
        ImGui.End();
        return result;
    }
}
