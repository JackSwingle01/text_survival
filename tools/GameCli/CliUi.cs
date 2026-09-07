using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Actions.Variants;
using text_survival.Crafting;
using text_survival.Desktop.UI;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using Gui = text_survival.Desktop.UI.GameGui;

namespace text_survival.GameCli;

/// <summary>Runs the real overlay render methods with a text sink, without a graphics context.</summary>
public sealed class CliUi(GameContext ctx) : IGameUi
{
    private Action? _render;
    private Action? _close;
    private Action<PlayerAction>? _playerAction;
    private Action<CombatInput?>? _combatAction;
    private TaskCompletionSource<float>? _frame;
    private readonly InventoryOverlay _inventory = new();
    private readonly CraftingOverlay _crafting = new();
    private readonly NeedCraftingSystem _craftingSystem = new();
    private readonly FireOverlay _fire = new();
    private readonly FoodOverlay _food = new();
    private readonly NPCOverlay _npcs = new();
    private readonly TransferOverlay _transfer = new();
    private readonly DiscoveryLogOverlay _discovery = new();
    private readonly ForageOverlay _forage = new();
    private readonly CombatPanel _combat = new();
    public string Screen { get; private set; } = "busy";
    public bool Waiting => _render != null;
    public List<object> CompletedProgress { get; } = [];
    public List<ProgressView> Progress { get; } = [];
    public static readonly HudRect Rect = new(0, 0, 1200, 1000);

    private Task<T> Prompt<T>(string screen, Action<Action<T>> render, Func<T>? cancel = null)
    {
        if (_render != null) throw new InvalidOperationException("A decision is already pending.");
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Finish(T result)
        {
            _render = null; _close = null; _playerAction = null; _combatAction = null;
            Screen = "busy";
            completion.SetResult(result);
        }
        Screen = screen;
        _render = () => render(Finish);
        _close = cancel == null ? null : () => Finish(cancel());
        return completion.Task;
    }

    public TextFrame Render(string? activate = null, string? value = null)
    {
        var frame = new TextFrame(activate, value);
        if (Gui.Capture != null) throw new InvalidOperationException("Nested text frame.");
        Gui.Capture = frame;
        try { _render?.Invoke(); }
        finally { Gui.Capture = null; }
        if (activate != null && !frame.Activated) throw new ArgumentException("Control is no longer available. Read the screen again.");
        return frame;
    }
    public void Close()
    {
        if (_close == null) throw new ArgumentException("This decision cannot be cancelled; choose a displayed control.");
        _close();
    }
    public void Act(PlayerAction action)
    {
        if (_playerAction == null) throw new ArgumentException("Finish the current decision before performing a map action.");
        _playerAction(action);
    }
    public void MoveCombat(CombatInput input)
    {
        if (_combatAction == null) throw new ArgumentException("Not waiting for a combat action.");
        _combatAction(input);
    }
    public void Tick()
    {
        var frame = _frame;
        _frame = null;
        frame?.SetResult(0.1f);
    }
    public Task<float> NextFrame()
    {
        _frame = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return _frame.Task;
    }
    public Task Wait(float seconds) => Task.CompletedTask;

