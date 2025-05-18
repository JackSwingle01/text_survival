using text_survival.Bodies;
using text_survival.Effects;
using text_survival.IO;

namespace text_survival.Survival;

public class TemperatureModule
{
    private readonly Body body;
    private readonly EffectRegistry _effectRegistry;
    public const double BaseBodyTemperature = 98.6F;
    public double BodyTemperature { get; private set; }
    public bool IsWarming { get; private set; }
    public bool IsDangerousTemperature { get; private set; }
    public TemperatureEnum TemperatureEffect { get; private set; }

    private const double SevereHypothermiaThreshold = 89.6; // °F
    private const double HypothermiaThreshold = 95.0;  // °F
    private const double HyperthermiaThreshold = 99.5; // °F  
    private const double SevereHyperthermiaThreshold = 104.0; // °F

    private const double ShiveringThreshold = 97.0; // °F
    private const double SweatingThreshold = 100.0; // °F


    public TemperatureModule(Body body, EffectRegistry effectRegistry)
    {
        this.body = body;
        this._effectRegistry = effectRegistry;
        BodyTemperature = BaseBodyTemperature;
        TemperatureEffect = TemperatureEnum.Warm;
        IsDangerousTemperature = false;
    }
    public enum TemperatureEnum
    {
        Warm,
        Cool,
        Cold,
        Freezing,
        Hot,
        HeatExhaustion,
    }

    public void Update(double environmentalTemp, double equipmentInsulation)
    {
        TemperatureEnum oldTemperature = TemperatureEffect;

        // double vitality = player.Body.CalculateVitality();
        // double baseColdResistance = player.Body.CalculateColdResistance();

        UpdateTemperatureTick(environmentalTemp, equipmentInsulation);

        UpdateTemperatureEffects(environmentalTemp);

        ApplyTemperatureInjuries();

        if (oldTemperature != TemperatureEffect)
        {
            WriteTemperatureEffectMessage(TemperatureEffect);
        }
    }

    private void UpdateTemperatureTick(double environmentalTemp, double equipmentInsulation)
    {
        double metabolicHeatGeneration = body.CalculateMetabolicRate() / 24000;
        var shiveringEffects = _effectRegistry.GetEffectsByKind("Shivering")
            .Where(e => e.IsActive)
            .Cast<ShiveringEffect>();

        double shiveringBoost = shiveringEffects.Sum(e => e.GetMetabolismBoost());
        if (shiveringBoost > 0)
        {
            metabolicHeatGeneration *= (1 + shiveringBoost);
        }

        BodyTemperature += metabolicHeatGeneration;

        double naturalInsulation = Math.Clamp(body.CalculateColdResistance(), 0, 1); // 0-1
        double totalInsulation = Math.Clamp(naturalInsulation + equipmentInsulation, 0, 0.95);

        double skinTemp = BodyTemperature - 8.4;
        double tempDifferential = environmentalTemp - skinTemp;
        double insulatedDiff = tempDifferential * (1 - totalInsulation);
        double tempDiffMagnitude = Math.Abs(insulatedDiff);
        double baseRate = 1.0 / 120.0;
        double exponentialFactor = 1.0 + (tempDiffMagnitude / 40.0);
        double rate = baseRate * exponentialFactor;

        // double surfaceAreaFactor = Math.Pow(body.Weight / 70.0, -0.2);

        double tempChange = insulatedDiff * rate;
        BodyTemperature += tempChange;

        IsWarming = tempChange > 0;

        UpdateTemperatureEffect();

        if (BodyTemperature < SevereHypothermiaThreshold)
        {
            IsDangerousTemperature = true;
        }
        else if (BodyTemperature >= SevereHyperthermiaThreshold)
        {
            IsDangerousTemperature = true;
        }
        else
        {
            IsDangerousTemperature = false;
        }
    }
    public DamageInfo? GetTemperatureDamage()
    {
        double damageAmount = 0;
        string damageType = "thermal";

        if (BodyTemperature < SevereHypothermiaThreshold)
        {
            damageAmount = (SevereHypothermiaThreshold - BodyTemperature) / 2.0;
            damageType = "cold";
        }
        else if (BodyTemperature > SevereHyperthermiaThreshold)
        {
            damageAmount = (BodyTemperature - SevereHyperthermiaThreshold) / 2.0;
            damageType = "heat";
        }

        if (damageAmount > 0)
        {
            return new DamageInfo
            {
                Amount = damageAmount,
                Type = damageType,
                IsPenetrating = true,
            };
        }

        return null;
    }

