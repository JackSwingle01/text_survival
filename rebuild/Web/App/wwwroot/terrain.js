// The map's ground, baked from terrain-core.js into chunks of cells and drawn at one screen pixel
// per art pixel. Baking is spread over frames, nearest the camera first; a chunk keeps showing its
// last image until its new one is done.
//
// Ground state: draw() takes ground (ground-state.js's memory: each cell's state and its key) and
// world (palette and snowline). A chunk rebakes only when the states, terrain or trails it can see
// change, so snow settling on one tile does not redraw the screen.
import { src } from './util.js';
import { createBaker, textureNames, printPixels, snowyCell, CELL } from './terrain-core.js';

/** Terrain names by the map's known-tile codes (MapDto.Known). */
export const TERRAIN = { F: 'forest', C: 'clearing', P: 'plain', H: 'hills', W: 'water', M: 'marsh', R: 'rock', X: 'mountain', D: 'deepwater', c: 'cavefloor', e: 'caveentrance' };

const CHUNK = 8;          // cells per chunk side
const BUDGET_MS = 6;      // baking per frame

const NO_TRAILS = { list: [], at: new Map(), key: '' };
const trailCache = new WeakMap();
/**
 * A map's trails (MapDto.Trails), worked out once per view: the list for the baker, each cell's trails
 * as one string for chunk signatures (at: cell index -> code), and a key for them all.
 */
export function trailsOf(m) {
  if (!m.trails?.length) return NO_TRAILS;
  let r = trailCache.get(m.trails);
  if (r) return r;
  const list = m.trails.map(t => ({ ax: t.ax, ay: t.ay, bx: t.bx, by: t.by, tier: t.tier })), at = new Map();
  const codes = list.map(t => `${t.ax},${t.ay},${t.bx},${t.by},${t.tier};`);
  list.forEach((t, k) => { for (const i of [t.ay * m.width + t.ax, t.by * m.width + t.bx]) at.set(i, (at.get(i) ?? '') + codes[k]); });
  trailCache.set(m.trails, r = { list, at, key: codes.join('') });
  return r;
}

/**
 * What a chunk's pixels can depend on: ground state two cells around it, and terrain and trails
 * three (a sprite anchored in the next cell reads that cell's 5x5 neighbourhood; trampled ground
 * counts the worn sides of the cells around a neighbour).
 */
export function chunkSignature(m, trails, cx0, cy0, ground, world) {
  let sig = world.key;
  for (let y = Math.max(0, cy0 - 3); y < Math.min(m.height, cy0 + CHUNK + 3); y++) {
    const xa = Math.max(0, cx0 - 3), xb = Math.min(m.width, cx0 + CHUNK + 3);
    sig += m.known.slice(y * m.width + xa, y * m.width + xb);
    if (trails.at.size) for (let x = xa; x < xb; x++) sig += trails.at.get(y * m.width + x) ?? '';
  }
  const xa = Math.max(0, cx0 - 2), xb = Math.min(m.width, cx0 + CHUNK + 2);
  for (let y = Math.max(0, cy0 - 2); y < Math.min(m.height, cy0 + CHUNK + 2); y++)
    for (let x = xa; x < xb; x++) sig += ground.keyAt(y * m.width + x);
  return sig;
}

