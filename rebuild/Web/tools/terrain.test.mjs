// The map's ground follows the Sim's ground, not the calendar: run with
//   node --test tools/terrain.test.mjs
// Oracles are the brief's (0 snow never draws snow, deep snow never draws bare) and the art on disk.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBaker, textureNames, wantedArt, resolveArt, PAINTED, CELL, SRC, VARIANTS, variant } from '../App/wwwroot/terrain-core.js';
import { cellState, worldState, createGroundMemory, GROUND, SEASONS, BARE } from '../App/wwwroot/ground-state.js';
import { decodePng } from './png.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const icons = path.resolve(here, '../../../assets/icons');
const fixture = JSON.parse(fs.readFileSync(path.join(here, 'fixtures/seed17.json'), 'utf8'));
const CODES = { forest: 'F', clearing: 'C', plain: 'P', hills: 'H', water: 'W', marsh: 'M', rock: 'R', mountain: 'X', deepwater: 'D', cavefloor: 'c' };
const TERRAIN = Object.fromEntries(Object.entries(CODES).map(([k, v]) => [v, k]));
const quiet = () => {};

const textures = new Map();
const tex = name => {
  if (!textures.has(name)) textures.set(name, decodePng(path.join(icons, `${name}.png`)));
  return textures.get(name);
};
function sampleOf(name, px, py) {
  const t = tex(resolveArt(name, quiet)), sx = Math.floor(px / SRC), sy = Math.floor(py / SRC);
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

/** Per pixel of cell (cx, cy) baked from `m`: whether it is the snow layer, the bare ground, or neither. */
function layers(m, cx, cy, world) {
  const ground = createGroundMemory(c => TERRAIN[c]);
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
    const s = sampleOf(`terrain/snow/full_${flav}_${n}`, px, py), g = sampleOf(`terrain/ground/${terrain}_${n}_${world.palette}`, px, py);
    const isSnow = s && String(s) === String(got), isBare = g && String(g) === String(got);
    if (isSnow && !isBare) snow++;
    if (isBare && !isSnow) bare++;
  }
  return { snow, bare, pixels: CELL * CELL };
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

test('mud and standing water wet the ground; frozen standing water is ice', () => {
  assert.equal(cellState({ ...dry, mudLevel: GROUND.mudMin / 2 }, 'plain').mud, 0);
  assert.equal(cellState({ ...dry, mudLevel: 0.6 }, 'plain').mud, 0.6);
  assert.equal(cellState({ ...dry, waterCm: GROUND.wetFullCm }, 'plain').mud, 1);
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

test('ground memory keeps the last seen state and refuses ground out of sight', () => {
  const g = createGroundMemory(c => TERRAIN[c]);
  const m = mapDto((x, y) => x === 3 && y === 3 ? deep : null);
  g.see(m);
  assert.equal(g.stateAt(3, 3).snow, 1);
  const away = { ...m, visible: m.visible.filter(i => i !== 3 * m.width + 3), surface: [] };
  g.see(away);
  assert.equal(g.stateAt(3, 3).snow, 1, 'out of sight, the ground is as last seen');
  g.forget();
  assert.equal(g.stateAt(3, 3), BARE);
  assert.throws(() => g.see({ ...away, surface: [{ i: 3 * m.width + 3, ...deep }] }), /not in sight/);
});

test('every art name resolves to a file, and PAINTED matches the files', () => {
  for (const season of Object.keys(SEASONS)) for (const name of textureNames(worldState({ season })))
    assert.ok(fs.existsSync(path.join(icons, `${name}.png`)), `${name}.png is missing`);
  for (const season of Object.keys(SEASONS)) for (const name of wantedArt(worldState({ season }))) {
    const exists = fs.existsSync(path.join(icons, `${name}.png`)), resolved = resolveArt(name, quiet) === name;
    assert.equal(resolved, exists, exists ? `${name}.png exists: add it to PAINTED in terrain-core.js` : `${name} is marked painted but has no file`);
  }
  assert.ok(PAINTED.palettes.length > 0);
});

test('missing art is said once per asset', () => {
  const said = [];
  assert.equal(resolveArt('terrain/ground/tundra_1_summer', m => said.push(m)), 'terrain/ground/tundra_1_winter');
  resolveArt('terrain/ground/tundra_2_summer', m => said.push(m));
  assert.equal(said.length, 1);
  assert.throws(() => resolveArt('terrain/lichen/x_1_winter', quiet), /no terrain art for layer/);
});
