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
  player's beliefs (explored tiles, remembered places, sightings), `Sight` for what is in view now,
  `Tiers.Of` for the vitals and alerts, the clock and sky, the fire on the tile, trip terms and odds.
  The map never reads the truth of a tile out of sight; the ground it carries (`Ground.SurfaceNow`) is
  the ground in sight now.
- **The words are Sim's.** Names come from `Names` (`Sim/Content/names.json`), journal and receipt lines
  from `Narration`, odds factors and make outcomes are already in words, and a job's end is Sim's
  `StepOutcome` (finished, refused, interrupted) with its reason. The host adds only icons
  (`Host/Words.cs`); the CLI reads the same words.
- **Actions are verb ids.** Each view lists every verb under an id; the client posts one back
  (`{"verb":"12.3"}`) and the host runs the exact `Activity` behind it. An id from an older view is
  refused. The page never builds an activity or names a game verb.
- **Time is paced by the client.** A verb starts a job; `{"advance":N}` runs it up to N more game
  minutes and returns the view, so the clock is seen to move and a walk animates tile to tile.
  `{"stop":true}` drops it. Wait, rest, sleep, walking and making run in those chunks (Sim continues
  each across steps; tests check a chunked run leaves the world byte-identical to one step). Other work
  (foraging, butchering, cooking) is one Sim step, which the page plays out on the clock.
- **Saves** are Sim's versioned JSON as bytes: kept in IndexedDB (one slot), and downloadable and
  uploadable as a file.
- `Scenario(name)` is development only (`Host/Scenarios.cs`: `supplied`, `fight`, `dead`, `won`), for
  screenshots and tests; the page uses it only for `?scenario=`.

## The map's ground

`wwwroot/terrain-core.js` draws the ground per pixel from known terrain and world pixel coordinates;
`wwwroot/terrain.js` bakes it into 8 x 8 cell chunks, a few milliseconds a frame, nearest the camera
first. Cells are 48 CSS px everywhere (one art pixel each: crisp at a device pixel ratio of 1 or 2).
Rivers are one channel through their cells, lakes have round shores, mountains are lit ridged
height with a snowline, and snow, mud, ice and every tree come from world noise, so nothing repeats
per tile. A pixel reads only its cell's 5 x 5 neighbourhood, and unknown cells read as the cell's own
terrain, so the ground never shows what the player has not seen.

Tiles supply only texture, painted by `scripts/build-terrain-layers.py` into `assets/icons/terrain/`.
Each cell's snow, mud and ice come from `MapDto.Surface` (`wwwroot/ground-state.js`, which holds
every threshold in `GROUND`); the date sets only the palette and the mountain snowline (`SEASONS`).
The view carries the ground in sight only, so the page keeps the ground as it last saw it; a tile not
in sight since the page loaded is drawn bare. Art not painted yet (`PAINTED` in `terrain-core.js`)
is drawn with the closest that is, and the console says so once per asset.

```bash
node --test tools/terrain.test.mjs                                               # ground follows the Sim, art matches PAINTED
node tools/terrain-preview.mjs tools/fixtures/seed17.json /tmp/ground.png      # winter
node tools/terrain-preview.mjs tools/fixtures/seed17.json /tmp/thaw.png --state '{"snow":0.5,"dust":false,"laden":false,"mud":0,"ice":"thin"}'
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

`?seed=N` picks the world; `?scenario=fight` (or `supplied`, `dead`, `won`) starts in a prepared state.

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

## Checking it

`tools/shots.mjs` drives the published page in headless Chrome over the DevTools protocol, writes
screenshots to `shots/`, and exits non-zero on any console error, uncaught exception or failed load.

```bash
node tools/shots.mjs /tmp/text-survival-web/wwwroot --run          # walk, forage, light a fire, make a staff, save, load
node tools/shots.mjs /tmp/text-survival-web/wwwroot --shot fire    # one state; repeat --shot for more
node tools/shots.mjs /tmp/text-survival-web/wwwroot --all --phone  # every state, desktop and 390 px
```

States: `start here trip walk fire lit forage make made body pack log save receipt sleep fight
fight-round dead won`. Look at the screenshots, not only the exit code.
