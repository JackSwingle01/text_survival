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
  art clearly at actual screen size. **Always render a preview and look at it**
  before considering an asset done — this format is authored blind, and text
  that "should" look right often doesn't.
- `render-all <sourceDir> <outputDir>` — render every `*.pxa` under `sourceDir` to a
  matching `*.png` under `outputDir`, preserving subfolder structure. This is
  the command that regenerates the real game assets:
  ```
  dotnet run --project tools/PixelArtCli -- render-all assets/pixelart assets/icons
  ```
- `sheet <sourceDir> <output.png> [--scale N] [--cols N]` — tile every `*.pxa` under
  `sourceDir` onto one upscaled contact sheet over a neutral grey ground:
  ```
  dotnet run --project tools/PixelArtCli -- sheet assets/pixelart /tmp/sheet.png --scale 10 --cols 3
  ```
  Reviewing the whole set at once is how style drift gets caught — assets that
  each look fine alone can still disagree on outline weight, palette, or scale.
  The grey ground also matters: transparent pixels render as black in most
  viewers, which hides exactly the dark outlines you need to judge.
- `validate <file.pxa>` — parse, execute, and run CHECKS without writing a PNG. Catches
  row-length mismatches, undefined palette keys, and failed CHECKS (see below)
  immediately — use this while iterating.

`render`/`render-all` run the same validation `validate` does and **refuse to
write a PNG if anything fails**, CHECKS included — a bad asset never silently
reaches the game.

## Why this format looks the way it does

Authoring pixel art through a CLI, with no live canvas, means every placement
is computed blind and only checked after the fact. Two failure modes came out
of the first pass at this: parts that were supposed to touch (a flame and the
logs under it) didn't, because both were placed using separately-guessed
absolute coordinates; and a full-body animal profile was unrecognizable,
because getting quadruped anatomy right from raw coordinates is genuinely
hard. The format below is built to make both mistakes structurally difficult:

- **Compose from named parts with relative placement**, not absolute
  coordinates for everything. `STACK ... BELOW ...` measures a part's *actual
  drawn pixels* and places the next part flush against them — the tool
  computes contact, you don't guess it.
- **Assert what should be true and let the tool refuse to render if it
  isn't.** `TOUCHES`/`CONNECTED` catch the exact class of bug described above
  before a PNG is ever written.
- **Draw with primitives, not raw pixel grids**, so symmetric shapes (most
  faces, most flames, most creatures) only need one half authored —
  `MIRRORX`/`MIRRORY` produce the other half exactly, instead of two
  independently-typed halves that can drift apart.

## The `.pxa` format

```
SIZE 16x16
PALETTE
. = #00000000
a = #2b2015
b = #6b4423

RECT 4 4 8 8 a
CIRCLE 8 8 2 b
MIRRORX

CHECKS
  CONNECTED
ENDCHECKS
```

- `SIZE WxH` — canvas size. Must come before any drawing.
- `PALETTE` — one `<single-char-key> = #RRGGBB` or `#RRGGBBAA` line per color.
  Every character used anywhere (grids, primitive commands) must be defined
  here — there is no implicit meaning for any character, including `.` (it's
  just a convention for "transparent", defined explicitly as `#00000000`).
  Blank lines and lines starting with `//` are ignored everywhere.

### Drawing primitives

Usable at the top level (drawing directly on the final image) or inside a
`PART` block (drawing on that part's own local canvas):

| Command | Effect |
|---|---|
| `PIXEL x y color` | Set one pixel. |
| `RECT x y w h color` | Filled rectangle. `color` can be `.` (transparent) to erase. |
| `LINE x0 y0 x1 y1 color` | One-pixel-wide line. |
| `CIRCLE cx cy r color` | Filled circle. |
| `FILL x y color` | Flood fill the contiguous same-color region touching (x,y). |
| `MIRRORX` | Copy the left half onto the right half (reflects around the vertical centerline). Draw one side, get both. |
| `MIRRORY` | Same, top half onto bottom half. |

Commands execute in file order — a later command drawn over an earlier one
wins for any pixel they share, same as drawing layers.

### `PIXELS` — literal grid (for fine detail a primitive can't express)

```
PIXELS
................
.....aaaaaaa....
....aabbbbaab...
................
```

Exactly `HEIGHT` rows of exactly `WIDTH` characters, top row first. Valid at
the top level or inside a `PART` (there called `GRID`, same syntax, sized to
the part). Mixable with primitives — draw broad shapes with primitives, then
drop a `PIXELS`/`GRID` block for a few precise pixels (an eye, a highlight).

### `PART` / `COMPOSE` — build from named pieces, place them by relationship

```
PART flame 8x7
  PIXEL 3 0 y
  RECT 2 1 2 2 o
  MIRRORX
ENDPART

PART logs 10x3
  RECT 0 0 10 1 h
  RECT 1 1 8 2 g
ENDPART

COMPOSE
  PLACE flame CENTERED
  STACK logs BELOW flame CENTERED OVERLAP 1
ENDCOMPOSE
```

- `PART <name> <W>x<H> ... ENDPART` — draws into its own local `W`x`H` canvas
  (same primitives/`GRID` as above, its own coordinate space starting at 0,0).
  Not written to the output until placed in `COMPOSE`.
- `PLACE <part> AT x y` — stamp a part at an absolute position on the output canvas (top-left of its local canvas).
- `PLACE <part> CENTERED` — center a part's *drawn content* (not its declared
  canvas size — an off-center shape inside a padded canvas still centers correctly)
  on the output canvas.
