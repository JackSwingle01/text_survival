# Hunting System Overhaul - Task Checklist

**Last Updated:** 2025-11-02 (MVP-First Approach)
**Status:** Not Started
**Current Phase:** None
**Approach:** MVP (2-3 weeks) then V2 (deferred)

---

## MVP Progress Overview

- [ ] MVP Phase 1: Core Animals & Distance System (0/5 tasks)
- [ ] MVP Phase 2: Stealth & Approach System (0/6 tasks)
- [ ] MVP Phase 3: Bow Hunting (0/7 tasks)
- [ ] MVP Phase 4: Blood Trail Tracking (0/6 tasks)

**MVP Total Progress:** 0/24 tasks (0%)

**Deferred to V2:**
- Trap System
- Simultaneous Combat Redesign
- Dangerous Prey Animals
- Multi-location Blood Trails
- Advanced AI Features

---

## MVP Phase 1: Core Animals & Distance System

**Goal:** Animal foundation with distance tracking
**Estimated Duration:** 3 days (Days 1-3)
**Status:** Not Started

### Tasks

- [ ] **1.1: Create Animal Architecture** (Effort: M)
  - [ ] Create `Actors/AnimalBehaviorType.cs` enum (Prey, Predator, Scavenger - no DangerousPrey in MVP)
  - [ ] Create `Actors/AnimalState.cs` enum (Idle, Alert, Detected - simpler for MVP)
  - [ ] Create `Actors/Animal.cs` inheriting from `Npc`
  - [ ] Add properties: `Behavior`, `State`, `Awareness`, `FleeThreshold`, `Distance` (meters from player)
  - [ ] Modify `Actors/NPC.cs`: Make `IsHostile` public setter
  - **Acceptance:** Animal class compiles, distance property exists

- [ ] **1.2: Add Prey Animals** (Effort: M)
  - [ ] Add to `NPCFactory.cs`: Deer (50kg, Antlers 8dmg, Awareness 0.8)
  - [ ] Add to `NPCFactory.cs`: Rabbit (3kg, Teeth 2dmg, Awareness 0.9)
  - [ ] Add to `NPCFactory.cs`: Ptarmigan (0.5kg, Beak 1dmg, Awareness 0.85)
  - [ ] Add to `NPCFactory.cs`: Fox (6kg, Teeth 4dmg, Awareness 0.7, Scavenger)
  - [ ] Set all: `IsHostile = false`, `Behavior = Prey/Scavenger`, `Distance = 100`
  - [ ] Add appropriate loot (meat, pelts, sinew)
  - **Acceptance:** 4 non-hostile animals spawn with distance = 100m

- [ ] **1.3: Update Predators** (Effort: S)
  - [ ] Modify Wolf to use new system (`Behavior = Predator`, `Awareness = 0.75`, `Distance = 100`)
  - [ ] Modify Bear to use new system
  - [ ] Modify Cave Bear to use new system
  - [ ] Keep `IsHostile = true` for all predators
  - **Acceptance:** Predators use new behavior system

- [ ] **1.4: Update Spawn Tables** (Effort: S)
  - [ ] Modify `LocationFactory.cs` for Forest biome only (MVP scope)
  - [ ] Add: Deer (2.0), Rabbit (3.0), Fox (1.5)
  - [ ] Reduce: Wolf (1.0), Bear (0.5)
  - **Acceptance:** Prey common, predators rare in Forest

- [ ] **1.5: Test Animal Foundation** (Effort: S)
  - [ ] Spawn test location with animals via TEST_MODE
  - [ ] Verify non-hostile animals don't attack
  - [ ] Verify predators attack as before
  - [ ] Verify distance displays correctly (100m)
  - [ ] Document any issues in ISSUES.md
  - **Acceptance:** Animals spawn and behave correctly

**MVP Phase 1 Complete When:** All 5 tasks checked, build succeeds, animals spawn with distance

---

## MVP Phases 2-4: Detailed Tasks

**For detailed MVP Phase 2-4 tasks, see `hunting-overhaul-plan.md` (lines 252-427)**

Quick overview:
- **MVP Phase 2:** Stealth & Approach System (6 tasks, Days 4-6)
- **MVP Phase 3:** Bow Hunting (7 tasks, Days 7-10)
- **MVP Phase 4:** Blood Trail Tracking (6 tasks, Days 11-14)

---

