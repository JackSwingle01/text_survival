# Game Balance Implementation - Context & Key Decisions

**Last Updated**: 2025-11-02
**Related Ticket**: `dev/active/game-balance/balance-issues-ticket.md`
**Related Playtest**: `dev/complete/crafting-foraging-overhaul/task-49-full-progression-playtest.md`

---

## Design Decisions Summary

### Decision 1: Separate Tool Crafting from Fire Starting
**Question**: How should fire-making RNG work?
**Answer**: Remove RNG from tool crafting, apply it to tool usage
**Rationale**:
- Material waste on crafting failures creates unfun death spirals
- Players should be rewarded for gathering materials (tool creation guaranteed)
- RNG tension maintained in fire starting (realistic - friction fire is hard)
- Tool durability adds strategic depth without punishment

**Alternative Rejected**: "Guaranteed slow method" (30min but 100% success)
- Would eliminate tension entirely
- Doesn't teach skill progression
- Less realistic (Ice Age fire-making was hard, not just slow)

---

### Decision 2: Resource Respawn Over Time
**Question**: Should resources respawn, and if so, how fast?
**Answer**: Slow respawn over 1-3 in-game days
**Rationale**:
- Prevents permanent dead-end states
- Encourages multi-location management (can't camp one spot)
- Maintains scarcity pressure (can't wait out problems)
- 48-hour respawn = 2 in-game days = strategic time scale

**Alternative Rejected**: No respawn (permanent depletion)
- Too punishing if player wastes materials learning
- Forces restart instead of strategic recovery
- Conflicts with "competent play succeeds" philosophy

---

### Decision 3: Starting Location 1.5-2x Resource Density
**Question**: Should starting area be tutorial-generous or harshly consistent?
**Answer**: Moderately generous (1.75x normal density)
**Rationale**:
- Breathing room for learning systems
- Still requires good decisions (not a safety net)
- Supports 5-8 fire attempts (accounting for failures)
- Gradual difficulty increase (start → exploration)

**Alternative Rejected**: Same as other locations
- Too harsh for first-time players
- Contradicts "competent play succeeds" (luck becomes factor)
- Eliminates tutorial space for learning mechanics

---

### Decision 4: Hybrid Food Spawning (Visible + Forageable)
**Question**: Should early food be RNG-based or guaranteed?
**Answer**: Both - visible berry bushes + forageable items
**Rationale**:
- Visible items provide reliability (strategic planning possible)
- Forageable items provide discovery (reward exploration)
- Balances player agency with emergent gameplay
- Realistic (berries are visible, grubs are hidden)

**Alternative Rejected**: All forageable (pure RNG)
- Too much RNG variance early game
- Players can't strategize (everything is luck)
- Conflicts with reducing RNG frustration goal

---

### Decision 5: Improve Gathered Food, Not Consumption Rate
**Question**: Should we slow food consumption or improve gathered food?
**Answer**: Improve gathered food (1% → 10%), keep consumption at 14%/hour
**Rationale**:
- Time pressure maintained (core survival mechanic)
- Gathering becomes viable strategy (not useless)
- Still incentivizes hunting (meat is 3-4x better)
- Doesn't change core survival processing

**Alternative Rejected**: Slower consumption (14% → 8%/hour)
- Removes urgency (too forgiving)
- Breaks other tuned systems (body simulation)
- Doesn't solve "gathered food useless" problem

---

### Decision 6: Living Document Approach
**Question**: Should spec be directive or iterative?
**Answer**: Living document - implement, test, revise
**Rationale**:
- Balance is inherently iterative (playtesting required)
- Numbers are starting points (5 tool uses, 48h respawn, etc.)
- Allows course correction without "failed spec" feeling
- Encourages experimentation

---

## Key Files & Line Numbers

### Fire-Making System
- **CraftingSystem.cs lines 165-221**: Current fire recipes (Hand Drill, Bow Drill, Flint & Steel)
  - Problem: `BaseSuccessChance` applies to entire recipe
  - Line 39: `recipe.ConsumeIngredients(_player)` - happens before skill check
  - Lines 42-66: Skill check system (to be moved to action)
- **CraftingRecipe.cs lines 29-31**: `BaseSuccessChance` and `SkillCheckDC` properties
- **ActionFactory.cs**: Where new StartFire action will be added (Crafting section)

### Resource System
- **ForageFeature.cs lines 6-72**: Full foraging implementation
  - Line 11: `ResourceDensity = baseResourceDensity / (numberOfHoursForaged + 1)` - depletion formula
  - Line 9: `numberOfHoursForaged` - tracks depletion (no respawn currently)
  - Lines 13-36: `Forage(hours)` method - where respawn logic will be added
- **LocationFactory.cs**: Where starting location is created (need to find exact line)

### Food System
- **ItemFactory.cs lines 15-66**: Food item factories
  - Line 17: `MakeMushroom()` - currently 25 calories (change to 120)
  - Line 48: `MakeBerry()` - already 120 calories (correct)
  - Line 58: `MakeRoots()` - already 100 calories (correct)
  - Need to add: `MakeNuts()`, `MakeEggs()`, `MakeGrubs()`
- **Player.cs** or **World.cs**: Player initialization (starting food 50% → 75%)
- **SurvivalProcessor.cs**: Food consumption rate (14%/hour) - NO CHANGES NEEDED

### Recipe Builder
- **RecipeBuilder.cs**: Fluent builder for recipes
  - `.WithSuccessChance(double)` - sets BaseSuccessChance
  - `.WithSkillCheck(string, int)` - sets skill and DC
  - `.ResultingInItem(Func<Item>)` - for tool recipes
  - `.ResultingInLocationFeature(...)` - for old fire recipes (removing)

---

## Dependencies Between Changes

### Critical Path
1. **FireMakingTool class MUST exist** before tool recipes can be created
2. **Tool recipes MUST exist** before StartFire action can reference them
3. **StartFire action MUST exist** before removing old fire recipes
4. **Old fire recipes MUST be removed** before system is coherent

**Implementation Order**: 1.1 → 1.2 → 1.3 → 1.4

### Independent Changes
- Resource respawn (Phase 2) is independent of fire changes (Phase 1)
- Food improvements (Phase 3) are independent of both
- Can implement in any order after Phase 1 complete

### Testing Dependencies
- Phase 1 must be validated before Phase 2/3 (fire is foundation)
- Phase 2 affects Phase 3 metrics (more resources = more foraging)
- Full validation requires all phases complete

---

## Reference Documents

### Project Documentation
- **documentation/fire-management-system.md**: Ember system, fuel tracking, heat sources
- **documentation/crafting-system.md**: Property-based crafting, recipe structure
- **documentation/skill-check-system.md**: 30% base + 10%/level formula, 5-95% clamp
- **documentation/survival-processing.md**: Food/water consumption, temperature regulation
- **CLAUDE.md**: Design philosophy, architectural patterns

### Development Status
- **dev/active/CURRENT-STATUS.md**: Must be updated after implementation
- **ISSUES.md**: Track new issues discovered during implementation
- **dev/complete/crafting-foraging-overhaul/**: Reference for what led to this ticket

### Industry Research (Light)
- **The Long Dark**: Fire-starting mechanics, resource scarcity
  - Fire requires tinder + accelerant (matches/lighter/magnifying glass)
  - Failure consumes time, not materials (matches are exception)
  - Tool condition affects success rate
- **RimWorld**: Skill-based success rates, failure consequences
  - Skill checks: Base chance + skill modifier
  - Failures grant XP (learning from mistakes)
  - Crafting failures can result in partial materials recovery
- **Don't Starve**: Early-game food sources, hunger mechanics
  - Berries are visible and renewable (bushes regrow)
  - Early game focuses on gathering → mid-game on farming/hunting
  - Hunger rate creates urgency without being unforgiving

---

## Existing Game Constants

### Skill System
- **Base Success Rate**: 30-50% (fire-making specific)
- **Skill Modifier**: +10% per skill level above DC
- **Success Range**: Clamped to 5-95% (never impossible, never guaranteed)
- **XP on Success**: Varies by recipe (fire = 2-3 XP)
- **XP on Failure**: 1 XP (learning from mistakes)

### Time System
- **Action Time Units**: Minutes (int)
- **World.Update(minutes)**: Advances global clock
- **Crafting Times**: 5 min (simple) to 480 min (8 hours, log cabin)
- **Foraging Intervals**: 15 min standard (0.25 hours)

### Resource System
- **Depletion Formula**: `density / (hours + 1)`
- **Property Quantities**: Weight-based (kg)
- **Item Properties**: Enum-based (ItemProperty.Wood, .Tinder, etc.)
- **Stacking**: Items group by type in inventory

### Survival System
- **Food Consumption**: 14% per in-game hour
- **Calorie to %**: ~1200 calories = 100% food
- **Temperature Impact**: Affects cold resistance, heat stress
- **Body Composition**: Fat/muscle ratios affect survival

---

## Testing Considerations

### Manual Playtest Protocol
1. **Start new game** (fresh character, starting location)
2. **Forage for materials** (track attempts, items found)
3. **Craft fire-making tool** (verify 100% success)
4. **Attempt fire starting** (track attempts, tool durability, material consumption)
5. **Forage for food** (track food items found, calorie restoration)
6. **Monitor survival stats** (food %, body temp, time elapsed)
7. **Continue to first hunt** (craft spear, hunt, track timeline)

### Metrics to Track
- Time to first fire (minutes)
- Fire attempts before success (count)
- Materials consumed for fire (wood, tinder)
- Forages needed for fire materials (count)
- Food level at fire establishment (%)
- Food level at first hunt (%)
- Total time to first hunt (hours)
- Survival outcome (alive/dead, cause if dead)

### Success Criteria
- ≥4/5 playtests establish fire
- ≥3/5 playtests reach hunting
- ≥3/5 playtests survive to 6+ hours
- No death spirals (material exhaustion → unwinnable)
- No new unwinnable states discovered

---

## Known Edge Cases

### Edge Case 1: Tool Breaks on Success Attempt
**Scenario**: Tool has 1 use left, fire attempt succeeds
**Resolution**: Tool destroyed on success (as intended)
**Code Location**: StartFire action, success branch

### Edge Case 2: Multiple Fire-Making Tools in Inventory
**Scenario**: Player has both Hand Drill and Bow Drill
**Resolution**: Action should let player choose tool (or use best available)
**Implementation**: May need tool selection submenu

### Edge Case 3: Respawn During Active Forage
**Scenario**: Player forages while resources are respawning
**Resolution**: Respawn calculated at moment of forage (stale resources)
**Code Location**: ForageFeature.Forage() line 20 (recalculates density each call)

### Edge Case 4: Fire Started But No Kindling Available
**Scenario**: Player has tool + tinder but no kindling (0.3 wood)
**Resolution**: Action .When() condition should check for kindling
**Code Location**: StartFire action, availability check

### Edge Case 5: Starting Location Already Has Campfire
**Scenario**: Player dies, restarts, location has old fire
**Resolution**: Not an issue (fire recipes fixed to reuse existing)
**Code Location**: Phase 1.4 fix handles this

---

## Configuration Values (Tunable)

### Fire-Making Tools
```csharp
// These are starting values - adjust based on playtesting
HandDrillTool: {
    MaxUses: 5,
    BaseSuccessChance: 0.3,
    RequiredSkill: "Firecraft",
    SkillCheckDC: 0
}

BowDrillTool: {
    MaxUses: 8,
    BaseSuccessChance: 0.5,
    RequiredSkill: "Firecraft",
    SkillCheckDC: 1
}

FlintSteelTool: {
    MaxUses: 15,
    BaseSuccessChance: 0.9,
    RequiredSkill: "Firecraft",
    SkillCheckDC: 0  // No skill requirement
}
```

### Resource Respawn
```csharp
// ForageFeature configuration
RespawnRateHours: 48  // 2 days to full respawn
StartingLocationDensity: 1.75  // 1.75x normal
NormalLocationDensity: 1.0  // Standard
```

### Food System
```csharp
// Player initialization
StartingFood: 0.75  // 75% (was 0.5)

// Food item calorie values
WildMushroom: 120  // Was 25
WildBerries: 120
Nuts: 100
Eggs: 150
Grubs: 80
Roots: 100  // Unchanged

// Food consumption (no change)
ConsumptionRate: 0.14  // 14% per hour
```

---

## Rollback Scenarios

### Scenario 1: Fire System Too Complex
**Symptoms**: Players confused, UX complaints, prefer old system
**Rollback**: Revert Phase 1, increase old recipe success rates instead
**Alternative Fix**: Better tutorial text, clearer messaging

### Scenario 2: Game Becomes Too Easy
**Symptoms**: >90% survival rate, no tension, trivialized difficulty
**Rollback**: Revert Phase 3 (food changes), keep Phase 1-2
**Alternative Fix**: Reduce food values (10% → 5%), reduce starting food (75% → 65%)

### Scenario 3: Respawn Too Fast/Slow
**Symptoms**: Players camping one location / still hitting dead-ends
**Rollback**: Not needed, just tune `RespawnRateHours` value
**Alternative Fix**: 24h (too fast) → 48h (baseline) → 72h (too slow)

### Scenario 4: Tool Durability Imbalanced
**Symptoms**: Tools break too fast / never break
**Rollback**: Not needed, just tune `MaxUses` values
**Alternative Fix**: Adjust 5/8/15 uses based on feedback

---

## Future Enhancement Hooks

### Post-Implementation Additions
1. **Tool Repair System**: Allow repairing fire-making tools with materials
2. **Quality System**: Higher skill = better quality tools (more uses)
3. **Weather Effects**: Rain affects fire starting success rate
4. **Biome Variations**: Different respawn rates per biome
5. **Food Spoilage**: Gathered food degrades over time
6. **Cooking Bonuses**: Cooked gathered food provides more calories
7. **Tool Specialization**: Different tools better in different conditions

### Extensibility Points
- `FireMakingTool` class can be extended for tool variations
- `ForageFeature.RespawnRateHours` can be per-location
- `ItemFactory.MakeX()` methods easily added for new foods
- `StartFire` action can be modified for weather/location effects

---

**End of Context Document**
