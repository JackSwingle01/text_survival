# Nightly baselines of main: the NPC bench and the cohesion gate

Hypothesis: a build that changes NPCs or animals wants main's numbers to compare with, and every
issue build so far measured them itself (#84 and #85 ran main's cohesion gate in a worktree; #87's NPC
bench waited 98 minutes and its "before" run never finished). The nightly run can measure main once
and publish it; builds read the result.

- Changes: `nightly.yml` gains an `npc-bench` job (the 20 seeds #87 used, 60 days, Release), a step in
  the `slow` job that reads the cohesion gate's printed lines out of its results
  (`.github/scripts/test_output.py`), and a `baseline` job that, on main, commits both files to the
  `baselines` branch (`.github/scripts/publish_baselines.sh`). CLAUDE.md and
  `documentation/verification.md` point to the branch. `NpcBenchReport` appends each seed's line to
  `NPC_BENCH_OUT` as that seed finishes.
- Affects: the `report` job needs only `test`, so the bench never opens the nightly issue. The
  `baseline` job holds `contents: write`, the nightly's first. A run on a branch publishes nothing; it
  uploads the two files as artifacts and shows them in its summary, that branch's "after".

Revised: one stuck seed hid every other seed's line (the bench wrote the file after all had finished) ->
each line is appended as its seed ends, under a lock, and the job sorts the file by seed; the job cuts the
bench off at an hour and publishes what finished, with the count.
Revised: main's bench as an artifact, read with `gh run download` by a script -> a `baselines` branch,
read with `git fetch`. An artifact needs a token that may read Actions (an issue build's is not known to),
`gh` (a cloud session has none), and expires in 30 days; any checkout can fetch a branch, and its
history is the trend.
Revised: only the NPC bench -> the cohesion gate's lines too: they are what #84 and #85 re-measured, and
the gate already runs nightly, so publishing them costs a step. `PlanBenchReport` (an A/B of one
question, in runner-dependent seconds) and `KillChanceTableReport` (prints a table to paste) are left out.
Revised: a night that measured nothing would overwrite good numbers with a header -> a file replaces the
branch's copy only when it holds lines past its header.

Checked:
- `NPC_BENCH_SEEDS=3,1,7 NPC_BENCH_DAYS=1` in Release: the file held seeds 1, 7, 3 in finish order, and
  the job's shell prints them 1, 3, 7 under the two header lines.
- `CohesionGateSeed17Tests` in Release with the nightly's trx logger (1 m 31 s): `test_output.py` reads
  its 24 lines from the results, and nothing for a name no test has.
- `publish_baselines.sh` against a local bare repo, three runs: it creates the branch with the one
  file that held measurements, then adds the other and keeps the first when the new one is only a
  header, then commits nothing when nothing changed; main and the local branches are untouched.
- `nightly.yml` parses as YAML. actionlint was not run here; CI runs it on the PR.
Not checked: any of it on a runner: how long 20 seeds by 60 days take there (#87's "after" took 18
minutes beside other work), the artifact download between jobs, and the push to `baselines`, which a
branch ruleset on the repo could refuse. The first nightly run after merge shows all three in its summary.

Follow-up: the baseline is main at the nightly's commit, which can be behind a branch's base; each file's
first line names the commit so a reader can tell.
Follow-up: `species.json`'s kill chances go stale when combat is tuned (#84's log); a nightly test that
measures the table again and compares would catch it. Not a baseline, so not here.
