namespace text_survival.Items;

/// <summary>
/// Category of gear - determines storage pattern and relevant properties.
/// </summary>
public enum GearCategory
{
    Tool,       // List storage, ToolType required
    Equipment,  // Slot storage (one per slot), insulation
    Accessory   // List storage, capacity bonus
}

/// <summary>
/// Type of tool - determines functionality.
/// </summary>
public enum ToolType
{
    Axe,
    Knife,
    FireStriker,
    HandDrill,
    BowDrill,
    WaterContainer,
    Spear,
    Club,
    Scraper,
    Needle,
    Cordage,
    Unarmed,
    Snare,
    Treatment,  // Consumable medical treatment (teas, poultices, salves)
    Torch,      // Portable light source
    Shovel,     // Digging tool - speeds up camp setup, fire pits, snow shelters
    KnappingStone,  // Hammer stone for shaping flint, shale, and bone tools
    Tent        // Portable shelter - can be deployed at any location
}

/// <summary>
/// Equipment slot for worn gear.
/// </summary>
public enum EquipSlot
{
    Head,
    Chest,
    Legs,
    Feet,
    Hands
}

/// <summary>
/// Weapon class for combat mechanics.
/// </summary>
public enum WeaponClass
{
    Blade,      // Slashing attacks (axes, knives)
    Blunt,      // Crushing attacks (clubs)
    Pierce,     // Piercing attacks (spears)
    Claw,       // Animal natural weapons
    Unarmed     // Bare fists
}

/// <summary>
/// Unified discrete item class. Tools, equipment, and accessories share
/// durability mechanics while maintaining category-specific properties.
/// </summary>
public class Gear
{
    // === Core Properties (all items) ===
    public string Name { get; set; } = "";
    public double Weight { get; set; }
    public GearCategory Category { get; init; }

    // === Durability (all items) ===
    /// <summary>
    /// Uses remaining before gear breaks. -1 = infinite durability. 0 = broken.
    /// </summary>
    public int Durability { get; set; } = -1;

    /// <summary>
    /// Maximum durability for calculating condition percentage.
    /// Set when crafted. -1 = infinite.
    /// </summary>
    public int MaxDurability { get; init; } = -1;

    public bool IsBroken => Durability == 0;
    public bool Works => Durability != 0;

    /// <summary>
    /// Condition as 0-1 percentage. Used for insulation degradation.
    /// Infinite durability items always return 1.0. Broken items return 0.
    /// </summary>
    public double ConditionPct
    {
        get
        {
            if (MaxDurability <= 0) return 1.0;  // Infinite durability
            if (Durability <= 0) return 0.0;     // Broken
            return (double)Durability / MaxDurability;
        }
    }

    /// <summary>
    /// Use the gear once, decrementing durability.
    /// Returns true if gear is still usable.
    /// </summary>
    public bool Use()
    {
        if (Durability == -1) return true;  // Infinite
        if (Durability == 0) return false;  // Already broken
        Durability--;
        return Durability > 0;
    }

    /// <summary>
    /// Repair the gear by the specified amount.
    /// Cannot exceed MaxDurability.
    /// </summary>
    public void Repair(int amount)
    {
        if (MaxDurability <= 0) return;  // Can't repair infinite durability items
        Durability = Math.Min(MaxDurability, Durability + amount);
    }

    // === Tool-Specific ===
    /// <summary>
    /// Tool type for category=Tool. Null for equipment/accessories.
    /// </summary>
    public ToolType? ToolType { get; init; }

    // === Combat (optional, for weapon-tools) ===
    public double? Damage { get; init; }
    public double? BlockChance { get; init; }
    public WeaponClass? WeaponClass { get; init; }
    public bool IsWeapon => Damage.HasValue;

    // === Equipment-Specific ===
    /// <summary>
    /// Equipment slot for category=Equipment. Null for tools/accessories.
    /// </summary>
    public EquipSlot? Slot { get; init; }

