# Save compatibility starts with the first build someone else plays

Saves carry a format version from now on (`Save.FormatVersion`), and a fixture test loads a fixed
sample save, so every format change is deliberate and visible in review
(`documentation/save-format.md`). Until a build is handed to someone else to play, a format change
just bumps the version and regenerates the fixture: old saves may stop loading, and there are no
migrations. Nobody plays rebuild saves yet, and migrations now would slow every step that touches
saved state. From the first build someone else plays, each format change ships with a migration and
a test against real-shaped saves. (Revised 2026-11-21: versioning was moved earlier than the first
release, so the later refactor is small.) (Revised 2026-11-23: #121 asked that saves from before the stash was renamed
the pile still load, so format 25 is migrated to 26, the first migration; others still wait for the
first release.)
