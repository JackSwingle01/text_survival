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

        // Step 2: Calculate combined material multipliers
        // var baseMultipliers = CapacityContainer.GetBaseCapacityMultiplier();
        // foreach (var material in new List<Tissue> { region.Skin, region.Muscle, region.Bone })
        // {
        //     // todo revisit this, I think this will cause too big of an effect, e.g. 0.5*0.5 = .25
        //     var multipliers = material.GetConditionMultipliers();
        //     baseMultipliers = baseMultipliers.ApplyMultipliers(multipliers);
        // }
        // above is OLD way - may remove after testing
        var materialMultipliers = materials.Select(m => m!.GetConditionMultipliers()).ToList();
        var avgMultipliers = AverageCapacityContainers(materialMultipliers);

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

}