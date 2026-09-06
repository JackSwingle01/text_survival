using System.Text.Json;
using text_survival.Actions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Environments.Surface;
using text_survival.Items;
using text_survival.Persistence;

namespace text_survival.Tests.Environments.Surface;

/// <summary>
/// Finding a thing and reaching it are different questions; the snow only answers the second.
/// </summary>
public class BuriedDiscoveryTests
{
    private static Location BareTile()
    {
        var weather = new Weather(-10, GameContext.StartTime);
        return new Location("Test Ground", "", weather) { Terrain = TerrainType.Plain };
    }

    private static SurfaceWeather HeavySnow => new(15, 0.2, 0, 0.005 / 60, 0);

    private static void Bury(Location location, int hours) =>
        location.Surface.Advance(60 * hours, HeavySnow);

    private static GroundItemsFeature DropSomething(Location location)
    {
        var stash = new GroundItemsFeature();
        var items = new Inventory();
        items.Add(Resource.Stick, 0.4);
        stash.Add(items);
        location.AddFeature(stash);
        return stash;
    }

    [Fact]
    public void ThingsPutDownNowRestOnTodaysSurface_NotUnderIt()
    {
        var location = BareTile();
        Bury(location, 20);
        double snow = location.Surface.SurfaceHeightM;
        Assert.True(snow > 0.3);

        var stash = DropSomething(location);

        Assert.Equal(snow, stash.PlacedBaseElevationM!.Value, 6);
        Assert.False(location.IsCovered(stash), "dropping something on the snow does not bury it");
    }

    [Fact]
    public void SnowfallBuriesWhatWasAlreadyOnTheGround()
    {
        var location = BareTile();
        var stash = DropSomething(location);
        Assert.False(location.IsCovered(stash));

        Bury(location, 6);

        Assert.True(location.IsCovered(stash), "a few hours of snow should cover a pile on the ground");
        Assert.True(location.Surface.GetExcavationMinutes(stash.Placement) > 0);
    }

    [Fact]
    public void ABuriedTargetOffersDiggingInsteadOfItsUsualActions()
    {
        var location = BareTile();
        var stash = DropSomething(location);
        Bury(location, 6);

        var ctx = new GameContext(new text_survival.Actors.Player.Player(), location, location.Weather);
        ctx.player.CurrentLocation = location;

        var options = location.GetWorkOptions(ctx).ToList();

        Assert.DoesNotContain(options, o => o.Id == "ground_stash");
        Assert.Contains(options, o => o.Id == $"dig_{stash.PlacementId}");
    }

    [Fact]
    public void AnUndiscoveredBodyNeverLeaksItsNameThroughADiggingOption()
    {
        var location = BareTile();
        var body = new NPCBodyFeature("Ake", "The cold took them.", GameContext.StartTime, new Inventory());
        location.AddFeature(body);
        Bury(location, 8);

        var ctx = new GameContext(new text_survival.Actors.Player.Player(), location, location.Weather);
        ctx.player.CurrentLocation = location;

        Assert.True(location.IsCovered(body));
        Assert.DoesNotContain(location.GetWorkOptions(ctx), o => o.Label.Contains("Ake"));

        body.IsDiscovered = true;
        Assert.Contains(location.GetWorkOptions(ctx), o => o.Label.Contains("Ake"));
    }

    [Fact]
    public void DirectAccessIsRefusedEvenWhenTheMenuIsBypassed()
    {
        var location = BareTile();
        var stash = DropSomething(location);
        Bury(location, 6);

        Assert.NotNull(SurfaceAccess.Check(location, stash));

        // ...and is allowed again once the snow is off it.
        location.Surface.ApplyExcavation(stash.Placement, 10_000);
        Assert.Null(SurfaceAccess.Check(location, stash));
    }

