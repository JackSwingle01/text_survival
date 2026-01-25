using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Actors;

public static class NPCFactory
{
    private static readonly string[] TestNames = ["Grog", "Ubik", "Olar"];

    public static NPC CreateTestNPC(Location location, GameMap map, Location? camp = null)
    {
        var name = TestNames[Utils.RandInt(0, TestNames.Length - 1)];
        var personality = new Personality
        {
            Boldness = Utils.RandDouble(0.3, 0.7),
            Selfishness = Utils.RandDouble(0.2, 0.5),
            Sociability = Utils.RandDouble(0.5, 0.8)
        };

        var npc = new NPC(name, personality, location, map)
        {
            Camp = camp
        };
        npc.Inventory.MaxWeightKg = 15;

        // Equip starting clothing (same as player)
        npc.Inventory.Equip(Gear.WornFurChestWrap());
        npc.Inventory.Equip(Gear.FurLegWraps(durability: 60));
        npc.Inventory.Equip(Gear.WornHideBoots());
        npc.Inventory.Equip(Gear.HideHandwraps());

        // Add starting supplies
        npc.Inventory.Tools.Add(Gear.HandDrill());
        npc.Inventory.Add(Resource.Stick, 0.5);
        npc.Inventory.Add(Resource.Tinder, 0.1);

        // Remember starting location and camp
        npc.ResourceMemory.RememberLocation(location);
        if (camp != null) npc.ResourceMemory.RememberLocation(camp);

        return npc;
    }

    /// <summary>
    /// Spawn one NPC adjacent to camp for testing.
    /// </summary>
    public static NPC? SpawnNearCamp(GameMap map, Location camp)
    {
        var campPos = map.GetPosition(camp);
        var adjacentPositions = new[]
        {
            new GridPosition(campPos.X + 1, campPos.Y),
            new GridPosition(campPos.X - 1, campPos.Y),
            new GridPosition(campPos.X, campPos.Y + 1),
            new GridPosition(campPos.X, campPos.Y - 1)
        };

        foreach (var pos in adjacentPositions)
        {
            var location = map.GetLocationAt(pos);
            if (location != null)
            {
                return CreateTestNPC(location, map, camp);
            }
        }
        return null;
    }
}
