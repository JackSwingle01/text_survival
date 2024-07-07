using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments.Locations
{
    internal class River : Location
    {
        new public const bool IsShelter = false;
        public River(IPlace parent, int numItems = 0, int numNpcs = 0) : base(parent, numItems, numNpcs)
        {
            Type = LocationType.Cave;
            TemperatureModifier = 0;

            var descriptors = new List<string>();
            descriptors.AddRange(riverAdjectives);
            descriptors.AddRange(genericLocationAdjectives);
            Name = Utils.GetRandomFromList(descriptors) + " " + Utils.GetRandomFromList(riverNames);
        }

        protected override List<Item> itemList => [ItemFactory.MakeWater(), ItemFactory.MakeFish()];
        protected override List<Npc> npcList => [];

        private static readonly List<string> riverNames =
        [
            "River", "Stream", "Creek", "Waterfall", "Brook",
             "Rapids",
        ];

        private static readonly List<string> riverAdjectives = ["", "Shallow", "Deep", "Still", "Quiet", "Calm", "Rippling", "Misty", "Foggy", "Murky", "Dark", "Shimmering", "Quick", "Loud", "Slow", "Lazy"];
    }
}
