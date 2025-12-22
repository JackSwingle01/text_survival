# Tension Story Arcs Design

5 new tension-based story arcs designed for **meaningful player tradeoffs**, **emergent gameplay**, and **thematic immersion** in the Ice Age survival setting.

---

## Arc 1: The Blood Trail (WoundedPrey Tension)

**Theme:** The hunt isn't over when you hit your target. Wounded prey flees, leaving a trail of opportunity and danger.

**Core Tradeoff:** Chase valuable meat deeper into dangerous territory, or abandon the kill and lose precious calories?

### Tension: `WoundedPrey`
- **Decay:** 0.08/hour (blood trail goes cold)
- **At Camp:** Yes (if you return, trail is lost)
- **Properties:** AnimalType, EstimatedDistance, BloodTrailStrength

### Events (3-4 stage arc)

**Stage 1: "The Hit"** (Entry point - triggered during hunting)
- You strike the animal but it bolts
- Blood splatter on the snow, tracks leading away
- **Choices:**
  - **Follow immediately** → Creates WoundedPrey (0.4), time cost
  - **Mark the direction, continue hunting** → Creates WoundedPrey (0.2), lower severity
  - **Let it go** → No tension, save time but lose the kill

**Stage 2: "Blood in the Snow"** (Requires WoundedPrey)
- Trail still visible, animal slowing
- But you're getting further from camp...
- Weight modifier: +2.0 if LowOnFood
- **Choices:**
  - **Press on** → Chance to find dying animal OR escalate (animal still moving)
  - **Set a snare on the trail** → Costs PlantFiber, chance to catch it later (MarkedDiscovery)
  - **Turn back** → Resolves tension, no reward

**Stage 3: "The Dying Animal"** (Requires WoundedPrey > 0.5)
- You find it, weakened but alive
- Not alone — scavengers have noticed too
- **Choices:**
  - **Finish it quickly** → Meat reward, but SpawnEncounter (scavenger) with low boldness
  - **Wait for it to die** → Time cost, scavengers may claim it first
  - **Scare off scavengers first** → Risk injury, but clean kill

**Stage 4: "Scavengers Converge"** (Requires WoundedPrey > 0.7, HasPredators)
- Too late — predators have found the blood trail
- They're between you and the animal
- **Choices:**
  - **Fight for your kill** → Encounter with predator, high boldness
  - **Abandon and retreat** → Resolves tension, lose everything
  - **Create distraction** → Costs resources (meat if you have any), chance to grab carcass

### Intersections
- **With Stalked:** Blood trail can CREATE Stalked tension (predators following the blood)
- **With FoodScentStrong:** Butchering at the kill site compounds danger
- **With fire margin:** Deep pursuit = risky return trip

---

## Arc 2: The Pack (PackNearby Tension)

**Theme:** A lone predator can be faced. A pack changes everything.

**Core Tradeoff:** Fire is your only real defense against a pack, but you can't carry your fire with you.

### Tension: `PackNearby`
- **Decay:** 0.03/hour (pack is patient)
- **At Camp:** Yes (fire deters approach)
- **Properties:** PackSize (2-6), AnimalType, LastSeenDirection

### Events (4-5 stage arc)

**Stage 1: "Pack Signs"** (Requires InAnimalTerritory, HasPredators)
- Multiple tracks, recent. Coordinated movement patterns.
- This isn't a lone hunter.
- **Choices:**
  - **Move carefully, watch flanks** → Time cost, no tension yet (awareness)
  - **Pick up pace toward camp** → Creates PackNearby (0.2)
  - **Hold position, assess** → Creates PackNearby (0.3), better intel on pack size

**Stage 2: "Eyes in the Treeline"** (Requires PackNearby)
- Glimpses of movement. They're paralleling you.
- Not attacking yet — testing, probing.
- Weight modifier: +3.0 if Injured, +2.0 if Slow
- **Choices:**
  - **Keep moving steadily** → May resolve OR escalate (50/50)
  - **Make yourself large, make noise** → De-escalates (-0.1) or escalates (+0.2)
  - **Light a torch** → Costs tinder + fuel, strong de-escalation (-0.3)
  - **Run for camp** → Escalates (+0.3), triggers chase instinct

**Stage 3: "Circling"** (Requires PackNearby > 0.4)
- They're closing the circle. Cutting off escape routes.
- You need defensible ground — NOW.
- **Choices:**
  - **Find high ground/choke point** → Time cost, better defensive position
  - **Back against cliff/tree** → Limits attack angles, but also limits escape
  - **Make a break for camp** → Speed check, failure = Stage 4
  - **Start a fire here** → Costs fuel/tinder, fire-starting check, creates safe zone

**Stage 4: "The Pack Commits"** (Requires PackNearby > 0.7)
- They've decided. This is happening.
- Multiple attackers, reduced by any defensive position/fire
- **Resolution:**
  - Fight (multiple attacks, rolling encounters)
  - Fire holds them (if you have one burning)
  - Sacrifice (drop all meat, they take it, you escape)

### Intersections
- **With Stalked:** PackNearby is WORSE than Stalked — can't confront head-on
- **With fire management:** Torch/fire is your lifeline but costs resources
- **With shelter:** Existing shelter becomes defensible position
- **With injuries:** Pack targets the weak — injury makes this arc deadlier

---

## Arc 3: The Den (ClaimedTerritory Tension)

**Theme:** You found shelter. Something else found it first.

**Core Tradeoff:** Superior shelter location vs. evicting the current tenant. Is this cave worth a fight?

### Tension: `ClaimedTerritory`
- **Decay:** 0.0/hour (structural - the animal lives there)
- **At Camp:** N/A (only exists at discovered location)
- **Properties:** AnimalType, Location, DenQuality (shelter value if claimed)

### Events (3-4 stage arc)

**Stage 1: "The Find"** (During exploration, requires location without shelter)
- A cave mouth. An overhang. Natural protection from wind and snow.
- But there are signs — tracks, scat, the smell of animal.
- **Choices:**
  - **Investigate carefully** → Creates ClaimedTerritory (0.3), reveals animal type + den quality
  - **Mark it and leave** → Creates MarkedDiscovery, come back prepared
  - **Move on** → No tension, lose the opportunity

**Stage 2: "Assessing the Claim"** (Requires ClaimedTerritory)
- You know what lives here now. The question is: is it worth it?
- Event shows: animal type, estimated danger, shelter quality comparison to current camp
- Weight modifier: +3.0 if ShelterWeakened or no shelter
- **Choices:**
  - **Wait for it to leave** → Time cost, may work (hibernating bear = seasons, wolf = daily hunting)
  - **Drive it out** → Noise, fire, aggression — may work or trigger fight
  - **Fight for it** → Immediate encounter, winner takes the den
  - **Abandon the claim** → Resolves tension, no reward

