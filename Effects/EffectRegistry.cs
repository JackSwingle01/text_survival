using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Effects;

public class EffectRegistry(Actor owner)
{
    public void AddEffect(Effect effect)
    {
        if (_effects.Contains(effect)) return;

        if (!effect.CanHaveMultiple) // if you can't have multiple then we need to check for existing
        {
            // Check for existing effect with same kind AND same target body part
            // This prevents infinite stacking of effects like Frostbite on the same body part
            var existingEffect = _effects.FirstOrDefault(e =>
                e.EffectKind == effect.EffectKind &&
                e.TargetBodyPart == effect.TargetBodyPart);

            if (existingEffect != null) // if we find existing, update, otherwise apply it below
            {
                double severityChange = Math.Abs(existingEffect.Severity - effect.Severity); // for now go with the more severe effect, but maybe we change this to most recent?
                UpdateSeverity(existingEffect, severityChange);
                return;
            }
        }
        // if multiple are allowed or if no existing, then it's new -> apply it
        _effects.Add(effect);
        effect.IsActive = true;
        _owner.AddLog(effect.ApplicationMessage);
    }

    public void RemoveEffect(Effect effect)
    {
        if (_effects.Remove(effect))
        {
            if (!effect.IsActive) return;
            effect.IsActive = false;
            if (!string.IsNullOrWhiteSpace(effect.RemovalMessage))
                _owner.AddLog(effect.RemovalMessage);
        }
        else
        {
            GameDisplay.AddWarning("ERROR: couldn't find effect to remove."); // shouldn't hit this, but soft error for now
        }
    }
    public void Update(int minutes)
    {
        foreach (Effect effect in _effects)
        {
            if (!effect.IsActive) continue;

            AdvanceSeverityProgress(effect, minutes);
        }
        // Clean up inactive effects
        _effects.RemoveAll(e => !e.IsActive || e.Severity <= 0);
    }

    /// <summary>
    /// Gets called every minute if the severity change rate is not 0
    /// </summary>
    private void AdvanceSeverityProgress(Effect effect, int minutes)
    {
        if (!effect.IsActive) return;

        double change = effect.HourlySeverityChange / 60 * minutes;

        UpdateSeverity(effect, change);
    }

    private const double RequiresTreatmentFloor = 0.05;

    private void UpdateSeverity(Effect effect, double change)
    {
        double oldSeverity = effect.Severity;

        // Effects that require treatment decay to a floor instead of fully clearing
        double floor = effect.RequiresTreatment ? RequiresTreatmentFloor : 0;
        effect.Severity = Math.Clamp(effect.Severity + change, floor, 1);

        var message = GetThresholdMessage(effect, oldSeverity);
        if (!string.IsNullOrWhiteSpace(message))
            _owner.AddLog(message);

    }

    private static string? GetThresholdMessage(Effect effect, double oldSeverity)
    {
        double newSeverity = effect.Severity;

        if (oldSeverity == newSeverity) return null;
        bool increasing = oldSeverity < newSeverity;

        double low = Math.Min(oldSeverity, newSeverity);
        double high = Math.Max(oldSeverity, newSeverity);

        var crossed = effect.ThresholdMessages
            .Where(x => x.WhenRising == increasing) // filter by increasing/decreasing
            .Where(x => low < x.Threshold && x.Threshold < high); // get all between

        // early return in case of 0 or 1 found
        if (!crossed.Any()) return null;
        if (crossed.Count() == 1) return crossed.First().Message;
        // else get the max or min threshold passed
        var mostSignificant = increasing ? crossed.MaxBy(x => x.Threshold) : crossed.MinBy(x => x.Threshold);
        return mostSignificant?.Message;
    }


    public List<Effect> GetAll() => _effects.Where(e => e.IsActive).ToList();
    public List<Effect> GetEffectsByKind(string kind) => [.. _effects.Where(e => e.EffectKind.Equals(kind, StringComparison.CurrentCultureIgnoreCase))];
    public bool HasEffect(string kind) => _effects.Any(e => e.EffectKind.Equals(kind, StringComparison.CurrentCultureIgnoreCase));
    public void RemoveEffectsByKind(string kind)
    {
        var effectsToRemove = _effects.Where(e => e.EffectKind.Equals(kind, StringComparison.CurrentCultureIgnoreCase)).ToList();
        foreach (var effect in effectsToRemove)
        {
            RemoveEffect(effect);
        }
    }

    public SurvivalStatsDelta GetSurvivalDelta()
    {
        var delta = new SurvivalStatsDelta();
        GetAll().ForEach(e => delta.Combine(e.StatsDelta.ApplyMultiplier(e.Severity)));
        return delta;
    }

    public List<DamageInfo> GetDamagesPerMinute()
    {
        var damages = new List<DamageInfo>();
        foreach (Effect e in GetAll())
        {
            if (e.Damage is null) continue;
            var damage = new DamageInfo(e.Damage.PerHour / 60 * e.Severity, e.Damage.Type, e.Source, e.TargetBodyPart);
            damages.Add(damage);
        }
        return damages;
    }

    public CapacityModifierContainer GetCapacityModifiers()
    {
        CapacityModifierContainer total = new();
        var modifiers = GetAll().Select(e => e.CapacityModifiers).ToList();
        foreach (var mod in modifiers)
        {
            total += mod;
        }
        return total;
    }

    private readonly Actor _owner = owner;
    private List<Effect> _effects = [];
}