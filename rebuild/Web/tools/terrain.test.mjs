// The map's ground follows the Sim's ground, not the calendar: run with
//   node --test tools/terrain.test.mjs
// Oracles are the brief's (0 snow never draws snow, deep snow never draws bare) and the art on disk.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBaker, wantedArt, resolveArt, ice, snowField, PAINTED, CELL, SRC, VARIANTS, variant } from '../App/wwwroot/terrain-core.js';
import { cellState, worldState, createGround, GROUND, SEASONS, BARE } from '../App/wwwroot/ground-state.js';
import { decodePng } from './png.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const icons = path.resolve(here, '../../../assets/icons');
const fixture = JSON.parse(fs.readFileSync(path.join(here, 'fixtures/seed17.json'), 'utf8'));
const CODES = { forest: 'F', clearing: 'C', plain: 'P', hills: 'H', water: 'W', marsh: 'M', rock: 'R', mountain: 'X', deepwater: 'D', cavefloor: 'c' };
const TERRAIN = Object.fromEntries(Object.entries(CODES).map(([k, v]) => [v, k]));

const textures = new Map();
const tex = name => {
  if (!textures.has(name)) textures.set(name, decodePng(path.join(icons, `${name}.png`)));
  return textures.get(name);
};
function sampleOf(name, px, py) {
  const t = tex(resolveArt(name)), sx = Math.floor(px / SRC), sy = Math.floor(py / SRC);
  const x = Math.floor((px - sx * SRC) * t.w / SRC), y = Math.floor((py - sy * SRC) * t.h / SRC), i = (y * t.w + x) * 4;
  return t.data[i + 3] ? [t.data[i], t.data[i + 1], t.data[i + 2]] : null;
}

const WET = t => t === 'water' || t === 'deepwater';

/** The fixture as a MapDto with every tile in sight, `surfaceOf(x, y)` as its ground, and its lake and deep-water tiles ice when `frozen`. */
function mapDto(surfaceOf, frozen = false) {
  const { W, H } = fixture, surface = [], frozenWater = [];
  let known = '';
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    known += CODES[fixture.terrain[y][x]];
    const s = surfaceOf(x, y);
    if (s) surface.push({ i: y * W + x, ...s });
    if (frozen && WET(fixture.terrain[y][x])) frozenWater.push(y * W + x);
  }
  return { width: W, height: H, known, visible: [...Array(W * H).keys()], surface, frozenWater };
}

/** Land cells far enough from water and mountains that every pixel is the cell's own ground. */
function openLand(terrain) {
  const out = [];
  for (let y = 2; y < fixture.H - 2; y++) for (let x = 2; x < fixture.W - 2; x++) {
    let ok = true;
    for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) if (fixture.terrain[y + dy][x + dx] !== terrain) ok = false;
    if (ok) out.push([x, y]);
  }
  return out;
}

/** Per pixel of cell (cx, cy) baked from `m`: whether it is any snow layer, the bare ground, or neither. */
function layers(m, cx, cy, world) {
  const ground = createGround(c => TERRAIN[c]);
  ground.see(m);
  const baker = createBaker({ width: m.width, height: m.height, terrainAt: (x, y) => TERRAIN[m.known[y * m.width + x]] },
    ground.stateAt, world, n => tex(n));
  const out = new Uint8ClampedArray(CELL * CELL * 4);
  baker.bakeCell(out, cx, cy, cx + 1, cx, cy);
  const terrain = TERRAIN[m.known[cy * m.width + cx]], flav = { hills: 'rugged', rock: 'crag' }[terrain] ?? 'flat';
  let snow = 0, bare = 0;
  for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
    const px = cx * CELL + x, py = cy * CELL + y, i = (y * CELL + x) * 4, got = [out[i], out[i + 1], out[i + 2]];
    const n = variant(Math.floor(px / SRC), Math.floor(py / SRC), VARIANTS) + 1;
    const g = sampleOf(`terrain/ground/${terrain}_${n}_${world.palette}`, px, py), isBare = g && String(g) === String(got);
    const isSnow = ['full', 'patchy', 'dust'].some(level => {
      const s = sampleOf(`terrain/snow/${level}_${flav}_${n}`, px, py);
      return s && String(s) === String(got);
    });
    if (isSnow && !isBare) snow++;
    if (isBare && !isSnow) bare++;
  }
  return { snow, bare, pixels: CELL * CELL };
}

