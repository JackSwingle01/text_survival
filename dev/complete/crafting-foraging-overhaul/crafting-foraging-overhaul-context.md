# Crafting & Foraging Overhaul - Context & Decisions

**Last Updated: 2025-11-01**

## SESSION PROGRESS (2025-11-01)

### ✅ COMPLETED
- Strategic planning completed
- Three dev docs files created (plan, context, tasks)
- Design philosophy documented from brainstorming session
- Material system designed (6 new properties, 15+ new items)
- Recipe structure planned (~40 recipes across 8 categories)
- 10 implementation phases defined with 49 detailed tasks
- **Plan review completed** - plan-reviewer agent identified and addressed critical issues:
  - Fire-making success/failure mechanism designed (skill check system)
  - Fire result type clarified (LocationFeature)
  - Obsidian durability deferred to post-MVP
  - Missing considerations added (harvesting yields, recipe organization, skill progression, storage, biome testing)
  - Timeline extended to 4-5 weeks for thorough testing

### 🟡 IN PROGRESS
- **Nothing yet** - plan approved, ready to begin Phase 1

### ⚠️ BLOCKERS
- None

### 📋 NEXT STEPS
1. Begin Phase 1, Task 1: Update starting inventory (Program.cs)
2. Continue through Phase 1 cleanup tasks
3. Implement fire-making skill check system in Phase 3

---

## Purpose
This document captures key context, architectural decisions, and important files for the crafting and foraging system overhaul. Use this as a reference when implementing to maintain consistency.

---

## Design Philosophy

### Core Principles (from brainstorming session)

1. **Realism First**
   - "I'm more interested in thinking about what would logically be available than the game progression"
   - When in doubt, choose realistic over game-balanced
   - Example: Fire-making should be difficult with just grass and sticks but not impossible

2. **Material-Driven Progression**
   - Progression comes from access to better materials, not arbitrary tiers
   - Stone → Flint → Bone → Obsidian is natural progression
   - Each material unlocks new capabilities, not just stat increases

3. **Ice Age Authenticity**
   - "You know as good as me what kinds of items would make sense in the stone age setting"
   - All items, recipes, and mechanics must fit the Ice Age theme
   - No anachronistic or fantasy elements (except shamanistic magic system, handled separately)

4. **Foraging vs Crafting Separation**
   - **Forage**: Only natural, unprocessed materials
   - **Craft**: Man-made items require recipes
   - "You shouldn't be able to forage for manmade items"

5. **Critical Path**
   - Fire should be achievable day 1 (but difficult)
   - Basic shelter in 1-3 days
   - Starting with almost nothing, very weak stuff

---

## Architectural Decisions

### Decision 1: Property-Based Crafting
**Context**: Game uses `ItemProperty` enum for material types instead of specific items
**Decision**: Continue property-based approach, extend with new properties
**Rationale**: Flexible, allows items to serve multiple purposes, already implemented
**Impact**: New materials need ItemProperty additions

### Decision 2: Fire-Making as Recipes with Skill Checks
**Context**: Fire is critical but not implemented in crafting. CraftingSystem always succeeds if ingredients available.
**Decision**: Extend CraftingRecipe with optional BaseSuccessChance and SkillCheckDC properties
**Rationale**: Allows fire-making to be challenging and skill-based while maintaining existing recipe architecture
**Implementation**:
- Add nullable BaseSuccessChance and SkillCheckDC to CraftingRecipe
- Modify CraftingSystem.Craft() to check success before creating result
- On failure: consume materials, grant small XP (learning from failure), display message
- On success: create HeatSourceFeature, grant normal XP
- Fire recipes use .ResultingInLocationFeature() to create HeatSourceFeature directly
- Recipe progression: Hand Drill (30% base) → Bow Drill (50% base) → Flint & Steel (90% base)
**Review Note**: This was identified as Critical Issue #1 in plan review

### Decision 3: Nothing Visible Until Foraged
**Context**: Currently items spawn visible in locations
**Decision**: Hide items initially, reveal only through Forage action
**Rationale**: "Nothing should even be visible until foraging"
**Implementation**: Set itemCount = 0 in LocationFactory, items added to location during Forage

