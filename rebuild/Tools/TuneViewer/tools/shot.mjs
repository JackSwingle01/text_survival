// Screenshots the viewer in headless Chrome over the DevTools protocol (as Web.Spike/tools/measure.mjs).
// node shot.mjs <url> <out.png> [width] [height] [script-to-evaluate-first] [wait-ms] [light|dark] [full|view]
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const [url, out, width = '1280', height = '2400', script = '', waitMs = '2500', scheme = 'light', area = 'full'] = process.argv.slice(2);
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
  if (m.method === 'Runtime.consoleAPICalled') logs.push(m.params.args.map(a => a.value ?? a.description).join(' '));
  if (m.method === 'Runtime.exceptionThrown') logs.push('EXCEPTION ' + JSON.stringify(m.params.exceptionDetails).slice(0, 600));
});
const send = (method, params = {}) => new Promise(r => { const i = ++id; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
await send('Runtime.enable');
await send('Emulation.setDeviceMetricsOverride', { width: +width, height: +height, deviceScaleFactor: 1, mobile: +width < 600 });
await send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: scheme }] });
await send('Page.navigate', { url });
await sleep(+waitMs);
if (script) { const r = await send('Runtime.evaluate', { expression: script, awaitPromise: true, returnByValue: true }); logs.push('script: ' + JSON.stringify(r.result?.result?.value ?? r.result?.exceptionDetails?.text)); await sleep(+waitMs); }
const shot = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: area === 'full' });
fs.writeFileSync(out, Buffer.from(shot.result.data, 'base64'));
console.log(JSON.stringify({ out, logs }));
ws.close(); proc.kill();
