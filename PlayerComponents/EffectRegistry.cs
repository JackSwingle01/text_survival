
using text_survival.Actors;
using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;
namespace text_survival.PlayerComponents;
public class EffectRegistry
{
    private readonly IActor _owner;
    private List<IEffect> _effects = [];

    public EffectRegistry(IActor owner)
    {
        _owner = owner;
    }

    public void AddEffect(IEffect effect)
    {
        if (_effects.Contains(effect)) return;

        _effects.Add(effect);
        effect.Apply(_owner);
    }

    public void RemoveEffect(IEffect effect)
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

    // public void RemoveAllEffects(List<IEffect> effects) => effects.ForEach(RemoveEffect);
    // public void AddAllEffects(List<IEffect> effects) => effects.ForEach(AddEffect);


    public void RemoveEffect(string effectType)
    {
        var effectsToRemove = _effects.Where(e => e.EffectType == effectType).ToList();
        foreach (var effect in effectsToRemove)
        {
            effect.Remove(_owner);
            _effects.Remove(effect);
        }
    }

    // public void RemoveEffectsWithTag(string tag)
    // {
    //     var effectsToRemove = _effects.Where(e => e.EffectType.StartsWith(tag)).ToList();
    //     foreach (var effect in effectsToRemove)
    //     {
    //         effect.Remove(_owner);
    //         _effects.Remove(effect);
    //     }
    // }

    public IReadOnlyList<IEffect> GetActiveEffects() => _effects.AsReadOnly();

    // Update all effects
    public void Update()
    {
        _effects.ForEach(e => e.Update(_owner));
        // Clean up inactive effects
        _effects.RemoveAll(e => !e.IsActive);
    }
}