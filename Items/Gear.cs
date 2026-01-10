namespace text_survival.Items;

public enum GearCategory { Tool, Equipment, Accessory }

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
    Tent,       // Portable shelter - can be deployed at any location
    EmberCarrier // Portable smoldering ember for fire transport
}

public enum EquipSlot
{
    Head,
    Chest,
    Legs,
    Feet,
    Hands
}

public enum WeaponClass { Blade, Blunt, Pierce, Claw, Unarmed }

public class Gear
{
    // === Core Properties (all items) ===
    public string Name { get; set; } = "";
    public double Weight { get; set; }
    public GearCategory Category { get; init; }

    // === Durability (all items) ===
    public int Durability { get; set; } = -1;  // -1=infinite, 0=broken
    public int MaxDurability { get; init; } = -1;

    public bool IsBroken => Durability == 0;
    public bool Works => Durability != 0;

    public double ConditionPct
    {
        get
        {
            if (MaxDurability <= 0) return 1.0;  // Infinite durability
            if (Durability <= 0) return 0.0;     // Broken
            return (double)Durability / MaxDurability;
        }
    }

    public bool Use()
    {
        if (Durability == -1) return true;  // Infinite
        if (Durability == 0) return false;  // Already broken
        Durability--;
        return Durability > 0;
    }

    public void Repair(int amount)
    {
        if (MaxDurability <= 0) return;  // Can't repair infinite durability items
        Durability = Math.Min(MaxDurability, Durability + amount);
    }

    // === Tool-Specific ===
    public ToolType? ToolType { get; init; }

    // === Treatment-Specific (ToolType.Treatment) ===
    public string? TreatsEffect { get; init; }
    public double EffectReduction { get; init; }
    public string? TreatmentDescription { get; init; }
    public string? GrantsEffect { get; init; }
    public string? SecondaryTreatsEffect { get; init; }
    public double SecondaryEffectReduction { get; init; }

    // === Combat (optional, for weapon-tools) ===
    public double? Damage { get; init; }
    public double? BlockChance { get; init; } // todo - remove - not used
    public WeaponClass? WeaponClass { get; init; }
    public bool IsWeapon => Damage.HasValue;

    // === Equipment-Specific ===
    public EquipSlot? Slot { get; init; }
    public double BaseInsulation { get; init; }
    public double Insulation => BaseInsulation * ConditionPct;

    // === Armor Properties (Equipment) ===
    // Cushioning: Absorbs impact damage (vs Blunt), range 0-1
    // Toughness: Resists cutting damage (vs Sharp, partially vs Pierce), range 0-1
    public double BaseCushioning { get; init; }
    public double BaseToughness { get; init; }
    public double Cushioning => BaseCushioning * ConditionPct;
    public double Toughness => BaseToughness * ConditionPct;

    // === Waterproofing (Equipment) ===
    public double BaseWaterproofLevel { get; init; }
    public int ResinTreatmentDurability { get; set; }
    public bool IsResinTreated => ResinTreatmentDurability > 0;
    public double TotalWaterproofLevel => Math.Min(1.0,
        BaseWaterproofLevel + (IsResinTreated ? 0.3 : 0));

    public void ApplyResinTreatment(int durability = 50) => ResinTreatmentDurability = durability;

    public void TickResinTreatment()
    {
        if (ResinTreatmentDurability > 0)
            ResinTreatmentDurability--;
    }

    // === Accessory-Specific ===
    public double CapacityBonusKg { get; init; }

    // === Tent-Specific (ToolType.Tent) ===
    public double ShelterTempInsulation { get; init; }
    public double ShelterOverheadCoverage { get; init; }
    public double ShelterWindCoverage { get; init; }
    public bool IsTent => ToolType == Items.ToolType.Tent;

    // === Ember Carrier-Specific (ToolType.EmberCarrier) ===
    public double EmberBurnHoursMax { get; init; }
    public double EmberBurnHoursRemaining { get; set; }
    public bool IsEmberLit => EmberBurnHoursRemaining > 0;
    public bool IsEmberCarrier => ToolType == Items.ToolType.EmberCarrier;

    // === Display ===
    public override string ToString()
    {
        if (IsBroken) return $"{Name} (broken)";

        if (IsEmberCarrier)
        {
            if (IsEmberLit)
                return $"{Name} (lit, {EmberBurnHoursRemaining:F1}h)";
            return $"{Name} (unlit)";
        }

        // Equipment with condition and/or waterproofing
        var parts = new List<string>();

        if (MaxDurability > 0 && Durability < MaxDurability)
            parts.Add($"{ConditionPct:P0}");

        // Show waterproof status with symbols: ~ (minimal), ≈ (moderate), ≋ (excellent)
        double waterproof = TotalWaterproofLevel;
        if (waterproof >= 0.1)
        {
            string symbol = waterproof >= 0.5 ? "≋" : waterproof >= 0.25 ? "≈" : "~";
            if (IsResinTreated && ResinTreatmentDurability <= 12)
                parts.Add($"{symbol}:{ResinTreatmentDurability}");
            else
                parts.Add(symbol);
        }

        if (parts.Count > 0)
            return $"{Name} ({string.Join(", ", parts)})";

        return Name;
    }

    // === Tool Factory Methods ===

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

