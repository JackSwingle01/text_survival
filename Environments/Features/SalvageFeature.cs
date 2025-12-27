using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// A one-time salvageable site - abandoned camp, frozen traveler, hidden cache.
/// Unlike HarvestableFeature, salvage sites don't regenerate.
/// Contains a mix of discrete items and aggregate resources.
/// </summary>
public class SalvageFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => IsDiscovered && HasLoot ? "search" : null;
    public override int IconPriority => 3;

    // Explicit public fields for serialization (System.Text.Json IncludeFields requires public)
    public string _displayName = "";
    private bool _isSalvaged = false;

    /// <summary>
    /// Display name shown in UI.
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// Flavor text shown on first discovery.
    /// </summary>
    public string? DiscoveryText { get; set; }

    /// <summary>
    /// Narrative hook - what happened here?
    /// </summary>
    public string? NarrativeHook { get; set; }

    /// <summary>
    /// Has the site been discovered? (Shown vs hidden)
    /// </summary>
    public bool IsDiscovered { get; set; } = false;

    /// <summary>
    /// Has the site been fully salvaged?
    /// </summary>
    public bool IsSalvaged => _isSalvaged;

    /// <summary>
    /// Discrete items available for salvage.
    /// </summary>
    public List<Gear> Tools { get; set; } = [];

    /// <summary>
    /// Equipment items available for salvage.
    /// </summary>
    public List<Gear> Equipment { get; set; } = [];

    /// <summary>
    /// Aggregate resources available for salvage.
    /// </summary>
    public Inventory Resources { get; set; } = new();

    /// <summary>
    /// Minutes required to salvage the site.
    /// </summary>
    public int MinutesToSalvage { get; set; } = 30;

    [System.Text.Json.Serialization.JsonConstructor]
    public SalvageFeature() : base("salvage") { }

    public SalvageFeature(string name, string displayName) : base(name)
    {
        _displayName = displayName;
    }

    /// <summary>
    /// Salvage the site and return all loot.
    /// Returns empty if already salvaged.
    /// </summary>
    public SalvageLoot Salvage()
    {
        if (_isSalvaged)
            return SalvageLoot.Empty;

        _isSalvaged = true;

        return new SalvageLoot
        {
            Tools = [.. Tools],
            Equipment = [.. Equipment],
            Resources = Resources,
            NarrativeHook = NarrativeHook
        };
    }

    /// <summary>
    /// Check if there's anything to salvage.
    /// </summary>
    public bool HasLoot =>
        !IsSalvaged && (Tools.Count > 0 || Equipment.Count > 0 || !Resources.IsEmpty);

    /// <summary>
    /// Check if this site can be salvaged.
    /// Alias for HasLoot for naming consistency.
    /// </summary>
    public bool CanBeSalvaged => HasLoot;

    /// <summary>
    /// Get work options for this feature.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (!CanBeSalvaged) yield break;
        yield return new WorkOption(
            $"Salvage {DisplayName} ({GetLootHint()})",
            "salvage",
            new SalvageStrategy()
        );
    }

    /// <summary>
    /// Get a description of available loot (without spoiling specifics).
    /// </summary>
    public string GetLootHint()
    {
        if (IsSalvaged)
            return "picked clean";

        var hints = new List<string>();

        if (Tools.Count > 0)
            hints.Add(Tools.Count == 1 ? "a tool" : "some tools");
        if (Equipment.Count > 0)
            hints.Add(Equipment.Count == 1 ? "some gear" : "clothing and gear");
        if (!Resources.IsEmpty)
            hints.Add("supplies");

        return hints.Count > 0 ? string.Join(", ", hints) : "nothing obvious";
    }

    // === Factory methods for common salvage types ===

    /// <summary>
    /// Create an abandoned camp salvage site.
    /// </summary>
    public static SalvageFeature CreateAbandonedCamp()
    {
        var salvage = new SalvageFeature("AbandonedCamp", "Abandoned Camp")
        {
            DiscoveryText = "Signs of an old camp. The fire pit is cold, shelter collapsed.",
            NarrativeHook = "Someone was here before you. What happened to them?",
            MinutesToSalvage = 45,
        };

        // Random loot for abandoned camp
        salvage.Resources.Add(Resource.Stick, 0.3);
        salvage.Resources.Add(Resource.Stick, 0.3);
        salvage.Resources.Add(Resource.Tinder, 0.05);
        salvage.Resources.Add(Resource.PlantFiber, 0.1);

        // Chance-based tool (50%)
        if (Utils.RandDouble(0, 1) < 0.5)
        {
            var knife = Gear.Knife("Worn Knife", 3);
            salvage.Tools.Add(knife);
        }

        return salvage;
    }

    /// <summary>
    /// Create a frozen traveler salvage site.
    /// </summary>
    public static SalvageFeature CreateFrozenTraveler()
    {
        var salvage = new SalvageFeature("FrozenTraveler", "Frozen Figure")
        {
            DiscoveryText = "A body, half-buried in snow. They didn't make it.",
            NarrativeHook = "Their gear might still be useful. Their fate is a warning.",
            MinutesToSalvage = 30,
        };

        // The traveler's gear
        var wornCoat = new Gear
        {
            Name = "Worn Coat",
            Category = GearCategory.Equipment,
            Slot = EquipSlot.Chest,
            Weight = 2.0,
            BaseInsulation = 0.15,
            Durability = 30,
            MaxDurability = 50
        };
        salvage.Equipment.Add(wornCoat);

        // Their supplies
        salvage.Resources.Add(Resource.Bone, 0.2);

        // Random additional items
        if (Utils.RandDouble(0, 1) < 0.4)
        {
            var knife = Gear.Knife("Bone Knife", 5);
            salvage.Tools.Add(knife);
        }
        if (Utils.RandDouble(0, 1) < 0.3)
        {
            salvage.Resources.Add(Resource.Hide, 0.5);
        }

        return salvage;
    }

    /// <summary>
    /// Create a hidden cache salvage site.
    /// </summary>
    public static SalvageFeature CreateHiddenCache()
    {
        var salvage = new SalvageFeature("HiddenCache", "Hidden Cache")
        {
            DiscoveryText = "Rocks piled deliberately. Someone's emergency stash.",
            NarrativeHook = "They never came back for it.",
            MinutesToSalvage = 20,
        };

        // Cached supplies - intentionally good stuff
        salvage.Resources.Add(Resource.Pine, 2.0);
        salvage.Resources.Add(Resource.Tinder, 0.05);
        salvage.Resources.Add(Resource.Tinder, 0.05);

        // Random food (might be spoiled after all this time)
        if (Utils.RandDouble(0, 1) < 0.6)
        {
            salvage.Resources.Add(Resource.Berries, 0.2);
        }

        // Rare tool find
        if (Utils.RandDouble(0, 1) < 0.25)
        {
            var handDrill = Gear.HandDrill("Hand Drill", 10);
            salvage.Tools.Add(handDrill);
        }

        return salvage;
    }
}

/// <summary>
/// Loot returned from salvaging a site.
/// </summary>
public class SalvageLoot
{
    public List<Gear> Tools { get; set; } = [];
    public List<Gear> Equipment { get; set; } = [];
    public Inventory Resources { get; set; } = new();
    public string? NarrativeHook { get; set; }

    public bool IsEmpty => Tools.Count == 0 && Equipment.Count == 0 && Resources.IsEmpty;

    public static SalvageLoot Empty => new();
}