- `STACK <part> BELOW|ABOVE|LEFTOF|RIGHTOF <otherPart> [CENTERED | DX n | DY n] [OVERLAP n]`
  — place a part relative to an *already-placed* part, computed from each
  part's actual bounding box of drawn pixels:
  - Direction controls the touching edge (e.g. `BELOW` puts this part's top
    edge one pixel past the other part's bottom edge).
  - `CENTERED` (default) aligns centers on the cross-axis; `DX n`/`DY n`
    instead left/top-aligns the two parts' bounding boxes, then shifts by `n`.
  - `OVERLAP n` overlaps the touching edges by `n` pixels instead of leaving
    them exactly flush — useful when "touching" still reads as a visible seam
    and you want the two parts to visually fuse.

### `CHECKS` — assert something and refuse to render if it's false

```
CHECKS
  TOUCHES flame logs
  CONNECTED
  CONNECTED logs
ENDCHECKS
```

- `TOUCHES <partA> <partB>` — fails unless the two (already-placed) parts have
  at least one pair of 8-adjacent non-transparent pixels after composition.
- `CONNECTED` — fails unless every non-transparent pixel in the *final image*
  forms one 8-connected blob (catches stray, disconnected pixels — a common
  symptom of a coordinate typo).
- `CONNECTED <part>` — same check, scoped to one part's own local canvas
  (useful even without `COMPOSE`, to sanity-check a single-part icon).

Put `CHECKS` after the drawing/`COMPOSE` it's asserting about — checks run in
the order written, against whatever has executed so far.

## Style guidance

**16x16 cannot resolve full anatomy — don't attempt it.** A side-on
four-legged animal profile at this resolution reads as a gray blob; there
just aren't enough pixels to place a head, spine, legs, and tail
recognizably. Instead, **draw the single most recognizable feature, large,
facing the viewer**: a wolf is a front-facing head (ears, eyes, muzzle, nose)
— not a full body in profile. Apply the same instinct broadly: an icon is one
bold, legible idea, not a small painting.

**Exploit symmetry.** Most recognizable subjects (faces, flames, most
creatures viewed head-on) are bilaterally symmetric — author the left half
only (8 columns of content, the rest `.`) and `MIRRORX` it. This is the
biggest lever against authoring "blind": half the coordinates to get right,
and no way for the two sides to drift out of alignment.

**Outline, shade, and taper — or it reads as stacked boxes.** The three
things that separate pixel art from flat blocks, in order of impact:

1. *A dark outline around the silhouette.* Not pure black — a very dark
   version of the subject's own hue. Without it a sprite has no contour and
   dissolves against the background. This matters most on icons drawn over
   varied terrain.
2. *Two or three tones per material*, lit consistently (top-left by
   convention): a base, a shadow at the edges/underside, a highlight on the
   lit side. One flat tone per material is the single clearest tell of
   amateur work.
3. *Stepped, tapering contours.* Curves are implied by stepping in one pixel
   at a time (`..o`, `.o`, `o`), never by an axis-aligned rectangle edge.
   A head narrows toward the muzzle; a bundle rounds at the shoulders.

Because of #3, prefer an explicit `PIXELS`/`GRID` block over `RECT`
composition for anything organic — `RECT` produces boxes by construction, so
`RECT`-built creatures come out looking like robots. Keep the primitives for
what they're good at: `MIRRORX`, flat man-made forms, and erasing with `.`.

**`CIRCLE` is unreliable below r≈4.** At small radii it rasterizes to a
sparse diamond, not a curve — a bear's ears drawn as `CIRCLE ... 2` came out
looking like antlers. Hand-place small round forms in a grid instead.

**Fire, light, and glowing things take no outline.** They read by heat
gradient (deep red → orange → yellow → white core), not by contour. Outlining
a flame makes it look like a painted cutout.

**Compose, don't eyeball.** Whenever an asset has two or more parts that
must visually connect (flame + logs, head + ears, body + limbs), build them
as separate `PART`s and use `STACK` to place them — never compute two sets
of absolute coordinates by hand and hope they meet. Back it with a `TOUCHES`
or `CONNECTED` check so a regression fails loudly instead of shipping a gap.

**Always render and look before calling it done.** `render --preview` +
viewing the PNG (e.g. via Claude's Read tool) is the only feedback loop this
format has. One-shot authoring without looking is how both of the mistakes
above happened.

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
