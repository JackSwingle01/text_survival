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
        if (!UiInput.GameCommandsAllowed) return null;
        if (HotkeyRegistry.IsPressed(HotkeyAction.Cancel)) { state.ClearSelection(); return null; }
        if (io.WantCaptureKeyboard) return null;
        if (UiInput.Pressed(KeyboardKey.X)) { state.BeginInspection(ctx); return null; }
        if (state.Inspecting && UiInput.Pressed(KeyboardKey.Enter))
        { state.ConfirmTravel = true; return null; }
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
        if (state.Inspecting)
        {
            if (step is { } inspectStep)
            {
                state.StepInspection(ctx, inspectStep.x, inspectStep.y);
                if (state.SelectedTile is { } cursor && ctx.Map is { } inspectMap &&
                    !world.Camera.ContainsScreenPoint(world.Camera.GetTileCenter(cursor.x, cursor.y)))
                {
                    var center = world.Camera.IsFollowingPlayer ? world.Camera.Center : world.Camera.Target;
                    world.Camera.Pan(new System.Numerics.Vector2(cursor.x, cursor.y) - center, inspectMap.Width, inspectMap.Height);
                }
            }
            return null;
        }
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
            if (action.Control.CanActivate(UiInput.GameCommandsAllowed, true)) return action.Payload;
            state.ShowFeedback(action.DisabledReason!);
            return null;
        }
        if (HotkeyRegistry.IsPressed(HotkeyAction.Forage)) state.ShowFeedback("There is no foraging work here.");
        return null;
    }
}
