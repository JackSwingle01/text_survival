using text_survival.Effects;
using text_survival.Survival;

namespace text_survival.Bodies;

public static class CapacityCalculator
{
    public static CapacityContainer GetCapacities(Body body)
    {
        CapacityContainer total = new();
        foreach (var part in body.Parts)
        {
            total += GetRegionCapacities(part);
        }

        // Apply body-wide effect modifiers
        var bodyModifier = GetEffectCapacityModifiers(body.EffectRegistry);
        total = total.ApplyModifier(bodyModifier);

        // Apply survival stat penalties (hunger, thirst, exhaustion)
        var survivalModifier = GetSurvivalStatModifiers(body);
        total = total.ApplyModifier(survivalModifier);

        // Apply cascading effects
        return ApplyCascadingEffects(total);
    }

    public static CapacityContainer GetRegionCapacities(BodyRegion region)
    {
        // Step 1: Sum all base capacities from organs
        var baseCapacities = new CapacityContainer();
        foreach (var organ in region.Organs)
        {
            baseCapacities += organ.GetBaseCapacities();
        }
        var materials = new[] { region.Skin, region.Muscle, region.Bone }.Where(m => m != null);
        foreach (var material in materials)
        {
            baseCapacities += material!.GetBaseCapacities();
        }

        // Step 2: Calculate combined material multipliers (including organs)
        var allParts = materials.Concat(region.Organs.Cast<Tissue>()).ToList();
        var allMultipliers = allParts.Select(p => p.GetConditionMultipliers()).ToList();
        var avgMultipliers = AverageCapacityContainers(allMultipliers);

        // Step 3: Apply multipliers to base capacities
        return baseCapacities.ApplyMultipliers(avgMultipliers);
    }

    private static CapacityContainer AverageCapacityContainers(List<CapacityContainer> containers)
    {
        if (containers.Count == 0) return CapacityContainer.GetBaseCapacityMultiplier();

        var result = new CapacityContainer();
        var capacityNames = CapacityNames.All;
        foreach (var capacityName in capacityNames)
        {
            double avg = containers.Average(c => c.GetCapacity(capacityName));
            result.SetCapacity(capacityName, avg);
        }
        return result;
    }

    private static CapacityContainer ApplyCascadingEffects(CapacityContainer baseCapacities)
    {
        var result = baseCapacities;

        // Poor blood circulation affects everything
        if (result.BloodPumping < 0.5)
        {
            double circulationPenalty = 1.0 - (0.5 - result.BloodPumping);
            result.Consciousness *= circulationPenalty;
            result.Moving *= circulationPenalty;
            result.Manipulation *= circulationPenalty;
        }

        // Can't breathe? Consciousness drops rapidly
        if (result.Breathing < 0.3)
        {
            double oxygenPenalty = result.Breathing / 0.3;
            result.Consciousness *= oxygenPenalty;
        }

        // Unconscious? Can't do physical actions
        if (result.Consciousness < 0.1)
        {
            result.Moving *= 0.1;
            result.Manipulation *= 0.1;
        }

        return result;
    }

    public static CapacityModifierContainer GetEffectCapacityModifiers(EffectRegistry effectRegistry)
    {
        CapacityModifierContainer total = new();
        var modifiers = effectRegistry.GetAll().Select(e => e.CapacityModifiers).ToList();
        foreach (var mod in modifiers)
        {
            total += mod;
        }
        return total;
    }

    #region Survival Stat Modifiers

    private static CapacityModifierContainer GetSurvivalStatModifiers(Body body)
    {
        double caloriePercent = body.CalorieStore / SurvivalProcessor.MAX_CALORIES;
        double hydrationPercent = body.Hydration / SurvivalProcessor.MAX_HYDRATION;
        double energyPercent = body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES;

        var modifiers = new CapacityModifierContainer();

        modifiers.SetCapacityModifier(CapacityNames.Moving,
            GetMovingModifier(caloriePercent, hydrationPercent, energyPercent));

        modifiers.SetCapacityModifier(CapacityNames.Manipulation,
            GetManipulationModifier(caloriePercent, hydrationPercent, energyPercent));

        modifiers.SetCapacityModifier(CapacityNames.Consciousness,
            GetConsciousnessModifier(caloriePercent, hydrationPercent, energyPercent));

        return modifiers;
    }

    /// <summary>
    /// Combines effectiveness multipliers. 0.6 * 0.7 = 0.42, returns -0.58
    /// </summary>
    private static double CombineMultiplicative(params double[] effectivenessValues)
    {
        double combined = 1.0;
        foreach (var eff in effectivenessValues)
        {
            combined *= eff;
        }
        return combined - 1.0;
    }

    private static double GetMovingModifier(double calories, double hydration, double energy)
    {
        double fromHunger = calories switch
        {
            < 0.01 => 0.65,  // Starving
            < 0.20 => 0.80,  // Very hungry
            < 0.50 => 0.92,  // Hungry
            _ => 1.0
        };

        double fromThirst = hydration switch
        {
            < 0.01 => 0.60,  // Severely dehydrated
            < 0.20 => 0.85,  // Very thirsty
            _ => 1.0
        };

        double fromExhaustion = energy switch
        {
            < 0.01 => 0.55,  // Exhausted
            < 0.20 => 0.75,  // Very tired
            < 0.50 => 0.92,  // Tired
            _ => 1.0
        };

        return CombineMultiplicative(fromHunger, fromThirst, fromExhaustion);
    }

    private static double GetManipulationModifier(double calories, double hydration, double energy)
    {
        double fromHunger = calories switch
        {
            < 0.01 => 0.65,
            < 0.20 => 0.80,
            < 0.50 => 0.92,
            _ => 1.0
        };

        double fromThirst = hydration switch
        {
            < 0.01 => 0.75,
            _ => 1.0
        };

        double fromExhaustion = energy switch
        {
            < 0.01 => 0.70,
            < 0.20 => 0.85,
            _ => 1.0
        };

        return CombineMultiplicative(fromHunger, fromThirst, fromExhaustion);
    }

    private static double GetConsciousnessModifier(double calories, double hydration, double energy)
    {
        double fromHunger = calories switch
        {
            < 0.01 => 0.85,
            < 0.20 => 0.92,
            _ => 1.0
        };

        double fromThirst = hydration switch
        {
            < 0.01 => 0.55,  // Dehydration hits consciousness hard
            < 0.20 => 0.75,
            < 0.50 => 0.92,
            _ => 1.0
        };

        double fromExhaustion = energy switch
        {
            < 0.01 => 0.55,  // Exhaustion hits consciousness hard
            < 0.20 => 0.75,
            < 0.50 => 0.92,
            _ => 1.0
        };

        return CombineMultiplicative(fromHunger, fromThirst, fromExhaustion);
    }

    #endregion
}