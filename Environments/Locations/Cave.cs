using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments.Locations
{
    public class Cave : Location
    {
        new public const bool IsShelter = true;

        public Cave(IPlace parent, int numItems = 0, int numNpcs = 0) : base(parent, numItems, numNpcs)
        {
            // set type and temperature
            Type = LocationType.Cave;
            TemperatureModifier = -5;

            // create name
            var descriptors = new List<string>();
            descriptors.AddRange(caveAdjectives);
            descriptors.AddRange(genericLocationAdjectives);
            Name = Utils.GetRandomFromList(descriptors) + " " + Utils.GetRandomFromList(caveNames);
            Name = Name.Trim();

            // add forageable resources
            ForageModule.AddResource(ItemFactory.MakeMushroom(), 0.5);
            ForageModule.AddResource(ItemFactory.MakeRock(), 0.5);
        }

        protected new LootTable LootTable = new([ItemFactory.MakeMushroom, ItemFactory.MakeRock, ItemFactory.MakeGemstone, ItemFactory.MakeTorch, Weapon.GenerateRandomWeapon]);
        protected override List<Npc> npcList => [NpcFactory.MakeSpider(), NpcFactory.MakeRat(), NpcFactory.MakeSnake(), NpcFactory.MakeBat(), NpcFactory.MakeCaveBear()];

        private static readonly List<string> caveNames = ["Cave", "Cavern", "Ravine"];
        private static readonly List<string> caveAdjectives = ["", "Abandoned", "Collapsed", "Shallow", "Deep", "Echoing", "Painted", "Sparkling", "Dim", "Icy"];


    }
}
