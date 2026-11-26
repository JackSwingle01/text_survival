// Headless checks of the web UI: drives the published page in Chrome over the DevTools protocol,
// screenshots each step into rebuild/Web/shots/, and fails on any console error or uncaught exception.
//
//   node shots.mjs <wwwroot> --run                 the scripted run: walk, fire, forage, make a staff, save, load
//   node shots.mjs <wwwroot> --shot fire [--shot make ...] [--phone]   one state each (see STATES)
//   node shots.mjs <wwwroot> --all [--phone]        every state
// --phone also shoots each state on phones (portrait 390×664 and 402×714, landscape 844×390 and 932×430, touch on) and
// fails when the layout does not fit the screen there (PHONE_LAYOUT).
// Options: --out <dir> (default rebuild/Web/shots), --seed <n>, --chrome <path>.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { serve } from './serve.mjs';

const args = process.argv.slice(2);
const opt = (name, dflt) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : dflt; };
const root = args[0];
if (!root || root.startsWith('--')) { console.error('usage: node shots.mjs <wwwroot> --run | --shot <state>... | --all [--phone]'); process.exit(2); }
const here = path.dirname(fileURLToPath(import.meta.url));
const out = path.resolve(opt('--out', path.join(here, '..', 'shots')));
const seed = +opt('--seed', 1);
const chrome = opt('--chrome', '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome');
const phone = args.includes('--phone');
fs.mkdirSync(out, { recursive: true });

// ---- the page, from outside ----

const page = {
  ready: `window.ui.ready.then(() => true)`,
  idle: `(async () => { for (let i = 0; i < 600 && window.ui.S.busy; i++) await new Promise(r => setTimeout(r, 50)); await new Promise(r => setTimeout(r, 250)); return !window.ui.S.busy; })()`,
  /** Runs the first verb matching a predicate over VerbDto (a JS expression of v). */
  run: pred => `(() => { const v = window.ui.allVerbs().find(v => ${pred}) ?? window.ui.S.view.combat?.actions.find(v => ${pred}); if (!v) throw new Error('no verb: ${pred.replace(/'/g, '"')}'); window.ui.run(v.id); return v.label; })()`,
  open: (kind, extra = '') => `(window.ui.openDrawer({ kind: '${kind}'${extra} }), true)`,
  key: k => `(document.dispatchEvent(new KeyboardEvent('keydown', { key: '${k}', bubbles: true })), true)`,
  view: `window.ui.S.view`,
  drawer: `window.ui.S.drawer?.kind ?? null`,
  centreOf: sel => `(() => { const r = document.querySelector('${sel}').getBoundingClientRect(); return [r.left + r.width / 2, r.top + r.height / 2]; })()`,
  tip: `(() => { const t = document.getElementById('ftip'); return t.hidden ? null : t.textContent; })()`,
};

/** On a phone, the page fits the screen (#315): it never scrolls; every verb is in the window or in a
 *  scroller of its own that brings it there; the sheet buttons are in the window with no scrolling; and an
 *  open drawer leaves a third of the map uncovered by it and the status strip. Returns the problems. */
