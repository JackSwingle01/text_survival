using text_survival.Effects;
using text_survival.Interfaces;

namespace text_survival.Actors
{
    public interface IActor : IUpdateable, IDamageable, ILocatable
    {
        public string Name { get; }

        void ApplyEffect(IEffect effect);
        void RemoveEffect(string effectType);

    }
}
