// The tune viewer: reads the run's progress.jsonl through the server, a few lines at a time, and
// draws it. Everything is plain SVG and one canvas; there is no framework.
'use strict';

const $ = id => document.getElementById(id);
const svgNS = 'http://www.w3.org/2000/svg';
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const fmt = (v, d = 3) => v == null || Number.isNaN(v) ? '-' : Math.abs(v) >= 100 ? v.toFixed(0) : (+v.toFixed(d)).toString();
const pct = v => v == null ? '-' : (v * 100).toFixed(v < 0.1 && v > 0 ? 1 : 0) + '%';

// How fights end, from team A's side, in the order the bars stack.
const ENDINGS = [
  ['Victory', 'A won', '--won'],
  ['AnimalFled', 'B ran', '--b-ran'],
  ['AnimalDisengaged', 'broke off', '--broke-off'],
  ['Fled', 'A ran', '--a-ran'],
  ['Defeat', 'A lost', '--lost'],
  ['Unfinished', 'unfinished', '--unfinished'],
];
const CODE_TO_ENDING = { V: 'Victory', D: 'Defeat', F: 'Fled', A: 'AnimalFled', X: 'AnimalDisengaged', U: 'Unfinished' };
const EDGE = 0.02;

const state = {
  header: null, gens: [], offset: 0, finished: false, legacy: null, gen: 1, follow: true,
  replay: { frames: [], index: 0, playing: null, info: null },
};

// ---- loading ----

async function boot() {
  const run = await (await fetch('/api/run')).json();
  state.run = run;
  $('run-name').textContent = run.name;
  document.title = `${run.name} · Tune viewer`;
  state.finished = run.files.includes('RESULT.md');
  if (!run.hasProgress) return legacy(run);
  await poll();
  setInterval(poll, 2000);
  setInterval(checkFinished, 5000);
}

async function poll() {
  const res = await fetch(`/api/progress?from=${state.offset}`);
  if (!res.ok) return;
  const text = await res.text();
  state.offset = +res.headers.get('X-Next-Offset');
  const lines = text.split('\n').filter(Boolean).map(l => JSON.parse(l));
  if (!lines.length && state.header) return renderStatus();
  for (const l of lines) {
    if (l.type === 'header') state.header = l;
    else if (l.type === 'generation') state.gens.push(l);
  }
  if (!state.header) return;
  if (!state.built) build();
  const n = state.gens.length;
  $('gen').max = Math.max(1, n);
  if (state.follow && n) setGen(n); else render();
}

async function checkFinished() {
  if (state.finished) return;
  const run = await (await fetch('/api/run')).json();
  state.finished = run.files.includes('RESULT.md');
  renderStatus();
}

// A run from before the log: only convergence.tsv can be shown.
async function legacy(run) {
  $('status').textContent = 'finished';
  const notice = $('notice');
  notice.hidden = false;
  notice.textContent = 'This run predates the run log (progress.jsonl). Only its convergence table is shown: '
    + 'per-scenario outcomes, parameter means and replays need a run made with today\'s tuner.';
  for (const id of ['scrubber', 'scenario-panel', 'param-panel', 'replay-panel']) $(id).hidden = true;
  if (!run.files.includes('convergence.tsv')) return;
  const tsv = await (await fetch('/api/file/convergence.tsv')).text();
  const [head, ...rows] = tsv.trim().split('\n').map(r => r.split('\t'));
  const col = name => head.indexOf(name);
  const data = rows.map(r => ({ g: +r[col('generation')], best: +r[col('best')], median: +r[col('median')], bestSoFar: +r[col('best_so_far')], sigma: +r[col('sigma')] }));
  lineChart($('fitness-chart'), data.map(d => d.g), [
    { name: 'best', color: css('--fit-best'), values: data.map(d => d.best) },
    { name: 'median', color: css('--fit-median'), values: data.map(d => d.median) },
    { name: 'best so far', color: css('--edge'), values: data.map(d => d.bestSoFar), dash: '4 3' },
  ], { log: true, height: 260 });
  lineChart($('sigma-chart'), data.map(d => d.g), [{ name: 'sigma (step size)', color: css('--a-ran'), values: data.map(d => d.sigma) }], { width: 300, height: 260, yMin: 0 });
  $('headline').textContent = `${data.length} generations, best so far ${fmt(data.at(-1).bestSoFar)}`;
}

