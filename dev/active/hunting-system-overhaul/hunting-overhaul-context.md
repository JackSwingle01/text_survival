# Hunting System Overhaul - Context & Key Information

**Last Updated:** 2025-11-02 (Refined via brainstorming)
**Related Plan:** `hunting-overhaul-plan.md`
**Status:** Planning Phase - MVP-First Approach

---

## Quick Reference

### What This Feature Does (MVP Scope)
Transforms the game into a lethal, tactical hunting simulator with:
- **MVP (2-3 weeks):**
  - 4-6 prey animals (Deer, Rabbit, Ptarmigan, Fox) + updated predators
  - Distance-based stealth approach (100m → 30m)
  - Simple Bow + Stone Arrows with skill-based accuracy
  - Blood trail tracking (single-location)

- **V2 (Deferred):**
  - Trap system for passive hunting
  - Simultaneous combat redesign
  - Dangerous prey (Bison, Elk, Moose, Auroch)
  - Advanced AI (pack coordination, multi-location tracking)

### Why We're Building This
Current system is binary (all animals hostile, turn-based spam combat) which doesn't align with survival-focused gameplay vision. Hunting should be the primary food acquisition method, combat should be rare and deadly.

---

## Clarified Design Decisions (from Brainstorming)

### Distance System: Simple Meter Tracking
- **Implementation:** Animals spawn at 100m distance from player
- **Approach mechanic:** Each "Approach" action reduces distance by 20-30m (takes 5-10 minutes)
- **Detection difficulty:** Increases as you get closer
  - 70-100m: Easy checks (distance modifier 0.5)
  - 40-70m: Moderate checks (distance modifier 1.0)
  - 0-40m: Hard checks (distance modifier 1.5)
- **Shooting range:** Bows effective at 30-50m (accuracy decreases with distance)
- **No grid system needed:** Just a single `distance` property per animal

### Combat Flow: Simultaneous Resolution
- **Why:** Avoids "spam 1 to attack" turn-based feel
- **How:** Both player and enemy choose actions simultaneously, resolve together
- **Counter-play:** Quick beats Power, Defensive beats Quick, Power beats Defensive
- **Winner bonus:** +25% hit chance, +25% damage on winning counter
- **Probabilistic AI:** Enemy choices are randomized (e.g., Predator: 50% Power, 30% Quick, 20% Feint)
- **Lethality:** 3x damage multiplier ensures combat ends in 1-3 exchanges
- **V2 feature:** Deferred until MVP validated

### Trap System Balance (V2)
Multiple layers to prevent trap spam:
1. **Bait requirement:** Must add meat/berries or -50% success rate
2. **Location limit:** Max 3 traps per location
3. **Breakage risk:** Large animals (>50kg) have 30-50% chance to break trap when caught
4. **Area learning:** After 2+ catches, animal spawn rate -50% for 7 days at that location

### Stealth Detection: Distance-Based Actions
- **Initial state:** Animals at 100m, Idle (unaware)
- **Approach action:** Player moves 20-30m closer, triggers stealth check
- **State progression:** Idle → Alert (suspicious) → Detected (flees/attacks)
- **Alert state:** Harder subsequent checks, no stealth damage bonus
- **Fully detected:** Second failed check triggers animal behavior response

### Animal AI: Simple State Machine (V1)
- **States:** Idle → Alert → Fleeing/Aggressive
- **No complex coordination:** Pack behavior = nearby same-species also become hostile (simple)
- **V2 features:** Multi-animal coordination, territory behavior, learning from encounters

### Blood Trails: Time Pressure Tracking
- **Trail creation:** Non-lethal hits create blood trail based on wound severity
- **Freshness decay:** Heavy wounds = slow decay (120 min), light wounds = fast decay (30 min)
- **Tracking checks:** Each attempt (15 min) = skill check based on freshness
- **Success:** Move closer to wounded animal
- **Failure:** Lose time, trail gets colder (harder next check)
- **Outcome:** Catch wounded animal, find dead animal (bled out), or lose trail
- **MVP limitation:** Single-location only (animal "hiding nearby"), multi-location in V2

