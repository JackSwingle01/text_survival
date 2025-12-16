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

    public HarvestableFeature(string name, string displayName)
        : base(name)
    {
        DisplayName = displayName;
    }

    /// <summary>
    /// Advance respawn timers for all depleted resources.
    /// </summary>
    public void Update(int minutes)
    {
        double hours = minutes / 60.0;

        foreach (var resource in _resources.Values)
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
            RespawnHoursPerUnit = respawnHoursPerUnit
        };
    }

    /// <summary>
    /// Check if any resources are currently available to harvest
    /// </summary>
    public bool HasAvailableResources()
    {
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
                    resource.RespawnProgressHours = 0; // Reset respawn progress on harvest
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
    /// Internal class to track individual resource state within a harvestable feature
    /// </summary>
    private class HarvestableResource
    {
        public Func<Item> ItemFactory { get; set; } = null!;
        public int MaxQuantity { get; set; }
        public int CurrentQuantity { get; set; }
        public double RespawnHoursPerUnit { get; set; }
        public double RespawnProgressHours { get; set; }
    }
}