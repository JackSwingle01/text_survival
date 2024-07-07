using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments.Locations
{
    internal class Trail : Location
    {
        new public const bool IsShelter = false;
        public Trail(IPlace parent, int numItems = 0, int numNpcs = 0) : base(parent, numItems, numNpcs)
        {
            Type = LocationType.Cave;
            TemperatureModifier = 0;

            var descriptors = new List<string>();
            descriptors.AddRange(trailAdjectives);
            descriptors.AddRange(genericLocationAdjectives);
            Name = Utils.GetRandomFromList(descriptors) + " " + Utils.GetRandomFromList(trailNames);
        }

        protected override List<Item> itemList => [ItemFactory.MakeRock(), ItemFactory.MakeStick(), ItemFactory.MakeBandage()];
        protected override List<Npc> npcList => [NpcFactory.MakeSnake()];

        private static readonly List<string> trailNames = ["Path", "Trail", "Pass"];
        private static readonly List<string> trailAdjectives = ["Dirt", "Gravel", "Stone", "Animal", "Hunter's", "Winding", "Straight", "Curved", "Twisting", "Bumpy", "Smooth", "Narrow", "Wide", "Long", "Short", "Steep", "Flat", "Sloping", "Rough", "Smooth", "Muddy",];

    }
}
