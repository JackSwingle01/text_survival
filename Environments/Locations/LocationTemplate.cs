using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments.Locations
{
    internal class LocationTemplate : Location
    {
        new public const bool IsShelter = false;
        public LocationTemplate(IPlace parent, int numItems = 0, int numNpcs = 0) : base(parent, numItems, numNpcs)
        {
            Type = LocationType.Cave;
            TemperatureModifier = 0;

            var descriptors = new List<string>();
            descriptors.AddRange(LocationTemplateAdjectives);
            descriptors.AddRange(genericLocationAdjectives);
            Name = Utils.GetRandomFromList(descriptors) + " " + Utils.GetRandomFromList(LocationTemplateNames);
            Name = Name.Trim();
        }

        protected override List<Item> itemList => [];
        protected override List<Npc> npcList => [];

        private static readonly List<string> LocationTemplateNames = ["LocationTemplate"];
        private static readonly List<string> LocationTemplateAdjectives = [""];
    }
}
