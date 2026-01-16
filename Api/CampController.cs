using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Survival;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record SleepRequest(int DurationMinutes);
public record WaitRequest(int Minutes);

[ApiController]
[Route("api/game/{sessionId}/camp")]
public class CampController : GameControllerBase
{
    [HttpPost("fire")]
    public ActionResult<GameResponse> Fire(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null)
            return BadRequest(new { error = "No fire pit at this location" });

        var fireData = FireManagementDto.FromContext(ctx, fire, null, null);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new FireOverlay(fireData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("inventory")]
    public ActionResult<GameResponse> Inventory(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var invData = InventoryDto.FromInventory(ctx.Inventory);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new InventoryOverlay(invData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("storage")]
    public ActionResult<GameResponse> Storage(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var cache = ctx.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
            return BadRequest(new { error = "No storage at this location" });

        var transferData = TransferDto.FromInventories(ctx.Inventory, cache.Storage, cache.Name);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new TransferOverlay(transferData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("crafting")]
    public ActionResult<GameResponse> Crafting(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var crafting = new NeedCraftingSystem();
        var craftingData = CraftingDto.FromContext(ctx, crafting);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new CraftingOverlay(craftingData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("eating")]
    public ActionResult<GameResponse> Eating(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var eatingData = BuildEatingData(ctx);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new EatingOverlay(eatingData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("sleep")]
    public ActionResult<GameResponse> Sleep(string sessionId, [FromBody] SleepRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        int minutes = Math.Clamp(req.DurationMinutes, 30, 480);
        ctx.Update(minutes, ActivityType.Sleeping);
        GameDisplay.AddNarrative(ctx, $"You sleep for {minutes / 60} hours.");

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("wait")]
    public ActionResult<GameResponse> Wait(string sessionId, [FromBody] WaitRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        int minutes = Math.Clamp(req.Minutes, 1, 120);
        ctx.Update(minutes, ActivityType.Resting);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    private static EatingOverlayDto BuildEatingData(GameContext ctx)
    {
        var foods = new List<ConsumableItemDto>();
        var drinks = new List<ConsumableItemDto>();

        // Add food items
        if (ctx.Inventory.Count(Resource.CookedMeat) > 0)
            foods.Add(new ConsumableItemDto(
                Id: "food_CookedMeat",
                Name: "Cooked Meat",
                Amount: $"{ctx.Inventory.Count(Resource.CookedMeat):F1}kg",
                CaloriesEstimate: 1750,
                HydrationEstimate: null,
                Warning: null
            ));

        if (ctx.Inventory.Count(Resource.RawMeat) > 0)
            foods.Add(new ConsumableItemDto(
                Id: "food_RawMeat",
                Name: "Raw Meat",
                Amount: $"{ctx.Inventory.Count(Resource.RawMeat):F1}kg",
                CaloriesEstimate: 1750,
                HydrationEstimate: null,
                Warning: "Raw - risk of illness"
            ));

        if (ctx.Inventory.Count(Resource.DriedMeat) > 0)
            foods.Add(new ConsumableItemDto(
                Id: "food_DriedMeat",
                Name: "Dried Meat",
                Amount: $"{ctx.Inventory.Count(Resource.DriedMeat):F1}kg",
                CaloriesEstimate: 2500,
                HydrationEstimate: null,
                Warning: null
            ));

        if (ctx.Inventory.Count(Resource.Berries) > 0)
            foods.Add(new ConsumableItemDto(
                Id: "food_Berries",
                Name: "Berries",
                Amount: $"{ctx.Inventory.Count(Resource.Berries):F1}kg",
                CaloriesEstimate: 500,
                HydrationEstimate: null,
                Warning: null
            ));

        if (ctx.Inventory.Count(Resource.Nuts) > 0)
            foods.Add(new ConsumableItemDto(
                Id: "food_Nuts",
                Name: "Nuts",
                Amount: $"{ctx.Inventory.Count(Resource.Nuts):F1}kg",
                CaloriesEstimate: 6000,
                HydrationEstimate: null,
                Warning: null
            ));

        if (ctx.Inventory.Count(Resource.Roots) > 0)
            foods.Add(new ConsumableItemDto(
                Id: "food_Roots",
                Name: "Roots",
                Amount: $"{ctx.Inventory.Count(Resource.Roots):F1}kg",
                CaloriesEstimate: 700,
                HydrationEstimate: null,
                Warning: null
            ));

        // Add drink
        if (ctx.Inventory.WaterLiters > 0)
            drinks.Add(new ConsumableItemDto(
                Id: "water",
                Name: "Water",
                Amount: $"{ctx.Inventory.WaterLiters:F1}L",
                CaloriesEstimate: null,
                HydrationEstimate: 100,
                Warning: null
            ));

        var body = ctx.player.Body;
        int caloriesPct = (int)(body.CalorieStore / Survival.SurvivalProcessor.MAX_CALORIES * 100);
        int hydrationPct = (int)(body.Hydration / Survival.SurvivalProcessor.MAX_HYDRATION * 100);

        return new EatingOverlayDto(
            CaloriesPercent: caloriesPct,
            HydrationPercent: hydrationPct,
            Foods: foods,
            Drinks: drinks,
            SpecialAction: null
        );
    }
}
