using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Types of shelter frames. Built shelters are constructed by the player;
/// natural shelters are found in the world.
/// </summary>
public enum ShelterType
{
    // Built shelters (player constructs frame)
    BranchFrame,
    LogFrame,

    // Natural shelters (found in world)
    RockOverhang,
    Cave,
    DenseThicket,
    FallenTree,

    // Special shelters (unique niches)
    SnowShelter,    // Emergency shelter, cannot be improved (packed snow)
    HideTent        // Portable shelter, limited improvement
}

/// <summary>
/// Configuration for each shelter type defining base stats and caps.
/// </summary>
public record ShelterTypeConfig(
    double BaseInsulation,
    double BaseOverhead,
    double BaseWind,
    double InsulationCap,
    double OverheadCap,
    double WindCap,
    bool IsNatural,
    bool IsSnowShelter = false
);

public class ShelterFeature : LocationFeature, IWorkableFeature
{
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (IsDestroyed)
            yield break;

        bool hasMaterials = MaterialProperties.ShelterMaterials.Any(m => ctx.Inventory.Count(m) > 0);
        if (hasMaterials)
        {
            yield return new WorkOption(
                $"Improve shelter ({Quality:P0})",
                "improve_shelter",
                new ShelterImprovementStrategy()
            );
        }
    }

    public override string? MapIcon => !IsDestroyed ? (IsSnowShelter ? "snow_shelter" : "shelter") : null;
    public override int IconPriority => 5; // Shelter is important

    public double TemperatureInsulation { get; set; } = 0; // ambient temp protection 0-1
    public double OverheadCoverage { get; set; } = 0; // rain / snow / sun protection 0-1
    public double WindCoverage { get; set; } = 0; // wind protection 0-1

    /// <summary>
    /// The type of shelter frame.
    /// </summary>
    public ShelterType ShelterType { get; set; } = ShelterType.BranchFrame;

    /// <summary>
    /// Whether this is a snow shelter that degrades in warm temperatures.
    /// </summary>
    public bool IsSnowShelter { get; init; } = false;

    /// <summary>
    /// The tent gear item that created this shelter (if deployed from a portable tent).
    /// Null for permanent structures like frame shelters.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public text_survival.Items.Gear? SourceTent { get; set; }

    /// <summary>
    /// Whether this shelter was deployed from a portable tent and can be packed up.
    /// </summary>
    public bool IsPortable => SourceTent != null;

    /// <summary>
    /// Whether this is a natural shelter (found in world) vs built by player.
    /// Natural shelters cannot be rebuilt/salvaged.
    /// </summary>
    public bool IsNatural => GetConfig(ShelterType).IsNatural;

    /// <summary>
    /// Materials invested in improvements. Used for salvage calculation when rebuilding.
    /// </summary>
    public Dictionary<Resource, int> MaterialsInvested { get; set; } = new();

    #region Type Configurations

    private static readonly Dictionary<ShelterType, ShelterTypeConfig> TypeConfigs = new()
    {
        // Built shelters - player constructs frame, can improve up to caps
        [ShelterType.BranchFrame] = new(0.15, 0.25, 0.20, 0.50, 0.50, 0.50, IsNatural: false),
        [ShelterType.LogFrame] = new(0.25, 0.35, 0.30, 0.80, 0.80, 0.80, IsNatural: false),

        // Natural shelters - found in world, asymmetric caps reflecting character
        [ShelterType.RockOverhang] = new(0.10, 0.70, 0.30, 0.40, 0.80, 0.40, IsNatural: true),
        [ShelterType.Cave] = new(0.25, 0.90, 0.20, 0.60, 0.95, 0.40, IsNatural: true),
        [ShelterType.DenseThicket] = new(0.15, 0.50, 0.60, 0.40, 0.60, 0.70, IsNatural: true),
        [ShelterType.FallenTree] = new(0.10, 0.40, 0.50, 0.30, 0.50, 0.60, IsNatural: true),

        // Special shelters with unique niches
        // SnowShelter: Emergency shelter, cannot be improved (caps = base)
        [ShelterType.SnowShelter] = new(0.60, 0.90, 0.80, 0.60, 0.90, 0.80, IsNatural: false, IsSnowShelter: true),
        // HideTent: Portable, limited improvement potential
        [ShelterType.HideTent] = new(0.50, 0.90, 0.70, 0.60, 0.95, 0.80, IsNatural: false)
    };

    public static ShelterTypeConfig GetConfig(ShelterType type) =>
        TypeConfigs.TryGetValue(type, out var config) ? config : TypeConfigs[ShelterType.BranchFrame];

    public ShelterTypeConfig Config => GetConfig(ShelterType);

    #endregion

    #region Caps

    public double InsulationCap => Config.InsulationCap;
    public double OverheadCap => Config.OverheadCap;
    public double WindCap => Config.WindCap;

    #endregion

    #region Constructors

    public ShelterFeature(string name, double tempInsulation, double overheadCoverage, double windCoverage, bool isSnowShelter = false) : base(name)
    {
        TemperatureInsulation = tempInsulation;
        OverheadCoverage = overheadCoverage;
        WindCoverage = windCoverage;
        IsSnowShelter = isSnowShelter;
    }

    public ShelterFeature(string name, ShelterType type) : base(name)
    {
        ShelterType = type;
        var config = GetConfig(type);
        TemperatureInsulation = config.BaseInsulation;
        OverheadCoverage = config.BaseOverhead;
        WindCoverage = config.BaseWind;
        IsSnowShelter = config.IsSnowShelter;
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public ShelterFeature() : base() { }

    #endregion

    #region Improvement System

    /// <summary>
    /// Base improvement per material before effectiveness modifier.
    /// </summary>
    private const double BaseImprovement = 0.05; // 5%

    /// <summary>
    /// Improve a specific aspect of the shelter using a material.
    /// Returns the actual improvement applied (after diminishing returns).
    /// </summary>
    public double Improve(ShelterImprovementType improvementType, Resource material, int quantity = 1)
    {
        double effectiveness = MaterialProperties.GetEffectiveness(material, improvementType);
        double totalImprovement = 0;

        for (int i = 0; i < quantity; i++)
        {
            double improvement = CalculateImprovement(improvementType, effectiveness);
            ApplyImprovement(improvementType, improvement);
            totalImprovement += improvement;

            // Track materials invested for salvage
            if (!MaterialsInvested.ContainsKey(material))
                MaterialsInvested[material] = 0;
            MaterialsInvested[material]++;
        }

        return totalImprovement;
    }

    private double CalculateImprovement(ShelterImprovementType type, double effectiveness)
    {
        double current = GetStatValue(type);
        double cap = GetStatCap(type);

        // Diminishing returns: actual = base × effectiveness × (1 - current/cap)
        double diminishingFactor = 1.0 - (current / cap);
        return BaseImprovement * effectiveness * Math.Max(0, diminishingFactor);
    }

    private void ApplyImprovement(ShelterImprovementType type, double improvement)
    {
        double cap = GetStatCap(type);
        switch (type)
        {
            case ShelterImprovementType.Insulation:
                TemperatureInsulation = Math.Min(cap, TemperatureInsulation + improvement);
                break;
            case ShelterImprovementType.Overhead:
                OverheadCoverage = Math.Min(cap, OverheadCoverage + improvement);
                break;
            case ShelterImprovementType.Wind:
                WindCoverage = Math.Min(cap, WindCoverage + improvement);
                break;
        }
    }

    public double GetStatValue(ShelterImprovementType type) => type switch
    {
        ShelterImprovementType.Insulation => TemperatureInsulation,
        ShelterImprovementType.Overhead => OverheadCoverage,
        ShelterImprovementType.Wind => WindCoverage,
        _ => 0
    };

    private double GetStatCap(ShelterImprovementType type) => type switch
    {
        ShelterImprovementType.Insulation => InsulationCap,
        ShelterImprovementType.Overhead => OverheadCap,
        ShelterImprovementType.Wind => WindCap,
        _ => 1.0
    };

    #endregion

    #region Salvage System

    /// <summary>
    /// Get materials that would be salvaged when rebuilding this shelter.
    /// Returns ~60% of materials invested. Only available for built shelters.
    /// </summary>
    public Dictionary<Resource, int> GetSalvageMaterials()
    {
        if (IsNatural)
            return new Dictionary<Resource, int>();

        var salvage = new Dictionary<Resource, int>();
        foreach (var (material, count) in MaterialsInvested)
        {
            int salvageCount = (int)Math.Floor(count * 0.6); // 60% salvage rate
            if (salvageCount > 0)
                salvage[material] = salvageCount;
        }
        return salvage;
    }

    /// <summary>
    /// Whether this shelter can be rebuilt (upgraded to a better frame type).
    /// </summary>
    public bool CanRebuild => !IsNatural && !IsPortable && ShelterType == ShelterType.BranchFrame;

    #endregion

    #region Weather Damage

    /// <summary>
    /// Update shelter state. Snow shelters degrade in warm temperatures.
    /// </summary>
    public override void Update(int minutes)
    {
        // Snow shelters melt when temperature rises above freezing
        if (IsSnowShelter)
        {
            // Note: We don't have direct access to location temperature here.
            // This will need to be called from Location.Update() with temp info,
            // or we check later during gameplay.
            // For now, we'll add the logic structure and integrate it properly later.
        }
    }

    /// <summary>
    /// Degrade snow shelter when temperature is above freezing.
    /// Called from location with temperature context.
    /// </summary>
    public void DegradeFromWarmth(double locationTempF, int minutes)
    {
        if (!IsSnowShelter) return;
        if (locationTempF <= 40) return;  // Snow stable below 40°F

        // Degrade based on how warm it is and time elapsed
        double warmthDelta = locationTempF - 40;
        double degradeRate = 0.01 * (warmthDelta / 20.0);  // Faster degradation in warmer temps
        double damageAmount = degradeRate * (minutes / 60.0);

        Damage(damageAmount);
    }

    /// <summary>
    /// Apply weather damage to a specific stat.
    /// </summary>
    public void DamageStat(ShelterImprovementType targetStat, double amount)
    {
        switch (targetStat)
        {
            case ShelterImprovementType.Insulation:
                TemperatureInsulation = Math.Max(0, TemperatureInsulation - amount);
                break;
            case ShelterImprovementType.Overhead:
                OverheadCoverage = Math.Max(0, OverheadCoverage - amount);
                break;
            case ShelterImprovementType.Wind:
                WindCoverage = Math.Max(0, WindCoverage - amount);
                break;
        }
    }

    #endregion

    #region Narrative Descriptions

    /// <summary>
    /// Get a narrative description of the shelter's current state.
    /// </summary>
    public string GetNarrativeDescription()
    {
        string insDesc = GetStatNarrative(TemperatureInsulation, "insulation");
        string overDesc = GetStatNarrative(OverheadCoverage, "overhead");
        string windDesc = GetStatNarrative(WindCoverage, "wind");

        // Combine into a cohesive description
        string overall = Quality switch
        {
            < 0.2 => "Your shelter is barely functional.",
            < 0.4 => "Your shelter provides minimal protection.",
            < 0.6 => "Your shelter keeps the worst weather out.",
            < 0.8 => "Your shelter provides solid protection.",
            _ => "Your shelter is snug and secure."
        };

        // Add specific weakness if notable
        double minStat = Math.Min(TemperatureInsulation, Math.Min(OverheadCoverage, WindCoverage));
        string weakness = "";
        if (minStat < 0.4)
        {
            if (minStat == TemperatureInsulation)
                weakness = " The cold seeps through.";
            else if (minStat == OverheadCoverage)
                weakness = " Rain finds its way in.";
            else
                weakness = " Wind cuts through the gaps.";
        }

        return overall + weakness;
    }

    private static string GetStatNarrative(double value, string statType) => value switch
    {
        < 0.2 => statType switch
        {
            "insulation" => "barely any warmth",
            "overhead" => "open to the sky",
            "wind" => "wind cuts straight through",
            _ => "minimal"
        },
        < 0.4 => statType switch
        {
            "insulation" => "thin protection from cold",
            "overhead" => "leaky overhead",
            "wind" => "drafty walls",
            _ => "weak"
        },
        < 0.6 => statType switch
        {
            "insulation" => "moderate warmth",
            "overhead" => "keeps most rain out",
            "wind" => "blocks most wind",
            _ => "adequate"
        },
        < 0.8 => statType switch
        {
            "insulation" => "good insulation",
            "overhead" => "solid roof",
            "wind" => "windproof walls",
            _ => "good"
        },
        _ => statType switch
        {
            "insulation" => "excellent warmth",
            "overhead" => "waterproof",
            "wind" => "airtight",
            _ => "excellent"
        }
    };

    /// <summary>
    /// Get short status showing current stats and caps.
    /// </summary>
    public string GetStatusText()
    {
        return $"Insulation: {TemperatureInsulation:P0}/{InsulationCap:P0} | " +
               $"Overhead: {OverheadCoverage:P0}/{OverheadCap:P0} | " +
               $"Wind: {WindCoverage:P0}/{WindCap:P0}";
    }

    #endregion

    #region Quality and Status

    /// <summary>
    /// Get the overall quality of the shelter (average of all coverages).
    /// </summary>
    public double Quality => (TemperatureInsulation + OverheadCoverage + WindCoverage) / 3.0;

    /// <summary>
    /// Damage the shelter, reducing all coverage values proportionally.
    /// </summary>
    /// <param name="amount">Amount to reduce quality (0-1)</param>
    public void Damage(double amount)
    {
        TemperatureInsulation = Math.Max(0, TemperatureInsulation - amount);
        OverheadCoverage = Math.Max(0, OverheadCoverage - amount);
        WindCoverage = Math.Max(0, WindCoverage - amount);
    }

    /// <summary>
    /// Repair the shelter, increasing coverage values.
    /// </summary>
    /// <param name="amount">Amount to increase quality (0-1)</param>
    public void Repair(double amount)
    {
        TemperatureInsulation = Math.Min(InsulationCap, TemperatureInsulation + amount);
        OverheadCoverage = Math.Min(OverheadCap, OverheadCoverage + amount);
        WindCoverage = Math.Min(WindCap, WindCoverage + amount);
    }

    /// <summary>
    /// Check if the shelter is still functional (any protection remaining).
    /// </summary>
    public bool IsDestroyed => Quality <= 0.05;

    /// <summary>
    /// Check if shelter is compromised (quality below safe threshold).
    /// Used for events about shelter needing repair.
    /// </summary>
    public bool IsCompromised => Quality <= 0.3;

    /// <summary>
    /// Check if shelter is weakened but still functional.
    /// Used for warning events before shelter fails.
    /// </summary>
    public bool IsWeakened => Quality > 0.3 && Quality <= 0.5;

    /// <summary>
    /// Check if shelter needs repair (any damage).
    /// </summary>
    public bool NeedsRepair => Quality < 1.0 && !IsDestroyed;

    #endregion

    #region Save/Load Support

    /// <summary>
    /// Restore shelter state from save data.
    /// </summary>
    internal void RestoreState(double tempInsulation, double overheadCoverage, double windCoverage)
    {
        TemperatureInsulation = tempInsulation;
        OverheadCoverage = overheadCoverage;
        WindCoverage = windCoverage;
    }

    #endregion

    public override FeatureUIInfo? GetUIInfo()
    {
        if (IsDestroyed) return null;
        return new FeatureUIInfo(
            "shelter",
            Name,
            $"{(int)(TemperatureInsulation * 100)}% ins, {(int)(WindCoverage * 100)}% wind",
            null);
    }

    #region Factory Methods

    /// <summary>
    /// Create a branch frame shelter (basic, quick to build).
    /// </summary>
    public static ShelterFeature CreateBranchFrame() => new("Branch Frame Shelter", ShelterType.BranchFrame);

    /// <summary>
    /// Create a log frame shelter (sturdier, higher caps).
    /// </summary>
    public static ShelterFeature CreateLogFrame() => new("Log Frame Shelter", ShelterType.LogFrame);

    /// <summary>
    /// Create a rock overhang shelter (natural, great overhead but drafty).
    /// </summary>
    public static ShelterFeature CreateRockOverhang() => new("Rock Overhang", ShelterType.RockOverhang);

    /// <summary>
    /// Create a cave shelter (natural, excellent overhead, wind at mouth).
    /// </summary>
    public static ShelterFeature CreateCave() => new("Cave", ShelterType.Cave);

    /// <summary>
    /// Create a dense thicket shelter (natural, good wind block, leaky roof).
    /// </summary>
    public static ShelterFeature CreateDenseThicket() => new("Dense Thicket", ShelterType.DenseThicket);

    /// <summary>
    /// Create a fallen tree shelter (natural, quick shelter, low ceiling).
    /// </summary>
    public static ShelterFeature CreateFallenTree() => new("Fallen Tree Shelter", ShelterType.FallenTree);

    /// <summary>
    /// Create a snow shelter dug into deep snow.
    /// Excellent insulation and protection, but melts in warm temperatures.
    /// Cannot be improved (it's packed snow).
    /// </summary>
    public static ShelterFeature CreateSnowShelter() => new("Snow shelter",
        tempInsulation: 0.6,  // Snow is excellent insulator
        overheadCoverage: 0.9,
        windCoverage: 0.8,
        isSnowShelter: true   // Will degrade if temp > 40°F
    )
    { ShelterType = ShelterType.SnowShelter };

    /// <summary>
    /// Create a hide tent supported by wooden poles.
    /// Good all-around protection, portable. Limited improvement potential.
    /// </summary>
    public static ShelterFeature CreateHideTent() => new("Hide tent", ShelterType.HideTent);

    /// <summary>
    /// Create a shelter from a deployed tent.
    /// The tent is stored so it can be packed up later.
    /// </summary>
    public static ShelterFeature CreateFromTent(text_survival.Items.Gear tent)
    {
        if (!tent.IsTent)
            throw new ArgumentException("Gear is not a tent", nameof(tent));

        var shelter = new ShelterFeature(
            tent.Name,
            tent.ShelterTempInsulation,
            tent.ShelterOverheadCoverage,
            tent.ShelterWindCoverage,
            isSnowShelter: false
        );
        shelter.SourceTent = tent;
        shelter.ShelterType = ShelterType.HideTent;
        return shelter;
    }

    #endregion

    public override List<Resource> ProvidedResources() => [];
}
