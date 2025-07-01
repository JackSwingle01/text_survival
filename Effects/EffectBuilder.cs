using text_survival.Actors;
using text_survival.Bodies;

namespace text_survival.Effects;

public class EffectBuilder
{
    private string _effectKind = "";
    private string _source = "";
    private string? _targetBodyPart = null;
    private double _severity = 1.0;
    private double _severityChangeRate = 0;
    private List<SurvivalStatsUpdate> _survivalStatsUpdates = [];
    private bool _canHaveMultiple = false;
    private bool _requiresTreatment = false;
    private readonly Dictionary<string, double> _capacityModifiers = [];

    // Hook actions - using lists to allow multiple actions
    private readonly List<Action<Actor>> _onApplyActions = [];
    private readonly List<Action<Actor>> _onUpdateActions = [];
    private readonly List<Action<Actor, double, double>> _onSeverityChangeActions = [];
    private readonly List<Action<Actor>> _onRemoveActions = [];

    public EffectBuilder Named(string effectKind)
    {
        _effectKind = effectKind;
        return this;
    }

    public EffectBuilder From(string source)
    {
        _source = source;
        return this;
    }

    public EffectBuilder Targeting(string? bodyPart)
    {
        _targetBodyPart = bodyPart;
        return this;
    }

    public EffectBuilder WithSeverity(double severity)
    {
        _severity = Math.Clamp(severity, 0, 1);
        return this;
    }

    public EffectBuilder WithHourlySeverityChange(double rate)
    {
        _severityChangeRate = rate;
        return this;
    }

    public EffectBuilder AllowMultiple(bool allow = true)
    {
        _canHaveMultiple = allow;
        return this;
    }

    public EffectBuilder RequiresTreatment(bool requires = true)
    {
        _requiresTreatment = requires;
        return this;
    }

    public EffectBuilder ReducesCapacity(string capacity, double reduction)
    {
        _capacityModifiers[capacity] = -reduction;
        return this;
    }

    public EffectBuilder ModifiesCapacity(string capacity, double multiplier)
    {
        _capacityModifiers[capacity] = multiplier;
        return this;
    }

    public EffectBuilder OnApply(Action<Actor> action)
    {
        _onApplyActions.Add(action);
        return this;
    }

    public EffectBuilder OnUpdate(Action<Actor> action)
    {
        _onUpdateActions.Add(action);
        return this;
    }

    public EffectBuilder OnSeverityChange(Action<Actor, double, double> action)
    {
        _onSeverityChangeActions.Add(action);
        return this;
    }

    public EffectBuilder OnRemove(Action<Actor> action)
    {
        _onRemoveActions.Add(action);
        return this;
    }

    public EffectBuilder WithSurvivalStatsUpdate(SurvivalStatsUpdate minuteUpdate)
    {
        _survivalStatsUpdates.Add(minuteUpdate);
        return this;
    }

    public DynamicEffect Build()
    {
        if (string.IsNullOrWhiteSpace(_effectKind))
        {
            throw new InvalidOperationException("Effect kind is required");
        }

        // Combine multiple actions like ActionBuilder does
        Action<Actor>? combinedOnApply = null;
        if (_onApplyActions.Count > 0)
        {
            combinedOnApply = target => _onApplyActions.ForEach(action => action(target));
        }

        Action<Actor>? combinedOnUpdate = null;
        if (_onUpdateActions.Count > 0)
        {
            combinedOnUpdate = target => _onUpdateActions.ForEach(action => action(target));
        }

        Action<Actor, double, double>? combinedOnSeverityChange = null;
        if (_onSeverityChangeActions.Count > 0)
        {
            combinedOnSeverityChange = (target, oldSeverity, newSeverity) =>
                _onSeverityChangeActions.ForEach(action => action(target, oldSeverity, newSeverity));
        }

        Action<Actor>? combinedOnRemove = null;
        if (_onRemoveActions.Count > 0)
        {
            combinedOnRemove = target => _onRemoveActions.ForEach(action => action(target));
        }

        SurvivalStatsUpdate combinedStatsUpdate = new();
        foreach (var update in _survivalStatsUpdates)
        {
            combinedStatsUpdate.Add(update);
        }

        return new DynamicEffect(
            effectKind: _effectKind,
            source: _source,
            targetBodyPart: _targetBodyPart,
            severity: _severity,
            severityChangeRate: _severityChangeRate,
            canHaveMultiple: _canHaveMultiple,
            requiresTreatment: _requiresTreatment,
            capacityModifiers: new Dictionary<string, double>(_capacityModifiers),
            onApply: combinedOnApply,
            onUpdate: combinedOnUpdate,
            onSeverityChange: combinedOnSeverityChange,
            onRemove: combinedOnRemove

        );
    }
}

public static class EffectBuilderExtensions
{
    public static EffectBuilder CreateEffect(string effectKind) => new EffectBuilder().Named(effectKind);