export function createTerrain() {
  const textures = new Map();
  let wanted = null, wantedFor = null;
  const chunks = new Map(); // "cx,cy" -> { canvas, sig, job }
  let baker = null, bakerFor = null;

  function load(world) {
    for (const name of textureNames(world)) {
      if (textures.has(name)) continue;
      textures.set(name, null);
      const im = new Image();
      im.onload = () => {
        const c = document.createElement('canvas');
        c.width = im.width; c.height = im.height;
        const g = c.getContext('2d');
        g.drawImage(im, 0, 0);
        textures.set(name, { w: im.width, h: im.height, data: g.getImageData(0, 0, im.width, im.height).data });
      };
      im.onerror = () => textures.set(name, new Error(`missing terrain texture ${name}`));
      im.src = src(name);
    }
    wanted = textureNames(world);
    wantedFor = world.key;
  }
  // A texture that failed to load would leave the map blank for ever; say so instead.
  const ready = () => wanted.every(n => { const t = textures.get(n); if (t instanceof Error) throw t; return t; });
  const tex = name => {
    const t = textures.get(name);
    if (!t) throw new Error(`terrain texture not loaded: ${name}`);
    return t;
  };

  /**
   * Draws the ground of cells [x0..x1] x [y0..y1] with the map's origin at (ox, oy), one cell CELL
   * pixels wide. m: MapDto (width, height, known); camera: cell the camera centres on;
   * ground: { stateAt(x, y), keyAt(i), version }; world: { palette, snowline, key }.
   */
  function draw(ctx, { m, ox, oy, x0, y0, x1, y1, camera, ground, world }) {
    if (wantedFor !== world.key) load(world);
    if (!ready()) return;
    const trails = trailsOf(m);
    // Ravines and cliffs come with the known tiles they lie beside, so m.known keys them too. Trails
    // change on their own, and the baker caches each cell's trails, so a changed trail is a new baker.
    const key = `${m.known}|${trails.key}|${world.key}|${ground.version}|${m.width}`;
    if (!baker || bakerFor !== key) {
      baker = createBaker({ width: m.width, height: m.height, terrainAt: (x, y) => {
        const c = m.known[y * m.width + x];
        return c === '.' ? null : TERRAIN[c];
      }, edges: m.edges.map(e => ({ ax: e.ax, ay: e.ay, bx: e.bx, by: e.by, type: e.kind })), trails: trails.list }, ground.stateAt, world, tex);
      bakerFor = key;
    }
    const todo = [];
    for (let cy0 = Math.floor(y0 / CHUNK) * CHUNK; cy0 <= y1; cy0 += CHUNK)
      for (let cx0 = Math.floor(x0 / CHUNK) * CHUNK; cx0 <= x1; cx0 += CHUNK) {
        const id = `${cx0},${cy0}`, sig = chunkSignature(m, trails, cx0, cy0, ground, world);
        let ch = chunks.get(id);
        if (!ch) chunks.set(id, ch = { canvas: null, sig: null, job: null });
        if (ch.sig !== sig && ch.job?.sig !== sig) {
          const xb = Math.min(m.width, cx0 + CHUNK), yb = Math.min(m.height, cy0 + CHUNK), cells = [];
          for (let y = cy0; y < yb; y++) for (let x = cx0; x < xb; x++) cells.push([x, y]);
          ch.job = { sig, baker, x0: cx0, y0: cy0, x1: xb, y1: yb, cells, i: 0,
            pixels: new Uint8ClampedArray((xb - cx0) * CELL * (yb - cy0) * CELL * 4) };
        }
        if (ch.job) todo.push(ch);
        if (ch.canvas) {
          ctx.imageSmoothingEnabled = false;
          ctx.drawImage(ch.canvas, ox + cx0 * CELL, oy + cy0 * CELL);
        }
      }
    // Bake for a few milliseconds, nearest the camera first.
    const dist = ch => Math.hypot(ch.job.x0 + CHUNK / 2 - camera.x, ch.job.y0 + CHUNK / 2 - camera.y);
    todo.sort((a, b) => dist(a) - dist(b));
    const t0 = performance.now();
    for (const ch of todo) {
      const j = ch.job;
      while (j.i < j.cells.length && performance.now() - t0 < BUDGET_MS) {
        const [x, y] = j.cells[j.i++];
        j.baker.bakeCell(j.pixels, j.x0, j.y0, j.x1, x, y);
      }
      if (j.i < j.cells.length) break;
      j.baker.bakeSprites(j.pixels, j.x0, j.y0, j.x1, j.y1);
      const w = (j.x1 - j.x0) * CELL, h = (j.y1 - j.y0) * CELL;
      ch.canvas ??= document.createElement('canvas');
      ch.canvas.width = w; ch.canvas.height = h;
      ch.canvas.getContext('2d').putImageData(new ImageData(j.pixels, w, h), 0, 0);
      ch.sig = j.sig; ch.job = null;
      if (performance.now() - t0 >= BUDGET_MS) break;
    }
  }

  // Print pixels per track, until the ground under them is baked anew.
  let prints = new Map(), printsFor = null;
  /**
   * A track's prints as [[x, y, fill]] from its cell's top-left, fill a CSS colour to lay one pixel
   * of over the drawn ground. Empty until the ground has been drawn once.
   */
  function printsOf(t, ground) {
    if (!baker) return [];
    if (printsFor !== baker) { prints = new Map(); printsFor = baker; }
    const snowy = snowyCell(ground.stateAt(t.x, t.y)), freshness = Math.round(t.freshnessPct * 50) / 50;
    const key = `${t.x},${t.y},${t.maker},${t.heading},${t.pattern},${t.traffic},${freshness},${snowy}`;
    let r = prints.get(key);
    if (!r) {
      r = printPixels({ x: t.x, y: t.y, maker: t.maker, heading: t.heading, pattern: t.pattern, traffic: t.traffic, freshness }, baker, snowy)
        .map(([x, y, [cr, cg, cb], a]) => [x, y, `rgba(${cr},${cg},${cb},${a.toFixed(3)})`]);
      prints.set(key, r);
    }
    return r;
  }

  return { draw, prints: printsOf };
}
