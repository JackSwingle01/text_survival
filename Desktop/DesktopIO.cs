using Raylib_cs;
using rlImGui_cs;
using text_survival.Actions;
using text_survival.Actions.Variants;
using text_survival.Actions.Handlers;
using text_survival.Combat;
using text_survival.Crafting;
using text_survival.Desktop.Dto;
using text_survival.Desktop.UI;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.UI;
using ForageFocus = text_survival.Actions.Variants.ForageFocus;

namespace text_survival.Desktop;

public static class DesktopIO
{
    public static string GenerateSemanticId(string label, int index)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(label.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        if (slug.Length > 30) slug = slug[..30];
        return $"{slug}_{index}";
    }

    public static void ClearInventory(GameContext ctx) { }
    public static void ClearCrafting(GameContext ctx) { }
    public static void ClearEvent(GameContext ctx) { }
    public static void ClearHazard(GameContext ctx) { }
    public static void ClearConfirm(GameContext ctx) { }
    public static void ClearForage(GameContext ctx) { }
    public static void ClearTransfer(GameContext ctx) { }
    public static void ClearFire(GameContext ctx) { }
    public static void ClearCooking(GameContext ctx) { }
    public static void ClearButcher(GameContext ctx) { }
    public static void ClearCombat(GameContext ctx) { }
    public static void ClearDiscovery(GameContext ctx) { }
    public static void ClearWeatherChange(GameContext ctx) { }
    public static void ClearDiscoveryLog(GameContext ctx) { }
    public static void ClearAllOverlays(string sessionId) { }

    public static void ShowDiscovery(GameContext ctx, string locationName, string discoveryText)
    {
        BlockingDialog.ShowMessageAndWait(ctx, "Discovery!", $"{locationName}\n\n{discoveryText}");
    }

    private static readonly UI.MajorDiscoveryOverlay _majorDiscoveryOverlay = new();

    /// <summary>
    /// Show a major discovery popup during foraging.
    /// Blocks until player acknowledges.
    /// </summary>
    public static void ShowMajorDiscovery(GameContext ctx, string message)
    {
        _majorDiscoveryOverlay.Open(message);

        while (_majorDiscoveryOverlay.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            // Dim background
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();
            _majorDiscoveryOverlay.Render(deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }
    }

    public static void ShowWeatherChange(GameContext ctx)
    {
        var weather = ctx.Weather;
        double tempF = weather.TemperatureInFahrenheit;
        string message = $"The weather has changed.\n\n" +
            $"Temperature: {tempF:F0}°F\n" +
            $"Wind: {weather.CurrentCondition}\n" +
            (weather.PrecipitationPct > 0 ? $"Precipitation: {weather.PrecipitationPct:P0}" : "Clear skies");
        BlockingDialog.ShowMessageAndWait(ctx, "Weather", message);
    }

    public static void ShowDiscoveryLogAndWait(GameContext ctx)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        overlays.ToggleDiscoveryLog();
        overlays.DiscoveryLog.SetData(ctx.Discoveries.ToDto());

        while (overlays.DiscoveryLog.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            // Handle escape to close (don't check 'L' here - it may still be pressed from the
            // main loop that opened this overlay, causing an immediate close before any frame renders)
            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                overlays.DiscoveryLog.IsOpen = false;
                break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            rlImGui.Begin();
            overlays.DiscoveryLog.Render(deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }
    }

    public static void ShowNPCsAndWait(GameContext ctx)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        var npcsHere = ctx.NPCs.Where(n => n.CurrentLocation == ctx.CurrentLocation).ToList();
        if (npcsHere.Count == 0) return;

        overlays.ToggleNPCs();

        while (overlays.NPCs.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                overlays.NPCs.IsOpen = false;
                break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            rlImGui.Begin();
            overlays.NPCs.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }
    }

