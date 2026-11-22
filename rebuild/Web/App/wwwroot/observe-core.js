// Observer mode's camera and pacing: pure, so node can test them (tools/observe.test.mjs).
//
// The camera is the cell at the centre of the screen (x, y, fractional) and a scale in screen pixels
// per cell. It zooms about the pointer and pans with it, from the whole valley on screen to a few
// cells filling it, and its centre never leaves the map.

/** Pixels per cell all the way out (the whole valley fits) and all the way in (four times the art). */
export const MIN_SCALE = 2, MAX_SCALE = 192;

const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));
const onMap = (cam, map) => ({ ...cam, x: clamp(cam.x, 0, map.width), y: clamp(cam.y, 0, map.height) });

/** The whole map fitted to the view. view: { w, h } in CSS pixels. */
export function createCamera(map, view) {
  const scale = clamp(Math.min(view.w / map.width, view.h / map.height), MIN_SCALE, MAX_SCALE);
  return { x: map.width / 2, y: map.height / 2, scale };
}

/** The cell (fractional) under screen point (sx, sy). */
export const cellAt = (cam, sx, sy, view) => ({ x: cam.x + (sx - view.w / 2) / cam.scale, y: cam.y + (sy - view.h / 2) / cam.scale });

/** Zooms by factor about (sx, sy): the cell under that point stays under it. */
export function zoomAt(cam, factor, sx, sy, view, map) {
  const c = cellAt(cam, sx, sy, view), scale = clamp(cam.scale * factor, MIN_SCALE, MAX_SCALE);
  return onMap({ x: c.x - (sx - view.w / 2) / scale, y: c.y - (sy - view.h / 2) / scale, scale }, map);
}

/** The map follows a drag of (dx, dy) screen pixels. */
export const panBy = (cam, dx, dy, map) => onMap({ ...cam, x: cam.x - dx / cam.scale, y: cam.y - dy / cam.scale }, map);

/**
 * Turns a rate in game minutes per real second into whole minutes to ask the world for. Time that
 * could not be run (the world was slower than the rate) is banked for at most a second, so a slow
 * world falls behind rather than spiralling; and each call is sized from what recent calls cost, so
 * a call stays near targetMs and pause or a click is never stuck behind a long one.
 */
export function createPacer({ targetMs = 100, windowMs = 3000 } = {}) {
  let debt = 0, minutesPerMs = null, start = null;
  const history = [];
  return {
    /** Minutes to advance now, after dtSeconds at rate minutes per second. */
    due(rate, dtSeconds) {
      debt = Math.min(debt + rate * dtSeconds, Math.max(rate, 1));
      let n = Math.floor(debt);
      if (minutesPerMs != null) n = Math.min(n, Math.max(1, Math.floor(targetMs * minutesPerMs)));
      debt -= n;
      return n;
    },
    /** A call that advanced minutes took ms, finishing at now (ms). */
    ran(minutes, ms, now = performance.now()) {
      const sample = minutes / Math.max(ms, 0.5);
      minutesPerMs = minutesPerMs == null ? sample : (minutesPerMs + sample) / 2;
      start ??= now;
      history.push({ t: now, minutes });
      while (history.length && history[0].t <= now - windowMs) history.shift();
    },
    /** Game minutes per real second over the last windowMs. */
    achieved(now = performance.now()) {
      const span = start == null ? 0 : Math.min(windowMs, now - start);
      if (span <= 0) return 0;
      return history.filter(h => h.t > now - windowMs).reduce((s, h) => s + h.minutes, 0) / (span / 1000);
    },
    /** Forgets banked time (on pause, or a new rate). */
    reset() { debt = 0; },
  };
}
