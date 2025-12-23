namespace text_survival.Items;

/// <summary>
/// Simple tool that "just works" - no condition/degradation tracking.
/// Tools can optionally have combat properties (Damage, BlockChance, etc.)
/// Examples: axe (chops AND fights), knife (cuts AND fights), fire striker (utility only)
/// </summary>
public class Tool
{
    public string Name { get; set; }
    public ToolType Type { get; set; }
    public double Weight { get; set; }

    /// <summary>
    /// Uses remaining before tool breaks. -1 means infinite durability.
    /// </summary>
    public int Durability { get; set; } = -1;

    /// <summary>
    /// Check if tool is broken (0 durability).
    /// Tools with -1 durability never break.
    /// </summary>
    public bool IsBroken => Durability == 0;

    /// <summary>
    /// Check if tool works (not broken).
    /// </summary>
    public bool Works => Durability != 0;

    // Combat properties - null means not a weapon
    public double? Damage { get; set; }
    public double? BlockChance { get; set; }
    public WeaponClass? WeaponClass { get; set; }

    // Computed property - true if this tool can be used as a weapon
    public bool IsWeapon => Damage.HasValue;

    public Tool(string name, ToolType type, double weight = 0.5)
    {
        Name = name;
        Type = type;
        Weight = weight;
    }

    /// <summary>
    /// Use the tool once, decrementing durability.
    /// Returns true if tool is still usable, false if broken.
    /// </summary>
    public bool Use()
    {
        if (Durability == -1) return true; // Infinite durability
        if (Durability == 0) return false; // Already broken

        Durability--;
        return Durability > 0;
    }

    public override string ToString() => Name;

    // Factory methods for common tools

    /// <summary>Stone axe - chops wood AND fights (Damage 12, Blade)</summary>
    public static Tool Axe(string name = "Stone Axe") =>
        new(name, ToolType.Axe, 1.5)
        {
            Damage = 12,
            BlockChance = 0.05,
            WeaponClass = Items.WeaponClass.Blade
        };

    /// <summary>Flint knife - cuts AND fights (Damage 6, Blade)</summary>
    public static Tool Knife(string name = "Flint Knife") =>
        new(name, ToolType.Knife, 0.3)
        {
            Damage = 6,
            BlockChance = 0.02,
            WeaponClass = Items.WeaponClass.Blade
        };

    /// <summary>Fire striker - utility only, no combat stats</summary>
    public static Tool FireStriker(string name = "Fire Striker") =>
        new(name, ToolType.FireStriker, 0.2);

    /// <summary>Hand drill - primitive friction fire-starter</summary>
    public static Tool HandDrill(string name = "Hand Drill") =>
        new(name, ToolType.HandDrill, 0.3);

    /// <summary>Bow drill - better friction fire-starter</summary>
    public static Tool BowDrill(string name = "Bow Drill") =>
        new(name, ToolType.BowDrill, 0.5);

    /// <summary>Water container - utility only, no combat stats</summary>
    public static Tool WaterContainer(string name = "Waterskin", double weight = 0.3) =>
        new(name, ToolType.WaterContainer, weight);

    /// <summary>Wooden spear - hunting AND fighting (Damage 8, Pierce)</summary>
    public static Tool Spear(string name = "Wooden Spear") =>
        new(name, ToolType.Spear, 2.0)
        {
            Damage = 8,
            BlockChance = 0.12,
            WeaponClass = Items.WeaponClass.Pierce
        };

    /// <summary>Club - blunt force weapon (Damage 10, Blunt)</summary>
    public static Tool Club(string name = "Wooden Club") =>
        new(name, ToolType.Club, 2.0)
        {
            Damage = 10,
            BlockChance = 0.08,
            WeaponClass = Items.WeaponClass.Blunt
        };
}

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
    Snare
}

/// <summary>
/// Combat style of a weapon - determines attack verb and damage type calculation.
/// </summary>
public enum WeaponClass
{
    Blade,      // Slashing attacks (axes, knives)
    Blunt,      // Crushing attacks (clubs)
    Pierce,     // Piercing attacks (spears)
    Claw,       // Animal natural weapons
    Unarmed     // Bare fists
}
