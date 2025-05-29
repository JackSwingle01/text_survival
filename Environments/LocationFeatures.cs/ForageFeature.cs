using text_survival.IO;
using text_survival.Items;

namespace text_survival.Environments;

public class ForageFeature(Location location, double resourceDensity = 1) : LocationFeature("forage", location)
{
    private double baseResourceDensity = resourceDensity;
    private int numberOfHoursForaged = 0;
    private Dictionary<Func<Item>, double> resourceRarities = [];
    private double ResourceDensity => baseResourceDensity / (numberOfHoursForaged + 1);

    public void Forage(int hours)
    {
        // todo: change the order of operations here
        List<Item> itemsFound = [];
        foreach (Func<Item> factory in resourceRarities.Keys)
        {
            double chance = ResourceDensity * resourceRarities[factory];

            for (int i = 0; i < hours; i++)
            {
                if (Utils.DetermineSuccess(chance))
                {
                    var item = factory();
                    Output.Write("You found: ", item);
                    item.IsFound = true;
                    ParentLocation.Items.Add(item);
                    numberOfHoursForaged++;
                }
            }
        }
        World.Update(hours * 60);
    }

    public void AddResource(Func<Item> factory, double rarity)
    {
        resourceRarities.Add(factory, rarity);
    }
}
