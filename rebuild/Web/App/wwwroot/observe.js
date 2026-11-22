// Observer mode (GLOSSARY), development only: a world with no player in it, the whole map as it truly
// is, run at a rate in game minutes per real second. It talks to the game only through client.js's
// observe calls (Host Observer); it decides nothing about the world.
import { createClient } from './client.js';
import { createObserverMap } from './observe-map.js';
import { createCamera, zoomAt, panBy, cellAt, createPacer } from './observe-core.js';
import { clock, esc } from './util.js';

const $ = id => document.getElementById(id);
const params = new URLSearchParams(location.search);
const obs = createClient().observe;
const pacer = createPacer();

/** Hand the renderer new ground at most this often: every chunk on screen rebakes when it changes. */
const GROUND_EVERY_MS = 2000, INSPECT_EVERY_MS = 500;

const S = {
  map: null, view: null, ground: null, cam: null,
  pendingGround: null, groundAt: 0,
  selected: null, hover: null, inspected: null, inspectedAt: 0,
  playing: false, busy: false, armed: null,
};
window.obs = { S, ready: null, client: obs }; // for headless checks (tools/shots.mjs --observe)

const viewport = () => { const r = $('world').getBoundingClientRect(); return { w: r.width, h: r.height }; };
const rate = () => $('rate').value === 'custom' ? Math.max(1, +$('rateCustom').value || 1) : +$('rate').value;

function toast(text) {
  const t = $('toast');
  t.textContent = text; t.hidden = false;
  clearTimeout(toast.timer); toast.timer = setTimeout(() => { t.hidden = true; }, 3000);
}

// ---------------------------------------------------------------- the world

function apply(v) {
  S.view = v;
  if (v.ground) S.pendingGround = v.ground;
  commitGround(false);
  renderBar(); renderLog();
}

function commitGround(force) {
  if (!S.pendingGround || (!force && S.ground && performance.now() - S.groundAt < GROUND_EVERY_MS)) return;
  S.ground = S.pendingGround; S.pendingGround = null; S.groundAt = performance.now();
}

/** One call to the world at a time: pause, step and clicks wait for the call in flight. */
async function call(fn) {
  if (S.busy) return null;
  S.busy = true;
  try { return await fn(); }
  catch (e) { toast(e.message); console.error(e); return null; }
  finally { S.busy = false; }
}

async function advance(minutes) {
  const t0 = performance.now();
  const v = await call(() => obs.advance(minutes));
  if (!v) return;
  pacer.ran(minutes, performance.now() - t0);
  apply(v);
  if (S.selected && performance.now() - S.inspectedAt > INSPECT_EVERY_MS) inspect(S.selected, true);
}

async function newWorld(seed) {
  S.playing = false; pacer.reset();
  const map = await call(() => obs.newWorld(seed));
  if (!map) return;
  S.map = map; S.ground = null; S.selected = null; S.inspected = null;
  S.cam = createCamera(map, viewport());
  renderSpawns();
  apply(await obs.view());
  commitGround(true);
  renderInspector();
  const u = new URL(location.href); u.searchParams.set('seed', seed); history.replaceState(null, '', u);
}

async function inspect(t, quiet = false) {
  S.selected = t;
  const d = await (quiet ? obs.inspect(t.x, t.y).catch(() => null) : call(() => obs.inspect(t.x, t.y)));
  S.inspectedAt = performance.now();
  if (d) { S.inspected = d; renderInspector(); }
}

async function spawnAt(t, keepArmed) {
  const v = await call(() => obs.spawn(S.armed, t.x, t.y));
  if (!v) return;
  apply(v);
  if (!keepArmed) { S.armed = null; renderSpawns(); }
  inspect(t, true);
}

// ---------------------------------------------------------------- time

let lastT = performance.now();
function loop(now) {
  requestAnimationFrame(loop);
  commitGround(false);
  const dt = (now - lastT) / 1000;
  if (!S.playing || !S.view) { lastT = now; return; }
  if (S.busy) return; // the time it waits is banked by the pacer, up to a second
  lastT = now;
  const n = pacer.due(rate(), dt);
  if (n > 0) advance(n);
}
requestAnimationFrame(loop);
setInterval(() => { $('achieved').textContent = S.playing ? `running ${Math.round(pacer.achieved())} min/s` : ''; }, 500);

function setPlaying(on) {
  S.playing = on; pacer.reset(); lastT = performance.now();
  $('play').textContent = on ? 'Pause' : 'Play';
  $('play').classList.toggle('on', on);
}

// ---------------------------------------------------------------- rendering the panels

function renderBar() {
  const c = S.view.clock;
  $('clock').textContent = `Day ${c.day} · ${clock(c.minuteOfDay)} · ${c.season} · ${c.sky} · ${Math.round(c.airF)}°F`;
}

function renderSpawns() {
  $('spawns').innerHTML = S.map.spawnables.map(s =>
    `<button class="obs-btn${S.armed === s.id ? ' on' : ''}" data-spawn="${esc(s.id)}" title="Then click a tile (Shift-click to place several)">${esc(s.label)}</button>`).join('');
  $('world').classList.toggle('spawning', !!S.armed);
}

/** A log line's world minute as day and clock, read off the view's clock (minute 0 is not midnight). */
function when(m) {
  const c = S.view.clock, ago = S.view.minute - m;
  const mod = c.minuteOfDay - ago, days = Math.floor(mod / 1440);
  return `D${c.day + days} ${clock(mod - days * 1440)}`;
}
function renderLog() {
  $('log').innerHTML = [...S.view.log].reverse().map((l, i) =>
    `<li data-i="${S.view.log.length - 1 - i}"><span class="t num">${when(l.line.minute)}</span><span class="${esc(l.line.kind)}">${esc(l.line.text)}</span> <span class="muted num">${l.x},${l.y}</span></li>`).join('');
}

