using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments.Locations
{
    internal class FrozenLake : Location
    {
        new public const bool IsShelter = false;
        public FrozenLake(IPlace parent, int numItems = 0, int numNpcs = 0) : base(parent, numItems, numNpcs)
        {
            Type = LocationType.FrozenLake;
            TemperatureModifier = -3;

            var descriptors = new List<string>();
            descriptors.AddRange(frozenLakeAdjectives);
            descriptors.AddRange(genericLocationAdjectives);
            Name = Utils.GetRandomFromList(descriptors) + " " + Utils.GetRandomFromList(frozenLakeNames);
            Name = Name.Trim();
        }

        protected new LootTable LootTable = new();

        private static readonly List<string> frozenLakeNames = ["Lake", "Pond", "Water"];
        private static readonly List<string> frozenLakeAdjectives = ["", "Shallow", "Deep", "Still", "Quiet", "Calm", "Rippling", "Misty", "Foggy", "Murky", "Dark", "Shimmering"];
    }
}
