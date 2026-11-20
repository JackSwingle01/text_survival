using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Tests.Support;
using text_survival.Tests.TestHelpers;

namespace text_survival.Tests.Actions;

public class EventOutcomeTests
{
    private static GameContext CreateContext()
    {
        var weather = new Weather(50, GameContext.StartTime);
        var map = new GameMap(2, 1) { Weather = weather };
        for (int x = 0; x < 2; x++)
            map.SetLocation(x, 0, new Location($"Ground {x}", "", weather, 0));
        map.CurrentPosition = new GridPosition(0, 0);
        var home = map.GetLocationAt(0, 0)!;
        var player = new Player { CurrentLocation = home, Map = map };
        return new GameContext(player, home, weather) { Map = map, Ui = new ScriptedUi() };
    }

    [Fact]
    public void EveryChainedCostAndRewardApplies()
    {
        var ctx = CreateContext();
        ctx.Inventory.Add(Resource.Tinder, 0.05);
        ctx.Inventory.Add(Resource.Stick, 0.3);
        ctx.Inventory.Add(Resource.Stick, 0.3);

        var outcome = new EventResult("Fire.")
            .Costs(ResourceType.Tinder, 1).Costs(ResourceType.Fuel, 2)
            .Rewards(RewardPool.FishCatch).Rewards(RewardPool.FishCatch);

        Assert.Equal(2, outcome.RewardPools.Count);
        outcome.Apply(ctx);

        Assert.Equal(0, ctx.Inventory.Count(Resource.Tinder));
        Assert.Equal(0, ctx.Inventory.Count(Resource.Stick));
    }

    [Fact]
    public void EatingAndDrinkingFeedTheBody()
    {
        var ctx = CreateContext();
        var body = ctx.player.Body;
        body.CalorieStore = 300;
        body.Hydration = 500;
        ctx.Inventory.Add(Resource.Berries, 0.2);
        ctx.Inventory.Add(Resource.CookedMeat, 0.3);
        ctx.Inventory.Add(Resource.Water, 1.0);

        new EventResult("Eat and drink.").Eats(1).Drinks(2).Apply(ctx);

        Assert.Equal(0, ctx.Inventory.Count(Resource.CookedMeat)); // best food goes first
        Assert.Equal(1, ctx.Inventory.Count(Resource.Berries));
        Assert.True(body.CalorieStore > 300);
        Assert.True(body.Hydration > 500);
    }

    [Fact]
    public void FeedingTheFirePutsFuelInIt()
    {
        var ctx = CreateContext();
        var fire = TestFixtures.CreateTestFire(1.0, FuelType.Kindling);
        ctx.CurrentLocation.AddFeature(fire);
        double before = fire.TotalMassKg;
        ctx.Inventory.Add(Resource.Stick, 0.3);
        ctx.Inventory.Add(Resource.Stick, 0.3);

        new EventResult("Feed it.").FeedsFire(2).Apply(ctx);

        Assert.Equal(0, ctx.Inventory.Count(Resource.Stick));
        Assert.True(fire.TotalMassKg > before + 0.5);
    }

    [Fact]
    public void ThrownMeatIsMeatNotBerries()
    {
        var ctx = CreateContext();
        ctx.Inventory.Add(Resource.Berries, 0.2);
        ctx.Inventory.Add(Resource.RawMeat, 0.5);

        new EventResult("Throw it.").Costs(ResourceType.Meat, 1).Apply(ctx);

        Assert.Equal(1, ctx.Inventory.Count(Resource.Berries));
        Assert.Equal(0, ctx.Inventory.Count(Resource.RawMeat));
    }

    [Fact]
    public void EventDamageMustBeOnTheZeroToOneScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventResult("Ouch.").Damage(5, DamageType.Internal));
    }

    [Theory]
    [InlineData(ActivityType.Butchering)]
    [InlineData(ActivityType.Chopping)]
    [InlineData(ActivityType.Tracking)]
    [InlineData(ActivityType.Fishing)]
    public void OutdoorWorkCountsAsWorking(ActivityType activity)
    {
        var ctx = CreateContext();
        ctx.UpdateWithoutEvents(0, activity); // sets the activity without advancing time
        Assert.True(ctx.Check(EventCondition.Working));
    }

    [Fact]
    public void TensionsAgeInGameTimeNotWallClock()
    {
        var ctx = CreateContext();
        ctx.Tensions.AddTension(text_survival.Actions.Tensions.ActiveTension.WoundUntreated(0.2, "hand"));

        foreach (var _ in ctx.Tensions.Update(11 * 60, atCamp: false)) { }
        Assert.False(Situations.TensionOlderThan(ctx, "WoundUntreated", 12 * 60));

        foreach (var _ in ctx.Tensions.Update(60, atCamp: false)) { }
        Assert.True(Situations.TensionOlderThan(ctx, "WoundUntreated", 12 * 60));
    }
}
