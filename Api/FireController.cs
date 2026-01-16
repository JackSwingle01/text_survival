using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record SelectToolRequest(string ToolId);
public record SelectTinderRequest(string TinderId);
public record AddFuelRequest(string FuelId, int? Count);
public record LightCarrierRequest(string CarrierId);

[ApiController]
[Route("api/game/{sessionId}/fire")]
public class FireController : GameControllerBase
{
    [HttpPost("select-tool")]
    public ActionResult<GameResponse> SelectTool(string sessionId, [FromBody] SelectToolRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null)
            return BadRequest(new { error = "No fire pit at this location" });

        var fireData = FireManagementDto.FromContext(ctx, fire, req.ToolId, null);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new FireOverlay(fireData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("select-tinder")]
    public ActionResult<GameResponse> SelectTinder(string sessionId, [FromBody] SelectTinderRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null)
            return BadRequest(new { error = "No fire pit at this location" });

        var fireData = FireManagementDto.FromContext(ctx, fire, null, req.TinderId);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new FireOverlay(fireData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("start")]
    public ActionResult<GameResponse> Start(string sessionId, [FromBody] FireStartRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null)
            return BadRequest(new { error = "No fire pit" });

        var fireTools = ctx.Inventory.Tools
            .Where(t => t.ToolType == ToolType.HandDrill ||
                       t.ToolType == ToolType.BowDrill ||
                       t.ToolType == ToolType.FireStriker)
            .ToList();

        int toolIndex = 0;
        if (req.ToolId?.StartsWith("tool_") == true)
            int.TryParse(req.ToolId[5..], out toolIndex);

        if (toolIndex >= fireTools.Count)
            return BadRequest(new { error = "No fire-starting tool selected" });

        var tool = fireTools[toolIndex];

        Resource tinderResource = Resource.Tinder;
        if (req.TinderId?.StartsWith("tinder_") == true)
        {
            var resourceName = req.TinderId[7..];
            if (Enum.TryParse<Resource>(resourceName, out var res))
                tinderResource = res;
        }

        if (ctx.Inventory.Count(tinderResource) <= 0)
            return BadRequest(new { error = "No tinder available" });

        int skillLevel = ctx.player.Skills.GetSkill("Firecraft").Level;
        var result = FireHandler.AttemptStartFire(
            ctx.player,
            ctx.Inventory,
            ctx.CurrentLocation,
            tool,
            tinderResource,
            skillLevel,
            fire
        );

        ctx.Update(10, ActivityType.TendingFire);

        if (result.Success)
        {
            GameDisplay.AddSuccess(ctx, result.Message);
            ctx.player.Skills.GetSkill("Firecraft").GainExperience(3);
        }
        else
        {
            GameDisplay.AddWarning(ctx, result.Message);
            ctx.player.Skills.GetSkill("Firecraft").GainExperience(1);
        }

        var fireData = FireManagementDto.FromContext(ctx, fire, req.ToolId, req.TinderId);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new FireOverlay(fireData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("add-fuel")]
    public ActionResult<GameResponse> AddFuel(string sessionId, [FromBody] AddFuelRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null)
            return BadRequest(new { error = "No fire pit" });

        if (!req.FuelId.StartsWith("fuel_"))
            return BadRequest(new { error = "Invalid fuel ID" });

        var resourceName = req.FuelId[5..];
        if (!Enum.TryParse<Resource>(resourceName, out var resource))
            return BadRequest(new { error = "Unknown resource type" });

        if (ctx.Inventory.Count(resource) <= 0)
            return BadRequest(new { error = "No fuel available" });

        var fuelType = GetFuelTypeFromResource(resource);
        if (fuelType == null)
            return BadRequest(new { error = "Not a valid fuel type" });

        for (int i = 0; i < (req.Count ?? 1); i++)
        {
            if (ctx.Inventory.Count(resource) <= 0) break;
            if (!fire.CanAddFuel(fuelType.Value)) break;

            double weight = ctx.Inventory.Pop(resource);
            fire.AddFuel(weight, fuelType.Value);
        }

        var fireData = FireManagementDto.FromContext(ctx, fire, null, null);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new FireOverlay(fireData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("light-carrier")]
    public ActionResult<GameResponse> LightCarrier(string sessionId, [FromBody] LightCarrierRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null || !fire.IsActive)
            return BadRequest(new { error = "No active fire" });

        if (!req.CarrierId.StartsWith("ember_"))
            return BadRequest(new { error = "Invalid carrier ID" });

        if (!int.TryParse(req.CarrierId[6..], out var carrierIndex))
            return BadRequest(new { error = "Invalid carrier index" });

        var carriers = ctx.Inventory.Tools
            .Where(t => t.ToolType == ToolType.EmberCarrier)
            .ToList();

        if (carrierIndex >= carriers.Count)
            return BadRequest(new { error = "Carrier not found" });

        var carrier = carriers[carrierIndex];
        carrier.EmberBurnHoursRemaining = carrier.EmberBurnHoursMax;
        ctx.Update(2, ActivityType.TendingFire);
        GameDisplay.AddSuccess(ctx, $"You light the {carrier.Name} from the fire.");

        var fireData = FireManagementDto.FromContext(ctx, fire, null, null);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new FireOverlay(fireData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("collect-charcoal")]
    public ActionResult<GameResponse> CollectCharcoal(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null)
            return BadRequest(new { error = "No fire pit" });

        double charcoal = fire.CollectCharcoal();
        if (charcoal <= 0)
            return BadRequest(new { error = "No charcoal to collect" });

        ctx.Inventory.Add(Resource.Charcoal, charcoal);
        GameDisplay.AddSuccess(ctx, $"You collect {charcoal:F2}kg of charcoal.");

        var fireData = FireManagementDto.FromContext(ctx, fire, null, null);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new FireOverlay(fireData) });

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

    private static FuelType? GetFuelTypeFromResource(Resource resource) => resource switch
    {
        Resource.Stick => FuelType.Kindling,
        Resource.Tinder => FuelType.Tinder,
        Resource.Pine => FuelType.PineWood,
        Resource.Birch => FuelType.BirchWood,
        Resource.Oak => FuelType.OakWood,
        Resource.BirchBark => FuelType.BirchBark,
        Resource.Bone => FuelType.Bone,
        Resource.Charcoal => FuelType.Kindling,
        Resource.Chaga => FuelType.Chaga,
        Resource.Amadou => FuelType.Amadou,
        Resource.Usnea => FuelType.Usnea,
        _ => null
    };
}

public record FireStartRequest(string? ToolId, string? TinderId);
