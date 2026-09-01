namespace text_survival.Core;

/// <summary>
/// A queue of continuations pumped once per frame. Installed as the current
/// <see cref="SynchronizationContext"/> before the game task starts, so every await in
/// game logic resumes here - inside <see cref="Pump"/>, never inside rendering.
/// </summary>
public sealed class FrameScheduler : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();

    /// <summary>True while <see cref="Pump"/> is running game logic.</summary>
    public bool IsPumping { get; private set; }

    public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

    public override void Send(SendOrPostCallback d, object? state) =>
        throw new NotSupportedException(
            "FrameScheduler is single-threaded. Send would run game logic inline, possibly during rendering.");

    public override SynchronizationContext CreateCopy() => this;

    /// <summary>
    /// Run everything queued now, and anything queued while running, until the queue is
    /// empty. Game logic that awaits the next frame cannot spin here, because only the
    /// frame completes that wait.
    /// </summary>
    public void Pump()
    {
        if (IsPumping)
            throw new InvalidOperationException("FrameScheduler.Pump is not reentrant.");

        IsPumping = true;
        try
        {
            while (_queue.Count > 0)
            {
                var (callback, state) = _queue.Dequeue();
                callback(state);
            }
        }
        finally
        {
            IsPumping = false;
        }
    }
}
