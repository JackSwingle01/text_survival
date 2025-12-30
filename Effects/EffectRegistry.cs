using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Effects;

public class EffectRegistry
{
    // Must be public for System.Text.Json IncludeFields serialization
    public List<Effect> _effects = [];

    public EffectRegistry()
    {
    }

    public string? AddEffect(Effect effect)
    {
        if (_effects.Contains(effect)) return null;

        if (!effect.CanHaveMultiple) // if you can't have multiple then we need to check for existing
        {
            // Check for existing effect with same kind AND same target body part
            // This prevents infinite stacking of effects like Frostbite on the same body part
            var existingEffect = _effects.FirstOrDefault(e =>
                e.EffectKind == effect.EffectKind &&
                e.TargetBodyPart == effect.TargetBodyPart);

            if (existingEffect != null) // if we find existing, update, otherwise apply it below
            {
                // Take the more severe of the two - natural decay handles reduction
                existingEffect.Severity = Math.Max(existingEffect.Severity, effect.Severity);
                return null;
            }
        }
        // if multiple are allowed or if no existing, then it's new -> apply it
        _effects.Add(effect);
        effect.IsActive = true;
        return effect.ApplicationMessage;
    }

    /// <summary>
    /// Set the severity of an effect, creating it if it doesn't exist.
    /// Unlike AddEffect which takes max severity, this directly sets the value.
    /// Used for effects like Wetness where severity can both increase and decrease.
    /// </summary>
    public string? SetEffectSeverity(Effect effect)
    {
        var existingEffect = _effects.FirstOrDefault(e =>
            e.EffectKind == effect.EffectKind &&
            e.TargetBodyPart == effect.TargetBodyPart);

        if (existingEffect != null)
        {
            double oldSeverity = existingEffect.Severity;
            existingEffect.Severity = effect.Severity;

            // Return threshold message if severity changed
            return GetThresholdMessage(existingEffect, oldSeverity);
        }
        else
        {
            // New effect - add it
            _effects.Add(effect);
            effect.IsActive = true;
            return effect.ApplicationMessage;
        }
    }

    public string? RemoveEffect(Effect effect)
    {
        if (_effects.Remove(effect))
        {
            if (!effect.IsActive) return null;
            effect.IsActive = false;
            return effect.RemovalMessage;
        }
        else
        {
            GameDisplay.AddWarning("ERROR: couldn't find effect to remove."); // shouldn't hit this, but soft error for now
            return null;
        }
    }
    public List<string> Update(int minutes)
    {
        var messages = new List<string>();

        foreach (Effect effect in _effects)
        {
            if (!effect.IsActive) continue;

            var message = AdvanceSeverityProgress(effect, minutes);
            if (!string.IsNullOrWhiteSpace(message))
                messages.Add(message);
        }
        // Clean up inactive effects
        _effects.RemoveAll(e => !e.IsActive || e.Severity <= 0);

        return messages;
    }

    /// <summary>
    /// Gets called every minute if the severity change rate is not 0
    /// </summary>
    private string? AdvanceSeverityProgress(Effect effect, int minutes)
    {
        if (!effect.IsActive) return null;

        double change = effect.HourlySeverityChange / 60 * minutes;

        return UpdateSeverity(effect, change);
    }

    private const double RequiresTreatmentFloor = 0.05;

    private string? UpdateSeverity(Effect effect, double change)
    {
        double oldSeverity = effect.Severity;

        // Effects that require treatment decay to a floor instead of fully clearing
        double floor = effect.RequiresTreatment ? RequiresTreatmentFloor : 0;
        effect.Severity = Math.Clamp(effect.Severity + change, floor, 1);

        return GetThresholdMessage(effect, oldSeverity);
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

    /// <summary>
    /// Get the severity of an effect by kind. Returns 0 if effect is not present.
    /// </summary>
    public double GetSeverity(string kind)
    {
        var effect = _effects.FirstOrDefault(e => e.EffectKind.Equals(kind, StringComparison.CurrentCultureIgnoreCase));
        return effect?.Severity ?? 0;
    }
    public List<string> RemoveEffectsByKind(string kind)
    {
        var messages = new List<string>();
        var effectsToRemove = _effects.Where(e => e.EffectKind.Equals(kind, StringComparison.CurrentCultureIgnoreCase)).ToList();
        foreach (var effect in effectsToRemove)
        {
            var message = RemoveEffect(effect);
            if (!string.IsNullOrWhiteSpace(message))
                messages.Add(message);
        }
        return messages;
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
            var target = e.TargetBodyPart ?? Bodies.BodyTarget.Random;
            var damage = new DamageInfo(e.Damage.PerHour / 60 * e.Severity, e.Damage.Type, target: target);
            damages.Add(damage);
        }
        return damages;
    }

    public CapacityModifierContainer GetCapacityModifiers()
    {
        CapacityModifierContainer total = new();
        foreach (var effect in GetAll())
        {
            // Scale capacity modifiers by severity
            var scaled = effect.CapacityModifiers.ApplyMultiplier(effect.Severity);
            total += scaled;
        }
        return total;
    }
}