// Drives the viewer in headless Chrome over the DevTools protocol and fails if any interaction moves
// or resizes a region of the page by more than 1 px, scrolls the page or a pane by itself, or if the
// page names a side "A" or "B".
//
// node layout-check.mjs <url> <out-dir> [width] [height] [light|dark] [--append FROM TO]
//
// --append copies the next generation line of FROM (a live progress.jsonl) onto TO (the file the
// viewer under test reads), to make a live update arrive. Screenshots go to out-dir, one per step.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const argv = process.argv.slice(2);
const ai = argv.indexOf('--append');
const append = ai >= 0 ? { from: argv[ai + 1], to: argv[ai + 2] } : null;
if (ai >= 0) argv.splice(ai, 3);
const [url, outDir, width = '1440', height = '900', scheme = 'light'] = argv;
if (!url || !outDir) { console.error('usage: node layout-check.mjs <url> <out-dir> [width] [height] [--append FROM TO]'); process.exit(2); }
fs.mkdirSync(outDir, { recursive: true });

const REGIONS = ['topbar', 'run-name', 'status', 'headline', 'scrubber', 'gen', 'left-pane', 'fitness-chart', 'sigma-chart',
  'params-scroll', 'center-pane', 'ending-legend', 'orders', 'scenarios-scroll', '.scen', 'dock', 'dock-head', 'replay-controls',
  'r-strip-box', 'r-message', 'arena', 'r-canvas', 'playback', 'r-round', 'units-box', 'r-events'];
const SCROLLERS = ['params-scroll', 'scenarios-scroll', 'dock', 'units-box'];

const chrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const port = 9300 + Math.floor(Math.random() * 500);
const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'tuneviewer-chrome-'));
const proc = spawn(chrome, ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`,
  '--no-first-run', `--window-size=${width},${height}`, 'about:blank'], { stdio: 'ignore' });
const sleep = ms => new Promise(r => setTimeout(r, ms));
let target;
for (let i = 0; i < 50 && !target; i++) {
  await sleep(200);
  try { target = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page'); } catch {}
}
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise(r => ws.addEventListener('open', r));
let id = 0; const pending = new Map(); const logs = [];
ws.addEventListener('message', ev => {
  const m = JSON.parse(ev.data);
  if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
  if (m.method === 'Runtime.exceptionThrown') logs.push('EXCEPTION ' + JSON.stringify(m.params.exceptionDetails).slice(0, 600));
});
const send = (method, params = {}) => new Promise(r => { const i = ++id; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
const evaluate = async expr => {
  const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true });
  if (r.result?.exceptionDetails) throw new Error('page: ' + JSON.stringify(r.result.exceptionDetails).slice(0, 400));
  return r.result?.result?.value;
};
const VK = { ' ': 32, ArrowLeft: 37, ArrowRight: 39, '[': 219, ']': 221 };
const key = async (k, code) => {
  const text = k.length === 1 ? k : undefined;
  await send('Input.dispatchKeyEvent', { type: text ? 'keyDown' : 'rawKeyDown', key: k, code, text, windowsVirtualKeyCode: VK[k] });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: k, code, windowsVirtualKeyCode: VK[k] });
};
const waitFor = async (expr, ms = 60000) => {
  const t0 = Date.now();
  while (Date.now() - t0 < ms) { if (await evaluate(expr)) return true; await sleep(150); }
  throw new Error('timed out waiting for ' + expr);
};

await send('Runtime.enable');
await send('Emulation.setDeviceMetricsOverride', { width: +width, height: +height, deviceScaleFactor: 1, mobile: false });
await send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: scheme }] });
await send('Page.navigate', { url });
await waitFor(`document.querySelectorAll('.scen .bands').length > 0`);
await sleep(800);   // fonts

const measure = () => evaluate(`(() => {
  const out = {};
  for (const r of ${JSON.stringify(REGIONS)}) {
    const els = r.startsWith('.') ? [...document.querySelectorAll(r)] : [document.getElementById(r)];
    els.forEach((el, i) => {
      if (!el) { out[r] = null; return; }
      const b = el.getBoundingClientRect();
      out[els.length > 1 ? r + '[' + i + ']' : r] = [b.x, b.y, b.width, b.height].map(v => Math.round(v * 10) / 10);
    });
  }
  const scroll = {};
  for (const s of ${JSON.stringify(SCROLLERS)}) scroll[s] = document.getElementById(s).scrollTop;
  scroll.page = document.scrollingElement.scrollTop;
  return { boxes: out, scroll, pageOverflow: document.scrollingElement.scrollHeight - innerHeight,
    hOverflow: document.scrollingElement.scrollWidth - innerWidth };
})()`);

// Side labels: a bare "B", "team A", or "A" doing a side's verb ("A won", "A ran"...). "A wolf"
// as an article is English, not a label.
const sideLabels = () => evaluate(`(() => {
  const text = document.body.innerText;
  const hits = [];
  const re = /\\b(?:team|side)\\s+[AB]\\b|\\b[AB]\\s+(?:won|wins|lost|loses|ran|died|dies|fled|flees|killed|kills|left|leaves)\\b|\\bB\\b|\\bA\\b(?=\\s*(?:[,.;:)]|$|\\n))/gm;
  for (const m of text.matchAll(re)) hits.push(text.slice(Math.max(0, m.index - 30), m.index + 30).replace(/\\s+/g, ' '));
  return hits;
})()`);

const shot = async name => {
  const s = await send('Page.captureScreenshot', { format: 'png' });
  const file = path.join(outDir, name + '.png');
  fs.writeFileSync(file, Buffer.from(s.result.data, 'base64'));
  return file;
};

const failures = [];
const base = await measure();
const shots = [await shot('00-initial')];
// Below 1100 px the panes stack and the page scrolls by design; only sideways scroll is a fault there.
const stacked = +width < 1100;
if (base.pageOverflow > 1 && !stacked) failures.push(`initial: the page itself scrolls (${base.pageOverflow} px taller than the window)`);
if (base.hOverflow > 1) failures.push(`initial: the page scrolls sideways (${base.hOverflow} px)`);
console.log(`regions measured: ${Object.keys(base.boxes).length} at ${width}x${height}, ${scheme}`);

function compare(step, now) {
  let moved = 0;
  for (const [r, b] of Object.entries(base.boxes)) {
    const n = now.boxes[r];
    if (b == null || n == null) { if (b !== n) { failures.push(`${step}: ${r} appeared or vanished`); moved++; } continue; }
    const d = b.map((v, i) => Math.abs(v - n[i]));
    if (Math.max(...d) > 1) {
      failures.push(`${step}: ${r} moved or resized: [x y w h] ${JSON.stringify(b)} -> ${JSON.stringify(n)}`);
      moved++;
    }
  }
  for (const [s, v] of Object.entries(base.scroll)) if (Math.abs(now.scroll[s] - v) > 1) { failures.push(`${step}: ${s} scrolled by itself (${v} -> ${now.scroll[s]})`); moved++; }
  if (now.pageOverflow > 1 && !stacked) { failures.push(`${step}: the page itself scrolls`); moved++; }
  console.log(`${moved ? 'FAIL' : 'ok  '} ${step}${moved ? ` (${moved} regions)` : ''}`);
}

async function step(n, name, act) {
  const note = await act();
  await sleep(300);
  compare(name + (note ? ` [${note}]` : ''), await measure());
  shots.push(await shot(`${String(n).padStart(2, '0')}-${name.replace(/\W+/g, '-')}`));
}

await step(1, 'pick a scenario', async () => {
  await evaluate(`document.querySelector('.scen[data-s="wolf_spear"]').click()`);
  return await evaluate(`document.getElementById('dock-h').textContent`);
});
await step(2, 'pick a fight', async () => {
  // The longest fight in the strip, so there are rounds to play and step through.
  await evaluate(`(() => { const bs = [...document.querySelectorAll('#r-strip button')];
    const r = b => +b.title.match(/(\\d+) rounds/)[1];
    bs.reduce((a, b) => r(b) > r(a) && r(b) <= 60 ? b : a).click(); })()`);
  await waitFor(`!document.getElementById('r-message').classList.contains('busy')`);
  return await evaluate(`document.getElementById('r-message').textContent.slice(0, 90)`);
});
await step(3, 'play 10 rounds', async () => {
  const frames = await evaluate(`+document.getElementById('r-frame').max`);
  await key(' ', 'Space');
  await waitFor(`+document.getElementById('r-frame').value >= ${Math.min(10, frames)} || document.getElementById('r-play').textContent === 'Play'`, 15000);
  await key(' ', 'Space');
  return await evaluate(`document.getElementById('r-round').textContent`);
});
await step(4, 'step', async () => {
  const before = await evaluate(`document.getElementById('r-round').textContent`);
  for (let i = 0; i < 3; i++) await key('ArrowLeft', 'ArrowLeft');
  await evaluate(`document.getElementById('r-back').click()`);
  await evaluate(`document.getElementById('r-step').click()`);
  await key('ArrowRight', 'ArrowRight');
  return `three left arrows, back, step, right arrow: ${before} -> ${await evaluate(`document.getElementById('r-round').textContent`)}`;
});
await step(5, 'scrub the generation', async () => {
  await key('[', 'BracketLeft'); await key('[', 'BracketLeft');
  await evaluate(`(() => { const g = document.getElementById('gen'); g.value = 2; g.dispatchEvent(new Event('input')); })()`);
  await key(']', 'BracketRight');
  return 'generation ' + await evaluate(`document.getElementById('gen-out').textContent`);
});
await step(6, 'pick a headless scenario and fight', async () => {
  await evaluate(`document.querySelector('.scen[data-s="wolves_vs_caribou"]').click()`);
  await evaluate(`document.querySelectorAll('#r-strip button')[0].click()`);
  await waitFor(`!document.getElementById('r-message').classList.contains('busy')`);
  return await evaluate(`document.getElementById('dock-h').textContent`);
});
await step(7, 'mean candidate (no strip)', async () => {
  await evaluate(`(() => { const s = document.getElementById('r-candidate'); s.value = 'mean'; s.dispatchEvent(new Event('change')); })()`);
  return await evaluate(`document.getElementById('r-strip-note').textContent.slice(0, 60)`);
});
await step(8, 'a replay the server answers with an error', async () => {
  // A fight number the scenario does not have: the server answers 400, shown as a refusal would be.
  await evaluate(`(async () => { document.getElementById('r-fight').value = 99999; await replay(); })()`);
  return await evaluate(`document.getElementById('r-message').textContent.slice(0, 60)`);
});
await step(9, 'follow the newest', async () => {
  await evaluate(`(() => { const s = document.getElementById('r-candidate'); s.value = 'best'; s.dispatchEvent(new Event('change')); document.getElementById('follow').click(); })()`);
  return 'generation ' + await evaluate(`document.getElementById('gen-out').textContent`);
});
if (append) {
  await step(10, 'wait for a live update', async () => {
    const have = fs.readFileSync(append.to, 'utf8').split('\n').filter(Boolean).length;
    const src = fs.readFileSync(append.from, 'utf8').split('\n').filter(Boolean);
    if (src.length <= have) return 'no newer line in the live log to copy';
    const before = await evaluate(`document.getElementById('gen-out').textContent`);
    fs.appendFileSync(append.to, src[have] + '\n');
    await waitFor(`document.getElementById('gen-out').textContent !== ${JSON.stringify(before)}`, 10000);
    return `generation ${before} -> ${await evaluate(`document.getElementById('gen-out').textContent`)}`;
  });
}

const labels = await sideLabels();
if (labels.length) failures.push(...labels.map(l => `side label in page text: "...${l}..."`));
console.log(`${labels.length ? 'FAIL' : 'ok  '} no bare A/B side labels in the page text`);
for (const l of logs) failures.push('page: ' + l);

console.log('screenshots:\n  ' + shots.join('\n  '));
if (failures.length) { console.log('\nFAILED:\n  ' + failures.join('\n  ')); }
else console.log('\nPASS: no region moved or resized by more than 1 px');
ws.close(); proc.kill();
process.exit(failures.length ? 1 : 0);
