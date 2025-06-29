using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

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
public readonly struct Capacity(double value)
{
    public double Value { get; } = Math.Clamp(value, 0, 1);

    public static implicit operator double(Capacity capacity) => capacity.Value;
    public static implicit operator Capacity(double value) => new(value);
}

public class Capacities
{
    public Capacity Moving { get; set; }
    public Capacity Manipulation { get; set; }
    public Capacity Breathing { get; set; }
    public Capacity BloodPumping { get; set; }
    public Capacity Consciousness { get; set; }
    public Capacity Sight { get; set; }
    public Capacity Hearing { get; set; }
    public Capacity Digestion { get; set; }

    public List<Capacity> AllCapacities => [Moving, Manipulation, Breathing, BloodPumping, Consciousness, Sight, Hearing, Digestion];

    public static Capacities operator +(Capacities a, Capacities b)
    {
        return new Capacities
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
    public Capacities ApplyMultipliers(Capacities multipliers)
    {
        return new Capacities
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

    public Capacities ApplyMultiplier(double multiplier)
    {
        return new Capacities
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
    public static Capacities GetBaseCapacityMultiplier()
    {
        return new Capacities
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
