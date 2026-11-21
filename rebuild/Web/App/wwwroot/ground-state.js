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
  mudMin: 0.1,          // wetness below this does not show
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
function intern(snow, dust, laden, mud, ice) {
  const key = `${snow.toFixed(2)}${dust ? 'd' : ''}${laden ? 'l' : ''}m${mud.toFixed(2)}${ice}`;
  let s = states.get(key);
  if (!s) states.set(key, s = Object.freeze({ snow, dust, laden, mud, ice, key }));
  return s;
}

/**
 * A cell's drawn state from its SurfaceDto (null: in sight with nothing on it) and terrain name:
 * { snow: 0..1 cover, dust, laden, mud: 0..1, ice: 'none' | 'thin' | 'thick', key }.
 */
export function cellState(surface, terrain) {
  const snowCm = surface?.snowCm ?? 0, waterCm = surface?.waterCm ?? 0, mudLevel = surface?.mudLevel ?? 0;
  const patch = (snowCm - GROUND.patchyCm) / (GROUND.fullCm - GROUND.patchyCm);
  const snow = snowCm < GROUND.patchyCm ? 0 : quantise(GROUND.patchyCover + (1 - GROUND.patchyCover) * patch);
  const dust = snowCm >= GROUND.dustCm && snow < 1;
  const wet = quantise(Math.max(mudLevel, waterCm / GROUND.wetFullCm));
  const pond = surface?.ice ? (waterCm >= GROUND.thickIceCm ? 'thick' : 'thin') : 'none';
  const lake = FROZEN_TERRAIN[terrain] ?? 'none';
  return intern(snow, dust, snowCm >= GROUND.ladenCm, wet < GROUND.mudMin ? 0 : wet, ICE_RANK[pond] > ICE_RANK[lake] ? pond : lake);
}

export const BARE = cellState(null, null);

/** The world state for a ClockDto: { palette, snowline, key }. */
export function worldState(clock) {
  const s = SEASONS[String(clock.season).toLowerCase()];
  if (!s) throw new Error(`no palette for season "${clock.season}"`);
  return { ...s, key: `${s.palette}@${s.snowline}` };
}

/**
 * The ground as the player last saw it, per cell. The view carries only the ground in sight, so the
 * page keeps what it has seen this session; a cell never in sight since is drawn bare.
 * terrainOf(code) -> terrain name for a MapDto.Known code.
 */
export function createGroundMemory(terrainOf) {
  let keys = [], cells = [], width = 0, version = 0, seen = null;
  return {
    /** Takes in a MapDto's ground in sight. */
    see(m) {
      if (m === seen) return;
      seen = m;
      if (keys.length !== m.width * m.height) { keys = new Array(m.width * m.height).fill(BARE.key); cells = new Array(m.width * m.height).fill(BARE); width = m.width; version++; }
      const surface = new Map(m.surface.map(s => [s.i, s])), inSight = new Set(m.visible);
      let changed = false;
      for (const i of m.visible) {
        const terrain = terrainOf(m.known[i]);
        if (!terrain) throw new Error(`tile ${i} is in sight but its terrain "${m.known[i]}" is not known`);
        const s = cellState(surface.get(i) ?? null, terrain);
        if (s.key !== keys[i]) { keys[i] = s.key; cells[i] = s; changed = true; }
      }
      for (const s of m.surface) if (!inSight.has(s.i)) throw new Error(`surface for tile ${s.i}, which is not in sight`);
      if (changed) version++;
    },
    forget() { keys = []; cells = []; seen = null; version++; },
    stateAt: (x, y) => cells[y * width + x] ?? BARE,
    keyAt: i => keys[i] ?? BARE.key,
    get version() { return version; },
  };
}
