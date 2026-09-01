using text_survival.Items;

namespace text_survival.Crafting;

public class CraftOption
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required NeedCategory Category { get; init; }
    public required int CraftingTimeMinutes { get; init; }
    public required int Durability { get; init; }

    public required List<MaterialRequirement> Requirements { get; init; }
    public List<ToolType> RequiredTools { get; init; } = [];
    public Func<Actions.GameContext, string?>? Prerequisite { get; init; }
    public Func<int, Gear>? GearFactory { get; init; }
    public Func<Environments.Features.LocationFeature>? FeatureFactory { get; init; }
    public List<MaterialOutput>? MaterialOutputs { get; init; }
    public Items.EquipSlot? MendSlot { get; init; }
    public bool RebuildShelter { get; init; } = false;

    public bool CanCraft(Inventory inventory)
    {
        foreach (var req in Requirements)
        {
            if (GetMaterialCount(inventory, req.Material) < req.Count)
                return false;
        }

        // Check required tools
        foreach (var toolType in RequiredTools)
        {
            var tool = inventory.GetTool(toolType);
            if (tool == null || tool.Durability < 1)
                return false;
        }

        return true;
    }

    public (bool CanCraft, List<string> Missing) CheckRequirements(Inventory inventory)
    {
        var missing = new List<string>();

        foreach (var req in Requirements)
        {
            int have = GetMaterialCount(inventory, req.Material);
            if (have < req.Count)
            {
                int need = req.Count - have;
                missing.Add($"{need} {GetMaterialDisplayName(req.Material)}");
            }
        }

        // Check tool requirements
        foreach (var toolType in RequiredTools)
        {
            var tool = inventory.GetTool(toolType);
            if (tool == null)
                missing.Add($"{toolType} (required tool)");
            else if (tool.Durability < 1)
                missing.Add($"{toolType} (broken - no durability left)");
        }

        return (missing.Count == 0, missing);
    }

    public bool ProducesMaterials => MaterialOutputs != null && MaterialOutputs.Count > 0;
    public bool ProducesGear => GearFactory != null;
    public bool ProducesFeature => FeatureFactory != null;
    public bool IsMendingRecipe => MendSlot.HasValue;

    public Gear? Craft(Inventory inventory)
    {
        // Consume materials
        foreach (var req in Requirements)
        {
            ConsumeMaterial(inventory, req.Material, req.Count);
        }

        // Consume tool durability (1 per required tool)
        foreach (var toolType in RequiredTools)
        {
            var tool = inventory.GetTool(toolType)!;
            tool.Use();
        }

        // If this is a mending recipe, repair the equipment
        if (IsMendingRecipe)
        {
            var equipment = inventory.GetEquipment(MendSlot!.Value);
            if (equipment != null)
            {
                // Restore 50% of max durability (imperfect field mending)
                int repairAmount = equipment.MaxDurability / 2;
                equipment.Repair(repairAmount);
            }
            return null;
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

    public Environments.Features.LocationFeature? CraftFeature(Inventory inventory)
    {
        if (!ProducesFeature)
            return null;

        ConsumeInputs(inventory);
        return FeatureFactory!();
    }

    /// <summary>
    /// Tear down the camp's shelter and put up a log frame in its place, returning the
    /// new shelter and what the old one gave back. Rebuilding is neither "produces gear"
    /// nor "produces a feature" - it replaces one - so it gets its own entry point rather
    /// than a flag the caller has to interpret.
    /// </summary>
    public (Environments.Features.ShelterFeature Shelter, Dictionary<Resource, int> Salvage)? CraftShelterRebuild(
        Environments.Location camp, Inventory inventory)
    {
        if (!RebuildShelter)
            return null;

        var old = camp.GetFeature<Environments.Features.ShelterFeature>()
            ?? throw new InvalidOperationException("Rebuild Shelter offered with no shelter at camp.");

        ConsumeInputs(inventory);

        var salvage = old.GetSalvageMaterials();
        foreach (var (resource, count) in salvage)
            inventory.Add(resource, count);

        camp.RemoveFeature(old);
        var rebuilt = Environments.Features.ShelterFeature.CreateLogFrame();
        camp.AddFeature(rebuilt);

        return (rebuilt, salvage);
    }

    /// <summary>Consume this recipe's materials and a point of durability from each required tool.</summary>
    private void ConsumeInputs(Inventory inventory)
    {
        foreach (var req in Requirements)
            ConsumeMaterial(inventory, req.Material, req.Count);

        foreach (var toolType in RequiredTools)
            inventory.GetTool(toolType)!.Use();
    }

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

    private static int GetMaterialCount(Inventory inv, MaterialSpecifier material) => material switch
    {
        MaterialSpecifier.Specific(var resource) => inv.Count(resource),
        MaterialSpecifier.Category(var category) => inv.GetCount(category),
        _ => 0
    };

    private static void ConsumeMaterial(Inventory inv, MaterialSpecifier material, int count)
    {
        switch (material)
        {
            case MaterialSpecifier.Specific(var resource):
                inv.Remove(resource, count);
                break;
            case MaterialSpecifier.Category(var category):
                // For category requirements, consume from the first available resource in that category
                var categoryResources = ResourceCategories.Items[category];
                int remaining = count;
                foreach (var res in categoryResources)
                {
                    while (remaining > 0 && inv.Count(res) > 0)
                    {
                        inv.Pop(res);
                        remaining--;
                    }
                    if (remaining <= 0) break;
                }
                break;
        }
    }

    private static string GetMaterialDisplayName(MaterialSpecifier material) => material switch
    {
        MaterialSpecifier.Specific(var r) => r.ToDisplayName(),
        MaterialSpecifier.Category(var c) => c.ToString().ToLower(),
        _ => "unknown"
    };
}

public abstract record MaterialSpecifier
{
    public sealed record Specific(Resource Resource) : MaterialSpecifier;
    public sealed record Category(ResourceCategory Value) : MaterialSpecifier;

    public static implicit operator MaterialSpecifier(Resource r) => new Specific(r);
    public static implicit operator MaterialSpecifier(ResourceCategory c) => new Category(c);
}

public record MaterialRequirement(MaterialSpecifier Material, int Count);
public record MaterialOutput(string Material, int Count, double WeightPerUnit = 0.1);
