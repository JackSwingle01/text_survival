using text_survival.Actors;

using text_survival.Items;
using text_survival.Level;

namespace text_survival
{
    public enum EventType
    {
        None,
        OnAttack,
        OnHit,
        OnDamaged,
        OnKill,
        OnDeath,
        OnTick,
    }
    public abstract class EventBase { }

    public class GameEvent : EventBase
    {
        public EventType Type { get; set; }

        public GameEvent(EventType type)
        {
            Type = type;
        }
    }

    public class CombatEvent : GameEvent
    {
        public ICombatant Attacker { get; set; }
        public ICombatant Defender { get; set; }
        public double Damage { get; set; }
        public SkillType? SkillType { get; set; }
        public Weapon Weapon { get; set; }

        public CombatEvent(EventType type, ICombatant attacker, ICombatant defender) : base(type)
        {
            Attacker = attacker;
            Defender = defender;
        }

    }

    public class SkillLevelUpEvent : EventBase
    {
        public Skill Skill { get; set; }

        public SkillLevelUpEvent(Skill skill)
        {
            Skill = skill;
        }
    }

    public class GainExperienceEvent : EventBase
    {
        public int Experience { get; set; }
        public SkillType Type { get; set; }

        public GainExperienceEvent(int experience, SkillType type)
        {
            Experience = experience;
            Type = type;
        }
    }
    public class WriteEvent : EventBase
    {
        public string Message { get; set; }


        public WriteEvent(string message)
        {
            Message = message;
        }
    }

    public class InputEvent : EventBase
    {
        public string Input { get; set; }

        public InputEvent(string input)
        {
            Input = input;
        }
    }
    public static class EventHandler
    {
        private static readonly Dictionary<Type, List<Action<EventBase>>> EventHandlers = new();

        public static void Subscribe<TEvent>(Action<TEvent> eventHandler) where TEvent : EventBase
        {
            var eventType = typeof(TEvent);

            if (!EventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Action<EventBase>>();
                EventHandlers.Add(eventType, handlers);
            }

            handlers.Add((e) => eventHandler((TEvent)e));
        }

        public static void Publish<TEvent>(TEvent evt) where TEvent : EventBase
        {
            var eventType = typeof(TEvent);

            while (eventType != null && eventType != typeof(EventBase))
            {
                if (EventHandlers.TryGetValue(eventType, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        handler(evt);
                    }
                }

                eventType = eventType.BaseType;
            }
        }
    }

}
