using text_survival.Actions.Variants;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Tests.Support;

/// <summary>
/// An <see cref="IGameUi"/> that answers from canned queues and returns already-completed
/// tasks, so the whole action loop runs synchronously with no window and no scheduler.
/// An unanswered prompt throws with the prompt text - it never invents a default.
/// </summary>
public sealed class ScriptedUi : IGameUi
{
    public Queue<PlayerAction> PlayerActions { get; } = new();
    public Queue<bool> Confirmations { get; } = new();
    public Queue<string> Choices { get; } = new();
    public Queue<int> Integers { get; } = new();
    public Queue<object> Selections { get; } = new();
    public Queue<CombatInput?> CombatInputs { get; } = new();

    /// <summary>
    /// Answers for the fire screen. An empty queue closes the screen, which is how a test
    /// that reaches the fire screen incidentally gets out of it.
    /// </summary>
    public Queue<FireOverlayResult?> FireRequests { get; } = new();

    /// <summary>Delta time handed to every <see cref="NextFrame"/>.</summary>
    public float FrameSeconds { get; set; } = 1f / 60f;

    /// <summary>
    /// Take the first available choice on any event rather than requiring a scripted
    /// answer. Random events can fire at any minute; a test that is not about events
    /// opts in here rather than scripting every possibility.
    /// </summary>
    public bool AutoResolveEvents { get; set; }

    public List<(string Title, string Text)> Messages { get; } = [];
    public List<WorkResultView> WorkResults { get; } = [];
    public int FramesRequested { get; private set; }

    private T Next<T>(Queue<T> queue, string prompt)
    {
        if (queue.Count == 0)
            throw new InvalidOperationException($"ScriptedUi has no answer for: {prompt}");
        return queue.Dequeue();
    }

    public Task<float> NextFrame()
    {
        FramesRequested++;
        return Task.FromResult(FrameSeconds);
    }

    public Task Wait(float seconds) => Task.CompletedTask;

    public Task<T> Select<T>(string prompt, IReadOnlyList<T> choices, Func<T, string> display,
        Func<T, bool>? isDisabled = null) where T : notnull
        => Task.FromResult((T)Next(Selections, prompt));

    public Task<bool> Confirm(string prompt) => Task.FromResult(Next(Confirmations, prompt));

    public Task<string> Choose(string message, IReadOnlyList<(string id, string label)> buttons)
        => Task.FromResult(Next(Choices, message));

    public Task<int> ReadInt(string prompt, int min, int max, bool allowCancel = false)
        => Task.FromResult(Next(Integers, prompt));

    public Task ShowMessage(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }

    public Task ShowWorkResult(WorkResultView view)
    {
        WorkResults.Add(view);
        return Task.CompletedTask;
    }

    public Task<string> ShowEventChoices(EventDto evt)
    {
        if (AutoResolveEvents && evt.Choices.Count > 0)
            return Task.FromResult(evt.Choices[0].Id);

        return Task.FromResult(Next(Choices, $"event: {evt.Name}"));
    }

    public Task ShowEventOutcome(EventDto outcome) => Task.CompletedTask;

    public Task<(ForageFocus? focus, int minutes)> SelectForageOptions(ForageFeature feature, IReadOnlyList<ForageClue> clues)
        => throw new InvalidOperationException("ScriptedUi has no answer for: forage options");

    public Task<string?> SelectButcherMode(CarcassFeature carcass, IReadOnlyList<string> warnings, bool hasCuttingTool)
        => throw new InvalidOperationException("ScriptedUi has no answer for: butcher mode");

    public Task ShowInventory() => Task.CompletedTask;
    public Task ShowDiscoveryLog() => Task.CompletedTask;
    public Task ShowNPCs() => Task.CompletedTask;
    public Task ShowTransfer(Inventory storage, string storageName) => Task.CompletedTask;

    public Task<CraftOption?> ShowCrafting() => Task.FromResult<CraftOption?>(null);
    public Task<FireOverlayResult?> ShowFire(FireFeedback? feedback)
        => Task.FromResult(FireRequests.Count > 0 ? FireRequests.Dequeue() : null);
    public Task<PendingFoodAction?> ShowFood() => Task.FromResult<PendingFoodAction?>(null);

    public Task<PlayerAction> WaitForPlayerAction() => Task.FromResult(Next(PlayerActions, "player action"));

    public Task<CombatInput?> WaitForCombatAction() => Task.FromResult(Next(CombatInputs, "combat action"));

    public ProgressView BeginProgress(ProgressKind kind, string status)
        => new(kind, status, () => Task.CompletedTask, () => { });
}
