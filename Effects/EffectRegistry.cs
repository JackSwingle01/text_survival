
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;
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
                double newSeverity = Math.Max(existingEffect.Severity, effect.Severity); // for now go with the more severe effect, but maybe we change this to most recent?
                existingEffect.UpdateSeverity(_owner, newSeverity);
                return;
            }
        }
        // if multiple are allowed or if no existing, then it's new -> apply it
        _effects.Add(effect);
        effect.Apply(_owner);
    }

    public void RemoveEffect(Effect effect)
    {
        if (_effects.Remove(effect))
        {
            effect.Remove(_owner);
        }
        else
        {
            Output.WriteWarning("ERROR: couldn't find effect to remove."); // shouldn't hit this, but soft error for now
        }
    }
    public void Update()
    {
        _effects.ForEach(e => e.Update(_owner));
        // Clean up inactive effects
        _effects.RemoveAll(e => !e.IsActive);
    }

    public List<Effect> GetAll() => _effects;
    public List<Effect> GetEffectsByKind(string kind) => [.. _effects.Where(e => e.EffectKind.Equals(kind, StringComparison.CurrentCultureIgnoreCase))];
    public void RemoveEffectsByKind(string kind)
    {
        var effectsToRemove = _effects.Where(e => e.EffectKind.Equals(kind, StringComparison.CurrentCultureIgnoreCase)).ToList();
        foreach (var effect in effectsToRemove)
        {
            RemoveEffect(effect);
        }
    }


    private readonly Actor _owner = owner;
    private List<Effect> _effects = [];
}