## V2 Features (Deferred Until MVP Validated)

The following phases from the original plan are deferred to V2:

### Phase 2 (Original): New Animal Roster - DEFERRED

**Estimated Duration:** 3 days
**Status:** Not Started

### Tasks

- [ ] **2.1: Add Prey Animals (4 types)** (Effort: M)
  - [ ] Create Deer in NPCFactory (50kg, Antlers 8dmg, loot: Medium Meat x2, Deer Hide, Antlers, Sinew)
  - [ ] Create Rabbit (3kg, Teeth 2dmg, loot: Small Meat, Rabbit Pelt, Sinew)
  - [ ] Create Ptarmigan (0.5kg, Beak 1dmg, loot: Tiny Meat, Feathers x3)
  - [ ] Create Mountain Goat (80kg, Horns 10dmg, loot: Large Meat x2, Goat Hide, Horns, Sinew)
  - [ ] Set all: `IsHostile = false`, `Behavior = Prey`, `Awareness = 0.8`, `FleeThreshold = 1.0`
  - **Acceptance:** 4 prey animals spawn as non-hostile, have correct stats/loot

- [ ] **2.2: Add Dangerous Prey Animals (4 types)** (Effort: M)
  - [ ] Create Bison (800kg, Horns 18dmg, loot: Large Meat x6, Thick Hide x2, Bison Horns, Sinew x2)
  - [ ] Create Auroch (1000kg, Massive Horns 22dmg, loot: Large Meat x8, Auroch Hide x2, Auroch Horns, Sinew x3)
  - [ ] Create Elk (400kg, Antlers 15dmg, loot: Large Meat x4, Elk Hide x2, Elk Antlers, Sinew x2)
  - [ ] Create Moose (500kg, Antlers 16dmg, loot: Large Meat x5, Moose Hide x2, Moose Antlers, Sinew x2)
  - [ ] Set all: `IsHostile = false`, `Behavior = DangerousPrey`, `Awareness = 0.6`, `FleeThreshold = 0.5`
  - **Acceptance:** 4 dangerous prey spawn as non-hostile but will fight if cornered

- [ ] **2.3: Add Scavenger Animals (4 types)** (Effort: M)
  - [ ] Create Fox (6kg, Teeth 4dmg, loot: Small Meat, Fox Pelt, Sinew)
  - [ ] Create Beaver (20kg, Teeth 6dmg, loot: Medium Meat, Beaver Pelt, Sinew)
  - [ ] Create Marmot (5kg, Teeth 3dmg, loot: Small Meat, Marmot Pelt, Sinew)
  - [ ] Create Wolverine (18kg, Claws 8dmg, loot: Medium Meat, Wolverine Pelt, Claws, Sinew)
  - [ ] Set all: `IsHostile = false`, `Behavior = Scavenger`, `Awareness = 0.7`, `FleeThreshold = 0.4`
  - **Acceptance:** 4 scavengers spawn, can assess player before attacking/fleeing

- [ ] **2.4: Update Existing Predators** (Effort: S)
  - [ ] Modify Wolf to use new system (`Behavior = Predator`, `Awareness = 0.75`, `FleeThreshold = 0.2`)
  - [ ] Modify Bear to use new system
  - [ ] Modify Cave Bear to use new system
  - [ ] Keep `IsHostile = true` for all predators
  - **Acceptance:** Existing predators use new behavior system

- [ ] **2.5: Update Location Spawn Tables** (Effort: M)
  - [ ] Modify `Environments/LocationFactory.cs` NpcTables
  - [ ] Forest: Add Deer (2.0), Rabbit (3.0), Fox (1.5), reduce Wolf to (1.0)
  - [ ] Plains: Add Bison (1.5), Elk (1.0), Ptarmigan (2.0), Wolverine (0.5)
  - [ ] Hillside: Add Mountain Goat (2.0), Marmot (2.0)
  - [ ] Riverbank: Add Beaver (2.5), reduce predators
  - [ ] Cave: Keep predators dominant (Bear, Wolverine)
  - [ ] Adjust spawn chances: Prey 50-60%, Scavengers 20-30%, Predators 10-20%
  - **Acceptance:** New animals spawn in appropriate biomes, balanced distribution

**Phase 2 Complete When:** All 5 tasks checked, 12+ animals exist, spawn tables updated

---

## Phase 3: Stealth & Tracking System

