using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles making camp and field camping actions.
/// </summary>
public static class CampHandler
{
    /// <summary>
    /// Create a makeshift camp at the given location.
    /// Adds bedding feature and takes time.
    /// </summary>
    public static void MakeCamp(GameContext ctx, Location location)
    {
        GameDisplay.AddNarrative(ctx, "You can setup a camp here to make a fire and rest.");

        var confirm = new Choice<bool>("Do you want to setup camp here? (45 min)");
        confirm.AddOption("Yes, establish camp here", true);
        confirm.AddOption("No, not yet", false);

        if (!confirm.GetPlayerChoice(ctx))
            return;

        GameDisplay.AddNarrative(ctx, "You clear the area and gather materials to make a place to rest...");
        GameDisplay.Render(ctx, statusText: "Setting camp.");

        // Time cost: 45 minutes to set up
        GameDisplay.UpdateAndRenderProgress(ctx, "Setting up camp", 45, ActivityType.Crafting);

        if (!ctx.player.IsAlive) return;

        // Create crude bedding at the location
        location.Features.Add(BeddingFeature.CreateMakeshiftBedding());
        // Create camp storage if it doesn't exist
        if (!location.HasFeature<CacheFeature>())
        {
            location.Features.Add(CacheFeature.CreateCampCache());
        }
        // update game context to reflect new camp
        ctx.EstablishCamp(location);

        GameDisplay.AddNarrative(ctx, "You have established a camp here. You can now make a fire and rest.");
        GameDisplay.Render(ctx);
    }
}
