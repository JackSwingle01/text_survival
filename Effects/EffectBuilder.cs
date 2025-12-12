using text_survival.Actors;
using text_survival.Actors.Player;
using text_survival.Bodies;

namespace text_survival.Effects;

public class EffectBuilder
{
    private string _effectKind = "";
    private string _source = "";
    private string? _targetBodyPart = null;
    private double _severity = 1.0;
    private double _severityChangeRate = 0;
    private List<SurvivalStatsDelta> _survivalStatsUpdates = [];
    private bool _canHaveMultiple = false;
    private bool _requiresTreatment = false;
    private readonly CapacityModifierContainer _capacityModifiers = new();

    // Hook actions - using lists to allow multiple actions
    private readonly List<Action<Actor>> _onApplyActions = [];
    private readonly List<Action<Actor>> _onUpdateActions = [];
    private readonly List<Action<Actor, double, double>> _onSeverityChangeActions = [];
    private readonly List<Action<Actor>> _onRemoveActions = [];

    // Messages
    private string? _applicationMessage;
    private string? _removalMessage;
    private readonly List<Effect.ThresholdMessage> _thresholdMessages = [];

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
        _capacityModifiers.SetCapacityModifier(capacity, -reduction);
        return this;
    }

    public EffectBuilder ModifiesCapacity(string capacity, double multiplier)
    {
        _capacityModifiers.SetCapacityModifier(capacity, multiplier);
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

    public EffectBuilder WithSurvivalStatsDelta(SurvivalStatsDelta minuteUpdate)
    {
        _survivalStatsUpdates.Add(minuteUpdate);
        return this;
    }

    public EffectBuilder WithApplicationMessage(string message)
    {
        _applicationMessage = message;
        return this;
    }

    public EffectBuilder WithRemovalMessage(string message)
    {
        _removalMessage = message;
        return this;
    }

    public EffectBuilder WithThresholdMessage(double threshold, string message, bool whenRising)
    {
        _thresholdMessages.Add(new Effect.ThresholdMessage(threshold, message, whenRising));
        return this;
    }

    public EffectBuilder WithRisingThreshold(double threshold, string message)
    {
        return WithThresholdMessage(threshold, message, whenRising: true);
    }

    public EffectBuilder WithFallingThreshold(double threshold, string message)
    {
        return WithThresholdMessage(threshold, message, whenRising: false);
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

        SurvivalStatsDelta combinedStatsUpdate = new();
        foreach (var update in _survivalStatsUpdates)
        {
            combinedStatsUpdate = combinedStatsUpdate.Add(update);
        }

        return new DynamicEffect(
            effectKind: _effectKind,
            source: _source,
            targetBodyPart: _targetBodyPart,
            severity: _severity,
            severityChangeRate: _severityChangeRate,
            canHaveMultiple: _canHaveMultiple,
            requiresTreatment: _requiresTreatment,
            capacityModifiers: _capacityModifiers,
            survivalStatsDelta: combinedStatsUpdate,
            applicationMessage: _applicationMessage,
            removalMessage: _removalMessage,
            thresholdMessages: _thresholdMessages,
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
            .WithApplicationMessage("{target} is bleeding!")
            .WithPeriodicMessage("Blood continues to flow from {target}'s wound...")
            .WithFallingThreshold(0.2, "{target}'s bleeding is slowing")
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
                target.Body.Damage(damageInfo);
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
                target.Body.Damage(damageInfo);
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
                target.Body.Heal(healInfo);
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
        return builder.WithHourlySeverityChange(-60.0 / minutes);
    }

    public static EffectBuilder Permanent(this EffectBuilder builder)
    {
        return builder.WithHourlySeverityChange(0);
    }

    public static EffectBuilder NaturalHealing(this EffectBuilder builder, double rate = -0.05)
    {
        return builder.WithHourlySeverityChange(rate);
    }

    // Message helpers - static versions use built-in properties
    public static EffectBuilder WithApplyMessage(this EffectBuilder builder, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return builder;
        return builder.WithApplicationMessage(message);
    }

    public static EffectBuilder WithRemoveMessage(this EffectBuilder builder, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return builder;
        return builder.WithRemovalMessage(message);
    }

    // Periodic messages still need hooks (no built-in support for random chance)
    public static EffectBuilder WithPeriodicMessage(this EffectBuilder builder, string message, double chance = 0.05)
    {
        if (string.IsNullOrWhiteSpace(message)) return builder;
        return builder.OnUpdate(target =>
        {
            if (Utils.DetermineSuccess(chance))
            {
                target.AddLog(message.Replace("{target}", target.Name));
            }
        });
    }

    public static EffectBuilder AsInstantEffect(this EffectBuilder builder)
    {
        return builder.WithHourlySeverityChange(-999);
    }

    public static EffectBuilder CausesDehydration(this EffectBuilder builder, double hydrationLossPerMinute)
    {
        return builder.WithSurvivalStatsDelta(new SurvivalStatsDelta { HydrationDelta = -hydrationLossPerMinute });
    }

    public static EffectBuilder CausesExhaustion(this EffectBuilder builder, double exhaustionPerMinute)
    {
        return builder.WithSurvivalStatsDelta(new SurvivalStatsDelta { EnergyDelta = exhaustionPerMinute });
    }

    public static EffectBuilder AffectsTemperature(this EffectBuilder builder, double hourlyTemperatureChange)
    {
        return builder.WithSurvivalStatsDelta(new SurvivalStatsDelta()
        {
            TemperatureDelta = hourlyTemperatureChange / 60
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

    // Threshold action hooks (for custom logic)
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

    // Threshold message helpers - use built-in threshold system
    public static EffectBuilder WhenSeverityDropsBelowWithMessage(this EffectBuilder builder, double threshold, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return builder;
        return builder.WithFallingThreshold(threshold, message);
    }

    public static EffectBuilder WhenSeverityRisesAboveWithMessage(this EffectBuilder builder, double threshold, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return builder;
        return builder.WithRisingThreshold(threshold, message);
    }

    public static EffectBuilder ClearsEffectType(this EffectBuilder builder, string effectKindToClear)
    {
        return builder.OnApply(target =>
        {
            var effectsToClear = target.EffectRegistry.GetEffectsByKind(effectKindToClear);
            foreach (var effect in effectsToClear)
            {
                target.EffectRegistry.RemoveEffect(effect);
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