/** Of the pixels of cell (cx, cy) where `paint(n, px, py)` gives a colour that differs from the bare ground, the share baked in it. */
function shownShare(m, cx, cy, world, paint) {
  const ground = createGround(c => TERRAIN[c]);
  ground.see(m);
  const baker = createBaker({ width: m.width, height: m.height, terrainAt: (x, y) => TERRAIN[m.known[y * m.width + x]] },
    ground.stateAt, world, n => tex(n));
  const out = new Uint8ClampedArray(CELL * CELL * 4), terrain = TERRAIN[m.known[cy * m.width + cx]];
  baker.bakeCell(out, cx, cy, cx + 1, cx, cy);
  let shown = 0, total = 0;
  for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
    const px = cx * CELL + x, py = cy * CELL + y, i = (y * CELL + x) * 4;
    const n = variant(Math.floor(px / SRC), Math.floor(py / SRC), VARIANTS) + 1;
    const a = paint(n, px, py), g = sampleOf(`terrain/ground/${terrain}_${n}_${world.palette}`, px, py);
    if (!a || String(a) === String(g)) continue;
    total++;
    if (String(a) === String([out[i], out[i + 1], out[i + 2]])) shown++;
  }
  return total ? shown / total : 0;
}

const dry = { snowCm: 0, crust: false, mudLevel: 0, waterCm: 0, ice: false };
const deep = { ...dry, snowCm: 40, crust: true };
const WINTER_WORLD = worldState({ season: 'Winter' }), SUMMER_WORLD = worldState({ season: 'Summer' });

test('a tile with no snow draws no snow layer, in winter colours too', () => {
  const cells = openLand('plain').concat(openLand('forest')).slice(0, 6);
  assert.ok(cells.length >= 2, 'fixture has open land');
  for (const world of [WINTER_WORLD, SUMMER_WORLD]) for (const [x, y] of cells) {
    const r = layers(mapDto(() => dry), x, y, world);
    assert.equal(r.snow, 0, `cell ${x},${y} with 0 cm of snow drew ${r.snow} snow pixels (${world.palette})`);
    assert.ok(r.bare > r.pixels * 0.5, `cell ${x},${y} with 0 cm of snow drew bare ground on only ${r.bare} pixels`);
  }
});

test('a tile under deep snow draws the snow layer, never bare ground, in summer colours too', () => {
  const cells = openLand('plain').concat(openLand('forest')).slice(0, 6);
  for (const world of [WINTER_WORLD, SUMMER_WORLD]) for (const [x, y] of cells) {
    const r = layers(mapDto(() => deep), x, y, world);
    assert.equal(r.bare, 0, `cell ${x},${y} under 40 cm of snow drew ${r.bare} bare pixels (${world.palette})`);
    assert.ok(r.snow > r.pixels * 0.9, `cell ${x},${y} under 40 cm of snow drew snow on only ${r.snow} pixels`);
  }
});

const art = name => (n, px, py) => sampleOf(name(n), px, py);

/**
 * A map of one terrain, `width` x `height` cells, with `snowCmAt(cx)` of snow down each column of
 * cells, baked. For each snow art, the pixels where it can be told from every other layer: per pixel
 * column the share of them that show it, and in all the count that show it.
 */
function snowArt(terrain, width, height, snowCmAt, world = WINTER_WORLD) {
  const baker = createBaker({ width, height, terrainAt: () => terrain },
    cx => cellState({ ...dry, snowCm: snowCmAt(cx) }, terrain), world, n => tex(n));
  const PW = width * CELL, out = new Uint8ClampedArray(PW * height * CELL * 4), flav = { hills: 'rugged', rock: 'crag' }[terrain] ?? 'flat';
  for (let cy = 0; cy < height; cy++) for (let cx = 0; cx < width; cx++) baker.bakeCell(out, 0, 0, width, cx, cy);
  const ARTS = ['full', 'patchy', 'dust'], shown = { full: [], patchy: [], dust: [] }, counts = { full: 0, patchy: 0, dust: 0, ground: 0 };
  let blind = 0;
  for (let px = 0; px < PW; px++) {
    const c = { full: [0, 0], patchy: [0, 0], dust: [0, 0] };
    for (let py = 0; py < height * CELL; py++) {
      const n = variant(Math.floor(px / SRC), Math.floor(py / SRC), VARIANTS) + 1, i = (py * PW + px) * 4;
      const got = String([out[i], out[i + 1], out[i + 2]]), at = name => String(sampleOf(name, px, py));
      const s = { full: at(`terrain/snow/full_${flav}_${n}`), patchy: at(`terrain/snow/patchy_${flav}_${n}`),
        dust: at(`terrain/snow/dust_${flav}_${n}`), ground: at(`terrain/ground/${terrain}_${n}_${world.palette}`) };
      if (got === s.ground && !['full', 'patchy', 'dust'].some(k => s[k] === got)) counts.ground++;
      for (const k of ARTS) {
        if (s[k] === 'null' || Object.entries(s).some(([o, v]) => o !== k && v === s[k])) continue;
        c[k][1]++;
        if (got === s[k]) { c[k][0]++; counts[k]++; }
      }
    }
    for (const k of ARTS) {
      if (!c[k][1]) blind++;
      shown[k].push(c[k][1] ? c[k][0] / c[k][1] : shown[k].at(-1) ?? 0);
    }
  }
  assert.ok(blind < PW * ARTS.length * 0.02, `${blind} pixel columns have no pixel where some snow art can be told apart`);
  return { shown, counts };
}

