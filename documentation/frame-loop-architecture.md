# Frame Loop Architecture

*How the desktop game loop, the simulation, and the UI fit together. This replaces the
"blocking dialog with nested render loops" design that was ported from the web version.*

---

## Why this exists

The previous design let game logic own the frame: every prompt, progress bar and screen
spun its own `while (!WindowShouldClose) { BeginDrawing … EndDrawing }` loop. That produced
30 nested frame loops and 25 hand-copied frame compositions, a HUD that changed depending
on which loop you were in, toast timers that froze in half the screens, a simulation that
opened modal dialogs from inside its own tick, and animation clocks that drifted apart
from the simulation they were supposed to depict. It also made nothing in `Actions/`
runnable without a window.

The new design has **one frame loop**, **one frame composition**, and a **one-way
dependency**: UI depends on the simulation; the simulation never depends on the UI.
Game logic stays written as ordinary sequential code — that is the one thing the old
design got right and it is preserved via `async`/`await`.

---

## The three layers

```
Core/Program.cs            the only frame loop
      │
      ▼
Desktop/DesktopUi          IGameUi implementation: modal stack, HUD, world rendering, input
      │ reads state, resolves prompts
      ▼
Actions/, Combat/, …       game logic: async, sequential, awaits ctx.Ui.* for every player interaction
      │ calls
      ▼
GameContext.UpdateInternal the simulation tick: synchronous, pure state mutation, never touches Ui
```

| Layer | May reference | Must never reference |
|---|---|---|
| `Core/`, `Desktop/` | everything | — |
| game logic (`Actions/`, `Combat/`, handlers, strategies) | `IGameUi` via `ctx.Ui`, game state | `Raylib`, `ImGui`, anything in `Desktop/` |
| simulation (`GameContext.UpdateInternal` and everything it calls: `Actors/`, `Environments/`, `Survival/`, `Bodies/`, `Items/`, `Effects/`) | game state | `ctx.Ui`, `Desktop/`, `Raylib`, `ImGui` |

A test enforces the last two rows by scanning source files (see *Guard test*).

---

## Invariants

These are the rules. Everything else in this document is consequence.

1. **Exactly one `Raylib.BeginDrawing()` call site in the codebase** — `DesktopUi.Frame`.
2. **Game logic runs only inside `FrameScheduler.Pump()`**, never inside `DesktopUi.Frame`.
   Prompts complete their `TaskCompletionSource` from inside `Frame`; the continuation is
   *posted* to the scheduler and runs on the next `Pump`. Every TCS is created with
   `TaskCreationOptions.RunContinuationsAsynchronously` so no continuation can run inline.
3. **No threads.** Game code never uses `Task.Run`, `Task.Delay`, `.Wait()`, `.Result`,
   `ConfigureAwait(false)`, `async void`, or `Thread`. The scheduler is single-threaded;
   any of these would deadlock or run game logic during rendering.
4. **The simulation tick is synchronous and UI-free.** `GameContext.UpdateInternal` and
   everything it calls mutates state and *queues* things (`EventQueue`, `Notices`,
   pending encounter). Game logic drains those queues and awaits the UI. Nothing below
   `UpdateInternal` awaits.
5. **Rendering reads game state; it never mutates it.** UI-only state (camera position,
   hovered tile, selected tile, open screen) lives in `Desktop/`.
6. **One clock per animation.** Anything that both animates and simulates (travel,
   progress bars) derives *both* from a single `TimedRun`. Animation progress and
   simulated minutes are two functions of the same elapsed real time, so they finish
   together by construction. Nothing tracks a separate "animation progress" next to a
   "simulation progress".
7. **`dt` is read once per frame** from `Raylib.GetFrameTime()` in `Program`, clamped to
   `MaxFrameSeconds = 0.1f`, and passed down. A hitch (save, load, GC) costs one slow
   frame; it never teleports an animation.
8. **Never fail silently.** `ctx.Ui` throws if accessed before it is set. A faulted game
   task is rethrown by the main loop, not swallowed. Missing assets and unknown prompt
   results throw.

---

## Core pieces

### `FrameScheduler : SynchronizationContext` (`Core/FrameScheduler.cs`)

A queue of continuations pumped once per frame.

```csharp
public sealed class FrameScheduler : SynchronizationContext
{
    public bool IsPumping { get; }
    public override void Post(SendOrPostCallback d, object? state);   // enqueue
    public override void Send(SendOrPostCallback d, object? state);   // throw — never supported
    public void Pump();   // run queued continuations until the queue is empty
}
```