### Decision 4: ItemStack Display for Forage
**Context**: Currently says "Stick" multiple times
**Decision**: Use existing ItemStack class to group forage results
**Rationale**: "We already have a stack class so we could use that"
**Implementation**: Modify ForageFeature.Forage() to group items before display

### Decision 5: No Starting Weapon
**Context**: Currently starts with knife
**Decision**: "Lets not even start with a weapon, just the cloathes"
**Rationale**: Forces immediate crafting, increases early difficulty
**Implementation**: Remove knife from starting oldBag, add only 2 tattered clothes

### Decision 6: Tiered But Not Rigid
**Context**: Discussion of 3 tiers vs realistic progression
**Decision**: "I know that's more than three tiers, but again I'm more interested in thinking about what would logically be available"
**Rationale**: Realism > artificial tier limits
**Example**: Spear progression is 5+ tiers (stick → sharpened → fire-hardened → flint → bone → obsidian)

### Decision 7: Recipe Categorization for Menu Organization
**Context**: 40+ recipes will make crafting menu overwhelming
**Decision**: Add Category enum to CraftingRecipe (Fire, Tools, Weapons, Shelters, Clothing, Misc)
**Rationale**: Improves discoverability and navigation
**Implementation**:
- Group recipes by category with headers in menu
- Sort unavailable recipes to bottom within each category
- Implemented in Phase 8, Task 39b
**Review Note**: Added from plan review recommendations

### Decision 8: Harvesting Yield Multipliers
**Context**: Tool tiers should affect harvesting effectiveness
**Decision**: Add HarvestMultiplier property to cutting tools
**Rationale**: Rewards tool progression beyond just combat damage
**Implementation**:
- Sharp Rock: 1.0x (base)
- Flint Knife: 1.3x
- Bone Knife: 1.5x
- Obsidian Blade: 1.8x
- Modify ForageFeature.Forage() to check equipped weapon and multiply yields
**Review Note**: Added from plan review missing considerations

### Decision 9: Obsidian Without Durability
**Context**: Plan mentioned obsidian as "fragile" but no durability system exists
**Decision**: For MVP, obsidian is simply rare and powerful (no durability mechanics)
**Rationale**: Durability system is significant additional scope
**Future Enhancement**: Durability can be added post-MVP
**Review Note**: Critical Issue #3 from plan review

### Decision 10: All Biomes Must Be Viable
**Context**: Material redistribution could make some biomes unplayable
**Decision**: Each biome must have materials for day-1 fire + shelter
**Rationale**: All starting locations should be survivable
**Implementation**: Add biome viability testing in Phase 9, Task 45
**Review Note**: Added as High Risk mitigation from plan review

---

## Key Files & Their Roles

### Core Systems

**`ForageFeature.cs`** (`Environments/LocationFeatures.cs/`)
- Handles time-based foraging with diminishing returns
- Contains resource abundance table per location
- **Current**: AddResource(factory, abundance) pattern
- **Changes Needed**:
  - Items revealed and added during Forage, not pre-spawned
  - ItemStack grouping for display
  - Remove crafted items from resource tables

**`CraftingSystem.cs`** (`Crafting/`)
- Manages all recipes via RecipeBuilder
- Has InitializeRecipes() method that calls Create*Recipes()
- **Current**: Only 4 placeholder recipes
- **Changes Needed**:
  - Add ~40 new recipes across all categories
  - Add skill check logic for fire-making
  - Create new category methods (CreateFireRecipes, CreateToolRecipes, etc.)

**`RecipeBuilder.cs`** (`Crafting/`)
- Fluent API for defining recipes
- **Key Methods**:
  - `.WithPropertyRequirement(property, minQty, isConsumed)`
  - `.RequiringSkill(skillName, level)`
  - `.RequiringCraftingTime(minutes)`
  - `.RequiringFire(bool)`
  - `.ResultingInItem(factory)` / `.ResultingInLocationFeature()` / `.ResultingInStructure()`
- **No Changes Needed**: System is sufficient as-is

