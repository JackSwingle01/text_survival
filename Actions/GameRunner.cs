using Raylib_cs;
using rlImGui_cs;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Handlers;
using text_survival.Persistence;
using text_survival.UI;
using text_survival.Environments;
using text_survival.Desktop;
using text_survival.Desktop.Input;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions;

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

    public void Run()
    {
        while (ctx.player.IsAlive && !Raylib.WindowShouldClose())
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

        // Player died - show death message
        if (!ctx.player.IsAlive)
        {
            GameDisplay.AddDanger(ctx, "Your vision fades to black as you collapse...");
            GameDisplay.AddDanger(ctx, "You have died.");
            BlockingDialog.ShowMessageAndWait(ctx, "Death", "You have died.");
        }
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

        // Auto-save periodically
        var (saved, saveError) = SaveManager.Save(ctx);
        if (!saved)
            Console.WriteLine($"[GameRunner] Save failed: {saveError}");

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
            float deltaTime = Raylib.GetFrameTime();

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
                if (input.ToggleFire) return HasActiveFire() ? "tend_fire" : "start_fire";
                if (input.Wait) return "wait";
                if (input.Cancel) tilePopup?.Hide();

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
                var clickedAction = actionPanel.Render(ctx, deltaTime);
                if (clickedAction != null)
                {
                    tilePopup?.Hide();
                    rlImGui.End();
                    Raylib.EndDrawing();
                    return clickedAction;
                }
            }

            // Render overlays
            overlays?.Render(ctx, deltaTime);

            // Render stats panel
            RenderStatsPanel(ctx);

            rlImGui.End();
            Raylib.EndDrawing();

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
    /// Process an action string from the game loop.
    /// </summary>
    private void ProcessAction(string action)
    {
        // Handle work actions
        if (action.StartsWith("work:"))
        {
            string workId = action.Substring(5);
            ExecuteWork(workId);
            return;
        }

        switch (action)
        {
            case "wait":
                Wait();
                break;
            case "tend_fire":
                TendFire();
                break;
            case "start_fire":
                StartFire();
                break;
            case "eat_drink":
                EatDrink();
                break;
            case "cook":
                CookMelt();
                break;
            case "inventory":
                RunInventoryMenu();
                break;
            case "crafting":
                RunCrafting();
                break;
            case "discovery_log":
                RunDiscoveryLog();
                break;
            case "storage":
                RunStorageMenu();
                break;
            case "curing_rack":
                UseCuringRack();
                break;
            case "sleep":
                Sleep();
                break;
            case "make_camp":
                MakeCamp();
                break;
            case "treat_wounds":
                ApplyDirectTreatment();
                break;
            default:
                // Unknown action, ignore
                break;
        }
    }

    private void MainMenu()
    {
        // Auto-save when at main menu
        var (saved, saveError) = SaveManager.Save(ctx);
        if (!saved)
            Console.WriteLine($"[GameRunner] Save failed: {saveError}");
        CheckFireWarning();

        var choice = new Choice<Action>();
        var capacities = ctx.player.GetCapacities();

        // Check for incapacitation - moving at exactly 0%
        if (capacities.Moving <= 0)
        {
            HandleIncapacitation();
            return; // Don't show menu, handler will loop until recovery/death
        }

        var isImpaired = AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness);
        var isClumsy = AbilityCalculator.IsManipulationImpaired(capacities.Manipulation);

        // can always wait
        choice.AddOption("Wait (5 min)", Wait);

        // Work options (field activities) - listed directly in menu
        var workOptions = ctx.CurrentLocation.GetWorkOptions(ctx).ToList();
        foreach (var opt in workOptions)
        {
            string workId = opt.Id; // Capture for lambda
            choice.AddOption(opt.Label, () => ExecuteWork(workId));
        }

        // Grid-based travel option (always visible, but may trigger warning)
        if (ctx.Map != null)
        {
            choice.AddOption("Travel", HandleTravel);
        }

        if (HasActiveFire())
            choice.AddOption("Tend fire", TendFire);

        if (CanStartFire())
            choice.AddOption("Start fire", StartFire);

        if (ctx.Inventory.HasFood || ctx.Inventory.HasWater)
            choice.AddOption("Eat/Drink", EatDrink);

        if (CanUseFireForCooking())
            choice.AddOption("Cook/Melt", CookMelt);

        if (CanLightTorch())
            choice.AddOption("Light torch", LightTorch);
        if (ctx.Inventory.HasLitTorch)
        {
            int mins = (int)ctx.Inventory.TorchBurnTimeRemainingMinutes;
            choice.AddOption($"Extinguish torch ({mins} min remaining)", ExtinguishTorch);
        }

        // Crafting always available - menu shows what's craftable/uncraftable
        string craftLabel = isClumsy ? "Crafting (your hands are unsteady)" : "Crafting";
        choice.AddOption(craftLabel, RunCrafting);

        // Inventory - always available when player has items
        if (HasItems())
            choice.AddOption("Inventory", RunInventoryMenu);

        // Discovery Log - always available
        choice.AddOption("Discovery Log", RunDiscoveryLog);

        // Storage - available at camp when player has items OR storage has items
        var storage = ctx.Camp.GetFeature<CacheFeature>();
        if (ctx.CurrentLocation == ctx.Camp && storage != null && (HasItems() || storage.Storage.CurrentWeightKg > 0))
            choice.AddOption("Storage", RunStorageMenu);

        var rack = ctx.Camp.GetFeature<CuringRackFeature>();
        if (rack != null)
        {
            string rackLabel = rack.HasReadyItems
                ? "Curing rack (items ready!)"
                : rack.ItemCount > 0
                    ? $"Curing rack ({rack.ItemCount} items curing)"
                    : "Curing rack (empty)";
            choice.AddOption(rackLabel, UseCuringRack);
        }

        if (CanApplyDirectTreatment())
            choice.AddOption("Treat wounds", ApplyDirectTreatment);

        if (CanApplyWaterproofing())
            choice.AddOption("Waterproof equipment", ApplyWaterproofing);

        // Sleep requires bedding at current location
        if (ctx.CurrentLocation.HasFeature<BeddingFeature>())
        {
            string sleepLabel = isImpaired ? "Sleep (you need rest)" : "Sleep";
            choice.AddOption(sleepLabel, Sleep);
        }
        else
        {
            // Can make camp at any location without bedding
            choice.AddOption("Make camp", MakeCamp);
        }

        choice.GetPlayerChoice(ctx).Invoke();
    }

    private bool CanCamp() => ctx.CurrentLocation.HasFeature<BeddingFeature>();
    private void MakeCamp() => CampHandler.MakeCamp(ctx, ctx.CurrentLocation);

    /// <summary>
    /// Execute a single work action by ID.
    /// </summary>
    private void ExecuteWork(string workId)
    {
        TravelRunner traveler = new(ctx);
        var work = new WorkRunner(ctx);
        var result = work.ExecuteById(ctx.CurrentLocation, workId);

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
            if (result.FoundAnimal != null)
            {
                var (outcome, huntMinutes) = HuntRunner.Run(
                    result.FoundAnimal, ctx.CurrentLocation, ctx, result.FoundHerd);

                // Time passage during hunt
                if (huntMinutes > 0)
                {
                    ctx.Update(huntMinutes, ActivityType.Hunting);
                }
            }
        }
    }

    private void RunCrafting()
    {
        var craftingRunner = new CraftingRunner(ctx);
        craftingRunner.Run();
    }

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

    private bool CanUseFireForCooking()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        // Can cook/melt with any active fire - don't need fuel in inventory
        return fire != null && fire.IsActive;
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

    private bool CanRestByFire()
    {
        return ctx.CurrentLocation.HasActiveHeatSource();
    }

    private void HandleTravel()
    {
        // Skip capacity check if there's a pending travel target from grid click
        // TravelRunner.DoTravel() will handle validation for grid-initiated travel
        if (!ctx.PendingTravelTarget.HasValue)
        {
            var capacities = ctx.player.GetCapacities();
            double moving = capacities.Moving;

            // Check for impairment and show popup if needed
            if (moving <= 0.5)
            {
                bool proceed = ShowMovementWarning(moving);
                if (!proceed)
                    return; // User cancelled or blocked
            }
        }

        // Proceed with travel
        new TravelRunner(ctx).DoTravel();
    }

    private bool ShowMovementWarning(double movingCapacity)
    {
        string message;
        Dictionary<string, string> buttons;

        if (movingCapacity <= 0.1)
        {
            message = "You can barely move at all. Your injuries prevent travel.";
            buttons = new() { { "ok", "OK" } };
            DesktopIO.PromptConfirm(ctx, message, buttons);
            return false; // Blocked
        }
        else if (movingCapacity <= 0.3)
        {
            int slowdown = (int)(1.0 / movingCapacity);
            message = $"You can barely stand. Travel will be extremely slow and dangerous. (approximately {slowdown}x slower)";
            buttons = new() { { "proceed", "Proceed" }, { "cancel", "Cancel" } };
        }
        else // movingCapacity <= 0.5
        {
            int slowdown = (int)(1.0 / movingCapacity);
            message = $"Moving is difficult. Travel will be noticeably slower. (approximately {slowdown}x slower)";
            buttons = new() { { "proceed", "Proceed" }, { "cancel", "Cancel" } };
        }

        return DesktopIO.PromptConfirm(ctx, message, buttons) == "proceed";
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

    private void RunStorageMenu()
    {
        var storage = ctx.Camp.GetFeature<CacheFeature>()!;
        // Start with storage view instead of player inventory
        Desktop.DesktopIO.RunTransferUI(ctx, storage.Storage, "CAMP STORAGE");
    }

    private void EatDrink()
    {
        DesktopIO.RunEatingUI(ctx);
    }

    private void CookMelt() => CookingHandler.CookMelt(ctx);

    private void UseCuringRack() => CuringRackHandler.UseCuringRack(ctx);

    private bool CanApplyDirectTreatment() => TreatmentHandler.CanApplyTreatment(ctx);

    private void ApplyDirectTreatment() => TreatmentHandler.ApplyTreatment(ctx);

    private bool CanApplyWaterproofing() => MaintenanceHandler.CanApplyWaterproofing(ctx);

    private void ApplyWaterproofing() => MaintenanceHandler.ApplyWaterproofing(ctx);
}