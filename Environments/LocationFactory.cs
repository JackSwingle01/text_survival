using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments
{
    internal class LocationFactory
    {
        public static Location GenerateLocation(Location.LocationType type, IPlace parent, int numItems = 1, int numNpcs = 1)
        {
            string name = GetRandomLocationAdjective(type) + " " + GetRandomLocationName(type);
            Location location = new(name, parent);
            return location;
        }

        private static NpcPool CreateNpcPool(Location.LocationType locationType)
        {
            NpcPool npcs = new();
            var npcList = LocationNpcs[locationType];
            foreach (string npcName in npcList)
            {
                npcs.Add(NpcFactory.NpcDefinitions[npcName]);
            }
            return npcs;
        }
        private static LootTable CreateLootTable(Location.LocationType locationType)
        {
            LootTable items = new();
            var itemList = EnvironmentItems[locationType];
            foreach (string itemName in itemList)
            {
                items.AddLoot(ItemFactory.ItemDefinitions[itemName]);
            }
            return items;
        }

        #region Npcs

        private static readonly Dictionary<Location.LocationType, List<string>> LocationNpcs = new()
        {
            { Location.LocationType.Cave, new List<string> {
                "Bat",
                "Spider",
                "Rat",
                "Snake",
                "Dragon",
                "Skeleton"
            } },
            { Location.LocationType.AbandonedBuilding, new List<string> {
                "Rat",
                "Spider",
                "Bandit",
                "Goblin",
                "Skeleton"
            } },
            { Location.LocationType.River, new List<string>
            {
                "Crocodile",
                "Snake",
            } },
            { Location.LocationType.Road, new List<string>()
            {
                "Bandit",
                "Snake",
            } },
        };

        #endregion Npcs

        #region Items

        private static readonly Dictionary<Location.LocationType, List<string>> EnvironmentItems = new()
        {
            { Location.LocationType.AbandonedBuilding, new List<string> {
                "Apple",
                "Bread",
                "Coin",
                "Sword",
                "Shield",
                "Bandage",
                "Health Potion",
                "Armor",
                "RandomWeapon"
            } },
            { Location.LocationType.Cave, new List<string> {
                "Mushroom",
                "Rock",
                "Gemstone",
                "Torch",
                "RandomWeapon"
            } },
            { Location.LocationType.River, new List<string> {
                "Fish",
                "Water",
                "Water",
                "Water",
                "Water",
            } },
            { Location.LocationType.Road, new List<string> {
                "Coin",
                "Bandage",
                "Rock",
                "Stick",
            } },

        };

        #endregion Items

        #region Names
        public static string GetRandomLocationName(Location.LocationType locationType)
        {
            List<string> names = locationType switch
            {
                Location.LocationType.Cave => caveNames,
                Location.LocationType.Lake => lakeNames,
                Location.LocationType.River => riverNames,
                Location.LocationType.AbandonedBuilding => abandonedBuildingNames,
                Location.LocationType.Road => roadNames,
                _ =>["Location"]
            };
            string name = Utils.GetRandomFromList(names);
            return name;
        }
        private static string GetRandomLocationAdjective(Location.LocationType locationType)
        {
            List<string> adjectives = locationType switch
            {
                Location.LocationType.Cave => caveAdjectives,
                Location.LocationType.Lake => lakeAdjectives,
                Location.LocationType.River => riverAdjectives,
                Location.LocationType.AbandonedBuilding => abandonedBuildingAdjectives,
                Location.LocationType.Road => roadAdjectives,
                _ =>[""]
            };
            adjectives.AddRange(genericAdjectives);
            string adjective = Utils.GetRandomFromList(adjectives);
            return adjective;
        }

        private static readonly List<string> caveNames = ["Cave", "Cave", "Cavern", "Tunnel", "Mine", "Den", "Lair", "Coal Mine", "Iron Mine", "Gold Mine", "Hideout", "Ravine",];
        private static readonly List<string> riverNames = ["River", "Stream", "Creek", "Waterfall", "Shallow River", "Brook", "Shallow Creek", "Shallow Stream", "Deep River", "Deep Creek", "Deep Stream", "Rapids", "Shallow Rapids", "Deep Rapids", "Small Waterfall", "Large Waterfall", "Huge Waterfall",];
        private static readonly List<string> abandonedBuildingNames = ["Building", "House", "Shack", "Cabin", "Church", "Chapel", "Hut", "Fortress", "Tower", "Keep", "Barn",];
        private static readonly List<string> lakeNames = ["Lake", "Pond", "Water", "Shallow Lake", "Shallow Pond", "Shallow Water", "Deep Lake", "Deep Pond", "Deep Water", "Shimmering Pond", "Shimmering Lake", "Shimmering Water", "Still Lake", "Still Pond", "Still Water", "Quiet Lake", "Quiet Pond", "Quiet Water", "Calm Lake", "Calm Pond", "Calm Water", "Rippling Lake", "Rippling Pond", "Rippling Water", "Misty Lake", "Misty Pond", "Misty Water", "Foggy Lake", "Foggy Pond", "Foggy Water", "Murky Lake", "Murky Pond", "Murky Water", "Dark Lake",];
        private static readonly List<string> roadNames = ["Road", "Path", "Trail", "Dirt Road", "Gravel Road", "Paved Road", "Dirt Path", "Gravel Path",];

        private static readonly List<string> genericAdjectives = ["", "Old", "Dusty", "Cool", "Breezy", "Quiet", "Ancient", "Ominous", "Sullen", "Forlorn", "Desolate", "Secret", "Hidden", "Forgotten", "Cold", "Dark", "Damp", "Wet", "Dry", "Hot", "Warm",];

        private static readonly List<string> roadAdjectives = ["Dirt", "Gravel", "Paved", "Winding", "Straight", "Curved", "Twisting", "Bumpy", "Smooth", "Narrow", "Wide", "Long", "Short", "Steep", "Flat", "Sloping", "Rough", "Smooth", "Muddy",];
        private static readonly List<string> caveAdjectives = ["", "Abandoned", "Collapsed", "Shallow", "Deep", "Echoing",];
        private static readonly List<string> abandonedBuildingAdjectives = ["", "Abandoned", "Collapsed", "Ruined"];
        private static readonly List<string> lakeAdjectives = ["", "Shallow", "Deep", "Still", "Quiet", "Calm", "Rippling", "Misty", "Foggy", "Murky", "Dark", "Shimmering",];
        private static readonly List<string> riverAdjectives = ["", "Shallow", "Deep", "Still", "Quiet", "Calm", "Rippling", "Misty", "Foggy", "Murky", "Dark", "Shimmering", "Quick", "Loud", "Slow", "Lazy"];
        #endregion Names
    }
}