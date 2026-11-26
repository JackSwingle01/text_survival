// The transcript-to-log timeline (tools/playtest-align.mjs): run with
//   node --test tools/playtest-align.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseTranscript, align } from './playtest-align.mjs';

// The note taker's export, as Jack's sample has it.
const SAMPLE = `00:01  Speaker 1  Test: one, two, three. One, two, three. Testing to see if there's timestamps for each thing that I say.

00:11  Speaker 2   Okay, I read the same thing. That's how I want it in the episode.

00:18  Speaker 1   Something else?

00:21  Speaker 2   You what?`;

test('the note taker\'s lines: the time since the start, the speaker, the words', () => {
  const lines = parseTranscript(SAMPLE);
  assert.equal(lines.length, 4);
  assert.deepEqual(lines[1], { at: 11, stamp: '00:11', speaker: 'Speaker 2', text: "Okay, I read the same thing. That's how I want it in the episode." });
  assert.equal(parseTranscript('1:02:03  Speaker 1  late\ncarried on')[0].at, 3723);
  assert.equal(parseTranscript('1:02:03  Speaker 1  late\ncarried on')[0].text, 'late carried on');
  assert.throws(() => parseTranscript('no time here'), /does not start with a time/);
});

// A session in a zone six hours behind UTC: the chip showed 22:31:05 at 04:31:05 UTC.
const T0 = Date.UTC(2026, 10, 27, 4, 31, 5);
const s = n => T0 + n * 1000;
const RECORD = { session: { startedAt: '2026-11-27T04:30:00.000Z', build: 'abc123' }, entries: [
  { t: s(-60), op: 'newGame', arg: 1, minute: 0, clock: 'Day 1 09:00', place: 'Forest Camp', save: '{}' },
  { t: s(0), op: 'mark', minute: 0, shown: '22:31:05' },
  { t: s(10), op: 'act', arg: { verb: '1.4' }, minute: 0, clock: 'Day 1 09:00', place: 'Forest Camp', job: { label: 'Forage', finished: false } },
  { t: s(11), op: 'ui', minute: 0, arg: { drawer: 'forage' } },
  { t: s(20), op: 'act', arg: { advance: 30 }, minute: 30, clock: 'Day 1 09:30', place: 'Forest Camp',
    job: { label: 'Forage', finished: true, outcome: 'finished' }, lines: ['You find some sticks.'] },
  { t: s(25), op: 'view', minute: 30, clock: 'Day 1 09:30', place: 'Forest Camp', job: { label: 'Forage', finished: true, outcome: 'finished' } },
  { t: s(40), op: 'act', arg: { verb: 'bad' }, minute: 30, error: 'No verb bad' },
  { t: s(300), op: 'mark', minute: 30, shown: '22:36:05' },
] };

const TRANSCRIPT = `00:05  Speaker 1  Marked 22:31:05.
00:30  Speaker 1  That forage felt too quick.
00:50  Speaker 1  Why was that refused?
05:05  Speaker 1  Mark 10:36.
05:10  Speaker 1  Done for now.`;

test('each line is placed by the mark it is nearest, in the player\'s own clock, with what the game did since', () => {
  const { markdown, anchors, warnings } = align(RECORD, TRANSCRIPT);
  assert.deepEqual(anchors.map(a => [a.at, a.shown]), [[5, '22:31:05'], [305, '22:36:05']], 'a 12-hour time with no seconds still finds its mark');
  assert.deepEqual(warnings, []);
  const [, first, forage, refused, , last] = markdown.split('\n## ');
  assert.match(first, /^22:31:05 · Day 1 09:00 · Forest Camp \(00:05\)/);
  assert.match(forage, /^22:31:30 · Day 1 09:30 · Forest Camp \(00:30\)/);
  assert.match(forage, /- Forage: finished\n  - "You find some sticks."\n- Looked at: forage/);
  assert.equal(forage.match(/Forage: finished/g).length, 1, 'the view after the job does not repeat it');
  assert.match(refused, /- Refused: No verb bad/);
  assert.match(last, /^22:36:10/);
  assert.match(last, /Save to load: entry 0 \(Day 1 09:00\)/);
});

test('a pause in the recording is said, and only moves the lines until the next mark', () => {
  const paused = TRANSCRIPT.replace('05:05  Speaker 1  Mark 10:36.', '03:05  Speaker 1  Mark 10:36.').replace('05:10', '03:10');
  const { markdown, warnings } = align(RECORD, paused);
  assert.match(warnings[0], /22:31:05 and 22:36:05 are 120 s further apart/);
  assert.match(markdown, /## 22:36:10 · Day 1 09:30 · Forest Camp \(03:10\)/);
});

test('with no mark said, it says which lines mention one, and takes an anchor by hand', () => {
  const unsaid = TRANSCRIPT.replace('Marked 22:31:05.', 'Marked twenty-two thirty-one.').replace('Mark 10:36.', 'ok');
  assert.throws(() => align(RECORD, unsaid), /00:05  Marked twenty-two thirty-one[\s\S]*--anchor/);
  const { anchors } = align(RECORD, unsaid, { manual: [{ at: 5, shown: '22:31:05' }] });
  assert.deepEqual(anchors.map(a => a.shown), ['22:31:05']);
  assert.throws(() => align(RECORD, unsaid, { manual: [{ at: 5, shown: '22:00:00' }] }), /no mark in the log shown as 22:00:00/);
});
