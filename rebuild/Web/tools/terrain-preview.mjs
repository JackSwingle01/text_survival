// Renders a terrain map to a PNG through the page's own renderer (wwwroot/terrain-core.js), with no
// browser: for checking the map art and the ground states without building the app.
//
//   node terrain-preview.mjs <map.json> <out.png> [--state <json>] [--world <json>]
//
// map.json: { "W": width, "H": height, "terrain": [[ "forest", "deepwater", ... ], ...] } (one row
// per y, lower-case terrain names; null for unknown). --state is one cell state for every cell, or
// states by terrain name with "*" for the rest; --world is the world state (terrain-core.js). Both
// default to winter.
// fixtures/seed17.json is a 64 x 32 crop of the world that seed 17 generates.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';
import { createBaker, textureNames, trackMarks, marked, snowyCell, WINTER, WINTER_WORLD, CELL } from '../App/wwwroot/terrain-core.js';
import { decodePng } from './png.mjs';

const args = process.argv.slice(2);
const opt = (name, dflt) => { const i = args.indexOf(name); return i >= 0 ? JSON.parse(args[i + 1]) : dflt; };
if (args.length < 2) { console.error('usage: node terrain-preview.mjs <map.json> <out.png> [--state <json>] [--world <json>]'); process.exit(2); }
const icons = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../assets/icons');

function encodePng(file, w, h, rgba) {
  const raw = Buffer.alloc(h * (w * 4 + 1));
  for (let y = 0; y < h; y++) Buffer.from(rgba.buffer, rgba.byteOffset + y * w * 4, w * 4).copy(raw, y * (w * 4 + 1) + 1);
  const chunk = (kind, data) => {
    const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
    const body = Buffer.concat([Buffer.from(kind, 'latin1'), data]), crc = Buffer.alloc(4);
    crc.writeUInt32BE(zlib.crc32(body)); return Buffer.concat([len, body, crc]);
  };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w); ihdr.writeUInt32BE(h, 4); ihdr.set([8, 6, 0, 0, 0], 8);
  fs.writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]));
}

const M = JSON.parse(fs.readFileSync(args[0], 'utf8'));
const state = opt('--state', WINTER), world = opt('--world', WINTER_WORLD);
const textures = new Map(textureNames(world).map(n => [n, decodePng(path.join(icons, `${n}.png`))]));
const tex = name => { const t = textures.get(name); if (!t) throw new Error(`missing terrain texture ${name}`); return t; };
// edges: [[ax, ay, bx, by, "ravine" | "cliff"], ...] between 4-adjacent cells, optional;
// trails: [[ax, ay, bx, by, "trace" | "path" | "trail"], ...] likewise.
const map = { width: M.W, height: M.H, terrainAt: (x, y) => M.terrain[y][x],
  edges: (M.edges ?? []).map(([ax, ay, bx, by, type]) => ({ ax, ay, bx, by, type })),
  trails: (M.trails ?? []).map(([ax, ay, bx, by, tier]) => ({ ax, ay, bx, by, tier })) };
const byTerrain = Object.values(state).some(v => typeof v === 'object');
if (byTerrain && !('*' in state)) throw new Error('--state by terrain needs a "*" entry for the rest');
const stateAt = byTerrain ? (x, y) => state[M.terrain[y][x]] ?? state['*'] : () => state;
const baker = createBaker(map, stateAt, world, tex);
const out = new Uint8Array(M.W * CELL * M.H * CELL * 4);
const t0 = performance.now();
for (let cy = 0; cy < M.H; cy++) for (let cx = 0; cx < M.W; cx++) baker.bakeCell(out, 0, 0, M.W, cx, cy);
baker.bakeSprites(out, 0, 0, M.W, M.H);
// tracks: [[x, y, maker, heading, pattern, traffic, ageMinutes, freshness], ...], optional, over everything
// that is ground (the page draws them over its baked chunks the same way).
for (const [x, y, maker, heading, pattern, traffic, , freshness] of M.tracks ?? [])
  for (const [mx, my, kind, f] of trackMarks({ x, y, maker, heading, pattern, traffic, freshness })) {
    const px = x * CELL + mx, py = y * CELL + my, p = baker.classify(px, py);
    if (!p || p.water || p.mountain || (p.feature && p.feature !== 'cliffshadow')) continue;
    const i = (py * M.W * CELL + px) * 4;
    out.set(marked([out[i], out[i + 1], out[i + 2]], kind, f, snowyCell(stateAt(x, y)) ? 'snow' : 'earth'), i);
  }
const ms = performance.now() - t0;
encodePng(args[1], M.W * CELL, M.H * CELL, out);
console.log(`${args[1]}: ${M.W}x${M.H} cells in ${Math.round(ms)} ms (${(ms / (M.W * M.H)).toFixed(2)} ms per cell)`);
