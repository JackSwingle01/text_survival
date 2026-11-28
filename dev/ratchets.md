# Ratchets

A ratchet is a check that holds everything except a list of known breaches, and the list only gets
shorter. This file is the one tracker for burning those lists down. Every day,
`.github/workflows/ratchet.yml` reads it, and for each ratchet below that has no open `ratchet`
issue it:

1. runs the ratchet's `remaining` command, which prints what's left, one item per line;
2. takes the first `batch` items and files one issue with the job below for those items. The issue
   agent builds it and opens the PR like any other ticket;
3. when `remaining` prints nothing, files one last issue instead: do the "When empty" step and
   delete the ratchet's section from this file.

So each ratchet gets at most one issue in flight, and the next batch is filed the day after its PR
merges. To try a change to this file: `python3 .github/scripts/ratchets.py run --dry-run` prints
the issues without filing them.

## Adding a ratchet

Add the check with its exception list first (a test, or a CI step), then a section here:

- `## name`: a short lower-case name, used in the issue title (`Ratchet: name`).
- `- **batch:** N`: items per issue. Size it so one PR stays easy to review.
- A `bash` code block: the `remaining` command, run from the repository root. It prints one item
  per line, in the order to take them, and exits 0 whether or not any are left. It runs under
  `bash -eo pipefail`, so guard a `grep` that may match nothing (`{ grep ... || true; }`).
- The job: what to do to each item, and the check that proves it's done. It goes into the issue
  word for word, so it must leave no decision to Jack.
- A paragraph starting `When empty:`: what to remove once nothing is left (the empty list, the
  exemption code), so the check holds everywhere.

## comments

- **batch:** 2

```bash
awk '/OverCommentLimit = new\(\)/{f=1;next} f&&/^ *};/{exit} f' rebuild/Sim.Tests/Architecture/CommentHygieneTests.cs |
  { grep -oP '\["\K[^"]+' || true; } |
  while read -r f; do find rebuild/Sim -name "$f" -not -path '*/obj/*' -not -path '*/bin/*'; done
```

Bring each file's comment share (comment lines over non-blank lines) to 10% or under, then take it
off `OverCommentLimit` in `rebuild/Sim.Tests/Architecture/CommentHygieneTests.cs`. Comments say
why only (`principles-code.md`, "Self-documenting"; `documentation/clean-room-architecture.md`,
"Comments say why only"). Delete history, restated code and narration. Where a comment explains
what the code does, rename or extract until the code says it. Keep every comment that gives a
reason the code can't, and shorten it. Change no behaviour: the fast suite
(`dotnet test rebuild/Sim.Tests --filter "Category!=Slow"`) stays green with no test changed except
the list entries. `CommentHygieneTests` passes.

When empty: delete `OverCommentLimit` and its branch in `SimComments_StayUnderTheRatchet`, so every
Sim file is held to the 10% limit.

## capacity-reads

- **batch:** 8

```bash
awk '/CapacityReadsToday =$/{f=1;next} f&&/^ *\];/{exit} f' rebuild/Sim.Tests/Architecture/CapacityReadTests.cs |
  { grep -oP '^\s*"\K[^"]+' || true; }
```

Each item is a test member that reads a capacity, as `CapacityReadTests` names it in
`CapacityReadsToday` (`rebuild/Sim.Tests/Architecture/CapacityReadTests.cs`). Rewrite each listed
test read as an outcome a caller can read: a faculty, vitality, alive, a verb's refusal or the
minutes it took. For example, "a wolf's legs count toward moving" becomes "a wolf with whole legs
has full speed". Keep what each test protects; where a test only restated a capacity's formula and
no outcome differs, delete it and say so in the PR. Then take the entry off the list. Change no sim
behaviour: the fast suite stays green.

When empty: delete the exception list and its branch in the rule, and hide the capacities type and
the read that returns it from everything outside the body, the test project included, so the
compiler holds the rule. The screen's named read-out stays. `Effects.CapacityDeltas` is hidden from
everything outside the body with them. `internal` alone does not hide them: Sim grants
InternalsVisibleTo to Sim.Tests, Tune and Host.Tests, so make them private to the body's own types
(`BodyTierTests.TheBodysTypes`), nested in them where more than one needs them, and delete `CapacityReadTests`' own reads of them, its
breaching sample included, since the compiler then holds the rule. Rename the `Capacity` enum
(`rebuild/Sim/Body.cs`) to `BodyFunction`, with everything that names it: the save's shape (regenerate
the fixture by `documentation/save-format.md`), the table in `names.json` and the body screen's
read-out. `GLOSSARY.md` gains an entry: a body function is what a part of the body supplies, and five
of them are the capacities. No type or member in the sim is named `Capacity` for the eight body functions; "capacity"
in code means one of the glossary's five.