`Pump` runs everything queued at the moment it is called *and* anything enqueued while it
runs, until the queue is empty. Game code that awaits `NextFrame()` cannot spin the pump
forever because `NextFrame` only completes from `Frame`. `Pump` is not reentrant; it
throws if called while `IsPumping`.

`Program` sets it as the current `SynchronizationContext` before starting the game task,
so every `await` in game code captures it.

### `IGameUi` (`UI/IGameUi.cs`)

The complete surface through which game logic reaches the player. Every method returns a
`Task`. Game logic gets it from `ctx.Ui`.

```csharp
public interface IGameUi
{
    // ── The primitive. Everything timed is built on it. ──
    /// Completes at the start of the next frame with that frame's clamped dt.
    Task<float> NextFrame();
    /// Completes after at least `seconds` of frames have elapsed.
    Task Wait(float seconds);

    // ── Prompts (modal, dim the world, top of stack gets input) ──
    Task<T> Select<T>(string prompt, IReadOnlyList<T> choices, Func<T, string> display,
                      Func<T, bool>? isDisabled = null) where T : notnull;
    Task<bool> Confirm(string prompt);
    Task<string> Choose(string message, IReadOnlyList<(string id, string label)> buttons);
    Task<int> ReadInt(string prompt, int min, int max, bool allowCancel = false);
    Task ShowMessage(string title, string message);
    Task ShowWorkResult(WorkResultView view);
    Task<string> ShowEventChoices(EventDto evt);      // returns the choice id
    Task ShowEventOutcome(EventDto outcome);
    Task<(ForageFocus? focus, int minutes)> SelectForageOptions(ForageFeature feature, IReadOnlyList<ForageClue> clues);
    Task<string?> SelectButcherMode(CarcassFeature carcass, IReadOnlyList<string> warnings, bool hasCuttingTool);

    // ── Screens (modal, self-contained ImGui panels; complete when closed or when they yield a result) ──
    Task ShowInventory();
    Task ShowDiscoveryLog();
    Task ShowNPCs();
    Task ShowTransfer(Inventory storage, string storageName);
    Task<CraftOption?> ShowCrafting();          // null = closed without crafting
    Task<FireOverlayResult?> ShowFire(HeatSourceFeature? fire, string? lastMessage);
    Task<PendingFoodAction?> ShowFood();

    // ── Base screens (the thing the player is "in" when no prompt is up) ──
    Task<PlayerAction> WaitForPlayerAction();    // map screen: action panel, tile popup, WASD, hotkeys
    Task<CombatInput?> WaitForCombatAction();    // combat screen; null = window closing

    // ── Progress (non-blocking handle; caller drives time with NextFrame) ──
    ProgressView BeginProgress(ProgressKind kind, string status);
}
```

Design notes:

- `GameContext` is not a parameter — the implementation holds the context it was created
  with. Game logic already passes `ctx` everywhere; the UI does not need it twice.
- `PlayerAction` is a small discriminated record replacing today's `InputResult` + the
  `ActionPanel` tuple + string action names:
  `Travel(x, y) | Camp(CampAction) | Work(IWorkStrategy) | Quit`. Hotkeys map to
  `Camp(...)` or `Work(...)`; there are no string action names.
- `ProgressView` is a mutable object the game logic updates between frames
  (`Status`, `Progress` 0–1, `Sections` — a list of headed, coloured line lists for
  forage finds / crafting materials). It is a modal until `Dispose()`; `WaitForContinue()`
  shows a Continue button and completes when pressed. Forage, crafting, camp setup, event
  time costs, sleep, and incapacitation all use this one view. There is no bespoke
  "foraging frame" or "crafting frame".
- Screens keep their existing ImGui bodies (`InventoryOverlay`, `CraftingOverlay`,
  `FireOverlay`, …). What changes is the shell: no `IsOpen`, no `OverlayManager`, no
  `PendingCraft`/`PendingAction` side channels — a screen *returns* what the player chose
  and game logic acts on it. Instant actions (transfer an item, add fuel) may be applied
  through a synchronous callback the screen is given; anything that passes game time
  must come back as a result so the caller can run it under a `ProgressView`.

### `ScriptedUi : IGameUi` (`text_survival.Tests/Support/ScriptedUi.cs`)

Answers prompts from a queue of canned responses and returns already-completed tasks.
`NextFrame` returns a fixed dt. Because everything completes synchronously, tests call
`GameRunner.RunAsync(ctx).GetAwaiter().GetResult()` with no scheduler and no window. An
unanswered prompt throws with the prompt text — never returns a default.

