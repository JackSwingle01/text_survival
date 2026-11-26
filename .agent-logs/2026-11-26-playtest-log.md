# The playtest log (?log)

## Hypothesis

Jack wants to play, talk into a phone voice memo, and line the two up with what happened in the game.
Every game call goes through `client.js`'s few calls, and Sim is deterministic from its seed, so
logging those calls with their wall-clock time is the whole game: no screen recording or state dumps
needed. v1: on only with `?log`, kept in memory, downloadable, with an endpoint seam (`?log=<url>`, batched
POSTs) for a collector later. A mark button/key gives a wall-clock anchor to say into the memo.

## Log

- Revised: `tools/shots.mjs --run` failed on main before this change: it runs `v.label === 'Forage'`,
  and once the ground is searched the label is "Forage (scarce)". The script now matches either.
- Revised: the replay first ran on the page's own client and was refused ("No verb '1.15' in the current
  view (generation 2)"): verb ids carry the host's view count, which `newGame` does not reset. A replay
  runs on a fresh client; the README says so.
- Revised: a save is about 445 KB, not the fixture's 80 KB, so the snapshots stay every six game hours
  rather than more often. A scripted two-hour run logs 224 entries, 1.6 MB with its save and load.
- Revised: the mark's toast sat on the chip on a phone (both bottom-centre); the chip shows the time itself.
- Verified: `node --test tools/playtest.test.mjs` (8) and `tools/page.test.mjs` (5) pass; `shots.mjs --run
  --replay` on an interpreter publish replays 224 entries to a byte-identical save; screenshots desktop,
  phone portrait and landscape with `?log=1`; without `?log` there is no chip and `window.ui.log` is null.
- Follow-up: none of the web tools' node tests (`page.test.mjs`, `playtest.test.mjs`) run in CI.
- Follow-up: a collector for `?log=<url>` (a Render web service needs a persistent disk for SQLite), and a
  tool that lines a transcribed memo up with a log's marks.
