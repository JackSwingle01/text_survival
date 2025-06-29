
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;
namespace text_survival.Effects;

public class EffectRegistry(Actor owner)
{
    public void AddEffect(Effect effect)
    {
        if (_effects.Contains(effect)) return;

        MajorBodyPart? part = effect.TargetBodyPart;

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


    public Capacities CapacityModifiers(string capacity, IBodyPart part)
    {
        var modifiers = GetEffectsOnBodyPart(part).Select(e => e.CapacityModifiers).ToList();
        Capacities total = new();
        foreach (var mod in modifiers) {
            var severityMod = mod.ApplyMultiplier(e.Severity);
            total += severityMod;
        }

        return total;
    }

    public List<Effect> GetEffectsOnBodyPart(IBodyPart part) => [.. _effects.Where(e => e.TargetBodyPart == part)];
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