### Critical Hits: Existing Body System
- **No special code:** Use `Body.Damage(DamageInfo)` for ranged attacks (same as melee)
- **Natural lethality:** Brain/heart hits on small animals (rabbit, ptarmigan) destroy vital organs → death
- **Scaling:** Large animals (deer, bear) have larger organs → more damage needed to destroy
- **Balance tool:** Adjust bow damage values + animal tissue protection as needed

### Implementation: MVP Then Iterate
- **Build:** Minimum viable hunting loop (animals + stealth + bow + tracking)
- **Test:** Validate fun factor, balance, technical implementation
- **Learn:** Discover what works before adding complexity
- **Iterate:** Add traps, combat redesign, advanced AI only after MVP proven

---

## Key Design Decisions

### Design Philosophy

**User Vision:**
> "I'm thinking it should be its own system and only some animals can fight back. Hand to hand combat should be more rare and lethal, most prey will run away if it's non lethal. I want combat and hunting in the game to feel extremely lethal, dangerous, and tactical. Not spamming 1 to attack in turns."

**Hunting ≠ Combat:**
- **Hunting** = Stealth, tracking, ranged weapons, traps (primary gameplay)
- **Combat** = Hand-to-hand, melee, desperate struggle (failure state)
- Prey animals don't fight, they flee
- Predators attack, but encounters should be rare
- Dangerous prey (bison, elk) only fight when cornered

**Lethality:**
- Combat should end in 1-3 exchanges, not 10+
- Critical hits to vitals can be instantly fatal
- Bleeding and shock are severe consequences
- Player can die quickly if careless

**Tactical Depth:**
- Stealth approach rewards patience and skill
- Weapon choice matters (bow for deer, spear for bison)
- Traps allow passive hunting while doing other activities
- Multiple valid strategies (not just "attack spam")

### Animal Behavior Matrix

| Type | Examples | Hostility | Detection Response | Wound Response | Special |
|------|----------|-----------|-------------------|----------------|---------|
| **Prey** | Deer, Rabbit, Ptarmigan, Goat | Non-hostile | Flee immediately | Flee faster | Easy to sneak up on, provides clean meat |
| **Dangerous Prey** | Bison, Auroch, Elk, Moose | Non-hostile | Assess → Flee or Fight | Fight if cornered | Large meat payoff, requires strategy |
| **Predator** | Wolf, Bear, Lynx, Hyena | Hostile | Attack if hungry | Attack harder | Pack behavior, very dangerous |
| **Scavenger** | Fox, Wolverine, Marmot | Opportunistic | Assess player strength | Flee if outmatched | Attack injured players, flee from healthy |

### Key Architectural Decisions

1. **Animal class separate from generic NPC** - Allows animal-specific behavior without affecting human NPCs
2. **Composition over inheritance** - Managers (StealthManager, TrackingManager, HuntingManager) added to Player
3. **State machine for AI** - Simple but extensible (Idle → Alert → Fleeing/Aggressive)
4. **Async trap checking** - Location.Update() triggers trap checks, not player action
5. **Skill-gated mechanics** - Hunting skill unlocks better stealth, tracking, critical hits

---

## Key Files to Modify

### Core Files (Major Changes)

| File | Purpose | Key Changes |
|------|---------|-------------|
| `Actors/NPC.cs` | Base NPC class | Make `IsHostile` public, add `State` property |
| `Actors/NPCFactory.cs` | Animal creation | Add 12+ new animals with behavior types |
| `Actions/ActionFactory.cs` | Action generation | Add "Hunting" section, redesign combat menu |
| `PlayerComponents/CombatManager.cs` | Combat mechanics | 3x damage multiplier, tactical actions, shock |
| `Items/Weapon.cs` | Weapon definition | Add ranged weapon properties |
| `Items/ItemFactory.cs` | Item creation | Add bows, arrows, traps |
| `Crafting/CraftingSystem.cs` | Recipe definition | Add hunting gear recipes |
| `Environments/ForageFeature.cs` | Foraging mechanics | Add track finding |
| `Environments/LocationFactory.cs` | Location spawning | Update NpcTables with new animals |
| `Environments/Location.cs` | Location updates | Call Feature.Update() for traps |