    // Common effect patterns
    public static EffectBuilder Bleeding(this EffectBuilder builder, double damagePerHour)
    {
        return builder
            .Named("Bleeding")
            .WithHourlySeverityChange(-0.05) // Natural clotting
            .AllowMultiple(true)
            .ReducesCapacity(CapacityNames.BloodPumping, 0.2)
            .ReducesCapacity(CapacityNames.Consciousness, 0.1)
            .WithApplyMessage("{target} is bleeding!")
            .WithPeriodicMessage("Blood continues to flow from {target}'s wound...")
            .WhenSeverityDropsBelowWithMessage(0.2, "{target}'s bleeding is slowing")
            .OnUpdate(target =>
            {
                double damage = (damagePerHour / 60.0) * builder.Build().Severity;
                var damageInfo = new DamageInfo
                {
                    Amount = damage,
                    Type = DamageType.Bleed,
                    Source = builder.Build().Source,
                    TargetPartName = builder.Build().TargetBodyPart
                };
                target.Damage(damageInfo);
            });
    }

    public static EffectBuilder Poisoned(this EffectBuilder builder, double damagePerHour)
    {
        return builder
            .Named("Poison")
            .WithHourlySeverityChange(-0.02) // Slow detox
            .AllowMultiple(true)
            .ReducesCapacity(CapacityNames.Consciousness, 0.3)
            .ReducesCapacity(CapacityNames.Manipulation, 0.2)
            .ReducesCapacity(CapacityNames.Moving, 0.2)
            .ReducesCapacity(CapacityNames.BloodPumping, .1)
            .OnUpdate(target =>
            {
                double damage = (damagePerHour / 60.0) * builder.Build().Severity;
                var damageInfo = new DamageInfo
                {
                    Amount = damage,
                    Type = DamageType.Poison,
                    Source = builder.Build().Source
                };
                target.Damage(damageInfo);
            });
    }

    public static EffectBuilder Healing(this EffectBuilder builder, double healPerHour)
    {
        return builder
            .Named("Healing")
            .WithHourlySeverityChange(-1.0 / 60) // Expires in 1 hour by default
            .OnUpdate(target =>
            {
                double heal = (healPerHour / 60.0) * builder.Build().Severity;
                var healInfo = new HealingInfo
                {
                    Amount = heal,
                    Quality = 1.0,
                    Source = builder.Build().Source,
                    TargetPart = builder.Build().TargetBodyPart
                };
                target.Heal(healInfo);
            });
    }

    public static EffectBuilder Temperature(this EffectBuilder builder, TemperatureType type)
    {
        return type switch
        {
            TemperatureType.Hypothermia => builder
                .Named("Hypothermia")
                .RequiresTreatment(true)
                .WithHourlySeverityChange(-.02)
                .ReducesCapacity(CapacityNames.Moving, 0.3)
                .ReducesCapacity(CapacityNames.Manipulation, 0.3)
                .ReducesCapacity(CapacityNames.Consciousness, 0.5)
                .ReducesCapacity(CapacityNames.BloodPumping, 0.2),

            TemperatureType.Hyperthermia => builder
                .Named("Hyperthermia")
                .RequiresTreatment(true)
                .WithHourlySeverityChange(-.01)
                .ReducesCapacity(CapacityNames.Consciousness, 0.5)
                .ReducesCapacity(CapacityNames.Moving, 0.3)
                .ReducesCapacity(CapacityNames.BloodPumping, 0.2),

            TemperatureType.Frostbite => builder
                .Named("Frostbite")
                .WithHourlySeverityChange(-0.02)
                .ReducesCapacity(CapacityNames.Manipulation, 0.5)
                .ReducesCapacity(CapacityNames.Moving, 0.5)
                .ReducesCapacity(CapacityNames.BloodPumping, 0.2),

            _ => builder
        };
    }

    public static EffectBuilder WithDuration(this EffectBuilder builder, int minutes)
    {
        return builder.WithHourlySeverityChange(-1.0 / minutes);
    }

    public static EffectBuilder Permanent(this EffectBuilder builder)
    {
        return builder.WithHourlySeverityChange(0);
    }

    public static EffectBuilder NaturalHealing(this EffectBuilder builder, double rate = -0.05)
    {
        return builder.WithHourlySeverityChange(rate);
    }

    // Message helpers
    public static EffectBuilder WithApplyMessage(this EffectBuilder builder, string message)
    {
        return builder.OnApply(target => IO.Output.WriteLine(message.Replace("{target}", target.Name)));
    }

    public static EffectBuilder WithRemoveMessage(this EffectBuilder builder, string message)
    {
        return builder.OnRemove(target => IO.Output.WriteLine(message.Replace("{target}", target.Name)));
    }

    public static EffectBuilder WithPeriodicMessage(this EffectBuilder builder, string message, double chance = 0.05)
    {
        return builder.OnUpdate(target =>
        {
            if (Utils.DetermineSuccess(chance))
            {
                IO.Output.WriteLine(message.Replace("{target}", target.Name));
            }
        });
    }

    public static EffectBuilder CausesDehydration(this EffectBuilder builder, double hydrationLossPerMinute)
    {
        return builder.WithSurvivalStatsUpdate(new SurvivalStatsUpdate { Hydration = -hydrationLossPerMinute });
    }

