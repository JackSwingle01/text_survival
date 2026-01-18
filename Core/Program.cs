using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;
using text_survival.Persistence;
using text_survival.Actions;
using text_survival.Desktop.Rendering;
using text_survival.Desktop;
using text_survival.Desktop.UI;
using text_survival.Desktop.Input;

namespace text_survival.Core;

public static class Program
{
    public static void Main()
    {
        // Set resizable flag before window creation
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);

        // Create initial window (will resize after getting monitor info)
        Raylib.InitWindow(1280, 720, "Text Survival");
        Raylib.SetExitKey(KeyboardKey.Null);  // Prevent ESC from closing window

        // Get current monitor and resize to ~90% of display
        int monitor = Raylib.GetCurrentMonitor();
        int monitorWidth = Raylib.GetMonitorWidth(monitor);
        int monitorHeight = Raylib.GetMonitorHeight(monitor);

        int windowWidth = (int)(monitorWidth * 0.9);
        int windowHeight = (int)(monitorHeight * 0.9);

        Raylib.SetWindowSize(windowWidth, windowHeight);
        Raylib.SetWindowPosition(
            (monitorWidth - windowWidth) / 2,
            (monitorHeight - windowHeight) / 2);

        Raylib.SetTargetFPS(60);

        // ImGui's default font lacks Unicode arrows (↑↓). Merge them from a system font.
        rlImGui.SetupUserFonts = (ImGuiIOPtr io) =>
        {
            string[] fontPaths = [
                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "/System/Library/Fonts/SFNS.ttf",
                "/Library/Fonts/Arial.ttf"
            ];

            string? fontPath = fontPaths.FirstOrDefault(File.Exists);
            if (fontPath == null) return;

            unsafe
            {
                const ushort ArrowUp = 0x2191;   // ↑
                const ushort ArrowDown = 0x2193; // ↓

                ImFontGlyphRangesBuilder* builder = ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder();
                ImGuiNative.ImFontGlyphRangesBuilder_AddChar(builder, ArrowUp);
                ImGuiNative.ImFontGlyphRangesBuilder_AddChar(builder, ArrowDown);

                ImVector ranges;
                ImGuiNative.ImFontGlyphRangesBuilder_BuildRanges(builder, &ranges);

                ImFontConfig* config = ImGuiNative.ImFontConfig_ImFontConfig();
                config->MergeMode = 1;
                config->PixelSnapH = 1;

                io.Fonts.AddFontFromFileTTF(fontPath, 13.0f, config, (nint)ranges.Data);

                ImGuiNative.ImFontConfig_destroy(config);
                ImGuiNative.ImFontGlyphRangesBuilder_destroy(builder);
            }
        };

        rlImGui.Setup(true);
        ImGui.GetIO().FontGlobalScale = 1.25f;  // Scale up default font for readability

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
        var inputHandler = new InputHandler(worldRenderer);
        var tilePopup = new TilePopup();
        var iconRenderer = new ProceduralIconRenderer();

        // Initialize desktop runtime for blocking I/O
        DesktopRuntime.Initialize(worldRenderer, overlays, actionPanel, inputHandler, tilePopup, iconRenderer);

        // Run the game through GameRunner
        // GameRunner uses Input/GameDisplay which route to DesktopIO
        // DesktopIO methods have nested render loops for blocking I/O
        var runner = new GameRunner(ctx);
        runner.Run();

        // Save on exit (but not if player died - save was already deleted)
        if (ctx.player.IsAlive)
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
