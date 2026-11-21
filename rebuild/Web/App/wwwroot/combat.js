// The fight's grid: one metre a cell, drawn from view.combat only. Thin on purpose: the combat rules
// are being rebuilt, so this draws whatever units and actions the host lists and decides nothing.
import { icon } from './util.js';

const ZONES = [[1, 'close'], [3, 'near'], [15, 'mid']];

/** state(): { view }. onCell(cell) is a click on the grid, in metres. */
export function createGrid(canvas, state, { onCell }) {
  const ctx = canvas.getContext('2d');
  let dpr = 1, hover = null, geo = null;
  function resize() {
    const r = canvas.getBoundingClientRect();
    dpr = Math.min(2, devicePixelRatio || 1);
    canvas.width = Math.round(r.width * dpr); canvas.height = Math.round(r.height * dpr);
  }
  new ResizeObserver(resize).observe(canvas);

  function frame() {
    requestAnimationFrame(frame);
    const c = state().view?.combat;
    const r = canvas.getBoundingClientRect(), w = r.width, h = r.height;
    if (!c || canvas.hidden || w === 0) return;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.fillStyle = '#0d1216'; ctx.fillRect(0, 0, w, h);
    const sideRoom = w > 860 ? 350 : 0;
    const cell = Math.max(6, Math.floor(Math.min((w - sideRoom - 32) / c.size, (h - 32) / c.size)));
    const ox = Math.round((w - sideRoom - cell * c.size) / 2), oy = Math.round((h - cell * c.size) / 2);
    geo = { cell, ox, oy, size: c.size };
    // the ground: snow-dulled earth, a line every five metres, the edges you can flee from
    ctx.fillStyle = '#c9cfd1'; ctx.fillRect(ox, oy, cell * c.size, cell * c.size);
    ctx.fillStyle = 'rgba(93,77,60,.22)';
    for (let i = 0; i < c.size; i++) for (let j = 0; j < c.size; j++) if ((i * 7 + j * 13) % 11 === 0) ctx.fillRect(ox + i * cell + 2, oy + j * cell + 2, 2, 2);
    ctx.strokeStyle = 'rgba(45,35,27,.18)'; ctx.lineWidth = 1;
    for (let i = 0; i <= c.size; i += 5) {
      ctx.beginPath(); ctx.moveTo(ox + i * cell + 0.5, oy); ctx.lineTo(ox + i * cell + 0.5, oy + cell * c.size); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(ox, oy + i * cell + 0.5); ctx.lineTo(ox + cell * c.size, oy + i * cell + 0.5); ctx.stroke();
    }
    ctx.fillStyle = 'rgba(207,111,76,.16)';
    const edge = 3 * cell;
    ctx.fillRect(ox, oy, cell * c.size, edge); ctx.fillRect(ox, oy + cell * (c.size - 3), cell * c.size, edge);
    ctx.fillRect(ox, oy, edge, cell * c.size); ctx.fillRect(ox + cell * (c.size - 3), oy, edge, cell * c.size);

    const me = c.units.find(u => u.isPlayer);
    const centre = u => [ox + (u.x + 0.5) * cell, oy + (u.y + 0.5) * cell];
    if (me?.inField) {
      const [mx, my] = centre(me);
      for (const [m, name] of ZONES) {
        ctx.strokeStyle = 'rgba(22,17,13,.35)'; ctx.setLineDash([4, 4]);
        ctx.beginPath(); ctx.arc(mx, my, m * cell, 0, Math.PI * 2); ctx.stroke(); ctx.setLineDash([]);
        ctx.fillStyle = 'rgba(22,17,13,.55)'; ctx.font = '600 12px "Barlow Semi Condensed", sans-serif'; ctx.textAlign = 'center';
        ctx.fillText(name, mx, my - m * cell - 3);
      }
      if (c.moveM > 0) { ctx.fillStyle = 'rgba(245,129,31,.10)'; ctx.beginPath(); ctx.arc(mx, my, c.moveM * cell, 0, Math.PI * 2); ctx.fill(); }
    }
    const size = Math.max(24, cell * 2.2);
    for (const u of c.units) {
      if (!u.inField && u.condition !== 'dead') continue;
      const [x, y] = centre(u), im = icon(u.isPlayer ? 'player' : u.species);
      ctx.globalAlpha = u.inField ? 1 : 0.4;
      if (u.index === c.target) { ctx.strokeStyle = '#ffc93c'; ctx.lineWidth = 2; ctx.strokeRect(x - size / 2 - 3, y - size / 2 - 3, size + 6, size + 6); }
      if (im) { ctx.imageSmoothingEnabled = false; ctx.drawImage(im, x - size / 2, y - size / 2, size, size); }
      ctx.globalAlpha = 1;
    }
    if (hover && me?.inField) {
      const d = Math.hypot(hover.x - me.x, hover.y - me.y);
      ctx.strokeStyle = d <= c.moveM ? '#eadfc8' : '#cf6f4c'; ctx.lineWidth = 2;
      ctx.strokeRect(ox + hover.x * cell + 1, oy + hover.y * cell + 1, cell - 2, cell - 2);
    }
  }
  requestAnimationFrame(frame);

  const cellAt = e => {
    if (!geo) return null;
    const r = canvas.getBoundingClientRect();
    const x = Math.floor((e.clientX - r.left - geo.ox) / geo.cell), y = Math.floor((e.clientY - r.top - geo.oy) / geo.cell);
    return x >= 0 && y >= 0 && x < geo.size && y < geo.size ? { x, y } : null;
  };
  canvas.addEventListener('mousemove', e => { hover = cellAt(e); });
  canvas.addEventListener('mouseleave', () => { hover = null; });
  canvas.addEventListener('click', e => { const c = cellAt(e); if (c) onCell(c); });
}
