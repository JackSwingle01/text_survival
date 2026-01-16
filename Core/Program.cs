using Raylib_cs;
using rlImGuiCs;
using ImGuiNET;
using text_survival.Persistence;
using text_survival.Actions;

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

        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            // Test world rendering - draw a simple grid placeholder
            int gridStartX = 50;
            int gridStartY = 50;
            int tileSize = 80;
            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 7; y++)
                {
                    var tileColor = (x == 3 && y == 3)
                        ? new Color(80, 120, 80, 255)  // Player tile
                        : new Color(40, 50, 40, 255);  // Other tiles
                    Raylib.DrawRectangle(
                        gridStartX + x * (tileSize + 2),
                        gridStartY + y * (tileSize + 2),
                        tileSize,
                        tileSize,
                        tileColor);
                }
            }

            // Draw player marker
            int playerX = gridStartX + 3 * (tileSize + 2) + tileSize / 2;
            int playerY = gridStartY + 3 * (tileSize + 2) + tileSize / 2;
            Raylib.DrawCircle(playerX, playerY, 15, new Color(200, 180, 140, 255));

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
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.8f, 0.3f, 1), "Desktop Migration - Phase 1");
            ImGui.Separator();
            ImGui.TextWrapped("This is a stub implementation. The game logic is loaded but UI interactions are not yet wired up.");
            ImGui.Separator();
            ImGui.Text("Next steps:");
            ImGui.BulletText("Implement world renderer");
            ImGui.BulletText("Wire up DesktopIO methods");
            ImGui.BulletText("Port overlay UIs to ImGui");
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
