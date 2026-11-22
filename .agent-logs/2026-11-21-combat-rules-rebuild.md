# combat rules rebuild (stages 1-3): stage 1, species bodies and the energy model

Hypothesis:
- Changes: species.json (build, motion, intake, cave bear and Homotherium recast), Species.cs (Motion, sex overlay),
  Content.Tables.cs species row (minimal: new fields), Body/BodySurvival (Kleiber for quadrupeds, calorie store
  scaling with mass, sprint reserve stored as spent level, refill over a mass-derived recovery time, kcal spending),
  Combat (dodge and flee draw on the reserve, bursts and movement charge kcal, allowance scales with body speed and load),
  Fauna (herd hunger derived from member calories; Kleiber burn; grazing intake x forage left; meat fills the store).
- Affects: every human body (68 kg / 0.12 / 0.38 man), NPC food gates, Phase 2 camper, herd goldens, combat goldens.
- Stays green: everything outside herds, combat and survival numbers.
- First red: a new SpeciesMotion test fails on SpeciesStats having no Motion.
Revised: editing Content.Tables.cs (the tuning agent's) was denied by the permission system → species.json keeps its schema (only values and the recast change); running, intake and the woman's build sit in an interim table in Species.cs (SpeciesTable.MotionRows), merged into SpeciesStats at load. The loader patch is for the orchestrator to land.
Revised: the baseline man made Body.Speed's reference build move → Speed keeps the old 75/0.15/0.30 reference as named constants, so the formula is unchanged; only the default body is new.
Revised: old-parity goldens (GoldenTests.BodyProcess, ItemsTests.Speed_UnderLoad, TravelTests crossing parity and injury, ExposureTests.FactorOneTile, CombatGolden shove/dodge and speared blow) built the default body → they now build TestKit.OldBaselineHuman(), the body the old game was measured on.
Revised: tests that pinned the default body's numbers (BodyTests composition and speed, TravelTests crossing 10→8 and 6→4, TracksTests 7→6, NpcTests target fat 9→7 kg) → re-pinned on the 68 kg man; the travel speed-up is the new body's Speed (0.849 → 0.99).
Revised: HerdMind.Hunger removed (derived: Fauna.Hunger) → herd tests set hunger through TestKit.SetHunger and check burn in kcal (Fauna.BurnKcalPerMinute) instead of +0.003 a minute.
Revised: CombatTests flee cost 20 energy-minutes → empties the reserve, 5 energy-minutes and its calories.
Follow-up: Sim.cs Surroundings charges Fight at activity factor 2.0; with fights now paying their own work it should be 1.0 (one line, not this agent's file).
Follow-up: NPC FatFloorPct 0.10 is just under the new man's 0.12 baseline fat: NPCs start near their food Need line.
Revised: CombatGoldenTests species bodies, speared blow and shove/dodge pinned the old AnimalFactory bodies → re-pinned on the species round's builds, old → new in the test's comment; SpeciesBodies_MatchOld renamed SpeciesBodies_AreAsMeasured.
Revised: CombatGoldenTests headless outcomes re-pinned (wolf vs spear Victory 0.14 → 0.33: the realistic wolf's Body.Speed, and with it DodgeChance, fell).
Revised: grazing intake scaled by the forage left (research §5.5) sent the camper's herds wandering (tracks on 293 → 518 tiles, save 912 → 1,019 KB, over the gate's 1 MB) → intake is flat, as the decision states it; the forage factor is a later option. The reserve snaps to full below 1e-4 spent so it stops being saved and ticked.
Revised: ContentTests anchors moved with the data (wolf weightKg 40 → 45, human 75 → 68).
Revised: recovery time is derived (a person's 6 minutes x (mass/68)^0.25, research §5.3's own rule), not a per-species column.
Revised (review): animal feeding capped at the store's room, so herd fat only ever fell → room includes fat lost below the species' build; each grazer eats its own intake to its room; raids capped at room.
Revised (review): Homotherium had 0 intake and starved in its Grazing state → 15 kcal/min small game (D), as the old solitary forage fed any species.
Revised (review): LastFedMinute set on grazing that ate nothing → only when something was eaten. Hunger counts only living members.
Revised (review): Cover charged inside Move, so a shoved or dodging fighter paid to be moved → Walk pays for voluntary steps only.
Revised (review): SpendKcal re-implemented starvation's burn order → one ReserveBurn shared by both; MinSpeedPct clamped once, in SpeciesTable.Running; Body.SprintUntilSpent for Flee; MetabolismKcalPerDay throws for a body with no organs; Hunger computed once per decision.
Follow-up: the cave bear recast is data only: its temperament is read by Combat.Boldness, which nothing in production calls until map-level engagement (MVP 2); in a fight it uses its row's boldness 1.8 and threat 3.0 anywhere. Keeping it near its den is a herd-movement change for that step or stage 2.
Follow-up: Npc.cs eats to Body.MaxCaloriesKcal, the baseline man's store; right for every NPC today (all 68 kg men), wrong for a woman.
Follow-up: DodgeChance and ShoveOdds read absolute Body.Speed (B11): the realistic mammoth's Speed doubled (0.62 → 1.23). Stage 2's defence roll should read Running instead. Herd travel (Travel.HerdCrossingMinutes) reads it too: wolves cross tiles about a third slower.
Revised: Sim.cs Fight activity factor 2.0 → 1.0 (cleared by the orchestrator), with a test that a waiting round pays basal once.

# stage 1b: running data into species.json; people start well fed for the autumn
Hypothesis:
- Changes: species.json (running columns, autumnFatPct, female), Content.Tables.cs (row, ToStats, checks), Species.cs (no switch; NewBody draws a person's fat), Body.cs (BaselineHuman reads the row), World.New and Npc.Spawn (bodies from SpeciesTable.NewBody on the person's own Birth stream), Tune specs.
- Affects: every person's starting fat (15-18%), NPC food behaviour, the 16-seed bench.
- Stays green: everything else; the save shape (no version bump).
- First red: ThePlayerAndEveryNpc_StartWellFed fails on the player at 12% fat.
Revised: the NPC food scenario tests (ShortOfFat, FoodSearch_PricesTheWalk, WhichHerdsALoneNpcHunts) set fat against a target clamped at 12% of weight, written for a lean spawn → TestKit.Lean puts their NPC back on the 12% man with a matching target.
Revised: HerdTests chunking gate replaced the player's body in one of its two runs only → both runs now.
Revised: CombatTests.Field and SpeciesBodyTests' fight put the player on the reference man, not the seed's autumn draw, so the combat pins hold.
Revised: ContentTests' human-build row tested the check this stage deletes → rows for the new running and autumn-fat checks.
Follow-up: NPC fat targets and Tiers' food tier are clamped at Body.BaselineFatPct (12%) of weight, so a well-fed NPC starts above its own target; that is NPC tuning, left alone.
Follow-up: bench seed 5 dies of the cold on day 4.7 (it died of thirst on day 14.6 before stage 1, the cold on day 19.6 after).
Revised: StalkingAFeedingAnimal bounds (feeding 0.5-0.9 → 0.8-0.97, alert ≤0.25 → ≤0.45): alertness now fades between notices (round 2).

# stage 2: the fighter state machine (combat rounds 1, 2, 6), hunt activity, targets, the round observer
Hypothesis:
- Changes: CombatAI.cs rewritten (FighterState, margin bands, alertness, priority lists, targets, morale off the target's state, round-6 pressure), Combat.cs (state on units, Turn and PickUp, defence roll, shove by mass x Running, throw drops on the grid, flee anywhere), CombatAftermath (dropped weapons), Npc.RespondToThreat (WouldStand), Fauna (Hunt for people), CombatObserver.cs, Web host targets, tuner policies and bear bands.
- Affects: every fight's outcome; the survey; CombatGoldenTests steering goldens (deleted with the steering).
- Stays green: everything outside combat.
- First red: CombatStateTests.Standoff_PressesFromPlusPoint2 fails on NextState missing.
Revised: CombatGoldenTests' old-Unit steering goldens (LoneWolfAndHuman, PackOfThree, LoneAnimals, HurtWolf) pinned MovementVector/WantsToAttack/PursuitTarget → deleted with the steering; the bands are pinned in CombatStateTests.
Revised: CombatTests on edge fleeing, pursuit vectors, PursuitTarget, Block and guards, the zone action lists → rewritten on states (flee anywhere, Intimidate at Near, no Block/Dodge).
Revised: naming another target with any action was refused → it spends the round turning (the decided cost), so scripted players that name the nearest each round still progress.
Revised: the NPC margin against a bear at 8 m is -0.1: it stands its ground (Standoff), which this stage counts as standing; it runs from a cave bear.
Revised: CombatGolden headless outcomes re-pinned (old → new in the test): the AI person gives ground and is worn down by a lone wolf (defeat 0.42 → 0.74), and runs at once from a bear.
Follow-up: the AI person giving ground only backs away (Q6's block is now automatic); whether it should strike back in reach is a balance question for the tuner and the user.
Revised (review): Pressure read the stored state, so an unconscious enemy still pressed → StateOf.
Revised (review): an ambushed player who acted stayed unaware → acting at all engages them; keeping still does not.
Revised (review): AI moves skipped Refusal and Wander ignored the allowance → every AI action, Move and Turn too, goes through Apply/Refusal and throws if refused; steps are cut to the allowance (WithinAllowance); Wait is open in every state (a cornered runner).
Revised (review): the chase instinct paid on every re-entry to Fleeing → once per fighter (CombatUnit.HasRun).
Revised (review): WouldStand restated Margin's terms → Courage and PerceivedThreat(threat, distance) shared with the fight's margin; an empty herd throws.
Revised (review): TargetOf hid a bad index as "no target" → throws. Alertness renamed AlertnessLevel / NoticeAlertnessDelta / FadeAlertnessDelta.
Revised (review): fading ignored step size, and a wary animal never came to look → the fade shrinks as the mover covers more than a stalking step; a wary animal steps nearer 15% of rounds.
Revised (review): a dodge or shove at the edge took a fighter off the grid → knocked-to cells are kept on the grid.
Revised (review): host NearestM held the faced target's distance → TargetM; UnitDto shows Combat.StateOf as State (main.js reads both).
Revised: ProgressTests asserted headless fights unwatchable → watchable now through Combat.Watch, with a headless replay checked.
Revised: SpeciesBodyTests' dodge tests set DodgeSet → they pass on the automatic defence.
Revised: NPCs bring their nature (Boldness - 0.5) into their fights as BoldnessDelta, as the margin they decide by does.
Revised: Throwing is 20 m / 0.70 for every weapon until the weapon table (round 4); the Stone axe's 25 m / 0.75 went with the name rule.
Revised: combat-bands.json gains the round-6 bear rows, scored, with bear_den (the temperament's defending boldness) for the +15 rows.
Revised: HerdLongRunSeed5 found a sated hyena flipping Feeding → Patrolling → Feeding on a carcass for days (Patrolling counts as wanting to move) → a scavenger with hunger under 0.1 lies up (Resting) instead.
Revised (stage 2b): rebased onto 05ca917 (Dto and save-format.md by hand); giving ground with the enemy in reach a fighter strikes at its species' strikeBackMargin or above (species.json, -0.2 to start, tunable in specs/combat.json for human, wolf and bear); the sleeping-fighter check waits for the body patch's Body.IsAsleep (stage 3).

# stage 3: flight and catch-up (combat round 3)
Hypothesis:
- Changes: CombatFlight.cs (CatchOdds, Flight, Chase, GetAway, BreakOffs, Drift), BodyMotion.SprintPlan, Combat (Flee and Chase actions, PendingFlight, the edge holding, FledHeading/FledTiles), CombatAI (Fleeing = Flee; a person runs only under 50% caught), CombatAftermath (agents drift on their heading; WoundedEscaped.Heading), species chaseLimitSeconds, host ChaseDto, tuner spec.
- Affects: every flight; hunts (a noticed caribou is gone at once); people against bears and packs.
- First red: CombatFlightTests.APerson_NeverCatchesAHealthyCaribou fails on CatchOdds missing.
Revised: break-off from every enemy at 30 m broke a hunt open at 34 m before the stalk began → only once an enemy is aware of the fight.
Revised: a flight's drift stopped at the first mountain → it swerves left then right of its heading, and stops boxed in.
Revised: state tests that kept the enemy away with boldness -100 now made it run and leave → -1.2, giving ground or standing off.
Revised: HerdTests' pack gate pinned the kill to the starting tile → the chase can end a tile or more away; the wounded-escape gate ran its caribou off the grid's edge → it Flees and drifts on its heading.
Revised: CombatGolden headless outcomes re-pinned (bear vs spear: fled 1.00 → defeat 0.97: a person gives ground to what would run it down).
Follow-up: people who escape or fail a chase stay on the tile (only animal agents drift); the 60 s person's chase fits inside a round, no extra time charged.
Revised (review): a fleer within 1 m was always caught, and caught it re-fled every round → no early return (the gain decides); a runner run down is RunDown for the rest of the round: no defence and no second flight.
Revised (review): a failed player chase counted as the player fleeing and lost the trail; chasers cost their side AllyFled → Gone(chasing) skips the cascade and Result reads a player who left chasing after a runner that got away as AnimalFled (so WoundedEscaped carries the heading).
Revised (review): the aftermath's drift compared units with members after the dead and the split-off left → the herds that ran off whole are read first.
Revised (review): odds only at three breakpoints missed the sqrt fade's interior maxima, and Seconds was the best breakpoint → the gain is checked every second to the chase limit; Seconds is when the gap closes.
Revised (review): the chase instinct could not start a chase → enemies facing the runner rerun their bands before the pursuers are read.
Revised (review): only the best pursuer rolled → one roll against 1 - the product of misses; the first to close catches; caught, all stop then; not caught, each runs its own limit.
Revised (review): no time was charged → a chase past its round adds its minutes to Encounter.Rounds (what headless callers charge).
Revised (review): the flight roll used the combat stream → its own "flight" stream. SpeedSpreadPct → SpeedSpreadSd, WorstCatch → BestCatchPct.
Revised (review): flight tests leaned on the code → the research §5.4 worked example (100 m: a long, even chase), a faster fleer at contact, a spent runner, the player's chase both ways with its trail, a pending chase through a save, a person running from what it can outrun.
Follow-up: people who escape, or fail a chase, stay on their tile (animals drift); moving the player needs Knowledge's arrival updates; a decision for the coordinator.
Follow-up: the player's chase longer than a round is charged to the fight's Rounds, not to the player's step.

## Stage 3 close-out (2026-11-21)
- House rules applied: stripped round/stage/date history from comments; the "breakpoints" wording now matches the 1 s scan.
- Save format 4 -> 5 (PendingFlight, FledHeading, FledTiles, LeftChasing, RunDown, WoundedEscaped.Heading); fixture regenerated, save-format.md updated.
- Survey vs stage 2b: people no longer flee what would run them down (person-fled ~0 in wolf/bear scenarios, deaths up under ai/cautious policies); caribou hunts' kills fall 84-97% -> 29-36% for melee because caribou outrun people; wolf hunts of caribou unchanged at 100%.
- Decision flagged: people who get away stay on their tile; only animals drift.
- Pending: Body.IsAsleep check, once the body patch lands.
