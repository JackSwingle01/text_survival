// The playtest log, on only with ?log in the URL. It records every call the page makes to the game
// (client.js's newGame, act, view, save, load, scenario) with the wall-clock time it was made, what
// came back in words (the minute, the place, the job's end, the new journal lines), the player's marks
// and the drawers they opened. Sim is deterministic from its seed, so the calls are the whole game:
// replay() runs them again on a fresh client and reaches the same world. A save is kept every few game
// hours as well, so a log is still readable after Sim has changed and the calls no longer replay.
//
// The log lives in memory and downloads as one JSON file. ?log=<url> also posts it there in batches
// as it grows ({ session, entries }); the memory copy stays whole either way.
//
// An entry: { t, op, arg?, minute, clock?, place?, job?, lines?, save?, error? }, where t is Date.now()
// at the call and clock is the game's ("Day 1 09:16"). A mark is { t, op: 'mark', minute, shown }, shown
// being the wall-clock time the chip displayed ("22:31:05", the player's own clock), to read into a
// voice memo; tools/playtest-align.mjs finds it again in the transcript. A drawer opened is
// { t, op: 'ui', arg: { drawer } }.

const SNAPSHOT_MINUTES = 6 * 60;
const FLUSH_MS = 5000;
const REPLAYED = new Set(['newGame', 'act', 'view', 'save', 'load', 'scenario']);
const two = n => String(n).padStart(2, '0');

/** A wall-clock time as the chip shows it and a player reads it out: 24-hour, local, "22:31:05". */
export const wallClock = t => { const d = new Date(t); return `${two(d.getHours())}:${two(d.getMinutes())}:${two(d.getSeconds())}`; };