    public static Gear FireStriker(string name = "Fire Striker", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.FireStriker,
        Weight = 0.2,
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear HandDrill(string name = "Hand Drill", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.HandDrill,
        Weight = 0.3,
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear BowDrill(string name = "Bow Drill", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.BowDrill,
        Weight = 0.5,
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear WaterContainer(string name = "Waterskin", double weight = 0.3, int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.WaterContainer,
        Weight = weight,
        Durability = durability,
        MaxDurability = durability
    };

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

    public static Gear Torch(string name = "Torch") => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Torch,
        Weight = 0.5,
        Durability = 1,
        MaxDurability = 1
    };

    public static Gear Shovel(string name = "Bone Shovel", int durability = -1) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Shovel,
        Weight = 1.2,
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear BoneNeedle(string name = "Bone Needle", int durability = 20) => new()
    {
        Name = name,
        Category = GearCategory.Tool,
        ToolType = Items.ToolType.Needle,
        Weight = 0.05,
        Durability = durability,
        MaxDurability = durability
    };

    // === Equipment Factory Methods ===

    public static Gear FurHood(string name = "Fur Hood", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Head,
        Weight = 0.4,
        BaseInsulation = 0.15,
        BaseWaterproofLevel = 0.1,  // Raw fur
        BaseCushioning = 0.15,  // Fur is good padding
        BaseToughness = 0.05,   // Minimal cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear WornFurChestWrap(string name = "Worn Fur Chest Wrap", int durability = 50) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Chest,
        Weight = 1.5,
        BaseInsulation = 0.20,
        BaseWaterproofLevel = 0.1,  // Raw fur
        BaseCushioning = 0.12,  // Worn fur, less padding
        BaseToughness = 0.04,   // Minimal cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear FurChestWrap(string name = "Fur Chest Wrap", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Chest,
        Weight = 1.8,
        BaseInsulation = 0.30,
        BaseWaterproofLevel = 0.1,  // Raw fur
        BaseCushioning = 0.20,  // Good fur padding (chest is key protection)
        BaseToughness = 0.08,   // Some cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear FurLegWraps(string name = "Fur Leg Wraps", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Legs,
        Weight = 1.0,
        BaseInsulation = 0.15,
        BaseWaterproofLevel = 0.1,  // Raw fur
        BaseCushioning = 0.15,  // Fur padding
        BaseToughness = 0.06,   // Minimal cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear FurBoots(string name = "Fur Boots", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Feet,
        Weight = 0.6,
        BaseInsulation = 0.10,
        BaseWaterproofLevel = 0.1,  // Raw fur
        BaseCushioning = 0.10,  // Some padding
        BaseToughness = 0.05,   // Minimal cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear WornHideBoots(string name = "Worn Hide Boots", int durability = 30) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Feet,
        Weight = 0.4,
        BaseInsulation = 0.05,
        BaseWaterproofLevel = 0.1,  // Raw hide
        BaseCushioning = 0.06,  // Less padding than fur
        BaseToughness = 0.08,   // Hide provides some cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear FurMittens(string name = "Fur Mittens", int durability = 100) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Hands,
        Weight = 0.3,
        BaseInsulation = 0.08,
        BaseWaterproofLevel = 0.1,  // Raw fur
        BaseCushioning = 0.12,  // Fur padding
        BaseToughness = 0.04,   // Minimal cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear HideHandwraps(string name = "Hide Handwraps", int durability = 50) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Hands,
        Weight = 0.15,
        BaseInsulation = 0.03,
        BaseWaterproofLevel = 0.1,  // Raw hide
        BaseCushioning = 0.05,  // Minimal padding
        BaseToughness = 0.10,   // Hide provides cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    // === High-Tier Equipment (Heavy Hide Armor) ===

    public static Gear BearHideChest(string name = "Bear Hide Chest", int durability = 150) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Chest,
        Weight = 3.0,
        BaseInsulation = 0.35,
        BaseWaterproofLevel = 0.2,  // Thick hide
        BaseCushioning = 0.18,  // Good padding from thick hide
        BaseToughness = 0.30,   // Excellent cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear MammothHideChest(string name = "Mammoth Hide Chest", int durability = 200) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Chest,
        Weight = 4.5,
        BaseInsulation = 0.45,
        BaseWaterproofLevel = 0.25,  // Very thick hide
        BaseCushioning = 0.25,  // Excellent padding from massive hide
        BaseToughness = 0.38,   // Best cut resistance
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear MammothHideLegs(string name = "Mammoth Hide Legs", int durability = 200) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Legs,
        Weight = 2.5,
        BaseInsulation = 0.30,
        BaseWaterproofLevel = 0.25,
        BaseCushioning = 0.22,
        BaseToughness = 0.35,
        Durability = durability,
        MaxDurability = durability
    };

    public static Gear CuredHideChest(string name = "Cured Hide Chest", int durability = 120) => new()
    {
        Name = name,
        Category = GearCategory.Equipment,
        Slot = EquipSlot.Chest,
        Weight = 2.0,
        BaseInsulation = 0.25,
        BaseWaterproofLevel = 0.35,  // Cured hide is waterproof
        BaseCushioning = 0.12,  // Less padding (hardened)
        BaseToughness = 0.22,   // Good cut resistance (cured)
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
