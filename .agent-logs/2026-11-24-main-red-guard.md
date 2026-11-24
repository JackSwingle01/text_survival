# A red main opens an issue, and the issue build waits

Hypothesis: main's CI went red on 2026-11-24 (a report test at 5.05 s against the 5 s limit) and nothing
said so: it showed only when an unrelated PR's check failed on it, and every build branched from main
meanwhile opened a PR carrying the red check. One open issue while main is red tells Jack, and gives the
build queue something to read.

- Changes: `ci.yml` gains a `main-red` job on pushes to main, after the four checks:
  `.github/scripts/main_red.sh` opens an issue labelled `main-red` when a check failed (or comments the
  new run on the open one) and closes it when all passed. `issue-build.yml`'s select step builds nothing
  while such an issue is open.
- Affects: a flaky failure on main stops builds until the next green push or until the issue is closed by
  hand. Closing an issue triggers triage, which dispatches the build queue again.

Checked: `main_red.sh` against a stub `gh`, five cases: red with none open (label, create), red with one
open (comment), green with one open (close), green with none open and a cancelled check with one open
(both do nothing). actionlint on both workflows.
Not checked: on a runner. Both run from main. The first push to main after merge shows the job (green:
it does nothing); the hold in the select step is first exercised when main next goes red.
Watch: I read the first `main-red` job after merge, and the next issue-build select step.

Follow-up: the issue agent does not build the `main-red` issue (the queue lets through only the bot's
`ratchet` issues); letting it would have main fix itself, at the cost of a build on a red main.
