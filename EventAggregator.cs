using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Actors;
using text_survival.Items;
using text_survival.Skills;

namespace text_survival
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

    public class ItemTakenEvent : EventBase
    {
        public Item TakenItem { get; set; }
        public ItemTakenEvent(Item item)
        {
            TakenItem = item;
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
    public static class EventAggregator
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