const PHONE_LAYOUT = `(() => {
  const W = innerWidth, H = innerHeight, out = [];
  const shown = e => e.getClientRects().length > 0;
  const inside = r => r.left >= -1 && r.top >= -1 && r.right <= W + 1 && r.bottom <= H + 1;
  const name = b => b.querySelector('.vn')?.textContent.trim() || b.dataset.open || b.textContent.trim().slice(0, 20);
  const scroller = e => { for (let p = e.parentElement; p && p !== document.body; p = p.parentElement) { const s = getComputedStyle(p); if (/auto|scroll/.test(s.overflowX + s.overflowY)) return p; } return null; };
  const reached = b => { const r = b.getBoundingClientRect(); if (!inside(r)) return 'out of reach'; const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2); return hit && b.contains(hit) ? null : 'covered'; };
  const doc = document.scrollingElement;
  if (doc.scrollHeight > H || doc.scrollWidth > W) out.push('the page is ' + doc.scrollWidth + '×' + doc.scrollHeight + ' in a ' + W + '×' + H + ' window');
  for (const sel of ['.app', '.top', '.map', '.dock']) { const e = document.querySelector(sel); if (e && shown(e) && !inside(e.getBoundingClientRect())) out.push(sel + ' runs past the window'); }
  const top = document.querySelector('.top'); if (top && top.scrollWidth > top.clientWidth) out.push('the top strip is ' + top.scrollWidth + ' px wide in ' + top.clientWidth + ': it scrolls sideways');
  for (const b of document.querySelectorAll('.sheets button')) { const p = reached(b); if (p) out.push('sheet ' + name(b) + ': ' + p); }
  for (const b of document.querySelectorAll('.dock button')) {
    if (!shown(b) || b.closest('.sheets')) continue;
    const sc = scroller(b); if (sc) { const r = b.getBoundingClientRect(), c = sc.getBoundingClientRect(); sc.scrollLeft += r.left - c.left; sc.scrollTop += r.top - c.top; }
    const p = reached(b); if (p) out.push('verb ' + name(b) + ': ' + p);
  }
  if (doc.scrollTop || doc.scrollLeft) out.push('reaching the verbs scrolled the page');
  const d = document.getElementById('drawer');
  if (d && shown(d)) {
    const m = document.querySelector('.map').getBoundingClientRect(), r = d.getBoundingClientRect();
    const st = document.querySelector('.status'), top = Math.max(m.top, st && shown(st) ? st.getBoundingClientRect().bottom : m.top);
    const above = Math.max(0, r.top - top) * m.width, beside = Math.max(0, r.left - m.left) * (m.bottom - top);
    const left = Math.max(above, beside) / (m.width * m.height);
    if (left < 1 / 3) out.push('an open drawer leaves ' + Math.round(left * 100) + '% of the map');
  }
  return out;
})()`;

// An iPhone 13 and an iPhone 18 (402 × 714: Safari's page between its bars) upright, and phones on their side.
const PHONES = [[390, 664, '-phone'], [402, 714, '-iphone18'], [844, 390, '-land'], [932, 430, '-land-max']];

/** Each state: a URL scenario (Host Scenarios) and the steps that drive the page there. */
const STATES = {
  start: { steps: [] },
  here: { steps: [page.open('here')] },
  trip: { steps: [`(window.ui.openTrip(window.ui.S.view.walk.find(w => !w.refused && !w.careful).dir), true)`] },
  walk: { steps: [page.run(`v.kind === 'walk' && !v.refused && !v.careful`), page.idle] },
  fire: { scenario: 'supplied', steps: [page.open('fire')] },
  lit: { scenario: 'supplied', steps: ['strike'] },
  forage: { steps: [page.run(`v.label === 'Forage'`), page.idle] },
  make: { scenario: 'supplied', steps: [`(window.ui.S.makeSel = 'staff', window.ui.openDrawer({ kind: 'make' }), true)`] },
  made: { scenario: 'supplied', steps: [`(window.ui.S.makeSel = 'staff', window.ui.openDrawer({ kind: 'make' }), true)`, page.run(`v.make?.recipe === 'staff'`), page.idle] },
  body: { steps: [page.open('body')] },
  pack: { scenario: 'supplied', steps: [page.open('pack')] },
  log: { steps: [page.run(`v.kind === 'wait'`), page.idle, page.open('log')] },
  save: { steps: [page.open('save'), `new Promise(r => setTimeout(() => r(true), 300))`] },
  receipt: { scenario: 'supplied', steps: ['strike', page.run(`v.kind === 'walk' && v.dir === 'east' && !v.careful`), page.idle,
    page.run(`v.kind === 'wait'`), page.idle, page.run(`v.kind === 'wait'`), page.idle,
    page.run(`v.kind === 'walk' && v.dir === 'west' && !v.careful`), page.idle] },
  sleep: { steps: [page.run(`v.kind === 'sleep'`), page.idle] },
  fight: { scenario: 'fight', steps: [] },
  'fight-round': { scenario: 'fight', steps: [page.run(`v.label === 'Advance'`), page.idle, page.run(`v.label === 'Advance'`), page.idle] },
  dead: { scenario: 'dead', steps: [] },
  won: { scenario: 'won', steps: [] },
  winter: { scenario: 'winter', steps: [] },
  // Winter beside routes worn out of camp and grazing ground, with fresh prints around the player.
  trails: { scenario: 'trails', steps: [] },
  // Mid-January, walked away from camp and the game saved and loaded: the ground behind, out of
  // sight, is the Sim's memory of it.
  remembered: { scenario: 'winter', steps: ['away', `window.ui.doSave().then(() => true)`, `window.ui.doLoad().then(() => true)`, page.idle, 'remembered', page.key('Escape')] },
};

