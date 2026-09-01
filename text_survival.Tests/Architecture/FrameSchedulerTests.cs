using text_survival.Core;

namespace text_survival.Tests.Architecture;

/// <summary>
/// The scheduler is what keeps game logic out of the frame. These pin the two properties
/// the rest of the design rests on: a completed prompt resumes on the next pump, not
/// inline, and nothing may push work onto the loop synchronously.
/// </summary>
public class FrameSchedulerTests
{
    /// <summary>Run <paramref name="body"/> with the scheduler installed, then restore.</summary>
    private static void WithScheduler(FrameScheduler scheduler, Action body)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(scheduler);
        try { body(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Fact]
    public void CompletedPrompt_ResumesOnNextPump_NotInline()
    {
        var scheduler = new FrameScheduler();

        WithScheduler(scheduler, () =>
        {
            // A prompt, as DesktopUi creates them.
            var prompt = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool resumed = false;

            async Task GameLogic()
            {
                await prompt.Task;
                resumed = true;
            }

            var game = GameLogic();
            Assert.False(resumed);

            // The frame answers the prompt. This must not run game logic.
            prompt.SetResult(1);
            Assert.False(resumed);
            Assert.False(game.IsCompleted);

            // The next pump does.
            scheduler.Pump();
            Assert.True(resumed);
            Assert.True(game.IsCompleted);
        });
    }

    [Fact]
    public void Pump_RunsWorkQueuedWhileItRuns()
    {
        var scheduler = new FrameScheduler();
        var order = new List<string>();

        scheduler.Post(_ =>
        {
            order.Add("first");
            scheduler.Post(_ => order.Add("second"), null);
        }, null);

        scheduler.Pump();

        Assert.Equal(["first", "second"], order);
    }

    [Fact]
    public void Pump_IsNotReentrant()
    {
        var scheduler = new FrameScheduler();
        Exception? caught = null;

        scheduler.Post(_ =>
        {
            caught = Record.Exception(scheduler.Pump);
        }, null);

        scheduler.Pump();

        Assert.IsType<InvalidOperationException>(caught);
    }

    [Fact]
    public void Send_IsNeverSupported()
    {
        var scheduler = new FrameScheduler();

        Assert.Throws<NotSupportedException>(() => scheduler.Send(_ => { }, null));
    }
}
