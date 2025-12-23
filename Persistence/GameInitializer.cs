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
        Zone zone = ZoneFactory.MakeForestZone();

        // Initialize weather for game start time (9:00 AM, Jan 1)
        var gameStartTime = new DateTime(2025, 1, 1, 9, 0, 0);
        zone.Weather.Update(gameStartTime);

        Location startingArea = zone.Graph.All.First(s => s.Name == "Forest Camp");

        // Add campfire (unlit - player must start it)
        HeatSourceFeature campfire = new HeatSourceFeature();
        campfire.AddFuel(2, FuelType.Kindling);
        startingArea.Features.Add(campfire);

        Player player = new Player();
        Camp camp = new Camp(startingArea);
        GameContext context = new GameContext(player, camp);

        // Equip starting clothing
        context.Inventory.Equip(Equipment.WornFurChestWrap());
        context.Inventory.Equip(Equipment.FurLegWraps());
        context.Inventory.Equip(Equipment.FurBoots());

        // Add starting supplies
        context.Inventory.Tools.Add(Tool.HandDrill());
        context.Inventory.Sticks.Add(0.3);
        context.Inventory.Sticks.Add(0.25);
        context.Inventory.Sticks.Add(0.35);
        context.Inventory.Tinder.Add(0.05);
        context.Inventory.Tinder.Add(0.04);

        return context;
    }

    /// <summary>
    /// Load game from save file. Returns null if no save exists or load fails.
    /// </summary>
    public static GameContext? LoadGame()
    {
        var saveData = SaveManager.Load();
        if (saveData == null)
            return null;

        // Create a fresh game context with the zone structure
        var ctx = CreateNewGame();

        // Restore state from save data
        SaveDataConverter.RestoreFromSaveData(ctx, saveData);

        return ctx;
    }

    /// <summary>
    /// Try to load a saved game, falling back to a new game if none exists.
    /// </summary>
    public static GameContext LoadOrCreateNew()
    {
        if (SaveManager.HasSaveFile())
        {
            var loaded = LoadGame();
            if (loaded != null)
                return loaded;
        }

        return CreateNewGame();
    }
}
