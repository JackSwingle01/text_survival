// The page knows only the host's five calls and the plain-data view they return.
import { dotnet } from './_framework/dotnet.js';

const t0 = window.__t0 ?? 0;
const { getAssemblyExports, getConfig } = await dotnet.create();
const tRuntime = performance.now();
const exports = await getAssemblyExports(getConfig().mainAssemblyName);
const G = exports.Game;
const tExports = performance.now();

const $ = id => document.getElementById(id);
const TILE = 32;
const imgs = new Map();
function img(src) {
  if (!imgs.has(src)) {
    const i = new Image();
    i.src = src;
    i.decode().then(() => draw(view)).catch(() => console.error('missing icon', src));
    imgs.set(src, i);
  }
  return imgs.get(src);
}

let view = null;
let saved = null;

function draw(v) {
  if (!v) return;
  const c = $('world'), ctx = c.getContext('2d');
  const n = v.radius * 2 + 1;
  c.width = c.height = n * TILE;
  ctx.imageSmoothingEnabled = false;
  ctx.fillStyle = '#0d1216';
  ctx.fillRect(0, 0, c.width, c.height);
  v.tiles.forEach((t, i) => {
    if (!t) return;
    const dx = i % n - v.radius, dy = Math.floor(i / n) - v.radius;
    const variant = (((v.x + dx) * 73856093) ^ ((v.y + dy) * 19349663)) >>> 0;
    const k = variant % 8;
    const im = img(`icons/${t}_tile${k === 0 ? '' : k + 1}.png`);
    if (im.complete) ctx.drawImage(im, (i % n) * TILE, Math.floor(i / n) * TILE, TILE, TILE);
  });
  const at = (dx, dy, src) => {
    const im = img(src);
    if (im.complete) ctx.drawImage(im, (dx + v.radius) * TILE, (dy + v.radius) * TILE, TILE, TILE);
  };
  for (const m of v.marks) {
    if (m.kind.startsWith('animal:')) at(m.dx, m.dy, `icons/animals/${m.kind.slice(7)}.png`);
    else if (m.kind === 'npc') at(m.dx, m.dy, 'icons/npc/male_0.png');
    else at(m.dx, m.dy, `icons/${m.kind}.png`);
  }
  at(0, 0, 'icons/player.png');
}

function render(v) {
  view = v;
  $('time').textContent = v.clock.time;
  $('day').textContent = `Day ${v.clock.day} · ${v.clock.season}`;
  $('sky').textContent = `${v.clock.sky}, ${v.clock.airF}°F, wind ${v.clock.windMph} mph`;
  $('feels').textContent = `${v.clock.feelsF}°F`;
  $('place').textContent = v.outcome === 'Surviving' ? v.place : `${v.place} — ${v.outcome}`;
  const bars = [['Warmth', v.vitals.warmth], ['Food', v.vitals.food], ['Water', v.vitals.water], ['Rest', v.vitals.rest]];
  $('vitals').innerHTML = bars.map(([name, pct]) => {
    const on = Math.round(pct * 10);
    return `<div class="gauge ${pct < 0.3 ? 'low' : ''}" title="${Math.round(pct * 100)}%"><span>${name}</span>
      <div class="ticks">${Array.from({ length: 10 }, (_, i) => `<span class="${i < on ? 'on' : ''}"></span>`).join('')}</div></div>`;
  }).join('') + `<div class="effects">${v.vitals.effects.join(', ')}</div><div class="label">core ${v.vitals.coreTempF}°F</div>`;
  $('log').textContent = v.log.slice(-14).join('\n');
  const dock = $('dock');
  dock.innerHTML = '';
  for (const a of v.actions) {
    const b = document.createElement('button');
    b.textContent = a.label;
    b.disabled = v.outcome !== 'Surviving';
    b.onclick = () => act(a.json);
    dock.append(b);
  }
  const s = document.createElement('button'); s.textContent = 'Save';
  s.onclick = () => { const t = performance.now(); saved = G.Save(); l.disabled = false; $('status').textContent = `saved ${(saved.length / 1024).toFixed(0)} KB in ${(performance.now() - t).toFixed(0)} ms`; };
  const l = document.createElement('button'); l.textContent = 'Load'; l.disabled = !saved;
  l.onclick = () => { const t = performance.now(); const v2 = JSON.parse(G.Load(saved)); $('status').textContent = `loaded in ${(performance.now() - t).toFixed(0)} ms`; render(v2); };
  dock.append(s, l);
  draw(v);
}

function act(json) {
  const t = performance.now();
  const v = JSON.parse(G.Act(json));
  $('status').textContent = `act ${(performance.now() - t).toFixed(1)} ms`;
  render(v);
}

const tNew0 = performance.now();
const first = JSON.parse(G.NewGame(1));
const tNew = performance.now();
render(first);
const startup = {
  runtimeReadyMs: tRuntime - t0, exportsReadyMs: tExports - t0, newGameMs: tNew - tNew0, firstViewMs: performance.now() - t0,
};
$('status').textContent = `ready in ${startup.firstViewMs.toFixed(0)} ms`;
window.spike = { startup, G };
console.log('startup', JSON.stringify(startup));

if (new URLSearchParams(location.search).has('bench')) runBench();

async function runBench() {
  const out = $('bench');
  const yieldFrame = () => new Promise(r => setTimeout(r, 0));
  const log = s => { out.textContent += s + '\n'; };
  out.textContent = '';
  // Through the boundary: one Act round trip (step + view JSON + interop), as the UI pays it.
  const acts = [];
  for (let i = 0; i < 5; i++) { const t = performance.now(); G.Act('{"kind":"wait","minutes":60}'); acts.push(performance.now() - t); }
  log(`act wait60 ms: ${acts.map(x => x.toFixed(0)).join(' ')}`);
  G.BenchNewWorld(1); G.BenchStepsAtStart(); await yieldFrame();
  for (let d = 0; d < 30; d++) { const ms = G.BenchAdvanceDay(); log(`day ${d + 1}: ${ms.toFixed(0)} ms`); await yieldFrame(); }
  G.BenchStepsAfter(); G.BenchSave();
  const r = JSON.parse(G.BenchResult());
  r.actWait60Ms = acts;
  r.startup = startup;
  window.benchResult = r;
  log('DONE ' + JSON.stringify(r));
  document.title = 'BENCH DONE';
}
