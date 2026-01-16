using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Crafting;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record CategoryRequest(string CategoryId);
public record CraftRequest(string RecipeId);

[ApiController]
[Route("api/game/{sessionId}/crafting")]
public class CraftingController : GameControllerBase
{
    private static readonly Dictionary<string, NeedCategory?> _selectedCategories = new();

    [HttpPost("open")]
    public ActionResult<GameResponse> Open(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        _selectedCategories[sessionId] = null;

        var crafting = new NeedCraftingSystem();
        var craftingData = CraftingDto.FromContext(ctx, crafting, null);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new CraftingOverlay(craftingData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("category")]
    public ActionResult<GameResponse> SelectCategory(string sessionId, [FromBody] CategoryRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (Enum.TryParse<NeedCategory>(req.CategoryId, out var category))
            _selectedCategories[sessionId] = category;
        else
            _selectedCategories[sessionId] = null;

        var crafting = new NeedCraftingSystem();
        var craftingData = CraftingDto.FromContext(ctx, crafting, _selectedCategories[sessionId]);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new CraftingOverlay(craftingData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("craft")]
    public ActionResult<GameResponse> Craft(string sessionId, [FromBody] CraftRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var crafting = new NeedCraftingSystem();
        var recipe = crafting.AllOptions.FirstOrDefault(o => o.Name == req.RecipeId);

        if (recipe == null)
            return BadRequest(new { error = "Recipe not found" });

        if (!recipe.CanCraft(ctx.Inventory))
            return BadRequest(new { error = "Cannot craft: missing materials or tools" });

        var craftedGear = recipe.Craft(ctx.Inventory);

        ctx.Update(recipe.CraftingTimeMinutes, ActivityType.Crafting);

        if (craftedGear != null)
        {
            ctx.Inventory.Tools.Add(craftedGear);
            GameDisplay.AddSuccess(ctx, $"You craft a {craftedGear.Name}.");
            ctx.RecordItemCrafted(craftedGear.Name);
        }
        else if (recipe.ProducesFeature && recipe.FeatureFactory != null)
        {
            var feature = recipe.FeatureFactory();
            ctx.CurrentLocation.AddFeature(feature);
            GameDisplay.AddSuccess(ctx, $"You build a {recipe.Name}.");
        }
        else if (recipe.ProducesMaterials)
        {
            GameDisplay.AddSuccess(ctx, $"You process {recipe.Name}.");
        }
        else if (recipe.IsMendingRecipe)
        {
            GameDisplay.AddSuccess(ctx, $"You mend your equipment.");
        }

        _selectedCategories.TryGetValue(sessionId, out var selectedCategory);
        var craftingData = CraftingDto.FromContext(ctx, crafting, selectedCategory);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new CraftingOverlay(craftingData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("close")]
    public ActionResult<GameResponse> Close(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        _selectedCategories.Remove(sessionId);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }
}
