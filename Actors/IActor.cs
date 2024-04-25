using text_survival.Interfaces;
using text_survival.Level;

namespace text_survival.Actors
{
    public interface IActor : IUpdateable, IBuffable
    {
        public string Name { get; }
        //public double Health { get; }
        //public double MaxHealth { get; }
        BodyPart Body { get; }
        public void Damage(double damage);
        public void Heal(double heal);
    }
}
