using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// A one-time salvageable site - abandoned camp, frozen traveler, hidden cache.
/// Unlike HarvestableFeature, salvage sites don't regenerate.
/// Contains a mix of discrete items and aggregate resources.
/// </summary>
public class SalvageFeature : LocationFeature
{
    /// <summary>
    /// Display name shown in UI.
    /// </summary>
    public string DisplayName { get; }

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
    public bool IsSalvaged { get; private set; } = false;

    /// <summary>
    /// Discrete items available for salvage.
    /// </summary>
    public List<Tool> Tools { get; set; } = [];

    /// <summary>
    /// Equipment items available for salvage.
    /// </summary>
    public List<Equipment> Equipment { get; set; } = [];

    /// <summary>
    /// Aggregate resources available for salvage.
    /// </summary>
    public FoundResources Resources { get; set; } = new();

    /// <summary>
    /// Minutes required to salvage the site.
    /// </summary>
    public int MinutesToSalvage { get; set; } = 30;

    public SalvageFeature(string name, string displayName) : base(name)
    {
        DisplayName = displayName;
    }

    /// <summary>
    /// Salvage the site and return all loot.
    /// Returns empty if already salvaged.
    /// </summary>
    public SalvageLoot Salvage()
    {
        if (IsSalvaged)
            return SalvageLoot.Empty;

        IsSalvaged = true;

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
        salvage.Resources.AddStick(0.3, "salvaged stake");
        salvage.Resources.AddStick(0.3, "salvaged stake");
        salvage.Resources.AddTinder(0.05, "old tinder");
        salvage.Resources.AddPlantFiber(0.1, "frayed cordage");

        // Chance-based tool (50%)
        if (Utils.RandDouble(0, 1) < 0.5)
        {
            var knife = new Tool("Worn Knife", ToolType.Knife, 0.2) { Durability = 3 };
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
        salvage.Equipment.Add(new Equipment("Worn Coat", EquipSlot.Chest, 2.0, 0.15));

        // Their supplies
        salvage.Resources.AddBone(0.2, "bone fragment");

        // Random additional items
        if (Utils.RandDouble(0, 1) < 0.4)
        {
            var knife = new Tool("Bone Knife", ToolType.Knife, 0.15) { Durability = 5 };
            salvage.Tools.Add(knife);
        }
        if (Utils.RandDouble(0, 1) < 0.3)
        {
            salvage.Resources.AddHide(0.5, "scrap hide");
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
        salvage.Resources.AddLog(2.0, "dry log");
        salvage.Resources.AddTinder(0.05, "birch bark");
        salvage.Resources.AddTinder(0.05, "birch bark");

        // Random food (might be spoiled after all this time)
        if (Utils.RandDouble(0, 1) < 0.6)
        {
            salvage.Resources.AddBerries(0.2, "dried berries");
        }

        // Rare tool find
        if (Utils.RandDouble(0, 1) < 0.25)
        {
            var handDrill = new Tool("Hand Drill", ToolType.HandDrill, 0.3) { Durability = 10 };
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
    public List<Tool> Tools { get; set; } = [];
    public List<Equipment> Equipment { get; set; } = [];
    public FoundResources Resources { get; set; } = new();
    public string? NarrativeHook { get; set; }

    public bool IsEmpty => Tools.Count == 0 && Equipment.Count == 0 && Resources.IsEmpty;

    public static SalvageLoot Empty => new();
}
