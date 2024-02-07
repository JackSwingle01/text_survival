using text_survival.Interfaces;
using text_survival.Level;

namespace text_survival.Actors
{
    public interface IActor : IUpdateable, IBuffable
    {
        public string Name { get; }
        public Attributes Attributes { get; }
        public double Health { get; }
        public double MaxHealth { get; }
        public void Damage(double damage);
        public void Heal(double heal);
    }
}