    public Task<T> Select<T>(string prompt, IReadOnlyList<T> choices, Func<T, string> display, Func<T, bool>? isDisabled = null) where T : notnull
        => Prompt<T>("select", finish =>
        {
            Gui.TextUnformatted(prompt);
            for (int i = 0; i < choices.Count; i++)
            {
                Gui.PushID(i);
                Gui.BeginDisabled(isDisabled?.Invoke(choices[i]) ?? false);
                if (Gui.Button(display(choices[i]))) finish(choices[i]);
                Gui.EndDisabled(); Gui.PopID();
            }
        });
    public Task<bool> Confirm(string prompt) => Prompt<bool>("confirm", finish =>
    {
        Gui.TextUnformatted(prompt);
        if (Gui.Button("Yes")) finish(true);
        if (Gui.Button("No")) finish(false);
    });
    public Task<string> Choose(string message, IReadOnlyList<(string id, string label)> buttons) => Prompt<string>("choose", finish =>
    {
        Gui.TextUnformatted(message);
        foreach (var (id, label) in buttons)
            if (Gui.Button($"{label}##{id}")) finish(id);
    });
    public Task<int> ReadInt(string prompt, int min, int max, bool allowCancel = false)
    {
        string number = min.ToString();
        return Prompt<int>("number", finish =>
        {
            Gui.TextUnformatted($"{prompt} (range {min}–{max})");
            Gui.InputTextWithHint("value", "Integer", ref number, 20);
            if (Gui.Button("OK"))
            {
                if (!int.TryParse(number, out int value) || value < min || value > max)
                    throw new ArgumentException($"Enter an integer between {min} and {max}.");
                finish(value);
            }
            if (allowCancel && Gui.Button("Cancel")) finish(-1);
        }, allowCancel ? () => -1 : null);
    }
    public Task ShowMessage(string title, string message) => Prompt<bool>("message", finish =>
    {
        Gui.TextUnformatted(title); Gui.TextUnformatted(message);
        if (Gui.Button("Continue")) finish(true);
    });
    public Task ShowWorkResult(WorkResultView view) => ShowMessage(view.Title,
        string.Join('\n', (view.Narrative ?? []).Append(view.Message).Concat(view.Warnings ?? []).Concat(view.ItemsGained)));
    public Task<string> ShowEventChoices(EventDto evt)
    {
        var overlay = new GameEventOverlay(); overlay.ShowEvent(evt);
        return Prompt<string>("event", finish => { if (overlay.Render(0) is { } choice) finish(choice); });
    }
    public Task ShowEventOutcome(EventDto outcome)
    {
        var overlay = new GameEventOverlay(); overlay.ShowEvent(outcome);
        return Prompt<bool>("outcome", finish => { overlay.Render(0); if (!overlay.IsOpen) finish(true); });
    }
    public Task<(ForageFocus? focus, int minutes)> SelectForageOptions(ForageFeature feature, IReadOnlyList<ForageClue> clues)
    {
        _forage.Open(ctx, feature, clues.ToList());
        return Prompt<(ForageFocus?, int)>("forage", finish =>
        {
            if (_forage.Render(ctx, 0) is { } result) { _forage.Close(); finish((result.Focus, result.Minutes)); }
        }, () => { _forage.Close(); return (null, 0); });
    }
    public async Task<string?> SelectButcherMode(CarcassFeature carcass, IReadOnlyList<string> warnings, bool hasCuttingTool)
    {
        var choices = new List<(string id, string label)> { ("cancel", "Cancel") };
        foreach (var (id, label, mode) in new[] {
            ("quick", "Quick Strip - Fast, meat-focused, messy", ButcheringMode.QuickStrip),
            ("careful", "Careful - Balanced approach", ButcheringMode.Careful),
            ("full", "Full Processing - Slow, maximum yield", ButcheringMode.FullProcessing) })
            if (mode != ButcheringMode.FullProcessing || hasCuttingTool)
                choices.Add((id, $"{label} (~{carcass.GetRemainingMinutes(mode)}min total)"));
        string choice = await Choose($"Butcher: {carcass.AnimalName}\nCondition: {carcass.GetDecayDescription()}\nTotal yield: ~{carcass.GetTotalRemainingKg():F1}kg\n{string.Join('\n', warnings)}", choices);
        return choice == "cancel" ? null : choice;
    }
    public Task ShowInventory()
    {
        _inventory.IsOpen = true;
        return Prompt<bool>("inventory", finish => { _inventory.Render(ctx, 0); if (!_inventory.IsOpen) finish(true); }, () => { _inventory.IsOpen = false; return true; });
    }
    public Task ShowDiscoveryLog()
    {
        _discovery.IsOpen = true; _discovery.SetData(ctx.Discoveries.ToDto());
        return Prompt<bool>("discoveries", finish => { _discovery.Render(0); if (!_discovery.IsOpen) finish(true); }, () => { _discovery.IsOpen = false; return true; });
    }
    public Task ShowNPCs()
    {
        _npcs.IsOpen = true;
        return Prompt<bool>("people", finish => { _npcs.Render(ctx, 0); if (!_npcs.IsOpen) finish(true); }, () => { _npcs.IsOpen = false; return true; });
    }
    public Task ShowTransfer(Inventory storage, string storageName)
    {
        _transfer.Open(storage, storageName);
        return Prompt<bool>("transfer", finish =>
        {
            if (_transfer.Render(ctx, 0) is { } move)
            {
                var source = move.FromPlayer ? ctx.Inventory : storage;
                var dest = move.FromPlayer ? storage : ctx.Inventory;
                string direction = move.FromPlayer ? "to storage" : "to inventory";
                var result = move switch {
                    { Resource: { } r } => TransferHandler.TransferResource(source, dest, r, direction),
                    { Tool: { } t } => TransferHandler.TransferTool(source, dest, t, direction),
                    { Equipment: { } e } => TransferHandler.TransferEquipment(source, dest, e, direction),
                    { Accessory: { } a } => TransferHandler.TransferAccessory(source, dest, a, direction),
                    _ => throw new InvalidOperationException("Transfer returned an empty move.") };
                _transfer.SetMessage(result.Message);
            }
            if (!_transfer.IsOpen) finish(true);
        }, () => { _transfer.IsOpen = false; return true; });
    }
    public Task<CraftOption?> ShowCrafting()
    {
        _crafting.IsOpen = true;
        return Prompt<CraftOption?>("crafting", finish =>
        {
            _crafting.Render(ctx, _craftingSystem, 0);
            if (_crafting.SelectedRecipe is { } recipe) { _crafting.ClearSelectedRecipe(); finish(recipe); }
            else if (!_crafting.IsOpen) finish(null);
        }, () => { _crafting.IsOpen = false; return null; });
    }
    public Task<FireOverlayResult?> ShowFire(FireFeedback? feedback)
    {
        if (!_fire.IsOpen) _fire.Open();
        if (feedback?.AttemptSucceeded is { } success) _fire.SetAttemptResult(success, feedback.Message);
        else if (feedback != null) _fire.SetTendMessage(feedback.Message);
        return Prompt<FireOverlayResult?>("fire", finish =>
        {
            if (_fire.Render(ctx, 0) is { } action) finish(action);
            else if (!_fire.IsOpen) finish(null);
        }, () => { _fire.IsOpen = false; return null; });
    }
    public Task<PendingFoodAction?> ShowFood()
    {
        _food.Open();
        return Prompt<PendingFoodAction?>("food", finish =>
        {
            _food.Render(ctx, 0);
            if (_food.PendingAction is { } action) { _food.ClearPendingAction(); finish(action); }
            else if (!_food.IsOpen) finish(null);
        }, () => { _food.IsOpen = false; return null; });
    }
    public Task<PlayerAction> WaitForPlayerAction() => Prompt<PlayerAction>("map", finish =>
    {
        _playerAction = finish;
        foreach (var action in HudActions.Build(ctx))
        {
            Gui.BeginDisabled(!action.Enabled);
            if (Gui.Button($"{action.Label}##{action.Id}")) finish(action.Payload);
            if (action.DisabledReason != null) Gui.TextUnformatted(action.DisabledReason);
            Gui.EndDisabled();
        }
    });
    public Task<CombatInput?> WaitForCombatAction() => Prompt<CombatInput?>("combat", finish =>
    {
        _combatAction = finish;
        if (_combat.Render(ctx, Rect, null) is { } action) finish(new(action, null));
    });
    public ProgressView BeginProgress(ProgressKind kind, string status)
    {
        ProgressView view = null!;
        view = new(kind, status, () => ShowMessage("Progress", view.Status), () =>
        {
            CompletedProgress.Add(new { view.Kind, view.Status, view.Progress, view.SimulatedMinutes, view.TotalMinutes, view.Sections });
            Progress.Remove(view);
        });
        Progress.Add(view); return view;
    }
}
