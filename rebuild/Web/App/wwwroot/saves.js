// Saves: one slot in IndexedDB, plus download and upload of the same bytes.
const DB = 'text-survival', STORE = 'saves', SLOT = 'slot1';

function open() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB, 1);
    req.onupgradeneeded = () => req.result.createObjectStore(STORE);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

function tx(mode, fn) {
  return open().then(db => new Promise((resolve, reject) => {
    const t = db.transaction(STORE, mode);
    const req = fn(t.objectStore(STORE));
    t.oncomplete = () => { db.close(); resolve(req?.result); };
    t.onerror = () => { db.close(); reject(t.error); };
  }));
}

/** meta: what the save list shows (day, time, place). */
export const putSave = (bytes, meta) => tx('readwrite', s => s.put({ bytes, meta, savedAt: Date.now() }, SLOT));
export const getSave = () => tx('readonly', s => s.get(SLOT));

export function download(bytes, name) {
  const url = URL.createObjectURL(new Blob([bytes], { type: 'application/json' }));
  const a = Object.assign(document.createElement('a'), { href: url, download: name });
  document.body.append(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function upload() {
  return new Promise(resolve => {
    const input = Object.assign(document.createElement('input'), { type: 'file', accept: '.json,application/json' });
    input.onchange = async () => resolve(input.files[0] ? new Uint8Array(await input.files[0].arrayBuffer()) : null);
    input.click();
  });
}
