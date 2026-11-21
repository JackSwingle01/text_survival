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
//   - snow: a dusting, then patches lingering in shade and hollows, then full cover shown through a
//     world-noise mask, so cover is continuous;
//   - mud, and ice on wet land: their textures through a world-noise mask that grows with how wet the
//     cell is; lake ice covers the whole water cell;
//   - sprites: trees, shrubs, boulders and reeds on a jittered world grid.
// A pixel needs only its cell's 5x5 neighbourhood, so the map can be baked a cell at a time.
// Unknown neighbours are read as the cell's own terrain: the renderer never shows what the
// player has not seen.
//
// Ground state per cell (stateAt) and for the world (world) select what is drawn; ground-state.js
// derives both from the view:
//   cell:  { snow: 0..1 cover, dust: bool, laden: bool (plants carry snow), mud: 0..1 (wet ground;
//            frozen where ice is not 'none'), ice: 'none' | 'thin' | 'thick' }
//   world: { palette: 'winter' | 'summer' | 'autumn', snowline: 0..1 mountain height that holds snow }
// The palette sets colour only; every snow, mud and ice layer follows the cell.

export const CELL = 48, SRC = 96, VARIANTS = 4, STEP = 6;
export const WINTER = Object.freeze({ snow: 1, dust: false, laden: true, mud: 0, ice: 'thick' });
export const WINTER_WORLD = Object.freeze({ palette: 'winter', snowline: 0.34 });

// ---------------------------------------------------------------- the art that exists
/**
 * The layers and palettes scripts/build-terrain-layers.py paints. tools/terrain.test.mjs fails when
 * this list and assets/icons/terrain disagree.
 */
