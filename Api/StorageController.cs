using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record ToStorageRequest(string ItemId);
public record ToPlayerRequest(string ItemId);

[ApiController]
[Route("api/game/{sessionId}/storage")]
public class StorageController : GameControllerBase
{
    [HttpPost("to-storage")]
    public ActionResult<GameResponse> ToStorage(string sessionId, [FromBody] ToStorageRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var cache = ctx.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
            return BadRequest(new { error = "No storage at this location" });

        TransferItem(ctx.Inventory, cache.Storage, req.ItemId);

        var transferData = TransferDto.FromInventories(ctx.Inventory, cache.Storage, cache.Name);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new TransferOverlay(transferData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("to-player")]
    public ActionResult<GameResponse> ToPlayer(string sessionId, [FromBody] ToPlayerRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var cache = ctx.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
            return BadRequest(new { error = "No storage at this location" });

        TransferItem(cache.Storage, ctx.Inventory, req.ItemId);

        var transferData = TransferDto.FromInventories(ctx.Inventory, cache.Storage, cache.Name);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new TransferOverlay(transferData) });

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

    private static void TransferItem(Inventory source, Inventory dest, string itemId)
    {
        // Parse item ID format: "prefix_category_index" or "prefix_category_ResourceName"
        var parts = itemId.Split('_');
        if (parts.Length < 2) return;

        var prefix = parts[0]; // "player" or "storage"
        var category = parts[1]; // "resource", "tool", "accessory", "water"

        if (category == "water")
        {
            double amount = Math.Min(source.WaterLiters, 1.0);
            source.WaterLiters -= amount;
            dest.WaterLiters += amount;
        }
        else if (category == "tool" && parts.Length >= 3 && int.TryParse(parts[2], out var toolIdx))
        {
            if (toolIdx < source.Tools.Count)
            {
                var tool = source.Tools[toolIdx];
                source.Tools.RemoveAt(toolIdx);
                dest.Tools.Add(tool);
            }
        }
        else if (category == "accessory" && parts.Length >= 3 && int.TryParse(parts[2], out var accIdx))
        {
            if (accIdx < source.Accessories.Count)
            {
                var acc = source.Accessories[accIdx];
                source.Accessories.RemoveAt(accIdx);
                dest.Accessories.Add(acc);
            }
        }
        else if (category == "resource" && parts.Length >= 3)
        {
            var resourceName = parts[2];
            if (Enum.TryParse<Resource>(resourceName, out var resource))
            {
                if (source.Count(resource) > 0)
                {
                    double weight = source.Pop(resource);
                    dest.Add(resource, weight);
                }
            }
        }
    }
}
