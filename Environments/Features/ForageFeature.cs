using text_survival.Crafting;
using text_survival.Items;

namespace text_survival.Environments.Features;

public class ForageFeature(double resourceDensity = 1) : LocationFeature("forage")
{
    private readonly double baseResourceDensity = resourceDensity;
    private double numberOfHoursForaged = 0;
    private double hoursSinceLastForage = 0;
    private bool hasForagedBefore = false;
    private readonly double respawnRateHours = 48.0; // Full respawn takes 48 hours
    private Dictionary<Func<Item>, double> resourceAbundance = [];

    public override void Update(int minutes)
    {
        if (hasForagedBefore)
        {
            hoursSinceLastForage += minutes / 60.0;
        }
    }

    private double ResourceDensity()
    {
        // Calculate base depleted density
        double depletedDensity = baseResourceDensity / (numberOfHoursForaged + 1);

        // Calculate respawn recovery if time has passed
        if (hasForagedBefore && numberOfHoursForaged > 0)
        {
            double amountDepleted = baseResourceDensity - depletedDensity;
            double respawnProgress = (hoursSinceLastForage / respawnRateHours) * amountDepleted;
            // EffectiveDensity = min(baseDensity, depletedDensity + respawnProgress)
            double effectiveDensity = Math.Min(baseResourceDensity, depletedDensity + respawnProgress);
            return effectiveDensity;
        }

        return depletedDensity;
    }

    public List<Item> Forage(double hours)
    {
        List<Item> itemsFound = [];

        // Run foraging checks with time scaling (15 min = 25% of hourly odds)
        foreach (Func<Item> factory in resourceAbundance.Keys)
        {
            double baseChance = ResourceDensity() * resourceAbundance[factory];
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

        // Reset time since last forage
        hoursSinceLastForage = 0;
        hasForagedBefore = true;

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

    /// <summary>
    /// Returns the top ItemProperty tags by weighted abundance at this location.
    /// </summary>
    /// <param name="count">Number of top categories to return (default 3)</param>
    /// <returns>List of ItemProperty values sorted by total abundance</returns>
    public List<ItemProperty> GetTopCategories(int count = 3)
    {
        var propertyAbundance = new Dictionary<ItemProperty, double>();

        foreach (var (factory, abundance) in resourceAbundance)
        {
            var item = factory();
            foreach (var property in item.CraftingProperties)
            {
                if (!propertyAbundance.TryAdd(property, abundance))
                {
                    propertyAbundance[property] += abundance;
                }
            }
        }

        return propertyAbundance
            .OrderByDescending(kvp => kvp.Value)
            .Take(count)
            .Select(kvp => kvp.Key)
            .ToList();
    }
}