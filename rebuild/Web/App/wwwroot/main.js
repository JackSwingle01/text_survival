// The page. It knows the five host calls (client.js) and the plain-data view they return; every action
// it offers is a verb id from that view, posted back as is. It decides nothing about the game.
//
// Information hierarchy (dev/active/ui-design.md): always on are the clock, daylight, feels-like,
// the four vitals and the place; alerts only past a line; everything else is in the drawer, at the
// decision it affects (odds sheets, the trip preview) or one key away (body, pack, journal).
import { createClient } from './client.js';
import { createMap } from './map.js';
import { createGrid } from './combat.js';
import { putSave, getSave, download, upload } from './saves.js';
import { logOptions, withLog } from './playtest.js';
import { icon, img, src, clock, dur, ago, pct, deg, signed, cap, esc, px, tip, veilKey, paceMs, oddsWhy } from './util.js';

const $ = id => document.getElementById(id);
const params = new URLSearchParams(location.search);
// ?log: the playtest log (playtest.js) wraps the client, so every call is recorded; off otherwise.
const logging = logOptions(params);
const build = () => fetch('build.txt', { cache: 'no-store' }).then(r => r.ok ? r.text() : 'unknown').then(s => s.trim());
const { client, log } = logging ? withLog(createClient(), { ...logging, build: build() }) : { client: createClient(), log: null };

const S = {
  view: null,
  drawer: null,          // { kind: 'here'|'fire'|'trip'|'place'|'make'|'body'|'pack'|'log'|'save'|'job'|'forage', ... }
  forage: { focus: 'anything', mins: 30 }, // the forage screen's choices: the focus (anything is General), how long
  busy: false,
  stop: false,
  drawPos: { x: 0, y: 0 },
  walking: false,
  displayMinute: null,   // the clock as shown while time is being played out
  selected: null,
  openFeature: null,       // the location feature whose Interact menu is open
  makeSel: null,         // the entry selected in the make drawer: a recipe, or a repair's recipe:target
  strikes: [],           // this fire's strikes: true caught, false failed
  lastLog: 0,
};
window.ui = { S, ready: null, client, log }; // for the headless checks (tools/shots.mjs)

// ---------------------------------------------------------------- verbs, read off the view

const allVerbs = v => [...v.locationFeatures.flatMap(t => t.menu), ...v.walk, ...v.tile, ...v.packVerbs, ...v.make];
const byId = id => allVerbs(S.view).find(x => x.id === id) ?? S.view.combat?.actions.find(x => x.id === id);
const fireFeature = v => v.locationFeatures.find(t => t.kind === 'fire');
/** The verb that lights a fire here: the tile's "Start a fire", or the cold pit's own "Light the fire". */
const lightVerb = v => v.tile.find(x => x.kind === 'fire') ?? fireFeature(v)?.menu.find(x => x.kind === 'fire');
const walkVerb = (v, dir, careful = false) => v.walk.find(w => w.dir === dir && w.careful === careful);
const DIRS = { north: [0, -1], east: [1, 0], south: [0, 1], west: [-1, 0] };
const dirTo = (from, to) => Object.keys(DIRS).find(d => from.x + DIRS[d][0] === to.x && from.y + DIRS[d][1] === to.y);

const KIND_ICON = {
  wait: 'ui/clock', rest: 'ui/energy', sleep: 'ui/moon', fire: 'ui/fire', fuel: 'ui/fuel', torch: 'ui/torch', charcoal: 'ui/charcoal',
  gather: 'ui/foraging', butcher: 'ui/knife', eat: 'ui/food', drink: 'ui/water', wash: 'ui/water', cook: 'ui/meat', melt: 'ui/water', putdown: 'ui/storage',
  take: 'ui/storage', wear: 'ui/equip', takeoff: 'ui/equip', walk: 'ui/follow', build: 'construction', improve: 'ui/shelter', pack: 'ui/backpack',
  make: 'ui/project', abandon: 'ui/cancel', hunt: 'ui/spear', walkabout: 'ui/continue', puton: 'ui/meat', takeback: 'ui/back', stopwork: 'ui/cancel', repair: 'construction', other: 'ui/inspect',
};
const FIGHT_ICON = {
  Wait: 'ui/clock', Advance: 'ui/follow', Retreat: 'ui/back', Attack: 'ui/spear', Throw: 'ui/throw', 'Throw stone': 'ui/throw', Dodge: 'ui/dodge',
  Block: 'ui/block', Shove: 'ui/shove', Intimidate: 'ui/intimidate', Flee: 'ui/flee', 'Call help': 'ui/call-help', 'Call retreat': 'ui/call-retreat', Move: 'ui/target',
};
const verbIcon = vb => vb.kind.startsWith('fight') ? FIGHT_ICON[vb.label] ?? 'ui/target' : vb.label.startsWith('Chop') ? 'forest' : KIND_ICON[vb.kind] ?? 'ui/inspect';
/** What a verb costs in time, in words. */
const verbTime = vb => vb.trip ? `${vb.trip.totalMinutes} min` : vb.minutes == null ? 'until done' : vb.minutes <= 2 ? 'a moment' : dur(vb.minutes);

// ---------------------------------------------------------------- running a verb: paced jobs

const logKey = l => `${l.minute}|${l.text}`;
const frameP = () => new Promise(r => requestAnimationFrame(r));

/** Plays the clock from one minute to another over ms, a frame at a time (the map's light and the top strip follow it). */
async function playClock(from, to, ms, job) {
  const t0 = performance.now();
  for (;;) {
    const k = ms <= 0 ? 1 : Math.min(1, (performance.now() - t0) / ms);
    S.displayMinute = from + (to - from) * k;
    if (job) placeWalker(job, k);
    renderTop();
    if (k >= 1) break;
    await frameP();
  }
}

/** The player between tiles while a crossing is under way: from its start tile, as far as the minutes say. */
function placeWalker(job) {
  if (job?.kind !== 'walk' || !job.dir || job.finished) return;
  const f = job.totalMinutes ? Math.min(1, job.doneMinutes / job.totalMinutes) : 0, [dx, dy] = DIRS[job.dir];
  S.drawPos = { x: job.fromX + dx * f, y: job.fromY + dy * f };
}

/** Starts a verb and runs it to its end in paced chunks, so the clock is seen to move. */
async function run(id, extra = {}) {
  if (S.busy || !S.view) return;
  const verb = byId(id);
  if (!verb) throw new Error(`No verb ${id} in this view`);
  if (verb.refused) { toast('ui/blocked', cap(verb.refused)); shake(id); return; }
  S.busy = true; S.stop = false;
  if (S.drawer?.kind === 'job') S.drawer = null; // the last job's result gives way to this one
  if (S.drawer?.kind === 'forage') Object.assign(S.drawer, { done: null, startMinute: S.view.minute, logFrom: new Set(S.view.log.map(logKey)) });
  const logFrom = new Set(S.view.log.map(logKey)); // the lines already shown: the rest are this job's
  const startMinute = S.view.minute;
  try {
    let v = await client.act({ verb: id, ...extra });
    apply(v, { keepDrawer: true });
    if (!v.job.finished) {
      const total = v.job.totalMinutes ?? verb.minutes ?? 60;
      const quiet = v.job.kind === 'walk' || v.job.kind === 'fight' || total <= 2;
      if (!quiet && S.drawer?.kind !== 'forage') openDrawer({ kind: 'job', verbId: id, verbKind: verb.kind, label: verb.label, startMinute, logFrom });
      const rateOf = (minutes, kind) => minutes / paceMs(minutes, kind);
      let rate = rateOf(total, v.job.kind), pacing = v.job.label;
      S.walking = v.job.kind === 'walk';
      while (!v.job.finished) {
        // The host may hand over a job the player did not choose (dropped asleep): it is paced as its own,
        // and only the host says when nothing is under way, so a stop it does not end runs on.
        if (v.job.label !== pacing) { rate = rateOf(v.job.totalMinutes ?? 60, v.job.kind); pacing = v.job.label; S.walking = v.job.kind === 'walk'; }
        if (S.stop) { S.stop = false; v = await client.act({ stop: true }); apply(v, { keepDrawer: true }); continue; }
        const before = S.displayMinute ?? v.minute, chunk = Math.max(1, Math.round(rate * 70));
        const t0 = performance.now();
        v = await client.act({ advance: chunk });
        S.view = v; // the job's progress, before the clock catches up
        await playClock(before, v.minute, (v.minute - before) / rate - (performance.now() - t0), v.job);
        apply(v, { keepDrawer: true });
      }
    }
    S.walking = false;
    S.displayMinute = null;
    finishJob(verb, v, startMinute, logFrom);
  } catch (e) {
    console.error(e);
    toast('ui/warning', String(e.message ?? e));
  } finally {
    S.busy = false; S.walking = false; S.displayMinute = null;
    if (S.view) { S.drawPos = { x: S.view.place.x, y: S.view.place.y }; render(); }
  }
}

