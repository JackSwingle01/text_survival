using System.Text.Json.Serialization;
using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actors.Animals;
using text_survival.Items;

namespace text_survival.Environments.Features;

public enum ButcheringMode
{
    QuickStrip,      // Fast, meat-focused, messy - more scent, less yield
    Careful,         // Balanced (default)
    FullProcessing   // Slow, maximum yield, bonus sinew
}

public record ButcheringModeConfig(
    double TimeFactor,       // Multiplier on base time
    double MeatYieldFactor,  // Multiplier on meat yield
    double HideYieldFactor,  // 0 = no hide (QuickStrip)
    double BoneYieldFactor,
    double SinewYieldFactor, // 0 = no sinew (QuickStrip)
    double FatYieldFactor,
    double ScentIncrease,    // Added to carcass scent
    double BloodySeverity    // Bloody effect severity applied to player
);

public class CarcassFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => !IsCompletelyButchered ? "carcass" : null;
    public override int IconPriority => 3;

    // Core identity
    public AnimalType AnimalType { get; set; }  // Primary identifier
    public string AnimalName { get; set; } = "";  // Kept for backward compatibility with old saves
    public double BodyWeightKg { get; set; }

    // Decay tracking
    public double RawHoursSinceDeath { get; set; }  // For scent intensity
    public double EffectiveHoursSinceDeath { get; set; }  // Temperature-adjusted, for decay
    public double LastKnownTemperatureF { get; set; } = 32;

    // Butchering progress
    public double MinutesButchered { get; set; }

    /// <summary>
    /// The butchering mode selected for this carcass. Once selected (on first butchering session),
    /// the mode persists for all subsequent sessions. Null means no butchering has started yet.
    /// </summary>
    public ButcheringMode? SelectedMode { get; set; }

    /// <summary>
    /// Progress as a percentage (0-1) based on yields remaining vs estimated initial.
    /// Note: This is approximate since initial yields aren't stored separately.
    /// </summary>
    public double ProgressPct => Math.Clamp(1.0 - (GetTotalRemainingKg() / (BodyWeightKg * 0.78)), 0, 1);

    // Yields remaining (initialized on construction, decremented as harvested)
    public double MeatRemainingKg { get; set; }
    public double BoneRemainingKg { get; set; }
    public double HideRemainingKg { get; set; }
    public double SinewRemainingKg { get; set; }
    public double FatRemainingKg { get; set; }
    public double IvoryRemainingKg { get; set; }  // Mammoth only
    public double MammothHideRemainingKg { get; set; }  // Mammoth only

    // Time estimation: base setup time + ~2 minutes per kg of total yield
    // Base time accounts for positioning, initial cuts, organizing - matters for small game
    private const double BaseButcheringMinutes = 5.0;
    private const double MinutesPerKgYield = 2.0;

    [JsonConstructor]
    public CarcassFeature() : base("carcass") { }

    public CarcassFeature(Animal animal, double harvestedPct = 0, double ageHours = 0)
        : base($"{animal.Name} carcass")
    {
        AnimalType = animal.AnimalType;  // Store enum directly
        AnimalName = animal.Name;  // Keep string for backward compatibility
        BodyWeightKg = animal.Body.WeightKG;
        RawHoursSinceDeath = ageHours;
        EffectiveHoursSinceDeath = ageHours;  // Assume average temp for found carcasses
        InitializeYieldsFromAnimal(animal, harvestedPct);
    }

    public static CarcassFeature FromAnimal(Animal animal, double harvestedPct = 0, double ageHours = 0)
    {
        return new CarcassFeature(animal, harvestedPct, ageHours);
    }

    private void InitializeYieldsFromAnimal(Animal animal, double harvestedPct = 0)
    {
        var body = animal.Body;

        // Process special yields (ivory, mammoth hide, etc.)
        bool hasSpecialHide = false;
        foreach (var (resource, kgYield) in animal.SpecialYields)
        {
            if (resource == Resource.Ivory)
                IvoryRemainingKg = kgYield;
            else if (resource == Resource.MammothHide)
            {
                MammothHideRemainingKg = kgYield;
                hasSpecialHide = true;
            }
            // Extensible for future special materials
        }

        // Composition-based yields
        MeatRemainingKg = body.MuscleKG * 0.65;   // ~65% of muscle is usable meat
        FatRemainingKg = body.BodyFatKG * 0.6;    // ~60% recoverable as rendered fat

        // Still percentage-based (not tracked in body composition)
        double baseWeight = body.WeightKG - body.MuscleKG - body.BodyFatKG;
        BoneRemainingKg = baseWeight * 0.25;
        HideRemainingKg = hasSpecialHide ? 0 : BodyWeightKg * 0.10;  // No regular hide if special
        SinewRemainingKg = BodyWeightKg * 0.05;

        // Cap megafauna yields for gameplay balance
        if (animal.IsMegafauna)
        {
            MeatRemainingKg = Math.Min(MeatRemainingKg, 80);
            FatRemainingKg = Math.Min(FatRemainingKg, 15);
            BoneRemainingKg = Math.Min(BoneRemainingKg, 8);
            HideRemainingKg = Math.Min(HideRemainingKg, 12);
        }

        // Apply pre-harvesting (scavenger consumption)
        if (harvestedPct > 0)
        {
            double remaining = 1 - harvestedPct;
            MeatRemainingKg *= remaining;
            FatRemainingKg *= remaining;
            // Bone/hide/sinew less affected (scavengers go for meat first)
            BoneRemainingKg *= Math.Max(0.7, remaining);
            HideRemainingKg *= Math.Max(0.5, remaining);  // More damage from tearing
            SinewRemainingKg *= Math.Max(0.8, remaining);
        }
    }

    public override void Update(int minutes)
    {
        RawHoursSinceDeath += minutes / 60.0;
    }

    public void ApplyTemperatureDecay(double temperatureF, int minutes)
    {
        LastKnownTemperatureF = temperatureF;
        double decayMultiplier = GetDecayMultiplier(temperatureF);
        EffectiveHoursSinceDeath += (minutes / 60.0) * decayMultiplier;
    }

    private static double GetDecayMultiplier(double temperatureF) => temperatureF switch
    {
        < 0 => 0.1,      // Deep freeze - near-preservation
        < 15 => 0.25,    // Very cold - slow decay
        < 32 => 0.5,     // Freezing point - moderate
        < 50 => 1.0,     // Cool - normal rate
        _ => 2.0         // Warm - fast spoilage
    };

    public bool IsFrozen => LastKnownTemperatureF < 15 && RawHoursSinceDeath > 2;

    public double ScentIntensityBonus { get; set; }  // From butchering activity

    public double ScentIntensity
    {
        get
        {
            double baseScent = RawHoursSinceDeath switch
            {
                < 2 => 0.8,    // Fresh blood - strongest
                < 6 => 0.6,    // Still warm
                < 12 => 0.4,   // Cooling, less scent
                < 24 => 0.2,   // Old, weak scent
                _ => 0.1       // Spoiled - different smell, less attractive
            };

            return Math.Min(1.0, baseScent + ScentIntensityBonus);
        }
    }

    public void ProcessScavenging(double hoursAway, bool predatorActivityNearby)
    {
        if (!predatorActivityNearby || hoursAway < 0.5) return;

        // Higher scent = more attractive to scavengers
        // Up to 15% meat loss per hour at max scent
        double lossRatePerHour = ScentIntensity * 0.15;
        double totalLossPct = Math.Min(0.8, lossRatePerHour * hoursAway);  // Cap at 80% loss

        MeatRemainingKg *= (1 - totalLossPct);

        // Scavengers also damage hide/sinew (tearing)
        if (totalLossPct > 0.3)
        {
            HideRemainingKg *= 0.7;  // 30% hide damage from scavenger activity
            SinewRemainingKg *= 0.8;  // 20% sinew loss
        }
    }

    public double DecayLevel  // 0=fresh, 1=spoiled
    {
        get
        {
            // Fresh: 0-4 effective hours (slow decay)
            // Good: 4-12 effective hours (moderate decay)
            // Questionable: 12-24 effective hours (faster decay)
            // Spoiled: 24+ effective hours (capped at 1.0)
            return EffectiveHoursSinceDeath switch
            {
                <= 4 => EffectiveHoursSinceDeath * 0.05,  // 0-0.2
                <= 12 => 0.2 + (EffectiveHoursSinceDeath - 4) * 0.0375,  // 0.2-0.5
                <= 24 => 0.5 + (EffectiveHoursSinceDeath - 12) * 0.025,  // 0.5-0.8
                _ => Math.Min(1.0, 0.8 + (EffectiveHoursSinceDeath - 24) * 0.02)  // 0.8-1.0
            };
        }
    }

    public string GetDecayDescription()
    {
        string baseDesc = DecayLevel switch
        {
            < 0.2 => "fresh",
            < 0.5 => "good condition",
            < 0.8 => "starting to spoil",
            _ => "spoiled"
        };

        return IsFrozen ? $"{baseDesc}, frozen" : baseDesc;
    }

    // === BUTCHERING MODES ===

    public static ButcheringModeConfig GetModeConfig(ButcheringMode mode) => mode switch
    {
        // QuickStrip: 50% time, 80% meat, no hide/sinew, 50% bone/fat, high scent/blood
        ButcheringMode.QuickStrip => new ButcheringModeConfig(
            TimeFactor: 0.5,
            MeatYieldFactor: 0.8,
            HideYieldFactor: 0,
            BoneYieldFactor: 0.5,
            SinewYieldFactor: 0,
            FatYieldFactor: 0.5,
            ScentIncrease: 0.2,
            BloodySeverity: 0.3
        ),
        // Careful: normal time, full yields
        ButcheringMode.Careful => new ButcheringModeConfig(
            TimeFactor: 1.0,
            MeatYieldFactor: 1.0,
            HideYieldFactor: 1.0,
            BoneYieldFactor: 1.0,
            SinewYieldFactor: 1.0,
            FatYieldFactor: 1.0,
            ScentIncrease: 0.1,
            BloodySeverity: 0.15
        ),
        // FullProcessing: 150% time, +10% meat/fat, +20% sinew, minimal scent/blood
        ButcheringMode.FullProcessing => new ButcheringModeConfig(
            TimeFactor: 1.5,
            MeatYieldFactor: 1.1,  // +10% from careful work
            HideYieldFactor: 1.0,
            BoneYieldFactor: 1.0,
            SinewYieldFactor: 1.2, // +20% from careful extraction
            FatYieldFactor: 1.1,   // +10% from thorough rendering
            ScentIncrease: 0.05,
            BloodySeverity: 0.1
        ),
        _ => GetModeConfig(ButcheringMode.Careful)
    };

    public int GetRemainingMinutes(ButcheringMode mode)
    {
        var config = GetModeConfig(mode);
        double kgTime = GetTotalRemainingKg() * MinutesPerKgYield;
        // Base time + kg-based time, all scaled by mode factor
        return (int)Math.Ceiling((BaseButcheringMinutes + kgTime) * config.TimeFactor);
    }

    public double GetTotalRemainingKg() =>
        MeatRemainingKg + BoneRemainingKg + HideRemainingKg +
        SinewRemainingKg + FatRemainingKg + IvoryRemainingKg + MammothHideRemainingKg;

    public int GetRemainingMinutes() =>
        (int)Math.Ceiling(BaseButcheringMinutes + GetTotalRemainingKg() * MinutesPerKgYield);

    public bool IsCompletelyButchered => GetTotalRemainingKg() < 0.01;

    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (IsCompletelyButchered) yield break;

        // Show progress percentage if butchering has started
        string progressText = SelectedMode != null ? $" ({ProgressPct:P0})" : "";

        yield return new WorkOption(
            $"Butcher {AnimalName} carcass{progressText}",
            "butcher",
            new ButcherStrategy(this)
        );
    }

    public Inventory Harvest(int minutes, bool hasCuttingTool, bool manipulationImpaired,
        ButcheringMode mode = ButcheringMode.Careful)
    {
        var result = new Inventory();

        if (IsCompletelyButchered)
            return result;

        var modeConfig = GetModeConfig(mode);

        // Calculate what fraction of total work this represents
        // Mode affects time: QuickStrip finishes faster per kg
        double adjustedMinutesPerKg = MinutesPerKgYield * modeConfig.TimeFactor;
        double totalMinutesRequired = GetTotalRemainingKg() * adjustedMinutesPerKg;
        double workFraction = Math.Min(1.0, minutes / totalMinutesRequired);

        MinutesButchered += minutes;

        // Apply modifiers
        double yieldMultiplier = 1.0;
        if (manipulationImpaired)
            yieldMultiplier *= 0.8;  // 20% waste from unsteady hands

        // Decay affects meat yield only
        double meatDecayMultiplier = 1.0 - (DecayLevel * 0.8);  // At 100% decay, only 20% meat usable

        // Without cutting tool: can only get meat (reduced) and bone
        // With tool: get everything (mode affects yields)
        if (hasCuttingTool)
        {
            HarvestFullButcher(result, workFraction, yieldMultiplier, meatDecayMultiplier, modeConfig);
        }
        else
        {
            HarvestWithoutKnife(result, workFraction, yieldMultiplier, meatDecayMultiplier);
        }

        return result;
    }

    /// <summary>
    /// Restores yields that couldn't fit in inventory back to the carcass.
    /// Called when player's pack is full - items left on carcass for later butchering.
    /// </summary>
    public void RestoreYields(Inventory leftovers)
    {
        // Add back resources that didn't fit
        foreach (Resource type in Enum.GetValues<Resource>())
        {
            double leftoverWeight = leftovers.Weight(type);
            if (leftoverWeight > 0)
            {
                // Map resources back to carcass yields
                switch (type)
                {
                    case Resource.RawMeat:
                        MeatRemainingKg += leftoverWeight;
                        break;
                    case Resource.Bone:
                        BoneRemainingKg += leftoverWeight;
                        break;
                    case Resource.Hide:
                        HideRemainingKg += leftoverWeight;
                        break;
                    case Resource.Sinew:
                        SinewRemainingKg += leftoverWeight;
                        break;
                    case Resource.RawFat:
                        FatRemainingKg += leftoverWeight;
                        break;
                    case Resource.Ivory:
                        IvoryRemainingKg += leftoverWeight;
                        break;
                    case Resource.MammothHide:
                        MammothHideRemainingKg += leftoverWeight;
                        break;
                }
            }
        }

        ClampRemaining();
    }

    private void HarvestFullButcher(Inventory result, double workFraction, double yieldMultiplier,
        double meatDecayMultiplier, ButcheringModeConfig mode)
    {
        // Extract proportional amounts of each resource
        double meatToExtract = MeatRemainingKg * workFraction;
        double boneToExtract = BoneRemainingKg * workFraction;
        double hideToExtract = HideRemainingKg * workFraction;
        double sinewToExtract = SinewRemainingKg * workFraction;
        double fatToExtract = FatRemainingKg * workFraction;
        double ivoryToExtract = IvoryRemainingKg * workFraction;
        double mammothHideToExtract = MammothHideRemainingKg * workFraction;

        // Apply yield multiplier, decay (meat only), and mode multipliers
        double effectiveMeat = meatToExtract * yieldMultiplier * meatDecayMultiplier * mode.MeatYieldFactor;
        // Guarantee minimum 0.1 kg meat yield for small animals (prevents 0 yield from ptarmigan etc.)
        if (effectiveMeat > 0 && effectiveMeat < 0.1 && MeatRemainingKg >= 0.1)
            effectiveMeat = 0.1;
        double effectiveBone = boneToExtract * yieldMultiplier * mode.BoneYieldFactor;
        double effectiveHide = hideToExtract * yieldMultiplier * mode.HideYieldFactor;
        double effectiveSinew = sinewToExtract * yieldMultiplier * mode.SinewYieldFactor;
        double effectiveFat = fatToExtract * yieldMultiplier * mode.FatYieldFactor;
        // Ivory and mammoth hide not affected by mode (trophy materials)
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

        // Decrement remaining yields (full amount consumed regardless of yield factor)
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
        // Guarantee minimum 0.1 kg meat yield for small animals (prevents 0 yield from ptarmigan etc.)
        if (effectiveMeat > 0 && effectiveMeat < 0.1 && MeatRemainingKg >= 0.1)
            effectiveMeat = 0.1;
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
        if (totalKg < 0.05) return;

        // Split into portions (roughly 0.5-1.5kg each)
        while (totalKg > 0.3)
        {
            double portionSize = Math.Min(totalKg, 0.5 + Random.Shared.NextDouble());
            result.Add(Resource.RawMeat, portionSize);
            totalKg -= portionSize;
        }

        // Add any remaining scraps
        if (totalKg > 0.05)
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

    public override List<Resource> ProvidedResources()
    {
        if (IsCompletelyButchered) return [];
        var resources = new List<Resource>();
        if (MeatRemainingKg > 0) resources.Add(Resource.RawMeat);
        if (BoneRemainingKg > 0) resources.Add(Resource.Bone);
        if (HideRemainingKg > 0) resources.Add(Resource.Hide);
        if (SinewRemainingKg > 0) resources.Add(Resource.Sinew);
        if (FatRemainingKg > 0) resources.Add(Resource.RawFat);
        if (IvoryRemainingKg > 0) resources.Add(Resource.Ivory);
        if (MammothHideRemainingKg > 0) resources.Add(Resource.MammothHide);
        return resources;
    }
}
