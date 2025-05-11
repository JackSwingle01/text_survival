using text_survival.Items;

namespace text_survival.Environments
{
    public class ForageModule(double resourceDensity = 1)
    {
        private double baseResourceDensity = resourceDensity;
        private int numberOfHoursForaged = 0;
        private List<Func<Item>> resources => [.. resourceRarities.Keys];
        private Dictionary<Func<Item>, double> resourceRarities = [];
        private double ResourceDensity => baseResourceDensity / (numberOfHoursForaged + 1);
        private List<Item> itemsFound = [];

        public void Forage(int hours)
        {
            foreach (Func<Item> factory in resources)
            {
                double chance = ResourceDensity * resourceRarities[factory];
               
                for (int i = 0; i < hours; i++)
                {
                    if (Utils.DetermineSuccess(chance))
                    {
                        itemsFound.Add(factory());
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

        public void AddResource(Func<Item> factory, double rarity)
        {
            // todo switch to factory methods instead of the items themselves like the location loot tables
            resourceRarities.Add(factory, rarity);
        }
    }
}