**Stage 3: "The Confrontation"** (Requires ClaimedTerritory > 0.5, or triggered by "Drive it out"/"Fight")
- The animal is aware of you. It's defending its home.
- Encounter spawns with HIGH boldness (it's cornered, protecting territory)
- **Resolution:**
  - Win fight → Claim den as new shelter location (or camp relocation option)
  - Lose/flee → Injured, tension resolved, den lost
  - Successful drive-out → Den claimed without fight (rare)

**Stage 4: "Claiming the Den"** (After successful confrontation)
- The shelter is yours. But there's work to do.
- Remove old bedding (disease risk), check for secondary entrances, make it livable
- **Outcome:** New shelter feature added to location, potential camp relocation prompt

### Intersections
- **With shelter system:** Creates new shelter locations dynamically
- **With predator encounters:** High-stakes fight (cornered animal = dangerous)
- **With camp relocation:** Successful claim could prompt "move camp here?"
- **With ShelterWeakened:** Damaged shelter makes den MORE attractive (weight modifier)

---

## Arc 4: The Herd (HerdNearby Tension)

**Theme:** Food is passing through. Enough to last weeks. But so are the predators following them.

**Core Tradeoff:** Major food opportunity vs. timing pressure, stampede risk, and predator convergence.

### Tension: `HerdNearby`
- **Decay:** 0.15/hour (they're migrating — window closes fast)
- **At Camp:** Yes (you can hear them from camp, but they're moving)
- **Properties:** HerdSize (Large/Massive), AnimalType (Deer/Caribou/Bison), Direction

### Events (3-4 stage arc)

**Stage 1: "Distant Thunder"** (Random during expedition, more likely in open terrain)
- The ground trembles. A sound like distant thunder, but rhythmic.
- A herd is moving through the area. Hundreds of animals.
- **Choices:**
  - **Track them** → Creates HerdNearby (0.4), reveals herd type and direction
  - **Let them pass** → No tension, no opportunity
  - **Rush to intercept** → Creates HerdNearby (0.6), risky positioning

**Stage 2: "The Edge of the Herd"** (Requires HerdNearby)
- You've found them. A river of fur and hooves.
- Predators shadow the edges — wolves, picking off the weak.
- Weight modifier: +2.0 for each hour elapsed (urgency — they're leaving)
- **Choices:**
  - **Hunt the stragglers** → Safer, smaller reward (1-2 animals)
  - **Go for a prime kill** → Riskier, larger reward, stampede chance
  - **Wait for predator leftovers** → Scavenge after wolves make kills (time cost, less meat)
  - **Observe and learn** → No kill, but reveals migration route (future opportunity)

**Stage 3a: "The Kill"** (Successful hunt from Stage 2)
- You've brought one down. Meat for days.
- But the blood is in the air. And you're far from camp.
- **Immediate:** Large meat reward
- **Complication:** Creates FoodScentStrong (0.5), attracts predators
- **Choice:** Butcher here (time + scent) or drag carcass toward camp (slow, exhausting)

**Stage 3b: "Stampede"** (Failed "prime kill" attempt or bad luck)
- They've spooked. The herd is running — toward you.
- **Choices:**
  - **Run perpendicular** → Speed check, escape or get trampled
  - **Find cover** → Look for rock/tree, may work
  - **Drop and pray** → Risky, but sometimes they flow around you

**Stage 4: "The Followers"** (Requires HerdNearby resolved with kill)
- The herd has moved on. But their shadows haven't.
- Wolves that were following the herd are now following YOU.
- **Creates:** Stalked or PackNearby tension (depending on pack size observed)

### Intersections
- **With hunting system:** Extends hunting to "event hunt" with higher stakes
- **With Stalked/Pack arcs:** Herd hunt can TRIGGER pack attention
- **With FoodScentStrong:** Butchering herd kill compounds danger
- **With fire margin:** Big opportunity, but far from camp — classic tradeoff
- **With weather:** Herds move faster in storms, shorter window

---

## Arc 5: The Bitter Snap (DeadlyCold Tension)

**Theme:** Cold isn't gradual. It comes in killing waves.

**Core Tradeoff:** Race for camp through dangerous conditions, or shelter in place with inadequate resources?

### Tension: `DeadlyCold`
- **Decay:** 0.0/hour (weather-based, resolves when weather changes)
- **At Camp:** N/A (triggers resolution — you made it to fire)
- **Properties:** TemperatureDrop, TimeRemaining (estimate before frostbite)

### Events (3-4 stage arc, FAST escalation)

**Stage 1: "The Wind Shifts"** (Requires ExtremelyCold OR (IsBlizzard AND OnExpedition))
- Temperature plummeting. Wind cutting through everything.
- This isn't normal cold. This is killing cold.
- **Choices:**
  - **Run for camp** → Creates DeadlyCold (0.4), race begins
  - **Find immediate shelter** → Look for terrain protection, slower approach
  - **Build emergency shelter** → Time + materials, but stops the clock

**Stage 2: "Numb"** (Requires DeadlyCold, auto-triggers within 10-15 min)
- Can't feel your fingers. Toes going next.
- Thoughts getting sluggish. This is how it starts.
- Weight modifier: +3.0 always (urgent)
- **Choices:**
  - **Keep moving** → Body heat from exertion, but stamina cost
  - **Stop and warm hands** → Slows frostbite, but costs time
  - **Burn something for warmth** → Emergency fire attempt, costs materials

**Stage 3: "Frostbite Setting In"** (Requires DeadlyCold > 0.5)
- Skin turning white. Damage happening NOW.
- **Automatic:** Applies Frostbite effect (0.3) to extremities
- **Choices:**
  - **Sacrifice fingers to save core** → Stop protecting hands, move faster
  - **Emergency bivouac** → Stop, dig in, wait for conditions to break
  - **Final push** → All-or-nothing sprint for camp

**Stage 4: "The Light of Camp"** OR "Darkness" (Resolution)
- **Success:** Made it to fire. Frostbite damage but alive.
- **Failure:** Collapse in snow. Fade to black. (Death or rescue event if severity < critical)

### Intersections
- **With fire skill:** Can you start emergency fire in these conditions?
- **With shelter knowledge:** Emergency bivouac is a learned skill
- **With injury:** Slow movement = death
- **With fuel reserves:** Having materials for emergency fire is lifesaving
- **With WoundedPrey/PackNearby:** Compound tensions during cold snap = nightmare scenario

---

## Arc 6: The Fever Dream (Sickness Tension)

**Theme:** The enemy within. Your own body betraying you.

**Core Tradeoff:** You must maintain fire and find food while your ability to think clearly degrades.

### Tension: `FeverRising`
- **Decay:** 0.01/hour (illness runs its course, but slowly)
- **At Camp:** Yes (rest accelerates recovery: 0.03/hour)
- **Properties:** Severity, CausedBy (wound/water/exposure)

### Events (3-4 stage arc, simplified)

**Stage 1: "Something Wrong"** (Triggered by: untreated wound 24h+, contaminated water, severe exposure)
- Chills that won't stop. Head pounding. Something's wrong inside.
- **Choices:**
  - **Rest by fire** → Time cost, may prevent escalation
  - **Push through** → Creates FeverRising (0.3)
  - **Treat with herbs** → If available, de-escalates

**Stage 2: "Fever Takes Hold"** (Requires FeverRising)
- Sweating despite the cold. Shivering despite the heat.
- World starting to blur at the edges.
- **Effects applied:** Fever effect (stat penalties, capacity reduction)
- **Choices:**
  - **Rest, stay warm** → Accelerated decay, but time passes
  - **Keep working** → Maintain survival, but escalates (+0.1)
  - **Seek medicinal plants** → Expedition risk while impaired

**Stage 3: "The Fire Illusion"** (Requires FeverRising > 0.4, ONE hallucination type)
- **The hallucination:** "Your fire is dying! Embers fading fast!"
- Player must choose: rush to tend fire, or trust that it's the fever talking
- **Reality:** Fire is fine (80%) OR fire actually IS low (20% — fever was right)
- **Outcome if fooled:** Waste time/energy rushing to fire that's fine
- **Outcome if ignored when real:** Fire goes out, serious problem
- **Player learning:** During fever, verify before reacting. But sometimes the fever shows truth.

**Stage 4: "Crisis Point"** (Requires FeverRising > 0.7)
- Collapse. The fever peaks.
- **Resolution branch:**
  - **Adequate care (fire + shelter + rest):** Recovery begins, tension decays faster
  - **Inadequate care:** Death check. Survival = lingering weakness effect
  - **Perfect care (herbs + fire + shelter + water):** Full recovery

### Intersections
- **With WoundUntreated:** Untreated wounds CAUSE fever arc
- **With fire management:** The Fire Illusion specifically targets fire anxiety
- **With food/water:** Must stay fed while barely functional
- **Future expansion:** More hallucination types can be added later (phantom predators, false discoveries, etc.)

---

---

## Implementation Plan

**Confirmed:** These arcs integrate cleanly with the existing tension/event system. No architectural changes needed.

### Files to Modify

| File | Changes |
|------|---------|
| `Actions/Tensions/ActiveTension.cs` | Add 6 factory methods for new tension types |
| `Actions/GameEventRegistry.cs` | Add 6 cases to tension switch (lines 242-253) |
| `Actions/GameContext.cs` | Add ~18 new EventConditions + Check() cases |

### Files to Create

| File | Purpose |
|------|---------|
| `Actions/Events/GameEventRegistry.BloodTrail.cs` | WoundedPrey arc (3-4 events) |
| `Actions/Events/GameEventRegistry.Pack.cs` | PackNearby arc (4-5 events) |
| `Actions/Events/GameEventRegistry.Den.cs` | ClaimedTerritory arc (3-4 events) |
| `Actions/Events/GameEventRegistry.Herd.cs` | HerdNearby arc (3-4 events) |
| `Actions/Events/GameEventRegistry.ColdSnap.cs` | DeadlyCold arc (3-4 events) |
| `Actions/Events/GameEventRegistry.Fever.cs` | FeverRising arc (3-4 events) |

### New Tensions

```csharp
// In ActiveTension.cs - factory methods encoding decay behavior
WoundedPrey(severity, animalType, location)      // Decays 0.08/hr, at camp (trail goes cold)
PackNearby(severity, animalType)                  // Decays 0.03/hr, at camp (fire deters)
ClaimedTerritory(severity, animalType, location) // No decay (structural - animal lives there)
HerdNearby(severity, animalType, direction)      // Decays 0.15/hr (migrating fast)
DeadlyCold(severity)                              // No decay (weather-based, resolves at fire)
FeverRising(severity, description)                // Decays 0.01/hr (0.03 at camp with rest)
```

### New EventConditions

```csharp
// Tension conditions (in GameContext.cs enum)
WoundedPrey, WoundedPreyHigh, WoundedPreyCritical  // any, >0.5, >0.7
PackNearby, PackNearbyHigh, PackNearbyCritical     // any, >0.4, >0.7
ClaimedTerritory, ClaimedTerritoryHigh             // any, >0.5
HerdNearby, HerdNearbyUrgent                       // any, >0.6 (window closing)
DeadlyCold, DeadlyColdCritical                     // any, >0.6
FeverRising, FeverHigh, FeverCritical              // any, >0.4, >0.7
```

### Fire Illusion (Single Hallucination - Expandable Later)

The fever's ONE hallucination is built into the arc events, not a separate system:
- "The Fire Illusion" event requires FeverRising > 0.4
- 80% chance fire is fine (player fooled), 20% chance fire actually is low (fever was right)
- This creates the core uncertainty without complex hallucination infrastructure
- Future expansion: add more hallucination event types as separate events

### Event Registration

Each new event file adds to `AllEventFactories` in the main registry:
```csharp
// In GameEventRegistry.cs
AllEventFactories.AddRange(BloodTrailEvents);
AllEventFactories.AddRange(PackEvents);
AllEventFactories.AddRange(DenEvents);
AllEventFactories.AddRange(HerdEvents);
AllEventFactories.AddRange(ColdSnapEvents);
AllEventFactories.AddRange(FeverEvents);
```

### Arc Interactions (Emergent Gameplay)
| Arc A | Arc B | Compound Effect |
|-------|-------|-----------------|
| Blood Trail | Pack | Pack drawn to blood, converging threats |
| Blood Trail | Herd | Chase wounded herd animal = pack followers |
| Pack | DeadlyCold | Can't flee properly, fire is only hope |
| Herd | Pack | Wolves following herd now following YOU |
| Den | Pack | Evict wolves from den = pack revenge? |
| DeadlyCold | FeverRising | Body can't regulate, both kill faster |
| WoundedPrey | Stalked | Predator following YOUR blood trail |

---

## Implementation Order

### Batch 1: Foundation
1. Add all 6 tension types to `ActiveTension.cs`
2. Add switch cases to `GameEventRegistry.cs`
3. Add all EventConditions to `GameContext.cs` with Check() implementations

### Batch 2: Blood Trail Arc (simplest, ties into hunting)
- Create `GameEventRegistry.BloodTrail.cs`
- 3-4 events: TheHit, BloodInSnow, DyingAnimal, ScavengersConverge

### Batch 3: Cold Snap Arc (urgent, fast-paced, simple)
- Create `GameEventRegistry.ColdSnap.cs`
- 3-4 events: WindShifts, Numb, FrostbiteSettingIn, Resolution

### Batch 4: Pack Arc (builds on predator system)
- Create `GameEventRegistry.Pack.cs`
- 4-5 events: PackSigns, EyesInTreeline, Circling, PackCommits

### Batch 5: Herd Arc (extends hunting, creates opportunity events)
- Create `GameEventRegistry.Herd.cs`
- 3-4 events: DistantThunder, EdgeOfHerd, TheKill/Stampede, TheFollowers

### Batch 6: Den Arc (shelter + territory conflict)
- Create `GameEventRegistry.Den.cs`
- 3-4 events: TheFind, AssessingClaim, Confrontation, ClaimingDen

### Batch 7: Fever Arc (simplified, one hallucination)
- Create `GameEventRegistry.Fever.cs`
- 3-4 events: SomethingWrong, FeverTakesHold, FireIllusion, CrisisPoint

---

## Summary

| Arc | Core Tension | Stages | Primary Tradeoff |
|-----|--------------|--------|------------------|
| Blood Trail | WoundedPrey | 3-4 | Chase meat vs. safety margin |
| The Pack | PackNearby | 4-5 | Fire dependence vs. mobility |
| The Den | ClaimedTerritory | 3-4 | Superior shelter vs. eviction fight |
| The Herd | HerdNearby | 3-4 | Feast opportunity vs. stampede/timing |
| Bitter Snap | DeadlyCold | 3-4 | Race home vs. shelter in place |
| Fever Dream | FeverRising | 3-4 | Maintain survival while impaired |

**Total new events:** ~20-24 events across 6 arc files
**Total new tensions:** 6 types
**Total new conditions:** ~18

---

## Detailed Implementation Specifications

### 1. ActiveTension.cs — New Factory Methods

```csharp
/// <summary>
/// Player wounded prey that escaped. Trail decays as blood dries/snow covers.
/// </summary>
public static ActiveTension WoundedPrey(double severity, string? animalType = null, Location? location = null) => new(
    type: "WoundedPrey",
    severity: severity,
    decayPerHour: 0.08,
    decaysAtCamp: true,  // Trail goes cold if you return to camp
    relevantLocation: location,
    animalType: animalType
);

/// <summary>
/// A pack of predators is nearby. Fire deters them; decay at camp reflects safety.
/// </summary>
public static ActiveTension PackNearby(double severity, string? animalType = null) => new(
    type: "PackNearby",
    severity: severity,
    decayPerHour: 0.03,
    decaysAtCamp: true,
    animalType: animalType
);

/// <summary>
/// A shelter location is claimed by wildlife. No decay - structural situation.
/// </summary>
public static ActiveTension ClaimedTerritory(double severity, string? animalType = null, Location? location = null) => new(
    type: "ClaimedTerritory",
    severity: severity,
    decayPerHour: 0.0,
    decaysAtCamp: false,
    relevantLocation: location,
    animalType: animalType
);

/// <summary>
/// A herd is passing through. High decay - they're migrating, window closes fast.
/// </summary>
public static ActiveTension HerdNearby(double severity, string? animalType = null, string? direction = null) => new(
    type: "HerdNearby",
    severity: severity,
    decayPerHour: 0.15,
    decaysAtCamp: true,
    animalType: animalType,
    direction: direction
);

/// <summary>
/// Deadly cold exposure. No natural decay - resolves when reaching fire or shelter.
/// </summary>
public static ActiveTension DeadlyCold(double severity) => new(
    type: "DeadlyCold",
    severity: severity,
    decayPerHour: 0.0,
    decaysAtCamp: false  // Resolves via event, not decay
);

/// <summary>
/// Fever/sickness rising. Slow decay, faster at camp with rest.
/// Note: Camp decay handled by special logic in TensionRegistry.Update()
/// </summary>
public static ActiveTension FeverRising(double severity, string? description = null) => new(
    type: "FeverRising",
    severity: severity,
    decayPerHour: 0.01,
    decaysAtCamp: true,  // Decays 3x faster at camp (0.03 effective)
    description: description
);
```

### 2. GameEventRegistry.cs — Switch Cases (lines ~242-253)

Add to existing switch:
```csharp
"WoundedPrey" => ActiveTension.WoundedPrey(tc.Severity, tc.AnimalType, tc.RelevantLocation),
"PackNearby" => ActiveTension.PackNearby(tc.Severity, tc.AnimalType),
"ClaimedTerritory" => ActiveTension.ClaimedTerritory(tc.Severity, tc.AnimalType, tc.RelevantLocation),
"HerdNearby" => ActiveTension.HerdNearby(tc.Severity, tc.AnimalType, tc.Direction),
"DeadlyCold" => ActiveTension.DeadlyCold(tc.Severity),
"FeverRising" => ActiveTension.FeverRising(tc.Severity, tc.Description),
```

### 3. GameContext.cs — New EventConditions

Add to EventCondition enum:
```csharp
// WoundedPrey arc
WoundedPrey,           // Any WoundedPrey tension exists
WoundedPreyHigh,       // WoundedPrey severity > 0.5
WoundedPreyCritical,   // WoundedPrey severity > 0.7

// Pack arc
PackNearby,            // Any PackNearby tension exists
PackNearbyHigh,        // PackNearby severity > 0.4
PackNearbyCritical,    // PackNearby severity > 0.7

// Den arc
ClaimedTerritory,      // Any ClaimedTerritory tension exists
ClaimedTerritoryHigh,  // ClaimedTerritory severity > 0.5

// Herd arc
HerdNearby,            // Any HerdNearby tension exists
HerdNearbyUrgent,      // HerdNearby severity > 0.6 (window closing)

// Cold Snap arc
DeadlyCold,            // Any DeadlyCold tension exists
DeadlyColdCritical,    // DeadlyCold severity > 0.6

// Fever arc
FeverRising,           // Any FeverRising tension exists
FeverHigh,             // FeverRising severity > 0.4
FeverCritical,         // FeverRising severity > 0.7
```

Add to Check() method switch:
```csharp
EventCondition.WoundedPrey => Tensions.HasTension("WoundedPrey"),
EventCondition.WoundedPreyHigh => Tensions.HasTensionAbove("WoundedPrey", 0.5),
EventCondition.WoundedPreyCritical => Tensions.HasTensionAbove("WoundedPrey", 0.7),

EventCondition.PackNearby => Tensions.HasTension("PackNearby"),
EventCondition.PackNearbyHigh => Tensions.HasTensionAbove("PackNearby", 0.4),
EventCondition.PackNearbyCritical => Tensions.HasTensionAbove("PackNearby", 0.7),

EventCondition.ClaimedTerritory => Tensions.HasTension("ClaimedTerritory"),
EventCondition.ClaimedTerritoryHigh => Tensions.HasTensionAbove("ClaimedTerritory", 0.5),

EventCondition.HerdNearby => Tensions.HasTension("HerdNearby"),
EventCondition.HerdNearbyUrgent => Tensions.HasTensionAbove("HerdNearby", 0.6),

EventCondition.DeadlyCold => Tensions.HasTension("DeadlyCold"),
EventCondition.DeadlyColdCritical => Tensions.HasTensionAbove("DeadlyCold", 0.6),

EventCondition.FeverRising => Tensions.HasTension("FeverRising"),
EventCondition.FeverHigh => Tensions.HasTensionAbove("FeverRising", 0.4),
EventCondition.FeverCritical => Tensions.HasTensionAbove("FeverRising", 0.7),
```

---

## Detailed Event Specifications

### Blood Trail Arc Events

**Event 1: "Blood in the Snow"** (Entry point)
- **Triggers:** During hunting, after a hit that doesn't kill
- **Integration:** This is a new hunting outcome, not a random event
- **Base Weight:** N/A (outcome-driven)
- **Required Conditions:** Working (hunting activity)

**Situational Reading (wound severity in description):**
- "Bright red arterial spray, pumping with each heartbeat" → High success chase (80%+)
- "Dark blood, muscle wound, the animal barely slowed" → Low success chase (30%)
- **Player learning:** Arterial = worth chasing. Dark/muscle = probably not.

**Choices:**
  1. "Follow the trail" → WoundedPrey(0.4), time +15min, success rate from wound type
  2. "Mark the trail" → **Requires cutting tool** (blaze trees), WoundedPrey(0.2), time +5min
  3. "Let it go" → No tension, no time cost

**Preparation Payoff:**
- High Moving capacity → faster tracking, less tension decay during pursuit
- Cutting tool → enables trail marking option

---

**Event 2: "The Trail Continues"** (Escalation)
- **Base Weight:** 1.5
- **Required:** WoundedPrey
- **Weight Modifiers:** LowOnFood (+2.0), Injured (-0.5)

**Situational Reading:**
- Blood trail description indicates progress: "bright pools, closely spaced" = animal slowing
- Track depth: "staggering prints, dragging leg" = close to collapse
- Time of day matters: "light fading" = urgency

**Choices:**
  1. "Press on" → Success rate based on reading (50-80%), find dying animal or escalate
  2. "Set snare on trail" → **Requires PlantFiber + cutting tool**, MarkedDiscovery tension
  3. "Turn back" → Resolves tension

---

**Event 3: "The Dying Animal"** (Resolution path A)
- **Base Weight:** 2.0
- **Required:** WoundedPreyHigh

**Situational Reading (scavenger type in description):**
- "Ravens circle overhead, cawing" → Noise works to drive off
- "A wolf watches from the treeline" → Need fire or weapon display
- "Multiple shadows moving in the brush" → Much harder

**Choices:**
  1. "Finish it quickly" → Meat reward, 30% encounter (scavenger type)
  2. "Wait for it to die" → Time +20min, scavenger claim risk based on type
  3. "Scare off scavengers" — **Resource-for-certainty tiers:**
     - Posturing alone: 40% success (free, risky)
     - Throw burning brand: +30% success (**costs Tinder + small fuel**)
     - Weapon display: success scales with weapon type (spear > knife > stones)
     - Noise (ravens only): 80% success (free)

---

**Event 4: "Scavengers Converge"** (Resolution path B)
- **Base Weight:** 2.5
- **Required:** WoundedPreyCritical, HasPredators

**Situational Reading:**
- Scavenger count in description: "a lone wolf" vs. "the pack has arrived"
- Time of day: "dusk settling" = scavengers bolder

**Choices:**
  1. "Fight for your kill" → Encounter (boldness based on count), meat if win
  2. "Abandon" → Resolves, no reward
  3. "Create distraction" — **Resource specificity:**
     - Throw YOUR carried meat: High effectiveness, lose all carried meat
     - Throw scraps from carcass (if butchering started): Medium effectiveness, lose partial yield
     - **Costs depend on what you have**

---

### Pack Arc Events

**Event 1: "Pack Signs"** (Entry)
- **Base Weight:** 0.8
- **Required:** InAnimalTerritory, HasPredators, Working OR Traveling
- **Weight Modifiers:** HasMeat (+2.0), Injured (+1.5)

**Situational Reading (pack size from tracks):**
- "Three distinct trails, moving in formation" → Small pack (2-3)
- "The snow is churned with prints, impossible to count" → Large pack (5+)
- Scat freshness: "still steaming" = very close, "frozen solid" = passed hours ago
- **Player learning:** Read track patterns to assess threat before committing

**Choices:**
  1. "Move carefully, watch flanks" → Time +10min, no tension (awareness only)
  2. "Pick up pace toward camp" → PackNearby(0.2)
  3. "Assess the pack" → PackNearby(0.3), reveals pack size estimate in next event

---

**Event 2: "Eyes in the Treeline"** (Escalation)
- **Base Weight:** 1.5
- **Required:** PackNearby
- **Weight Modifiers:** Injured (+3.0), Slow (+2.0), HasMeat (+2.0)

**Situational Reading (wolf posture determines noise outcome):**
- "Ears forward, watching with curiosity" → Noise works (de-escalate)
- "Ears flat against skull, circling low" → Noise escalates (committed)
- "One wolf sits while others pace" → Alpha assessing, outcome uncertain
- **Player learning:** Read the posture, then choose. Wrong read = wrong outcome.

**Choices:**
  1. "Keep moving steadily" → 50% resolve, 30% escalate (+0.15), 20% de-escalate (-0.1)
  2. "Make yourself large, shout" → Outcome based on posture reading (see above)
  3. "Light a torch" → **Requires Tinder + Stick (fuel)**, de-escalate (-0.3)
  4. "Run for camp" → Escalate (+0.3), triggers chase instinct
  5. **NEW:** "Back away slowly, maintain eye contact" → Low cost, moderate success, only works if you read their commitment correctly

---

**Event 3: "Circling"** (High severity)
- **Base Weight:** 2.0
- **Required:** PackNearbyHigh

**Preparation Gates:**
- "Find defensible ground" success depends on terrain type (from location data)
- "Start fire here" **requires fire-starting tool + tinder + fuel** (full kit)
- **NEW:** "Climb a tree" → Only if location has trees AND player has good Manipulation (injured hands = can't climb)

**Choices:**
  1. "Find defensible ground" → Time +15min, success based on terrain
  2. "Back against obstacle" → Limits attack angles, also limits escape
  3. "Start fire here" → Fire-starting check (tool quality + tinder dryness + wind), if success creates safe zone
  4. "Climb a tree" → **Requires trees in location + Manipulation > 0.5**, escape but trapped until they leave
  5. "Make break for camp" → Speed check based on Moving capacity (60% base, modified)

---

**Event 4: "The Pack Commits"** (Critical)
- **Base Weight:** 3.0
- **Required:** PackNearbyCritical
- **Resolution:** Forced encounter OR fire holds them

**Fire Defense Tiers (resource-for-certainty):**
- Small fire (minimum fuel): Holds them but they wait (time pressure, can't leave)
- Large fire (2x fuel): Drives them off, tension resolves
- **Player chooses:** How much fuel to commit?

**Choices:**
  1. "Stand and fight" → Multi-stage encounter (2-3 wolves in sequence), damage accumulates
  2. "Feed the fire" → **Costs variable fuel**, effectiveness scales with investment
  3. "Drop all meat and flee" → Lose all carried meat, 80% escape (they take the food)

---

### Den Arc Events

**Event 1: "The Find"** (Entry)
- **Base Weight:** 0.6
- **Required:** Working (exploring), location without ShelterFeature
- **Weight Modifiers:** ShelterWeakened (+3.0), ExtremelyCold (+2.0)

**Situational Reading (den quality from description):**
- "Deep cave with narrow entrance, dry and windless inside" → Excellent shelter, hard to evict
- "Shallow overhang, some protection from wind" → Modest shelter, easier to claim
- "A rocky crevice, barely large enough to crawl into" → Minimal shelter, easy claim
- **Player learning:** Entrance size + depth = difficulty estimate

**Situational Reading (occupancy signs):**
- Wind direction in description: Downwind approach = stealthier investigation
- Scat age: "old, dried droppings" = animal out hunting, "fresh scat, still warm" = home
- Track freshness: Same pattern as pack arc

**Preparation Gates:**
- Having torch lit: Reveals more about interior but may alert animal
- Approach from downwind: Better outcomes for "investigate carefully"

**Choices:**
  1. "Investigate carefully" → ClaimedTerritory(0.3), reveals animal + quality
  2. "Mark it and leave" → **Requires cutting tool** (blaze nearby tree), MarkedDiscovery
  3. "Move on" → No tension

---

**Event 2: "Assessing the Claim"** (Escalation)
- **Base Weight:** 1.5
- **Required:** ClaimedTerritory
- **Description:** Shows animal type, danger level, shelter quality vs current camp

**Situational Reading (animal behavior determines tactics):**
- Bear: Fire works best (smoke them out), noise doesn't help
- Wolves: Noise can work, fire works, may leave for hunting
- Badger/wolverine: Nothing works great, very aggressive when cornered
- **Player learning:** Different animals, different approaches

**Eviction Tactics (resource-for-certainty):**
- "Wait for it to leave" → Works for animals that hunt (wolves), not for hibernators (bear)
- "Smoke them out" → **Requires fuel + fire-starting**, high success, time cost
- "Noise alone" → Free but unreliable, depends on animal type
- **Combined tactics:** Best odds, highest resource cost

**Choices:**
  1. "Wait for it to leave" → Time +60min, success based on animal type
  2. "Drive it out with smoke" → **Requires fuel + fire-starting**, 70% success
  3. "Drive it out with noise" → Free, success based on animal type (30-60%)
  4. "Fight for it now" → Encounter (very high boldness - cornered)
  5. "Abandon claim" → Resolves tension

---

**Event 3: "The Confrontation"** (Resolution)
- **Base Weight:** 2.0
- **Required:** ClaimedTerritoryHigh

**Preparation Payoff (weapon choice matters in confined space):**
- Short weapons (knife): Better in tight den interiors
- Long weapons (spear): Can fight from entrance, don't have to go in
- **Player learning:** Bring the right tool for the situation

**Encounter:** Animal defending territory, boldness 0.9 (cornered, fighting for home)
- **Win outcome:** Den claimed, shelter quality revealed
- **Lose outcome:** Injured, tension resolved, den lost

---

**Event 4: "Claiming the Den"** (Post-victory)
- **Triggered:** After winning confrontation (not random event)

**Choices:**
  1. "Clear it out thoroughly" → Time +30min, clean shelter, no disease risk
  2. "Move in immediately" → 20% disease risk (FeverRising trigger)
  3. "Relocate camp here" → Major action, moves camp to this location

**Outcome:** AddFeature (ShelterFeature) to location, quality based on den type

---

### Herd Arc Events

**Event 1: "Distant Thunder"** (Entry)
- **Base Weight:** 0.5
- **Required:** OnExpedition, NOT InAnimalTerritory (open ground preferred)
- **Weight Modifiers:** LowOnFood (+2.0)

**Situational Reading (herd type from sounds/signs):**
- "Heavy hoofbeats, the ground shaking with each step" → Bison (dangerous, high reward)
- "Lighter, faster rhythm, like distant drumming" → Deer/caribou (safer, moderate reward)
- Direction matters: "moving toward your camp" = intercept feasible, "moving away" = long chase

**Choices:**
  1. "Track them" → HerdNearby(0.4), reveals herd type
  2. "Let them pass" → No tension, no opportunity
  3. "Rush to intercept" → HerdNearby(0.6), risky positioning, skip to Stage 2

---

**Event 2: "Edge of the Herd"** (Action phase)
- **Base Weight:** 2.0 (urgent)
- **Required:** HerdNearby
- **Weight Modifiers:** Per-hour elapsed = urgency increases (they're leaving)

**Situational Reading (target selection):**
- "A young buck favoring its left leg" → Easier target, description telegraphs opportunity
- "A large bull, scarred from past fights" → High risk, high reward
- Predator positions: "wolves on the far side" → Safer to hunt this side
- Terrain: "herd funneling through narrow valley" → Ambush opportunity

**Preparation Gates:**
- "Go for prime kill" **requires ranged weapon** (spear for throwing, stones)
- "Hunt stragglers" can use any weapon (ambush)
- **Having cordage:** Enables snare option on herd path (passive catch attempt)

**Choices:**
  1. "Hunt the stragglers" → 70% small kill, 20% nothing, 10% stampede
  2. "Go for a prime kill" → **Requires ranged weapon**, 40% large kill, 30% nothing, 30% stampede
  3. "Set snare on their path" → **Requires cordage**, passive attempt, check later (MarkedDiscovery)
  4. "Wait for predator leftovers" → Time +45min, 60% scavenge meat
  5. "Observe migration route" → MarkedDiscovery (future opportunity), no immediate reward

---

**Event 3a: "The Kill"** (Success branch)
- **Triggered:** After successful hunt
- **Immediate:** Large meat reward (5-10kg)
- **Complication:** FoodScentStrong(0.5) automatically created

**Choices:**
  1. "Butcher here" → Full yield, Time +40min, scent escalates
  2. "Drag carcass toward camp" → Partial yield, slow travel, Energy cost
  3. "Take what you can carry" → Partial yield, leave rest, faster

---

**Event 3b: "Stampede"** (Failure branch)
- **Triggered:** Bad hunt outcome (spooked the herd)

**Situational Reading (terrain determines options):**
- "Scattered boulders nearby" → "Find cover" has good odds
- "Open grassland, nothing to hide behind" → "Find cover" will fail
- Description should make options clear BEFORE choosing

**Preparation Payoff:**
- High Moving capacity → Better "run perpendicular" odds
- **Drop heavy items:** Option to drop pack for +20% escape (lose carried items)

**Choices:**
  1. "Run perpendicular" → Speed check based on Moving (60-80%)
  2. "Find cover" → **Only works if terrain has cover** (description-dependent)
  3. "Drop and curl up" → 40% they flow around, 60% trampled
  4. **NEW:** "Drop your pack and sprint" → Lose carried items, +20% escape chance

**Trampled outcome:** Blunt damage (20-30), "trampled by herd," expedition aborted

---

**Event 4: "The Followers"** (Aftermath)
- **Triggered:** After kill, when leaving area
- **Description:** Wolves that were following herd now following you

**Creates:** Stalked(0.3) OR PackNearby(0.2) based on observed wolf count
- "A lone wolf peels off from the pack" → Stalked
- "Three shapes detach from the herd's shadow" → PackNearby

---

### Cold Snap Arc Events

**This arc heavily rewards preparation. The core skill is: did you bring emergency supplies?**

**Event 1: "The Wind Shifts"** (Entry)
- **Base Weight:** 1.0
- **Required:** OnExpedition, (ExtremelyCold OR IsBlizzard)
- **Weight Modifiers:** Injured (+2.0), LowOnFuel (+1.5)

**Situational Reading (show the math):**
- Description includes: estimated distance to camp, current Moving capacity
- "Camp is perhaps 30 minutes away at a good pace" + player knows their state
- Wind direction: "wind at your back" = faster but colder, "into the wind" = slower but can breathe
- Terrain: "trees ahead offer windbreak" vs. "open ground, exposed"

**Preparation Gates:**
- "Build emergency shelter" **requires cutting tool + materials** (or debris hut knowledge)
- Success probability shown based on tools + conditions

**Choices:**
  1. "Run for camp" → DeadlyCold(0.4), race begins, show estimated time
  2. "Find terrain shelter" → Search for windbreak, Time +15min
  3. "Build emergency shelter" → **Requires tools + materials**, Time +30min, pauses tension

---

**Event 2: "Going Numb"** (Escalation, fast)
- **Base Weight:** 3.0 (urgent)
- **Required:** DeadlyCold
- **Auto-trigger:** 10-15 minutes after Stage 1
- **Effects:** Cold effect applied if not already

**Resource-for-Certainty (what do you burn?):**
- Tinder + fuel = small fire, temporary warmth, won't last
- **Desperate options:** Burn tool handle? Clothing? The pack itself?
- Each has real, visible costs — player makes informed tradeoff

**Choices:**
  1. "Keep moving" → Exertion warmth, Energy cost, maintains pace
  2. "Warm hands in armpits" → Time +10min, slows frostbite progression
  3. "Emergency fire" → Fire-starting check (tool quality + tinder dryness + wind exposure)
  4. **NEW:** "Burn something for warmth" → Sacrifice item for brief fire (desperate)

---

**Event 3: "Frostbite Setting In"** (Critical)
- **Base Weight:** 3.5
- **Required:** DeadlyColdCritical

**Show the Math (honest, not fever-cruel):**
- "You're X minutes from camp. At your current pace, severe frostbite in Y minutes."
- Player can calculate: do I make it, or do I need to stop?
- **Cold Snap is honest.** The danger is real but the information is true.

**Situational Reading:**
- "The sky is lightening to the south" → Conditions might break, hold on
- "The wind is dying" → Turning point possible
- "Snow thickening, visibility dropping" → It's getting worse

**Effects:** Frostbite(0.3) applied to extremities (hands/feet)

**Choices:**
  1. "Sacrifice extremities to save core" → Stop protecting hands, move faster, permanent damage
  2. "Emergency bivouac" → Dig into snow, wait for weather break
  3. "Final push" → All-or-nothing sprint, high Energy cost, succeed or collapse

---

**Event 4: Resolution** (Outcome)
- **Success (reach camp/fire):** DeadlyCold resolves, Frostbite damage persists but treatable
- **Bivouac success:** Survive, resume when weather breaks
- **Failure (collapse):** Death check based on severity + care conditions
- **Note:** DeadlyCold auto-resolves when NearFire becomes true

**Preparation Payoff Summary:**
- Fire-starting tools + dry tinder = emergency fire option
- Cutting tool + materials = shelter option
- High Moving capacity = better sprint success
- Good insulation (clothing) = slower frostbite progression
- **The weight limit forces tradeoffs:** You can't prepare for everything

---

### Fever Arc Events

**This arc rewards prevention above all. The skill is never getting here.**

**Event 1: "Something Wrong"** (Entry)
- **Base Weight:** 0.8
- **Required:** WoundUntreatedHigh OR prolonged LowTemperature
- **Weight Modifiers:** WoundUntreated (+2.0)

**Prevention is the Skill:**
- Treating wounds promptly prevents this arc
- Clean water prevents this arc
- Staying warm prevents this arc
- **The best players never see Stage 2+**

**Choices:**
  1. "Rest by fire" → Time +60min, 70% no tension, 30% FeverRising(0.2)
  2. "Push through" → FeverRising(0.3)
  3. "Treat the cause" → **Requires appropriate treatment**, prevents tension

---

**Event 2: "Fever Takes Hold"** (Escalation)
- **Base Weight:** 1.5
- **Required:** FeverRising
- **Effects:** Fever effect (stat penalties, capacity reduction)

**Preparation Gates:**
- "Seek medicinal plants" requires knowing which plants help (learned from exploration?)
- Those plants must exist at reachable locations
- Must have capacity to make expedition while impaired

**Choices:**
  1. "Rest, stay warm" → Time +90min, de-escalate (-0.15)
  2. "Keep working" → Escalate (+0.1), maintain survival activities
  3. "Seek medicinal plants" → **Only if known + reachable**, expedition while impaired

---

**Event 3: "Fever Visions"** (Hallucination Pool)
- **Base Weight:** 2.0
- **Required:** FeverHigh

**Multiple Hallucination Types (80/20 fake/real split for each):**

1. **"The Fire Illusion"** (NearFire required)
   - "Your fire is dying! The embers are fading fast!"
   - Reality: 80% fine, 20% actually low

2. **"Footsteps Outside"** (AtCamp required)
   - "Something is circling your camp. Footsteps in the snow."
   - Reality: 80% nothing, 20% actually Stalked tension created

3. **"The Cache is Open"** (HasFood required)
   - "Your food cache — the cover is off. Something got in."
   - Reality: 80% fine, 20% actually Infested tension created

4. **"The Path is Wrong"** (OnExpedition required)
   - "This isn't right. You've gone the wrong way. Camp is... that direction?"
   - Reality: 80% you're fine, 20% actually disoriented (time penalty)

**Hallucination Tells (subtle, for experienced players):**
- Fever text is slightly "off" — more poetic, less precise
- Sensory details that shouldn't be there (colors in darkness, sounds too specific)
- Inconsistencies with established reality
- **Player learning:** During fever, verify before panicking. But don't ignore everything.

**Choices (for each hallucination):**
  1. "React immediately" → If fake = wasted time/resources. If real = handled.
  2. "Ignore it, trust nothing" → If fake = correct. If real = problem escalates.
  3. "Carefully verify first" → Time +3min, reveals truth, then informed choice
  - **Third option is the learned response**

---

**Event 4: "Crisis Point"** (Resolution)
- **Base Weight:** 2.5
- **Required:** FeverCritical

**Preparation Payoff:**
- Having medicinal herbs BEFORE getting sick = faster recovery
- Having food/water stockpiled at camp = can focus on rest
- Having robust fire with fuel reserve = less Fire Illusion anxiety

**Resolution based on conditions:**
  - Fire + Shelter + Rest → Recovery begins, accelerated decay (0.05/hr)
  - Missing conditions → Death check, survival = lingering weakness effect
  - Herbs available → Full recovery, no lingering effects

---

**Fever Design Notes:**
- All hallucinations have the same 80/20 split — consistent rule, learnable
- Third "verify" option always exists — skill expression through patience
- Hallucination tells are subtle but present — rewards careful reading
- **Future expansion:** More hallucination types, intersections with other arcs

---

## Consistent Vocabulary Reference

**These terms ALWAYS mean the same thing. Document for consistent implementation:**

### Blood/Wound Indicators
| Description | Meaning | Chase Success |
|-------------|---------|---------------|
| "Bright red arterial spray, pumping" | Severe wound | 80%+ |
| "Dark blood, muscle wound" | Minor wound | 30% |
| "Barely bleeding, just fur" | Graze | Don't chase |

### Wolf Posture
| Description | Meaning | Noise Outcome |
|-------------|---------|---------------|
| "Ears forward, watching with curiosity" | Testing | De-escalate |
| "Ears flat against skull, circling low" | Committed | Escalate |
| "One wolf sits while others pace" | Alpha assessing | Uncertain |

### Track Freshness
| Description | Meaning |
|-------------|---------|
| "Still steaming" | Very close, minutes ago |
| "Fresh, edges sharp" | Recent, within hour |
| "Edges softening, filling with snow" | Hours ago |
| "Frozen solid, crusted over" | Day or more |

### Pack Size
| Description | Meaning | Threat Level |
|-------------|---------|--------------|
| "Three distinct trails, moving in formation" | Small pack (2-3) | Moderate |
| "Several trails, hard to count" | Medium pack (4-5) | High |
| "Snow churned with prints, impossible to count" | Large pack (5+) | Extreme |

### Terrain/Cover
| Description | Meaning |
|-------------|---------|
| "Scattered boulders nearby" | Cover available |
| "Dense trees" | Climbing possible, windbreak |
| "Open grassland, nothing to hide behind" | No cover, exposed |
| "Rocky outcrop to the east" | Defensible position |

---

## Expedition Loadout Becomes Meaningful

**The weight limit (15kg) forces tradeoffs. These arcs make loadout planning consequential:**

| Item | Weight | Arcs It Helps |
|------|--------|---------------|
| Fire-starting tools | Light | Pack (torch), Cold Snap (emergency fire), Den (smoke out) |
| Tinder (dry) | Light | Pack, Cold Snap, Blood Trail (burning brand) |
| Fuel (sticks/logs) | Heavy | Pack (fire defense), Cold Snap, all fire-related |
| Cutting tool | Medium | Blood Trail (marking), Den (marking), Cold Snap (shelter) |
| Cordage/PlantFiber | Light | Blood Trail (snare), Herd (snare) |
| Ranged weapon (spear) | Medium | Herd (prime kill), Den (fight from entrance) |
| Short weapon (knife) | Light | Den (interior fight) |
| Extra clothing | Heavy | Cold Snap (insulation) |
| Food | Variable | Pack (distraction), emergency calories |
| Herbs | Light | Fever (treatment) |

**The Tradeoff:** You can't carry everything. An expedition focused on hunting needs different loadout than one focused on exploration. Cold weather demands fire supplies. Predator territory demands torch materials.

**Experienced players plan their loadout based on destination and goals.**

---

## Edge Cases and Integration Notes

### Hunting Integration (Blood Trail)
- Need to modify hunting miss/partial outcomes to inject WoundedPrey event
- Current hunting: hit or miss. New flow: hit-kill, hit-wound (new), miss

### DeadlyCold Auto-Resolution
- Add check in GameContext.Update() or event handling:
  ```csharp
  if (Tensions.HasTension("DeadlyCold") && Check(EventCondition.NearFire))
      Tensions.ResolveTension("DeadlyCold");
  ```

### FeverRising Camp Decay Bonus
- Option A: Override decay in TensionRegistry.Update() for FeverRising
- Option B: Create FeverRising with dynamic decay based on location
- Recommendation: Option A, simpler

### Shelter Feature Creation (Den Arc)
- Den victory adds ShelterFeature to location
- Quality range: based on den type discovered (cave = high, overhang = medium)
- Need AddFeature capability in EventResult (already exists)

### Pack Multi-Encounter
- "The Pack Commits" spawns 2-3 sequential encounters
- Implementation: Encounter queue? Or single encounter with special "pack" modifier?
- Recommendation: Single encounter, damage represents multiple attackers

### Stampede Damage
- New damage type? Or Blunt with high value?
- Recommendation: Blunt damage (20-30), source "trampled by herd"

### Herd Animal Types
- Deer, Caribou, Bison (from zone/season context)
- Need to ensure AnimalTerritoryFeature can provide herd animals
- May need to extend GetRandomAnimalName() or add GetRandomHerdAnimal()

---

## Testing Considerations

For each arc, test:
1. Entry conditions trigger correctly
2. Escalation happens at right severity thresholds
3. Resolution outcomes work (rewards, damages, feature changes)
4. Tension decay behaves as expected
5. Cross-arc interactions (Blood Trail → Pack, Herd → Pack, etc.)

Priority test scenarios:
- Complete Blood Trail from hit to scavenger confrontation
- Pack escalation with torch de-escalation
- Den claim with camp relocation
- Herd hunt ending in pack followers
- Cold snap with emergency fire success
- Fever hallucination with all three choice outcomes