### `DesktopUi : IGameUi` (`Desktop/DesktopUi.cs`)

Owns everything visual: `WorldRenderer`, `Camera`, HUD panels, the modal stack, the
per-frame `NextFrame`/`Wait` waiters, and the overlay/screen objects.

```csharp
public void Frame(GameContext ctx, float dt)
{
    // 1. resolve time waiters: NextFrame() waiters get dt; Wait() waiters count down
    // 2. input: only the top modal receives input; world hover always updates
    // 3. update UI-only animation: camera smoothing, snow, toast timers
    // 4. draw — the ONLY BeginDrawing in the codebase:
    //      world (map or combat grid)
    //      dim layer if any modal on the stack dims
    //      ImGui: modals bottom→top, then HUD (StatsPanel, JournalPanel, Toasts) — always
}
```

**Modal stack.** `abstract class Modal { abstract void Render(...); virtual bool DimsWorld => true; }`.
Base screens (`MapScreen`, `CombatScreen`) are modals with `DimsWorld => false`; they
sit at the bottom while game logic awaits them. Prompt modals wrap a
`TaskCompletionSource<T>`. Progress views are modals. The stack is empty only while game
logic is running between awaits without a screen up (e.g. passing time with no dialog)
— the world and HUD still draw. Escape closes the top screen if it is closable. Nested
prompts (an event during a progress bar, a confirm inside a screen) stack naturally and
render underneath each other, which is the intended look.

**HUD is unconditional.** `StatsPanel`, `JournalPanel`, and toasts render every frame in
every state — map, combat, dialogs, screens. There is no "minimal" status panel.

### `Program.Main` (`Core/Program.cs`)

```csharp
var scheduler = new FrameScheduler();
SynchronizationContext.SetSynchronizationContext(scheduler);
var ui = new DesktopUi(ctx);
ctx.Ui = ui;

Task<bool> game = GameRunner.RunAsync(ctx);      // runs until its first await

while (!Raylib.WindowShouldClose() && !game.IsCompleted)
{
    float dt = MathF.Min(Raylib.GetFrameTime(), MaxFrameSeconds);
    scheduler.Pump();          // game logic advances until it awaits again
    ui.Frame(ctx, dt);         // one frame; may complete prompts → continuations queued for next Pump
}

if (game.IsFaulted) throw game.Exception!;       // never fail silently
// restart / save / shutdown as today
```

Restart re-creates `ctx` and `DesktopUi` and starts a new game task. On window close the
game task is simply abandoned (it is awaiting a prompt that will never complete); the
save-on-exit path runs as before.

---

## Time passing

`Pacing` (`Actions/Pacing.cs`) is the one place that decides how fast game time flows on
screen:

```csharp
public static class Pacing
{
    public const float SecondsPerMinute = 0.3f;
    public static float ProgressSeconds(int minutes) => Math.Clamp(minutes * SecondsPerMinute, 1f, 30f);
    public static float TravelSeconds(int minutes)   => Math.Clamp(0.5f + minutes * 0.03f, 0.5f, 1.2f);
}
```

`TimedRun` (`Actions/TimedRun.cs`) turns real time into due minutes with a fractional
accumulator — no truncation, no "at least one minute per frame":

```csharp
public sealed class TimedRun(int totalMinutes, float durationSeconds)
{
    public float ElapsedSeconds { get; private set; }
    public int   SimulatedMinutes { get; private set; }
    public float Progress => Math.Min(ElapsedSeconds / durationSeconds, 1f);   // drives animation
    public bool  Done => SimulatedMinutes >= totalMinutes;
    /// Advance the clock; return how many whole minutes are now due (0..n). Never more than remain.
    public int Advance(float dt);
    public void MarkSimulated(int minutes);
}
```

The canonical loop, used by rest, sleep, work sessions, camp setup, event time costs,
incapacitation, and (through `ActiveTravelState`) travel:

```csharp
public static async Task<(int elapsed, bool interrupted)> PassTime(
    GameContext ctx, int minutes, ActivityType activity, ProgressView? view, bool allowEvents = true)
{
    var run = new TimedRun(minutes, Pacing.ProgressSeconds(minutes));
    while (!run.Done && ctx.player.IsAlive)
    {
        float dt = await ctx.Ui.NextFrame();
        int due = run.Advance(dt);
        for (int i = 0; i < due; i++)
        {
            if (allowEvents) await ctx.Update(1, activity); else ctx.UpdateWithoutEvents(1, activity);
            run.MarkSimulated(1);
            if (allowEvents && ctx.EventOccurredLastUpdate) return (run.SimulatedMinutes, true);
            if (!ctx.player.IsAlive) break;
        }
        if (view != null) view.Progress = run.Progress;
    }
    return (run.SimulatedMinutes, false);
}
```

