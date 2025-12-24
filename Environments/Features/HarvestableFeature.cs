using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Represents a discoverable resource node that can be harvested.
/// Unlike ForageFeature (RNG-based searching), HarvestableFeature is a visible,
/// quantity-based resource that respawns over time.
/// Examples: berry bushes, deadfall pile, water source, willow stand
/// </summary>
public class HarvestableFeature : LocationFeature
{
    public string DisplayName { get; }
    public string Description { get; set; } = "";
    public bool IsDiscovered { get; set; } = true; // Start discovered for v1

    /// <summary>
    /// Minutes of work required to complete one harvest cycle.
    /// Each cycle yields one unit of each non-depleted resource.
    /// </summary>
    public int MinutesToHarvest { get; set; } = 5;

    private int _minutesWorked = 0;
    private readonly List<HarvestableResource> _resources = [];

    public HarvestableFeature(string name, string displayName)
        : base(name)
    {
        DisplayName = displayName;
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
    /// <param name="addToInventory">Action to add the resource to inventory</param>
    /// <param name="maxQuantity">Maximum available quantity</param>
    /// <param name="weightPerUnit">Weight in kg per unit harvested</param>
    /// <param name="respawnHoursPerUnit">Hours required to respawn one unit</param>
    public HarvestableFeature AddResource(string displayName, Action<Inventory, double, string> addToInventory,
        int maxQuantity, double weightPerUnit, double respawnHoursPerUnit)
    {
        _resources.Add(new HarvestableResource
        {
            DisplayName = displayName,
            AddToInventory = addToInventory,
            MaxQuantity = maxQuantity,
            CurrentQuantity = maxQuantity,
            WeightPerUnit = weightPerUnit,
            RespawnHoursPerUnit = respawnHoursPerUnit
        });
        return this;
    }

    // Convenience methods for common resource types (description param ignored - descriptions derived from contents)
    public HarvestableFeature AddLogs(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, (inv, w, _) => inv.Logs.Push(w), maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddSticks(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, (inv, w, _) => inv.Sticks.Push(w), maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddTinder(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, (inv, w, _) => inv.Tinder.Push(w), maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddBerries(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, (inv, w, _) => inv.Berries.Push(w), maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddWater(string displayName, int maxQuantity, double litersPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, (inv, w, _) => inv.WaterLiters += w, maxQuantity, litersPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddPlantFiber(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, (inv, w, _) => inv.PlantFiber.Push(w), maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddBone(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, (inv, w, _) => inv.Bone.Push(w), maxQuantity, weightPerUnit, respawnHoursPerUnit);

    public HarvestableFeature AddStone(string displayName, int maxQuantity, double weightPerUnit, double respawnHoursPerUnit) =>
        AddResource(displayName, (inv, w, _) => inv.Stone.Push(w), maxQuantity, weightPerUnit, respawnHoursPerUnit);

    /// <summary>
    /// Check if any resources are currently available to harvest
    /// </summary>
    public bool HasAvailableResources()
    {
        return _resources.Any(r => r.CurrentQuantity > 0);
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
                    string description = $"some {resource.DisplayName}";
                    resource.AddToInventory(found, resource.WeightPerUnit, description);
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

    private class HarvestableResource
    {
        public string DisplayName { get; set; } = "";
        public Action<Inventory, double, string> AddToInventory { get; set; } = null!;
        public int MaxQuantity { get; set; }
        public int CurrentQuantity { get; set; }
        public double WeightPerUnit { get; set; }
        public double RespawnHoursPerUnit { get; set; }
        public double RespawnProgressHours { get; set; }
    }
}
