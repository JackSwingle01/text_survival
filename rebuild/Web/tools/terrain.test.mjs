// The map's ground follows the Sim's ground, not the calendar: run with
//   node --test tools/terrain.test.mjs
// Oracles are the brief's (0 snow never draws snow, deep snow never draws bare) and the art on disk.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBaker, wantedArt, resolveArt, ice, PAINTED, CELL, SRC, VARIANTS, variant } from '../App/wwwroot/terrain-core.js';
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

/** The fixture as a MapDto with every tile in sight and `surfaceOf(x, y)` as its ground. */
function mapDto(surfaceOf) {
  const { W, H } = fixture, surface = [];
  let known = '';
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    known += CODES[fixture.terrain[y][x]];
    const s = surfaceOf(x, y);
    if (s) surface.push({ i: y * W + x, ...s });
  }
  return { width: W, height: H, known, visible: [...Array(W * H).keys()], surface };
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

test('dust, patches, mud and pond ice each draw their own art', () => {
  const [x, y] = openLand('plain').concat(openLand('forest'))[0];
  const cases = [
    ['dusting', { ...dry, snowCm: 2 }, art(n => `terrain/snow/dust_flat_${n}`), 0.95],
    ['patchy snow', { ...dry, snowCm: 6 }, art(n => `terrain/snow/patchy_flat_${n}`), 0.8],
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
  const m = mapDto(() => null), ground = createGround(c => TERRAIN[c]);
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

test('lake ice does not repeat with the textures\' period', () => {
  const map = { width: fixture.W, height: fixture.H, terrainAt: (x, y) => fixture.terrain[y][x] };
  for (const level of ['thin', 'thick']) {
    const state = { snow: 0, dust: true, laden: false, mud: 0, water: 0, ice: level };
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
  assert.deepEqual([at(0).snow, at(0).dust, at(0).laden], [0, false, false]);
  assert.deepEqual([at(GROUND.dustCm).snow, at(GROUND.dustCm).dust], [0, true]);
  assert.equal(at(GROUND.patchyCm - 0.01).snow, 0);
  assert.equal(at(GROUND.patchyCm).snow, GROUND.patchyCover);
  assert.ok(at(10).snow > GROUND.patchyCover && at(10).snow < 1);
  assert.deepEqual([at(GROUND.fullCm).snow, at(GROUND.fullCm).dust, at(GROUND.fullCm).laden], [1, false, true]);
  for (let cm = 0; cm < 60; cm += 0.5) assert.ok(at(cm + 0.5).snow >= at(cm).snow, `cover never falls as snow deepens (${cm} cm)`);
});

test('mud and standing water are apart; frozen standing water is ice', () => {
  assert.equal(cellState({ ...dry, mudLevel: GROUND.wetMin / 2 }, 'plain').mud, 0);
  assert.deepEqual([cellState({ ...dry, mudLevel: 0.6 }, 'plain').mud, cellState({ ...dry, mudLevel: 0.6 }, 'plain').water], [0.6, 0]);
  assert.deepEqual([cellState({ ...dry, waterCm: GROUND.wetFullCm }, 'plain').water, cellState({ ...dry, waterCm: GROUND.wetFullCm }, 'plain').mud], [1, 0]);
  assert.equal(cellState({ ...dry, waterCm: 1, mudLevel: 1 }, 'plain').water, 0.2);
  assert.equal(cellState({ ...dry, waterCm: 1 }, 'plain').ice, 'none');
  assert.equal(cellState({ ...dry, waterCm: 1, ice: true }, 'plain').ice, 'thin');
  assert.equal(cellState({ ...dry, waterCm: GROUND.thickIceCm, ice: true }, 'plain').ice, 'thick');
  assert.equal(cellState(null, 'water').ice, 'thick');
  assert.equal(cellState(null, 'plain').ice, 'none');
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
