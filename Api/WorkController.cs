using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Environments.Features;
using text_survival.UI;

namespace text_survival.Api;

public record ForageRequest(string? FocusId, string TimeId);
public record HarvestRequest(string ResourceId);

[ApiController]
[Route("api/game/{sessionId}/work")]
public class WorkController : GameControllerBase
{
    [HttpPost("forage")]
    public ActionResult<GameResponse> Forage(string sessionId, [FromBody] ForageRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var workRunner = new WorkRunner(ctx);
        var result = workRunner.DoForage(ctx.CurrentLocation);

        if (result.PlayerDied)
        {
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("hunt")]
    public ActionResult<GameResponse> Hunt(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var workRunner = new WorkRunner(ctx);
        var searchResult = workRunner.ExecuteById(ctx.CurrentLocation, "hunt");

        if (searchResult.PlayerDied)
        {
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        // If animal found, run interactive hunt
        if (searchResult.FoundAnimal != null)
        {
            var (outcome, minutesElapsed) = HuntRunner.Run(
                searchResult.FoundAnimal,
                ctx.CurrentLocation,
                ctx,
                searchResult.FoundHerd
            );

            if (outcome == HuntOutcome.PlayerDied)
            {
                SaveGameContext(sessionId, ctx);
                return Ok(GameResponse.Success(ctx));
            }

            if (outcome == HuntOutcome.Success)
            {
                // Animal killed and carcass created - show message
                GameDisplay.AddSuccess(ctx, $"Hunt successful! The {searchResult.FoundAnimal.Name} carcass awaits butchering.");
            }
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("harvest")]
    public ActionResult<GameResponse> Harvest(string sessionId, [FromBody] HarvestRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var workRunner = new WorkRunner(ctx);
        var result = workRunner.DoHarvest(ctx.CurrentLocation);

        if (result.PlayerDied)
        {
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("chop")]
    public ActionResult<GameResponse> Chop(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var feature = ctx.CurrentLocation.GetFeature<WoodedAreaFeature>();
        if (feature == null)
            return BadRequest(new { error = "No trees to chop here" });

        var workRunner = new WorkRunner(ctx);
        var result = workRunner.ExecuteById(ctx.CurrentLocation, "chop");

        if (result.PlayerDied)
        {
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("snares/check")]
    public ActionResult<GameResponse> CheckSnares(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var snareLine = ctx.CurrentLocation.GetFeature<SnareLineFeature>();
        if (snareLine?.CanBeChecked != true)
            return BadRequest(new { error = "No snares set here" });

        var workRunner = new WorkRunner(ctx);
        var result = workRunner.DoCheckTraps(ctx.CurrentLocation);

        if (result.PlayerDied)
        {
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("snares/set")]
    public ActionResult<GameResponse> SetSnare(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        if (territory == null)
            return BadRequest(new { error = "No game trails here for snares" });

        var workRunner = new WorkRunner(ctx);
        var result = workRunner.DoSetTrap(ctx.CurrentLocation);

        if (result.PlayerDied)
        {
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("butcher")]
    public ActionResult<GameResponse> Butcher(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        if (carcass == null)
            return BadRequest(new { error = "No carcass to butcher here" });

        var workRunner = new WorkRunner(ctx);
        var result = workRunner.ExecuteById(ctx.CurrentLocation, "butcher");

        if (result.PlayerDied)
        {
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }
}
