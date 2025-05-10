using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Interfaces;

namespace text_survival.Actors
{
    public interface IActor : IUpdateable, IDamageable, IPhysicalEntity
    {
        public double EquipmentWarmth { get; }
        void ApplyEffect(IEffect effect);
        void RemoveEffect(string effectType);
        public Location CurrentLocation { get; }
        public Zone CurrentZone { get; }
    }
}
