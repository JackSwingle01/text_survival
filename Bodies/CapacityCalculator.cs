namespace text_survival.Bodies;

public static class CapacityCalculator
{
    public static CapacityContainer GetCapacities(Body body, CapacityModifierContainer effectModifiers)
    {
        CapacityContainer baseCapacities = new();
        foreach (var part in body.Parts)
        {
            baseCapacities += GetRegionCapacities(part);
        }
        // Apply effect modifiers (includes survival stat effects like Hungry, Thirsty, Tired)
        var withEffects = baseCapacities.ApplyModifier(effectModifiers);

        // Blood multiplies BloodPumping (Heart pumps, Blood is what gets pumped)
        withEffects.BloodPumping *= body.Blood.Condition;

        // Apply cascading effects (pass blood condition for circulation cascade)
        return ApplyCascadingEffects(withEffects, body.Blood.Condition);
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

    private static CapacityContainer ApplyCascadingEffects(CapacityContainer baseCapacities, double bloodCondition)
    {
        var result = baseCapacities;

        // Blood loss fatal at 50% - circulation fails
        // Only triggers from actual blood loss, not BloodPumping capacity reductions (cold, etc.)
        if (bloodCondition < 1.0)
        {
            double circulationPenalty = Math.Max(0, (bloodCondition - 0.5) / 0.5);
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
}