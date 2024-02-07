using text_survival.Actors;

namespace text_survival.Magic
{
    public enum BuffType
    {
        Generic,
        Bleed,
        Poison,
        Heal,
        Warmth,
    }

    public class Buff
    {
        public string Name { get; set; }
        public int NumTicks { private get; set; }
        public Action<IBuffable> ApplyEffect { private get; set; }
        public Action<IBuffable> RemoveEffect { private get; set; }
        public Action<IBuffable> TickEffect { private get; set; }
        public BuffType Type { get; set; }
        private EventType _triggerOn;
        public EventType TriggerOn
        {
            get => _triggerOn;
            set
            {
                //if (_triggerOn != EventType.None)
                //{
                //    EventHandler.Unsubscribe<GameEvent>(OnGameEvent);
                //}
                _triggerOn = value;
                if (_triggerOn != EventType.None)
                {
                    EventHandler.Subscribe<GameEvent>(OnGameEvent);
                }
            }
        }

        public Action<GameEvent> TriggerEffect { private get; set; }

        public IBuffable? Target { get; private set; }

        public void ApplyTo(IBuffable target)
        {
            Target = target;
            target.Buffs.Add(this);
            ApplyEffect?.Invoke(target);
        }

        public void Remove()
        {
            if (Target == null) return;
            Target.Buffs.Remove(this);
            RemoveEffect.Invoke(Target);
            Target = null;
        }

        public Buff(string name, int numTicks = -1, BuffType type = BuffType.Generic)
        {
            Name = name;
            NumTicks = numTicks; // -1 means infinite duration
            Type = type;
            TriggerOn = EventType.None;
            ApplyEffect = (target) => { }; // applies once when applied
            RemoveEffect = (target) => { }; // should undo ApplyEffect
            TickEffect = (target) => { }; // applies once per minute
            TriggerEffect = (e) => { }; // applies on TriggerOn
        }
        public void Tick()
        {
            if (NumTicks == -1) return; // -1 means infinite duration

            if (NumTicks > 0)
            {
                NumTicks--;
            }
            if (NumTicks == 0)
            {
                Remove();
            }
            TickEffect?.Invoke(Target);
        }

        private void Trigger(GameEvent e)
        {
            TriggerEffect?.Invoke(e);
        }

        private void OnGameEvent(GameEvent e)
        {
            if (e.Type == TriggerOn)
            {
                Trigger(e);
            }
        }
    }
}
