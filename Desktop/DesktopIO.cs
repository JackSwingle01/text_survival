using Raylib_cs;
using rlImGui_cs;
using text_survival.Actions;
using text_survival.Actions.Variants;
using text_survival.Actions.Handlers;
using text_survival.Crafting;
using text_survival.Desktop.Dto;
using text_survival.Desktop.UI;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using ForageFocus = text_survival.Actions.Variants.ForageFocus;

namespace text_survival.Desktop;

/// <summary>
/// Desktop I/O implementation using Raylib + ImGui.
/// All blocking methods use nested render loops to keep the UI responsive.
/// </summary>
public static class DesktopIO
{
    /// <summary>
    /// Generate a semantic ID from a label for more debuggable choice matching.
    /// </summary>
    public static string GenerateSemanticId(string label, int index)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(label.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        if (slug.Length > 30) slug = slug[..30];
        return $"{slug}_{index}";
    }

    // Clear methods (no-op for now)
    public static void ClearInventory(GameContext ctx) { }
    public static void ClearCrafting(GameContext ctx) { }
    public static void ClearEvent(GameContext ctx) { }
    public static void ClearHazard(GameContext ctx) { }
    public static void ClearConfirm(GameContext ctx) { }
    public static void ClearForage(GameContext ctx) { }
    public static void ClearHunt(GameContext ctx) { }
    public static void ClearTransfer(GameContext ctx) { }
    public static void ClearFire(GameContext ctx) { }
    public static void ClearCooking(GameContext ctx) { }
    public static void ClearButcher(GameContext ctx) { }
    public static void ClearEncounter(GameContext ctx) { }
    public static void ClearCombat(GameContext ctx) { }
    public static void ClearDiscovery(GameContext ctx) { }
    public static void ClearWeatherChange(GameContext ctx) { }
    public static void ClearDiscoveryLog(GameContext ctx) { }
    public static void ClearAllOverlays(string sessionId) { }

    // Hunt methods

    // Persistent hunt overlay for the hunt sequence
    private static HuntOverlay? _huntOverlay;

    /// <summary>
    /// Render hunt state (non-blocking, for intermediate states).
    /// </summary>
    public static void RenderHunt(GameContext ctx, HuntDto huntData)
    {
        _huntOverlay ??= new HuntOverlay();
        _huntOverlay.Open(huntData);

        // Single frame render
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        DesktopRuntime.WorldRenderer?.Update(ctx, Raylib.GetFrameTime());
        DesktopRuntime.WorldRenderer?.Render(ctx);

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color(0, 0, 0, 128));

        rlImGui.Begin();
        _huntOverlay.Render(ctx, Raylib.GetFrameTime());
        rlImGui.End();

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Show hunt UI and block until player makes a choice.
    /// </summary>
    public static string WaitForHuntChoice(GameContext ctx, HuntDto huntData)
    {
        _huntOverlay ??= new HuntOverlay();
        _huntOverlay.Open(huntData);

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
            choice = _huntOverlay.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        return choice ?? "stop";
    }

