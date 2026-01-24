namespace text_survival.Actions.Events;

/// <summary>
/// Centralized queue for game events. Intentional triggers (tension stage changes,
/// weather transitions, threshold crossings) queue events here before random event polling.
/// This ensures intentional events take precedence and provides unified logging/handling.
/// </summary>
public class EventQueue
{
    private readonly Queue<GameEvent> _queue = new();

    /// <summary>
    /// Add an event to the queue. Null events are safely ignored.
    /// </summary>
    public void Enqueue(GameEvent? evt)
    {
        if (evt != null)
            _queue.Enqueue(evt);
    }

    /// <summary>
    /// Try to get the next event from the queue.
    /// </summary>
    public bool TryDequeue(out GameEvent? evt)
    {
        if (_queue.Count > 0)
        {
            evt = _queue.Dequeue();
            return true;
        }
        evt = null;
        return false;
    }

    /// <summary>
    /// Check if the queue has any pending events.
    /// </summary>
    public bool IsEmpty => _queue.Count == 0;

    /// <summary>
    /// Number of events currently in the queue.
    /// </summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Clear all pending events from the queue.
    /// </summary>
    public void Clear() => _queue.Clear();

    /// <summary>
    /// Peek at the next event without removing it.
    /// </summary>
    public GameEvent? Peek() => _queue.Count > 0 ? _queue.Peek() : null;
}
