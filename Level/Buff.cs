using text_survival.Actors;

namespace text_survival.Level
{
    public enum TriggerTypes
    {
        None,
        OnAttack,
        OnHit,
        OnDamaged,
        OnKill,
        OnDeath,
        OnTick,
    }

    public class Buff
    {
        public string Name { get; set; }
        public int NumTicks { get; private set; }
        public TriggerTypes TriggerOn { get; set; }
        public Action<IBuffable> ApplyEffect { get; set; }
        public Action<IBuffable> RemoveEffect { get; set; }
        public Action<IBuffable> TickEffect { get; set; }
        public Action<IBuffable> TriggerEffect { get; set; }
        public Buff(string name, int numTicks = -1)
        {
            Name = name;
            NumTicks = numTicks; // -1 means infinite duration
            TriggerOn = TriggerTypes.None;
            ApplyEffect = (target) => { }; // applies once when applied
            RemoveEffect = (target) => { }; // should undo ApplyEffect
            TickEffect = (target) => { }; // applies once per minute
            TriggerEffect = (target) => { }; // applies on TriggerOn
        }
        public void Tick(IBuffable target)
        {
            if (NumTicks == -1) return; // -1 means infinite duration

            TickEffect?.Invoke(target);
            if (NumTicks > 0)
            {
                NumTicks--;
            }
            if (NumTicks == 0)
            {
                target.RemoveBuff(this);

            }
        }

        public void Trigger(IBuffable target, TriggerTypes trigger)
        {
            if (TriggerOn == trigger)
            {
                TriggerEffect?.Invoke(target);
            }
        }
    }


}
