# Game testing CLI

A persistent, windowless player interface to the real `GameRunner`. It emits JSON Lines;
there is no ASCII map, terminal redraw, graphics context, or automated choice selection.

## Non-blocking agent mode

Build once, then use the one-shot wrapper from the repository root:

```sh
dotnet build tools/GameCli
./scripts/gamecli start --seed 42 --session cli_experiment
./scripts/gamecli peek cli_experiment
./scripts/gamecli send cli_experiment "act Inventory" --request inventory-1
./scripts/gamecli poll cli_experiment inventory-1
./scripts/gamecli send cli_experiment "click Inventory/ItemGrid/res_Stick"
./scripts/gamecli peek cli_experiment
./scripts/gamecli stop cli_experiment
```

**No terminal session needs to stay open.** Each client invocation prints one JSON response
and exits. `start` waits only for a short process-detachment handshake (at most three
seconds), not world generation. `send` and `stop` immediately return a request ID; they do
not wait for simulation or player input. `poll` reads a result without waiting. The worker
retains the actual async game stack, including open menus and unanswered events.

| Agent command | Behavior |
|---|---|
| `start [--seed N] [--session cli_NAME] [--load cli_NAME]` | Launch a detached worker; receipt includes session, run ID, and PID |
| `send SESSION "COMMAND" [--value TEXT] [--request ID] [--revision N]` | Queue any gameplay or observation command and return a receipt |
| `poll SESSION REQUEST_ID [RUN_ID]` | Report `queued`, `running`, `completed`, `failed`, or `interrupted`; never wait |
| `peek SESSION` | Read worker status and the most recent snapshot without queueing a game command |
| `stop SESSION` | Queue `quit` with request ID `stop`; poll it to confirm shutdown |

For example, a receipt looks like:

```json
{"ok":true,"result":{"session":"cli_experiment","run":"...","requestId":"inventory-1","status":"queued"}}
```

A completed `poll` contains `result.response`, whose `ok` and `result`/`error` fields hold
the normal game response. A request can complete with `response.ok:false` for invalid
input. Pending decisions appear as the resulting game's `screen` (`event`, `number`,
`inventory`, etc.); they do not leave the transport request running. Submit another
command to answer that decision. The worker automatically pumps long activities to the
next decision, so background callers do not need to send `step`.

`peek` reports `starting`, `ready`, `running`, `stopped`, `failed`, or `interrupted`.
Its `state.snapshot` is the last published `look` result. Check
`state.snapshotCurrent`: during work or after a crash it is false, so an old snapshot
must not be mistaken for a completed action. The initial snapshot is absent until world
generation finishes. `state.revision` advances for every completed request, including
read-only commands and rejected input; `peek`, `poll`, and duplicate submissions do not
advance it.

Wait for a request result before choosing the next action. Deliberate command batches
are processed in submission order, one at a time. `stop` also follows that order and
never saves implicitly. While work is running, clients can freely poll or peek; they
never access the live simulation from another thread.

For retry-safe calls, supply your own `--request` ID. Reusing it with exactly the same
command returns its existing status/result, and reusing it for different input is an
error. Add `--revision N` from the latest snapshot/result to reject decisions made against
an older state. Both protections apply within one worker run. Use the optional run ID
when polling to detect a restarted session.

The local transport lives under `.gamecli/` (gitignored), with a separate directory for
each session and worker run. No network listener or additional dependency is needed.
`GAMECLI_STATE_DIR` can override the transport root. Worker logs and request/result files
remain available after shutdown; `peek` includes the log path. Once stopped, a session's
transport directory can be deleted. The game save is separate and still uses the normal
isolated CLI save path.

A crashed worker is reported as interrupted; pending commands are never silently replayed.
If it died during a command without publishing a result, `poll` marks `outcomeUnknown`:
that command may already have changed the game. Inspect the last snapshot/log and restart
from an explicit save rather than assuming the action did not happen. Start a stopped
session with `--load cli_NAME` to resume its save, or start a new run if no save exists.
A new run gets a new run ID and does not consume the previous queue.

