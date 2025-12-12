using text_survival.Actors;
using text_survival.Bodies;

namespace text_survival.Effects;

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
        CapacityModifierContainer capacityModifiers,
        SurvivalStatsDelta? survivalStatsDelta = null,
        string? applicationMessage = null,
        string? removalMessage = null,
        List<ThresholdMessage>? thresholdMessages = null,
        Action<Actor>? onApply = null,
        Action<Actor>? onUpdate = null,
        Action<Actor, double, double>? onSeverityChange = null,
        Action<Actor>? onRemove = null)
        : base(effectKind, source, targetBodyPart, severity, severityChangeRate)
    {
        CanHaveMultiple = canHaveMultiple;
        RequiresTreatment = requiresTreatment;
        CapacityModifiers = capacityModifiers;
        
        if (survivalStatsDelta != null)
            SurvivalStatsDelta = survivalStatsDelta;
        
        ApplicationMessage = applicationMessage;
        RemovalMessage = removalMessage;
        
        if (thresholdMessages != null)
            ThresholdMessages.AddRange(thresholdMessages);

        _onApply = onApply;
        _onUpdate = onUpdate;
        _onSeverityChange = onSeverityChange;
        _onRemove = onRemove;
    }

    protected override void OnApply(Actor target) => _onApply?.Invoke(target);
    protected override void OnUpdate(Actor target) => _onUpdate?.Invoke(target);
    protected override void OnSeverityChange(Actor target, double oldSeverity, double updatedSeverity)
        => _onSeverityChange?.Invoke(target, oldSeverity, updatedSeverity);
    protected override void OnRemove(Actor target) => _onRemove?.Invoke(target);
}