test('thin snow draws the patchy art with ground between, and no full cover', () => {
  for (const terrain of ['plain', 'hills', 'rock']) for (const cm of [6, 8, 9.5]) {
    const { counts } = snowArt(terrain, 4, 4, () => cm);
    assert.ok(counts.patchy > 0, `${cm} cm on ${terrain} drew no patchy art`);
    assert.ok(counts.ground > 0, `${cm} cm on ${terrain} showed no ground between the patches`);
    assert.equal(counts.full, 0, `${cm} cm on ${terrain} drew ${counts.full} pixels of full cover`);
  }
});

test('deep snow draws full cover and no patches, and closes over them only at full depth', () => {
  for (const terrain of ['plain', 'hills', 'rock']) {
    for (const cm of [GROUND.fullCm, 20, 40]) {
      const { counts } = snowArt(terrain, 4, 4, () => cm);
      assert.ok(counts.full > 0, `${cm} cm on ${terrain} drew no full cover`);
      assert.equal(counts.patchy, 0, `${cm} cm on ${terrain} drew ${counts.patchy} pixels of patchy art`);
    }
    const { counts } = snowArt(terrain, 4, 4, () => GROUND.fullCm - 1);
    assert.ok(counts.full > 0 && counts.patchy > 0, `${GROUND.fullCm - 1} cm on ${terrain}: full ${counts.full}, patches ${counts.patchy}`);
  }
});

test('the dusting shows from a dusting\'s depth until patches cover it', () => {
  for (const terrain of ['plain', 'hills', 'rock']) {
    for (const cm of [(GROUND.dustCm + GROUND.patchyCm) / 2, GROUND.patchyCm, (GROUND.patchyCm + GROUND.patchedCm) / 2])
      assert.ok(snowArt(terrain, 4, 4, () => cm).counts.dust > 0, `no dusting at ${cm} cm on ${terrain}`);
    for (const cm of [GROUND.dustCm / 2, GROUND.patchedCm, (GROUND.patchedCm + GROUND.fullCm) / 2, GROUND.fullCm + 5]) {
      const { counts } = snowArt(terrain, 4, 4, () => cm);
      assert.equal(counts.dust, 0, `${cm} cm on ${terrain} drew ${counts.dust} pixels of dusting`);
    }
  }
});

test('the snow mask\'s noise lies almost wholly within the range its thresholds span', () => {
  let low = 0, high = 0, n = 0;
  for (let py = 0; py < 3000; py += 7) for (let px = 0; px < 3000; px += 7, n++) {
    const v = snowField(px, py);
    if (v < 0.2) low++;
    if (v > 0.8) high++;
  }
  assert.ok(low / n < 0.02 && high / n < 0.02, `${(low / n * 100).toFixed(1)}% below 0.2, ${(high / n * 100).toFixed(1)}% above 0.8`);
});

test('bare plants take their season\'s palette', () => {
  const files = palette => fs.readdirSync(path.join(icons, 'terrain/sprites')).filter(f => f.endsWith(`_${palette}.png`));
  const colours = palette => new Set(files(palette).flatMap(f => {
    const t = decodePng(path.join(icons, 'terrain/sprites', f)), out = [];
    for (let i = 0; i < t.data.length; i += 4) if (t.data[i + 3]) out.push(String([t.data[i], t.data[i + 1], t.data[i + 2]]));
    return out;
  }));
  const summer = colours('summer'), autumn = colours('autumn');
  const only = (a, b) => new Set([...a].filter(c => !b.has(c)));
  const own = { summer: only(summer, autumn), autumn: only(autumn, summer) };
  // Spruce needles are the same in every palette; shrubs and reeds are not.
  const width = 8, height = 8, map = { width, height, terrainAt: x => x < width / 2 ? 'clearing' : 'marsh' };
  for (const season of Object.keys(SEASONS)) {
    const world = worldState({ season }), bare = cellState(dry, 'clearing');
    const baker = createBaker(map, () => bare, world, n => tex(n)), out = new Uint8ClampedArray(width * CELL * height * CELL * 4);
    baker.bakeSprites(out, 0, 0, width, height);
    const seen = { summer: 0, autumn: 0 };
    for (let i = 0; i < out.length; i += 4) if (out[i + 3]) for (const p of ['summer', 'autumn']) if (own[p].has(String([out[i], out[i + 1], out[i + 2]]))) seen[p]++;
    const want = world.palette === 'summer' ? 'summer' : 'autumn', other = want === 'summer' ? 'autumn' : 'summer';
    assert.ok(seen[want] > 0 && seen[other] === 0, `${season} plants: ${seen.summer} summer pixels, ${seen.autumn} autumn`);
  }
});

