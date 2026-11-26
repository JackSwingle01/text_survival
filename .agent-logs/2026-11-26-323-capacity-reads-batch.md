# #323: Ratchet capacity-reads, the next eight test reads

Hypothesis:
- Changes: `BodyTests` (seven members) and `EffectsPropertyTests.Readouts_AreSeverityScaledSumsOfTheTable` read faculties, `CrippledBy` and shares instead of capacities; their eight entries leave `CapacityReadsToday`.
- Affects: no sim code. Each test keeps what it protected: a whole human's faculties follow from its capacities as products (speed = moving x build, vision = sight x consciousness, perception = mean of sight and hearing x consciousness, dexterity = manipulation x (0.5 + 0.5 consciousness) x wet hands), so a capacity's change on an otherwise whole body shows one for one in a faculty's share.
- Stays green: the fast suite; `CapacityReadTests.OnlyTheBodyReadsACapacity` with the list shortened.
- First red: none expected beyond the list; the rule fails if an entry goes while its read stays.

Revised: unattended run, the work is eight test rewrites in two files I had already read whole → no scout or design-review sub-agent; the scouting is the reading of `BodyTiers.cs` (faculty formulas, `Capacities.Scaled`, `TissueReads.Fill`) and the previous batch's log (#205).
Revised: the previous batch restored `Readouts_AreSeverityScaledSumsOfTheTable` because its severity scaling, absent effects and saturation curve each show in a faculty → kept as a property over the same generated effect sequences, its oracle now a whole human's speed, dexterity, vision and perception from the summed table. Blood deltas (Hypothermia, Frostbite, Hyperthermia, Shock) show in no faculty or in life (life reads tissue without effects), and no effect names breath, so those columns are unchecked: no outcome differs. Mutants checked: DiscomfortMaxLoss 0.8 → 0.7, and severity dropped from the non-discomfort sum: both fail it.
Revised: `HalfDamagedArm_RoughlyHalvesItsManipulation` read one region's manipulation → both arms at half; dexterity's share falls into 0.2..0.7 (`HalfDamagedArms_RoughlyHalveDexterity`).
Revised: `LostOrgans`' shattered-bone check read one leg's region capacity (0.04) → both legs' bone at 0.2 leaves a speed share in 0.03..0.05 (0.039 measured; linear in bone it would be about 0.19).
Revised: "every capacity at about a fifth" → every faculty share at least 0.18: dexterity, a product of two lowered capacities, is 0.183 and vision 0.192 at full hunger, thirst and tiredness (probed).
Revised: `Effects_DisableButNeverKill`'s consciousness at zero → `CrippledBy(Perception)` is exactly `[Unconscious]`. `LostOrgans`' eyes → vision zero and `CrippledBy(Vision)` is `[NoWorkingEye]`; half a brain → vision 0.5.
Revised: renamed where the name said capacity: `HealthyBody_AllFacultiesNearAFreshBodys`, `Hypothermia_AtFull_TakesThreeTenthsOfSpeed`, `Discomforts_Saturate_ButDamageBloodLossAndColdStillTakeSpeedToNothing`.

Evidence:
- `dotnet test rebuild/Sim.Tests -c Release --filter "Category!=Slow"`: 2200 passed, 2 skipped.
- The ratchet's remaining command prints eight entries, none of this batch.

Revised (review): perception's share over 0.9 let hearing fall to about 0.82 unseen → the healthy test also holds perception in full darkness (hearing's half alone) above 0.45.
Revised (review): the 0.18 comment named dexterity alone; vision is 0.19 → it names both. `Left` read as a side of the body → `Remaining`.
Revised (review): the property test's oracle restates the faculty formulas as well as the effect table, so retuning a faculty breaks it → kept: comparing each faculty with and without each effect cannot tell summing from saturating without the same formulas, and the previous batch's review restored this test for exactly the scaling and the curve.
Revised (review): `Discomforts_StackWithDiminishingReturns` no longer holds hearing at a fifth on its own → kept: hearing reaches a caller only through perception, whose share is held at 0.18 or more; no test was deleted in this batch.

Follow-up: the Blood deltas of Hypothermia, Frostbite, Hyperthermia and Shock change no faculty and not life (life reads tissue without effects; the lost-blood scale reads BloodPct), so they do nothing in the sim. Pre-existing; for a person to decide whether they should.
