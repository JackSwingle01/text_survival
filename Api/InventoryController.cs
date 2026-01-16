using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record EquipToolRequest(string ToolId);

[ApiController]
[Route("api/game/{sessionId}/inventory")]
public class InventoryController : GameControllerBase
{
    [HttpPost("close")]
    public ActionResult<GameResponse> Close(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("equip")]
    public ActionResult<GameResponse> Equip(string sessionId, [FromBody] EquipToolRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (!req.ToolId.StartsWith("tool_") && !req.ToolId.StartsWith("equip_"))
            return BadRequest(new { error = "Invalid tool ID" });

        var prefix = req.ToolId.StartsWith("tool_") ? "tool_" : "equip_";
        if (!int.TryParse(req.ToolId[prefix.Length..], out var toolIndex))
            return BadRequest(new { error = "Invalid tool index" });

        if (toolIndex >= ctx.Inventory.Tools.Count)
            return BadRequest(new { error = "Tool not found" });

        var tool = ctx.Inventory.Tools[toolIndex];
        if (!tool.IsWeapon)
            return BadRequest(new { error = "Tool cannot be used as weapon" });

        ctx.Inventory.Weapon = tool;
        GameDisplay.AddNarrative(ctx, $"You ready your {tool.Name}.");

        var invData = InventoryDto.FromInventory(ctx.Inventory);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new InventoryOverlay(invData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("unequip")]
    public ActionResult<GameResponse> Unequip(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.Inventory.Weapon != null)
        {
            GameDisplay.AddNarrative(ctx, $"You put away your {ctx.Inventory.Weapon.Name}.");
            ctx.Inventory.Weapon = null;
        }

        var invData = InventoryDto.FromInventory(ctx.Inventory);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new InventoryOverlay(invData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }
}
