using text_survival.Level;

namespace text_survival.Actors
{
    public interface IBuffable : IActor
    {
        public List<Buff> Buffs { get; }
        public void ApplyBuff(Buff buff);
        public void RemoveBuff(Buff buff);
    }
}
