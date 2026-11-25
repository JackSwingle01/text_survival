// The tune viewer: reads the run's progress.jsonl through the server, a few lines at a time, and
// draws it. Everything is plain SVG and one canvas; there is no framework.
// Layout rule: renders replace the contents of fixed-size boxes and never change a box's size, and
// nothing scrolls on its own (tools/layout-check.mjs measures it).
'use strict';

const $ = id => document.getElementById(id);
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const fmt = (v, d = 3) => v == null || Number.isNaN(v) ? '-' : Math.abs(v) >= 100 ? v.toFixed(0) : (+v.toFixed(d)).toString();
const pct = v => v == null ? '-' : (v * 100).toFixed(v < 0.1 && v > 0 ? 1 : 0) + '%';

// How fights end, in the order the bars stack. The colour belongs to the ending everywhere; the
// words come from the scenario's sides (endingLabel).
const ENDINGS = [
  ['Victory', '--won'],
  ['AnimalFled', '--b-ran'],
  ['AnimalDisengaged', '--broke-off'],
  ['Fled', '--a-ran'],
  ['Defeat', '--lost'],
  ['Unfinished', '--unfinished'],
];
const colourOf = ending => ENDINGS.find(e => e[0] === ending)?.[1] ?? '--rule';
const CODE_TO_ENDING = { V: 'Victory', D: 'Defeat', F: 'Fled', A: 'AnimalFled', X: 'AnimalDisengaged', U: 'Unfinished' };
const EDGE = 0.02;
const UNIT_ROWS = 6;

const state = {
  header: null, gens: [], offset: 0, finished: false, gen: 1, follow: true, scenario: null,
  replay: { frames: [], index: 0, playing: null, info: null },
};

// ---- the two sides of a scenario, named from its spec ----

const WEAPON = { none: 'bare hands', flint_knife: 'knife', wooden_spear: 'spear', ground_stone_axe: 'axe',
  flint_axe: 'flint axe', obsidian_knife: 'obsidian knife', bone_knife: 'bone knife', stone_club: 'club',
  flint_tipped_spear: 'flint spear', obsidian_tipped_spear: 'obsidian spear', ivory_tipped_spear: 'ivory spear' };
const POLICY = {
  ai: 'fought by the AI', aggressive: 'attacks', cautious: 'cautious', stand_intimidate: 'stands and intimidates', run: 'runs',
  careful_melee: 'careful approach, melee', careful_throw: 'careful approach, throws', quick_melee: 'quick approach, melee', quick_throw: 'quick approach, throws',
};
const COND = { healthy: 'healthy', tired: 'tired', hungry: 'hungry', cold95: 'chilled', cold92: 'hypothermic', leg: 'leg wound', arm: 'arm wound', bleeding: 'bleeding', meat: 'carrying meat' };

// A side: name (on cards), the (as a subject), obj (as an object), you (the player drives it).
const one = (species, extra = '') => ({ name: species + extra, the: 'the ' + species, obj: 'it', subj: 'it' });
const many = (n, species, plural, pack) => ({
  name: pack ? `pack of ${n} ${plural}` : `${n} ${plural}`, the: pack ? 'the pack' : 'the ' + plural, obj: 'them', subj: 'they', pack,
});
const you = weapon => ({ name: `you (${WEAPON[weapon] ?? weapon.replace(/_/g, ' ')})`, the: 'you', obj: 'you', subj: 'you', you: true });
const PLURAL = { wolf: 'wolves', caribou: 'caribou', bison: 'bison', bear: 'bears', person: 'people' };
const side = (n, species, pack) => n === 1 ? one(species) : many(n, species, PLURAL[species] ?? species + 's', pack);

