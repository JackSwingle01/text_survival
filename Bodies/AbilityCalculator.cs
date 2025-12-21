namespace text_survival.Bodies;

public static class AbilityCalculator
{
    public static double CalculateStrength(Body body, CapacityModifierContainer effectModifiers)
    {
        var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);

        double bfPercent = body.BodyFatPercentage;
        double musclePercent = body.MusclePercentage;

        // Base strength that everyone has
        double baseStrength = 0.30;

        // Muscle contribution with diminishing returns
        double muscleContribution;
        if (musclePercent < 0.2)
            muscleContribution = musclePercent * 2.5;
        else if (musclePercent < 0.4)
            muscleContribution = 0.5 + (musclePercent - 0.2) * 1.0;
        else
            muscleContribution = 0.7 + (musclePercent - 0.4) * 0.5;

        muscleContribution += body.MuscleKG / 3 / 100;

        double fatBonus = body.BodyFatKG / 50 / 100;
        fatBonus -= (bfPercent < 0.05) ? (0.05 - bfPercent) * 3.0 : 0;

        double manipulationContribution = 0.80 + 0.20 * capacities.Manipulation;
        double movingContribution = 0.50 + 0.50 * capacities.Moving;
        double bloodPumping = capacities.BloodPumping;
        double bodyContribution = baseStrength + muscleContribution + fatBonus;

        return bodyContribution * movingContribution * manipulationContribution * bloodPumping;
    }

    public static double CalculateSpeed(Body body, CapacityModifierContainer effectModifiers)
    {
        var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);

        double baseWeight = body.WeightKG - body.MuscleKG - body.BodyFatKG;
        double bfPercent = body.BodyFatPercentage;
        double musclePercent = body.MusclePercentage;

        double muscleModifier = (musclePercent - Body.BaselineHumanStats.musclePercent) / 1.5;
        muscleModifier += body.MuscleKG / 20 / 100;

        double fatPenalty;
        if (bfPercent < 0.10)
            fatPenalty = -0.01;
        else if (bfPercent <= Body.BaselineHumanStats.fatPercent)
            fatPenalty = ((bfPercent - 0.10) * 0.20) - 0.01;
        else
            fatPenalty = (bfPercent - Body.BaselineHumanStats.fatPercent) * 1.5;
        fatPenalty += body.BodyFatKG / 10 / 100;

        double structuralWeightRatio = baseWeight / body.WeightKG / 0.45;
        double weightEffect = -(Math.Pow(structuralWeightRatio, 0.7) - 1.0);

        double sizeRatio = body.WeightKG / Body.BaselineHumanStats.overallWeight;
        double sizeModifier = 1 - 0.03 * Math.Log(sizeRatio, 2);

        return capacities.Moving * (1 + muscleModifier - fatPenalty + weightEffect) * sizeModifier;
    }

    public static double CalculateVitality(Body body, CapacityModifierContainer effectModifiers)
    {
        var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);

        // Vitality = minimum of critical life-sustaining systems
        // If any critical system fails (heart, lungs, or brain), you die
        return Math.Min(
            capacities.Breathing,
            Math.Min(capacities.BloodPumping, capacities.Consciousness)
        );
    }

    // Overload for simpler calls without effect modifiers
    public static double CalculateVitality(Body body) => CalculateVitality(body, new CapacityModifierContainer());

    public static double CalculatePerception(Body body, CapacityModifierContainer effectModifiers)
    {
        var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);
        return (capacities.Sight + capacities.Hearing) / 2;
    }

    // Consciousness impairment check - used for mental fog effects
    public static bool IsConsciousnessImpaired(double consciousness) => consciousness < 0.5;

    // Doesn't use capacities, just body composition
    public static double CalculateColdResistance(Body body)
    {
        // Base resistance: fur, feathers, blubber (species-dependent)
        double baseResistance = body.BaseColdResistance;
        
        // Body fat provides modest insulation, diminishing returns past normal levels
        double bfPercent = body.BodyFatPercentage;
        double fatInsulation;

        if (bfPercent < 0.05)
            fatInsulation = bfPercent / 0.05 * 0.05;  // 0-5% -> 0-5% insulation (dangerously thin)
        else if (bfPercent < 0.15)
            fatInsulation = 0.05 + ((bfPercent - 0.05) / 0.10 * 0.10);  // 5-15% -> 5-15% insulation
        else if (bfPercent < 0.30)
            fatInsulation = 0.15 + ((bfPercent - 0.15) / 0.15 * 0.05);  // 15-30% -> 15-20% insulation
        else
            fatInsulation = 0.20;  // Cap at 20% - you're not a walrus

        return Math.Clamp(baseResistance + fatInsulation, 0, 0.95);
    }
}