function finishJob(verb, v, startMinute, logFrom) {
  const job = v.job, fresh = v.log.filter(l => !logFrom.has(logKey(l)));
  if (verb.kind === 'fire') {
    const lit = v.fire?.burning;
    S.strikes.push(!!lit);
    if (S.drawer?.kind !== 'fire') openDrawer({ kind: 'fire' });
    S.drawer.result = lit ? { good: true, text: 'It caught.' } : { good: false, text: fresh.find(l => l.icon === 'ui/fire')?.text ?? cap(job.reason ?? 'It did not catch.') };
  } else if (S.drawer?.kind === 'forage') {
    S.drawer.done = { job, lines: fresh, walked: verb.kind === 'walkabout' };
  } else if (S.drawer?.kind === 'job' && S.drawer.verbId === verb.id) {
    S.drawer.done = { outcome: job.outcome, reason: job.reason, minutes: v.minute - startMinute, lines: fresh };
  } else if (job.outcome === 'interrupted' || job.outcome === 'refused' || job.kind === 'fight' && !v.combat) {
    // Sim says whether it finished or was cut short: an arrival or a meal eaten is not news. A fight's
    // end is: its grid is gone, and the round's reason is how it ended.
    toast(job.kind === 'walk' ? 'ui/warning' : verbIcon(verb), cap(job.reason));
  }
  if (verb.kind === 'make' && S.drawer?.kind === 'job') S.drawer.back = 'make';
  render();
  if (v.receipt) showReceipt(v.receipt);
}

function apply(v, { keepDrawer = false } = {}) {
  S.view = v;
  if (pinned) hideTip();
  if (!S.busy || !S.walking) S.drawPos = { x: v.place.x, y: v.place.y };
  if (!keepDrawer && S.drawer?.kind === 'trip') S.drawer = null;
  render();
}

// ---------------------------------------------------------------- the always-on strip

function renderTop() {
  const v = S.view; if (!v) return;
  const c = v.clock, shownMinute = S.displayMinute ?? v.minute;
  const mod = c.minuteOfDay + (shownMinute - v.minute);
  const [hm, ap] = clock(mod).split(' ');
  const left = Math.max(0, c.daylightLeftMinutes - (shownMinute - v.minute));
  const span = c.darkFromMinute - c.lightFromMinute;
  const f = v.feels;
  const weather = [`Air ${deg(c.airF)}`, `Wind ${Math.round(c.windMph)} mph${c.windDirection !== 'calm' ? ` from the ${c.windDirection}` : ''}`, c.sky,
    `m:Light at ${clock(c.lightFromMinute)} · dark at ${clock(c.darkFromMinute)}`];
  $('top').innerHTML = `
    <div class="t-clock" ${tip(`Day ${dayOf(shownMinute)} · ${c.season}`, weather)}>
      <span class="clock-time">${hm}<span class="clock-ampm">${ap}</span></span></div>
    <div class="t-light ${left && left <= 60 ? 'warn' : ''}" ${tip('Daylight', [left ? `Dark at ${clock(c.darkFromMinute)}` : `Light at ${clock(c.lightFromMinute)}`])}>
      ${img(left ? 'ui/sun' : 'ui/moon')}<span class="lbar"><i style="width:${Math.min(1, left / span) * 100}%"></i></span>
      <span class="num">${left ? `${dur(left)} light` : 'Night'}</span></div>
    <div class="t-feels" ${tip('Feels like', f.terms.map((t, i) => i === 0 ? `${t.name} ${deg(t.deltaF)}` : `${t.deltaF > 0 ? 'g:' : 'b:'}${t.name} ${signed(Math.round(t.deltaF), '°')}`))}>
      ${img('ui/temperature')}<span class="num" style="color:${f.totalF < 20 ? 'var(--frost)' : 'var(--bone)'}">${deg(f.totalF)}</span><span class="muted">feels like</span></div>`;
}

const PRIORITY_CLASS = { none: '', want: '', need: 'low', critical: 'crit' };
const VITAL = { warmth: ['Warmth', 'ui/temperature'], food: ['Food', 'ui/food'], water: ['Water', 'ui/water'], rest: ['Rest', 'ui/energy'] };
function renderStatus() {
  const v = S.view;
  const warming = v.feels.terms.some(t => t.name === 'Fire');
  const color = { warmth: warming ? '#f5811f' : '#8fa6b7', food: '#eadfc8', water: '#b9d3dc', rest: '#eadfc8' };
  const bars = v.vitals.map(x => `<div class="vbar ${PRIORITY_CLASS[x.priority]}" ${tip(VITAL[x.stat][0], [x.detail, ...(x.priority === 'need' || x.priority === 'critical' ? [`b:${cap(x.priority)}`] : [])])}>
      ${img(VITAL[x.stat][1])}<span class="vtrack" style="--c:${color[x.stat]}"><i style="width:${Math.round(x.pct * 100)}%"></i></span></div>`).join('');
  const al = v.alerts, shown = al.slice(0, 3), rest = al.slice(3);
  $('status').innerHTML = `<div class="plate vitals" aria-label="Vitals">${bars}</div>
    <div class="alerts">${shown.map(a => `<button class="alert ${a.kind}" data-sheet="body" ${tip(a.text, a.lines)}>${img(a.icon)}${esc(a.text)}</button>`).join('')}
    ${rest.length ? `<button class="alert more" data-sheet="body" ${tip('More', rest.map(a => a.text))}>+${rest.length} more</button>` : ''}</div>`;
}
$('status').addEventListener('click', e => { if (e.target.closest('[data-sheet]')) openDrawer({ kind: 'body' }); });

// ---------------------------------------------------------------- the dock

const SHEETS = [['body', 'ui/person', 'B', 'Body'], ['pack', 'ui/backpack', 'I', 'Pack'], ['log', 'ui/journal', 'J', 'Journal'], ['save', 'ui/storage', 'S', 'Save']];

function verbButton(vb, { name = vb.label, sub = verbTime(vb), key = '', open = null, small = false } = {}) {
  const refused = vb?.refused;
  return `<button class="verb${small ? ' small' : ''}" ${open ? `data-open="${open}"` : `data-verb="${vb.id}"`} ${refused && !open ? 'aria-disabled="true"' : ''}
      ${refused ? tip(name, [`b:${cap(refused)}`]) : key ? tip(name, [sub, `m:Press ${key}`]) : ''}>
    <img src="${src(verbIcon(vb))}" alt=""><span class="vn">${px(name)}${key ? ` <span class="key">${key}</span>` : ''}</span>
    <span class="vt ${refused ? 'no' : ''}">${esc(refused ? cap(refused) : sub)}</span></button>`;
}

function dockVerbs(v) {
  const out = [];
  const fire = fireFeature(v), light = lightVerb(v);
  if (fire) out.push(verbButton(fire.menu[0] ?? light, { name: 'Tend fire', sub: `${fire.name.replace(/^fire \(|\)$/g, '')}${v.fire?.burning ? ` · ${dur(v.fire.fuelHours * 60)} fuel` : ''}`, key: 'F', open: 'fire' }));
  else if (light) out.push(verbButton(light, { name: 'Start fire', sub: light.odds ? `${pct(light.odds.totalPct)} chance` : verbTime(light), key: 'F', open: 'fire' }));
  for (const t of v.locationFeatures) {
    if (t.kind === 'fire' || t.menu.length === 0) continue;
    if (isForage(v, t)) { out.push(verbButton(t.menu[0], { name: 'Forage', sub: v.forage.quality ? `${v.forage.quality} ground` : 'search the ground', key: 'G', open: 'forage' })); continue; }
    if (t.action) { const a = t.menu.find(x => x.id === t.action); out.push(verbButton(a, { sub: a.refused ? '' : a.label.toLowerCase() === t.name.toLowerCase() ? verbTime(a) : `${t.name} · ${verbTime(a)}`, key: a.kind === 'gather' && a.label === 'Forage' ? 'G' : '' })); }
    else if (t.interact) out.push(verbButton({ ...t.menu[0], label: cap(t.name), refused: null }, { name: cap(t.name), sub: `${t.menu.length} things to do`, open: `feature:${v.locationFeatures.indexOf(t)}` }));
  }
  for (const vb of v.tile) {
    if (vb.kind === 'fire') continue;
    out.push(verbButton(vb, { key: vb.kind === 'sleep' ? 'Z' : vb.kind === 'wait' ? 'W' : '', small: vb.kind === 'wait' || vb.kind === 'rest' }));
  }
  const makeable = v.make.filter(x => x.kind === 'make' && !x.refused).length;
  out.push(verbButton({ id: 'make', kind: 'make', label: 'Make' }, { name: 'Make', sub: makeable ? `${makeable} you can make` : 'nothing yet', key: 'M', open: 'make' }));
  return out.join('');
}

