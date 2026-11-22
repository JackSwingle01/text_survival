// The page's own small rules, away from the DOM: run with
//   node --test tools/page.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { veilKey } from '../App/wwwroot/util.js';

test('a receipt closes on Escape, Space or Enter, and any other key closes it and acts', () => {
  for (const k of ['Escape', ' ', 'Enter']) assert.equal(veilKey('receipt', k), 'close', k);
  for (const k of ['g', 'f', 'm', 'ArrowUp']) assert.equal(veilKey('receipt', k), 'pass', k);
  for (const k of ['Shift', 'Tab', 'CapsLock']) assert.equal(veilKey('receipt', k), 'keep', k);
});

test('the end of the run is not dismissed by a key', () => {
  for (const k of ['Escape', ' ', 'g']) assert.equal(veilKey('end', k), 'keep', k);
});
