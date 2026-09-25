using ImGuiNET;
using text_survival.Desktop.Input;
using Raylib_cs;
using rlImGui_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actions.Handlers;
using text_survival.Actions.Variants;
using text_survival.Core;
using text_survival.Crafting;
using text_survival.Desktop.Audio;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.UI;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Desktop;

/// <summary>
/// The whole visual side of the game: world rendering, the HUD, the modal stack, and the
/// only <see cref="Raylib.BeginDrawing"/> in the codebase. Game logic awaits the methods
/// on <see cref="IGameUi"/>; this resolves them from inside a frame, and the
/// continuations run on the next scheduler pump - never during rendering.
/// </summary>
public sealed class DesktopUi : IGameUi, IDisposable
{
    private static readonly Color Background = new(20, 25, 30, 255);
    private static readonly Color Dim = new(0, 0, 0, 128);

    private readonly GameContext _ctx;
    private readonly FrameScheduler _scheduler;

    private readonly HudController _hud = new();
    private readonly WorldRenderer _world;

    private readonly InventoryOverlay _inventory = new();
    private readonly CraftingOverlay _crafting = new();
    private readonly NeedCraftingSystem _craftingSystem = new();
    private readonly FireOverlay _fire = new();
    private readonly FoodOverlay _food = new();
    private readonly TransferOverlay _transfer = new();
    private readonly DiscoveryLogOverlay _discoveryLog = new();
    private readonly NPCOverlay _npcs = new();
    private readonly GameEventOverlay _event = new();
    private readonly ForageOverlay _forage = new();

    private readonly List<Modal> _stack = [];
    private readonly InputGate _inputGate = new();
    private int _hudPanel = -1;
    private readonly List<TaskCompletionSource<float>> _frameWaiters = [];
    private readonly List<TimeWaiter> _timeWaiters = [];

    public void Dispose() => _world.Dispose();

