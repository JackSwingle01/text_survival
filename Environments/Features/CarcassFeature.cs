using System.Text.Json.Serialization;
using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Represents a dead animal carcass that can be butchered for resources.
/// Absorbs all butchering logic from ButcherRunner.
/// Carcasses decay over time - meat spoils but bone/hide/sinew remain.
/// </summary>
public class CarcassFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => !IsCompletelyButchered ? "restaurant" : null;
    public override int IconPriority => 3;

    // Core identity
    public string AnimalName { get; set; } = "";
    public double BodyWeightKg { get; set; }

    // Decay tracking
    public double HoursSinceDeath { get; set; }

    // Butchering progress
    public double MinutesButchered { get; set; }

    // Yields remaining (initialized on construction, decremented as harvested)
    public double MeatRemainingKg { get; set; }
    public double BoneRemainingKg { get; set; }
    public double HideRemainingKg { get; set; }
    public double SinewRemainingKg { get; set; }
    public double FatRemainingKg { get; set; }
    public double IvoryRemainingKg { get; set; }  // Mammoth only
    public double MammothHideRemainingKg { get; set; }  // Mammoth only

    // Yield percentages (normal animals)
    private const double MeatPct = 0.40;
    private const double BonePct = 0.15;
    private const double HidePct = 0.10;
    private const double SinewPct = 0.05;
    private const double FatPct = 0.08;

    // Time estimation: ~2 minutes per kg of total yield
    private const double MinutesPerKgYield = 2.0;

    [JsonConstructor]
    public CarcassFeature() : base("carcass") { }

    public CarcassFeature(string animalName, double bodyWeightKg) : base($"{animalName} carcass")
    {
        AnimalName = animalName;
        BodyWeightKg = bodyWeightKg;
        InitializeYields();
    }

    /// <summary>
    /// Create a carcass from a territory-based animal selection.
    /// Uses default weight for the animal type.
    /// </summary>
    public static CarcassFeature FromAnimalName(string animalName)
    {
        return new CarcassFeature(animalName, GetDefaultWeight(animalName));
    }

    /// <summary>
    /// Get default body weight for common animal types.
    /// </summary>
    public static double GetDefaultWeight(string animalName) => animalName.ToLower() switch
    {
        "deer" => 80,
        "rabbit" => 2,
        "wolf" => 40,
        "fox" => 6,
        "ptarmigan" => 0.5,
        "bear" => 200,
        "cave bear" => 400,
        "rat" => 0.3,
        "mammoth" or "woolly mammoth" => 4000,
        "saber-tooth tiger" or "saber-tooth" => 200,
        _ => 30  // Default to medium-sized animal
    };

    private void InitializeYields()
    {
        string name = AnimalName.ToLower();

        // Special handling for megafauna - fixed trophy yields
        if (name.Contains("mammoth"))
        {
            MeatRemainingKg = 50;  // Multiple trips or caching required
            BoneRemainingKg = 4;
            IvoryRemainingKg = 4;  // 2 tusks
            MammothHideRemainingKg = 15;
            SinewRemainingKg = 4;
            FatRemainingKg = 6;
            HideRemainingKg = 0;  // Uses MammothHide instead
        }
        else
        {
            // Normal animals: yields proportional to body weight
            MeatRemainingKg = BodyWeightKg * MeatPct;
            BoneRemainingKg = BodyWeightKg * BonePct;
            HideRemainingKg = BodyWeightKg * HidePct;
            SinewRemainingKg = BodyWeightKg * SinewPct;
            FatRemainingKg = BodyWeightKg * FatPct;
        }
    }

    /// <summary>
    /// Advance decay based on elapsed time.
    /// </summary>
    public override void Update(int minutes)
    {
        HoursSinceDeath += minutes / 60.0;
    }

    /// <summary>
    /// Calculate decay level from hours since death.
    /// Returns 0-1 where 0=fresh, 1=completely spoiled.
    /// </summary>
    public double DecayLevel
    {
        get
        {
            // Fresh: 0-4 hours (slow decay)
            // Good: 4-12 hours (moderate decay)
            // Questionable: 12-24 hours (faster decay)
            // Spoiled: 24+ hours (capped at 1.0)
            return HoursSinceDeath switch
            {
                <= 4 => HoursSinceDeath * 0.05,  // 0-0.2
                <= 12 => 0.2 + (HoursSinceDeath - 4) * 0.0375,  // 0.2-0.5
                <= 24 => 0.5 + (HoursSinceDeath - 12) * 0.025,  // 0.5-0.8
                _ => Math.Min(1.0, 0.8 + (HoursSinceDeath - 24) * 0.02)  // 0.8-1.0
            };
        }
    }

    /// <summary>
    /// Get human-readable decay description.
    /// </summary>
    public string GetDecayDescription() => DecayLevel switch
    {
        < 0.2 => "fresh",
        < 0.5 => "good condition",
        < 0.8 => "starting to spoil",
        _ => "spoiled"
    };

    /// <summary>
    /// Total remaining yield in kg (for time estimation).
    /// </summary>
    public double GetTotalRemainingKg() =>
        MeatRemainingKg + BoneRemainingKg + HideRemainingKg +
        SinewRemainingKg + FatRemainingKg + IvoryRemainingKg + MammothHideRemainingKg;

    /// <summary>
    /// Estimated minutes to completely butcher remaining carcass.
    /// </summary>
    public int GetRemainingMinutes() => (int)Math.Ceiling(GetTotalRemainingKg() * MinutesPerKgYield);

    /// <summary>
    /// Check if carcass has been completely butchered.
    /// </summary>
    public bool IsCompletelyButchered => GetTotalRemainingKg() < 0.01;

    /// <summary>
    /// Get work options for this feature.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (IsCompletelyButchered) yield break;

        string decayDesc = GetDecayDescription();
        yield return new WorkOption(
            $"Butcher {AnimalName} carcass ({decayDesc})",
            "butcher",
            new ButcherStrategy(this)
        );
    }

    /// <summary>
    /// Butcher the carcass for the specified time, yielding resources.
    /// Tool availability and manipulation state affect yields.
    /// </summary>
    /// <param name="minutes">Minutes of work to perform</param>
    /// <param name="hasCuttingTool">Whether player has a cutting tool</param>
    /// <param name="manipulationImpaired">Whether player's manipulation is impaired</param>
    /// <returns>Inventory with harvested resources</returns>
    public Inventory Harvest(int minutes, bool hasCuttingTool, bool manipulationImpaired)
    {
        var result = new Inventory();

        if (IsCompletelyButchered)
            return result;

        // Calculate what fraction of total work this represents
        double totalMinutesRequired = GetTotalRemainingKg() * MinutesPerKgYield;
        double workFraction = Math.Min(1.0, minutes / totalMinutesRequired);

        MinutesButchered += minutes;

        // Apply modifiers
        double yieldMultiplier = 1.0;
        if (manipulationImpaired)
            yieldMultiplier *= 0.8;  // 20% waste from unsteady hands

        // Decay affects meat yield only
        double meatDecayMultiplier = 1.0 - (DecayLevel * 0.8);  // At 100% decay, only 20% meat usable

        // Without cutting tool: can only get meat (reduced) and bone
        // With tool: get everything
        if (hasCuttingTool)
        {
            HarvestFullButcher(result, workFraction, yieldMultiplier, meatDecayMultiplier);
        }
        else
        {
            HarvestWithoutKnife(result, workFraction, yieldMultiplier, meatDecayMultiplier);
        }

        return result;
    }

    private void HarvestFullButcher(Inventory result, double workFraction, double yieldMultiplier, double meatDecayMultiplier)
    {
        // Extract proportional amounts of each resource
        double meatToExtract = MeatRemainingKg * workFraction;
        double boneToExtract = BoneRemainingKg * workFraction;
        double hideToExtract = HideRemainingKg * workFraction;
        double sinewToExtract = SinewRemainingKg * workFraction;
        double fatToExtract = FatRemainingKg * workFraction;
        double ivoryToExtract = IvoryRemainingKg * workFraction;
        double mammothHideToExtract = MammothHideRemainingKg * workFraction;

        // Apply yield multiplier and decay (meat only for decay)
        double effectiveMeat = meatToExtract * yieldMultiplier * meatDecayMultiplier;
        double effectiveBone = boneToExtract * yieldMultiplier;
        double effectiveHide = hideToExtract * yieldMultiplier;
        double effectiveSinew = sinewToExtract * yieldMultiplier;
        double effectiveFat = fatToExtract * yieldMultiplier;
        double effectiveIvory = ivoryToExtract * yieldMultiplier;
        double effectiveMammothHide = mammothHideToExtract * yieldMultiplier;

        // Add to inventory
        AddMeat(result, effectiveMeat);
        AddBones(result, effectiveBone);
        AddHide(result, effectiveHide);
        AddSinew(result, effectiveSinew);
        AddFat(result, effectiveFat);
        AddIvory(result, effectiveIvory);
        AddMammothHide(result, effectiveMammothHide);

        // Decrement remaining yields
        MeatRemainingKg -= meatToExtract;
        BoneRemainingKg -= boneToExtract;
        HideRemainingKg -= hideToExtract;
        SinewRemainingKg -= sinewToExtract;
        FatRemainingKg -= fatToExtract;
        IvoryRemainingKg -= ivoryToExtract;
        MammothHideRemainingKg -= mammothHideToExtract;

        ClampRemaining();
    }

    private void HarvestWithoutKnife(Inventory result, double workFraction, double yieldMultiplier, double meatDecayMultiplier)
    {
        // Without knife: 50% meat yield (tearing by hand), bone only, no hide/sinew/fat
        double meatToExtract = MeatRemainingKg * workFraction;
        double boneToExtract = BoneRemainingKg * workFraction;

        // Reduced meat yield when tearing by hand
        double effectiveMeat = meatToExtract * 0.5 * yieldMultiplier * meatDecayMultiplier;
        double effectiveBone = boneToExtract * yieldMultiplier * 0.67;  // Less efficient bone extraction

        AddMeat(result, effectiveMeat);
        AddBones(result, effectiveBone);

        // Still consume from carcass (wasted)
        MeatRemainingKg -= meatToExtract;
        BoneRemainingKg -= boneToExtract;
        HideRemainingKg -= HideRemainingKg * workFraction;  // Ruined
        SinewRemainingKg -= SinewRemainingKg * workFraction;  // Ruined
        FatRemainingKg -= FatRemainingKg * workFraction;  // Ruined

        ClampRemaining();
    }

    private void ClampRemaining()
    {
        MeatRemainingKg = Math.Max(0, MeatRemainingKg);
        BoneRemainingKg = Math.Max(0, BoneRemainingKg);
        HideRemainingKg = Math.Max(0, HideRemainingKg);
        SinewRemainingKg = Math.Max(0, SinewRemainingKg);
        FatRemainingKg = Math.Max(0, FatRemainingKg);
        IvoryRemainingKg = Math.Max(0, IvoryRemainingKg);
        MammothHideRemainingKg = Math.Max(0, MammothHideRemainingKg);
    }

    // Resource adding helpers (from ButcherRunner)
    private static void AddMeat(Inventory result, double totalKg)
    {
        if (totalKg < 0.1) return;

        // Split into portions (roughly 0.5-1.5kg each)
        while (totalKg > 0.3)
        {
            double portionSize = Math.Min(totalKg, 0.5 + Random.Shared.NextDouble());
            result.Add(Resource.RawMeat, portionSize);
            totalKg -= portionSize;
        }

        // Add any remaining scraps
        if (totalKg > 0.1)
        {
            result.Add(Resource.RawMeat, totalKg);
        }
    }

    private static void AddBones(Inventory result, double totalKg)
    {
        if (totalKg < 0.05) return;

        // Split into bones depending on total weight
        int boneCount = totalKg switch
        {
            < 0.3 => 1,
            < 0.8 => 2,
            < 1.5 => 3,
            _ => 4
        };

        double perBone = totalKg / boneCount;
        for (int i = 0; i < boneCount; i++)
        {
            result.Add(Resource.Bone, perBone);
        }
    }

    private static void AddHide(Inventory result, double totalKg)
    {
        if (totalKg > 0.1)
        {
            result.Add(Resource.Hide, totalKg);
        }
    }

    private static void AddSinew(Inventory result, double totalKg)
    {
        if (totalKg > 0.05)
        {
            result.Add(Resource.Sinew, totalKg);
        }
    }

    private static void AddFat(Inventory result, double totalKg)
    {
        if (totalKg < 0.1) return;

        // Fat comes in chunks - split into 2-3 pieces
        int chunks = totalKg switch
        {
            < 0.3 => 1,
            < 0.6 => 2,
            _ => 3
        };

        double perChunk = totalKg / chunks;
        for (int i = 0; i < chunks; i++)
        {
            result.Add(Resource.RawFat, perChunk);
        }
    }

    private static void AddIvory(Inventory result, double totalKg)
    {
        if (totalKg < 0.1) return;

        // Ivory from mammoth tusks - split into 2 tusks
        double perTusk = totalKg / 2;
        result.Add(Resource.Ivory, perTusk);
        result.Add(Resource.Ivory, perTusk);
    }

    private static void AddMammothHide(Inventory result, double totalKg)
    {
        if (totalKg < 0.5) return;

        // Mammoth hide is trophy material - split into large pieces
        int pieces = totalKg switch
        {
            < 5 => 1,
            < 10 => 2,
            _ => 3
        };

        double perPiece = totalKg / pieces;
        for (int i = 0; i < pieces; i++)
        {
            result.Add(Resource.MammothHide, perPiece);
        }
    }

    /// <summary>
    /// Returns UI display information for this feature.
    /// </summary>
    public override FeatureUIInfo? GetUIInfo()
    {
        if (IsCompletelyButchered)
            return null;

        string status = GetDecayDescription();
        double remainingKg = GetTotalRemainingKg();

        var details = new List<string>
        {
            $"~{remainingKg:F1}kg remaining",
            $"~{GetRemainingMinutes()} min to butcher"
        };

        return new FeatureUIInfo(
            Type: "carcass",
            Label: $"{AnimalName} carcass",
            Status: status,
            Details: details
        );
    }
}