`GameContext.Update` becomes `Task<int>` because handling an event awaits the player. The
`render` parameter is removed. `UpdateWithoutEvents` stays synchronous.

---

## Travel

`ActiveTravelState` keeps `Origin`, `Destination`, `OriginPosition`, the injury/quick
flags, and a `TimedRun` built with `Pacing.TravelSeconds`. It no longer has
`AnimationProgress`, `AnimationDurationSeconds`, or `SimulatedMinutes` of its own — those
are the run's. `TravelRunner.DoTravel` is async and *is* the travel: it sets
`ctx.ActiveTravel`, runs the `PassTime`-shaped loop with `ActivityType.Traveling`, then
does what `CompleteTravel` does today, then clears `ActiveTravel`. There is no
"pending travel processed later by GameRunner" split.

Rendering derives the sprite position: when `ctx.ActiveTravel != null`, the player is
drawn at `Lerp(origin, destination, EaseOutCubic(run.Progress))` in world tile
coordinates; otherwise at `Map.CurrentPosition`. `WorldRenderer.PlayerPositionOverride`
is deleted. The camera target is the same interpolated position, so the camera follows
the sprite instead of racing it on its own clock.

---

## Camera

`Camera` holds a continuous world-space centre (`Vector2 Center`, in tiles) and a
`Target`. Each frame:

```
Center += (Target - Center) * (1 - exp(-Smoothing * dt))     // Smoothing ≈ 10
if |Target - Center| > SnapDistanceTiles (≈ 6) → Center = Target   // new game, teleport
```

`SetCenter(x, y, animate)` and the from/to/progress transition state are removed;
`Snap()` exists for the cases that must not glide.

`WorldToScreen`/`ScreenToWorld` use the float centre. `GetVisibleTiles()` returns the
view plus **one tile of overscan on every side**, and the world is drawn inside
`BeginScissorMode(grid rect)` so nothing spills under the panels while panning. The
5×5 view size, tile-size clamping, and panel-reserved layout stay as they are.

The combat grid rendering is unchanged except that it reads hover from `DesktopUi`.

---

## Simulation queues (the sim never talks to the player)

- **Weather change popup** (`GameContext.UpdateInternal`): deleted. The weather event that
  is already queued on the same line is the player-facing notice; add a journal line
  (`GameDisplay.AddWarning`) for the temperature/wind summary so nothing is lost.
- **NPC death witnessed / body discovered** (`GameContext.UpdateInternal`),
  **resource discovery** (`InventoryCapacityHelper`): become entries on
  `ctx.Notices` (`Queue<Notice(string Title, string Text)>`, `[JsonIgnore]`). Game logic
  drains it — `GameRunner` after each player action and `TravelRunner` on arrival —
  with `await ctx.Ui.ShowMessage`. Same pattern as `HasPendingEncounter`.
- `GameDisplay.Render(ctx)` ("draw a frame so narrative reaches the screen") is deleted
  along with every call site. The frame loop always draws; narrative added before an
  await is on screen when the await renders.

---

## Saving

`SaveManager.Save` serialises ~28 MB of JSON on the render thread. It is called on
every action today. New rule, in one place (`GameRunner`): save when at least
`SaveIntervalSeconds = 120` of real time has passed since the last save, plus always on
quit, death, and victory. The hitch is still there when it happens; invariant 7 keeps it
from breaking animations.

---

## Combat

`CombatOrchestrator.RunWithPlayer` and `RunCombatTurn` are async:
`var input = await ctx.Ui.WaitForCombatAction();` and AI turns are paced with
`await ctx.Ui.Wait(1f)` between turns. `ResolveHeadless` stays synchronous — it has no
player and awaits nothing. The combat screen keeps the action panel's combat buttons and
grid-click movement.

---

## What gets deleted

- `Desktop/DesktopIO.cs`, `Desktop/DesktopRuntime.cs` (including `BlockingDialog`,
  `RenderForagingFrame`, `RenderCraftingProgressFrame`, `RenderMinimalStatePanel`,
  `CraftingMaterialState`), `Desktop/UI/OverlayManager.cs`, `Desktop/Input/InputHandler.cs`
  (its logic moves into `MapScreen`), `IO/Input.cs`, `GameDisplay.Render`,
  `GameDisplay.UpdateAndRenderProgress`.