    /// <summary>
    /// Wait for player to dismiss hunt outcome screen.
    /// </summary>
    public static void WaitForHuntContinue(GameContext ctx)
    {
        if (_huntOverlay == null || !_huntOverlay.IsOpen) return;

        string? choice = null;

        while (choice != "continue" && _huntOverlay.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();
            choice = _huntOverlay.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        _huntOverlay.Close();
    }

    // Encounter methods

    // Persistent encounter overlay for the encounter sequence
    private static EncounterOverlay? _encounterOverlay;

    /// <summary>
    /// Render encounter state (non-blocking, for intermediate states).
    /// </summary>
    public static void RenderEncounter(GameContext ctx, EncounterDto encounterData)
    {
        _encounterOverlay ??= new EncounterOverlay();
        _encounterOverlay.Open(encounterData);

        // Single frame render
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        DesktopRuntime.WorldRenderer?.Update(ctx, Raylib.GetFrameTime());
        DesktopRuntime.WorldRenderer?.Render(ctx);

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color(0, 0, 0, 128));

        rlImGui.Begin();
        _encounterOverlay.Render(ctx, Raylib.GetFrameTime());
        rlImGui.End();

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Show encounter UI and block until player makes a choice.
    /// </summary>
    public static string WaitForEncounterChoice(GameContext ctx, EncounterDto encounterData)
    {
        _encounterOverlay ??= new EncounterOverlay();
        _encounterOverlay.Open(encounterData);

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
            choice = _encounterOverlay.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        return choice ?? "run";
    }

    /// <summary>
    /// Wait for player to dismiss encounter outcome screen.
    /// </summary>
    public static void WaitForEncounterContinue(GameContext ctx)
    {
        if (_encounterOverlay == null || !_encounterOverlay.IsOpen) return;

        string? choice = null;

        while (choice != "continue" && _encounterOverlay.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();
            choice = _encounterOverlay.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        _encounterOverlay.Close();
    }

    // Combat methods

    // Persistent combat overlay for the combat sequence
    private static CombatOverlay? _combatOverlay;

    /// <summary>
    /// Render combat state (non-blocking, for intermediate states).
    /// </summary>
    public static void RenderCombat(GameContext ctx, CombatDto combatData)
    {
        _combatOverlay ??= new CombatOverlay();
        _combatOverlay.Update(combatData);

        // Single frame render
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        DesktopRuntime.WorldRenderer?.Update(ctx, Raylib.GetFrameTime());
        DesktopRuntime.WorldRenderer?.Render(ctx);

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color(0, 0, 0, 128));

        rlImGui.Begin();
        _combatOverlay.Render(ctx, Raylib.GetFrameTime());
        rlImGui.End();

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Show combat UI and block until player makes a choice.
    /// </summary>
    public static string WaitForCombatChoice(GameContext ctx, CombatDto combatData)
    {
        _combatOverlay ??= new CombatOverlay();
        _combatOverlay.Open(combatData);

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
            choice = _combatOverlay.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        return choice ?? "disengage";
    }

    /// <summary>
    /// Show target selection and block until player chooses a target.
    /// </summary>
    public static string WaitForTargetChoice(GameContext ctx, List<CombatActionDto> targetingOptions, string animalName)
    {
        // Build a selection prompt from targeting options
        string prompt = $"Select target on {animalName}:";
        var choices = targetingOptions.Select(t => (t.Id, $"{t.Label} {(t.HitChance != null ? $"({t.HitChance})" : "")}")).ToList();

        if (choices.Count == 0)
        {
            return "torso"; // Default target
        }

        var selection = BlockingDialog.Select(ctx, prompt, choices, c => c.Item2);
        return selection.Id;
    }

    /// <summary>
    /// Wait for player to dismiss combat outcome or auto-advance.
    /// </summary>
    public static void WaitForCombatContinue(GameContext ctx)
    {
        if (_combatOverlay == null || !_combatOverlay.IsOpen) return;

        string? choice = null;

        while (choice != "continue" && _combatOverlay.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();
            choice = _combatOverlay.Render(ctx, deltaTime);
            rlImGui.End();

            Raylib.EndDrawing();
        }

        _combatOverlay.Close();
    }

    // Discovery/Weather methods
    public static void ShowDiscovery(GameContext ctx, string locationName, string discoveryText)
    {
        BlockingDialog.ShowMessageAndWait(ctx, "Discovery!", $"{locationName}\n\n{discoveryText}");
    }

    public static void ShowWeatherChange(GameContext ctx)
    {
        var weather = ctx.Weather;
        double tempF = weather.BaseTemperature * 9 / 5 + 32; // Convert C to F
        string message = $"The weather has changed.\n\n" +
            $"Temperature: {tempF:F0}°F\n" +
            $"Wind: {weather.CurrentCondition}\n" +
            (weather.Precipitation > 0 ? $"Precipitation: {weather.Precipitation:P0}" : "Clear skies");
        BlockingDialog.ShowMessageAndWait(ctx, "Weather", message);
    }

    public static void ShowDiscoveryLogAndWait(GameContext ctx)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        // Open discovery log overlay
        overlays.ToggleDiscoveryLog();
        overlays.DiscoveryLog.SetData(ctx.Discoveries.ToDto());

        // Blocking render loop until overlay is closed
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

        // Check if there are any NPCs at current location
        var npcsHere = ctx.NPCs.Where(n => n.CurrentLocation == ctx.CurrentLocation).ToList();
        if (npcsHere.Count == 0) return;

        // Open NPC overlay
        overlays.ToggleNPCs();

        // Blocking render loop until overlay is closed
        while (overlays.NPCs.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            // Handle escape or N to close
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

    // Core selection methods
    public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices, Func<T, string> display, Func<T, bool>? isDisabled = null)
        => BlockingDialog.Select(ctx, prompt, choices, display, isDisabled);

    public static bool Confirm(GameContext ctx, string prompt)
        => BlockingDialog.Confirm(ctx, prompt);

    public static string PromptConfirm(GameContext ctx, string message, Dictionary<string, string> buttons)
        => BlockingDialog.PromptConfirm(ctx, message, buttons);

    public static int ReadInt(GameContext ctx, string prompt, int min, int max, bool allowCancel = false)
        => BlockingDialog.ReadInt(ctx, prompt, min, max, allowCancel);

    // Event methods

    // Persistent event overlay for event sequences
    private static GameEventOverlay? _eventOverlay;

    /// <summary>
    /// Render event state (non-blocking, for intermediate states).
    /// </summary>
    public static void RenderEvent(GameContext ctx, EventDto eventData)
    {
        _eventOverlay ??= new GameEventOverlay();
        _eventOverlay.ShowEvent(eventData);

        // Single frame render
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

    /// <summary>
    /// Show event UI and block until player makes a choice.
    /// </summary>
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

    /// <summary>
    /// Wait for player to dismiss event outcome screen.
    /// </summary>
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

        _eventOverlay = null; // Clear after use
    }

    /// <summary>
    /// Show event outcome directly (for events resolved by the backend).
    /// </summary>
    public static void ShowEventOutcome(GameContext ctx, EventOutcomeDto outcome)
    {
        _eventOverlay ??= new GameEventOverlay();
        _eventOverlay.ShowOutcome(outcome);

        // Wait for player to dismiss
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

    // Render methods
    public static void Render(GameContext ctx, string? statusText = null)
    {
        // Single frame render - used for intermediate states
        DesktopRuntime.RenderFrame(ctx);
    }

    public static void RenderWithDuration(GameContext ctx, string statusText, int estimatedMinutes)
    {
        // Show progress bar while time passes
        BlockingDialog.ShowProgress(ctx, statusText, estimatedMinutes);
    }

    // RenderTravelProgress removed - travel now uses non-blocking incremental simulation
    // via GameContext.ActiveTravel and GameRunner.ProcessTravelTick()

    // Inventory methods

    /// <summary>
    /// Render inventory state (non-blocking, single frame).
    /// </summary>
    public static void RenderInventory(GameContext ctx, Inventory inventory, string title)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        // Open inventory overlay if not already open
        if (!overlays.Inventory.IsOpen)
        {
            overlays.ToggleInventory();
        }

        // Single frame render
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        DesktopRuntime.WorldRenderer?.Update(ctx, Raylib.GetFrameTime());
        DesktopRuntime.WorldRenderer?.Render(ctx);

        rlImGui.Begin();
        overlays.Render(ctx, Raylib.GetFrameTime());
        rlImGui.End();

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Show inventory and wait for user to close it.
    /// </summary>
    public static void ShowInventoryAndWait(GameContext ctx, Inventory inventory, string title)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        // Open inventory overlay
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

    // Crafting methods

    /// <summary>
    /// Render crafting state (non-blocking, single frame).
    /// </summary>
    public static void RenderCrafting(GameContext ctx, NeedCraftingSystem crafting, string title = "CRAFTING")
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        // Open crafting overlay if not already open
        if (!overlays.Crafting.IsOpen)
        {
            overlays.ToggleCrafting();
        }

        // Single frame render
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        DesktopRuntime.WorldRenderer?.Update(ctx, Raylib.GetFrameTime());
        DesktopRuntime.WorldRenderer?.Render(ctx);

        rlImGui.Begin();
        overlays.Render(ctx, Raylib.GetFrameTime());
        rlImGui.End();

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Run the crafting UI in a blocking loop until user closes it.
    /// The overlay handles all crafting logic internally (material consumption, time advancement).
    /// </summary>
    public static void RunCraftingAndWait(GameContext ctx)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        // Open crafting overlay
        if (!overlays.Crafting.IsOpen)
        {
            overlays.ToggleCrafting();
        }

        while (overlays.Crafting.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            // Handle escape to close
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

    // Grid/Map methods

    /// <summary>
    /// Render the grid and wait for player input.
    /// Returns a PlayerResponse indicating what the player chose to do.
    /// </summary>
    public static PlayerResponse RenderGridAndWaitForInput(GameContext ctx, string? statusText = null)
    {
        var worldRenderer = DesktopRuntime.WorldRenderer;
        var actionPanel = DesktopRuntime.ActionPanel;
        var overlays = DesktopRuntime.Overlays;
        int inputId = 0; // Desktop doesn't need input IDs

        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            // Check for keyboard input
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

            // Check for action keyboard shortcuts
            if (Raylib.IsKeyPressed(KeyboardKey.I))
                return new PlayerResponse(null, inputId, "action", Action: "inventory");
            if (Raylib.IsKeyPressed(KeyboardKey.C))
                return new PlayerResponse(null, inputId, "action", Action: "crafting");
            if (Raylib.IsKeyPressed(KeyboardKey.Space))
                return new PlayerResponse(null, inputId, "action", Action: "wait");

            // Check for tile click
            if (worldRenderer != null)
            {
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

            // Render frame
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            if (worldRenderer != null)
            {
                worldRenderer.Update(ctx, deltaTime);
                worldRenderer.Render(ctx);
            }

            // Draw status text if provided
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

            // Render action panel and check for clicked actions
            if (actionPanel != null)
            {
                var clickedAction = actionPanel.Render(ctx, deltaTime);
                if (clickedAction != null)
                {
                    rlImGui.End();
                    Raylib.EndDrawing();
                    return new PlayerResponse(null, inputId, "action", Action: clickedAction);
                }
            }

            // Render overlays
            overlays?.Render(ctx, deltaTime);

            rlImGui.End();
            Raylib.EndDrawing();
        }

        // Window close - return a default response
        return new PlayerResponse(null, inputId, "action", Action: "quit");
    }

    // Hazard methods
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
        return choice;  // "quick" or "careful"
    }

    // Forage methods

    // Persistent forage overlay for foraging sequences
    private static UI.ForageOverlay? _forageOverlay;

    /// <summary>
    /// Show forage UI and block until player makes selections.
    /// Returns (focus, minutes). Focus=null and minutes=0 means cancelled.
    /// Focus=null and minutes=-1 means "keep walking" (reroll clues).
    /// </summary>
    public static (ForageFocus? focus, int minutes) SelectForageOptions(GameContext ctx, ForageDto forageData)
    {
        _forageOverlay ??= new UI.ForageOverlay();
        _forageOverlay.Open(forageData);

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
            return (null, 0); // Window closed

        return (result.Focus, result.Minutes);
    }

    // Butcher methods
    public static string? SelectButcherOptions(GameContext ctx, CarcassFeature carcass, List<string>? warnings = null)
    {
        var choices = new List<(string id, string label)>
        {
            ("cancel", "Cancel")
        };

        // Build mode options from carcass
        var modes = new[]
        {
            ("quick", "Quick Strip", carcass.GetRemainingMinutes(ButcheringMode.QuickStrip)),
            ("careful", "Careful", carcass.GetRemainingMinutes(ButcheringMode.Careful)),
            ("full", "Full Processing", carcass.GetRemainingMinutes(ButcheringMode.FullProcessing))
        };

        foreach (var (id, label, minutes) in modes)
        {
            string timeStr = minutes > 0 ? $" ({minutes}min)" : "";
            choices.Add((id, $"{label}{timeStr}"));
        }

        string description = $"Butcher: {carcass.AnimalName}\n" +
            $"Condition: {carcass.GetDecayDescription()}\n" +
            $"Remaining: {carcass.GetTotalRemainingKg():F1}kg";

        if (warnings != null && warnings.Count > 0)
        {
            description += "\n\n" + string.Join("\n", warnings);
        }

        var selection = BlockingDialog.Select(ctx, description, choices, c => c.label);

        return selection.id == "cancel" ? null : selection.id;
    }

    // Work result methods
    public static void ShowWorkResult(GameContext ctx, string activityName, string message, List<string> itemsGained)
    {
        string fullMessage = message;
        if (itemsGained.Count > 0)
        {
            fullMessage += "\n\nGained:\n" + string.Join("\n", itemsGained.Select(i => $"  - {i}"));
        }
        BlockingDialog.ShowMessageAndWait(ctx, activityName, fullMessage);
    }

    // Complex UI loops - blocking versions that handle their own result processing

    /// <summary>
    /// Run the transfer UI in a blocking loop until user closes it.
    /// </summary>
    public static void RunTransferUI(GameContext ctx, Inventory storage, string storageName)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        overlays.OpenTransfer(storage, storageName);
        var playerInv = ctx.Inventory;

        while (overlays.Transfer.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            // Render world in background
            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            // Dim overlay
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            // Render the transfer overlay
            var result = overlays.Transfer.Render(ctx, deltaTime);

            // Process transfer result immediately
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

    /// <summary>
    /// Run the fire UI in a blocking loop until user closes it.
    /// </summary>
    public static void RunFireUI(GameContext ctx, HeatSourceFeature? fire)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        overlays.OpenFire(fire);

        while (overlays.Fire.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            // Render world in background
            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            // Dim overlay
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            // Render the fire overlay
            var result = overlays.Fire.Render(ctx, deltaTime);

            // Process fire result immediately
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

        // Use appropriate overlay method based on action type
        if (result.Action == FireAction.StartFire || result.Action == FireAction.StartFromEmber)
            overlay.SetAttemptResult(actionResult.Success, actionResult.Message);
        else
            overlay.SetTendMessage(actionResult.Message);
    }

    /// <summary>
    /// Run the eating UI in a blocking loop until user closes it.
    /// </summary>
    public static void RunEatingUI(GameContext ctx)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        overlays.OpenEating();

        while (overlays.Eating.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            // Render world in background
            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            // Dim overlay
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            // Render the eating overlay
            var consumedId = overlays.Eating.Render(ctx, deltaTime);

            // Process consumption result immediately
            if (consumedId != null)
            {
                var consumeResult = ConsumptionHandler.Consume(ctx, consumedId);
                overlays.Eating.SetConsumeResult(consumeResult.Message, consumeResult.IsWarning);
            }

            rlImGui.End();
            Raylib.EndDrawing();
        }
    }

    /// <summary>
    /// Run the cooking UI in a blocking loop until user closes it.
    /// </summary>
    public static void RunCookingUI(GameContext ctx)
    {
        var overlays = DesktopRuntime.Overlays;
        if (overlays == null) return;

        overlays.OpenCooking();

        while (overlays.Cooking.IsOpen && !Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            // Render world in background
            DesktopRuntime.WorldRenderer?.Update(ctx, deltaTime);
            DesktopRuntime.WorldRenderer?.Render(ctx);

            // Dim overlay
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
                new Color(0, 0, 0, 128));

            rlImGui.Begin();

            // Render the cooking overlay
            var result = overlays.Cooking.Render(ctx, deltaTime);

            // Process cooking result immediately
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
            CookingAction.MeltSnow => CookingHandler.ProcessMeltSnow(ctx),
            _ => new CookingHandler.CookingResult(false, "Unknown action", 0)
        };

        overlay.SetActionResult(actionResult.Success, actionResult.Message);
    }
}
