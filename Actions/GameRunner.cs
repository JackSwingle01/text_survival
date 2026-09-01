using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actions.Handlers;
using text_survival.Persistence;
using text_survival.UI;
using text_survival.Environments;
using text_survival.Desktop;
using text_survival.Desktop.Audio;
using text_survival.Desktop.Input;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions;

public enum CampAction
{
    Wait,
    TendFire,
    StartFire,
    Food,
    Inventory,
    Crafting,
    DiscoveryLog,
    NPCs,
    Storage,
    CuringRack,
    Sleep,
    MakeCamp,
    PitchTent,
    PackTent,
    TreatWounds
}

public class Choice<T>(string? prompt = null)
{
    public string? Prompt = prompt;
    private readonly Dictionary<string, T> options = [];
    public void AddOption(string label, T item)
    {
        options[label] = item;
    }
    public T GetPlayerChoice(GameContext ctx)
    {
        if (options.Count == 0)
        {
            throw new InvalidOperationException("No Choices Available");
        }
        string choice = Input.Select(ctx, Prompt ?? "Choose:", options.Keys);
        return options[choice];
    }
}

public partial class GameRunner(GameContext ctx)
{
    private readonly GameContext ctx = ctx;
    private static readonly Action BackAction = () => { };

    /// <summary>Serialising the world costs a visible hitch, so it happens on a clock, not per action.</summary>
    private const double SaveIntervalSeconds = 120;
    private DateTime _lastSaveUtc = DateTime.UtcNow;

    /// <summary>Save if enough real time has passed since the last one.</summary>
    private void SaveIfDue()
    {
        if ((DateTime.UtcNow - _lastSaveUtc).TotalSeconds < SaveIntervalSeconds)
            return;

        _lastSaveUtc = DateTime.UtcNow;
        var (saved, saveError) = SaveManager.Save(ctx);
        if (!saved)
            Console.WriteLine($"[GameRunner] Save failed: {saveError}");
    }

    public bool Run()
    {
        AudioManager.PlayMusic();

        // The run ends when the player dies or reaches the far side of the pass. Both are
        // derived from state - there is no separate "game over" flag to keep in sync.
        while (ctx.player.IsAlive && !ctx.CurrentLocation.IsCrossingExit && !Raylib.WindowShouldClose())
        {
            // Handle pending travel from map click or WASD
            if (ctx.PendingTravelTarget.HasValue)
            {
                DesktopRuntime.TilePopup?.Hide();
                new TravelRunner(ctx).DoTravel();
                continue;
            }

            // Handle pending encounter from event or activity
            if (ctx.HasPendingEncounter)
            {
                DesktopRuntime.TilePopup?.Hide();
                ctx.HandlePendingEncounter();
                continue;
            }

            // Run the main game loop with input processing
            string? action = RunGameLoop();

            // Process the action
            if (action != null)
            {
                ProcessAction(action);
            }
        }

        if (ctx.player.IsAlive && ctx.CurrentLocation.IsCrossingExit)
            return HandleVictory();

        // Player died - show death message and offer restart
        if (!ctx.player.IsAlive)
        {
            // Render one final frame to show the fatal state
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));
            DesktopRuntime.WorldRenderer?.Render(ctx);
            rlImGui.Begin();
            RenderStatsPanel(ctx);
            rlImGui.End();
            Raylib.EndDrawing();
            GameDisplay.AddDanger(ctx, "Your vision fades to black as you collapse...");
            GameDisplay.AddDanger(ctx, "You have died.");

            // Ask player what to do next - show full stats so player can see what killed them
            string choice = BlockingDialog.PromptConfirm(ctx,
                "You have died. What would you like to do?",
                new Dictionary<string, string>
                {
                    { "new_game", "Start New Game" },
                    { "quit", "Quit to Desktop" }
                },
                showFullStats: true);

            // Delete save so next launch starts fresh
            SaveManager.DeleteSave(ctx.SessionId);

