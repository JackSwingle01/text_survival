namespace text_survival.Events;

public interface IGameEvent { }

public interface IEventHandler<T> where T : IGameEvent
{
    void Handle(T gameEvent);
}

public static class EventBus
{
    private static readonly Dictionary<Type, List<object>> _handlers = new();

    public static void Subscribe<T>(IEventHandler<T> handler) where T : IGameEvent
    {
        var eventType = typeof(T);
        if (!_handlers.TryGetValue(eventType, out List<object>? value))
        {
            value = [];
            _handlers[eventType] = value;
        }

        value.Add(handler);
    }

    public static void Publish<T>(T gameEvent) where T : IGameEvent
    {
        var eventType = typeof(T);
        if (_handlers.TryGetValue(eventType, out List<object>? eventHandlers))
        {
            eventHandlers.Cast<IEventHandler<T>>().ToList().ForEach(h => h.Handle(gameEvent));
        }
    }
}