
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;
namespace text_survival.Effects;

public class EffectRegistry(Actor owner)
{
    public void AddEffect(Effect effect)
    {
        if (_effects.Contains(effect)) return;

        BodyPart? part = effect.TargetBodyPart;

        if (!effect.CanHaveMultiple)
        {
            var existingEffect = _effects.FirstOrDefault(e => e.TargetBodyPart == part && e.EffectKind == effect.EffectKind);
            if (existingEffect != null)
            {
                double newSeverity = Math.Max(existingEffect.Severity, effect.Severity);
                existingEffect.UpdateSeverity(_owner, newSeverity);
                return;
            }

        }

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
            Output.WriteWarning("ERROR: couldn't find effect to remove.");
        }
    }
    public void Update()
    {
        _effects.ForEach(e => e.Update(_owner));
        // Clean up inactive effects
        _effects.RemoveAll(e => !e.IsActive);
    }


    public double GetPartCapacityModifier(string capacity, BodyPart part) => GetEffectsOnBodyPart(part).Sum(e => e.CapacityModifiers.GetValueOrDefault(capacity) * e.Severity);
    public double GetBodyCapacityModifier(string capacity) => _effects.Where(e => e.TargetBodyPart == null).Sum(e => e.CapacityModifiers.GetValueOrDefault(capacity) * e.Severity);


    public List<Effect> GetEffectsOnBodyPart(BodyPart part) => [.. _effects.Where(e => e.TargetBodyPart == part)];
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