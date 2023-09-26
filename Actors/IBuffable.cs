using text_survival_rpg_web.Magic;

namespace text_survival_rpg_web.Actors
{
    public interface IBuffable : IActor
    {
        public List<Buff> Buffs { get; }
    }
}
