# Feature-Parity Checklist

*The Phase 5 gate in [clean-room-architecture.md](clean-room-architecture.md) is "feature-parity
checklist done; old project retired". This is that checklist. Every player-facing feature and every
simulation behaviour of the old game (root `text_survival.csproj`) is listed, with where it stands in
the rebuild (`rebuild/Sim`, `rebuild/Cli`).*

**Snapshot:** 2026-11-20, branch `rebuild-phase4-overnight` at `c74533e` plus the uncommitted phase 4
work in the tree. The feature list comes from the old code (handlers, work strategies, runners,
recipes, features, effects, body, combat, NPCs, herds, items, Desktop UI, save/load, settings), not
from the docs. Every "done" claim was checked by grepping `rebuild/`; the test named is the one that
covers it. Tests were not run for this snapshot, because another agent is editing `rebuild/`.

> **Since 2026-11-22: Done means the player's experience matches the old game,** not only that the
> Sim has the rule. Porting follows `dev/active/porting.md` (the old game is the default; anything
> simplified is flagged for the user) and the decision records in `documentation/decisions/`. Rows
> the parity audit (`dev/active/parity-audit-items.json`) found marked Done but reduced or changed now
> say **Reduced** or **Changed**. The summary counts below predate that correction, and the rest of
> the checklist has not been re-audited.

## Legend

