namespace text_survival.Crafting;

/// <summary>
/// Categories of player needs that crafting can address.
/// Players select a need, then see what they can make from available materials.
/// </summary>
public enum NeedCategory
{
    /// <summary>
    /// Fire-starting tools (hand drill, bow drill, etc.)
    /// </summary>
    FireStarting,

    /// <summary>
    /// Cutting tools for butchering, processing wood, general utility.
    /// </summary>
    CuttingTool,

    /// <summary>
    /// Hunting weapons (spear, bow, arrows).
    /// </summary>
    HuntingWeapon

    // Future categories:
    // Shelter,
    // Clothing,
    // Container
}
