namespace text_survival.Crafting;

public class ItemCraftingProperty(string name, double quantity = 1, double quality = 1)
{
    public string Name = name;
    public double Quantity = quantity;
    public double Quality = quality;

    public override string ToString() => $"{Name}: {Quantity:F1} (Q: {Quality:F1})";
}

public class PropertyRequirement(string propertyName, double minQuantity, double minQuality = 0, bool isConsumed = true)
{
    public string PropertyName { get; set; } = propertyName;
    public double MinQuantity { get; set; } = minQuantity;
    public double MinQuality { get; set; } = minQuality;
    public bool IsConsumed { get; set; } = isConsumed;

    public override string ToString() => $"{PropertyName}: {MinQuantity:F1}+ (Q: {MinQuality:F1}+)";
}