    public static EffectBuilder CausesExhaustion(this EffectBuilder builder, double exhaustionPerMinute)
    {
        return builder.WithSurvivalStatsUpdate(new SurvivalStatsUpdate { Exhaustion = exhaustionPerMinute });
    }

    public static EffectBuilder AffectsTemperature(this EffectBuilder builder, double hourlyTemperatureChange)
    {
        return builder.WithSurvivalStatsUpdate(new SurvivalStatsUpdate()
        {
            Temperature = hourlyTemperatureChange / 60
        });
    }

    public static EffectBuilder GrantsExperience(this EffectBuilder builder, string skillName, int xpPerMinute)
    {
        return builder.OnUpdate(target =>
        {
            if (target is Player player)
            {
                player.Skills.GetSkill(skillName).GainExperience(xpPerMinute);
            }
        });
    }

    public static EffectBuilder AppliesOnRemoval(this EffectBuilder builder, Effect effectToApply)
    {
        return builder.OnRemove(target =>
        {
            target.EffectRegistry.AddEffect(effectToApply);
        });
    }
    public static EffectBuilder WhenSeverityDropsBelow(this EffectBuilder builder, double threshold, Action<Actor> action)
    {
        return builder.OnSeverityChange((target, oldSeverity, newSeverity) =>
        {
            if (newSeverity < threshold && oldSeverity >= threshold)
            {
                action(target);
            }
        });
    }

    public static EffectBuilder WhenSeverityRisesAbove(this EffectBuilder builder, double threshold, Action<Actor> action)
    {
        return builder.OnSeverityChange((target, oldSeverity, newSeverity) =>
        {
            if (newSeverity > threshold && oldSeverity <= threshold)
            {
                action(target);
            }
        });
    }

    public static EffectBuilder WhenSeverityDropsBelowWithMessage(this EffectBuilder builder, double threshold, string message)
    {
        return builder.OnSeverityChange((target, oldSeverity, newSeverity) =>
        {
            if (newSeverity < threshold && oldSeverity >= threshold)
            {
                IO.Output.WriteLine(message.Replace("{target}", target.Name));
            }
        });
    }

    public static EffectBuilder WhenSeverityRisesAboveWithMessage(this EffectBuilder builder, double threshold, string message)
    {
        return builder.OnSeverityChange((target, oldSeverity, newSeverity) =>
        {
            if (newSeverity > threshold && oldSeverity <= threshold)
            {
                IO.Output.WriteLine(message.Replace("{target}", target.Name));
            }
        });
    }
    public static EffectBuilder ClearsEffectType(this EffectBuilder builder, string effectKindToClear)
    {
        return builder.OnApply(target =>
        {
            var effectsToClear = target.EffectRegistry.GetEffectsByKind(effectKindToClear);
            foreach (var effect in effectsToClear)
            {
                effect.Remove(target);
            }
        });
    }
}

public enum TemperatureType
{
    Hypothermia,
    Hyperthermia,
    Frostbite,
    Burn
}

// Dynamic effect class to support the builder
public class DynamicEffect : Effect
{
    private readonly Action<Actor>? _onApply;
    private readonly Action<Actor>? _onUpdate;
    private readonly Action<Actor, double, double>? _onSeverityChange;
    private readonly Action<Actor>? _onRemove;

    public DynamicEffect(
        string effectKind,
        string source,
        string? targetBodyPart,
        double severity,
        double severityChangeRate,
        bool canHaveMultiple,
        bool requiresTreatment,
        Dictionary<string, double> capacityModifiers,
        Action<Actor>? onApply = null,
        Action<Actor>? onUpdate = null,
        Action<Actor, double, double>? onSeverityChange = null,
        Action<Actor>? onRemove = null,
        SurvivalStatsUpdate? survivalStatsUpdate = null)
        : base(effectKind, source, targetBodyPart, severity, severityChangeRate)
    {
        CanHaveMultiple = canHaveMultiple;
        RequiresTreatment = requiresTreatment;

        // Apply capacity modifiers
        foreach (var modifier in capacityModifiers)
        {
            CapacityModifiers.GetType()
                .GetProperty(modifier.Key)?
                .SetValue(CapacityModifiers, modifier.Value);
        }

        _onApply = onApply;
        _onUpdate = onUpdate;
        _onSeverityChange = onSeverityChange;
        _onRemove = onRemove;
        if (survivalStatsUpdate != null)
        {
            this.SurvivalStatsEffect = survivalStatsUpdate;
        }
    }

    protected override void OnApply(Actor target) => _onApply?.Invoke(target);
    protected override void OnUpdate(Actor target) => _onUpdate?.Invoke(target);
    protected override void OnSeverityChange(Actor target, double oldSeverity, double updatedSeverity)
        => _onSeverityChange?.Invoke(target, oldSeverity, updatedSeverity);
    protected override void OnRemove(Actor target) => _onRemove?.Invoke(target);
}