    /// <summary>
    /// Base insulation value (when new). Range 0-1.
    /// </summary>
    public double BaseInsulation { get; init; }

    /// <summary>
    /// Effective insulation accounting for wear.
    /// Degrades linearly with condition.
    /// </summary>
    public double Insulation => BaseInsulation * ConditionPct;

    // === Accessory-Specific ===
    /// <summary>
    /// Carrying capacity bonus in kg. Only used for category=Accessory.
    /// </summary>
    public double CapacityBonusKg { get; init; }

    // === Tent-Specific (ToolType.Tent) ===
    /// <summary>
    /// Temperature insulation provided when deployed (0-1 scale).
    /// </summary>
    public double ShelterTempInsulation { get; init; }

    /// <summary>
    /// Overhead coverage when deployed (0-1 scale, blocks precipitation).
    /// </summary>
    public double ShelterOverheadCoverage { get; init; }

    /// <summary>
    /// Wind coverage when deployed (0-1 scale, blocks wind chill).
    /// </summary>
    public double ShelterWindCoverage { get; init; }

    /// <summary>
    /// Whether this item is a deployable tent.
    /// </summary>
    public bool IsTent => ToolType == Items.ToolType.Tent;

    // === Display ===
    public override string ToString()
    {
        if (IsBroken) return $"{Name} (broken)";
        if (MaxDurability > 0 && Durability < MaxDurability)
            return $"{Name} ({ConditionPct:P0})";
        return Name;
    }

    // === Tool Factory Methods ===

    /// <summary>Stone axe - chops wood AND fights (Damage 12, Blade)</summary>
    public static Gear Axe(string name = "Stone Axe", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Axe,
        Weight = 1.5,
        Durability = durability,
        MaxDurability = durability,
        Damage = 12,
        BlockChance = 0.05,
        WeaponClass = Items.WeaponClass.Blade
    };

    /// <summary>Flint knife - cuts AND fights (Damage 6, Blade)</summary>
    public static Gear Knife(string name = "Flint Knife", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Knife,
        Weight = 0.3,
        Durability = durability,
        MaxDurability = durability,
        Damage = 6,
        BlockChance = 0.02,
        WeaponClass = Items.WeaponClass.Blade
    };

