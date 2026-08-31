# pixelart

A CLI for authoring pixel-art game textures/icons as plain text, and rendering
them to PNGs the game loads directly. Replaces ad hoc SVG/photo-style assets
with a consistent, hand-authorable pixel-art pipeline.

Standalone console project (net10.0, zero NuGet dependencies — it writes PNG
bytes itself, no image library). It is **not** part of `text_survival.sln` and
is not built by `dotnet build` at the repo root; build/run it directly:

```
dotnet run --project tools/PixelArtCli -- <command> ...
```

## Commands

- `new <file.pxa> [--width N] [--height N]` — scaffold a blank template (default 16x16).
- `render <file.pxa> <output.png> [--preview <path>] [--preview-scale N]` — render one
  file to a native-resolution PNG. `--preview` also writes a nearest-neighbor
  upscaled copy (default 16x) for visual review — never load the preview file
  in-game, it exists purely so a human (or Claude, via the Read tool) can see the
  art clearly at actual screen size.
- `render-all <sourceDir> <outputDir>` — render every `*.pxa` under `sourceDir` to a
  matching `*.png` under `outputDir`, preserving subfolder structure. This is
  the command that regenerates the real game assets:
  ```
  dotnet run --project tools/PixelArtCli -- render-all assets/pixelart assets/icons
  ```
- `validate <file.pxa>` — parse and report errors without writing anything. Catches
  row-length mismatches and undefined palette keys immediately — use this while
  iterating on a grid by hand.

## The `.pxa` format

```
SIZE 16x16
PALETTE
. = #00000000
a = #2b2015
b = #6b4423
PIXELS
................
.....aaaaaaa....
....aabbbbaab...
................
```

- `SIZE WxH` — canvas size (defaults to 16x16 if omitted).
- `PALETTE` — one `<single-char-key> = #RRGGBB` or `#RRGGBBAA` line per color.
  Every character used in `PIXELS` must be defined here — there is no implicit
  meaning for any character, including `.` (it's just a convention for
  "transparent", defined explicitly as `#00000000`).
- `PIXELS` — exactly `HEIGHT` rows of exactly `WIDTH` characters, top row first.
- Blank lines and lines starting with `//` are ignored outside `PIXELS`.

## Where assets live

- Source of truth: `assets/pixelart/**/*.pxa` (checked into git, human/AI-editable).
- Generated output: `assets/icons/**/*.png` (loaded by the game at runtime; do not
  hand-edit these PNGs — regenerate them from `.pxa` sources instead).

## Adding a new asset

Rendering conventions the game's texture loaders already expect:

- **Terrain tile**: `assets/pixelart/<terrain>_tile.pxa` → `assets/icons/<terrain>_tile.png`,
  where `<terrain>` matches `Location.Terrain.ToString().ToLowerInvariant()`
  (`Desktop/Rendering/TileRenderer.cs`).
- **Feature icon**: `assets/pixelart/<icon>.pxa` → `assets/icons/<icon>.png`, where
  `<icon>` matches the feature's `MapIcon` string (e.g. `HeatSourceFeature.MapIcon`
  in `Environments/Features/*.cs`). Falls back to `ProceduralIconRenderer` if absent.
- **Animal**: `assets/pixelart/animals/<type>.pxa` → `assets/icons/animals/<type>.png`,
  where `<type>` matches an `AnimalType` enum value, case-insensitive
  (`Actors/Animals/AnimalType.cs`). Falls back to procedural drawing in
  `AnimalRenderer` if absent.
- **Player**: `assets/pixelart/player.pxa` → `assets/icons/player.png`.

Any category without a `.pxa`/`.png` pair keeps using its existing procedural
Raylib drawing — the fallback pattern is already wired up everywhere, so adding
art is just dropping in a file and running `render-all`.

All loaded textures are set to `TextureFilter.Point` (nearest-neighbor) so they
stay crisp — never blurred — when the game scales them up to tile/icon size.
