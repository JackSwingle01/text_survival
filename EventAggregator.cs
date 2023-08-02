using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Actors;
using text_survival.Items;

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

    public static class EventAggregator
    {
        private static readonly Dictionary<Type, List<Action<EventBase>>> _eventHandlers = new Dictionary<Type, List<Action<EventBase>>>();

        public static void Subscribe<TEvent>(Action<TEvent> eventHandler) where TEvent : EventBase
        {
            var eventType = typeof(TEvent);

            if (!_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Action<EventBase>>();
                _eventHandlers.Add(eventType, handlers);
            }

            handlers.Add((e) => eventHandler((TEvent)e));
        }

        public static void Publish<TEvent>(TEvent evt) where TEvent : EventBase
        {
            var eventType = typeof(TEvent);

            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler(evt);
                }
            }
        }
    }

}