function sidesOf(name) {
  const info = state.header.scenarios[name];
  if (info.sides) return info.sides;                 // a display name per side, when the spec gives one
  const s = info.setup;
  let m;
  if ((m = s.match(/^hunt_([a-z]+)_/))) return { a: you(info.weapon), b: one(m[1]), hunt: true };
  if ((m = s.match(/^wolf1_/))) return { a: you(info.weapon), b: one('wolf') };
  if ((m = s.match(/^pack(\d+)_/))) return { a: you(info.weapon), b: side(+m[1], 'wolf', true) };
  if (s === 'bear_den') return { a: you(info.weapon), b: { ...one('bear'), name: 'bear at its den' } };
  if (s.startsWith('bear_')) return { a: you(info.weapon), b: one('bear') };
  if ((m = s.match(/^wolves(\d+)_vs_([a-z]+?)(\d+)/))) return { a: side(+m[1], 'wolf', true), b: side(+m[3], m[2]) };
  if ((m = s.match(/^(one|two)_people?_vs_wolves(\d+)/))) {
    const n = m[1] === 'two' ? 2 : 1;
    const a = n === 1 ? { name: 'a person', the: 'the person', obj: 'them', subj: 'they' } : side(2, 'person');
    if (info.weapon !== 'none') a.name += ` (${WEAPON[info.weapon] ?? info.weapon})`;
    return { a, b: side(+m[2], 'wolf') };
  }
  if ((m = s.match(/^(.+)_vs_(.+)$/))) return { a: { ...one(m[1].replace(/_/g, ' ')), the: 'the ' + m[1].replace(/_/g, ' ') }, b: one(m[2].replace(/_/g, ' ')) };
  return { a: you(info.weapon), b: one(s.replace(/_/g, ' ')) };
}

const versus = sides => `${sides.a.name} vs ${sides.b.name}`;

// An ending in words, from the first side's point of view.
function endingLabel(ending, sides) {
  const { a, b } = sides;
  switch (ending) {
    case 'Victory': return a.you ? `you killed ${b.obj}` : a.pack ? 'the pack made a kill' : `${a.the} killed ${b.obj}`;
    case 'AnimalFled': return `${b.subj} fled`;
    case 'AnimalDisengaged': return 'broke off';
    case 'Fled': return `${a.the} fled`;
    case 'Defeat': return a.you ? 'you died' : `${a.the} died`;
    case 'Unfinished': return 'unfinished';
    default: return ending;
  }
}

// The metrics the bands measure, in the scenario's words.
function metricLabel(metric, sides) {
  const { a, b } = sides;
  return {
    aDies: `${a.the} died`, bKilled: `${b.the} killed`, aFled: `${a.the} fled`, bFled: `${b.the} fled`,
    aWinsOrDrivesOff: `${a.the} won or drove ${b.obj} off`, bLeaves: `${b.the} left`, rounds: 'rounds',
  }[metric] ?? metric;
}

// ---- loading ----

async function boot() {
  const run = await (await fetch('/api/run')).json();
  state.run = run;
  $('run-name').textContent = run.name;
  document.title = `${run.name}, tune viewer`;
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
  for (const id of ['scrubber', 'param-panel', 'center-pane', 'dock']) $(id).hidden = true;
  if (!run.files.includes('convergence.tsv')) return;
  const tsv = await (await fetch('/api/file/convergence.tsv')).text();
  const [head, ...rows] = tsv.trim().split('\n').map(r => r.split('\t'));
  const col = name => head.indexOf(name);
  const data = rows.map(r => ({ g: +r[col('generation')], best: +r[col('best')], median: +r[col('median')], bestSoFar: +r[col('best_so_far')], sigma: +r[col('sigma')] }));
  lineChart($('fitness-chart'), data.map(d => d.g), [
    { name: 'best', color: css('--fit-best'), values: data.map(d => d.best) },
    { name: 'median', color: css('--fit-median'), values: data.map(d => d.median) },
    { name: 'best so far', color: css('--edge'), values: data.map(d => d.bestSoFar), dash: '4 3' },
  ], { log: true });
  lineChart($('sigma-chart'), data.map(d => d.g), [{ name: 'sigma (step size)', color: css('--a-ran'), values: data.map(d => d.sigma) }], { yMin: 0 });
  $('headline').textContent = `${data.length} generations, best so far ${fmt(data.at(-1).bestSoFar)}`;
}

// ---- layout, once the header is in ----

