# Hunting System Overhaul - Comprehensive Implementation Plan

**Last Updated:** 2025-11-02 (Refined via brainstorming)
**Status:** Planning Phase - Design Clarified
**Estimated Duration:** 2-3 weeks (MVP-first approach)
**Complexity:** L (MVP) → XL (Full system)

---

## Executive Summary

This plan outlines a complete overhaul of the game's combat and hunting systems, transforming it from a turn-based RPG combat system into a **lethal, tactical survival experience**. The new system will feature:

- **Hunting as primary gameplay loop** - Stealth, tracking, ranged weapons, and traps for food acquisition
- **Lethal combat as failure state** - Rare, dangerous hand-to-hand combat when hunting fails or predators attack
- **Diverse animal behaviors** - Four distinct animal archetypes (Prey, Dangerous Prey, Predators, Scavengers) with unique AI responses
- **Tactical depth** - Multiple approaches to encounters (stealth, ranged, traps, avoidance)

### Vision Statement

> "Combat should feel like a life-or-death struggle, not turn-based spam. Hunting should be about careful preparation, stealth, and precision. Every encounter with wildlife should be tactical, tense, and meaningful."

### Core Design Principles

1. **Lethality First** - Both player and animals can die quickly from serious injuries
2. **Tactical Choices Matter** - Stealth vs aggression, ranged vs melee, preparation vs improvisation
3. **Distinct Animal Behaviors** - Prey flees, predators hunt, dangerous prey fights when cornered
4. **Hunting ≠ Combat** - Hunting is stealth/ranged/traps; combat is when things go wrong
5. **Ice Age Authenticity** - Bow hunting, primitive traps, realistic animal behavior

---

## Design Clarifications (from Brainstorming Session)

These decisions were refined through collaborative design exploration:

### 1. Distance System: **Simple Meter Tracking**
- Animals start at 100m (safe range)
- "Approach" actions reduce distance by 20-30m (takes 5-10 minutes)
- Detection difficulty increases as you get closer (70m = easy, 50m = moderate, 30m = hard)
- Bow effective range: 30-50m (accuracy decreases with distance)

### 2. Combat Flow: **Simultaneous Resolution**
- Both player and enemy choose actions at once (no turn spam)
- Counter-play mechanics: Quick beats Power, Defensive beats Quick, Power beats Defensive
- Winning counter = +25% hit chance, +25% damage
- Combat ends in 1-3 exchanges (3x damage multiplier)
- Enemy AI is probabilistic (not deterministic) to prevent perfect counter-play

### 3. Trap Balance: **Multi-Layered**
- Requires bait (meat/berries) or -50% success rate
- Location limit: Max 3 traps per location
- Large animals (>50kg) have 30-50% chance to break trap when caught
- Area learning: After 2+ catches, animal spawn rate -50% for 7 days

### 4. Stealth Detection: **Distance-Based with Actions**
- Start at 100m (safe), approach action = skill check
- Animal states: Idle (unaware) → Alert (suspicious) → Detected (flees/attacks)
- Failed stealth check = Alert state (harder subsequent checks, no damage bonus)
- Second failure = fully detected

### 5. Animal AI: **Simple State Machine (V1)**
- Idle → Alert → Fleeing/Aggressive
- No complex coordination (V2 feature)
- Pack behavior = nearby same-species also become hostile (simple)

### 6. Blood Trails: **Time Pressure Tracking**
- Trail freshness decays over time (heavy wounds = slower decay)
- Each tracking attempt = skill check based on freshness
- Failure = lose time, trail gets colder
- Creates tension: hesitate too long = lose wounded animal

### 7. Critical Hits: **Use Existing Body System**
- Ranged attacks use `Body.Damage(DamageInfo)` (same as melee)
- Organ hits (brain, heart) naturally more lethal on small animals
- Balance via bow damage + tissue protection calculations
- No special "instant kill" code needed

### 8. Implementation: **MVP Then Iterate**
- Build minimum viable hunting first (animals + stealth + bow)
- Test and balance before adding complexity
- Defer traps, combat redesign, advanced AI to V2

---

## Current State Analysis

### Existing Combat System (To Be Replaced)

**Location:** `PlayerComponents/CombatManager.cs`, `Actions/ActionFactory.cs`

**Current Flow:**
1. All animals spawn as hostile NPCs (`IsHostile = true` hardcoded)
2. Encounter triggers turn-based combat immediately
3. Player options: Attack, Targeted Attack (Fighting 2+), Flee
4. Combat loops until death or successful flee
5. Damage is low, combat takes many turns ("spam attack button")

