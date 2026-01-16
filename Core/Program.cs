using Raylib_cs;
using rlImGuiCs;
using ImGuiNET;
using text_survival.Persistence;
using text_survival.Actions;
using text_survival.Desktop.Rendering;

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

        // Create world renderer
        var worldRenderer = new WorldRenderer();

        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

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

            // Status panel
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 470), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 200), ImGuiCond.FirstUseEver);
            ImGui.Begin("Status");
            ImGui.TextColored(new System.Numerics.Vector4(0.3f, 1f, 0.5f, 1), "Desktop Migration - Phase 2");
            ImGui.Separator();
            ImGui.TextWrapped("World rendering active. Terrain textures, fog of war, and effects are working.");
            ImGui.Separator();

            // Hover info
            var hoveredTile = worldRenderer.GetHoveredTile();
            if (hoveredTile.HasValue && ctx != null)
            {
                var (hx, hy) = hoveredTile.Value;
                ImGui.Text($"Hovered: ({hx}, {hy})");
                if (ctx.Map.IsValidPosition(hx, hy))
                {
                    var loc = ctx.Map.GetLocationAt(hx, hy);
                    if (loc != null)
                    {
                        ImGui.Text($"  {loc.Name}");
                        ImGui.Text($"  Terrain: {loc.Terrain}");
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("Hover over a tile for info");
            }

            ImGui.Separator();
            ImGui.Text("Next steps:");
            ImGui.BulletText("Wire up DesktopIO methods");
            ImGui.BulletText("Port overlay UIs to ImGui");
            ImGui.BulletText("Add click-to-travel");
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
}