### New Files (To Create)

| File | Purpose | Priority |
|------|---------|----------|
| `Actors/AnimalBehaviorType.cs` | Enum for behavior types | Phase 1 |
| `Actors/AnimalState.cs` | Enum for AI states | Phase 1 |
| `Actors/Animal.cs` | Animal-specific class | Phase 1 |
| `Actors/IWildlife.cs` | Interface for future AI | Phase 1 |
| `PlayerComponents/StealthManager.cs` | Stealth/concealment | Phase 3 |
| `PlayerComponents/TrackingManager.cs` | Track finding/following | Phase 3 |
| `PlayerComponents/HuntingManager.cs` | Ranged attacks | Phase 4 |
| `PlayerComponents/AmmunitionManager.cs` | Ammo tracking/recovery | Phase 4 |
| `Items/RangedWeapon.cs` | Bow/ranged weapon class | Phase 4 |
| `Items/Trap.cs` | Trap item class | Phase 5 |
| `Environments/TrapFeature.cs` | Placed trap location feature | Phase 5 |
| `Utils/HuntingCalculator.cs` | Detection, stealth calculations | Phase 3 |
| `PlayerComponents/CombatAction.cs` | Combat action enum | Phase 6 |

---

## Integration Points

### Existing Systems to Leverage

**Body System:**
- Use existing damage penetration (DamageProcessor)
- Leverage organ targeting for critical hits
- Body composition affects animal stats (muscle %, fat %)

**Skill System:**
- Hunting skill already defined (level 0-5)
- Use SkillCheckCalculator for stealth, tracking, accuracy
- Award XP for successful hunts, failed attempts

**Crafting System:**
- Property-based recipes perfect for bows (Wood + Sinew)
- RecipeBuilder pattern for hunting gear
- Arrows should be batch-crafted (5 at a time)

**Action System:**
- ActionBuilder for hunting menus
- `.When()` conditions for skill gates
- `.ThenShow()` for menu chaining
- `.TakesMinutes()` for time passage

**Item Properties:**
- New property: `ItemProperty.Ammunition`
- New property: `ItemProperty.Ranged`
- Existing: `ItemProperty.Sharp`, `ItemProperty.Wood`, `ItemProperty.Sinew`

### External Dependencies

**None** - This is a self-contained feature (text-based game, no external assets)

**But depends on:**
- Working build (dotnet 9.0)
- TEST_MODE for testing (`./play_game.sh`)
- Current systems (Body, Skill, Crafting, Action)

---

## Testing Strategy

### Unit Testing
**Location:** `text_survival.Tests/`

**Test Coverage:**
- `HuntingCalculatorTests.cs` - Detection chance, stealth calculations
- `RangedWeaponTests.cs` - Accuracy at range, damage calculations
- `AnimalBehaviorTests.cs` - State transitions, behavior responses
- `TrapTests.cs` - Trigger chance, animal size limits

**What NOT to unit test:**
- Action menu flow (integration test territory)
- Animal spawning (integration test)
- Full hunting loop (playtest)

### Integration Testing
**Method:** `./play_game.sh` (TEST_MODE automation)

**Test Scenarios:**
1. **Stealth Hunt Deer** - Sneak up, shoot, verify kill/wound
2. **Failed Stealth** - Get detected, deer flees, can track
3. **Bow Hunting Progression** - Hunting 0 vs 5, accuracy difference
4. **Trap Overnight** - Set snare, sleep 8 hours, check trap
5. **Predator Attack** - Wolf spawns hostile, combat feels lethal
6. **Wounded Dangerous Prey** - Shoot bison, it charges back

### Balance Testing
**Requires:** Multiple full playthroughs (4-8 hours each)