function renderDock() {
  const v = S.view;
  if (v.combat) {
    // What can be done this round, in full; what cannot, small, with its reason on hover.
    const acts = v.combat.actions.filter(a => a.kind === 'fight');
    $('dock').innerHTML = `<div class="group">${acts.filter(a => !a.refused).map(a => verbButton(a, { sub: 'one round' }).replace('class="verb"', 'class="verb fight"')).join('')}</div>
      <div class="group refused">${acts.filter(a => a.refused).map(a => verbButton(a, { small: true, sub: '' })).join('')}</div>`;
    return;
  }
  $('dock').innerHTML = `<div class="verbs">${dockVerbs(v)}</div>
    <button class="where-btn" data-open="here" ${tip('Where you are', ['Press X'])}><b>${px(v.place.name)}</b><small>${esc(v.place.terrain)}${v.fire ? ` · fire ${v.fire.phase.toLowerCase()}` : ''}</small></button>
    <div class="sheets">${SHEETS.map(([id, ic, k, name]) => `<button class="sbtn" data-open="${id}" aria-pressed="${S.drawer?.kind === id}" ${tip(name, [`Press ${k}`])}><img src="${src(ic)}" alt=""><span class="key">${k}</span></button>`).join('')}</div>`;
}

document.addEventListener('click', e => {
  const b = e.target.closest('[data-verb],[data-open],[data-act]');
  if (!b || S.busy && !['stop', 'mark', 'logfile'].includes(b.dataset.act)) return; // while time runs, only Stop (and the playtest log's buttons)
  if (b.dataset.verb) return run(b.dataset.verb, b.dataset.forage ? { minutes: S.forage.mins } : {});
  if (b.dataset.open) {
    const o = b.dataset.open;
    if (o.startsWith('feature:')) { S.openFeature = +o.slice(8); return openDrawer({ kind: 'here' }); }
    if (o.startsWith('recipe:')) { S.makeSel = o.slice(7); return renderDrawer(); }
    if (S.drawer?.kind === o) return closeDrawer();
    return openDrawer({ kind: o });
  }
  const act = b.dataset.act;
  if (act === 'close') closeDrawer();
  else if (act === 'stop') S.stop = true;
  else if (act === 'menu') { S.openFeature = S.openFeature === +b.dataset.i ? null : +b.dataset.i; renderDrawer(); }
  else if (act === 'back') openDrawer({ kind: b.dataset.to });
  else if (act === 'continue') closeVeil();
  else if (act === 'save') doSave();
  else if (act === 'load') doLoad();
  else if (act === 'download') doDownload();
  else if (act === 'upload') doUpload();
  else if (act === 'new') newGame(Math.floor(Math.random() * 1e6));
  else if (act === 'mark') markLog();
  else if (act === 'logfile') download(JSON.stringify(log.toJSON()), log.fileName());
});

// ---------------------------------------------------------------- the drawer

function openDrawer(d) { S.drawer = d; log?.ui({ drawer: d.kind }); render(); }
function closeDrawer() { if ((S.drawer?.kind === 'job' || S.drawer?.kind === 'forage') && S.busy) return; S.drawer = null; S.selected = null; render(); }
function head(ic, title, extra = '') {
  $('sideHead').innerHTML = `${img(ic, 'ico x2')}<h2>${px(title)}</h2>${extra}<button class="xbtn" data-act="close" aria-label="Close">ESC</button>`;
}
const section = (label, body, aside = '') => `<div class="sec"><div class="sec-h"><span class="label">${esc(label)}</span>${aside ? `<span class="aside">${aside}</span>` : ''}</div>${body}</div>`;
const costChip = c => `<span class="cost">${img('ui/clock')}${esc(c)}</span>`;

/** How a piece at a station is going: its forecast finish, and why it is slow. */
const progressLine = p => [p.finishMinute != null ? `done about ${finishClock(p.finishMinute)}` : 'no finish', p.why].filter(Boolean).join(' · ');
const finishClock = m => `${dayOf(m) !== S.view.clock.day ? `day ${dayOf(m)}, ` : ''}${clock(minuteOfDay(m))}`;
const stationWords = s => s === 'ready' ? 'Ready at the fire' : 'Working at the fire';

/** A row for a verb in a list: its label, its time or its refusal. */
function menuItem(vb, { name = vb.label, sub = verbTime(vb), pressed = false, open = null } = {}) {
  return `<button class="mi" ${open ? `data-open="${open}"` : `data-verb="${vb.id}"`} ${vb.refused && !open ? 'disabled' : ''} aria-pressed="${pressed}">
    ${img(verbIcon(vb))}<span>${px(name)}${vb.refused ? `<small class="no">${esc(cap(vb.refused))}</small>` : ''}${vb.progress ? `<small>${esc(progressLine(vb.progress))}</small>` : ''}</span><span class="m">${esc(vb.refused ? '' : sub)}</span></button>`;
}

/** The odds sheet: a big total, the bar (base and additions solid, subtractions hatched), each factor. */
function oddsSheet(o, { title = 'Chance' } = {}) {
  const pos = o.terms.filter(t => t.deltaPct > 0), neg = o.terms.filter(t => t.deltaPct < 0);
  const top = o.basePct + pos.reduce((a, t) => a + t.deltaPct, 0), scale = Math.max(1, top);
  let x = 0; const seg = (w, cls) => { const s = `<span class="seg ${cls}" style="left:${x / scale * 100}%;width:${Math.max(0, w) / scale * 100}%"></span>`; x += w; return s; };
  let bar = seg(o.basePct, 'base') + pos.map(t => seg(t.deltaPct, 'plus')).join('');
  const negSum = -neg.reduce((a, t) => a + t.deltaPct, 0); x = top - negSum;
  bar += neg.map(t => seg(-t.deltaPct, 'minus')).join('');
  const row = (cls, name, v, why = '') => `<div class="factor ${cls}"><span></span><span>${esc(name)}${why ? `<span class="why">${esc(why)}</span>` : ''}</span><span class="v">${v}</span></div>`;
  return `<div class="odds">
    <div class="odds-top"><span class="label">${esc(title)}</span><span class="odds-total">${Math.round(o.totalPct * 100)}<small>%</small></span></div>
    <div class="oddsbar">${bar}</div>
    <div class="factors">${row('base', o.base, `${Math.round(o.basePct * 100)}%`)}
      ${o.terms.map(t => row(t.deltaPct >= 0 || t.bonus ? 'plus' : 'minus', t.factor, signed(+(t.deltaPct * 100).toFixed(Math.abs(t.deltaPct) < 0.1 ? 1 : 0), '%'), t.why ?? (t.op === 'scale' ? `×${t.value.toFixed(2)}` : ''))).join('')}
      ${o.capped ? row('', 'Capped', `${Math.round(o.totalPct * 100)}%`) : ''}</div></div>`;
}

function renderDrawer() {
  const d = S.drawer, v = S.view;
  $('drawer').hidden = !d || !!v.combat;
  if (!d || v.combat) return;
  ({ here: renderHere, fire: renderFire, trip: renderTrip, place: renderPlace, make: renderMake, body: renderBody, pack: renderPack, log: renderLog, save: renderSave, job: renderJob, forage: renderForage }[d.kind])(d, v);
}

function fireBlock(v) {
  if (!v.fire) return '';
  const hrs = v.fire.fuelHours;
  return `<div class="firestate"><span class="pit">${img(v.fire.burning ? 'fire' : 'embers', '')}</span><span>
    <span class="phase">${px(v.fire.phase)}</span> <span class="muted">${v.fire.burning ? `${Math.round(v.fire.temperatureF)}°F` : ''}</span>
    <span class="fuelbar" ${tip('Fuel', [`${dur(hrs * 60)} of burning on the fire`])}><span class="brn" style="width:${Math.min(1, hrs / 8) * 100}%"></span></span>
    <span class="fuelleg"><span>${hrs > 0 ? `${dur(hrs * 60)} of fuel` : 'No fuel on it'}</span>${v.fire.charcoalKg > 0 ? `<span>${v.fire.charcoalKg} kg charcoal</span>` : ''}</span></span></div>`;
}

function renderHere(d, v) {
  const p = v.place;
  head(p.camp ? 'shelter' : 'ui/location', p.name, p.name !== p.terrain ? `<span class="terrain-chip">${esc(p.terrain)}</span>` : '');
  const features = v.locationFeatures.map((t, i) => {
    const conds = t.conditions.map(c => `<span class="cond">${esc(c.name)}</span>`).join('');
    const action = t.action ? t.menu.find(x => x.id === t.action) : null;
    const open = S.openFeature === i;
    if (isForage(v, t)) return menuItem(t.menu[0], { name: 'Forage', sub: v.forage.quality ?? '', open: 'forage' });
    if (action && !conds && action.label.toLowerCase() === t.name.toLowerCase()) return menuItem(action);
    return `<div class="feature"><div class="feature-h"><b>${px(cap(t.name))}</b>${conds}
        ${action ? '' : t.interact ? `<button class="xbtn" data-act="menu" data-i="${i}">${open ? 'CLOSE' : 'INTERACT'}</button>` : t.menu.length === 0 ? '<span class="muted">nothing to do</span>' : ''}</div>
      ${action ? `<div class="menu">${menuItem(action)}</div>` : ''}
      ${open ? `<div class="menu">${t.menu.map(x => menuItem(x)).join('')}</div>` : ''}</div>`;
  }).join('');
  $('sideBody').innerHTML = `
    ${features ? section('Here', `<div class="menu">${features}</div>`) : ''}
    ${section('Stay', `<div class="menu">${v.tile.map(x => menuItem(x)).join('')}</div>`)}
    ${v.fire ? section('Fire', fireBlock(v)) : ''}
    ${p.story ? `<p class="story">${esc(p.story)}</p>` : ''}`;
}

