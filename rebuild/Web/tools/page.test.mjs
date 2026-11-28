// The page's own small rules, away from the DOM: run with
//   node --test tools/page.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { veilKey, paceMs, cameraTarget, oddsWhy, cardPlace } from '../App/wwwroot/util.js';

test('a receipt closes on Escape, Space or Enter, and any other key closes it and acts', () => {
  for (const k of ['Escape', ' ', 'Enter']) assert.equal(veilKey('receipt', k), 'close', k);
  for (const k of ['g', 'f', 'm', 'ArrowUp']) assert.equal(veilKey('receipt', k), 'pass', k);
  for (const k of ['Shift', 'Tab', 'CapsLock']) assert.equal(veilKey('receipt', k), 'keep', k);
});

test('the end of the run is not dismissed by a key', () => {
  for (const k of ['Escape', ' ', 'g']) assert.equal(veilKey('end', k), 'keep', k);
});

test('time passes on screen as the old game paced it: longer jobs play longer, a sleep faster, a crossing briefly', () => {
  assert.ok(paceMs(60, 'gather') > paceMs(15, 'gather'));
  assert.equal(paceMs(1000, 'wait'), 30000); // capped
  assert.equal(paceMs(1, 'wait'), 1000);     // at least a second
  assert.ok(paceMs(480, 'sleep') < paceMs(480, 'rest'));
  assert.ok(paceMs(3, 'walk') <= 1200 && paceMs(3, 'walk') >= 500);
});

test('the camera centres the player in the part of the map nothing covers', () => {
  // 48 px cells; the player stands at cell (10, 20), so its centre is (10.5, 20.5).
  assert.deepEqual(cameraTarget({ x: 10, y: 20 }, { top: 0, right: 0, bottom: 0 }, 48), { x: 10.5, y: 20.5 });
  // A drawer 480 px wide on the right: the camera looks 240 px (5 cells) right of the player, which then
  // sits in the middle of the uncovered left part.
  assert.deepEqual(cameraTarget({ x: 10, y: 20 }, { top: 0, right: 480, bottom: 0 }, 48), { x: 15.5, y: 20.5 });
  // A bottom sheet 384 px tall under a 96 px strip at the top: the middle of the gap between them is
  // (384 - 96) / 2 = 144 px (3 cells) above the map's middle, so the camera looks 3 cells below the player.
  assert.deepEqual(cameraTarget({ x: 10, y: 20 }, { top: 96, right: 0, bottom: 384 }, 48), { x: 10.5, y: 23.5 });
});

test('an odds line names only what moves it, each as the whole percent shown', () => {
  // A fall on rock at seed 17: ground 2%, light snow +0.9%, bad legs +0.04%.
  const fall = { base: 'Ground', basePct: 0.02, totalPct: 0.029,
    terms: [{ factor: 'Light snow', deltaPct: 0.009 }, { factor: 'Bad legs', deltaPct: 0.0004 }] };
  assert.deepEqual(oddsWhy(fall), ['ground 2%', 'light snow +1%']);
  // A minute's search at dusk with an axe: no base worth naming (it is the whole search), two factors.
  const search = { base: 'Search', basePct: 1, totalPct: 0.88,
    terms: [{ factor: 'Eyes', deltaPct: -0.2 }, { factor: 'Axe', deltaPct: 0.08 }, { factor: 'Shovel', deltaPct: 0 }] };
  assert.deepEqual(oddsWhy(search, { base: false }), ['eyes −20%', 'axe +8%']);
  assert.deepEqual(oddsWhy({ base: 'Search', basePct: 1, totalPct: 1, terms: [] }, { base: false }), []);
});

test('a trip card sits on the side of its tile away from the player, inside the map', () => {
  const area = { w: 402, h: 470, top: 50 };          // the map, and the bottom of the status strip
  const card = { w: 320, h: 120 };
  // South of the player: below the tile, centred on it.
  assert.deepEqual(cardPlace({ x: 177, y: 200, size: 48 }, card, area, 'below'), { left: 41, top: 256, side: 'below' });
  // North of the player: above the tile.
  assert.deepEqual(cardPlace({ x: 177, y: 200, size: 48 }, card, area, 'above'), { left: 41, top: 72, side: 'above' });
  // Too near the bottom to go below: it goes above instead.
  assert.equal(cardPlace({ x: 177, y: 330, size: 48 }, card, area, 'below').side, 'above');
  // At the left edge: kept 8 px in.
  assert.deepEqual(cardPlace({ x: 0, y: 200, size: 48 }, card, area, 'below'), { left: 8, top: 256, side: 'below' });
});

test('on a short map a trip card goes to an edge, over neither the player nor its tile', () => {
  // A phone on its side: the map is 634 x 346, the strip ends at 40, the player's tile at (293, 169).
  const area = { w: 634, h: 346, top: 40 }, card = { w: 320, h: 112 }, me = { x: 293, y: 169, size: 48 };
  const north = { x: 293, y: 121, size: 48 };
  const p = cardPlace(north, card, area, 'above', me);
  const over = t => p.left < t.x + t.size && p.left + card.w > t.x && p.top < t.y + t.size && p.top + card.h > t.y;
  assert.equal(over(me), false, JSON.stringify(p));
  assert.equal(over(north), false, JSON.stringify(p));
  assert.ok(p.left >= 8 && p.left + card.w <= area.w - 8 && p.top >= area.top + 8 && p.top + card.h <= area.h - 8, JSON.stringify(p));
  assert.equal(p.side, 'bottom'); // above the tile there is no room, below it the player, so the map's bottom edge
});