**Goal:** Implement stealth approach, animal detection, and tracking mechanics

**Estimated Duration:** 4 days
**Status:** Not Started

### Tasks

- [ ] **3.1: Create StealthManager Component** (Effort: L)
  - [ ] Create `PlayerComponents/StealthManager.cs`
  - [ ] Implement `IsSneaking` property
  - [ ] Implement `Concealment` property (calculated from Hunting skill, clothing, weather)
  - [ ] Implement `CalculateConcealment()` method
  - [ ] Implement `DetectionCheck(Animal target, double distance)` method
  - [ ] Add StealthManager to Player constructor
  - **Acceptance:** Player has StealthManager, concealment calculates correctly

- [ ] **3.2: Implement Detection System** (Effort: L)
  - [ ] Create `Utils/HuntingCalculator.cs`
  - [ ] Implement `IsDetected(concealment, awareness, distance)` static method
  - [ ] Implement `GetStealthSuccessChance(huntingSkill, behaviorType)` static method
  - [ ] Implement distance modifier logic (1.0 at 50m, 0.5 at 100m, 2.0 at 25m)
  - [ ] Unit test detection logic
  - **Acceptance:** Detection logic works, prey easier to sneak up on than predators

- [ ] **3.3: Create Tracking System** (Effort: M)
  - [ ] Create `PlayerComponents/TrackingManager.cs`
  - [ ] Implement `AnimalTrack` class (animal type, direction, age, location)
  - [ ] Implement `FindTrack(huntingSkill)` method with skill check
  - [ ] Implement `FollowTrack(track, location)` method
  - [ ] Add TrackingManager to Player constructor
  - **Acceptance:** Can find and follow tracks based on Hunting skill

- [ ] **3.4: Integrate Tracking with Foraging** (Effort: M)
  - [ ] Modify `Environments/ForageFeature.cs`
  - [ ] Add track finding to `Forage()` method (after normal results)
  - [ ] Skill check: 10% + (huntingSkill * 5%)
  - [ ] Create track if check succeeds, add to TrackingManager
  - [ ] Display message to player
  - **Acceptance:** Foraging can find animal tracks, displayed to player

- [ ] **3.5: Create Hunting Actions** (Effort: L)
  - [ ] Add "Hunting" section to `Actions/ActionFactory.cs`
  - [ ] Implement `ToggleStealth()` action (toggle sneaking on/off)
  - [ ] Implement `ApproachAnimal(Animal target)` action (stealth check, detection)
  - [ ] Implement `AssessAnimal(Animal target)` action (show stats, threat level, recommended weapon)
  - [ ] Implement `FollowTrack(AnimalTrack track)` action (navigate to track location)
  - [ ] Add actions to main menu when appropriate
  - **Acceptance:** Can toggle stealth, approach animals, assess threat, follow tracks

- [ ] **3.6: Test Stealth System** (Effort: S)
  - [ ] Playtest scenario: spawn deer, sneak up with Hunting 0, 2, 5
  - [ ] Verify concealment scales with Hunting skill
  - [ ] Verify prey easier to approach than predators
  - [ ] Document balance issues in ISSUES.md
  - **Acceptance:** Stealth works, detection feels balanced

**Phase 3 Complete When:** All 6 tasks checked, stealth/tracking functional, balance tested

---

## Phase 4: Ranged Weapons & Hunting Attacks

**Goal:** Implement bows, arrows, and ranged hunting mechanics with skill-based accuracy

**Estimated Duration:** 5 days
**Status:** Not Started

### Tasks

- [ ] **4.1: Create RangedWeapon Class** (Effort: M)
  - [ ] Create `Items/RangedWeapon.cs` extending `Weapon`
  - [ ] Add properties: `Range`, `AmmoType`, `BaseAccuracy`, `DrawTime`
  - [ ] Implement `GetAccuracyAtRange(distance, huntingSkill)` method
  - [ ] Accuracy formula: `BaseAccuracy - (distance / Range) + (huntingSkill * 0.05)`, clamped 5%-95%
  - **Acceptance:** RangedWeapon class compiles, extends Weapon correctly

- [ ] **4.2: Modify Weapon.cs for Ranged Support** (Effort: S)
  - [ ] Add `bool IsRanged { get; }` property
  - [ ] Add `int Range { get; }` property (default 0 for melee)
  - [ ] Add `string AmmoType { get; }` property (null for melee)
  - [ ] Update constructor to accept optional ranged parameters
  - **Acceptance:** Weapon.cs supports both melee and ranged weapons

