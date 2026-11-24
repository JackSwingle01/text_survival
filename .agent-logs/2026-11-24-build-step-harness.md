# Issue build: a harness for its Claude step, and a cancel that stops the queue

Hypothesis: three things let one untested workflow change (#208, background tasks on) break four
builds. Nothing ran the build's Claude step before the merge, though a four-minute local run shows the
failure; nobody was watching the first build after it; and cancelling the broken runs parked their
issues and dispatched the next run on the same version.

- Changes: `.github/scripts/try_build_step.sh` runs `claude -p` with the Implement step's own `env` and
  `--json-schema`, read from the workflow, on a toy build (a sub-agent whose report it needs, a
  150-second command) and fails unless it answers built; the workflow's header names it. The Report
  step and the `next` job skip a cancelled run. CLAUDE.md: a change proven only on main names its watch.
- Affects: a cancelled build's issue keeps `ready-for-agent`, and nothing is dispatched until the hourly
  run or the next dispatch.

Checked: actionlint on the workflow.
Not checked: the cancel path, which exists only on main. Watch: I cancel nothing to test it; the first
run someone cancels after the merge should leave its issues' labels alone and dispatch no next run.
