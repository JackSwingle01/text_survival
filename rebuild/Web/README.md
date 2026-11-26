# rebuild/Web: the game in the browser

The first slice of `dev/active/ui-design.md` on the clean-room Sim: the map drawn from what the player
knows, the always-on layer, the tile's verbs, odds sheets, paced time, the fight, save and load, death
and the crossing. Sim runs in-browser as AOT WebAssembly in a Web Worker (the decision in
`dev/active/rebuild-roadmap.md`, measured in `rebuild/Web.Spike/REPORT.md`).

```
Host/        GameHost: NewGame, Act, View, Save, Load. Plain net10.0, no wasm. Views are DTOs (Dto.cs).
Host.Tests/  The host, headless (xUnit).
App/         The wasmbrowser app: a JSExport shim (Program.cs) and the page (wwwroot/).
tools/       publish.sh, serve.mjs (static server), shots.mjs (headless Chrome checks),
             terrain-preview.mjs (the map's ground to a PNG, no browser).
shots/       Screenshots from shots.mjs (not committed).
```

## The boundary

The page reaches the game only through `wwwroot/client.js`: `newGame(seed)`, `act(action)`, `view()`,
`save()`, `load(bytes)`, each a promise. Today they post to the worker; a server would replace that one
file with `fetch` calls to the same five operations.

- **Views are plain data**, rebuilt on every call from the player's side of the world: `Verbs.At`, the
  player's memory (explored tiles, remembered places, sightings), `Sight` for what is in view now,
  `Priorities.Of` for the vitals and alerts, the clock and sky, the fire on the tile, trip terms and odds.
  The map never reads the truth of a tile out of sight: the ground it carries is `Ground.Underfoot` in
  sight and the player's memory of it (`Memory.Explored`) out of sight.
- **The words are Sim's.** Names come from `Names` (`Sim/Content/names.json`), journal and receipt lines
  from `Narration`, odds factors and make outcomes are already in words, and a job's end is Sim's
  `StepOutcome` (finished, refused, interrupted) with its reason. The host adds only icons
  (`Host/Words.cs`); the CLI reads the same words.
- **Actions are verb ids.** Each view lists every verb under an id; the client posts one back
  (`{"verb":"12.3"}`) and the host runs the exact `Activity` behind it. An id from an older view is
  refused. The page never builds an activity or names a game verb.
- **Time is paced by the client.** A verb starts a job; `{"advance":N}` runs it up to N more game
  minutes and returns the view, so the clock is seen to move and a walk animates tile to tile.
  `{"stop":true}` ends it at that minute. Wait, rest, sleep, walking, making and work (foraging,
  butchering, cooking) run in those chunks: Sim continues each across steps, and holds work between them
  (`Sim.Step` with `more`), so a chunked search is one search with one luck. The page plays the minutes
  out at the old game's pace (`paceMs`). A verb with `durations` takes `{"verb":id,"minutes":N}`.
- **Saves** are Sim's versioned JSON as bytes: kept in IndexedDB (one slot), and downloadable and
  uploadable as a file.
- `Scenario(name)` is development only (`Host/Scenarios.cs`: `supplied`, `fight`, `dead`, `won`, `winter`), for
  screenshots and tests; the page uses it only for `?scenario=`.

## Observer mode

