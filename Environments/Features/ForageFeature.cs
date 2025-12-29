using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actions.Variants;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Configuration for a forageable resource.
/// </summary>
public record ForageResource(
    string Name,
    Resource ResourceType,  // Serializable enum instead of delegate
    double Abundance,
    double MinWeight,
    double MaxWeight);

public class ForageFeature : LocationFeature, IWorkableFeature
{
    private readonly double respawnRateHours = 168.0; // Full respawn takes 1 week
    [System.Text.Json.Serialization.JsonInclude]
    private List<ForageResource> _resources = [];
    private static readonly Random rng = new();

    internal double BaseResourceDensity { get; set; } = 1;
    internal double NumberOfHoursForaged { get; set; } = 0;
    internal double HoursSinceLastForage { get; set; } = 0;

    /// <summary>
    /// Seed for deterministic clue generation. Changes after foraging or "keep walking".
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    internal int ClueSeed { get; private set; } = Random.Shared.Next();

    public ForageFeature(double resourceDensity = 1) : base("forage")
    {
        BaseResourceDensity = resourceDensity;
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public ForageFeature() : base("forage") { }

    // Derived from NumberOfHoursForaged - no need to track separately
    private bool HasForagedBefore => NumberOfHoursForaged > 0;

    public override void Update(FeatureUpdateContext ctx)
    {
        if (HasForagedBefore)
        {
            HoursSinceLastForage += ctx.Minutes / 60.0;
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

        foreach (var resource in _resources)
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
                found.Add(resource.ResourceType, weight);
            }

            // Roll for fractional remainder
            if (remainder > 0 && Utils.DetermineSuccess(remainder))
            {
                double weight = RandomWeight(resource.MinWeight, resource.MaxWeight);
                found.Add(resource.ResourceType, weight);
            }
        }

        // Only deplete if resources were found
        if (!found.IsEmpty)
        {
            NumberOfHoursForaged += hours;
            HoursSinceLastForage = 0;
        }

        // Regenerate clue seed for next forage attempt
        ClueSeed = Random.Shared.Next();

        return found;
    }

    /// <summary>
    /// Reroll the clue seed (used by "keep walking" option).
    /// </summary>
    public void RerollClues() => ClueSeed = Random.Shared.Next();

    private static double RandomWeight(double min, double max)
    {
        return min + rng.NextDouble() * (max - min);
    }

    /// <summary>
    /// Add a resource type that can be found when foraging.
    /// </summary>
    public ForageFeature AddResource(string name, Resource resourceType, double abundance, double minWeight, double maxWeight)
    {
        _resources.Add(new ForageResource(name, resourceType, abundance, minWeight, maxWeight));
        return this;
    }

    /// <summary>
    /// Check if this feature has any resources matching a forage focus.
    /// </summary>
    public bool HasResourcesForFocus(ForageFocus focus) => focus switch
    {
        ForageFocus.Fuel => _resources.Any(r => r.ResourceType.IsFuel()),
        ForageFocus.Food => _resources.Any(r => r.ResourceType.IsFood()),
        ForageFocus.Medicine => _resources.Any(r => r.ResourceType.IsMedicine()),
        ForageFocus.Materials => _resources.Any(r => r.ResourceType.IsMaterial()),
        ForageFocus.General => true,
        _ => true
    };

    /// <summary>
    /// Map individual resources to sub-category names for cleaner UI display.
    /// </summary>
    private static string GetSubCategory(Resource resource) => resource switch
    {
        // Medicine
        Resource.BirchPolypore or Resource.Chaga or Resource.Amadou => "fungi",
        Resource.WillowBark => "bark",
        Resource.RoseHip or Resource.JuniperBerry => "berries",
        Resource.PineResin => "tree resin",
        Resource.Usnea => "lichen",
        Resource.SphagnumMoss => "moss",
        Resource.PineNeedles => "needles",

        // Materials
        Resource.Stone or Resource.Shale or Resource.Flint or Resource.Pyrite => "stone",
        Resource.PlantFiber or Resource.RawFiber or Resource.Rope => "fiber",
        Resource.Bone or Resource.Hide or Resource.ScrapedHide or Resource.CuredHide
            or Resource.Sinew or Resource.RawFat or Resource.Tallow
            or Resource.Ivory or Resource.MammothHide => "animal parts",
        Resource.Charcoal => "charcoal",

        // Food
        Resource.RawMeat or Resource.CookedMeat or Resource.DriedMeat => "meat",
        Resource.Berries or Resource.DriedBerries => "berries",
        Resource.Nuts => "nuts",
        Resource.Roots => "roots",
        Resource.Honey => "honey",

        // Fuel
        Resource.Stick or Resource.Tinder => "kindling",
        Resource.Pine or Resource.Birch or Resource.Oak => "wood",
        Resource.BirchBark => "bark",
        Resource.Peat => "peat",

        _ => resource.ToString().ToLower()
    };

