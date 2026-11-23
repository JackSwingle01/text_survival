# Cost budgets are tripwires, not limits

The cost tests (`HerdTests.ThirtyPopulatedDays_StayCheap` and the phase-2 gate in `Phase2GateTests`: under
6000 MB allocated over 30 populated days, and a save under 1 MB) are there to catch an unexpected blow-up
and to call for a judgement. They are not design limits. The 1 MB save was the phase 1 exit criterion, a
round number for a save that loads at once; the 6000 MB came in when the herd test moved off the wall clock
(`aa34cbf`), at about three times the 2.1 GB a populated month allocated then. Crossing one means a person
decides whether the cost buys something the player sees, and if it does, the budget is re-baselined. An
agent that crosses one does not redesign a feature to get back under it, and does not re-baseline it
either. It reports the measurement and the choices as an open question in its PR.

**Considered Options:** budgets as hard limits a change must fit under; budgets as tripwires a person
rules on (chosen).
