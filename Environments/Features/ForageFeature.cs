using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Configuration for a forageable resource. Uses delegates for type-safe resource addition.
/// </summary>
public record ForageResource(
    string Name,
    Action<Inventory, double> AddToInventory,
    double Abundance,
    double MinWeight,
    double MaxWeight);

public class ForageFeature(double resourceDensity = 1) : LocationFeature("forage")
{
    private readonly double respawnRateHours = 48.0; // Full respawn takes 48 hours
    private readonly List<ForageResource> resources = [];
    private static readonly Random rng = new();

    internal double BaseResourceDensity { get; private set; } = resourceDensity;
    internal double NumberOfHoursForaged { get; private set; } = 0;
    internal double HoursSinceLastForage { get; private set; } = 0;
    internal bool HasForagedBefore { get; private set; } = false;

    public override void Update(int minutes)
    {
        if (HasForagedBefore)
        {
            HoursSinceLastForage += minutes / 60.0;
        }
    }

    private double ResourceDensity()
    {
        // Calculate base depleted density
        double depletedDensity = BaseResourceDensity / (NumberOfHoursForaged + 1);

        // Calculate respawn recovery if time has passed
        if (HasForagedBefore && NumberOfHoursForaged > 0)
        {
            double amountDepleted = BaseResourceDensity - depletedDensity;
            double respawnProgress = (HoursSinceLastForage / respawnRateHours) * amountDepleted;
            double effectiveDensity = Math.Min(BaseResourceDensity, depletedDensity + respawnProgress);
            return effectiveDensity;
        }

        return depletedDensity;
    }

    /// <summary>
    /// Forage for resources. Returns Inventory with found items.
    /// </summary>
    public Inventory Forage(double hours)
    {
        var found = new Inventory();

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
                resource.AddToInventory(found, weight);
            }