**Metrics to Track:**
- Time to first successful hunt (should be <30 min)
- Food security timeline (hunting viable by day 2?)
- Combat survival rate (50-70% with good tactics?)
- Arrow economy (running out too fast/slow?)
- Trap ROI (worth time investment?)

---

## Implementation Phases Summary

| Phase | Focus | Duration | Risk |
|-------|-------|----------|------|
| 1 | Animal Behavior Foundation | 3 days | Low |
| 2 | New Animal Roster | 3 days | Low |
| 3 | Stealth & Tracking | 4 days | Medium |
| 4 | Ranged Weapons | 5 days | High |
| 5 | Trap System | 3 days | Medium |
| 6 | Lethal Combat Redesign | 3 days | High |
| 7 | Animal Behavior Integration | 4 days | Medium |

**Total:** 25 days (realistic estimate)

**High-Risk Phases:**
- Phase 4 (Ranged Weapons) - Complex calculations, ammunition tracking
- Phase 6 (Combat Redesign) - Balance difficulty, lethal but not unfair

---

## Critical Constraints

### From CLAUDE.md

1. **NPCs have NO skills** - Animal abilities derived from body stats only
   - Wolf: High speed (60% muscle, 20% fat)
   - Bear: High strength (250kg, 55% muscle)

2. **Single damage entry point** - Always use `Body.Damage(DamageInfo)`, never modify parts directly

3. **Action menu flow** - Use `.ThenShow()` and `.ThenReturn()`, no manual loops

4. **Time passage** - Actions must call `World.Update(minutes)` or use `.TakesMinutes()`

5. **Property-based crafting** - Bows need Wood+Sinew properties, not specific items

### From README.md

**Ice Age Authenticity:**
- Flint, bone, hide (not steel or magic)
- Realistic animal behaviors
- Period-appropriate technology

**Survival Over Combat:**
- Environmental challenges > kill-everything
- Food acquisition = primary gameplay
- Combat is rare and dangerous

**Emergent Storytelling:**
- Systems interact to create unique moments
- Player stories > scripted narratives

---

## Balance Guidelines

### Hunting Difficulty Curve

**Early Game (Hunting 0-2):**
- Prey: 60% stealth success, 40% bow accuracy
- Should be challenging but achievable
- Bow crafting available at Crafting 2
- Arrow economy: ~5 arrows per hunt (3 shots, 2 recovered)

**Mid Game (Hunting 3-4):**
- Prey: 80% stealth success, 60% bow accuracy
- Dangerous prey accessible (bison, elk)
- Critical hits unlock at Hunting 3
- Trap success: 40-60%

**Late Game (Hunting 5):**
- Prey: 95% stealth success, 80% bow accuracy
- Can target specific body parts
- Trap mastery: 60-80% success
- Silent movement (can approach without detection)

### Combat Lethality Targets

**Player vs Prey:**
- Should never fight (prey flees)
- If cornered, 1-2 hits to kill prey

**Player vs Dangerous Prey:**
- 2-4 hits to kill player (if fight)
- 3-6 hits to kill prey
- Goal: avoid combat via ranged hunting

**Player vs Predator:**
- 1-3 hits to kill player (LETHAL)
- 2-5 hits to kill predator
- Goal: flee or kill quickly, no attrition

### Resource Economy

**Arrows:**
- Craft 5 arrows at a time (30 min)
- 50% recovery on kill, 20% on miss
- Net: 3-4 hunts per crafting session

**Traps:**
- Take 30-60 min to set
- 40-60% success rate
- Reusable (collect after use)
- Limit: 3 traps per location

**Food from Hunting:**
- Rabbit: 3kg meat = 600 calories
- Deer: 15kg meat = 3000 calories (2+ days)
- Bison: 240kg meat = 48000 calories (weeks!)

---

## Common Pitfalls to Avoid

### Implementation Pitfalls

1. **Don't break existing combat** - Keep old system until new is proven
2. **Don't hardcode animal stats** - Use NPCFactory pattern
3. **Don't create circular dependencies** - HuntingManager → Animal, not reverse
4. **Don't forget time passage** - Every action takes time
5. **Don't skip skill checks** - Hunting should improve with practice