// ---- layout, once the header is in ----

function build() {
  state.built = true;
  const h = state.header;
  const p = h.provenance;
  $('run-meta').textContent = `seed ${h.seeds.runSeed}, ${h.populations[0].size} candidates a generation, `
    + `${h.seeds.fightsPerScenario} fights per scenario, commit ${p.commit ? p.commit.slice(0, 10) : 'unknown'}${p.dirty ? ' with uncommitted changes' : ''}`;
  $('ending-legend').innerHTML = ENDINGS.map(([, label, c]) => `<span><i style="background:var(${c})"></i>${label}</span>`).join('');
  const names = Object.keys(h.scenarios);
  $('scenarios').innerHTML = names.map(n => `<article class="scen" tabindex="0" data-s="${esc(n)}"></article>`).join('');
  for (const el of document.querySelectorAll('.scen')) {
    const pick = () => { $('r-scenario').value = el.dataset.s; renderStrip(); $('replay-panel').scrollIntoView({ behavior: 'smooth' }); };
    el.addEventListener('click', pick);
    el.addEventListener('keydown', e => { if (e.key === 'Enter') pick(); });
  }
  $('r-scenario').innerHTML = names.map(n => `<option>${esc(n)}</option>`).join('');
  $('gen').addEventListener('input', e => { state.follow = false; $('follow').checked = false; setGen(+e.target.value); });
  $('follow').addEventListener('change', e => { state.follow = e.target.checked; if (state.follow) setGen(state.gens.length); });
  $('r-scenario').addEventListener('change', renderStrip);
  $('r-candidate').addEventListener('change', renderStrip);
  $('r-go').addEventListener('click', replay);
  $('r-play').addEventListener('click', togglePlay);
  $('r-step').addEventListener('click', () => showFrame(state.replay.index + 1));
  $('r-back').addEventListener('click', () => showFrame(state.replay.index - 1));
  $('r-frame').addEventListener('input', e => showFrame(+e.target.value));
}

function setGen(g) {
  state.gen = Math.max(1, Math.min(g, state.gens.length));
  $('gen').value = state.gen;
  render();
}

const pop = g => g.populations[0];
const genAt = () => state.gens[state.gen - 1];

function renderStatus() {
  const s = $('status');
  s.className = 'status' + (state.finished ? '' : ' live');
  s.textContent = state.finished ? 'finished' : 'live';
  const last = state.gens.at(-1);
  $('headline').textContent = last
    ? `generation ${last.generation}, best so far ${fmt(pop(last).fitness.bestSoFar)}, ${Math.round(last.seconds)} s in`
    : 'waiting for the first generation';
}

function render() {
  renderStatus();
  if (!state.gens.length) return;
  $('gen-out').textContent = state.gen;
  renderFitness();
  renderScenarios();
  renderParams();
  renderCandidates();
}

// ---- charts ----

