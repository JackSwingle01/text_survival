using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Tool tier levels for tool-gated harvesting.
/// </summary>
public enum ToolTier
{
    None = 0,       // No tool needed
    Improvised = 1, // Sharp rock, basic tools
    Basic = 2,      // Stone knife, hand axe
    Quality = 3     // Hafted axe, quality knife
}

/// <summary>
/// Represents a discoverable resource node that can be harvested.
/// Unlike ForageFeature (RNG-based searching), HarvestableFeature is a visible,
/// quantity-based resource that respawns over time.
/// Examples: berry bushes, deadfall pile, water source, willow stand
/// </summary>
public class HarvestableFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => CanBeHarvested() ? "harvest" : null;
    public override int IconPriority => 1;

    [System.Text.Json.Serialization.JsonInclude]
    private string _displayName = "";

    public string DisplayName => _displayName;
    public string Description { get; set; } = "";
    public bool IsDiscovered { get; set; } = true; // Start discovered for v1

    /// <summary>
    /// Minutes of work required to complete one harvest cycle.
    /// Each cycle yields one unit of each non-depleted resource.
    /// </summary>
    public int MinutesToHarvest { get; set; } = 5;

    // Tool requirements for gated harvesting
    /// <summary>
    /// The type of tool required to harvest (null = no tool needed).
    /// </summary>
    public ToolType? RequiredToolType { get; set; }

    /// <summary>
    /// Minimum tool tier required (Quality > Basic > Improvised > None).
    /// </summary>
    public ToolTier RequiredToolTier { get; set; } = ToolTier.None;

    [System.Text.Json.Serialization.JsonInclude]
    private int _minutesWorked = 0;
    [System.Text.Json.Serialization.JsonInclude]
    private List<HarvestableResource> _resources = [];

    [System.Text.Json.Serialization.JsonConstructor]
    public HarvestableFeature() : base("harvestable") { }

    public HarvestableFeature(string name, string displayName)
        : base(name)
    {
        _displayName = displayName;
    }

    /// <summary>
    /// Advance respawn timers for all depleted resources.
    /// </summary>
    public override void Update(int minutes)
    {
        double hours = minutes / 60.0;

        foreach (var resource in _resources)
        {
            if (resource.CurrentQuantity < resource.MaxQuantity)
            {
                resource.RespawnProgressHours += hours;

                while (resource.RespawnProgressHours >= resource.RespawnHoursPerUnit &&
                       resource.CurrentQuantity < resource.MaxQuantity)
                {
                    resource.RespawnProgressHours -= resource.RespawnHoursPerUnit;
                    resource.CurrentQuantity++;
                }
            }
        }
    }

    /// <summary>
    /// Add a harvestable resource to this feature.
    /// </summary>
    /// <param name="displayName">Name for display (e.g., "berries", "dry wood")</param>
    /// <param name="resourceType">Resource enum to add to inventory</param>
    /// <param name="maxQuantity">Maximum available quantity</param>
    /// <param name="weightPerUnit">Weight in kg per unit harvested</param>
    /// <param name="respawnHoursPerUnit">Hours required to respawn one unit</param>
    /// <param name="isWater">True if this adds to WaterLiters instead of resources</param>
    public HarvestableFeature AddResource(string displayName, Resource resourceType,
        int maxQuantity, double weightPerUnit, double respawnHoursPerUnit, bool isWater = false)
    {
        _resources.Add(new HarvestableResource
        {
            DisplayName = displayName,
            ResourceType = resourceType,
            IsWater = isWater,
            MaxQuantity = maxQuantity,
            CurrentQuantity = maxQuantity,
            WeightPerUnit = weightPerUnit,
            RespawnHoursPerUnit = respawnHoursPerUnit
        });
        return this;
    }

    // Convenience methods for common resource types
    public HarvestableFeature AddLogs(string displayName, Resource woodType, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit)
    {
        if (woodType != Resource.Pine && woodType != Resource.Birch && woodType != Resource.Oak)
            throw new ArgumentException($"Wood type must be Pine, Birch, or Oak, got {woodType}");
        return AddResource(displayName, woodType, maxQuantity, weightPerUnit, respawnHoursPerUnit);
    }

    public HarvestableFeature AddSticks(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, Resource.Stick, maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddTinder(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, Resource.Tinder, maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddBerries(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, Resource.Berries, maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddWater(string displayName, int maxQuantity, double litersPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, Resource.Stick, maxQuantity, litersPerUnit, respawnHoursPerUnit, isWater: true);  // ResourceType unused for water

    public HarvestableFeature AddPlantFiber(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, Resource.PlantFiber, maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddBone(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, Resource.Bone, maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddStone(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, Resource.Stone, maxQuantity, weightPerUnit, respawnHoursPerUnit);

    // Builder methods for tool requirements
    /// <summary>
    /// Require a specific tool type to harvest this resource.
    /// </summary>
    public HarvestableFeature RequiresTool(ToolType toolType, ToolTier tier = ToolTier.Basic)
    {
        RequiredToolType = toolType;
        RequiredToolTier = tier;
        return this;
    }

    /// <summary>
    /// Check if a tool meets the harvesting requirements.
    /// </summary>
    public bool MeetsToolRequirement(Gear? tool)
    {
        if (RequiredToolType == null && RequiredToolTier == ToolTier.None)
            return true; // No requirement

        if (tool == null)
            return false;

        if (RequiredToolType != null && tool.ToolType != RequiredToolType.Value)
            return false;

        // For now, assume any non-broken tool meets tier requirements
        // TODO: Add tool quality/tier system when implementing full Tier 2
        return !tool.IsBroken;
    }

    /// <summary>
    /// Get a description of what tool is needed.
    /// </summary>
    public string GetToolRequirementDescription()
    {
        if (RequiredToolType == null && RequiredToolTier == ToolTier.None)
            return "";

        string tierDesc = RequiredToolTier switch
        {
            ToolTier.Quality => "quality ",
            ToolTier.Basic => "",
            ToolTier.Improvised => "improvised ",
            _ => ""
        };

        string toolDesc = RequiredToolType?.ToString().ToLower() ?? "tool";
        return $"Requires {tierDesc}{toolDesc}";
    }

    /// <summary>
    /// Check if any resources are currently available to harvest
    /// </summary>
    public bool HasAvailableResources()
    {
        return _resources.Any(r => r.CurrentQuantity > 0);
    }

    /// <summary>
    /// Check if this feature can be harvested right now.
    /// Combines discovery state and resource availability.
    /// </summary>
    public bool CanBeHarvested() => IsDiscovered && HasAvailableResources();

    /// <summary>
    /// Check if all resources are depleted.
    /// </summary>
    public bool IsDepleted() => _resources.Count > 0 && _resources.All(r => r.CurrentQuantity == 0);

    /// <summary>
    /// Check if resources are nearly depleted (less than 25% remaining).
    /// </summary>
    public bool IsNearlyDepleted()
    {
        if (_resources.Count == 0) return false;
        int current = _resources.Sum(r => r.CurrentQuantity);
        int max = _resources.Sum(r => r.MaxQuantity);
        return max > 0 && current < max * 0.25;
    }

    /// <summary>
    /// Get work options for this feature.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (!CanBeHarvested()) yield break;

        string toolHint = RequiredToolType != null
            ? $" ({GetToolRequirementDescription()})"
            : "";

        yield return new WorkOption(
            $"Harvest {DisplayName}{toolHint}",
            "harvest",
            new HarvestStrategy()
        );
    }

    /// <summary>
    /// Work for the specified minutes, yielding resources when harvest cycles complete.
    /// Each completed cycle drops one of each non-depleted resource.
    /// </summary>
    /// <param name="minutes">Minutes of work to perform</param>
    /// <returns>Inventory with harvested items</returns>
    public Inventory Harvest(int minutes)
    {
        var found = new Inventory();

        _minutesWorked += minutes;

        while (_minutesWorked >= MinutesToHarvest && HasAvailableResources())
        {
            _minutesWorked -= MinutesToHarvest;

            foreach (var resource in _resources)
            {
                if (resource.CurrentQuantity > 0)
                {
                    if (resource.IsWater)
                        found.WaterLiters += resource.WeightPerUnit;
                    else
                        found.Add(resource.ResourceType, resource.WeightPerUnit);

                    resource.CurrentQuantity--;
                    resource.RespawnProgressHours = 0;
                }
            }
        }

        // Reset worked minutes if depleted
        if (!HasAvailableResources())
        {
            _minutesWorked = 0;
        }

        return found;
    }

    /// <summary>
    /// Get total minutes required to fully harvest all remaining resources.
    /// </summary>
    public int GetTotalMinutesToHarvest()
    {
        if (!_resources.Any())
            return 0;

        int maxRemaining = _resources.Max(r => r.CurrentQuantity);

        if (maxRemaining == 0)
            return 0;

        int totalMinutes = maxRemaining * MinutesToHarvest;
        return Math.Max(0, totalMinutes - _minutesWorked);
    }

    /// <summary>
    /// Get a human-readable status description of available resources.
    /// </summary>
    public string GetStatusDescription()
    {
        var descriptions = new List<string>();
        foreach (var resource in _resources)
        {
            string status = resource.CurrentQuantity switch
            {
                0 => "depleted",
                var q when q < resource.MaxQuantity / 3.0 => "sparse",
                var q when q < resource.MaxQuantity * 2.0 / 3.0 => "moderate",
                _ => "abundant"
            };

            descriptions.Add($"{resource.DisplayName}: {status}");
        }

        if (descriptions.Count == 0)
        {
            return "no resources";
        }

        return string.Join(", ", descriptions);
    }

    public override List<Resource> ProvidedResources() =>
        _resources.Where(r => !r.IsWater).Select(r => r.ResourceType).Distinct().ToList();

    public class HarvestableResource
    {
        public string DisplayName { get; set; } = "";
        public Resource ResourceType { get; set; }  // Serializable enum instead of delegate
        public bool IsWater { get; set; } = false;  // Water handled specially
        public int MaxQuantity { get; set; }
        public int CurrentQuantity { get; set; }
        public double WeightPerUnit { get; set; }
        public double RespawnHoursPerUnit { get; set; }
        public double RespawnProgressHours { get; set; }
    }
}
