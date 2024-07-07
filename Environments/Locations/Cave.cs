using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments.Locations
{
    public class Cave : Location
    {
        new public const bool IsShelter = true;
        
        public Cave(IPlace parent, int numItems = 0, int numNpcs = 0) : base(parent, numItems, numNpcs)
        {
            Type = LocationType.Cave;
            TemperatureModifier = -5;

            var descriptors = new List<string>();
            descriptors.AddRange(caveAdjectives);
            descriptors.AddRange(genericLocationAdjectives);
            Name = Utils.GetRandomFromList(descriptors) + " " + Utils.GetRandomFromList(caveNames);
        }

        protected override List<Item> itemList => [ItemFactory.MakeMushroom(), ItemFactory.MakeRock(), ItemFactory.MakeGemstone(), ItemFactory.MakeTorch(), Weapon.GenerateRandomWeapon()];
        protected override List<Npc> npcList => [NpcFactory.MakeSpider(), NpcFactory.MakeRat(), NpcFactory.MakeSnake(), NpcFactory.MakeBat(), NpcFactory.MakeCaveBear()];

        private static readonly List<string> caveNames = ["Cave", "Cave", "Cavern", "Tunnel", "Mine", "Den", "Lair", "Hideout", "Ravine",];
        private static readonly List<string> caveAdjectives = ["", "Abandoned", "Collapsed", "Shallow", "Deep", "Echoing", "Ritual", "Painted", "Sparkling", "Dim", "Icy"];


    }
}
