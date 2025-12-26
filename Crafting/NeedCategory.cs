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
    HuntingWeapon,

    /// <summary>
    /// Trapping equipment (snares for passive hunting).
    /// </summary>
    Trapping,

    /// <summary>
    /// Material processing (scraping hides, rendering fat, processing fiber).
    /// </summary>
    Processing,

    /// <summary>
    /// Medical treatments (teas, poultices, remedies).
    /// </summary>
    Treatment,

    /// <summary>
    /// Clothing and equipment (hide clothing, insulation gear).
    /// </summary>
    Equipment,

    /// <summary>
    /// Light sources (torches for dark areas and night work).
    /// </summary>
    Lighting,

    /// <summary>
    /// Carrying gear (pouches, belts, bags) that increase carrying capacity.
    /// </summary>
    Carrying
}
