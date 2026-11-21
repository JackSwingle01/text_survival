// The map's ground, baked from terrain-core.js into chunks of cells and drawn at one screen pixel
// per art pixel. Baking is spread over frames, nearest the camera first; a chunk keeps showing its
// last image until its new one is done.
//
// Ground state: draw() takes ground (ground-state.js's memory: each cell's state and its key) and
// world (palette and snowline). A chunk rebakes only when the states or terrain it can see change,
// so snow settling on one tile does not redraw the screen.
import { src } from './util.js';
import { createBaker, textureNames, CELL } from './terrain-core.js';

/** Terrain names by the map's known-tile codes (MapDto.Known). */
export const TERRAIN = { F: 'forest', C: 'clearing', P: 'plain', H: 'hills', W: 'water', M: 'marsh', R: 'rock', X: 'mountain', D: 'deepwater', c: 'cavefloor', e: 'rock' };

const CHUNK = 8;          // cells per chunk side
const BUDGET_MS = 6;      // baking per frame

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
      im.onerror = () => console.error('Missing terrain texture', name);
      im.src = src(name);
    }
    wanted = textureNames(world);
    wantedFor = world.key;
  }
  const ready = () => wanted.every(n => textures.get(n));
  const tex = name => {
    const t = textures.get(name);
    if (!t) throw new Error(`terrain texture not loaded: ${name}`);
    return t;
  };

  /** The known codes of a chunk and the two cells around it: what its pixels can depend on. */
  function signature(m, cx0, cy0, ground, world) {
    const xa = Math.max(0, cx0 - 2), xb = Math.min(m.width, cx0 + CHUNK + 2);
    let sig = world.key;
    for (let y = Math.max(0, cy0 - 2); y < Math.min(m.height, cy0 + CHUNK + 2); y++) {
      sig += m.known.slice(y * m.width + xa, y * m.width + xb);
      for (let x = xa; x < xb; x++) sig += ground.keyAt(y * m.width + x);
    }
    return sig;
  }

  /**
   * Draws the ground of cells [x0..x1] x [y0..y1] with the map's origin at (ox, oy), one cell CELL
   * pixels wide. m: MapDto (width, height, known); camera: cell the camera centres on;
   * ground: { stateAt(x, y), keyAt(i), version }; world: { palette, snowline, key }.
   */
  function draw(ctx, { m, ox, oy, x0, y0, x1, y1, camera, ground, world }) {
    if (wantedFor !== world.key) load(world);
    if (!ready()) return;
    const key = `${world.key}|${ground.version}|${m.width}`;
    if (!baker || bakerFor !== m.known + key) {
      baker = createBaker({ width: m.width, height: m.height, terrainAt: (x, y) => {
        const c = m.known[y * m.width + x];
        return c === '.' ? null : TERRAIN[c];
      } }, ground.stateAt, world, tex);
      bakerFor = m.known + key;
    }
    const todo = [];
    for (let cy0 = Math.floor(y0 / CHUNK) * CHUNK; cy0 <= y1; cy0 += CHUNK)
      for (let cx0 = Math.floor(x0 / CHUNK) * CHUNK; cx0 <= x1; cx0 += CHUNK) {
        const id = `${cx0},${cy0}`, sig = signature(m, cx0, cy0, ground, world);
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

  return { draw };
}
