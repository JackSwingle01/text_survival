using text_survival.Bodies;

namespace text_survival.Interfaces
{
    public interface IDamageable
    {
        public void Damage(DamageInfo damage);
        public void Heal(HealingInfo heal);
    }
}
