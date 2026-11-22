// Observer mode's map: the whole valley as it truly is, no fog. The map is a MapDto (MapView.Truth), the
// one the player's renderer draws. Zoomed in (a cell 24 px or more) the ground, ravines, routes and prints
// are terrain.js's, drawn under the camera's scale; zoomed out it is one pixel a tile, coloured by terrain
// and snow, so the whole map never has to be baked at 48 px a cell.
// Actors ease from where they were drawn toward where the latest view puts them.
import { icon } from './util.js';
import { createTerrain, TERRAIN } from './terrain.js';
import { CELL } from './terrain-core.js';
import { createGround, worldState } from './ground-state.js';
import { FLAT } from './observe-core.js';

/** Below this many pixels a cell the painted ground gives way to the flat map. */
export const PAINTED_FROM_SCALE = 24;

const SNOW = [232, 238, 242], ICE = [188, 211, 223];

/** get(): { map (MapDto, MapView.Truth), view (ObserverViewDto), cam, selected, hover }. */
export function createObserverMap(canvas, get) {
  const ctx = canvas.getContext('2d');
  const terrain = createTerrain(), ground = createGround(code => TERRAIN[code]);
  const flat = document.createElement('canvas');
  let flatFor = null, dpr = 1;
  const drawn = new Map(); // actor id -> { x, y } where it was last drawn
  let last = performance.now();

  new ResizeObserver(() => {
    const r = canvas.getBoundingClientRect();
    dpr = Math.min(2, devicePixelRatio || 1);
    canvas.width = Math.round(r.width * dpr); canvas.height = Math.round(r.height * dpr);
  }).observe(canvas);

  function bakeFlat(m) {
    if (flatFor === m) return;
    flatFor = m;
    flat.width = m.width; flat.height = m.height;
    const g = flat.getContext('2d'), img = g.createImageData(m.width, m.height);
    const snow = new Map(m.surface.map(s => [s.i, s.snowCm ?? 0])), frozen = new Set(m.frozenWater);
    for (let i = 0; i < m.width * m.height; i++) {
      const name = TERRAIN[m.known[i]], base = FLAT[name];
      if (!base) throw new Error(`no flat colour for terrain ${name}`);
      const cover = frozen.has(i) ? 1 : Math.min(1, Math.max(0, ((snow.get(i) ?? 0) - 0.5) / 15));
      const to = frozen.has(i) ? ICE : SNOW;
      for (let k = 0; k < 3; k++) img.data[i * 4 + k] = base[k] + (to[k] - base[k]) * cover * 0.85;
      img.data[i * 4 + 3] = 255;
    }
    g.putImageData(img, 0, 0);
  }

  const sprite = (name, x, y, size, alpha = 1) => {
    const im = icon(name); if (!im) return false;
    ctx.globalAlpha = alpha; ctx.imageSmoothingEnabled = false; ctx.drawImage(im, x, y, size, size); ctx.globalAlpha = 1;
    return true;
  };
  function label(text, x, y, color = '#eadfc8') {
    ctx.font = '600 13px "Barlow Semi Condensed", "Arial Narrow", sans-serif';
    const w = ctx.measureText(text).width + 8;
    ctx.fillStyle = 'rgba(22,17,13,.85)'; ctx.fillRect(Math.round(x - w / 2), Math.round(y - 9), Math.round(w), 18);
    ctx.fillStyle = color; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(text, Math.round(x), Math.round(y));
  }
  const colourOf = a => a.kind === 'npc' ? '#ffc93c' : a.predator ? '#d13b1f' : '#9fbb86';

  function frame(now) {
    requestAnimationFrame(frame);
    const S = get(), dt = Math.min(0.1, (now - last) / 1000); last = now;
    const r = canvas.getBoundingClientRect(), w = r.width, h = r.height;
    if (!S.map || !S.view || w === 0) return;
    const { cam, map: m, view: V } = S, s = cam.scale;
    // screen position of a cell's top-left corner
    const sx = x => w / 2 + (x - cam.x) * s, sy = y => h / 2 + (y - cam.y) * s;
    const x0 = Math.max(0, Math.floor(cam.x - w / 2 / s)), x1 = Math.min(m.width - 1, Math.ceil(cam.x + w / 2 / s));
    const y0 = Math.max(0, Math.floor(cam.y - h / 2 / s)), y1 = Math.min(m.height - 1, Math.ceil(cam.y + h / 2 / s));

    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.fillStyle = '#0d1216'; ctx.fillRect(0, 0, w, h);
    if (s >= PAINTED_FROM_SCALE) {
      ground.see(m);
      const k = s / CELL; // art pixels to screen pixels
      ctx.setTransform(dpr * k, 0, 0, dpr * k, 0, 0);
      const ox = sx(0) / k, oy = sy(0) / k;
      terrain.draw(ctx, { m, ox, oy, x0, y0, x1, y1, camera: cam, ground, world: worldState(V.clock) });
      terrain.drawPrints(ctx, { m, ox, oy, x0, y0, x1, y1, ground });
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    } else {
      bakeFlat(m);
      ctx.imageSmoothingEnabled = false;
      ctx.drawImage(flat, sx(0), sy(0), m.width * s, m.height * s);
    }

    // places: fires, shelters, carcasses, stashes
    const icons = s >= 16, sp = Math.min(32, s * 0.7);
    for (const p of V.places) {
      if (p.x < x0 - 1 || p.x > x1 + 1 || p.y < y0 - 1 || p.y > y1 + 1) continue;
      const px = sx(p.x), py = sy(p.y);
      const burning = p.fire && p.fire !== 'Cold' && p.fire !== 'Embers';
      if (burning) {
        const g = ctx.createRadialGradient(px + s / 2, py + s / 2, 1, px + s / 2, py + s / 2, Math.max(4, s * 1.4));
        g.addColorStop(0, 'rgba(255,170,60,.5)'); g.addColorStop(1, 'rgba(255,120,30,0)');
        ctx.fillStyle = g; ctx.fillRect(px - s * 1.5, py - s * 1.5, s * 4, s * 4);
      }
      if (!icons) continue;
      if (p.shelter) sprite('shelter', px + 2, py + 2, sp * 0.8);
      if (p.carcass) sprite('carcass', px + s - sp * 0.7, py + s - sp * 0.7, sp * 0.6);
      if (p.stash) sprite('cache', px + 4, py + s - sp * 0.6, sp * 0.5);
      if (p.fire && p.fire !== 'Cold') sprite(burning ? 'fire' : 'embers', px + s - sp * 0.8, py + s - sp * 0.8, sp * 0.75);
    }

    // actors, eased toward where the latest view puts them (mid-crossing: part way to the next tile)
    const ease = Math.min(1, dt * 8), seen = new Set(), onTile = new Map();
    for (const a of V.actors) {
      seen.add(a.id);
      // several on one tile stand side by side rather than on top of each other
      const k = `${a.x},${a.y}`, nth = onTile.get(k) ?? 0; onTile.set(k, nth + 1);
      const fan = nth === 0 ? 0 : 0.28 * Math.ceil(nth / 2) * (nth % 2 ? 1 : -1);
      const tx = (a.toX != null ? a.x + (a.toX - a.x) * a.crossingPct : a.x) + fan, ty = (a.toY != null ? a.y + (a.toY - a.y) * a.crossingPct : a.y) + Math.abs(fan) * 0.5;
      let d = drawn.get(a.id);
      if (!d || Math.abs(d.x - tx) + Math.abs(d.y - ty) > 6) drawn.set(a.id, d = { x: tx, y: ty }); // a jump, not a walk
      d.x += (tx - d.x) * ease; d.y += (ty - d.y) * ease;
      if (d.x < x0 - 2 || d.x > x1 + 2 || d.y < y0 - 2 || d.y > y1 + 2) continue;
      const cx = sx(d.x) + s / 2, cy = sy(d.y) + s / 2, size = Math.max(10, Math.min(40, s * 0.75));
      if (s < 12 || !sprite(a.icon, cx - size / 2, cy - size / 2, size)) {
        ctx.fillStyle = colourOf(a); ctx.beginPath(); ctx.arc(cx, cy, Math.max(2.5, Math.min(6, s / 3)), 0, Math.PI * 2); ctx.fill();
        ctx.strokeStyle = 'rgba(13,18,22,.8)'; ctx.lineWidth = 1; ctx.stroke();
      }
      if (s >= 40) label(`${a.count > 1 ? `${a.count} ` : ''}${a.doing}`, cx, cy - size / 2 - 8, a.kind === 'npc' ? '#ffc93c' : '#b3a68e');
    }
    for (const id of drawn.keys()) if (!seen.has(id)) drawn.delete(id);

    if (s >= PAINTED_FROM_SCALE)
      for (const n of m.named) if (n.x >= x0 && n.x <= x1 && n.y >= y0 && n.y <= y1) label(n.name, sx(n.x) + s / 2, sy(n.y) + s + 8);

    const box = (t, color) => { if (!t) return; ctx.strokeStyle = color; ctx.lineWidth = 2; ctx.strokeRect(sx(t.x) + 1, sy(t.y) + 1, Math.max(2, s - 2), Math.max(2, s - 2)); };
    box(S.hover, 'rgba(234,223,200,.7)');
    box(S.selected, '#ffc93c');

    // the light: night by the clock
    const c = V.clock, mod = c.minuteOfDay, rise = c.lightFromMinute, set = c.darkFromMinute;
    const night = mod < rise - 60 || mod > set + 60 ? 0.45 : mod < rise ? 0.45 * (rise - mod) / 60 : mod > set ? 0.45 * (mod - set) / 60 : 0;
    if (night > 0) { ctx.fillStyle = `rgba(14,22,40,${night})`; ctx.fillRect(0, 0, w, h); }
  }
  requestAnimationFrame(frame);
}