    [Fact]
    public void AShovelDoublesTheDigging_AndABrokenOneDoesNot()
    {
        var bare = BareTile();
        var withShovel = BareTile();
        var barehand = DropSomething(bare);
        var shovelled = DropSomething(withShovel);
        Bury(bare, 10);
        Bury(withShovel, 10);

        // Short enough that neither run finishes, so this compares rates.
        bare.Surface.ApplyExcavation(barehand.Placement, 3, toolFactor: 1);
        withShovel.Surface.ApplyExcavation(shovelled.Placement, 3, toolFactor: 2);

        double byHand = bare.Surface.GetBlockingCoverM(barehand.Placement);
        double byShovel = withShovel.Surface.GetBlockingCoverM(shovelled.Placement);

        double clearedByHand = bare.Surface.SolidCoverHeightM - byHand;
        double clearedByShovel = withShovel.Surface.SolidCoverHeightM - byShovel;

        Assert.Equal(clearedByHand * 2, clearedByShovel, 4);
    }

    [Fact]
    public void DiggingOnlyClearsTheTargetsOwnFootprint()
    {
        var location = BareTile();
        var stash = DropSomething(location);
        var neighbour = new GroundItemsFeature();
        var items = new Inventory();
        items.Add(Resource.Stone, 0.4);
        neighbour.Add(items);
        location.Features.Add(neighbour);   // authored: ground level, like the stash

        Bury(location, 10);
        double tileSnow = location.Surface.SnowDepthM;

        location.Surface.ApplyExcavation(stash.Placement, 10_000);

        Assert.False(location.IsCovered(stash));
        Assert.True(location.IsCovered(neighbour), "the pile two metres away is still buried");
        Assert.Equal(tileSnow, location.Surface.SnowDepthM, 3);
    }

    [Fact]
    public void SnowfallCanReburyAClearedTarget_AndThawCanFreeIt()
    {
        var location = BareTile();
        var stash = DropSomething(location);
        Bury(location, 10);
        location.Surface.ApplyExcavation(stash.Placement, 10_000);
        Assert.False(location.IsCovered(stash));

        Bury(location, 10);
        Assert.True(location.IsCovered(stash), "fresh snow fills the hole back in");

        location.Surface.Advance(60 * 24 * 6, new SurfaceWeather(52, 0.3, 0.7, 0, 0));
        Assert.False(location.IsCovered(stash), "and a thaw does the digging for you");
    }

    [Fact]
    public void ShallowWaterAloneNeverDemandsDigging()
    {
        var location = BareTile();
        var stash = DropSomething(location);
        location.Surface.AddWater(0.25);

        Assert.False(location.IsCovered(stash), "you can reach through a puddle, you just get wet");
    }

    [Fact]
    public void DeeperBurialSlowsNewSearchingButNeverRevaluesEarnedEffort()
    {
        var location = BareTile();
        location.Features.Add(new ForageFeature(1));
        var find = new EnvironmentalDetail("bones", "Scattered Bones", "Old bones.");
        location.HiddenFeatures.Add(new HiddenFeature(find, 2.0, DiscoveryCategory.Minor));

        location.RevealDiscoveries(1.0);
        double earned = location.HiddenFeatures[0].EffectiveSearchHours;
        Assert.Equal(1.0, earned, 6);

        Bury(location, 30);
        location.RevealDiscoveries(1.0);

        double afterSnow = location.HiddenFeatures[0].EffectiveSearchHours;
        Assert.True(afterSnow > earned, "progress never goes backwards");
        Assert.True(afterSnow - earned < 0.2, $"an hour under deep snow bought {afterSnow - earned:F3}");
        Assert.True(afterSnow - earned > 0, "a determined search still finds things eventually");
    }

    [Fact]
    public void ANewFindInheritsNoneOfTheTilesSearchHistory()
    {
        var location = BareTile();
        location.Features.Add(new ForageFeature(1));
        var old = new EnvironmentalDetail("bones", "Scattered Bones", "Old bones.");
        location.HiddenFeatures.Add(new HiddenFeature(old, 5.0, DiscoveryCategory.Minor));

        location.RevealDiscoveries(3.0);

        var fresh = new EnvironmentalDetail("nest", "Old Nest", "A nest.");
        location.HiddenFeatures.Add(new HiddenFeature(fresh, 5.0, DiscoveryCategory.Minor));

        Assert.Equal(3.0, location.HiddenFeatures[0].EffectiveSearchHours, 6);
        Assert.Equal(0.0, location.HiddenFeatures[1].EffectiveSearchHours, 6);
    }