function renderInspector() {
  const d = S.inspected;
  if (!d) { $('inspector').innerHTML = '<div class="muted">Click a tile to see what is there.</div>'; return; }
  const fire = d.fire ? `<div>Fire: ${esc(d.fire.phase)}${d.fire.burning ? ` · ${d.fire.fuelHours} h of fuel` : ''}</div>` : '';
  const features = d.features.length ? `<ul>${d.features.map(f => `<li>${esc(f.name)}${f.undiscovered ? ' <span class="muted">(undiscovered)</span>' : ''}</li>`).join('')}</ul>` : '<div class="muted">Nothing here.</div>';
  const actors = d.actors.map(a => `<div class="actor"><b>#${a.id} ${esc(a.name)}</b> <span class="muted">${esc(a.doing)}</span>
    <ul>${a.lines.map(l => `<li>${esc(l)}</li>`).join('')}</ul></div>`).join('');
  $('inspector').innerHTML = `<h3>${esc(d.place)}</h3><div class="muted">${esc(d.terrain)} · ${d.x},${d.y}</div>
    ${d.story ? `<p>${esc(d.story)}</p>` : ''}${fire}${features}${actors}`;
}

// ---------------------------------------------------------------- input

createObserverMap($('world'), () => S);

const canvas = $('world');
let drag = null;
const tileAt = e => {
  const r = canvas.getBoundingClientRect(), c = cellAt(S.cam, e.clientX - r.left, e.clientY - r.top, viewport());
  const t = { x: Math.floor(c.x), y: Math.floor(c.y) };
  return t.x >= 0 && t.y >= 0 && t.x < S.map.width && t.y < S.map.height ? t : null;
};
canvas.addEventListener('pointerdown', e => { if (!S.cam) return; drag = { x: e.clientX, y: e.clientY, moved: 0 }; canvas.setPointerCapture(e.pointerId); });
canvas.addEventListener('pointermove', e => {
  if (!S.cam) return;
  if (drag) {
    const dx = e.clientX - drag.x, dy = e.clientY - drag.y;
    drag.moved += Math.abs(dx) + Math.abs(dy);
    if (drag.moved > 4) { canvas.classList.add('dragging'); S.cam = panBy(S.cam, dx, dy, S.map); }
    drag.x = e.clientX; drag.y = e.clientY;
  }
  S.hover = tileAt(e);
});
canvas.addEventListener('pointerup', e => {
  const wasClick = drag && drag.moved <= 4;
  drag = null; canvas.classList.remove('dragging');
  if (!wasClick || !S.cam) return;
  const t = tileAt(e);
  if (!t) return;
  if (S.armed) spawnAt(t, e.shiftKey); else inspect(t);
});
canvas.addEventListener('pointerleave', () => { S.hover = null; });
canvas.addEventListener('wheel', e => {
  if (!S.cam) return;
  e.preventDefault();
  const r = canvas.getBoundingClientRect();
  S.cam = zoomAt(S.cam, Math.exp(-e.deltaY * 0.0015), e.clientX - r.left, e.clientY - r.top, viewport(), S.map);
}, { passive: false });

const zoomCentre = f => { const v = viewport(); S.cam = zoomAt(S.cam, f, v.w / 2, v.h / 2, v, S.map); };
const step = () => { if (!S.playing && S.view) advance(Math.max(1, Math.round(rate()))); };

$('play').onclick = () => setPlaying(!S.playing);
$('step').onclick = step;
$('rate').onchange = () => { $('rateCustom').hidden = $('rate').value !== 'custom'; pacer.reset(); };
$('rateCustom').onchange = () => pacer.reset();
$('newWorld').onclick = () => newWorld(+$('seed').value || 1);
$('spawns').onclick = e => {
  const b = e.target.closest('[data-spawn]');
  if (!b) return;
  S.armed = S.armed === b.dataset.spawn ? null : b.dataset.spawn;
  renderSpawns();
};
$('log').onclick = e => {
  const li = e.target.closest('li[data-i]');
  if (!li) return;
  const l = S.view.log[+li.dataset.i];
  S.cam = { ...S.cam, x: l.x + 0.5, y: l.y + 0.5, scale: Math.max(S.cam.scale, 48) };
  inspect({ x: l.x, y: l.y });
};
addEventListener('keydown', e => {
  if (e.target.closest('input, select') || !S.cam) return;
  if (e.key === ' ') { e.preventDefault(); setPlaying(!S.playing); }
  else if (e.key === '.') step();
  else if (e.key === '+' || e.key === '=') zoomCentre(1.25);
  else if (e.key === '-' || e.key === '_') zoomCentre(0.8);
  else if (e.key === 'Escape') { S.armed = null; renderSpawns(); }
});

// ---------------------------------------------------------------- start

async function start() {
  const seed = +(params.get('seed') ?? 1);
  $('seed').value = seed;
  try {
    await newWorld(seed);
    if (!S.map) throw new Error('the world did not start');
    for (let i = 0; i < +(params.get('npcs') ?? 0); i++) apply(await obs.spawn('npc', S.map.campX, S.map.campY));
    $('loading').hidden = true;
  } catch (e) {
    $('loadingNote').textContent = String(e.message ?? e);
    throw e;
  }
}
window.obs.ready = start();
