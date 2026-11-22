// The world-space terrain renderer's pixels: pure functions, no DOM, so the page and the node
// preview tool (tools/terrain-preview.mjs) draw the same thing.
//
// The map is drawn per pixel from known terrain and world pixel coordinates. Tiles supply only
// texture (scripts/build-terrain-layers.py writes them to icons/terrain/); everything that must
// cross cells or must not repeat is computed here:
//   - borders: terrain is looked up through a world-noise displacement, so edges are organic;
//   - water: lake cells (in any wet 2x2) blend into round shores; river cells draw a channel to
//     each wet side, as a quarter arc when they turn; the rest of a water cell is its nearest land;
//   - mountains: ridged, domain-warped height scaled by how deep into the range a pixel is, lit
//     from the top left, with snow above a snowline;
//   - snow: a dusting, then patches spreading over it, then full cover over the patches, each by the
//     cell's depth through one world-noise mask, interpolated between cells so depth changes smoothly;
//   - mud, and standing water on land: mud art through one world-noise mask that grows with how much
//     of each the cell has; standing water that has frozen is ice;
//   - ice, on water and on frozen standing water: tone, cracks, leads and clear windows from world noise;
//   - sprites: trees, shrubs, boulders and reeds on a jittered world grid;
//   - caves: tunnels cut through the mountain as dark passages (the river channel's geometry), the
//     mouth opening onto the ground beside it; the range stays whole over them;
//   - ravines and cliffs, which lie between cells (map.edges): a dark cleft whose far wall catches the
//     light, and a one-sided rock face dropping toward the higher-numbered cell, with its shadow.
// A pixel needs only its cell's 5x5 neighbourhood, so the map can be baked a cell at a time.
// Unknown neighbours are read as the cell's own terrain: the renderer never shows what the
// player has not seen.
//
// Ground state per cell (stateAt) and for the world (world) select what is drawn; ground-state.js
// derives both from the view:
//   cell:  { dust: 0 | 1 (where patches are not), patches: 0..1 of the ground under patches, snow: 0..1 under full cover,
//            laden: bool (plants carry snow), mud: 0..1 (wet ground),
//            water: 0..1 (standing water's extent; frozen where ice is not 'none'), ice: 'none' | 'thin' | 'thick' }
//   world: { palette: 'winter' | 'summer' | 'autumn', snowline: 0..1 mountain height that holds snow }
// The palette sets colour only; every snow, mud and ice layer follows the cell, except the mountains'
// snow, which follows the season's snowline because the Sim keeps no snow on them.

export const CELL = 48, SRC = 96, VARIANTS = 4, STEP = 6;
export const WINTER = Object.freeze({ dust: 0, patches: 1, snow: 1, laden: true, mud: 0, water: 0, ice: 'thick' });
export const WINTER_WORLD = Object.freeze({ palette: 'winter', snowline: 0.34 });

// ---------------------------------------------------------------- the art that exists
/**
 * The layers and palettes scripts/build-terrain-layers.py paints. tools/terrain.test.mjs fails when
 * this list and assets/icons/terrain disagree.
 */
export const PAINTED = Object.freeze({
  palettes: ['winter', 'summer', 'autumn'],
  layers: ['ground', 'mud', 'snow', 'sprites'],
});

const layerOf = name => name.startsWith('terrain/') ? name.slice(8).split(/[/_]/)[0] : null;
/** A terrain name ends in its palette, or in its variant number when its colours never change. */
const paletteOf = name => { const p = name.slice(name.lastIndexOf('_') + 1); return /^\d+$/.test(p) ? null : p; };

/** `name`, if it is art that has been painted. Anything else is a bug in the caller, so it throws. */
export function resolveArt(name) {
  const layer = layerOf(name);
  if (!layer) return name;
  if (!PAINTED.layers.includes(layer)) throw new Error(`no terrain art for layer "${layer}" (${name})`);
  const pal = paletteOf(name);
  if (pal && !PAINTED.palettes.includes(pal)) throw new Error(`no terrain art in palette "${pal}" (${name})`);
  return name;
}

const WET = t => t === 'water' || t === 'deepwater';
const CAVE = t => t === 'cavefloor' || t === 'caveentrance';
/** Rock for height: a cave runs under the mountain, so the range stays whole over it. */
const ROCKY = t => t === 'mountain' || CAVE(t);
const OPEN = t => t && !ROCKY(t) && !WET(t);
const FLAVOUR = { hills: 'rugged', rock: 'crag' };
const hex = s => [parseInt(s.slice(1, 3), 16), parseInt(s.slice(3, 5), 16), parseInt(s.slice(5, 7), 16)];
const C = Object.fromEntries(Object.entries({
  s0: '#f4f6f4', s1: '#e3eaec', s2: '#ccd7dd', s3: '#b0c0ca', s4: '#8fa3b2', s5: '#6c8193',
  r0: '#b6b8b5', r1: '#8a8d8d', r2: '#646a6e', r3: '#454b52', r4: '#2b3037',
  e3: '#463a2c', sc: '#7d7a6b',
  i0: '#cfe4ea', i1: '#aecfda', i2: '#89b3c4', i3: '#5f8ea3', i4: '#3f6f86',
  ti0: '#9db4bc', ti1: '#7f9aa6', ti2: '#647f8d', ti3: '#4f6a7a',
}).map(([k, v]) => [k, hex(v)]));
/** A sprite's shadow on bare ground, by palette (the build script's `sh`; winter's bare ground is autumn's). */
const SHADOW = { summer: hex('#323d2b'), autumn: hex('#3a3126'), winter: hex('#3a3126') };
const SNOW_LAYERS = ['dust', 'patches', 'snow'];
const SNOW = ['s0', 's1', 's2', 's3', 's4', 's5'].map(k => C[k]);
const ROCK = ['r0', 'r1', 'r2', 'r3', 'r4'].map(k => C[k]);
const ICE = ['i0', 'i1', 'i2', 'i3'].map(k => C[k]);
const THIN = ['ti0', 'ti1', 'ti2', 'ti3'].map(k => C[k]);
/** A cave floor: dry dust and grit, darker toward the walls. */
const CAVE_FLOOR = ['#5a4d40', '#4a3f35', '#3d342c', '#302924', '#25201c', '#1b1815'].map(hex);
const CLEFT = hex('#191c20');

