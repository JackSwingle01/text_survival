# Strategic Combat System Redesign

**✅ IMPLEMENTED** - This redesign was completed during the desktop migration. Combat now uses a 25x25m tactical grid with distance-based actions, team combat, and AI-driven enemy behavior.

## Summary
Transform combat from basic turn-based HP trading into a distance-based tactical dance where positioning, defensive options, and reading animal behavior create meaningful decisions.

## Key Design Decisions
- **Merge Encounter + Combat** into one unified distance-based system
- **Descriptive animal health** ("wounded", "staggering") instead of HP bars
- **Energy affects effectiveness** - low energy makes actions worse, doesn't block them
- **Full redesign** - implement all core systems together

---

## Core Mechanics

### Distance Zones
| Zone | Distance | Character |
|------|----------|-----------|
| Melee | 0-3m | Committed - trading blows, desperate |
| Close | 3-8m | The dance - thrust range, circling |
| Mid | 8-15m | Standoff - throwing range, intimidation |
| Far | 15-20m | Exit window - disengage possible |

### Animal Behavior States (Readable Tells)
| State | Signal | Player Opportunity |
|-------|--------|-------------------|
| Circling | Looking for opening | Hold ground, don't turn |
| Threatening | Testing nerve | Intimidate or prepare |
| Charging | Committed attack | Brace/dodge/strike first |
| Recovering | Off-balance after miss | **Your opening** |
| Retreating | Lost nerve | Let go or press |

### Player Actions by Zone

**Melee (0-3m)**
- Strike - deal damage, take counterattack
- Shove - push to Close zone, take glancing damage
- Grapple - pin for kill, mutual damage risk
- Go Down - play dead, risky but may end fight

**Close (3-8m)**
- Thrust - attack with reach weapon
- Hold Ground - maintain distance, read animal
- Back Away - gain distance, emboldens animal
- Set Brace - bonus damage if charged (spear)

**Mid (8-15m)**
- Throw - ranged attack, lose weapon
- Intimidate - chance to make animal retreat
- Close Distance - move to Close zone
- Careful Retreat - move to Far zone

**Far (15-20m)**
- Disengage - attempt to end combat
- Hold Position - wait for animal decision
- Re-engage - close to Mid zone

### Defensive Options
| Action | Effect | Cost |
|--------|--------|------|
| Dodge | Avoid attack entirely | Pushed back 1 zone, energy cost |
| Block | Reduce incoming damage | Weapon durability, energy cost |
| Brace | Counter charging animal | Locked in place, exposed if no charge |
| Give Ground | Retreat to avoid attack | Shows weakness (boldness+), terrain limits |

### Energy Integration
- Actions drain Energy (small amounts)
- Low Energy reduces effectiveness:
  - Dodge success chance decreases
  - Block absorbs less damage
  - Strike damage reduced
  - Actions remain available, just worse

### Player Lethality - Vulnerability Windows
| Animal State | Hit Modifier | Critical Chance |
|--------------|-------------|-----------------|
| Circling | 1.0x | 5% |
| Threatening | 0.8x | 3% |
| Charging | 1.2x | 15% |
| **Recovering** | **1.5x** | **25%** |
| Retreating | 1.3x | 10% |

Critical hits target vitals - can be lethal to large predators.

---

## Implementation Plan

### Step 1: Combat State Model
Create new combat state tracking:
- `Combat/CombatState.cs` - distance, animal behavior, turn tracking
- `Combat/AnimalCombatBehavior.cs` - behavior state machine with transitions
- `Combat/DistanceZone.cs` - zone definitions and helpers

### Step 2: Rewrite CombatRunner
Replace existing loop with distance-based system:
- Unified loop handling all distance zones
- Action availability based on zone + equipment + impairments
- Animal behavior state machine driving NPC decisions
- Energy drain on actions, effectiveness scaling

### Step 3: Merge EncounterRunner Logic
- Remove EncounterRunner as separate phase
- Encounter setup (distance, boldness) feeds directly into combat
- Pre-combat options (stand/back away/run/drop meat) become Far zone actions
- Single entry point for predator encounters