/** null when the URL asks for no log; otherwise where else it goes (null: memory only). */
export function logOptions(params) {
  if (!params.has('log')) return null;
  const to = params.get('log');
  return { endpoint: /^https?:\/\//.test(to) || to.startsWith('/') ? to : null };
}

/** The client with every call logged, and the log. build: the commit the page was built from (a
 *  string, or a promise of one). post: fetch, or a stand-in for it. */
export function withLog(inner, { endpoint = null, build = 'unknown', now = Date.now, post = (...a) => fetch(...a) } = {}) {
  const session = { id: crypto.randomUUID(), startedAt: new Date(now()).toISOString(), build: null, userAgent: globalThis.navigator?.userAgent ?? null };
  Promise.resolve(build).then(b => { session.build = b; }, () => { session.build = 'unknown'; });
  const entries = [], queue = [];
  const sink = { sent: 0, failures: 0, lastError: null };
  let shown = new Set(), minute = null, lastSnapshot = null, timer = null, sessionSent = false;

  const push = e => {
    entries.push(e);
    if (!endpoint) return;
    queue.push(e);
    timer ??= setTimeout(flush, FLUSH_MS);
    timer.unref?.(); // Node only: a pending post does not hold a test run open
  };

  async function flush() {
    timer = null;
    if (!queue.length) return;
    const batch = queue.splice(0);
    try {
      const r = await post(endpoint, { method: 'POST', headers: { 'Content-Type': 'application/json' }, keepalive: true,
        body: JSON.stringify({ session: sessionSent ? { id: session.id } : session, entries: batch }) });
      if (!r.ok) throw new Error(`${endpoint} answered ${r.status}`);
      sessionSent = true;
      sink.sent += batch.length;
    } catch (e) {
      queue.unshift(...batch); // kept for the next try; the memory copy has them regardless
      sink.failures++;
      sink.lastError = String(e?.message ?? e);
      console.warn(`playtest log: could not post ${batch.length} entries: ${sink.lastError}`);
      timer ??= setTimeout(flush, FLUSH_MS * 2);
      timer.unref?.();
    }
  }

  /** What a view says happened, in words: its minute, place, the job's end and the journal lines not seen before. */
  function summary(v) {
    const lines = v.log.filter(l => !shown.has(`${l.minute}|${l.text}`)).map(l => l.text);
    shown = new Set(v.log.map(l => `${l.minute}|${l.text}`));
    minute = v.minute;
    const job = v.job && { label: v.job.label, kind: v.job.kind, done: v.job.doneMinutes, finished: v.job.finished,
      ...(v.job.outcome && { outcome: v.job.outcome }), ...(v.job.reason && { reason: v.job.reason }) };
    return { minute: v.minute, ...(v.clock && { clock: `Day ${v.clock.day} ${two(Math.floor(v.clock.minuteOfDay / 60))}:${two(v.clock.minuteOfDay % 60)}` }), place: v.place.name, ...(job && { job }), ...(lines.length && { lines }),
      ...(v.outcome !== 'surviving' && { outcome: v.outcome }) }; // the end of the run: died or won
  }

  /** A save now and then, while no job is under way (the host refuses to save during one). */
  async function snapshot(v, entry) {
    if (v.job && !v.job.finished) return;
    if (lastSnapshot != null && v.minute - lastSnapshot < SNAPSHOT_MINUTES) return;
    lastSnapshot = v.minute;
    entry.save = new TextDecoder().decode(await inner.save());
  }

  const logged = (op, call) => async arg => {
    const t = now();
    const entry = { t, op, ...(arg !== undefined && { arg: op === 'load' ? new TextDecoder().decode(arg) : arg }) };
    try {
      const value = await call(arg);
      if (op === 'load' || op === 'newGame' || op === 'scenario') { shown = new Set(); lastSnapshot = null; }
      if (value && typeof value === 'object' && 'minute' in value) {
        Object.assign(entry, summary(value));
        await snapshot(value, entry);
      } else entry.minute = minute;
      return value;
    } catch (e) {
      entry.error = String(e?.message ?? e);
      entry.minute = minute;
      throw e;
    } finally {
      push(entry);
    }
  };

  const client = {
    ...inner,
    newGame: logged('newGame', seed => inner.newGame(seed)),
    act: logged('act', action => inner.act(action)),
    view: logged('view', () => inner.view()),
    save: logged('save', () => inner.save()),
    load: logged('load', bytes => inner.load(bytes)),
    scenario: logged('scenario', name => inner.scenario(name)),
  };

  const log = {
    session, entries, sink,
    /** The player's mark: a moment to find again in the voice memo. Returns the time to show and read out. */
    mark() { const t = now(), shown = wallClock(t); push({ t, op: 'mark', minute, shown }); return shown; },
    /** What the page showed that the game calls do not say: a drawer opened. */
    ui(arg) { push({ t: now(), op: 'ui', minute, arg }); },
    toJSON: () => ({ session, entries }),
    fileName: () => `playtest-${session.startedAt.replace(/[:.]/g, '-')}.json`,
    flush,
  };
  if (endpoint && typeof addEventListener === 'function')
    addEventListener('pagehide', () => {
      if (queue.length) navigator.sendBeacon(endpoint, new Blob([JSON.stringify({ session, entries: queue.splice(0) })], { type: 'application/json' }));
    });
  return { client, log };
}

/** Runs a log's calls again, in order, on a fresh client; resolves to the last view. Throws where a call
 *  that worked then fails now, or one that failed then works now: the game has changed under the log. */
export async function replay(record, client) {
  let view = null;
  for (const [i, e] of record.entries.entries()) {
    if (!REPLAYED.has(e.op)) continue;
    const arg = e.op === 'load' ? new TextEncoder().encode(e.arg) : e.arg;
    let value, error = null;
    try { value = await client[e.op](arg); } catch (err) { error = String(err?.message ?? err); }
    if ((error != null) !== (e.error != null))
      throw new Error(`entry ${i} (${e.op}): ${e.error ? `failed then (${e.error}) but not now` : `failed now: ${error}`}`);
    if (value && typeof value === 'object' && 'minute' in value) {
      if (value.minute !== e.minute) throw new Error(`entry ${i} (${e.op}): minute ${value.minute} now, ${e.minute} then`);
      view = value;
    }
  }
  return view;
}