// A line chart of several series over x. opts: log, height, cursor (an x to mark), yMin, yMax.
function lineChart(fig, xs, series, opts = {}) {
  const W = opts.width || 900, H = opts.height || 220, L = 44, R = 10, T = 10, B = 24;
  const all = series.flatMap(s => s.values).filter(v => v != null && Number.isFinite(v) && (!opts.log || v > 0));
  let lo = opts.yMin ?? Math.min(...all), hi = opts.yMax ?? Math.max(...all);
  if (!(hi > lo)) { hi = lo + 1; }
  const tf = opts.log ? Math.log10 : v => v;
  const x0 = xs[0], x1 = xs.at(-1) === x0 ? x0 + 1 : xs.at(-1);
  const X = x => L + (x - x0) / (x1 - x0) * (W - L - R);
  const Y = v => T + (1 - (tf(v) - tf(lo)) / (tf(hi) - tf(lo))) * (H - T - B);
  const ticks = opts.log ? logTicks(lo, hi) : linTicks(lo, hi);
  let s = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="${esc(series.map(x => x.name).join(', '))}"><g class="axis">`;
  for (const t of ticks) s += `<line x1="${L}" x2="${W - R}" y1="${Y(t)}" y2="${Y(t)}"/><text x="${L - 6}" y="${Y(t) + 4}" text-anchor="end">${fmt(t, 2)}</text>`;
  for (const t of linTicks(x0, x1, 8).filter(t => Number.isInteger(t))) s += `<text x="${X(t)}" y="${H - 6}" text-anchor="middle">${t}</text>`;
  s += '</g>';
  for (const se of series) {
    const pts = xs.map((x, i) => [x, se.values[i]]).filter(([, v]) => v != null && Number.isFinite(v) && (!opts.log || v > 0));
    if (!pts.length) continue;
    s += `<polyline fill="none" stroke="${se.color}" stroke-width="${se.width || 2}" ${se.dash ? `stroke-dasharray="${se.dash}"` : ''} points="${pts.map(([x, v]) => `${X(x).toFixed(1)},${Y(Math.min(hi, Math.max(lo, v))).toFixed(1)}`).join(' ')}"/>`;
  }
  if (opts.cursor != null) s += `<line class="cursor" x1="${X(opts.cursor)}" x2="${X(opts.cursor)}" y1="${T}" y2="${H - B}"/>`;
  s += '</svg>';
  fig.innerHTML = s + `<figcaption>${series.map(se => `<span class="key" style="color:${se.color}"><i></i><span style="color:var(--ink)">${esc(se.name)}</span></span>`).join('')}</figcaption>`;
}

function linTicks(lo, hi, n = 4) {
  const step = niceStep((hi - lo) / n);
  const out = [];
  for (let t = Math.ceil(lo / step) * step; t <= hi + 1e-9; t += step) out.push(+t.toFixed(10));
  return out;
}
function niceStep(raw) {
  const p = Math.pow(10, Math.floor(Math.log10(raw || 1)));
  const f = raw / p;
  return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * p;
}
function logTicks(lo, hi) {
  const out = [];
  for (let e = Math.floor(Math.log10(lo)); e <= Math.ceil(Math.log10(hi)); e++)
    for (const m of [1, 2, 5]) { const t = m * Math.pow(10, e); if (t >= lo * 0.999 && t <= hi * 1.001) out.push(t); }
  return out.length >= 2 ? out : [lo, hi];
}

function renderFitness() {
  const xs = state.gens.map(g => g.generation);
  const f = state.gens.map(g => pop(g).fitness);
  lineChart($('fitness-chart'), xs, [
    { name: 'worst', color: css('--fit-worst'), values: f.map(v => v.worst), width: 1.5 },
    { name: 'median', color: css('--fit-median'), values: f.map(v => v.median) },
    { name: 'best', color: css('--fit-best'), values: f.map(v => v.best) },
    { name: 'best so far', color: css('--edge'), values: f.map(v => v.bestSoFar), dash: '4 3' },
  ], { log: true, height: 260, cursor: state.gen });
  lineChart($('sigma-chart'), xs, [
    { name: 'sigma (step size)', color: css('--a-ran'), values: state.gens.map(g => pop(g).sigma) },
  ], { width: 300, height: 260, cursor: state.gen, yMin: 0 });
}

// ---- scenarios ----

function endingShares(results) {
  const total = Object.values(results).reduce((a, b) => a + b, 0) || 1;
  return ENDINGS.map(([k]) => (results[k] || 0) / total);
}

// Stacked endings over every generation: one column per generation.
function endingsArea(name) {
  const W = 300, H = 56, n = state.gens.length;
  const w = W / Math.max(1, n);
  let s = `<svg viewBox="0 0 ${W} ${H}" preserveAspectRatio="none" aria-hidden="true">`;
  state.gens.forEach((g, i) => {
    const sc = pop(g).best.scenarios[name];
    if (!sc) return;
    let y = 0;
    endingShares(sc.results).forEach((share, k) => {
      if (share > 0) s += `<rect x="${(i * w).toFixed(2)}" y="${(y * H).toFixed(2)}" width="${(w + 0.4).toFixed(2)}" height="${(share * H).toFixed(2)}" fill="var(${ENDINGS[k][2]})"/>`;
      y += share;
    });
  });
  const cx = (state.gen - 0.5) * w;
  s += `<line x1="${cx}" x2="${cx}" y1="0" y2="${H}" stroke="var(--ink)" stroke-width="1.5"/></svg>`;
  return s;
}

// One band's metric over generations, its target zone shaded, and a dot at the scrubbed generation.
function bandSpark(name, band) {
  const W = 92, H = 18, n = state.gens.length;
  const vals = state.gens.map(g => pop(g).best.scenarios[name]?.bands.find(b => b.name === band.name)?.value);
  const rounds = band.metric === 'rounds';
  const hi = rounds ? Math.max(1, ...vals.filter(v => v != null), band.max ?? 0, band.min ?? 0) * 1.1 : 1;
  const Y = v => H - 1 - (v / hi) * (H - 2);
  const X = i => n <= 1 ? W / 2 : i / (n - 1) * (W - 4) + 2;
  const zlo = band.min ?? 0, zhi = band.max ?? hi;
  let s = `<svg viewBox="0 0 ${W} ${H}" aria-hidden="true"><rect x="0" width="${W}" y="${Y(zhi)}" height="${Math.max(1, Y(zlo) - Y(zhi))}" fill="var(--band)"/>`;
  s += `<polyline fill="none" stroke="var(--ink-soft)" stroke-width="1.3" points="${vals.map((v, i) => v == null ? '' : `${X(i).toFixed(1)},${Y(v).toFixed(1)}`).join(' ')}"/>`;
  const v = vals[state.gen - 1];
  if (v != null) s += `<circle cx="${X(state.gen - 1)}" cy="${Y(v)}" r="2.6" fill="var(--ink)"/>`;
  return s + '</svg>';
}

function bandTarget(b) {
  const show = b.metric === 'rounds' ? v => fmt(v, 1) : pct;
  return b.min == null ? `≤ ${show(b.max)}` : b.max == null ? `≥ ${show(b.min)}` : `${show(b.min)}–${show(b.max)}`;
}

function renderScenarios() {
  const h = state.header, g = genAt(), best = pop(g).best;
  for (const el of document.querySelectorAll('.scen')) {
    const name = el.dataset.s, info = h.scenarios[name], sc = best.scenarios[name];
    if (!sc) { el.innerHTML = `<h3>${esc(name)}</h3><p class="setup">not fought this generation</p>`; continue; }
    const scored = info.bands.filter(b => b.scored);
    const misses = scored.filter(b => (sc.bands.find(r => r.name === b.name)?.distance ?? 0) > 0).length;
    el.className = 'scen' + (scored.length ? (misses ? ' miss' : ' hit') : '');
    const shares = endingShares(sc.results);
    const bar = shares.map((sh, k) => sh > 0 ? `<i style="width:${(sh * 100).toFixed(2)}%;background:var(${ENDINGS[k][2]})" title="${ENDINGS[k][1]} ${pct(sh)}"></i>` : '').join('');
    const parts = shares.map((sh, k) => sh > 0 ? `${ENDINGS[k][1]} ${pct(sh)}` : null).filter(Boolean).join(', ');
    const bands = info.bands.map(b => {
      const r = sc.bands.find(x => x.name === b.name);
      const cls = !b.scored ? 'unscored' : r && r.distance > 0 ? 'miss' : 'hit';
      const val = r ? (b.metric === 'rounds' ? fmt(r.value, 1) : pct(r.value)) : '-';
      return `<div class="band ${cls}" title="${esc(b.name)}: target ${bandTarget(b)} ${b.metric}${b.scored ? '' : ' (not scored)'}${r ? `, distance ${fmt(r.distance)}` : ''}">`
        + `<span class="name">${b.kind === 'penalty' ? 'penalty: ' : ''}${esc(b.name)}</span>${bandSpark(name, b)}<span class="val">${val} <small>${bandTarget(b)}</small></span></div>`;
    }).join('');
    el.innerHTML = `<h3>${esc(name)}</h3><p class="setup">${esc(info.setup)}, ${esc(info.weapon)}, ${esc(info.cond)}, ${esc(info.policy)}`
      + ` &nbsp;·&nbsp; ${sc.metrics.rounds.toFixed(1)} rounds on average</p>`
      + endingsArea(name) + `<div class="bar" role="img" aria-label="${esc(parts)}">${bar}</div><p class="bars-note">${esc(parts)}</p>` + bands;
  }
  $('orders').innerHTML = best.orders.length
    ? 'Orders: ' + best.orders.map(o => `<b>${esc(o.name)}</b> ${o.distance > 0 ? `off by ${fmt(o.distance)}` : 'held'}`).join('; ')
    : '';
}

// ---- parameters ----

function renderParams() {
  const params = state.header.populations[0].parameters, g = genAt(), p = pop(g);
  const n = state.gens.length;
  const W = 420, H = 26;
  const X = i => n <= 1 ? W / 2 : i / (n - 1) * (W - 2) + 1;
  const Y = u => H - 1 - Math.min(1, Math.max(0, u)) * (H - 2);
  let rows = '<tr><th>Parameter</th><th class="spark-col">Mean over generations</th><th>Mean now</th><th>Best now</th><th class="opt">Start</th><th class="opt">Range</th></tr>';
  params.forEach((par, i) => {
    const us = state.gens.map(x => pop(x).mean.unit[i]);
    const u = us[state.gen - 1];
    const edge = u < EDGE || u > 1 - EDGE;
    const start = unitOf(par, par.start);
    let s = `<svg viewBox="0 0 ${W} ${H}" preserveAspectRatio="none" aria-hidden="true">`
      + `<rect x="0" y="0" width="${W}" height="${(H - 2) * EDGE + 1}" fill="var(--edge)" opacity="0.25"/>`
      + `<rect x="0" y="${Y(EDGE)}" width="${W}" height="${(H - 2) * EDGE + 1}" fill="var(--edge)" opacity="0.25"/>`
      + `<line x1="0" x2="${W}" y1="${Y(start)}" y2="${Y(start)}" stroke="var(--rule)" stroke-dasharray="3 3"/>`
      + `<polyline fill="none" stroke="${edge ? 'var(--edge)' : 'var(--ink)'}" stroke-width="1.6" points="${us.map((v, k) => `${X(k).toFixed(1)},${Y(v).toFixed(1)}`).join(' ')}"/>`
      + `<line x1="${X(state.gen - 1)}" x2="${X(state.gen - 1)}" y1="0" y2="${H}" stroke="var(--edge)"/></svg>`;
    rows += `<tr class="${edge ? 'edge' : ''}"><td>${esc(par.name)}</td><td class="spark-col">${s}</td>`
      + `<td class="num">${fmt(p.mean.values[i])}</td><td class="num">${fmt(p.candidates[p.best.candidate].values[i])}</td>`
      + `<td class="num opt">${fmt(par.start)}</td><td class="num range opt">${fmt(par.min)}–${fmt(par.max)}${par.log ? ' log' : ''}${par.integer ? ' int' : ''}</td></tr>`;
  });
  $('params').innerHTML = rows;
}

function unitOf(par, v) {
  return par.log ? Math.log(v / par.min) / Math.log(par.max / par.min) : (v - par.min) / (par.max - par.min);
}

// ---- replays ----

function renderCandidates() {
  const sel = $('r-candidate'), p = pop(genAt()), was = sel.value;
  const opts = [`<option value="best">best of generation ${state.gen} (#${p.best.candidate}, fitness ${fmt(p.best.fitness)})</option>`,
    '<option value="mean">mean (not fought during the search)</option>']
    .concat(p.candidates.map((c, k) => `<option value="${k}">#${k}, fitness ${fmt(c.fitness)}</option>`));
  sel.innerHTML = opts.join('');
  sel.value = [...sel.options].some(o => o.value === was) ? was : 'best';
  $('r-fight').max = state.header.seeds.fightsPerScenario - 1;
  renderStrip();
}