    /// <summary>
    /// Get a description of available resources for a focus category.
    /// Returns comma-separated sub-category names (e.g., "stone, fiber").
    /// </summary>
    public string GetFocusDescription(ForageFocus focus)
    {
        var matchingResources = focus switch
        {
            ForageFocus.Fuel => _resources.Where(r => r.ResourceType.IsFuel()),
            ForageFocus.Food => _resources.Where(r => r.ResourceType.IsFood()),
            ForageFocus.Medicine => _resources.Where(r => r.ResourceType.IsMedicine()),
            ForageFocus.Materials => _resources.Where(r => r.ResourceType.IsMaterial()),
            ForageFocus.General => _resources,
            _ => _resources
        };

        var subCategories = matchingResources
            .Select(r => GetSubCategory(r.ResourceType))
            .Distinct()
            .ToList();

        return subCategories.Count == 0 ? "" : string.Join(", ", subCategories);
    }

    // Convenience methods for common configurations

    /// <summary>
    /// Add mixed wood types for generic forest areas.
    /// Distributes abundance across Pine (40%), Birch (35%), Oak (25%).
    /// </summary>
    public ForageFeature AddMixedWood(double abundance = 0.3, double minKg = 1.0, double maxKg = 3.0)
    {
        AddPine(abundance * 0.4, minKg, maxKg);
        AddBirch(abundance * 0.35, minKg, maxKg);
        AddOak(abundance * 0.25, minKg, maxKg);
        return this;
    }

    public ForageFeature AddSticks(double abundance = 0.6, double minKg = 0.1, double maxKg = 0.5) =>
        AddResource("kindling", Resource.Stick, abundance, minKg, maxKg);

    public ForageFeature AddTinder(double abundance = 0.4, double minKg = 0.02, double maxKg = 0.08) =>
        AddResource("tinder", Resource.Tinder, abundance, minKg, maxKg);

    public ForageFeature AddBerries(double abundance = 0.2, double minKg = 0.05, double maxKg = 0.2) =>
        AddResource("berries", Resource.Berries, abundance, minKg, maxKg);