test('snow deepening from cell to cell shows no seam at the cells\' edges', () => {
  // A centimetre more snow in each cell, through patches into full cover. A seam is a step in the art
  // shown across a cell edge larger than any across the middle of a cell. Only odd edges are compared:
  // at even ones the art's textures change variant, which changes the art shown with or without snow.
  const width = 11, k = CELL / 4, lo = GROUND.patchyCm, hi = GROUND.fullCm;
  const { shown } = snowArt('plain', width, 48, cx => lo + (hi - lo) * cx / (width - 1));
  for (const art of ['full', 'patchy']) {
    const a = shown[art], band = (x0, x1) => a.slice(x0, x1).reduce((u, v) => u + v, 0) / (x1 - x0);
    const step = x => Math.abs(band(x - k, x) - band(x, x + k));
    let within = 0;
    for (let cx = 0; cx < width; cx++) within = Math.max(within, step(cx * CELL + CELL / 2));
    for (let cx = 1; cx < width; cx += 2)
      assert.ok(step(cx * CELL) <= within * 1.25, `${art} art steps ${step(cx * CELL).toFixed(2)} at the edge of cell ${cx}, against at most ${within.toFixed(2)} inside a cell`);
  }
});

test('dust, patches, mud and pond ice each draw their own art', () => {
  const [x, y] = openLand('plain').concat(openLand('forest'))[0];
  const cases = [
    ['dusting', { ...dry, snowCm: 2 }, art(n => `terrain/snow/dust_flat_${n}`), 0.95],
    ['patchy snow', { ...dry, snowCm: GROUND.patchedCm }, art(n => `terrain/snow/patchy_flat_${n}`), 0.8],
    ['soaked ground', { ...dry, mudLevel: 1 }, art(n => `terrain/mud_${n}_summer`), 0.95],
    ['thick pond ice', { ...dry, waterCm: 5, ice: true }, (n, px, py) => ice(px, py, 'thick'), 0.95],
    ['thin ice on a flooded cell', { ...dry, waterCm: GROUND.thickIceCm - 0.1, ice: true }, (n, px, py) => ice(px, py, 'thin'), 0.95],
  ];
  // Every cell carries the surface: a pixel shows the state of the cell whose terrain it shows.
  for (const [what, surface, paint, share] of cases) {
    const got = shownShare(mapDto(() => surface), x, y, SUMMER_WORLD, paint);
    assert.ok(got >= share, `${what} showed only ${(got * 100).toFixed(0)}% of its art`);
  }
});

test('saturated mud under a thin frozen film draws mostly mud, not a sheet of ice', () => {
  const [x, y] = openLand('plain').concat(openLand('forest'))[0];
  const m = mapDto(() => ({ ...dry, mudLevel: 1, waterCm: 1, ice: true }));
  const film = shownShare(m, x, y, SUMMER_WORLD, (n, px, py) => ice(px, py, 'thin')), mud = shownShare(m, x, y, SUMMER_WORLD, art(n => `terrain/mud_${n}_summer`));
  assert.ok(mud > 0.6, `the soaked ground showed only ${(mud * 100).toFixed(0)}% of its mud`);
  assert.ok(film > 0 && film < 0.4, `a 1 cm frozen film covered ${(film * 100).toFixed(0)}% of the ice`);
});

test('ice is painted pixel for pixel as the reference painter paints it', () => {
  // fixtures/ice-<level>.png: dev/prototypes/art/seasons/world.py's ice() on worktree-short-loop-plan, over
  // world pixels (2000, 1100) to (2128, 1228); clear where open water shows.
  for (const level of ['thin', 'thick']) {
    const ref = decodePng(path.join(here, `fixtures/ice-${level}.png`));
    for (let y = 0; y < ref.h; y++) for (let x = 0; x < ref.w; x++) {
      const i = (y * ref.w + x) * 4, want = ref.data[i + 3] ? String(ref.data.subarray(i, i + 3)) : 'null';
      const got = ice(2000 + x, 1100 + y, level);
      assert.equal(got ? String(got) : 'null', want, `${level} ice at (${2000 + x}, ${1100 + y})`);
    }
  }
});