function renderFire(d, v) {
  const fire = fireFeature(v), light = lightVerb(v);
  head('fire', fire && v.fire?.burning ? 'The fire' : 'Start a fire', costChip('1 min'));
  const hist = S.strikes.length ? `<div class="hist" ${tip('Strikes', [`${S.strikes.filter(Boolean).length} of ${S.strikes.length} caught`])}>${S.strikes.map(s => `<span class="${s ? 's' : 'f'}"></span>`).join('')}</div>` : '';
  const result = d.result ? `<div class="result-line ${d.result.good ? 'good' : 'bad'} ${d.result.good ? 'flash' : 'shake'}">${esc(d.result.text)}</div>` : '';
  const tend = fire ? fire.menu.filter(x => x.kind !== 'fire') : [];
  const striking = light && !v.fire?.burning;
  $('sideBody').innerHTML = `
    ${striking ? `<div class="sec"><div class="row">${light.refused ? `<p class="note">${esc(cap(light.refused))}</p>` : `<button class="act" data-verb="${light.id}">Strike <span class="num">(1 min)</span> <span class="key">F</span></button>`}${hist}</div>${result}</div>` : result}
    ${tend.length ? section('Tend', `<div class="menu">${tend.map(x => menuItem(x)).join('')}</div>`) : ''}
    ${v.fire ? section('The pit', fireBlock(v)) : ''}
    ${striking ? `<div class="sec">${light.odds ? oddsSheet(light.odds, { title: 'Chance it catches' }) : `<p class="note">${esc(cap(light.refused ?? 'No fire-starting tool in the pack.'))}</p>`}
      ${light.uses ? `<p class="note">Each strike uses ${esc(light.uses.slice(0, -1).join(' and '))}, with the ${esc(light.uses.at(-1))}.</p>` : ''}</div>` : ''}`;
}

function openTrip(dir) {
  const v = S.view, w = walkVerb(v, dir);
  if (!w) return;
  S.selected = { x: v.place.x + DIRS[dir][0], y: v.place.y + DIRS[dir][1] };
  openDrawer({ kind: 'trip', dir });
}

function renderTrip(d, v) {
  const quick = walkVerb(v, d.dir), careful = walkVerb(v, d.dir, true);
  const t = quick.trip;
  head('ui/follow', t ? cap(t.to) : `Walk ${d.dir}`, t ? costChip(`${t.totalMinutes} min`) : '');
  if (!t) { $('sideBody').innerHTML = `<p class="note">${esc(cap(quick.refused ?? 'You cannot go that way.'))}</p>`; return; }
  const left = v.clock.daylightLeftMinutes;
  const camp = v.map.places.find(p => p.x === v.map.campX && p.y === v.map.campY);
  // A chance of a fall that shows as 0% is no hazard worth a line.
  const risk = trip => trip?.risk && Math.round(trip.risk.totalPct * 100) > 0 ? trip.risk : null;
  const rows = (trip) => trip.terms.map((x, i) => `<div class="kv ${i === 0 ? '' : x.minutes > 0 ? 'minus' : 'plus'}"><span></span><span>${esc(x.factor)}</span><span class="v">${i === 0 ? `${x.minutes} min` : signed(x.minutes, ' min')}</span></div>`).join('');
  $('sideBody').innerHTML = `
    <div class="row">${careful ? `<button class="act quiet" data-verb="${careful.id}">Careful <span class="num">(${careful.trip?.totalMinutes ?? '?'} min)</span></button>` : ''}
      <button class="act" data-verb="${quick.id}">Go <span class="num">(${t.totalMinutes} min)</span> <span class="key">Enter</span></button></div>
    ${risk(t) ? `<p class="note odds-line">Fall risk <b class="minus">${pct(t.risk.totalPct)}</b> · ${esc(oddsWhy(t.risk).join(', '))}${careful ? ` · careful ${risk(careful.trip) ? pct(careful.trip.risk.totalPct) : 'is safer'}` : ''}</p>`
      : t.hazardous ? '<p class="note odds-line">Hazardous ground · careful is slower, and safer</p>' : ''}
    <div class="preview"><div class="head"><span class="label">${esc(cap(d.dir))} · ${t.toKnown ? 'as you know it' : 'unexplored'}</span><span class="big">${t.totalMinutes}<small class="muted"> min</small></span></div>
      <div class="factors">${rows(t)}</div></div>
    ${section('Daylight', `<div class="kv ${left && left < t.totalMinutes * 2 + 30 ? 'minus' : ''}">${img(left ? 'ui/sun' : 'ui/moon')}<span>${left ? `${dur(left)} of light left` : 'It is night'}</span><span class="v">${left && left > t.totalMinutes * 2 + 30 ? 'back before dark' : left ? 'tight' : ''}</span></div>`)}
    ${!v.place.camp && camp?.fire ? section('Your camp fire', `<div class="kv">${img(camp.fire === 'Embers' || camp.fire === 'Cold' ? 'embers' : 'fire')}<span>${esc(camp.fire)}, as you last saw it</span><span class="v">${ago(v.minute - camp.seenMinute)}</span></div>
      ${camp.fireHoursLeft != null ? `<p class="note">About ${dur(camp.fireHoursLeft * 60)} of fuel left, by your reckoning.</p>` : ''}`) : ''}`;
}

function renderPlace(d, v) {
  const { x, y } = d, m = v.map, code = m.known[y * m.width + x];
  const named = m.named.find(n => n.x === x && n.y === y), seen = map.inSight(x, y);
  const terr = code === '.' ? 'Unexplored' : cap(map.terrainName(code) ?? '');
  head('ui/location', named?.name ?? terr, named ? `<span class="terrain-chip">${esc(terr)}</span>` : '');
  const p = m.places.find(q => q.x === x && q.y === y), sights = m.sightings.filter(s => s.x === x && s.y === y);
  const lines = [];
  if (p?.fire) lines.push([p.fire === 'Embers' || p.fire === 'Cold' ? 'embers' : 'fire', `Fire: ${p.fire}`, ago(v.minute - p.seenMinute)]);
  if (p?.station) lines.push(['ui/meat', stationWords(p.station), '']);
  if (p?.shelter) lines.push(['shelter', p.shelter, '']);
  if (p?.carcass) lines.push(['carcass', 'A carcass', '']);
  if (p?.pile) lines.push(['cache', 'A pile', '']);
  if (p?.den) lines.push(['danger', 'A den', '']);
  if (p?.hurt) lines.push(['ui/blood', 'You were hurt here', '']);
  for (const s of sights) lines.push([s.species, `${s.count > 1 ? `${s.count} × ` : ''}${s.species.split('/').pop()}`, ago(v.minute - s.minute)]);
  if (m.surface.find(s => s.i === y * m.width + x)?.troddenLevel) lines.push(['ui/tracks', 'Trodden ground', '']);
  for (const t of m.tracks.filter(t => t.x === x && t.y === y)) lines.push([`tracks/${t.maker === 'human' ? 'boot' : t.maker}`, `${cap(t.maker)} prints, ${t.reading}`, ago(t.ageMinutes)]);
  $('sideBody').innerHTML = `
    ${code === '.' ? '<p class="note">You have never seen this ground.</p>' : seen ? '' : '<span class="stale">As you last saw it</span>'}
    ${lines.length ? section('Known here', `<div class="place-feats">${lines.map(([ic, t, a]) => `<div class="pf">${img(ic)}<span>${esc(t)}</span><span class="v">${a}</span></div>`).join('')}</div>`) : code !== '.' ? '<p class="note">Nothing you noted.</p>' : ''}
    <p class="note">${Math.abs(x - v.place.x) + Math.abs(y - v.place.y)} tiles away. Walk there a tile at a time.</p>`;
}