- [ ] **4.3: Create Ammunition System** (Effort: M)
  - [ ] Create `PlayerComponents/AmmunitionManager.cs`
  - [ ] Implement `GetAmmoCount(ammoType)` method
  - [ ] Implement `AddAmmo(ammoType, count)` method
  - [ ] Implement `ConsumeAmmo(ammoType, count)` method
  - [ ] Implement `RecoverArrows(shotsFired, targetKilled)` method (50% on kill, 20% on miss)
  - [ ] Add AmmunitionManager to Player constructor
  - **Acceptance:** Can track/consume/recover ammunition

- [ ] **4.4: Create Bow & Arrow Items** (Effort: M)
  - [ ] Add `MakeSimpleBow()` to ItemFactory (30m range, 40% accuracy, 12 dmg)
  - [ ] Add `MakeRecurveBow()` to ItemFactory (50m range, 60% accuracy, 18 dmg)
  - [ ] Add `MakeStoneArrow()` to ItemFactory (Pierce damage, 0.05kg)
  - [ ] Add `MakeBoneArrow()` to ItemFactory (Pierce damage, 0.04kg)
  - [ ] Set appropriate CraftingProperties
  - **Acceptance:** Bow and arrow items exist with correct stats

- [ ] **4.5: Add Bow & Arrow Crafting Recipes** (Effort: M)
  - [ ] Add Simple Bow recipe to CraftingSystem (Wood 1kg + Sinew 0.2kg, 120 min, Crafting 2)
  - [ ] Add Recurve Bow recipe (Wood 1.5kg + Sinew 0.3kg + Bone 0.5kg, 180 min, Crafting 3)
  - [ ] Add Stone Arrows recipe (Wood 0.5kg + Flint 0.25kg + Feather 0.05kg, 30 min, makes 5, Crafting 1)
  - [ ] Add Bone Arrows recipe (Wood 0.5kg + Bone 0.25kg + Feather 0.05kg, 30 min, makes 5, Crafting 1)
  - **Acceptance:** Can craft bows and arrows with proper materials

- [ ] **4.6: Create HuntingManager Component** (Effort: XL)
  - [ ] Create `PlayerComponents/HuntingManager.cs`
  - [ ] Implement `RangedAttack(Animal target, RangedWeapon weapon, double distance)` method
  - [ ] Check ammunition, consume 1 arrow
  - [ ] Calculate accuracy (weapon base + skill - range penalty)
  - [ ] Apply stealth bonuses (+50% accuracy if undetected, +50% damage on idle targets)
  - [ ] Hit check (skill check)
  - [ ] Determine hit location (skill-based: random → region → specific part)
  - [ ] Critical hit check (headshot on small game = instant kill)
  - [ ] Create DamageInfo, apply to target body
  - [ ] Award XP (2 on hit, 1 on miss)
  - [ ] Add HuntingManager to Player constructor
  - **Acceptance:** Can perform ranged attacks with accuracy/damage calculations

- [ ] **4.7: Create Shooting Actions** (Effort: L)
  - [ ] Add `ShootAnimal(Animal target)` action to ActionFactory
  - [ ] Check player has ranged weapon equipped
  - [ ] Execute RangedAttack(), show result
  - [ ] Handle kill (recover arrows, loot)
  - [ ] Handle wound (animal response)
  - [ ] Handle miss (animal flees/attacks)
  - [ ] Add `TargetedShot(Animal target)` action (Hunting 2+, choose body part)
  - **Acceptance:** Can shoot animals, hit/miss/damage works correctly

- [ ] **4.8: Test Ranged Hunting** (Effort: M)
  - [ ] Playtest: Craft bow, hunt deer with Hunting 0, 2, 5
  - [ ] Verify accuracy scales with skill (0: ~30%, 2: ~50%, 5: ~75%)
  - [ ] Verify stealth bonus applies (+50% accuracy when undetected)
  - [ ] Verify headshots kill small game (rabbit, ptarmigan)
  - [ ] Verify arrow recovery (50% on kill, 20% on miss)
  - [ ] Verify wounded animals respond appropriately
  - [ ] Document balance issues in ISSUES.md
  - **Acceptance:** Bow hunting feels tactical and skill-based