test('a lake reaching into a land cell freezes with the lake', () => {
  const m = mapDto(() => null, true), ground = createGround(c => TERRAIN[c]);
  ground.see(m);
  const baker = createBaker({ width: m.width, height: m.height, terrainAt: (x, y) => TERRAIN[m.known[y * m.width + x]] },
    ground.stateAt, WINTER_WORLD, n => tex(n));
  const out = new Uint8ClampedArray(CELL * CELL * 4);
  let shore = 0;
  for (let cy = 0; cy < fixture.H; cy++) for (let cx = 0; cx < fixture.W; cx++) {
    if (['water', 'deepwater', 'mountain'].includes(fixture.terrain[cy][cx])) continue;
    baker.bakeCell(out, cx, cy, cx + 1, cx, cy);
    for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
      const px = cx * CELL + x, py = cy * CELL + y, p = baker.classify(px, py), want = ice(px, py, 'thick');
      if (!p.water || !want) continue;
      shore++;
      const i = (y * CELL + x) * 4;
      assert.equal(String(out.subarray(i, i + 3)), String(want), `open water at (${px}, ${py}) in a ${fixture.terrain[cy][cx]} cell beside a frozen lake`);
    }
  }
  assert.ok(shore > 100, `fixture has ${shore} lake pixels in land cells`);
});

test('the map draws the lakes and rivers the view calls frozen as ice, and the rest as open water', () => {
  // The Sim's answer, not the season's: the world state is the same summer in both.
  const thick = (px, py) => ice(px, py, 'thick');
  const lakePixels = m => {
    const ground = createGround(c => TERRAIN[c]);
    ground.see(m);
    const baker = createBaker({ width: m.width, height: m.height, terrainAt: (x, y) => TERRAIN[m.known[y * m.width + x]] },
      ground.stateAt, SUMMER_WORLD, n => tex(n));
    const out = new Uint8ClampedArray(CELL * CELL * 4);
    let ice = 0, open = 0;
    for (let cy = 0; cy < fixture.H; cy++) for (let cx = 0; cx < fixture.W; cx++) {
      if (!WET(fixture.terrain[cy][cx])) continue;
      baker.bakeCell(out, cx, cy, cx + 1, cx, cy);
      for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
        const px = cx * CELL + x, py = cy * CELL + y, p = baker.classify(px, py), want = thick(px, py);
        if (!p.water || !want) continue;
        const i = (y * CELL + x) * 4, n = variant(Math.floor(px / SRC), Math.floor(py / SRC), VARIANTS) + 1;
        const got = String(out.subarray(i, i + 3)), water = sampleOf(`terrain/ground/${p.kind}_${n}_${SUMMER_WORLD.palette}`, px, py);
        if (got === String(want)) ice++;
        else if (water && got === String(water)) open++;
      }
    }
    return { ice, open };
  };
  const winter = lakePixels(mapDto(() => null, true)), summer = lakePixels(mapDto(() => null, false));
  assert.ok(winter.ice > 1000 && winter.open === 0, `frozen: ${winter.ice} ice pixels, ${winter.open} open`);
  assert.ok(summer.open > 1000 && summer.ice === 0, `open: ${summer.open} open-water pixels, ${summer.ice} ice`);
});

test('lake ice does not repeat with the textures\' period', () => {
  const map = { width: fixture.W, height: fixture.H, terrainAt: (x, y) => fixture.terrain[y][x] };
  for (const level of ['thin', 'thick']) {
    const state = { dust: 1, patches: 0, snow: 0, laden: false, mud: 0, water: 0, ice: level };
    const baker = createBaker(map, () => state, WINTER_WORLD, n => tex(n));
    const out = new Uint8ClampedArray(fixture.W * CELL * fixture.H * CELL * 4);
    for (let cy = 0; cy < fixture.H; cy++) for (let cx = 0; cx < fixture.W; cx++)
      if (fixture.terrain[cy][cx] === 'deepwater') baker.bakeCell(out, 0, 0, fixture.W, cx, cy);
    const PW = fixture.W * CELL, water = new Uint8Array(PW * fixture.H * CELL);
    for (let cy = 0; cy < fixture.H; cy++) for (let cx = 0; cx < fixture.W; cx++) if (fixture.terrain[cy][cx] === 'deepwater')
      for (let py = cy * CELL; py < (cy + 1) * CELL; py++) for (let px = cx * CELL; px < (cx + 1) * CELL; px++) water[py * PW + px] = baker.classify(px, py).water ? 1 : 0;
    const lake = (px, py) => water[py * PW + px];
    const at = (px, py) => String(out.subarray((py * fixture.W * CELL + px) * 4, (py * fixture.W * CELL + px) * 4 + 3));
    // Pixels a texture period apart against pixels unrelated distances apart, along both axes: art
    // that repeats matches far more often at the period. The shapes of the pairs that fall inside the
    // lake move the match rate a few points from shift to shift, so the control is a mean.
    const same = (sx, sy) => {
      let eq = 0, n = 0;
      for (let py = 0; py + sy < fixture.H * CELL; py += 2) for (let px = 0; px + sx < fixture.W * CELL; px += 2) {
        if (!lake(px, py) || !lake(px + sx, py + sy)) continue;
        n++;
        if (at(px, py) === at(px + sx, py + sy)) eq++;
      }
      assert.ok(n > 2000, `fixture has ${n} lake pixel pairs (${sx}, ${sy}) px apart`);
      return eq / n;
    };
    const unrelated = [61, 83, 107, 131];
    for (const [axis, shift] of [['x', d => [d, 0]], ['y', d => [0, d]]]) {
      const period = same(...shift(SRC)), control = unrelated.reduce((sum, d) => sum + same(...shift(d)), 0) / unrelated.length;
      assert.ok(period < control + 0.15, `${level} lake ice matches itself ${SRC} px away along ${axis} on ${(period * 100).toFixed(0)}% of pixels, against ${(control * 100).toFixed(0)}% at unrelated distances`);
    }
  }
});

