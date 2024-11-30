using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments.Zones
{
    internal class Forest : Zone
    {
        public Forest() : base("Forest", "")
        {
            BaseTemperature = 65;

            var descriptors = new List<string>();
            descriptors.AddRange(ForestAdjectives);
            descriptors.AddRange(genericAdjectives);
            Name = Utils.GetRandomFromList(descriptors) + " " + Utils.GetRandomFromList(ForestNames);
            Name = Name.Trim();
        }

        public override List<Item> ItemList => [ItemFactory.MakeBerry(), ItemFactory.MakeWater(), ItemFactory.MakeMushroom(), ItemFactory.MakeStick(), ItemFactory.MakeWood()]; // Add EdibleRoot
        public override List<Npc> NpcList => [NpcFactory.MakeWolf(), NpcFactory.MakeBear()];

        private static readonly List<string> ForestNames = ["Forest", "Clearing", "Grove", "Woods", "Hollow"];
        private static readonly List<string> ForestAdjectives = ["Old Growth", "Overgrown"];
    }
}