**Phase 4 Complete When:** All 8 tasks checked, ranged hunting functional, balance tested

---

## Phase 5: Trap System

**Goal:** Implement craftable traps for passive hunting over time

**Estimated Duration:** 3 days
**Status:** Not Started

### Tasks

- [ ] **5.1: Create Trap Item Class** (Effort: M)
  - [ ] Create `Items/Trap.cs`
  - [ ] Add properties: `TrapType`, `SuccessRate`, `MaxAnimalSize`, `IsSet`, `TimeSet`, `Location`
  - [ ] Implement `CanCatch(Animal)` method (weight check)
  - [ ] Implement `TriggerCheck(Animal)` method (success rate, behavior modifier)
  - **Acceptance:** Trap class compiles with proper properties

- [ ] **5.2: Create Trap Items** (Effort: S)
  - [ ] Add `MakeRabbitSnare()` to ItemFactory (50% success, max 5kg)
  - [ ] Add `MakeLargeSnare()` to ItemFactory (40% success, max 50kg)
  - [ ] Add `MakeDeadfallTrap()` to ItemFactory (60% success, max 100kg)
  - [ ] Set CraftingProperties
  - **Acceptance:** 3 trap types created with appropriate stats

- [ ] **5.3: Add Trap Crafting Recipes** (Effort: S)
  - [ ] Add Rabbit Snare recipe (Sinew 0.2kg, 20 min, Hunting 2)
  - [ ] Add Large Snare recipe (Sinew 0.5kg + Wood 0.5kg, 45 min, Hunting 3)
  - [ ] Add Deadfall Trap recipe (Stone 5kg + Wood 2kg, 60 min, Hunting 4)
  - **Acceptance:** Can craft traps with proper skill/material requirements

- [ ] **5.4: Create TrapFeature for Locations** (Effort: M)
  - [ ] Create `Environments/TrapFeature.cs` extending LocationFeature
  - [ ] Add properties: `Trap`, `CaughtAnimal`, `TimeSet`
  - [ ] Implement `CheckTrap(Location)` method (find eligible animals, attempt capture)
  - [ ] Implement `Update(TimeSpan)` override (check every hour)
  - **Acceptance:** TrapFeature exists, can check for catches

- [ ] **5.5: Implement Trap Placement Actions** (Effort: M)
  - [ ] Add `SetTrap(Trap trap)` action to ActionFactory
  - [ ] Remove trap from inventory, create TrapFeature, add to location
  - [ ] Takes 30-60 min based on trap weight
  - [ ] Award Hunting XP
  - [ ] Add `CheckTraps()` action (show all traps at location, which have catches)
  - [ ] Add `CollectTrap(TrapFeature)` action (dispatch animal, return trap to inventory)
  - **Acceptance:** Can set traps, check them, collect catches

- [ ] **5.6: Integrate Traps with Location.Update()** (Effort: S)
  - [ ] Modify `Environments/Location.cs`
  - [ ] Ensure `Update()` calls `Features.ForEach(f => f.Update(TimeSpan.FromMinutes(1)))`
  - [ ] Verify traps check during World.Update()
  - **Acceptance:** Traps check automatically during world updates

- [ ] **5.7: Test Trap System** (Effort: M)
  - [ ] Playtest: Set rabbit snare, wait 2 hours (sleep/forage), check trap
  - [ ] Verify traps check correctly during time passage
  - [ ] Test success rates (feel balanced?)
  - [ ] Test different trap types for different animals
  - [ ] Can collect trap + catch successfully
  - [ ] Document balance issues
  - **Acceptance:** Trap system functional and satisfying

**Phase 5 Complete When:** All 7 tasks checked, trap system functional, balance tested

---

## Phase 6: Lethal Combat Redesign

**Goal:** Transform combat from turn-based spam into tactical, deadly encounters

**Estimated Duration:** 3 days
**Status:** Not Started

### Tasks

- [ ] **6.1: Redesign CombatManager for Lethality** (Effort: L)
  - [ ] Modify `PlayerComponents/CombatManager.cs`
  - [ ] Add `LETHAL_DAMAGE_MULTIPLIER = 3.0` constant
  - [ ] Multiply all damage by 3x
  - [ ] Implement `IsCriticalHit(BodyPart)` method (heart, head, artery, lung = critical)
  - [ ] Critical hits do 2x damage
  - [ ] Implement `CheckForShock(Actor, DamageResult)` method
  - [ ] Major damage (>30%) or blood loss (<50%) can cause shock/unconsciousness
  - **Acceptance:** Combat deals 3x damage, critical hits work

