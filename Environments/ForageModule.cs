using text_survival.Items;

namespace text_survival.Environments
{
    public class ForageModule(double resourceDensity = 1)
    {
        private double baseResourceDensity = resourceDensity;
        private int numberOfHoursForaged = 0;
        private List<Item> resources => [.. resourceRarities.Keys];
        private Dictionary<Item, double> resourceRarities = [];
        private double ResourceDensity => baseResourceDensity / (numberOfHoursForaged + 1);
        private List<Item> itemsFound = [];

        public void Forage(int hours)
        {
            foreach (Item item in resources)
            {
                double chance = ResourceDensity * resourceRarities[item];
               
                for (int i = 0; i < hours; i++)
                {
                    if (Utils.DetermineSucess(chance))
                    {
                        itemsFound.Add(item.Clone());
                        numberOfHoursForaged++;
                    }
                }
            }
            World.Update(hours * 60);
        }
        public List<Item> GetItemsFound()
        {
            var items = itemsFound;
            itemsFound = [];
            return items;
        }

        public void AddResource(Item item, double rarity)
        {
            resourceRarities.Add(item, rarity);
        }
    }
}
