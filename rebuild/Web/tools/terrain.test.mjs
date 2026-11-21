// The map's ground follows the Sim's ground, not the calendar: run with
//   node --test tools/terrain.test.mjs
// Oracles are the brief's (0 snow never draws snow, deep snow never draws bare) and the art on disk.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBaker, wantedArt, resolveArt, PAINTED, CELL, SRC, VARIANTS, variant } from '../App/wwwroot/terrain-core.js';
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

/** Of the pixels of cell (cx, cy) where `art` is opaque and differs from the bare ground, the share baked as `art`. */
function shownShare(m, cx, cy, world, art) {
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
    const a = sampleOf(art(n), px, py), g = sampleOf(`terrain/ground/${terrain}_${n}_${world.palette}`, px, py);
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

test('dust, patches, mud and pond ice each draw their own art', () => {
  const [x, y] = openLand('plain').concat(openLand('forest'))[0];
  const cases = [
    ['dusting', { ...dry, snowCm: 2 }, n => `terrain/snow/dust_flat_${n}`, 0.95],
    ['patchy snow', { ...dry, snowCm: 6 }, n => `terrain/snow/patchy_flat_${n}`, 0.8],
    ['soaked ground', { ...dry, mudLevel: 1 }, n => `terrain/mud_${n}_summer`, 0.95],
    ['thick pond ice', { ...dry, waterCm: 5, ice: true }, n => `terrain/ice/thick_${n}`, 0.95],
    ['thin ice on a flooded cell', { ...dry, waterCm: GROUND.thickIceCm - 0.1, ice: true }, n => `terrain/ice/thin_${n}`, 0.95],
  ];
  for (const [what, surface, art, share] of cases) {
    const got = shownShare(mapDto((cx, cy) => cx === x && cy === y ? surface : null), x, y, SUMMER_WORLD, art);
    assert.ok(got >= share, `${what} showed only ${(got * 100).toFixed(0)}% of its art`);
  }
});

test('saturated mud under a thin frozen film draws mostly mud, not a sheet of ice', () => {
  const [x, y] = openLand('plain').concat(openLand('forest'))[0];
  const m = mapDto((cx, cy) => cx === x && cy === y ? { ...dry, mudLevel: 1, waterCm: 1, ice: true } : null);
  const ice = shownShare(m, x, y, SUMMER_WORLD, n => `terrain/ice/thin_${n}`), mud = shownShare(m, x, y, SUMMER_WORLD, n => `terrain/mud_${n}_summer`);
  assert.ok(mud > 0.6, `the soaked ground showed only ${(mud * 100).toFixed(0)}% of its mud`);
  assert.ok(ice > 0 && ice < 0.4, `a 1 cm frozen film covered ${(ice * 100).toFixed(0)}% of the ice art`);
});

test('lake ice carries snow only as a dusting', () => {
  const lake = [];
  for (let y = 0; y < fixture.H; y++) for (let x = 0; x < fixture.W; x++) if (fixture.terrain[y][x] === 'deepwater') lake.push([x, y]);
  const [x, y] = lake.find(([x, y]) => [[-1, 0], [1, 0], [0, -1], [0, 1]].every(([dx, dy]) => fixture.terrain[y + dy]?.[x + dx] === 'deepwater'));
  const got = shownShare(mapDto((cx, cy) => cx === x && cy === y ? deep : null), x, y, WINTER_WORLD, n => `terrain/snow/dust_flat_${n}`);
  assert.ok(got >= 0.95, `snow on lake ice showed only ${(got * 100).toFixed(0)}% of the dusting`);
});

test('wetter ground shows more mud, from the least wetness that shows', () => {
  const cells = openLand('plain').concat(openLand('forest')).slice(0, 8);
  const share = mudLevel => {
    const m = mapDto(() => ({ ...dry, mudLevel }));
    return cells.reduce((sum, [x, y]) => sum + shownShare(m, x, y, SUMMER_WORLD, n => `terrain/mud_${n}_summer`), 0) / cells.length;
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
