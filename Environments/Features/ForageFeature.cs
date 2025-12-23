using text_survival.Items;

namespace text_survival.Environments.Features;

public enum ForageResourceType
{
    Log,
    Stick,
    Tinder,
    Berries,
    RawMeat,  // Small game found while foraging
    Water,
    Stone,        // For crafting tools
    PlantFiber,   // For bindings/cordage
    Bone          // For tools and crafting
}

public record ForageResource(ForageResourceType Type, double Abundance, double MinWeight, double MaxWeight);

public class ForageFeature(double resourceDensity = 1) : LocationFeature("forage")
{
    private readonly double baseResourceDensity = resourceDensity;
    private double numberOfHoursForaged = 0;
    private double hoursSinceLastForage = 0;
    private bool hasForagedBefore = false;
    private readonly double respawnRateHours = 48.0; // Full respawn takes 48 hours
    private readonly List<ForageResource> resources = [];
    private static readonly Random rng = new();

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
            double effectiveDensity = Math.Min(baseResourceDensity, depletedDensity + respawnProgress);
            return effectiveDensity;
        }

        return depletedDensity;
    }

    /// <summary>
    /// Forage for resources. Returns FoundResources with varying weights.
    /// </summary>
    public FoundResources Forage(double hours)
    {
        var found = new FoundResources();

        foreach (var resource in resources)
        {
            double baseChance = ResourceDensity() * resource.Abundance;
            double scaledChance = baseChance * hours;

            // Guaranteed finds from floor of scaledChance
            int guaranteedFinds = (int)Math.Floor(scaledChance);
            double remainder = scaledChance - guaranteedFinds;

            // Add guaranteed items
            for (int i = 0; i < guaranteedFinds; i++)
            {
                double weight = RandomWeight(resource.MinWeight, resource.MaxWeight);
                AddResourceToFound(found, resource.Type, weight);
            }

            // Roll for fractional remainder
            if (remainder > 0 && Utils.DetermineSuccess(remainder))
            {
                double weight = RandomWeight(resource.MinWeight, resource.MaxWeight);
                AddResourceToFound(found, resource.Type, weight);
            }
        }

        // Only deplete if resources were found
        if (!found.IsEmpty)
        {
            numberOfHoursForaged += hours;
            hoursSinceLastForage = 0;
            hasForagedBefore = true;
        }

        return found;
    }

    private static double RandomWeight(double min, double max)
    {
        return min + rng.NextDouble() * (max - min);
    }

    private static void AddResourceToFound(FoundResources found, ForageResourceType type, double weight)
    {
        switch (type)
        {
            case ForageResourceType.Log:
                found.AddLog(weight);
                break;
            case ForageResourceType.Stick:
                found.AddStick(weight);
                break;
            case ForageResourceType.Tinder:
                found.AddTinder(weight);
                break;
            case ForageResourceType.Berries:
                found.AddBerries(weight);
                break;
            case ForageResourceType.RawMeat:
                found.AddRawMeat(weight);
                break;
            case ForageResourceType.Water:
                found.AddWater(weight);
                break;
            case ForageResourceType.Stone:
                found.AddStone(weight);
                break;
            case ForageResourceType.PlantFiber:
                found.AddPlantFiber(weight);
                break;
            case ForageResourceType.Bone:
                found.AddBone(weight);
                break;
        }
    }

    /// <summary>
    /// Add a resource type that can be found when foraging.
    /// </summary>
    /// <param name="type">Type of resource</param>
    /// <param name="abundance">Chance to find per hour at full density (0.5 = 50% chance)</param>
    /// <param name="minWeight">Minimum weight when found</param>
    /// <param name="maxWeight">Maximum weight when found</param>
    public ForageFeature AddResource(ForageResourceType type, double abundance, double minWeight, double maxWeight)
    {
        resources.Add(new ForageResource(type, abundance, minWeight, maxWeight));
        return this;
    }

    // Convenience methods for common configurations
    public ForageFeature AddLogs(double abundance = 0.3, double minKg = 1.0, double maxKg = 3.0) =>
        AddResource(ForageResourceType.Log, abundance, minKg, maxKg);

    public ForageFeature AddSticks(double abundance = 0.6, double minKg = 0.1, double maxKg = 0.5) =>
        AddResource(ForageResourceType.Stick, abundance, minKg, maxKg);

    public ForageFeature AddTinder(double abundance = 0.4, double minKg = 0.02, double maxKg = 0.08) =>
        AddResource(ForageResourceType.Tinder, abundance, minKg, maxKg);

    public ForageFeature AddBerries(double abundance = 0.2, double minKg = 0.05, double maxKg = 0.2) =>
        AddResource(ForageResourceType.Berries, abundance, minKg, maxKg);

    public ForageFeature AddStone(double abundance = 0.3, double minKg = 0.2, double maxKg = 0.5) =>
        AddResource(ForageResourceType.Stone, abundance, minKg, maxKg);

    public ForageFeature AddPlantFiber(double abundance = 0.4, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource(ForageResourceType.PlantFiber, abundance, minKg, maxKg);

    public ForageFeature AddBone(double abundance = 0.2, double minKg = 0.1, double maxKg = 0.4) =>
        AddResource(ForageResourceType.Bone, abundance, minKg, maxKg);

    /// <summary>
    /// Get summary of what can be found here for display.
    /// </summary>
    public List<string> GetAvailableResourceTypes()
    {
        return resources.Select(r => r.Type switch
        {
            ForageResourceType.Log => "firewood",
            ForageResourceType.Stick => "kindling",
            ForageResourceType.Tinder => "tinder",
            ForageResourceType.Berries => "berries",
            ForageResourceType.RawMeat => "small game",
            ForageResourceType.Water => "water",
            ForageResourceType.Stone => "stone",
            ForageResourceType.PlantFiber => "plant fiber",
            ForageResourceType.Bone => "bones",
            _ => "resources"
        }).Distinct().ToList();
    }

    /// <summary>
    /// Get a description of the forage quality based on resource density.
    /// </summary>
    public string GetQualityDescription()
    {
        double density = ResourceDensity();
        return density switch
        {
            >= 0.8 => "abundant",
            >= 0.5 => "decent",
            >= 0.3 => "sparse",
            _ => "picked over"
        };
    }

    /// <summary>
    /// Deplete resources by simulating additional hours of foraging.
    /// Used by events that damage or consume location resources.
    /// </summary>
    public void Deplete(double hours)
    {
        numberOfHoursForaged += hours;
        hasForagedBefore = true;
    }

    /// <summary>
    /// Restore resources by simulating time passing without foraging.
    /// </summary>
    public void Restore(double hours)
    {
        hoursSinceLastForage += hours;
    }

    #region Save/Load Support

    /// <summary>
    /// Restore forage state from save data.
    /// </summary>
    internal void RestoreState(double hoursForaged, double hoursSinceForage, bool hasForaged, List<ForageResource> resourceList)
    {
        numberOfHoursForaged = hoursForaged;
        hoursSinceLastForage = hoursSinceForage;
        hasForagedBefore = hasForaged;
        resources.Clear();
        resources.AddRange(resourceList);
    }

    /// <summary>
    /// Get base resource density for save.
    /// </summary>
    internal double GetBaseResourceDensity() => baseResourceDensity;

    /// <summary>
    /// Get hours foraged for save.
    /// </summary>
    internal double GetNumberOfHoursForaged() => numberOfHoursForaged;

    /// <summary>
    /// Get hours since last forage for save.
    /// </summary>
    internal double GetHoursSinceLastForage() => hoursSinceLastForage;

    /// <summary>
    /// Get has foraged before flag for save.
    /// </summary>
    internal bool GetHasForagedBefore() => hasForagedBefore;

    /// <summary>
    /// Get resources list for save.
    /// </summary>
    internal IReadOnlyList<ForageResource> GetResources() => resources.AsReadOnly();

    #endregion
}