    public ForageFeature AddStone(double abundance = 0.3, double minKg = 0.2, double maxKg = 0.5) =>
        AddResource("stone", Resource.Stone, abundance, minKg, maxKg);
    public ForageFeature AddPlantFiber(double abundance = 0.4, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("plant fiber", Resource.PlantFiber, abundance, minKg, maxKg);

    public ForageFeature AddBone(double abundance = 0.2, double minKg = 0.1, double maxKg = 0.4) =>
        AddResource("bones", Resource.Bone, abundance, minKg, maxKg);

    public ForageFeature AddSmallGame(double abundance = 0.1, double minKg = 0.2, double maxKg = 0.5) =>
        AddResource("small game", Resource.RawMeat, abundance, minKg, maxKg);

    // Note: Water uses special handling - adds directly to WaterLiters
    // We'll need to handle this specially in Forage() method or create a Water Resource type
    public ForageFeature AddWater(double abundance = 0.5, double minLiters = 0.2, double maxLiters = 0.5) =>
        AddResource("water", Resource.RawMeat, abundance, minLiters, maxLiters);  // TODO: Create Water resource or special handling

    // Stone type convenience methods
    public ForageFeature AddShale(double abundance = 0.2, double minKg = 0.2, double maxKg = 0.5) =>
        AddResource("shale", Resource.Shale, abundance, minKg, maxKg);

    public ForageFeature AddFlint(double abundance = 0.1, double minKg = 0.1, double maxKg = 0.3) =>
        AddResource("flint", Resource.Flint, abundance, minKg, maxKg);

    public ForageFeature AddPyrite(double abundance = 0.05, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("pyrite", Resource.Pyrite, abundance, minKg, maxKg);

    // Wood type convenience methods
    public ForageFeature AddPine(double abundance = 0.3, double minKg = 1.0, double maxKg = 3.0) =>
        AddResource("pine", Resource.Pine, abundance, minKg, maxKg);
    public ForageFeature AddBirch(double abundance = 0.3, double minKg = 1.0, double maxKg = 3.0) =>
        AddResource("birch", Resource.Birch, abundance, minKg, maxKg);

    public ForageFeature AddOak(double abundance = 0.2, double minKg = 1.5, double maxKg = 4.0) =>
        AddResource("oak", Resource.Oak, abundance, minKg, maxKg);

    public ForageFeature AddBirchBark(double abundance = 0.3, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("birch bark", Resource.BirchBark, abundance, minKg, maxKg);

    // Fungi convenience methods (year-round on trees)
    public ForageFeature AddBirchPolypore(double abundance = 0.1, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("birch polypore", Resource.BirchPolypore, abundance, minKg, maxKg);

    public ForageFeature AddChaga(double abundance = 0.08, double minKg = 0.05, double maxKg = 0.2) =>
        AddResource("chaga", Resource.Chaga, abundance, minKg, maxKg);

    public ForageFeature AddAmadou(double abundance = 0.1, double minKg = 0.02, double maxKg = 0.08) =>
        AddResource("amadou", Resource.Amadou, abundance, minKg, maxKg);

    // Persistent plant convenience methods (winter-available)
    public ForageFeature AddRoseHips(double abundance = 0.2, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("rose hips", Resource.RoseHip, abundance, minKg, maxKg);

    public ForageFeature AddJuniperBerries(double abundance = 0.15, double minKg = 0.02, double maxKg = 0.08) =>
        AddResource("juniper berries", Resource.JuniperBerry, abundance, minKg, maxKg);

    public ForageFeature AddWillowBark(double abundance = 0.15, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("willow bark", Resource.WillowBark, abundance, minKg, maxKg);

    public ForageFeature AddPineNeedles(double abundance = 0.3, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("pine needles", Resource.PineNeedles, abundance, minKg, maxKg);

    // Tree product convenience methods
    public ForageFeature AddPineResin(double abundance = 0.1, double minKg = 0.02, double maxKg = 0.1) =>
        AddResource("pine resin", Resource.PineResin, abundance, minKg, maxKg);

    public ForageFeature AddUsnea(double abundance = 0.15, double minKg = 0.02, double maxKg = 0.08) =>
        AddResource("usnea", Resource.Usnea, abundance, minKg, maxKg);

    public ForageFeature AddSphagnum(double abundance = 0.2, double minKg = 0.1, double maxKg = 0.3) =>
        AddResource("sphagnum", Resource.SphagnumMoss, abundance, minKg, maxKg);

    // Food expansion convenience methods
    public ForageFeature AddNuts(double abundance = 0.2, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("nuts", Resource.Nuts, abundance, minKg, maxKg);

    public ForageFeature AddRoots(double abundance = 0.15, double minKg = 0.1, double maxKg = 0.3) =>
        AddResource("roots", Resource.Roots, abundance, minKg, maxKg);

    // Raw material convenience methods (usually from processing, but can be foraged)
    public ForageFeature AddRawFiber(double abundance = 0.3, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("raw fiber", Resource.RawFiber, abundance, minKg, maxKg);

    // Fire remnant convenience methods
    public ForageFeature AddCharcoal(double abundance = 0.2, double minKg = 0.05, double maxKg = 0.15) =>
        AddResource("charcoal", Resource.Charcoal, abundance, minKg, maxKg);

    /// <summary>
    /// Get summary of what can be found here for display.
    /// </summary>
    public List<string> GetAvailableResourceTypes()
    {
        return _resources.Select(r => r.Name).Distinct().ToList();
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
    /// Check if this forage area has fuel resources (logs, sticks, tinder).
    /// Used by events that care about fuel availability.
    /// </summary>
    public bool HasFuelResources()
    {
        var fuelResources = ResourceCategories.Items[ResourceCategory.Fuel];
        return _resources.Any(r => fuelResources.Contains(r.ResourceType));
    }

    /// <summary>
    /// Check if this forage area is depleted (density below useful threshold).
    /// </summary>
    public bool IsDepleted() => ResourceDensity() < 0.1;

    /// <summary>
    /// Check if this forage area is nearly depleted.
    /// </summary>
    public bool IsNearlyDepleted() => ResourceDensity() < 0.3;

    /// <summary>
    /// Check if foraging can be productive here.
    /// </summary>
    public bool CanForage() => ResourceDensity() >= 0.1;

    /// <summary>
    /// Get work options for this feature.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (!CanForage()) yield break;
        yield return new WorkOption(
            $"Forage ({GetQualityDescription()})",
            "forage",
            new ForageStrategy()
        );
    }

    /// <summary>
    /// Deplete resources by simulating additional hours of foraging.
    /// Used by events that damage or consume location resources.
    /// </summary>
    public void Deplete(double hours)
    {
        NumberOfHoursForaged += hours;
    }

    /// <summary>
    /// Restore resources by simulating time passing without foraging.
    /// </summary>
    public void Restore(double hours)
    {
        HoursSinceLastForage += hours;
    }

    public override FeatureUIInfo? GetUIInfo()
    {
        if (!CanForage()) return null;
        return new FeatureUIInfo(
            "forage",
            "Foraging",
            GetQualityDescription(),
            GetAvailableResourceTypes());
    }

    #region Save/Load Support

    // Collection needs backing field for mutation
    // JsonIgnore prevents serializer from using this property instead of the private field
    [System.Text.Json.Serialization.JsonIgnore]
    internal IReadOnlyList<ForageResource> Resources => _resources.AsReadOnly();

    #endregion
}
