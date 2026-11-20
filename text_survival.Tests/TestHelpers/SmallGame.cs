using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Factories;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Tests.Support;
using text_survival.Items;

namespace text_survival.Tests.TestHelpers;

/// <summary>A real game graph without world generation or the thirty-day settling pass.</summary>
internal static class SmallGame
{
    // Explicit starting kit for UI and survival scenarios that depend on clothing or fire tools.
    public static GameContext CreateWithStartingEquipment(TerrainType campTerrain = TerrainType.Plain)
    {
        var ctx = Create(campTerrain);
        ctx.Discoveries.InitializeStartingKnowledge();
        ctx.Inventory.Equip(Gear.WornFurChestWrap());
        ctx.Inventory.Equip(Gear.FurLegWraps(durability: 60));
        ctx.Inventory.Equip(Gear.WornHideBoots());
        ctx.Inventory.Equip(Gear.HideHandwraps());
        ctx.Inventory.Tools.Add(Gear.HandDrill());
        foreach (double weight in new[] { 0.3, 0.25, 0.35 }) ctx.Inventory.Add(Resource.Stick, weight);
        foreach (double weight in new[] { 0.05, 0.04, 0.05, 0.04 }) ctx.Inventory.Add(Resource.Tinder, weight);
        return ctx;
    }

    public static GameContext Create(TerrainType campTerrain = TerrainType.Plain)
    {
        var weather = new Weather(25, GameContext.StartTime);
        var map = new GameMap(3, 1) { Weather = weather, CurrentPosition = new(0, 0) };
        var camp = new Location("Test Camp", "", weather,
            windFactor: campTerrain.BaseWindFactor(), overheadCoverLevel: campTerrain.BaseOverheadCover())
            { Terrain = campTerrain };
        camp.AddFeature(new ForageFeature(1).AddSticks());
        camp.AddFeature(new HeatSourceFeature());
        camp.AddFeature(CacheFeature.CreateCampCache());
        camp.MarkExplored();
        map.SetLocation(0, 0, camp);
        for (int x = 1; x < map.Width; x++)
            map.SetLocation(x, 0, LocationFactory.MakeTerrainLocation(TerrainType.Plain, weather, x));
        map.UpdateVisibility();

        var player = new Player { CurrentLocation = camp, Map = map };
        return new GameContext(player, camp, weather) { Map = map, Ui = new ScriptedUi() };
    }
}
