using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival.Magic
{
    public class TriggeredBuff : Buff
    {
        private EventType _triggerOn;
        public EventType TriggerOn
        {
            private get => _triggerOn;
            set
            {
                _triggerOn = value;
                if (_triggerOn != EventType.None)
                {
                    EventHandler.Subscribe<GameEvent>(OnGameEvent);
                }
            }
        }

        public Action<GameEvent> TriggerEffect { private get; set; }

        public TriggeredBuff(string name, BuffType type = BuffType.Generic) : base(name, type)
        {
            TriggerOn = EventType.None;
            TriggerEffect = (e) => { }; // applies on Trigger
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
