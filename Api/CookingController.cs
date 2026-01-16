using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

[ApiController]
[Route("api/game/{sessionId}/cooking")]
public class CookingController : GameControllerBase
{
    [HttpPost("open")]
    public ActionResult<GameResponse> Open(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null || !fire.IsActive)
            return BadRequest(new { error = "No active fire for cooking" });

        var cookingData = BuildCookingData(ctx);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new CookingOverlay(cookingData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("cook-meat")]
    public ActionResult<GameResponse> CookMeat(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var handlerResult = CookingHandler.CookMeat(ctx.Inventory, ctx.CurrentLocation);

        ctx.Update(CookingHandler.CookMeatTimeMinutes, ActivityType.TendingFire);

        var resultDto = new CookingResultDto(
            Message: handlerResult.Message,
            Icon: handlerResult.Success ? "check_circle" : "error",
            IsSuccess: handlerResult.Success
        );

        if (handlerResult.Success)
            GameDisplay.AddSuccess(ctx, handlerResult.Message);
        else
            GameDisplay.AddWarning(ctx, handlerResult.Message);

        var cookingData = BuildCookingData(ctx, resultDto);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new CookingOverlay(cookingData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("melt-snow")]
    public ActionResult<GameResponse> MeltSnow(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var handlerResult = CookingHandler.MeltSnow(ctx.Inventory, ctx.CurrentLocation);

        ctx.Update(CookingHandler.MeltSnowTimeMinutes, ActivityType.TendingFire);

        var resultDto = new CookingResultDto(
            Message: handlerResult.Message,
            Icon: handlerResult.Success ? "water_drop" : "error",
            IsSuccess: handlerResult.Success
        );

        if (handlerResult.Success)
            GameDisplay.AddSuccess(ctx, handlerResult.Message);
        else
            GameDisplay.AddWarning(ctx, handlerResult.Message);

        var cookingData = BuildCookingData(ctx, resultDto);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new CookingOverlay(cookingData) });

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

    private static CookingDto BuildCookingData(GameContext ctx, CookingResultDto? lastResult = null)
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool hasActiveFire = fire?.IsActive == true;

        var options = new List<CookingOptionDto>
        {
            new CookingOptionDto(
                Id: "cook_meat",
                Label: "Cook Meat",
                Icon: "restaurant",
                TimeMinutes: 15,
                IsAvailable: hasActiveFire && ctx.Inventory.Count(Resource.RawMeat) > 0,
                DisabledReason: !hasActiveFire ? "Need active fire" :
                               ctx.Inventory.Count(Resource.RawMeat) <= 0 ? "No raw meat" : null
            ),
            new CookingOptionDto(
                Id: "melt_snow",
                Label: "Melt Snow",
                Icon: "water_drop",
                TimeMinutes: 10,
                IsAvailable: hasActiveFire,
                DisabledReason: !hasActiveFire ? "Need active fire" : null
            )
        };

        return new CookingDto(
            Options: options,
            WaterLiters: ctx.Inventory.WaterLiters,
            RawMeatKg: ctx.Inventory.Count(Resource.RawMeat),
            CookedMeatKg: ctx.Inventory.Count(Resource.CookedMeat),
            LastResult: lastResult
        );
    }
}
