// Lines a voice-memo transcript up with a playtest log (App/wwwroot/playtest.js) and writes the timeline:
// each thing said, at the wall-clock time and game moment it was said, with what happened in the game
// since the line before and the save to load to see it.
//
//   node tools/playtest-align.mjs <playtest.json> <transcript.txt> [--anchor MM:SS=HH:MM:SS]... [-o out.md]
//
// The transcript is "MM:SS  Speaker  text" (or "H:MM:SS"), one line each, the time since the recording
// began; a line without a time continues the one before. The anchors are the marks: tap Mark, read out
// the time the chip shows ("marked 22:31:05"), and the line that says it ties that point in the
// recording to the mark in the log. Each line is placed from the anchor nearest it, so a pause in the
// recording only shifts lines until the next mark. --anchor gives one by hand: the transcript time, then
// the mark's time as shown.

import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const two = n => String(n).padStart(2, '0');
const DAY = 86_400_000;

/** "1:02:03" or "02:03" to seconds. */
const seconds = stamp => stamp.split(':').reduce((a, x) => a * 60 + +x, 0);

/** The transcript's lines: { at (seconds into the recording), stamp, speaker, text }. */
export function parseTranscript(text) {
  const lines = [];
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) continue;
    const m = line.match(/^(\d{1,2}(?::\d{2}){1,2})\s+(.*)$/);
    if (!m) {
      if (!lines.length) throw new Error(`The transcript does not start with a time: "${line}"`);
      lines.at(-1).text += ' ' + line;
      continue;
    }
    const parts = m[2].split(/\s{2,}|\t+/);
    const [speaker, text] = parts.length > 1 ? [parts[0], parts.slice(1).join(' ')] : [null, parts[0]];
    lines.push({ at: seconds(m[1]), stamp: m[1], speaker, text });
  }
  if (!lines.length) throw new Error('The transcript has no timed lines');
  return lines;
}

/** Times of day said in a line, as the transcriber wrote them: "22:31:05", "10:31", "22.31". */
const spokenTimes = text => [...text.matchAll(/\b(\d{1,2})[:.](\d{2})(?:[:.](\d{2}))?\b/g)]
  .map(m => ({ h: +m[1], m: +m[2], s: m[3] == null ? null : +m[3], said: m[0] }));

const sameTime = (said, shown) => {
  const [h, m, s] = shown.split(':').map(Number);
  return said.h % 12 === h % 12 && said.m === m && (said.s == null || said.s === s);
};

/** Ties points in the recording to marks in the log: { at, t, shown, line? }, in recording order. */
export function findAnchors(lines, marks, manual = []) {
  const anchors = [];
  for (const { at, shown } of manual) {
    const mark = marks.find(k => k.shown === shown);
    if (!mark) throw new Error(`--anchor: no mark in the log shown as ${shown} (marks: ${marks.map(k => k.shown).join(', ') || 'none'})`);
    anchors.push({ at, t: mark.t, shown, line: null });
  }
  let from = 0; // marks are said in the order they were made
  for (const line of lines) {
    for (const said of spokenTimes(line.text)) {
      const i = marks.findIndex((k, j) => j >= from && sameTime(said, k.shown));
      if (i < 0) continue;
      anchors.push({ at: line.at, t: marks[i].t, shown: marks[i].shown, line });
      from = i + 1;
      break;
    }
  }
  return anchors.sort((a, b) => a.at - b.at);
}

/** The player's clock, from an anchor: shown is local, t is UTC. */
function zoneOf(anchor) {
  const local = seconds(anchor.shown) * 1000, utc = ((anchor.t % DAY) + DAY) % DAY;
  let off = Math.round((local - utc) / 60_000) * 60_000;
  if (off > 14 * 3_600_000) off -= DAY;
  if (off < -12 * 3_600_000) off += DAY;
  return t => { const d = new Date(t + off); return `${two(d.getUTCHours())}:${two(d.getUTCMinutes())}:${two(d.getUTCSeconds())}`; };
}

