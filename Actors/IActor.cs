using text_survival_rpg_web.Level;

namespace text_survival_rpg_web.Actors
{
    public interface IActor
    {
        public string Name { get; }
        public Attributes Attributes { get; }
        public double Health { get; }
        public double MaxHealth { get; }
        public void Update();
        public void Damage(double damage);
        public void Heal(double heal);
    }
}