- [ ] **6.2: Create Combat Action Enum** (Effort: S)
  - [ ] Create `PlayerComponents/CombatAction.cs`
  - [ ] Define enum: QuickAttack, PowerAttack, TargetedStrike, DefensiveStance, Feint, Disengage
  - **Acceptance:** Enum compiles, ready for use

- [ ] **6.3: Implement Tactical Combat Actions** (Effort: L)
  - [ ] Modify `Actions/ActionFactory.cs` combat menu
  - [ ] Implement `QuickAttack(enemy)` action (fast, lower damage, harder to dodge)
  - [ ] Implement `PowerAttack(enemy)` action (slow, high damage, easier to dodge)
  - [ ] Implement `TargetedStrike(enemy)` action (choose body part, Fighting 2+)
  - [ ] Implement `DefensiveStance(enemy)` action (skip attack, +50% dodge/block)
  - [ ] Implement `Feint(enemy)` action (Fighting 3+ skill check to lower enemy defense)
  - [ ] Implement `Disengage(enemy)` action (safer flee with defensive bonus)
  - [ ] Implement `ShouldEnemyFlee(enemy)` helper (behavior-based flee checks)
  - **Acceptance:** Tactical combat actions work, combat feels strategic

- [ ] **6.4: Increase Bleeding Severity** (Effort: M)
  - [ ] Modify bleeding effect generation (if in SurvivalProcessor)
  - [ ] Increase bleeding rate multiplier by 2x
  - [ ] Major artery hits = 10x bleeding (life-threatening)
  - [ ] Add "Bandage" action to stop bleeding
  - **Acceptance:** Bleeding is much more dangerous, requires treatment

- [ ] **6.5: Test Lethal Combat** (Effort: M)
  - [ ] Playtest: Fight wolf with different weapons/actions
  - [ ] Take serious hit, verify shock/bleeding effects
  - [ ] Test tactical options (feint, defensive stance, disengage)
  - [ ] Verify combat ends in 1-3 exchanges (not 10+)
  - [ ] Verify critical hits to vitals are devastating
  - [ ] Verify player can die quickly if careless
  - [ ] Verify tactical choices matter
  - [ ] Balance tuning as needed
  - [ ] Document issues in ISSUES.md
  - **Acceptance:** Combat feels lethal, tactical, tense

**Phase 6 Complete When:** All 5 tasks checked, combat redesigned, lethality tested

---

## Phase 7: Animal Behavior Integration

**Goal:** Implement animal AI responses to hunting, wounds, and player actions

**Estimated Duration:** 4 days
**Status:** Not Started

### Tasks

- [ ] **7.1: Implement Animal State Machine** (Effort: L)
  - [ ] Modify `Actors/Animal.cs`
  - [ ] Implement `OnDetected(Player)` method
    - [ ] Prey: Flee immediately
    - [ ] DangerousPrey: Assess threat → flee or fight
    - [ ] Predator: Attack if hungry/threatened
    - [ ] Scavenger: Assess player strength → attack weak, flee strong
  - [ ] Implement `OnWounded(damagePercent)` method
    - [ ] Prey: Panic flee
    - [ ] DangerousPrey: Flee if below threshold, else fight
    - [ ] Predator: Fight harder
    - [ ] Scavenger: Flee if badly wounded
  - [ ] Implement `AssessThreat(Player)` helper (compare size/strength)
  - [ ] Implement `AttemptFlee(Player)` method (speed check, remove from location if escape)
  - **Acceptance:** Animals respond to detection/wounds based on behavior type

- [ ] **7.2: Implement Blood Trail Tracking** (Effort: M)
  - [ ] Modify `PlayerComponents/TrackingManager.cs`
  - [ ] Create `BloodTrail` class (wounded animal, location, time, blood amount)
  - [ ] Implement `FindBloodTrail(Location)` method
  - [ ] Implement `FollowBloodTrail(BloodTrail)` method (skill-based)
  - [ ] Add action to follow blood trails
  - **Acceptance:** Can track wounded fleeing animals

