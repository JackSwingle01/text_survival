using text_survival.IO;
using text_survival.Items;

namespace text_survival.Environments;

public class ForageFeature(Location location, double resourceDensity = 1) : LocationFeature("forage", location)
{
    private readonly double baseResourceDensity = resourceDensity;
    private int numberOfHoursForaged = 0;
    private Dictionary<Func<Item>, double> resourceAbundance = [];
    private double ResourceDensity => baseResourceDensity / (numberOfHoursForaged + 1);

    public void Forage(int hours)
    {
        List<Item> itemsFound = [];
        for (int i = 0; i < hours; i++)
        {
            foreach (Func<Item> factory in resourceAbundance.Keys)
            {
                double chance = ResourceDensity * resourceAbundance[factory];

                if (Utils.DetermineSuccess(chance))
                {
                    var item = factory();
                    Output.WriteLine("You found: ", item);
                    item.IsFound = true;
                    ParentLocation.Items.Add(item);
                }
            }
            numberOfHoursForaged++;
        }
        World.Update(hours * 60);
    }

    /// <summary>
    /// Adds a resource type that can be found when foraging at this location.
    /// </summary>
    /// <param name="factory">A function that creates new instances of the item when found</param>
    /// <param name="abundance">How common this resource is. With default resource density (1.0), 
    /// an abundance of 0.5 means a 50% chance of finding this item in the first hour of foraging.
    /// Values ≥ 1.0 typically result in guaranteed finds each hour (at least initially).
    /// The actual chance each hour = current ResourceDensity × abundance, so chances decrease 
    /// over time as the area becomes depleted from continued foraging.</param>
    public void AddResource(Func<Item> factory, double abundance)
    {
        resourceAbundance.Add(factory, abundance);
    }
}