**`ItemFactory.cs`** (`Items/`)
- Static factory methods for all items
- Each method returns configured item instance
- **Current**: ~40 items, many with CraftingProperties
- **Changes Needed**:
  - Add ~15 new material items (tinder, plant fibers, etc.)
  - Add progression items (sharp rock, fire-hardened spear, etc.)
  - Remove from world spawn tables (done in LocationFactory)

**`ItemProperty.cs`** (`Crafting/`)
- Enum of all material properties
- **Current**: 18 properties (Stone, Wood, Binding, Flammable, etc.)
- **Changes Needed**: Add Tinder, Fur, Antler, Leather, Charcoal, PlantFiber

**`LocationFactory.cs`** (`Environments/`)
- Creates locations with appropriate ForageFeatures and spawned items
- Each biome has:
  - ForageFeature with AddResource() calls
  - GetRandom*Item() method for initial spawns
- **Current**: Spawns crafted items (spears, torches, armor)
- **Changes Needed**:
  - Remove crafted items from GetRandom*Item()
  - Reduce/eliminate initial itemCount
  - Add new natural materials to forage tables

**`Program.cs`** (root)
- Game entry point, sets up starting conditions
- **Current**: Lines 26-32 create oldBag with knife + leather armor
- **Changes Needed**: Replace with 2 tattered clothing items

### Supporting Systems

**`Action.cs` / `ActionFactory.cs`** (`Actions/`)
- Forage action currently at ActionFactory.Survival.Forage()
- **Current**: Calls ForageFeature.Forage(hours), displays results
- **Changes Needed**: Update display to use ItemStack grouping

**`Player.cs`** (root)
- Has InventoryManager, Skills, and Item handling
- **No Changes Needed**: Existing item/crafting integration sufficient

**`CraftingRecipe.cs`** (`Crafting/`)
- Recipe data structure used by RecipeBuilder
- **No Changes Needed**: Structure supports all planned recipes

---

## Material → Recipe Mapping

This ensures every material has purpose in the crafting system.

### Natural Materials (Forageable)

| Material | ItemProperties | Primary Use | Secondary Uses |
|----------|---------------|-------------|----------------|
| Stick | Wood, Flammable | Spears, shelters | Fire fuel |
| Dry Grass | Tinder, Flammable | Fire starting | - |
| Bark Strips | Binding, Tinder | Cordage, tinder | Bandages |
| Plant Fibers | PlantFiber, Binding | Cordage | Clothing |
| River Stone | Stone | Basic tools | Fire rings |
| Flint | Stone, Sharp, Firestarter | Advanced tools | Fire starting |
| Obsidian | Stone, Sharp | Elite tools | - |
| Clay | Clay | Vessels | - |
| Berries | - | Food | - |
| Mushrooms | - | Food/medicine | - |
| Water | - | Hydration | - |
| Roots | - | Food | - |

### Hunting-Derived Materials

| Material | ItemProperties | Primary Use | Secondary Uses |
|----------|---------------|-------------|----------------|
| Hide | Hide | Armor, binding | Containers |
| Fur | Fur, Insulation | Cold-weather gear | Bedding |
| Bone | Bone, Sharp | Advanced tools/weapons | Needles |
| Sinew | Binding | Bowstrings, cordage | Sewing |
| Antler | Antler, Sharp | Tool handles | Punches |
| Meat | RawMeat, Fat | Food | - |

### Processed Materials

| Material | ItemProperties | How Obtained | Uses |
|----------|---------------|--------------|------|
| Charcoal | Charcoal, Flammable | Burn wood in fire | Fire-hardening |
| Leather | Leather | Tan hide | Better armor |
| Cooked Meat | CookedMeat | Cook raw meat | Better food |

---

## Recipe Categories

### Fire-Making (Critical Path)
- Hand Drill (challenging, day 1)
- Bow Drill (easier, needs cordage)
- Flint & Steel (easy, needs flint)
- Campfire (feature creation)

### Cutting Tools (Critical Path)
- Sharp Rock (immediate)
- Flint Knife (early-mid)
- Bone Knife (mid)
- Obsidian Blade (rare/optional)

### Weapons - Spears (Primary Path)
- Sharpened Stick
- Fire-Hardened Spear
- Flint-Tipped Spear
- Bone-Tipped Spear
- Obsidian Spear

