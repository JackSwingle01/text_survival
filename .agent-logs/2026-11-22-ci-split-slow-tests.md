# ci-split-slow-tests: PR CI runs the fast suite; Slow tests move to the nightly run

Hypothesis:
- Cause: PR CI took 13-25 min, all of it `dotnet test` on Sim.Tests (21 m 54 s in run 45052097215). The 47 Slow tests are ~2,780 CPU-s (timed locally from a trx); a 4-core runner shared with three other test projects stretches that to ~20 min. Biggest single test: NpcBenchReport.Bench, 401 s, no assertions.
- Changes: ci.yml `verify` -> `test` with `--filter "Category!=Slow"`; property-search.yml -> nightly.yml, a matrix of `slow` (Slow tests outside Verification, solution-wide) and `property-search` (unchanged search), one shared issue-per-failure step; ReportFact attribute (skips unless its env var is set) on NpcBenchReport, NPC_BENCH=1 to run it.
- The user's option 4 (fewer `_Many` iterations on CI) was already in place: every Slow `_Many` property has a quick fast-suite sibling (e.g. Ledger 15 vs 600), and the nightly search runs the big counts x10. What was left: the nightly `slow` job excludes Survival.Tests.Verification so the `_Many` tests do not run twice.
- Stays green: actionlint; fast Sim.Tests; the slow job's exact command locally.
Revised: a solution-wide run with `LogFileName=nightly.trx` can let projects overwrite one file -> `LogFilePrefix=nightly`, and the issue script globs every trx.
Checked: the slow-job command locally: Sim.Tests 29 passed + bench skipped (1 m 24 s), Tune.Tests 4 passed, four distinct trx files, the issue script parses them (0 failures). Fast Sim.Tests 1492 passed in 47 s. NPC_BENCH=1 with one seed and one day runs the bench.
Follow-up: the nightly property search at x10 runs ConservationButcheringTests' two `_Many` tests one after the other: ~3,200 CPU-s locally, maybe 80-100 min on a CI core, close to the job's 120 min timeout. It has never run yet; watch the first run.
Follow-up: the old suite (text_survival.Tests, ~4 min) still runs on every PR; CLAUDE.md says agents skip it unless files outside rebuild/ change, and CI could path-filter the same way.
