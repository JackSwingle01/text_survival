// Observer mode's camera and pacing, away from the DOM: run with
//   node --test tools/observe.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createCamera, zoomAt, panBy, cellAt, createPacer, MIN_SCALE, MAX_SCALE } from '../App/wwwroot/observe-core.js';

const view = { w: 1000, h: 600 }, map = { width: 224, height: 82 };

test('zooming keeps the cell under the cursor under the cursor', () => {
  const cam = createCamera(map, view);
  const before = cellAt(cam, 700, 150, view);
  const z = zoomAt(cam, 2.5, 700, 150, view, map);
  const after = cellAt(z, 700, 150, view);
  assert.ok(Math.abs(after.x - before.x) < 1e-9 && Math.abs(after.y - before.y) < 1e-9, `${JSON.stringify(before)} -> ${JSON.stringify(after)}`);
  assert.equal(z.scale, cam.scale * 2.5);
});

test('zoom stops at its bounds, from the whole valley to a few cells', () => {
  let cam = createCamera(map, view);
  for (let i = 0; i < 100; i++) cam = zoomAt(cam, 0.5, 500, 300, view, map);
  assert.equal(cam.scale, MIN_SCALE);
  assert.ok(MIN_SCALE * map.width < view.w, 'all the way out, the whole map fits');
  for (let i = 0; i < 100; i++) cam = zoomAt(cam, 2, 500, 300, view, map);
  assert.equal(cam.scale, MAX_SCALE);
});

test('dragging moves the map with the pointer, and the centre stays on the map', () => {
  const cam = { x: 100, y: 40, scale: 48 };
  const moved = panBy(cam, 96, -48, map);
  assert.deepEqual([moved.x, moved.y], [98, 41]); // the map follows the pointer: the centre goes the other way
  const far = panBy(cam, 1e7, 1e7, map);
  assert.deepEqual([far.x, far.y], [0, 0]);
});

test('the pacer asks for the rate times the time that passed, carrying fractions', () => {
  const p = createPacer();
  assert.equal(p.due(60, 0.5), 30);
  let total = 0;
  for (let i = 0; i < 8; i++) total += p.due(1, 0.25);
  assert.equal(total, 2);
});

test('the pacer never banks more than a second of time it could not run', () => {
  const p = createPacer();
  assert.equal(p.due(600, 10), 600);
});

test('the pacer sizes each call from what the last ones cost, to keep the page answering', () => {
  const p = createPacer({ targetMs: 100 });
  p.ran(60, 200); // 60 minutes took 200 ms
  assert.equal(p.due(1000, 0.2), 30); // 100 ms worth; the rest waits for the next call
  assert.equal(p.due(1000, 0), 30);
  p.ran(30, 10); // cheap again
  assert.ok(p.due(1000, 0) > 30);
});

test('the pacer reports the rate it actually achieved over the last few seconds', () => {
  const p = createPacer();
  let t = 0;
  for (let i = 0; i < 10; i++) { t += 500; p.ran(30, 5, t); }
  assert.equal(Math.round(p.achieved(t)), 60);
});

test('after a pause the achieved rate counts only from the restart', () => {
  const p = createPacer();
  let t = 0;
  for (let i = 0; i < 10; i++) { t += 500; p.ran(30, 5, t); }
  p.reset(); // paused, then played again
  t += 10000; p.ran(30, 5, t); t += 500; p.ran(30, 5, t);
  assert.equal(Math.round(p.achieved(t)), 120); // 60 minutes over the half second since the restart
});
