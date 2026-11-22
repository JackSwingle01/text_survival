// The map's ground, baked from terrain-core.js a cell at a time in a pool of workers
// (terrain-worker.js) and drawn at one screen pixel per art pixel. A cell's pixels depend only on its
// neighbourhood (terrain-core.js's REACH), so each cell is rebaked only when that neighbourhood
// changes - a step reveals a strip of ground and rebakes only the cells it can reach - and keeps
// showing its last image until the new one comes back. Cells nearest the camera are baked first.
//
// Ground state: draw() takes ground (ground-state.js's memory: each cell's state and its key) and
// world (palette and snowline). Snow settling on one tile rebakes only the cells around it.
import { src } from './util.js';
import { textureNames, around, edgePieces, piecesNear, CELL, GROUND_REACH } from './terrain-core.js';

/** Terrain names by the map's known-tile codes (MapDto.Known). */
export const TERRAIN = { F: 'forest', C: 'clearing', P: 'plain', H: 'hills', W: 'water', M: 'marsh', R: 'rock', X: 'mountain', D: 'deepwater', c: 'cavefloor', e: 'caveentrance' };

const CHUNK = 8;            // cells per canvas side: fewer, larger draws per frame
const POOL = Math.max(1, Math.min(8, (navigator.hardwareConcurrency || 4) - 1));
const IN_FLIGHT = 2;        // jobs queued per worker, so none idles while the page posts the next

