# Review findings merged unaddressed (#295, #304, #308), and the red PRs

## Hypothesis

A sweep of merged PRs #251-#308 for review threads and review bodies finds eleven inline findings; eight
were fixed before merge (#253, #281, six on #283). Three are still true on main:

- #295: `Fauna.FeedMinute` eats carcasses and bodies before the pile, with no `HeldKill` check, so a
  scavenger at Critical food on a predator-held kill, sent to `Feed` by a pile there, eats the kill.
  Fix: skip the carcass and body loops under `HeldKill`, as `MeatAt` already reads it.
- #304: `Sight.ViewOf` keys its kept view on tile, budget and season. At budget 0 a body that sees (its own
  tile) and one out cold (nothing) share an entry. Fix: `SightView.Sees` joins the key.
- #308: the put-in and bury checks refuse only a full cache; `Items.Move` moves whole pieces, so a piece
  bigger than the room left moves nothing while the verb is offered. Fix: compare the lightest piece with
  the room, as `PackRoomRefusal` does for takes; a bury with no snow cache yet reads the row's capacity.

Each gets a failing test first through the public seam (`Sight.InView`, `Work.Refusal`, `Sim.Advance`).

Red PRs: #309's `CapacityReadTests` scans TuneViewer.dll, which `tests.slnf` never builds because
TuneViewer is not in `text_survival.sln`. #305 and #309 also failed CI's 5 s limit on
`CombatFlightTests.NotCaught_...`, whose seed scan builds a world per seed from 150 to its hit at 224.
#297 fails `NpcNeedTests.AtNeed_ItDoesNotPace` only when merged with main: handed to its own agent.

## Log

- Revised: the #295 test at hunger 0.95 never reached Critical (Critical is `Priorities.Starving`: no
  calories and fat at the 3% floor), so the fox kept off and the test failed on the pile, not the carcass.
  With a starving fox it reproduced: 1 kg off the wolves' carcass in ten minutes.
- The seed scan starts at 224, its first hit on main today; the same change goes on #305's and #309's
  branches so their merges with main stay clean.