## Interactive mode

With no arguments, the CLI prints help and exits. The original stdin/stdout mode is
now explicitly available through `interactive` (legacy startup flags also still work):

From the repository root:

```sh
dotnet build tools/GameCli
./scripts/gamecli interactive --seed 42 --session cli_interactive
```

The initial line is the current decision. Send one command per line; every command produces
one JSON response. Keep the process alive between tool calls to retain menus, activities,
and pending events. Plain input pipes work; a PTY is optional. Send EOF or `quit` to stop.
Game diagnostics go to stderr, so stdout can be parsed directly as JSON.

```text
look
map 3
inspect 48,50 49,50 48,49
actions
act Inventory
click Inventory/ItemGrid/res_Stick
click Drop 1
close
act Crafting
{"command":"set Crafting/search","value":"knife"}
close
travel 49,50
```

Coordinates above are examples. Read your own location and available controls first.
`click` accepts a full returned control ID or an exact, unambiguous label. Controls are
scoped to the current screen. A screen may show more controls after selecting an item,
recipe family, maintenance target, or category. `set` edits a text field; `click OK`
commits a numeric prompt. Checkboxes toggle with `click`.

## Commands

| Command | Result |
|---|---|
| `look` | Current screen, controls, short survival summary, progress, recent log |
| `status` | Complete survivor panel, including expanded condition, temperature, and clothing details |
| `weather` | Weather, front, wind, precipitation, temperature breakdown |
| `log [count]` | Last 30 entries by default, including severity and timestamps |
| `map [radius\|all]` | Coordinate rows with a columns schema; radius defaults to 3 |
| `inspect x,y [x,y ...]` | Multiple location inspectors, travel options, boundaries, trails, people, animal markers |
| `actions` | Map action IDs and disabled reasons; `availableNow` accounts for pending decisions |
| `act ID` | Perform a listed map action, including work and screen navigation |
| `travel x,y [quick\|careful]` | Use the real travel runner; omitted pace prompts when needed |
| `click ID_OR_LABEL` | Activate one currently exposed control |
| `choose ID_OR_LABEL` | Alias for `click` |
| `{"command":"set ID","value":"text"}` | Set a text/search/number field |
| `close` | Cancel a cancellable screen; mandatory decisions remain pending |
| `combat` | Tactical grid dimensions, unit coordinate rows, movement allowance, thrown weapons |
| `unit ID [ID ...]` | Inspect one or more units using the actual combat panel |
| `move x,y` | Combat movement; the game resolves movement range and occupancy |
| `save` | Save this CLI session at a map decision |
| `new` | The real new-game confirmation and restart |
| `step` | Continue bounded simulation if the screen reports `busy` |
| `quit` | End the CLI process without saving |

All commands also accept `{"command":"..."}`. Errors return `ok:false`; invalid input
leaves the pending decision available. Unexpected game exceptions are fatal, print a stack
trace to stderr, and exit with a nonzero code. Commands are sequential, not transactional.
Never blindly continue a scripted route through unexpected event prompts.

`ok:true` means the command was handled, not that the character successfully performed
work. Check the returned screen, progress, and `log` for gameplay outcomes and unmet
requirements (for example, chopping without an axe). Both snapshot `log` and `log [count]`
return entries shaped like `{"text":"You need an axe to fell trees.","level":"Warning","timestamp":"9:00"}`.
Unsuccessful megafauna searches report their outcome here; a search without a discovery
does not add a discovery-log entry. Injury bars report **percent damage** (100% means
destroyed). Resource storage buttons transfer the entire displayed quantity in one click.

Map rows omit unexplored tiles. Coordinates are zero-based, with north at `y-1`. Explored
terrain remains inspectable; current features, tracks, people, and animal markers follow
player visibility. Concealed cave interiors remain mountain. `map all` lists known tiles
across the world; it does not reveal the map. Combat uses a separate coordinate system.

## Screen coverage

