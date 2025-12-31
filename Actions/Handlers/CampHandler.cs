using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;

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

        // Check for shovel - halves camp setup time
        bool hasShovel = ctx.Inventory.GetTool(ToolType.Shovel) != null;
        int setupTimeMinutes = hasShovel ? 22 : 45;

        if (!WebIO.Confirm(ctx, $"Do you want to setup camp here? ({setupTimeMinutes} min)"))
            return;

        if (hasShovel)
        {
            GameDisplay.AddNarrative(ctx, "Your shovel makes quick work of clearing the ground and preparing the site...");
        }
        else
        {
            GameDisplay.AddNarrative(ctx, "You clear the area and gather materials to make a place to rest...");
        }
        GameDisplay.Render(ctx, statusText: "Setting camp.");

        // Time cost: 45 minutes normally, 22 minutes with shovel
        // Note: ActivityType.Crafting has EventMultiplier=0, so no events can interrupt this
        GameDisplay.UpdateAndRenderProgress(ctx, "Setting up camp", setupTimeMinutes, ActivityType.Crafting);

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

    /// <summary>
    /// Deploy a portable tent at the current location.
    /// Removes tent from inventory and adds a ShelterFeature to the location.
    /// </summary>
    public static void DeployTent(GameContext ctx, Gear tent)
    {
        if (!tent.IsTent)
        {
            GameDisplay.AddWarning(ctx, "That's not a tent.");
            return;
        }

        var location = ctx.CurrentLocation;
        if (location.HasFeature<ShelterFeature>())
        {
            GameDisplay.AddWarning(ctx, "There's already a shelter here.");
            return;
        }

        // Remove tent from inventory
        ctx.Inventory.Tools.Remove(tent);

        // Create shelter from tent
        var shelter = ShelterFeature.CreateFromTent(tent);
        location.Features.Add(shelter);

        // Takes a few minutes to set up
        GameDisplay.AddNarrative(ctx, $"You set up your {tent.Name}...");
        GameDisplay.UpdateAndRenderProgress(ctx, $"Setting up {tent.Name}", 10, ActivityType.Crafting);

        GameDisplay.AddSuccess(ctx, $"{tent.Name} is now set up. You have shelter here.");
        GameDisplay.Render(ctx);
    }

    /// <summary>
    /// Pack up a deployed tent and return it to inventory.
    /// </summary>
    public static void PackTent(GameContext ctx)
    {
        var location = ctx.CurrentLocation;
        var shelter = location.GetFeature<ShelterFeature>();

        if (shelter == null || !shelter.IsPortable)
        {
            GameDisplay.AddWarning(ctx, "There's no tent to pack up here.");
            return;
        }

        var tent = shelter.SourceTent!;

        // Remove shelter from location
        location.Features.Remove(shelter);

        // Return tent to inventory
        ctx.Inventory.Tools.Add(tent);

        // Takes a few minutes to pack up
        GameDisplay.AddNarrative(ctx, $"You pack up your {tent.Name}...");
        GameDisplay.UpdateAndRenderProgress(ctx, $"Packing {tent.Name}", 5, ActivityType.Crafting);

        GameDisplay.AddSuccess(ctx, $"{tent.Name} packed and ready to carry.");
        GameDisplay.Render(ctx);
    }

    /// <summary>
    /// Check if a tent can be deployed at the current location.
    /// </summary>
    public static bool CanDeployTent(GameContext ctx)
    {
        // Need a tent and no existing shelter
        return ctx.Inventory.Tools.Any(t => t.IsTent)
            && !ctx.CurrentLocation.HasFeature<ShelterFeature>();
    }

    /// <summary>
    /// Check if a tent can be packed up at the current location.
    /// </summary>
    public static bool CanPackTent(GameContext ctx)
    {
        var shelter = ctx.CurrentLocation.GetFeature<ShelterFeature>();
        return shelter?.IsPortable == true;
    }

    /// <summary>
    /// Get the deployable tent from inventory.
    /// </summary>
    public static Gear? GetDeployableTent(GameContext ctx)
    {
        return ctx.Inventory.Tools.FirstOrDefault(t => t.IsTent);
    }
}
