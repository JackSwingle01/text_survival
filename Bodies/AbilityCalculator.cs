namespace text_survival.Bodies;

public static class AbilityCalculator
{
    public static double CalculateStrength(Body body)
    {
        var capacities = CapacityCalculator.GetCapacities(body);

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

        muscleContribution += body.MuscleKG / 3 / 100;  // 1% bonus for every 3KG muscle

        double fatBonus = body.BodyFatKG / 50 / 100; // 1% bonus for every 50KG of fat
        fatBonus -= (bfPercent < 0.05) ? (0.05 - bfPercent) * 3.0 : 0;

        double manipulationContribution = 0.80 + 0.20 * capacities.Manipulation;
        double movingContribution = 0.50 + 0.50 * capacities.Moving;
        double bloodPumping = capacities.BloodPumping;
        double bodyContribution = baseStrength + muscleContribution + fatBonus;

        return bodyContribution * movingContribution * manipulationContribution * bloodPumping;
    }

    public static double CalculateSpeed(Body body)
    {
        var capacities = CapacityCalculator.GetCapacities(body);

        double baseWeight = body.Weight - body.MuscleKG - body.BodyFatKG;
        double bfPercent = body.BodyFatPercentage;
        double musclePercent = body.MusclePercentage;

        // 1% faster/slower for each 1.5% difference from human baseline (30%)
        double muscleModifier = (musclePercent - Body.BaselineHumanStats.musclePercent) / 1.5;
        muscleModifier += body.MuscleKG / 20 / 100; // 1% bonus for every 20KG of muscle

        double fatPenalty;
        if (bfPercent < 0.10)
        {
            fatPenalty = -0.01;
        }
        else if (bfPercent <= Body.BaselineHumanStats.fatPercent)
        {
            fatPenalty = ((bfPercent - 0.10) * 0.20) - 0.01;
        }
        else
        {
            fatPenalty = (bfPercent - Body.BaselineHumanStats.fatPercent) * 1.5;
        }
        fatPenalty += body.BodyFatKG / 10 / 100; // 1% penalty per 10kg of fat

        // Penalty for excess weight relative to frame
        double structuralWeightRatio = baseWeight / body.Weight / 0.45;
        double weightEffect = -(Math.Pow(structuralWeightRatio, 0.7) - 1.0);

        // Smaller creatures are faster, larger ones slower
        double sizeRatio = body.Weight / Body.BaselineHumanStats.overallWeight;
        double sizeModifier = 1 - 0.03 * Math.Log(sizeRatio, 2);

        return capacities.Moving * (1 + muscleModifier - fatPenalty + weightEffect) * sizeModifier;
    }

    public static double CalculateVitality(Body body)
    {
        var capacities = CapacityCalculator.GetCapacities(body);

        double bfPercent = body.BodyFatPercentage;
        double musclePercent = body.MusclePercentage;

        double organFunction = (2 * (capacities.Breathing + capacities.BloodPumping) + capacities.Digestion) / 5;

        double baseMultiplier = 0.7;
        double muscleContribution = musclePercent * 0.25;
        double fatContribution;

        if (bfPercent < 0.10)
            fatContribution = bfPercent * 0.5;
        else if (bfPercent < 0.25)
            fatContribution = 0.05;
        else
            fatContribution = 0.05 - (bfPercent - 0.25) * 0.1;

        double bodyComposition = baseMultiplier + muscleContribution + fatContribution;

        return organFunction; // * bodyComposition;
    }

    public static double CalculatePerception(Body body)
    {
        var capacities = CapacityCalculator.GetCapacities(body);
        return (capacities.Sight + capacities.Hearing) / 2;
    }

    public static double CalculateColdResistance(Body body)
    {
        double bfPercent = body.BodyFatPercentage;

        double baseColdResistance = 0.5;
        double fatInsulation;

        if (bfPercent < 0.05)
            fatInsulation = bfPercent / 0.05 * 0.1;
        else if (bfPercent < 0.15)
            fatInsulation = 0.1 + ((bfPercent - 0.05) / 0.1 * 0.15);
        else
            fatInsulation = 0.25 + ((bfPercent - 0.15) * 0.15);

        return baseColdResistance + fatInsulation;
    }
}