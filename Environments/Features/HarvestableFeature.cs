using text_survival.Core;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Represents a discoverable resource node that can be harvested for items.
/// Unlike ForageFeature (RNG-based searching), HarvestableFeature is a visible,
/// quantity-based resource that respawns over time.
/// Examples: berry bushes, willow stands, water sources, sap seeps
/// </summary>
public class HarvestableFeature : LocationFeature
{
    public string DisplayName { get; }
    public string Description { get; set; } = "";
    public bool IsDiscovered { get; set; } = true; // Start discovered for v1

    /// <summary>
    /// Minutes of work required to complete one harvest cycle.
    /// Each cycle yields one of each non-depleted resource.
    /// </summary>
    public int MinutesToHarvest { get; set; } = 5;

    private int _minutesWorked = 0;
    private readonly Dictionary<Func<Item>, HarvestableResource> _resources = [];

    public HarvestableFeature(string name, string displayName, Location location)
        : base(name, location)
    {
        DisplayName = displayName;
    }

    /// <summary>
    /// Add a harvestable resource to this feature.
    /// </summary>
    /// <param name="itemFactory">Factory to create the item when harvested</param>
    /// <param name="maxQuantity">Maximum available quantity</param>
    /// <param name="respawnHoursPerUnit">Hours required to respawn one unit</param>
    public void AddResource(Func<Item> itemFactory, int maxQuantity, double respawnHoursPerUnit)
    {
        _resources[itemFactory] = new HarvestableResource
        {
            ItemFactory = itemFactory,
            MaxQuantity = maxQuantity,
            CurrentQuantity = maxQuantity,
            RespawnHoursPerUnit = respawnHoursPerUnit,
            LastHarvestTime = DateTime.MinValue
        };
    }

    /// <summary>
    /// Check if any resources are currently available to harvest
    /// </summary>
    public bool HasAvailableResources()
    {
        UpdateRespawn();
        return _resources.Values.Any(r => r.CurrentQuantity > 0);
    }

    /// <summary>
    /// Work for the specified minutes, yielding items when harvest cycles complete.
    /// Each completed cycle drops one of each non-depleted resource.
    /// </summary>
    /// <param name="minutes">Minutes of work to perform</param>
    /// <returns>List of items harvested (may be empty if no cycles completed)</returns>
    public List<Item> Harvest(int minutes)
    {
        UpdateRespawn();
        var items = new List<Item>();

        _minutesWorked += minutes;

        while (_minutesWorked >= MinutesToHarvest && HasAvailableResources())
        {
            _minutesWorked -= MinutesToHarvest;

            foreach (var resource in _resources.Values)
            {
                if (resource.CurrentQuantity > 0)
                {
                    var item = resource.ItemFactory();
                    items.Add(item);
                    resource.CurrentQuantity--;
                    resource.LastHarvestTime = World.GameTime;
                }
            }
        }

        // Reset worked minutes if depleted (no partial progress on empty resource)
        if (!HasAvailableResources())
        {
            _minutesWorked = 0;
        }

        return items;
    }

    /// <summary>
    /// Get total minutes required to fully harvest all remaining resources,
    /// accounting for work already done toward the current cycle.
    /// </summary>
    public int GetTotalMinutesToHarvest()
    {
        UpdateRespawn();

        if (!_resources.Values.Any())
            return 0;

        int maxRemaining = _resources.Values.Max(r => r.CurrentQuantity);

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
        UpdateRespawn();

        var descriptions = new List<string>();
        foreach (var resource in _resources.Values)
        {
            // Create a sample item to get its name
            var sampleItem = resource.ItemFactory();

            string status = resource.CurrentQuantity switch
            {
                0 => "depleted",
                var q when q < resource.MaxQuantity / 3.0 => "sparse",
                var q when q < resource.MaxQuantity * 2.0 / 3.0 => "moderate",
                _ => "abundant"
            };

            descriptions.Add($"{sampleItem.Name}: {status}");
        }

        if (descriptions.Count == 0)
        {
            return $"no resources";
        }

        return $"{string.Join(", ", descriptions)})";
    }

    /// <summary>
    /// Update respawn progress for all resources based on time elapsed.
    /// Called lazily when Harvest() or GetStatusDescription() is invoked.
    /// </summary>
    private void UpdateRespawn()
    {
        foreach (var resource in _resources.Values)
        {
            // Skip if already at max or never harvested
            if (resource.CurrentQuantity >= resource.MaxQuantity ||
                resource.LastHarvestTime == DateTime.MinValue)
            {
                continue;
            }

            double hoursSinceHarvest = (World.GameTime - resource.LastHarvestTime).TotalHours;
            int unitsRespawned = (int)(hoursSinceHarvest / resource.RespawnHoursPerUnit);

            if (unitsRespawned > 0)
            {
                resource.CurrentQuantity = Math.Min(
                    resource.MaxQuantity,
                    resource.CurrentQuantity + unitsRespawned
                );

                // Update last harvest time to account for respawned units
                // (prevents compound respawn if checked multiple times in same period)
                double hoursUsed = unitsRespawned * resource.RespawnHoursPerUnit;
                resource.LastHarvestTime = resource.LastHarvestTime.AddHours(hoursUsed);
            }
        }
    }

    /// <summary>
    /// Internal class to track individual resource state within a harvestable feature
    /// </summary>
    private class HarvestableResource
    {
        public Func<Item> ItemFactory { get; set; } = null!;
        public int MaxQuantity { get; set; }
        public int CurrentQuantity { get; set; }
        public double RespawnHoursPerUnit { get; set; }
        public DateTime LastHarvestTime { get; set; }
    }
}