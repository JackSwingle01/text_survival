// Renders a terrain map to a PNG through the page's own renderer (wwwroot/terrain-core.js), with no
// browser: for checking the map art and the ground states without building the app.
//
//   node terrain-preview.mjs <map.json> <out.png> [--state <json>] [--world <json>]
//
// map.json: { "W": width, "H": height, "terrain": [[ "forest", "deepwater", ... ], ...] } (one row
// per y, lower-case terrain names; null for unknown). --state is one cell state for every cell and
// --world the world state (terrain-core.js); both default to winter.
// fixtures/seed17.json is a 64 x 32 crop of the world that seed 17 generates.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';
import { createBaker, textureNames, WINTER, WINTER_WORLD, CELL } from '../App/wwwroot/terrain-core.js';

const args = process.argv.slice(2);
const opt = (name, dflt) => { const i = args.indexOf(name); return i >= 0 ? JSON.parse(args[i + 1]) : dflt; };
if (args.length < 2) { console.error('usage: node terrain-preview.mjs <map.json> <out.png> [--state <json>] [--world <json>]'); process.exit(2); }
const icons = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../assets/icons');

function decodePng(file) {
  const buf = fs.readFileSync(file);
  let pos = 8, w = 0, h = 0, type = 0, depth = 0, palette = null, trns = null;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos), kind = buf.toString('latin1', pos + 4, pos + 8), data = buf.subarray(pos + 8, pos + 8 + len);
    if (kind === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); depth = data[8]; type = data[9]; }
    else if (kind === 'PLTE') palette = data;
    else if (kind === 'tRNS') trns = data;
    else if (kind === 'IDAT') idat.push(data);
    pos += 12 + len;
  }
  if (depth !== 8 || ![2, 3, 6].includes(type)) throw new Error(`${file}: unsupported PNG (depth ${depth}, type ${type})`);
  const bpp = type === 6 ? 4 : type === 2 ? 3 : 1, raw = zlib.inflateSync(Buffer.concat(idat)), row = w * bpp;
  const px = Buffer.alloc(h * row);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (row + 1)], src = raw.subarray(y * (row + 1) + 1, (y + 1) * (row + 1));
    for (let x = 0; x < row; x++) {
      const a = x >= bpp ? px[y * row + x - bpp] : 0, b = y ? px[(y - 1) * row + x] : 0, c = x >= bpp && y ? px[(y - 1) * row + x - bpp] : 0;
      const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
      const pred = [0, a, b, (a + b) >> 1, pa <= pb && pa <= pc ? a : pb <= pc ? b : c][f];
      px[y * row + x] = (src[x] + pred) & 255;
    }
  }
  const data = new Uint8Array(w * h * 4);
  for (let i = 0; i < w * h; i++) {
    if (type === 6) data.set(px.subarray(i * 4, i * 4 + 4), i * 4);
    else if (type === 2) { data.set(px.subarray(i * 3, i * 3 + 3), i * 4); data[i * 4 + 3] = 255; }
    else { const k = px[i]; data.set(palette.subarray(k * 3, k * 3 + 3), i * 4); data[i * 4 + 3] = trns && k < trns.length ? trns[k] : 255; }
  }
  return { w, h, data };
}

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
const map = { width: M.W, height: M.H, terrainAt: (x, y) => M.terrain[y][x] };
const baker = createBaker(map, () => state, world, tex);
const out = new Uint8Array(M.W * CELL * M.H * CELL * 4);
const t0 = performance.now();
for (let cy = 0; cy < M.H; cy++) for (let cx = 0; cx < M.W; cx++) baker.bakeCell(out, 0, 0, M.W, cx, cy);
baker.bakeSprites(out, 0, 0, M.W, M.H);
const ms = performance.now() - t0;
encodePng(args[1], M.W * CELL, M.H * CELL, out);
console.log(`${args[1]}: ${M.W}x${M.H} cells in ${Math.round(ms)} ms (${(ms / (M.W * M.H)).toFixed(2)} ms per cell)`);
