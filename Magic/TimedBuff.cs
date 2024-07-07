using text_survival.Actors;

namespace text_survival.Magic
{
    public class TimedBuff : Buff
    {
        public int NumTicks { private get; set; }
        public Action<IBuffable> TickEffect { private get; set; }

        public TimedBuff(string name, int numTicks = -1, BuffType type = BuffType.Generic) : base(name, type)
        {
            NumTicks = numTicks; // -1 means infinite duration
            TickEffect = (target) => { }; // applies once per minute
        }

        public void Tick()
        {
            if (NumTicks == -1) return; // -1 means infinite duration

            if (NumTicks > 0)
            {
                TickEffect?.Invoke(Target);
                NumTicks--;
            }
            if (NumTicks == 0)
            {
                Remove();
            }

        }
    }
}
