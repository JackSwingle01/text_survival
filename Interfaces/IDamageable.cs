using text_survival.Actors;

namespace text_survival.Interfaces
{
    public interface IDamageable
    {
        BodyPart Body { get; }
        public void Damage(double damage);
        public void Heal(double heal);
    }
}
