namespace text_survival.Bodies;


// for type hints if needed
public static class CapacityNames
{
    public const string Moving = "Moving";
    public const string Manipulation = "Manipulation";
    public const string Breathing = "Breathing";
    public const string BloodPumping = "BloodPumping";
    public const string Consciousness = "Consciousness";
    public const string Sight = "Sight";
    public const string Hearing = "Hearing";
    public const string Digestion = "Digestion";
}

/// <summary>
/// Acts as a double that is clamped between 0 and 1
/// </summary>
/// <param name="value">A value between 0 and 1 representing a percent capacity value.</param>
public readonly struct CapacityValue(double value)
{
    public double Value { get; } = Math.Clamp(value, 0, 1);

    public static implicit operator double(CapacityValue capacity) => capacity.Value;
    public static implicit operator CapacityValue(double value) => new(value);
}

/// <summary>
/// A container class to hold all capacity values and provide strong typing for body part capacity operations. Implements operators like addition and multiplication.
/// </summary>
public class CapacityContainer
{
    public CapacityValue Moving { get; set; }
    public CapacityValue Manipulation { get; set; }
    public CapacityValue Breathing { get; set; }
    public CapacityValue BloodPumping { get; set; }
    public CapacityValue Consciousness { get; set; }
    public CapacityValue Sight { get; set; }
    public CapacityValue Hearing { get; set; }
    public CapacityValue Digestion { get; set; }

    public List<CapacityValue> AllCapacities => [Moving, Manipulation, Breathing, BloodPumping, Consciousness, Sight, Hearing, Digestion];

    public static CapacityContainer operator +(CapacityContainer a, CapacityContainer b)
    {
        return new CapacityContainer
        {
            Moving = a.Moving + b.Moving,
            Manipulation = a.Manipulation + b.Manipulation,
            Breathing = a.Breathing + b.Breathing,
            BloodPumping = a.BloodPumping + b.BloodPumping,
            Consciousness = a.Consciousness + b.Consciousness,
            Sight = a.Sight + b.Sight,
            Hearing = a.Hearing + b.Hearing,
            Digestion = a.Digestion + b.Digestion
        };
    }
    public CapacityContainer ApplyMultipliers(CapacityContainer multipliers)
    {
        return new CapacityContainer
        {
            Moving = Moving * multipliers.Moving,
            Manipulation = Manipulation * multipliers.Manipulation,
            Breathing = Breathing * multipliers.Breathing,
            BloodPumping = BloodPumping * multipliers.BloodPumping,
            Consciousness = Consciousness * multipliers.Consciousness,
            Sight = Sight * multipliers.Sight,
            Hearing = Hearing * multipliers.Hearing,
            Digestion = Digestion * multipliers.Digestion
        };
    }

    public CapacityContainer ApplyMultiplier(double multiplier)
    {
        return new CapacityContainer
        {
            Moving = Moving * multiplier,
            Manipulation = Manipulation * multiplier,
            Breathing = Breathing * multiplier,
            BloodPumping = BloodPumping * multiplier,
            Consciousness = Consciousness * multiplier,
            Sight = Sight * multiplier,
            Hearing = Hearing * multiplier,
            Digestion = Digestion * multiplier
        };
    }


    public CapacityContainer ApplyModifier(CapacityModifierContainer modifiers)
    {
        return new CapacityContainer
        {
            Moving = Moving + modifiers.Moving,
            Manipulation = Manipulation + modifiers.Manipulation,
            Breathing = Breathing + modifiers.Breathing,
            BloodPumping = BloodPumping +  modifiers.BloodPumping,
            Consciousness = Consciousness + modifiers.Consciousness,
            Sight = Sight + modifiers.Sight,
            Hearing = Hearing + modifiers.Hearing,
            Digestion = Digestion + modifiers.Digestion
        };
    }


    /// <summary>
    /// Helper for generating a base CapacityContainer with all values set to one - intended for capacity multipliers.
    /// </summary>
    /// <returns>A new CapacityContainer that has all values set to 1</returns>
    public static CapacityContainer GetBaseCapacityMultiplier()
    {
        return new CapacityContainer
        {
            Moving = 1,
            Manipulation = 1,
            Breathing = 1,
            BloodPumping = 1,
            Consciousness = 1,
            Sight = 1,
            Hearing = 1,
            Digestion = 1,
        };
    }
}

/// <summary>
/// For use with additive capacity modifiers
/// </summary>
/// <param name="value">a value between -1 and 1</param>
public readonly struct CapacityModifierValue(double value)
{
    public double Value { get; } = Math.Clamp(value, -1, 1);

    public static implicit operator double(CapacityModifierValue capacity) => capacity.Value;
    public static implicit operator CapacityModifierValue(double value) => new(value);
}


public class CapacityModifierContainer
{
    public CapacityModifierValue Moving { get; set; } = 0;
    public CapacityModifierValue Manipulation { get; set; } = 0;
    public CapacityModifierValue Breathing { get; set; } = 0;
    public CapacityModifierValue BloodPumping { get; set; } = 0;
    public CapacityModifierValue Consciousness { get; set; } = 0;
    public CapacityModifierValue Sight { get; set; } = 0;
    public CapacityModifierValue Hearing { get; set; } = 0;
    public CapacityModifierValue Digestion { get; set; } = 0;

    // public List<CapacityModifierValue> AllCapacities => [Moving, Manipulation, Breathing, BloodPumping, Consciousness, Sight, Hearing, Digestion];

    public static CapacityModifierContainer operator +(CapacityModifierContainer a, CapacityModifierContainer b)
    {
        return new CapacityModifierContainer
        {
            Moving = a.Moving + b.Moving,
            Manipulation = a.Manipulation + b.Manipulation,
            Breathing = a.Breathing + b.Breathing,
            BloodPumping = a.BloodPumping + b.BloodPumping,
            Consciousness = a.Consciousness + b.Consciousness,
            Sight = a.Sight + b.Sight,
            Hearing = a.Hearing + b.Hearing,
            Digestion = a.Digestion + b.Digestion
        };
    }

}