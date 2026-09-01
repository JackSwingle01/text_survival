using ImGuiNET;
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
using text_survival.Desktop.Input;
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
public sealed class DesktopUi : IGameUi
{
    private static readonly Color Background = new(20, 25, 30, 255);
    private static readonly Color Dim = new(0, 0, 0, 128);

    private readonly GameContext _ctx;
    private readonly FrameScheduler _scheduler;

    private readonly WorldRenderer _world = new();
    private readonly ActionPanel _actionPanel;
    private readonly TilePopup _tilePopup = new();

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
    private readonly List<TaskCompletionSource<float>> _frameWaiters = [];
    private readonly List<TimeWaiter> _timeWaiters = [];

    public DesktopUi(GameContext ctx, FrameScheduler scheduler)
    {
        _ctx = ctx;
        _scheduler = scheduler;
        _actionPanel = new ActionPanel(_world);
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

        ResolveTimeWaiters(dt);

        AudioManager.Update();
        _world.Update(ctx, dt);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Background);

        _world.Render(ctx);

        if (_stack.Any(m => m.DimsWorld))
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Dim);

        rlImGui.Begin();

        for (int i = 0; i < _stack.Count; i++)
            _stack[i].Render(ctx, dt, isTop: i == _stack.Count - 1);

        // The HUD is unconditional: the same stats, journal and toasts in every state.
        StatsPanel.Render(ctx);
        ToastManager.Render(dt);

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

        return Push(new Prompt<T>((self, _, _, _) =>
        {
            OverlaySizes.SetupDialog();
            ImGui.Begin("Select", DialogFlags);

            ImGui.TextWrapped(prompt);
            ImGui.Separator();
            ImGui.Spacing();

            foreach (var choice in choices)
            {
                string label = display(choice);
                bool disabled = isDisabled?.Invoke(choice) ?? false;

                if (disabled)
                {
                    ImGui.BeginDisabled();
                    ImGui.Button(label, new Vector2(-1, 0));
                    ImGui.EndDisabled();
                }
                else if (ImGui.Button(label, new Vector2(-1, 0)))
                {
                    self.Finish(choice);
                }
            }

            ImGui.End();
        }));
    }

    public Task<bool> Confirm(string prompt) =>
        Push(new Prompt<bool>((self, _, _, _) =>
        {
            OverlaySizes.SetupSmallDialog();
            ImGui.Begin("Confirm", DialogFlags);

            ImGui.TextWrapped(prompt);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            float buttonWidth = (ImGui.GetContentRegionAvail().X - 10) / 2;

            if (ImGui.Button("Yes", new Vector2(buttonWidth, 30))) self.Finish(true);
            ImGui.SameLine();
            if (ImGui.Button("No", new Vector2(buttonWidth, 30))) self.Finish(false);

            ImGui.End();
        }));

    public Task<string> Choose(string message, IReadOnlyList<(string id, string label)> buttons)
    {
        if (buttons.Count == 0)
            throw new ArgumentException($"Choose was given no buttons for: {message}", nameof(buttons));

        return Push(new Prompt<string>((self, _, _, _) =>
        {
            CenterDialog(650);
            ImGui.Begin("Confirm", DialogFlags);

            ImGui.TextWrapped(message);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            foreach (var (id, label) in buttons)
            {
                if (ImGui.Button(label, new Vector2(-1, 30)))
                    self.Finish(id);
            }

            ImGui.End();
        }));
    }

    public Task<int> ReadInt(string prompt, int min, int max, bool allowCancel = false)
    {
        int value = min;

        return Push(new Prompt<int>((self, _, _, _) =>
        {
            OverlaySizes.SetupSmallDialog();
            ImGui.Begin("Input", DialogFlags);

            ImGui.TextWrapped(prompt);
            ImGui.Spacing();

            ImGui.SliderInt("##value", ref value, min, max);
            ImGui.Text($"Range: {min} - {max}");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            float buttonWidth = allowCancel ? (ImGui.GetContentRegionAvail().X - 10) / 2 : -1;

            if (ImGui.Button("OK", new Vector2(buttonWidth, 30)))
                self.Finish(value);

            if (allowCancel)
            {
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(buttonWidth, 30)))
                    self.Finish(-1);
            }

            ImGui.End();
        }));
    }

    public Task ShowMessage(string title, string message) =>
        Push(new Prompt<bool>((self, _, _, isTop) =>
        {
            CenterDialog(650);
            ImGui.Begin(title, DialogFlags);

            ImGui.TextWrapped(message);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Continue [Enter]", new Vector2(-1, 30)) || (isTop && Raylib.IsKeyPressed(KeyboardKey.Enter)))
                self.Finish(true);

            ImGui.End();
        }));

    public Task ShowWorkResult(WorkResultView view)
    {
        var text = new System.Text.StringBuilder();

        if (view.Narrative is { Count: > 0 })
        {
            foreach (var line in view.Narrative)
                text.AppendLine(line);
            text.AppendLine();
        }

        text.Append(view.Message);

        if (view.Warnings is { Count: > 0 })
        {
            text.AppendLine();
            text.AppendLine();
            foreach (var warning in view.Warnings)
                text.AppendLine($"⚠ {warning}");
        }

        if (view.ItemsGained.Count > 0)
        {
            text.AppendLine();
            text.AppendLine();
            text.AppendLine("Gained:");
            foreach (var item in view.ItemsGained)
                text.AppendLine($"  - {item}");
        }

        return ShowMessage(view.Title, text.ToString().TrimEnd());
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
            if (isTop && Raylib.IsKeyPressed(KeyboardKey.Escape)) _inventory.IsOpen = false;
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
            if (isTop && Raylib.IsKeyPressed(KeyboardKey.Escape)) _discoveryLog.IsOpen = false;
            if (_discoveryLog.IsOpen) _discoveryLog.Render(dt);
            if (!_discoveryLog.IsOpen) self.Finish(true);
        }));
    }

    public Task ShowNPCs()
    {
        _npcs.IsOpen = true;

        return Push(new Prompt<bool>((self, ctx, dt, isTop) =>
        {
            if (isTop && Raylib.IsKeyPressed(KeyboardKey.Escape)) _npcs.IsOpen = false;
            if (_npcs.IsOpen) _npcs.Render(ctx, dt);
            if (!_npcs.IsOpen) self.Finish(true);
        }));
    }

    public Task ShowTransfer(Inventory storage, string storageName)
    {
        _transfer.Open(storage, storageName);

        return Push(new Prompt<bool>((self, ctx, dt, isTop) =>
        {
            if (isTop && Raylib.IsKeyPressed(KeyboardKey.Escape)) _transfer.IsOpen = false;

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
        var source = move.FromPlayer ? ctx.Inventory : storage;
        var dest = move.FromPlayer ? storage : ctx.Inventory;
        string direction = move.FromPlayer ? "to storage" : "to inventory";

        TransferHandler.TransferResult result = move switch
        {
            { Resource: { } r } => TransferHandler.TransferResource(source, dest, r, direction),
            { Tool: { } t } => TransferHandler.TransferTool(source, dest, t, direction),
            { Equipment: { } e } => TransferHandler.TransferEquipment(source, dest, e, direction),
            { Accessory: { } a } => TransferHandler.TransferAccessory(source, dest, a, direction),
            _ => throw new InvalidOperationException("The transfer screen returned a move with nothing in it.")
        };

        if (result.Success)
            _transfer.SetMessage(result.Message);
    }

    public Task<CraftOption?> ShowCrafting()
    {
        _crafting.IsOpen = true;

        return Push(new Prompt<CraftOption?>((self, ctx, dt, isTop) =>
        {
            if (isTop && Raylib.IsKeyPressed(KeyboardKey.Escape)) _crafting.IsOpen = false;

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

    public Task<FireOverlayResult?> ShowFire(HeatSourceFeature? fire, FireFeedback? feedback)
    {
        // The screen keeps its selection between actions, so only open it the first time.
        if (!_fire.IsOpen) _fire.Open(fire);

        if (feedback != null)
        {
            if (feedback.AttemptSucceeded.HasValue)
                _fire.SetAttemptResult(feedback.AttemptSucceeded.Value, feedback.Message);
            else
                _fire.SetTendMessage(feedback.Message);
        }

        return Push(new Prompt<FireOverlayResult?>((self, ctx, dt, isTop) =>
        {
            if (isTop && Raylib.IsKeyPressed(KeyboardKey.Escape)) _fire.IsOpen = false;

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
            if (isTop && Raylib.IsKeyPressed(KeyboardKey.Escape)) _food.IsOpen = false;

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
        if (isTop)
        {
            var keyed = ReadMapInput(ctx);
            if (keyed != null)
            {
                _tilePopup.Hide();
                self.Finish(keyed);
                return;
            }
        }

        if (_tilePopup.IsOpen && _tilePopup.Render(ctx, dt) == "go" && _tilePopup.SelectedTile.HasValue)
        {
            var (x, y) = _tilePopup.SelectedTile.Value;
            _tilePopup.Hide();
            self.Finish(new PlayerAction.Travel(x, y));
            return;
        }

        var (campAction, workStrategy, _) = _actionPanel.Render(ctx, dt);

        if (campAction != null)
        {
            _tilePopup.Hide();
            self.Finish(new PlayerAction.Camp(campAction.Value));
        }
        else if (workStrategy != null)
        {
            _tilePopup.Hide();
            self.Finish(new PlayerAction.Work(workStrategy));
        }
    }

    /// <summary>
    /// Mouse and keyboard on the map: a click opens the tile popup, WASD walks, hotkeys
    /// take actions. Returns the action the player asked for, or null.
    /// </summary>
    private PlayerAction? ReadMapInput(GameContext ctx)
    {
        var map = ctx.Map ?? throw new InvalidOperationException("The map screen needs a map.");

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !ImGui.GetIO().WantCaptureMouse)
        {
            var clicked = _world.HandleClick();
            if (clicked.HasValue && map.IsValidPosition(clicked.Value.x, clicked.Value.y))
            {
                var location = map.GetLocationAt(clicked.Value.x, clicked.Value.y);
                if (location != null && location.Visibility != TileVisibility.Unexplored)
                {
                    var screenPos = _world.GetTileScreenPosition(clicked.Value.x, clicked.Value.y);
                    _tilePopup.Show(ctx, clicked.Value.x, clicked.Value.y, screenPos);
                }
            }
        }

        var step = ReadMovementKey();
        if (step != null)
        {
            var current = map.CurrentPosition;
            int targetX = current.X + step.Value.dx;
            int targetY = current.Y + step.Value.dy;

            if (!map.CanMoveTo(targetX, targetY))
                _actionPanel.ShowMessage("Cannot move there.");
            else if (map.IsEdgeBlocked(current, new GridPosition(targetX, targetY), ctx.Weather.CurrentSeason))
                _actionPanel.ShowMessage("The way is blocked.");
            else
                return new PlayerAction.Travel(targetX, targetY);
        }

        if (HotkeyRegistry.IsPressed(HotkeyAction.Inventory)) return new PlayerAction.Camp(CampAction.Inventory);
        if (HotkeyRegistry.IsPressed(HotkeyAction.Crafting)) return new PlayerAction.Camp(CampAction.Crafting);
        if (HotkeyRegistry.IsPressed(HotkeyAction.DiscoveryLog)) return new PlayerAction.Camp(CampAction.DiscoveryLog);
        if (HotkeyRegistry.IsPressed(HotkeyAction.NPCs)) return new PlayerAction.Camp(CampAction.NPCs);
        if (HotkeyRegistry.IsPressed(HotkeyAction.Storage)) return new PlayerAction.Camp(CampAction.Storage);
        if (HotkeyRegistry.IsPressed(HotkeyAction.Wait)) return new PlayerAction.Camp(CampAction.Wait);

        if (HotkeyRegistry.IsPressed(HotkeyAction.Fire))
            return new PlayerAction.Camp(HasFireToTend(ctx) ? CampAction.TendFire : CampAction.StartFire);

        if (HotkeyRegistry.IsPressed(HotkeyAction.Forage) && ctx.CurrentLocation.GetFeature<ForageFeature>() != null)
            return new PlayerAction.Work(new ForageStrategy());

        if (HotkeyRegistry.IsPressed(HotkeyAction.Cancel))
            _tilePopup.Hide();

        return null;
    }

    private static (int dx, int dy)? ReadMovementKey()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.Up)) return (0, -1);
        if (Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressed(KeyboardKey.Down)) return (0, 1);
        if (Raylib.IsKeyPressed(KeyboardKey.A) || Raylib.IsKeyPressed(KeyboardKey.Left)) return (-1, 0);
        if (Raylib.IsKeyPressed(KeyboardKey.D) || Raylib.IsKeyPressed(KeyboardKey.Right)) return (1, 0);
        return null;
    }

    private static bool HasFireToTend(GameContext ctx)
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        return fire != null && (fire.IsActive || fire.HasEmbers) && ctx.Inventory.HasFuel;
    }

    public Task<CombatInput?> WaitForCombatAction() =>
        Push(new Prompt<CombatInput?>((self, ctx, dt, isTop) =>
        {
            if (isTop)
            {
                var moveTarget = _world.HandleCombatClick();
                if (moveTarget.HasValue)
                {
                    self.Finish(new CombatInput(null, new GridPosition(moveTarget.Value.x, moveTarget.Value.y)));
                    return;
                }
            }

            var (_, _, combatAction) = _actionPanel.Render(ctx, dt);
            if (combatAction != null)
                self.Finish(new CombatInput(combatAction, null));
        }, dims: false));

    // ═══════════════════════════════════════════════════════════════════════════
    // PROGRESS
    // ═══════════════════════════════════════════════════════════════════════════

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

            ImGui.TextWrapped(_view.Status);
            ImGui.Spacing();
            ImGui.ProgressBar(Math.Clamp(_view.Progress, 0f, 1f), new Vector2(-1, 20),
                $"{_view.SimulatedMinutes}/{_view.TotalMinutes} min");

            foreach (var section in _view.Sections)
            {
                if (section.Lines.Count == 0) continue;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.Text(section.Header);

                foreach (var line in section.Lines)
                    ImGui.TextColored(ToneColor(line.Tone), $"  {line.Text}");
            }

            if (_continue != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.Button("Continue [Enter]", new Vector2(-1, 30)) || (isTop && Raylib.IsKeyPressed(KeyboardKey.Enter)))
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
        ProgressTone.Muted => new Vector4(0.5f, 0.5f, 0.5f, 0.6f),
        ProgressTone.Done => new Vector4(0.5f, 0.7f, 0.5f, 0.7f),
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
