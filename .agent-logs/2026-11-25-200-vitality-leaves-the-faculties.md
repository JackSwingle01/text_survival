# #200: Vitality leaves the faculties

Hypothesis:
- Changes: `Sim/BodyTiers.cs` (Faculties drop vitality; dexterity reads consciousness; perception counts
  it once; Vitality becomes the mean of the four faculties' shares of a fresh body of the species, with
  no surroundings); `Sim/Body.cs` (`Body.Vitality(body, species)`); `Sim/Make.cs` and `Sim/Fire.cs` (the
  dazed odds terms and the dazed work slow-down go); `Sim/Fauna.cs` (the hunter's pick of the weakest
  reads vitality alone); vitality's callers in combat, the screen and the CLI pass the species;
  `BodyTierTests` (exception list empties); `CapacityReadTests` (the dazed reads come off).
- Affects: every faculty of a body with lowered blood, breath or consciousness (strength and speed rise,
  dexterity and perception move); making and fire-starting odds and times for a dazed body; combat's
  threat, morale, intimidation and blow size, which read vitality and so read the new mean; the
  screen's condition word; which animal of a herd a person hunts.
- Stays green: the fast suite but the tests that assert the old numbers; the combat goldens on fresh
  bodies (vitality was 1 there and stays 1).
- First red: `BodyTierTests.EachTier_ReadsOnlyTheTiersBelowIt_ButTheListedBreaches` once the list is
  empty: the five faculty reads of vitality; then new body-tier tests on a blow to the head.

Revised: the scout found `Fauna.Weakest` is a person's hunt of a herd, not a pack's (packs send their whole side) → the issue's "herd's predator" is that hunter; it orders by vitality alone, then id.
Revised: `BodyState` holds its kind and sex but not its species, and a fresh wolf and a fresh bear differ → `Body.Vitality` takes the species, and each caller passes the agent's species; a Species field on the body would change the save.
Revised (review): making's work rate kept a separate `Consciousness < 0.5` slow-down beside the odds term → it goes too, so its CapacityReadTests entry keeps only moving and manipulation.
Revised (review): a quadruped's "Head" took the human head's 2% of manipulation, so a fresh wolf's dexterity was 0.02, not 0, and a bitten head would cost it vitality → the 2% weights are a human body's only; a quadruped's manipulation is 0, as the spec says it is.
Revised (review): combat reads vitality (threat, courage, intimidation, boldness against a weak target, the blow's 70-100%) and the spec names only the hunter and the screen as its readers → combat keeps reading `Body.Vitality`, now the mean: keeping the old least-of under another name would be a new capacity read on a list that only shrinks, and how a fight reads a body is #153's. The drift is measured by the combat tests and noted.
Revised (review): an unconscious body's strength and speed still fall through the consciousness-below-0.1 cascade on moving and manipulation → kept: that cascade is a capacity rule, and #201/#206 own it; a blow to the head (brain at half) stays above 0.1, so strength and speed are whole.
Revised (review): fresh shares are cached → one table built in `Vitality`'s static initializer (thread-safe), each share clamped to 0-1 so a body better than fresh reads 1 and a negative speed reads 0; `SpeciesTable`'s own fresh strength and speed caches are left as they are.
Revised: tests may not read a capacity (CapacityReadTests), so the new faculty tests take consciousness from its organ (the brain at half is consciousness at half) rather than reading it.
Revised: changed pre-existing tests to the new rule. `BodyTests.Effects_DisableButNeverKill` (vitality 0 at consciousness 0 → perception 0: strength keeps its build), `BodyTests.Vitality_HealthyIsOne_AndConsciousnessEffectsDragItDown` (the playtest body's vitality under 0.20 → under 0.40, now 35%: the mean, not the least), `CombatStateTests.AWound_CostsMorale_ByVitalityAndLostBlood` (species passed, comment), `CombatGoldenTests.SpeciesBodies_AreAsMeasured` (every animal's strength × 0.80/0.804, the quadruped head's manipulation gone; blows unchanged), `CombatTests.AUnitIsNotItsOwnAlly` (the pack's other wolves were spawned at random sizes, so their vitality is 0.9998 → every member normalised), `MakeTests.Odds_NeverAddUpToMoreThanACertainty` (no dazed 1.2: the worst case now sums to exactly 1, so no Capped term is asserted), `OddsTests.FireOdds_AreTheOldNumbers_AndAddUp` and `FireOdds_AtTheExtremes_AreCappedAndSayWhy` (the Dazed term and its -0.2 gone), `FireTests.StartChance_MatchesOldTable` and `FireStartingTests.NoStartOdds_AreAbove95Pct` (the impaired argument gone), and the callers that pass a species now (`HerdBudgetTests`, `IndividualsTests`, `CombatGoldenTests.HerdBoldness*`, `CombatTests.SpottedSmallGame*`, `BalanceReport`).
Revised: with the dazed term gone, making's worst odds sum to exactly 1, so the "Capped" scaling is not reached by any loaded recipe → kept as the guard it is.
Revised: a re-measured kill-chance table moved a lone sabre-tooth's kills by half (caribou 0.47 → 0.18, bison 0.42 → 0.10, megaloceros 0.49 → 0.13); main measures the committed table exactly. A probe that switched every combat read of vitality back to the old least-of left them at 0.18 and 0.10 → the shift is the faculties' own (a dazed or breathless animal keeps its strength and speed), the change the spec asks for, not combat's reads; species.json's table is measured again and pasted, as its comment asks when combat changes.
Revised: the balance report printed NaN for a wolf's dexterity (0 of 0) → it prints "-", the faculty vitality leaves out.
Follow-up: lone predators kill far less often (the kill-chance table above); whether to retune sabre-tooth (or any predator) for it is a balance call for Jack, not this ticket's ("no effect's sizes change").
Follow-up: combat still reads vitality (threat, courage, intimidation, boldness, the blow's 70-100%, where strength now counts twice with StrengthPct); how a fight reads a body is #153's.
Follow-up: spawned bodies are not fresh bodies (animals 0.7-1.3 of table size, people with an autumn's fat), so an unhurt one's vitality is a hair under 1; vitality capping each share at 1 keeps a big one from reading above.
Follow-up: the NPC bench and the cohesion gate were not run (each over 30 minutes or nightly); compare the nightly numbers on the `baselines` branch after merge.
