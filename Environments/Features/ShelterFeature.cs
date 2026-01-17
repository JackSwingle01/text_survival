using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Items;

namespace text_survival.Environments.Features;

public enum ShelterType
{
    BranchFrame,
    LogFrame,
    RockOverhang,
    Cave,
    DenseThicket,
    FallenTree,
    SnowShelter,
    HideTent,
    NaturalShelter
}

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
                "Improve shelter",
                "improve_shelter",
                new ShelterImprovementStrategy()
            );
        }
    }

    public override string? MapIcon => !IsDestroyed ? (IsSnowShelter ? "snow_shelter" : "shelter") : null;
    public override int IconPriority => 5; // Shelter is important

    public double TemperatureInsulation { get; set; } = 0;
    public double OverheadCoverage { get; set; } = 0;
    public double WindCoverage { get; set; } = 0;
    public ShelterType ShelterType { get; set; } = ShelterType.BranchFrame;
    public bool IsSnowShelter { get; init; } = false;

    [System.Text.Json.Serialization.JsonIgnore]
    public text_survival.Items.Gear? SourceTent { get; set; }

    public bool IsPortable => SourceTent != null;
    public bool IsNatural => GetConfig(ShelterType).IsNatural;
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
        [ShelterType.HideTent] = new(0.50, 0.90, 0.70, 0.60, 0.95, 0.80, IsNatural: false),

        // Generic natural shelter - high caps, actual limits set per-shelter via constructor
        [ShelterType.NaturalShelter] = new(0.20, 0.50, 0.40, 0.80, 0.90, 0.80, IsNatural: true)
    };

    public static ShelterTypeConfig GetConfig(ShelterType type) =>
        TypeConfigs.TryGetValue(type, out var config) ? config : TypeConfigs[ShelterType.BranchFrame];

    public ShelterTypeConfig Config => GetConfig(ShelterType);

    #endregion

    #region Caps

    // Optional custom caps that override type defaults
    private double? _customInsulationCap;
    private double? _customOverheadCap;
    private double? _customWindCap;

    public double InsulationCap => _customInsulationCap ?? Config.InsulationCap;
    public double OverheadCap => _customOverheadCap ?? Config.OverheadCap;
    public double WindCap => _customWindCap ?? Config.WindCap;

    #endregion

    #region Constructors

    public ShelterFeature(string name, ShelterType type,
        double tempInsulation, double overheadCoverage, double windCoverage,
        double? insulationCap = null, double? overheadCap = null, double? windCap = null) : base(name)
    {
        ShelterType = type;
        TemperatureInsulation = tempInsulation;
        OverheadCoverage = overheadCoverage;
        WindCoverage = windCoverage;
        IsSnowShelter = GetConfig(type).IsSnowShelter;
        _customInsulationCap = insulationCap;
        _customOverheadCap = overheadCap;
        _customWindCap = windCap;
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

    private const double BaseImprovement = 0.05;

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

    public bool CanRebuild => !IsNatural && !IsPortable && ShelterType == ShelterType.BranchFrame;

    #endregion

    #region Weather Damage

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

    public string GetStatusText()
    {
        return $"Insulation: {TemperatureInsulation:P0}/{InsulationCap:P0} | " +
               $"Overhead: {OverheadCoverage:P0}/{OverheadCap:P0} | " +
               $"Wind: {WindCoverage:P0}/{WindCap:P0}";
    }

    #endregion

    #region Quality and Status

    public double Quality => (TemperatureInsulation + OverheadCoverage + WindCoverage) / 3.0;

    public void Damage(double amount)
    {
        TemperatureInsulation = Math.Max(0, TemperatureInsulation - amount);
        OverheadCoverage = Math.Max(0, OverheadCoverage - amount);
        WindCoverage = Math.Max(0, WindCoverage - amount);
    }

    public void Repair(double amount)
    {
        TemperatureInsulation = Math.Min(InsulationCap, TemperatureInsulation + amount);
        OverheadCoverage = Math.Min(OverheadCap, OverheadCoverage + amount);
        WindCoverage = Math.Min(WindCap, WindCoverage + amount);
    }

    public bool IsDestroyed => Quality <= 0.05;

    public bool IsCompromised => Quality <= 0.3;

    public bool IsWeakened => Quality > 0.3 && Quality <= 0.5;

    public bool NeedsRepair => Quality < 1.0 && !IsDestroyed;

    #endregion

    #region Save/Load Support

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

    public static ShelterFeature CreateBranchFrame() => new("Branch Frame Shelter", ShelterType.BranchFrame);

    public static ShelterFeature CreateLogFrame() => new("Log Frame Shelter", ShelterType.LogFrame);

    public static ShelterFeature CreateRockOverhang() => new("Rock Overhang", ShelterType.RockOverhang);

    public static ShelterFeature CreateCave() => new("Cave", ShelterType.Cave);

    public static ShelterFeature CreateDenseThicket() => new("Dense Thicket", ShelterType.DenseThicket);

    public static ShelterFeature CreateFallenTree() => new("Fallen Tree Shelter", ShelterType.FallenTree);

    public static ShelterFeature CreateSnowShelter() =>
        new("Snow shelter", ShelterType.SnowShelter, 0.6, 0.9, 0.8);

    public static ShelterFeature CreateHideTent() => new("Hide tent", ShelterType.HideTent);

    public static ShelterFeature CreateFromTent(text_survival.Items.Gear tent)
    {
        if (!tent.IsTent)
            throw new ArgumentException("Gear is not a tent", nameof(tent));

        var shelter = new ShelterFeature(
            tent.Name,
            ShelterType.HideTent,
            tent.ShelterTempInsulation,
            tent.ShelterOverheadCoverage,
            tent.ShelterWindCoverage
        );
        shelter.SourceTent = tent;
        return shelter;
    }

    #endregion

    public override List<Resource> ProvidedResources() => [];
}
