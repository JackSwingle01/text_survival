using text_survival.Interfaces;

namespace text_survival.Actors
{
    public interface IActor : IUpdateable, IBuffable, IDamageable
    {
        public string Name { get; }

    }
}