// ---------------------------------------------------------------- world noise
// 32-bit integer hashes, so every platform draws the same pixels.
export function lattice(ix, iy, seed) {
  let h = (Math.imul(ix, 374761393) + Math.imul(iy, 668265263) + Math.imul(seed, 982451653)) >>> 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177) >>> 0;
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}
export function vnoise(x, y, scale, seed) {
  x /= scale; y /= scale;
  const ix = Math.floor(x), iy = Math.floor(y);
  let fx = x - ix, fy = y - iy;
  fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
  const a = lattice(ix, iy, seed), b = lattice(ix + 1, iy, seed), c = lattice(ix, iy + 1, seed), d = lattice(ix + 1, iy + 1, seed);
  return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
}
const hash01 = (px, py, salt) => lattice(px, py, 7000 + salt);
function ramp(cols, v, px, py, salt) {
  const t = hash01(px, py, salt) * 0.5 + vnoise(px, py, 2, 900 + salt) * 0.5;
  v = Math.max(0, Math.min(cols.length - 1, v));
  const lo = Math.floor(v);
  return cols[Math.min(cols.length - 1, lo + (v - lo > t ? 1 : 0))];
}
/** The world mask snow lies by: where it is high, snow lies first and longest. */
export const snowField = (px, py) => vnoise(px, py, 60, 31) * 0.55 + vnoise(px, py, 22, 32) * 0.3 + vnoise(px, py, 7, 33) * 0.15;
/** Which of `count` texture variants a 96px source square uses (same hash as the old TileRenderer). */
export function variant(sx, sy, count) {
  let h = Math.imul(sx, 73856093) ^ Math.imul(sy, 19349663);
  h ^= h >> 13; h = Math.imul(h, 1274126177); h ^= h >> 16;
  return (h >>> 0) % count;
}

// ---------------------------------------------------------------- ice
/**
 * Ice at a world pixel, or null where open water shows through. Painted in world space because a
 * lake spans many textures and any period in its ice would show: cracks follow contours of
 * domain-warped noise, open water shows through leads and clear windows, and thick ice carries a
 * powdering of snow, thicker where the wind drops it.
 */
export function ice(px, py, level) {
  const X = px + (vnoise(px, py, 50, 81) - 0.5) * 60, Y = py + (vnoise(px, py, 50, 82) - 0.5) * 60;
  if (level === 'thin') {
    if (Math.abs(vnoise(X, Y, 40, 83) - 0.5) < 0.016 || Math.abs(vnoise(Y, X, 15, 84) - 0.5) < 0.01) return null;
    if (vnoise(px, py, 90, 85) * 0.7 + vnoise(px, py, 30, 86) * 0.3 > 0.66) return null;
    if (hash01(px, py, 89) < 0.012) return C.s1; // frost
    return ramp(THIN, 1.4 + (vnoise(px, py, 55, 87) - 0.5) * 2.4 + (vnoise(px, py, 18, 88) - 0.5) * 0.6, px, py, 3);
  }
  if (level !== 'thick') throw new Error(`no ice "${level}"`);
  const drift = vnoise(px, py, 70, 95) * 0.6 + vnoise(px, py, 22, 96) * 0.4;
  if (hash01(px, py, 97) < 0.06 + Math.max(0, Math.min(1, drift - 0.5)) * 0.9) return C.s1;
  if (vnoise(px, py, 80, 90) * 0.7 + vnoise(px, py, 26, 91) * 0.3 > 0.72) return null;
  if (Math.abs(vnoise(X, Y, 45, 94) - 0.5) < 0.012) return C.i4;
  return ramp(ICE, 1.1 + (vnoise(px, py, 60, 92) - 0.5) * 1.6 + (vnoise(px, py, 20, 93) - 0.5) * 0.5, px, py, 4);
}

// ---------------------------------------------------------------- textures
/** Art names a world state draws from. */
export function wantedArt(world) {
  const p = world.palette, names = [];
  for (let n = 1; n <= VARIANTS; n++) {
    for (const t of ['forest', 'clearing', 'plain', 'hills', 'rock', 'marsh', 'water', 'deepwater']) names.push(`terrain/ground/${t}_${n}_${p}`);
    for (const level of ['dust', 'patchy', 'full']) for (const f of ['flat', 'rugged', 'crag']) names.push(`terrain/snow/${level}_${f}_${n}`);
    names.push(`terrain/mud_${n}_${p}`);
  }
  for (const s of SPRITE_NAMES) for (const laden of [true, false]) names.push(spriteArt(s, p, laden));
  return names;
}
/** Texture files a world state draws from. */
export const textureNames = world => [...new Set(wantedArt(world).map(n => resolveArt(n)))];
const SPRITE_NAMES = ['spruce_l', 'spruce_m', 'spruce_s', 'shrub', 'boulder', 'reed_2', 'reed_3', 'reed_4'];
/**
 * A bare plant draws its season's palette. Winter's palette is autumn's colours (dead needles and
 * grass), and the files named winter are painted carrying snow, so a bare winter plant draws the
 * autumn file. Snow on a plant follows the cell (laden), not the season.
 */
const BARE_SPRITES = { winter: 'autumn', summer: 'summer', autumn: 'autumn' };
const spriteArt = (name, palette, laden) => `terrain/sprites/${name}_${laden ? 'winter' : BARE_SPRITES[palette]}`;

// ---------------------------------------------------------------- ravines and cliffs
/**
 * Ravines and cliffs (map.edges: [{ ax, ay, bx, by, type }]) as straight pieces in cell coordinates.
 * The boundary segments of each kind are chained, and each chain is rounded (corner cutting), so a
 * ravine that steps from cell to cell reads as one curving line; an open chain's ends taper. A piece
 * depends on its whole chain, so the map's pieces are worked out once, and a cell baked on its own is
 * handed the pieces in its reach (piecesNear).
 */