**Problems:**
- ❌ Binary hostile encounters (no stealth/tracking/preparation)
- ❌ Turn-based RPG feel (not survival-focused)
- ❌ Low lethality (takes 5-10+ hits to kill)
- ❌ No hunting mechanics (can't approach animals strategically)
- ❌ All animals are hostile (no prey/predator distinction)
- ❌ Hunting skill exists but is completely unused

### Existing Animal System

**Location:** `Actors/NPCFactory.cs`

**Current Animals (9 types):**
- Rat, Bat, Spider, Snake (small hostiles)
- Wolf, Bear, Cave Bear (medium/large predators)
- Woolly Mammoth, Saber-Tooth Tiger (mega-fauna)
- All are **hostile by default**, no behavioral variety

**Gaps:**
- ❌ No prey animals (deer, rabbit, etc.)
- ❌ No non-hostile animals
- ❌ No animal AI/behavior system
- ❌ No fleeing or dynamic responses
- ❌ Limited Ice Age fauna variety

### What Works Well (Keep/Extend)

✅ **Body system** - Damage penetration, organ targeting, injury effects
✅ **Action builder pattern** - Perfect for complex hunting menus
✅ **Composition architecture** - Easy to add `HuntingManager` component
✅ **Skill framework** - Hunting skill defined, ready for implementation
✅ **Crafting system** - Property-based, supports bows/arrows/traps

---

## Proposed Future State

### New Hunting System Architecture

```
Player Encounter with Wildlife
    ↓
1. DETECTION PHASE
   - Animal type determines awareness (prey = high, scavengers = medium)
   - Player stealth affects detection (Hunting skill + concealment)
   - If detected: Animal enters ALERT state → flees/attacks based on behavior type
   - If undetected: Player can approach, assess, or prepare attack
    ↓
2. ENGAGEMENT PHASE (Player Choice)
   - Option A: Ranged Attack (bow/throwing spear) → Hit/Miss/Wound
   - Option B: Set Trap → Wait for trigger (async)
   - Option C: Melee Approach → Triggers detection check → Combat if detected
   - Option D: Assess & Retreat → Gather info, avoid engagement
    ↓
3. RESOLUTION PHASE
   SUCCESS PATHS:
   - Clean kill (headshot, vital organ hit) → Loot animal
   - Wounded animal → Flees, leaves blood trail → Track & finish
   - Trapped animal → Captured, quick dispatch → Loot

   FAILURE PATHS:
   - Missed shot → Animal flees or attacks (behavior dependent)
   - Detected during approach → Prey flees, predator attacks
   - Failed combat → Player injured/killed
```

### Animal Behavior Matrix

| Behavior Type | Hostility | On Detection | When Wounded | When Cornered | Examples |
|---------------|-----------|--------------|--------------|---------------|----------|
| **Prey** | Non-hostile | Flee immediately | Flee faster | Flee (no fight) | Deer, Rabbit, Ptarmigan, Goat |
| **Dangerous Prey** | Non-hostile | Assess threat | Flee if can | Fight back | Bison, Auroch, Elk, Moose |
| **Predator** | Hostile | Attack if hungry | Attack harder | Fight to death | Wolf, Bear, Lynx, Hyena |
| **Scavenger** | Opportunistic | Assess player strength | Flee if outmatched | Fight if cornered | Fox, Wolverine, Marmot |

### Hunting Skill Progression

| Level | Unlocks | Stealth Bonus | Tracking | Special |
|-------|---------|---------------|----------|---------|
| 0 | Basic approach | +0% concealment | Can't track | - |
| 1 | Follow obvious tracks | +10% | Fresh tracks only | - |
| 2 | Set basic snares | +20% | Tracks < 1hr old | +10% bow accuracy |
| 3 | Ranged critical hits | +30% | Tracks < 4hr old | +20% bow accuracy |
| 4 | Advanced traps | +40% | Tracks < 12hr old | +30% bow accuracy |
| 5 | Master hunter | +50% | All tracks | Silent movement |

---

## MVP Implementation Plan (2 Weeks)

**Philosophy:** Build the minimum viable hunting loop first, test thoroughly, then iterate.

**MVP Scope:**
- 4-6 prey animals (Deer, Rabbit, Ptarmigan, Fox)
- 3 updated predators (Wolf, Bear, Cave Bear)
- Distance-based stealth approach
- Simple Bow + Stone Arrows
- Blood trail tracking (single-location only)

**Deferred to V2:**
- Traps system
- Simultaneous combat redesign
- Dangerous prey animals (Bison, Elk, Moose, Auroch)
- Multi-location blood trails
- Advanced AI (pack coordination, area learning)

---

### MVP Phase 1: Core Animals & Distance System (Days 1-3)

**Goal:** Animal foundation with distance tracking

**Tasks:**

**1.1: Create Animal Architecture** (Effort: M)
- Create `Actors/AnimalBehaviorType.cs` enum (Prey, Predator, Scavenger)
- Create `Actors/AnimalState.cs` enum (Idle, Alert, Detected)
- Create `Actors/Animal.cs` inheriting from `Npc`
- Add properties: `Behavior`, `State`, `Awareness`, `FleeThreshold`, `Distance` (meters from player)
- Modify `Actors/NPC.cs`: Make `IsHostile` public setter
- **Acceptance:** Animal class compiles, distance property exists

**1.2: Add Prey Animals** (Effort: M)
- Add to `NPCFactory.cs`:
  - Deer (50kg, Antlers 8dmg, Awareness 0.8)
  - Rabbit (3kg, Teeth 2dmg, Awareness 0.9)
  - Ptarmigan (0.5kg, Beak 1dmg, Awareness 0.85)
  - Fox (6kg, Teeth 4dmg, Awareness 0.7, Scavenger)
- Set: `IsHostile = false`, `Behavior = Prey/Scavenger`, `Distance = 100`
- Add appropriate loot (meat, pelts, sinew)
- **Acceptance:** 4 non-hostile animals spawn with distance = 100m

**1.3: Update Predators** (Effort: S)
- Modify Wolf, Bear, Cave Bear to use new system
- Set: `Behavior = Predator`, `Awareness = 0.75`, `Distance = 100`
- Keep `IsHostile = true`
- **Acceptance:** Predators use new behavior system

**1.4: Update Spawn Tables** (Effort: S)
- Modify `LocationFactory.cs` for Forest biome only (MVP scope)
- Add: Deer (2.0), Rabbit (3.0), Fox (1.5)
- Reduce: Wolf (1.0), Bear (0.5)
- **Acceptance:** Prey common, predators rare in Forest

**1.5: Test Animal Foundation** (Effort: S)
- Spawn test location with animals
- Verify non-hostile animals don't attack
- Verify predators attack as before
- Verify distance displays correctly
- **Acceptance:** Animals spawn and behave correctly

---

### MVP Phase 2: Stealth & Approach System (Days 4-6)

**Goal:** Distance-based stealth approach with detection

**Tasks:**

**2.1: Create StealthManager** (Effort: M)
- Create `PlayerComponents/StealthManager.cs`
- Properties: `IsSneaking`, `Concealment` (calculated from Hunting skill)
- Method: `CalculateConcealment()` - returns 0.0-1.0 based on skill
- Method: `DetectionCheck(Animal, distance)` - returns true if detected
- Add to Player constructor
- **Acceptance:** Player has StealthManager

**2.2: Create HuntingCalculator** (Effort: M)
- Create `Utils/HuntingCalculator.cs`
- Static method: `GetDistanceModifier(distance)` - 0.5 at 100m, 1.0 at 50m, 1.5 at 30m
- Static method: `CalculateDetectionChance(concealment, awareness, distanceMod)`
- Static method: `GetApproachDistance()` - random 20-30m
- **Acceptance:** Detection calculations work correctly

**2.3: Create Approach Action** (Effort: L)
- Add "Hunting" section to `ActionFactory.cs`
- Implement `ApproachAnimal(Animal)` action:
  - Takes 5-10 minutes
  - Reduces animal distance by 20-30m
  - Runs detection check
  - Updates animal state: Idle → Alert → Detected
  - Shows appropriate feedback
- **Acceptance:** Can approach animals, detection works

**2.4: Create Assess Action** (Effort: S)
- Implement `AssessAnimal(Animal)` action
- Shows: Animal type, behavior, weight, threat level, distance
- Recommends weapon type
- **Acceptance:** Can view animal information

**2.5: Animal Detection Response** (Effort: M)
- Implement `Animal.OnDetected(Player)` method:
  - Prey: Immediately flees (removes from location)
  - Predator: Becomes aggressive (triggers combat)
  - Scavenger: Assesses player health → flees or attacks
- **Acceptance:** Animals respond correctly to detection

**2.6: Test Stealth System** (Effort: M)
- Test approaching deer at different Hunting levels (0, 2, 5)
- Test detection at different distances
- Test Alert → Detected progression
- Balance concealment calculations
- **Acceptance:** Stealth feels balanced and skill-based

---

### MVP Phase 3: Bow Hunting (Days 7-10)

**Goal:** Ranged weapon system with arrows

**Tasks:**

**3.1: Create RangedWeapon Class** (Effort: M)
- Create `Items/RangedWeapon.cs` extending `Weapon`
- Properties: `Range`, `AmmoType`, `BaseAccuracy`
- Method: `GetAccuracyAtRange(distance, huntingSkill)`
  - Formula: `BaseAccuracy - (distance / Range) + (huntingSkill * 0.05)`
  - Clamp to 5%-95%
- **Acceptance:** RangedWeapon class compiles

**3.2: Create Bow & Arrow Items** (Effort: S)
- Add to `ItemFactory.cs`:
  - Simple Bow (30m range, 40% base accuracy, 12 dmg, Pierce)
  - Stone Arrow (0.05kg, Pierce damage)
- Set CraftingProperties
- **Acceptance:** Bow and arrows exist

**3.3: Add Crafting Recipes** (Effort: S)
- Add to `CraftingSystem.cs`:
  - Simple Bow (Wood 1kg + Sinew 0.2kg, 120 min, Crafting 2)
  - Stone Arrows x5 (Wood 0.5kg + Flint 0.25kg + Feather 0.05kg, 30 min, Crafting 1)
- **Acceptance:** Can craft bow and arrows

**3.4: Create AmmunitionManager** (Effort: M)
- Create `PlayerComponents/AmmunitionManager.cs`
- Method: `GetAmmoCount(ammoType)`
- Method: `ConsumeAmmo(ammoType, count)`
- Method: `AddAmmo(ammoType, count)`
- Method: `RecoverArrows(shotsFired, targetKilled)` - 50% on kill, 20% on miss
- Add to Player constructor
- **Acceptance:** Ammo tracking works

**3.5: Create HuntingManager** (Effort: XL)
- Create `PlayerComponents/HuntingManager.cs`
- Implement `RangedAttack(Animal, RangedWeapon, distance)`:
  1. Check ammo, consume 1 arrow
  2. Calculate accuracy (weapon + skill - range penalty)
  3. Apply stealth bonuses: +50% accuracy if undetected, +50% damage if Idle
  4. Hit check (SkillCheckCalculator)
  5. Determine hit location (random for now, skill-based in V2)
  6. Create DamageInfo (Pierce, weapon damage)
  7. Apply to `target.Body.Damage(damageInfo)`
  8. Award XP (2 on hit, 1 on miss)
  9. Return result
- Add to Player constructor
- **Acceptance:** Ranged attacks work with accuracy/damage

**3.6: Create Shoot Action** (Effort: M)
- Implement `ShootAnimal(Animal)` action:
  - Check player has ranged weapon equipped
  - Check in range (distance <= weapon.Range)
  - Execute RangedAttack()
  - Handle kill (show message, recover arrows, make lootable)
  - Handle wound (animal flees, blood trail created)
  - Handle miss (animal detects, flees/attacks)
- **Acceptance:** Can shoot animals from menu

**3.7: Test Bow Hunting** (Effort: L)
- Test full hunting loop: approach → shoot → loot
- Test accuracy at different ranges and skill levels
- Test arrow recovery rates
- Test stealth damage bonus
- Balance damage values
- **Acceptance:** Bow hunting feels satisfying and balanced

---

### MVP Phase 4: Blood Trail Tracking (Days 11-14)

**Goal:** Track wounded animals within single location

**Tasks:**

**4.1: Create BloodTrail System** (Effort: M)
- Add to `PlayerComponents/TrackingManager.cs` (create if doesn't exist):
  - `BloodTrail` class: `WoundedAnimal`, `TrailFreshness`, `TimeCreated`
  - Method: `CreateBloodTrail(Animal, woundSeverity)`
  - Method: `GetTrailFreshness()` - decays over time
  - Method: `FollowTrailCheck(huntingSkill)` - skill check based on freshness
- **Acceptance:** Blood trail system exists

**4.2: Integrate with Ranged Attack** (Effort: S)
- Modify `HuntingManager.RangedAttack()`:
  - If animal wounded but not killed, create blood trail
  - Wound severity based on damage percent
  - Animal flees (removed from location for MVP, hiding nearby)
- **Acceptance:** Non-lethal hits create blood trails

**4.3: Create Blood Trail Actions** (Effort: M)
- Implement `FollowBloodTrail(BloodTrail)` action:
  - Takes 15 minutes per attempt
  - Skill check based on trail freshness
  - Success: Find wounded animal, can finish it off
  - Failure: Trail gets colder, lose time
  - Multiple failures: Lose trail completely
- Show trail freshness to player ("very fresh", "moderate", "fading", "cold")
- **Acceptance:** Can track wounded animals

**4.4: Handle Wounded Animal Death** (Effort: S)
- If trail freshness < threshold OR multiple successful tracks:
  - Animal has bled out, find corpse
  - Full loot available
- **Acceptance:** Wounded animals can bleed out

**4.5: Test Blood Trail System** (Effort: M)
- Test wounding animal and tracking at different skill levels
- Test trail decay timing
- Test multiple failed tracking attempts
- Test finding dead animal vs catching wounded one
- Balance difficulty
- **Acceptance:** Blood trail system is tense and skill-based

**4.6: Integration Testing** (Effort: L)
- Full MVP test: Find deer → approach → shoot → wound → track → finish kill → loot
- Test with different animals (rabbit, ptarmigan, fox)
- Test predator encounters
- Test skill progression (Hunting 0 vs 5)
- Fix any bugs discovered
- **Acceptance:** Complete hunting loop works end-to-end

---

## Full Implementation (Deferred to V2)

*The detailed phases below are the full system design. These are deferred until MVP is tested and validated.*

### Phase 1: Animal Behavior Foundation (Days 1-3)

**Goal:** Create the architectural foundation for diverse animal types with distinct behaviors.

#### Tasks

**1.1: Create Animal Behavior Type System** (Effort: M)
- Create `Actors/AnimalBehaviorType.cs` enum
  ```csharp
  public enum AnimalBehaviorType
  {
      Prey,           // Flees immediately when detected
      DangerousPrey,  // Flees unless cornered or protecting young
      Predator,       // Attacks when hungry or threatened
      Scavenger       // Opportunistic - assesses player strength
  }
  ```
- Create `Actors/AnimalState.cs` enum
  ```csharp
  public enum AnimalState
  {
      Idle,       // Roaming, feeding, resting
      Alert,      // Detected player, assessing
      Fleeing,    // Running away
      Aggressive  // Attacking player
  }
  ```
- **Acceptance:** Enums compile, no errors

**1.2: Extract Animal Class from NPC** (Effort: M)
- Create `Actors/Animal.cs` inheriting from `Npc`
- Add properties:
  - `AnimalBehaviorType Behavior { get; }`
  - `AnimalState State { get; set; }`
  - `double Awareness { get; }` (0.0-1.0, affects detection range)
  - `double FleeThreshold { get; }` (HP % when they flee)
- Move animal-specific logic from `Npc` to `Animal`
- **Acceptance:** Animal class compiles, inherits from Npc correctly

**1.3: Modify NPC.cs for Behavior Support** (Effort: S)
- Make `IsHostile` property public setter (currently private)
- Add `State` property (default Idle)
- Ensure backward compatibility with human NPCs
- **Acceptance:** Build succeeds, no regressions

**1.4: Create IWildlife Interface** (Effort: S)
- Create `Actors/IWildlife.cs` for future AI behavior
  ```csharp
  public interface IWildlife
  {
      AnimalState State { get; set; }
      bool DetectPlayer(Player player, double distance);
      void OnDetected(Player player);
      void OnWounded(double damagePercent);
  }
  ```
- Animal implements IWildlife (stub methods for now)
- **Acceptance:** Interface defined, Animal implements it

**1.5: Test Foundation** (Effort: S)
- Create test animal in NPCFactory with behavior type
- Spawn in test location
- Verify animal properties display correctly
- **Acceptance:** Can create/spawn animal with behavior type

---

### Phase 2: New Animal Roster (Days 3-5)

**Goal:** Add 12+ new Ice Age animals across all behavior types with proper stats and loot.

#### Reference Stats Formula
- **Weight** = Realistic animal weight in kg
- **Muscle %** = 40-70% (prey lower, predators higher)
- **Fat %** = 10-35% (seasonal, Ice Age animals had higher fat)
- **Damage** = Weapon damage (antlers, horns, claws)
- **Loot** = Meat (weight * 0.3), Hide (1-2), Bones, Special items

#### Tasks

**2.1: Add Prey Animals (4 types)** (Effort: M)
- **Deer** (50kg, 45% muscle, 15% fat, Antlers 8dmg)
  - Loot: Medium Meat x2, Deer Hide, Antlers, Sinew
- **Rabbit** (3kg, 50% muscle, 20% fat, Teeth 2dmg)
  - Loot: Small Meat, Rabbit Pelt, Sinew
- **Ptarmigan** (0.5kg, 60% muscle, 15% fat, Beak 1dmg)
  - Loot: Tiny Meat, Feathers x3
- **Mountain Goat** (80kg, 50% muscle, 12% fat, Horns 10dmg)
  - Loot: Large Meat x2, Goat Hide, Horns, Sinew

Set: `IsHostile = false`, `Behavior = Prey`, `Awareness = 0.8`, `FleeThreshold = 1.0` (always flee)

**Acceptance:** 4 prey animals spawn as non-hostile, have correct stats/loot

**2.2: Add Dangerous Prey Animals (4 types)** (Effort: M)
- **Bison** (800kg, 55% muscle, 25% fat, Horns 18dmg)
  - Loot: Large Meat x6, Thick Hide x2, Bison Horns, Sinew x2
- **Auroch** (1000kg, 55% muscle, 20% fat, Massive Horns 22dmg)
  - Loot: Large Meat x8, Auroch Hide x2, Auroch Horns, Sinew x3
- **Elk** (400kg, 50% muscle, 18% fat, Antlers 15dmg)
  - Loot: Large Meat x4, Elk Hide x2, Elk Antlers, Sinew x2
- **Moose** (500kg, 52% muscle, 20% fat, Antlers 16dmg)
  - Loot: Large Meat x5, Moose Hide x2, Moose Antlers, Sinew x2

Set: `IsHostile = false`, `Behavior = DangerousPrey`, `Awareness = 0.6`, `FleeThreshold = 0.5` (flee when half HP)

**Acceptance:** 4 dangerous prey spawn as non-hostile but will fight if cornered

**2.3: Add Scavenger Animals (4 types)** (Effort: M)
- **Fox** (6kg, 55% muscle, 20% fat, Teeth 4dmg)
  - Loot: Small Meat, Fox Pelt (warm), Sinew
- **Beaver** (20kg, 50% muscle, 25% fat, Teeth 6dmg)
  - Loot: Medium Meat, Beaver Pelt (waterproof), Sinew
- **Marmot** (5kg, 45% muscle, 30% fat, Teeth 3dmg)
  - Loot: Small Meat, Marmot Pelt, Sinew
- **Wolverine** (18kg, 65% muscle, 20% fat, Claws 8dmg)
  - Loot: Medium Meat, Wolverine Pelt (tough), Claws, Sinew

Set: `IsHostile = false`, `Behavior = Scavenger`, `Awareness = 0.7`, `FleeThreshold = 0.4`

**Acceptance:** 4 scavengers spawn, can assess player before attacking/fleeing

**2.4: Update Existing Predators** (Effort: S)
- Modify Wolf, Bear, Cave Bear to use new system
- Set: `IsHostile = true`, `Behavior = Predator`, `Awareness = 0.75`, `FleeThreshold = 0.2`
- **Acceptance:** Existing predators use new behavior system

**2.5: Update Location Spawn Tables** (Effort: M)
- `Environments/LocationFactory.cs` - Update all biome NpcTables
- **Forest**: Add Deer (2.0), Rabbit (3.0), Fox (1.5), reduce Wolf to (1.0)
- **Plains**: Add Bison (1.5), Elk (1.0), Ptarmigan (2.0), Wolverine (0.5)
- **Hillside**: Add Mountain Goat (2.0), Marmot (2.0)
- **Riverbank**: Add Beaver (2.5), reduce predators
- **Cave**: Keep predators dominant (Bear, Wolverine)
- Adjust spawn chances: Prey 50-60%, Scavengers 20-30%, Predators 10-20%

**Acceptance:** New animals spawn in appropriate biomes, balanced distribution

---

### Phase 3: Stealth & Tracking System (Days 5-8)

**Goal:** Implement stealth approach, animal detection, and tracking mechanics.

#### Tasks

**3.1: Create StealthManager Component** (Effort: L)
- Create `PlayerComponents/StealthManager.cs`
  ```csharp
  public class StealthManager
  {
      private Player _player;
      public bool IsSneaking { get; set; }
      public double Concealment { get; private set; } // 0.0-1.0

      public void CalculateConcealment()
      {
          // Base: Hunting skill * 0.1 (0-50% at skill 5)
          // Bonus: Clothing (ghillie = +20%), weather (fog = +30%), night (+20%)
          // Penalty: Movement speed, fire nearby (-20%), wind direction
      }

      public bool DetectionCheck(Animal target, double distance)
      {
          // Compare: Concealment vs (Animal.Awareness * distance modifier)
          // Return: true if detected, false if hidden
      }
  }
  ```
- Add to Player constructor
- **Acceptance:** Player has StealthManager, concealment calculates correctly

**3.2: Implement Detection System** (Effort: L)
- Create `Utils/HuntingCalculator.cs`
  ```csharp
  public static class HuntingCalculator
  {
      public static bool IsDetected(double concealment, double awareness, double distance)
      {
          // Distance modifier: 1.0 at 50m, 0.5 at 100m, 2.0 at 25m
          // Detection threshold = awareness * distanceModifier
          // Detected if concealment < threshold
          return concealment < (awareness * GetDistanceModifier(distance));
      }

      public static double GetStealthSuccessChance(int huntingSkill, AnimalBehaviorType behavior)
      {
          // Base: 20% + (huntingSkill * 10%)
          // Modified by behavior: Prey easier (+10%), Predator harder (-10%)
      }
  }
  ```
- **Acceptance:** Detection logic works, prey easier to sneak up on than predators

**3.3: Create Tracking System** (Effort: M)
- Create `PlayerComponents/TrackingManager.cs`
  ```csharp
  public class TrackingManager
  {
      public List<AnimalTrack> KnownTracks { get; } = new();

      public AnimalTrack? FindTrack(int huntingSkill)
      {
          // Skill check to find track during foraging
          // Success chance: 10% + (huntingSkill * 5%)
          // Return: Track with animal type, direction, freshness
      }

      public Animal? FollowTrack(AnimalTrack track, Location currentLocation)
      {
          // Navigate to track location, find animal if still there
          // Freshness affects success: <1hr = 80%, <4hr = 50%, <12hr = 20%
      }
  }

  public class AnimalTrack
  {
      public string AnimalType { get; set; }
      public Direction Direction { get; set; }
      public TimeSpan Age { get; set; }
      public Location Location { get; set; }
  }
  ```
- **Acceptance:** Can find and follow tracks based on Hunting skill

**3.4: Integrate Tracking with Foraging** (Effort: M)
- Modify `Environments/ForageFeature.cs`
- Add track finding to `Forage()` method
  ```csharp
  // After normal foraging results
  if (SkillCheck for Hunting) {
      var track = TrackingManager.FindTrack(actor.Skills.Hunting.Level);
      if (track != null) {
          Output.WriteLine($"You notice {track.AnimalType} tracks heading {track.Direction}...");
          actor.TrackingManager.KnownTracks.Add(track);
      }
  }
  ```
- **Acceptance:** Foraging can find animal tracks, displayed to player

**3.5: Create Hunting Actions** (Effort: L)
- Add "Hunting" section to `Actions/ActionFactory.cs`
  ```csharp
  public static IGameAction ToggleStealth()
  {
      return CreateAction("Toggle Stealth Mode")
          .Do(ctx => {
              ctx.player.StealthManager.IsSneaking = !ctx.player.StealthManager.IsSneaking;
              string mode = ctx.player.StealthManager.IsSneaking ? "ON" : "OFF";
              Output.WriteLine($"Stealth mode: {mode}");
              Output.WriteLine($"Concealment: {ctx.player.StealthManager.Concealment:P0}");
          })
          .ThenReturn()
          .Build();
  }

  public static IGameAction ApproachAnimal(Animal target)
  {
      return CreateAction($"Approach {target.Name}")
          .When(ctx => !target.IsEngaged && target.IsFound)
          .Do(ctx => {
              bool detected = ctx.player.StealthManager.DetectionCheck(target, distance: 50);
              if (detected) {
                  target.OnDetected(ctx.player);
                  Output.WriteLine($"The {target.Name} noticed you!");
              } else {
                  Output.WriteLine($"You carefully approach the {target.Name}...");
              }
          })
          .ThenShow(ctx => [
              AssessAnimal(target),
              HuntAnimal(target),
              Common.Return()
          ])
          .Build();
  }

  public static IGameAction AssessAnimal(Animal target)
  {
      return CreateAction($"Assess {target.Name}")
          .Do(ctx => {
              Output.WriteLine($"\n=== {target.Name} ===");
              Output.WriteLine($"Type: {target.Behavior}");
              Output.WriteLine($"Weight: {target.Body.Weight:F1} kg");
              Output.WriteLine($"Threat Level: {GetThreatLevel(target)}");
              Output.WriteLine($"Recommended Weapon: {GetRecommendedWeapon(target)}");
          })
          .ThenReturn()
          .Build();
  }

  public static IGameAction FollowTrack(AnimalTrack track)
  {
      return CreateAction($"Follow {track.AnimalType} tracks ({track.Age})")
          .TakesMinutes(15 + (int)track.Age.TotalMinutes / 4) // Older = slower
          .Do(ctx => {
              var animal = ctx.player.TrackingManager.FollowTrack(track, ctx.currentLocation);
              if (animal != null) {
                  Output.WriteLine($"You found a {animal.Name}!");
                  animal.IsFound = true;
              } else {
                  Output.WriteLine("The trail went cold...");
              }
          })
          .ThenReturn()
          .Build();
  }
  ```
- **Acceptance:** Can toggle stealth, approach animals, assess threat, follow tracks

**3.6: Test Stealth System** (Effort: S)
- Create playtest scenario: spawn deer, sneak up with different Hunting levels
- Verify: Higher Hunting = better concealment, prey easier than predators
- **Acceptance:** Stealth works, detection feels balanced

---

### Phase 4: Ranged Weapons & Hunting Attacks (Days 8-12)

**Goal:** Implement bows, arrows, and ranged hunting mechanics with skill-based accuracy.

#### Tasks

**4.1: Create RangedWeapon Class** (Effort: M)
- Create `Items/RangedWeapon.cs` extending `Weapon`
  ```csharp
  public class RangedWeapon : Weapon
  {
      public int Range { get; }           // Max effective range (meters)
      public string AmmoType { get; }     // "Arrow", "SpearThrown", "Rock"
      public double BaseAccuracy { get; } // 0.0-1.0, modified by skill
      public double DrawTime { get; }     // Minutes to ready weapon

      public double GetAccuracyAtRange(double distance, int huntingSkill)
      {
          // Accuracy = BaseAccuracy - (distance / Range) + (huntingSkill * 0.05)
          // Clamped 5%-95%
      }
  }
  ```
- **Acceptance:** RangedWeapon class compiles, extends Weapon correctly

**4.2: Modify Weapon.cs for Ranged Support** (Effort: S)
- Add properties:
  - `bool IsRanged { get; }`
  - `int Range { get; }` (default 0 for melee)
  - `string AmmoType { get; }` (null for melee)
- Update constructor to accept optional ranged parameters
- **Acceptance:** Weapon.cs supports both melee and ranged weapons

**4.3: Create Ammunition System** (Effort: M)
- Create `PlayerComponents/AmmunitionManager.cs`
  ```csharp
  public class AmmunitionManager
  {
      private Dictionary<string, int> _ammo = new();

      public int GetAmmoCount(string ammoType) => _ammo.GetValueOrDefault(ammoType, 0);
      public void AddAmmo(string ammoType, int count) => _ammo[ammoType] += count;
      public bool ConsumeAmmo(string ammoType, int count = 1);

      public int RecoverArrows(int shotsFired, bool targetKilled)
      {
          // 50% recovery on kill, 20% on miss (arrows lost in environment)
          int recovered = targetKilled
              ? (int)(shotsFired * 0.5)
              : (int)(shotsFired * 0.2);
          return Math.Max(recovered, 0);
      }
  }
  ```
- Add to Player constructor
- **Acceptance:** Can track/consume/recover ammunition

**4.4: Create Bow & Arrow Items** (Effort: M)
- Modify `Items/ItemFactory.cs` - Add bow methods:
  ```csharp
  public static RangedWeapon MakeSimpleBow()
  {
      return new RangedWeapon(
          name: "Simple Bow",
          weight: 1.2,
          damage: 12,
          accuracy: 0.6,
          damageType: DamageType.Pierce,
          range: 30,
          ammoType: "Arrow",
          baseAccuracy: 0.4
      ) {
          CraftingProperties = new[] { ItemProperty.Wood, ItemProperty.Ranged }
      };
  }

  public static RangedWeapon MakeRecurveBow()
  {
      return new RangedWeapon(
          name: "Recurve Bow",
          weight: 1.5,
          damage: 18,
          accuracy: 0.75,
          damageType: DamageType.Pierce,
          range: 50,
          ammoType: "Arrow",
          baseAccuracy: 0.6
      ) {
          CraftingProperties = new[] { ItemProperty.Wood, ItemProperty.Bone, ItemProperty.Ranged }
      };
  }

  public static Item MakeStoneArrow()
  {
      return new Item("Stone Arrow", 0.05, new[] { ItemProperty.Ammunition })
      {
          Description = "Arrow with flint tip"
      };
  }

  public static Item MakeBoneArrow()
  {
      return new Item("Bone Arrow", 0.04, new[] { ItemProperty.Ammunition })
      {
          Description = "Arrow with sharpened bone tip"
      };
  }
  ```
- **Acceptance:** Bow and arrow items exist with correct stats

**4.5: Add Bow & Arrow Crafting Recipes** (Effort: M)
- Modify `Crafting/CraftingSystem.cs` - Add recipes:
  ```csharp
  var simpleBow = new RecipeBuilder()
      .Named("Simple Bow")
      .RequiringCraftingTime(120) // 2 hours
      .WithPropertyRequirement(ItemProperty.Wood, 1.0)
      .WithPropertyRequirement(ItemProperty.Sinew, 0.2)
      .WithPropertyRequirement(ItemProperty.Sharp, 0.1) // Knife to carve
      .RequiringSkill("Crafting", 2)
      .ResultingInItem(ItemFactory.MakeSimpleBow)
      .Build();

  var recurveBow = new RecipeBuilder()
      .Named("Recurve Bow")
      .RequiringCraftingTime(180) // 3 hours
      .WithPropertyRequirement(ItemProperty.Wood, 1.5)
      .WithPropertyRequirement(ItemProperty.Sinew, 0.3)
      .WithPropertyRequirement(ItemProperty.Bone, 0.5)
      .WithPropertyRequirement(ItemProperty.Sharp, 0.1)
      .RequiringSkill("Crafting", 3)
      .ResultingInItem(ItemFactory.MakeRecurveBow)
      .Build();

  var stoneArrows = new RecipeBuilder()
      .Named("Stone Arrows (x5)")
      .RequiringCraftingTime(30) // 30 min for 5 arrows
      .WithPropertyRequirement(ItemProperty.Wood, 0.5)
      .WithPropertyRequirement(ItemProperty.Flint, 0.25)
      .WithPropertyRequirement(ItemProperty.Feather, 0.05)
      .RequiringSkill("Crafting", 1)
      .ResultingInItems(() => Enumerable.Range(0, 5).Select(_ => ItemFactory.MakeStoneArrow()).ToList())
      .Build();
  ```
- **Acceptance:** Can craft bows and arrows with proper materials

**4.6: Create HuntingManager Component** (Effort: XL)
- Create `PlayerComponents/HuntingManager.cs`
  ```csharp
  public class HuntingManager
  {
      private Player _player;

      public DamageResult RangedAttack(Animal target, RangedWeapon weapon, double distance)
      {
          // 1. Check ammunition
          if (!_player.AmmunitionManager.ConsumeAmmo(weapon.AmmoType, 1)) {
              Output.WriteLine("Out of ammunition!");
              return null;
          }

          // 2. Calculate accuracy
          int huntingSkill = _player.Skills.Hunting.Level;
          double accuracy = weapon.GetAccuracyAtRange(distance, huntingSkill);

          // 3. Stealth bonus
          if (_player.StealthManager.IsSneaking && target.State != AnimalState.Alert) {
              accuracy *= 1.5; // +50% accuracy when undetected
          }

          // 4. Hit check
          bool hit = SkillCheckCalculator.SuccessCheck(accuracy);
          if (!hit) {
              Output.WriteLine($"Your arrow missed the {target.Name}!");
              target.OnDetected(_player); // Animal now knows you're here
              return null;
          }

          // 5. Damage calculation
          int baseDamage = weapon.Damage;

          // 6. Critical hit check (headshot instant kill on small game)
          bool criticalHit = false;
          BodyPart targetPart = DetermineHitLocation(target, huntingSkill);

          if (targetPart.Name.Contains("Head") && target.Body.Weight < 50) {
              criticalHit = true;
              baseDamage *= 3;
          }

          // 7. Stealth damage bonus
          if (_player.StealthManager.IsSneaking && target.State == AnimalState.Idle) {
              baseDamage = (int)(baseDamage * 1.5); // +50% damage on unaware targets
          }

          // 8. Apply damage
          var damageInfo = new DamageInfo {
              Amount = baseDamage,
              Type = weapon.DamageType,
              Attacker = _player,
              BodyPartTargeted = targetPart
          };

          var result = target.Body.Damage(damageInfo);

          // 9. Award XP
          _player.Skills.Hunting.GainExperience(hit ? 2 : 1);

          return result;
      }

      private BodyPart DetermineHitLocation(Animal target, int huntingSkill)
      {
          // Higher skill = better targeting
          // Skill 0-1: Random hit
          // Skill 2-3: Can choose region (torso, head, legs)
          // Skill 4-5: Can choose specific part

          if (huntingSkill >= 4) {
              // Player chooses exact body part
              return ChooseBodyPart(target);
          } else if (huntingSkill >= 2) {
              // Player chooses region, random part within
              return ChooseRegion(target);
          } else {
              // Random hit
              return target.Body.GetRandomPart();
          }
      }
  }
  ```
- Add to Player constructor
- **Acceptance:** Can perform ranged attacks with accuracy/damage calculations

**4.7: Create Shooting Actions** (Effort: L)
- Add to `Actions/ActionFactory.cs`
  ```csharp
  public static IGameAction ShootAnimal(Animal target)
  {
      return CreateAction($"Shoot {target.Name}")
          .When(ctx => ctx.player.Inventory.GetEquippedWeapon() is RangedWeapon)
          .Do(ctx => {
              var weapon = ctx.player.Inventory.GetEquippedWeapon() as RangedWeapon;
              var result = ctx.player.HuntingManager.RangedAttack(target, weapon, distance: 30);

              if (result != null) {
                  Output.WriteLine($"Hit! {result.Description}");

                  if (target.Body.IsDead) {
                      Output.WriteLine($"The {target.Name} is dead.");
                      // Recover arrows
                      int recovered = ctx.player.AmmunitionManager.RecoverArrows(1, true);
                      if (recovered > 0) {
                          Output.WriteLine($"You recovered {recovered} arrow(s).");
                      }
                  } else {
                      // Wounded animal response
                      target.OnWounded(result.DamagePercent);
                  }
              }
          })
          .ThenShow(ctx => {
              if (target.IsAlive && target.State == AnimalState.Aggressive) {
                  return new[] { Combat.StartCombat(target) }; // Animal attacks back
              } else {
                  return new[] { Hunting.ContinueHunt(target) }; // Track wounded animal
              }
          })
          .Build();
  }

  public static IGameAction TargetedShot(Animal target)
  {
      return CreateAction($"Targeted Shot")
          .When(ctx => ctx.player.Skills.Hunting.Level >= 2)
          .Do(ctx => {
              Output.WriteLine("Choose target area:");
              // Show body part selection menu
          })
          .ThenShow(ctx => [/* body part selection */])
          .Build();
  }
  ```
- **Acceptance:** Can shoot animals, hit/miss/damage works correctly

**4.8: Test Ranged Hunting** (Effort: M)
- Playtest scenario: Craft bow, hunt deer with different Hunting levels
- Verify:
  - Accuracy scales with skill
  - Stealth bonus applies correctly
  - Headshots kill small game
  - Arrow recovery works
  - Wounded animals respond appropriately
- **Acceptance:** Bow hunting feels tactical and skill-based

---

### Phase 5: Trap System (Days 12-15)

**Goal:** Implement craftable traps for passive hunting over time.

#### Tasks

**5.1: Create Trap Item Class** (Effort: M)
- Create `Items/Trap.cs`
  ```csharp
  public class Trap : Item
  {
      public string TrapType { get; }         // Snare, Deadfall, Pitfall
      public double SuccessRate { get; }      // 0.0-1.0 chance to catch
      public int MaxAnimalSize { get; }       // Max kg animal can catch
      public bool IsSet { get; set; }
      public TimeSpan TimeSet { get; set; }
      public Location? Location { get; set; }

      public bool CanCatch(Animal animal)
      {
          return animal.Body.Weight <= MaxAnimalSize;
      }

      public bool TriggerCheck(Animal animal)
      {
          // Success rate modified by animal behavior
          double adjustedRate = SuccessRate;
          if (animal.Behavior == AnimalBehaviorType.Prey) {
              adjustedRate *= 1.2; // Prey easier to trap
          } else if (animal.Behavior == AnimalBehaviorType.Predator) {
              adjustedRate *= 0.7; // Predators avoid traps
          }

          return SkillCheckCalculator.SuccessCheck(adjustedRate);
      }
  }
  ```
- **Acceptance:** Trap class compiles with proper properties

**5.2: Create Trap Items** (Effort: S)
- Add to `Items/ItemFactory.cs`
  ```csharp
  public static Trap MakeRabbitSnare()
  {
      return new Trap(
          name: "Rabbit Snare",
          weight: 0.3,
          trapType: "Snare",
          successRate: 0.5,
          maxAnimalSize: 5
      ) {
          CraftingProperties = new[] { ItemProperty.Sinew, ItemProperty.Binding },
          Description = "Simple snare for small game"
      };
  }

  public static Trap MakeLargeSnare()
  {
      return new Trap(
          name: "Large Snare",
          weight: 1.0,
          trapType: "Snare",
          successRate: 0.4,
          maxAnimalSize: 50
      ) {
          CraftingProperties = new[] { ItemProperty.Sinew, ItemProperty.Wood, ItemProperty.Binding },
          Description = "Rope snare for medium game"
      };
  }

  public static Trap MakeDeadfallTrap()
  {
      return new Trap(
          name: "Deadfall Trap",
          weight: 8.0,
          trapType: "Deadfall",
          successRate: 0.6,
          maxAnimalSize: 100
      ) {
          CraftingProperties = new[] { ItemProperty.Stone, ItemProperty.Wood },
          Description = "Heavy stone trap for large game"
      };
  }
  ```
- **Acceptance:** 3 trap types created with appropriate stats

**5.3: Add Trap Crafting Recipes** (Effort: S)
- Add to `Crafting/CraftingSystem.cs`
  ```csharp
  var rabbitSnare = new RecipeBuilder()
      .Named("Rabbit Snare")
      .RequiringCraftingTime(20)
      .WithPropertyRequirement(ItemProperty.Sinew, 0.2)
      .RequiringSkill("Hunting", 2)
      .ResultingInItem(ItemFactory.MakeRabbitSnare)
      .Build();

  var largeSnare = new RecipeBuilder()
      .Named("Large Snare")
      .RequiringCraftingTime(45)
      .WithPropertyRequirement(ItemProperty.Sinew, 0.5)
      .WithPropertyRequirement(ItemProperty.Wood, 0.5)
      .RequiringSkill("Hunting", 3)
      .ResultingInItem(ItemFactory.MakeLargeSnare)
      .Build();

  var deadfallTrap = new RecipeBuilder()
      .Named("Deadfall Trap")
      .RequiringCraftingTime(60)
      .WithPropertyRequirement(ItemProperty.Stone, 5.0)
      .WithPropertyRequirement(ItemProperty.Wood, 2.0)
      .RequiringSkill("Hunting", 4)
      .ResultingInItem(ItemFactory.MakeDeadfallTrap)
      .Build();
  ```
- **Acceptance:** Can craft traps with proper skill/material requirements

**5.4: Create TrapFeature for Locations** (Effort: M)
- Create `Environments/TrapFeature.cs`
  ```csharp
  public class TrapFeature : LocationFeature
  {
      public Trap Trap { get; }
      public Animal? CaughtAnimal { get; private set; }
      public TimeSpan TimeSet { get; set; }

      public TrapFeature(Trap trap)
      {
          Trap = trap;
          TimeSet = World.CurrentTime;
      }

      public void CheckTrap(Location location)
      {
          // If already has catch, skip
          if (CaughtAnimal != null) return;

          // Find eligible animals at location
          var eligibleAnimals = location.NPCs
              .OfType<Animal>()
              .Where(a => Trap.CanCatch(a) && a.IsAlive)
              .ToList();

          if (!eligibleAnimals.Any()) return;

          // Pick random animal, attempt capture
          var target = eligibleAnimals[Random.Shared.Next(eligibleAnimals.Count)];

          if (Trap.TriggerCheck(target)) {
              CaughtAnimal = target;
              target.State = AnimalState.Trapped; // New state
              Output.WriteLine($"[Trap Check] Your {Trap.Name} caught a {target.Name}!");
          }
      }

      public override void Update(TimeSpan elapsed)
      {
          // Traps check every hour
          if ((World.CurrentTime - TimeSet).TotalHours >= 1.0) {
              CheckTrap(ParentLocation);
              TimeSet = World.CurrentTime;
          }
      }
  }
  ```
- **Acceptance:** TrapFeature exists, can check for catches

**5.5: Implement Trap Placement Actions** (Effort: M)
- Add to `Actions/ActionFactory.cs`
  ```csharp
  public static IGameAction SetTrap(Trap trap)
  {
      return CreateAction($"Set {trap.Name}")
          .When(ctx => ctx.player.Inventory.HasItem(trap))
          .TakesMinutes(30 + (trap.Weight * 5)) // Heavier = longer to set
          .Do(ctx => {
              // Remove from inventory
              ctx.player.Inventory.RemoveItem(trap);

              // Add TrapFeature to location
              var trapFeature = new TrapFeature(trap) {
                  ParentLocation = ctx.currentLocation
              };
              ctx.currentLocation.Features.Add(trapFeature);

              Output.WriteLine($"You set the {trap.Name}. Check back later to see if you caught anything.");

              // Award XP
              ctx.player.Skills.Hunting.GainExperience(1);
          })
          .ThenReturn()
          .Build();
  }

  public static IGameAction CheckTraps()
  {
      return CreateAction("Check Traps")
          .When(ctx => ctx.currentLocation.Features.OfType<TrapFeature>().Any())
          .Do(ctx => {
              var traps = ctx.currentLocation.Features.OfType<TrapFeature>().ToList();

              Output.WriteLine($"\n=== Checking {traps.Count} Trap(s) ===");

              foreach (var trapFeature in traps) {
                  if (trapFeature.CaughtAnimal != null) {
                      Output.WriteLine($"✓ {trapFeature.Trap.Name}: Caught {trapFeature.CaughtAnimal.Name}!");
                  } else {
                      var timeSet = World.CurrentTime - trapFeature.TimeSet;
                      Output.WriteLine($"○ {trapFeature.Trap.Name}: Empty (set {timeSet.TotalHours:F1}h ago)");
                  }
              }
          })
          .ThenShow(ctx => {
              var trapsWithCatch = ctx.currentLocation.Features
                  .OfType<TrapFeature>()
                  .Where(t => t.CaughtAnimal != null)
                  .ToList();

              var actions = trapsWithCatch
                  .Select(t => CollectTrap(t))
                  .Cast<IGameAction>()
                  .ToList();

              actions.Add(Common.Return());
              return actions;
          })
          .Build();
  }

  public static IGameAction CollectTrap(TrapFeature trapFeature)
  {
      return CreateAction($"Collect {trapFeature.Trap.Name} (has {trapFeature.CaughtAnimal?.Name})")
          .TakesMinutes(5)
          .Do(ctx => {
              // Dispatch caught animal
              if (trapFeature.CaughtAnimal != null) {
                  trapFeature.CaughtAnimal.Body.Kill("trapped and dispatched");
                  Output.WriteLine($"You dispatched the {trapFeature.CaughtAnimal.Name}.");

                  // Award XP
                  ctx.player.Skills.Hunting.GainExperience(3);
              }

              // Return trap to inventory
              ctx.player.Inventory.AddItem(trapFeature.Trap);

              // Remove from location
              ctx.currentLocation.Features.Remove(trapFeature);

              Output.WriteLine($"You collected your {trapFeature.Trap.Name}.");
          })
          .ThenReturn()
          .Build();
  }
  ```
- **Acceptance:** Can set traps, check them, collect catches

**5.6: Integrate Traps with Location.Update()** (Effort: S)
- Modify `Environments/Location.cs`
  ```csharp
  public void Update()
  {
      _npcs.ForEach(n => n.Update());

      // Update features (including traps)
      Features.ForEach(f => f.Update(TimeSpan.FromMinutes(1)));
  }
  ```
- **Acceptance:** Traps check automatically during world updates

**5.7: Test Trap System** (Effort: M)
- Playtest scenario: Set rabbit snare, wait 2 hours, check trap
- Verify:
  - Traps check correctly during time passage
  - Success rate feels balanced
  - Can collect trap + catch
  - Different trap types work for different animals
- **Acceptance:** Trap system functional and satisfying

---

### Phase 6: Lethal Combat Redesign (Days 15-18)

**Goal:** Transform combat from turn-based spam into tactical, deadly encounters.

#### Design Philosophy
- Combat should be **rare** (most animals flee or are hunted passively)
- Combat should be **lethal** (serious injuries/death in 1-3 exchanges)
- Combat should be **tactical** (positioning, timing, weapon choice matter)
- Combat should have **consequences** (bleeding, shock, permanent injuries)

#### Tasks

**6.1: Redesign CombatManager for Lethality** (Effort: L)
- Modify `PlayerComponents/CombatManager.cs`
  ```csharp
  // Increase damage multipliers
  public const double LETHAL_DAMAGE_MULTIPLIER = 3.0; // 3x current damage

  public DamageResult Attack(Actor target, Weapon weapon, CombatAction action)
  {
      // 1. Determine hit (unchanged)
      bool hit = DetermineHit(weapon);
      if (!hit) return MissResult();

      // 2. Check dodge (now more impactful)
      bool dodged = DetermineDodge(target);
      if (dodged) {
          Output.WriteLine($"{target.Name} dodged your attack!");
          return DodgeResult();
      }

      // 3. Calculate damage (NEW - much higher)
      int baseDamage = weapon.Damage;
      baseDamage *= LETHAL_DAMAGE_MULTIPLIER; // 3x damage

      // 4. Apply combat action modifier
      baseDamage = action switch {
          CombatAction.PowerAttack => (int)(baseDamage * 1.5),
          CombatAction.QuickAttack => (int)(baseDamage * 0.7),
          CombatAction.TargetedStrike => baseDamage, // Precision over power
          _ => baseDamage
      };

      // 5. Critical hit check (NEW - vital organ hits)
      BodyPart targetPart = DetermineHitLocation(target, action);
      bool critical = IsCriticalHit(targetPart);
      if (critical) {
          baseDamage *= 2;
          Output.WriteLine("CRITICAL HIT!");
      }

      // 6. Apply damage through body system
      var damageInfo = new DamageInfo {
          Amount = baseDamage,
          Type = weapon.DamageType,
          Attacker = this.Actor,
          BodyPartTargeted = targetPart
      };

      var result = target.Body.Damage(damageInfo);

      // 7. Check for shock/unconsciousness (NEW)
      CheckForShock(target, result);

      return result;
  }

  private bool IsCriticalHit(BodyPart part)
  {
      // Critical if hit vital organ or major artery
      return part.Name.Contains("Heart") ||
             part.Name.Contains("Head") ||
             part.Name.Contains("Artery") ||
             part.Name.Contains("Lung");
  }

  private void CheckForShock(Actor target, DamageResult result)
  {
      // Major damage or blood loss can cause shock
      if (result.DamagePercent > 0.3 || target.Body.BloodPercent < 0.5) {
          double shockChance = result.DamagePercent + (1.0 - target.Body.BloodPercent);
          if (SkillCheckCalculator.SuccessCheck(shockChance)) {
              // Apply Shock effect (reduces consciousness, may cause knockout)
              target.EffectRegistry.AddEffect(
                  EffectBuilder.CreateEffect("Shock")
                      .WithSeverity(result.DamagePercent)
                      .ReducesCapacity(CapacityType.Consciousness, result.DamagePercent)
                      .Build()
              );
          }
      }
  }
  ```
- **Acceptance:** Combat deals 3x damage, critical hits work

**6.2: Create Combat Action Enum** (Effort: S)
- Create `PlayerComponents/CombatAction.cs`
  ```csharp
  public enum CombatAction
  {
      QuickAttack,    // Fast, lower damage, harder to dodge
      PowerAttack,    // Slow, high damage, easier to dodge
      TargetedStrike, // Precise, aims for vitals
      DefensiveStance,// Skip attack, +50% dodge/block
      Feint,          // Skill check to lower enemy defense
      Disengage       // Attempt to flee
  }
  ```
- **Acceptance:** Enum compiles, ready for use

**6.3: Implement Tactical Combat Actions** (Effort: L)
- Modify `Actions/ActionFactory.cs` - Redesign combat menu
  ```csharp
  public static IGameAction PlayerCombatTurn(Npc enemy)
  {
      return CreateAction($"Your turn")
          .ThenShow(ctx => new[] {
              QuickAttack(enemy),
              PowerAttack(enemy),
              TargetedStrike(enemy),
              DefensiveStance(enemy),
              Feint(enemy),
              Disengage(enemy)
          })
          .Build();
  }

  public static IGameAction QuickAttack(Npc enemy)
  {
      return CreateAction("Quick Attack")
          .Do(ctx => {
              var result = ctx.player.CombatManager.Attack(enemy,
                  ctx.player.Inventory.GetEquippedWeapon(),
                  CombatAction.QuickAttack);
              CombatNarrator.NarrateAttack(result);
          })
          .ThenShow(ctx => CombatResolution(enemy))
          .Build();
  }

  public static IGameAction PowerAttack(Npc enemy)
  {
      return CreateAction("Power Attack (high damage, easier to dodge)")
          .Do(ctx => {
              var result = ctx.player.CombatManager.Attack(enemy,
                  ctx.player.Inventory.GetEquippedWeapon(),
                  CombatAction.PowerAttack);
              CombatNarrator.NarrateAttack(result);
          })
          .ThenShow(ctx => CombatResolution(enemy))
          .Build();
  }

  public static IGameAction TargetedStrike(Npc enemy)
  {
      return CreateAction("Targeted Strike (choose body part)")
          .When(ctx => ctx.player.Skills.Fighting.Level >= 2)
          .ThenShow(ctx => {
              // Show body part selection menu
              var parts = enemy.Body.Parts.Select(p =>
                  CreateAction($"Strike {p.Name}")
                      .Do(_ => {
                          var result = ctx.player.CombatManager.Attack(enemy,
                              ctx.player.Inventory.GetEquippedWeapon(),
                              CombatAction.TargetedStrike,
                              targetPart: p);
                          CombatNarrator.NarrateAttack(result);
                      })
                      .ThenShow(_ => CombatResolution(enemy))
                      .Build()
              ).ToList();

              parts.Add(Common.Return());
              return parts;
          })
          .Build();
  }

  public static IGameAction DefensiveStance(Npc enemy)
  {
      return CreateAction("Defensive Stance (skip attack, +50% dodge/block)")
          .Do(ctx => {
              ctx.player.CombatManager.SetDefensiveBonus(0.5);
              Output.WriteLine("You take a defensive stance...");
          })
          .ThenShow(ctx => new[] { EnemyCombatTurn(enemy) })
          .Build();
  }

  public static IGameAction Feint(Npc enemy)
  {
      return CreateAction("Feint (Fighting check to lower enemy defense)")
          .When(ctx => ctx.player.Skills.Fighting.Level >= 3)
          .Do(ctx => {
              double successChance = SkillCheckCalculator.CalculateSuccessChance(
                  baseChance: 0.4,
                  skillLevel: ctx.player.Skills.Fighting.Level,
                  skillDC: 3
              );

              bool success = SkillCheckCalculator.SuccessCheck(successChance);

              if (success) {
                  enemy.CombatManager.SetDefensiveBonus(-0.3); // -30% defense
                  Output.WriteLine($"You feinted! The {enemy.Name}'s defense is lowered.");
                  ctx.player.Skills.Fighting.GainExperience(2);
              } else {
                  Output.WriteLine("Your feint failed.");
                  ctx.player.Skills.Fighting.GainExperience(1);
              }
          })
          .ThenShow(ctx => new[] { EnemyCombatTurn(enemy) })
          .Build();
  }

  public static IGameAction Disengage(Npc enemy)
  {
      return CreateAction("Disengage (safer flee attempt)")
          .Do(ctx => {
              // Defensive bonus makes enemy attack less effective
              ctx.player.CombatManager.SetDefensiveBonus(0.3);

              bool success = CombatUtils.SpeedCheck(ctx.player, enemy);

              if (success) {
                  Output.WriteLine($"You successfully disengaged from the {enemy.Name}!");
                  ctx.player.Skills.Reflexes.GainExperience(2);
                  // Exit combat
              } else {
                  Output.WriteLine($"The {enemy.Name} prevented your escape!");
                  ctx.player.Skills.Reflexes.GainExperience(1);
                  // Enemy gets attack of opportunity (reduced damage due to defensive bonus)
              }
          })
          .ThenShow(ctx => {
              if (/* flee success */) {
                  return new[] { Common.Return() };
              } else {
                  return new[] { EnemyCombatTurn(enemy) };
              }
          })
          .Build();
  }

  private static IGameAction[] CombatResolution(Npc enemy)
  {
      // Check combat end conditions
      if (enemy.Body.IsDead) {
          return new[] { EndCombat(enemy) };
      }

      // Check if enemy flees (wounded dangerous prey)
      if (ShouldEnemyFlee(enemy)) {
          Output.WriteLine($"The {enemy.Name} flees from combat!");
          return new[] { Common.Return() };
      }

      // Continue combat
      return new[] { EnemyCombatTurn(enemy) };
  }

  private static bool ShouldEnemyFlee(Npc enemy)
  {
      if (enemy is Animal animal) {
          double healthPercent = animal.Body.HealthPercent;

          // Prey always flees when wounded
          if (animal.Behavior == AnimalBehaviorType.Prey) {
              return healthPercent < 1.0;
          }

          // Dangerous prey flees when below threshold
          if (animal.Behavior == AnimalBehaviorType.DangerousPrey) {
              return healthPercent < animal.FleeThreshold;
          }

          // Scavengers flee if badly wounded
          if (animal.Behavior == AnimalBehaviorType.Scavenger) {
              return healthPercent < 0.4;
          }

          // Predators rarely flee (only at very low HP)
          if (animal.Behavior == AnimalBehaviorType.Predator) {
              return healthPercent < 0.2;
          }
      }

      return false;
  }
  ```
- **Acceptance:** Tactical combat actions work, combat feels strategic

**6.4: Increase Bleeding Severity** (Effort: M)
- Modify `Survival/SurvivalProcessor.cs` or bleeding effect generation
  ```csharp
  // Increase blood loss rate from wounds
  public const double BLEEDING_RATE_MULTIPLIER = 2.0;

  // Major artery hits should be immediately life-threatening
  if (damageResult.HitLocation.Contains("Artery")) {
      double bleedRate = baseDamage * BLEEDING_RATE_MULTIPLIER * 5; // 10x normal
      // Player has minutes to bandage or die
  }
  ```
- Add "Bandage" action to stop bleeding
- **Acceptance:** Bleeding is much more dangerous, requires treatment

**6.5: Test Lethal Combat** (Effort: M)
- Playtest scenarios:
  - Fight wolf with different weapons/actions
  - Take serious hit, verify shock/bleeding
  - Test tactical options (feint, defensive stance)
- Verify:
  - Combat ends in 1-3 exchanges (not 10+)
  - Critical hits to vitals are devastating
  - Player can die quickly if careless
  - Tactical choices matter (defensive stance vs power attack)
- Balance tuning as needed
- **Acceptance:** Combat feels lethal, tactical, tense

---

### Phase 7: Animal Behavior Integration (Days 18-21)

**Goal:** Implement animal AI responses to hunting, wounds, and player actions.

#### Tasks

**7.1: Implement Animal State Machine** (Effort: L)
- Modify `Actors/Animal.cs`
  ```csharp
  public void OnDetected(Player player)
  {
      State = AnimalState.Alert;

      // Behavior-specific response
      switch (Behavior) {
          case AnimalBehaviorType.Prey:
              // Immediately flee
              State = AnimalState.Fleeing;
              AttemptFlee(player);
              break;

          case AnimalBehaviorType.DangerousPrey:
              // Assess threat
              if (AssessThreat(player)) {
                  State = AnimalState.Fleeing;
                  AttemptFlee(player);
              } else {
                  State = AnimalState.Aggressive;
                  Output.WriteLine($"The {Name} lowers its head aggressively!");
              }
              break;

          case AnimalBehaviorType.Predator:
              // Attack if hungry or threatened
              if (IsHungry() || HealthPercent < 0.8) {
                  State = AnimalState.Aggressive;
                  IsHostile = true;
                  Output.WriteLine($"The {Name} snarls and prepares to attack!");
              } else {
                  State = AnimalState.Fleeing;
                  AttemptFlee(player);
              }
              break;

          case AnimalBehaviorType.Scavenger:
              // Assess player strength
              if (player.Body.HealthPercent < 0.5 || player.Body.IsInjured) {
                  State = AnimalState.Aggressive;
                  IsHostile = true;
                  Output.WriteLine($"The {Name} senses weakness and approaches!");
              } else {
                  State = AnimalState.Fleeing;
                  AttemptFlee(player);
              }
              break;
      }
  }

  public void OnWounded(double damagePercent)
  {
      // Significant damage triggers response
      if (damagePercent > 0.2) {
          switch (Behavior) {
              case AnimalBehaviorType.Prey:
                  // Panic flee
                  State = AnimalState.Fleeing;
                  Output.WriteLine($"The {Name} bolts in terror!");
                  break;

              case AnimalBehaviorType.DangerousPrey:
                  if (HealthPercent < FleeThreshold) {
                      State = AnimalState.Fleeing;
                      Output.WriteLine($"The wounded {Name} tries to escape!");
                  } else {
                      State = AnimalState.Aggressive;
                      IsHostile = true;
                      Output.WriteLine($"The wounded {Name} turns to fight!");
                  }
                  break;

              case AnimalBehaviorType.Predator:
                  // Fight harder when wounded
                  State = AnimalState.Aggressive;
                  IsHostile = true;
                  Output.WriteLine($"The wounded {Name} attacks with renewed fury!");
                  break;

              case AnimalBehaviorType.Scavenger:
                  if (HealthPercent < 0.4) {
                      State = AnimalState.Fleeing;
                      Output.WriteLine($"The {Name} yelps and runs away!");
                  } else {
                      State = AnimalState.Aggressive;
                      IsHostile = true;
                  }
                  break;
          }
      }
  }

  private bool AssessThreat(Player player)
  {
      // Dangerous prey compares size/strength
      double playerThreat = player.Body.Weight + (player.Body.StrengthPercent * 100);
      double mySize = Body.Weight;

      // Flee if player seems much stronger
      return playerThreat > (mySize * 1.5);
  }

  private void AttemptFlee(Player player)
  {
      // Speed check
      bool escaped = Body.SpeedPercent > player.Body.SpeedPercent;

      if (escaped) {
          // Remove from location (moved to adjacent zone)
          Output.WriteLine($"The {Name} escaped into the distance.");
          ParentLocation?.RemoveNpc(this);
      } else {
          Output.WriteLine($"The {Name} couldn't outrun you!");
          // Player can pursue/attack
      }
  }
  ```
- **Acceptance:** Animals respond to detection/wounds based on behavior type

**7.2: Implement Blood Trail Tracking** (Effort: M)
- Modify `PlayerComponents/TrackingManager.cs`
  ```csharp
  public class BloodTrail
  {
      public Animal WoundedAnimal { get; set; }
      public Location StartLocation { get; set; }
      public TimeSpan TimeCreated { get; set; }
      public double BloodAmount { get; set; } // Based on wound severity
  }

  public BloodTrail? FindBloodTrail(Location location)
  {
      // Check if wounded animal fled from this location
      // Blood trail lasts based on amount (heavy = 2hr, light = 30min)
  }

  public Animal? FollowBloodTrail(BloodTrail trail)
  {
      // Higher Hunting skill = easier to follow
      // Fresh trails easier than old
      // Heavy bleeding easier to track
  }
  ```
- Add action to follow blood trails
- **Acceptance:** Can track wounded fleeing animals

**7.3: Handle Hunting Failure States** (Effort: M)
- Modify hunting actions to handle animal responses
  ```csharp
  // In ApproachAnimal action
  if (detected) {
      target.OnDetected(ctx.player);

      // Show appropriate menu based on animal response
      if (target.State == AnimalState.Fleeing) {
          return new[] {
              TrackFleeingAnimal(target),
              LetItGo(),
              Common.Return()
          };
      } else if (target.State == AnimalState.Aggressive) {
          return new[] {
              Combat.StartCombat(target),
              AttemptRetreat()
          };
      }
  }
  ```
- **Acceptance:** Hunting failures lead to appropriate responses (flee/fight)

**7.4: Implement Pack Behavior (Optional)** (Effort: L)
- Wolves/Hyenas call for help when attacked
  ```csharp
  public void CallPack(Player player)
  {
      var nearbyPack = ParentLocation.NPCs
          .OfType<Animal>()
          .Where(a => a.Name == this.Name && a != this)
          .ToList();

      if (nearbyPack.Any()) {
          Output.WriteLine($"The {Name} howls! Other {Name}s respond!");
          foreach (var packMember in nearbyPack) {
              packMember.OnDetected(player);
              packMember.IsHostile = true;
          }
      }
  }
  ```
- **Acceptance:** Predators can call packmates for help

**7.5: Test Animal Behavior** (Effort: M)
- Playtest scenarios:
  - Approach deer → Should flee immediately
  - Wound bison → Should flee or fight based on HP
  - Attack wolf → Should call pack if present
  - Approach fox as injured player → Should attack opportunistically
- Verify:
  - Each behavior type responds correctly
  - State transitions work
  - Blood trails are trackable
- **Acceptance:** Animal AI feels realistic and varied

---

## Risk Assessment and Mitigation

### Technical Risks

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| **Performance degradation** (trap checking every minute) | Medium | Medium | Optimize trap checks (only when animals present), limit active traps per location |
| **Balance difficulty** (combat too easy/hard) | High | High | Extensive playtesting, tunable damage multipliers, gradual rollout |
| **Complexity creep** (too many systems) | Medium | Medium | Phased implementation, can cut scope (e.g., skip pack behavior) |
| **AI feels robotic** (simple state machine) | Medium | Low | Accept limitation for V1, document for future enhancement |
| **Arrow economy breaks** (too easy/hard to recover) | Medium | Medium | Tunable recovery rates, playtest different values |
| **Trap spam** (players set 20 traps) | Low | Low | Limit traps per location (max 3), require investment (time/materials) |

### Design Risks

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| **Combat feels unfair** (one-shot deaths) | High | High | Ensure player has counterplay (defensive stance, flee), telegraph danger |
| **Hunting too easy** (trivializes food) | Medium | High | Balance success rates, make ammunition scarce, animals should be rare |
| **Stealth too binary** (detected = failure) | Medium | Medium | Gradual detection (alert → spotted), allow recovery attempts |
| **Predators too punishing** (can't escape) | Medium | High | Ensure flee mechanics work, predators shouldn't chase forever |
| **New players confused** (too many options) | High | Medium | Tutorial messages, gradual unlock (Hunting level gates), clear UI |

### Integration Risks

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| **Breaking existing combat** | Low | Medium | Keep old combat as fallback, extensive testing |
| **Skill system conflicts** (Hunting vs Fighting) | Low | Low | Clear separation: Hunting = ranged/stealth, Fighting = melee |
| **Body system incompatibility** (animal bodies different) | Low | High | Use existing BodyPartFactory, test damage on all animal types |
| **Action menu bloat** (too many options) | Medium | Medium | Nested menus, context-aware filtering |

---

## Success Metrics

### Functional Metrics
- [ ] 12+ new animals spawn in appropriate biomes
- [ ] All 4 behavior types respond correctly to player actions
- [ ] Stealth system works (detection, concealment, approach)
- [ ] Tracking system works (find tracks, follow trails, blood trails)
- [ ] Bow hunting works (craft, shoot, accuracy scales with skill)
- [ ] Traps work (set, trigger, collect, async checking)
- [ ] Lethal combat works (3x damage, tactical actions, shock/bleeding)
- [ ] Animal AI works (flee, fight, pack behavior)

### Gameplay Metrics
- [ ] Hunting feels tactical (stealth approach, positioning, weapon choice)
- [ ] Combat feels dangerous (death possible in 1-3 exchanges)
- [ ] Different animal types feel distinct (prey vs predator experiences)
- [ ] Hunting skill progression feels rewarding (unlocks better techniques)
- [ ] Multiple viable strategies (stealth, ranged, traps, avoidance)

### Balance Metrics
- [ ] Early game hunting is challenging but achievable (Hunting 0-2)
- [ ] Mid game hunting provides good food source (Hunting 3-4)
- [ ] Late game hunting allows specialization (master bow or master traps)
- [ ] Predator encounters are scary but survivable with preparation
- [ ] Food economy balanced (hunting difficulty vs food scarcity)

### Technical Metrics
- [ ] No performance regressions (trap checking doesn't lag)
- [ ] No breaking bugs (animals spawn, combat doesn't crash)
- [ ] Code follows architectural patterns (composition, action builders)
- [ ] All unit tests pass (body damage, skill checks)

---

## Required Resources and Dependencies

### Technical Dependencies
- **Existing Systems (Must Work)**:
  - Body system (damage penetration, organs)
  - Skill system (Hunting skill progression)
  - Crafting system (recipes for bows, arrows, traps)
  - Action builder pattern (menu generation)
  - Item property system (materials for crafting)

- **New External Assets**: None (text-based game)

### Knowledge Dependencies
- Understanding of current combat flow (CombatManager, ActionFactory)
- Understanding of NPC spawning (LocationFactory, NpcTable)
- Understanding of action system (ActionBuilder, GameContext)
- Ice Age fauna research (animal sizes, behaviors, habitats)

### Testing Dependencies
- `./play_game.sh` script for automated testing
- TEST_MODE for rapid iteration
- Multiple playthrough scenarios for balance testing

---

## Timeline Estimates

### MVP Timeline (Recommended)

**Optimistic (10 days)**
- MVP Phase 1 (Animals & Distance): 2 days
- MVP Phase 2 (Stealth & Approach): 2 days
- MVP Phase 3 (Bow Hunting): 3 days
- MVP Phase 4 (Blood Trails): 2 days
- Testing & Polish: 1 day

**Realistic (14 days - recommended)**
- MVP Phase 1 (Animals & Distance): 3 days
- MVP Phase 2 (Stealth & Approach): 3 days
- MVP Phase 3 (Bow Hunting): 4 days
- MVP Phase 4 (Blood Trails): 3 days
- Testing & Polish: 2 days

**Pessimistic (18 days)**
- MVP Phase 1 (Animals & Distance): 4 days
- MVP Phase 2 (Stealth & Approach): 4 days
- MVP Phase 3 (Bow Hunting): 5 days
- MVP Phase 4 (Blood Trails): 4 days
- Testing & Polish: 3 days
- Unexpected issues: 2 days buffer

**Recommended MVP Timeline:** 2-3 weeks (realistic estimate)

---

### Full System Timeline (V2, After MVP Validated)

**Realistic (Additional 2-3 weeks after MVP)**
- Trap System: 3-4 days
- Simultaneous Combat Redesign: 3-4 days
- Dangerous Prey Animals: 2-3 days
- Multi-location Blood Trails: 2-3 days
- Advanced AI Features: 4-5 days
- Testing & Polish: 3-4 days

**Total Timeline (MVP + V2):** 4-6 weeks

---

## Implementation Notes

### Code Style Guidelines
- Follow existing patterns (ActionBuilder, composition, property-based crafting)
- All new components should be in appropriate namespaces
- Document public APIs with XML comments
- Use descriptive names (no abbreviations)
- Keep methods under 50 lines (extract helpers)

### Testing Strategy
- Unit test calculations (accuracy, damage, detection)
- Integration test with `./play_game.sh`
- Playtest each phase before moving to next
- Balance testing requires multiple full playthroughs

### Documentation Requirements
- Update CLAUDE.md with new systems
- Create `/documentation/hunting-system.md` reference
- Document all new manager components
- Add examples to complete-examples.md

### Git Commit Strategy
- Commit after each phase completion
- Use descriptive messages: "Phase 3: Implement stealth and tracking system"
- Tag major milestones: `v0.2.0-hunting-overhaul`
- Don't commit broken builds (each phase should compile)

---

## Post-Implementation Enhancements

### Future Improvements (Out of Scope for V1)
1. **Advanced AI** - Neural network behavior, learning player tactics
2. **Animal ecology** - Herds, territories, migration patterns
3. **Weather integration** - Rain washes away tracks, wind affects scent
4. **Companion animals** - Tamed wolves for hunting assistance
5. **Trophy system** - Mount antlers, craft from unique materials
6. **Hunting challenges** - Legendary animals, rare variants
7. **Multiplayer hunting** - Cooperative hunts with NPCs
8. **Seasonal variation** - Animal fat % changes with season
9. **Food spoilage** - Meat preservation becomes important
10. **Hunter's journal** - Track animals hunted, locations, behaviors

### Known Limitations (V1)
- Simple state machine (not true AI)
- No positional system (distance abstracted)
- Synchronous trap checking (not event-driven)
- Limited pack behavior (wolves call nearby, don't coordinate)
- No animal reproduction (spawns only)
- Blood trails don't persist across saves

---

## Conclusion

This hunting overhaul is a **major system redesign** that will fundamentally change gameplay from "turn-based combat RPG" to "tactical survival hunting simulator". The phased approach allows for incremental testing and course correction.

**Critical Success Factors:**
1. **Playtesting early and often** - Balance is everything
2. **Maintain architectural consistency** - Follow existing patterns
3. **Don't scope creep** - Ship V1, iterate based on feedback
4. **Document as you go** - Future you will thank present you

The system is ambitious but achievable given the solid foundation of the existing codebase. The composition architecture, action builder pattern, and body system provide excellent extension points for hunting mechanics.

**Next Steps:**
1. Review and approve this plan
2. Create task tracking in `/dev/active/hunting-system-overhaul/`
3. Begin Phase 1: Animal Behavior Foundation
4. Iterate based on feedback

---

**Document Version:** 1.0
**Author:** Claude Code
**Date:** 2025-11-02
**Status:** Pending Approval