function renderMake(d, v) {
  head('ui/project', 'Make', '');
  const makes = v.make.filter(x => x.kind === 'make'), abandons = v.make.filter(x => x.kind === 'abandon'), builds = v.make.filter(x => x.kind === 'build');
  // A repair is one entry per piece it works on: the recipe and the target name it.
  const key = x => x.make.target ? `${x.make.recipe}:${x.make.target}` : x.make.recipe;
  if (!S.makeSel || !makes.some(x => key(x) === S.makeSel)) S.makeSel = key(makes.find(x => !x.refused) ?? makes[0] ?? { make: {} });
  const sel = makes.find(x => key(x) === S.makeSel);
  let detail = '', action = '';
  if (sel) {
    const mk = sel.make;
    // The odds sheet of a recipe: everything to plan, less each outcome's share; what is left is the plain result.
    const sheet = { base: 'Goes to plan', basePct: 1, totalPct: mk.plainPct, rawPct: mk.plainPct, capped: false,
      terms: mk.outcomes.map(o => ({ factor: o.text, op: 'add', value: -o.odds.totalPct, deltaPct: -o.odds.totalPct, bonus: !o.failureLike,
        why: o.odds.terms.map(t => `${t.factor} ×${t.value.toFixed(2)}`).join(', ') })) };
    detail = `
      ${section(mk.name, oddsSheet(sheet, { title: `Comes out as planned: ${mk.result.toLowerCase()}` }), mk.progressPct != null ? `${pct(mk.progressPct)} done` : '')}
      ${section('Takes', `<div class="factors">
        ${mk.materials.map(m => `<div class="kv">${img('ui/materials')}<span>${esc(m)}</span><span class="v"></span></div>`).join('')}
        ${mk.tools.map(t => `<div class="kv">${img('ui/gear')}<span>A tool for ${esc(t)}</span><span class="v"></span></div>`).join('')}
        ${mk.requires.map(t => `<div class="kv">${img('ui/warning')}<span>Needs ${esc(t)}</span><span class="v"></span></div>`).join('')}
        <div class="kv">${img('ui/clock')}<span>Work</span><span class="v">${dur(mk.workMinutes)}</span></div></div>`)}`;
    action = `<div class="row">${sel.refused ? `<p class="note">${esc(cap(sel.refused))}</p>` : `<button class="act" data-verb="${sel.id}">${px(sel.label)} <span class="num">(${dur(mk.workMinutes)})</span></button>`}</div>`;
  }
  let lastCat = '';
  const list = makes.map(x => {
    const cat = x.make.category !== lastCat ? `<div class="cat">${esc(x.make.category)}</div>` : ''; lastCat = x.make.category;
    return cat + menuItem(x, { name: x.make.name, sub: x.refused ? '' : `${dur(x.make.workMinutes)} · ${pct(x.make.plainPct)}`, pressed: key(x) === S.makeSel, open: `recipe:${key(x)}` })
      ;
  }).join('');
  $('sideBody').innerHTML = `${action}${detail}
    ${abandons.length ? section('Unfinished', `<div class="menu">${abandons.map(x => menuItem(x)).join('')}</div>`) : ''}
    ${section('Recipes', `<div class="menu">${list}</div>`)}
    ${builds.length ? section('Build here', `<div class="menu">${builds.map(x => menuItem(x)).join('')}</div>`) : ''}`;
}

function renderBody(d, v) {
  const b = v.body;
  head('ui/person', 'Body', '');
  const g = (name, ic, val, color, text, priority) => {
    const on = Math.round(val * 10);
    return `<div class="gauge ${PRIORITY_CLASS[priority] ?? ''}">${img(ic)}<span class="name">${name}</span><span class="val">${text}</span>
      <span class="ticks" style="--c:${color}">${Array.from({ length: 10 }, (_, i) => `<span class="${i < on ? 'on' : ''}"></span>`).join('')}</span></div>`;
  };
  const vit = Object.fromEntries(v.vitals.map(x => [x.stat, x]));
  const f = v.feels;
  $('sideBody').innerHTML = `
    <div class="sec">
      ${g('Warmth', 'ui/temperature', vit.warmth.pct, '#8fa6b7', `${b.coreTempF.toFixed(1)}°F`, vit.warmth.priority)}
      ${g('Food', 'ui/food', vit.food.pct, '#eadfc8', `${Math.round(b.caloriesKcal)} kcal`, vit.food.priority)}
      ${g('Water', 'ui/water', vit.water.pct, '#b9d3dc', `${(b.hydrationMl / 1000).toFixed(1)} L`, vit.water.priority)}
      ${g('Rest', 'ui/energy', vit.rest.pct, '#eadfc8', pct(b.energyPct), vit.rest.priority)}
      ${b.bloodPct < 0.995 ? g('Blood', 'ui/vitality', b.bloodPct, '#d97a6a', pct(b.bloodPct), b.bloodPct < 0.7 ? 'need' : '') : ''}
    </div>
    ${v.alerts.length ? section('Now', `<div class="factors">${v.alerts.map(a => `<div class="kv minus">${img(a.icon)}<span>${esc(a.text)}<span class="why"> ${esc(a.lines.join(' · '))}</span></span><span class="v"></span></div>`).join('')}</div>`) : ''}
    ${b.effects.length ? section('Effects', `<div class="effects">${b.effects.map(e => `<span class="eff">${esc(e.name)} <span class="muted num">${pct(e.severity)}</span></span>`).join('')}</div>`) : ''}
    ${b.impaired.length ? section('Impaired', `<div class="caps">${b.impaired.map(c => `<div class="cap"><span>${esc(c.name)}</span><span class="minibar" style="--c:${c.pct < 0.7 ? 'var(--minus)' : 'var(--bone-2)'}"><i style="width:${c.pct * 100}%"></i></span><span class="num">${pct(c.pct)}</span></div>`).join('')}</div>`) : ''}
    ${section('Feels like', `<div class="feels"><span class="big">${deg(f.totalF)}</span><span class="muted">air ${deg(f.terms[0].deltaF)}</span></div>
      <div class="factors">${f.terms.slice(1).map(t => `<div class="kv ${t.deltaF > 0 ? 'plus' : 'minus'}">${img(t.name === 'Fire' ? 'ui/fire' : t.name === 'Torch' ? 'ui/torch' : 'ui/wind')}<span>${esc(t.name)}</span><span class="v">${signed(Math.round(t.deltaF), '°')}</span></div>`).join('')}</div>`)}
    ${section('Wearing', v.pack.worn.map(gr => `<div class="slot"><span class="where">${esc(gr.slot ?? '')}</span><span class="nm">${esc(gr.name)}</span><span class="minibar" style="--c:${gr.conditionPct < 0.5 ? 'var(--minus)' : 'var(--lichen)'}"><i style="width:${gr.conditionPct * 100}%"></i></span></div>`).join(''), `${b.clothingClo} clo`)}
    ${b.skills.length ? section('Skills', b.skills.map(s => `<div class="kv">${img('ui/confirm')}<span>${esc(s.name)}</span><span class="v">${s.level}</span></div>`).join('')) : ''}`;
}

function renderPack(d, v) {
  const p = v.pack;
  head('ui/backpack', 'Pack', `<span class="cost num">${p.carriedKg.toFixed(1)} / ${p.capacityKg} kg</span>`);
  $('sideBody').innerHTML = `
    ${v.packVerbs.length ? section('Use', `<div class="menu">${v.packVerbs.map(x => menuItem(x)).join('')}</div>`) : ''}
    <div class="capacity"><div class="bar"><i style="width:${Math.min(1, p.carriedKg / p.capacityKg) * 100}%"></i></div></div>
    ${section('Carried', p.stacks.length ? `<div class="bag">${p.stacks.map(s => `<div class="it">${img(stackIcon(s.resource))}<span>${esc(s.name)}</span><span class="n"></span><span class="kg">${esc(s.amount)}</span></div>`).join('')}</div>` : '<p class="note">Nothing.</p>')}
    ${section('Tools and gear', p.gear.length ? `<div class="bag">${p.gear.map(gr => `<div class="it">${img('ui/gear')}<span>${esc(gr.name)}</span><span class="n">${pct(gr.conditionPct)}</span><span class="minibar" style="--c:${gr.conditionPct < 0.3 ? 'var(--minus)' : 'var(--lichen)'}"><i style="width:${gr.conditionPct * 100}%"></i></span></div>`).join('')}</div>` : '<p class="note">None.</p>')}
    ${p.projects.length ? section('Unfinished', p.projects.map(pr => `<div class="it">${img('ui/project')}<span>${esc(pr.name)}</span><span class="n">${pct(pr.progressPct)}</span><span></span></div>`).join('')) : ''}`;
}
const stackIcon = r => /Tinder|Bark|Usnea|Amadou|Chaga/.test(r) ? 'ui/tinder' : /Stick|Branch|Firewood|Peat/.test(r) ? 'ui/sticks' : /Meat|Fish|Berr|Nut|Root/.test(r) ? 'ui/food'
  : r === 'Water' ? 'ui/water' : /Hide/.test(r) ? 'ui/hide' : /Bone/.test(r) ? 'ui/bone' : /Charcoal/.test(r) ? 'ui/charcoal' : 'ui/materials';

