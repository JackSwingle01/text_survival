using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.UI;
using text_survival.Desktop.Input;

namespace text_survival.Desktop;

/// <summary>
/// Holds shared runtime state for the desktop application.
/// Enables blocking I/O methods to render the game while waiting for input.
/// </summary>
public static class DesktopRuntime
{
    public static WorldRenderer? WorldRenderer { get; set; }
    public static OverlayManager? Overlays { get; set; }
    public static ActionPanel? ActionPanel { get; set; }
    public static InputHandler? InputHandler { get; set; }
    public static TilePopup? TilePopup { get; set; }

    /// <summary>
    /// Initialize the runtime with required components.
    /// </summary>
    public static void Initialize(WorldRenderer worldRenderer, OverlayManager overlays, ActionPanel actionPanel,
        InputHandler inputHandler, TilePopup tilePopup)
    {
        WorldRenderer = worldRenderer;
        Overlays = overlays;
        ActionPanel = actionPanel;
        InputHandler = inputHandler;
        TilePopup = tilePopup;
    }

    /// <summary>
    /// Render a single frame of the game (world + existing overlays).
    /// Used by blocking dialogs to keep the game responsive.
    /// </summary>
    public static void RenderFrame(GameContext ctx)
    {
        float deltaTime = Raylib.GetFrameTime();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        // Render world
        if (WorldRenderer != null)
        {
            WorldRenderer.Update(ctx, deltaTime);
            WorldRenderer.Render(ctx);
        }

        rlImGui.Begin();

        // Render minimal game state panel
        RenderMinimalStatePanel(ctx);

        rlImGui.End();

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Render a frame with a blocking dialog overlay.
    /// </summary>
    public static void RenderFrameWithDialog(GameContext ctx, Action renderDialog)
    {
        float deltaTime = Raylib.GetFrameTime();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        // Render world (dimmed background)
        if (WorldRenderer != null)
        {
            WorldRenderer.Update(ctx, deltaTime);
            WorldRenderer.Render(ctx);
        }

        // Draw semi-transparent overlay to dim background
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color(0, 0, 0, 128));

        rlImGui.Begin();

        // Render the blocking dialog
        renderDialog();

        rlImGui.End();

        Raylib.EndDrawing();
    }

    private static void RenderMinimalStatePanel(GameContext ctx)
    {
        ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(200, 100), ImGuiCond.Always);
        ImGui.Begin("Status", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

        var body = ctx.player.Body;
        ImGui.Text($"Energy: {body.Energy:F0}");
        ImGui.Text($"Calories: {body.CalorieStore:F0}");
        ImGui.Text($"Time: {ctx.GameTime:h:mm tt}");

        ImGui.End();
    }
}