function build() {
  state.built = true;
  const h = state.header;
  const p = h.provenance;
  $('run-meta').textContent = `seed ${h.seeds.runSeed}, ${h.populations[0].size} candidates a generation, `
    + `${h.seeds.fightsPerScenario} fights per scenario, commit ${p.commit ? p.commit.slice(0, 10) : 'unknown'}${p.dirty ? ' with uncommitted changes' : ''}`;
  $('run-meta').title = $('run-meta').textContent + (p.sourceHash ? `, fight code ${p.sourceHash.slice(0, 12)}` : '');
  // The legend speaks for the side that started it: you, or the hunting pack.
  const generic = { a: { the: 'you', obj: 'it', you: true }, b: { the: 'it', subj: 'it', obj: 'it' } };
  $('ending-legend').innerHTML = '<span>Endings:</span>' + ENDINGS.map(([k, c]) => `<span><i style="background:var(${c})"></i>${endingLabel(k, generic)}</span>`).join('');
  $('ending-legend').title = 'Each ending has one colour everywhere. Cards and the strip name it from their own sides: for a pack hunt, "you killed it" is "the pack made a kill".';

  const names = Object.keys(h.scenarios);
  // Every card is as tall as the one with the most bands, so cards line up and never grow.
  const maxBands = Math.max(1, ...names.map(n => h.scenarios[n].bands.length));
  document.documentElement.style.setProperty('--card-h', `${154 + 38 * maxBands}px`);
  $('scenarios').innerHTML = names.map(n => `<article class="scen" tabindex="0" role="button" data-s="${esc(n)}"></article>`).join('');
  for (const el of document.querySelectorAll('.scen')) {
    el.addEventListener('click', () => selectScenario(el.dataset.s));
    el.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); selectScenario(el.dataset.s); } });
  }

  // The strip's box is sized once, for every fight a scenario has.
  const n = h.seeds.fightsPerScenario;
  let cols = Math.min(60, n), rows = Math.ceil(n / cols);
  if (rows > 8) { rows = 8; cols = Math.ceil(n / rows); }
  document.documentElement.style.setProperty('--strip-cols', cols);
  document.documentElement.style.setProperty('--strip-h', `${22 + rows * 15}px`);
  $('r-fight').max = n - 1;

  $('gen').addEventListener('input', e => { unfollow(); setGen(+e.target.value); });
  $('gen-prev').addEventListener('click', () => { unfollow(); setGen(state.gen - 1); });
  $('gen-next').addEventListener('click', () => { unfollow(); setGen(state.gen + 1); });
  $('follow').addEventListener('change', e => { state.follow = e.target.checked; if (state.follow) setGen(state.gens.length); });
  $('r-candidate').addEventListener('change', renderStrip);
  $('r-fight').addEventListener('change', markStrip);
  $('r-go').addEventListener('click', replay);
  $('r-play').addEventListener('click', togglePlay);
  $('r-step').addEventListener('click', () => { stopPlay(); showFrame(state.replay.index + 1); });
  $('r-back').addEventListener('click', () => { stopPlay(); showFrame(state.replay.index - 1); });
  $('r-frame').addEventListener('input', e => { stopPlay(); showFrame(+e.target.value); });
  document.addEventListener('keydown', onKey);
  emptyUnits();
  selectScenario(names[0]);
}

function unfollow() { state.follow = false; $('follow').checked = false; }

function onKey(e) {
  if (e.metaKey || e.ctrlKey || e.altKey) return;
  const t = e.target;
  if (t.matches?.('input, select, textarea')) return;
  if (e.key === ' ') { e.preventDefault(); if (state.replay.frames.length) togglePlay(); }
  else if (e.key === 'ArrowRight') { e.preventDefault(); stopPlay(); showFrame(state.replay.index + 1); }
  else if (e.key === 'ArrowLeft') { e.preventDefault(); stopPlay(); showFrame(state.replay.index - 1); }
  else if (e.key === ']') { unfollow(); setGen(state.gen + 1); }
  else if (e.key === '[') { unfollow(); setGen(state.gen - 1); }
}