function renderLog(d, v) {
  head('ui/journal', 'Journal', '');
  let day = null;
  const rows = [...v.log].reverse().map(l => {
    const dd = dayOf(l.minute), hdr = dd !== day ? `<div class="day">Day ${dd}</div>` : ''; day = dd;
    return `${hdr}<div class="e ${l.kind === 'bad' ? 'bad' : l.kind === 'rare' ? 'rare' : ''}"><span class="t">${clock(minuteOfDay(l.minute))}</span>${img(l.icon)}<span>${esc(l.text)}</span></div>`;
  }).join('');
  $('sideBody').innerHTML = `<div class="log">${rows || '<p class="note">Nothing yet.</p>'}</div>`;
}
// The view gives the clock for now; any other minute is placed by its offset from now.
const minuteOfDay = m => S.view.clock.minuteOfDay + (m - S.view.minute);
const dayOf = m => S.view.clock.day + Math.floor((S.view.clock.minuteOfDay + (m - S.view.minute)) / 1440);

async function renderSave(d) {
  head('ui/storage', 'Save', '');
  const slot = d.slot === undefined ? (d.slot = null, getSave().then(s => { d.slot = s ?? false; if (S.drawer === d) renderDrawer(); }).catch(() => { d.slot = false; }), null) : d.slot;
  $('sideBody').innerHTML = `
    ${section('In this browser', `${slot ? `<div class="slot-meta">Day ${slot.meta.day}, ${esc(slot.meta.time)} · ${esc(slot.meta.place)}<br><span class="muted">saved ${new Date(slot.savedAt).toLocaleString()}</span></div>` : `<p class="note">${slot === null ? 'Looking…' : 'No save yet.'}</p>`}
      <div class="row"><button class="act" data-act="save">Save here</button><button class="act quiet" data-act="load" ${slot ? '' : 'disabled'}>Load</button></div>`)}
    ${section('As a file', `<div class="row"><button class="act quiet" data-act="download">Download</button><button class="act quiet" data-act="upload">Upload</button></div>`)}
    ${section('Start again', `<div class="row"><button class="act quiet" data-act="new">New game</button></div>`)}
    ${d.note ? `<p class="note" id="saveNote">${esc(d.note)}</p>` : ''}`;
}

/** The forage ground's feature: the one whose menu holds the forage screen's Keep Walking. */
const isForage = (v, t) => v.forage && t.menu.some(x => x.id === v.forage.walkVerb);

// The forage screen (old ForageOverlay, in the prototype's style): the ground's word and how hard it has been
// searched, what you notice, what to look for and how long, then the finds as they turn up.
function renderForage(d, v) {
  const f = v.forage, running = S.busy, job = running ? v.job : d.done?.job;
  if (!f) { closeDrawer(); return; }
  const focus = forageFocus(f);
  S.forage.focus = focus.focus;
  head('ui/foraging', `Forage · ${v.place.name}`, costChip(dur(S.forage.mins)));
  const showFinds = job && job.kind === 'gather';
  const done = showFinds ? Math.min(1, job.doneMinutes / (job.totalMinutes || 1)) : 0;
  const finds = showFinds ? job.finds ?? [] : [];
  const lines = d.logFrom ? v.log.filter(l => !d.logFrom.has(logKey(l))) : [];
  const rare = lines.filter(l => l.kind === 'rare'), told = !running && d.done && !d.done.walked ? lines.filter(l => l.kind !== 'rare' && l.icon !== 'ui/foraging' || (l.icon === 'ui/foraging' && finds.length === 0)) : [];
  const mood = job?.mood;
  const clues = f.clues.map(c => c.focus
    ? `<button class="clue" data-focusname="${esc(c.focus)}" ${running ? 'disabled' : ''} ${tip(c.text, [`Look for ${c.focus}`])}>${img('ui/inspect')}<span>${esc(c.text)}</span></button>`
    : `<div class="clue still">${img(c.kind === 'scavenge' ? 'carcass' : c.kind === 'game' ? 'ui/tracks' : 'ui/inspect')}<span>${esc(c.text)}</span></div>`).join('');
  $('sideBody').innerHTML = `
    <div class="row">${running ? `<button class="act quiet" data-act="stop">Stop <span class="key">Esc</span></button>`
      : `<button class="act" data-verb="${focus.verb}" data-forage="1">Search <span class="num">(${dur(S.forage.mins)})</span> <span class="key">G</span></button><button class="act quiet" data-verb="${f.walkVerb}" ${tip('Keep walking', ['Spend 5 minutes walking to find new clues'])}>Keep walking <span class="num">(5 min)</span> <span class="key">K</span></button>`}</div>
    ${oddsWhy(f.yield, { base: false }).length ? `<p class="note odds-line">Search speed <b class="${f.yield.totalPct < 1 ? 'minus' : 'plus'}">×${f.yield.totalPct.toFixed(2)}</b> · ${esc(oddsWhy(f.yield, { base: false }).join(' · '))}</p>` : ''}
    <div class="sec"><div class="sec-h"><span class="label">The ground</span><span class="aside">${esc(f.effort)}</span></div>
      <p class="ground">${f.quality ? `Resources look <b>${esc(f.quality)}</b>.` : 'You haven\'t foraged here yet.'}</p></div>
    ${clues ? section('You notice', `<div class="clues">${clues}</div>`) : ''}
    <div class="sec"><span class="label">Look for</span><div class="chips">${f.foci.map(x => `<button class="chip" data-focus="${x.focus}" aria-pressed="${x.focus === focus.focus}" ${running ? 'disabled' : ''} ${tip(cap(x.focus), [x.hint])}><img class="ico" src="${src(FOCUS_ICON[x.focus] ?? 'ui/foraging')}" alt="">${esc(x.focus === 'anything' ? 'General' : cap(x.focus))}</button>`).join('')}</div>
      <p class="note">${esc(focus.hint)}</p></div>
    <div class="sec"><span class="label">How long</span><div class="chips">${(byId(focus.verb)?.durations ?? []).map(m => `<button class="chip" data-mins="${m}" aria-pressed="${S.forage.mins === m}" ${running ? 'disabled' : ''}>${dur(m)}</button>`).join('')}</div></div>
    <div class="run">
      <div class="runbar ${!running && d.done ? 'ok' : ''}"><span class="fill" style="width:${done * 100}%"></span><span class="ticks10"></span></div>
      <div class="runmeta">${mood ? `<span class="mood ${job.luck === 'lean' ? 'thin' : 'good'}">${esc(mood)}</span>` : running ? '<span class="mood wait">Searching</span>' : job?.outcome === 'stopped' ? '<span class="mood wait">Stopped</span>'
        : job && (job.outcome === 'interrupted' || job.outcome === 'refused') && job.reason ? `<span class="mood thin">${esc(cap(job.reason))}</span>` : '<span></span>'}<span class="num">${showFinds ? `${(job.foundKg ?? 0).toFixed(2)} kg` : ''}</span></div>
    </div>
    <div class="found">${finds.length || rare.length ? '' : `<span class="empty">${told.length ? esc(told[told.length - 1].text) : 'Finds appear here as you search.'}</span>`}
      ${rare.map(l => `<div class="item rare">${img(l.icon)}<span>${esc(l.text)}</span></div>`).join('')}
      ${finds.map(x => `<div class="item ${x.category}">${img(stackIcon(x.resource))}${esc(cap(x.name))}<span class="n">×${x.count}</span></div>`).join('')}</div>
`;
}
/** The chosen focus, or General where the ground does not offer it. */
const forageFocus = f => f.foci.find(x => x.focus === S.forage.focus) ?? f.foci[f.foci.length - 1];
const FOCUS_ICON = { fuel: 'ui/sticks', food: 'ui/food', medicine: 'ui/medicine', materials: 'ui/materials', anything: 'ui/foraging' };
document.addEventListener('click', e => {
  const b = e.target.closest('[data-focus],[data-mins],[data-focusname]');
  if (!b || S.busy || S.drawer?.kind !== 'forage') return;
  if (b.dataset.focus) S.forage.focus = b.dataset.focus;
  else if (b.dataset.mins) S.forage.mins = +b.dataset.mins;
  else S.forage.focus = b.dataset.focusname;
  renderDrawer();
});

