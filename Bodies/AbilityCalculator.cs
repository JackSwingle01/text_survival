namespace text_survival.Bodies;

public static class AbilityCalculator
{
    // unified interface that just accepts body
    public static double CalculateStrength(Body body) => CalculateStrength(body.GetBodyStats(), CapacityCalculator.GetCapacities(body));
    public static double CalculateSpeed(Body body) => CalculateSpeed(body.GetBodyStats(), CapacityCalculator.GetCapacities(body));
    public static double CalculateVitality(Body body) => CalculateVitality(body.GetBodyStats(), CapacityCalculator.GetCapacities(body));
    public static double CalculatePerception(Body body) => CalculatePerception(CapacityCalculator.GetCapacities(body));
    public static double CalculateColdResistance(Body body) => CalculateColdResistance(body.GetBodyStats());


    // actual calculator methods
    private static double CalculateStrength(BodyStats stats, CapacityContainer capacities)
    {
        // derived stats
        double bfPercent = stats.FatWeight / stats.BodyWeight;
        double musclePercent = stats.MuscleWeight / stats.BodyWeight;

        // Base strength that everyone has
        double baseStrength = 0.30; // 30% strength from structural aspects

        // Muscle contribution with diminishing returns
        double muscleContribution;
        if (musclePercent < 0.2) // Below normal
            muscleContribution = musclePercent * 2.5; // Rapid gains when building from low muscle
        else if (musclePercent < 0.4) // Normal to athletic
            muscleContribution = 0.5 + (musclePercent - 0.2) * 1.0; // Moderate gains
        else // Athletic+
            muscleContribution = 0.7 + (musclePercent - 0.4) * 0.5; // Diminishing returns

        muscleContribution += stats.MuscleWeight / 3 / 100;  // 1% bonus for every 3KG muscle

        double fatBonus = stats.FatWeight / 50 / 100; // 1% bonus for every 50KG of fat
        // Very low body fat impairs strength
        fatBonus -= (bfPercent < 0.05) ? (0.05 - bfPercent) * 3.0 : 0;

        double manipulationContribution = 0.80 + 0.20 * capacities.Manipulation; // can reduce by 20%
        double movingContribution = 0.50 + 0.50 * capacities.Moving; // can reduce by 50%
        double bloodPumping = capacities.BloodPumping; // Energy delivery - can reduce by 100%
        double bodyContribution = baseStrength + muscleContribution + fatBonus;
        return bodyContribution * movingContribution * manipulationContribution * bloodPumping;
    }

    private static double CalculateSpeed(BodyStats stats, CapacityContainer capacities)
    {
        double movingCapacity = capacities.Moving;

        // derived stats
        double baseWeight = stats.BodyWeight - stats.MuscleWeight - stats.FatWeight;
        double bfPercent = stats.FatWeight / stats.BodyWeight;
        double musclePercent = stats.MuscleWeight / stats.BodyWeight;

        // 1% faster/slower for each 1.5% difference from human baseline (30%)
        // so 50% muscle = 30% bonus
        double muscleModifier = (musclePercent - Body.BaselineHumanStats.musclePercent) / 1.5;
        muscleModifier += stats.MuscleWeight / 20 / 100; // add a 1% bonus for every 20KG of muscle


        // Minimal fat has no penalty, excess has increasing penalties
        double fatPenalty;
        if (bfPercent < 0.10)
        {
            // 10% is minimal necessary fat
            fatPenalty = -.01; // negative penalty if under 10% bf
        }
        else if (bfPercent <= Body.BaselineHumanStats.fatPercent)
        {
            fatPenalty = ((bfPercent - .10) * .20) - .01; // at baseline a 0% penalty
        }
        else
        {
            // Steeper penalty for excess, 1.5% reduction per 1% of fat 
            // 1.5(fat% - baselineFat%)
            // fat% => speed penalty (abs)
            // 20%  =>  8.5%, 
            // 30%  => 23.5%
            // 40%  => 38.5%
            // 50%  => 53.5%
            fatPenalty = (bfPercent - Body.BaselineHumanStats.fatPercent) * 1.5;
        }
        fatPenalty += stats.FatWeight / 10 / 100; // 1% penalty per 10kg of fat

        // Penalty for excess weight relative to frame (high fat and/or muscle percent)
        // -(ratio^.7 - 1)
        // ratio => penalty (abs)
        // 0.1   => +0.80
        // 0.5   => +0.38
        // 0.9   => +0.07
        // 1.5   => -0.33
        // 3.0   => -1.16
        double structuralWeightRatio = baseWeight / stats.BodyWeight / 0.45; // avg 45% structure weight
        double weightEffect = -(Math.Pow(structuralWeightRatio, 0.7) - 1.0);

        // smaller creatures are faster and larger ones are slower
        // 1 - .03(Log2(sizeRatio)) 
        // ratio => speed (multiplier)
        // 0.1 => 1.40  
        // 0.5 => 1.03   - 1/2 size means 3% faster
        // 2.0 => 0.97   - 3% slower  
        // 10. => 0.80   - 20% slower
        // 50  => 0.83
        double sizeRatio = stats.BodyWeight / Body.BaselineHumanStats.overallWeight;
        double sizeModifier = 1 - 0.03 * Math.Log(sizeRatio, 2);

        return movingCapacity * (1 + muscleModifier - fatPenalty + weightEffect) * sizeModifier;
    }
    private static double CalculateVitality(BodyStats stats, CapacityContainer capacities)
    {
        double breathing = capacities.Breathing;
        double bloodPumping = capacities.BloodPumping;
        double digestion = capacities.Digestion;

        // derived stats
        double bfPercent = stats.FatWeight / stats.BodyWeight;
        double musclePercent = stats.MuscleWeight / stats.BodyWeight;

        double organFunction = (2 * (breathing + bloodPumping) + digestion) / 5;

        // Base vitality that scales more gently with body composition
        double baseMultiplier = 0.7;  // Everyone gets 70% baseline
        double muscleContribution = musclePercent * 0.25;  // Up to 25% from muscle
        double fatContribution;

        // Essential fat is beneficial, excess isn't
        if (bfPercent < .10)
            fatContribution = bfPercent * 0.5;  // Fat is very important when low
        else if (bfPercent < .25)
            fatContribution = 0.05;  // Optimal fat gives 5%
        else
            fatContribution = 0.05 - (bfPercent - .25) * 0.1;  // Excess fat penalizes slightly

        double bodyComposition = baseMultiplier + muscleContribution + fatContribution;
        return organFunction;// * bodyComposition;
    }

    private static double CalculatePerception(CapacityContainer capacities)
    {
        double sight = capacities.Sight;
        double hearing = capacities.Hearing;

        return (sight + hearing) / 2;
    }

    private static double CalculateColdResistance(BodyStats stats)
    {
        double bfPercent = stats.FatWeight / stats.BodyWeight;

        // Base cold resistance that everyone has
        double baseColdResistance = 0.5;
        double fatInsulation;

        if (bfPercent < 0.05)
            fatInsulation = bfPercent / 0.05 * 0.1;  // Linear up to 5%
        else if (bfPercent < 0.15)
            fatInsulation = 0.1 + ((bfPercent - 0.05) / 0.1 * 0.15);  // From 0.1 to 0.25
        else
            fatInsulation = 0.25 + ((bfPercent - 0.15) * 0.15);  // Diminishing returns after 15%

        return baseColdResistance + fatInsulation;
    }
}