export function createTerrain() {
  const textures = new Map(), sent = new Set();
  let wanted = null, wantedFor = null, failure = null;
  const workers = Array.from({ length: POOL }, () => {
    const w = new Worker(new URL('./terrain-worker.js', import.meta.url), { type: 'module' });
    w.jobs = 0;
    w.onmessage = ({ data }) => { w.jobs--; done(data); };
    w.onerror = e => { failure = new Error(`terrain worker failed: ${e.message}`); };
    return w;
  });
  // Per map: chunk canvases, and per cell the bake its pixels show and the latest one asked for.
  let W = 0, H = 0, chunks, drawn, asked, sigs = new Map(), sigsFor = null;
  // The map's ravines and cliffs as pieces (a piece depends on its whole chain), per edge list.
  let pieces = [], piecesFor = null;
  const near = new Map(); // cell index -> the pieces in its reach, for the current pieces
  const jobs = new Map(); // id -> { i, cx, cy, sig, W }
  let nextId = 1;

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

  /**
   * What a cell's pixels depend on (terrain-core.js's REACH: terrain three cells around, ground state
   * two), or null when nothing in reach is known and the cell is blank.
   */
  function signature(m, ground, world, cx, cy) {
    const codes = around(W, H, cx, cy, (x, y) => m.known[y * W + x]).join('');
    if (!/[^.]/.test(codes)) return null;
    let sig = world.key + codes;
    for (const p of piecesAround(cx, cy)) sig += `|${p.type}${p.x1},${p.y1},${p.x2},${p.y2},${p.start},${p.total}`;
    for (let y = Math.max(0, cy - GROUND_REACH); y <= Math.min(H - 1, cy + GROUND_REACH); y++)
      for (let x = Math.max(0, cx - GROUND_REACH); x <= Math.min(W - 1, cx + GROUND_REACH); x++) sig += '|' + ground.keyAt(y * W + x);
    return sig;
  }

  function piecesAround(cx, cy) {
    const i = cy * W + cx;
    let list = near.get(i);
    if (!list) near.set(i, list = piecesNear(pieces, cx, cy));
    return list;
  }

  function done({ id, i, pixels }) {
    const job = jobs.get(id);
    jobs.delete(id);
    if (job.W !== W) return; // a bake for a map since replaced
    if (asked.get(i)?.id === id) asked.delete(i);
    if ((drawn.get(i)?.id ?? 0) > id) return; // a newer bake of this cell came back first
    drawn.set(i, { id, sig: job.sig });
    const cx0 = Math.floor(job.cx / CHUNK) * CHUNK, cy0 = Math.floor(job.cy / CHUNK) * CHUNK, key = `${cx0},${cy0}`;
    let canvas = chunks.get(key);
    if (!canvas) {
      chunks.set(key, canvas = document.createElement('canvas'));
      canvas.width = Math.min(CHUNK, W - cx0) * CELL; canvas.height = Math.min(CHUNK, H - cy0) * CELL;
    }
    canvas.getContext('2d').putImageData(new ImageData(pixels, CELL, CELL), (job.cx - cx0) * CELL, (job.cy - cy0) * CELL);
  }

  /**
   * Draws the ground of cells [x0..x1] x [y0..y1] with the map's origin at (ox, oy), one cell CELL
   * pixels wide. m: MapDto (width, height, known); camera: cell the camera centres on;
   * ground: { stateAt(x, y), keyAt(i), version }; world: { palette, snowline, key }.
   */
  function draw(ctx, { m, ox, oy, x0, y0, x1, y1, camera, ground, world }) {
    if (failure) throw failure;
    if (m.width !== W || m.height !== H) { W = m.width; H = m.height; chunks = new Map(); drawn = new Map(); asked = new Map(); sigsFor = null; piecesFor = null; }
    if (wantedFor !== world.key) load(world);
    if (!ready()) return;
    const fresh = wanted.filter(n => !sent.has(n));
    if (fresh.length) {
      for (const w of workers) w.postMessage({ textures: fresh.map(n => [n, textures.get(n)]) });
      for (const n of fresh) sent.add(n);
    }
    if (piecesFor !== m.edges) {
      pieces = edgePieces(m.edges.map(e => ({ ax: e.ax, ay: e.ay, bx: e.bx, by: e.by, type: e.kind })));
      piecesFor = m.edges;
      near.clear();
    }
    // Ravines and cliffs come with the known tiles they lie beside, so m.known keys them too.
    const inputs = `${world.key}|${ground.version}|${m.known}`;
    if (sigsFor !== inputs) { sigs = new Map(); sigsFor = inputs; }

    const todo = [];
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const i = y * W + x;
      let sig = sigs.get(i);
      if (sig === undefined) sigs.set(i, sig = signature(m, ground, world, x, y));
      if (sig !== null && drawn.get(i)?.sig !== sig && asked.get(i)?.sig !== sig) todo.push(i);
    }
    ctx.imageSmoothingEnabled = false;
    for (let cy0 = Math.floor(y0 / CHUNK) * CHUNK; cy0 <= y1; cy0 += CHUNK)
      for (let cx0 = Math.floor(x0 / CHUNK) * CHUNK; cx0 <= x1; cx0 += CHUNK) {
        const canvas = chunks.get(`${cx0},${cy0}`);
        if (canvas) ctx.drawImage(canvas, ox + cx0 * CELL, oy + cy0 * CELL);
      }

    // Hand the cells nearest the camera to whichever workers have room.
    const dist = i => Math.hypot(i % W + 0.5 - camera.x, Math.floor(i / W) + 0.5 - camera.y);
    todo.sort((a, b) => dist(a) - dist(b));
    let next = 0;
    for (const w of workers) while (w.jobs < IN_FLIGHT && next < todo.length) {
      const i = todo[next++], cx = i % W, cy = Math.floor(i / W), id = nextId++, sig = sigs.get(i);
      jobs.set(id, { i, cx, cy, sig, W });
      asked.set(i, { id, sig });
      w.jobs++;
      w.postMessage({ id, i, width: W, height: H, cx, cy, world: { palette: world.palette, snowline: world.snowline },
        terrain: around(W, H, cx, cy, (x, y) => TERRAIN[m.known[y * W + x]] ?? null),
        states: around(W, H, cx, cy, ground.stateAt), pieces: piecesAround(cx, cy) });
    }
  }

  return { draw };
}