            // Return true if player wants to restart
            return choice == "new_game";
        }

        // Normal exit (window closed while alive)
        return false;
    }

    /// <summary>
    /// The player crossed the pass. Ends the run the same way death does: a closing
    /// screen, the save deleted, and the choice to start again.
    /// </summary>
    private bool HandleVictory()
    {
        GameDisplay.ClearNarrative(ctx);
        GameDisplay.AddSuccess(ctx, "You made it.");
        GameDisplay.AddNarrative(ctx, "The pass is behind you now.");
        GameDisplay.AddNarrative(ctx, "Below, the far valley stretches green and sheltered.");
        GameDisplay.AddNarrative(ctx, "Smoke rises from distant fires. Your tribe is there.");
        GameDisplay.AddNarrative(ctx, "You survived.");

        string summary =
            $"You crossed the pass.\n\n" +
            $"Days survived: {ctx.DaysSurvived}\n" +
            $"Season: {ctx.Weather.GetSeasonLabel()}";

        string choice = BlockingDialog.PromptConfirm(ctx, summary,
            new Dictionary<string, string>
            {
                { "new_game", "Start New Game" },
                { "quit", "Quit to Desktop" }
            },
            showFullStats: true);

        // The run is over either way - the save must not resume past the ending.
        SaveManager.DeleteSave(ctx.SessionId);

        return choice == "new_game";
    }

    /// <summary>
    /// Main game loop: render world, process input, show UI.
    /// Returns an action string when the player takes an action.
    /// </summary>
    private string? RunGameLoop()
    {
        var inputHandler = DesktopRuntime.InputHandler;
        var worldRenderer = DesktopRuntime.WorldRenderer;
        var tilePopup = DesktopRuntime.TilePopup;
        var actionPanel = DesktopRuntime.ActionPanel;
        var overlays = DesktopRuntime.Overlays;

        SaveIfDue();

        CheckFireWarning();

        // Check for incapacitation
        var capacities = ctx.player.GetCapacities();
        if (capacities.Moving <= 0)
        {
            HandleIncapacitation();
            return null;
        }

        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = DesktopRuntime.BeginFrame();

            // Process active travel (non-blocking travel simulation)
            if (ctx.ActiveTravel != null)
            {
                bool travelComplete = ProcessTravelTick(ctx, deltaTime);
                if (travelComplete)
                {
                    // Travel complete - call completion handler
                    var traveler = new TravelRunner(ctx);
                    bool survived = traveler.CompleteTravel();
                    if (!survived)
                    {
                        return null; // Player died during travel completion
                    }
                    continue; // Skip rest of frame, next iteration will have no active travel
                }

                // During active travel, skip normal input processing
                // Just render the world with progress bar
                RenderTravelFrame(ctx, deltaTime);
                continue;
            }

            // Process input
            if (inputHandler != null)
            {
                var input = inputHandler.ProcessInput(ctx);

                // Handle tile popup
                if (input.ShowTilePopup && worldRenderer != null && tilePopup != null)
                {
                    var screenPos = worldRenderer.GetTileScreenPosition(input.PopupTileX, input.PopupTileY);
                    tilePopup.Show(ctx, input.PopupTileX, input.PopupTileY, screenPos);
                }

                // Handle WASD instant travel
                if (input.TravelInitiated)
                {
                    tilePopup?.Hide();
                    return null; // PendingTravelTarget is set, loop will handle it
                }

                // Handle keyboard shortcuts
                if (input.OpenInventory) return "inventory";
                if (input.OpenCrafting) return "crafting";
                if (input.OpenDiscoveryLog) return "discovery_log";
                if (input.OpenNPCs) return "npcs";
                if (input.OpenStorage) return "storage";
                if (input.ToggleFire) return HasActiveFire() ? "tend_fire" : "start_fire";
                if (input.Wait) return "wait";
                if (input.Cancel) tilePopup?.Hide();

                // Handle forage hotkey
                if (input.StartForaging)
                {
                    var forageFeature = ctx.CurrentLocation.GetFeature<ForageFeature>();
                    if (forageFeature != null)
                    {
                        tilePopup?.Hide();
                        ExecuteWork(new ForageStrategy());
                        return null;
                    }
                }

                // Show message if any
                if (input.Message != null)
                {
                    actionPanel?.ShowMessage(input.Message);
                }
            }

            // Begin frame
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            // Render world
            worldRenderer?.Update(ctx, deltaTime);
            worldRenderer?.Render(ctx);

            rlImGui.Begin();

            // Render tile popup if open
            if (tilePopup != null && tilePopup.IsOpen)
            {
                var popupResult = tilePopup.Render(ctx, deltaTime);
                if (popupResult == "go" && tilePopup.SelectedTile.HasValue)
                {
                    // Set travel target and return
                    var (x, y) = tilePopup.SelectedTile.Value;
                    ctx.PendingTravelTarget = (x, y);
                    tilePopup.Hide();
                    rlImGui.End();
                    Raylib.EndDrawing();
                    return null; // Travel will be handled in main loop
                }
            }

            // Render action panel
            if (actionPanel != null)
            {
                var (campAction, workStrategy, _) = actionPanel.Render(ctx, deltaTime);
                if (campAction != null)
                {
                    tilePopup?.Hide();
                    rlImGui.End();
                    Raylib.EndDrawing();
                    ProcessCampAction(campAction.Value);
                    return null; // Action processed, continue loop
                }
                if (workStrategy != null)
                {
                    tilePopup?.Hide();
                    rlImGui.End();
                    Raylib.EndDrawing();
                    ExecuteWork(workStrategy);
                    return null; // Action processed, continue loop
                }
                // Note: Combat actions don't go through GameRunner - they use DesktopIO directly
            }

            // Render overlays
            overlays?.Render(ctx, deltaTime);

            // Render stats panel
            RenderStatsPanel(ctx);

            // Render toast notifications
            Desktop.UI.ToastManager.Render(deltaTime);

            rlImGui.End();
            Raylib.EndDrawing();

            // Process pending crafts outside ImGui frame (allows blocking animation)
            if (overlays?.Crafting.PendingCraft != null)
            {
                overlays.Crafting.ProcessPendingCraft(ctx);
                continue; // Start fresh frame after crafting
            }

            // Check if travel target was set (from popup)
            if (ctx.PendingTravelTarget.HasValue)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Render the stats panel.
    /// </summary>
    private static void RenderStatsPanel(GameContext ctx)
    {
        Desktop.UI.StatsPanel.Render(ctx);
    }

    /// <summary>
    /// Process one tick of travel simulation.
    /// Returns true when travel is complete.
    /// </summary>
    private static bool ProcessTravelTick(GameContext ctx, float deltaTime)
    {
        var travel = ctx.ActiveTravel!;

        // Update animation progress using stored duration
        float animDuration = travel.AnimationDurationSeconds;
        travel.AnimationProgress += deltaTime / animDuration;
        travel.AnimationProgress = Math.Min(travel.AnimationProgress, 1f);

        // Calculate how many minutes to simulate this frame
        // Spread total minutes across the animation duration
        float minutesPerSecond = travel.TotalMinutes / animDuration;
        int minutesToSimulate = (int)(minutesPerSecond * deltaTime);
        minutesToSimulate = Math.Max(1, minutesToSimulate); // At least 1 minute
        minutesToSimulate = Math.Min(minutesToSimulate, travel.TotalMinutes - travel.SimulatedMinutes);

        if (minutesToSimulate > 0)
        {
            // Simulate 1 minute at a time to allow events to interrupt
            for (int i = 0; i < minutesToSimulate; i++)
            {
                ctx.Update(1, ActivityType.Traveling);
                travel.SimulatedMinutes++;

                // Check for events or death
                if (ctx.EventOccurredLastUpdate)
                {
                    travel.EventInterrupted = true;
                    break;
                }
                if (!ctx.player.IsAlive)
                {
                    break;
                }
            }
        }

        // Travel is complete when animation is done AND simulation is done (or player died/event interrupted)
        bool animDone = travel.AnimationProgress >= 1f;
        bool simDone = travel.SimulatedMinutes >= travel.TotalMinutes;
        bool interrupted = travel.EventInterrupted || !ctx.player.IsAlive;

        return animDone && (simDone || interrupted);
    }



    /// <summary>
    /// Render a frame during active travel with progress bar and stats.
    /// </summary>
    private void RenderTravelFrame(GameContext ctx, float deltaTime)
    {
        var travel = ctx.ActiveTravel!;
        var worldRenderer = DesktopRuntime.WorldRenderer;

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20, 25, 30, 255));

        // Render world (player icon will use override position)
        worldRenderer?.Update(ctx, deltaTime);
        worldRenderer?.Render(ctx);

        rlImGui.Begin();

        // Render stats panel - this stays visible during travel!
        RenderStatsPanel(ctx);

        // Render travel progress bar at bottom
        RenderTravelProgressBar(travel);

        // Render toast notifications
        Desktop.UI.ToastManager.Render(deltaTime);

        rlImGui.End();
        Raylib.EndDrawing();
    }

    /// <summary>
    /// Render the travel progress bar at the bottom of the screen.
    /// </summary>
    private static void RenderTravelProgressBar(GameContext.ActiveTravelState travel)
    {
        float progress = (float)travel.SimulatedMinutes / travel.TotalMinutes;
        progress = Math.Min(progress, 1f);

        var io = ImGui.GetIO();
        float barWidth = io.DisplaySize.X - 40;
        float barHeight = 35;
        float barY = io.DisplaySize.Y - barHeight - 20;

        ImGui.SetNextWindowPos(new Vector2(20, barY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(barWidth, barHeight + 10), ImGuiCond.Always);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings;

        if (ImGui.Begin("##travel_progress", flags))
        {
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.3f, 0.55f, 0.7f, 1f));
            ImGui.ProgressBar(progress, new Vector2(-1, barHeight), $"Traveling to {travel.Destination.Name}...");
            ImGui.PopStyleColor();
        }
        ImGui.End();
    }

    /// <summary>
    /// Process an action string from the game loop.
    /// Handles keyboard shortcut strings - work and camp actions use typed methods.
    /// </summary>
    private void ProcessAction(string action)
    {
        // Keyboard shortcut string handling
        switch (action)
        {
            case "wait":
                ProcessCampAction(CampAction.Wait);
                break;
            case "tend_fire":
                ProcessCampAction(CampAction.TendFire);
                break;
            case "start_fire":
                ProcessCampAction(CampAction.StartFire);
                break;
            case "inventory":
                ProcessCampAction(CampAction.Inventory);
                break;
            case "crafting":
                ProcessCampAction(CampAction.Crafting);
                break;
            case "discovery_log":
                ProcessCampAction(CampAction.DiscoveryLog);
                break;
            case "npcs":
                ProcessCampAction(CampAction.NPCs);
                break;
            case "storage":
                ProcessCampAction(CampAction.Storage);
                break;
            default:
                // Unknown action, ignore
                break;
        }
    }

    /// <summary>
    /// Process a typed camp action.
    /// </summary>
    private void ProcessCampAction(CampAction action)
    {
        switch (action)
        {
            case CampAction.Wait:
                Wait();
                break;
            case CampAction.TendFire:
                TendFire();
                break;
            case CampAction.StartFire:
                StartFire();
                break;
            case CampAction.Food:
                RunFood();
                break;
            case CampAction.Inventory:
                RunInventoryMenu();
                break;
            case CampAction.Crafting:
                RunCrafting();
                break;
            case CampAction.DiscoveryLog:
                RunDiscoveryLog();
                break;
            case CampAction.NPCs:
                RunNPCs();
                break;
            case CampAction.Storage:
                RunStorageMenu();
                break;
            case CampAction.CuringRack:
                UseCuringRack();
                break;
            case CampAction.Sleep:
                Sleep();
                break;
            case CampAction.MakeCamp:
                MakeCamp();
                break;
            case CampAction.PitchTent:
                CampHandler.DeployTent(ctx, CampHandler.GetDeployableTent(ctx)!);
                break;
            case CampAction.PackTent:
                CampHandler.PackTent(ctx);
                break;
            case CampAction.TreatWounds:
                ApplyDirectTreatment();
                break;
        }
    }