function candidateIndex() {
  const v = $('r-candidate').value, p = pop(genAt());
  return v === 'best' ? p.best.candidate : v === 'mean' ? 'mean' : +v;
}

// The best candidate's fights in this scenario, one cell each, coloured by how they ended.
function renderStrip() {
  const wrap = $('r-strip'), name = $('r-scenario').value, p = pop(genAt());
  const c = candidateIndex();
  const sc = p.best.scenarios[name];
  if (c !== p.best.candidate || !sc || !sc.fights) {
    wrap.innerHTML = '<p>Fight-by-fight outcomes are logged for the best candidate only. Pick a fight index to fight it again.</p>';
    return;
  }
  const codes = sc.fights.split(' ');
  const info = state.header.scenarios[name];
  wrap.innerHTML = `<p>The ${codes.length} fights of generation ${state.gen}'s best candidate. Pick one.${info.watchable ? '' : ' This scenario is fought headless: a replay shows its outcome, not its rounds.'}</p>`
    + `<div class="strip">${codes.map((code, i) => {
      const e = ENDINGS.find(x => x[0] === CODE_TO_ENDING[code[0]]);
      return `<button type="button" data-i="${i}" style="background:var(${e ? e[2] : '--rule'})" title="fight ${i}: ${describe(code)}" aria-label="fight ${i}: ${describe(code)}"></button>`;
    }).join('')}</div>`;
  for (const b of wrap.querySelectorAll('button')) b.addEventListener('click', () => { $('r-fight').value = b.dataset.i; markStrip(); replay(); });
  markStrip();
}