    /// <summary>Fire striker - utility only, no combat stats</summary>
    public static Gear FireStriker(string name = "Fire Striker", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.FireStriker,
        Weight = 0.2,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Hand drill - primitive friction fire-starter</summary>
    public static Gear HandDrill(string name = "Hand Drill", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.HandDrill,
        Weight = 0.3,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Bow drill - better friction fire-starter</summary>
    public static Gear BowDrill(string name = "Bow Drill", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.BowDrill,
        Weight = 0.5,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Water container - utility only, no combat stats</summary>
    public static Gear WaterContainer(string name = "Waterskin", double weight = 0.3, int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.WaterContainer,
        Weight = weight,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Wooden spear - hunting AND fighting (Damage 8, Pierce)</summary>
    public static Gear Spear(string name = "Wooden Spear", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Spear,
        Weight = 2.0,
        Durability = durability,
        MaxDurability = durability,
        Damage = 8,
        BlockChance = 0.12,
        WeaponClass = Items.WeaponClass.Pierce
    };

    /// <summary>Club - blunt force weapon (Damage 10, Blunt)</summary>
    public static Gear Club(string name = "Wooden Club", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Club,
        Weight = 2.0,
        Durability = durability,
        MaxDurability = durability,
        Damage = 10,
        BlockChance = 0.08,
        WeaponClass = Items.WeaponClass.Blunt
    };

    /// <summary>Torch - portable light source, burns for ~1 hour, provides modest warmth</summary>
    public static Gear Torch(string name = "Torch") => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Torch,
        Weight = 0.5,
        Durability = 1,
        MaxDurability = 1
    };

    /// <summary>Shovel - digging tool that speeds up camp setup, fire pits, and snow shelters</summary>
    public static Gear Shovel(string name = "Bone Shovel", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Shovel,
        Weight = 1.2,
        Durability = durability,
        MaxDurability = durability
    };

    // === Equipment Factory Methods ===

    /// <summary>Fur head covering - good insulation</summary>
    public static Gear FurHood(string name = "Fur Hood", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Head,
        Weight = 0.4,
        BaseInsulation = 0.15,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Worn fur chest wrap - moderate insulation</summary>
    public static Gear WornFurChestWrap(string name = "Worn Fur Chest Wrap", int durability = 50) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Chest,
        Weight = 1.5,
        BaseInsulation = 0.20,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Fur chest wrap - good insulation</summary>
    public static Gear FurChestWrap(string name = "Fur Chest Wrap", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Chest,
        Weight = 1.8,
        BaseInsulation = 0.30,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Fur leg wraps - moderate insulation</summary>
    public static Gear FurLegWraps(string name = "Fur Leg Wraps", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Legs,
        Weight = 1.0,
        BaseInsulation = 0.15,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Fur boots - good foot insulation</summary>
    public static Gear FurBoots(string name = "Fur Boots", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Feet,
        Weight = 0.6,
        BaseInsulation = 0.10,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Worn hide boots - poor foot insulation</summary>
    public static Gear WornHideBoots(string name = "Worn Hide Boots", int durability = 30) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Feet,
        Weight = 0.4,
        BaseInsulation = 0.05,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Fur mittens - hand protection</summary>
    public static Gear FurMittens(string name = "Fur Mittens", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Hands,
        Weight = 0.3,
        BaseInsulation = 0.08,
        Durability = durability,
        MaxDurability = durability
    };

    /// <summary>Hide handwraps - minimal hand protection</summary>
    public static Gear HideHandwraps(string name = "Hide Handwraps", int durability = 50) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Hands,
        Weight = 0.15,
        BaseInsulation = 0.03,
        Durability = durability,
        MaxDurability = durability
    };

    // === Accessory Factory Methods ===

    public static Gear SmallPouch(int durability = 100) => new()
    {
        Name = "Small Pouch",
        Category = GearCategory.Accessory,
        Weight = 0.1,
        CapacityBonusKg = 0.5,
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear RopeBelt(int durability = 100) => new()
    {
        Name = "Rope Belt",
        Category = GearCategory.Accessory,
        Weight = 0.4,
        CapacityBonusKg = 3.0,
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear ProperBelt(int durability = 150) => new()
    {
        Name = "Proper Belt",
        Category = GearCategory.Accessory,
        Weight = 0.3,
        CapacityBonusKg = 4.0,
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear LargeBag(int durability = 100) => new()
    {
        Name = "Large Bag",
        Category = GearCategory.Accessory,
        Weight = 0.8,
        CapacityBonusKg = 10.0,
        Durability = durability,
        MaxDurability = durability
    };

    // === Tent Factory Methods ===

    /// <summary>
    /// Portable hide tent - good all-around protection.
    /// </summary>
    public static Gear HideTent(int durability = 50) => new()
    {
        Name = "Hide Tent",
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Tent,
        Weight = 3.0,
        Durability = durability,
        MaxDurability = durability,
        ShelterTempInsulation = 0.5,
        ShelterOverheadCoverage = 0.9,
        ShelterWindCoverage = 0.7
    };

    /// <summary>
    /// Mammoth hide tent - superior protection, heavier.
    /// </summary>
    public static Gear MammothHideTent(int durability = 80) => new()
    {
        Name = "Mammoth Hide Tent",
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Tent,
        Weight = 5.0,
        Durability = durability,
        MaxDurability = durability,
        ShelterTempInsulation = 0.6,
        ShelterOverheadCoverage = 0.95,
        ShelterWindCoverage = 0.85
    };
}