### Step 4: Defensive Action Resolution
- `Combat/DefensiveActions.cs` - dodge, block, brace, give ground logic
- Integration with damage system (DamageCalculator)
- Energy cost calculations with effectiveness scaling
- Impairment checks (can't dodge with bad legs)

### Step 5: Animal Behavior AI
- Behavior transitions based on:
  - Current state + timer
  - Player actions (backing away triggers charge)
  - Animal boldness + wounds
  - Distance zone
- Readable text descriptions for each state

### Step 6: Descriptive Health System
- Remove enemy HP bar from UI
- Add health description generator:
  - "The wolf favors its left leg" (injured)
  - "Blood mats its fur" (badly hurt)
  - "It staggers, barely standing" (near death)
- Player must read the animal

### Step 7: Update UI
- `Web/Dto/CombatDto.cs` - new structure with zones, behavior, actions
- `wwwroot/` - combat overlay redesign
- Distance indicator, animal state description, action buttons with hints

### Step 8: Integration & Testing
- Update event outcomes that spawn combat
- Test all entry points (hunt detection, event encounters)
- Balance pass on damage/lethality numbers
- Playtest common scenarios

---

## Files to Modify

**Core Combat (rewrite)**
- `Actions/CombatRunner.cs` - complete rewrite
- `Combat/CombatManager.cs` - add defensive calculations

**Merge/Remove**
- `Actions/EncounterRunner.cs` - merge into CombatRunner, then remove

**Animal Behavior**
- `Actors/Animals/Animal.cs` - add combat behavior state

**UI**
- `Web/Dto/Overlay.cs` - update CombatDto
- `wwwroot/app.js` - combat rendering
- `wwwroot/modules/effects.js` - combat animations

**New Files**
- `Combat/CombatState.cs`
- `Combat/DistanceZone.cs`
- `Combat/AnimalCombatBehavior.cs`
- `Combat/DefensiveActions.cs`

---

## Example Combat Flow

```
[FAR ZONE - 18m]
A wolf watches you from the treeline. Its hackles rise.

WOLF: Threatening
> Hold Position | Close Distance | Disengage | Drop Meat

[Player: Close Distance]

[MID ZONE - 12m]
The wolf paces, eyes locked on you.

WOLF: Circling
> Throw Spear (45% hit) | Intimidate | Close Distance | Careful Retreat

[Player: Close Distance]

[CLOSE ZONE - 6m]
The wolf snarls, coiling to spring.

WOLF: Threatening
> Thrust (65% hit) | Hold Ground | Set Brace | Back Away

[Player: Set Brace]

The wolf launches at your throat!

WOLF: Charging! [Auto-response to brace]
Your spear catches it mid-leap. The point drives deep.
The wolf yelps, thrashing—then goes still.

[VICTORY]
Wolf carcass at your feet.
```

---

## Validation Against Principles

**"Two players would reasonably choose differently"**
- Aggressive: Thrust when circling, press retreating animals
- Cautious: Set brace, let it come, let wounded animals flee

**"Player knowledge is progression"**
- Learn: Recovering = opening, backing away = triggers charge, threatening without charging = losing nerve

**"Compound pressure creates choices"**
- Injured + low energy + bleeding = can't dodge well, must fight or bluff

**"Realism as intuition aid"**
- Distance zones match real spatial dynamics
- Bracing spear against charge works like reality
- Animals telegraph attacks

---

## Refined Design (Brainstorming Session)

### Core System Integration

**Time Economy**
Each combat turn calls `ctx.Update(1, ActivityType.Fighting)`. The world ticks - fire burns, calories drain, weather progresses. Extended fights become losing propositions. A 10-turn standoff costs 10 minutes of fire margin. Creates "press for kill vs. accept standoff" pressure without special combat code.

**Continuous Distance**
Distance tracked as double (meters), zones derived. Movement formula:
```
distance = terrainHazard * movingCapacity * baseMove * random(0.8-1.2)
```
- Injured player repositions slowly
- Rocky terrain slows both sides
- Variance prevents predictability
- Multiple turns can occur in same zone

**Boldness as Core Driver**
Additive formula, no special cases:
```
boldness = 0.4 (base) + meatCarried(0.2) + playerWeakness(0.15)
         + bloodScent(0.15) + packNearby(0.25) - torchHeld(0.3)
         - playerSizeAdvantage(0.1) - animalWounded(0.1-0.3)
```
Bold animals (>0.6) progress faster through states. Cautious animals (<0.4) linger, retreat to intimidation.

---

### Targeting & Body System

**Targeting Available at Close Range Only**
- Melee (0-3m): Frantic fighting, no targeting
- Close (3-8m): Tactical layer - "Thrust" → "Where?" two-stage menu
- Mid (8-15m): Too far for precision throwing

**Base Hit Chances by Target**
| Target | Base | Effect on Hit |
|--------|------|---------------|
| Legs | 70% | Cripples movement (slower charges, easier escape) |
| Torso | 80% | Standard damage, animal stays mobile |
| Head | 50% | High damage/lethal potential, risky |

**Animal State Modifiers to Targeting**
| State | Legs | Torso | Head |
|-------|------|-------|------|
| Charging | -15% | +10% | +15% |
| Circling | +10% | +0% | -10% |
| Recovering | +15% | +15% | +15% |
| Threatening | +0% | +0% | -5% |

Player learns: "It's charging - go for the head" vs "It's circling - take the leg shot."

---

### Equipment & Terrain

**Torch as Deterrent**
-0.3 boldness. Feeds into additive formula. Starving wolf (+0.3 hunger) cancels torch effect. Well-fed wolf backs off. Context emerges from math.

**Thrown Weapons**
- Throw at Mid → weapon lands at target distance
- `CombatState.ThrownWeaponDistanceMeters` tracks position
- "Retrieve Weapon" action when within ~3m of spear
- Retrieval = vulnerable turn, animal gets free attack if in range
- Post-combat: automatic retrieval

**Secondary Throwables**
Stones available at Mid range: lower damage, keep primary weapon.

**Terrain Effects**
Location hazard rating affects both combatants:
- Dodge: `dodgeChance *= (1 - hazardLevel * 0.3)`
- Movement: `moveDistance *= (1 - hazardLevel * 0.2)`

---

### Animal Behavior & Recovery

**Standoff Boldness Decay**
Each turn without engagement:
- Base decay: -0.03 boldness
- Player holds ground: additional -0.02
- Player shows weakness: +0.05 instead

Creates natural standoff resolution. The wolf is deciding. Your fire is burning.

**Recovery Timing Formula**
```
turns = floor(speciesBase * (2 - vitality) * random(0.8-1.2))
```

| Animal | Base | Character |
|--------|------|-----------|
| Bear | 0.8 | Relentless - almost no window |
| Wolf | 1.5 | Quick but readable |
| Big cat | 1.2 | Fast and deadly |

Examples:
- Healthy wolf: 1 turn window
- Wounded wolf (40%): 2 turns
- Healthy bear: 0-1 turns

Different animals require different tactical approaches.

---

### Non-Lethal Resolution & Outcomes

**Standoff as Common Outcome**
- Boldness decay naturally leads to animal retreat
- Mutual exhaustion (both sides low energy)
- Player can accept standoff and disengage at Far range

**Wounded Animal Escapes → Future Opportunity**
- Creates `WoundedPrey` tension
- Blood trail tracking available later
- "Victory" can mean "it left wounded"

**Outcome Types**
| Outcome | Condition |
|---------|-----------|
| Victory | Animal killed |
| Animal Fled | Boldness < threshold at Far range |
| Player Disengaged | Successful disengage at Far |
| Mutual Retreat | Both sides back off (time pressure) |
| Animal Disengaged | Player incapacitated, animal leaves |
| Distracted | Dropped meat, animal took bait |
| Defeat | Player killed |

**The Key Insight**
Killing isn't always the goal. Sometimes you want the wolf to leave so you can get back to your fire. Combat serves the larger survival loop.
