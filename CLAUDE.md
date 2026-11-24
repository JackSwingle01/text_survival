## Primary References

Read these when the task touches what they cover:

- `principles.md` — design principles: experience goals, events, tensions, pacing. Read before
  designing a feature or writing player-facing text.
- `principles-code.md` — technical principles and naming conventions. Read before writing code.
- `documentation/overview.md` — the old game's systems and where they live. Read before working
  outside `rebuild/`.

## Build and Test

```bash
dotnet build
dotnet test
```

For the clean-room rebuild (`rebuild/`):

- Run tests in Release (`-c Release`): Debug is over twice as slow, and CI measures Release.
- Each red-green cycle, and each commit along the way: only the test classes in hand,
  `dotnet test rebuild/Sim.Tests -c Release --filter "FullyQualifiedName~<ClassName>"`. Before a push,
  handback or PR: the fast suite, `--filter "Category!=Slow"` (about 3 min on 4 cores, 2026-11-23;
  add `&Category!=LongRun` for the quick tests alone).
- Never run the whole Slow suite. Run a Slow test by name only when it checks what you changed. PR CI
  runs only the fast suite; the Slow tests and the random property search run nightly
  (`.github/workflows/nightly.yml`; `gh workflow run nightly.yml --ref <branch>` runs them on a branch),
  and Stryker at most daily (`.github/workflows/mutation.yml`).
- Ratchets (a check with an exception list that only shrinks) are burned down by one daily run that
  files a batch at a time for the issue agent. When you add one, add its section to `dev/ratchets.md`.
- `NpcBenchReport` reports and never gates; it runs only with `NPC_BENCH=1`.
- Agents choosing which checks to run or add use the `mp:verification` skill.
- A quick test takes under 5 s on CI's runner, and CI fails one that doesn't. Make it quicker, or, if it
  has to step a world for hours or days to see what it checks, tag it
  `[Trait("Category", TestCategories.LongRun)]`: it still runs on every PR, in its own check. A test that
  steps `World.New(seed)` pays for every animal on the map each minute.
- CI has three test checks, each under two minutes: `rebuild quick`, `rebuild long-run` and `old game`.
  When one nears two minutes, make its tests cheaper; do not add a check.
- Run long simulations, sweeps and probes in Release (`-c Release`), and split independent seeds
  across parallel processes.
- Changing what a save holds: follow `documentation/save-format.md`. The format is derived from the
  saved types, so there is no version number to raise and no changelog to append to; regenerate the
  fixture and commit it with the change.
- Skip the old suite (`text_survival.Tests`) and the full solution build unless files outside
  `rebuild/` changed.
- Wait for long runs with `run_in_background` and its completion notice, not `until ...; do sleep`
  polling. A poll that greps for its own pattern (`pgrep -f "X"` from a shell whose command line
  contains "X") never ends, and leftover pollers outlive the agent that started them.
- For long-running agent work (e.g. overnight), schedule an hourly check for stalled agents and leftover processes.
- When something needs the user to review, verify or validate it (a page, a UI change, a run they
  should watch), don't stop at instructions: build it, start it for them (e.g. publish and serve the
  web app in the background), and hand over a working link plus short numbered steps to test.

## Domain Language and Decisions

- `GLOSSARY.md` defines the project's terms (stat tiers, effects, conditions, contest...). Use them.
- `documentation/decisions/` holds decision records (`YYYY-MM-DD-short-name.md`, a paragraph each)
  for hard-to-reverse, surprising choices. Read them before reopening a design question.
- Before adding any feature to the rebuild, read "Where things go" in
  `documentation/clean-room-architecture.md`: the home, rule and touch points for each kind.

## Tickets, branches and PRs

- Tickets are GitHub issues. A plan too big for one session is a wayfinder map: a parent issue with
  child issues, blocking expressed with GitHub's native "blocked by".
- Each ticket gets its own branch off `main`, named `<issue#>-short-name`, worked in its own git
  worktree. Agents touch only their own worktree.
- One PR per ticket, into `main`. Merge when CI and the Claude review are green.
- Porting the old game: follow `dev/active/porting.md`.
- Each ticket's agent keeps a work log (the `mp:work-log` skill's format) in `.agent-logs/`,
  one file per task named `YYYY-MM-DD-short-name.md`, committed with its PR. See
  `.agent-logs/README.md`. Never add `.agent-logs/` to `.gitignore` or `.git/info/exclude`,
  whatever the `mp:work-log` skill says: this repo commits the logs on purpose.

### The issue agent

Two workflows. `.github/workflows/issue-triage.yml` triages every open issue written by someone
with write access, oldest first, within the hour: when it is opened, when any issue closes, when a
label comes off, hourly, or on a manual run. Sonnet reads it; a ready one gets the
`ready-for-agent` label. `.github/workflows/issue-build.yml` builds the oldest `ready-for-agent`
issue, one per run, through `mp:implement` on Opus (seams chosen and logged, nobody asked), ending
as a PR into `main`, and starts the next run while any remain. How each works is in its header. To
get the outcome you want from an issue you write:

- **Ready to build** means it says what to build, has acceptance criteria an agent can check, and
  leaves no decision to Jack. Anything short of that is parked: a "Triage Notes" comment with the
  questions, and the `needs-user` label. Answer on the issue and remove the label to requeue it.
- **Design work** (grilling, maps, a report that must come back before any change) says so in the
  issue, e.g. `**Type:** grilling`, so triage parks it at once. Label `needs-user` at creation to
  keep any issue out of the queue.
- **Blocking** goes in GitHub's native "blocked by" links: the queue skips the issue for free until
  its blockers close, then triages it again. Blocking written only in the text costs a triage, which
  then adds the links and the `blocked` label.
- **Every PR that does a ticket's work says `Closes #N`.** That is how the queue knows the work is
  taken, and how merging it unblocks what waits on it. Without it the agent builds the ticket again.
- **Working a ticket by hand:** push its `<issue#>-short-name` branch early; the queue leaves an
  issue with that branch alone.
- The agents read only what people with write access wrote: the issue, their comments, and the
  issues it mentions. Put decisions there, not in a PR thread.
- **The build queue is the `ready-for-agent` label.** Add it by hand to build an issue without
  triage; remove it to take an issue out of the build queue and send it back to triage.
- To pause the agent, disable either workflow in the Actions tab: triage stops labelling, or the
  build stops building and the labelled issues wait.

### Merge conflicts

`.github/workflows/resolve-conflicts.yml` merges `main` into every open, non-draft PR that conflicts
with it, on each push to `main` and hourly: Opus resolves, builds, tests and pushes one merge commit.
A PR it can't resolve gets a comment and `needs-user`; remove the label to try again. Mark a PR draft
to keep it off your branch.
