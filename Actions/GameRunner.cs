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
        while (ctx.player.IsAlive && !ctx.HasWon)
        {
            GameDisplay.Render(ctx, statusText: "Resting.");
            CheckFireWarning();

            // Handle pending travel from map click
            if (ctx.PendingTravelTarget.HasValue)
            {
                new TravelRunner(ctx).DoTravel();
                continue;
            }

            MainMenu();
        }

        if (ctx.HasWon)
        {
            // Victory was already displayed in ExpeditionRunner
            return;
        }

        // Player died from survival conditions - show death message
        GameDisplay.AddDanger(ctx, "Your vision fades to black as you collapse...");
        GameDisplay.AddDanger(ctx, "You have died.");
        GameDisplay.Render(ctx, addSeparator: false);
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
        var isImpaired = AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness);
        var isClumsy = AbilityCalculator.IsManipulationImpaired(capacities.Manipulation);

        // Work options (field activities) - listed directly in menu
        var workOptions = ctx.CurrentLocation.GetWorkOptions(ctx).ToList();
        foreach (var opt in workOptions)
        {
            string workId = opt.Id; // Capture for lambda
            choice.AddOption(opt.Label, () => ExecuteWork(workId));
        }

        // Grid-based travel option (allows WebIO.Select to handle travel_to responses)
        if (ctx.Map != null)
        {
            choice.AddOption("Travel", () => new TravelRunner(ctx).DoTravel());
        }

        // Camp activities (combined from CampWork menu)
        if (CanRestByFire())
            choice.AddOption("Wait", Wait);

        if (HasActiveFire())
            choice.AddOption("Tend fire", TendFire);

        if (CanStartFire())
            choice.AddOption("Start fire", StartFire);

        if (ctx.Inventory.HasFood || ctx.Inventory.HasWater)
            choice.AddOption("Eat/Drink", EatDrink);

        if (HasActiveFire() && (ctx.Inventory.Count(Resource.RawMeat) > 0 || true))
            choice.AddOption("Cook/Melt", CookMelt);

        if (CanLightTorch())
            choice.AddOption("Light torch", LightTorch);
        if (ctx.Inventory.HasLitTorch)
        {
            int mins = (int)ctx.Inventory.TorchBurnTimeRemainingMinutes;
            choice.AddOption($"Extinguish torch ({mins} min remaining)", ExtinguishTorch);
        }

        if (ctx.Inventory.HasCraftingMaterials)
        {
            string craftLabel = isClumsy ? "Crafting (your hands are unsteady)" : "Crafting";
            choice.AddOption(craftLabel, RunCrafting);
        }

        if (ctx.Inventory.HasBuildingMaterials && ctx.CurrentLocation == ctx.Camp)
            choice.AddOption("Improve Camp", ImproveCamp);

        // Inventory - always available when player has items
        if (HasItems())
            choice.AddOption("Inventory", RunInventoryMenu);

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
    private void CampWork()
    {
        ctx.EstablishCamp(ctx.CurrentLocation);

        while (true)
        {
            // Auto-save when at camp menu
            var (saved, saveError) = SaveManager.Save(ctx);
            if (!saved)
                Console.WriteLine($"[GameRunner] Save failed: {saveError}");
            CheckFireWarning();

            var choice = new Choice<Action>();
            var capacities = ctx.player.GetCapacities();
            var isImpaired = AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness);
            var isClumsy = AbilityCalculator.IsManipulationImpaired(capacities.Manipulation);

            if (CanRestByFire())
                choice.AddOption("Wait", Wait);

            if (HasActiveFire())
                choice.AddOption("Tend fire", TendFire);

            if (CanStartFire())
                choice.AddOption("Start fire", StartFire);

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

            // Improve Camp - build infrastructure (fire pits, shelters, bedding)
            if (ctx.Inventory.HasBuildingMaterials)
            {
                choice.AddOption("Improve Camp", ImproveCamp);
            }

            // Inventory - always available when player has items
            if (HasItems())
                choice.AddOption("Inventory", RunInventoryMenu);

            // Storage - available when player has items OR storage has items
            var storage = ctx.Camp.GetFeature<CacheFeature>();
            if (storage != null && (HasItems() || storage.Storage.CurrentWeightKg > 0))
                choice.AddOption("Storage", RunStorageMenu);

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

            // Waterproofing - when player has resin and treatable equipment
            if (CanApplyWaterproofing())
                choice.AddOption("Waterproof equipment", ApplyWaterproofing);

            // Sleep requires bedding at location
            if (ctx.Camp.HasFeature<BeddingFeature>())
            {
                string sleepLabel = isImpaired ? "Sleep (you need rest)" : "Sleep";
                choice.AddOption(sleepLabel, Sleep);
            }

            choice.AddOption("Done", BackAction);

            var action = choice.GetPlayerChoice(ctx);
            if (action == BackAction)
                break;

            action.Invoke();
        }
    }


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
                    result.FoundAnimal, ctx.CurrentLocation, ctx);

                // Time passage during hunt
                if (huntMinutes > 0)
                {
                    ctx.Update(huntMinutes, ActivityType.Hunting);
                }
            }
        }
    }





    // private void WorkAroundCamp()
    // {
    //     var campLocation = ctx.Camp;
    //     var work = new WorkRunner(ctx);

    //     var choice = new Choice<string>("What do you want to do?");

    //     if (campLocation.HasFeature<ForageFeature>())
    //     {
    //         var forage = campLocation.GetFeature<ForageFeature>()!;
    //         choice.AddOption($"Forage nearby ({forage.GetQualityDescription()})", "forage");
    //     }

    //     if (ctx.HasUnrevealedLocations())
    //         choice.AddOption("Scout the area (discover new locations)", "scout");

    //     choice.AddOption("Cancel", "cancel");

    //     string action = choice.GetPlayerChoice(ctx);

    //     switch (action)
    //     {
    //         case "forage":
    //             work.DoForage(campLocation);
    //             break;
    //         case "scout":
    //             var result = work.DoExplore(campLocation);
    //             if (result.DiscoveredLocation != null &&
    //                 WorkRunner.PromptTravelToDiscovery(ctx, result.DiscoveredLocation))
    //             {
    //                 var expedition = new Expedition(ctx.Camp, ctx.player);
    //                 ctx.Expedition = expedition;
    //                 var runner = new ExpeditionRunner(ctx);
    //                 runner.TravelToLocation(expedition, result.DiscoveredLocation);
    //                 runner.Run();
    //             }
    //             break;
    //     }
    // }

    private void RunCrafting()
    {
        var craftingRunner = new CraftingRunner(ctx);
        craftingRunner.Run();
    }

    private void ImproveCamp()
    {
        var improveCampRunner = new ImproveCampRunner(ctx);
        improveCampRunner.Run();
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

    private void TendFire() => FireHandler.TendFire(ctx);

    private void StartFire() => FireHandler.StartFire(ctx);

    private bool CanLightTorch() => TorchHandler.CanLightTorch(ctx);

    private void LightTorch() => TorchHandler.LightTorch(ctx);

    private void ExtinguishTorch() => TorchHandler.ExtinguishTorch(ctx);

    private void Sleep()
    {
        // Check fire status before allowing sleep
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool hasFire = fire != null && (fire.IsActive || fire.HasEmbers);
        int fireMinutes = hasFire && fire != null ? (int)(fire.TotalHoursRemaining * 60) : 0;

        int hours = Input.ReadInt(ctx, "How many hours would you like to sleep?", 1, 12);
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

    private bool HasItems()
    {
        var inv = ctx.Inventory;
        return inv.HasFuel || inv.HasFood || inv.HasWater || inv.Tools.Count > 0;
    }

    private void RunInventoryMenu()
    {
        // For web UI, show inventory overlay and wait for close
        if (ctx.SessionId != null)
        {
            Web.WebIO.ShowInventoryAndWait(ctx, ctx.Inventory, "INVENTORY");
            return;
        }

        // Console fallback - just show read-only inventory view
        GameDisplay.RenderInventoryScreen(ctx);
    }

    private void RunStorageMenu()
    {
        var storage = ctx.Camp.GetFeature<CacheFeature>()!;
        // Start with storage view instead of player inventory
        InventoryTransferHelper.RunTransferMenu(ctx, storage.Storage, "CAMP STORAGE", viewStorageFirst: true);
    }

    private void EatDrink() => ConsumptionHandler.EatDrink(ctx);

    private void CookMelt() => CookingHandler.CookMelt(ctx);

    private void UseCuringRack() => CuringRackHandler.UseCuringRack(ctx);

    private bool CanApplyDirectTreatment() => TreatmentHandler.CanApplyDirectTreatment(ctx);

    private void ApplyDirectTreatment() => TreatmentHandler.ApplyDirectTreatment(ctx);

    private bool CanApplyWaterproofing() => MaintenanceHandler.CanApplyWaterproofing(ctx);

    private void ApplyWaterproofing() => MaintenanceHandler.ApplyWaterproofing(ctx);
}