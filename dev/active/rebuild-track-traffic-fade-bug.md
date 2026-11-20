# Bug: track traffic only fades when the track is walked again

**Status:** open, deferred (noted 2026-11-20 during the clean-room rebuild, phase 2)
**Where:** `rebuild/Sim/Tracks.cs` (port of old `Environments/Grid/TrackRegistry.cs`); same behaviour in the old game.

## What happens

A track stores `Traffic` (how many individuals came through) and the direction totals as they stood
at its last stamp. When someone walks the tile again, the old traffic is faded by the track's
freshness and the new passage added (`Tracks.cs` ~line 115). Between stamps nothing fades it.

`Depth` is derived from `Traffic` (`Tracks.cs:99`), and how long a print lasts derives from depth. So
a track's depth and lifespan use the traffic as of its last stamp, not as it has decayed since:

- A herd passes, then nothing for days: the track keeps the herd's full traffic and depth until it
  expires, rather than thinning to what a few days of weather would leave.
- Readable traffic is faded on read (`Read` multiplies by freshness), but depth and lifespan are not,
  so the sign and the print's survival disagree about how much traffic is left.

## Why it is a bug

Weather erodes prints continuously; the traffic a track shows should fade the same way whether or
not anyone walks it again. Re-stamping should not be what triggers decay.

## Suggested fix

Derive the effective traffic at read time from the stamp-time traffic and the erosion since the stamp
(the erosion clock is already derived: `Tracks.ErosionAt`), and compute depth and lifespan from that
effective value. Keep one formula for "traffic now" and use it in `Read`, `Depth`, expiry and
`RecordMove`'s re-stamp.

Add a regression test: one herd passage, then N days with no traffic, then assert depth/lifespan
match a track stamped with the decayed traffic. The current golden tests in `TracksGoldenTests` pin the
old behaviour and will need re-pinning with a note.