`observe.html?seed=N&npcs=K` is a development page (GLOSSARY: Observer mode): the same world with the
player taken out, the whole map as it truly is, run at a rate in game minutes per real second. Drag to
pan, wheel to zoom (from the whole valley to a few cells; below 24 px a cell the ground is a flat map,
above it terrain.js's painted chunks), Space plays, `.` steps one second's worth. The Spawn buttons arm
a kind (one row per kind in `Host/Observer.cs`'s `Spawnables`: an NPC today), then a click places it
(Shift-click for several). Clicking a tile lists what is there in `Verbs.NameOf`'s words, and who,
with their priorities or herd state; the log is world events in Sim's narration with their tile.

It talks to `Host/Observer.cs` through `client.js`'s `observe` calls, never to `GameHost`. Its map is the
player's `MapDto`, built by `Host/MapView.cs` from the other side: `MapView.Remembered` is a person's
memory, `MapView.Truth` every tile known and in sight, every print and worn route. The page hands it to
the same renderer (terrain.js, ground-state.js), so whatever the map gains, ravines and routes included,
the observer draws too. The page fetches `observe.map()` when the view's minute has passed
the map it holds, at most every 2 s; actors, places (fires, shelters, carcasses, piles) and the log
come with every view. The rate it achieves is shown beside the clock; Sim's own speed
is the ceiling (about 26-40 game minutes a second in the AOT build with a populated valley).

```bash
node --test tools/observe.test.mjs                               # camera, pacing, flat colours
node tools/shots.mjs /tmp/text-survival-web/wwwroot --observe    # play, zoom, spawn, inspect, a ravine; times the calls
```

## The map's ground

`wwwroot/terrain-core.js` draws the ground per pixel from known terrain and world pixel coordinates;
`wwwroot/terrain.js` bakes it a cell at a time in a pool of workers (`wwwroot/terrain-worker.js`),
nearest the camera first, and rebakes a cell only when the terrain within three cells or the ground
state within two changes (`REACH` in `terrain-core.js`), so a step rebakes only the cells it reveals
and their neighbours. A cell baked on its own is the same pixels as the whole map baked at once
(`tools/terrain.test.mjs`). Cells are 48 CSS px everywhere (one art pixel each: crisp at a device pixel ratio of 1 or 2).
Rivers are one channel through their cells, lakes have round shores, and mountains are lit ridged
height with a snowline. Caves are dark passages cut through the rock along their cells, out of the
mouth onto the ground beside it. Ravines and cliffs come from `MapDto.Edges` (those beside a known
tile): the boundary steps are chained and rounded into a cleft whose far wall catches the light, or
a one-sided face that drops toward the lower right (the Sim gives a cliff no high side), with a lip
and a shadow. A chain is rounded as a whole, so the page works out its pieces once per map
(`edgePieces`) and hands each cell's bake the pieces in its reach (`piecesNear`). Worn trails come from
`MapDto.Trails` (between known tiles: in sight as they are, out of sight as the player's memory last saw
them): a line from tile centre to tile centre by tier, a packed trench in snow and trodden earth out of
it; where most sides of the tiles around are worn (herds' grazing ground) the ground is trampled
instead, so it never draws the grid. Each cell's bake gets the trails in its reach (`trailsNear`). Prints
(`MapDto.Tracks`, within 3 tiles in sight) are pressed into the ground along the heading the Sim reads,
one line per animal up to three, never on water, rock or under a tree (`printPixels`); `map.js` lays
them over the baked cells. Ice, where snow and mud lie, and where every tree stands come from world
noise; snow and mud are painted 96 px textures shown through those masks. A pixel takes the ground
state of the cell whose terrain it shows, so state follows the same organic borders as the ground.
A pixel reads only its cell's 5 x 5 neighbourhood, and unknown cells read as the cell's own terrain,
so the ground never shows what the player has not seen. The renderer ports
`dev/prototypes/art/seasons/world.py` on `worktree-short-loop-plan`; `tools/terrain.test.mjs` holds
its ice to that reference pixel for pixel.

Tiles supply only texture, painted by `scripts/build-terrain-layers.py` into `assets/icons/terrain/`.
Each cell's snow, mud and ice come from `MapDto.Surface` (`wwwroot/ground-state.js`, which holds
every threshold in `GROUND`); the date sets only the palette and the mountain snowline (`SEASONS`).
The view carries the ground of every known tile, live in sight and as last seen out of sight, so the
page keeps no ground of its own and a save loads with its snow. Every layer and palette is painted
(`PAINTED` in `terrain-core.js`); asking for art that is not throws.

```bash
node --test tools/page.test.mjs                                                  # the page's key rules
node --test tools/terrain.test.mjs                                               # ground follows the Sim, art matches PAINTED
node tools/terrain-preview.mjs tools/fixtures/seed17.json /tmp/ground.png      # winter
node tools/terrain-preview.mjs tools/fixtures/seed17.json /tmp/thaw.png --state '{"dust":0,"patches":1,"snow":0.5,"laden":false,"mud":0,"water":0.4,"ice":"thin"}'
```

## Building

The app needs the **`wasm-tools` workload**. If the system SDK is root-owned (as on this machine),
install a private SDK and the workload once:

```bash
curl -sSL -o /tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
bash /tmp/dotnet-install.sh --version 10.0.400 --install-dir ~/.dotnet-wasm
export DOTNET_ROOT=~/.dotnet-wasm
$DOTNET_ROOT/dotnet workload install wasm-tools
```

Then, from `rebuild/Web`:

```bash
tools/publish.sh /tmp/text-survival-web                    # AOT (about 2 min), the shipping build
tools/publish.sh /tmp/text-survival-web -p:Aot=false       # interpreter (about 15 s), for iterating
node tools/serve.mjs /tmp/text-survival-web/wwwroot 8183   # then open http://127.0.0.1:8183/
```

`?seed=N` picks the world; `?scenario=fight` (or `supplied`, `dead`, `won`, `winter`, `trails`) starts in a prepared state.

The host and its tests need no workload: `dotnet test rebuild/Web/Host.Tests`.

**Building against a clean Sim.** While someone else has the working tree's Sim mid-change, build
against an extract of HEAD instead (keeps their `bin/obj` untouched):

```bash
mkdir -p /private/tmp/head && git archive HEAD Directory.Build.props global.json rebuild/Sim | tar -x -C /private/tmp/head
dotnet test rebuild/Web/Host.Tests -p:SimProject=/private/tmp/head/rebuild/Sim/Sim.csproj
tools/publish.sh /tmp/text-survival-web -p:SimProject=/private/tmp/head/rebuild/Sim/Sim.csproj
```

(Use the `/private/tmp` spelling on macOS: MSBuild loses the reference across the `/tmp` symlink.)

**CI** builds the solution, which does not include these projects, and is left alone: GitHub's
`setup-dotnet` could install `wasm-tools`, but publishing AOT adds minutes to every run. Adding
`Host` and `Host.Tests` to the solution would put the host tests in CI with no workload.

## Deployed

The Render static site `text-survival-rebuild` (https://text-survival-rebuild.onrender.com) builds
`main` on every push: its build command is `bash rebuild/Web/tools/render-build.sh`, which installs the
SDK and `wasm-tools` into the checkout and publishes AOT (about 4 min); it serves `web-out/wwwroot`.

## Checking it

`tools/shots.mjs` drives the published page in headless Chrome over the DevTools protocol, writes
screenshots to `shots/`, and exits non-zero on any console error, uncaught exception or failed load.

```bash
node tools/shots.mjs /tmp/text-survival-web/wwwroot --run          # walk, forage, light a fire, make a staff, save, load
node tools/shots.mjs /tmp/text-survival-web/wwwroot --shot fire    # one state; repeat --shot for more
node tools/shots.mjs /tmp/text-survival-web/wwwroot --all --phone  # every state, desktop and phones; fails if a phone layout scrolls
```

`--phone` shoots each state at 390 × 664, 402 × 714 (an iPhone 18 in Safari), 844 × 390 and 932 × 430 with touch on, and fails when the page
scrolls there, a verb or sheet button is out of reach, or an open drawer leaves less than a third of the
map; it also taps the clock, the map and a verb (`PHONE_LAYOUT`, `phoneTaps`). On Linux pass
`--chrome <path>` (as root, Chrome gets `--no-sandbox`).

States: `start here trip walk fire lit forage make made body pack log save receipt sleep fight
fight-round dead won winter trails remembered`. Look at the screenshots, not only the exit code.