### Design Pitfalls

1. **Don't make hunting trivial** - Should feel like an accomplishment
2. **Don't make combat unwinnable** - Lethal ≠ unfair
3. **Don't make stealth binary** - Gradual detection, recovery possible
4. **Don't make predators unkillable** - Scary but beatable with tactics
5. **Don't forget non-combat options** - Flee/avoid should always be viable

### Testing Pitfalls

1. **Don't test in isolation** - Hunting affects food, food affects survival
2. **Don't balance on theory** - Playtest, playtest, playtest
3. **Don't assume player skill** - Test with Hunting 0-5
4. **Don't forget edge cases** - What if player runs out of arrows mid-hunt?
5. **Don't skip performance testing** - Trap checks every minute could lag

---

## Quick Start Guide (For Next Session)

### When Resuming Work

1. **Read:** `hunting-overhaul-plan.md` - Full implementation details
2. **Check:** `hunting-overhaul-tasks.md` - What's done, what's next
3. **Review:** This file - Key decisions and constraints
4. **Build:** `dotnet build` - Ensure clean starting point
5. **Start:** Begin with current phase tasks

### First Steps (Phase 1)

1. Create `Actors/AnimalBehaviorType.cs` enum
2. Create `Actors/AnimalState.cs` enum
3. Extract `Actors/Animal.cs` from NPC
4. Modify `Actors/NPC.cs` - Make IsHostile public
5. Test: Create test animal, verify properties

### Testing Each Phase

```bash
# Build
dotnet build

# Unit tests
dotnet test

# Integration test
./play_game.sh start
./play_game.sh send 1  # Look around
./play_game.sh log 20  # Check output
./play_game.sh stop

# Playtest (manual)
dotnet run
```

---

## Reference Materials

### Ice Age Animals (Research)

**Prey Animals:**
- **Deer** (Red Deer): 50-200kg, found in forests/plains
- **Rabbit** (European Rabbit): 1-3kg, burrows in hillsides
- **Ptarmigan** (Rock Ptarmigan): 0.4-0.7kg, tundra/mountains
- **Mountain Goat** (Alpine Ibex): 40-100kg, rocky hillsides

**Dangerous Prey:**
- **Bison** (Steppe Bison): 400-900kg, massive horns
- **Auroch** (Extinct cattle ancestor): 800-1500kg, very aggressive
- **Elk** (Giant Elk/Irish Elk): 300-700kg, massive antlers
- **Moose**: 400-700kg, dangerous when protecting young

**Predators:**
- **Wolf** (Dire Wolf): 50-80kg, pack hunters
- **Bear** (Cave Bear): 300-500kg, omnivore but dangerous
- **Lynx** (Eurasian Lynx): 18-30kg, solitary stalker
- **Hyena** (Cave Hyena): 40-70kg, scavenger/hunter

**Scavengers:**
- **Fox** (Arctic/Red Fox): 3-10kg, opportunistic
- **Wolverine**: 9-25kg, fierce scavenger
- **Marmot** (Alpine Marmot): 4-8kg, den defender
- **Beaver**: 11-30kg, defensive of territory

### Hunting Techniques (Historical)

**Bow Hunting:**
- Effective range: 20-50 meters
- Silent approach critical
- Aim for heart/lungs (quick kill)
- Tracking wounded game common

**Trapping:**
- Snares for small game (rabbits, birds)
- Deadfalls for medium game (deer, goat)
- Pitfalls for large game (bison, mammoth)
- Check daily for catches

**Tracking:**
- Fresh tracks (<1hr): Clear prints, sharp edges
- Old tracks (>12hr): Filled with debris, weathered
- Blood trail: Heavy = easy to follow, light = skill needed
- Broken vegetation, scat, feeding signs

---

## Version History

### v1.0 (2025-11-02)
- Initial context document
- Design decisions from user consultation
- Key files and integration points identified
- Balance guidelines established
- Testing strategy defined

---

**Document Status:** Active Reference
**Next Review:** After Phase 1 completion
**Maintainer:** Development team
