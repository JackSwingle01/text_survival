using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Factories;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Persistence;

/// <summary>
/// Handles game initialization for both new games and loaded saves.
/// </summary>
public static class GameInitializer
{
    /// <summary>
    /// Create a new game from scratch with starting equipment and supplies.
    /// </summary>
    public static GameContext CreateNewGame()
    {
        // Clear event cooldowns for fresh game
        GameEventRegistry.ClearTriggerTimes();

        Zone zone = ZoneFactory.MakeForestZone();

        // Initialize weather for game start time (9:00 AM, Jan 1)
        var gameStartTime = new DateTime(2025, 1, 1, 9, 0, 0);
        zone.Weather.Update(gameStartTime);

        Location startingArea = zone.Graph.All.First(s => s.Name == "Forest Camp");

        // Add campfire (unlit - player must start it)
        HeatSourceFeature campfire = new HeatSourceFeature();
        campfire.AddFuel(2, FuelType.Kindling);
        startingArea.Features.Add(campfire);

        // Add camp storage cache
        startingArea.Features.Add(CacheFeature.CreateCampCache());

        Player player = new Player();
        Camp camp = new Camp(startingArea);
        GameContext context = new GameContext(player, camp);

        // Equip starting clothing
        context.Inventory.Equip(Equipment.WornFurChestWrap());
        context.Inventory.Equip(Equipment.FurLegWraps());
        context.Inventory.Equip(Equipment.FurBoots());

        // Add starting supplies
        context.Inventory.Tools.Add(Tool.HandDrill());
        context.Inventory.Sticks.Push(0.3);
        context.Inventory.Sticks.Push(0.25);
        context.Inventory.Sticks.Push(0.35);
        context.Inventory.Tinder.Push(0.05);
        context.Inventory.Tinder.Push(0.04);

        return context;
    }

    /// <summary>
    /// Load game from save file. Returns null if no save exists or load fails.
    /// </summary>
    public static GameContext? LoadGame(string? sessionId = null)
    {
        var saveData = SaveManager.Load(sessionId);
        if (saveData == null)
            return null;

        // Create a blank zone container - SaveDataConverter will populate it
        var zone = new Zone(saveData.Zone.Name, saveData.Zone.Description);
        
        // We need a dummy camp location to initialize the Context, 
        // but it will be overwritten/corrected by RestoreFromSaveData
        var dummyLoc = new Location("Loading...", "", zone, 0); 
        // Note: Do not add dummyLoc to zone.Graph - it's temporary

        Player player = new Player();
        Camp camp = new Camp(dummyLoc);
        
        var ctx = new GameContext(player, camp);

        // Restore state from save data (this will replace the dummy location/zone content)
        SaveDataConverter.RestoreFromSaveData(ctx, saveData);

        return ctx;
    }

    /// <summary>
    /// Try to load a saved game, falling back to a new game if none exists.
    /// </summary>
    public static GameContext LoadOrCreateNew(string? sessionId = null)
    {
        if (SaveManager.HasSaveFile(sessionId))
        {
            var loaded = LoadGame(sessionId);
            if (loaded != null)
                return loaded;
        }

        return CreateNewGame();
    }
}
