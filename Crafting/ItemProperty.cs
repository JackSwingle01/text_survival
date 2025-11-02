namespace text_survival.Crafting;

public enum ItemProperty : byte
{
    Stone,
    Wood,
    Binding,
    Flammable,
    Firestarter,
    Insulation,
    RawMeat,
    CookedMeat,
    Bone,
    Hide,
    Poison,
    Fat,
    Sharp,
    Clay,
    // Phase 2 additions for crafting/foraging overhaul
    Tinder,      // Fire-starting materials (dry grass, bark)
    PlantFiber,  // Early-game cordage alternative to sinew
    Fur,         // Superior insulation from hunting
    Antler,      // Tool handles and specialized components
    Leather,     // Processed hide (requires tanning)
    Charcoal,    // Fire-hardened wood, fuel
    // Phase 4 additions for tool progression
    Flint,       // High-quality stone for sharp blades
    Obsidian,    // Volcanic glass - sharpest material
    // Harvestable feature additions
    Adhesive,      // Pine sap, tree resin - for waterproofing and gluing
    Waterproofing  // Materials that can seal containers
}

public class CraftingPropertyRequirement(ItemProperty property, double minQuantity, bool isConsumed = true)
{
    public ItemProperty Property { get; set; } = property;
    public double MinQuantity { get; set; } = minQuantity;
    public bool IsConsumed { get; set; } = isConsumed;
}