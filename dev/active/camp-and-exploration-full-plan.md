# Camp Investment & Exploration Enhancement - Full Vision

## Goal
Create a reinforcing feedback loop between camp progression and exploration that strengthens early/mid-game experience. Better camps enable farther exploration; exploration discoveries fuel camp improvements.

## User Intent
- **Camp as strategic hub**: Tiered progression with meaningful investment choices
- **Exploration as pull goal**: Rare locations feel earned and rewarding
- **Feedback loop**: Camp upgrades → Better gear → Access rare locations → Special materials → Better camp

## Design Approach

### Camp Progression (Derived State)

Camp tier calculated from features present, not tracked separately:

**Tier 1 - Makeshift Camp** (current)
- Bedding + Fire + Basic Cache
- Benefit: Can rest and store

**Tier 2 - Established Camp**
- + ShelterFeature (quality ≥ 0.5)
- Benefits: Better rest efficiency, weather protection, slower tension decay

**Tier 3 - Fortified Camp**
- + WorkbenchFeature + Improved Cache
- Benefits: Craft advanced items, predator deterrence, efficient processing

**Tier 4 - Stronghold**
- + DefensiveStructureFeature + Upgraded shelter
- Benefits: Weather severe storms, safe food storage, multiple workstations

### New Camp Features

**WorkbenchFeature**
- Quality (0-1) affects craft speed and recipe access
- Type (Stone/Wood/Bone) determines craftable items
- Enables: climbing gear, better tools, insulated clothing, repairs

**DefensiveStructureFeature**
- Strength (0-1) affects predator deterrence
- Type (spike barrier/stone wall/thorn hedge)
- Reduces "Stalked" tension by -0.2/tick at camp
- Prevents camp raid events
- Degrades over time, needs maintenance

**ImprovedCacheFeature** (extends CacheFeature)
- Larger capacity (100kg vs 50kg)
- Protects from predators
- Preserves food in cold climate

### Exploration Enhancement

**Location Accessibility Tiers**

Locations have implicit requirements based on properties:

- **Tier 2** (gear recommended): High TerrainHazard/ClimbRisk → Risk injury without boots/rope
- **Tier 3** (gear required): Predator territory + distance → Needs weapons + prep
- **Tier 4** (mastery required): Multiple hazards + event unlock gates

**Soft vs Hard Gates**

Soft gates (accessible but risky):
- High terrain hazard without boots → Injury event chance
- Climb risk without rope → "Dangerous Ascent" event (turn back or risk)
- Predator territory without weapon → Higher encounter boldness

Hard gates (cannot access):
- Ancient Grove → Requires discovering hidden path (event unlock)
- Glacier caves → Need ice tools to enter

### New Equipment (Crafted at Workbench)

**Climbing Gear**
- Rope (3 plant fiber + 2 sinew) → Reduces climb risk 50%
- Ice picks (2 bone + stone) → Enables ice cave access

**Protective Equipment**
- Reinforced boots (hide + bone + sinew) → Reduces terrain hazard damage
- Insulated parka (3 hides + plant fiber) → Extra insulation for extreme cold

**Advanced Tools**
- Ice tools (bone + flint) → Ice fishing, ice cave access
- Climbing stakes (bone + stone) → Safe ascent/descent

### Location-Specific Rewards

**Tier 2 locations**: Better forage, natural features (water access), special materials (Flint Seam)

**Tier 3 locations**: Unique materials (bear fat for waterproofing), strategic advantages (The Lookout reveals new locations)

**Tier 4 locations**: Trophy materials, permanent upgrades (Hot Spring temp bonus), story revelations

### Location-Specific Events

New event registries for special locations:

**BearCave.cs**
- "The Den is Empty" (safe window)
- "Fresh Tracks" (abort or confront)
- "Hibernation Store" (huge food cache, risk waking bear)
- "Bear Returns" (escalating → encounter)

**AncientGrove.cs**
- "Hidden Path Revealed" (discovery unlock)
- "Medicinal Bounty" (healing resources)
- "Sacred Ground" (tension + spiritual element)
- "Old Bones" (narrative discovery)

**TheLookout.cs**
- "Precarious Climb" (skill check, rope reduces risk)
- "Breathtaking View" (reveals 3-5 locations)
- "Falling Ice" (environmental hazard)
- "Signal Fire" (affects tensions)

## Example Scenarios

**Scenario 1: Camp Location Choice (Day 3)**
Player's Forest Camp is depleting. Discovered Sheltered Valley (great insulation) and Rocky Overlook (near Flint Seam).

Decision: Rocky Overlook gives flint access for better tools. Move there temporarily, use materials to upgrade to T2, then relocate to Sheltered Valley for endgame T4 camp.

**Scenario 2: Gear Gate (Day 10)**
Player finds The Lookout (vantage point) with ClimbRisk 0.7.

Without rope: "Precarious Climb" event → Choice: Risk (50% injury) or Turn back
With rope: "Technical Climb" → Success, reveals 3-5 new locations, +confidence buff

Teaches: Preparation pays off. Rope worth crafting.

**Scenario 3: Endgame Progression (Day 25)**
Player has T3 camp at Sheltered Valley. Wants Ancient Grove medicinal plants for mountain crossing.

1. Event at Rocky Ridge discovers path to Ancient Grove
2. Grove has predators + distance → Craft better weapon + insulated clothing at workbench
3. "Trespass Warning" → Creates "Sacred Ground" tension
4. Navigate tension through respectful choices
5. Unlock "Medicinal Bounty" harvest
6. Return to T3 camp, craft advanced healing
7. Now prepared for mountain crossing

Player feels: Earned through progression, gear-crafting, smart choices.
