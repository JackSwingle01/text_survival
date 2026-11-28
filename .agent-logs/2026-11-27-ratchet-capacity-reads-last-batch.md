# Ratchet: capacity-reads, the last eight test reads (#355)

**Hypothesis.** Each of the eight tests can read the faculties instead of the capacities and keep what it
protects, because every faculty is monotone in the capacities it reads (Speed in moving when unloaded,
Dexterity in manipulation and consciousness, Perception in sight, hearing and consciousness, Vision in
sight and consciousness, Strength in moving and manipulation). No sim code changes, so no behaviour can.
#332 (the blocker) has merged; the eight items are still on the list unchanged.

Revised: skipped the scout, design, adversarial-review and code-review sub-agents, as the #354 and #321
ratchets did: the change is eight test bodies, the list, and one helper, and the fast suite is the risk check.
Revised: LoweringAnyTissue reads strength, speed, dexterity, perception, perception in pitch dark (hearing
alone, so a tissue raising hearing while lowering sight still shows), vision and the hold on life. Renamed
`..._NeverRaisesAnyFaculty`.
Revised: Unconscious reads speed, dexterity and perception at zero, and `CrippledBy.Unconscious` for speed
and dexterity, so the zero is shown to come from the head, not the limbs.
Revised: Dirty compares a dirty and a clean baseline's five faculties (new `TestKit.FacultiesOf`); renamed
`..._TakesNothingOffAnyFaculty`. WellFed compares the same five before and after Well fed comes off.
Revised: EveryEffectKind reads `Body.Vitality`, which reads every faculty and so every row's deltas.
Revised: the pang compares against a fed twin lived the same minute (speed 0.94-0.96, dexterity 0.93-0.95,
vision 0.97-0.99 for consciousness, as sight is whole).
Revised: the no-cliff test walks dexterity, not speed: speed also moves with fat and muscle (the build), so it
would not isolate hunger. Measured along the path: largest 100 g step 0.0091 of dexterity against 0.0077 of
moving before. The bound went from 0.01 to 0.012, a point of moving's worth (hunger takes 0.35 manipulation
and 0.15 consciousness per severity: about 1.2 points of dexterity per point of moving).
Revised: `AnEntryTakenOffWhileItsReadRemains_Fails...` took `CapacityReadsToday[0]`, which an empty list
lacks; it now uses the breaching sample's listed read. No test was deleted: none only restated a formula.

- The ratchet's remaining command prints nothing.
- Class tests (CapacityReadTests, BodyTests, DirtyTests, SurvivalTests, Release): 174 passed.
- Fast suite (`Category!=Slow`, Release): 2223 passed, 0 failed, 2 skipped.

Follow-up: the list is empty, so `dev/ratchets.md`'s "When empty" step is due (delete the list, hide
`Capacities`, `GetCapacities`, `RegionCapacities` and `Effects.CapacityDeltas` from outside the body, rename
`Capacity` to `BodyFunction`, glossary entry). Not done here: the issue limits changes to its items.
