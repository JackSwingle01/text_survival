using Raylib_cs;
using rlImGuiCs;
using ImGuiNET;
using text_survival.Persistence;
using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.Input;
using text_survival.Desktop.UI;

namespace text_survival.Core;

public static class Program
{
    public static void Main()
    {
        Raylib.InitWindow(1280, 720, "Text Survival");
        Raylib.SetTargetFPS(60);
        rlImGui.Setup(true);

        // Load game (reuses existing save system)
        GameContext? ctx = null;
        string? loadError = null;

        try
        {
            ctx = GameInitializer.LoadOrCreateNew();
        }
        catch (Exception ex)
        {
            loadError = ex.Message;
        }

        // Create world renderer and input handler
        var worldRenderer = new WorldRenderer();
        var inputHandler = new InputHandler(worldRenderer);
        var actionPanel = new ActionPanel();

        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            // Process input before rendering
            if (ctx != null && loadError == null)
            {
                var inputResult = inputHandler.ProcessInput(ctx);

                // Handle travel if initiated
                if (inputResult.TravelInitiated && ctx.PendingTravelTarget.HasValue)
                {
                    // Travel will be processed - for now just show message
                    // Full integration requires async handling
                    var target = ctx.PendingTravelTarget.Value;
                    var destination = ctx.Map?.GetLocationAt(target.X, target.Y);
                    if (destination != null)
                    {
                        actionPanel.ShowMessage($"Traveling to {destination.Name}...");
                        // Simple travel: just move (full travel runner has hazards, events, etc.)
                        ctx.Map?.MoveTo(destination, ctx.player);
                        ctx.PendingTravelTarget = null;
                    }
                }

                // Handle input messages
                if (inputResult.Message != null)
                {
                    actionPanel.ShowMessage(inputResult.Message);
                }

                // Handle wait action
                if (inputResult.Wait)
                {
                    ctx.Update(5, ActivityType.Resting);
                    actionPanel.ShowMessage("Rested for 5 minutes.");
                }
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            // Render world if game loaded
            if (ctx != null && loadError == null)
            {
                worldRenderer.Update(ctx, deltaTime);
                worldRenderer.Render(ctx);
            }

            // ImGui panels
            rlImGui.Begin();

            // Game State panel
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 50), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 400), ImGuiCond.FirstUseEver);
            ImGui.Begin("Game State");

            if (loadError != null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.3f, 0.3f, 1), "Load Error:");
                ImGui.TextWrapped(loadError);
                if (ImGui.Button("Start New Game"))
                {
                    ctx = GameContext.CreateNewGame();
                    loadError = null;
                }
            }
            else if (ctx != null)
            {
                ImGui.Text($"Location: {ctx.CurrentLocation?.Name ?? "Unknown"}");
                ImGui.Separator();

                // Survival stats
                var body = ctx.player.Body;
                ImGui.Text("Survival Stats:");
                ImGui.ProgressBar((float)(body.Energy / 480.0), new System.Numerics.Vector2(-1, 0), $"Energy: {body.Energy:F0}");
                ImGui.ProgressBar((float)(body.CalorieStore / 2000.0), new System.Numerics.Vector2(-1, 0), $"Calories: {body.CalorieStore:F0}");
                ImGui.ProgressBar((float)(body.Hydration / 3.0), new System.Numerics.Vector2(-1, 0), $"Hydration: {body.Hydration:F1}L");

                ImGui.Separator();
                ImGui.Text($"Body Temp: {body.BodyTemperature:F1}°F");
                ImGui.Text($"Vitality: {ctx.player.Vitality * 100:F0}%");

                ImGui.Separator();
                ImGui.Text($"Game Time: {ctx.GameTime:MMM d, h:mm tt}");
                ImGui.Text($"Day: {(ctx.GameTime - new DateTime(2025, 1, 1)).Days + 1}");

                ImGui.Separator();
                if (ImGui.Button("Save Game"))
                {
                    SaveManager.Save(ctx);
                    ImGui.OpenPopup("Saved");
                }

                if (ImGui.BeginPopup("Saved"))
                {
                    ImGui.Text("Game saved!");
                    ImGui.EndPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button("Quit"))
                {
                    SaveManager.Save(ctx);
                    break;
                }
            }

            ImGui.End();

            // Action panel (shows available actions)
            if (ctx != null)
            {
                var clickedAction = actionPanel.Render(ctx, deltaTime);
                if (clickedAction != null)
                {
                    HandleAction(ctx, clickedAction, actionPanel);
                }
            }

            // Status panel
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 470), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 200), ImGuiCond.FirstUseEver);
            ImGui.Begin("Status");
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1f, 1), "Desktop Migration - Phase 3");
            ImGui.Separator();
            ImGui.TextWrapped("Input active! Click tiles or use WASD to move. Actions panel shows available options.");
            ImGui.Separator();

            // Hover info
            var hoveredTile = worldRenderer.GetHoveredTile();
            if (hoveredTile.HasValue && ctx != null)
            {
                var (hx, hy) = hoveredTile.Value;
                var currentPos = ctx.Map.CurrentPosition;
                bool isAdjacent = Math.Abs(hx - currentPos.X) <= 1 && Math.Abs(hy - currentPos.Y) <= 1
                    && (hx != currentPos.X || hy != currentPos.Y);

                ImGui.Text($"Tile: ({hx}, {hy})");
                if (ctx.Map.IsValidPosition(hx, hy))
                {
                    var loc = ctx.Map.GetLocationAt(hx, hy);
                    if (loc != null)
                    {
                        ImGui.Text($"  {loc.Name}");
                        if (isAdjacent && ctx.Map.CanMoveTo(hx, hy))
                            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1f, 0.5f, 1), "  Click to travel");
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("Hover over a tile for info");
            }

            ImGui.Separator();
            ImGui.TextDisabled("Keys: WASD=move, Space=wait, I=inventory, C=craft");
            ImGui.End();

            rlImGui.End();

            Raylib.EndDrawing();
        }

        // Save on exit
        if (ctx != null)
        {
            SaveManager.Save(ctx);
        }

        rlImGui.Shutdown();
        Raylib.CloseWindow();
    }

    /// <summary>
    /// Handle an action from the action panel.
    /// </summary>
    private static void HandleAction(GameContext ctx, string actionId, ActionPanel panel)
    {
        switch (actionId)
        {
            case "wait":
                ctx.Update(5, ActivityType.Resting);
                panel.ShowMessage("Rested for 5 minutes.");
                break;

            case "inventory":
                panel.ShowMessage("Inventory not yet implemented.");
                break;

            case "crafting":
                panel.ShowMessage("Crafting not yet implemented.");
                break;

            case "discovery_log":
                panel.ShowMessage("Discovery log not yet implemented.");
                break;

            case "storage":
                panel.ShowMessage("Storage not yet implemented.");
                break;

            case "tend_fire":
            case "start_fire":
                panel.ShowMessage("Fire management not yet implemented.");
                break;

            case "eat_drink":
                panel.ShowMessage("Eating/drinking not yet implemented.");
                break;

            case "cook":
                panel.ShowMessage("Cooking not yet implemented.");
                break;

            case "sleep":
                panel.ShowMessage("Sleep not yet implemented.");
                break;

            case "make_camp":
                panel.ShowMessage("Make camp not yet implemented.");
                break;

            case "treat_wounds":
                panel.ShowMessage("Treatment not yet implemented.");
                break;

            case "curing_rack":
                panel.ShowMessage("Curing rack not yet implemented.");
                break;

            default:
                if (actionId.StartsWith("work:"))
                {
                    var workId = actionId[5..];
                    panel.ShowMessage($"Work '{workId}' not yet implemented.");
                }
                else
                {
                    panel.ShowMessage($"Unknown action: {actionId}");
                }
                break;
        }
    }
}