### Weapons - Clubs (Alternative Path)
- Heavy Stick (found)
- Stone-Weighted Club
- Bone-Studded Club

### Shelters (Critical Path)
- Windbreak (emergency)
- Lean-to (basic permanent)
- Debris Hut (good permanent)
- Log Cabin (excellent endgame)

### Clothing (Progressive)
- Bark Wrappings (day 1)
- Grass Foot Wraps (day 1)
- Hide Armor (hunting required)
- Fur-Lined Gear (advanced hunting)

### Utility
- Hand Axe (wood gathering)
- Bandages (healing)
- Torch (light/warmth)
- Containers (storage)

---

## Biome Material Profiles

### Forest
**Abundance**: Wood, plant fiber, tinder, berries
**Scarcity**: Stone, flint
**Unique**: Mushrooms, healing herbs
**Strategy**: Best for early shelter/fire, need to travel for tools

### Riverbank
**Abundance**: Water, clay, smooth stones, fish
**Scarcity**: Wood, tinder
**Unique**: Rushes (water-based plant fiber)
**Strategy**: Food/water rich, need wood from forest

### Cave
**Abundance**: Stone, flint, obsidian (rare)
**Scarcity**: Organic materials
**Unique**: Obsidian, ochre pigment
**Strategy**: Tool materials, but need organics elsewhere

### Plains/Tundra
**Abundance**: Roots, stone
**Scarcity**: Wood, shelter materials
**Unique**: Bone from old kills (rare)
**Strategy**: Open hunting grounds, need to import wood

### Hillside
**Abundance**: Stone, flint
**Moderate**: Roots
**Scarcity**: Wood
**Unique**: Ochre, obsidian (rare)
**Strategy**: Similar to cave, mining for materials

---

## Tool Effectiveness Progression

### Cutting Tools (Damage / Harvesting Multiplier)
- Sharp Rock: 3 dmg / 1.0x yield
- Flint Knife: 6 dmg / 1.3x yield
- Bone Knife: 8 dmg / 1.5x yield
- Obsidian Blade: 12 dmg / 1.8x yield (rare material)

### Spears (Damage / Range)
- Sharpened Stick: 4 dmg / melee
- Fire-Hardened: 6 dmg / melee
- Flint-Tipped: 10 dmg / melee+thrust
- Bone-Tipped: 12 dmg / melee+thrust
- Obsidian: 16 dmg / melee+thrust (rare material)

### Clubs (Damage / Stun Chance)
- Heavy Stick: 5 dmg / 0%
- Stone-Weighted: 9 dmg / 10%
- Bone-Studded: 11 dmg / 15%

### Shelters (Warmth Bonus / Weather Block)
- Windbreak: +2°F / 10%
- Lean-to: +5°F / 40%
- Debris Hut: +8°F / 70%
- Log Cabin: +15°F / 95%

---

## Time Estimates (Realistic)

### Fire-Making
- Hand Drill: 15-30 min (skill-dependent)
- Bow Drill: 45 min to construct + use
- Flint & Steel: ~5 min

### Basic Tools
- Sharp Rock: 5 min (smashing stones)
- Sharpened Stick: 5 min (with cutting tool)
- Flint Knife: 20 min (knapping + hafting)
- Bone Knife: 45 min (shaping + hardening + hafting)

### Weapons
- Fire-Hardened Spear: 20 min (includes fire time)
- Flint-Tipped Spear: 40 min (point + hafting)
- Bone-Tipped Spear: 60 min (shaping + hafting)

### Shelters
- Windbreak: 30 min
- Lean-to: 2 hours
- Debris Hut: 4 hours
- Log Cabin: 8 hours (simplified, realistically days)

---

## Integration Points

### With Existing Systems

**Combat System**:
- New weapons must work with existing combat damage calculations
- CombatManager.Attack() uses Weapon.Damage

**Survival System**:
- Shelter warmth bonuses affect SurvivalContext.LocationTemperature
- Clothing insulation affects SurvivalContext.ClothingInsulation

**Skill System**:
- Fire-making uses Firecraft skill
- Tool crafting uses Crafting skill
- Success checks via SkillRegistry