    [Fact]
    public void ASaveWrittenBeforePerFindEffortKeepsTheHoursItEarned()
    {
        var location = BareTile();
        var forage = new ForageFeature(1);
        forage.DiscoveryProgress = 2.5;      // as an older save would have it
        location.Features.Add(forage);
        location.HiddenFeatures.Add(new HiddenFeature(
            new EnvironmentalDetail("bones", "Scattered Bones", "Old bones."), 4.0, DiscoveryCategory.Minor));

        location.RevealDiscoveries(0);

        Assert.Equal(2.5, location.HiddenFeatures[0].EffectiveSearchHours, 6);

        // ...once, and not again.
        location.RevealDiscoveries(0.5);
        Assert.Equal(3.0, location.HiddenFeatures[0].EffectiveSearchHours, 6);
    }

    [Fact]
    public void HiddenThingsAgeWithoutRevealingThemselves()
    {
        var location = BareTile();
        var body = new NPCBodyFeature("Ake", "The cold took them.", GameContext.StartTime, new Inventory());
        location.HiddenFeatures.Add(new HiddenFeature(body, 100, DiscoveryCategory.Major));

        location.Update(60 * 24);

        Assert.Equal(24, body.HoursSinceDeath, 3);
        Assert.Single(location.HiddenFeatures);
        Assert.Empty(location.Features.OfType<NPCBodyFeature>());
    }

    [Fact]
    public void SurfaceAndDiggingStateSurviveASaveAndLoad()
    {
        var ctx = GameContext.CreateNewGame(seed: 99);
        var here = ctx.CurrentLocation;
        var stash = DropSomething(here);

        here.Surface.Advance(60 * 12, HeavySnow);
        here.Surface.ApplyExcavation(stash.Placement, 8);
        here.RevealDiscoveries(0.4);

        double snow = here.Surface.SnowDepthM;
        double water = here.Surface.StoredWaterM;
        double cover = here.Surface.GetBlockingCoverM(stash.Placement);
        double effort = here.HiddenFeatures.FirstOrDefault()?.EffectiveSearchHours ?? -1;
        int layers = here.Surface.Layers.Count;

        string json = JsonSerializer.Serialize(ctx, SaveManager.Options);
        var loaded = JsonSerializer.Deserialize<GameContext>(json, SaveManager.Options)!;

        var reloaded = loaded.CurrentLocation;
        var reloadedStash = reloaded.GetFeature<GroundItemsFeature>()!;

        Assert.Equal(snow, reloaded.Surface.SnowDepthM, 6);
        Assert.Equal(water, reloaded.Surface.StoredWaterM, 6);
        Assert.Equal(layers, reloaded.Surface.Layers.Count);
        Assert.Equal(cover, reloaded.Surface.GetBlockingCoverM(reloadedStash.Placement), 6);
        Assert.Equal(effort, reloaded.HiddenFeatures.FirstOrDefault()?.EffectiveSearchHours ?? -1, 6);
        Assert.True(reloaded.SearchEffortMigrated);
    }

    [Fact]
    public void ASaveWithNoSurfaceAtAllLoadsAsCleanGroundForItsTerrain()
    {
        var location = BareTile();
        string json = JsonSerializer.Serialize(location, SaveManager.Options);
        Assert.Contains("surface", json, StringComparison.OrdinalIgnoreCase);

        var stripped = new Location("Test Ground", "", location.Weather) { Terrain = TerrainType.Marsh };

        Assert.Equal(0, stripped.Surface.SnowDepthM, 9);
        Assert.Equal(SubstrateProfile.Peat.Name, stripped.Surface.Substrate.Name);
        Assert.True(stripped.Surface.WetnessPct > 0.5, "a marsh loads wet, as a marsh is");
    }
}