private void MakeCamp() => CampHandler.MakeCamp(ctx, ctx.CurrentLocation);

    /// <summary>
    /// Execute a work strategy directly.
    /// </summary>
    private void ExecuteWork(IWorkStrategy strategy)
    {
        TravelRunner traveler = new(ctx);
        var work = new WorkRunner(ctx);
        var result = work.Execute(ctx.CurrentLocation, strategy);

        if (result != null)
        {
            // Handle discovered locations
            if (result.DiscoveredLocation != null)
            {
                GameDisplay.AddNarrative(ctx, $"Discovered: {result.DiscoveredLocation.Name}");
                if (WorkRunner.PromptTravelToDiscovery(ctx, result.DiscoveredLocation))
                {
                    traveler.TravelToLocation(result.DiscoveredLocation);
                }
            }

            // Handle found animal from hunt search - run interactive hunt
            // Time is tracked during combat via GameContext.Update()
            if (result.FoundAnimal != null)
            {
                HuntRunner.Run(result.FoundAnimal, ctx);
            }
        }
    }

    private void RunCrafting() => DesktopIO.RunCraftingAndWait(ctx);

    // ═══════════════════════════════════════════════════════════════════════════
    // FIRE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    private bool HasActiveFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null) return false;

        // Show "Tend fire" if there's an active fire AND we have fuel to add
        return (fire.IsActive || fire.HasEmbers) && ctx.Inventory.HasFuel;
    }

    private void CheckFireWarning()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null || (!fire.IsActive && !fire.HasEmbers))
            return;

        // Don't warn when fire is growing
        string phase = fire.GetFirePhase();
        if (phase == "Igniting" || phase == "Building")
            return;

        int minutes = (int)(fire.BurningHoursRemaining * 60);

        if (minutes <= 5)
            GameDisplay.AddDanger(ctx, $"Your fire will die in {minutes} minutes!");
        else if (minutes <= 15)
            GameDisplay.AddWarning(ctx, $"Fire burning low - {Utils.FormatFireTime(minutes)} remaining.");
    }

    private bool CanStartFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        bool noFire = fire == null;
        bool coldFire = fire != null && !fire.IsActive && !fire.HasEmbers;

        if (!noFire && !coldFire) return false;

        // Need a fire tool and materials
        bool hasTool = ctx.Inventory.Tools.Any(t =>
            t.ToolType == ToolType.FireStriker ||
            t.ToolType == ToolType.HandDrill ||
            t.ToolType == ToolType.BowDrill);
        return hasTool && ctx.Inventory.CanStartFire;
    }

    private void TendFire() => FireHandler.ManageFire(ctx);

    private void StartFire() => FireHandler.ManageFire(ctx);

    private bool CanLightTorch() => TorchHandler.CanLightTorch(ctx);

    private void LightTorch() => TorchHandler.LightTorch(ctx);

    private void ExtinguishTorch() => TorchHandler.ExtinguishTorch(ctx);

    private void Sleep()
    {
        // Check fire status before allowing sleep
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool hasFire = fire != null && (fire.IsActive || fire.HasEmbers);
        int fireMinutes = hasFire && fire != null ? (int)(fire.TotalHoursRemaining * 60) : 0;

        int hours = Input.ReadInt(ctx, "How many hours would you like to sleep?", 1, 8, allowCancel: true);
        if (hours < 0)
        {
            GameDisplay.AddNarrative(ctx, "You decide to stay awake.");
            return;
        }
        int sleepMinutes = hours * 60;

        // Warning if fire won't last
        if (hasFire && fireMinutes < sleepMinutes)
        {
            int shortfall = (sleepMinutes - fireMinutes) / 60;
            string warning = $"Your fire will die {shortfall} hour{(shortfall != 1 ? "s" : "")} before you wake. You'll freeze without it.\n\nSleep anyway?";

            if (!Input.Confirm(ctx, warning))
            {
                GameDisplay.AddNarrative(ctx, "You decide to stay awake.");
                return;
            }
        }
        else if (!hasFire)
        {
            string warning = "There's no fire. You'll freeze to death in your sleep.\n\nSleep without fire?";

            if (!Input.Confirm(ctx, warning))
            {
                GameDisplay.AddNarrative(ctx, "You decide to stay awake.");
                return;
            }
        }

        int totalMinutes = sleepMinutes;
        int slept = 0;

        while (slept < totalMinutes && ctx.player.IsAlive)
        {
            // Sleep in 60-minute chunks, checking for events
            int chunkMinutes = Math.Min(60, totalMinutes - slept);
            ctx.player.Body.Rest(chunkMinutes, ctx.CurrentLocation, ctx.player.EffectRegistry);

            int minutes = ctx.Update(chunkMinutes, ActivityType.Sleeping, render: true);
            slept += minutes;
        }

        if (slept > 0)
            GameDisplay.AddNarrative(ctx, $"You slept for {slept / 60} hours.");
    }

    private void Wait()
    {
        // ActivityType.Resting has EventMultiplier=0, so no events can interrupt
        GameDisplay.UpdateAndRenderProgress(ctx, "Resting", 5, ActivityType.Resting);
    }

    private void HandleIncapacitation()
    {
        GameDisplay.AddNarrative(ctx, "You cannot move. All you can do now is wait.");

        const int chunkMinutes = 5;  // Update in 5-minute chunks like Wait
        const double recoveryThreshold = 0.01;  // >1% moving to recover

        while (ctx.player.IsAlive)
        {
            var capacities = ctx.player.GetCapacities();

            // Check for recovery
            if (capacities.Moving > recoveryThreshold)
            {
                GameDisplay.AddNarrative(ctx, "You can move again.");
                return;
            }

            // Process time chunk with event interruption
            var (elapsed, interrupted) = GameDisplay.UpdateAndRenderProgress(
                ctx, "Incapacitated", chunkMinutes, ActivityType.Incapacitated);

            // If event interrupted, it may have changed state - check again next loop
            // If player died, IsAlive will be false and loop exits
        }

        // If loop exits and player is dead, GameRunner.Run() will handle death
    }

    private bool HasItems()
    {
        var inv = ctx.Inventory;
        return inv.HasFuel || inv.HasFood || inv.HasWater || inv.Tools.Count > 0;
    }

    private void RunInventoryMenu()
    {
        Desktop.DesktopIO.ShowInventoryAndWait(ctx, ctx.Inventory, "INVENTORY");
    }

    private void RunDiscoveryLog()
    {
        Desktop.DesktopIO.ShowDiscoveryLogAndWait(ctx);
    }

    private void RunNPCs()
    {
        Desktop.DesktopIO.ShowNPCsAndWait(ctx);
    }

    private void RunStorageMenu()
    {
        var storage = ctx.Camp.GetFeature<CacheFeature>()!;
        // Start with storage view instead of player inventory
        Desktop.DesktopIO.RunTransferUI(ctx, storage.Storage, "CAMP STORAGE");
    }

    private void RunFood()
    {
        DesktopIO.RunFoodUI(ctx);
    }

    private void UseCuringRack() => CuringRackHandler.UseCuringRack(ctx);

    private void ApplyDirectTreatment() => TreatmentHandler.ApplyTreatment(ctx);

    private bool CanApplyWaterproofing() => MaintenanceHandler.CanApplyWaterproofing(ctx);

    private void ApplyWaterproofing() => MaintenanceHandler.ApplyWaterproofing(ctx);
}