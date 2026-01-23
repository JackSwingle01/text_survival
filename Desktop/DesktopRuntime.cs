using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.UI;
using text_survival.Desktop.Input;
using text_survival.IO;
using text_survival.Items;

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
    public static IIconRenderer? IconRenderer { get; set; }

    /// <summary>
    /// Initialize the runtime with required components.
    /// </summary>
    public static void Initialize(WorldRenderer worldRenderer, OverlayManager overlays, ActionPanel actionPanel,
        InputHandler inputHandler, TilePopup tilePopup, IIconRenderer iconRenderer)
    {
        WorldRenderer = worldRenderer;
        Overlays = overlays;
        ActionPanel = actionPanel;
        InputHandler = inputHandler;
        TilePopup = tilePopup;
        IconRenderer = iconRenderer;
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

        // Render toast notifications
        ToastManager.Render(deltaTime);

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

        // Render toast notifications
        ToastManager.Render(deltaTime);

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
    /// Show a progress dialog that simulates time incrementally and shows animated stats.
    /// Returns (elapsed minutes, whether event interrupted).
    /// </summary>
    public static (int elapsed, bool interrupted) ShowProgress(GameContext ctx, string statusText, int durationMinutes, ActivityType activity)
    {
        // ~0.3 seconds per in-game minute, clamped to reasonable bounds
        float animDuration = Math.Clamp(durationMinutes * 0.3f, 1.0f, 30.0f);
        float elapsed = 0;
        int simulatedMinutes = 0;

        while (simulatedMinutes < durationMinutes && !Raylib.WindowShouldClose() && ctx.player.IsAlive)
        {
            float deltaTime = Raylib.GetFrameTime();
            elapsed += deltaTime;

            // Calculate how many minutes to simulate this frame
            float minutesPerSecond = durationMinutes / animDuration;
            int targetMinutes = Math.Min((int)(elapsed * minutesPerSecond), durationMinutes);

            // Simulate any pending minutes
            while (simulatedMinutes < targetMinutes && ctx.player.IsAlive)
            {
                ctx.Update(1, activity);
                simulatedMinutes++;

                // Check for event interruption
                if (ctx.EventOccurredLastUpdate)
                {
                    return (simulatedMinutes, true); // Exit early - event handled
                }
            }

            float progress = (float)simulatedMinutes / durationMinutes;

            // Render frame with full StatsPanel + dialog
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            // Full stats panel
            UI.StatsPanel.Render(ctx);

            // Centered progress dialog
            var io = ImGui.GetIO();
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(400, 0), ImGuiCond.Always);

            ImGui.Begin("Activity", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
            ImGui.TextWrapped(statusText);
            ImGui.Spacing();
            ImGui.ProgressBar(progress, new Vector2(-1, 20),
                $"{simulatedMinutes}/{durationMinutes} min");
            ImGui.End();

            rlImGui.End();
            Raylib.EndDrawing();
        }

        return (simulatedMinutes, false);
    }

    /// <summary>
    /// Show a progress dialog with loot items revealed during the activity.
    /// Items appear after 20% progress, biased towards the end, with 1 second between each.
    /// Returns (elapsed minutes, whether event interrupted).
    /// </summary>
    public static (int elapsed, bool interrupted) ShowProgressWithLoot(
        GameContext ctx,
        string statusText,
        int durationMinutes,
        ActivityType activity,
        List<Items.LootItem> items)
    {
        // ~0.3 seconds per in-game minute, clamped to reasonable bounds
        float animDuration = Math.Clamp(durationMinutes * 0.3f, 1.0f, 30.0f);
        float elapsed = 0;
        int simulatedMinutes = 0;

        // Calculate reveal times for items (biased towards end, none in first 20%)
        var revealTimes = CalculateRevealTimes(items.Count, animDuration);
        int revealedCount = 0;
        float totalWeight = 0;

        while (simulatedMinutes < durationMinutes && !Raylib.WindowShouldClose() && ctx.player.IsAlive)
        {
            float deltaTime = Raylib.GetFrameTime();
            elapsed += deltaTime;

            // Calculate how many minutes to simulate this frame
            float minutesPerSecond = durationMinutes / animDuration;
            int targetMinutes = Math.Min((int)(elapsed * minutesPerSecond), durationMinutes);

            // Simulate any pending minutes
            while (simulatedMinutes < targetMinutes && ctx.player.IsAlive)
            {
                ctx.Update(1, activity);
                simulatedMinutes++;

                // Check for event interruption
                if (ctx.EventOccurredLastUpdate)
                {
                    return (simulatedMinutes, true);
                }
            }

            // Check for item reveals
            while (revealedCount < items.Count && elapsed >= revealTimes[revealedCount])
            {
                totalWeight += (float)items[revealedCount].WeightKg;
                revealedCount++;
            }

            float progress = (float)simulatedMinutes / durationMinutes;

            // Render frame
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            UI.StatsPanel.Render(ctx);

            // Progress dialog with loot
            var io = ImGui.GetIO();
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(400, 0), ImGuiCond.Always);

            ImGui.Begin("Activity", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

            ImGui.TextWrapped(statusText);
            ImGui.Spacing();
            ImGui.ProgressBar(progress, new Vector2(-1, 20),
                $"{simulatedMinutes}/{durationMinutes} min");

            // Show revealed items
            if (revealedCount > 0)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("Found:");
                for (int i = 0; i < revealedCount; i++)
                {
                    var item = items[i];
                    var color = GetCategoryColor(item.Category);
                    string text = item.Count > 1
                        ? $"  {item.Count}x {item.Name} ({item.WeightKg:F1}kg)"
                        : $"  {item.Name} ({item.WeightKg:F2}kg)";
                    ImGui.TextColored(color, text);
                }

                ImGui.Spacing();
                ImGui.Text($"Total: {totalWeight:F1}kg");
            }

            ImGui.End();

            rlImGui.End();
            Raylib.EndDrawing();
        }

        return (simulatedMinutes, false);
    }

    /// <summary>
    /// Calculate reveal times for items. No items in first 20%, biased towards end.
    /// Each item has 1 second of display time before the next appears.
    /// </summary>
    private static List<float> CalculateRevealTimes(int itemCount, float totalDuration)
    {
        var times = new List<float>();
        if (itemCount == 0) return times;

        var rng = new Random();

        // Available window: 20% to 100% of duration
        float startTime = totalDuration * 0.2f;
        float endTime = totalDuration;
        float window = endTime - startTime;

        // Generate random positions biased towards end (using x^2 distribution)
        var rawPositions = new List<float>();
        for (int i = 0; i < itemCount; i++)
        {
            // x^2 biases towards 1.0 (end)
            double x = rng.NextDouble();
            float biasedPosition = (float)(x * x);
            rawPositions.Add(biasedPosition);
        }

        // Sort positions
        rawPositions.Sort();

        // Convert to actual times, ensuring 1 second minimum gap
        float minGap = 1.0f;
        float lastTime = startTime - minGap; // Allow first item at startTime

        foreach (var pos in rawPositions)
        {
            float idealTime = startTime + pos * window;
            float actualTime = Math.Max(idealTime, lastTime + minGap);

            // Don't exceed end time
            if (actualTime > endTime)
                actualTime = endTime;

            times.Add(actualTime);
            lastTime = actualTime;
        }

        return times;
    }

    private static Vector4 GetCategoryColor(Items.ResourceCategory? category) => category switch
    {
        Items.ResourceCategory.Fuel => new Vector4(0.8f, 0.6f, 0.4f, 1f),      // Warm brown
        Items.ResourceCategory.Food => new Vector4(0.5f, 0.8f, 0.5f, 1f),      // Green
        Items.ResourceCategory.Medicine => new Vector4(0.7f, 0.5f, 0.8f, 1f),  // Purple
        Items.ResourceCategory.Material => new Vector4(0.6f, 0.7f, 0.8f, 1f),  // Blue-gray
        Items.ResourceCategory.Tinder => new Vector4(0.9f, 0.7f, 0.5f, 1f),    // Orange
        _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)                                  // Gray
    };
}