test('wetter ground shows more mud, from the least wetness that shows', () => {
  const cells = openLand('plain').concat(openLand('forest')).slice(0, 8);
  const share = mudLevel => {
    const m = mapDto(() => ({ ...dry, mudLevel }));
    return cells.reduce((sum, [x, y]) => sum + shownShare(m, x, y, SUMMER_WORLD, art(n => `terrain/mud_${n}_summer`)), 0) / cells.length;
  };
  const shares = [GROUND.wetMin, 0.3, 0.5, 0.7, 1].map(share);
  assert.ok(shares[0] > 0, `wetness ${GROUND.wetMin} drew no mud`);
  for (let i = 1; i < shares.length; i++) assert.ok(shares[i] > shares[i - 1], `mud shown does not grow with wetness: ${shares.map(v => v.toFixed(2))}`);
});

test('a tile in sight with no surface entry is bare, dry ground', () => {
  const [x, y] = openLand('plain').concat(openLand('forest'))[0];
  const r = layers(mapDto(() => null), x, y, WINTER_WORLD);
  assert.equal(r.snow, 0);
});

test('snow depth maps to dust, patches and full cover at the thresholds', () => {
  const at = cm => cellState({ ...dry, snowCm: cm }, 'plain');
  assert.deepEqual([at(0).dust, at(0).patches, at(0).snow, at(0).laden], [0, 0, 0, false]);
  assert.deepEqual([at(GROUND.dustCm).dust, at(GROUND.dustCm).patches, at(GROUND.dustCm).snow], [1, 0, 0]);
  assert.deepEqual([at(GROUND.patchyCm).dust, at(GROUND.patchyCm).patches], [1, 0]);
  assert.deepEqual([at(GROUND.patchedCm - 0.01).dust, at(GROUND.patchedCm).dust], [1, 0]);
  const thin = at((GROUND.patchyCm + GROUND.patchedCm) / 2);
  assert.ok(thin.patches > 0 && thin.patches < 1 && thin.snow === 0, `half way to patched: ${thin.key}`);
  assert.deepEqual([at(GROUND.patchedCm).patches, at(GROUND.patchedCm).snow], [1, 0]);
  const deepening = at((GROUND.patchedCm + GROUND.fullCm) / 2);
  assert.ok(deepening.patches === 1 && deepening.snow > 0 && deepening.snow < 1, `half way to full: ${deepening.key}`);
  assert.deepEqual([at(GROUND.fullCm).snow, at(GROUND.fullCm).laden], [1, true]);
  for (let cm = 0; cm < 60; cm += 0.5) for (const k of ['patches', 'snow'])
    assert.ok(at(cm + 0.5)[k] >= at(cm)[k], `${k} never falls as snow deepens (${cm} cm)`);
});

test('mud and standing water are apart; frozen standing water is ice', () => {
  assert.equal(cellState({ ...dry, mudLevel: GROUND.wetMin / 2 }, 'plain').mud, 0);
  assert.deepEqual([cellState({ ...dry, mudLevel: 0.6 }, 'plain').mud, cellState({ ...dry, mudLevel: 0.6 }, 'plain').water], [0.6, 0]);
  assert.deepEqual([cellState({ ...dry, waterCm: GROUND.wetFullCm }, 'plain').water, cellState({ ...dry, waterCm: GROUND.wetFullCm }, 'plain').mud], [1, 0]);
  assert.equal(cellState({ ...dry, waterCm: 1, mudLevel: 1 }, 'plain').water, 0.2);
  assert.equal(cellState({ ...dry, waterCm: 1 }, 'plain').ice, 'none');
  assert.equal(cellState({ ...dry, waterCm: 1, ice: true }, 'plain').ice, 'thin');
  assert.equal(cellState({ ...dry, waterCm: GROUND.thickIceCm, ice: true }, 'plain').ice, 'thick');
  assert.equal(cellState(null, 'water', true).ice, 'thick');
  assert.equal(cellState(null, 'water').ice, 'none');
  assert.equal(cellState(null, 'plain').ice, 'none');
  assert.throws(() => cellState(null, 'plain', true), /frozen water on plain/);
});

