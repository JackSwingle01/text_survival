// What the map draws on each cell, from the ground the Sim reports (MapDto.Surface), and the world's
// colour from the date. Pure, so the page and the node tools read the same thresholds.
//
// The date sets colour only. Snow, mud and ice come from the Sim's ground and water, so a dry cold July
// day draws bare ground and a June blizzard draws snow.

/** Every threshold between the Sim's numbers and the art. */
export const GROUND = Object.freeze({
  dustCm: 0.5,          // less than this reads as bare ground; from here a dusting
  patchyCm: 5,          // from here patches spread, and the dusting lies only between them
  patchedCm: 10,        // patches lie everywhere, so no dusting; from here full cover spreads over them
  fullCm: 15,           // from here the ground is covered
  ladenCm: 5,           // trees and shrubs carry snow from this depth
  wetFullCm: 5,         // standing water this deep wets the whole cell
  wetMin: 0.1,          // mud or standing water below this does not show
  thickIceCm: 5,        // a frozen pond at least this deep is thick ice
  steps: 20,            // cover is drawn in twentieths, so small changes do not rebake the map
});

/** Lake and deep-water terrain: ice when the Sim says so (MapDto.FrozenWater), else open water. */
const WATER = new Set(['water', 'deepwater']);

/**
 * Palette, for the ground and the plants, and mountain snowline by season (ClockDto.Season). Spring has
 * no palette of its own: the melt shows last autumn's dead grass. Mountains carry no snow of their
 * own in the Sim, so their snowline follows the season.
 */
export const SEASONS = Object.freeze({
  winter: { palette: 'winter', snowline: 0.34 },
  spring: { palette: 'autumn', snowline: 0.62 },
  summer: { palette: 'summer', snowline: 0.8 },
  fall: { palette: 'autumn', snowline: 0.74 },
});

const ICE_RANK = { none: 0, thin: 1, thick: 2 };
const quantise = v => Math.round(Math.min(1, Math.max(0, v)) * GROUND.steps) / GROUND.steps;
const states = new Map();
function intern(patches, snow, dust, laden, mud, water, ice, trodden) {
  const key = `d${dust}p${patches.toFixed(2)}s${snow.toFixed(2)}${laden ? 'l' : ''}m${mud.toFixed(2)}w${water.toFixed(2)}${ice}t${trodden.toFixed(2)}`;
  let s = states.get(key);
  if (!s) states.set(key, s = Object.freeze({ patches, snow, dust, laden, mud, water, ice, trodden, key }));
  return s;
}

/**
 * A cell's drawn state from its SurfaceDto (null: in sight with nothing on it) and terrain name:
 * { dust: 0 | 1 (a dusting lies where patches do not), patches: 0..1 of the ground under patches,
 * snow: 0..1 under full cover, laden, mud: 0..1, water: 0..1 standing water's extent,
 * ice: 'none' | 'thin' | 'thick' (the standing water's, or the lake's), trodden: 0..1 packed by time spent on it, key }. Mud and standing water are apart, so a film of ice on soaked ground covers only as
 * much as the film does. frozen: the lake or river is ice; the payload carries no thickness for it, so it
 * is thick.
 */
export function cellState(surface, terrain, frozen = false) {
  if (frozen && !WATER.has(terrain)) throw new Error(`frozen water on ${terrain}`);
  const snowCm = surface?.snowCm ?? 0, waterCm = surface?.waterCm ?? 0, mudLevel = surface?.mudLevel ?? 0;
  const patches = quantise((snowCm - GROUND.patchyCm) / (GROUND.patchedCm - GROUND.patchyCm));
  const snow = quantise((snowCm - GROUND.patchedCm) / (GROUND.fullCm - GROUND.patchedCm));
  const dust = snowCm >= GROUND.dustCm && snowCm < GROUND.patchedCm ? 1 : 0;
  const shown = v => { const q = quantise(v); return q < GROUND.wetMin ? 0 : q; };
  const pond = surface?.ice ? (waterCm >= GROUND.thickIceCm ? 'thick' : 'thin') : 'none';
  const lake = frozen ? 'thick' : 'none';
  return intern(patches, snow, dust, snowCm >= GROUND.ladenCm, shown(mudLevel), shown(waterCm / GROUND.wetFullCm), ICE_RANK[pond] > ICE_RANK[lake] ? pond : lake,
    quantise(surface?.trodden ?? 0));
}

export const BARE = cellState(null, null);

/** The world state for a ClockDto: { palette, snowline, key }. */
export function worldState(clock) {
  const s = SEASONS[String(clock.season).toLowerCase()];
  if (!s) throw new Error(`no palette for season "${clock.season}"`);
  return { ...s, key: `${s.palette}@${s.snowline}` };
}

/**
 * Every cell's drawn state for one MapDto. Its Surface carries the ground of every known tile, and its
 * FrozenWater which lake and deep-water tiles are ice: in sight as they are now, out of sight as the player last
 * saw them (the Sim remembers, so the page keeps nothing between views). A known tile not listed is its terrain's bare ground; a tile never seen is
 * BARE, the unknown.
 * terrainOf(code) -> terrain name for a MapDto.Known code.
 */
export function createGround(terrainOf) {
  let keys = [], cells = [], width = 0, version = 0, seen = null;
  const bare = new Map();
  const terrain = code => {
    const t = terrainOf(code);
    if (!t) throw new Error(`terrain "${code}" is not known`);
    return t;
  };
  return {
    /** Takes in a MapDto's ground. */
    see(m) {
      if (m === seen) return;
      seen = m;
      if (!Array.isArray(m.frozenWater)) throw new Error('the view has no frozenWater');
      const n = m.width * m.height, surface = new Map(), frozen = new Set(m.frozenWater);
      for (const s of m.surface) {
        if (m.known[s.i] === '.') throw new Error(`surface for tile ${s.i}, which has never been seen`);
        surface.set(s.i, s);
      }
      for (const i of frozen) if (m.known[i] === '.' || !WATER.has(terrain(m.known[i]))) throw new Error(`frozen water on tile ${i}, which is not known water`);
      const next = new Array(n);
      let changed = n !== keys.length;
      for (let i = 0; i < n; i++) {
        const code = m.known[i], s = surface.get(i), ice = frozen.has(i);
        let state = BARE;
        if (s) state = cellState(s, terrain(code), ice);
        else if (code !== '.') {
          const k = ice ? `${code}*` : code;
          if (!bare.has(k)) bare.set(k, cellState(null, terrain(code), ice));
          state = bare.get(k);
        }
        next[i] = state;
        if (!changed && state.key !== keys[i]) changed = true;
      }
      cells = next; keys = next.map(c => c.key); width = m.width;
      if (changed) version++;
    },
    stateAt: (x, y) => cells[y * width + x] ?? BARE,
    keyAt: i => keys[i] ?? BARE.key,
    get version() { return version; },
  };
}
