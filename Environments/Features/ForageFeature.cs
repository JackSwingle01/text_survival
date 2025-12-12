using text_survival.Core;
using text_survival.Items;

namespace text_survival.Environments.Features;

public class ForageFeature(Location location, double resourceDensity = 1) : LocationFeature("forage", location)
{
    private readonly double baseResourceDensity = resourceDensity;
    private double numberOfHoursForaged = 0;
    private DateTime lastForageTime = DateTime.MinValue;
    private readonly double respawnRateHours = 48.0; // Full respawn takes 48 hours
    private Dictionary<Func<Item>, double> resourceAbundance = [];

    private double ResourceDensity
    {
        get
        {
            // Calculate base depleted density
            double depletedDensity = baseResourceDensity / (numberOfHoursForaged + 1);

            // Calculate respawn recovery if time has passed
            if (lastForageTime != DateTime.MinValue && numberOfHoursForaged > 0)
            {
                double hoursElapsed = (World.GameTime - lastForageTime).TotalHours;
                double amountDepleted = baseResourceDensity - depletedDensity;
                double respawnProgress = (hoursElapsed / respawnRateHours) * amountDepleted;

                // EffectiveDensity = min(baseDensity, depletedDensity + respawnProgress)
                double effectiveDensity = Math.Min(baseResourceDensity, depletedDensity + respawnProgress);
                return effectiveDensity;
            }

            return depletedDensity;
        }
    }

    public List<Item> Forage(double hours)
    {
        List<Item> itemsFound = [];

        // Run foraging checks with time scaling (15 min = 25% of hourly odds)
        foreach (Func<Item> factory in resourceAbundance.Keys)
        {
            double baseChance = ResourceDensity * resourceAbundance[factory];
            double scaledChance = baseChance * hours; // Scale by time spent

            if (Utils.DetermineSuccess(scaledChance))
            {
                var item = factory();
                item.IsFound = true;
                itemsFound.Add(item);
            }
        }

        // Only deplete if items were actually found
        if (itemsFound.Count > 0)
        {
            numberOfHoursForaged += hours;
        }

        // Update last forage time
        lastForageTime = World.GameTime;

        int minutes = (int)(hours * 60);
        World.Update(minutes);

        return itemsFound;
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