export function edgePieces(edges) {
  const pieces = [];
for (const type of ['ravine', 'cliff']) {
  const at = new Map(), key = (x, y) => `${x},${y}`, segs = [];
  for (const e of edges) {
    if (e.type !== type) continue;
    const side = e.bx !== e.ax; // neighbours side by side share a vertical boundary
    const bx = Math.max(e.ax, e.bx), by = Math.max(e.ay, e.by);
    const g = side ? [[bx, e.ay], [bx, e.ay + 1]] : [[e.ax, by], [e.ax + 1, by]];
    segs.push(g);
    for (const v of g) { const k = key(...v); if (!at.has(k)) at.set(k, []); at.get(k).push(g); }
  }
  const used = new Set();
  const walk = start => {
    const pts = [start];
    for (let v = start; ;) {
      const next = (at.get(key(...v)) ?? []).find(g => !used.has(g));
      if (!next) break;
      used.add(next);
      v = next[0][0] === v[0] && next[0][1] === v[1] ? next[1] : next[0];
      pts.push(v);
    }
    return pts;
  };
  const chains = [];
  for (const [k, list] of at) if (list.length === 1 && !used.has(list[0])) chains.push({ pts: walk(k.split(',').map(Number)), open: true });
  for (const g of segs) if (!used.has(g)) chains.push({ pts: walk(g[0]), open: false });
  for (const { pts: raw, open } of chains) {
    // A closed chain ends on its first point; corner cutting goes round it, so it is cut as a ring
    // (each pass wrapping from the last point to the first) and closed again after.
    let pts = open ? raw : raw.slice(0, -1);
    for (let it = 0; it < 3; it++) {
      const out = open ? [pts[0]] : [];
      for (let i = 0; i < (open ? pts.length - 1 : pts.length); i++) {
        const [a, b] = [pts[i], pts[(i + 1) % pts.length]];
        out.push([a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25], [a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75]);
      }
      if (open) out.push(pts[pts.length - 1]);
      pts = out;
    }
    if (!open) pts.push(pts[0]);
    let run = 0, total = 0;
    for (let i = 0; i < pts.length - 1; i++) total += Math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]);
    for (let i = 0; i < pts.length - 1; i++) {
      const [[x1, y1], [x2, y2]] = [pts[i], pts[i + 1]], len = Math.hypot(x2 - x1, y2 - y1);
      if (len === 0) continue;
      // The normal that points to the lower right: a cliff drops that way, so its face is in shadow.
      let nx = -(y2 - y1) / len, ny = (x2 - x1) / len;
      if (nx + ny < 0) { nx = -nx; ny = -ny; }
      pieces.push({ x1, y1, x2, y2, len, nx, ny, type, start: run, total, open });
      run += len;
    }
  }
}
  return pieces;
}

/** The cells a piece is drawn from: every cell within one of either end. */
function pieceCells(p, fn) {
  for (const [x, y] of [[p.x1, p.y1], [p.x2, p.y2]]) for (let oy = -2; oy <= 1; oy++) for (let ox = -2; ox <= 1; ox++)
    fn(Math.floor(x) + ox, Math.floor(y) + oy);
}

/** The pieces drawn from any cell within REACH of (cx, cy), in the map's order: all a cell's pixels need. */
export function piecesNear(pieces, cx, cy) {
  return pieces.filter(p => {
    let near = false;
    pieceCells(p, (x, y) => { if (Math.abs(x - cx) <= REACH && Math.abs(y - cy) <= REACH) near = true; });
    return near;
  });
}

// ---------------------------------------------------------------- the map
/**
 * map: { width, height, terrainAt(x, y) -> terrain name, or null where unknown,
 *        edges?: [{ ax, ay, bx, by, type: 'ravine' | 'cliff' }] between 4-adjacent known cells, or
 *        pieces?: edgePieces of them, worked out already }.
 * stateAt(x, y) -> cell state; world -> world state; tex(name) -> { w, h, data } (RGBA).
 */
