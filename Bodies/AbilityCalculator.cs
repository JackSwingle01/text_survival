using text_survival.Effects;

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

    // Moving impairment check - used for limping effects
    public static bool IsMovingImpaired(double moving) => moving < 0.5;

    // Manipulation impairment check - used for clumsy/fumbling effects
    public static bool IsManipulationImpaired(double manipulation) => manipulation < 0.5;

    // BloodPumping impairment check - used for weak circulation effects
    public static bool IsBloodPumpingImpaired(double bloodPumping) => bloodPumping < 0.5;

    // Perception impairment check - used for foggy/dulled senses effects
    public static bool IsPerceptionImpaired(double perception) => perception < 0.5;

    // Breathing impairment check - used for winded/short of breath effects
    // Lower threshold (0.75) because Coughing only reduces breathing by 0.25
    public static bool IsBreathingImpaired(double breathing) => breathing < 0.75;

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

    /// <summary>
    /// Calculate time factor and warnings for work impairments.
    /// Returns (timeFactor, warnings) where timeFactor is a multiplier (1.0 = no impairment, 1.2 = 20% slower).
    /// </summary>
    public static (double timeFactor, List<string> warnings) GetWorkImpairments(
        CapacityContainer capacities,
        CapacityModifierContainer effectModifiers,
        bool checkMoving = false,
        bool checkBreathing = false,
        bool checkPerception = false,
        bool checkManipulation = false,
        EffectRegistry? effectRegistry = null)
    {
        double timeFactor = 1.0;
        var warnings = new List<string>();

        // Movement impairment (+20% time)
        if (checkMoving && IsMovingImpaired(capacities.Moving))
        {
            timeFactor *= 1.20;
            // Debug logging to catch exact state when warning triggers
            Console.WriteLine($"[DEBUG] Moving impaired: capacity={capacities.Moving:F3}, modifier={effectModifiers.GetCapacityModifier(CapacityNames.Moving):F3}");
            Console.WriteLine($"[DEBUG] All capacities: Moving={capacities.Moving:F3}, Manipulation={capacities.Manipulation:F3}, Breathing={capacities.Breathing:F3}, Consciousness={capacities.Consciousness:F3}, BloodPumping={capacities.BloodPumping:F3}");
            warnings.Add(GetMovingImpairmentCause(effectRegistry));
        }

        // Breathing impairment (+15% time)
        if (checkBreathing && IsBreathingImpaired(capacities.Breathing))
        {
            timeFactor *= 1.15;
            warnings.Add(GetBreathingImpairmentCause(effectRegistry));
        }

        // Manipulation impairment (+25% time)
        if (checkManipulation && IsManipulationImpaired(capacities.Manipulation))
        {
            timeFactor *= 1.25;
            warnings.Add(GetManipulationImpairmentCause(effectRegistry));
        }

        return (timeFactor, warnings);
    }

    private static string GetMovingImpairmentCause(EffectRegistry? registry)
    {
        if (registry != null)
        {
            // Check for specific effects that reduce Moving, in order of severity
            if (registry.HasEffect("Tired")) return "Your exhaustion slows the work.";
            if (registry.HasEffect("Thirsty")) return "Dehydration slows your movements.";
            if (registry.HasEffect("Hungry")) return "Hunger weakens your movements.";
            if (registry.HasEffect("Hypothermia")) return "The cold stiffens your movements.";
            if (registry.HasEffect("Frostbite")) return "Your frostbitten limbs slow the work.";
            if (registry.HasEffect("Sprained Ankle")) return "Your injured ankle slows the work.";
            if (registry.HasEffect("Stiff")) return "Your stiff joints slow the work.";
            if (registry.HasEffect("Exhausted")) return "Your exhaustion slows the work.";
            if (registry.HasEffect("Fever")) return "Your fever weakens your movements.";
            if (registry.HasEffect("Sore")) return "Your sore muscles slow the work.";
        }
        return "Your limited movement slows the work.";
    }

    private static string GetBreathingImpairmentCause(EffectRegistry? registry)
    {
        if (registry != null)
        {
            if (registry.HasEffect("Coughing")) return "Your coughing makes it hard to work.";
        }
        return "Your labored breathing slows the work.";
    }

    private static string GetManipulationImpairmentCause(EffectRegistry? registry)
    {
        if (registry != null)
        {
            if (registry.HasEffect("Frostbite")) return "Your numb fingers fumble the work.";
            if (registry.HasEffect("Shivering")) return "Your shivering hands slow the work.";
            if (registry.HasEffect("Clumsy")) return "Your clumsy hands make this harder.";
            if (registry.HasEffect("Fear")) return "Your trembling hands slow the work.";
            if (registry.HasEffect("Shaken")) return "Your unsteady hands make this harder.";
        }
        return "Your clumsy hands make the work difficult.";
    }
}