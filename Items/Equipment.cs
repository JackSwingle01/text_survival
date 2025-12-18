namespace text_survival.Items;

/// <summary>
/// Simple equipment (armor/clothing) that provides insulation.
/// Worn in one of 5 slots: Head, Chest, Legs, Feet, Hands.
/// </summary>
public class Equipment
{
    public string Name { get; set; }
    public EquipSlot Slot { get; set; }
    public double Weight { get; set; }

    /// <summary>
    /// Insulation value 0-1 representing percentage of skin-air temp differential blocked.
    /// </summary>
    public double Insulation { get; set; }

    public Equipment(string name, EquipSlot slot, double weight, double insulation)
    {
        Name = name;
        Slot = slot;
        Weight = weight;
        Insulation = insulation;
    }

    public override string ToString() => Name;

    // Factory methods for common equipment

    /// <summary>Fur head covering - good insulation</summary>
    public static Equipment FurHood(string name = "Fur Hood") =>
        new(name, EquipSlot.Head, 0.4, 0.15);

    /// <summary>Worn fur chest wrap - moderate insulation</summary>
    public static Equipment WornFurChestWrap(string name = "Worn Fur Chest Wrap") =>
        new(name, EquipSlot.Chest, 1.5, 0.20);

    /// <summary>Fur chest wrap - good insulation</summary>
    public static Equipment FurChestWrap(string name = "Fur Chest Wrap") =>
        new(name, EquipSlot.Chest, 1.8, 0.30);

    /// <summary>Fur leg wraps - moderate insulation</summary>
    public static Equipment FurLegWraps(string name = "Fur Leg Wraps") =>
        new(name, EquipSlot.Legs, 1.0, 0.15);

    /// <summary>Fur boots - good foot insulation</summary>
    public static Equipment FurBoots(string name = "Fur Boots") =>
        new(name, EquipSlot.Feet, 0.6, 0.10);

    /// <summary>Fur mittens - hand protection</summary>
    public static Equipment FurMittens(string name = "Fur Mittens") =>
        new(name, EquipSlot.Hands, 0.3, 0.08);
}

/// <summary>
/// Equipment slots for armor/clothing (not weapon - that uses Tool)
/// </summary>
public enum EquipSlot
{
    Head,
    Chest,
    Legs,
    Feet,
    Hands
}