export function createBaker(map, stateAt, world, tex) {
  const { width: W, height: H } = map;
  const cells = new Map();
  // Ravines and cliffs, indexed by the cells they are drawn from.
  const edgeIndex = new Map();
  for (const piece of map.pieces ?? edgePieces(map.edges ?? []))
    pieceCells(piece, (cx, cy) => {
      const k = cy * W + cx;
      if (!edgeIndex.has(k)) edgeIndex.set(k, new Set());
      edgeIndex.get(k).add(piece);
    });

  /** A cell's 5x5 neighbourhood, terrain and ground state, with unknown cells read as the cell's own. */
  function cell(cx, cy) {
    const key = cy * W + cx;
    let c = cells.get(key);
    if (c) return c;
    const own = map.terrainAt(cx, cy);
    const nb = new Array(25), st = new Array(25), mine = own ? stateAt(cx, cy) : null;
    let wet = false, mtn = false, cave = false;
    for (let oy = -2; oy <= 2; oy++) for (let ox = -2; ox <= 2; ox++) {
      const x = Math.min(W - 1, Math.max(0, cx + ox)), y = Math.min(H - 1, Math.max(0, cy + oy));
      const t = map.terrainAt(x, y);
      nb[(oy + 2) * 5 + ox + 2] = t ?? own;
      st[(oy + 2) * 5 + ox + 2] = t ? stateAt(x, y) : mine;
      if (WET(t ?? own)) wet = true;
      if (ROCKY(t ?? own)) mtn = true;
      if (Math.abs(ox) <= 1 && Math.abs(oy) <= 1 && CAVE(t ?? own)) cave = true;
    }
    c = { own, nb, st, wet, mtn, cave, edges: [...(edgeIndex.get(key) ?? [])] };
    cells.set(key, c);
    return c;
  }
  const at = (c, ox, oy) => c.nb[(oy + 2) * 5 + ox + 2];

  function height(c, cx, cy, fx, fy) {
    const rx = fx - cx - 0.5, ry = fy - cy - 0.5;
    let acc = 0, norm = 1e-6;
    for (let oy = -2; oy <= 2; oy++) for (let ox = -2; ox <= 2; ox++) {
      const d2 = (rx - ox) ** 2 + (ry - oy) ** 2;
      const k = Math.max(0, Math.min(1, 1 - d2 / 4)) ** 2;
      if (ROCKY(at(c, ox, oy))) acc += k;
      norm += k;
    }
    const m = acc / norm, lift = Math.max(0, Math.min(1, m * 1.6 - 0.3));
    if (lift === 0) return [0, m];
    const X = fx * CELL, Y = fy * CELL;
    // Ridged value noise crests along lattice lines; rotate and warp it so ridges wander.
    const wx = X + (vnoise(X, Y, 90, 61) - 0.5) * 120, wy = Y + (vnoise(X, Y, 90, 62) - 0.5) * 120;
    const ax1 = wx * 0.866 - wy * 0.5, ay1 = wx * 0.5 + wy * 0.866;
    const ax2 = wx * 0.6 + wy * 0.8, ay2 = -wx * 0.8 + wy * 0.6;
    const r1 = (1 - Math.abs(vnoise(ax1, ay1, 150, 21) * 2 - 1)) ** 1.6;
    const r2 = (1 - Math.abs(vnoise(ax2, ay2, 60, 22) * 2 - 1)) ** 1.6;
    const r3 = vnoise(X, Y + 11, 16, 23);
    const crag = (1 - Math.abs(vnoise(ax2 + 500, ay2, 22, 27) * 2 - 1)) * 0.07 + vnoise(X, Y, 7, 28) * 0.025;
    return [lift * (0.25 + 0.5 * r1 + 0.2 * r2 + 0.05 * r3 + crag), m];
  }

  /**
   * What is at a pixel: { kind, water, bank, mountain, h, h1, h2, state }, or null in an unknown cell.
   * The state is that of the cell whose terrain the pixel shows, so a lake reaching into a land cell is
   * frozen with the lake and snow follows the same organic borders as the ground under it.
   */
  function classify(px, py) {
    const cx = Math.floor(px / CELL), cy = Math.floor(py / CELL);
    const c = cell(cx, cy);
    if (!c.own) return null;
    const fxc = (px + 0.5) / CELL, fyc = (py + 0.5) / CELL;
    // Borders: terrain through a smooth displacement (under half a cell) plus a fine jitter.
    const dx = (vnoise(px, py, 26, 51) - 0.5) * 0.62 + (vnoise(px, py, 8, 52) - 0.5) * 0.16 + (hash01(px, py, 53) - 0.5) * 0.06;
    const dy = (vnoise(px, py, 26, 54) - 0.5) * 0.62 + (vnoise(px, py, 8, 55) - 0.5) * 0.16 + (hash01(px, py, 56) - 0.5) * 0.06;
    const lx = Math.min(1, Math.max(-1, Math.floor(fxc + dx) - cx)), ly = Math.min(1, Math.max(-1, Math.floor(fyc + dy) - cy));
    const pick = at(c, lx, ly);

    let water = false, bank = false, kind = pick, sx = lx, sy = ly;
    // The meandered position relative to the cell centre, shared by water and the land fallback.
    const mx = (vnoise(px, py, 40, 11) - 0.5) * 0.24 + (vnoise(px, py, 13, 12) - 0.5) * 0.06;
    const my = (vnoise(px, py, 40, 13) - 0.5) * 0.24 + (vnoise(px, py, 13, 14) - 0.5) * 0.06;
    const qx = fxc - cx - 0.5 + mx, qy = fyc - cy - 0.5 + my;
    if (c.wet) {
      const w5 = (ox, oy) => WET(at(c, ox, oy));
      const wobble = (vnoise(px, py, 30, 15) - 0.5) * 0.08;
      let sdist = 9, wtype = 'water', wx = 0, wy = 0, kb = 0, kn = 1e-6, kbest = 0, btype = 'deepwater', bx = 0, by = 0;
      for (let coy = -1; coy <= 1; coy++) for (let cox = -1; cox <= 1; cox++) {
        const here = w5(cox, coy);
        let body = false;
        if (here) for (const bx of [-1, 0]) for (const by of [-1, 0])
          if (w5(cox + bx, coy + by) && w5(cox + bx + 1, coy + by) && w5(cox + bx, coy + by + 1) && w5(cox + bx + 1, coy + by + 1)) body = true;
        const rx = qx - cox, ry = qy - coy;
        const kc = Math.max(0, Math.min(1, 1 - (rx * rx + ry * ry) / 1.5)) ** 2;
        if (body) kb += kc;
        kn += kc;
        if (body && kc > kbest) { kbest = kc; btype = at(c, cox, coy); bx = cox; by = coy; }
        if (!here || body) continue;
        const conn = { e: w5(cox + 1, coy), w: w5(cox - 1, coy), s: w5(cox, coy + 1), n: w5(cox, coy - 1) };
        const count = conn.e + conn.w + conn.s + conn.n;
        let chan = 9;
        for (const [ddx, ddy, ok] of [[1, 0, conn.e], [-1, 0, conn.w], [0, 1, conn.s], [0, -1, conn.n]]) {
          if (!ok) continue;
          const t = Math.max(0, Math.min(1, (rx * ddx + ry * ddy) / 0.5));
          chan = Math.min(chan, Math.hypot(rx - ddx * 0.5 * t, ry - ddy * 0.5 * t));
        }
        if (count === 0) chan = Math.hypot(rx, ry) - 0.12; // a lone pool
        if (count === 2) for (const sx of [1, -1]) for (const sy of [1, -1]) {
          if (!((sx > 0 ? conn.e : conn.w) && (sy > 0 ? conn.s : conn.n))) continue;
          const kx = sx * 0.5, ky = sy * 0.5;
          const inq = (rx - kx) * -sx >= 0 && (ry - ky) * -sy >= 0;
          chan = inq ? Math.abs(Math.hypot(rx - kx, ry - ky) - 0.5) : Math.min(Math.hypot(rx - kx, ry), Math.hypot(rx, ry - ky));
        }
        const kindC = at(c, cox, coy);
        const s = chan - ((kindC === 'deepwater' ? 0.19 : 0.14) + wobble);
        if (s < sdist) { sdist = s; wtype = kindC; wx = cox; wy = coy; }
      }
      const lake = kb / kn + (vnoise(px, py, 20, 16) - 0.5) * 0.25 + (vnoise(px, py, 6, 17) - 0.5) * 0.08;
      const sLake = (0.5 - lake) * 0.7;
      if (sLake < sdist) { wtype = btype; wx = bx; wy = by; sdist = sLake; }
      water = sdist < 0;
      bank = sdist >= 0 && sdist < 2.5 / CELL;
      if (water) { kind = wtype; sx = wx; sy = wy; }
    }
    if (!water && WET(pick)) {
      // Land the lookup gave to a water cell belongs to the nearest land cell instead.
      let best = 9; kind = null;
      for (let oy = -1; oy <= 1; oy++) for (let ox = -1; ox <= 1; ox++) {
        const t = at(c, ox, oy), d = Math.hypot(qx - ox, qy - oy);
        if (!WET(t) && d < best) { best = d; kind = t; sx = ox; sy = oy; }
      }
      kind ??= 'marsh'; // a cell wholly surrounded by water
    }

    let mountain = false, h = 0, h1 = 0, h2 = 0;
    if (c.mtn) {
      let m;
      [h, m] = height(c, cx, cy, fxc, fyc);
      mountain = m > 0.375 && m + (vnoise(px, py, 14, 24) - 0.5) * 0.25 > 0.5;
      if (mountain) { h1 = height(c, cx, cy, fxc + 1 / CELL, fyc)[0]; h2 = height(c, cx, cy, fxc, fyc + 1 / CELL)[0]; }
      if (ROCKY(kind) && !mountain) {
        let best = 9; kind = 'plain';
        for (let oy = -1; oy <= 1; oy++) for (let ox = -1; ox <= 1; ox++) {
          const t = at(c, ox, oy), d = Math.hypot(qx - ox, qy - oy);
          if (OPEN(t) && d < best) { best = d; kind = t; sx = ox; sy = oy; }
        }
      }
    }
    // Caves: a passage through the rock along the cave's cells, out of the mouth onto open ground.
    let feature = null, across = 0, lit = false, mouth = 0;
    if (c.cave && !water) {
      const open = (ox, oy) => OPEN(at(c, ox, oy));
      const wx = qx + (vnoise(px, py, 80, 130) - 0.5) * 0.9 + (vnoise(px, py, 26, 128) - 0.5) * 0.16,
        wy = qy + (vnoise(px, py, 80, 129) - 0.5) * 0.9 + (vnoise(px, py, 26, 127) - 0.5) * 0.16;
      let best = 9;
      for (let coy = -1; coy <= 1; coy++) for (let cox = -1; cox <= 1; cox++) {
        const t = at(c, cox, coy);
        if (!CAVE(t)) continue;
        const links = [[1, 0], [-1, 0], [0, 1], [0, -1]].filter(([dx, dy]) =>
          CAVE(at(c, cox + dx, coy + dy)) || (t === 'caveentrance' && open(cox + dx, coy + dy)));
        const rx = wx - cox, ry = wy - coy;
        let d = links.length ? 9 : Math.hypot(rx, ry) - 0.1, near = [0, 0];
        for (const [dx, dy] of links) {
          const k = Math.max(0, Math.min(1, (rx * dx + ry * dy) / (open(cox + dx, coy + dy) ? 0.75 : 0.5)));
          const ex = dx * (open(cox + dx, coy + dy) ? 0.75 : 0.5) * k, ey = dy * (open(cox + dx, coy + dy) ? 0.75 : 0.5) * k;
          const dd = Math.hypot(rx - ex, ry - ey);
          if (dd < d) { d = dd; near = [ex, ey]; }
        }
        // Ragged walls, and a chamber where passages meet or turn.
        const chamber = links.length > 2 || (links.length === 2 && links[0][0] !== -links[1][0]) ? 0.1 * Math.max(0, 1 - Math.hypot(rx, ry) / 0.45) : 0;
        const r = 0.2 + chamber + (vnoise(px, py, 26, 131) - 0.5) * 0.14 + (vnoise(px, py, 6, 126) - 0.5) * 0.07;
        if (d - r < best) {
          best = d - r;
          across = Math.min(1, d / r);
          lit = (rx - near[0]) + (ry - near[1]) > 0; // the wall on the lower right faces the light
          mouth = t === 'caveentrance' ? Math.max(0, 1 - Math.hypot(rx, ry) / 0.8) : 0;
        }
      }
      if (best < 0) { feature = 'cave'; mountain = false; }
      else if (best < 1.5 / CELL && mountain) feature = 'caverim';
    }
    // Ravines and cliffs, along the boundaries between cells.
    if (!feature && c.edges.length && !water) {
      const ex = qx + cx + 0.5 + (vnoise(px, py, 80, 121) - 0.5) * 0.8 + (vnoise(px, py, 25, 119) - 0.5) * 0.14,
        ey = qy + cy + 0.5 + (vnoise(px, py, 80, 122) - 0.5) * 0.8 + (vnoise(px, py, 25, 120) - 0.5) * 0.14;
      // The nearest ravine and the nearest cliff, then which part of either the pixel is in.
      let rav = null, cliff = null;
      for (const g of c.edges) {
        const tx = (g.x2 - g.x1) / g.len, ty = (g.y2 - g.y1) / g.len;
        const raw = (ex - g.x1) * tx + (ey - g.y1) * ty, along = Math.max(0, Math.min(g.len, raw));
        const ox = ex - g.x1 - tx * along, oy = ey - g.y1 - ty * along, d = Math.hypot(ox, oy);
        // An open chain narrows to nothing over its last 0.6 cell.
        const pos = g.start + along, taper = g.open ? Math.max(0, Math.min(1, (pos + 0.1) / 0.6, (g.total - pos + 0.1) / 0.6)) : 1;
        if (g.type === 'ravine') {
          const r = (0.19 + (vnoise(px, py, 40, 123) - 0.5) * 0.16 + (vnoise(px, py, 7, 125) - 0.5) * 0.05) * taper;
          if (!rav || d - r < rav.s) rav = { s: d - r, across: r > 0 ? Math.min(1, d / r) : 1, lit: ox + oy > 0 };
        } else if (!cliff || d < cliff.d) {
          cliff = { d, side: ox * g.nx + oy * g.ny, w: (0.34 + (vnoise(px, py, 24, 124) - 0.5) * 0.14) * taper, taper };
        }
      }
      if (rav && rav.s < 0) { feature = 'ravine'; across = rav.across; lit = rav.lit; }
      else if (rav && rav.s < 2 / CELL) feature = 'ravinerim';
      else if (cliff && cliff.w > 0 && Math.abs(cliff.side) >= cliff.d - 0.02) {
        if (cliff.side >= 0 && cliff.side < cliff.w) { feature = 'cliff'; across = cliff.side / cliff.w; }
        else if (cliff.side >= cliff.w && cliff.side < cliff.w + 0.22 * cliff.taper) { feature = 'cliffshadow'; across = (cliff.side - cliff.w) / (0.22 * cliff.taper); }
        else if (cliff.side < 0 && cliff.side > -2.5 / CELL) feature = 'clifflip';
      }
    }
    return { kind, water, bank, mountain: mountain && !water, h, h1, h2, feature, across, lit, mouth, state: c.st[(sy + 2) * 5 + sx + 2] };
  }

  const art = new Map();
  const texture = name => { let a = art.get(name); if (!a) art.set(name, a = tex(resolveArt(name))); return a; };
  function sample(name, px, py) {
    const t = texture(name), sx = Math.floor(px / SRC), sy = Math.floor(py / SRC);
    const x = Math.floor((px - sx * SRC) * t.w / SRC), y = Math.floor((py - sy * SRC) * t.h / SRC);
    const i = (y * t.w + x) * 4;
    return t.data[i + 3] ? [t.data[i], t.data[i + 1], t.data[i + 2]] : null;
  }

  /** The colour of a pixel before sprites, or null in an unknown cell. */
  function colour(px, py) {
    const p = classify(px, py);
    if (!p) return null;
    const s = p.state, pal = world.palette;
    const n = variant(Math.floor(px / SRC), Math.floor(py / SRC), VARIANTS) + 1;
    if (p.feature === 'cave') {
      // The walls: the far one catches what light there is, the near one is black.
      if (p.across > 0.78) return p.lit ? ramp(ROCK, 2.4 - (p.across - 0.78) * 3 + (vnoise(px, py, 5, 132) - 0.5), px, py, 7) : C.r4;
      // Fallen rock and grit on the floor.
      if (vnoise(px, py, 5, 125) > 0.76) return ramp(ROCK, 3 + (vnoise(px, py, 3, 133) - 0.5) * 1.2, px, py, 11);
      if (hash01(px, py, 133) < 0.03) return CAVE_FLOOR[0];
      // Dark toward the walls, and darkest where the mouth gives way to the unlit inside.
      return ramp(CAVE_FLOOR, 1.8 + p.across * 2.4 + (1 - p.mouth) * 1.1 + (vnoise(px, py, 9, 134) - 0.5) * 1.2, px, py, 8);
    }
    if (p.feature === 'caverim') return hash01(px, py, 135) < 0.85 ? C.r4 : C.r3;
    if (p.feature === 'ravine') {
      const [, , cover] = between(px, py, SNOW_LAYERS);
      if (p.across < 0.3) return hash01(px, py, 136) < 0.04 ? C.r3 : CLEFT; // the floor, out of the light
      if (!p.lit) return p.across > 0.85 && hash01(px, py, 137) < 0.5 ? C.r3 : C.r4;
      // The lit wall: ledges catch snow when the ground around has it.
      if (cover > 0.5 && vnoise(px, py, 5, 138) > 0.62) return C.s2;
      return ramp(ROCK, 2.9 - p.across * 2 + (vnoise(px, py, 4, 139) - 0.5) * 0.9, px, py, 9);
    }
    if (p.feature === 'cliff') {
      // Strata run along the face; fissures cut down it.
      const strata = Math.floor(p.across * 5 + vnoise(px, py, 30, 141) * 2) % 2;
      if (vnoise(px, py, 3, 142) > 0.8) return C.r4; // fissures
      // Snow on the ledges of the face, when the ground above has it.
      const [, , cover] = between(px, py, SNOW_LAYERS);
      if (cover > 0.5 && strata && vnoise(px, py, 4, 145) > 0.6) return C.s3;
      return ramp(ROCK, (strata ? 1.8 : 2.6) + p.across * 0.9 + (vnoise(px, py, 7, 143) - 0.5) * 0.6, px, py, 10);
    }
    if (p.mountain) {
      const shade = Math.max(-1.4, Math.min(1.4, ((p.h1 - p.h) + (p.h2 - p.h)) * 40));
      let rock = ramp(ROCK, 2.1 - shade * 2.2 + (vnoise(px, py, 9, 25) - 0.5) * 0.4, px, py, 1);
      const line = world.snowline + (vnoise(px, py, 16, 26) - 0.5) * 0.12;
      const holds = p.h > line && shade > -0.35 + (p.h - line) * 1.5; // steep shaded faces shed snow
      const band = Math.floor(p.h * 9) !== Math.floor(p.h1 * 9) || Math.floor(p.h * 9) !== Math.floor(p.h2 * 9);
      if (band && shade < -0.15 && vnoise(px, py, 12, 29) > 0.45) rock = C.r4;
      if (holds) return ramp(SNOW, 1.2 - shade * 1.7, px, py, 2);
      if (p.h < 0.2 && hash01(px, py, 6) < 0.25) return between(px, py, ['snow'])[0] >= 1 ? C.s3 : C.sc; // scree, buried only under full cover
      return rock;
    }
    if (WET(p.kind)) {
      let col = sample(`terrain/ground/${p.kind}_${n}_${pal}`, px, py);
      return (s.ice === 'none' ? null : ice(px, py, s.ice)) ?? col;
    }
    if (CAVE(p.kind)) p.kind = 'rock'; // the ground around a cave mouth, outside the rock
    let col = sample(`terrain/ground/${p.kind}_${n}_${pal}`, px, py);
    const flav = FLAVOUR[p.kind] ?? 'flat', [dust, patches, cover] = between(px, py, SNOW_LAYERS);
    const lies = snowed(px, py, cover);
    // Mud, and standing water in the wettest of it: wet ground while liquid, ice where it has frozen.
    // One mask for both, so water lies in the lowest of the mud. Its noise lies almost wholly within
    // 0.15..0.85, so its threshold spans that range: the least wetness shows, and a soaked cell shows
    // all of the (itself sparse) mud art.
    const wet = vnoise(px, py, 34, 41) * 0.65 + vnoise(px, py, 11, 42) * 0.35;
    const shows = level => level > 0 && !lies && wet > 0.85 - level * 0.7;
    if (shows(s.mud)) col = sample(`terrain/mud_${n}_${pal}`, px, py) ?? col;
    if (shows(s.water)) col = (s.ice === 'none' ? null : ice(px, py, s.ice)) ?? sample(`terrain/mud_${n}_${pal}`, px, py) ?? col;
    const patched = snowed(px, py, patches);
    if (snowed(px, py, dust) && !patched) col = sample(`terrain/snow/dust_${flav}_${n}`, px, py) ?? col;
    if (patched && !lies) col = sample(`terrain/snow/patchy_${flav}_${n}`, px, py) ?? col;
    if (lies) col = sample(`terrain/snow/full_${flav}_${n}`, px, py) ?? col;
    // Banks: a dark wet edge, or a shadowed snow lip where the ground is covered.
    if (p.bank && hash01(px, py, 5) < (cover >= 1 ? 0.8 : 0.75)) col = cover >= 1 ? C.s4 : C.e3;
    if (p.feature === 'ravinerim' && hash01(px, py, 140) < 0.7) col = C.r4; // a broken dark lip
    if (p.feature === 'clifflip') col = cover >= 0.5 ? C.s0 : C.r0;
    if (p.feature === 'cliffshadow' && hash01(px, py, 144) < 1 - p.across * 0.7) col = col.map(v => Math.round(v * 0.66));
    return col;
  }

  /**
   * Cell state numbers at a pixel, interpolated between cell centres, so snow deepens smoothly from
   * cell to cell rather than stepping at their borders.
   */
  function between(px, py, keys) {
    const cx = Math.floor(px / CELL), cy = Math.floor(py / CELL), st = cell(cx, cy).st;
    const fx = (px + 0.5) / CELL - cx - 0.5, fy = (py + 0.5) / CELL - cy - 0.5;
    const ox = Math.floor(fx), oy = Math.floor(fy), tx = fx - ox, ty = fy - oy;
    return keys.map(key => {
      const v = (dx, dy) => {
        const x = st[(oy + dy + 2) * 5 + ox + dx + 2][key];
        if (!Number.isFinite(x)) throw new Error(`cell state near (${cx}, ${cy}) has no number "${key}": ${x}`);
        return x;
      };
      return (v(0, 0) * (1 - tx) + v(1, 0) * tx) * (1 - ty) + (v(0, 1) * (1 - tx) + v(1, 1) * tx) * ty;
    });
  }

  // One world mask decides where the dusting, patches and full cover lie, so deeper snow grows from
  // the same hollows. Its noise lies almost wholly within 0.2..0.8, so a cover's threshold spans that range:
  // a little snow shows a little, and half cover is world.py's half.
  const threshold = cover => 0.8 - cover * 0.6;
  const snowed = (px, py, cover) => cover >= 1 || (cover > 0 && snowField(px, py) > threshold(cover));

  /**
   * Bakes the pixels of cells [x0, x1) x [y0, y1) into out (RGBA, (x1 - x0) * CELL wide), then the
   * sprites over them. Cells may be baked in any order and in separate calls; sprites last.
   */
  function bakeCell(out, x0, y0, x1, cx, cy) {
    const stride = (x1 - x0) * CELL;
    for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
      const px = cx * CELL + x, py = cy * CELL + y, col = colour(px, py);
      const i = (((cy - y0) * CELL + y) * stride + (cx - x0) * CELL + x) * 4;
      if (!col) { out[i + 3] = 0; continue; }
      out[i] = col[0]; out[i + 1] = col[1]; out[i + 2] = col[2]; out[i + 3] = 255;
    }
  }

  function bakeSprites(out, x0, y0, x1, y1) {
    const stride = (x1 - x0) * CELL, X0 = x0 * CELL, Y0 = y0 * CELL, X1 = x1 * CELL, Y1 = y1 * CELL;
    const MW = W * CELL, MH = H * CELL, pal = world.palette;
    const put = (x, y, col) => {
      if (x < X0 || x >= X1 || y < Y0 || y >= Y1) return;
      const i = ((y - Y0) * stride + (x - X0)) * 4;
      out[i] = col[0]; out[i + 1] = col[1]; out[i + 2] = col[2]; out[i + 3] = 255;
    };
    const solid = p => !p || p.water || p.mountain || (p.feature && p.feature !== 'cliffshadow');
    const blocked = (x, y) => solid(classify(x, y));
    const found = [];
    // Any anchor whose sprite (up to 12 rows tall, 9 wide, a 2-row shadow) can reach this area.
    const gx0 = Math.max(0, Math.floor((X0 - 8) / STEP)), gx1 = Math.min(MW / STEP - 1, Math.floor((X1 + 8) / STEP));
    const gy0 = Math.max(0, Math.floor((Y0 - 4) / STEP)), gy1 = Math.min(MH / STEP - 1, Math.floor((Y1 + 14) / STEP));
    for (let gy = gy0; gy <= gy1; gy++) for (let gx = gx0; gx <= gx1; gx++) {
      const ax = gx * STEP + Math.floor(lattice(gx, gy, 101) * STEP), ay = gy * STEP + Math.floor(lattice(gx, gy, 102) * STEP);
      const roll = lattice(gx, gy, 103), r = lattice(gx, gy, 104);
      const dens = vnoise(ax, ay, 40, 105) * 0.7 + vnoise(ax, ay, 12, 106) * 0.3;
      const p = classify(ax, ay);
      if (solid(p)) continue;
      const t = p.kind, laden = p.state.laden;
      let what = null;
      if (t === 'forest' && dens > 0.2 && roll < 0.92) what = 'LLMMS'[Math.floor(r * 5)];
      else if (t === 'clearing' && dens > 0.6 && roll < 0.14) what = 'MSS'[Math.floor(r * 3)];
      else if (t === 'clearing' && dens > 0.35 && roll >= 0.14 && roll < 0.34) what = 'H';
      else if ((t === 'plain' || t === 'hills') && dens > 0.68 && roll < 0.06) what = 'SM'[Math.floor(r * 2)];
      else if ((t === 'plain' || t === 'hills') && dens > 0.5 && roll >= 0.06 && roll < 0.16) what = 'H';
      else if (t === 'rock' && roll < 0.3) what = 'B';
      else if (t === 'hills' && roll >= 0.16 && roll < 0.24) what = 'B';
      else if (t === 'marsh' && roll < (laden ? 0.45 : 1) && dens > 0.25) what = 'R'; // snow buries reeds
      if (what) found.push([ay, ax, what, laden]);
    }
    found.sort((a, b) => a[0] - b[0] || a[1] - b[1]);
    for (const [y, x, what, laden] of found) {
      let name, shadow;
      if ('LMS'.includes(what)) { name = `spruce_${what.toLowerCase()}`; shadow = true; }
      else if (what === 'B') { name = 'boulder'; shadow = false; }
      else if (what === 'H') { name = 'shrub'; shadow = !laden; }
      else { name = `reed_${2 + (x * 7 + y * 3) % 3}`; shadow = false; }
      const sp = texture(spriteArt(name, pal, laden)), shadowTones = laden ? [C.s4, C.s3] : [SHADOW[pal]];
      if (shadow) for (let xx = 0; xx < Math.floor(sp.w / 2) + 2; xx++) shadowTones.forEach((tone, yy) => {
        const sx = x + xx - 1, sy = y + yy;
        if (sx >= 0 && sx < MW && sy >= 0 && sy < MH && sx >= X0 && sx < X1 && sy >= Y0 && sy < Y1 && !blocked(sx, sy)) put(sx, sy, tone);
      });
      const left = x - Math.floor(sp.w / 2), top = y - sp.h + 1;
      for (let j = 0; j < sp.h; j++) for (let i = 0; i < sp.w; i++) {
        const k = (j * sp.w + i) * 4, sx = left + i, sy = top + j;
        if (sp.data[k + 3] && sx >= 0 && sx < MW && sy >= 0 && sy < MH) put(sx, sy, [sp.data[k], sp.data[k + 1], sp.data[k + 2]]);
      }
    }
  }

  return { bakeCell, bakeSprites, classify };
}

