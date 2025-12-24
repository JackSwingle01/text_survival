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
    /// Factory function to create the resulting tool.
    /// </summary>
    public required Func<int, Tool> ToolFactory { get; init; }

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
    /// Consume materials and create the tool.
    /// </summary>
    public Tool Craft(Inventory inventory)
    {
        // Consume materials
        foreach (var req in Requirements)
        {
            ConsumeMaterial(inventory, req.Material, req.Count);
        }

        // Create and return the tool
        return ToolFactory(Durability);
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

    private static int GetMaterialCount(Inventory inv, string material) =>
        inv.GetCount(material);

    private static void ConsumeMaterial(Inventory inv, string material, int count) =>
        inv.Take(material, count);
}

/// <summary>
/// A single material requirement for crafting.
/// </summary>
public record MaterialRequirement(string Material, int Count);
