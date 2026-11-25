# No save migrations until the rebuild is finished

The rebuild migrates no save from an older format until the rebuild is finished. A format change
regenerates the fixture, and a save in any older format is refused
on load (`documentation/save-format.md`). This holds even when a ticket asks for old saves to keep
loading: the ticket is built without that criterion. The format has moved 26 times while the
rebuild is still being built, and nobody's rebuild save is worth keeping. A migration is code every
later format change has to keep working, and the one written for the stash's rename to the pile
(#121, format 25 to 26) took a JSON rewrite of field names, event kinds and the text the game had
written, with a frozen old fixture and its own tests, for a rename that changed no behaviour. It was
removed before it merged. Once the rebuild is finished, each format change ships with a migration
from the previous one, tested against real saves. This replaces
`2026-11-21-save-compatibility-from-first-release.md`, which started migrations at the first build
someone else plays.

(Later: the version number was replaced by a format derived from the saved types, #159; see
`documentation/save-format.md`.)
