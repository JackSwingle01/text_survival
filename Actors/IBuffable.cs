using text_survival_rpg_web.Magic;

namespace text_survival_rpg_web.Actors
{
    public interface IBuffable : IActor
    {
        public void ApplyBuff(Buff buff);
        public void RemoveBuff(Buff buff);
    }
}
