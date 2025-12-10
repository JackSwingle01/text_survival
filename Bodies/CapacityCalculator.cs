using text_survival.Effects;

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
        // var baseMultipliers = CapacityContainer.GetBaseCapacityMultiplier();
        // foreach (var material in new List<Tissue> { region.Skin, region.Muscle, region.Bone })
        // {
        //     // todo revisit this, I think this will cause too big of an effect, e.g. 0.5*0.5 = .25
        //     var multipliers = material.GetConditionMultipliers();
        //     baseMultipliers = baseMultipliers.ApplyMultipliers(multipliers);
        // }
        // above is OLD way - may remove after testing

        // Include organs in condition multiplier calculation
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
            double oxygenPenalty = result.Breathing / 0.3; // 0.0 to 1.0
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

    /// <summary>
    /// Calculate capacity modifiers from survival stats (hunger, thirst, exhaustion).
    /// These penalties make the player vulnerable before death.
    /// </summary>
    private static CapacityModifierContainer GetSurvivalStatModifiers(Body body)
    {
        var modifiers = new CapacityModifierContainer();
        var data = body.BundleSurvivalData();

        // Get current survival stats (0-100%)
        double caloriePercent = data.Calories / Survival.SurvivalProcessor.MAX_CALORIES;
        double hydrationPercent = data.Hydration / Survival.SurvivalProcessor.MAX_HYDRATION;
        double energyPercent = data.Energy / Survival.SurvivalProcessor.MAX_ENERGY_MINUTES;

        // ===== HUNGER PENALTIES =====
        // Progressive weakness as calories drop
        if (caloriePercent < 0.50)  // Below 50%
        {
            if (caloriePercent < 0.01) // Starving (0-1%)
            {
                modifiers.SetCapacityModifier(CapacityNames.Moving, -0.40);
                modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.40);
                modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.20);
            }
            else if (caloriePercent < 0.20) // Very hungry (1-20%)
            {
                modifiers.SetCapacityModifier(CapacityNames.Moving, -0.25);
                modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.25);
                modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.10);
            }
            else // Hungry (20-50%)
            {
                modifiers.SetCapacityModifier(CapacityNames.Moving, -0.10);
                modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.10);
            }
        }

        // ===== DEHYDRATION PENALTIES =====
        // Affects consciousness and movement
        if (hydrationPercent < 0.50)
        {
            if (hydrationPercent < 0.01) // Severely dehydrated (0-1%)
            {
                modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.60);
                modifiers.SetCapacityModifier(CapacityNames.Moving, -0.50);
                modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.30);
            }
            else if (hydrationPercent < 0.20) // Very thirsty (1-20%)
            {
                modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.30);
                modifiers.SetCapacityModifier(CapacityNames.Moving, -0.20);
            }
            else // Thirsty (20-50%)
            {
                modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.10);
            }
        }

        // ===== EXHAUSTION PENALTIES =====
        // Severe penalties allowing indefinite wakefulness but with major debuffs
        if (energyPercent < 0.01) // Exhausted (near 0%)
        {
            modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.60);
            modifiers.SetCapacityModifier(CapacityNames.Moving, -0.60);
            modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.40);
        }
        else if (energyPercent < 0.20) // Very tired (1-20%)
        {
            modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.30);
            modifiers.SetCapacityModifier(CapacityNames.Moving, -0.30);
            modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.20);
        }
        else if (energyPercent < 0.50) // Tired (20-50%)
        {
            modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.10);
            modifiers.SetCapacityModifier(CapacityNames.Moving, -0.10);
        }

        return modifiers;
    }

}