| Status | Meaning |
|---|---|
| **Done** | `rebuild/Sim` has it, a test covers it, and the CLI can drive it where the player would. |
| **Core, no FE** | `rebuild/Sim` has it and a test covers it, but no front end reaches it: the CLI has no command for it and there is no desktop UI yet. |
| **Partial** | Some of it exists; the row says what is missing. |
| **Missing** | Not in the rebuild. |
| **Replaced** | A plan or emergent-world-design decision supersedes it. The row cites the decision and says whether the replacement exists yet. |
| **Cut** | Dropped on purpose (the plan's Keep/Port/Cut table, or a recorded decision). |
| **Reduced** | Marked Done before 2026-11-22, but the player gets less than the old game gave (parity audit). The row says what is missing. |
| **Changed** | Marked Done before 2026-11-22, but it works differently from the old game for the player, and no decision record or user choice made that change, unless the row cites one. |

Sizes for Missing and Partial rows: **S** is under a day, **M** is a few days, **L** is a week or more.

The **desktop front end** is missing as a whole (Phase 5 has not started). So every Done row also
still needs desktop UI. That work is listed once, in [§26 Desktop front end](#26-desktop-front-end),
and not repeated on every row.

---

## Summary

| # | System | Done | Core, no FE | Partial | Missing | Replaced | Cut | Total |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | Core loop, saves, run outcome | 6 | 0 | 1 | 1 | 0 | 1 | 9 |
| 2 | Fire and torches | 10 | 0 | 2 | 3 | 0 | 1 | 16 |
| 3 | Survival simulation | 10 | 0 | 0 | 0 | 0 | 0 | 10 |
| 4 | Body and abilities | 5 | 0 | 0 | 1 | 0 | 1 | 7 |
| 5 | Effects | 5 | 0 | 0 | 4 | 3 | 0 | 12 |
| 6 | Treatment and medicine | 0 | 0 | 0 | 4 | 0 | 0 | 4 |
| 7 | Food, water and cooking | 4 | 0 | 1 | 2 | 0 | 0 | 7 |
| 8 | Curing and preservation | 0 | 0 | 0 | 2 | 0 | 0 | 2 |
| 9 | Items, inventory and gear | 6 | 0 | 2 | 1 | 0 | 0 | 9 |
| 10 | Crafting | 1 | 0 | 0 | 13 | 0 | 0 | 14 |
| 11 | Gathering work | 5 | 0 | 1 | 0 | 0 | 1 | 7 |
| 12 | Discovery and knowledge | 3 | 0 | 0 | 3 | 1 | 0 | 7 |
| 13 | Shelter, bedding and camp | 8 | 0 | 2 | 0 | 0 | 0 | 10 |
| 14 | Storage and caches | 2 | 0 | 1 | 0 | 0 | 0 | 3 |
| 15 | Map, locations and world generation | 6 | 0 | 2 | 0 | 0 | 1 | 9 |
| 16 | Travel | 6 | 0 | 0 | 2 | 1 | 0 | 9 |
| 17 | Ground surface | 4 | 0 | 0 | 1 | 0 | 0 | 5 |
| 18 | Tracks and trails | 3 | 1 | 0 | 1 | 0 | 0 | 5 |
| 19 | Weather and calendar | 4 | 0 | 0 | 0 | 0 | 0 | 4 |
| 20 | Herds and fauna | 6 | 0 | 1 | 1 | 0 | 1 | 9 |
| 21 | Hunting and combat | 0 | 6 | 1 | 1 | 0 | 0 | 8 |
| 22 | Small game, trapping and fishing | 0 | 0 | 0 | 6 | 0 | 1 | 7 |
| 23 | Megafauna | 1 | 0 | 0 | 0 | 1 | 0 | 2 |
| 24 | NPCs and companions | 0 | 4 | 1 | 6 | 0 | 0 | 11 |
| 25 | Events, tensions and their replacement | 0 | 0 | 0 | 4 | 9 | 1 | 14 |
| 26 | Desktop front end | 1 | 0 | 0 | 26 | 2 | 0 | 29 |
| 27 | Tooling and retirement | 2 | 0 | 1 | 3 | 2 | 0 | 8 |
| | **Total** | **98** | **11** | **16** | **85** | **19** | **8** | **237** |

### What blocks retiring the old project, ranked

Ranked by how much of the game is unplayable without it, then by how much else waits on it.

1. **The desktop front end (§26), L, 26 rows.** Nothing player-facing has a UI beyond a debug
   CLI. It needs the async driver over `Sim.Step`, the renderers ported onto the rebuild's data, and
   every screen in `dev/active/ui-design.md`. Depends on: the knowledge model (#4) for a map drawn
   from beliefs, and chance functions that return their factors (short-loop "Asks") for the odds
   sheet.
2. **Crafting (§10), L.** This is the biggest simulation hole. Without it there are no tools,
   weapons, clothing, torches, snares, nets, bags, tents, bandages, teas, rope, tallow or mending,
   and the mammoth and bear gear in `Items.GearKinds` cannot be obtained. Most of §6, §8 and §22
   waits on it. Recipes should land as data (plan decision 6).
3. **Predators never come for the player (§21, §20), M.** `Fauna.cs:47`: "Herds never come for the
   player or an NPC yet". With no stalking, no ambushes and no encounters started by animals, the
   world has no danger, and meat, blood and scent (`Bloody`, carcass `Scent`) have no consequence.
   The emergent design's replacement for Stalked/Hunted is exactly this: a pack mind that picks you
   as prey.
4. **The knowledge model (§12), M.** There is no fog of war, explored versus visible tiles, sightings
   or discovery log (`Map.cs:114`). The front end draws the player's beliefs (plan decision 7;
   ui-design gate), so §26 cannot finish without it.
5. **Moments: the replacement for events (§25), L.** Tensions and authored events are cut, but
   emergent steps 2 to 4 (substrate, kill chain, affordances and moments) are not built. Until they
   are, the rebuild has *zero* events. That is the old playtest's top complaint ("Nothing happened")
   made total.
6. **Treatment and medicine (§6), M.** Frostbite, Shock and Bloody can start at a severity that
   needs treatment and then stop decaying at a floor (`Effects.cs`), but nothing treats them. A
   frostbitten player stays impaired forever. Infection and fever do not exist.
7. **Small game, snares and fishing (§22), M + M.** These are the old day-1 to day-3 food loop and
   the short-loop plan's "reason to head out each morning". Depends on crafting (snares, rod, net)
   and on ice holes for winter fishing.
8. **NPCs in the world (§24), M.** The NPC AI passes its 14-day gate, but `World.New` never spawns
   one (only tests call `Npc.Spawn`). There are no companions, no NPC bodies to find, loot or bury,
   and nothing writes relationship memory.
9. **Curing and preservation (§8), S to M.** There is no dried food and no cured hide, which leaves
   no route to clothing even once crafting exists.
10. **Old tools that reference `text_survival.csproj` (§27), S.** `tools/GameCli`, `HudPreview`,
    `UiPreview`, `NpcSim` and `TerrainPreview` all reference the old project. Each must be retired or
    ported before the old project can go.
11. **Smaller gaps, each S:** skills; seasonal edges; burial and digging; trail cutting; resin
    waterproofing; ember carriers; fire pit upgrades; remote caches; washing off blood; honey's
    energy; climb edges.

### Biggest missing areas

| Area | Rows missing | What is there today |
|---|---:|---|
| Desktop front end | 26 | A debug CLI (`rebuild/Cli/Program.cs`); the reference prototype `dev/prototypes/ui/` |
| Crafting | 13 | `Make` (`Make.cs`, `recipes.json`), its Camp recipes: bedding, branch and log frames, snow shelter, pitching a tent |
| Small game, trapping, fishing | 6 | Rabbit and Fox bodies (`Species.cs`) and `SiteSmallGame` carried as data, all unused |
| NPCs and companions | 6 | The AI (`Npc.cs`) and its tests; never placed in a real game |
| Effects (illness, injury, mood) | 4 + 3 replaced | The 14 body-produced effects (`Effects.cs`) |
| Treatment and medicine | 4 | Ten medicine resources that nothing uses |
| Event replacement (moments) | 4 | Find dump and distribution report only (emergent step 1, `Report.cs`) |
| Knowledge and discovery log | 3 | Hidden-pool search hours (`Agent.cs` `Knowledge`) |
| Curing | 2 | `DriedMeat`, `DriedFish`, `DriedBerries`, `CuredHide` resources, with no way to make them |

---

## 1. Core loop, saves, run outcome

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Time passes per activity; every agent lives every minute | `GameContext.Update`, `Pacing.PassTime` | Done | `Sim.Step`, `Scheduler.Minute` (`Sim.cs`); `CoreTests.Wait_DrainsAndAdvancesClock`, `HerdTests.EveryBody_LivesEveryMinuteOnce_*` | | |
| Deterministic, headless simulation | (none; old used the process RNG in places) | Done | Seeded stream per system (`Rng.cs`, `World.Rng`); `CoreTests.SameSeedAndActions_GiveIdenticalWorld`, `Phase2GateTests.ThirtyDays_SameSeedAndActions_SameWorld` | | |
| New game with the starting kit | `GameContext.CreateNewGame`, `Persistence/GameInitializer.cs` | Done | `World.New`, `Items.StartingPack/StartingClothes`; `ItemsTests.StartingKit_MatchesOld_CloWeightAndLoad` | | |
| Save and load | `Core/SaveManager.cs`, `Persistence/SaveManager.cs` | Changed | Death offers "Load your save" and there is no autosave; the old game deleted the save on death (decision "Death is final"). Sim: `Save.cs` (the World as JSON, terrain regenerated); CLI `save`/`load`; `CoreTests.SaveRoundTrips_AndResumesRngExactly`, `Phase3GateTests.MixedRun_SavedAndLoadedEveryFewSteps_IsByteIdenticalToAStraightRun` | | |
| Death ends the run | `GameRunner.HandleDeath` | Done | `Map.Outcome` returns `Died`; `CoreTests.BleedingOut_EmitsDiedAndInterrupts_ThenTheDeadCannotAct`, `CrossingTests.Outcome_IsDerived_SurvivingDeadOrAcross` | | |
| Victory by crossing the pass | `GameRunner.HandleVictory`, `Location.IsCrossingExit` | Done | `Map.Outcome` returns `Crossed`; `Sim.Step` refuses the player afterwards; `CrossingTests.Crossed_TheRunIsOver`, `TravelTests.ReachingTheFarSide_CrossesThePass` | | |
| Typed fact stream instead of notices and narrative plumbing | `ctx.Notices`, `NarrativeLog`, `RelationshipEvents` | Partial | `WorldEvent.cs` exists and every step returns events. Missing: the short-loop "Asks": not every event carries a tile (`FuelAdded` has none), and nothing consumes the stream yet (no log, no notices, no memory) | S | |
| Tables as data files | `LocationFactory`, `NeedCraftingSystem`, `RewardGenerator` in code | Done | Twelve files under `Content/`, one per table family: locations, loot, items (resources, gear, slot coverage, starting kit), fire (fuels, pits), species (rows, temperaments, diets, sightings), effects, shelters (kinds, tents, materials, recipes, beddings), butchering, terrain (terrains, caves, substrates, edges), forage, discoveries (finds, details, draws), accidents. `Content.Parse` checks them strictly; `ContentTests.AllContent_LoadsAndEveryReferenceResolves` and the `Loader_*_InEveryFile` theories. Kept as code: the camp tile, felling yields, carcass composition, body plans, the climate model (`rebuild-deferred.md`) | | crafting recipes should be born as data |
| Web-mode session IDs | `SaveManager` `sessionId`, `Dockerfile`, `package.json` | Cut | A leftover of the web version; the target is the desktop app (plan Phase 5) | | |

## 2. Fire and torches

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Two-mass fuel model, fire phases, temperature | `HeatSourceFeature` | Done | `Fire.cs`; `FireTests.UnburnedFuel_CatchesOverTime`, `GoldenTests.Fire_MatchesOldTrajectory` | | |
| Typed fuels (pine, birch, oak, bone, peat, tinders) | `FuelType.cs` | Done | `Fire.Props`; `FireTests.Hardwood_NeedsAHotFire`, `ItemsTests.EveryResource_HasARow_AndFuelsAreResources` | | |
| Start a fire: tool, tinder, kindling; chance by tool and tinder | `FireHandler.AttemptStartFire` | Reduced | The player cannot choose the tool or the tinder; a fixed 0.05 kg a strike gives the starting kit three strikes, not four. Sim: `Fire.Start`; CLI `fire`; `FireTests.StartChance_MatchesOldTable`, `ItemsTests.StartFire_SpendsTinderEachTry_AndKindlingOnlyWhenItCatches` | | |
| Fire chance reads the firecraft skill | `FireHandler.CalculateFireChance(skillLevel)` | Partial | Dexterity and consciousness are now passed (`Fire.cs` `Start`); skill is not (no skills exist, §4). The short-loop "Ask" (chance returns its factors) is not done | S | skills |
| Add fuel, temperature-gated, capped by the pit | `FireHandler.AddFuel`, `TendFire` | Done | `Fire.Feed`; CLI `fuel`; `FireTests.AddFuel_IsCappedByPitCapacity`, `StepRulesTests.StartFire_OnAFullPit_IsRefused_AndLightsNothing` | | |
| Embers, and relighting from embers | `StartFromEmber`, ember phase | Done | `FireTests.Embers_DecayThenDie_AndLeaveCharcoal`, `FireTests.AddFuel_ToEmbers_Relights` | | |
| Fire warms the tile and the body; lazy catch-up while away | `HeatSourceFeature`, `Location.GetTemperature` | Done | `Fire.WarmthFor`, `Fire.CatchUp`; `FireTests.UnattendedFire_CatchesUpLazily_WithEventsAtTheRightMinute`, `FireBodyTests.RestingByAFire_StaysWarmerThanRestingWithout` | | |
| Charcoal left by a dead fire | `FireHandler.CollectCharcoal` | Done | `GatherCharcoal`; CLI `charcoal`; `ItemsTests.GatherCharcoal_PutsWhatADeadFireLeftInThePack` | | |
| Fire-starter tools wear out | `Gear.Use` | Reduced | Every fire-starter row has `maxDurability: -1`, so crafted drills and strikers never wear out (old: 5/15/20/30 uses). Sim: `Items.Use` in `Fire.Start`; covered by `ItemsTests.StartFire_*` | | |
| Pit types: open, mound, stone | `HeatSourceFeature` pit types | Reduced | The types exist, but no mound or stone pit can be built; every fire is an Open pit. Sim: `PitType`, wind and efficiency (`Content/fire.json` pits); `FireTests.StonePit_BurnsSlowerThanOpen` | | |
| Upgrading a pit costs a project | `FirePitUpgradeProject`, "Mound/Stone Fire Pit (Project)" recipes | Partial | `StartFire` takes any `PitType` for free on a new pit. Missing: the mound and stone projects (materials, time) | S | crafting |
| Torch: light from a fire, carried heat and light, burns out | `TorchHandler.LightTorch`, `UpdateTorchBurnTime` | Done | `Fire.Torch`; CLI `torch`; `FireTests.Torch_LightsFromFire_WarmsItsCarrier_AndBurnsOutInAnHour`; a resin torch burns twice as long (#150), `FireTests.AResinTorch_BurnsTwiceAsLongAsATorch`. As a system of its own it is Cut (below) | | |
| Torch chains onto a fresh one automatically | `TorchHandler` chaining | Missing | Lighting a new torch from the one in hand works by hand; there is no automatic chain when one gutters away from a fire | S | |
| Put out a torch | `TorchHandler.ExtinguishTorch` | Missing | No activity for it | S | |
| Carry an ember (amadou or polypore ember carrier); collect an ember | `FireHandler.CollectEmber`, ember-carrier recipes | Missing | Nothing carries fire between tiles except a torch | S | crafting |
| `TorchHandler` as its own system | `TorchHandler.cs` | Cut | Plan Keep/Port/Cut: "a torch is a carried heat-and-light source in Fire" | | |

## 3. Survival simulation

| Feature | Old source | Status | Evidence | Size | Depends on |
|---|---|---|---|---|---|
| Energy, hydration, calories each minute | `SurvivalProcessor` | Done | `BodySurvival.cs`; `SurvivalTests.AnHourAwake_SpendsEnergyWaterAndFood`, `GoldenTests.BodyProcess_MatchesOld` | | |
| BMR metabolism from body composition | `SurvivalProcessor` | Done | `SurvivalTests.Metabolism_IsKatchMcArdleOnLeanMass` | | |
| Sleep restores energy; rest does not | `CampHandler` sleep | Reduced | Sleep is a fixed 8 h with Stop; the old choice of 1-8 hours is gone (its fire warning stays out, decision "The UI shows what the game knows"). Sim: CLI `sleep`/`rest`; `SurvivalTests.Sleep_RestoresEnergy_BurnsLess_AndLosesSeventyPercentWater`, `CoreTests.Rest_DoesNotRestoreEnergy_OnlySleepDoes`, `CoreTests.Sleep_StopsWhenFullyRested` | | |
| Starvation: fat, then muscle, then organs; food past full becomes fat | `SurvivalProcessor` | Done | `SurvivalTests.Starvation_ThenMuscle_ThenOrgans`, `SurvivalTests.Eat_PastAFullStore_BecomesFat_NothingLost` (old bug fixed) | | |
| Dehydration kills | `SurvivalProcessor` | Done | `SurvivalTests.Dehydration_KillsWithinAnHourOrSo` | | |
| Heat transfer: clothing clo, wind chill, sun, shelter, fire | `SurvivalProcessor`, `Location.GetTemperature` | Done | `SurvivalTests.Cold_CoolsYou_AndInsulationSlowsIt`, `GoldenTests.EffectiveHeatOnAnExposedTile_MatchesOld` | | |
| Shivering, hypothermia, frostbite, hyperthermia, sweating | `SurvivalProcessor`, `EffectFactory` | Done | `SurvivalTests.BitterCold_Naked_ShiversThenHypothermiaThenFrostbite`, `SurvivalTests.RealOverheating_SpendsWater` | | |
| Wetness: precipitation and ground contact, drying by fire, freezing below zero, lost insulation | `SurvivalContext`, wetness effect | Reduced | Clothing waterproofing is never read: the garments' base levels do nothing. Sim: `SurvivalTests.SixHoursInRain_MakesYouWet`, `WetClothes_FreezeBelowZero_OnlyAFireDriesThem`, `Wetness_RobsClothingOfInsulation`; `GroundTests.CrossingAFootOfWaterSoaksYouFarMoreThanWalkingOnDampGround` | | |
| Regeneration when fed, watered and rested; blood regrows | `SurvivalProcessor` | Done | `SurvivalTests.FedWateredAndRested_Heals_AndRegrowsBlood`, `SleepHeals_FasterThanRegenerationAlone` | | |
| Activity profiles (metabolic factor, fire proximity, stationary) | `Config/ActivityConfig.cs` | Done | `Sim.Surroundings` (the event multiplier is gone with events); covered across `WorkTests`, `ShelterTests` | | |

## 4. Body and abilities

| Feature | Old source | Status | Evidence | Size | Depends on |
|---|---|---|---|---|---|
| Regions, tissues, organs; human and quadruped plans | `Bodies/Body.cs`, `BodyPartFactory` | Done | `Body.cs`; `BodyTests.BodyPlans_HumanHasSevenRegions_QuadrupedFourLegs` | | |
| Capacities from tissue and organ condition | `CapacityCalculator` | Done | `BodyTests.DestroyedLung_HalvesBreathingRoughly`, `HalfDamagedArm_RoughlyHalvesItsManipulation` | | |
| Blood and circulation | `Blood.cs` | Done | `BodyTests.BloodLoss_CollapsesCirculationAtHalf`, `SurvivalTests.Bleeding_DrainsBloodUntilDeath` | | |
| Abilities: vitality, strength, speed, perception, dexterity, cold resistance | `AbilityCalculator`, `AbilityContext` | Done | `BodyTests.Speed_HeavyLoadSlows`, `Perception_FullInDaylight_HearingCarriesItInTheDark`, `Dexterity_DarknessAndWetHandsHurt`, `ColdResistance_FatInsulatesWithDiminishingReturns` | | |
| Damage: layered penetration, targeting, armour | `DamageCalculator`, `BodyTargetResolver` | Done | `BodyTests.Damage_*`, `Clothing_ReducesBluntByCushioning_ButNotInternal` | | |
| Skills (Fighting, Hunting, Foraging, Firecraft, Crafting, Healing, Mending...) | `Skills/Skill.cs`, `SkillRegistry.cs` | Missing | No skills anywhere in `rebuild/`. The short-loop plan asks for skills on `Agent` during the rebuild, with the old free first level-up fixed. The combat skill bonus is skipped for now (`dev/active/rebuild-combat-balance.md`) | M | |
| Serpentine, arachnid and flying bodies | `BodyPartFactory` | Cut | Plan Keep/Port/Cut | | |

## 5. Effects

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Hungry, Thirsty, Tired from low stats | `EffectFactory` | Done | `Effects.cs`; `SurvivalTests.LowStats_RaiseHungryThirstyTired` | | |
| Temperature effects (Shivering, Hypothermia, Frostbite, Hyperthermia, Sweating) | `EffectFactory` | Done | `SurvivalTests.Shivering_StartsWhereConstrictionRunsOut`, `SurvivalTests.Heat_MakesYouSweat` | | |
| Wet, Bloody | `EffectFactory.Wet/Bloody` | Done | `SurvivalTests.Drying_ToZero_RemovesWet`, `StepRulesTests.BriefBleeding_LeavesATinyBloody_NotTheTreatmentFloor` | | |
| Damage-triggered Bleeding, Pain, Dazed, Shock | `EffectFactory`, `DamageCalculator` | Done | `BodyTests.Damage_Cut_TriggersBleedingAndPain`, `SurvivalTests.Shock_NeedsTreatmentOnlyWhenSevere` | | |
| Effects decay; treatment-required ones stop at a floor | `EffectRegistry` | Done | `SurvivalTests.Effects_DecayAndClear_ButTreatmentOnesStopAtAFloor` | | |
| Illness: Fever, Inflamed (wound infection), Nauseous, Coughing | `EffectFactory` and the Wound/Infection and Fever arcs | Missing | The design keeps these as real body effects ("WoundUntreated, FeverRising → body effects", emergent-world-design §1), so they are not covered by cutting events. They need an infection model on open wounds | M | treatment (§6) |
| Physical impairment: SprainedAnkle, Sore, Stiff, Clumsy, Exhausted (effect) | `EffectFactory` (applied by events and accident variants) | Missing | Travel injuries do tissue damage only. A sprain from a quick crossing or a fall is the obvious carrier | S | |
| Burn | `EffectFactory.Burn` (camp events) | Missing | Nothing in the rebuild burns you; natural home is a fire mishap or cooking | S | moments |
| Buffs from consumption: Energized (honey), Nourished (rose hip tea) | `ConsumptionHandler.EatHoney`, tea recipes | Missing | `Honey` is food with calories only | S | crafting (teas) |
| Fear, Shaken, Paranoid | `EffectFactory` (events, tensions) | Replaced | emergent-world-design §1: "Disturbed, Shaken → fear effects on your body". Not yet built for people (herds have learned fear, `HerdMind.Fear`) | | moments |
| Event buffs: Warmed, Rested, Focused, Hardened | `EffectFactory` (event outcomes) | Replaced | Outcome templates and events are cut (plan Keep/Port/Cut); warmth and rest are the real body state | | |
| "Cold" exposure outcome | `EffectFactory.Cold` (events) | Replaced | Real cold comes from the heat balance; the event-only cold hit goes with outcome templates (plan Keep/Port/Cut) | | |

## 6. Treatment and medicine

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Treat with raw medicine (moss, polypore, resin, usnea, amadou, chaga, willow bark, juniper, pine needles, rose hip) | `TreatmentHandler.DirectTreatments` | Missing | The ten medicine resources exist (`Items.cs`) and forage pools yield them; nothing consumes them | M | |
| Crafted treatments: four teas, two poultices, fiber wrap, hide bandage, resin seal, sealed bandage | `NeedCraftingSystem` Treatment, `TreatmentHandler` | Missing | | M | crafting |
| Anything that clears a treatment floor (frostbite, severe shock, bloody) | `TreatmentHandler` | Missing | `Effects.Add` sets `RequiresTreatment`; no activity lowers it. Frostbite never heals today. Bloody also needs washing (§7) | S | the two rows above |
| Treat wounds as a camp action | `CampAction.TreatWounds` | Missing | Comes with the rows above as an activity | S | |

## 7. Food, water and cooking

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Eat any food: calories and water per kg | `ConsumptionHandler` | Done | `Work` `Eat`; CLI `eat`; `WorkTests.Eating_TakesTenMinutes_AndPutsItsCaloriesAndWaterIntoTheBody` | | |
| Drink from the pack, or from a river or lake | `ConsumptionHandler`, `WaterFeature` | Changed | Drinking straight from a river or lake is new and easier; the old game drank only from the pack. Sim: CLI `drink`; `WorkTests.Drinking_UsesThePacksWaterFirst_AtMostALitre_AndRefusesWhenThereIsNone`, `ARiverOrLake_GivesWaterWithoutAPack` | | |
| Cold water cools an overheated body | `ConsumptionHandler` | Done | `Work.Begin` (`Work.cs:202`); covered by the drink test | | |
| Cook meat and fish at a burning fire | `CookingHandler.CookMeat/CookFish` | Changed | Put on the fire and taken later, not cooked by hand (spec 2026-11-24 fire-and-rack-as-stations); `StationTests` | | |
| Melt snow | `CookingHandler.MeltSnow` | Partial | Set melting at the fire and the water taken (spec 2026-11-24 fire-and-rack-as-stations); `StationTests`. Missing: it ignores the real snow depth, so it works in July, as the old one did | S | |
| Wash off blood | `ConsumptionHandler` `wash_blood` | Missing | Bloody only decays at its floor | S | |
| Honey's energy boost | `ConsumptionHandler.EatHoney` | Missing | See §5 | S | |

## 8. Curing and preservation

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Curing rack: raw meat to dried meat, fish to dried fish, berries to dried berries, scraped hide to cured hide, with weight lost in drying | `CuringRackFeature`, `CuringRackHandler`, `CampAction.CuringRack` | Missing | The dried and cured resources exist with no way to make them. Natural fit: a fire-or-tile job in the short-loop "workbench" (feature 6) | M | crafting (the rack recipe) |
| Food spoilage of stored meat | (old carcass decay only) | Missing | Carcass decay is ported (§21); meat in a pack or stash never spoils. Not old behaviour either, so not a parity blocker; noted because preservation has no reason without it | S | |

## 9. Items, inventory and gear

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Resources by weight; gear instances with durability | `Inventory`, `Gear` | Changed | One weight per resource lost the pieces and their counts; to be restored as discrete or continuous (decision "Resources are discrete or continuous"). Sim: `Items.cs`; `ItemsTests.EveryResource_HasARow_AndFuelsAreResources` | | |
| Carrying capacity, accessories, encumbrance slows travel | `Inventory.MaxWeightKg`, `AbilityContext` | Done | `ItemsTests.Accessories_StackCapacity_AndTheirWeightCounts`, `Speed_UnderLoad_MatchesOld`, `AHeavyPack_SlowsTheCrossing` | | |
| A full pack drops the rest on the ground | `InventoryCapacityHelper`, `GroundItemsFeature` | Reduced | Resources only: gear on a stash can never be picked up, so dropped or thrown gear is lost. Sim: `Items.Receive`; `ItemsTests.Receive_FillsThePack_ThenDropsTheRestOnTheTile` | | |
| Clothing by slot, area-weighted clo, worn gear loses warmth | `Inventory.SlotCoverage`, `Gear.CloValue` | Reduced | The rule exists, but nothing ever wears clothing, so condition never falls. Sim: `ItemsTests.Clothing_IsAreaWeighted_NotSummed`, `WornGear_LosesWeightAndWarmthWithCondition_AsOld` | | |
| Put on a garment | `TransferHandler.TransferEquipment` | Done | `Wear`; CLI `wear`; `ItemsTests.Wear_PutsOnAGarment_AndThePackTakesWhatItReplaced` | | |
| Tools and weapons wear with use | `Gear.Use` | Done | `Items.Use/UseFor`; `WorkTests.Chopping_InOneStepOrInQuarterHours_CostsTheAxeTheSame`, combat weapon use in `CombatTests` | | |
| Take a garment off without a replacement | `InventoryOverlay` unequip | Partial | `Wear` only swaps. Missing: an unequip (for drying a layer, or shedding heat) | S | |
| Gear table complete for the old item set | `Gear.cs` factories | Partial | `Items.GearKinds` has 44 kinds. Missing kinds the old recipes make: sharp rock, crude edge, stone, shale and flint knives (flint knife present), knapping stone, snares, fishing rod and net, ember carriers, treatment items | S | crafting (land together) |
| Resin waterproofing of clothing | `MaintenanceHandler.ApplyWaterproofing`, `Gear.ResinTreatmentDurability` | Missing | `WaterproofLevel` exists on gear; no treatment raises it | S | |

## 10. Crafting

The old `NeedCraftingSystem` has about 60 recipes in 12 need categories, plus the need-based
browser (`CraftingOverlay`), effort and odds (`CraftingEffort`, `CraftEvaluation`) and recipe
unlocking through the discovery log. The rebuild has the Make recipes in `recipes.json`, camp builds among them
(the `Recipe` record there says "full crafting ... is not ported").

| Feature | Old recipes or source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Camp structures by recipe: makeshift, padded and moss bedding, sleeping bag, branch frame, log frame, snow shelter | CampInfrastructure | Reduced | The snow shelter needs a shovel, and no shovel can be made or found, so it never happens in play. Sim: the Camp recipes in `Content/recipes.json`, made by `Make`; CLI `build <recipe>`; `ShelterTests.BranchFrame_TakesItsSticksAndHalfAnHour`, `SnowShelter_IsDugWithAShovel_InTheCold`, `SleepingBag_WarmsTheSleeper` | | |
| Crafting engine: recipe table as data, materials, time, tool needs, odds | `NeedCraftingSystem`, `CraftOption`, `CraftingHandler`, `CraftingEffort` | Missing | Everything below waits on this. Short-loop feature 8 (crafting luck) and the odds sheet build on it | L | data tables (§1) |
| Fire starting: hand drill, bow drill, flint striker, pyrite strike-a-light, tinder bundle | FireStarting | Replaced | Spec "Recipes that make sense", Fire (#144): hand and bow drills, a flint-and-pyrite strike-a-light made by feel, resin tinder (40 g tinder + 10 g pine resin); no flint-and-amadou striker and no tinder bundle (FIR-5 reversed). The replacement exists. Sim: the FireStarting recipes in `Content/recipes.json`, catch bonuses in `Content/fire.json`; `FireStartingTests` | | engine |
| Cutting tools: crude edge, sharp rock, stone, bone, shale and flint knives, stone axe, bone shovel, knapping stone, bone needle | CuttingTool | Done (#145) | The crude edge stands for the sharp rock, the stone for the knapping stone; flint, obsidian and bone knives, the ground stone and flint axes, the bone shovel and needle. No shale or stone knife (spec 2026-11-24 recipes-that-make-sense) | S | engine |
| Hunting weapons: wooden, heavy, stone-tipped and ivory-tipped spears | HuntingWeapon | Done (#147) | A staff, a wooden spear sharpened and fire-hardened in one recipe, flint-, obsidian- and ivory-tipped spears and a stone club. No crude or heavy spear; the flint point stands for the stone one (spec 2026-11-24 recipes-that-make-sense). Sim: `SpearsAndClubTests` | S | engine |
| Trapping: simple and reinforced snares | Trapping | Missing | | S | engine, §22 |
| Fishing: rod, net | Fishing | Missing | | S | engine, §22 |
| Processing: scrape hide, render fat, process willow bark, rope | Processing | Partial | Scraping (0.8 of the hide, with a blade) and drying by a fire (0.9 of the scraped hide) are made, for a mammoth hide too, and a cured hide cuts into two or three hide strips (#146): `MakeTests.AHide_ScrapesAndDries_ToAShareOfItsWeight`, `ACuredHide_CutsIntoTwoOrThreeStrips`, `TheThirdStrip_ComesAboutHalfTheTime`. Raw fat renders by hand at a fire to 0.6 of its weight in tallow (the fire works it as a station in #189), willow bark gives 0.8 of its weight in fibre, and 300 g of fibre makes a rope (#150): `MakeTests.EachRecipe_IsMadeFromWhatItLists_AndRefusedWithoutItsToolOrFire`, `AHalfKiloOfRawFat_RendersToThreeHundredGramsOfTallow` | S | engine |
| Treatment: teas, poultices, wraps, bandages, resin seal | Treatment | Missing | See §6 | S | engine, §6 |
| Clothing: hide gloves, cap, wrap, leggings, boots; mammoth hide coat and hood | Equipment | Replaced | Spec "Recipes that make sense", Clothing (#146): gloves and cap from two hide strips, boots, wrap and leggings from cured hides, each with sinew and a needle in the light; the coat and hood from a cured mammoth hide, never a raw one (the old coat took three raw ones). Each garment weighs no more than its lightest inputs, so the coat is 1.35 kg, not 4. Sim: the Clothing recipes in `Content/recipes.json`; `MakeTests.EachGarment_IsSewnFromWhatItLists_AndNeedsANeedle`, `AMammothHide_IsCuredBeforeItIsSewn` | | |
| Lighting: simple, birch bark and resin torches; amadou and polypore ember carriers | Lighting | Partial | A torch (a stick and birch bark or tinder) and a resin torch that burns twice as long (#150): `MakeTests.TwoTorchRecipes_MakeTwoKindsOfTorch`. Missing: ember carriers | S | engine |
| Carrying: small pouch, rope belt, proper belt, large bag, mammoth hide pack | Carrying | Done | A birch-bark pouch, rope belt, hide belt (the old proper belt), large bag and mammoth hide pack, each with its tool, weighing no more than their lightest inputs (#150): `MakeTests.EachRecipe_IsMadeFromWhatItLists_AndRefusedWithoutItsToolOrFire`, `ItemsTests.EachCarryingThing_AddsItsCapacity`, `TheCarryingThings_Stack` | S | engine |
| Camp projects not yet ported: curing rack, mound and stone pits, hide and mammoth hide tents, rebuilding a branch frame into a log frame, reclaiming an abandoned project | CampInfrastructure, `CraftingProjectFeature` | Missing | `Shelter.cs:79-83` records the rebuild and reclaim gap; tents can be pitched but not made | M | engine |
| Mending: boots, gloves, cap, chest wrap, leggings | Mending | Replaced | Spec "Recipes that make sense", Repairs (#148): a repair recipe works on one held piece and restores a share of its missing condition. Mend any hide or fur garment with a hide strip and a sinew, a needle and light, 15 minutes (20 for a wrap), 80% of the missing condition (the old mend gave half the maximum); sharpen any blade that wears with something for striking, 10 minutes, 50%. The piece keeps its parts record and the strip and sinew join it. Sim: `mend`, `mend_wrap`, `sharpen` in `Content/recipes.json`; `RepairTests` | | |

## 11. Gathering work

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Forage by density, with focus, depletion and regrowth | `ForageStrategy`, `ForageFeature`, `ForageDensity` | Reduced | The page always forages General for 60 min; no focus, time choice, clues, quality word, live finds or luck line; focus rule changed (`FocusGain` 1.3). Sim: `Gather` on a `ForagePool`; CLI `forage`; `PoolsTests.Forage_YieldsAndThinsLikeTheOld`, `WorkTests.AFocusedSearch_FindsMoreOfWhatItLooksFor_ThanAGeneralOne` | | |
| Harvest a visible stock, tool-gated, with regrowth | `HarvestStrategy`, `HarvestableFeature` | Done | `StockPool` Harvest; CLI `harvest`; `WorkTests.Harvesting_TakesAUnitACycle_UntilPickedClean`, `AHarvestThatNeedsATool_IsRefusedWithoutIt` (old bug fixed) | | |
| Chop: progress persists, trees fell and regrow, needs an axe | `ChoppingStrategy`, `WoodedAreaFeature` | Done | `TreePool`; CLI `chop`; `WorkTests.Chopping_NeedsAnAxe_PersistsAcrossSteps_AndStopsWhenTheTreeFalls`, `PoolsTests.ALimitedStand_ClearsAndRegrowsOneTreeAWeek` | | |
| Salvage: a loot table rolled once | `SalvageStrategy`, `SalvageFeature`, `RewardGenerator` | Done | `StockPool` Salvage, `Content/loot.json`; CLI `salvage`; `WorkTests.Salvage_ComesAllAtOnce_GearIncluded`, `PoolsTests.ASalvage_IsRolledOnce_FromTheSeedAndTile` | | |
| Darkness blocks work; impairment slows it | `WorkRunner.CheckDarknessBlocking`, `GetWorkImpairments` | Done | `Work.TooDarkToWork`; `WorkTests.AtNight_OnlyForagingWorksWithoutLight`, `ImpairedChopping_IsSlower_AndInterruptedChoppingKeepsItsProgress` | | |
| Examine environmental details | `ExamineStrategy`, `EnvironmentalDetail` | Partial | Details that yield something are `StockKind.Detail` pools (`Content/discoveries.json` details). Missing: the examine-only ones (tracks, scat, burrows), which told a story only; they become signs (emergent §3.2) | S | substrate (§25) |
| Harvest, forage, chop, salvage, detail as separate feature classes | five feature classes | Cut | Plan Keep/Port/Cut "Merge": one pool on a tile that work draws from | | |

## 12. Discovery and knowledge

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Hidden features revealed by perception-weighted search hours | `HiddenFeature`, `DiscoveryGenerator` | Done | `Knowledge.SearchHours`, `Pools.Search`; `PoolsTests.Discoveries_SpawnAndRevealLikeTheOldPools`, `AHiddenPool_CannotBeWorkedUntilFound_AndOnlyItsFinderKnowsIt` | | |
| Natural shelters found by searching | `DiscoveryFeatureFactory` | Done | `ShelterFind`; `PoolsTests.HiddenShelter_IsPlacedWhenFound` | | |
| Snow over a find slows the search, earned effort kept | `HiddenFeature.EffectiveSearchHours` | Done | `PoolsTests.Snow_SlowsTheSearch_ButKeepsEffortAlreadySpent`, `GroundTests.BurialReducesNewSearchProgressToAFloorButNeverToNothing` | | |
| Tile visibility: unexplored, explored, visible; sight rays | `TileVisibility`, `Perception/Sight.cs`, `GameMap.UpdateVisibility` | Missing | `Map.cs:114`: "No tile visibility / fog of war yet". Belongs in the agent knowledge model (plan decision 7) | M | |
| Map memory: what a tile held when last seen | (old: implicit in explored state) | Missing | The short-loop "Ask" scopes it (feature 4, place memory). Needed for the ui-design gate test (the fire shows as last seen) | M | the row above |
| Discovery log (locations, beasts, provisions, medicine, works) and recipe unlocking | `Discovery/DiscoveryLog.cs`, `DiscoveryLogOverlay` | Missing | | S | crafting (unlocks), front end |
| Discovery events placed in the world (abandoned camps, frozen travellers, caches) | `EventTriggerFeature`, `DiscoveryEventFactory` | Replaced | emergent-world-design §6 (the settling pass produces these from the real sim), leftovers in `authored-content-catalog.md`. Not built: emergent step 3 | | settling pass (§25) |

## 13. Shelter, bedding and camp

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Shelters: insulation, overhead, wind; natural and built | `ShelterFeature` | Done | `Shelter.cs`; `ShelterTests.ShelterStep_MatchesOld`, `WindCoverage_CutsTheChillOnce_NotTheBodysWindToo` | | |
| Improve a shelter with materials | `ShelterImprovementStrategy` | Done | `ImproveShelter`; CLI `improve`; `ShelterTests.Improve_UsesAPieceInTenMinutes_WithDiminishingReturns` | | |
| Snow shelters melt in warm air | `ShelterFeature` (stub in the old game) | Done | `Shelters.Melt`; `ShelterTests.SnowShelter_MeltsByTheHour_HoweverOftenItIsLookedAt` (rate invented, overnight review) | | |
| Shelter damage and repair | `ShelterFeature.Damage/Repair` | Changed | `Shelters.Repair` has no caller; Improve stands in for repair. Sim: `Shelters.Damage/Repair` (`Shelter.cs:211`); only melting calls Damage, since the old callers were events | | |
| Bedding: sleep quality, ground protection, warmth | `BeddingFeature` | Done | `ShelterTests.CampBedding_SleepsAtFullQuality_AndKeepsTheGroundOff`, `BetterBedding_HealsSleepFaster` (old bug fixed: warmth now read) | | |
| Multi-session building projects | `CraftingProjectFeature`, `CraftingProjectStrategy` | Done | `Project`; `ShelterTests.Project_PersistsAcrossSteps_AndBlocksAnotherBuild` | | |
| Tent: pitch anywhere, pack up, keep improvements | `CampHandler.DeployTent/PackTent` | Reduced | No tent can be made or found, so none can be pitched in play. Sim: `Build(Tent)`, `PackTent`; CLI `build tent`, `pack`; `ShelterTests.Tent_PitchesAndPacks_ThroughASave`, `Tent_KeepsItsImprovements_WhenPackedAndPitchedAgain` | | |
| The starting camp: overhang and sleeping area | `GridWorldGenerator.CreateCampLocation` | Done | `World.New`; `PoolsTests.SiteShelters_StandFromTheStart_CampKeepsOneOverhang` | | |
| Make camp elsewhere (moves "camp") | `CampHandler.MakeCamp`, `GameContext.EstablishCamp` | Partial | Makeshift bedding can be built anywhere (`Build(MakeshiftBedding)`), and storage is any tile's stash. Missing: the player's notion of *which* tile is camp, which "return to camp", the camp receipt and the UI's camp marker all read | S | knowledge model |
| NPCs decide to build or improve shelter | `NPCActions` building | Partial | NPCs use a shelter and bedding where they sleep, but never build or improve one (overnight review, phase 4b decision) | S | |


## 14. Storage and caches

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Camp storage (unlimited, at camp) | `CacheFeature.CreateCampCache`, `CampAction.Storage` | Changed | Any tile's stash is unlimited camp storage; the old camp cache was 1000 kg and only at camp. Sim: Any tile's stash (`Items.StashAt`); CLI `stash`/`take`; `ItemsTests.StashAndUnstash_MoveThingsBetweenPackAndTile_AndSurviveASave` | | |
| Drop and pick up on the ground | `GroundStashStrategy`, `GroundItemsFeature` | Reduced | No choice of amount: the whole stack moves (old: a chosen count, Drop 1, Drop All). Sim: Same stash; `WorkTests.AnEmptiedStash_IsGone` | | |
| Remote caches with capacity, predator, weather and food protection | `CacheFeature`, `CacheStrategy` | Partial | Named sites carry `SiteCache` as data (`Content.cs`), unused. The stash is unlimited and unprotected by decision (`Shelter.cs:81`). Raids are an emergent-table row ("Pack or bear digs out a stash with meat") | S | predators acting on stashes |

## 15. Map, locations and world generation

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Seeded grid, terrain, rivers, caves, edges | `GridWorldGenerator`, `GameMap` | Done | `MapGen.cs`, `Map.cs`; `MapTests.Generate_MatchesOldGeneratorExactly`, `Generate_CavesEdgesAndRiversMatchOldExactly` | | |
| Named places (93 sites) with their own ground and features | `LocationFactory`, `CaveLocationFactory` | Done | `Content/locations.json`; `MapTests.Generate_NamedPlacesMatchOldExactly`, `PoolsTests.EverySite_PutsItsForageFirst_ItsVisibleStockInSight_AndKeepsTheTerrainsHiddenFinds` | | |
| The mountain crossing: six stages, the far side ends the run | `GenerateMountainRange`, `PlacePassLocations` | Done | `CrossingTests.Stages_StandWhereTheOldOnesDid_WithTheirOldGround`, `MapTests.Generate_ReachableExitAndNoBypassAroundThePass` | | |
| Caves are dark; cave floors connect only to their mouth | `CaveLocationFactory` | Done | `MapTests.Blocked_CaveFloorConnectsOnlyToItsOwnCaveAndMouth`, `Work.TooDarkToWork` | | |
| Location properties: hazard, wind, cover, visibility, temperature | `Location` | Done | `MapTests.ANamedSite_SetsItsTilesGroundAndAir`, `TileGround_MatchesOldTerrainLocation` | | |
| Site discovery and first-visit text | `LocationDef.DiscoveryText` | Done | `Map.DiscoveryText`, CLI `look` | | |
| Climb edges on sites (6 min a side) | `WithEdgesOnAllSides(Climb)` | Partial | Carried as data, not applied (`Content.cs` note); the old map never read them either. A decision, then S | S | |
| Escape terrain and vantage points | `Location` escape/vantage | Partial | Carried on `LocationDef`, unused until combat escape terrain and scouting read them | S | |
| Old 96x96 north-wall map | `GridWorldGenerator` (documentation/overview.md) | Cut | The 224x82 valley with its eastern pass corridor was kept (overnight review, phase 4a decision) | | |

## 16. Travel

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Move N/S/E/W; crossing minutes from terrain and speed | `TravelRunner`, `TravelProcessor` | Done | `Traversal` (`Travel.cs`); CLI `n/s/e/w`; `TravelTests.CrossingMinutesAndRisk_MatchOld` | | |
| Hazardous ground: quick at injury risk or careful and slower | `TravelProcessor`, hazard prompt | Done | `Pace`; CLI `careful`; `TravelTests.QuickHazardousCrossings_InjureAtTheOldRate_CarefulNever`, `InjuryRisk_RisesWithImpairedMovement` | | |
| Travel injury | `TravelHandler.ApplyTravelInjury` | Reduced | Every fall reads "You hurt your leg."; the accident lines and the sprain effects are not ported. Sim: `Traversal` injures; `TravelTests.EachSegmentsInjury_ComesFromItsOwnTilesPools` | | |
| Impassable terrain and blocking edges (cliff, ravine) | `GameMap.CanMoveTo`, `IsEdgeBlocked` | Done | `TravelTests.ImpassableMove_IsRefusedAtNoCost`, `MapTests.Blocked_ByEdgeOfWorldImpassableTerrainAndBarriersBothWays` | | |
| Cannot walk with a broken body | `TravelRunner` | Done | `TravelTests.Travel_IsRefusedWhenTheBodyCannotWalk` | | |
| A half crossing can be continued or abandoned | (old: event continue/stop prompt) | Done | `TravelTests.Travel_StaysOnTheOriginUntilTheCrossingCompletes_AndCanBeContinued` | | |
| Seasonal edges (rivers flooding in spring, frozen crossings) | `TileEdge.RequiredSeason`, `IsBlockedIn(season)` | Missing | `Map.cs:77`: "No seasons yet: the old River could flood in spring" | S | |
| Trail marking and cutting by the player (blazes, cairns, cleared paths) | `TrailMarkingStrategy` | Missing | `EdgeType.TrailMarker` and `CutTrail` exist with their minutes (`Content/terrain.json` edges); nothing makes them | S | |
| Travel interrupted by events | `TravelRunner` event interruption | Replaced | Events become moments (emergent-world-design §7); `Step` already returns `Interrupted(reason)` | | moments |

## 17. Ground surface

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Snow layers, melt, refreeze, soaking, ponding, the water ledger | `GroundSurface`, `SurfaceLayer`, `SubstrateProfile` | Done | `Ground.cs`; `GroundTests.Golden_LayerStateAndQueriesMatchTheOldGroundSurface`, `Golden_LedgerBalancesInEveryScriptedState` | | |
| Surface slows travel and adds footing hazard | `TraversalFactor`, `HazardDelta` | Done | `GroundTests.DeepSnowSlowsTravelAndDeepWaterSlowsItFurther`, `IcedOverStandingWater_IsAFootingHazard_ButNotBesideARiver` | | |
| Contact wetting from the ground | `GetContactWettingRate` | Done | `GroundTests.StandingWater_WetsWhoeverStandsInIt_UnderARoofToo` | | |
| Lazy catch-up instead of quarter-hour batches | `GameMap.AdvanceWorld` | Done | `GroundTests.CatchUp_IsTheSameHoweverOftenTheTileIsLookedAt`, `LazySubSteps_StayCloseToPerMinuteStepping` (plan decision 3) | | |
| Burial access and digging up (`Dig up X`, shovel doubles, holes refill) | `GroundSurface.Excavation`, `DigUpStrategy`, `SurfaceAccess` | Missing | `Ground.cs:208`: not ported; needs per-placement elevation, footprint and hole depth. Emergent `Access.Refusal` is the planned gate | M | substrate (§25) |

(Route-local snow packing was deferred in the old game too, so it is not a parity item.)

## 18. Tracks and trails

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Footprints by maker shape, traffic, heading, erosion clock | `TrackRegistry` | Done | `Tracks.cs`; `TracksTests.ScriptedPassages_ReadAsOld`, `Erosion_After30SeededDays_MatchesTheOldAccumulator`. Known bug carried over: traffic fades only when re-walked (`dev/active/rebuild-track-traffic-fade-bug.md`) | | |
| Desire paths: wear tiers that speed travel and decay | `TrailWear` | Reduced | Not drawn on the map, so a Trace does nothing the player can see. Sim: `TracksTests.TrailTimelines_MatchOld`, `WalkingARouteOften_MakesItFaster`, `AbandonedPath_IsGoneInAboutAMonth_AsOld` | | |
| Herds and NPCs mark the ground through one seam | `GameMap.RecordMove` | Done | `Tracks.RecordMove`; `TracksTests.AHerdAgent_StampsAllItsMembers` | | |
| Reading prints on a tile (maker, heading, age) | `TilePopup.RenderTracks`, `TrailSignSelector` | Core, no FE | `Tracks.Read` exists and is tested (`TracksTests.Traffic_SummarizesDirectionAsANetVector`); the CLI `look` does not show prints | | |
| Prints shown within a Perception-scaled radius | `TrackRenderer` | Missing | Front end plus a perception radius rule | S | front end |

## 19. Weather and calendar

| Feature | Old source | Status | Evidence | Size | Depends on |
|---|---|---|---|---|---|
| Fronts, cold snaps, grace period, a year's statistics | `Weather`, `WeatherFront`, `FrontPatterns` | Reduced | The front is never shown (old top bar: "Calm Before The Storm"), nor precipitation as Light/Moderate/Heavy. Sim: `Weather.cs` (a pure function of seed and minute); `WeatherTests.Year_MatchesOldStatistics`, `GracePeriod_HasNoSevereWeather`, `ColdSnapThatSkipsItsLastState_EndsEarly` | | |
| Wind chill, sun curve, visibility | `Weather`, `Location.GetTemperature` | Done | `WeatherTests.WindChill_MatchesOld`, `SunCurve_MatchesOld`, `VisibilityAndCalm_AsOld` | | |
| Seasons, sunrise and sunset, night | `Weather.CurrentSeason`, `TimeOfDay` | Done | `Weather.SeasonAt`, `SunriseHour` (`Weather.cs:104-119`); `WeatherTests.Calendar_StartsJulyFirstAtNine` | | |
| Weather reaches the ground separately from what a person feels | `Location.GetSurfaceWeather` | Done | `WeatherTests.SurfaceRates_AsOldGetSurfaceWeather`, `GroundTests.SurfaceWeather_CanopyInterceptsOnce_WhiteoutDepositsNothing_PassIsColder` | | |

## 20. Herds and fauna

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Herds as multi-member agents with minds: prey, pack, solitary, scavenger | `Herd`, `Behaviors/` | Done | `Fauna.cs`, `HerdMind`; `HerdTests.Gate_HerdsMoveGrazeAndWearTrails`, `APatrollingPack_RangesAcrossItsLand` (plan "Merge Herd into Agent") | | |
| Grazing depletes forage by diet | `Herd` grazing | Done | `HerdTests.GrazingThinsWhatThisDietEats_AndNotTheRest` | | |
| Pack hunts on the combat grid; carcass held and fed on | `PackPredatorBehavior`, `HerdVigilance` | Done | `HerdTests.Gate_WolvesKillACaribouInScheduledTime_AndFeedOnTheCarcass`, `CombatTests.PackHunt_LeavesConsistentWorldWhateverHappened` | | |
| Wounded animals split off as their own agent, leaving prints | `Herd` wounded split | Done | `HerdTests.Gate_AWoundedAnimalThatGetsAway_IsItsOwnHerd_AndLeavesPrints`, `ALameMember_IsLeftBehind_AndTheHerdMovesOn` | | |
| Valley population; dens and caves anchor packs, bears, mammoths | `HerdPopulator` | Done | `Populate.cs`; `HerdTests.TheValley_IsPopulatedLikeTheOldGame`, `EveryWolfDen_HasAPackLivingOnIt`, `TheBearCave_HasItsBear` | | |
| Learned fear, starving packs range wider | `Herd` fear and hunger | Done | `HerdTests.AFrightenedPack_LeavesPreyAlone`, `AForayEnds_WhenThePackScavenges` | | |
| Predators engage the player and NPCs (stalking, boldness to engage, ambush) | `PredatorInteraction`, `Herd.BoldnessToward`, `ShouldEngagePlayer`, `GameContext` pending encounter | Missing | `Fauna.cs:47`: "Herds never come for the player or an NPC yet". `Combat.Boldness` is ready (`CombatGoldenTests.HerdBoldness_MatchesOld`). This is also the emergent replacement for Stalked/Hunted | M | |
| Meat, blood and scent draw predators | `Situations.AttractiveToPredators` | Partial | `Bloody` and carcass `Scent` are modelled (`Carcass.Scent`, `Work.cs:344`); scavengers smell carcasses (`Fauna.cs:654`). Missing: anything that smells the player | S | the row above |
| Ptarmigan, rat, fish as animal bodies | `AnimalType` | Cut | `Species.cs:3`: birds lost their body plan (plan Keep/Port/Cut); the others are small-game pools | | |

## 21. Hunting and combat

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Player hunts a herd member: stalk, then the grid | `HuntRunner`, `HuntStrategy`, `CombatOrchestrator.RunHunt` | Core, no FE | `Hunt` activity, `Fauna.BeginHunt`; `HerdTests.ThePlayer_CanHuntAHerdMember_ButcherItAndEat`. The CLI has no `hunt` command | | |
| Tactical combat: zones, attack, throw, dodge, block, shove, intimidate, flee, call help | `CombatScenario`, `CombatActionRules`, `CombatFormulas` | Core, no FE | `Combat.cs`, `Fight`; `CombatTests.ActionsDependOnTheZone`, `CombatGoldenTests.*_MatchOld`. The CLI has no `fight` command | | |
| Combat AI and morale | `CombatAI` | Core, no FE | `CombatAI.cs`; `CombatGoldenTests.Intimidation_MatchesOld`, `CombatTests.PursuitClosesAtFullSpeedAndRespectsMorale` | | |
| Headless fights for NPCs and herds | `CombatOrchestrator.ResolveHeadless` | Core, no FE | `CombatTests.HeadlessFightsAreDeterministic`, `CombatGoldenTests.HeadlessOutcomes_MatchOldStatistically` | | |
| Combat aftermath: carcasses, herd losses, fear, wounded escape | `CombatAftermath` | Core, no FE | `CombatAftermath.cs`; `CombatTests.AWoundedAnimalThatGetsAwaySplitsIntoItsOwnAgent`, `OpeningMoraleAndTheFearAFightLeaves` | | |
| Butchering: modes, decay, tools, frozen carcass, blood and scent | `ButcherStrategy`, `CarcassFeature`, `ButcheringProcessor` | Core, no FE | `Carcass.cs`, `Butcher`; CLI `butcher` exists, but no CLI route makes a carcass (no hunt or fight command), so it is unreachable in play. `CarcassTests.Yields_AndButcheringTimes_MatchOld`, `Butchering_ByDecayAndBlade_MatchesOld`, `DecayTimeline_MatchesOld` | | |
| Combat balance after the self-ally fix | `CombatScenario` | Partial | Retune deferred: spear against a lone wolf wins about 8% (`dev/active/rebuild-combat-balance.md`) | S | a design call |
| Carcasses decay to bones and leave | (old: never) | Missing | Overnight review: 90-day-old kills still hold 35-75 kg. Emergent step 3's body and bone pipeline. Not old behaviour; needed by the settling pass | S | §25 |

## 22. Small game, trapping and fishing

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Small game as a density: search, encounter, depletion, peak hours | `SmallGameFeature`, `AnimalPresence`, `HuntStrategy` (small game) | Missing | `SiteSmallGame` is carried as data (`Content.cs`); the terrain draws that wait for it roll and leave nothing (`Content/discoveries.json`, `waitsFor`). Rabbit and Fox bodies exist (`Species.cs`) and small game bolts in combat (`CombatTests.SpottedSmallGameRunsInsteadOfFighting`) | M | |
| Snares: set, bait, passive catch, states (active, catch ready, stolen, destroyed), durability | `TrapStrategy`, `SnareLineFeature`, `PlacedSnare` | Missing | | M | small game, crafting |
| Ice holes: cut by thickness, refreeze | `IceCuttingStrategy`, `WaterFeature` | Missing | `Work.cs:155`: a frozen lake should need a hole; Ground does not model ice thickness on water yet | M | |
| Rod fishing | `FishingStrategy` | Missing | `SiteWater.Fish` is carried; `RawFish` is food | S | ice holes, crafting |
| Fishing nets: set and check, catch, loss and theft | `SetNetStrategy`, `CheckNetStrategy`, `NetFishingFeature`, `PlacedNet` | Missing | | S | ice holes, crafting |
| Water ice that grows and thaws as a hazard | `WaterFeature` ice thickness | Missing | Ice is a fixed site value (`SiteWater.HazardDelta`); thin river ice is a fixed hazard | S | |
| Ptarmigan, rat, fish as fighters | `AnimalType` | Cut | See §20; they come back as small-game pool entries | | |

## 23. Megafauna

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Mammoth and saber-tooth as herds; a dead megafauna herd stays dead | `HerdPopulator`, `Herd` | Done | `HerdTests.BoneHollow_HasTheMammoths_AndAKilledHerdStaysDead` | | |
| Scout, track, approach (staged by the hunt tension) | `MegafaunaStrategy` | Replaced | The tension is cut (emergent-world-design §1). The replacement is reading the herd's real prints and following them (`Tracks.Read`), then `Hunt`. Neither the player's print-following nor its UI exists yet | | tracks reading in the front end |

## 24. NPCs and companions

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Needs-based utility AI (warmth, water, rest, food), fire keeping, forage, chop, harvest | `NPC.cs`, `NPCActions` | Core, no FE | `Npc.cs`; `NpcTests.FourteenDaysAlone_OnTheWorldScheduler`, `TonightsFuelNotBanked_WantedFood_GoesForWood`. Open review items: `rebuild-npc-food-and-forage-review.md` (including a fire-keeping bug, seed 8) | | |
| Resource memory, stale beliefs | `ResourceMemory` | Core, no FE | `NpcMind.Places`; `NpcTests.Memory_IsOnePlacePerTile` | | |
| Threat response: flee or fight | `NPC` (old had none) | Core, no FE | `NpcTests.WolvesNextDoor_Flees`, `BearNextToLitCamp_HoldsTheFireAndSleeps` | | |
| Joins the player's fight | `NPC.DecideToHelpInCombat`, `CompanionCombat` | Core, no FE | `NpcTests.PlayerFightingWolves_BoldNpcJoinsIn` | | |
| NPCs placed in a new game (starting ally near camp, groups) | `NPCFactory.SpawnNearCamp`, `PopulateGroups` | Missing | `World.New` spawns none; only tests call `Npc.Spawn` | S | |
| NPC hunting by following prints | (old: none effective) | Partial | NPCs hunt only prey already within 3 tiles; in gate runs they never did (`dev/active/rebuild-npc-hunting.md`) | M | |
| NPC crafts its own tools | `NPCActions` crafting | Missing | The spawn kit's axe and knife stand in (`Npc.cs:55-57`) | S | crafting |
| NPC death leaves a body with belongings; decays; found on arrival | `NPCBodyFeature`, notices | Missing | A dead NPC is just a dead agent | S | |
| Loot and bury a body | `LootBodyStrategy`, `BuryStrategy` | Missing | | S | the row above |
| Relationship memory and opinion | `RelationshipMemory`, `RelationshipEvents` | Missing | `NpcMind.Opinions` exists; "Nothing writes this yet" (`Npc.cs`) | S | event stream consumers |
| Companions: invite, follow, request and give resources, talk | `Actors/Behavior/Following.cs`, `FollowIntent`, `CompanionInteractions` | Missing | Emergent step 6 plans a narrow verb set for human encounters (hail, trade, give, ask about, recruit, part) | M | NPCs placed, moments |

## 25. Events, tensions and their replacement

The plan cuts tensions, most authored events and outcome templates
([clean-room-architecture.md](clean-room-architecture.md) Keep/Port/Cut,
[emergent-world-design.md](emergent-world-design.md) §1). The rows marked Replaced need nothing
ported. The replacement itself is not built, though, and the last four rows track it, because
without it the rebuild has no events at all. Leftover authored content is being catalogued in
`documentation/authored-content-catalog.md`.

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Random contextual events (~20 registry partials) | `GameEventRegistry.*`, `EventQueue` | Replaced | emergent-world-design §1 and §7: moments | | moments |
| Tensions (Stalked, WoundedPrey, FoodScentStrong, SmokeSpotted...) | `Actions/Tensions/` | Replaced | emergent-world-design §1 table: each is real state | | |
| Situations (compound predicates) | `Situations.cs` | Replaced | emergent-world-design §3.3: computed tags and capabilities | | substrate |
| Variants (injury, discovery, illness, trail-sign text) | `Events/Variants/` | Replaced | emergent-world-design §2.6: templates over fact records | | |
| Discovery events and first-visit events | `DiscoveryEventFactory`, `EventTriggerFeature`, `GameEventRegistry.FirstVisit` | Replaced | emergent-world-design §6 settling pass; `SiteEvent`/`FirstVisitEvent` carried as data until then | | settling pass |
| Edge events, weather events, predator scenes | `EdgeEvents`, `WeatherEventFactory`, `AuthoredPredatorScenes`, `PredatorEventFactory` | Replaced | emergent-world-design §7 (moments fire on real dynamics: a storm reaching you mid-crossing, a pack closing) | | moments |
| Threshold warnings to the log | `ThresholdWarnings` | Replaced | ui-design.md alerts, derived each tick; front end | | front end |
| Event choice screen, at least two choices per event | `EventOverlay`, `EventChoiceTests` | Replaced | emergent-world-design gate: "no find ever offers zero verbs"; universal verbs (§3.4) | | moments |
| Event rate and early-game damping | `EventsPerHour` | Replaced | The director (§7); short-loop feature 10 sets the target rate | | moments |
| Outcome templates | `OutcomeTemplates.cs` | Cut | Plan Keep/Port/Cut | | |
| Substrate: `ThingRef`, `Cause`, `Signs.At`, `Access.Refusal` | (emergent step 2) | Missing | None of these types exist in `rebuild/Sim` | M | |
| Kill chain: herd renewal, scavenger beacon, hourly stepping, settling pass, body and bone pipeline, computed reveal | (emergent step 3) | Missing | Renewal is "required before any long settle"; caribou fall 21 to 5 in 90 days (overnight review) | L | substrate |
| Affordances and moments, minimal director | (emergent step 4) | Missing | The parity-critical piece: the first source of events in the rebuild | L | substrate, kill chain |
| Full interaction table, travellers | (emergent steps 5 and 6) | Missing | Not a parity blocker; the rows marked star in emergent §4 | L | step 4 |

## 26. Desktop front end

The old UI is Raylib plus ImGui (`Desktop/`, `UI/`, `Core/Program.cs`). Phase 5 replaces it with
the design in [dev/active/ui-design.md](../dev/active/ui-design.md), on the rebuild. The only
rebuild front end is the CLI (`rebuild/Cli/Program.cs`): a debug driver, not a game UI.

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Pixel-art pipeline (`.pxa` to PNG) | `tools/PixelArtCli`, `assets/pixelart/` | Done | Independent of both projects; plan Keep/Port/Cut "Keep" | | |
| Frame loop, scheduler, async driver over `Sim.Step` | `Core/Program.cs`, `FrameScheduler`, `IGameUi`, `GameRunner` | Missing | The Phase 5 skeleton: awaits the player, picks an `Activity`, runs `Step` under a progress view | L | |
| Timed activities on screen (progress view, pacing) | `ProgressView`, `Pacing`, `TimedRun` | Missing | | M | driver |
| World renderer, camera, terrain and tiles | `WorldRenderer`, `Camera`, `TileRenderer`, `TerrainRenderer`, `CaveEntranceRenderer` | Missing | Terrain art for the map scale exists (`81d9def`) | M | driver |
| Map drawn from beliefs: fog, remembered ground, last-seen fire | `FogRenderer` | Missing | ui-design gate test: a dead fire still shows roaring until seen | M | knowledge model (§12) |
| Feature, animal, NPC and player sprites | `IconRenderer`, `AnimalRenderer`, `TileRenderer` | Missing | | S | driver |
| Tracks, trails and edges on the map | `TrackRenderer`, `TrailRenderer`, `EdgeRenderer` | Missing | | S | `Tracks.Read` |
| Weather effects | `EffectsRenderer` | Missing | | S | |
| Top strip: clock, daylight left, feels-like | `TopBar` | Missing | | S | |
| Vitals and alert chips | `SurvivorPanel`, `SurvivorWarnings`, `HudWidgets` | Missing | ui-design gate: calm screen shows only the always-on layer; at most three alerts | M | |
| Place sheet and tile popup | `LocationInspector`, `TravelInspection` | Missing | | M | knowledge model |
| Trip preview: time, hazard, careful or go, daylight, camp fire as last seen | `TravelInspection`, hazard prompt | Missing | ui-design gate: dock times equal preview totals | M | `Traversal` returning its terms |
| Fire screen with the odds sheet | `FireOverlay` | Missing | ui-design gate: factors sum to the total | M | chance factors (short-loop "Ask") |
| Forage screen and its warnings | `ForageOverlay`, `ForageWarnings` | Missing | | M | |
| Eat and drink | `FoodOverlay` | Missing | | S | |
| Pack sheet and wearing gear | `InventoryOverlay` | Missing | | S | |
| Stash transfer | `TransferOverlay`, `TransferHandler` | Missing | | S | |
| Crafting screen | `CraftingOverlay`, `NeedCategoryDisplay` | Missing | | M | crafting (§10) |
| People sheet | `NPCOverlay` | Missing | | S | NPCs placed (§24) |
| Journal: log and discoveries | `EventLogPanel`, `NarrativeLog`, `DiscoveryLogOverlay`, `GameDisplay` | Missing | | M | event stream consumers, discovery log |
| Combat screen | `CombatPanel`, `CombatUiState`, `CombatNarrator` | Missing | | L | |
| Work results | `WorkResultDialog`, `WorkResultView` | Missing | The short-loop plan folds most of these into the progress summary | S | |
| Confirmations and prompts | `ConfirmationDialog`, `DesktopUi` prompts | Missing | | S | driver |
| Keyboard: hotkeys, focus navigation | `HotkeyRegistry`, `UiNavigation`, `MapInputRouter` | Missing | ui-design keyboard table | S | |
| UI scale setting | `UiSettings`, `UiSettingsDialog` | Missing | | S | |
| Music and mute | `AudioManager`, `assets/Audio` | Missing | | S | |
| Death and victory screens; new game and restart | `GameRunner.HandleDeath/HandleVictory`, `PlayerAction.NewGame` | Missing | Core outcome is derived (`Map.Outcome`) | S | |
| Autosave every two minutes; save deleted on death | `GameRunner.SaveIfDue`, `SaveManager.DeleteSave` | Missing | `Save.ToJson` exists; the policy is front-end work | S | |
| Event overlay | `EventOverlay` | Replaced | Moments use the same verb list as the player's menu (emergent §3.4); see §25 | | moments |
| Layering rules (no UI in the sim) | `LayeringTests` | Replaced | `ArchitectureTests.SimSource_HasNoUiAsyncOrThreading`, `SimAssembly_ReferencesNoUiAssembly` already enforce the core side; the front end must keep them | | |

## 27. Tooling and retirement

| Feature | Old source | Status | Evidence or what is missing | Size | Depends on |
|---|---|---|---|---|---|
| Scripted CLI for bots and bug reports (seed plus commands) | `tools/GameCli` | Partial | `rebuild/Cli` covers survival, fire, work, building and travel; `Phase2GateTests.Cli_SameSeedAndCommandsInTwoProcesses_SaveTheSameWorld`. Missing commands: hunt, fight, read tracks, NPCs | S | |
| Balance and distribution reports | (none) | Done | `Report.cs`, CLI `report`; `ReportTests.*` (emergent step 1) | | |
| Determinism and replay | (none) | Done | `Phase3GateTests.MixedRun_SavedAndLoadedEveryFewSteps_IsByteIdenticalToAStraightRun` | | |
| NPC headless sim harness | `tools/NpcSim` | Replaced | `NpcTests.FourteenDaysAlone_*` and `Sim.Advance` drive the same thing headless | | |
| HUD and UI previews | `tools/HudPreview`, `tools/UiPreview` | Replaced | `dev/prototypes/ui/` is the Phase 5 visual reference (ui-design.md) | | |
| Terrain preview | `tools/TerrainPreview` (references the old csproj and `PixelArtCli`) | Missing | Port onto `rebuild/Sim`'s `Map.Generate`, or retire | S | |
| Remove projects that reference `text_survival.csproj` from the solution | `text_survival.sln`, the five tools above | Missing | `GameCli`, `HudPreview`, `UiPreview`, `NpcSim`, `TerrainPreview` all reference it | S | the rows above |
| Retire the old project and its tests | `text_survival.csproj`, `text_survival.Tests` | Missing | The last step of the Phase 5 gate; the old tests on ported physics are already mirrored by the rebuild's golden tests | S | everything above |

---

## Not parity items

These are planned for after the rebuild and are not needed to retire the old project. They appear
here so nobody mistakes them for gaps:

- Short-loop features ([short-loop-plan.md](../dev/active/short-loop-plan.md)): fire gauntlet,
  place-memory receipts, the fire as a workbench, the night as a vigil, cooking quality, friction
  cuts, belief sharing between allies. Its "Asks of the Rebuild" (event tiles, historical minutes,
  chance factors, skills on `Agent`, scoped knowledge) are cheap and should land first; they are
  counted above where they block parity (§1 event stream, §4 skills, §12 knowledge, §26 odds sheet).
- Emergent-world steps 5 and 6 (full interaction table, travellers), and the legacy run.
- The long horizon (leading a band, basins as chapters, migration).