- [ ] **7.3: Handle Hunting Failure States** (Effort: M)
  - [ ] Modify hunting actions in ActionFactory
  - [ ] Handle detection in `ApproachAnimal` → trigger OnDetected()
  - [ ] Show appropriate menu based on animal response
    - [ ] Fleeing: Track or let go
    - [ ] Aggressive: Combat or retreat
  - [ ] Handle missed shots → animal flees or attacks
  - **Acceptance:** Hunting failures lead to appropriate responses

- [ ] **7.4: Implement Pack Behavior** (Effort: L) [OPTIONAL]
  - [ ] Add `CallPack(Player)` method to Animal
  - [ ] Find nearby same-species animals
  - [ ] Make them hostile and engage player
  - [ ] Show message "The wolf howls! Other wolves respond!"
  - [ ] Test with 2-3 wolves in same location
  - **Acceptance:** Predators can call packmates for help

- [ ] **7.5: Test Animal Behavior** (Effort: M)
  - [ ] Playtest: Approach deer → Should flee immediately
  - [ ] Playtest: Wound bison → Should flee or fight based on HP
  - [ ] Playtest: Attack wolf → Should call pack if present
  - [ ] Playtest: Approach fox as injured player → Should attack opportunistically
  - [ ] Verify each behavior type responds correctly
  - [ ] Verify state transitions work
  - [ ] Verify blood trails are trackable
  - [ ] Document issues in ISSUES.md
  - **Acceptance:** Animal AI feels realistic and varied

**Phase 7 Complete When:** All 5 tasks checked, animal behavior functional, AI tested

---

## Post-Implementation Tasks

**After All Phases Complete**

- [ ] **Documentation**
  - [ ] Update CLAUDE.md with hunting system overview
  - [ ] Create `/documentation/hunting-system.md` reference doc
  - [ ] Add examples to `/documentation/complete-examples.md`
  - [ ] Document all new manager components

- [ ] **Testing**
  - [ ] Run full unit test suite (`dotnet test`)
  - [ ] Run integration tests via `./play_game.sh`
  - [ ] Conduct 2+ full playthroughs (4-8 hours each)
  - [ ] Balance testing and tuning

- [ ] **Balance Tuning**
  - [ ] Adjust stealth success rates based on feedback
  - [ ] Adjust bow accuracy at different skill levels
  - [ ] Adjust trap success rates
  - [ ] Adjust combat damage multiplier (3x may need tweaking)
  - [ ] Adjust arrow recovery rates
  - [ ] Adjust food yield from animals

- [ ] **Bug Fixes**
  - [ ] Address any issues found in ISSUES.md
  - [ ] Regression testing for existing systems
  - [ ] Performance testing (trap checking lag?)

- [ ] **Polish**
  - [ ] Add narrative descriptions for hunting actions
  - [ ] Improve combat narration for new actions
  - [ ] Add flavor text for different animal behaviors
  - [ ] Tutorial messages for new players

- [ ] **Git Commit**
  - [ ] Review all changes
  - [ ] Create comprehensive commit message
  - [ ] Tag release: `v0.2.0-hunting-overhaul`

- [ ] **Archive Development Docs**
  - [ ] Move `/dev/active/hunting-system-overhaul/` to `/dev/complete/`
  - [ ] Update status to "Complete"
  - [ ] Document lessons learned

---

## Quick Status Updates

**Format:** Date - Task # - Status - Notes

*Example:*
- 2025-11-03 - Task 1.1 - Complete - Animal behavior enums created
- 2025-11-03 - Task 1.2 - In Progress - Animal class 80% done, testing needed
- 2025-11-04 - Task 1.2 - Complete - Animal class working, spawns correctly

**Actual Updates:**

*(Add updates here as you work)*

---

## Blocker Tracking

**Current Blockers:** None

**Format:** Date - Blocker - Affected Tasks - Resolution Plan

*Example:*
- 2025-11-05 - Body.Damage() not working with animal bodies - Tasks 4.6, 4.7 - Investigate BodyPartFactory for animals

**Actual Blockers:**

*(Add blockers here if encountered)*

---

## Notes & Discoveries

**Things learned during implementation that aren't in the plan:**

*(Add notes here as you discover things)*

---

**Task List Version:** 2.0 (MVP-First)
**Last Updated:** 2025-11-02
**MVP Progress:** 0% (0/24 tasks)
**Full System Progress:** 0% (0/65+ tasks including V2)
