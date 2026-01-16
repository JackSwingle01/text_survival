using Raylib_cs;
using rlImGui_cs;
using text_survival.Persistence;
using text_survival.Actions;
using text_survival.Desktop.Rendering;
using text_survival.Desktop;
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

        if (loadError != null || ctx == null)
        {
            // Show error and allow new game
            ShowLoadError(loadError ?? "Unknown error");
            ctx = GameContext.CreateNewGame();
        }

        // Create rendering infrastructure
        var worldRenderer = new WorldRenderer();
        var overlays = new OverlayManager();
        var actionPanel = new ActionPanel();

        // Initialize desktop runtime for blocking I/O
        DesktopRuntime.Initialize(worldRenderer, overlays, actionPanel);

        // Run the game through GameRunner
        // GameRunner uses Input/GameDisplay which route to DesktopIO
        // DesktopIO methods have nested render loops for blocking I/O
        var runner = new GameRunner(ctx);
        runner.Run();

        // Save on exit
        SaveManager.Save(ctx);

        rlImGui.Shutdown();
        Raylib.CloseWindow();
    }

    private static void ShowLoadError(string error)
    {
        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            int screenWidth = Raylib.GetScreenWidth();
            int screenHeight = Raylib.GetScreenHeight();

            // Error message
            string title = "Load Error";
            int titleWidth = Raylib.MeasureText(title, 32);
            Raylib.DrawText(title, (screenWidth - titleWidth) / 2, screenHeight / 2 - 60, 32, new Color(255, 100, 100, 255));

            int errorWidth = Raylib.MeasureText(error, 18);
            Raylib.DrawText(error, (screenWidth - errorWidth) / 2, screenHeight / 2, 18, new Color(200, 200, 200, 255));

            string hint = "Press ENTER to start a new game, or ESC to quit";
            int hintWidth = Raylib.MeasureText(hint, 16);
            Raylib.DrawText(hint, (screenWidth - hintWidth) / 2, screenHeight / 2 + 60, 16, new Color(150, 150, 150, 255));

            Raylib.EndDrawing();

            if (Raylib.IsKeyPressed(KeyboardKey.Enter))
                break;
            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                Raylib.CloseWindow();
                Environment.Exit(0);
            }
        }
    }
}
