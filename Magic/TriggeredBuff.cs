namespace text_survival.Magic
{
    public class TriggeredBuff : Buff
    {
        private int _uses;
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

        public TriggeredBuff(string name, int uses = 1, BuffType type = BuffType.Generic) : base(name, type)
        {
            _uses = uses;
            TriggerOn = EventType.None;
            TriggerEffect = (e) => { }; // applies on Trigger
        }

        private void Trigger(GameEvent e)
        {
            TriggerEffect?.Invoke(e);
            if (_uses > 0)
            {
                _uses--;
            }
            if (_uses == 0)
            {
                Remove();
            }
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
