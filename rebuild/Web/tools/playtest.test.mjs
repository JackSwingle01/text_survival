// The playtest log (App/wwwroot/playtest.js) against a stand-in client: run with
//   node --test tools/playtest.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { logOptions, withLog, replay } from '../App/wwwroot/playtest.js';

/** A game of a few rules: a verb starts a job, advance runs it, the world is the minute and a counter. */
function fakeGame() {
  let w = null, job = null;
  const view = () => ({ minute: w.minute, place: { name: `tile ${w.x}` }, outcome: 'surviving',
    job: job && { label: 'Wait', kind: 'wait', doneMinutes: job.done, totalMinutes: job.total, finished: job.done >= job.total },
    log: w.log.slice(-40) });
  const line = text => w.log.push({ minute: w.minute, text });
  return {
    newGame: async seed => { w = { seed, minute: 0, x: seed, log: [] }; job = null; return view(); },
    act: async a => {
      if (a.verb === 'bad') throw new Error('No verb bad');
      if (a.verb) { job = { done: 0, total: a.minutes ?? 30 }; line('You wait.'); }
      if (a.advance) { const m = Math.min(a.advance, job.total - job.done); job.done += m; w.minute += m; if (job.done >= job.total) line('Done waiting.'); }
      if (a.stop) job.done = job.total;
      return view();
    },
    view: async () => view(),
    save: async () => { if (job && job.done < job.total) throw new Error('Finish or stop the job'); return new TextEncoder().encode(JSON.stringify(w)); },
    load: async bytes => { w = JSON.parse(new TextDecoder().decode(bytes)); job = null; return view(); },
    scenario: async () => view(),
    observe: { marker: true },
  };
}

const clock = (t = 1_000_000) => () => (t += 1000);

test('the log is on only with ?log, and posts only to a URL it names', () => {
  assert.equal(logOptions(new URLSearchParams('seed=3')), null);
  assert.deepEqual(logOptions(new URLSearchParams('log')), { endpoint: null });
  assert.deepEqual(logOptions(new URLSearchParams('log=1')), { endpoint: null });
  assert.deepEqual(logOptions(new URLSearchParams('log=/playtest')), { endpoint: '/playtest' });
  assert.deepEqual(logOptions(new URLSearchParams('log=https://x.example/p')), { endpoint: 'https://x.example/p' });
});

test('each call is logged with its time, its minute and place, the job, and only the journal lines it brought', async () => {
  const { client, log } = withLog(fakeGame(), { now: clock(), build: 'abc123' });
  await client.newGame(7);
  await client.act({ verb: '1.2', minutes: 20 });
  await client.act({ advance: 20 });
  await Promise.resolve();
  assert.equal(log.session.build, 'abc123');
  const [born, started, done] = log.entries;
  assert.deepEqual([born.op, born.arg, born.minute, born.place], ['newGame', 7, 0, 'tile 7']);
  assert.deepEqual(started.lines, ['You wait.']);
  assert.equal(started.job.finished, false);
  assert.deepEqual(done.lines, ['Done waiting.']);
  assert.equal(done.job.finished, true);
  assert.equal(done.minute, 20);
  assert.ok(started.t < done.t);
  assert.equal(client.observe.marker, true, 'the rest of the client passes through');
});

test('a failed call is logged with its error, and still throws', async () => {
  const { client, log } = withLog(fakeGame(), { now: clock() });
  await client.newGame(1);
  await assert.rejects(client.act({ verb: 'bad' }), /No verb bad/);
  assert.equal(log.entries.at(-1).error, 'No verb bad');
});

test('a save is kept at the start and every six game hours, never while a job is under way', async () => {
  const { client, log } = withLog(fakeGame(), { now: clock() });
  await client.newGame(1);
  assert.ok(log.entries[0].save, 'the first view');
  for (let h = 0; h < 7; h++) { await client.act({ verb: 'w', minutes: 60 }); await client.act({ advance: 30 }); await client.act({ advance: 30 }); }
  const saves = log.entries.filter(e => e.save);
  assert.deepEqual(saves.map(e => e.minute), [0, 360]);
  assert.ok(saves.every(e => !e.job || e.job.finished));
});

test('marks and drawers are logged at the minute last seen', async () => {
  const { client, log } = withLog(fakeGame(), { now: clock() });
  await client.newGame(1);
  await client.act({ verb: 'w', minutes: 15 }); await client.act({ advance: 15 });
  const at = log.mark();
  log.ui({ drawer: 'fire' });
  assert.ok(at instanceof Date);
  assert.deepEqual(log.entries.slice(-2).map(e => [e.op, e.minute]), [['mark', 15], ['ui', 15]]);
  assert.equal(JSON.parse(JSON.stringify(log)).entries.length, log.entries.length, 'the download is the whole log');
});

test('a replay on a fresh game reaches the same world, loads included', async () => {
  const game = fakeGame(), { client, log } = withLog(game, { now: clock() });
  await client.newGame(4);
  await client.act({ verb: 'w', minutes: 30 }); await client.act({ advance: 30 });
  const bytes = await client.save();
  await client.act({ verb: 'w', minutes: 90 }); await client.act({ advance: 90 });
  await client.load(bytes);
  await client.act({ verb: 'w', minutes: 10 }); await client.act({ advance: 10 });
  log.mark();
  await assert.rejects(client.act({ verb: 'bad' }));
  const fresh = fakeGame(), record = JSON.parse(JSON.stringify(log));
  const last = await replay(record, fresh);
  assert.equal(last.minute, 40);
  assert.deepEqual(await fresh.save(), await game.save());
});

test('a replay says where the game has changed under the log', async () => {
  const { client, log } = withLog(fakeGame(), { now: clock() });
  await client.newGame(4);
  await client.act({ verb: 'w', minutes: 30 }); await client.act({ advance: 30 });
  const record = JSON.parse(JSON.stringify(log));
  record.entries[2].minute = 25;
  await assert.rejects(replay(record, fakeGame()), /entry 2 \(act\): minute 30 now, 25 then/);
});

test('with an endpoint, entries go out in batches; a failed post keeps them for the next', async () => {
  const posts = []; let up = false;
  const post = async (url, init) => { if (!up) throw new Error('offline'); posts.push({ url, body: JSON.parse(init.body) }); return { ok: true }; };
  const warn = console.warn; console.warn = () => {};
  try {
    const { client, log } = withLog(fakeGame(), { endpoint: '/p', post, now: clock(), build: 'b1' });
    await client.newGame(1);
    await log.flush();
    assert.equal(log.sink.failures, 1);
    assert.equal(log.sink.sent, 0);
    up = true;
    await client.act({ verb: 'w', minutes: 5 });
    await log.flush();
    assert.equal(posts.length, 1);
    assert.deepEqual(posts[0].body.entries.map(e => e.op), ['newGame', 'act']);
    assert.equal(posts[0].body.session.build, 'b1', 'the first batch carries the session');
    log.mark(); await log.flush();
    assert.deepEqual(Object.keys(posts[1].body.session), ['id'], 'later ones only its id');
    assert.equal(log.sink.sent, 3);
    assert.equal(log.entries.length, 3);
  } finally { console.warn = warn; }
});