function selectScenario(name) {
  state.scenario = name;
  for (const el of document.querySelectorAll('.scen')) el.classList.toggle('sel', el.dataset.s === name);
  const info = state.header.scenarios[name], sides = sidesOf(name);
  $('dock-h').textContent = versus(sides);
  $('dock-setup').textContent = `${name}: ${setupWords(info)}`;
  if (state.gens.length) renderStrip();
}

function setupWords(info) {
  return [info.setup.replace(/_/g, ' '), POLICY[info.policy] ?? info.policy, COND[info.cond] ?? info.cond].join(', ');
}

function setGen(g) {
  if (!state.gens.length) return;
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
  $('gen-of').textContent = `of ${state.gens.length}`;
  renderFitness();
  renderScenarios();
  renderParams();
  renderCandidates();
}

// ---- charts ----

// A line chart of several series over x, drawn at the figure's own pixel size.
// opts: log, cursor (an x to mark), yMin, yMax.
function lineChart(fig, xs, series, opts = {}) {
  const W = Math.max(120, fig.clientWidth || 300), H = Math.max(60, (fig.clientHeight || 160) - 20), L = 40, R = 6, T = 6, B = 18;
  const all = series.flatMap(s => s.values).filter(v => v != null && Number.isFinite(v) && (!opts.log || v > 0));
  let lo = opts.yMin ?? Math.min(...all), hi = opts.yMax ?? Math.max(...all);
  if (!(hi > lo)) { hi = lo + 1; }
  const tf = opts.log ? Math.log10 : v => v;
  const x0 = xs[0], x1 = xs.at(-1) === x0 ? x0 + 1 : xs.at(-1);
  const X = x => L + (x - x0) / (x1 - x0) * (W - L - R);
  const Y = v => T + (1 - (tf(v) - tf(lo)) / (tf(hi) - tf(lo))) * (H - T - B);
  const ticks = opts.log ? logTicks(lo, hi) : linTicks(lo, hi, 3);
  let s = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="${esc(series.map(x => x.name).join(', '))}"><g class="axis">`;
  for (const t of ticks) s += `<line x1="${L}" x2="${W - R}" y1="${Y(t)}" y2="${Y(t)}"/><text x="${L - 5}" y="${Y(t) + 4}" text-anchor="end">${fmt(t, 2)}</text>`;
  for (const t of linTicks(x0, x1, 5).filter(t => Number.isInteger(t))) s += `<text x="${X(t)}" y="${H - 4}" text-anchor="middle">${t}</text>`;
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
  ], { log: true, cursor: state.gen });
  lineChart($('sigma-chart'), xs, [
    { name: 'sigma (step size)', color: css('--a-ran'), values: state.gens.map(g => pop(g).sigma) },
  ], { cursor: state.gen, yMin: 0 });
}

// ---- scenarios ----

function endingShares(results) {
  const total = Object.values(results).reduce((a, b) => a + b, 0) || 1;
  return ENDINGS.map(([k]) => (results[k] || 0) / total);
}

// Stacked endings over every generation: one column per generation.
function endingsArea(name) {
  const W = 300, H = 50, n = state.gens.length;
  const w = W / Math.max(1, n);
  let s = `<svg class="area" viewBox="0 0 ${W} ${H}" preserveAspectRatio="none" aria-hidden="true">`;
  state.gens.forEach((g, i) => {
    const sc = pop(g).best.scenarios[name];
    if (!sc) return;
    let y = 0;
    endingShares(sc.results).forEach((share, k) => {
      if (share > 0) s += `<rect x="${(i * w).toFixed(2)}" y="${(y * H).toFixed(2)}" width="${(w + 0.4).toFixed(2)}" height="${(share * H).toFixed(2)}" fill="var(${ENDINGS[k][1]})"/>`;
      y += share;
    });
  });
  const cx = (state.gen - 0.5) * w;
  s += `<line x1="${cx}" x2="${cx}" y1="0" y2="${H}" stroke="var(--ink)" stroke-width="2" vector-effect="non-scaling-stroke"/></svg>`;
  return s;
}

// One band's metric over generations, its target zone shaded, and a dot at the scrubbed generation.
function bandSpark(name, band) {
  const W = 200, H = 16, n = state.gens.length;
  const vals = state.gens.map(g => pop(g).best.scenarios[name]?.bands.find(b => b.name === band.name)?.value);
  const rounds = band.metric === 'rounds';
  const hi = rounds ? Math.max(1, ...vals.filter(v => v != null), band.max ?? 0, band.min ?? 0) * 1.1 : 1;
  const Y = v => H - 1 - (v / hi) * (H - 2);
  const X = i => n <= 1 ? W / 2 : i / (n - 1) * (W - 4) + 2;
  const zlo = band.min ?? 0, zhi = band.max ?? hi;
  let s = `<svg viewBox="0 0 ${W} ${H}" preserveAspectRatio="none" aria-hidden="true"><rect x="0" width="${W}" y="${Y(zhi)}" height="${Math.max(1, Y(zlo) - Y(zhi))}" fill="var(--band)"/>`;
  s += `<polyline fill="none" stroke="var(--ink-soft)" stroke-width="1.3" vector-effect="non-scaling-stroke" points="${vals.map((v, i) => v == null ? '' : `${X(i).toFixed(1)},${Y(v).toFixed(1)}`).join(' ')}"/>`;
  const v = vals[state.gen - 1];
  if (v != null) s += `<circle cx="${X(state.gen - 1)}" cy="${Y(v)}" r="2.6" fill="var(--ink)"/>`;
  return s + '</svg>';
}

function bandTarget(b) {
  const show = b.metric === 'rounds' ? v => fmt(v, 1) : pct;
  return b.min == null ? `≤ ${show(b.max)}` : b.max == null ? `≥ ${show(b.min)}` : `${show(b.min)}–${show(b.max)}`;
}

const MARK = { hit: '✓', miss: '✕', unscored: '' };

function renderScenarios() {
  const h = state.header, g = genAt(), best = pop(g).best;
  for (const el of document.querySelectorAll('.scen')) {
    const name = el.dataset.s, info = h.scenarios[name], sc = best.scenarios[name], sides = sidesOf(name);
    const sel = name === state.scenario ? ' sel' : '';
    const head = `<div class="title"><h3 title="${esc(versus(sides))}">${esc(versus(sides))}</h3>`;
    if (!sc) { el.className = 'scen' + sel; el.innerHTML = head + `</div><p class="setup">${esc(name)}: not fought this generation</p>`; continue; }
    const scored = info.bands.filter(b => b.scored);
    const misses = scored.filter(b => (sc.bands.find(r => r.name === b.name)?.distance ?? 0) > 0).length;
    el.className = 'scen' + (scored.length ? (misses ? ' miss' : ' hit') : '') + sel;
    const tally = scored.length
      ? `<span class="tally ${misses ? 'miss' : 'hit'}">${scored.length - misses} of ${scored.length} bands hit</span>`
      : '<span class="tally none">no bands</span>';
    const shares = endingShares(sc.results);
    const words = ENDINGS.map(([k]) => endingLabel(k, sides));
    const bar = shares.map((sh, k) => sh > 0 ? `<i style="width:${(sh * 100).toFixed(2)}%;background:var(${ENDINGS[k][1]})" title="${esc(words[k])} ${pct(sh)}"></i>` : '').join('');
    const parts = shares.map((sh, k) => sh > 0 ? `${words[k]} ${pct(sh)}` : null).filter(Boolean).join(', ');
    const bands = info.bands.map(b => {
      const r = sc.bands.find(x => x.name === b.name);
      const cls = !b.scored ? 'unscored' : r && r.distance > 0 ? 'miss' : 'hit';
      const val = r ? (b.metric === 'rounds' ? fmt(r.value, 1) : pct(r.value)) : '-';
      return `<div class="band ${cls}" title="${esc(b.name)}: ${esc(metricLabel(b.metric, sides))}, target ${bandTarget(b)}${b.scored ? '' : ' (not scored)'}${r ? `, distance ${fmt(r.distance)}` : ''}">`
        + `<span class="mark ${cls}" aria-label="${cls === 'hit' ? 'hit' : cls === 'miss' ? 'missed' : 'not scored'}">${MARK[cls]}</span>`
        + `<span class="name">${b.kind === 'penalty' ? 'penalty: ' : ''}${esc(b.name)}</span>${bandSpark(name, b)}<span class="val">${val} <small>${bandTarget(b)}</small></span></div>`;
    }).join('');
    el.innerHTML = head + tally + '</div>'
      + `<p class="setup" title="${esc(name)}">${esc(name)}: ${esc(setupWords(info))}, ${sc.metrics.rounds.toFixed(1)} rounds on average</p>`
      + endingsArea(name) + `<div class="bar" role="img" aria-label="${esc(parts)}">${bar}</div><p class="bars-note" title="${esc(parts)}">${esc(parts)}</p>`
      + `<div class="bands">${bands || '<p class="no-bands">No bands on this scenario: watched, not scored.</p>'}</div>`;
  }
  $('orders').innerHTML = best.orders.length
    ? 'Orders: ' + best.orders.map(o => `<b>${esc(o.name)}</b> ${o.distance > 0 ? `off by ${fmt(o.distance)}` : 'held'}`).join('; ')
    : '';
  $('orders').title = $('orders').textContent;
}

// ---- parameters ----

function renderParams() {
  const params = state.header.populations[0].parameters, g = genAt(), p = pop(g);
  const n = state.gens.length;
  const W = 120, H = 26;
  const X = i => n <= 1 ? W / 2 : i / (n - 1) * (W - 2) + 1;
  const Y = u => H - 1 - Math.min(1, Math.max(0, u)) * (H - 2);
  let rows = '<colgroup><col class="c-name"><col class="c-spark"><col class="c-num"><col class="c-num"></colgroup>'
    + '<tr><th>Parameter</th><th>Mean so far</th><th class="num">Mean</th><th class="num">Best</th></tr>';
  params.forEach((par, i) => {
    const us = state.gens.map(x => pop(x).mean.unit[i]);
    const u = us[state.gen - 1];
    const edge = u < EDGE || u > 1 - EDGE;
    const start = unitOf(par, par.start);
    const s = `<svg viewBox="0 0 ${W} ${H}" preserveAspectRatio="none" aria-hidden="true">`
      + `<rect x="0" y="0" width="${W}" height="${(H - 2) * EDGE + 1}" fill="var(--edge)" opacity="0.25"/>`
      + `<rect x="0" y="${Y(EDGE)}" width="${W}" height="${(H - 2) * EDGE + 1}" fill="var(--edge)" opacity="0.25"/>`
      + `<line x1="0" x2="${W}" y1="${Y(start)}" y2="${Y(start)}" stroke="var(--rule)" stroke-dasharray="3 3"/>`
      + `<polyline fill="none" stroke="${edge ? 'var(--edge)' : 'var(--ink)'}" stroke-width="1.6" vector-effect="non-scaling-stroke" points="${us.map((v, k) => `${X(k).toFixed(1)},${Y(v).toFixed(1)}`).join(' ')}"/>`
      + `<line x1="${X(state.gen - 1)}" x2="${X(state.gen - 1)}" y1="0" y2="${H}" stroke="var(--edge)" vector-effect="non-scaling-stroke"/></svg>`;
    const range = `start ${fmt(par.start)}, ${fmt(par.min)}–${fmt(par.max)}${par.log ? ' log' : ''}${par.integer ? ' int' : ''}${edge ? ', at the edge' : ''}`;
    rows += `<tr class="${edge ? 'edge' : ''}"><td><span class="pname" title="${esc(par.name)}">${esc(par.name)}</span><span class="range">${range}</span></td><td>${s}</td>`
      + `<td class="num">${fmt(p.mean.values[i])}</td><td class="num">${fmt(p.candidates[p.best.candidate].values[i])}</td></tr>`;
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
  renderStrip();
}

function candidateIndex() {
  const v = $('r-candidate').value, p = pop(genAt());
  return v === 'best' ? p.best.candidate : v === 'mean' ? 'mean' : +v;
}

// The best candidate's fights in this scenario, one cell each, coloured by how they ended.
function renderStrip() {
  const strip = $('r-strip'), note = $('r-strip-note'), name = state.scenario, p = pop(genAt());
  const c = candidateIndex();
  const sc = p.best.scenarios[name];
  if (c !== p.best.candidate || !sc || !sc.fights) {
    note.textContent = 'Fight-by-fight endings are logged for the best candidate only. Pick a fight number to fight it again.';
    strip.innerHTML = '';
    return;
  }
  const codes = sc.fights.split(' '), sides = sidesOf(name);
  note.textContent = `Generation ${state.gen}'s best candidate, ${codes.length} fights. Pick one to fight it again; space plays, arrows step.`;
  strip.innerHTML = codes.map((code, i) => {
    const d = describe(code, sides);
    return `<button type="button" data-i="${i}" style="background:var(${colourOf(CODE_TO_ENDING[code[0]])})" title="fight ${i}: ${esc(d)}" aria-label="fight ${i}: ${esc(d)}"></button>`;
  }).join('');
  for (const b of strip.querySelectorAll('button')) b.addEventListener('click', () => { $('r-fight').value = b.dataset.i; replay(); });
  markStrip();
}

