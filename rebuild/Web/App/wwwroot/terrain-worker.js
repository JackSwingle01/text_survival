// Bakes map cells off the page's thread (terrain.js keeps a pool of these), so baking runs on every
// core and never costs the page a frame. In: { textures: [[name, { w, h, data }]] } once per
// palette, then jobs { id, i, ...bakeAlone's job }. Out: { id, i, pixels }.
import { bakeAlone } from './terrain-core.js';

const textures = new Map();
const tex = name => {
  const t = textures.get(name);
  if (!t) throw new Error(`terrain texture not sent to the worker: ${name}`);
  return t;
};

onmessage = ({ data }) => {
  if (data.textures) { for (const [name, t] of data.textures) textures.set(name, t); return; }
  const pixels = bakeAlone(data, tex);
  postMessage({ id: data.id, i: data.i, pixels }, [pixels.buffer]);
};