export const PAINTED = Object.freeze({
  palettes: ['winter', 'summer', 'autumn'],
  layers: ['ground', 'mud', 'snow', 'ice', 'sprites'],
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
const FLAVOUR = { hills: 'rugged', rock: 'crag' };
const hex = s => [parseInt(s.slice(1, 3), 16), parseInt(s.slice(3, 5), 16), parseInt(s.slice(5, 7), 16)];
const C = Object.fromEntries(Object.entries({
  s0: '#f4f6f4', s1: '#e3eaec', s2: '#ccd7dd', s3: '#b0c0ca', s4: '#8fa3b2', s5: '#6c8193',
  r0: '#b6b8b5', r1: '#8a8d8d', r2: '#646a6e', r3: '#454b52', r4: '#2b3037',
  e3: '#463a2c', sc: '#7d7a6b', sh: '#323d2b',
}).map(([k, v]) => [k, hex(v)]));
const SNOW = ['s0', 's1', 's2', 's3', 's4', 's5'].map(k => C[k]);
const ROCK = ['r0', 'r1', 'r2', 'r3', 'r4'].map(k => C[k]);

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
/** Which of `count` texture variants a 96px source square uses (same hash as the old TileRenderer). */
export function variant(sx, sy, count) {
  let h = Math.imul(sx, 73856093) ^ Math.imul(sy, 19349663);
  h ^= h >> 13; h = Math.imul(h, 1274126177); h ^= h >> 16;
  return (h >>> 0) % count;
}

// ---------------------------------------------------------------- textures
/** Art names a world state draws from. */
export function wantedArt(world) {
  const p = world.palette, names = [];
  for (let n = 1; n <= VARIANTS; n++) {
    for (const t of ['forest', 'clearing', 'plain', 'hills', 'rock', 'marsh', 'water', 'deepwater']) names.push(`terrain/ground/${t}_${n}_${p}`);
    for (const level of ['dust', 'patchy', 'full']) for (const f of ['flat', 'rugged', 'crag']) names.push(`terrain/snow/${level}_${f}_${n}`);
    for (const level of ['thin', 'thick']) names.push(`terrain/ice/${level}_${n}`);
    names.push(`terrain/mud_${n}_${p}`);
  }
  for (let k = 0; k < 8; k++) names.push(`cavefloor_tile${k === 0 ? '' : k + 1}`);
  for (const s of SPRITE_NAMES) for (const laden of [true, false]) names.push(spriteArt(s, p, laden));
  return names;
}
/** Texture files a world state draws from. */
export const textureNames = world => [...new Set(wantedArt(world).map(n => resolveArt(n)))];
const SPRITE_NAMES = ['spruce_l', 'spruce_m', 'spruce_s', 'shrub', 'boulder', 'reed_2', 'reed_3', 'reed_4'];
/**
 * The winter sprites are painted carrying snow, so a laden plant draws them in any season; a bare one
 * draws its palette's, and bare in a winter palette is the dead autumn look.
 */
const BARE_SPRITES = { winter: 'autumn', summer: 'summer', autumn: 'autumn' };
const spriteArt = (name, palette, laden) => `terrain/sprites/${name}_${laden ? 'winter' : BARE_SPRITES[palette]}`;

// ---------------------------------------------------------------- the map
/**
 * map: { width, height, terrainAt(x, y) -> terrain name, or null where unknown }.
 * stateAt(x, y) -> cell state; world -> world state; tex(name) -> { w, h, data } (RGBA).
 */
export function createBaker(map, stateAt, world, tex) {
  const { width: W, height: H } = map;
  const cells = new Map();

  /** A cell's 5x5 neighbourhood with unknown terrain read as the cell's own. */
  function cell(cx, cy) {
    const key = cy * W + cx;
    let c = cells.get(key);
    if (c) return c;
    const own = map.terrainAt(cx, cy);
    const nb = new Array(25);
    let wet = false, mtn = false;
    for (let oy = -2; oy <= 2; oy++) for (let ox = -2; ox <= 2; ox++) {
      const x = Math.min(W - 1, Math.max(0, cx + ox)), y = Math.min(H - 1, Math.max(0, cy + oy));
      const t = map.terrainAt(x, y) ?? own;
      nb[(oy + 2) * 5 + ox + 2] = t;
      if (WET(t)) wet = true;
      if (t === 'mountain') mtn = true;
    }
    c = { own, nb, wet, mtn, state: own ? stateAt(cx, cy) : null };
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
      if (at(c, ox, oy) === 'mountain') acc += k;
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

  /** What is at a pixel: { kind, water, bank, mountain, h, h1, h2 }, or null in an unknown cell. */
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

    let water = false, bank = false, kind = pick;
    // The meandered position relative to the cell centre, shared by water and the land fallback.
    const mx = (vnoise(px, py, 40, 11) - 0.5) * 0.24 + (vnoise(px, py, 13, 12) - 0.5) * 0.06;
    const my = (vnoise(px, py, 40, 13) - 0.5) * 0.24 + (vnoise(px, py, 13, 14) - 0.5) * 0.06;
    const qx = fxc - cx - 0.5 + mx, qy = fyc - cy - 0.5 + my;
    if (c.wet) {
      const w5 = (ox, oy) => WET(at(c, ox, oy));
      const wobble = (vnoise(px, py, 30, 15) - 0.5) * 0.08;
      let sdist = 9, wtype = 'water', kb = 0, kn = 1e-6, kbest = 0, btype = 'deepwater';
      for (let coy = -1; coy <= 1; coy++) for (let cox = -1; cox <= 1; cox++) {
        const here = w5(cox, coy);
        let body = false;
        if (here) for (const bx of [-1, 0]) for (const by of [-1, 0])
          if (w5(cox + bx, coy + by) && w5(cox + bx + 1, coy + by) && w5(cox + bx, coy + by + 1) && w5(cox + bx + 1, coy + by + 1)) body = true;
        const rx = qx - cox, ry = qy - coy;
        const kc = Math.max(0, Math.min(1, 1 - (rx * rx + ry * ry) / 1.5)) ** 2;
        if (body) kb += kc;
        kn += kc;
        if (body && kc > kbest) { kbest = kc; btype = at(c, cox, coy); }
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
        if (s < sdist) { sdist = s; wtype = kindC; }
      }
      const lake = kb / kn + (vnoise(px, py, 20, 16) - 0.5) * 0.25 + (vnoise(px, py, 6, 17) - 0.5) * 0.08;
      const sLake = (0.5 - lake) * 0.7;
      if (sLake < sdist) { wtype = btype; sdist = sLake; }
      water = sdist < 0;
      bank = sdist >= 0 && sdist < 2.5 / CELL;
      if (water) kind = wtype;
    }
    if (!water && WET(pick)) {
      // Land the lookup gave to a water cell belongs to the nearest land cell instead.
      let best = 9; kind = null;
      for (let oy = -1; oy <= 1; oy++) for (let ox = -1; ox <= 1; ox++) {
        const t = at(c, ox, oy), d = Math.hypot(qx - ox, qy - oy);
        if (!WET(t) && d < best) { best = d; kind = t; }
      }
      kind ??= 'marsh'; // a cell wholly surrounded by water
    }

    let mountain = false, h = 0, h1 = 0, h2 = 0;
    if (c.mtn) {
      let m;
      [h, m] = height(c, cx, cy, fxc, fyc);
      mountain = m > 0.375 && m + (vnoise(px, py, 14, 24) - 0.5) * 0.25 > 0.5;
      if (mountain) { h1 = height(c, cx, cy, fxc + 1 / CELL, fyc)[0]; h2 = height(c, cx, cy, fxc, fyc + 1 / CELL)[0]; }
      if (kind === 'mountain' && !mountain) {
        let best = 9; kind = 'plain';
        for (let oy = -1; oy <= 1; oy++) for (let ox = -1; ox <= 1; ox++) {
          const t = at(c, ox, oy), d = Math.hypot(qx - ox, qy - oy);
          if (t !== 'mountain' && !WET(t) && d < best) { best = d; kind = t; }
        }
      }
    }
    return { kind, water, bank, mountain: mountain && !water, h, h1, h2, state: c.state };
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
    if (p.mountain) {
      const shade = Math.max(-1.4, Math.min(1.4, ((p.h1 - p.h) + (p.h2 - p.h)) * 40));
      let rock = ramp(ROCK, 2.1 - shade * 2.2 + (vnoise(px, py, 9, 25) - 0.5) * 0.4, px, py, 1);
      const line = world.snowline + (vnoise(px, py, 16, 26) - 0.5) * 0.12;
      const holds = p.h > line && shade > -0.35 + (p.h - line) * 1.5; // steep shaded faces shed snow
      const band = Math.floor(p.h * 9) !== Math.floor(p.h1 * 9) || Math.floor(p.h * 9) !== Math.floor(p.h2 * 9);
      if (band && shade < -0.15 && vnoise(px, py, 12, 29) > 0.45) rock = C.r4;
      if (holds) return ramp(SNOW, 1.2 - shade * 1.7, px, py, 2);
      if (p.h < 0.2 && hash01(px, py, 6) < 0.25) return snowed(px, py, s.snow) ? C.s3 : C.sc; // scree
      return rock;
    }
    if (WET(p.kind)) {
      let col = sample(`terrain/ground/${p.kind}_${n}_${pal}`, px, py);
      if (s.ice === 'none') return col;
      col = sample(`terrain/ice/${s.ice}_${n}`, px, py) ?? col;
      // Wind scours lake ice, so any snow the cell holds lies on it only as a dusting.
      if (s.dust || s.snow > 0) col = sample(`terrain/snow/dust_flat_${n}`, px, py) ?? col;
      return col;
    }
    if (p.kind === 'cavefloor') { // caves keep the old per-tile art: they have no seasons
      const k = variant(Math.floor(px / SRC), Math.floor(py / SRC), 8);
      return sample(`cavefloor_tile${k === 0 ? '' : k + 1}`, px, py);
    }
    let col = sample(`terrain/ground/${p.kind}_${n}_${pal}`, px, py);
    const flav = FLAVOUR[p.kind] ?? 'flat', lies = snowed(px, py, s.snow);
    // Wet ground, and where its water has frozen, ice. The mask's noise lies almost wholly within
    // 0.15..0.85, so its threshold spans that range: the least wetness shows, and a soaked cell shows
    // all of the (itself sparse) mud art.
    const wet = vnoise(px, py, 34, 41) * 0.65 + vnoise(px, py, 11, 42) * 0.35;
    if (s.mud > 0 && !lies && wet > 0.85 - s.mud * 0.7)
      col = sample(s.ice === 'none' ? `terrain/mud_${n}_${pal}` : `terrain/ice/${s.ice}_${n}`, px, py) ?? col;
    if (s.dust) col = sample(`terrain/snow/dust_${flav}_${n}`, px, py) ?? col;
    if (s.snow > 0 && !lies) col = sample(`terrain/snow/patchy_${flav}_${n}`, px, py) ?? col;
    if (lies) col = sample(`terrain/snow/full_${flav}_${n}`, px, py) ?? col;
    if (p.bank && hash01(px, py, 5) < (lies ? 0.8 : 0.75)) col = lies ? C.s4 : C.e3;
    return col;
  }

  // One world mask decides where full cover lies; more snow lowers its threshold.
  const snowField = (px, py) => vnoise(px, py, 60, 31) * 0.55 + vnoise(px, py, 22, 32) * 0.3 + vnoise(px, py, 7, 33) * 0.15;
  const snowed = (px, py, cover) => cover >= 1 || (cover > 0 && snowField(px, py) > 1 - cover);

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
    const blocked = (x, y) => { const p = classify(x, y); return !p || p.water || p.mountain; };
    const found = [];
    // Any anchor whose sprite (up to 12 rows tall, 9 wide, a 2-row shadow) can reach this area.
    const gx0 = Math.max(0, Math.floor((X0 - 8) / STEP)), gx1 = Math.min(MW / STEP - 1, Math.floor((X1 + 8) / STEP));
    const gy0 = Math.max(0, Math.floor((Y0 - 4) / STEP)), gy1 = Math.min(MH / STEP - 1, Math.floor((Y1 + 14) / STEP));
    for (let gy = gy0; gy <= gy1; gy++) for (let gx = gx0; gx <= gx1; gx++) {
      const ax = gx * STEP + Math.floor(lattice(gx, gy, 101) * STEP), ay = gy * STEP + Math.floor(lattice(gx, gy, 102) * STEP);
      const roll = lattice(gx, gy, 103), r = lattice(gx, gy, 104);
      const dens = vnoise(ax, ay, 40, 105) * 0.7 + vnoise(ax, ay, 12, 106) * 0.3;
      const p = classify(ax, ay);
      if (!p || p.water || p.mountain) continue;
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
      const sp = texture(spriteArt(name, pal, laden)), shadowTones = laden ? [C.s4, C.s3] : [C.sh];
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