function markStrip() {
  for (const b of $('r-strip').querySelectorAll('button')) b.classList.toggle('sel', b.dataset.i === $('r-fight').value);
}

function describe(code, sides) {
  const label = endingLabel(CODE_TO_ENDING[code[0]] ?? code[0], sides);
  const flags = +code[1], rounds = code.slice(3);
  // The flags say what the ending does not already: a kill in a fight that ended another way.
  const e = code[0];
  const bits = [flags & 1 && e !== 'D' ? `${sides.a.the} died` : null, flags & 2 && e !== 'V' ? `${sides.b.the} killed` : null,
    flags & 4 && e !== 'F' ? `${sides.a.the} fled` : null].filter(Boolean);
  return `${label}${bits.length ? ' (' + bits.join(', ') + ')' : ''}, ${rounds} rounds`;
}

function setMessage(text, kind = '') {
  const m = $('r-message');
  m.className = 'r-message' + (kind ? ' ' + kind : '');
  m.textContent = text;
}
function setArenaState(text, kind = '') {
  const a = $('arena-state');
  a.hidden = text == null;
  a.className = 'arena-state' + (kind ? ' ' + kind : '');
  a.textContent = text ?? '';
}

async function replay() {
  stopPlay();
  markStrip();
  const name = state.scenario, cand = candidateIndex(), fight = +$('r-fight').value, sides = sidesOf(name);
  const ticket = (state.ticket = (state.ticket || 0) + 1);
  setMessage(`Fighting ${versus(sides)}, fight ${fight}, again...`, 'busy');
  setArenaState('Fighting it again...');
  const res = await fetch(`/api/replay?gen=${state.gen}&candidate=${cand}&scenario=${encodeURIComponent(name)}&fight=${fight}`);
  if (ticket !== state.ticket) return;   // a later pick superseded this one
  if (!res.ok) {
    let detail = await res.text();
    try { const j = JSON.parse(detail); detail = (j.title ? j.title + ': ' : '') + (j.detail || ''); } catch { /* plain text */ }
    clearReplay();
    setMessage(detail, 'refused');
    setArenaState('No replay: the server refused it. The reason is above.', 'refused');
    return;
  }
  const lines = (await res.text()).split('\n').filter(Boolean).map(l => JSON.parse(l));
  if (ticket !== state.ticket) return;
  const info = lines[0], frames = lines.slice(1);
  state.replay = { frames, index: 0, playing: null, info, grid: info.gridSize, sides };
  let text = `Generation ${info.gen}, ${info.mean ? 'the mean' : 'candidate #' + info.candidate}, fight ${info.fight} (seed ${info.seed}): ${describe(info.outcome, sides)}.`;
  if (info.matchesRecord === true) text += ' Matches the logged ending.';
  if (info.matchesRecord === false) text += ` Does not match the logged ending (${describe(info.recorded, sides)}).`;
  if (info.mean) text += ' The mean was never fought during the search: this fight is computed now, on that generation\'s seeds.';
  setMessage(text, info.matchesRecord === false ? 'refused' : 'ok');
  $('r-frame').max = Math.max(0, frames.length - 1);
  if (!frames.length) {
    clearReplay(info);
    setArenaState(info.traceNote || 'No rounds were recorded for this fight: only its ending is known.');
    return;
  }
  setArenaState(null);
  showFrame(0);
}