test('the date sets the palette and snowline only', () => {
  for (const season of ['Winter', 'Spring', 'Summer', 'Fall']) {
    const w = worldState({ season });
    assert.deepEqual([w.palette, w.snowline], [SEASONS[season.toLowerCase()].palette, SEASONS[season.toLowerCase()].snowline]);
  }
  assert.throws(() => worldState({ season: 'Monsoon' }));
  assert.equal(cellState(null, 'plain'), BARE);
});

test('the ground is the view\'s own, out of sight too: the page keeps nothing between views', () => {
  const g = createGround(c => TERRAIN[c]);
  const i = 3 * fixture.W + 3, m = mapDto((x, y) => x === 3 && y === 3 ? deep : null);
  const away = { ...m, visible: m.visible.filter(v => v !== i) };
  g.see(away);
  assert.equal(g.stateAt(3, 3).snow, 1, 'out of sight, the ground is the Sim\'s memory of it');
  const before = g.version;
  g.see({ ...away, surface: [] });
  assert.equal(g.stateAt(3, 3), cellState(null, TERRAIN[m.known[i]]), 'a known tile the view does not list is bare');
  assert.ok(g.version > before);
  const unseen = { ...away, known: m.known.slice(0, i) + '.' + m.known.slice(i + 1), surface: [] };
  g.see(unseen);
  assert.equal(g.stateAt(3, 3), BARE);
  assert.throws(() => g.see({ ...unseen, surface: [{ i, ...deep }] }), /never been seen/);
});

test('a new view with the same ground does not rebake the map', () => {
  const g = createGround(c => TERRAIN[c]);
  const m = mapDto((x, y) => x === 3 && y === 3 ? deep : null);
  g.see(m);
  const v = g.version;
  g.see({ ...m, surface: m.surface.map(s => ({ ...s })) });
  assert.equal(g.version, v);
});

test('every art name a world state draws is a file, and PAINTED matches the files', () => {
  for (const season of Object.keys(SEASONS)) for (const name of wantedArt(worldState({ season })))
    assert.ok(fs.existsSync(path.join(icons, `${name}.png`)), `${name}.png is missing`);
  const files = fs.readdirSync(path.join(icons, 'terrain'), { recursive: true })
    .filter(f => f.endsWith('.png')).map(f => `terrain/${f.replaceAll(path.sep, '/').slice(0, -4)}`);
  for (const name of files) assert.doesNotThrow(() => resolveArt(name), `${name}.png exists: add its layer or palette to PAINTED in terrain-core.js`);
  for (const layer of PAINTED.layers)
    assert.ok(files.some(f => f.slice(8).split(/[/_]/)[0] === layer), `layer "${layer}" is marked painted but has no file`);
  for (const palette of PAINTED.palettes)
    assert.ok(files.some(f => f.endsWith(`_${palette}`)), `palette "${palette}" is marked painted but has no file`);
});

test('art that was never painted is an error', () => {
  assert.equal(resolveArt('terrain/ground/plain_1_summer'), 'terrain/ground/plain_1_summer');
  assert.throws(() => resolveArt('terrain/lichen/x_1_winter'), /no terrain art for layer/);
  assert.throws(() => resolveArt('terrain/ground/plain_1_spring'), /no terrain art in palette/);
});

// ---------------------------------------------------------------- caves, ravines and cliffs
const hexRgb = s => [1, 3, 5].map(i => parseInt(s.slice(i, i + 2), 16));
const CLEFT = hexRgb('#191c20');
const ROCKS = ['#b6b8b5', '#8a8d8d', '#646a6e', '#454b52', '#2b3037'].map(hexRgb);
const same = (a, b) => a[0] === b[0] && a[1] === b[1] && a[2] === b[2];
const BARE_STATE = { dust: 0, patches: 0, snow: 0, laden: false, mud: 0, water: 0, ice: 'none' };

/** Bakes a small map (rows of terrain names) with edges; returns pixel(x, y) -> [r, g, b]. */
function bake(rows, edges = []) {
  const H = rows.length, W = rows[0].length;
  const baker = createBaker({ width: W, height: H, terrainAt: (x, y) => rows[y][x], edges }, () => BARE_STATE, SUMMER_WORLD, tex);
  const out = new Uint8Array(W * CELL * H * CELL * 4);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) baker.bakeCell(out, 0, 0, W, x, y);
  baker.bakeSprites(out, 0, 0, W, H);
  return (x, y) => { const i = (y * W * CELL + x) * 4; return [out[i], out[i + 1], out[i + 2]]; };
}
const field = (w, h, t) => Array.from({ length: h }, () => Array(w).fill(t));
/** Counts pixels in [x0, x1) x [y0, y1) for which `is` holds. */
function count(px, x0, y0, x1, y1, is) {
  let n = 0;
  for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) if (is(px(x, y))) n++;
  return n;
}

