# The two 30-day memory budgets rise to 8,200 and 8,600 MB (#294)

## Hypothesis
Two Slow tests cap the MB a 30-day run allocates: `HerdTests.ThirtyPopulatedDays_StayCheap` (6,600 MB, measured
6,848) and `Phase2GateTests.ThirtyDays_Headless_CompletesAliveAndSmall` (6,100 MB, measured 7,181 on the nightly).
Raising the two constants to 8,200 and 8,600 and replacing the "nor raise the number" wording with Jack's rule
changes nothing else; no sim code is touched.

## Revised
- Revised: no scout or design-review sub-agents, and a self-review in place of a code-review sub-agent. The change
  is two constants and three comment lines whose text the issue dictates; there is no design to review.
- Revised: the "do not raise" wording lives in HerdTests' comment ("nor raise the number"); Phase2GateTests points
  to it. Removed those words there; the rest of both comments' history stays.
- Revised: this work log is committed beside the two test files, as CLAUDE.md requires of every ticket, though the
  issue's criterion says the diff touches only those two files. The criterion is read as covering code.

## Evidence
- `HerdTests.ThirtyPopulatedDays_StayCheap`, Release: passed, 6,845 MB allocated in 21.4 s, save 881 KB.
- Fast suite (`Category!=Slow`), Release: 2,053 passed, 0 failed, 2 skipped.

## Follow-ups
- `Phase2GateTests.ThirtyDays_Headless_CompletesAliveAndSmall` was not run: it fails on main before its memory
  check (the camper does not live 30 days), which the issue leaves to a separate session.
