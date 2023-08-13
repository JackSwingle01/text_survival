using text_survival.Magic;

namespace text_survival.Actors
{
    public interface IBuffable : IActor
    {
        public void ApplyBuff(Buff buff);
        public void RemoveBuff(Buff buff);
    }
}