// Up to eight tiles away from camp: on in one direction while it is open, never straight back.
const AWAY = `(async () => {
  const back = { north: 'south', south: 'north', east: 'west', west: 'east' };
  let dir = null;
  for (let i = 0; i < 8; i++) {
    const open = window.ui.S.view.walk.filter(w => !w.careful && !w.refused);
    const go = open.find(w => w.dir === dir) ?? open.find(w => w.dir !== back[dir]);
    if (!go && i >= 3) break; // deep snow wears a person out: three tiles is far enough
    if (!go) throw new Error('nowhere to walk after ' + i + ' tiles');
    dir = go.dir;
    await window.ui.run(go.id);
    await ${page.idle};
  }
  return true;
})()`;

// Snow out of sight in the view: what the Sim remembers, since the page keeps nothing across a load.
const REMEMBERED = `(() => {
  const m = window.ui.S.view.map, inSight = new Set(m.visible);
  const n = m.surface.filter(s => !inSight.has(s.i) && s.snowCm > 0).length;
  if (!n) throw new Error('no remembered snow out of sight');
  return n;
})()`;

// Strike until the fire catches (each failed strike is a minute; the odds sheet says how likely).
const STRIKE = `(async () => {
  for (let i = 0; i < 25; i++) {
    const v = window.ui.S.view;
    if (v.fire?.burning) break;
    const light = v.tile.find(x => x.kind === 'fire') ?? v.locationFeatures.find(t => t.kind === 'fire')?.menu.find(x => x.kind === 'fire');
    if (!light || light.refused) throw new Error('cannot light: ' + (light?.refused ?? 'no verb'));
    window.ui.openDrawer({ kind: 'fire' });
    await window.ui.run(light.id);
  }
  return window.ui.S.view.fire?.burning ?? false;
})()`;

// ---- Chrome over CDP ----

