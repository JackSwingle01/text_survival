// Reads the PNGs the terrain art is painted as, with no dependencies, for the node tools.
import fs from 'node:fs';
import zlib from 'node:zlib';

/** RGBA pixels of an 8-bit truecolour, truecolour-alpha or paletted PNG: { w, h, data }. */
export function decodePng(file) {
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