function renderJob(d, v) {
  const job = v.job;
  const done = job?.doneMinutes ?? 0, total = job?.totalMinutes;
  const shown = S.displayMinute != null ? Math.round(S.displayMinute - d.startMinute) : done;
  head(verbIcon({ kind: d.verbKind, label: d.label }), d.label, total ? costChip(dur(total)) : '');
  const f = d.done ? 1 : total ? Math.min(1, Math.max(0, shown) / total) : 0;
  const lines = v.log.filter(l => !d.logFrom.has(logKey(l)));
  $('sideBody').innerHTML = `
    <div class="row">${d.done ? `${d.back ? `<button class="act quiet" data-act="back" data-to="${d.back}">Back to making</button>` : ''}<button class="act" data-act="close">Done <span class="key">Esc</span></button>` : `<button class="act quiet" data-act="stop">Stop</button>`}</div>
    <div class="run"><div class="runbar ${d.done ? (d.done.outcome === 'interrupted' || d.done.outcome === 'refused' ? 'fail' : 'ok') : ''}"><span class="fill" style="width:${f * 100}%"></span><span class="ticks10"></span></div>
      <div class="runmeta"><span class="num">${dur(Math.max(0, d.done ? d.done.minutes : shown))}${total && !d.done ? ` of ${dur(total)}` : ''}</span><span>${d.done ? esc(cap(d.done.reason ?? 'done')) : 'under way'}</span></div></div>
    ${lines.length ? section(d.done ? 'What came of it' : 'So far', `<div class="factors">${lines.map(l => `<div class="kv ${l.kind === 'bad' ? 'minus' : l.kind === 'good' || l.kind === 'rare' ? 'plus' : ''}">${img(l.icon)}<span>${esc(l.text)}</span><span class="v"></span></div>`).join('')}</div>`) : ''}`;
}

// ---------------------------------------------------------------- the fight

function renderCombat() {
  const c = S.view.combat;
  $('world').hidden = !!c; $('grid').hidden = !c; $('combatside').hidden = !c;
  if (!c) return;
  const units = c.units.map(u => `<div class="unit ${u.inField ? '' : 'out'} ${u.index === c.target ? 'target' : ''}"><img src="${src(u.isPlayer ? 'player' : u.species)}" alt="">
    <span><b>${esc(u.name)}</b><small>${esc(u.condition)}${u.isPlayer ? '' : ` · ${esc(u.state)}`}${u.weapon ? ` · ${esc(u.weapon)}` : ''}${u.inField ? '' : ' · out of the fight'}</small></span>
    <span class="c">${u.team === 0 ? '' : u.index === c.target ? 'target' : ''}</span></div>`).join('');
  const recent = c.lines.slice(-5).reverse();
  $('combatside').innerHTML = `<div class="side-head">${img('ui/target', 'ico x2')}<h2>Fight</h2><span class="cost num">round ${c.round}</span></div>
    <div class="side-body"><div class="sec"><div class="sec-h"><span class="label">Range</span><span class="zone">${esc(c.zone)} · ${c.targetM} m</span></div>
      <p class="note">Click the ground to move, up to ${c.moveM} m a round.</p></div>
      ${section('On the ground', `<div class="place-feats">${units}</div>`)}
      ${recent.length ? section('Just now', `<div class="factors">${recent.map(l => `<div class="kv ${l.kind === 'bad' ? 'minus' : ''}">${img(l.icon)}<span>${esc(l.text)}</span><span class="v"></span></div>`).join('')}</div>`) : ''}</div>`;
}

// ---------------------------------------------------------------- receipt, end of the run, toasts

function showReceipt(r) {
  const lines = r.lines.map(l => `<div class="line ${l.kind === 'bad' ? 'bad' : l.kind === 'rare' ? 'rare' : ''}"><span class="t">${clock(minuteOfDay(l.minute))}</span><img class="who" src="${src(l.icon)}" alt=""><span>${esc(l.text)}</span><span class="amt"></span></div>`).join('');
  $('veil').innerHTML = `<div class="plate receipt" role="dialog" aria-label="${esc(r.title)}"><header><h3>${px(r.title)}</h3>
    <div class="span">${img('ui/clock')}${r.sinceMinute != null ? `${clock(minuteOfDay(r.sinceMinute))} to ${clock(minuteOfDay(r.minute))}` : clock(minuteOfDay(r.minute))}${r.full ? '' : ' · what you can see for yourself'}</div></header>
    <div class="lines">${lines || '<p class="note">Nothing changed.</p>'}</div>
    <footer><button class="act" data-act="continue">Continue <span class="key">Space</span></button></footer></div>`;
  $('veil').hidden = false; $('veil').dataset.kind = 'receipt';
}
function closeVeil() { if ($('veil').dataset.kind === 'end') return; $('veil').hidden = true; $('veil').dataset.kind = ''; }

function renderEnd() {
  const v = S.view;
  if (v.outcome === 'surviving') { if ($('veil').dataset.kind === 'end') { $('veil').hidden = true; $('veil').dataset.kind = ''; } return; }
  const died = v.outcome === 'died';
  const days = v.clock.day;
  $('veil').innerHTML = `<div class="plate end ${v.outcome}" role="dialog"><header>${img(died ? 'body' : 'overlook', 'ico x2')}<h3>${died ? 'You died' : 'You crossed the pass'}</h3>
    <div class="span muted">Day ${days} · ${clock(v.clock.minuteOfDay)} · ${esc(v.clock.season)}</div></header>
    <div class="lines">${died ? `<div class="line bad"><span class="t"></span>${img('ui/blood', 'who')}<span>${esc(cap(v.deathCause ?? 'Your body gave out'))}</span><span></span></div>` : `<div class="line rare"><span class="t"></span>${img('ui/sun', 'who')}<span>Over the mountains, with ${days} ${days === 1 ? "day" : "days"} behind you. The run is won.</span><span></span></div>`}
      <div class="line"><span class="t"></span>${img('ui/location', 'who')}<span>${esc(v.place.name)}</span><span></span></div></div>
    <footer><button class="act" data-act="new">New game</button><button class="act quiet" data-act="load">Load your save</button></footer></div>`;
  $('veil').hidden = false; $('veil').dataset.kind = 'end';
}