async function launch() {
  const port = 9400 + Math.floor(Math.random() * 400);
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'ts-shots-'));
  const proc = spawn(chrome, ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, '--no-first-run',
    '--window-size=1280,860', '--hide-scrollbars', ...(process.getuid?.() === 0 ? ['--no-sandbox'] : []), 'about:blank'], { stdio: 'ignore' });
  let target;
  for (let i = 0; i < 60 && !target; i++) {
    await new Promise(r => setTimeout(r, 200));
    try { target = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page'); } catch {}
  }
  if (!target) throw new Error('Chrome did not start');
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise(r => ws.addEventListener('open', r));
  let id = 0; const pending = new Map(); const problems = []; const notes = [];
  ws.addEventListener('message', ev => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
    if (m.method === 'Runtime.consoleAPICalled') {
      const text = m.params.args.map(a => a.value ?? a.description).join(' ');
      (m.params.type === 'error' || m.params.type === 'assert' ? problems : notes).push(`console.${m.params.type}: ${text}`);
    }
    if (m.method === 'Runtime.exceptionThrown') problems.push('exception: ' + (m.params.exceptionDetails.exception?.description ?? m.params.exceptionDetails.text));
    if (m.method === 'Log.entryAdded' && m.params.entry.level === 'error' && !/fonts\.g/.test(m.params.entry.url ?? '')) problems.push(`log: ${m.params.entry.text} ${m.params.entry.url ?? ''}`);
  });
  const send = (method, params = {}) => new Promise(r => { const i = ++id; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
  const evaluate = async expr => {
    const r = await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true });
    if (r.result?.exceptionDetails) throw new Error(`in page: ${r.result.exceptionDetails.exception?.description ?? r.result.exceptionDetails.text}`);
    return r.result?.result?.value;
  };
  await send('Runtime.enable'); await send('Log.enable'); await send('Page.enable');
  return { send, evaluate, problems, notes, close: () => { ws.close(); proc.kill(); } };
}

async function size(cdp, w, h, mobile = false) {
  await cdp.send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile });
  await cdp.send('Emulation.setTouchEmulationEnabled', { enabled: mobile, maxTouchPoints: mobile ? 5 : 1 });
}

async function goto(cdp, url) {
  await cdp.send('Page.navigate', { url });
  for (let i = 0; i < 300; i++) {
    await new Promise(r => setTimeout(r, 100));
    try { if (await cdp.evaluate(`!!(window.ui && window.ui.ready)`)) break; } catch {}
  }
  await cdp.evaluate(page.ready);
  await new Promise(r => setTimeout(r, 600)); // icons decode, the camera settles
}

async function shot(cdp, name) {
  await new Promise(r => setTimeout(r, 350));
  const r = await cdp.send('Page.captureScreenshot', { format: 'png' });
  const file = path.join(out, `${name}.png`);
  fs.writeFileSync(file, Buffer.from(r.result.data, 'base64'));
  console.log(`  ${file}`);
}

/** A finger's tap at a point in the window. */
async function tap(cdp, x, y) {
  const at = [{ x, y, radiusX: 4, radiusY: 4, force: 1, id: 1 }];
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: at });
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  await new Promise(r => setTimeout(r, 400)); // the click after the touch, and the page's answer to it
}

/** A finger's swipe from one point to another, slow enough that the browser pans what is under it. */
async function swipe(cdp, [x0, y0], [x1, y1]) {
  const at = (x, y) => [{ x, y, radiusX: 4, radiusY: 4, force: 1, id: 1 }];
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: at(x0, y0) });
  for (let i = 1; i <= 8; i++) {
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: at(x0 + (x1 - x0) * i / 8, y0 + (y1 - y0) * i / 8) });
    await new Promise(r => setTimeout(r, 16));
  }
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  await new Promise(r => setTimeout(r, 400));
}

/** On a phone, a tap shows what a mouse would hover (#315): the clock's tap shows the day and the weather;
 *  the next tap only puts it away, so a tap on the map walks nowhere; a tap on a verb leaves no tip up. */
