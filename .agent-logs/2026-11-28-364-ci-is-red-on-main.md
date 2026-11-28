# 364: CI is red on main

Hypothesis: main's `rebuild quick` failed only its duration gate. `CoreTests.SameSeedAndActions_GiveIdenticalWorld`
runs two full worlds (every animal) for 1,620 minutes each and took 5.9 s alone on CI's runner, over the 5 s
limit. Since #334 (tiredness is a sleep debt) the player is no longer stopped by exhaustion inside those
steps, so they run their full length. `IndividualsTests.APackHunt_DrawsItsSide_FromTheFamiliarWolvesOnTheTile`
was flagged only under load and took 2.4 s alone, so it passes the gate as it is. Fix: tag the
determinism test LongRun, as a63e5480 did for `SignsAt_AgreesWithTheFindDump`. It keeps running on every
PR, in the long-run check. Behavior stays the same; no test is skipped or loosened.

Revised: no scout, design-review or code-review sub-agents. The change is one attribute line that copies
a63e5480, and CLAUDE.md names this exact fix for a quick test over 5 s.
Revised: did not make the test quicker with an animal-free world. That would stop it checking that
animals are deterministic, which is part of what it covers.

Evidence: CoreTests+IndividualsTests pass (28). CI's quick step run locally (`Category!=Slow&Category!=LongRun`,
then the duration check, then a serial re-run of flagged tests) passes: 2222 Sim, 51 Host and 40 Tune tests.
One test was flagged under load (BodyTierTests, 7.4 s) and took 1.7 s alone. The fast suite
(`Category!=Slow`) passes: 2278 Sim tests.

Follow-up: the long-run check gets about 6 s more. Watch that it stays under two minutes.