    private void UpdateTemperatureEffect()
    {
        if (BodyTemperature >= BaseBodyTemperature && BodyTemperature <= HyperthermiaThreshold)
        {
            // Normal body temperature, no effects
            TemperatureEffect = TemperatureEnum.Warm;
        }
        else if (BodyTemperature >= HypothermiaThreshold && BodyTemperature < BaseBodyTemperature)
        {
            // Normal body temperature, no effects
            TemperatureEffect = TemperatureEnum.Cool;
        }
        else if (BodyTemperature >= SevereHypothermiaThreshold && BodyTemperature < HypothermiaThreshold)
        {
            // Moderate hypothermia effects
            TemperatureEffect = TemperatureEnum.Cold;
        }
        else if (BodyTemperature < SevereHypothermiaThreshold)
        {
            // Severe hypothermia effects
            TemperatureEffect = TemperatureEnum.Freezing;
        }
        else if (BodyTemperature > HyperthermiaThreshold && BodyTemperature <= SevereHyperthermiaThreshold)
        {
            // Heat exhaustion effects
            TemperatureEffect = TemperatureEnum.Hot;
        }
        else if (BodyTemperature > SevereHyperthermiaThreshold)
        {
            // Heat stroke effects
            TemperatureEffect = TemperatureEnum.HeatExhaustion;
        }
    }

    public static void WriteTemperatureEffectMessage(TemperatureEnum tempEnum)
    {
        switch (tempEnum)
        {
            case TemperatureEnum.Warm:
                Output.WriteLine("You feel warm.");
                break;
            case TemperatureEnum.Cool:
                Output.WriteLine("You feel cool.");
                break;
            case TemperatureEnum.Cold:
                Output.WriteWarning("You feel cold.");
                break;
            case TemperatureEnum.Freezing:
                Output.WriteDanger("You are freezing cold.");
                break;
            case TemperatureEnum.Hot:
                Output.WriteWarning("You feel hot.");
                break;
            case TemperatureEnum.HeatExhaustion:
                Output.WriteDanger("You are burning up.");
                break;
            default:
                Output.WriteDanger("Error: Temperature effect not found.");
                break;
        }
    }

    private void UpdateTemperatureEffects(double environmentalTemp)
    {
        // Apply or update physiological responses based on current temperature
        UpdatePhysiologicalEffects();

        // Add new temperature injuries if needed
        ApplyTemperatureInjuries();
    }
    private void UpdatePhysiologicalEffects()
    {
        // Shivering response to cold
        if (BodyTemperature < ShiveringThreshold)
        {
            // Calculate severity based on temperature
            double severity = Math.Clamp((ShiveringThreshold - BodyTemperature) / 5.0, 0.1, 1.0);

            // Apply to whole body (will handle stacking through EffectRegistry)
            var bodyCore = body.GetAllParts()[0];
            var shiveringEffect = new ShiveringEffect(bodyCore, severity);

            _effectRegistry.AddEffect(shiveringEffect);
        }
        else
        {
            // Remove shivering effects when temperature normalizes
            var shiveringEffects = _effectRegistry.GetEffectsByKind("Shivering");
            foreach (var effect in shiveringEffects)
            {
                _effectRegistry.RemoveEffect(effect);
            }
        }

        // Sweating response to heat
        if (BodyTemperature > SweatingThreshold)
        {
            // Calculate severity based on temperature
            double severity = Math.Clamp((BodyTemperature - SweatingThreshold) / 4.0, 0.1, 1.0);

            // Apply to whole body (will handle stacking through EffectRegistry)
            var bodyCore = body.GetAllParts()[0];
            var sweatingEffect = new SweatingEffect(bodyCore, severity);

            _effectRegistry.AddEffect(sweatingEffect);
        }
        else
        {
            // Remove sweating effects when temperature normalizes
            var sweatingEffects = _effectRegistry.GetEffectsByKind("Sweating");
            foreach (var effect in sweatingEffects)
            {
                _effectRegistry.RemoveEffect(effect);
            }
        }
    }