async function phoneTaps(cdp) {
  await size(cdp, 390, 664, true);
  await goto(cdp, `${url}?seed=${seed}`);
  const problems = [];
  await tap(cdp, ...await cdp.evaluate(page.centreOf('.t-clock')));
  const shown = await cdp.evaluate(page.tip);
  if (!/Day \d.*Air .*Wind /.test(shown ?? '')) problems.push(`a tap on the clock showed ${JSON.stringify(shown)}, not the day and weather`);
  const [mx, my] = await cdp.evaluate(`(() => { const r = document.getElementById('world').getBoundingClientRect(); return [r.left + 30, r.bottom - 30]; })()`);
  await tap(cdp, mx, my);
  if (await cdp.evaluate(page.tip)) problems.push('a second tap left the tip up');
  if (await cdp.evaluate(page.drawer)) problems.push('the tap that put the tip away also acted on the map');
  // A swipe that puts a tip away pans instead of clicking: the tap after it still acts.
  await tap(cdp, ...await cdp.evaluate(page.centreOf('.t-clock')));
  const [vx, vy] = await cdp.evaluate(page.centreOf('.dock .verbs'));
  await swipe(cdp, [vx + 60, vy], [vx - 60, vy]);
  if (await cdp.evaluate(page.tip)) problems.push('a swipe left the tip up');
  await cdp.evaluate(`(document.querySelector('.dock .verb[data-open="make"]').scrollIntoView({ block: 'nearest', inline: 'nearest' }), true)`);
  await tap(cdp, ...await cdp.evaluate(page.centreOf('.dock .verb[data-open="make"]')));
  if (await cdp.evaluate(page.tip)) problems.push('a tap on a verb left its tip up');
  if (await cdp.evaluate(page.drawer) !== 'make') problems.push('a tap on Make did not open it');
  return problems;
}

async function step(cdp, s) { return cdp.evaluate(s === 'strike' ? STRIKE : s === 'remembered' ? REMEMBERED : s === 'away' ? AWAY : s); }

// ---- modes ----

