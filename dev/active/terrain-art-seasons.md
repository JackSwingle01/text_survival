# Terrain art for the seasons

*Status: the renderer and the art for every state are done; the Sim gaps below remain. The map
should show the ground as it is: bare in July, snow arriving in autumn, mud in a thaw, ice on the
ponds. The simulation already tracks every tile's snow depth, wetness and mud, and ice on water
(`rebuild/Sim/Ground.cs`). The UI draws from that ground state, not from the calendar.*

## Done: the world-space renderer and its art

The tile-per-cell approach below was replaced. `rebuild/Web/App/wwwroot/terrain-core.js` draws the
ground per pixel in world space, and tiles supply only texture (`scripts/build-terrain-layers.py`
-> `assets/icons/terrain/`): ground per terrain and palette (winter, summer, autumn), mud per
palette, snow at three levels (dust, patchy, full) in three flavours (flat; rugged for hills; crag
for rock, which keeps bare faces), and sprites (spruce, shrub, boulder, reeds). Mountains, rivers,
lake shores, thin and thick ice (on lakes, rivers and frozen wet ground), the masks that spread
snow and mud cover, and sprite placement are computed in world space: a lake spans many textures,
and any period in its ice would show. The renderer is a port of the approved prototype,
`dev/prototypes/art/seasons/world.py` on `worktree-short-loop-plan`. `rebuild/Web/README.md` has
the details and the preview tool.

The renderer takes the ground state as input, and every state below already draws (try them with
`tools/terrain-preview.mjs --state`):

- per cell: `{ dust: 0 | 1, patches: 0..1 of the ground under patches, snow: 0..1 under full
  cover, laden: bool, mud: 0..1, water: 0..1 standing water's extent, ice: 'none' | 'thin' |
  'thick' }` (pond ice covers the standing water only, not the mud);
- per world: `{ palette, snowline }` (mountain height above which snow lies, 0..1).

The page derives both from the view (`wwwroot/ground-state.js`): cells from `MapDto.Surface`, the
world from `ClockDto.Season`. Asking for art that is not painted throws (`PAINTED`, `resolveArt`
in `terrain-core.js`); `tools/terrain.test.mjs` fails when `PAINTED` and the files disagree.

## To wire next

1. **Sim gaps.** `SurfaceDto` has an ice flag, not a thickness. Deep water is impassable and
   skipped, and lake and river ice is static (`SiteWater`) until it grows and thaws.
2. **Crust art.** `SurfaceDto.Crust` (snow whose top has refrozen) reaches the page, but nothing
   draws it: it needs an overlay, crust over full and patchy snow.

## Today

Ten terrains have eight variants each in `assets/pixelart/` (rendered to `assets/icons/`), and every
one is snow-covered:

forest, clearing, plain, hills, marsh, rock, water, deep water, mountain, cave floor.

The game starts on 1 July, so day 1 looks like January.

## Approach: bare base tiles plus overlays

Draw bare base tiles and a few overlays layered on top, rather than every combination as its own
tile. States then combine for free: patchy snow over mud in a spring thaw, a dusting on a frozen
pond.

### New base tiles (bare ground)

| Terrain | Needs |
|---|---|
| Forest, clearing, plain, hills, rock | One bare version each: summer ground, grass, needles, stone |
| Marsh | Bare, meaning wet: standing water among the tussocks |
| Water, deep water | Open water. The current tiles become the frozen version |
| Mountain | Nothing new; peaks stay snowy all year |
| Cave floor | Nothing new; caves don't change |

That's 7 new base sets. Four variants each is plenty, since the overlays add variety.

### Overlays, shared across terrains

| Overlay | When it's drawn |
|---|---|
| Snow: dusting | A few centimetres |
| Snow: patchy | Melting, or a thin cover |
| Snow: full cover | The current look, as a layer |
| Forest snow | Snow held on the canopy. Trees hold snow differently from open ground, so forest may need its own |
| Mud or wet | Thawing, soaked ground |
| Ice on water | Thin ice, then thick ice, over the open-water tiles |
| Snow crust | Snow whose top has refrozen hard (`SurfaceDto.Crust`) |

### Seasonal colour by palette swap

A `.pxa` file is an ASCII grid plus a colour palette (`tools/PixelArtCli/README.md`). A summer and an
autumn version of the same bare tile can share one grid and swap only the palette: greens for
summer, browns and golds for autumn. That gives seasonal colour without new drawing.

## Order

1. **Minimum, so July stops looking like January:** the 7 bare base sets plus the full-snow overlay.
2. Dusting and patchy snow, and ice on water (thin, then thick).
3. Mud or wet, and forest canopy snow.
4. Autumn palettes.

## What the UI needs from the simulation

Per known tile, from the player's own observation of it:
- snow depth and crust;
- wet or mud;
- ice thickness on water.

The Sim-gaps work is adding this to the host's map payload. The page then picks the base tile from
the terrain, adds the overlays from those values with thresholds set in the UI, and the palette from
the season.