function markStrip() {
  for (const b of $('r-strip').querySelectorAll('button')) b.classList.toggle('sel', b.dataset.i === $('r-fight').value);
}

function describe(code) {
  const ending = ENDINGS.find(x => x[0] === CODE_TO_ENDING[code[0]])?.[1] ?? code[0];
  const flags = +code[1], rounds = code.slice(3);
  const bits = [flags & 1 ? 'A died' : null, flags & 2 ? 'B killed' : null, flags & 4 ? 'A fled' : null].filter(Boolean);
  return `${ending}${bits.length ? ' (' + bits.join(', ') + ')' : ''}, ${rounds} rounds`;
}

async function replay() {
  stopPlay();
  markStrip();
  const msg = $('r-message');
  const name = $('r-scenario').value, cand = candidateIndex(), fight = +$('r-fight').value;
  msg.className = 'r-message';
  msg.textContent = `Fighting ${name}, fight ${fight}, again...`;
  $('r-body').hidden = true;
  const res = await fetch(`/api/replay?gen=${state.gen}&candidate=${cand}&scenario=${encodeURIComponent(name)}&fight=${fight}`);
  if (!res.ok) {
    let detail = await res.text();
    try { const j = JSON.parse(detail); detail = (j.title ? j.title + ': ' : '') + (j.detail || ''); } catch { /* plain text */ }
    msg.className = 'r-message refused';
    msg.textContent = detail;
    return;
  }
  const lines = (await res.text()).split('\n').filter(Boolean).map(l => JSON.parse(l));
  const info = lines[0], frames = lines.slice(1);
  state.replay = { frames, index: 0, playing: null, info };
  let text = `Generation ${info.gen}, ${info.mean ? 'the mean' : 'candidate #' + info.candidate}, ${name}, fight ${info.fight} (seed ${info.seed}): ${describe(info.outcome)}.`;
  if (info.matchesRecord === true) text += ' Matches the logged outcome.';
  if (info.matchesRecord === false) text += ` Does not match the logged outcome (${describe(info.recorded)}).`;
  if (info.mean) text += ' The mean was never fought during the search: this fight is computed now, on that generation\'s seeds.';
  msg.className = 'r-message' + (info.matchesRecord === false ? ' refused' : '');
  msg.textContent = text;
  if (!frames.length) {
    const note = document.createElement('p');
    note.className = 'r-message note';
    note.textContent = info.traceNote || 'No rounds were recorded for this fight.';
    msg.append(note);
    return;
  }
  $('r-body').hidden = false;
  $('r-frame').max = frames.length - 1;
  state.replay.grid = info.gridSize;
  showFrame(0);
}

