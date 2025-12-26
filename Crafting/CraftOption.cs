using text_survival.Items;

namespace text_survival.Crafting;

/// <summary>
/// Represents a single craftable option within a need category.
/// Defines what materials are required and what tool is produced.
/// </summary>
public class CraftOption
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required NeedCategory Category { get; init; }
    public required int CraftingTimeMinutes { get; init; }
    public required int Durability { get; init; }

    /// <summary>
    /// Material requirements as list of (MaterialName, Count).
    /// MaterialName must match an aggregate in Inventory (e.g., "Sticks", "Stone", "Bone").
    /// </summary>
    public required List<MaterialRequirement> Requirements { get; init; }

    /// <summary>
    /// Optional prerequisite check function. Returns error message if blocked, null if available.
    /// Used for progression requirements (e.g., Stone Pit requires Mound Pit) or environmental constraints.
    /// </summary>
    public Func<Actions.GameContext, string?>? Prerequisite { get; init; }

    /// <summary>
    /// Factory function to create the resulting gear (tools, equipment, accessories).
    /// Receives durability value from recipe. Null if this recipe produces materials or features instead.
    /// </summary>
    public Func<int, Gear>? GearFactory { get; init; }

    /// <summary>
    /// Factory function to create a camp feature (e.g., CuringRack).
    /// Null if this recipe produces something else.
    /// </summary>
    public Func<Environments.Features.LocationFeature>? FeatureFactory { get; init; }

    /// <summary>
    /// For processing recipes: materials to add to inventory instead of creating a tool.
    /// List of (MaterialName, Count) to add.
    /// </summary>
    public List<MaterialOutput>? MaterialOutputs { get; init; }

    /// <summary>
    /// Check if player has all required materials.
    /// </summary>
    public bool CanCraft(Inventory inventory)
    {
        foreach (var req in Requirements)
        {
            if (GetMaterialCount(inventory, req.Material) < req.Count)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Check requirements and return what's missing.
    /// </summary>
    public (bool CanCraft, List<string> Missing) CheckRequirements(Inventory inventory)
    {
        var missing = new List<string>();

        foreach (var req in Requirements)
        {
            int have = GetMaterialCount(inventory, req.Material);
            if (have < req.Count)
            {
                int need = req.Count - have;
                missing.Add($"{need} {req.Material.ToLower()}");
            }
        }

        return (missing.Count == 0, missing);
    }

    /// <summary>
    /// Whether this recipe produces materials instead of an item.
    /// </summary>
    public bool ProducesMaterials => MaterialOutputs != null && MaterialOutputs.Count > 0;

    /// <summary>
    /// Whether this recipe produces gear (tools, equipment, or accessories).
    /// </summary>
    public bool ProducesGear => GearFactory != null;

    /// <summary>
    /// Whether this recipe produces a camp feature.
    /// </summary>
    public bool ProducesFeature => FeatureFactory != null;

    /// <summary>
    /// Consume materials and create the item.
    /// Returns null if this recipe produces materials or features instead.
    /// </summary>
    public Gear? Craft(Inventory inventory)
    {
        // Consume materials
        foreach (var req in Requirements)
        {
            ConsumeMaterial(inventory, req.Material, req.Count);
        }

        // If this produces materials instead of an item, add them to inventory
        if (ProducesMaterials)
        {
            foreach (var output in MaterialOutputs!)
            {
                AddMaterialToInventory(inventory, output);
            }
            return null;
        }

        // If this produces a feature, handle separately
        if (ProducesFeature)
        {
            return null;
        }

        // Create and return the gear
        return GearFactory!(Durability);
    }

    /// <summary>
    /// Consume materials and create a camp feature.
    /// Returns null if this recipe doesn't produce a feature.
    /// </summary>
    public Environments.Features.LocationFeature? CraftFeature(Inventory inventory)
    {
        if (!ProducesFeature)
            return null;

        // Consume materials
        foreach (var req in Requirements)
        {
            ConsumeMaterial(inventory, req.Material, req.Count);
        }

        return FeatureFactory!();
    }

    /// <summary>
    /// Add output materials to inventory based on type.
    /// </summary>
    private static void AddMaterialToInventory(Inventory inv, MaterialOutput output)
    {
        for (int i = 0; i < output.Count; i++)
        {
            switch (output.Material)
            {
                case "ScrapedHide":
                    inv.Add(Resource.ScrapedHide, output.WeightPerUnit);
                    break;
                case "CuredHide":
                    inv.Add(Resource.CuredHide, output.WeightPerUnit);
                    break;
                case "Tallow":
                    inv.Add(Resource.Tallow, output.WeightPerUnit);
                    break;
                case "PlantFiber":
                    inv.Add(Resource.PlantFiber, output.WeightPerUnit);
                    break;
                case "Charcoal":
                    inv.Add(Resource.Charcoal, output.WeightPerUnit);
                    break;
                case "Rope":
                    inv.Add(Resource.Rope, output.WeightPerUnit);
                    break;
                default:
                    throw new ArgumentException($"Unknown material output: {output.Material}");
            }
        }
    }

    /// <summary>
    /// Get a summary string for display (shows status).
    /// </summary>
    public string GetRequirementsSummary(Inventory inventory)
    {
        var parts = new List<string>();
        foreach (var req in Requirements)
        {
            int have = GetMaterialCount(inventory, req.Material);
            string status = have >= req.Count ? "ok" : $"need {req.Count - have} more";
            parts.Add($"{req.Count} {req.Material.ToLower()} ({status})");
        }
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Get a short requirements list for display (just counts).
    /// </summary>
    public string GetRequirementsShort()
    {
        var parts = Requirements.Select(r => $"{r.Count} {r.Material.ToLower()}");
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Get a description of what this recipe produces (for processing recipes).
    /// </summary>
    public string GetOutputDescription()
    {
        if (MaterialOutputs == null || MaterialOutputs.Count == 0)
            return Name;

        var parts = MaterialOutputs.Select(o => $"{o.Count} {FormatMaterialName(o.Material)}");
        return string.Join(", ", parts);
    }

    private static string FormatMaterialName(string material) => material switch
    {
        "ScrapedHide" => "scraped hide",
        "CuredHide" => "cured hide",
        "Tallow" => "tallow",
        "PlantFiber" => "plant fiber",
        "Charcoal" => "charcoal",
        _ => material.ToLower()
    };

    private static int GetMaterialCount(Inventory inv, string material)
    {
        // Try to parse as Resource enum first
        if (Enum.TryParse<Resource>(material, out var resource))
            return inv.Count(resource);

        // Fall back to category check
        if (Enum.TryParse<ResourceCategory>(material, out var category))
            return inv.GetCount(category);

        return 0;
    }

    private static void ConsumeMaterial(Inventory inv, string material, int count)
    {
        // Try to parse as Resource enum first
        if (Enum.TryParse<Resource>(material, out var resource))
        {
            inv.Take(resource, count);
            return;
        }

        // Material name doesn't match enum - this is an error
        throw new ArgumentException($"Unknown material: {material}");
    }
}

/// <summary>
/// A single material requirement for crafting.
/// </summary>
public record MaterialRequirement(string Material, int Count);

/// <summary>
/// Output material from a processing recipe.
/// </summary>
public record MaterialOutput(string Material, int Count, double WeightPerUnit = 0.1);
