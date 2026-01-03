using text_survival.Effects;

namespace text_survival.Bodies;

public static class AbilityCalculator
{
    #region Core Abilities (with dependency chain)

    /// <summary>
    /// Vitality: Foundation ability - "how alive are you".
    /// Minimum of critical life-sustaining systems. No dependencies.
    /// </summary>
    public static double CalculateVitality(Body body, CapacityModifierContainer effectModifiers)
    {
        var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);
        return Math.Min(
            capacities.Breathing,
            Math.Min(capacities.BloodPumping, capacities.Consciousness)
        );
    }

    public static double CalculateVitality(Body body) => CalculateVitality(body, new CapacityModifierContainer());

    /// <summary>
    /// Strength: Power output, scaled by Vitality.
    /// Dying = weak, regardless of muscles.
    /// </summary>
    public static double CalculateStrength(Body body, CapacityModifierContainer effectModifiers)
    {
        var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);
        double vitality = CalculateVitality(body, effectModifiers);

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
        double bodyContribution = baseStrength + muscleContribution + fatBonus;

        // Vitality scales strength - dying = weak
        return bodyContribution * movingContribution * manipulationContribution * vitality;
    }

    /// <summary>
    /// Speed (base): Body composition and Moving capacity.
    /// Use context-aware overload for encumbrance.
    /// </summary>
    private static double CalculateBaseSpeed(Body body, CapacityModifierContainer effectModifiers)
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

    /// <summary>
    /// Speed (no context): For backward compatibility.
    /// </summary>
    public static double CalculateSpeed(Body body, CapacityModifierContainer effectModifiers)
    {
        return CalculateSpeed(body, effectModifiers, AbilityContext.Default);
    }

    /// <summary>
    /// Speed (with context): Depends on Vitality + Strength (for encumbrance modulation).
    /// Strong person carrying 30kg feels it less than weak person.
    /// </summary>
    public static double CalculateSpeed(Body body, CapacityModifierContainer effectModifiers, AbilityContext context)
    {
        double baseSpeed = CalculateBaseSpeed(body, effectModifiers);
        double vitality = CalculateVitality(body, effectModifiers);
        double strength = CalculateStrength(body, effectModifiers);

        // Vitality factor: at 0 vitality, speed is 50% of base
        double vitalityFactor = 0.5 + (vitality * 0.5);

        // Strength modulates encumbrance: strong = load feels lighter
        // At strength 1.0, divisor is 1.0 (no help)
        // At strength 0.5, divisor is 0.75 (encumbrance feels heavier)
        double strengthDivisor = 0.5 + (strength * 0.5);
        double effectiveEncumbrance = context.EncumbrancePct / strengthDivisor;

        // Encumbrance penalty: >50% effective load starts slowing
        double encumbrancePenalty = Math.Max(0, (effectiveEncumbrance - 0.5) * 0.8);

        return baseSpeed * vitalityFactor * (1 - encumbrancePenalty);
    }

    /// <summary>
    /// Perception (no context): For backward compatibility.
    /// </summary>
    public static double CalculatePerception(Body body, CapacityModifierContainer effectModifiers)
    {
        return CalculatePerception(body, effectModifiers, AbilityContext.Default);
    }

    /// <summary>
    /// Perception (with context): Depends on Vitality + Consciousness directly.
    /// Consciousness double-dips: once in Vitality, once directly. Being dazed tanks perception.
    /// </summary>
    public static double CalculatePerception(Body body, CapacityModifierContainer effectModifiers, AbilityContext context)
    {
        var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);
        double vitality = CalculateVitality(body, effectModifiers);

        // Darkness reduces sight effectiveness (unless light source present)
        double sightEffectiveness = context.HasLightSource ? 1.0 : (1.0 - context.DarknessLevel);
        double baseSight = capacities.Sight * sightEffectiveness;

        // In darkness, hearing becomes more important
        double sightWeight = 0.5;
        double hearingWeight = 0.5;
        if (context.DarknessLevel > 0 && !context.HasLightSource)
        {
            sightWeight = 0.5 * (1 - context.DarknessLevel);
            hearingWeight = 1 - sightWeight;
        }

        double basePerception = (baseSight * sightWeight) + (capacities.Hearing * hearingWeight);

        // Vitality affects alertness
        double vitalityFactor = 0.7 + (vitality * 0.3);

        // Consciousness double-dips - being dazed specifically hurts awareness
        return basePerception * capacities.Consciousness * vitalityFactor;
    }

    /// <summary>
    /// Dexterity: Fine motor control. Depends on Vitality for steadiness.
    /// Affected by darkness (can't see hands) and wetness (slippery grip).
    /// </summary>
    public static double CalculateDexterity(Body body, CapacityModifierContainer effectModifiers, AbilityContext context)
    {
        var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);
        double vitality = CalculateVitality(body, effectModifiers);

        double baseDexterity = capacities.Manipulation;

        // Darkness penalty: can't see what you're doing (up to 50% penalty)
        double darknessPenalty = context.HasLightSource ? 0 : context.DarknessLevel * 0.5;

        // Wetness penalty: slippery grip (starts at 30% wetness, up to 21% penalty)
        double wetnessPenalty = Math.Max(0, (context.WetnessPct - 0.3) * 0.3);

        // Vitality affects steadiness: dying = shaky hands
        double steadiness = 0.7 + (vitality * 0.3);

        return baseDexterity * (1 - darknessPenalty) * (1 - wetnessPenalty) * steadiness;
    }

    /// <summary>
    /// Dexterity (no context): For backward compatibility.
    /// </summary>
    public static double CalculateDexterity(Body body, CapacityModifierContainer effectModifiers)
    {
        return CalculateDexterity(body, effectModifiers, AbilityContext.Default);
    }

    #endregion

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