function togglePlay() {
  if (state.replay.playing) return stopPlay();
  if (state.replay.index >= state.replay.frames.length - 1) showFrame(0);
  $('r-play').textContent = 'Pause';
  state.replay.playing = setInterval(() => {
    if (state.replay.index >= state.replay.frames.length - 1) return stopPlay();
    showFrame(state.replay.index + 1);
  }, 450);
}
function stopPlay() {
  clearInterval(state.replay.playing);
  state.replay.playing = null;
  $('r-play').textContent = 'Play';
}

const icons = {};
function icon(src) {
  if (!icons[src]) { const img = new Image(); img.onload = () => showFrame(state.replay.index); img.src = src; icons[src] = img; }
  return icons[src].complete && icons[src].naturalWidth ? icons[src] : null;
}
const iconFor = species => species === 'Human' ? '/icons/player.png' : `/icons/animals/${species.toLowerCase()}.png`;

function showFrame(i) {
  const r = state.replay;
  if (!r.frames.length) return;
  r.index = Math.max(0, Math.min(i, r.frames.length - 1));
  const f = r.frames[r.index];
  $('r-frame').value = r.index;
  $('r-round').textContent = `round ${f.round} of ${r.frames.at(-1).round}`;
  drawArena(f, r);
  const meter = v => v == null ? '-' : `<span class="meter"><i style="width:${Math.max(0, Math.min(1, v)) * 100}%"></i></span>${pct(v)}`;
  $('r-units').innerHTML = '<tr><th>Fighter</th><th>State</th><th>Action</th><th>Margin</th><th>Vitality</th><th>Blood</th><th>Reserve</th><th>Wounds</th></tr>'
    + f.units.map(u => `<tr class="${u.state === 'Down' ? 'down' : ''}"><td class="${u.team ? 'team-b' : 'team-a'}">${esc(u.species)}${u.member ? ' ' + (u.member + 1) : ''}</td>`
      + `<td>${esc(u.state)}</td><td>${esc(u.action ?? '')}</td><td class="num">${u.margin == null ? '-' : (u.margin >= 0 ? '+' : '') + u.margin.toFixed(2)}</td>`
      + `<td>${meter(u.vitality)}</td><td>${meter(u.blood)}</td><td>${meter(u.reserve)}</td><td>${meter(u.wounds)}</td></tr>`).join('');
  $('r-events').innerHTML = f.events.map(e => `<li>${esc(e)}</li>`).join('');
}