            // Roll for fractional remainder
            if (remainder > 0 && Utils.DetermineSuccess(remainder))
            {
                double weight = RandomWeight(resource.MinWeight, resource.MaxWeight);
                resource.AddToInventory(found, weight);
            }
        }

        // Only deplete if resources were found
        if (!found.IsEmpty)
        {
            NumberOfHoursForaged += hours;
            HoursSinceLastForage = 0;
            HasForagedBefore = true;
        }

        return found;
    }

    private static double RandomWeight(double min, double max)
    {
        return min + rng.NextDouble() * (max - min);
    }

    /// <summary>
    /// Add a resource type that can be found when foraging.
    /// </summary>
    public ForageFeature AddResource(string name, Action<Inventory, double> addToInventory, double abundance, double minWeight, double maxWeight)
    {
        resources.Add(new ForageResource(name, addToInventory, abundance, minWeight, maxWeight));
        return this;
    }

    // Convenience methods for common configurations
    public ForageFeature AddLogs(double abundance = 0.3, double minKg = 1.0, double maxKg = 3.0) =>
        AddResource("firewood", (inv, w) => inv.Logs.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddSticks(double abundance = 0.6, double minKg = 0.1, double maxKg = 0.5) =>
        AddResource("kindling", (inv, w) => inv.Sticks.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddTinder(double abundance = 0.4, double minKg = 0.02, double maxKg = 0.08) =>
        AddResource("tinder", (inv, w) => inv.Tinder.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddBerries(double abundance = 0.2, double minKg = 0.05, double maxKg = 0.2) =>
        AddResource("berries", (inv, w) => inv.Berries.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddStone(double abundance = 0.3, double minKg = 0.2, double maxKg = 0.5) =>
        AddResource("stone", (inv, w) => inv.Stone.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddPlantFiber(double abundance = 0.4, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("plant fiber", (inv, w) => inv.PlantFiber.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddBone(double abundance = 0.2, double minKg = 0.1, double maxKg = 0.4) =>
        AddResource("bones", (inv, w) => inv.Bone.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddSmallGame(double abundance = 0.1, double minKg = 0.2, double maxKg = 0.5) =>
        AddResource("small game", (inv, w) => inv.RawMeat.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddWater(double abundance = 0.5, double minLiters = 0.2, double maxLiters = 0.5) =>
        AddResource("water", (inv, w) => inv.WaterLiters += w, abundance, minLiters, maxLiters);

    // Stone type convenience methods
    public ForageFeature AddShale(double abundance = 0.2, double minKg = 0.2, double maxKg = 0.5) =>
        AddResource("shale", (inv, w) => inv.Shale.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddFlint(double abundance = 0.1, double minKg = 0.1, double maxKg = 0.3) =>
        AddResource("flint", (inv, w) => inv.Flint.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddPyrite(double abundance = 0.05, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("pyrite", (inv, w) => inv.Pyrite += w, abundance, minKg, maxKg);

    // Wood type convenience methods
    public ForageFeature AddPine(double abundance = 0.3, double minKg = 1.0, double maxKg = 3.0) =>
        AddResource("pine", (inv, w) => inv.Pine.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddBirch(double abundance = 0.3, double minKg = 1.0, double maxKg = 3.0) =>
        AddResource("birch", (inv, w) => inv.Birch.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddOak(double abundance = 0.2, double minKg = 1.5, double maxKg = 4.0) =>
        AddResource("oak", (inv, w) => inv.Oak.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddBirchBark(double abundance = 0.3, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("birch bark", (inv, w) => inv.BirchBark.Push(w), abundance, minKg, maxKg);

    // Fungi convenience methods (year-round on trees)
    public ForageFeature AddBirchPolypore(double abundance = 0.1, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("birch polypore", (inv, w) => inv.BirchPolypore.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddChaga(double abundance = 0.08, double minKg = 0.05, double maxKg = 0.2) =>
        AddResource("chaga", (inv, w) => inv.Chaga.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddAmadou(double abundance = 0.1, double minKg = 0.02, double maxKg = 0.08) =>
        AddResource("amadou", (inv, w) => inv.Amadou.Push(w), abundance, minKg, maxKg);

    // Persistent plant convenience methods (winter-available)
    public ForageFeature AddRoseHips(double abundance = 0.2, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("rose hips", (inv, w) => inv.RoseHips.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddJuniperBerries(double abundance = 0.15, double minKg = 0.02, double maxKg = 0.08) =>
        AddResource("juniper berries", (inv, w) => inv.JuniperBerries.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddWillowBark(double abundance = 0.15, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("willow bark", (inv, w) => inv.WillowBark.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddPineNeedles(double abundance = 0.3, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("pine needles", (inv, w) => inv.PineNeedles.Push(w), abundance, minKg, maxKg);

    // Tree product convenience methods
    public ForageFeature AddPineResin(double abundance = 0.1, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("pine resin", (inv, w) => inv.PineResin.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddUsnea(double abundance = 0.15, double minKg = 0.02, double maxKg = 0.08) =>
        AddResource("usnea", (inv, w) => inv.Usnea.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddSphagnum(double abundance = 0.2, double minKg = 0.1, double maxKg = 0.3) =>
        AddResource("sphagnum", (inv, w) => inv.Sphagnum.Push(w), abundance, minKg, maxKg);

    // Food expansion convenience methods
    public ForageFeature AddNuts(double abundance = 0.2, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("nuts", (inv, w) => inv.Nuts.Push(w), abundance, minKg, maxKg);

    public ForageFeature AddRoots(double abundance = 0.15, double minKg = 0.1, double maxKg = 0.3) =>
        AddResource("roots", (inv, w) => inv.Roots.Push(w), abundance, minKg, maxKg);

    // Raw material convenience methods (usually from processing, but can be foraged)
    public ForageFeature AddRawFiber(double abundance = 0.3, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("raw fiber", (inv, w) => inv.RawFiber.Push(w), abundance, minKg, maxKg);

    /// <summary>
    /// Get summary of what can be found here for display.
    /// </summary>
    public List<string> GetAvailableResourceTypes()
    {
        return resources.Select(r => r.Name).Distinct().ToList();
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
        NumberOfHoursForaged += hours;
        HasForagedBefore = true;
    }

    /// <summary>
    /// Restore resources by simulating time passing without foraging.
    /// </summary>
    public void Restore(double hours)
    {
        HoursSinceLastForage += hours;
    }

    #region Save/Load Support

    /// <summary>
    /// Restore forage state from save data.
    /// </summary>
    internal void RestoreState(double hoursForaged, double hoursSinceForage, bool hasForaged, List<ForageResource> resourceList)
    {
        NumberOfHoursForaged = hoursForaged;
        HoursSinceLastForage = hoursSinceForage;
        HasForagedBefore = hasForaged;
        resources.Clear();
        resources.AddRange(resourceList);
    }

    // Collection needs backing field for mutation
    internal IReadOnlyList<ForageResource> Resources => resources.AsReadOnly();

    #endregion
}
