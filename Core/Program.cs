using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;
using text_survival.Persistence;
using text_survival.Actions;
using text_survival.Desktop;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.Audio;
using text_survival.Desktop.UI;
using text_survival.UI;

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

        // Get current monitor and resize to 95% of display (keeps window controls visible)
        int monitor = Raylib.GetCurrentMonitor();
        int monitorWidth = Raylib.GetMonitorWidth(monitor);
        int monitorHeight = Raylib.GetMonitorHeight(monitor);

        int windowWidth = (int)(monitorWidth * 0.95);
        int windowHeight = (int)(monitorHeight * 0.95);

        Raylib.SetWindowSize(windowWidth, windowHeight);
        Raylib.SetWindowPosition(
            (monitorWidth - windowWidth) / 2,
            (monitorHeight - windowHeight) / 2);

        Raylib.SetTargetFPS(60);

        Raylib.InitAudioDevice();
        AudioManager.Initialize();

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
            // A save that will not load is not a reason to fail silently - say so, in the
            // journal and on stderr, and start fresh.
            Console.Error.WriteLine($"[Program] Could not load save: {loadError ?? "unknown error"}");
            ctx = GameContext.CreateNewGame();
            ctx.Notices.Enqueue(new Notice("Load Error",
                $"Your saved game could not be loaded:\n\n{loadError ?? "Unknown error"}\n\nStarting a new run."));
        }

        // Load sprite textures (must be after Raylib.InitWindow)
        TileRenderer.LoadSprites();

        AudioManager.PlayMusic();

        // Every await in game logic resumes here, between frames.
        var scheduler = new FrameScheduler();
        SynchronizationContext.SetSynchronizationContext(scheduler);

        UiIcons.Load();
        try
        {
            RunGame(ctx, scheduler);
        }
        finally
        {
            UiIcons.Unload();
        }

        AudioManager.Shutdown();
        Raylib.CloseAudioDevice();

        rlImGui.Shutdown();
        Raylib.CloseWindow();
    }

    /// <summary>How much of a hitch one frame is allowed to charge an animation.</summary>
    private const float MaxFrameSeconds = 0.1f;

    /// <summary>
    /// The only frame loop. Game logic advances during the pump; the frame draws what it
    /// left behind. A finished run either restarts with a fresh world or ends the app.
    /// </summary>
    private static void RunGame(GameContext ctx, FrameScheduler scheduler)
    {
        while (true)
        {
            var ui = new DesktopUi(ctx, scheduler);
            ctx.Ui = ui;

            Task<bool> game = new GameRunner(ctx).RunAsync();

            while (!Raylib.WindowShouldClose() && !game.IsCompleted)
            {
                float dt = MathF.Min(Raylib.GetFrameTime(), MaxFrameSeconds);
                scheduler.Pump();
                ui.Frame(ctx, dt);
            }

            // Never fail silently: a broken run surfaces instead of quietly stopping.
            if (game.IsFaulted)
                throw game.Exception!;

            if (!game.IsCompleted)
            {
                // The window closed mid-action. The game task is abandoned where it stands.
                if (ctx.player.IsAlive)
                    SaveManager.Save(ctx);
                return;
            }

            bool requestRestart = game.Result;
            if (!requestRestart)
            {
                if (ctx.player.IsAlive)
                    SaveManager.Save(ctx);
                return;
            }

            if (Raylib.WindowShouldClose()) return;

            ctx = GameContext.CreateNewGame();
        }
    }
}
