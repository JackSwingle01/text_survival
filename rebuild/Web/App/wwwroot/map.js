// The map: drawn from what the player knows (view.map), never from the world. Explored ground in
// colour where it is in sight now, desaturated where it is only remembered, dark where it was never
// seen. Fires, shelters and animals are drawn as last seen, with their age when out of sight.
// The ground itself is terrain.js: one art pixel per CSS pixel, so cells are CELL (48) wide on every
// screen and stay crisp at a device pixel ratio of 1 or 2.
import { icon, ago } from './util.js';
import { createTerrain, TERRAIN } from './terrain.js';
import { CELL } from './terrain-core.js';
import { createGround, worldState } from './ground-state.js';
const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;

/** state(): { view, drawPos: {x,y}, drawerOpen, selected: {x,y}|null }. */
export function createMap(canvas, state, { onHover, onClick }) {
  const ctx = canvas.getContext('2d');
  const TS = CELL, terrain = createTerrain(), ground = createGround(code => TERRAIN[code]);
  let dpr = 1, cam = null, hover = null, flakes = [], last = performance.now();
  let visible = new Set(), visibleFor = null;

  function resize() {
    const r = canvas.getBoundingClientRect();
    dpr = Math.min(2, devicePixelRatio || 1);
    canvas.width = Math.round(r.width * dpr); canvas.height = Math.round(r.height * dpr);
    flakes = Array.from({ length: Math.round(r.width * r.height / 9000) }, () => ({ x: Math.random() * r.width, y: Math.random() * r.height, s: 1 + Math.random() * 1.5, v: 12 + Math.random() * 20 }));
  }
  new ResizeObserver(resize).observe(canvas);

  const sprite = (name, px, py, size, alpha = 1) => {
    const im = icon(name); if (!im) return;
    ctx.globalAlpha = alpha; ctx.imageSmoothingEnabled = false; ctx.drawImage(im, px, py, size, size); ctx.globalAlpha = 1;
  };
  function label(text, px, py, color = '#eadfc8') {
    ctx.font = '600 15px "Barlow Semi Condensed", "Arial Narrow", sans-serif';
    const w = ctx.measureText(text).width + 10;
    ctx.fillStyle = 'rgba(22,17,13,.88)'; ctx.fillRect(Math.round(px - w / 2), Math.round(py - 10), Math.round(w), 20);
    ctx.fillStyle = color; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(text, Math.round(px), Math.round(py));
  }
  function brackets(px, py, color) {
    ctx.fillStyle = color; const L = 9, k = 2;
    for (const [a, b, sx, sy] of [[px, py, 1, 1], [px + TS, py, -1, 1], [px, py + TS, 1, -1], [px + TS, py + TS, -1, -1]]) {
      ctx.fillRect(sx > 0 ? a : a - L, sy > 0 ? b : b - k, L, k);
      ctx.fillRect(sx > 0 ? a : a - k, sy > 0 ? b : b - L, k, L);
    }
  }

  function frame(now) {
    requestAnimationFrame(frame);
    const S = state(), v = S.view;
    const dt = Math.min(0.1, (now - last) / 1000); last = now;
    const r = canvas.getBoundingClientRect(), w = r.width, h = r.height;
    if (!v || canvas.hidden || w === 0) return;
    const m = v.map, W = m.width, H = m.height;
    if (visibleFor !== m.visible) { visible = new Set(m.visible); visibleFor = m.visible; }
    const known = (x, y) => x >= 0 && y >= 0 && x < W && y < H && m.known.charCodeAt(y * W + x) !== 46; // '.'
    const inSight = (x, y) => visible.has(y * W + x);

    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.fillStyle = '#0d1216'; ctx.fillRect(0, 0, w, h);
    const shift = S.drawerOpen && w > 860 ? 232 / TS : 0;
    const tx = S.drawPos.x + 0.5 + shift, ty = S.drawPos.y + 0.5;
    if (!cam) cam = { x: tx, y: ty };
    cam.x += (tx - cam.x) * Math.min(1, dt * 5); cam.y += (ty - cam.y) * Math.min(1, dt * 5);
    const ox = Math.round(w / 2 - cam.x * TS), oy = Math.round(h / 2 - cam.y * TS);
    const x0 = Math.max(0, Math.floor(-ox / TS)), y0 = Math.max(0, Math.floor(-oy / TS));
    const x1 = Math.min(W - 1, Math.ceil((w - ox) / TS)), y1 = Math.min(H - 1, Math.ceil((h - oy) / TS));
    const at = (x, y) => [ox + x * TS, oy + y * TS];
    frame.ox = ox; frame.oy = oy; frame.TS = TS;

    // ground: only what is known is drawn at all
    ground.see(m);
    terrain.draw(ctx, { m, ox, oy, x0, y0, x1, y1, camera: cam, ground, world: worldState(v.clock) });
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++)
      if (m.known[y * W + x] === 'e') { const [px, py] = at(x, y); sprite('cave_entrance', px + TS / 8, py + TS / 8, TS * 0.75); }
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const [px, py] = at(x, y);
      if (!known(x, y)) { ctx.fillStyle = 'rgba(13,18,22,.96)'; ctx.fillRect(px, py, TS, TS); }
      else if (!inSight(x, y)) {
        ctx.globalCompositeOperation = 'saturation'; ctx.fillStyle = 'rgba(128,128,128,.85)'; ctx.fillRect(px, py, TS, TS);
        ctx.globalCompositeOperation = 'source-over'; ctx.fillStyle = 'rgba(30,44,58,.34)'; ctx.fillRect(px, py, TS, TS);
      }
    }
    // a dithered edge where the known meets the unknown
    ctx.fillStyle = 'rgba(13,18,22,.6)';
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      if (known(x, y)) continue;
      const [px, py] = at(x, y);
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        if (!known(x + dx, y + dy)) continue;
        for (let i = 0; i < TS; i += 4) for (let j = 0; j < 8; j += 4) {
          if (((i + j) / 4) % 2) continue;
          const ex = dx === 1 ? px + TS - 8 + j : dx === -1 ? px + j : px + i;
          const ey = dy === 1 ? py + TS - 8 + j : dy === -1 ? py + j : py + i;
          ctx.fillRect(ex, ey, 4, 4);
        }
      }
    }

    const sp = 32; // 16 px art at an exact 2x on 48 px cells
    const onScreen = (x, y) => x >= x0 - 1 && x <= x1 + 1 && y >= y0 - 1 && y <= y1 + 1;
    for (const t of m.tracks) {
      if (!onScreen(t.x, t.y)) continue;
      const [px, py] = at(t.x, t.y), off = t.maker === 'human' ? -8 : t.maker === 'paw' ? 6 : 0;
      sprite(`tracks/${t.maker === 'human' ? 'boot' : t.maker}`, px + TS / 2 - 12 + off, py + TS / 2 - 12 + off, 24, t.ageMinutes > 720 ? 0.45 : 0.8);
    }
    // the camp: always known
    { const [px, py] = at(m.campX, m.campY); if (onScreen(m.campX, m.campY)) sprite('shelter', px + 2, py + 2, sp * 0.8, inSight(m.campX, m.campY) ? 1 : 0.6); }
    for (const p of m.places) {
      if (!onScreen(p.x, p.y)) continue;
      const [px, py] = at(p.x, p.y), vis = inSight(p.x, p.y), age = v.minute - p.seenMinute;
      if (p.shelter && !(p.x === m.campX && p.y === m.campY)) sprite('shelter', px + 2, py + 2, sp * 0.8, vis ? 1 : 0.6);
      if (p.carcass) sprite('carcass', px + TS - sp * 0.7, py + TS - sp * 0.7, sp * 0.6, vis ? 1 : 0.6);
      if (p.stash) sprite('cache', px + 4, py + TS - sp * 0.6, sp * 0.5, vis ? 1 : 0.6);
      if (p.den) sprite('danger', px + TS - sp * 0.55, py + 4, sp * 0.5, vis ? 1 : 0.7);
      if (p.fire && p.fire !== 'Cold') {
        const burning = p.fire !== 'Embers';
        if (vis && burning) {
          const fl = reduce ? 1 : 0.85 + Math.sin(now / 90) * 0.08 + Math.random() * 0.07;
          const g = ctx.createRadialGradient(px + TS / 2, py + TS * 0.62, 2, px + TS / 2, py + TS * 0.62, TS * 1.4 * fl);
          g.addColorStop(0, 'rgba(255,170,60,.42)'); g.addColorStop(1, 'rgba(255,120,30,0)');
          ctx.fillStyle = g; ctx.fillRect(px - TS, py - TS, TS * 3, TS * 3);
        }
        sprite(burning ? 'fire' : 'embers', px + TS - sp * 0.8, py + TS - sp * 0.8, sp * 0.75, vis ? 1 : 0.55);
        if (!vis) label(`${p.fire} · ${ago(age)}`, px + TS / 2, py - 8, '#b3a68e');
      }
    }
    // what was seen, where it was seen
    for (const s of m.sightings) {
      if (!onScreen(s.x, s.y)) continue;
      const [px, py] = at(s.x, s.y), now_ = s.minute === v.minute && inSight(s.x, s.y);
      sprite(s.species, px + TS / 2 - sp / 2, py + TS / 2 - sp / 2, sp, now_ ? 1 : 0.55);
      if (!now_) label(`${s.species.split('/').pop().replace(/_\d$/, '')}${s.count > 1 ? ` ×${s.count}` : ''} · ${ago(v.minute - s.minute)}`, px + TS / 2, py - 6, '#cf6f4c');
      else if (s.count > 1) label(`×${s.count}`, px + TS - 10, py + 10);
    }
    for (const n of m.named) if (onScreen(n.x, n.y)) { const [px, py] = at(n.x, n.y); label(n.name, px + TS / 2, py + TS + 8); }

    // the player, walking or standing
    { const px = ox + S.drawPos.x * TS, py = oy + S.drawPos.y * TS, bob = S.walking && !reduce ? Math.abs(Math.sin(now / 80)) * 2 : 0;
      ctx.fillStyle = 'rgba(0,0,0,.18)'; ctx.fillRect(px + TS / 2 - 10, py + TS - 7, 20, 4);
      sprite('player', Math.round(px + TS / 2 - sp / 2), Math.round(py + TS - sp - 4 - bob), sp); }

    if (S.selected) { const [px, py] = at(S.selected.x, S.selected.y); brackets(px, py, '#ffc93c'); }
    if (hover) { const [px, py] = at(hover.x, hover.y); brackets(px, py, '#eadfc8'); }

    // the light: night by the clock, a warm dusk
    const mod = S.displayMinuteOfDay ?? v.clock.minuteOfDay, rise = v.clock.sunriseMinute, set = v.clock.sunsetMinute;
    const night = mod < rise - 60 || mod > set + 60 ? 0.6 : mod < rise ? 0.6 * (rise - mod) / 60 : mod > set ? 0.6 * (mod - set) / 60 : 0;
    if (night > 0) { ctx.fillStyle = `rgba(14,22,40,${night})`; ctx.fillRect(0, 0, w, h); }
    if (mod > set - 90 && mod < set + 30) { ctx.fillStyle = `rgba(140,80,50,${0.12 * (1 - Math.abs(mod - (set - 30)) / 60)})`; ctx.fillRect(0, 0, w, h); }
    if (!reduce && /snow|blizzard|whiteout/i.test(v.clock.sky)) {
      ctx.fillStyle = 'rgba(240,246,248,.75)';
      const heavy = /heavy|blizzard|whiteout/i.test(v.clock.sky);
      for (const f of flakes) { f.y += f.v * dt * (heavy ? 2 : 1); f.x -= f.v * 0.35 * dt * (heavy ? 3 : 1); if (f.y > h) { f.y = -4; f.x = Math.random() * (w + 60); } if (f.x < -4) f.x = w; ctx.fillRect(Math.round(f.x), Math.round(f.y), f.s, f.s); }
    }
    const vg = ctx.createRadialGradient(w / 2, h / 2, Math.min(w, h) * 0.35, w / 2, h / 2, Math.max(w, h) * 0.75);
    vg.addColorStop(0, 'rgba(13,18,22,0)'); vg.addColorStop(1, 'rgba(13,18,22,.55)'); ctx.fillStyle = vg; ctx.fillRect(0, 0, w, h);
  }
  requestAnimationFrame(frame);

  const tileAt = e => {
    const r = canvas.getBoundingClientRect();
    if (frame.TS == null) return null;
    const x = Math.floor((e.clientX - r.left - frame.ox) / frame.TS), y = Math.floor((e.clientY - r.top - frame.oy) / frame.TS);
    const m = state().view?.map;
    return m && x >= 0 && y >= 0 && x < m.width && y < m.height ? { x, y } : null;
  };
  canvas.addEventListener('mousemove', e => { hover = tileAt(e); onHover(hover, e); });
  canvas.addEventListener('mouseleave', () => { hover = null; onHover(null); });
  canvas.addEventListener('click', e => { const t = tileAt(e); if (t) onClick(t); });

  return {
    known: (x, y) => { const m = state().view.map; return m.known.charCodeAt(y * m.width + x) !== 46; },
    inSight: (x, y) => visible.has(y * state().view.map.width + x),
    terrainName: code => TERRAIN[code],
  };
}