- `GameRunner.RunGameLoop`, `ProcessTravelTick`, `RenderTravelFrame`, `ProcessAction`
  (string actions), `TravelRunner.CompleteTravel`, `ForageStrategy`'s frame loop,
  `WorldRenderer.PlayerPositionOverride`, `Camera` transition state, both copies of
  `EaseOutCubic` outside one shared `Easing` helper.
- Every `while (!Raylib.WindowShouldClose())` outside `Program.Main`.
- `CraftingOverlay.PendingCraft/ProcessPendingCraft`, `FoodOverlay.PendingAction/ProcessPendingAction`
  and the fire/transfer "process result inside the render loop" code.

`GameDisplay.AddNarrative` and friends stay: they write to the log and raise toasts, which
is sim→state, not sim→UI. `ToastManager` stays but only `DesktopUi.Frame` ticks it.

---

## Guard test

`text_survival.Tests/Architecture/LayeringTests.cs` scans `*.cs` under the repo
(excluding `bin/`, `obj/`, `tools/`, `text_survival.Tests/`) and asserts:

1. Files outside `Desktop/` and `Core/` contain no `Raylib`, `rlImGui`, `ImGuiNET`,
   `ImGui.`, or `text_survival.Desktop` references.
2. Files under `Actors/`, `Environments/`, `Survival/`, `Bodies/`, `Items/`, `Effects/`
   contain no `.Ui` / `IGameUi` references.
3. Exactly one file contains `Raylib.BeginDrawing`.
4. No file outside `Core/` and `Desktop/` contains `Task.Run(`, `Task.Delay(`,
   `.Result`, `.Wait()`, `ConfigureAwait(`, or `async void`.

The message on failure names the file and line.

---

## Headless smoke test

`text_survival.Tests/Actions/GameLoopSmokeTests.cs` runs `GameRunner.RunAsync` against
`ScriptedUi` with a script such as `[Camp(Wait), Travel(east), Camp(Wait), Quit]` and
asserts: it returns, game time advanced, the player moved one tile, no prompt went
unanswered. This is the first time the whole action loop has been executable in a test;
keep it cheap (seeded world, few actions).

---

## Migration order

Each step should build; the big one (3) will not build until it is finished, so it is
done in one sweep. Commit after each step.

1. **Independent fixes:** `Pacing`, `TimedRun`, `Easing`; camera smoothing + overscan +
   scissor; save throttle. Old loops keep working on top of these.
2. **Scaffolding:** `IGameUi`, `Notice`, `PlayerAction`, `ProgressView`, `FrameScheduler`,
   `DesktopUi` with modal stack and `Frame()`, `ScriptedUi`. Not yet wired in.
3. **The sweep:** make the game-logic chain async from `GameRunner.RunAsync` down
   (`GameContext.Update`, `GameEventRegistry.HandleEvent/HandleOutcome`, `WorkRunner`,
   every `IWorkStrategy` that prompts, every handler that prompts, `TravelRunner`,
   `HuntRunner`, `CombatOrchestrator.RunWithPlayer`). Replace every `DesktopIO.*`,
   `BlockingDialog.*`, `Input.*`, `GameDisplay.Render`, `UpdateAndRenderProgress` call
   with the `ctx.Ui` equivalent. Move sim-internal UI calls to `Notices`. Wire `Program`.
4. **Delete** everything in *What gets deleted*. Nothing may remain "just in case".
5. **Tests and docs:** guard test, smoke test, fix `SerializationTests` for the async
   `Update`, update `documentation/overview.md` (Desktop UI section and the Update Flow
   diagram in Architecture) to describe this design, and delete the nested-loop
   description from it.

---

## Acceptance

- `dotnet build` has zero warnings introduced by this work; `dotnet test` passes,
  including the guard and smoke tests.
- `grep -rn "WindowShouldClose" --include=*.cs .` outside `bin/obj` hits only `Program.cs`.
- `grep -rn "BeginDrawing" --include=*.cs .` hits exactly one file.
- Travel: sprite and camera arrive together; long trips still take ≤1.2 s; a 2-minute
  trip does not finish simulating in two frames.
- Panning shows no blank strip on the incoming edge and no tiles under the side panels.
- HUD (stats, journal, toasts) is present in: map, travel, combat, every prompt, every
  screen, every progress bar.
- An event that fires during a progress bar draws on top of the bar; dismissing it
  resumes the bar.
- Toast timers run everywhere.
- No prompt anywhere returns a silent default when the window closes mid-prompt; the
  loop exits instead.
