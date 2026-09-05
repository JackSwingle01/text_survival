using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Tests.Support;
using text_survival.UI;

namespace text_survival.Tests.Actions;

/// <summary>
/// The fire screen and the location have to agree on which fire they are talking about.
/// A fire the handler holds but the location does not own is invisible to the player:
/// the screen reports it lit and then keeps offering to light it.
/// </summary>
public class FireHandlerTests
{
    [Fact]
    public async Task ManageFire_StartingFromEmber_PutsTheFireOnTheLocation()
    {
        var ctx = GameContext.CreateNewGame();
        var ui = new ScriptedUi { AutoResolveEvents = true };
        ctx.Ui = ui;

        var location = ctx.CurrentLocation;
        foreach (var existing in location.Features.OfType<HeatSourceFeature>().ToList())
            location.RemoveFeature(existing);

        var carrier = LitEmberCarrier();
        ctx.Inventory.Tools.Add(carrier);
        ctx.Inventory.Add(Resource.Stick, 0.5);

        ui.FireRequests.Enqueue(new FireOverlayResult
        {
            Action = FireAction.StartFromEmber,
            EmberCarrier = carrier
        });

        await FireHandler.ManageFire(ctx);

        var fire = location.GetFeature<HeatSourceFeature>();
        Assert.NotNull(fire);
        Assert.True(fire.IsActive, "The fire the player lit should be burning on the location.");
    }

    [Fact]
    public async Task ManageFire_AddingFuel_ReachesTheLocationFire()
    {
        var ctx = GameContext.CreateNewGame();
        var ui = new ScriptedUi { AutoResolveEvents = true };
        ctx.Ui = ui;

        var location = ctx.CurrentLocation;
        foreach (var existing in location.Features.OfType<HeatSourceFeature>().ToList())
            location.RemoveFeature(existing);

        var carrier = LitEmberCarrier();
        ctx.Inventory.Tools.Add(carrier);
        ctx.Inventory.Add(Resource.Stick, 0.5);
        ctx.Inventory.Add(Resource.Birch, 2.0);

        ui.FireRequests.Enqueue(new FireOverlayResult
        {
            Action = FireAction.StartFromEmber,
            EmberCarrier = carrier
        });
        ui.FireRequests.Enqueue(new FireOverlayResult
        {
            Action = FireAction.AddFuel,
            FuelResource = Resource.Birch
        });

        await FireHandler.ManageFire(ctx);

        var fire = location.GetFeature<HeatSourceFeature>();
        Assert.NotNull(fire);
        Assert.True(fire.TotalMassKg > 0.5, "The log should have gone onto the fire the player is looking at.");
    }

    private static Gear LitEmberCarrier() => new()
    {
        Name = "Amadou Ember Carrier",
        Category = GearCategory.Tool,
        ToolType = ToolType.EmberCarrier,
        Weight = 0.1,
        Durability = 1,
        MaxDurability = 1,
        EmberBurnHoursMax = 8.0,
        EmberBurnHoursRemaining = 8.0
    };
}
