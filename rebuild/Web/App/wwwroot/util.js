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

/** The words of an odds line (an Odds as one line under its buttons): the base, unless `base` is false,
 *  then each factor that moves it by a whole percent or more as shown, in lower case. */
export function oddsWhy(o, { base = true } = {}) {
  const terms = o.terms.filter(t => Math.round(t.deltaPct * 100) !== 0)
    .map(t => `${t.factor.toLowerCase()} ${signed(Math.round(t.deltaPct * 100), '%')}`);
  return base ? [`${o.base.toLowerCase()} ${Math.round(o.basePct * 100)}%`, ...terms] : terms;
}

/** Where a trip card goes (CSS px in the map), kept 8 px inside it, as { left, top, side }: next to its
 *  tile on the `prefer`red side ('below' or 'above', away from the player) or else the other, centred on
 *  the tile; failing those, along the map's top or bottom edge; failing those, beside the tile. The first
 *  that fits and covers neither the player's tile (`me`) nor its own wins; if none, the first that fits.
 *  `area.top` is where the map stops being covered (the status strip's bottom). */
export function cardPlace(tile, card, area, prefer, me = null) {
  const lo = area.top + 8, hi = area.h - 8 - card.h, clampX = x => Math.max(8, Math.min(area.w - card.w - 8, x)), clampY = y => Math.max(lo, Math.min(hi, y));
  const cx = clampX(tile.x + tile.size / 2 - card.w / 2), cy = clampY(tile.y + tile.size / 2 - card.h / 2);
  const vertical = { below: [cx, tile.y + tile.size + 8], above: [cx, tile.y - 8 - card.h] };
  const order = prefer === 'above' ? ['above', 'below'] : ['below', 'above'];
  const spots = [...order.map(s => [s, ...vertical[s]]), ['top', cx, lo], ['bottom', cx, hi],
    ['right', tile.x + tile.size + 8, cy], ['left', tile.x - 8 - card.w, cy]];
  const fits = ([, x, y]) => x >= 8 && x + card.w <= area.w - 8 && y >= lo && y <= hi;
  const over = ([, x, y], t) => t && x < t.x + t.size && x + card.w > t.x && y < t.y + t.size && y + card.h > t.y;
  const [side, left, top] = spots.find(s => fits(s) && !over(s, me) && !over(s, tile)) ?? spots.find(fits) ?? spots[0];
  return { left: Math.round(left), top: Math.round(top), side };
}