function drawArena(f, r) {
  const cv = $('r-canvas'), ctx = cv.getContext('2d'), size = r.grid || 50, cell = cv.width / size;
  ctx.imageSmoothingEnabled = false;
  const tile = icon('/icons/plain_tile.png');
  if (tile) { for (let y = 0; y < size; y += 2) for (let x = 0; x < size; x += 2) ctx.drawImage(tile, x * cell, y * cell, cell * 2, cell * 2); }
  else { ctx.fillStyle = css('--sheet'); ctx.fillRect(0, 0, cv.width, cv.height); }
  ctx.fillStyle = 'rgba(255,255,255,0.35)';
  ctx.fillRect(0, 0, cv.width, cv.height);
  ctx.strokeStyle = 'rgba(31,44,47,0.12)';
  ctx.lineWidth = 1;
  for (let k = 0; k <= size; k += 5) {
    ctx.beginPath(); ctx.moveTo(k * cell, 0); ctx.lineTo(k * cell, cv.height); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(0, k * cell); ctx.lineTo(cv.width, k * cell); ctx.stroke();
  }
  // The grid's y runs up; the canvas's runs down.
  const px = u => (u.x + 0.5) * cell, py = u => (size - u.y - 0.5) * cell;
  if (f.fire) {
    const fire = icon('/icons/fire.png');
    if (fire) ctx.drawImage(fire, px(f.fire) - cell * 1.5, py(f.fire) - cell * 1.5, cell * 3, cell * 3);
  }
  // Trails: where each fighter has been so far.
  f.units.forEach((u, k) => {
    ctx.strokeStyle = u.team ? css('--team-b') : css('--team-a');
    ctx.globalAlpha = 0.45;
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    r.frames.slice(0, r.index + 1).forEach((fr, j) => { const v = fr.units[k]; if (j === 0) ctx.moveTo(px(v), py(v)); else ctx.lineTo(px(v), py(v)); });
    ctx.stroke();
    ctx.globalAlpha = 1;
  });
  for (const u of f.units) {
    if (u.target != null && f.units[u.target]) {
      const t = f.units[u.target];
      ctx.setLineDash([4, 3]); ctx.strokeStyle = css('--ink'); ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(px(u), py(u)); ctx.lineTo(px(t), py(t)); ctx.stroke(); ctx.setLineDash([]);
    }
  }
  const s = cell * 2.2;
  for (const u of f.units) {
    const x = px(u), y = py(u);
    ctx.globalAlpha = u.state === 'Gone' ? 0.35 : 1;
    ctx.fillStyle = u.team ? css('--team-b') : css('--team-a');
    ctx.beginPath(); ctx.arc(x, y, s * 0.62, 0, Math.PI * 2); ctx.fill();
    const img = icon(iconFor(u.species));
    if (img) ctx.drawImage(img, x - s / 2, y - s / 2, s, s);
    if (u.state === 'Down') {
      ctx.strokeStyle = '#fff'; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(x - s / 2, y - s / 2); ctx.lineTo(x + s / 2, y + s / 2); ctx.moveTo(x + s / 2, y - s / 2); ctx.lineTo(x - s / 2, y + s / 2); ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }
}

boot().catch(e => { $('notice').hidden = false; $('notice').textContent = 'The viewer could not load this run: ' + e.message; });
