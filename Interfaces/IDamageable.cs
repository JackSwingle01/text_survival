using text_survival.Actors;

namespace text_survival.Interfaces
{
    public interface IDamageable
    {
        public void Damage(double damage);
        public void Heal(double heal);
    }
}