const { url } = await serve(root);
const cdp = await launch();
let failed = false;
try {
  if (args.includes('--run')) {
    await size(cdp, 1280, 860);
    console.log('scripted run');
    await goto(cdp, `${url}?seed=${seed}`);
    await shot(cdp, 'run-01-start');
    await step(cdp, page.run(`v.kind === 'walk' && v.dir === 'east' && !v.careful && !v.refused`)); await step(cdp, page.idle);
    await shot(cdp, 'run-02-walked-east');
    await step(cdp, page.run(`v.kind === 'walk' && v.dir === 'west' && !v.careful && !v.refused`)); await step(cdp, page.idle);
    if (!(await cdp.evaluate(`window.ui.S.view.place.camp`))) throw new Error('walking back did not reach camp');
    // Forage for tinder and wood first: the starting pack holds three or four strikes' worth of tinder.
    await step(cdp, page.run(`v.label === 'Forage' && !v.refused`)); await step(cdp, page.idle);
    await shot(cdp, 'run-03-foraged');
    await step(cdp, page.open('fire'));
    await shot(cdp, 'run-04-fire-odds');
    let lit = false;
    for (let round = 0; round < 4 && !lit; round++) {
      lit = await step(cdp, 'strike').catch(e => { if (/not enough/.test(e.message)) return false; throw e; });
      if (!lit) { await step(cdp, page.run(`v.label === 'Forage' && !v.refused`)); await step(cdp, page.idle); }
    }
    if (!lit) throw new Error('the fire never caught');
    await shot(cdp, 'run-05-fire-lit');
    // Wood for a staff: forage until the recipe can start.
    let tries = 0;
    for (; tries < 8 && await cdp.evaluate(`window.ui.allVerbs().find(v => v.make?.recipe === 'staff').refused != null`); tries++) {
      await step(cdp, page.run(`v.label === 'Forage' && !v.refused`)); await step(cdp, page.idle);
    }
    const refusal = await cdp.evaluate(`window.ui.allVerbs().find(v => v.make?.recipe === 'staff').refused`);
    if (refusal) throw new Error(`after ${tries} more forages the staff is still refused: ${refusal}`);
    await step(cdp, `(window.ui.S.makeSel = 'staff', window.ui.openDrawer({ kind: 'make' }), true)`);
    await shot(cdp, 'run-06-make-staff');
    await step(cdp, page.run(`v.make?.recipe === 'staff'`)); await step(cdp, page.idle);
    await shot(cdp, 'run-07-made');
    const before = await cdp.evaluate(`({ minute: window.ui.S.view.minute, gear: window.ui.S.view.pack.gear.map(g => g.kind) })`);
    await step(cdp, `window.ui.doSave().then(() => true)`);
    await shot(cdp, 'run-08-saved');
    await step(cdp, page.run(`v.kind === 'wait'`)); await step(cdp, page.idle);
    await step(cdp, `window.ui.doLoad().then(() => true)`);
    await shot(cdp, 'run-09-loaded');
    const after = await cdp.evaluate(`({ minute: window.ui.S.view.minute, gear: window.ui.S.view.pack.gear.map(g => g.kind) })`);
    if (after.minute !== before.minute || JSON.stringify(after.gear) !== JSON.stringify(before.gear))
      throw new Error(`load did not restore the save: ${JSON.stringify(before)} vs ${JSON.stringify(after)}`);
    console.log(`  staff made: ${before.gear.includes('staff')}, save restored at minute ${after.minute}`);
    // What a call costs through the worker, as the page pays it (view JSON and messaging included).
    const ms = await cdp.evaluate(`(async () => {
      const c = window.ui.client, t = () => performance.now();
      let t0 = t(); let v = await c.view(); const view = t() - t0;
      const wait = v.tile.find(x => x.kind === 'wait');
      await c.act({ verb: wait.id });
      t0 = t(); v = await c.act({ advance: 60 }); const hour = t() - t0;
      window.ui.S.view = v; window.ui.render();
      return { view: Math.round(view), waitHour: Math.round(hour) };
    })()`);
    console.log(`  worker round trip: view ${ms.view} ms, an hour's wait ${ms.waitHour} ms`);
  }
  if (args.includes('--observe')) {
    // Observer mode (observe.html): no player, the whole map, time at a rate, spawn and inspect.
    await size(cdp, 1280, 860);
    console.log('observer');
    await cdp.send('Page.navigate', { url: `${url}observe.html?seed=${seed}&npcs=3` });
    for (let i = 0; i < 300; i++) {
      await new Promise(r => setTimeout(r, 100));
      try { if (await cdp.evaluate(`!!(window.obs && window.obs.ready)`)) break; } catch {}
    }
    await cdp.evaluate(`window.obs.ready.then(() => true)`);
    const O = 'window.obs.S';
    const npcs = await cdp.evaluate(`${O}.view.actors.filter(a => a.kind === 'npc').length`);
    if (npcs !== 3) throw new Error(`?npcs=3 spawned ${npcs}`);
    await new Promise(r => setTimeout(r, 800));
    await shot(cdp, 'observe-01-valley');
    // Six real seconds at an hour a second.
    const m0 = await cdp.evaluate(`${O}.view.minute`);
    await cdp.evaluate(`(document.getElementById('play').click(), true)`);
    await new Promise(r => setTimeout(r, 6000));
    const run = await cdp.evaluate(`({ minute: ${O}.view.minute, achieved: document.getElementById('achieved').textContent })`);
    await cdp.evaluate(`(document.getElementById('play').click(), true)`);
    console.log(`  6 s at 60 min/s ran ${run.minute - m0} game minutes (${run.achieved})`);
    if (run.minute - m0 < 60) throw new Error('time barely moved');
    // In close on the camp: the painted ground, the people, the inspector.
    await cdp.evaluate(`(${O}.cam = { x: ${O}.map.campX + 0.5, y: ${O}.map.campY + 0.5, scale: 64 }, true)`);
    await new Promise(r => setTimeout(r, 2500)); // the chunks bake over a few frames
    await shot(cdp, 'observe-02-camp');
    // Spawn one more person by the camp through the tool, as a click would.
    const spawned = await cdp.evaluate(`(async () => {
      const S = ${O}, before = S.view.actors.filter(a => a.kind === 'npc').length;
      document.querySelector('[data-spawn="npc"]').click();
      const r = document.getElementById('world').getBoundingClientRect();
      const ev = (type, x, y) => document.getElementById('world').dispatchEvent(new PointerEvent(type, { clientX: x, clientY: y, bubbles: true, pointerId: 1 }));
      ev('pointerdown', r.left + r.width / 2, r.top + r.height / 2); ev('pointerup', r.left + r.width / 2, r.top + r.height / 2);
      for (let i = 0; i < 50 && S.view.actors.filter(a => a.kind === 'npc').length === before; i++) await new Promise(r => setTimeout(r, 100));
      for (let i = 0; i < 50 && !S.inspected; i++) await new Promise(r => setTimeout(r, 100));
      return { npcs: S.view.actors.filter(a => a.kind === 'npc').length - before, inspected: S.inspected?.place, people: S.inspected?.actors.length };
    })()`);
    console.log(`  spawned ${spawned.npcs}, inspected ${spawned.inspected} with ${spawned.people} there`);
    if (spawned.npcs !== 1 || !spawned.inspected) throw new Error(`spawn or inspect failed: ${JSON.stringify(spawned)}`);
    await shot(cdp, 'observe-03-spawned');
    // What a call costs through the worker, as the page pays it (view JSON and messaging included).
    const ms = await cdp.evaluate(`(async () => {
      const c = window.obs.client, t = () => performance.now();
      let t0 = t(); await c.advance(1); const minute = t() - t0;
      t0 = t(); await c.advance(60); const hour = t() - t0;
      t0 = t(); await c.inspect(${O}.map.campX, ${O}.map.campY); const look = t() - t0;
      t0 = t(); const m = await c.map(); const map = t() - t0;
      return { minute: Math.round(minute), hour: Math.round(hour), inspect: Math.round(look), map: Math.round(map),
        kb: Math.round(JSON.stringify(m).length / 1024) };
    })()`);
    console.log(`  worker round trip: a minute ${ms.minute} ms, an hour ${ms.hour} ms, inspect ${ms.inspect} ms, the map ${ms.map} ms (${ms.kb} KB)`);
    // The map is the player's MapDto (MapView.Truth): its ravines and cliffs draw here as on the player's page.
    const edge = await cdp.evaluate(`${O}.map.edges[0] ?? null`);
    if (!edge) throw new Error('the observer map has no ravines or cliffs');
    await cdp.evaluate(`(${O}.cam = { x: ${(edge.ax + edge.bx) / 2 + 0.5}, y: ${(edge.ay + edge.by) / 2 + 0.5}, scale: 48 }, true)`);
    await new Promise(r => setTimeout(r, 2500));
    await shot(cdp, 'observe-04-ravine');
  }
  if (phone) for (const p of await phoneTaps(cdp)) { console.error(`  taps: ${p}`); failed = true; }
  const wanted = args.includes('--all') ? Object.keys(STATES) : args.flatMap((a, i) => a === '--shot' ? [args[i + 1]] : []);
  for (const name of wanted) {
    const st = STATES[name];
    if (!st) throw new Error(`No state '${name}': ${Object.keys(STATES).join(', ')}`);
    for (const [w, h, suffix] of phone ? [[1280, 860, ''], ...PHONES] : [[1280, 860, '']]) {
      await size(cdp, w, h, suffix !== '');
      await goto(cdp, `${url}?seed=${seed}${st.scenario ? `&scenario=${st.scenario}` : ''}`);
      for (const s of st.steps) await step(cdp, s);
      await shot(cdp, `${name}${suffix}`);
      if (suffix) for (const p of await cdp.evaluate(PHONE_LAYOUT)) { console.error(`  ${name}${suffix}: ${p}`); failed = true; }
    }
  }
} catch (e) {
  console.error('FAILED:', e.message);
  failed = true;
} finally {
  cdp.close();
}
for (const n of cdp.notes) console.log('  ' + n);
if (cdp.problems.length) { console.error('Console errors:'); for (const p of cdp.problems) console.error('  ' + p); failed = true; }
process.exit(failed ? 1 : 0);
