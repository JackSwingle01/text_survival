using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// A craftable camp feature for curing hides and drying food.
/// Items placed on the rack transform over time into preserved versions.
/// </summary>
public class CuringRackFeature : LocationFeature
{
    /// <summary>
    /// Items currently being processed on the rack.
    /// </summary>
    private readonly List<CuringItem> _items = [];

    /// <summary>
    /// Maximum number of items the rack can hold.
    /// </summary>
    public int Capacity { get; init; } = 4;

    /// <summary>
    /// Current number of items on the rack.
    /// </summary>
    public int ItemCount => _items.Count;

    /// <summary>
    /// Whether the rack has space for more items.
    /// </summary>
    public bool HasSpace => _items.Count < Capacity;

    /// <summary>
    /// Curing time requirements in game-minutes.
    /// </summary>
    private static readonly Dictionary<CurableItemType, int> CuringTimeMinutes = new()
    {
        { CurableItemType.ScrapedHide, 2880 },  // 2 days (48 hours)
        { CurableItemType.RawMeat, 1440 },      // 1 day (24 hours)
        { CurableItemType.Berries, 720 }        // 12 hours
    };

    public CuringRackFeature() : base("CuringRack") { }

    /// <summary>
    /// Advance curing time for all items.
    /// </summary>
    public override void Update(int minutes)
    {
        foreach (var item in _items)
        {
            item.MinutesCured += minutes;
        }
    }

    /// <summary>
    /// Add an item to the rack for curing.
    /// Returns true if successful, false if rack is full or item type invalid.
    /// </summary>
    public bool AddItem(CurableItemType type, double weightKg)
    {
        if (!HasSpace)
            return false;

        if (!CuringTimeMinutes.ContainsKey(type))
            return false;

        _items.Add(new CuringItem
        {
            Type = type,
            WeightKg = weightKg,
            MinutesCured = 0,
            MinutesRequired = CuringTimeMinutes[type]
        });

        return true;
    }

    /// <summary>
    /// Get all items currently on the rack with their status.
    /// </summary>
    public List<CuringItemStatus> GetItemStatus()
    {
        return _items.Select(item => new CuringItemStatus
        {
            Type = item.Type,
            WeightKg = item.WeightKg,
            IsReady = item.IsReady,
            ProgressPct = item.ProgressPct,
            HoursRemaining = item.HoursRemaining
        }).ToList();
    }

    /// <summary>
    /// Check if any items are ready to collect.
    /// </summary>
    public bool HasReadyItems => _items.Any(i => i.IsReady);

    /// <summary>
    /// Collect all finished items and add them to inventory.
    /// Returns the number of items collected.
    /// </summary>
    public int CollectFinished(Inventory inventory)
    {
        var ready = _items.Where(i => i.IsReady).ToList();

        foreach (var item in ready)
        {
            AddFinishedToInventory(inventory, item);
            _items.Remove(item);
        }

        return ready.Count;
    }

    /// <summary>
    /// Add a finished item to inventory based on its type.
    /// </summary>
    private static void AddFinishedToInventory(Inventory inventory, CuringItem item)
    {
        switch (item.Type)
        {
            case CurableItemType.ScrapedHide:
                inventory.CuredHide.Push(item.WeightKg * 0.9); // Slight weight loss from drying
                break;
            case CurableItemType.RawMeat:
                inventory.DriedMeat.Push(item.WeightKg * 0.4); // Significant weight loss
                break;
            case CurableItemType.Berries:
                inventory.DriedBerries.Push(item.WeightKg * 0.3); // Most weight is water
                break;
        }
    }

    /// <summary>
    /// Get a human-readable description of the rack's contents.
    /// </summary>
    public string GetDescription()
    {
        if (_items.Count == 0)
            return "The rack is empty.";

        var parts = new List<string>();
        foreach (var item in _items)
        {
            string itemName = item.Type switch
            {
                CurableItemType.ScrapedHide => "scraped hide",
                CurableItemType.RawMeat => "meat",
                CurableItemType.Berries => "berries",
                _ => "unknown"
            };

            if (item.IsReady)
            {
                parts.Add($"{itemName} (ready!)");
            }
            else
            {
                parts.Add($"{itemName} ({item.HoursRemaining:F0}h remaining)");
            }
        }

        return $"On the rack: {string.Join(", ", parts)}";
    }

    private class CuringItem
    {
        public CurableItemType Type { get; init; }
        public double WeightKg { get; init; }
        public int MinutesCured { get; set; }
        public int MinutesRequired { get; init; }

        public bool IsReady => MinutesCured >= MinutesRequired;
        public double ProgressPct => Math.Min(1.0, (double)MinutesCured / MinutesRequired);
        public double HoursRemaining => Math.Max(0, (MinutesRequired - MinutesCured) / 60.0);
    }
}

/// <summary>
/// Types of items that can be cured/dried on the rack.
/// </summary>
public enum CurableItemType
{
    ScrapedHide,
    RawMeat,
    Berries
}

/// <summary>
/// Status information for a curing item (for display).
/// </summary>
public class CuringItemStatus
{
    public CurableItemType Type { get; init; }
    public double WeightKg { get; init; }
    public bool IsReady { get; init; }
    public double ProgressPct { get; init; }
    public double HoursRemaining { get; init; }
}
