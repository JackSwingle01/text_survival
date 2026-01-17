namespace text_survival.Items;

/// <summary>
/// Material properties for shelter improvement effectiveness.
/// Each material has values 0-1 indicating how effective it is for each improvement type.
/// </summary>
public static class MaterialProperties
{
    /// <summary>
    /// How effective this material is for insulation (temperature protection).
    /// Higher = better for insulation improvements.
    /// </summary>
    public static double GetInsulating(Resource resource) => resource switch
    {
        // Excellent insulators
        Resource.Hide => 0.9,
        Resource.CuredHide => 0.85,
        Resource.MammothHide => 0.95,
        Resource.SphagnumMoss => 0.8,

        // Moderate insulators
        Resource.PlantFiber => 0.6,
        Resource.PineNeedles => 0.5,
        Resource.Tinder => 0.5,

        // Poor insulators but have structural/waterproof uses
        Resource.BirchBark => 0.3,
        Resource.Stick => 0.2,
        Resource.Pine => 0.3,
        Resource.Birch => 0.3,
        Resource.Oak => 0.4,
        Resource.Stone => 0.1,
        Resource.Bone => 0.1,

        _ => 0.1
    };

    /// <summary>
    /// How effective this material is for overhead coverage (rain/snow protection).
    /// Higher = better for waterproofing improvements.
    /// </summary>
    public static double GetWaterproof(Resource resource) => resource switch
    {
        // Excellent waterproofing
        Resource.BirchBark => 0.8,
        Resource.PineResin => 0.9,
        Resource.Tallow => 0.7,
        Resource.Hide => 0.7,
        Resource.CuredHide => 0.75,
        Resource.MammothHide => 0.8,

        // Moderate waterproofing
        Resource.Pine => 0.5,
        Resource.Birch => 0.5,
        Resource.Oak => 0.5,
        Resource.Stone => 0.6,

        // Poor waterproofing
        Resource.SphagnumMoss => 0.3,
        Resource.Stick => 0.3,
        Resource.PlantFiber => 0.2,
        Resource.PineNeedles => 0.2,
        Resource.Tinder => 0.1,
        Resource.Bone => 0.2,

        _ => 0.1
    };

    /// <summary>
    /// How effective this material is for wind coverage (structural blocking).
    /// Higher = better for wind protection improvements.
    /// </summary>
    public static double GetStructural(Resource resource) => resource switch
    {
        // Excellent structural
        Resource.Pine => 0.9,
        Resource.Birch => 0.9,
        Resource.Oak => 0.95,
        Resource.Stone => 0.95,
        Resource.Bone => 0.6,

        // Moderate structural
        Resource.Stick => 0.7,
        Resource.Hide => 0.4,
        Resource.CuredHide => 0.45,
        Resource.MammothHide => 0.5,

        // Poor structural
        Resource.BirchBark => 0.2,
        Resource.PlantFiber => 0.1,
        Resource.SphagnumMoss => 0.1,
        Resource.Tinder => 0.1,
        Resource.PineNeedles => 0.1,
        Resource.PineResin => 0.1,
        Resource.Tallow => 0.1,

        _ => 0.1
    };

    /// <summary>
    /// Get all three properties at once.
    /// </summary>
    public static (double insulating, double waterproof, double structural) GetProperties(Resource resource)
        => (GetInsulating(resource), GetWaterproof(resource), GetStructural(resource));

    /// <summary>
    /// Get the effectiveness of a material for a specific improvement type.
    /// </summary>
    public static double GetEffectiveness(Resource resource, ShelterImprovementType improvementType)
        => improvementType switch
        {
            ShelterImprovementType.Insulation => GetInsulating(resource),
            ShelterImprovementType.Overhead => GetWaterproof(resource),
            ShelterImprovementType.Wind => GetStructural(resource),
            _ => 0.1
        };

    /// <summary>
    /// Resources that can be used for shelter improvements.
    /// Only materials that make sense for shelter construction.
    /// </summary>
    public static readonly Resource[] ShelterMaterials = new[]
    {
        Resource.SphagnumMoss,
        Resource.PineNeedles,
        Resource.PlantFiber,
        Resource.Tinder,
        Resource.BirchBark,
        Resource.Stick,
        Resource.Pine,
        Resource.Birch,
        Resource.Oak,
        Resource.Hide,
        Resource.CuredHide,
        Resource.MammothHide,
        Resource.Stone,
        Resource.Bone,
        Resource.PineResin,
        Resource.Tallow
    };
}

/// <summary>
/// Types of shelter improvements the player can make.
/// </summary>
public enum ShelterImprovementType
{
    Insulation,  // Temperature protection
    Overhead,    // Rain/snow protection
    Wind         // Wind protection
}
