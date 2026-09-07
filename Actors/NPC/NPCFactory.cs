using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Actors;

public static class NPCFactory
{
    private static readonly string[] Names =
        ["Grog", "Ubik", "Olar", "Tesk", "Vela", "Marn", "Sura", "Dorn", "Hesk", "Ilva", "Rok", "Anu"];

    public static NPC CreateTestNPC(Location location, GameMap map, Location? camp = null)
    {
        var name = Names[Utils.RandInt(0, Names.Length - 1)];
        return Create(name, location, map, camp, groupId: 0);
    }

    private static NPC Create(string name, Location location, GameMap map, Location? camp, int groupId)
    {
        var personality = new Personality
        {
            Boldness = Utils.RandDouble(0.3, 0.7),
            Selfishness = Utils.RandDouble(0.2, 0.5),
            Sociability = Utils.RandDouble(0.5, 0.8)
        };

        var npc = new NPC(name, personality, location, map)
        {
            Camp = camp,
            GroupId = groupId,
            // ponytail: uniform roll. Tie it to Sociability if strangers should feel less
            // arbitrary. -1.0 exactly is already hostile-on-sight (NPC.IsHostileTo), which
            // is intentional: some people out here will not talk to you.
            StrangerDisposition = groupId == 0 ? 0 : Utils.RandDouble(-1, 0)
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
    /// One companion adjacent to camp at world gen. Ungrouped, so they meet the player at a
    /// neutral 0 rather than a stranger's roll - this is the ally you start with.
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
            if (location != null && location.IsPassable &&
                !map.IsEdgeBlocked(campPos, pos, map.Weather.CurrentSeason))
            {
                return CreateTestNPC(location, map, camp);
            }
        }
        return null;
    }

    /// <summary>
    /// Scatter 3-4 bands of 1-3 survivors across the map, each with its own camp, far enough
    /// from the player's that meeting one costs a trip. They run their own survival loop from
    /// day one, so what you find on day twelve is an outcome, not a setup.
    /// </summary>
    public static List<NPC> PopulateGroups(GameMap map, Location playerCamp, int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var npcs = new List<NPC>();
        var campPos = map.GetPosition(playerCamp);

        var candidates = new List<GridPosition>();
        for (int x = 2; x < map.Width - 2; x++)
            for (int y = 2; y < map.Height - 2; y++)
            {
                var pos = new GridPosition(x, y);
                int d = pos.ManhattanDistance(campPos);
                if (d < 10 || d > 40) continue;
                if (map.GetLocationAt(pos)?.IsPassable == true) candidates.Add(pos);
            }
        if (candidates.Count == 0) return npcs;

        var names = Names.OrderBy(_ => rng.Next()).ToList();
        int groups = 3 + rng.Next(2);
        for (int g = 1; g <= groups && names.Count > 0; g++)
        {
            var home = map.GetLocationAt(candidates[rng.Next(candidates.Count)])!;
            if (!home.HasFeature<CacheFeature>()) home.Features.Add(CacheFeature.CreateCampCache());

            int size = Math.Min(1 + rng.Next(3), names.Count);
            for (int i = 0; i < size; i++)
            {
                var name = names[^1];
                names.RemoveAt(names.Count - 1);
                npcs.Add(Create(name, home, map, home, groupId: g));
            }
        }
        return npcs;
    }
}
