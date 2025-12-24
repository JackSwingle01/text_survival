
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

    public static readonly List<string> All =
    [
        Moving, Manipulation, Breathing, BloodPumping, Consciousness, Sight, Hearing, Digestion
    ];
}

/// <summary>
/// A container class to hold all capacity values and provide strong typing for body part capacity operations. Implements operators like addition and multiplication.
/// </summary>
public class CapacityContainer
{
    private Dictionary<string, double> capacities = new()
    {
        {CapacityNames.Moving, 0 },
        {CapacityNames.Manipulation, 0},
        {CapacityNames.Breathing, 0},
        {CapacityNames.BloodPumping, 0},
        {CapacityNames.Consciousness, 0},
        {CapacityNames.Sight, 0},
        {CapacityNames.Hearing, 0},
        {CapacityNames.Digestion, 0},
    };

    // Property accessors for backward compatibility if needed
    public double Moving
    {
        get => capacities[CapacityNames.Moving];
        set => capacities[CapacityNames.Moving] = Math.Clamp(value, 0, 1);
    }
    public double Manipulation
    {
        get => capacities[CapacityNames.Manipulation];
        set => capacities[CapacityNames.Manipulation] = Math.Clamp(value, 0, 1);
    }
    public double Breathing
    {
        get => capacities[CapacityNames.Breathing];
        set => capacities[CapacityNames.Breathing] = Math.Clamp(value, 0, 1);
    }
    public double BloodPumping
    {
        get => capacities[CapacityNames.BloodPumping];
        set => capacities[CapacityNames.BloodPumping] = Math.Clamp(value, 0, 1);
    }
    public double Consciousness
    {
        get => capacities[CapacityNames.Consciousness];
        set => capacities[CapacityNames.Consciousness] = Math.Clamp(value, 0, 1);
    }
    public double Sight
    {
        get => capacities[CapacityNames.Sight];
        set => capacities[CapacityNames.Sight] = Math.Clamp(value, 0, 1);
    }
    public double Hearing
    {
        get => capacities[CapacityNames.Hearing];
        set => capacities[CapacityNames.Hearing] = Math.Clamp(value, 0, 1);
    }
    public double Digestion
    {
        get => capacities[CapacityNames.Digestion];
        set => capacities[CapacityNames.Digestion] = Math.Clamp(value, 0, 1);
    }

    public List<double> AllCapacities => capacities.Values.ToList();

    public double GetCapacity(string capacityName) => capacities.GetValueOrDefault(capacityName);
    public void SetCapacity(string capacityName, double value) => capacities[capacityName] = Math.Clamp(value, 0, 1);

    public static CapacityContainer operator +(CapacityContainer a, CapacityContainer b)
    {
        var newCapacities = a.capacities.Keys.Union(b.capacities.Keys)
            .ToDictionary(
                key => key,
                key => Math.Clamp(a.capacities.GetValueOrDefault(key) + b.capacities.GetValueOrDefault(key), 0, 1)
            );
        return new CapacityContainer { capacities = newCapacities };
    }

    public CapacityContainer ApplyMultipliers(CapacityContainer multipliers)
    {
        var newCapacities = capacities.Keys.Union(multipliers.capacities.Keys)
            .ToDictionary(
                key => key,
                key => Math.Clamp(capacities.GetValueOrDefault(key) * multipliers.capacities.GetValueOrDefault(key, 1), 0, 1)
            );
        return new CapacityContainer { capacities = newCapacities };
    }

    public CapacityContainer ApplyMultiplier(double multiplier)
    {
        var newCapacities = capacities.ToDictionary(
            kvp => kvp.Key,
            kvp => Math.Clamp(kvp.Value * multiplier, 0, 1)
        );
        return new CapacityContainer { capacities = newCapacities };
    }

    public CapacityContainer ApplyModifier(CapacityModifierContainer modifiers)
    {
        var newCapacities = capacities.ToDictionary(
            kvp => kvp.Key,
            kvp => Math.Clamp(kvp.Value + modifiers.GetCapacityModifier(kvp.Key), 0, 1)
        );
        return new CapacityContainer { capacities = newCapacities };
    }

    /// <summary>
    /// Helper for generating a base CapacityContainer with all values set to one - intended for capacity multipliers.
    /// </summary>
    /// <returns>A new CapacityContainer that has all values set to 1</returns>
    public static CapacityContainer GetBaseCapacityMultiplier()
    {
        return new CapacityContainer
        {
            capacities = new Dictionary<string, double>
            {
                {CapacityNames.Moving, 1 },
                {CapacityNames.Manipulation, 1},
                {CapacityNames.Breathing, 1},
                {CapacityNames.BloodPumping, 1},
                {CapacityNames.Consciousness, 1},
                {CapacityNames.Sight, 1},
                {CapacityNames.Hearing, 1},
                {CapacityNames.Digestion, 1},
            }
        };
    }
}

public class CapacityModifierContainer
{
    public double GetCapacityModifier(string capacityName) => Math.Clamp(capacities.GetValueOrDefault(capacityName), -1, 1);
    public void SetCapacityModifier(string capacityName, double value) => capacities[capacityName] = Math.Clamp(value, -1, 1);

    private Dictionary<string, double> capacities;

    public CapacityModifierContainer()
    {
        capacities = new()
        {
            {CapacityNames.Moving, 0 },
            {CapacityNames.Manipulation, 0},
            {CapacityNames.Breathing, 0},
            {CapacityNames.BloodPumping, 0},
            {CapacityNames.Consciousness, 0},
            {CapacityNames.Sight, 0},
            {CapacityNames.Hearing, 0},
            {CapacityNames.Digestion, 0},
        };
    }

    private CapacityModifierContainer(Dictionary<string, double> modifiers)
    {
        capacities = modifiers;
    }

    public static CapacityModifierContainer operator +(CapacityModifierContainer a, CapacityModifierContainer b)
    {
        var newModifiers = a.capacities.Keys.Union(b.capacities.Keys)
            .ToDictionary(
                key => key,
                key => a.capacities.GetValueOrDefault(key) + b.capacities.GetValueOrDefault(key)
            );
        return new CapacityModifierContainer(newModifiers);
    }

    /// <summary>
    /// Scales all modifiers by a multiplier (typically effect severity).
    /// </summary>
    public CapacityModifierContainer ApplyMultiplier(double multiplier)
    {
        var newModifiers = capacities.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value * multiplier
        );
        return new CapacityModifierContainer(newModifiers);
    }

    /// <summary>
    /// Export modifiers as dictionary (for serialization).
    /// Only includes non-zero values.
    /// </summary>
    public Dictionary<string, double> ToDictionary()
    {
        return capacities
            .Where(kvp => Math.Abs(kvp.Value) > 0.0001)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Create from dictionary (for deserialization).
    /// </summary>
    public static CapacityModifierContainer FromDictionary(Dictionary<string, double>? dict)
    {
        if (dict == null || dict.Count == 0)
            return new CapacityModifierContainer();

        var container = new CapacityModifierContainer();
        foreach (var kvp in dict)
            container.SetCapacityModifier(kvp.Key, kvp.Value);
        return container;
    }
}