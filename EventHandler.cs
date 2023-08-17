using text_survival_rpg_web.Actors;
using text_survival_rpg_web.Level;

namespace text_survival_rpg_web
{
    public abstract class EventBase { }

    public class EnemyDefeatedEvent : EventBase
    {
        public Npc DefeatedEnemy { get; set; }
        public EnemyDefeatedEvent(Npc enemy)
        {
            DefeatedEnemy = enemy;
        }
    }

    //public class ItemTakenEvent : EventBase
    //{
    //    public Item TakenItem { get; set; }
    //    public ItemTakenEvent(Item item)
    //    {
    //        TakenItem = item;
    //    }

    //}

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

            if (!EventHandlers.TryGetValue(eventType, out var handlers)) return;

            foreach (var handler in handlers)
            {
                handler(evt);
            }
        }
    }

}
