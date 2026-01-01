namespace text_survival.Bodies;

public static class CapacityCalculator
{
    public static CapacityContainer GetCapacities(Body body, CapacityModifierContainer effectModifiers)
    {
        // Collect per-region capacities
        var regionCapacities = new Dictionary<string, CapacityContainer>();
        foreach (var part in body.Parts)
        {
            regionCapacities[part.Name] = GetRegionCapacities(part);
        }

        // Weighted combination for limb-dependent capacities
        var baseCapacities = CombineRegionCapacities(regionCapacities);

        // Apply effect modifiers (includes survival stat effects like Hungry, Thirsty, Tired)
        var withEffects = baseCapacities.ApplyModifier(effectModifiers);

        // Blood multiplies BloodPumping (Heart pumps, Blood is what gets pumped)
        withEffects.BloodPumping *= body.Blood.Condition;

        // Apply cascading effects (pass blood condition for circulation cascade)
        return ApplyCascadingEffects(withEffects, body.Blood.Condition);
    }

    private static CapacityContainer CombineRegionCapacities(Dictionary<string, CapacityContainer> regions)
    {
        var result = new CapacityContainer();

        // Weight constants: primary limbs contribute 90%, others contribute 10%
        const double primaryLimbWeight = 0.45;  // per limb (2 limbs = 90%)
        const double otherWeight = 0.02;        // split remaining 10% among 5 other regions

        // Moving: 90% legs, 10% other (destroyed legs = can only crawl/drag)
        result.Moving = WeightedCapacity(regions, CapacityNames.Moving,
            (BodyRegionNames.LeftLeg, primaryLimbWeight),
            (BodyRegionNames.RightLeg, primaryLimbWeight),
            (BodyRegionNames.Head, otherWeight),
            (BodyRegionNames.Chest, otherWeight),
            (BodyRegionNames.Abdomen, otherWeight),
            (BodyRegionNames.LeftArm, otherWeight),
            (BodyRegionNames.RightArm, otherWeight));

        // Manipulation: 90% arms, 10% other
        result.Manipulation = WeightedCapacity(regions, CapacityNames.Manipulation,
            (BodyRegionNames.LeftArm, primaryLimbWeight),
            (BodyRegionNames.RightArm, primaryLimbWeight),
            (BodyRegionNames.Head, otherWeight),
            (BodyRegionNames.Chest, otherWeight),
            (BodyRegionNames.Abdomen, otherWeight),
            (BodyRegionNames.LeftLeg, otherWeight),
            (BodyRegionNames.RightLeg, otherWeight));

        // Other capacities: simple sum (existing behavior)
        foreach (var region in regions.Values)
        {
            result.Breathing += region.Breathing;
            result.Consciousness += region.Consciousness;
            result.BloodPumping += region.BloodPumping;
            result.Sight += region.Sight;
            result.Hearing += region.Hearing;
            result.Digestion += region.Digestion;
        }

        return result;
    }

    private static double WeightedCapacity(
        Dictionary<string, CapacityContainer> regions,
        string capacityName,
        params (string region, double weight)[] weights)
    {
        double total = 0;
        foreach (var (region, weight) in weights)
        {
            if (regions.TryGetValue(region, out var caps))
                total += caps.GetCapacity(capacityName) * weight;
        }
        return total;
    }


    public static CapacityContainer GetRegionCapacities(BodyRegion region)
    {
        // Destroyed region = no functioning organs
        if (region.IsDestroyed)
        {
            return new CapacityContainer(); // All zeros
        }

        // Step 1: Sum all base capacities from organs and tissues
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

        // Step 2: Calculate combined tissue multipliers (region damage affects all capacities)
        var allParts = materials.Concat(region.Organs.Cast<Tissue>()).ToList();
        var allMultipliers = allParts.Select(p => p.GetConditionMultipliers()).ToList();
        var avgMultipliers = AverageCapacityContainers(allMultipliers);

        // Step 3: Apply averaged multipliers
        var result = baseCapacities.ApplyMultipliers(avgMultipliers);

        // Step 4: Critical organ override - if an organ providing full capacity is destroyed, zero it
        foreach (var organ in region.Organs)
        {
            if (organ.Condition <= 0)
            {
                var provided = organ.GetBaseCapacities();
                foreach (var capacityName in CapacityNames.All)
                {
                    if (provided.GetCapacity(capacityName) >= 1.0)
                    {
                        result.SetCapacity(capacityName, 0);
                    }
                }
            }
        }

        return result;
    }

    private static CapacityContainer AverageCapacityContainers(List<CapacityContainer> containers)
    {
        if (containers.Count == 0) return CapacityContainer.GetBaseCapacityMultiplier();

        var result = new CapacityContainer();
        foreach (var capacityName in CapacityNames.All)
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