// ---------------------------------------------------------------- one cell on its own
/**
 * How far a cell's finished pixels reach for their inputs: sprites anchored in the next cell read
 * that cell's 5x5 neighbourhood, so terrain three cells away and ground state two cells away.
 */
export const REACH = 3, GROUND_REACH = 2;
const SIDE = 2 * REACH + 1;

/** fn(x, y) for every cell within REACH of (cx, cy), row by row, clamped to the map as the baker clamps. */
export function around(width, height, cx, cy, fn) {
  const out = new Array(SIDE * SIDE);
  for (let oy = -REACH; oy <= REACH; oy++) for (let ox = -REACH; ox <= REACH; ox++)
    out[(oy + REACH) * SIDE + ox + REACH] = fn(Math.min(width - 1, Math.max(0, cx + ox)), Math.min(height - 1, Math.max(0, cy + oy)));
  return out;
}

/**
 * The finished pixels (ground and sprites, RGBA, CELL x CELL) of cell (cx, cy), from its
 * neighbourhood alone: the same pixels as baking the whole map, so a cell can be baked anywhere
 * (a worker) and kept until its neighbourhood changes.
 * job: { width, height, cx, cy, world, terrain, states, pieces } with terrain and states from around(),
 * pieces from piecesNear().
 */
export function bakeAlone({ width, height, cx, cy, world, terrain, states, pieces = [] }, tex) {
  const look = (arr, x, y) => {
    const dx = x - cx + REACH, dy = y - cy + REACH;
    if (dx < 0 || dy < 0 || dx >= SIDE || dy >= SIDE) throw new Error(`cell (${cx}, ${cy}) read (${x}, ${y}), beyond its reach`);
    return arr[dy * SIDE + dx];
  };
  const baker = createBaker({ width, height, terrainAt: (x, y) => look(terrain, x, y), pieces }, (x, y) => look(states, x, y), world, tex);
  const out = new Uint8ClampedArray(CELL * CELL * 4);
  baker.bakeCell(out, cx, cy, cx + 1, cx, cy);
  baker.bakeSprites(out, cx, cy, cx + 1, cy + 1);
  return out;
}