    private void ApplyTemperatureInjuries()
    {
        // Hypothermia (system-wide cold injury)
        if (BodyTemperature < HypothermiaThreshold)
        {
            // Calculate severity based on temperature
            double severity = Math.Clamp((HypothermiaThreshold - BodyTemperature) / 10.0, 0.1, 1.0);

            // Apply to whole body (will handle stacking through EffectRegistry)
            var bodyCore = body.GetPartsToNDepth(1)![0];
            var hypothermia = new TemperatureInjury(
                TemperatureInjury.TemperatureInjuryType.Hypothermia,
                "Cold exposure",
                bodyCore,
                severity);

            _effectRegistry.AddEffect(hypothermia);
        }

        // Hyperthermia (system-wide heat injury)
        if (BodyTemperature > HyperthermiaThreshold)
        {
            // Calculate severity based on temperature
            double severity = Math.Clamp((BodyTemperature - HyperthermiaThreshold) / 10.0, 0.1, 1.0);

            // Apply to whole body (will handle stacking through EffectRegistry)
            var bodyCore = body.GetAllParts()[0];
            var hyperthermia = new TemperatureInjury(
                TemperatureInjury.TemperatureInjuryType.Hyperthermia,
                "Heat exposure",
                bodyCore,
                severity);

            _effectRegistry.AddEffect(hyperthermia);
        }

        // Severe hypothermia causes frostbite on extremities
        if (BodyTemperature < SevereHypothermiaThreshold)
        {
            // Get extremities (hands and feet)
            var extremities = body.GetAllParts()
                .Where(p => p.Name.Contains("Hand") || p.Name.Contains("Foot"))
                .ToList();

            foreach (var extremity in extremities)
            {
                // Calculate severity based on temperature
                double severity = Math.Clamp((SevereHypothermiaThreshold - BodyTemperature) / 5.0, 0.1, 1.0);

                // Apply frostbite to extremity (will handle stacking through EffectRegistry)
                var frostbite = new TemperatureInjury(
                    TemperatureInjury.TemperatureInjuryType.Frostbite,
                    "Extreme cold",
                    extremity,
                    severity);

                _effectRegistry.AddEffect(frostbite);
            }
        }

        // Severe hyperthermia causes burns
        if (BodyTemperature > SevereHyperthermiaThreshold)
        {
            // Get skin or exposed parts
            var exposedParts = body.GetAllParts()
                .Where(p => p.Name.Contains("Skin") || p.Name.Contains("Face"))
                .ToList();

            if (exposedParts.Count == 0) // Fallback if no specific skin parts
            {
                exposedParts = [body.GetAllParts()[0]]; // Use body root
            }

            foreach (var part in exposedParts)
            {
                // Calculate severity based on temperature
                double severity = Math.Clamp((BodyTemperature - SevereHyperthermiaThreshold) / 5.0, 0.1, 1.0);

                // Apply burn to exposed part (will handle stacking through EffectRegistry)
                var burn = new TemperatureInjury(
                    TemperatureInjury.TemperatureInjuryType.Burn,
                    "Heat exposure",
                    part,
                    severity);

                _effectRegistry.AddEffect(burn);
            }
        }
    }

    public void Describe()
    {
        string tempChange = IsWarming ? "Warming up" : "Getting colder";
        Output.WriteLine("Body Temperature: ", BodyTemperature, "°F (", TemperatureEffect, ")");
    }
}
