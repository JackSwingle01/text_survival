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
the issues without filing them (needs `dotnet` on the path for the formatting ratchet).

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

## formatting

- **batch:** 10

```bash
out=$(mktemp -d)
dotnet format whitespace . --folder --verify-no-changes --report "$out" >/dev/null 2>&1 || true
python3 -c 'import json, os, sys
print("\n".join(sorted({os.path.relpath(x["FilePath"]) for x in json.load(open(sys.argv[1]))})))' "$out/format-report.json"
```

Run `dotnet format whitespace --folder --include <the files>` and commit the result. Change nothing
but whitespace. `dotnet format whitespace --folder --verify-no-changes --include <the files>`
passes. Build and run the suites for the projects the files belong to (`rebuild/` files: the fast
suite; others: `dotnet build` and `dotnet test`).

When empty: change the `format` job in `.github/workflows/ci.yml` to check every C# file in the
repository (`dotnet format whitespace . --folder --verify-no-changes`), not only the ones a PR
touches, and drop its "Ratchet" comment.

## fast-test-time

- **batch:** 3

```bash
{ grep -v -e '^#' -e '^$' rebuild/Sim.Tests/slow-fast-tests.txt || true; }
```

Get each test under 5 s in the Release fast suite, then delete its line from
`rebuild/Sim.Tests/slow-fast-tests.txt`. First time it alone
(`dotnet test rebuild/Sim.Tests -c Release --filter "FullyQualifiedName~<name>"`) and find where the
time goes. The usual cause: the test steps `World.New(seed)`, so every minute it waits or works also
simulates every animal on the map. Keep what the test checks, and cut what it doesn't: a world without
the animals when animals aren't its subject (a `TestKit` helper if none exists), a hand-built or
smaller world, fewer simulated minutes. Change no `rebuild/Sim` behaviour to do it. A test whose
subject is a long run by nature (populated days, a season, a month's budget) is tagged
`[Trait("Category", TestCategories.Slow)]` instead, so it runs nightly; if no other fast test checks
the same rule, add a quick sibling that does, on a small world. Check: the fast suite
(`dotnet test rebuild/Sim.Tests -c Release --filter "Category!=Slow" --logger "trx;LogFileName=$PWD/fast.trx"`)
is green, and
`python3 .github/scripts/test_durations.py fast.trx --allow rebuild/Sim.Tests/slow-fast-tests.txt`
passes with the lines removed.

When empty: delete `rebuild/Sim.Tests/slow-fast-tests.txt` and the `--allow` argument in the
"Fast tests stay fast" step of `.github/workflows/ci.yml`, and drop that step's "Ratchet" comment.

## test-worlds

- **batch:** 1

```bash
awk '/NotYetOnTestWorlds = new\(\)/{f=1;next} f&&/^ *};/{exit} f' rebuild/Sim.Tests/Architecture/TestWorldSourceTests.cs |
  { grep -oP '\["\K[^"]+' || true; } |
  while read -r f; do find rebuild/Sim.Tests -name "$f" -not -path '*/obj/*' -not -path '*/bin/*'; done
```

In each file, change every direct `World.New(`, `Save.FromJson(` and `Map.Generate(` call to
`TestWorlds.New`, `TestWorlds.Load` or `TestWorlds.Map` (`rebuild/Sim.Tests/TestWorlds.cs`), except in a
test whose subject is world generation or loading (it compares generated maps, checks a save's bytes, or
loads a save to compare with a straight run), or a search over 20 or more seeds. Then take the file off
`NotYetOnTestWorlds` in `rebuild/Sim.Tests/Architecture/TestWorldSourceTests.cs`; if any direct calls
are left, list the file in `MayCallDirectly` with their count and which of those reasons holds. Change no
`rebuild/Sim` code. Check: `TestWorldSourceTests` passes, and so does the fast suite
(`dotnet test rebuild/Sim.Tests -c Release --filter "Category!=Slow"`).

When empty: delete `NotYetOnTestWorlds` and pass `MayCallDirectly` alone to `CountRatchet` in
`TestsGetTheirWorlds_FromTestWorlds`.
