// Small shared pieces: icons, time and number formatting, escaping.

const images = new Map();
/** The loaded image for an icon path under icons/ (no extension), or null while it loads. A missing file is an error. */
export function icon(name) {
  let e = images.get(name);
  if (!e) {
    const im = new Image();
    e = { im, ok: false };
    im.onload = () => { e.ok = true; };
    im.onerror = () => console.error('Missing icon', name);
    im.src = `icons/${name}.png`;
    images.set(name, e);
  }
  return e.ok ? e.im : null;
}
export const src = name => `icons/${name}.png`;
export const img = (name, cls = 'ico') => `<img class="${cls}" src="${src(name)}" alt="">`;

/** 12-hour clock: "1:05 PM". */
export function clock(minuteOfDay) {
  const m = ((Math.round(minuteOfDay) % 1440) + 1440) % 1440, h = Math.floor(m / 60);
  return `${h % 12 || 12}:${String(m % 60).padStart(2, '0')} ${h < 12 ? 'AM' : 'PM'}`;
}
export const dur = m => m >= 60 ? `${Math.floor(m / 60)}h ${String(Math.round(m % 60)).padStart(2, '0')}m` : `${Math.round(m)} min`;
export const ago = m => m < 2 ? 'just now' : m < 60 ? `${Math.round(m)}m ago` : m < 2880 ? `${Math.round(m / 60)}h ago` : `${Math.round(m / 1440)}d ago`;
export const pct = v => `${Math.round(v * 100)}%`;
export const deg = f => `${Math.round(f)}°F`;
export const signed = (n, unit = '') => `${n > 0 ? '+' : n < 0 ? '−' : ''}${Math.abs(n)}${unit}`;
export const cap = s => s ? s[0].toUpperCase() + s.slice(1) : s;

const ESC = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
export const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ESC[c]);
/** Text for the pixel face: it draws "fi" and "fl" as one glyph, so a zero-width non-joiner keeps them apart. */
export const px = s => esc(s).replace(/f(?=[il])/g, 'f‌');

/** A hover tip: title, then lines; a line may start g: (good), b: (bad) or m: (muted). */
export const tip = (title, lines = []) => `data-tip="${esc([title, ...lines].join('|'))}"`;

/** What a key does while a popup covers the page. A receipt is news to read and go on from: Escape,
 * Space or Enter close it ('close'); an action key (a letter or an arrow) closes it and goes on to do
 * what it does ('pass'). Other keys (Shift, Tab) and the end of the run leave it be ('keep'). */
export const veilKey = (kind, key) => kind !== 'receipt' ? 'keep' : key === 'Escape' || key === ' ' || key === 'Enter' ? 'close'
  : key.length === 1 || key.startsWith('Arrow') ? 'pass' : 'keep';

/** How long a job's minutes play on screen, in ms, as the old game paced them (Pacing): 0.3 s a game minute,
 *  1 to 30 s; a sleep four times faster; a crossing half a second to a second. */
export function paceMs(minutes, kind) {
  if (kind === 'walk') return Math.min(1200, Math.max(500, 500 + minutes * 30));
  return Math.min(30000, Math.max(1000, minutes * 300)) / (kind === 'sleep' ? 4 : 1);
}

/** Where the map's camera looks, in cells, so the player at `pos` sits in the middle of the part of the
 *  map that nothing covers. `coverPx` is how far in from the map's top, right and bottom edges the drawer
 *  and the status strip reach; `cellPx` is a cell's width. */
export function cameraTarget(pos, coverPx, cellPx) {
  return { x: pos.x + 0.5 + coverPx.right / 2 / cellPx, y: pos.y + 0.5 + (coverPx.bottom - coverPx.top) / 2 / cellPx };
}
