// Drives headless Chrome over the DevTools protocol: loads the page (cache disabled), records
// start-up and transfer sizes, optionally runs ?bench to completion, and screenshots the page.
// node measure.mjs <url> <out-prefix> [bench]
import { spawn } from 'node:child_process';
import fs from 'node:fs';

const [url, out, bench] = process.argv.slice(2);
const chrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const port = 9300 + Math.floor(Math.random() * 500);
const profile = fs.mkdtempSync('/tmp/wasmspike/chrome-');
const proc = spawn(chrome, ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`,
  '--no-first-run', '--window-size=1280,860', 'about:blank'], { stdio: 'ignore' });

const sleep = ms => new Promise(r => setTimeout(r, ms));
let target;
for (let i = 0; i < 50 && !target; i++) {
  await sleep(200);
  try { target = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page'); } catch {}
}
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise(r => ws.addEventListener('open', r));
let id = 0; const pending = new Map(); const consoleLines = [];
ws.addEventListener('message', ev => {
  const m = JSON.parse(ev.data);
  if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
  if (m.method === 'Runtime.consoleAPICalled') consoleLines.push(m.params.args.map(a => a.value ?? a.description).join(' '));
  if (m.method === 'Runtime.exceptionThrown') consoleLines.push('EXCEPTION ' + JSON.stringify(m.params.exceptionDetails).slice(0, 800));
});
const send = (method, params = {}) => new Promise(r => { const i = ++id; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
const evaluate = async expr => (await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true })).result?.result?.value;

await send('Runtime.enable');
await send('Network.enable');
await send('Network.setCacheDisabled', { cacheDisabled: true });
await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 860, deviceScaleFactor: 1, mobile: false });
await send('Page.navigate', { url });

let startup;
for (let i = 0; i < 600 && !startup; i++) { await sleep(100); startup = await evaluate('window.spike && window.spike.startup'); }
await sleep(800); // let the icons decode and draw
// Smoke the boundary through the page's own buttons: walk, wait, save, load.
const click = label => evaluate(`[...document.querySelectorAll('#dock button')].find(b => b.textContent === ${JSON.stringify(label)}).click()`);
for (const label of ['Walk E', 'Walk E', 'Walk N', 'Wait 60 min', 'Save']) { await click(label); await sleep(150); }
const savedStatus = await evaluate(`document.getElementById('status').textContent`);
await click('Load'); await sleep(300);
const loadedStatus = await evaluate(`document.getElementById('status').textContent`);
const logText = await evaluate(`document.getElementById('log').textContent`);
const resources = await evaluate(`performance.getEntriesByType('resource').map(e => ({ name: e.name.split('/').pop(), transfer: e.transferSize, decoded: e.decodedBodySize }))`);
const shot = await send('Page.captureScreenshot', { format: 'png' });
fs.writeFileSync(out + '.png', Buffer.from(shot.result.data, 'base64'));

let result = null;
if (bench) {
  for (let i = 0; i < 3600 && !result; i++) { await sleep(1000); result = await evaluate('window.benchResult'); }
  fs.writeFileSync(out + '-world.json', await evaluate('window.spike.G.BenchWorldJson()'));
}
const framework = resources.filter(r => !r.name.endsWith('.png'));
const sum = (xs, k) => xs.reduce((a, r) => a + (r[k] || 0), 0);
const report = {
  url, startup, smoke: { savedStatus, loadedStatus, logText }, console: consoleLines.slice(0, 40),
  downloadFramework: { transferBytes: sum(framework, 'transfer'), decodedBytes: sum(framework, 'decoded') },
  downloadIcons: { transferBytes: sum(resources.filter(r => r.name.endsWith('.png')), 'transfer') },
  resources: framework, bench: result,
};
fs.writeFileSync(out + '.json', JSON.stringify(report, null, 2));
console.log(JSON.stringify({ startup, smoke: { savedStatus, loadedStatus, logText }, console: consoleLines.slice(0, 10), downloadFramework: report.downloadFramework, bench: result && { ...result, advanceDayMs: undefined } }, null, 1));
ws.close(); proc.kill();
