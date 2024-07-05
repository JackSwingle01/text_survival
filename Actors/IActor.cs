using text_survival.Interfaces;
using text_survival.Level;

namespace text_survival.Actors
{
    public interface IActor : IUpdateable, IBuffable, IDamageable
    {
        public string Name { get; }
        
    }
}
