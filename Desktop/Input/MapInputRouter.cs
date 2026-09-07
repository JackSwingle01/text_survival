using ImGuiNET;
using Raylib_cs;
using text_survival.Actions;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.UI;
using text_survival.Environments.Grid;
using text_survival.UI;

namespace text_survival.Desktop.Input;

public static class MapInputRouter
{
    public static PlayerAction? Read(GameContext ctx, WorldRenderer world, HudState state, IReadOnlyList<HudAction> actions)
    {
        var io = ImGui.GetIO();
        if (!io.WantCaptureMouse && world.HandleClick() is { } tile)
            state.Select(ctx, tile.x, tile.y);
        if (io.WantTextInput || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId)) return null;
        if (HotkeyRegistry.IsPressed(HotkeyAction.Cancel)) { state.ClearSelection(); return null; }
        if (io.WantCaptureKeyboard) return null;
        if (Raylib.IsKeyPressed(KeyboardKey.F12))
        {
            GameMap.RevealAll = !GameMap.RevealAll;
            ctx.Map?.UpdateVisibility();
            state.ShowFeedback(GameMap.RevealAll ? "Debug: map revealed." : "Debug: reveal off.");
            return null;
        }
        (int x, int y)? step = Raylib.IsKeyPressed(KeyboardKey.W) ? (0, -1) :
            Raylib.IsKeyPressed(KeyboardKey.S) ? (0, 1) : Raylib.IsKeyPressed(KeyboardKey.A) ? (-1, 0) :
            Raylib.IsKeyPressed(KeyboardKey.D) ? (1, 0) : null;
        if (step is { } delta && ctx.Map is { } map)
        {
            var target = new GridPosition(map.CurrentPosition.X + delta.x, map.CurrentPosition.Y + delta.y);
            if (!map.CanMoveTo(target.X, target.Y)) state.ShowFeedback("Cannot move there.");
            else if (map.IsEdgeBlocked(map.CurrentPosition, target, ctx.Weather.CurrentSeason)) state.ShowFeedback("The way is blocked.");
            else return new PlayerAction.Travel(target.X, target.Y);
            return null;
        }
        foreach (var action in actions)
        {
            if (action.Shortcut is not { } key || !HotkeyRegistry.IsPressed(key)) continue;
            if (action.Enabled) return action.Payload;
            state.ShowFeedback(action.DisabledReason!);
            return null;
        }
        if (HotkeyRegistry.IsPressed(HotkeyAction.Forage)) state.ShowFeedback("There is no foraging work here.");
        return null;
    }
}