    public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices, Func<T, string> display, Func<T, bool>? isDisabled = null)
        => BlockingDialog.Select(ctx, prompt, choices, display, isDisabled);

    public static bool Confirm(GameContext ctx, string prompt)
        => BlockingDialog.Confirm(ctx, prompt);

    public static string PromptConfirm(GameContext ctx, string message, Dictionary<string, string> buttons)
        => BlockingDialog.PromptConfirm(ctx, message, buttons);

    public static int ReadInt(GameContext ctx, string prompt, int min, int max, bool allowCancel = false)
        => BlockingDialog.ReadInt(ctx, prompt, min, max, allowCancel);

    private static GameEventOverlay? _eventOverlay;

    public static void RenderEvent(GameContext ctx, EventDto eventData)
    {
        _eventOverlay ??= new GameEventOverlay();
        _eventOverlay.ShowEvent(eventData);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        DesktopRuntime.WorldRenderer?.Update(ctx, Raylib.GetFrameTime());
        DesktopRuntime.WorldRenderer?.Render(ctx);

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color(0, 0, 0, 128));

        rlImGui.Begin();
        _eventOverlay.Render(Raylib.GetFrameTime());
        rlImGui.End();

        Raylib.EndDrawing();
    }

    public static string WaitForEventChoice(GameContext ctx, EventDto eventData)
    {
        _eventOverlay ??= new GameEventOverlay();
        _eventOverlay.ShowEvent(eventData);

        string? choice = null;

        while (choice == null && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();
            choice = _eventOverlay.Render(deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        return choice ?? "cancel";
    }

    public static void WaitForEventContinue(GameContext ctx)
    {
        if (_eventOverlay == null || !_eventOverlay.IsOpen) return;

        while (_eventOverlay.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();
            _eventOverlay.Render(deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        _eventOverlay = null;
    }

    public static void ShowEventOutcome(GameContext ctx, EventOutcomeDto outcome)
    {
        _eventOverlay ??= new GameEventOverlay();
        _eventOverlay.ShowOutcome(outcome);

        while (_eventOverlay.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();
            _eventOverlay.Render(deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        _eventOverlay = null;
    }

    public static void Render(GameContext ctx, string? statusText = null)
    {
        DesktopRuntime.RenderFrame(ctx);
    }

    public static void RenderWithDuration(GameContext ctx, string statusText, int estimatedMinutes)
    {
        // Legacy: just animate without simulation (for backwards compatibility)
        // ~0.3 seconds per in-game minute, clamped to reasonable bounds
        float animDuration = Math.Clamp(estimatedMinutes * 0.3f, 1.0f, 30.0f);
        float elapsed = 0;

        while (elapsed < animDuration && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();
            elapsed += deltaTime;
            float progress = Math.Min(elapsed / animDuration, 1.0f);

            DesktopRuntime.RenderFrameWithDialog(ctx, () =>
            {
                var io = ImGuiNET.ImGui.GetIO();
                Desktop.UI.OverlaySizes.SetupDialog();

                ImGuiNET.ImGui.Begin("Activity", ImGuiNET.ImGuiWindowFlags.NoResize | ImGuiNET.ImGuiWindowFlags.NoMove | ImGuiNET.ImGuiWindowFlags.NoCollapse);
                ImGuiNET.ImGui.TextWrapped(statusText);
                ImGuiNET.ImGui.Spacing();
                ImGuiNET.ImGui.ProgressBar(progress, new System.Numerics.Vector2(-1, 20),
                    $"{(int)(progress * estimatedMinutes)}/{estimatedMinutes} min");
                ImGuiNET.ImGui.End();
            });
        }
    }

    public static (int elapsed, bool interrupted) RenderWithDuration(GameContext ctx, string statusText, int estimatedMinutes, ActivityType activity)
    {
        return BlockingDialog.ShowProgress(ctx, statusText, estimatedMinutes, activity);
    }

    public static (int elapsed, bool interrupted) RenderWithDurationAndLoot(
        GameContext ctx,
        string statusText,
        int estimatedMinutes,
        ActivityType activity,
        List<LootItem> items)
    {
        return BlockingDialog.ShowProgressWithLoot(ctx, statusText, estimatedMinutes, activity, items);
    }

    public static void RenderInventory(GameContext ctx, Inventory inventory, string title)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        if (!overlays.Inventory.IsOpen)
        {
            overlays.ToggleInventory();
        }

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        DesktopRuntime.WorldRenderer?.Update(ctx, Raylib.GetFrameTime());
        DesktopRuntime.WorldRenderer?.Render(ctx);

        rlImGui.Begin();
        overlays.Render(ctx, Raylib.GetFrameTime());
        rlImGui.End();

        Raylib.EndDrawing();
    }

    public static void ShowInventoryAndWait(GameContext ctx, Inventory inventory, string title)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        if (!overlays.Inventory.IsOpen)
        {
            overlays.ToggleInventory();
        }

        while (overlays.Inventory.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            // Handle escape to close (don't check 'I' here - it may still be pressed from the
            // main loop that opened this overlay, causing an immediate close before any frame renders)
            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                overlays.Inventory.IsOpen = false;
                break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            rlImGui.Begin();
            overlays.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }
    }

    public static void RenderCrafting(GameContext ctx, NeedCraftingSystem crafting, string title = "CRAFTING")
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        if (!overlays.Crafting.IsOpen)
        {
            overlays.ToggleCrafting();
        }

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        DesktopRuntime.WorldRenderer?.Update(ctx, Raylib.GetFrameTime());
        DesktopRuntime.WorldRenderer?.Render(ctx);

        rlImGui.Begin();
        overlays.Render(ctx, Raylib.GetFrameTime());
        rlImGui.End();

        Raylib.EndDrawing();
    }

    // The overlay handles all crafting logic internally (material consumption, time advancement).
    public static void RunCraftingAndWait(GameContext ctx)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        if (!overlays.Crafting.IsOpen)
        {
            overlays.ToggleCrafting();
        }

        while (overlays.Crafting.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                overlays.Crafting.IsOpen = false;
                break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            rlImGui.Begin();
            overlays.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }
    }

    public static PlayerResponse RenderGridAndWaitForInput(GameContext ctx, string? statusText = null)
    {
        var worldRenderer = DesktopRuntime.WorldRenderer;
        var actionPanel = DesktopRuntime.ActionPanel;
        var overlays = DesktopRuntime.Overlays;
        int inputId = 0;

        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            if (Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.Up))
            {
                var pos = ctx.Map.CurrentPosition;
                if (ctx.Map.CanMoveTo(pos.X, pos.Y - 1))
                    return new PlayerResponse(null, inputId, "travel_to", pos.X, pos.Y - 1);
            }
            if (Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressed(KeyboardKey.Down))
            {
                var pos = ctx.Map.CurrentPosition;
                if (ctx.Map.CanMoveTo(pos.X, pos.Y + 1))
                    return new PlayerResponse(null, inputId, "travel_to", pos.X, pos.Y + 1);
            }
            if (Raylib.IsKeyPressed(KeyboardKey.A) || Raylib.IsKeyPressed(KeyboardKey.Left))
            {
                var pos = ctx.Map.CurrentPosition;
                if (ctx.Map.CanMoveTo(pos.X - 1, pos.Y))
                    return new PlayerResponse(null, inputId, "travel_to", pos.X - 1, pos.Y);
            }
            if (Raylib.IsKeyPressed(KeyboardKey.D) || Raylib.IsKeyPressed(KeyboardKey.Right))
            {
                var pos = ctx.Map.CurrentPosition;
                if (ctx.Map.CanMoveTo(pos.X + 1, pos.Y))
                    return new PlayerResponse(null, inputId, "travel_to", pos.X + 1, pos.Y);
            }

            if (Raylib.IsKeyPressed(KeyboardKey.I))
                return new PlayerResponse(null, inputId, "action", Action: "inventory");
            if (Raylib.IsKeyPressed(KeyboardKey.C))
                return new PlayerResponse(null, inputId, "action", Action: "crafting");
            if (Raylib.IsKeyPressed(KeyboardKey.Space))
                return new PlayerResponse(null, inputId, "action", Action: "wait");

            if (worldRenderer != null)
            {
                // Handle combat grid clicks
                if (ctx.ActiveCombat != null)
                {
                    var combatClick = worldRenderer.HandleCombatClick();
                    if (combatClick.HasValue)
                    {
                        return new PlayerResponse(null, inputId, "action",
                            CombatMoveTarget: new GridPosition(combatClick.Value.x, combatClick.Value.y));
                    }
                }
                else
                {
                    // Handle world map clicks (only when not in combat)
                    var clicked = worldRenderer.HandleClick();
                    if (clicked.HasValue)
                    {
                        var (x, y) = clicked.Value;
                        var currentPos = ctx.Map.CurrentPosition;
                        bool isAdjacent = Math.Abs(x - currentPos.X) <= 1 && Math.Abs(y - currentPos.Y) <= 1
                            && (x != currentPos.X || y != currentPos.Y);

                        if (isAdjacent && ctx.Map.CanMoveTo(x, y))
                        {
                            return new PlayerResponse(null, inputId, "travel_to", x, y);
                        }
                    }
                }
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            if (worldRenderer != null)
            {
                worldRenderer.Update(ctx, deltaTime);
                worldRenderer.Render(ctx);
            }

            if (statusText != null)
            {
                int fontSize = 20;
                int textWidth = Raylib.MeasureText(statusText, fontSize);
                int screenWidth = Raylib.GetScreenWidth();
                int screenHeight = Raylib.GetScreenHeight();
                Raylib.DrawRectangle(10, screenHeight - 40, textWidth + 20, 30, new Color(30, 35, 40, 200));
                Raylib.DrawText(statusText, 20, screenHeight - 35, fontSize, new Color(200, 220, 240, 255));
            }

            rlImGui.Begin();

            if (actionPanel != null)
            {
                var (campAction, _, combatAction) = actionPanel.Render(ctx, deltaTime);
                if (campAction != null)
                {
                    rlImGui.End();
                    Raylib.EndDrawing();
                    return new PlayerResponse(null, inputId, "action", CampAction: campAction);
                }
                // Work is handled by GameRunner.RunGameLoop directly, not through this path
                // Combat is the only caller and doesn't use work actions
                if (combatAction != null)
                {
                    rlImGui.End();
                    Raylib.EndDrawing();
                    return new PlayerResponse(null, inputId, "action", CombatAction: combatAction);
                }
            }

            overlays?.Render(ctx, deltaTime);

            // Render stats panel (stays visible during combat)
            Desktop.UI.StatsPanel.Render(ctx);

            Desktop.UI.ToastManager.Render(deltaTime);

            rlImGui.End();
            Raylib.EndDrawing();
        }

        return new PlayerResponse(null, inputId, "action", Action: "quit");
    }

    public static string? PromptHazardChoice(
        GameContext ctx,
        Location targetLocation,
        int targetX,
        int targetY,
        int quickTimeMinutes,
        int carefulTimeMinutes,
        double hazardLevel)
    {
        int riskPercent = (int)(hazardLevel * 100);
        string message = $"Hazardous terrain ahead: {targetLocation.Name}\n\n" +
            $"Risk of injury: {riskPercent}%\n\n" +
            $"Quick crossing: {quickTimeMinutes} minutes (full risk)\n" +
            $"Careful crossing: {carefulTimeMinutes} minutes (reduced risk)";

        var buttons = new Dictionary<string, string>
        {
            { "quick", $"Quick ({quickTimeMinutes}min)" },
            { "careful", $"Careful ({carefulTimeMinutes}min)" },
            { "cancel", "Turn back" }
        };

        string choice = BlockingDialog.PromptConfirm(ctx, message, buttons);
        if (choice == "cancel") return null;
        return choice;
    }

    private static UI.ForageOverlay? _forageOverlay;

    /// <summary>
    /// Show forage overlay and get player selection.
    /// Returns (focus, minutes). Focus=null and minutes=0 means cancelled.
    /// Focus=null and minutes=-1 means "keep walking" (reroll clues).
    /// </summary>
    public static (ForageFocus? focus, int minutes) SelectForageOptions(
        GameContext ctx,
        ForageFeature feature,
        List<ForageClue> clues)
    {
        _forageOverlay ??= new UI.ForageOverlay();
        _forageOverlay.Open(ctx, feature, clues);

        UI.ForageResult? result = null;

        while (result == null && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();
            result = _forageOverlay.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        _forageOverlay.Close();

        if (result == null)
            return (null, 0);

        return (result.Focus, result.Minutes);
    }

    /// <summary>
    /// Select butchering mode for a fresh carcass. Returns mode ID or null if cancelled.
    /// Only called when carcass.SelectedMode is null (first butchering session).
    /// Time selection happens separately via standard time chunk UI.
    /// </summary>
    public static string? SelectButcherMode(GameContext ctx, CarcassFeature carcass, List<string>? warnings = null)
    {
        var choices = new List<(string id, string label)>
        {
            ("cancel", "Cancel")
        };

        // Show estimated total time for each mode to help player choose
        var modes = new[]
        {
            ("quick", "Quick Strip - Fast, meat-focused, messy", carcass.GetRemainingMinutes(ButcheringMode.QuickStrip)),
            ("careful", "Careful - Balanced approach", carcass.GetRemainingMinutes(ButcheringMode.Careful)),
            ("full", "Full Processing - Slow, maximum yield", carcass.GetRemainingMinutes(ButcheringMode.FullProcessing))
        };

        foreach (var (id, label, minutes) in modes)
        {
            string timeStr = $" (~{minutes}min total)";
            choices.Add((id, $"{label}{timeStr}"));
        }

        string description = $"Butcher: {carcass.AnimalName}\n" +
            $"Condition: {carcass.GetDecayDescription()}\n" +
            $"Total yield: ~{carcass.GetTotalRemainingKg():F1}kg\n\n" +
            "Choose your approach:";

        if (warnings != null && warnings.Count > 0)
        {
            description += "\n\n" + string.Join("\n", warnings);
        }

        var selection = BlockingDialog.Select(ctx, description, choices, c => c.label);

        return selection.id == "cancel" ? null : selection.id;
    }

    public static void ShowWorkResult(
        GameContext ctx,
        string activityName,
        string message,
        List<string> itemsGained,
        List<string>? narrative = null,
        List<string>? warnings = null)
    {
        var sb = new System.Text.StringBuilder();

        // Add contextual narrative first (what happened during the activity)
        if (narrative != null && narrative.Count > 0)
        {
            foreach (var line in narrative)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine();
        }

        // Add result message
        sb.Append(message);

        // Add warnings
        if (warnings != null && warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            foreach (var warning in warnings)
            {
                sb.AppendLine($"⚠ {warning}");
            }
        }

        // Add items gained
        if (itemsGained.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Gained:");
            foreach (var item in itemsGained)
            {
                sb.AppendLine($"  - {item}");
            }
        }

        BlockingDialog.ShowMessageAndWait(ctx, activityName, sb.ToString().TrimEnd());
    }

    private static readonly LootRevealOverlay _lootRevealOverlay = new();

    /// <summary>
    /// Show found loot summary with all items displayed.
    /// </summary>
    public static void ShowLootReveal(GameContext ctx, Inventory found)
    {
        var items = found.GetLootItems();
        if (items.Count == 0)
        {
            BlockingDialog.ShowMessageAndWait(ctx, "Foraging", "You didn't find anything.");
            return;
        }

        _lootRevealOverlay.SetItems(items);

        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            // Dim background
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            if (_lootRevealOverlay.Render(deltaTime))
            {
                rlImGui.End();
                Raylib.EndDrawing();
                break;
            }

            rlImGui.End();
            Raylib.EndDrawing();
        }
    }

    public static void RunTransferUI(GameContext ctx, Inventory storage, string storageName)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        overlays.OpenTransfer(storage, storageName);
        var playerInv = ctx.Inventory;

        while (overlays.Transfer.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                overlays.Transfer.IsOpen = false;
                break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            var result = overlays.Transfer.Render(ctx, deltaTime);

            if (result != null)
            {
                ProcessTransferResult(ctx, result, playerInv, storage, overlays.Transfer);
            }

            rlImGui.End();
            Raylib.EndDrawing();
        }
    }

    private static void ProcessTransferResult(GameContext ctx, TransferResult uiResult, Inventory playerInv, Inventory storage, TransferOverlay overlay)
    {
        var source = uiResult.FromPlayer ? playerInv : storage;
        var dest = uiResult.FromPlayer ? storage : playerInv;
        string direction = uiResult.FromPlayer ? "to storage" : "to inventory";

        TransferHandler.TransferResult result = uiResult switch
        {
            { Resource: { } r } => TransferHandler.TransferResource(source, dest, r, direction),
            { IsWater: true } => TransferHandler.TransferWater(source, dest, uiResult.WaterAmount, direction),
            { Tool: { } t } => TransferHandler.TransferTool(source, dest, t, direction),
            { Equipment: { } e } => TransferHandler.TransferEquipment(source, dest, e, direction),
            { Accessory: { } a } => TransferHandler.TransferAccessory(source, dest, a, direction),
            _ => new TransferHandler.TransferResult(false, "Nothing to transfer")
        };

        if (result.Success)
            overlay.SetMessage(result.Message);
    }

    public static void RunFireUI(GameContext ctx, HeatSourceFeature? fire)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        overlays.OpenFire(fire);

        while (overlays.Fire.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                overlays.Fire.IsOpen = false;
                break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            var result = overlays.Fire.Render(ctx, deltaTime);

            if (result != null)
            {
                ProcessFireResult(ctx, result, overlays.Fire);
            }

            rlImGui.End();
            Raylib.EndDrawing();
        }
    }

    private static void ProcessFireResult(GameContext ctx, FireOverlayResult result, FireOverlay overlay)
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        FireHandler.FireActionResult actionResult = result.Action switch
        {
            FireAction.StartFire when result.Tool != null && result.Tinder != null
                => FireHandler.ProcessStartFire(ctx, result.Tool, result.Tinder.Value, fire),
            FireAction.StartFromEmber when result.EmberCarrier != null
                => FireHandler.ProcessStartFromEmber(ctx, result.EmberCarrier, fire),
            FireAction.AddFuel when result.FuelResource != null && fire != null
                => FireHandler.AddFuelWithResult(ctx.Inventory, fire, result.FuelResource.Value),
            FireAction.CollectCharcoal
                => FireHandler.CollectCharcoal(fire, ctx.Inventory),
            FireAction.LightTorch
                => FireHandler.LightTorchFromFire(fire, ctx.Inventory),
            FireAction.CollectEmber
                => FireHandler.CollectEmber(fire, ctx.Inventory),
            _ => new FireHandler.FireActionResult(false, "Unknown action")
        };

        if (result.Action == FireAction.StartFire || result.Action == FireAction.StartFromEmber)
            overlay.SetAttemptResult(actionResult.Success, actionResult.Message);
        else
            overlay.SetTendMessage(actionResult.Message);
    }

    public static void RunEatingUI(GameContext ctx)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        overlays.OpenEating();

        while (overlays.Eating.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                overlays.Eating.IsOpen = false;
                break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            var consumedId = overlays.Eating.Render(ctx, deltaTime);

            if (consumedId != null)
            {
                var consumeResult = ConsumptionHandler.Consume(ctx, consumedId);
                overlays.Eating.SetConsumeResult(consumeResult.Message, consumeResult.IsWarning);
            }

            rlImGui.End();
            Raylib.EndDrawing();
        }
    }

    public static void RunCookingUI(GameContext ctx)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        overlays.OpenCooking();

        while (overlays.Cooking.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                overlays.Cooking.IsOpen = false;
                break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            var result = overlays.Cooking.Render(ctx, deltaTime);

            if (result != null)
            {
                ProcessCookingResult(ctx, result, overlays.Cooking);
            }

            rlImGui.End();
            Raylib.EndDrawing();
        }
    }

    private static void ProcessCookingResult(GameContext ctx, CookingOverlayResult result, CookingOverlay overlay)
    {
        CookingHandler.CookingResult actionResult = result.Action switch
        {
            CookingAction.CookMeat => CookingHandler.ProcessCookMeat(ctx),
            CookingAction.CookFish => CookingHandler.ProcessCookFish(ctx),
            CookingAction.MeltSnow => CookingHandler.ProcessMeltSnow(ctx),
            _ => new CookingHandler.CookingResult(false, "Unknown action", 0)
        };

        overlay.SetActionResult(actionResult.Success, actionResult.Message);
    }

    public static void RunAITurnsWithAnimation(GameContext ctx, CombatScenario scenario, Unit playerUnit)
    {
        while (scenario.HasRemainingAITurns(playerUnit) && !Raylib.WindowShouldClose())
        {
            var narrative = scenario.RunNextAITurn(playerUnit);

            if (narrative != null)
                GameDisplay.AddNarrative(ctx, narrative);

            if (scenario.IsOver) break;

            // Render and wait 1 second before next AI turn
            RenderCombatDelayForSeconds(ctx, 1.0f);
        }
    }

    private static void RenderCombatDelayForSeconds(GameContext ctx, float seconds)
    {
        float elapsed = 0;
        while (elapsed < seconds && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();
            elapsed += deltaTime;

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            rlImGui.Begin();
            DesktopRuntime.Overlays?.Render(ctx, deltaTime);
            Desktop.UI.StatsPanel.Render(ctx);
            Desktop.UI.ToastManager.Render(deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }
    }
}
