using text_survival.Items;

namespace text_survival.Web.Dto;

/// <summary>
/// Single source of truth for computing gear summary from inventory.
/// Used by both InventoryDto (overlay) and GameStateDto (side panel).
/// </summary>
public static class GearSummaryHelper
{
    public static GearSummaryDto ComputeGearSummary(Inventory inv)
    {
        // Count tools by category
        var allTools = inv.Tools.ToList();
        if (inv.Weapon != null) allTools.Add(inv.Weapon);

        int cuttingCount = allTools.Count(t => t.ToolType == ToolType.Knife || t.ToolType == ToolType.Axe);
        int fireCount = allTools.Count(t => t.ToolType == ToolType.FireStriker ||
                                            t.ToolType == ToolType.HandDrill ||
                                            t.ToolType == ToolType.BowDrill);
        int otherCount = allTools.Count - cuttingCount - fireCount;

        // Food portions (count-based - more semantically correct than weight-based)
        int foodPortions = inv.GetCount(ResourceCategory.Food);

        // Water portions (~0.25L each)
        int waterPortions = (int)(inv.WaterLiters / 0.25);

        // Total crafting materials
        int craftingCount = inv.GetCount(ResourceCategory.Material);

        // Total medicinals
        int medicinalCount = inv.GetCount(ResourceCategory.Medicine);

        // Has rare materials (flint or pyrite)
        bool hasRare = inv.Count(Resource.Flint) > 0 || inv.Count(Resource.Pyrite) > 0;

        return new GearSummaryDto(
            WeaponName: inv.Weapon?.Name,
            WeaponDamage: inv.Weapon?.Damage,
            CuttingToolCount: cuttingCount,
            FireStarterCount: fireCount,
            OtherToolCount: otherCount,
            FoodPortions: foodPortions,
            WaterPortions: waterPortions,
            CraftingMaterialCount: craftingCount,
            MedicinalCount: medicinalCount,
            HasRareMaterials: hasRare
        );
    }
}
