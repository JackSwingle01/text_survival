using text_survival.Actors.Animals;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Handlers;
using text_survival.Persistence;
using text_survival.UI;
using text_survival.Survival;
using System.Net;

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

    public void Run()
    {
        while (ctx.player.IsAlive)
        {
            if (ctx.Expedition is not null)
            {
                // if resuming a save mid expedition - finish it before continuing 
                var expeditionRunner = new ExpeditionRunner(ctx);
                expeditionRunner.Run();
            }
            GameDisplay.Render(ctx, statusText: "Resting.");
            CheckFireWarning();
            RunCampMenu();
        }

        // Player died from survival conditions - show death message
        GameDisplay.AddDanger(ctx, "Your vision fades to black as you collapse...");
        GameDisplay.AddDanger(ctx, "You have died.");
        GameDisplay.Render(ctx, addSeparator: false);
        Input.WaitForKey(ctx);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MAIN MENU
    // ═══════════════════════════════════════════════════════════════════════════

    private void RunCampMenu()
    {
        // Auto-save when at camp menu (discard result - auto-save is best-effort)
        _ = SaveManager.Save(ctx);

        var choice = new Choice<Action>();
        var capacities = ctx.player.GetCapacities();
        var isImpaired = AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness);
        var isLimping = AbilityCalculator.IsMovingImpaired(capacities.Moving);
        var isClumsy = AbilityCalculator.IsManipulationImpaired(capacities.Manipulation);
        var isFoggy = AbilityCalculator.IsPerceptionImpaired(
            AbilityCalculator.CalculatePerception(ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers()));
        var isWinded = AbilityCalculator.IsBreathingImpaired(capacities.Breathing);

        if (CanRestByFire())
            choice.AddOption("Wait", Wait);

        if (HasActiveFire())
            choice.AddOption("Tend fire", TendFire);

        if (CanStartFire())
            choice.AddOption("Start fire", StartFire);

        // Work around camp - foraging, exploring without leaving
        if (HasCampWork())
        {
            string workLabel = "Work near camp";
            if (isFoggy && isWinded)
                workLabel = "Work near camp (foggy, winded)";
            else if (isFoggy)
                workLabel = "Work near camp (your senses are dulled)";
            else if (isWinded)
                workLabel = "Work near camp (you're short of breath)";
            choice.AddOption(workLabel, WorkAroundCamp);
        }

        // Leave camp - travel to other locations (only show if destinations exist)
        if (ctx.Camp.ConnectionNames.Count > 0)
        {
            // Build descriptors for travel-affecting impairments
            var travelImpairments = new List<string>();
            if (isImpaired) travelImpairments.Add("impaired");
            if (isLimping) travelImpairments.Add("limping");
            if (isWinded) travelImpairments.Add("winded");

            string leaveLabel = travelImpairments.Count switch
            {
                0 => "Leave camp",
                1 => travelImpairments[0] switch
                {
                    "impaired" => "Leave camp (you're not thinking clearly)",
                    "limping" => "Leave camp (your movement is limited)",
                    "winded" => "Leave camp (you're short of breath)",
                    _ => "Leave camp"
                },
                _ => $"Leave camp ({string.Join(", ", travelImpairments)})"
            };
            choice.AddOption(leaveLabel, LeaveCamp);
        }

        // Eat/Drink - consume food and water
        if (ctx.Inventory.HasFood || ctx.Inventory.HasWater)
            choice.AddOption("Eat/Drink", EatDrink);

        // Cook/Melt - requires active fire
        if (HasActiveFire() && (ctx.Inventory.Count(Resource.RawMeat) > 0 || true)) // Snow always available (Ice Age)
            choice.AddOption("Cook/Melt", CookMelt);

        // Torch management
        if (CanLightTorch())
            choice.AddOption("Light torch", LightTorch);
        if (ctx.Inventory.HasLitTorch)
        {
            int mins = (int)ctx.Inventory.TorchBurnTimeRemainingMinutes;
            choice.AddOption($"Extinguish torch ({mins} min remaining)", ExtinguishTorch);
        }

        // Crafting - make tools from available materials
        if (ctx.Inventory.HasCraftingMaterials)
        {
            string craftLabel = isClumsy ? "Crafting (your hands are unsteady)" : "Crafting";
            choice.AddOption(craftLabel, RunCrafting);
        }

        var storage = ctx.Camp.GetFeature<CacheFeature>();
        if (storage != null && (HasItems() || storage.Storage.CurrentWeightKg > 0))
            choice.AddOption("Inventory", RunInventoryMenu);

        // Curing rack - if player has one at camp
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

        // Direct treatments - when player has treatable conditions and materials
        if (CanApplyDirectTreatment())
            choice.AddOption("Treat wounds", ApplyDirectTreatment);

        // Sleep emphasized when impaired
        if (isImpaired || ctx.player.Body.IsTired)
        {
            string sleepLabel = isImpaired ? "Sleep (you need rest)" : "Sleep";
            choice.AddOption(sleepLabel, Sleep);
        }

        choice.GetPlayerChoice(ctx).Invoke();
    }

    private bool HasCampWork() =>
        WorkRunner.HasWorkOptions(ctx, ctx.Camp);


    private void WorkAroundCamp()
    {
        var campLocation = ctx.Camp;
        var work = new WorkRunner(ctx);

        var choice = new Choice<string>("What do you want to do?");

        if (campLocation.HasFeature<ForageFeature>())
        {
            var forage = campLocation.GetFeature<ForageFeature>()!;
            choice.AddOption($"Forage nearby ({forage.GetQualityDescription()})", "forage");
        }

        if (ctx.HasUnrevealedLocations())
            choice.AddOption("Scout the area (discover new locations)", "scout");

        choice.AddOption("Cancel", "cancel");

        string action = choice.GetPlayerChoice(ctx);

        switch (action)
        {
            case "forage":
                work.DoForage(campLocation);
                break;
            case "scout":
                work.DoExplore(campLocation);
                break;
        }
    }

    private void RunCrafting()
    {
        var craftingRunner = new CraftingRunner(ctx);
        craftingRunner.Run();
    }

    private void LeaveCamp()
    {
        var expeditionRunner = new ExpeditionRunner(ctx);
        expeditionRunner.Run();
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

    private void CheckFireWarning()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null || (!fire.IsActive && !fire.HasEmbers))
            return;

        // Don't warn when fire is growing
        string phase = fire.GetFirePhase();
        if (phase == "Igniting" || phase == "Building")
            return;

        int minutes = (int)(fire.HoursRemaining * 60);

        if (minutes <= 5)
            GameDisplay.AddDanger(ctx, $"Your fire will die in {minutes} minutes!");
        else if (minutes <= 15)
            GameDisplay.AddWarning(ctx, $"Fire burning low - {minutes} minutes remaining.");
    }

    private bool CanStartFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        bool noFire = fire == null;
        bool coldFire = fire != null && !fire.IsActive && !fire.HasEmbers;

        if (!noFire && !coldFire) return false;

        // Need a fire tool and materials
        bool hasTool = ctx.Inventory.Tools.Any(t =>
            t.Type == ToolType.FireStriker ||
            t.Type == ToolType.HandDrill ||
            t.Type == ToolType.BowDrill);
        return hasTool && ctx.Inventory.CanStartFire;
    }

    private bool CanRestByFire()
    {
        return ctx.CurrentLocation.HasActiveHeatSource();
    }

    private void TendFire() => FireHandler.TendFire(ctx);

    private void StartFire() => FireHandler.StartFire(ctx);

    private bool CanLightTorch() => TorchHandler.CanLightTorch(ctx);

    private void LightTorch() => TorchHandler.LightTorch(ctx);

    private void ExtinguishTorch() => TorchHandler.ExtinguishTorch(ctx);

    private void Sleep()
    {
        int hours = Input.ReadInt(ctx, "How many hours would you like to sleep?", 1, 12);

        int totalMinutes = hours * 60;
        int slept = 0;

        while (slept < totalMinutes && ctx.player.IsAlive)
        {
            // Sleep in 60-minute chunks, checking for events
            int chunkMinutes = Math.Min(60, totalMinutes - slept);
            ctx.player.Body.Rest(chunkMinutes);

            int minutes = ctx.Update(chunkMinutes, ActivityType.Sleeping, render: true);
            slept += minutes;
        }

        if (slept > 0)
            GameDisplay.AddNarrative(ctx, $"You slept for {slept / 60} hours.");
    }

    private void Wait()
    {
        GameDisplay.UpdateAndRenderProgress(ctx, "Resting", 5, ActivityType.Resting);
    }

    private bool HasItems()
    {
        var inv = ctx.Inventory;
        return inv.HasFuel || inv.HasFood || inv.HasWater || inv.Tools.Count > 0;
    }

    private void RunInventoryMenu()
    {
        bool atCamp = ctx.CurrentLocation == ctx.Camp;

        if (!atCamp)
        {
            // Not at camp - just show read-only inventory view
            GameDisplay.RenderInventoryScreen(ctx);
            Input.WaitForKey("Press any key to return...");
            if (ctx.SessionId != null)
                Web.WebIO.ClearInventory(ctx);
            return;
        }

        // At camp - use shared transfer helper
        InventoryTransferHelper.RunTransferMenu(ctx, ctx.Camp.GetFeature<CacheFeature>()!.Storage, "CAMP STORAGE");
    }

    private void EatDrink() => ConsumptionHandler.EatDrink(ctx);

    private void CookMelt() => CookingHandler.CookMelt(ctx);

    private void UseCuringRack() => CuringRackHandler.UseCuringRack(ctx);

    private void StartCombat(Animal enemy) => CombatHandler.StartCombat(ctx, enemy);

    private bool CanApplyDirectTreatment() => TreatmentHandler.CanApplyDirectTreatment(ctx);

    private void ApplyDirectTreatment() => TreatmentHandler.ApplyDirectTreatment(ctx);
}