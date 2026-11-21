// What the map draws on each cell, from the ground the Sim reports (MapDto.Surface), and the world's
// colour from the date. Pure, so the page and the node tools read the same thresholds.
//
// The date sets colour only. Snow, mud and ice come from the ground itself, so a dry cold July day
// draws bare ground and a June blizzard draws snow.

/** Every threshold between the Sim's numbers and the art. */
export const GROUND = Object.freeze({
  dustCm: 0.5,          // less than this reads as bare ground
  patchyCm: 5,          // up to here a dusting; from here snow lies in patches
  fullCm: 15,           // from here the ground is covered
  patchyCover: 0.25,    // cover where patches begin, rising to 1 at fullCm
  ladenCm: 5,           // trees and shrubs carry snow from this depth
  wetFullCm: 5,         // standing water this deep wets the whole cell
  wetMin: 0.1,          // mud or standing water below this does not show
  thickIceCm: 5,        // a frozen pond at least this deep is thick ice
  steps: 20,            // cover is drawn in twentieths, so small changes do not rebake the map
});

/**
 * Terrain the Sim keeps frozen whatever the weather: lakes and rivers are ice it does not grow or
 * thaw, and the payload carries no thickness for them.
 */
export const FROZEN_TERRAIN = Object.freeze({ water: 'thick', deepwater: 'thick' });

/** Palette and mountain snowline by season (ClockDto.Season). A spring thaw shows last autumn's dead grass. */
export const SEASONS = Object.freeze({
  winter: { palette: 'winter', snowline: 0.34 },
  spring: { palette: 'autumn', snowline: 0.55 },
  summer: { palette: 'summer', snowline: 0.8 },
  fall: { palette: 'autumn', snowline: 0.55 },
});

const ICE_RANK = { none: 0, thin: 1, thick: 2 };
const quantise = v => Math.round(Math.min(1, Math.max(0, v)) * GROUND.steps) / GROUND.steps;
const states = new Map();
function intern(snow, dust, laden, mud, water, ice) {
  const key = `${snow.toFixed(2)}${dust ? 'd' : ''}${laden ? 'l' : ''}m${mud.toFixed(2)}w${water.toFixed(2)}${ice}`;
  let s = states.get(key);
  if (!s) states.set(key, s = Object.freeze({ snow, dust, laden, mud, water, ice, key }));
  return s;
}

/**
 * A cell's drawn state from its SurfaceDto (null: in sight with nothing on it) and terrain name:
 * { snow: 0..1 cover, dust, laden, mud: 0..1, water: 0..1 standing water's extent, ice: 'none' | 'thin' | 'thick'
 * (the standing water's, or the lake's), key }. Mud and standing water are apart, so a film of ice
 * on soaked ground covers only as much as the film does.
 */
export function cellState(surface, terrain) {
  const snowCm = surface?.snowCm ?? 0, waterCm = surface?.waterCm ?? 0, mudLevel = surface?.mudLevel ?? 0;
  const patch = (snowCm - GROUND.patchyCm) / (GROUND.fullCm - GROUND.patchyCm);
  const snow = snowCm < GROUND.patchyCm ? 0 : quantise(GROUND.patchyCover + (1 - GROUND.patchyCover) * patch);
  const dust = snowCm >= GROUND.dustCm && snow < 1;
  const shown = v => { const q = quantise(v); return q < GROUND.wetMin ? 0 : q; };
  const pond = surface?.ice ? (waterCm >= GROUND.thickIceCm ? 'thick' : 'thin') : 'none';
  const lake = FROZEN_TERRAIN[terrain] ?? 'none';
  return intern(snow, dust, snowCm >= GROUND.ladenCm, shown(mudLevel), shown(waterCm / GROUND.wetFullCm), ICE_RANK[pond] > ICE_RANK[lake] ? pond : lake);
}

export const BARE = cellState(null, null);

/** The world state for a ClockDto: { palette, snowline, key }. */
export function worldState(clock) {
  const s = SEASONS[String(clock.season).toLowerCase()];
  if (!s) throw new Error(`no palette for season "${clock.season}"`);
  return { ...s, key: `${s.palette}@${s.snowline}` };
}

/**
 * Every cell's drawn state for one MapDto. Its Surface carries the ground of every known tile: in
 * sight as it is now, out of sight as the player last saw it (the Sim remembers, so the page keeps
 * nothing between views). A known tile not listed is its terrain's bare ground; a tile never seen is
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
      const n = m.width * m.height, surface = new Map();
      for (const s of m.surface) {
        if (m.known[s.i] === '.') throw new Error(`surface for tile ${s.i}, which has never been seen`);
        surface.set(s.i, s);
      }
      const next = new Array(n);
      let changed = n !== keys.length;
      for (let i = 0; i < n; i++) {
        const code = m.known[i], s = surface.get(i);
        let state = BARE;
        if (s) state = cellState(s, terrain(code));
        else if (code !== '.') {
          if (!bare.has(code)) bare.set(code, cellState(null, terrain(code)));
          state = bare.get(code);
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