    public DesktopUi(GameContext ctx, FrameScheduler scheduler)
    {
        _ctx = ctx;
        _scheduler = scheduler;
        _world = new WorldRenderer(_hud.State);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // THE FRAME
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One frame: resolve time waiters, update UI-only animation, draw. Nothing here
    /// runs game logic - prompts only complete their tasks, whose continuations the
    /// scheduler runs on the next pump.
    /// </summary>
    public void Frame(GameContext ctx, float dt)
    {
        if (_scheduler.IsPumping)
            throw new InvalidOperationException("DesktopUi.Frame ran inside a scheduler pump. Rendering must never nest in game logic.");

        UiStyle.ApplyPending();
        object owner = (object?)_stack.LastOrDefault() ?? this;
        UiInput.Ready = _inputGate.Enter(owner, UiInput.AnyKeyDown());
        bool modalOpen = _stack.Any(m => m.DimsWorld);
        UiInput.Context = modalOpen ? InputContext.Dialog : ctx.ActiveCombat != null ? InputContext.Combat : InputContext.Exploration;
        var inputIo = ImGui.GetIO();
        if (modalOpen) { _hudPanel = -1; UiInput.HudFocused = false; }
        bool popupOpen = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId);
        if (!modalOpen && !popupOpen && UiInput.Ready && !inputIo.WantTextInput)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.F6)) { _hudPanel = (_hudPanel + 1) % 4; UiInput.HudFocused = true; }
            else if (Raylib.IsKeyPressed(KeyboardKey.Tab) && !UiInput.HudFocused) { _hudPanel = 0; UiInput.HudFocused = true; }
            else if (Raylib.IsKeyPressed(KeyboardKey.Escape) && UiInput.HudFocused)
            { UiInput.HudFocused = false; _hudPanel = -1; UiInput.Ready = false; }
        }
        if (modalOpen || UiInput.HudFocused || popupOpen) inputIo.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        else inputIo.ConfigFlags &= ~ImGuiConfigFlags.NavEnableKeyboard;
        ResolveTimeWaiters(dt);

        AudioManager.Update();
        _hud.Update(ctx, _world, dt);
        if (!modalOpen && UiInput.Ready && !UiInput.HudFocused && !popupOpen)
        {
            var io = ImGui.GetIO();
            _world.HandleCameraInput(ctx, dt, io.WantCaptureMouse, io.WantCaptureKeyboard);
        }
        _world.Update(ctx, dt);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Background);

        _world.Render(ctx);

        if (_stack.Any(m => m.DimsWorld))
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Dim);

        rlImGui.Begin();
        if (ImGui.GetIO().WantTextInput) UiInput.Context = InputContext.TextEntry;
        else if (ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId)) UiInput.Context = InputContext.Popup;

        if (!modalOpen && UiInput.HudFocused &&
            (Raylib.IsKeyPressed(KeyboardKey.F6) || Raylib.IsKeyPressed(KeyboardKey.Tab) && _hudPanel == 0))
            ImGui.SetWindowFocus(_hudPanel switch { 0 => "##TopBar", 1 => "##Survivor", 2 => ctx.ActiveCombat != null ? "##Combat" : "##LocationInspector", _ => "##Events" });

        bool mapInteractive = _stack.LastOrDefault() is Prompt<PlayerAction> && !_stack[^1].Finished;
        bool combatInteractive = _stack.LastOrDefault() is Prompt<CombatInput?> && !_stack[^1].Finished;
        ImGui.BeginDisabled(modalOpen || !UiInput.Ready);
        _hud.Render(ctx, _world, mapInteractive && UiInput.Ready, combatInteractive && UiInput.Ready);
        ImGui.EndDisabled();
        if (UiSettingsDialog.Requested && !modalOpen)
        {
            UiSettingsDialog.Requested = false;
            var settings = new UiSettingsDialog();
            Push(new Prompt<bool>((self, _, _, active) => { if (settings.Render(active)) self.Finish(true); }));
        }
        for (int i = 0; i < _stack.Count; i++)
        {
            bool isTop = i == _stack.Count - 1;
            bool ready = UiInput.Ready;
            UiInput.Ready = ready && isTop && ReferenceEquals(owner, _stack[i]);
            ImGui.BeginDisabled(!UiInput.Ready);
            _stack[i].Render(ctx, dt, UiInput.Ready);
            ImGui.EndDisabled();
            UiInput.Ready = ready;
        }

        rlImGui.End();
        Raylib.EndDrawing();

        PublishFinishedModals();
    }

    private void ResolveTimeWaiters(float dt)
    {
        if (_frameWaiters.Count > 0)
        {
            var waiters = _frameWaiters.ToArray();
            _frameWaiters.Clear();
            foreach (var waiter in waiters)
                waiter.SetResult(dt);
        }

        for (int i = _timeWaiters.Count - 1; i >= 0; i--)
        {
            var waiter = _timeWaiters[i];
            waiter.RemainingSeconds -= dt;
            if (waiter.RemainingSeconds > 0) continue;

            _timeWaiters.RemoveAt(i);
            waiter.Completion.SetResult(true);
        }
    }

    private void PublishFinishedModals()
    {
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            if (!_stack[i].Finished) continue;

            var modal = _stack[i];
            _stack.RemoveAt(i);
            modal.Publish();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MODAL STACK
    // ═══════════════════════════════════════════════════════════════════════════

    private abstract class Modal
    {
        public bool Finished { get; protected set; }
        public virtual bool DimsWorld => true;
        public abstract void Render(GameContext ctx, float dt, bool isTop);
        public abstract void Publish();
    }

    /// <summary>A modal that resolves to a value. Everything the player answers is one of these.</summary>
    private sealed class Prompt<T> : Modal
    {
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action<Prompt<T>, GameContext, float, bool> _render;
        private readonly bool _dims;
        private T _result = default!;

        public Prompt(Action<Prompt<T>, GameContext, float, bool> render, bool dims = true)
        {
            _render = render;
            _dims = dims;
        }

        public Task<T> Task => _completion.Task;
        public override bool DimsWorld => _dims;

        public void Finish(T value)
        {
            if (Finished) return;
            _result = value;
            Finished = true;
        }

        public override void Render(GameContext ctx, float dt, bool isTop)
        {
            if (!Finished) _render(this, ctx, dt, isTop);
        }

        public override void Publish() => _completion.SetResult(_result);
    }

    private Task<T> Push<T>(Prompt<T> prompt)
    {
        _stack.Add(prompt);
        return prompt.Task;
    }

    private sealed class TimeWaiter
    {
        public float RemainingSeconds;
        public TaskCompletionSource<bool> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TIME
    // ═══════════════════════════════════════════════════════════════════════════

    public Task<float> NextFrame()
    {
        var waiter = new TaskCompletionSource<float>(TaskCreationOptions.RunContinuationsAsynchronously);
        _frameWaiters.Add(waiter);
        return waiter.Task;
    }

    public Task Wait(float seconds)
    {
        var waiter = new TimeWaiter { RemainingSeconds = seconds };
        _timeWaiters.Add(waiter);
        return waiter.Completion.Task;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PROMPTS
    // ═══════════════════════════════════════════════════════════════════════════

    public Task<T> Select<T>(string prompt, IReadOnlyList<T> choices, Func<T, string> display,
        Func<T, bool>? isDisabled = null) where T : notnull
    {
        if (choices.Count == 0)
            throw new ArgumentException($"Select was given no choices for prompt: {prompt}", nameof(choices));

        int safe = choices.ToList().FindIndex(c => c is ValueTuple<string, string> pair && pair.Item1 is "cancel" or "back" or "no");
        bool focus = true;
        return Push(new Prompt<T>((self, _, _, _) =>
        {
            OverlaySizes.SetupStandard();
            ImGui.Begin("Select", DialogFlags);
            UiText.Wrapped(prompt);
            UiText.Disabled("1-9: choose · Tab / arrows · Enter: select");
            ImGui.Separator();
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                bool disabled = isDisabled?.Invoke(choice) ?? false;
                if (!disabled && (safe < 0 || i == safe)) UiNavigation.Initial(ref focus);
                if (UiControls.Button(new($"choice-{i}", UiNavigation.Numbered(display(choice), i), "continue",
                    DisabledReason: disabled ? "Unavailable for this choice." : null), size: new Vector2(-1, 0)) || !disabled && UiNavigation.Number(i))
                    self.Finish(choice);
            }
            if (safe >= 0 && !(isDisabled?.Invoke(choices[safe]) ?? false) && UiNavigation.Key(ImGuiKey.Escape)) self.Finish(choices[safe]);
            ImGui.End();
        }));
    }

    public Task<bool> Confirm(string prompt)
    {
        var dialog = new ConfirmationDialog(prompt);
        return Push(new Prompt<bool>((self, _, _, _) =>
        {
            if (dialog.Render() is { } answer) self.Finish(answer);
        }));
    }

    public Task<string> Choose(string message, IReadOnlyList<(string id, string label)> buttons)
    {
        if (buttons.Count == 0)
            throw new ArgumentException($"Choose was given no buttons for: {message}", nameof(buttons));
        bool focus = true;
        int safeIndex = buttons.ToList().FindIndex(b => b.id is "cancel" or "no" or "back");
        int initialIndex = safeIndex >= 0 ? safeIndex : 0;
        return Push(new Prompt<string>((self, _, _, _) =>
        {
            OverlaySizes.SetupStandard();
            ImGui.Begin("Choose", DialogFlags);
            UiText.Wrapped(message);
            UiText.Disabled("1-9: choose · Tab / arrows · Enter: select");
            ImGui.Separator();
            for (int i = 0; i < buttons.Count; i++)
            {
                var (id, label) = buttons[i];
                if (i == initialIndex) UiNavigation.Initial(ref focus);
                if (UiIcons.Button(i == safeIndex ? "back" : "continue", UiNavigation.Numbered(label, i), id, new Vector2(-1, 0)) || UiNavigation.Number(i)) self.Finish(id);
            }
            if (safeIndex >= 0 && UiNavigation.Key(ImGuiKey.Escape)) self.Finish(buttons[safeIndex].id);
            ImGui.End();
        }));
    }

    public Task<int> ReadInt(string prompt, int min, int max, bool allowCancel = false)
    {
        if (min > max) throw new ArgumentException("Minimum must not exceed maximum.");
        int value = min;
        bool focus = true;
        return Push(new Prompt<int>((self, _, _, _) =>
        {
            OverlaySizes.SetupSmallDialog();
            ImGui.Begin("Input", DialogFlags);
            UiText.Wrapped(prompt);
            UiText.Text($"Range: {min} - {max}");
            UiNavigation.Initial(ref focus);
            // Enter commits the edited number; out-of-range input stays visible for correction.
            bool submitted = UiControls.InputIntSubmitted("Amount", ref value);
            bool valid = value >= min && value <= max;
            UiText.Colored(valid ? UiStyle.Supporting : UiStyle.Caution,
                valid ? "Enter: confirm amount" : "Enter an amount within the range.");
            if (UiControls.Button(new("amount-ok", "OK", "confirm", DisabledReason: valid ? null : "Amount is outside the range."), size: new Vector2(-1, 0)) || submitted && valid && UiInput.Ready)
                self.Finish(value);
            if (allowCancel && (UiIcons.Button("cancel", "Cancel [Esc]", "amount-cancel", new Vector2(-1, 0)) || UiNavigation.Key(ImGuiKey.Escape))) self.Finish(-1);
            ImGui.End();
        }));
    }

    public Task ShowMessage(string title, string message) =>
        Push(new Prompt<bool>((self, _, _, isTop) =>
        {
            CenterDialog(650);
            ImGui.Begin(title, DialogFlags);

            UiText.Wrapped(message);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (UiIcons.Button("continue", "Continue [Enter]", "continue", new Vector2(-1, 0)) || (isTop && UiInput.Pressed(KeyboardKey.Enter)))
                self.Finish(true);

            ImGui.End();
        }));

    public Task ShowWorkResult(WorkResultView view)
    {
        var dialog = new WorkResultDialog(view);
        return Push(new Prompt<bool>((self, _, _, _) => { if (dialog.Render()) self.Finish(true); }));
    }

    public Task<string> ShowEventChoices(EventDto evt)
    {
        _event.ShowEvent(evt);

        return Push(new Prompt<string>((self, _, dt, _) =>
        {
            string? choice = _event.Render(dt);
            if (choice != null) self.Finish(choice);
        }));
    }

    public Task ShowEventOutcome(EventDto outcome)
    {
        _event.ShowEvent(outcome);

        return Push(new Prompt<bool>((self, _, dt, _) =>
        {
            _event.Render(dt);
            if (!_event.IsOpen) self.Finish(true);
        }));
    }

    public Task<(ForageFocus? focus, int minutes)> SelectForageOptions(ForageFeature feature, IReadOnlyList<ForageClue> clues)
    {
        _forage.Open(_ctx, feature, clues.ToList());

        return Push(new Prompt<(ForageFocus?, int)>((self, ctx, dt, _) =>
        {
            var result = _forage.Render(ctx, dt);
            if (result == null) return;

            _forage.Close();
            self.Finish((result.Focus, result.Minutes));
        }));
    }

    public Task<string?> SelectButcherMode(CarcassFeature carcass, IReadOnlyList<string> warnings, bool hasCuttingTool)
    {
        var modes = new List<(string id, string label, int minutes)>
        {
            ("quick", "Quick Strip - Fast, meat-focused, messy", carcass.GetRemainingMinutes(ButcheringMode.QuickStrip)),
            ("careful", "Careful - Balanced approach", carcass.GetRemainingMinutes(ButcheringMode.Careful))
        };

        // Full processing needs precise extractions, which need a blade.
        if (hasCuttingTool)
            modes.Add(("full", "Full Processing - Slow, maximum yield", carcass.GetRemainingMinutes(ButcheringMode.FullProcessing)));

        var choices = new List<(string id, string label)> { ("cancel", "Cancel") };
        foreach (var (id, label, minutes) in modes)
            choices.Add((id, $"{label} (~{minutes}min total)"));

        string description = $"Butcher: {carcass.AnimalName}\n" +
            $"Condition: {carcass.GetDecayDescription()}\n" +
            $"Total yield: ~{carcass.GetTotalRemainingKg():F1}kg\n\n" +
            "Choose your approach:";

        if (warnings.Count > 0)
            description += "\n\n" + string.Join("\n", warnings);

        return SelectFromList(description, choices);
    }

    private async Task<string?> SelectFromList(string description, List<(string id, string label)> choices)
    {
        var selection = await Select(description, choices, c => c.label);
        return selection.id == "cancel" ? null : selection.id;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SCREENS
    // ═══════════════════════════════════════════════════════════════════════════

    public Task ShowInventory()
    {
        _inventory.IsOpen = true;

        return Push(new Prompt<bool>((self, ctx, dt, isTop) =>
        {
            if (isTop && UiInput.Pressed(KeyboardKey.Escape)) _inventory.IsOpen = false;
            if (_inventory.IsOpen) _inventory.Render(ctx, dt);
            if (!_inventory.IsOpen) self.Finish(true);
        }));
    }

    public Task ShowDiscoveryLog()
    {
        _discoveryLog.IsOpen = true;
        _discoveryLog.SetData(_ctx.Discoveries.ToDto());

        return Push(new Prompt<bool>((self, _, dt, isTop) =>
        {
            if (isTop && UiInput.Pressed(KeyboardKey.Escape)) _discoveryLog.IsOpen = false;
            if (_discoveryLog.IsOpen) _discoveryLog.Render(dt);
            if (!_discoveryLog.IsOpen) self.Finish(true);
        }));
    }

    public Task ShowNPCs()
    {
        _npcs.IsOpen = true;

        return Push(new Prompt<bool>((self, ctx, dt, isTop) =>
        {
            if (isTop && UiInput.Pressed(KeyboardKey.Escape)) _npcs.IsOpen = false;
            if (_npcs.IsOpen) _npcs.Render(ctx, dt);
            if (!_npcs.IsOpen) self.Finish(true);
        }));
    }

    public Task ShowTransfer(Inventory storage, string storageName)
    {
        _transfer.Open(storage, storageName);

        return Push(new Prompt<bool>((self, ctx, dt, isTop) =>
        {
            if (isTop && UiInput.Pressed(KeyboardKey.Escape)) _transfer.IsOpen = false;

            if (_transfer.IsOpen)
            {
                var move = _transfer.Render(ctx, dt);
                // Moving an item costs no game time, so the screen applies it directly.
                if (move != null) ApplyTransfer(ctx, move, storage);
            }

            if (!_transfer.IsOpen) self.Finish(true);
        }));
    }

    private void ApplyTransfer(GameContext ctx, TransferResult move, Inventory storage)
    {
        _transfer.SetMessage(move.Apply(ctx.Inventory, storage));
    }

    public Task<CraftOption?> ShowCrafting()
    {
        _crafting.IsOpen = true;

        return Push(new Prompt<CraftOption?>((self, ctx, dt, isTop) =>
        {
            if (isTop && UiInput.Pressed(KeyboardKey.Escape)) _crafting.IsOpen = false;

            if (_crafting.IsOpen)
                _crafting.Render(ctx, _craftingSystem, dt);

            if (_crafting.SelectedRecipe != null)
            {
                var recipe = _crafting.SelectedRecipe;
                _crafting.ClearSelectedRecipe();
                self.Finish(recipe);
                return;
            }

            if (!_crafting.IsOpen) self.Finish(null);
        }));
    }

    public Task<FireOverlayResult?> ShowFire(FireFeedback? feedback)
    {
        // The screen keeps its selection between actions, so only open it the first time.
        if (!_fire.IsOpen) _fire.Open();

        if (feedback != null)
        {
            if (feedback.AttemptSucceeded.HasValue)
                _fire.SetAttemptResult(feedback.AttemptSucceeded.Value, feedback.Message);
            else
                _fire.SetTendMessage(feedback.Message);
        }

        return Push(new Prompt<FireOverlayResult?>((self, ctx, dt, isTop) =>
        {
            if (isTop && UiInput.Pressed(KeyboardKey.Escape)) _fire.IsOpen = false;

            if (_fire.IsOpen)
            {
                var result = _fire.Render(ctx, dt);
                if (result != null)
                {
                    self.Finish(result);
                    return;
                }
            }

            if (!_fire.IsOpen) self.Finish(null);
        }));
    }

    public Task<PendingFoodAction?> ShowFood()
    {
        _food.Open();

        return Push(new Prompt<PendingFoodAction?>((self, ctx, dt, isTop) =>
        {
            if (isTop && UiInput.Pressed(KeyboardKey.Escape)) _food.IsOpen = false;

            if (_food.IsOpen) _food.Render(ctx, dt);

            if (_food.PendingAction != null)
            {
                var action = _food.PendingAction;
                _food.ClearPendingAction();
                self.Finish(action);
                return;
            }

            if (!_food.IsOpen) self.Finish(null);
        }));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BASE SCREENS
    // ═══════════════════════════════════════════════════════════════════════════

    public Task<PlayerAction> WaitForPlayerAction() =>
        Push(new Prompt<PlayerAction>(RenderMapScreen, dims: false));

    private void RenderMapScreen(Prompt<PlayerAction> self, GameContext ctx, float dt, bool isTop)
    {
        if (isTop && _hud.TakeAction() is { } action) self.Finish(action);
    }

    public Task<CombatInput?> WaitForCombatAction() =>
        Push(new Prompt<CombatInput?>((self, ctx, dt, isTop) =>
        {
            if (isTop && _hud.TakeCombatInput() is { } input) self.Finish(input);
        }, dims: false));

    public ProgressView BeginProgress(ProgressKind kind, string status)
    {
        var modal = new ProgressModal(kind);
        _stack.Add(modal);

        var view = new ProgressView(kind, status, modal.WaitForContinue, () => _stack.Remove(modal));
        modal.Bind(view);
        return view;
    }

    private sealed class ProgressModal(ProgressKind kind) : Modal
    {
        private ProgressView _view = null!;
        private TaskCompletionSource<bool>? _continue;

        public void Bind(ProgressView view) => _view = view;

        public Task WaitForContinue()
        {
            _continue ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _continue.Task;
        }

        public override void Render(GameContext ctx, float dt, bool isTop)
        {
            OverlaySizes.SetupDialog();
            ImGui.Begin(Title(kind), DialogFlags);

            UiIcons.LabelWrapped(kind == ProgressKind.Forage ? "foraging" : kind == ProgressKind.Crafting ? "project" : "clock", UiStyle.Text, _view.Status);
            ImGui.Spacing();
            ImGui.ProgressBar(Math.Clamp(_view.Progress, 0f, 1f), new Vector2(-1, UiStyle.BarHeight),
                $"{_view.SimulatedMinutes}/{_view.TotalMinutes} min");

            foreach (var section in _view.Sections)
            {
                if (section.Lines.Count == 0) continue;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                UiIcons.Label("journal", section.Header);

                foreach (var line in section.Lines)
                {
                    ImGui.Indent();
                    if (line.Icon != null)
                        UiIcons.LabelColored(line.Icon, ToneColor(line.Tone), line.Text);
                    else
                        UiText.Colored(ToneColor(line.Tone), line.Text);
                    ImGui.Unindent();
                }
            }

            if (_continue != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (UiIcons.Button("continue", "Continue [Enter]", "continue", new Vector2(-1, 0)) || (isTop && UiInput.Pressed(KeyboardKey.Enter)))
                {
                    var waiter = _continue;
                    _continue = null;
                    waiter.SetResult(true);
                }
            }

            ImGui.End();
        }

        public override void Publish() { }

        private static string Title(ProgressKind kind) => kind switch
        {
            ProgressKind.Forage => "Searching",
            ProgressKind.Crafting => "Crafting",
            _ => "Activity"
        };
    }

    private static Vector4 ToneColor(ProgressTone tone) => tone switch
    {
        ProgressTone.Muted => UiStyle.Supporting,
        ProgressTone.Done => UiStyle.Success,
        ProgressTone.Discovery => new Vector4(0.9f, 0.7f, 0.3f, 1f),
        ProgressTone.Fuel => new Vector4(0.8f, 0.6f, 0.4f, 1f),
        ProgressTone.Food => new Vector4(0.5f, 0.8f, 0.5f, 1f),
        ProgressTone.Medicine => new Vector4(0.7f, 0.5f, 0.8f, 1f),
        ProgressTone.Material => new Vector4(0.6f, 0.7f, 0.8f, 1f),
        ProgressTone.Tinder => new Vector4(0.9f, 0.7f, 0.5f, 1f),
        _ => new Vector4(0.9f, 0.9f, 0.9f, 1f)
    };

    // ═══════════════════════════════════════════════════════════════════════════

    private const ImGuiWindowFlags DialogFlags =
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse;

    private static void CenterDialog(float width)
    {
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
            ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(width, 0), ImGuiCond.Always);
    }
}
