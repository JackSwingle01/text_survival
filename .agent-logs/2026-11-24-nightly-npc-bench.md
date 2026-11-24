# Nightly NPC bench, published as main's baseline

Hypothesis: a build that changes NPCs wants main's bench numbers to compare with, and measuring them
itself is too long for an issue build (#87: 98 minutes waiting, and the "before" run never finished).
The nightly run can measure main once and publish it; builds read the result.

- Changes: `nightly.yml` gains an `npc-bench` job (the 20 seeds #87 used, 60 days, Release) that uploads
  `npc-bench.txt` as the `npc-bench` artifact, kept 30 days, and writes it to the run's summary;
  `.github/scripts/npc_bench_baseline.sh` prints the newest one from main; CLAUDE.md points to it.
  `NpcBenchReport` appends each seed's line to `NPC_BENCH_OUT` as that seed finishes.
- Affects: the `report` job needs only `test`, so the bench never opens the nightly issue. Dispatched on a
  branch, the same job is that branch's "after".

Revised: one stuck seed hid every other seed's line (the bench wrote the file after all had finished) ->
each line is appended as its seed ends, under a lock, and the job sorts the file by seed; the job cuts the
bench off at an hour and publishes what finished, with the count.

Checked: `NPC_BENCH_SEEDS=3,1,7 NPC_BENCH_DAYS=1` in Release: the file held seeds 1, 7, 3 in finish
order, and the job's `sort` puts them 1, 3, 7. `nightly.yml` parses as YAML.
Not checked: the job on a runner (how long 20 seeds by 60 days take there: #87's "after" took 18
minutes beside other work), and the script against a real artifact: none exists until the first nightly
run after merge. Whether an issue build's own `gh` token may download artifacts is not known either.

Follow-up: the baseline is main at the nightly's commit, which can be behind a branch's base; the file's
first line names the commit so a reader can tell.