**Effect System**:
- Some consumables (bandages) create effects
- EffectBuilder integration already exists

**Location System**:
- Shelters create new Location objects as sub-locations
- Features (HeatSourceFeature, ShelterFeature) attach to locations

---

## Common Pitfalls to Avoid

1. **Don't make progression too linear**: Provide alternatives (clubs vs spears, different shelter paths)

2. **Don't orphan materials**: Every ItemProperty must be used in at least one recipe

3. **Don't break the theme**: All items must fit Ice Age setting (no metal, gunpowder, etc.)

4. **Don't make day-1 impossible**: Must be able to get fire (hard) + basic shelter + sharp rock

5. **Don't let crafted items spawn**: Only natural materials in LocationFactory spawn tables

6. **Don't forget time costs**: Fire-making takes time, building shelter takes time, crafting takes time

7. **Don't make stats arbitrary**: Damage/warmth values should follow material logic

---

## Testing Checklist

- [ ] New game starts with only tattered clothes
- [ ] Locations appear empty until foraged
- [ ] Forage reveals natural materials only (no crafted items)
- [ ] Can make fire on day 1 (with reasonable attempts)
- [ ] Can build windbreak on day 1
- [ ] Can craft sharp rock on day 1
- [ ] Material progression feels natural
- [ ] Each tool tier is noticeably better
- [ ] Recipe times are realistic
- [ ] All ItemProperties used in recipes
- [ ] No crafted items in world spawns
- [ ] ItemStack display works correctly
- [ ] Shelter warmth bonuses affect survival
- [ ] Full progression playthrough (start → cabin) works

---

## Questions & Answers (from brainstorming)

**Q**: How many tiers per item type?
**A**: As many as are realistic, not constrained to exactly 3. Example: spears have 5+ tiers.

**Q**: Should items be objectively better per tier?
**A**: Not just stats - "they don't have to be objectively better, everything should strive for some degree of realism". Each tier unlocks new capabilities.

**Q**: What about the display issue with duplicate items?
**A**: Use ItemStack class. Also abstract items until foraging ("nothing visible until foraging").

**Q**: How difficult should fire-making be?
**A**: Based on realism - "difficult to start a fire with just grass and sticks but not impossible, and better chance depending on what you have available"

**Q**: Can we forage for crafted items?
**A**: No. "You shouldn't be able to forage for manmade items."

---

## Future Considerations (Out of Scope)

- Hunting system (noted as todo, not part of this overhaul)
- Detailed harvesting yields (basic implementation sufficient)
- Tool durability/degradation (nice-to-have)
- Advanced tanning/processing mechanics (basic is fine)
- Cooking recipes beyond basic meat (future content)
- Seasonal variations in foraging (future)

---

## References

- Main Plan: `crafting-foraging-overhaul-plan.md`
- Task Tracking: `crafting-foraging-overhaul-tasks.md`
- Code Reference: `CLAUDE.md` (repository root)
- Ideas backlog: `ideas.txt` (repository root)

---

## Quick Resume

**To continue this task after a context reset:**

1. **Read this file first** - Check SESSION PROGRESS section at top
2. **Review tasks.md** - See what's completed and what's next
3. **Refer to plan.md** - Understand overall strategy if needed

**Current Status**: Planning complete and reviewed. Plan approved for implementation. Ready to begin Phase 1.

**Plan Review Results**:
- Critical issues addressed (fire-making skill checks, fire result type, obsidian durability)
- Timeline extended to 4-5 weeks for thorough testing
- 49 tasks across 10 phases

**Next Action**:
- Start Phase 1, Task 1: Update starting inventory in `Program.cs`
- Remove oldBag with knife/armor
- Add 2 tattered clothing items (low warmth/protection)

**Key Files to Modify First**:
- `Program.cs` (lines 26-32) - starting conditions
- `LocationFactory.cs` - clean spawn tables
- `ItemFactory.cs` - add new material items

**Important Implementation Notes**:
- Fire-making requires extending CraftingRecipe with BaseSuccessChance/SkillCheckDC properties (Phase 3)
- Recipe categorization needed for menu organization (Phase 8, Task 39b)
- Biome viability testing critical (Phase 9, Task 45)
