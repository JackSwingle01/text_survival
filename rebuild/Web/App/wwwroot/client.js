// The page's whole interface to the game: newGame, act, view, save and load, each a promise. This
// implementation runs the game in a Web Worker; a server would replace this one file with fetch calls
// to the same five operations (rebuild/Web.Spike/REPORT.md).
export function createClient(url = new URL('./worker.js', import.meta.url)) {
  const worker = new Worker(url, { type: 'module' });
  const pending = new Map();
  let next = 0;
  worker.onmessage = ({ data }) => {
    const p = pending.get(data.id);
    pending.delete(data.id);
    if (data.ok) p.resolve(data.value); else p.reject(new Error(data.error));
  };
  worker.onerror = e => { for (const p of pending.values()) p.reject(new Error(e.message || 'worker failed')); pending.clear(); };
  const call = (op, arg, transfer) => new Promise((resolve, reject) => {
    const id = ++next;
    pending.set(id, { resolve, reject });
    worker.postMessage({ id, op, arg }, transfer ?? []);
  });
  return {
    /** A new world from this seed; resolves to the first view. */
    newGame: seed => call('newGame', seed),
    /** { verb: id } starts a verb; { advance: minutes } runs the job on; { stop: true } drops it. */
    act: action => call('act', action),
    view: () => call('view'),
    /** The world as bytes (a versioned JSON save). */
    save: () => call('save'),
    load: bytes => call('load', bytes),
    /** Development only: a state for screenshots (Host Scenarios). */
    scenario: name => call('scenario', name),
    /** Development only: Observer mode (Host Observer), a world with no player in it. */
    observe: {
      /** Resolves to what never changes: terrain, named places, the camp, what can be spawned. */
      newWorld: seed => call('observeNew', seed),
      view: () => call('observeView'),
      map: () => call('observeMap'),
      advance: minutes => call('observeAdvance', minutes),
      spawn: (kind, x, y) => call('observeSpawn', { kind, x, y }),
      inspect: (x, y) => call('observeInspect', { x, y }),
    },
  };
}
