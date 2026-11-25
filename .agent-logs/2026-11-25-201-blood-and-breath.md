# #201: Blood and breath feed the capacities, and zero means the part gives nothing

Hypothesis:
- Changes: `Sim/BodyTiers.cs` (`Capacities` holds five: moving, manipulation, consciousness, sight, hearing;
  `BloodAndBreath` holds blood and breath beside its circulation and short-breath scales; moving is the legs'
  mean times (0.9 + 0.1 × the rest's mean), manipulation the arms' mean times (0.9 + 0.1 × the mean of head,
  chest, abdomen and legs), a human's only; consciousness at zero zeroes moving and manipulation, the
  below-a-tenth cut goes; speed held at zero; `Alive` reads blood and breath from `BloodAndBreath`;
  digestion becomes `TissueReads.Digestion`, read by eating and healing only; `BodyReadOut` shows blood and
  breath from `BloodAndBreath`); `Sim/Body.cs` (the `Capacity` enum's `Breathing` and `BloodPumping` become
  `Breath` and `Blood`, same numbers); `Sim/Content/effects.json` and `names.json` (the four
  `BloodPumping` rows name `Blood`); `Sim/Content.Tables.cs` (an effect naming `Digestion` is a content
  problem); `Sim/Work.cs` (foraging's breathing slow-down goes: `Breathing` is no longer on the record).
- Affects: speed and dexterity of a body with no working leg or arm (0.10 and 0.10 today → 0); strength of
  the same (it reads moving and manipulation); every faculty but strength of an unconscious body (→ 0); a
  body whose consciousness is under a tenth but above zero (its limbs no longer cut to a tenth); speed
  under a load past 1.75 effective (negative today → 0); the walking refusal, `Fauna.Mobile` and combat's
  `IsDown`, whose thresholds stay but now see these numbers; foraging time with short breath; the combat
  goldens and kill-chance table only where a body is legless or unconscious.
- Stays green: the fast suite but the tests that assert the old numbers (`BodyTests`' destroyed-limb,
  unconscious and blood-pumping tests); the combat and herd goldens on whole bodies; `SaveFixtureTests`
  after the fixture is regenerated (the enum's names are in the format hash).
- First red: a new `FacultiesTests` case, both legs destroyed has speed at zero, fails on 0.10 × the build.

Revised (review): speed's build term and load term can both go negative, and their product is then positive (an absurd build gets faster under a pack) → each term is held at zero, not only the product.
Revised (review): `CapacityReadTests` counts any read of `TissueReads` or `BloodAndBreath` from outside the body → `ConservationCaloriesTests` reads the liver's and stomach's condition, and the BodyTests on lungs and the heart read faculties and life, so the list shrinks and gains nothing.
Revised (review): with the below-a-tenth cut gone, a body at consciousness 0.05 is down in combat (< 0.1) but mobile and can walk → kept, as the ticket says (the thresholds stay; #202 and #204 move them); pinned by a test and named in the PR.
Revised (review): digestion's slot still took effect deltas in `Fill` → `TissueReads.Digestion` reads the tissue with no effects, as `WithoutEffects` did, so eating and healing do not move; the `Capacity` enum's doc says it names more than the five.
Revised (review): a body with no legs fell back to the mean of every region → it has moving at zero, the rule "no working leg, no moving"; no body plan lacks legs.
Revised: the spec reviewers' seams are the spec's own (the body's faculties; the content checks) → used as they are; no seam added.
Follow-up: a chase floors running speed at a tenth of fresh (`Species.Running`, `MinSpeedPct`), so a legless or unconscious body still runs at 0.1 → the spec's "never computed for a body at zero" is #204's and #153's.
Revised: the enum's names are in the save's format hash, but the fixture holds the numbers, which stay → `SaveFixtureTests` passes with the fixture as it is; nothing regenerated.
Revised: changed pre-existing tests to the new rule. `BodyTests.DestroyedLegs_LeaveOnlyACrawl` → `DestroyedLegs_LeaveNoMoving` (moving 0, was under 0.15) and `DestroyedArms_LeaveAlmostNoManipulation` → `DestroyedArms_LeaveNoManipulation` (0, was under 0.15); `Unconscious_CannotMoveOrManipulate` (0, was under 0.1); `HealthyBody_AllCapacitiesNearPerfect` (the five, and the hold on life whole); `DestroyedHeart_StopsBloodPumping_AndKills` → `DestroyedHeart_Kills` (the hold on life 0); `ChestTrauma_ReducesBreathing_AndPumping` → `ChestTrauma_LoosensTheHoldOnLife` (`Body.TissueVitality` between 0 and 1); `DestroyedLung_HalvesBreathingRoughly` deleted (one lung is breath at a half, above where it fogs the head, so nothing a caller reads moves: `FacultiesTests.ADestroyedLung_FogsTheHead_OnceBreathIsBelowThreeTenths` says so); `LostOrgans_TakeTheirCapacityWithThem` (lungs and heart through the hold on life; the gut through eating, which absorbs half); `LoweringAnyTissue_NeverRaisesAnyCapacity` (the five and the hold on life); `DestroyedChest_NoBreath_NoConsciousness` (the hold on life 0); `ConservationCaloriesTests.Run` (a whole gut read from the abdomen's organs, not digestion); `CapacityReadTests.CapacityReadsToday` (four entries gone, six shorter or renamed: none added).
Revised: the kill-chance table measured again moved by up to 0.05 a cell (sabre-tooth on bison 0.10 → 0.13 alone), as an unconscious or legless animal now gives nothing → species.json's table is pasted again, as its comment asks.
Revised: the balance report's speed column is the chase's share (`Species.Running`, floored at 0.1), so both legs destroyed and half the blood still read 0.10 there while the faculty is 0 → kept; it is the follow-up above, and the PR says so.