test('a ravine draws a dark cleft along its boundary, and none without the edge', () => {
  const rows = field(8, 7, 'plain');
  // A ravine down the boundary between columns 3 and 4, rows 1 to 5.
  const edges = [1, 2, 3, 4, 5].map(y => ({ ax: 3, ay: y, bx: 4, by: y, type: 'ravine' }));
  const box = [4 * CELL - 30, 2 * CELL, 4 * CELL + 30, 5 * CELL];
  const cleft = p => same(p, CLEFT);
  assert.ok(count(bake(rows, edges), ...box, cleft) > 200, 'the cleft is drawn');
  assert.equal(count(bake(rows), ...box, cleft), 0, 'no edge, no cleft');
});

test('a ravine that closes on itself is drawn all the way round', () => {
  // Regression: corner cutting did not wrap a closed chain, leaving a gap where its walk began.
  const edges = [];
  for (let i = 2; i <= 4; i++) edges.push(
    { ax: i, ay: 1, bx: i, by: 2, type: 'ravine' }, { ax: i, ay: 4, bx: i, by: 5, type: 'ravine' },
    { ax: 1, ay: i, bx: 2, by: i, type: 'ravine' }, { ax: 4, ay: i, bx: 5, by: i, type: 'ravine' });
  const px = bake(field(7, 7, 'plain'), edges), cleft = p => same(p, CLEFT);
  // A ray from the middle of the ringed block out past each corner crosses the ring.
  for (const [dx, dy] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) {
    let hits = 0;
    for (let k = 0; k < 2.4 * CELL; k++) {
      const x = Math.round(3.5 * CELL + dx * k), y = Math.round(3.5 * CELL + dy * k);
      for (let o = -3; o <= 3; o++) if (cleft(px(x + o, y)) || cleft(px(x, y + o))) { hits++; break; }
    }
    assert.ok(hits > 3, `the ring is broken toward corner ${dx},${dy}: ${hits} cleft pixels on the ray`);
  }
});

test('a cliff\'s face lies on the lower-right side of its boundary', () => {
  const rows = field(8, 7, 'plain');
  const edges = [1, 2, 3, 4, 5].map(y => ({ ax: 3, ay: y, bx: 4, by: y, type: 'cliff' }));
  const px = bake(rows, edges), rock = p => ROCKS.some(r => same(p, r));
  let left = 0, right = 0;
  for (let y = 2 * CELL; y < 5 * CELL; y++) {
    // Where the face begins on this row: the boundary wanders, so measure from the first rock pixel.
    let x0 = -1;
    for (let x = 3 * CELL; x < 5 * CELL && x0 < 0; x++) if (rock(px(x, y))) x0 = x;
    if (x0 < 0) continue;
    left += count(px, x0 - 12, y, x0, y + 1, rock);
    right += count(px, x0, y, x0 + 12, y + 1, rock);
  }
  assert.ok(right > 1000 && right > left * 4, `face to the right: ${right} rock pixels right, ${left} left`);
});

test('a cave cuts a dark passage through the mountain, out of its mouth', () => {
  const rows = field(9, 7, 'mountain');
  for (let y = 0; y < 7; y++) rows[y][0] = rows[y][1] = 'plain';
  for (let x = 3; x < 8; x++) rows[3][x] = 'cavefloor';
  rows[3][2] = 'caveentrance';
  const lum = p => p[0] + p[1] + p[2];
  const solid = bake(field(9, 7, 'mountain').map(r => r.map((t, x) => x < 2 ? 'plain' : t)));
  const cave = bake(rows);
  let darker = 0;
  for (let x = 3; x < 8; x++) {
    // Mean brightness of the cell's middle: the passage wanders, but it runs through the middle.
    let a = 0, b = 0, n = 0;
    for (let y = 3 * CELL + 14; y < 4 * CELL - 14; y++) for (let px = x * CELL + 14; px < (x + 1) * CELL - 14; px++) {
      a += lum(cave(px, y)); b += lum(solid(px, y)); n++;
    }
    if (a / n < b / n * 0.8) darker++;
  }
  assert.equal(darker, 5, 'every cave cell is darker than the same rock without a cave');
  assert.ok(!wantedArt(SUMMER_WORLD).some(n => n.startsWith('cavefloor_tile')), 'the old cave-floor tiles are retired');
});
