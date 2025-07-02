using text_survival.Actors;

namespace text_survival.Effects;

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