/// <summary>
/// Helper class for running blocking dialogs with nested render loops.
/// </summary>
public static class BlockingDialog
{
    /// <summary>
    /// Show a selection dialog and block until the user makes a choice.
    /// </summary>
    public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices,
        Func<T, string> display, Func<T, bool>? isDisabled = null) where T : notnull
    {
        var choiceList = choices.ToList();
        if (choiceList.Count == 0)
            throw new ArgumentException("No choices provided");

        T? result = default;
        bool done = false;
        int selectedIndex = 0;

        while (!done && !Raylib.WindowShouldClose())
        {
            DesktopRuntime.RenderFrameWithDialog(ctx, () =>
            {
                // Center the dialog
                var io = ImGui.GetIO();
                ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                    ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                ImGui.SetNextWindowSize(new Vector2(400, 0), ImGuiCond.Always);

                ImGui.Begin("Select", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

                ImGui.TextWrapped(prompt);
                ImGui.Separator();
                ImGui.Spacing();

                for (int i = 0; i < choiceList.Count; i++)
                {
                    var choice = choiceList[i];
                    string label = display(choice);
                    bool disabled = isDisabled?.Invoke(choice) ?? false;

                    if (disabled)
                    {
                        ImGui.BeginDisabled();
                        ImGui.Button(label, new Vector2(-1, 0));
                        ImGui.EndDisabled();
                    }
                    else
                    {
                        if (ImGui.Button(label, new Vector2(-1, 0)))
                        {
                            result = choice;
                            done = true;
                        }
                    }
                }

                ImGui.End();
            });
        }

        return result ?? choiceList[0];
    }

    /// <summary>
    /// Show a confirmation dialog and block until the user responds.
    /// </summary>
    public static bool Confirm(GameContext ctx, string prompt)
    {
        bool? result = null;

        while (result == null && !Raylib.WindowShouldClose())
        {
            DesktopRuntime.RenderFrameWithDialog(ctx, () =>
            {
                var io = ImGui.GetIO();
                ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                    ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Always);

                ImGui.Begin("Confirm", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

                ImGui.TextWrapped(prompt);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float buttonWidth = (ImGui.GetContentRegionAvail().X - 10) / 2;

                if (ImGui.Button("Yes", new Vector2(buttonWidth, 30)))
                {
                    result = true;
                }

                ImGui.SameLine();

                if (ImGui.Button("No", new Vector2(buttonWidth, 30)))
                {
                    result = false;
                }

                ImGui.End();
            });
        }

        return result ?? false;
    }

    /// <summary>
    /// Show a confirmation dialog with custom button labels.
    /// </summary>
    public static string PromptConfirm(GameContext ctx, string message, Dictionary<string, string> buttons)
    {
        string? result = null;

        while (result == null && !Raylib.WindowShouldClose())
        {
            DesktopRuntime.RenderFrameWithDialog(ctx, () =>
            {
                var io = ImGui.GetIO();
                ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                    ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                ImGui.SetNextWindowSize(new Vector2(400, 0), ImGuiCond.Always);

                ImGui.Begin("Confirm", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

                ImGui.TextWrapped(message);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var (id, label) in buttons)
                {
                    if (ImGui.Button(label, new Vector2(-1, 30)))
                    {
                        result = id;
                    }
                }

                ImGui.End();
            });
        }

        return result ?? buttons.Keys.First();
    }

    /// <summary>
    /// Show an integer input dialog and block until the user enters a value.
    /// Returns -1 if cancelled (when allowCancel is true).
    /// </summary>
    public static int ReadInt(GameContext ctx, string prompt, int min, int max, bool allowCancel = false)
    {
        int? result = null;
        int value = min;

        while (result == null && !Raylib.WindowShouldClose())
        {
            DesktopRuntime.RenderFrameWithDialog(ctx, () =>
            {
                var io = ImGui.GetIO();
                ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                    ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Always);

                ImGui.Begin("Input", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

                ImGui.TextWrapped(prompt);
                ImGui.Spacing();

                ImGui.SliderInt("##value", ref value, min, max);
                ImGui.Text($"Range: {min} - {max}");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float buttonWidth = allowCancel ? (ImGui.GetContentRegionAvail().X - 10) / 2 : -1;

                if (ImGui.Button("OK", new Vector2(buttonWidth, 30)))
                {
                    result = value;
                }

                if (allowCancel)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", new Vector2(buttonWidth, 30)))
                    {
                        result = -1;
                    }
                }

                ImGui.End();
            });
        }

        return result ?? min;
    }

    /// <summary>
    /// Show a message and wait for the user to dismiss it.
    /// </summary>
    public static void ShowMessageAndWait(GameContext ctx, string title, string message)
    {
        bool done = false;

        while (!done && !Raylib.WindowShouldClose())
        {
            DesktopRuntime.RenderFrameWithDialog(ctx, () =>
            {
                var io = ImGui.GetIO();
                ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                    ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                ImGui.SetNextWindowSize(new Vector2(400, 0), ImGuiCond.Always);

                ImGui.Begin(title, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

                ImGui.TextWrapped(message);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.Button("Continue", new Vector2(-1, 30)))
                {
                    done = true;
                }

                ImGui.End();
            });
        }
    }

    /// <summary>
    /// Show a progress dialog that blocks for a duration.
    /// </summary>
    public static void ShowProgress(GameContext ctx, string statusText, int durationMinutes, Action<int>? onMinuteTick = null)
    {
        float totalSeconds = durationMinutes * 0.5f; // Speed up for gameplay (0.5 real seconds per game minute)
        float elapsed = 0;
        int lastMinute = 0;

        while (elapsed < totalSeconds && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();
            elapsed += deltaTime;

            int currentMinute = (int)(elapsed / 0.5f);
            if (currentMinute > lastMinute && currentMinute <= durationMinutes)
            {
                onMinuteTick?.Invoke(currentMinute);
                lastMinute = currentMinute;
            }

            float progress = Math.Min(elapsed / totalSeconds, 1.0f);

            DesktopRuntime.RenderFrameWithDialog(ctx, () =>
            {
                var io = ImGui.GetIO();
                ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                    ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                ImGui.SetNextWindowSize(new Vector2(400, 0), ImGuiCond.Always);

                ImGui.Begin("Activity", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

                ImGui.TextWrapped(statusText);
                ImGui.Spacing();

                ImGui.ProgressBar(progress, new Vector2(-1, 20),
                    $"{(int)(progress * durationMinutes)}/{durationMinutes} min");

                ImGui.End();
            });
        }
    }
}
