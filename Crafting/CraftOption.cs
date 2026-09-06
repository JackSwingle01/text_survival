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

    public string Id { get; set; } = "";
    public string FamilyId { get; set; } = "";
    public string Method { get; set; } = "Make";
    public int ProjectWorkMinutes { get; init; }
    public bool ProjectBenefitsFromShovel { get; init; }
    public Dictionary<ToolType, int> ToolWear { get; init; } = [];
    public Gear? TargetGear { get; init; }
    public Func<Gear>? TargetResult { get; init; }
    public Func<string?>? TargetBlocker { get; init; }

    public bool CanCraft(Inventory inventory) => CraftInputs.Resolve(this, inventory).Ready;

    public (bool CanCraft, List<string> Missing) CheckRequirements(Inventory inventory)
    {
        var inputs = CraftInputs.Resolve(this, inventory);
        return (inputs.Ready, inputs.Missing);
    }

    public bool ProducesMaterials => MaterialOutputs != null && MaterialOutputs.Count > 0;
    public bool ProducesGear => GearFactory != null;
    public bool ProducesFeature => FeatureFactory != null;
    public bool IsMendingRecipe => MendSlot.HasValue;

    public Gear? Craft(Inventory inventory, CraftInputs? inputs = null)
    {
        if (TargetGear != null && (!CraftInputs.OwnedGear(inventory).Contains(TargetGear) || TargetBlocker?.Invoke() != null))
            throw new InvalidOperationException("The selected equipment can no longer be worked on.");
        var transformed = TargetResult?.Invoke();
        (inputs ?? CraftInputs.Resolve(this, inventory)).Consume(inventory);
        if (transformed != null)
        {
            inventory.ReplaceGear(TargetGear!, transformed);
            return transformed;
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
        var gear = GearFactory!(Durability);
        gear.DesignId = Id;
        return gear;
    }

    public Environments.Features.LocationFeature? CraftFeature(Inventory inventory, CraftInputs? inputs = null)
    {
        if (!ProducesFeature)
            return null;

        (inputs ?? CraftInputs.Resolve(this, inventory)).Consume(inventory);
        return FeatureFactory!();
    }

    /// <summary>
    /// Tear down the camp's shelter and put up a log frame in its place, returning the
    /// new shelter and what the old one gave back. Rebuilding is neither "produces gear"
    /// nor "produces a feature" - it replaces one - so it gets its own entry point rather
    /// than a flag the caller has to interpret.
    /// </summary>
    public (Environments.Features.ShelterFeature Shelter, Dictionary<Resource, int> Salvage)? CraftShelterRebuild(
        Environments.Location camp, Inventory inventory, CraftInputs? inputs = null)
    {
        if (!RebuildShelter)
            return null;

        var old = camp.GetFeature<Environments.Features.ShelterFeature>()
            ?? throw new InvalidOperationException("Rebuild Shelter offered with no shelter at camp.");

        (inputs ?? CraftInputs.Resolve(this, inventory)).Consume(inventory);

        var salvage = old.GetSalvageMaterials();
        foreach (var (resource, count) in salvage)
            inventory.Add(resource, count);

        camp.RemoveFeature(old);
        var rebuilt = Environments.Features.ShelterFeature.CreateLogFrame();
        camp.AddFeature(rebuilt);

        return (rebuilt, salvage);
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
