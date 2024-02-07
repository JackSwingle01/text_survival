using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments
{
    public static class AreaFactory
    {
        public static string GetRandomAreaName(Area.EnvironmentType environmentType)
        {
            List<string> names = environmentType switch
            {
                Area.EnvironmentType.Forest => new List<string>()
                    {
                        "Forest",
                        "Clearing",
                        "Grove",
                        "Lake",
                        "Trail",
                        "Abandoned Campsite",
                        "Dark Forest",
                        "Dark Grove",
                        "Dark Trail",
                        "Open Field",
                        "Field",
                        "Meadow",
                        "Grassland",
                        "Plains",
                        "Woods",
                        "Dark Woods",
                        "Dark Forest",
                        "Hollow",
                        "Dark Hollow",
                        "Hill",
                        "Valley",
                        "Dark Valley",
                        "Ominous Forest",
                        "Shady Forest",
                        "Lonely Tree",
                        "Ancient Tree",
                        "Ancient Forest",
                        "Ancient Woods",
                        "Ancient Grove",
                        "Old Growth Forest",
                        "Abandoned Camp",
                        "Abandoned Cornfield",
                        "Grassy Field",
                        "Dirt Trail",
                        "Gravel Trail",



                    },
                Area.EnvironmentType.Cave => new List<string>()
                    {
                        "Cave",
                        "Cavern",
                        "Tunnel",
                        "Mine",
                        "Abandoned Mine",
                        "Dark Cave",
                        "Den",
                        "Lair",
                        "Coal Mine",
                        "Iron Mine",
                        "Gold Mine",
                        "Empty Mine",
                        "Abandoned Hideout",
                        "Dark Cavern",
                        "Dark Tunnel",
                        "Dark Mine",
                        "Collapsed Mine",
                        "Collapsed Tunnel",
                        "Cool Cave",
                        "Cool Cavern",
                        "Breezy Cave",
                        "Breezy Cavern",
                        "Breezy Tunnel",
                        "Breezy Mine",
                        "Quiet Cave",
                        "Quiet Cavern",
                        "Quiet Tunnel",
                        "Quiet Mine",
                        "Shallow Ravine",
                        "Deep Ravine",
                        "Echoing Cave",
                        "Echoing Cavern",
                        "Echoing Tunnel",
                        "Echoing Mine",
                    },
                Area.EnvironmentType.AbandonedBuilding => new List<string>()
                    {
                        "Abandoned Building",
                        "Abandoned House",
                        "Abandoned Shack",
                        "Abandoned Cabin",
                        "Abandoned Church",
                        "Old Hut",
                        "Old House",
                        "Old Shack",
                        "Old Cabin",
                        "Old Church",
                        "Ruins",
                        "Old Ruins",
                        "Abandoned Ruins"

                    },
                Area.EnvironmentType.Road => new List<string>()
                    {
                        "Road",
                        "Path",
                        "Trail",
                        "Dirt Road",
                        "Gravel Road",
                        "Paved Road",
                        "Dirt Path",
                        "Gravel Path",

                    },
                Area.EnvironmentType.River => new List<string>()
                    {
                        "River",
                        "Stream",
                        "Creek",
                        "Waterfall",
                        "Lake",
                        "Pond",
                        "Water",
                        "Swamp",
                        "Marsh",
                        "Bog",
                        "Wetland",
                        "Wetlands",
                        "Wet Swamp",
                        "Shallow River",
                        "Brook",
                        "Shallow Creek",
                        "Shallow Stream",
                        "Deep River",
                        "Deep Creek",
                        "Deep Stream",
                        "Rapids",
                        "Shallow Rapids",
                        "Deep Rapids",
                        "Small Waterfall",
                        "Large Waterfall",
                        "Huge Waterfall",
                        "Shimmering Pond",
                        "Shimmering Lake",
                    },
                _ => new List<string>()
                    {
                        "Location"
                    },
            };
            string name = names[Utils.RandInt(0, names.Count - 1)];
            return name;
        }


        private static readonly Dictionary<string, Func<Item>> ItemDefinitions = new()
        {
            { "Mushroom", ItemFactory.MakeMushroom },
            { "Apple", ItemFactory.MakeApple },
            { "Bread", ItemFactory.MakeBread },
            { "Berry", ItemFactory.MakeBerry },
            { "Carrot", ItemFactory.MakeCarrot },
            { "Water", ItemFactory.MakeWater },
            { "Stick", ItemFactory.MakeStick },
            { "Wood", ItemFactory.MakeWood },
            { "Rock", ItemFactory.MakeRock },
            { "Gemstone", ItemFactory.MakeGemstone },
            { "Coin", ItemFactory.MakeCoin },
            { "Sword", ItemFactory.MakeSword },
            { "Shield", ItemFactory.MakeShield },
            { "Armor", ItemFactory.MakeArmor },
            { "Health Potion", ItemFactory.MakeHealthPotion },
            { "Bandage", ItemFactory.MakeBandage },
            { "Torch", ItemFactory.MakeTorch },
            { "Fish", ItemFactory.MakeFish },
            { "RandomWeapon", Weapon.GenerateRandomWeapon }
        };

        private static readonly Dictionary<Area.EnvironmentType, List<string>> EnvironmentItems = new()
        {
            { Area.EnvironmentType.AbandonedBuilding, new List<string> {
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
            { Area.EnvironmentType.Forest, new List<string> {
                "Berry",
                "Carrot",
                "Water",
                "Mushroom",
                "Stick",
                "Wood"
            } },
            { Area.EnvironmentType.Cave, new List<string> {
                "Mushroom",
                "Rock",
                "Gemstone",
                "Torch",
                "RandomWeapon"
            } },
            { Area.EnvironmentType.River, new List<string> {
                "Fish",
                "Water",
                "Water",
                "Water",
                "Water",
            } },
            { Area.EnvironmentType.Road, new List<string> {
                "Coin",
                "Bandage",
                "Rock",
                "Stick",
            } },

        };

        private static readonly Dictionary<string, Func<Npc>> NpcDefinitions = new()
        {
            { "Rat", NpcFactory.MakeRat },
            { "Wolf", NpcFactory.MakeWolf },
            { "Bear", NpcFactory.MakeBear },
            { "Snake", NpcFactory.MakeSnake },
            { "Bat", NpcFactory.MakeBat },
            { "Spider", NpcFactory.MakeSpider },
            { "Bandit", NpcFactory.MakeBandit },
            { "Goblin", NpcFactory.MakeGoblin },
            { "Dragon", NpcFactory.MakeDragon },
            { "Skeleton", NpcFactory.MakeSkeleton },
            { "Crocodile", NpcFactory.MakeCrocodile },

        };


        private static readonly Dictionary<Area.EnvironmentType, List<string>> EnvironmentNpcs = new()
        {
            { Area.EnvironmentType.Forest, new List<string> {
                "Wolf",
                "Bear",
                "Snake",
                "Goblin",
                "Bandit",
            } },
            { Area.EnvironmentType.Cave, new List<string> {
                "Bat",
                "Spider",
                "Rat",
                "Snake",
                "Dragon",
                "Skeleton"
            } },
            { Area.EnvironmentType.AbandonedBuilding, new List<string> {
                "Rat",
                "Spider",
                "Bandit",
                "Goblin",
                "Skeleton"
            } },
            { Area.EnvironmentType.River, new List<string>
            {
                "Crocodile",
                "Snake",
            } },
            { Area.EnvironmentType.Road, new List<string>()
            {
                "Bandit",
                "Snake",
            } },
        };

        private static double GetAreaBaseTemperature(Area.EnvironmentType environment)
        {
            return environment switch
            {
                Area.EnvironmentType.Forest => 70,
                Area.EnvironmentType.Cave => 50,
                Area.EnvironmentType.AbandonedBuilding => 75,
                Area.EnvironmentType.River => 70,
                Area.EnvironmentType.Road => 75,
                _ => 70,
            };
        }

        public static Area GenerateArea(Area.EnvironmentType type, int numItems = 1, int numNpcs = 1)
        {
            Area area = new(GetRandomAreaName((type)), "", GetAreaBaseTemperature(type));
            ItemPool itemPool = CreateItemPool(type);
            NpcPool npcPool = CreateNpcPool(type);
            area.IsShelter = IsEnvironmentShelter(type);
            for (int i = 0; i < numItems; i++)
            {
                area.PutThing(itemPool.GenerateRandomItem());
            }
            for (int i = 0; i < numNpcs; i++)
            {
                area.PutThing(npcPool.GenerateRandomNpc());
            }
            return area;
        }

        private static bool IsEnvironmentShelter(Area.EnvironmentType type)
        {
            return type switch
            {
                Area.EnvironmentType.AbandonedBuilding => true,
                Area.EnvironmentType.Cave => true,
                _ => false,
            };
        }

        private static NpcPool CreateNpcPool(Area.EnvironmentType environment)
        {
            NpcPool npcs = new();
            var npcList = EnvironmentNpcs[environment];
            foreach (string npcName in npcList)
            {
                npcs.Add(NpcDefinitions[npcName]);
            }
            return npcs;
        }
        private static ItemPool CreateItemPool(Area.EnvironmentType environment)
        {
            ItemPool items = new();
            var itemList = EnvironmentItems[environment];
            foreach (string itemName in itemList)
            {
                items.Add(ItemDefinitions[itemName]);
            }
            return items;
        }
    }
}
