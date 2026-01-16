using Raylib_cs;
using rlImGuiCs;
using ImGuiNET;
using text_survival.Persistence;
using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Handlers;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.Input;
using text_survival.Desktop;
using text_survival.Desktop.UI;
using text_survival.Environments.Features;
using text_survival.Items;

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

        // Create world renderer, input handler, and overlay manager
        var worldRenderer = new WorldRenderer();
        var inputHandler = new InputHandler(worldRenderer);
        var actionPanel = new ActionPanel();
        var overlays = new OverlayManager();

        // Initialize desktop runtime for blocking I/O
        DesktopRuntime.Initialize(worldRenderer, overlays, actionPanel);

        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            // Process input before rendering
            if (ctx != null && loadError == null)
            {
                // Handle overlay keyboard shortcuts first
                bool iPressed = Raylib.IsKeyPressed(KeyboardKey.I);
                bool cPressed = Raylib.IsKeyPressed(KeyboardKey.C);
                bool escPressed = Raylib.IsKeyPressed(KeyboardKey.Escape);
                overlays.HandleKeyboardShortcuts(iPressed, cPressed, escPressed);

                var inputResult = inputHandler.ProcessInput(ctx);

                // Handle travel if initiated
                if (inputResult.TravelInitiated && ctx.PendingTravelTarget.HasValue)
                {
                    // Travel will be processed - for now just show message
                    // Full integration requires async handling
                    var target = ctx.PendingTravelTarget.Value;
                    var destination = ctx.Map?.GetLocationAt(target.X, target.Y);
                    if (destination != null)
                    {
                        actionPanel.ShowMessage($"Traveling to {destination.Name}...");
                        // Simple travel: just move (full travel runner has hazards, events, etc.)
                        ctx.Map?.MoveTo(destination, ctx.player);
                        ctx.PendingTravelTarget = null;
                    }
                }

                // Handle input messages
                if (inputResult.Message != null)
                {
                    actionPanel.ShowMessage(inputResult.Message);
                }

                // Handle wait action
                if (inputResult.Wait)
                {
                    ctx.Update(5, ActivityType.Resting);
                    actionPanel.ShowMessage("Rested for 5 minutes.");
                }
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 25, 30, 255));

            // Render world if game loaded
            if (ctx != null && loadError == null)
            {
                worldRenderer.Update(ctx, deltaTime);
                worldRenderer.Render(ctx);
            }

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

            // Action panel (shows available actions)
            if (ctx != null)
            {
                var clickedAction = actionPanel.Render(ctx, deltaTime);
                if (clickedAction != null)
                {
                    HandleAction(ctx, clickedAction, actionPanel, overlays);
                }

                // Render overlays and process results
                var overlayResults = overlays.Render(ctx, deltaTime);
                ProcessOverlayResults(ctx, overlayResults, actionPanel, overlays);
            }

            // Status panel
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 470), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 200), ImGuiCond.FirstUseEver);
            ImGui.Begin("Status");
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1f, 1), "Desktop Migration - Phase 6");
            ImGui.Separator();
            ImGui.TextWrapped("Blocking I/O enabled! Select, Confirm, and progress dialogs now work.");
            ImGui.Separator();

            // Hover info
            var hoveredTile = worldRenderer.GetHoveredTile();
            if (hoveredTile.HasValue && ctx != null)
            {
                var (hx, hy) = hoveredTile.Value;
                var currentPos = ctx.Map.CurrentPosition;
                bool isAdjacent = Math.Abs(hx - currentPos.X) <= 1 && Math.Abs(hy - currentPos.Y) <= 1
                    && (hx != currentPos.X || hy != currentPos.Y);

                ImGui.Text($"Tile: ({hx}, {hy})");
                if (ctx.Map.IsValidPosition(hx, hy))
                {
                    var loc = ctx.Map.GetLocationAt(hx, hy);
                    if (loc != null)
                    {
                        ImGui.Text($"  {loc.Name}");
                        if (isAdjacent && ctx.Map.CanMoveTo(hx, hy))
                            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1f, 0.5f, 1), "  Click to travel");
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("Hover over a tile for info");
            }

            ImGui.Separator();
            ImGui.TextDisabled("Keys: WASD=move, Space=wait, I=inventory, C=craft");
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

    /// <summary>
    /// Handle an action from the action panel.
    /// </summary>
    private static void HandleAction(GameContext ctx, string actionId, ActionPanel panel, OverlayManager overlays)
    {
        switch (actionId)
        {
            case "wait":
                ctx.Update(5, ActivityType.Resting);
                panel.ShowMessage("Rested for 5 minutes.");
                break;

            case "inventory":
                overlays.ToggleInventory();
                break;

            case "crafting":
                overlays.ToggleCrafting();
                break;

            case "discovery_log":
                panel.ShowMessage("Discovery log not yet implemented.");
                break;

            case "storage":
                var campStorage = ctx.CampStorage;
                if (campStorage != null)
                {
                    overlays.OpenTransfer(campStorage, "Camp Storage");
                }
                else
                {
                    panel.ShowMessage("No storage available at this location.");
                }
                break;

            case "tend_fire":
            case "start_fire":
                var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
                overlays.OpenFire(fire);
                break;

            case "eat_drink":
                overlays.OpenEating();
                break;

            case "cook":
                overlays.OpenCooking();
                break;

            case "sleep":
                panel.ShowMessage("Sleep not yet implemented.");
                break;

            case "make_camp":
                panel.ShowMessage("Make camp not yet implemented.");
                break;

            case "treat_wounds":
                panel.ShowMessage("Treatment not yet implemented.");
                break;

            case "curing_rack":
                panel.ShowMessage("Curing rack not yet implemented.");
                break;

            default:
                if (actionId.StartsWith("work:"))
                {
                    var workId = actionId[5..];
                    panel.ShowMessage($"Work '{workId}' not yet implemented.");
                }
                else
                {
                    panel.ShowMessage($"Unknown action: {actionId}");
                }
                break;
        }
    }

    /// <summary>
    /// Process results from overlay interactions.
    /// </summary>
    private static void ProcessOverlayResults(GameContext ctx, OverlayResults results, ActionPanel panel, OverlayManager overlays)
    {
        if (results.CraftedItem != null)
        {
            panel.ShowMessage($"Crafted: {results.CraftedItem}");
        }

        if (results.EventChoice != null)
        {
            panel.ShowMessage($"Choice made: {results.EventChoice}");
        }

        // Fire overlay results
        if (results.FireResult != null)
        {
            ProcessFireResult(ctx, results.FireResult, panel, overlays);
        }

        // Eating overlay results
        if (results.ConsumedItemId != null)
        {
            var consumeResult = ConsumptionHandler.Consume(ctx, results.ConsumedItemId);
            overlays.Eating.SetConsumeResult(consumeResult.Message, consumeResult.IsWarning);
            panel.ShowMessage(consumeResult.Message);
        }

        // Cooking overlay results
        if (results.CookingResult != null)
        {
            ProcessCookingResult(ctx, results.CookingResult, panel, overlays);
        }

        // Transfer overlay results
        if (results.TransferResult != null)
        {
            ProcessTransferResult(ctx, results.TransferResult, panel, overlays);
        }
    }

    /// <summary>
    /// Process fire overlay action results.
    /// </summary>
    private static void ProcessFireResult(GameContext ctx, FireOverlayResult result, ActionPanel panel, OverlayManager overlays)
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        switch (result.Action)
        {
            case FireAction.StartFire:
                if (result.Tool != null && result.Tinder != null)
                {
                    int skillLevel = ctx.player.Skills.GetSkill("Firecraft").Level;
                    var startResult = FireHandler.AttemptStartFire(
                        ctx.player, ctx.Inventory, ctx.CurrentLocation,
                        result.Tool, result.Tinder.Value, skillLevel, fire);

                    ctx.Update(10, ActivityType.TendingFire);
                    overlays.Fire.SetAttemptResult(startResult.Success, startResult.Message);
                    panel.ShowMessage(startResult.Message);

                    if (startResult.Success)
                    {
                        ctx.player.Skills.GetSkill("Firecraft").GainExperience(3);
                    }
                    else
                    {
                        ctx.player.Skills.GetSkill("Firecraft").GainExperience(1);
                    }
                }
                break;

            case FireAction.StartFromEmber:
                if (result.EmberCarrier != null)
                {
                    FireHandler.StartFromEmber(ctx.player, ctx.Inventory, ctx.CurrentLocation, result.EmberCarrier, fire);
                    ctx.Update(5, ActivityType.TendingFire);
                    overlays.Fire.SetAttemptResult(true, "Fire started from ember!");
                    panel.ShowMessage("Fire started from ember!");
                }
                break;

            case FireAction.AddFuel:
                if (result.FuelResource != null && fire != null)
                {
                    FireHandler.AddFuel(ctx.Inventory, fire, result.FuelResource.Value);
                    overlays.Fire.SetTendMessage($"Added {result.FuelResource.Value} to fire.");
                    panel.ShowMessage($"Added fuel to fire.");
                }
                break;

            case FireAction.CollectCharcoal:
                if (fire != null && fire.HasCharcoal)
                {
                    double collected = fire.CollectCharcoal();
                    ctx.Inventory.Add(Resource.Charcoal, collected);
                    overlays.Fire.SetTendMessage($"Collected {collected:F2}kg charcoal.");
                    panel.ShowMessage($"Collected {collected:F2}kg charcoal.");
                }
                break;

            case FireAction.LightTorch:
                var torch = ctx.Inventory.Tools.FirstOrDefault(t => t.ToolType == Items.ToolType.Torch && !t.IsTorchLit);
                if (torch != null && fire != null && fire.IsActive)
                {
                    torch.LightTorch();
                    overlays.Fire.SetTendMessage("Torch lit!");
                    panel.ShowMessage("Torch lit!");
                }
                break;

            case FireAction.CollectEmber:
                var carrier = ctx.Inventory.Tools.FirstOrDefault(t => t.IsEmberCarrier && !t.IsEmberLit);
                if (carrier != null && fire != null && fire.HasEmbers)
                {
                    carrier.LightEmber();
                    overlays.Fire.SetTendMessage($"Ember collected in {carrier.Name}!");
                    panel.ShowMessage($"Ember collected in {carrier.Name}!");
                }
                break;
        }
    }

    /// <summary>
    /// Process cooking overlay action results.
    /// </summary>
    private static void ProcessCookingResult(GameContext ctx, CookingOverlayResult result, ActionPanel panel, OverlayManager overlays)
    {
        switch (result.Action)
        {
            case CookingAction.CookMeat:
                var cookResult = CookingHandler.CookMeat(ctx.Inventory, ctx.CurrentLocation);
                if (cookResult.Success)
                {
                    ctx.Update(CookingHandler.CookMeatTimeMinutes, ActivityType.TendingFire);
                }
                overlays.Cooking.SetActionResult(cookResult.Success, cookResult.Message);
                panel.ShowMessage(cookResult.Message);
                break;

            case CookingAction.MeltSnow:
                var meltResult = CookingHandler.MeltSnow(ctx.Inventory, ctx.CurrentLocation);
                if (meltResult.Success)
                {
                    ctx.Update(CookingHandler.MeltSnowTimeMinutes, ActivityType.TendingFire);
                }
                overlays.Cooking.SetActionResult(meltResult.Success, meltResult.Message);
                panel.ShowMessage(meltResult.Message);
                break;
        }
    }

    /// <summary>
    /// Process transfer overlay action results.
    /// </summary>
    private static void ProcessTransferResult(GameContext ctx, TransferResult result, ActionPanel panel, OverlayManager overlays)
    {
        var playerInv = ctx.Inventory;
        var storage = ctx.CampStorage;
        if (storage == null) return;

        var source = result.FromPlayer ? playerInv : storage;
        var dest = result.FromPlayer ? storage : playerInv;
        string direction = result.FromPlayer ? "to storage" : "to inventory";

        if (result.Resource != null)
        {
            if (source.Count(result.Resource.Value) > 0)
            {
                double weight = source.Pop(result.Resource.Value);
                dest.Add(result.Resource.Value, weight);
                overlays.Transfer.SetMessage($"Moved {result.Resource.Value} {direction}");
            }
        }
        else if (result.IsWater)
        {
            double amount = Math.Min(result.WaterAmount, source.WaterLiters);
            source.WaterLiters -= amount;
            dest.WaterLiters += amount;
            overlays.Transfer.SetMessage($"Transferred {amount:F1}L water {direction}");
        }
        else if (result.Tool != null)
        {
            if (source.Tools.Remove(result.Tool))
            {
                dest.Tools.Add(result.Tool);
                overlays.Transfer.SetMessage($"Moved {result.Tool.Name} {direction}");
            }
        }
        else if (result.Equipment != null)
        {
            if (source.Equipment.Remove(result.Equipment))
            {
                dest.Equipment.Add(result.Equipment);
                overlays.Transfer.SetMessage($"Moved {result.Equipment.Name} {direction}");
            }
        }
        else if (result.Accessory != null)
        {
            if (source.Accessories.Remove(result.Accessory))
            {
                dest.Accessories.Add(result.Accessory);
                overlays.Transfer.SetMessage($"Moved {result.Accessory.Name} {direction}");
            }
        }
    }
}
