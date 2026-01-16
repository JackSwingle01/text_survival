using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Items;
using text_survival.Survival;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record EatFoodRequest(string ItemId);

[ApiController]
[Route("api/game/{sessionId}/eating")]
public class EatingController : GameControllerBase
{
    [HttpPost("food")]
    public ActionResult<GameResponse> EatFood(string sessionId, [FromBody] EatFoodRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var result = ConsumptionHandler.Consume(ctx, req.ItemId);

        if (result.IsWarning)
            GameDisplay.AddWarning(ctx, result.Message);
        else
            GameDisplay.AddNarrative(ctx, result.Message);

        var eatingData = BuildEatingData(ctx);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new EatingOverlay(eatingData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("water")]
    public ActionResult<GameResponse> DrinkWater(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var result = ConsumptionHandler.Consume(ctx, "water");

        if (result.IsWarning)
            GameDisplay.AddWarning(ctx, result.Message);
        else
            GameDisplay.AddNarrative(ctx, result.Message);

        var eatingData = BuildEatingData(ctx);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new EatingOverlay(eatingData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("close")]
    public ActionResult<GameResponse> Close(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    private static EatingOverlayDto BuildEatingData(GameContext ctx)
    {
        var body = ctx.player.Body;
        int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
        int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);

        var foods = new List<ConsumableItemDto>();
        var drinks = new List<ConsumableItemDto>();

        // Add food items
        foreach (var foodResource in new[] { Resource.CookedMeat, Resource.RawMeat, Resource.DriedMeat, Resource.Berries, Resource.Nuts, Resource.Roots })
        {
            int count = ctx.Inventory.Count(foodResource);
            if (count <= 0) continue;

            foods.Add(new ConsumableItemDto(
                Id: $"food_{foodResource}",
                Name: foodResource.ToDisplayName(),
                Amount: count.ToString(),
                CaloriesEstimate: GetFoodCalories(foodResource),
                HydrationEstimate: null,
                Warning: foodResource == Resource.RawMeat ? "Raw - risk of illness" : null
            ));
        }

        // Add water
        if (ctx.Inventory.WaterLiters >= 0.1)
        {
            drinks.Add(new ConsumableItemDto(
                Id: "water",
                Name: "Water",
                Amount: $"{ctx.Inventory.WaterLiters:F1}L",
                CaloriesEstimate: null,
                HydrationEstimate: 50, // Per 0.5L drink
                Warning: null
            ));
        }

        return new EatingOverlayDto(
            CaloriesPercent: caloriesPercent,
            HydrationPercent: hydrationPercent,
            Foods: foods,
            Drinks: drinks,
            SpecialAction: null
        );
    }

    private static int GetFoodCalories(Resource food) => food switch
    {
        Resource.CookedMeat => 250,
        Resource.RawMeat => 200,
        Resource.DriedMeat => 300,
        Resource.Berries => 50,
        Resource.Nuts => 150,
        Resource.Roots => 80,
        _ => 100
    };
}