/** The timeline as Markdown, and any warnings. */
export function align(record, transcript, { manual = [] } = {}) {
  const { session, entries } = record;
  const lines = parseTranscript(transcript);
  const marks = entries.filter(e => e.op === 'mark' && e.shown);
  const anchors = findAnchors(lines, marks, manual);
  if (!anchors.length) {
    const said = lines.filter(l => /mark/i.test(l.text)).map(l => `  ${l.stamp}  ${l.text}`).join('\n');
    throw new Error(`No line of the transcript says a mark's time (marks: ${marks.map(k => k.shown).join(', ') || 'none'}).`
      + (said ? ` Lines that mention a mark:\n${said}\nGive one by hand: --anchor MM:SS=HH:MM:SS` : ' Give one by hand: --anchor MM:SS=HH:MM:SS'));
  }
  const warnings = [];
  for (let i = 1; i < anchors.length; i++) {
    const a = anchors[i - 1], b = anchors[i], drift = (b.t - a.t) / 1000 - (b.at - a.at);
    if (Math.abs(drift) > 10) warnings.push(`The marks at ${a.shown} and ${b.shown} are ${Math.round(drift)} s further apart in the log than in the recording: was it paused?`);
  }
  const clock = zoneOf(anchors[0]);
  const wallOf = at => { const a = anchors.reduce((best, x) => Math.abs(x.at - at) < Math.abs(best.at - at) ? x : best); return a.t + (at - a.at) * 1000; };

  const out = [`# Playtest ${session.startedAt}`, '',
    `Build ${session.build ?? 'unknown'} · ${entries.length} log entries · ${lines.length} transcript lines`, '',
    `Anchors: ${anchors.map(a => `${a.shown} at ${a.line?.stamp ?? fmtAt(a.at)}${a.line ? '' : ' (by hand)'}`).join(', ')}`, ''];
  for (const w of warnings) out.push(`> Warning: ${w}`, '');

  let k = 0;
  for (const line of lines) {
    const t = wallOf(line.at);
    const since = [];
    for (; k < entries.length && entries[k].t <= t; k++) since.push(entries[k]);
    const state = lastWhere(entries, e => e.t <= t && e.place);
    const save = lastIndexWhere(entries, e => e.t <= t && e.save);
    out.push(`## ${clock(t)} · ${state ? `${state.clock ?? `minute ${state.minute}`} · ${state.place}` : 'before the game started'} (${line.stamp})`, '');
    out.push(`> ${line.speaker ? `**${line.speaker}:** ` : ''}${line.text}`, '');
    out.push(...happened(since));
    if (save >= 0) out.push(`- Save to load: entry ${save} (${entries[save].clock ?? `minute ${entries[save].minute}`})`);
    if (t > entries.at(-1).t + 60_000) out.push('- After the log ends');
    out.push('');
  }
  return { markdown: out.join('\n'), warnings, anchors };
}

const fmtAt = at => `${Math.floor(at / 60)}:${two(at % 60)}`;
const lastWhere = (xs, f) => { for (let i = xs.length - 1; i >= 0; i--) if (f(xs[i])) return xs[i]; return null; };
const lastIndexWhere = (xs, f) => { for (let i = xs.length - 1; i >= 0; i--) if (f(xs[i])) return i; return -1; };

/** What the game did between two lines: jobs ended, journal lines, drawers opened, errors. */
function happened(since) {
  const out = [], seen = new Set();
  for (const e of since) {
    if (e.op === 'newGame') out.push(`- New game, seed ${e.arg}`);
    else if (e.op === 'load') out.push('- Loaded a save');
    else if (e.error) out.push(`- Refused: ${e.error}`);
    else if (e.job?.finished && !seen.has(`job|${e.job.label}|${e.minute}`)) { // a view after the end shows it again
      seen.add(`job|${e.job.label}|${e.minute}`);
      out.push(`- ${e.job.label}: ${e.job.outcome ?? 'finished'}${e.job.reason ? ` (${e.job.reason})` : ''}`);
    }
    for (const l of e.lines ?? []) if (!seen.has(l)) { seen.add(l); out.push(`  - "${l}"`); }
    if (e.outcome) out.push(`- The run ended: ${e.outcome}`);
  }
  const drawers = [...new Set(since.filter(e => e.op === 'ui' && e.arg?.drawer).map(e => e.arg.drawer))];
  if (drawers.length) out.push(`- Looked at: ${drawers.join(', ')}`);
  return out.length ? out : ['- Nothing new in the game'];
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  const manual = [], files = [];
  let outFile = null;
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--anchor') {
      const m = (args[++i] ?? '').match(/^(\d{1,2}(?::\d{2}){1,2})=(\d{2}:\d{2}:\d{2})$/);
      if (!m) { console.error('--anchor takes MM:SS=HH:MM:SS, the transcript time then the mark as shown'); process.exit(2); }
      manual.push({ at: seconds(m[1]), shown: m[2] });
    } else if (args[i] === '-o') outFile = args[++i];
    else files.push(args[i]);
  }
  if (files.length !== 2) { console.error('usage: node playtest-align.mjs <playtest.json> <transcript.txt> [--anchor MM:SS=HH:MM:SS]... [-o out.md]'); process.exit(2); }
  try {
    const { markdown, warnings, anchors } = align(JSON.parse(fs.readFileSync(files[0], 'utf8')), fs.readFileSync(files[1], 'utf8'), { manual });
    for (const w of warnings) console.error('warning: ' + w);
    if (outFile) { fs.writeFileSync(outFile, markdown); console.error(`${outFile}: ${anchors.length} anchor(s)`); } else process.stdout.write(markdown);
  } catch (e) {
    console.error(e.message);
    process.exit(1);
  }
}