let toastT;
function toast(ic, text) { const t = $('toast'); t.innerHTML = `${img(ic)}${esc(text)}`; t.hidden = false; clearTimeout(toastT); toastT = setTimeout(() => { t.hidden = true; }, 2800); }
function shake(id) { const b = document.querySelector(`[data-verb="${id}"]`); if (b) { b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake'); } }

// ---------------------------------------------------------------- tips

// A mouse shows a tip on hover. A finger has no hover: a tap on something that only has a tip (the clock,
// the light, the vitals) pins it, and the next tap anywhere only puts it away, so it never walks or acts.
const ftip = $('ftip');
let pinned = false, swallow = false;
function showTip(el) {
  const [title, ...lines] = el.dataset.tip.split('|');
  ftip.innerHTML = `<div class="t">${px(title)}</div>${lines.map(l => { const m = /^([gbm]):(.*)$/.exec(l); return `<div class="${m ? m[1] : ''}">${esc(m ? m[2] : l)}</div>`; }).join('')}`;
  ftip.hidden = false;
  const r = el.getBoundingClientRect(), w = ftip.offsetWidth, h = ftip.offsetHeight;
  ftip.style.left = `${Math.max(8, Math.min(innerWidth - w - 8, r.left + r.width / 2 - w / 2))}px`;
  ftip.style.top = `${Math.min(innerHeight - h - 8, r.top - h - 8 < 8 ? r.bottom + 8 : r.top - h - 8)}px`;
}
function hideTip() { ftip.hidden = true; pinned = false; }
document.addEventListener('pointerover', e => {
  if (e.pointerType !== 'mouse') return;
  const el = e.target.closest('[data-tip]');
  if (el) showTip(el); else hideTip();
});
// Each touch decides afresh whether it only puts a tip away, so a swipe (a pan ends in pointercancel,
// with no click to swallow) never leaves the next tap swallowed.
document.addEventListener('pointerdown', e => {
  if (e.pointerType === 'mouse') return;
  swallow = pinned;
  if (pinned) hideTip();
}, true);
document.addEventListener('pointercancel', () => { swallow = false; }, true);
document.addEventListener('click', e => { if (swallow) { swallow = false; e.stopPropagation(); e.preventDefault(); } }, true);
document.addEventListener('pointerup', e => {
  if (e.pointerType === 'mouse' || swallow) return;
  const el = e.target.closest('[data-tip]');
  if (el && !el.closest('button, [data-verb], [data-open], [data-act], [data-sheet]')) { showTip(el); pinned = true; }
});

// ---------------------------------------------------------------- the map's hover and click

const tipEl = $('tip');
function hoverTile(t, e) {
  if (!t || !S.view) { tipEl.hidden = true; return; }
  const m = S.view.map, code = m.known[t.y * m.width + t.x];
  const named = m.named.find(n => n.x === t.x && n.y === t.y);
  const rows = [];
  if (code !== '.') {
    const p = m.places.find(q => q.x === t.x && q.y === t.y);
    if (p?.fire) rows.push(`${img(p.fire === 'Embers' || p.fire === 'Cold' ? 'embers' : 'fire')}Fire: ${esc(p.fire)}`);
    if (p?.station) rows.push(`${img('ui/meat')}${stationWords(p.station)}`);
    if (p?.shelter) rows.push(`${img('shelter')}${esc(p.shelter)}`);
    for (const s of m.sightings.filter(s => s.x === t.x && s.y === t.y)) rows.push(`${img(s.species)}${s.count > 1 ? `${s.count} × ` : ''}${esc(s.species.split('/').pop())} · ${ago(S.view.minute - s.minute)}`);
    const dir = dirTo(S.view.place, t), w = dir && walkVerb(S.view, dir);
    if (w?.trip) rows.push(`${img('ui/clock')}${w.trip.totalMinutes} min to cross`);
    if (!map.inSight(t.x, t.y)) rows.push('<span class="muted">as you last saw it</span>');
  }
  tipEl.innerHTML = `<div class="t">${px(named?.name ?? (code === '.' ? 'Unexplored' : cap(map.terrainName(code))))}</div>${rows.map(r => `<div class="r">${r}</div>`).join('')}`;
  const r = $('mapArea').getBoundingClientRect();
  tipEl.style.left = `${e.clientX - r.left}px`; tipEl.style.top = `${e.clientY - r.top - 14}px`;
  tipEl.hidden = false;
}
function clickTile(t) {
  if (S.busy || !S.view) return;
  const here = S.view.place;
  if (t.x === here.x && t.y === here.y) return openDrawer({ kind: 'here' });
  const dir = dirTo(here, t);
  if (dir) return openTrip(dir);
  S.selected = t;
  openDrawer({ kind: 'place', x: t.x, y: t.y });
}
/** How far in from the map's edges the drawer (with the margin it keeps from the edge, so it is centred in
 *  what is left) and a status strip along its top reach, in CSS px (cameraTarget). */
function mapCoverPx() {
  const m = $('mapArea').getBoundingClientRect(), c = { top: 0, right: 0, bottom: 0 };
  const d = $('drawer');
  if (!d.hidden) {
    const r = d.getBoundingClientRect();
    if (r.width > m.width * 0.9) c.bottom = 2 * m.bottom - r.top - r.bottom; else c.right = 2 * m.right - r.left - r.right;
  }
  const st = $('status').getBoundingClientRect();
  if (st.width > m.width / 2 && st.top - m.top < 24) c.top = st.bottom - m.top;
  return c;
}
const map = createMap($('world'), () => ({ ...S, coverPx: mapCoverPx(), displayMinuteOfDay: S.displayMinute != null ? minuteOfDay(S.displayMinute) : null }), { onHover: hoverTile, onClick: clickTile });
createGrid($('grid'), () => S, {
  onCell: c => {
    const mv = S.view?.combat?.actions.find(a => a.kind === 'fight-move');
    if (mv && !S.busy) run(mv.id, { to: c });
  },
});

// ---------------------------------------------------------------- keys

document.addEventListener('keydown', e => {
  if (!S.view || e.metaKey || e.ctrlKey || e.altKey) return;
  const v = S.view, k = e.key;
  if (!$('veil').hidden) {
    const does = veilKey($('veil').dataset.kind, k);
    if (does === 'keep') return;
    closeVeil();
    if (does === 'close') { e.preventDefault(); return; }
  }
  if (log && k.toLowerCase() === 'n' && !e.repeat) { markLog(); return; }
  if (k === 'Escape') { if (S.busy && (S.drawer?.kind === 'job' || S.drawer?.kind === 'forage')) S.stop = true; else closeDrawer(); return; }
  if (S.busy || v.combat) return;
  const arrows = { ArrowUp: 'north', ArrowRight: 'east', ArrowDown: 'south', ArrowLeft: 'west' };
  if (arrows[k]) {
    e.preventDefault();
    const w = walkVerb(v, arrows[k]);
    if (w && !w.refused && w.trip && !w.trip.hazardous) run(w.id); else openTrip(arrows[k]);
    return;
  }
  const key = k.toLowerCase();
  if (key === 'enter' && S.drawer?.kind === 'trip') { const w = walkVerb(v, S.drawer.dir); if (w) run(w.id); }
  else if (key === 'f') {
    const light = lightVerb(v);
    if (S.drawer?.kind === 'fire' && light && !v.fire?.burning) run(light.id); else openDrawer({ kind: 'fire' });
  }
  else if (key === 'g') { if (S.drawer?.kind === 'forage') run(forageFocus(v.forage).verb, { minutes: S.forage.mins }); else if (v.forage) openDrawer({ kind: 'forage' }); }
  else if (key === 'k') { if (S.drawer?.kind === 'forage' && v.forage) run(v.forage.walkVerb); }
  else if (key === 'z') { const s = v.tile.find(x => x.kind === 'sleep'); if (s) run(s.id); }
  else if (key === 'w') { const s = v.tile.find(x => x.kind === 'wait'); if (s) run(s.id); }
  else if (key === 'm') S.drawer?.kind === 'make' ? closeDrawer() : openDrawer({ kind: 'make' });
  else if (key === 'x') S.drawer?.kind === 'here' ? closeDrawer() : openDrawer({ kind: 'here' });
  else if (key === 'b') S.drawer?.kind === 'body' ? closeDrawer() : openDrawer({ kind: 'body' });
  else if (key === 'i') S.drawer?.kind === 'pack' ? closeDrawer() : openDrawer({ kind: 'pack' });
  else if (key === 'j') S.drawer?.kind === 'log' ? closeDrawer() : openDrawer({ kind: 'log' });
  else if (key === 's') S.drawer?.kind === 'save' ? closeDrawer() : openDrawer({ kind: 'save' });
});

// ---------------------------------------------------------------- saving

const saveName = () => `text-survival-day${S.view.clock.day}.json`;
const saveMeta = () => ({ day: S.view.clock.day, time: clock(S.view.clock.minuteOfDay), place: S.view.place.name });
async function doSave() {
  const bytes = await client.save();
  await putSave(bytes, saveMeta());
  openDrawer({ kind: 'save', note: `Saved: day ${S.view.clock.day}, ${clock(S.view.clock.minuteOfDay)}.` });
}
async function doLoad() {
  const s = await getSave();
  if (!s) return toast('ui/warning', 'No save in this browser');
  await loadBytes(s.bytes, `Loaded: day ${s.meta.day}, ${s.meta.time}.`);
}
async function doDownload() { download(await client.save(), saveName()); }
async function doUpload() { const bytes = await upload(); if (bytes) await loadBytes(bytes, 'Loaded the file.'); }
async function loadBytes(bytes, note) {
  try {
    const v = await client.load(bytes);
    $('veil').hidden = true; $('veil').dataset.kind = '';
    S.strikes = [];
    apply(v);
    openDrawer({ kind: 'save', note });
  } catch (e) { toast('ui/warning', `Could not load: ${e.message}`); }
}

// ---------------------------------------------------------------- the playtest log (?log)

/** A mark in the log, its wall-clock time shown on the chip a while, to say into the voice memo. */
let markedAt = null, markT = 0;
function markLog() {
  markedAt = log.mark();
  clearTimeout(markT); markT = setTimeout(() => { markedAt = null; renderLogChip(); }, 4000);
  renderLogChip();
}

/** The recording chip: how many calls are logged, a mark, and the log as a file. */
function renderLogChip() {
  if (!log) return;
  let chip = $('logChip');
  if (!chip) {
    chip = Object.assign(document.createElement('div'), { id: 'logChip', className: 'plate logchip' });
    $('mapArea').append(chip);
  }
  const s = log.sink, posting = logging.endpoint ? ` · ${s.failures && s.sent < log.entries.length ? 'not posting' : 'posted'} ${s.sent}` : '';
  chip.innerHTML = `<span class="rec${markedAt ? ' marked' : ''}" ${tip('Playtest log', [`${log.entries.length} entries${posting}`, s.lastError ?? 'N marks the moment'])}>${markedAt ? `Marked ${markedAt}` : `REC ${log.entries.length}`}</span>
    <button class="act quiet" data-act="mark">Mark <span class="key">N</span></button><button class="act quiet" data-act="logfile">Log file</button>`;
}

// ---------------------------------------------------------------- render and start

function render() {
  if (!S.view) return;
  renderTop(); renderStatus(); renderDock(); renderCombat(); renderDrawer(); renderEnd(); renderLogChip();
}

async function newGame(seed) {
  $('veil').hidden = true; $('veil').dataset.kind = '';
  S.drawer = null; S.strikes = []; S.selected = null;
  apply(await client.newGame(seed));
}

async function start() {
  const seed = +(params.get('seed') ?? 1);
  const t0 = performance.now();
  try {
    S.view = await client.newGame(seed);
    if (params.get('scenario')) S.view = await client.scenario(params.get('scenario'));
    S.drawPos = { x: S.view.place.x, y: S.view.place.y };
    render();
    $('loading').hidden = true;
    console.log(`ready in ${Math.round(performance.now() - t0)} ms`);
  } catch (e) {
    $('loading').classList.add('err');
    $('loadingNote').textContent = String(e.message ?? e);
    throw e;
  }
}
window.ui.ready = start();
window.ui.run = run;
window.ui.openDrawer = openDrawer;
window.ui.render = render;
window.ui.loadBytes = loadBytes;
window.ui.doSave = doSave;
window.ui.doLoad = doLoad;
window.ui.openTrip = openTrip;
window.ui.allVerbs = () => allVerbs(S.view);