The CLI runs the **same screen code** as the desktop through `GameGui`, a small rendering
adapter. In desktop mode, its calls forward to ImGui. In a text frame, they record text,
progress values, tooltips, and controls; activating a control runs its existing callback.
This is why inventory drops, NPC requests, crafting prerequisites, and food warnings stay
consistent with the desktop instead of acquiring a separate implementation.

| Player interface | CLI coverage |
|---|---|
| Survivor HUD | Stats, trends, injuries, organs, effects, tensions, warnings, clothing, temperature details |
| World map | Terrain, fog, camp, feature icons, visible NPC actions and animal markers, edge types and trail tiers |
| Location inspector | Ground conditions, fire, shelter, resources, tracks, storage, people, work, travel restrictions |
| Inventory | Categories, item selection, descriptions, nutrition, gear stats, condition, equip/unequip/drop/remove |
| Crafting | Search, sections/families, make/improve/maintain, filters, targets, variants, prerequisites, comparison, linked plans, commit |
| Fire | Tools, tinder, ember carriers, ignition odds, start/relight, fuel, charcoal, torches, ember collection |
| Food and water | Food selection, warnings, nutrition, eat, drink, wash, cook meat/fish, melt snow |
| Storage | Player/storage lists and all existing item transfer controls |
| Companions | Needs, condition, memories, relationships, follow/leave, supplies, inventory |
| Discoveries | All categories and known/unknown counts |
| Foraging | Environmental clues (including clickable clues), focus, duration, search/cancel/keep walking |
| Butchering | Modes, time estimates, condition, yield, cutting-tool restrictions, warnings |
| Events | Description, available/disabled choices, costs, outcomes, effects, damage, gains/losses, tensions |
| Combat | Coordinates, all unit hover details, zone-dependent actions, movement, help/retreat/flee |
| Other prompts | Selection, confirmation, numeric input/cancel, notices, work results, death/victory/restart |
| Progress | Active activity, minutes, sections/finds/materials, completed progress, explicit continuation |

Trees, tabs, combo choices, and tooltips are expanded in text; selecting an item still
controls its detail panel. Scrollbars, camera pan/zoom/follow, music mute, fonts, animation,
and decorative artwork are desktop presentation controls and have no CLI equivalent.
The tool tests gameplay and screen behavior, not visual layout or mouse hit testing.

## Saves and repeatability

Session IDs must start with `cli_` and contain only ASCII letters, digits, `_`, or `-`.
Without `--session`, a unique ID is generated. Saves use `saves/save_cli_NAME.json`, never
the normal desktop `save.json`. Existing IDs are rejected on a new run.

```sh
./scripts/gamecli start --load cli_experiment
```

Explicit `save` is restricted to map decisions because the persistence format does not
store suspended async call stacks. The real runner also autosaves at its normal interval
at the top of the action loop, using the isolated session ID. `quit`/EOF do not create a
final save. Death, victory, and confirmed new game delete only this session's save.

`--seed` seeds new-game generation. Keep commands and responses as a transcript for
reproduction. Exact replay is not promised across code changes or save/load: the existing
save format does not persist RNG state, and some UI IDs incorporate runtime item identity.
Use IDs returned by the current process, not hardcoded IDs for generated gear.

## Implementation plan and validation

1. Route the shared screen primitives through a native-or-text adapter.
2. Implement every `IGameUi` method, retaining actual overlays and game handlers.
3. Pump the existing frame scheduler until a decision, without wall-clock animation waits.
4. Add compact, visibility-aware world observations and batch tile/unit inspection.
5. Provide a persistent JSON-lines process with isolated saves and recoverable input errors.
6. Exercise real loop travel/rest, screen actions, prompt validation, events, combat,
   visibility boundaries, and save behavior; run the existing regression suite.

Tests live in `text_survival.Tests/Cli/`, including detached worker startup, FIFO
execution, request deduplication, revision rejection, failure detection, and shutdown:

```sh
dotnet test text_survival.Tests --filter FullyQualifiedName~text_survival.Tests.Cli
```

No new NuGet dependencies are required. When building offline with cached packages,
`-p:NuGetAudit=false` avoids a network-only vulnerability metadata lookup; it is a local
build option, not a repository policy change.