function clearReplay(info = null) {
  state.replay = { frames: [], index: 0, playing: null, info };
  $('r-frame').max = 0; $('r-frame').value = 0;
  $('r-round').textContent = 'round -';
  emptyUnits();
  $('r-events').innerHTML = '';
  const cv = $('r-canvas'); cv.getContext('2d').clearRect(0, 0, cv.width, cv.height);
}

function togglePlay() {
  if (state.replay.playing) return stopPlay();
  if (!state.replay.frames.length) return;
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

// Fighters by species and number on their side ("wolf 2"); the person you drive is "you".
function fighterNames(units, sides) {
  const counts = {}, totals = {};
  for (const u of units) { const k = u.team + ':' + u.species; totals[k] = (totals[k] || 0) + 1; }
  return units.map(u => {
    const k = u.team + ':' + u.species;
    counts[k] = (counts[k] || 0) + 1;
    if (sides.a.you && u.team === 0 && u.species === 'Human' && counts[k] === 1) return { name: 'you', you: true };
    const sp = u.species === 'Human' ? 'person' : u.species.toLowerCase();
    return { name: totals[k] > 1 ? `${sp} ${counts[k]}` : sp };
  });
}

const HEAD = '<colgroup><col class="c-who"><col class="c-state"><col class="c-act"><col class="c-resolve"><col><col><col><col></colgroup>'
  + '<tr><th>Fighter</th><th>State</th><th>Action</th><th>Resolve</th><th>Vitality</th><th>Blood</th><th>Reserve</th><th>Wounds</th></tr>';
function emptyUnits() {
  $('r-units').innerHTML = HEAD + '<tr><td colspan="8" style="color:var(--ink-soft)">Fighters show here once a fight is loaded.</td></tr>';
}

function showFrame(i) {
  const r = state.replay;
  if (!r.frames.length) return;
  r.index = Math.max(0, Math.min(i, r.frames.length - 1));
  const f = r.frames[r.index];
  $('r-frame').value = r.index;
  $('r-round').textContent = `round ${f.round} of ${r.frames.at(-1).round}`;
  drawArena(f, r);
  const names = fighterNames(f.units, r.sides);
  const meter = v => v == null ? '-' : `<span class="meter" title="${pct(v)}"><i style="width:${Math.max(0, Math.min(1, v)) * 100}%"></i></span>`;
  $('r-units').innerHTML = HEAD
    + f.units.map((u, k) => `<tr class="${u.state === 'Down' ? 'down' : ''}${names[k].you ? ' you-row' : ''}"><td class="${u.team ? 'team-b' : 'team-a'}">${names[k].you ? '<span class="you" aria-hidden="true"></span>' : ''}${esc(names[k].name)}</td>`
      + `<td>${esc(u.state)}</td><td>${esc(u.action ?? '')}</td><td class="num">${u.resolve == null ? '-' : (u.resolve >= 0 ? '+' : '') + u.resolve.toFixed(2)}</td>`
      + `<td>${meter(u.vitality)}</td><td>${meter(u.blood)}</td><td>${meter(u.reserve)}</td><td>${meter(u.wounds)}</td></tr>`).join('');
  $('r-events').innerHTML = f.events.length ? f.events.map(e => `<li title="${esc(e)}">${esc(e)}</li>`).join('') : '<li>Nothing happened this round.</li>';
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
    r.frames.slice(0, r.index + 1).forEach((fr, j) => { const v = fr.units[k]; if (!v) return; if (j === 0) ctx.moveTo(px(v), py(v)); else ctx.lineTo(px(v), py(v)); });
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
