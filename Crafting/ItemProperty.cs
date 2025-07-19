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
    Clay
}

public class CraftingPropertyRequirement(ItemProperty property, double minQuantity, bool isConsumed = true)
{
    public ItemProperty Property { get; set; } = property;
    public double MinQuantity { get; set; } = minQuantity;
    public bool IsConsumed { get; set; } = isConsumed;
}