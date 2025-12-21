# Gameplay Fixes Sprint - Technical Context

## Key Technical Decisions

### 1. Survival Stat Damage Implementation
**Decision**: Apply damage directly in `Body.Update()` rather than through effects system
**Rationale**: 
- Simpler implementation
- Avoids complexity of adding damage to effects
- Direct damage is more predictable for testing
- Follows existing pattern (Body.Damage is the single entry point)

**Alternative Considered**: Create Starvation/Dehydration effects with DamagePerHour
- Rejected: Adds unnecessary complexity to effect system
- Effects are for buffs/debuffs, not core survival mechanics

### 2. Message Spam Solution
**Decision**: Suppress messages during sleep, show summary after
**Rationale**:
- Clean user experience
- Preserves performance (no message batching logic)
- Matches player expectation (sleep should be fast)

**Alternative Considered**: Batch duplicate messages (e.g., "You are cold (x100)")
- Rejected: Still generates thousands of message objects
- Still confusing to see "(occurred 269914 times)"

### 3. Death System Placement
**Decision**: Check `player.IsAlive` in main game loop (Program.cs)
**Rationale**:
- Single point of control
- Can't take actions while dead
- Clear death screen before exit

**Alternative Considered**: Throw exception on death, catch in Program.cs
- Rejected: Exceptions for control flow is anti-pattern
- Harder to show graceful death screen

### 4. Auto-Equip Behavior
**Decision**: Add confirmation prompt, keep auto-equip for new items
**Rationale**:
- Balances convenience vs player agency
- Makes sense for freshly crafted gear
- Config option allows turning off entirely

**Alternative Considered**: Never auto-equip
- Rejected: Adds tedium (must equip every item manually)
- Most players want to equip what they craft

### 5. Foraging Message Order
**Decision**: Remove output from ForageFeature.Forage(), move to action
**Rationale**:
- Action controls UI flow, Feature handles logic (separation of concerns)
- Allows action to show collection menu after message
- Consistent with other feature patterns

**Alternative Considered**: Have Feature output messages, action shows menu
- Rejected: Creates race condition (message appears before menu ready)

---

## Architecture Patterns Used

### 1. Single Damage Entry Point
**Pattern**: `Body.Damage(DamageInfo)` is the ONLY way to apply damage
**Files**: Bodies/Body.cs, Survival/SurvivalProcessor.cs
**Why**: Ensures all damage goes through validation, logging, body part targeting
**Applied to**: Issues 1.2, 1.3

### 2. Pure Function Survival Processing
**Pattern**: `SurvivalProcessor.Process()` is stateless, returns results
**Files**: Survival/SurvivalProcessor.cs
**Why**: Testable, predictable, no side effects
**Applied to**: Issues 1.2, 2.1
**Note**: Considered adding `suppressMessages` parameter but decided to handle in Body.Rest() instead to keep SurvivalProcessor pure

### 3. Action Builder Pattern
**Pattern**: Fluent builder creates menu actions with `.Do()`, `.ThenShow()`, `.When()`
**Files**: Actions/ActionFactory.cs, Actions/ActionBuilder.cs
**Why**: Declarative, composable, testable
**Applied to**: Issues 2.3, 2.4, 3.3

### 4. Feature Composition
**Pattern**: Location has Features (ForageFeature, HeatSourceFeature, HarvestableFeature)
**Files**: Environments/LocationFeatures.cs/*
**Why**: Modular, reusable, data-driven
**Applied to**: Issue 2.4 (HarvestableFeature)

---

## Code Hotspots (Files That Need Changes)

### High Impact (Many Changes)
1. **Actions/ActionFactory.cs** - 8 issues affect this file
   - Fire-making skill name (1.1)
   - Duplicate menu items (2.2)
   - Sleep bounds (2.3)
   - Harvest action (2.4)
   - Foraging message (2.6)
   - Equipment screen (3.3b)

2. **Survival/SurvivalProcessor.cs** - 3 issues
   - Starvation/dehydration damage (1.2)
   - Temperature damage (1.3)
   - Message spam (2.1)

3. **Bodies/Body.cs** - 3 issues
   - Apply survival damage (1.2)
   - Sleep batching (2.1)
   - Performance optimization (3.2)

### Medium Impact
1. **Effects/EffectBuilder.cs** - 1 issue
   - Temperature damage integration (1.3)

2. **Effects/EffectRegistry.cs** - 1 issue
   - Apply damage from effects (1.3)

3. **Player.cs** - 1 issue
   - Auto-equip prompt (3.1)

4. **Program.cs** - 1 issue
   - Death check in main loop (1.4)

### Low Impact
1. **IO/Input.cs** - 1 issue
   - Validation consistency (4.1)

2. **Config.cs** - 1 issue
   - Auto-equip config (3.1)

---

## Data Flow Analysis

### Issue 1.2: Starvation/Dehydration Damage
```
World.Update(minutes)
  ↓
Player.Update()
  ↓
Body.Update(timeSpan, context)
  ↓
SurvivalProcessor.Process(data, minutes, effects)
  ↓
Returns SurvivalProcessorResult
  ↓
Body.UpdateBodyBasedOnResult(result)
  ↓
[NEW] If Calories <= 0 or Hydration <= 0:
  Body.Damage(DamageInfo)
  ↓
  DamageProcessor.DamageBody(damageInfo, body)
```

### Issue 1.3: Temperature Damage
```
SurvivalProcessor.Process()
  ↓
GenerateColdEffects() creates Hypothermia/Frostbite effects
  ↓
Returns result.Effects
  ↓
Body.UpdateBodyBasedOnResult() adds effects to EffectRegistry
  ↓
EffectRegistry.Update() [called every minute]
  ↓
[NEW] If effect.DamagePerHour > 0:
  Body.Damage(DamageInfo)
```

### Issue 2.1: Message Spam
**Current Flow**:
```
Body.Rest(minutes) [30000 hours = 1,800,000 minutes]
  ↓
For each minute:
  SurvivalProcessor.Sleep(data, 1) [1.8M calls]
    ↓
    GenerateColdEffects() [1.8M calls]
      ↓
      5% chance of "still feeling cold" [~90K messages]
  ↓
  UpdateBodyBasedOnResult() outputs each message
```

**Fixed Flow**:
```
Body.Rest(minutes)
  ↓
SurvivalProcessor.Sleep(data, minutes) [1 call, processes all at once]
  ↓
GenerateColdEffects() NOT called (suppressMessages = true)
  ↓
UpdateBodyBasedOnResult() does NOT output messages
  ↓
Body.Rest() outputs summary after completion
```

---

## Critical Code Locations

### Skill Registry
- **File**: Level/SkillRegistry.cs
- **Issue**: 1.1 (Fire-making skill)
- **Properties**:
  - Firecraft (line 14) ← Correct name
  - GetSkill() switch (line 60) ← Must use "Firecraft"

### Survival Processing
- **File**: Survival/SurvivalProcessor.cs
- **Constants**:
  - MAX_CALORIES = 2000 (line 14)
  - MAX_HYDRATION = 4000 (line 13)
  - MAX_ENERGY_MINUTES = 960 (line 11)
  - HypothermiaThreshold = 95.0°F (line 18)
  - SevereHypothermiaThreshold = 89.6°F (line 17)

### Body Stats
- **File**: Bodies/Body.cs
- **Properties**:
  - CalorieStore (line 68) ← Private, accessed via BundleSurvivalData()
  - Hydration (line 70) ← Private
  - Energy (line 69) ← Private
  - BodyTemperature (line 66) ← Public
  - Health (line 24) ← Calculated from body parts

### Death Check
- **File**: Actors/Actor.cs
- **Property**: IsAlive (line 16)
  ```csharp
  public bool IsAlive => !Body.IsDestroyed;
  ```
- **File**: Bodies/Body.cs
- **Property**: IsDestroyed (line 36)
  ```csharp
  public bool IsDestroyed => Health <= 0;
  ```

---

## Testing Strategy Details

### Unit Test Files to Create/Modify
1. **SurvivalProcessorTests.cs** (already exists)
   - Add: Test_StarvationAppliesDamage()
   - Add: Test_DehydrationAppliesDamage()
   - Add: Test_HypothermiaCreatesEffect()

2. **BodyTests.cs** (may need to create)
   - Add: Test_UpdateAppliesDamageAtZeroCalories()
   - Add: Test_UpdateAppliesDamageAtZeroHydration()
   - Add: Test_IsDestroyedWhenHealthZero()

3. **EffectRegistryTests.cs** (may need to create)
   - Add: Test_UpdateAppliesDamageFromEffects()
   - Add: Test_HypothermiaEffectAppliesDamage()

### Integration Test Scenarios
1. **Starvation Death**
   ```bash
   # Using play_game.sh
   ./play_game.sh send "4"  # Open inventory
   ./play_game.sh send "..."  # Eat all food
   ./play_game.sh send "7"  # Sleep
   ./play_game.sh send "24"  # Sleep 24 hours
   # Repeat until food = 0%, then wait for death
   # Expected: Death after ~2-3 weeks game time
   ```

2. **Hypothermia Death**
   ```bash
   # Let fire die, don't add fuel
   # Wait for body temp < 95°F
   # Expected: Hypothermia effect appears
   # Expected: Gradual health loss
   # Expected: Death after prolonged exposure
   ```

3. **Water Harvesting**
   ```bash
   ./play_game.sh send "7"  # Go somewhere
   ./play_game.sh send "2"  # Go to Shadowy Forest (has puddle)
   ./play_game.sh send "2"  # Harvest Resources (new action)
   # Expected: Get water item
   ```

---

## Performance Considerations

### Issue 2.1/3.2: Sleep Performance
**Problem**: 30,000 hour sleep = 1.8M minutes of processing
**Current**: O(n) where n = minutes
**Target**: O(1) or O(hours) at most

**Optimization Strategy**:
1. Process sleep in hour-long chunks (60 calls for 30K hours)
2. Suppress message generation during processing
3. Batch effect application (don't add/remove effects per minute)

**Expected Improvement**:
- Before: 30K hour sleep takes ~30 seconds (60 checks/second)
- After: 30K hour sleep takes <1 second (500 hours/second)

---

## Compatibility & Migration

### Breaking Changes
**None** - All changes are additive or fix bugs

### Save Game Compatibility
**Not Applicable** - Game doesn't have save/load yet

### Config Changes
```csharp
// New config options:
public static bool AUTO_EQUIP_GEAR = false;  // Issue 3.1
```

### Skill Name Change
**Issue 1.1**: "Fire-making" → "Firecraft"
- No migration needed (skill already named Firecraft in registry)
- Just fixing incorrect references in ActionFactory.cs

---

## Dependencies Between Fixes

### Blocking Dependencies
1. **Issue 1.4 (Death system) BLOCKS**:
   - Issue 1.2 (starvation damage) - Need death check to see damage working
   - Issue 1.3 (temperature damage) - Need death check to see damage working

2. **Issue 2.1 (Message spam) BLOCKS**:
   - Issue 3.2 (Performance) - Same fix addresses both

### Recommended Order
1. **First**: 1.1 (Fire-making skill) - Quick win, unblocks fire testing
2. **Second**: 1.4 (Death system) - Enables testing of damage fixes
3. **Third**: 1.2, 1.3 (Damage systems) - Can now verify with death
4. **Fourth**: 2.1 (Message spam) - Makes testing less annoying
5. **Fifth**: All other issues (independent)

---

## Known Issues NOT In Scope

### Deferred to Future Sprints
1. **Cooking System** - Requires design, not just bug fix
2. **Advanced Hunting** - Complex stealth mechanics redesign
3. **Skill Progression Balance** - Needs playtesting data
4. **Biome Difficulty Curve** - Design decision, not bug
5. **Container System** - Works but limited, needs expansion

### Working as Designed (Not Bugs)
1. **Forage "found nothing"** - Realistic failure rate
2. **Stat drain during sleep** - Correct (0.5x metabolism rate)
3. **Cold messages during gameplay** - 5% chance is intentional

---

## Rollback Plan

### If Critical Bug Introduced
1. Revert specific commit (git revert)
2. Document issue in ISSUES.md
3. Re-test without problematic change
4. Re-design fix with different approach

### High-Risk Changes That May Need Rollback
1. **Issue 1.2** (Survival damage) - If damage rates too harsh
   - Rollback: Remove damage application, keep stat reduction
   - Fix: Tune damage rates in config

2. **Issue 1.3** (Temperature damage) - If conflicts with effects
   - Rollback: Remove DamagePerHour from effects
   - Fix: Apply damage in SurvivalProcessor instead

3. **Issue 2.1** (Message suppression) - If breaks other features
   - Rollback: Re-enable messages, just batch them
   - Fix: Add better batching logic

---

## Post-Implementation Validation

### Checklist for Each Issue
- [ ] Code changes implemented
- [ ] Unit tests added/passing
- [ ] Integration test passing
- [ ] No new compiler warnings
- [ ] Performance acceptable
- [ ] User acceptance criteria met
- [ ] Documentation updated
- [ ] ISSUES.md updated (marked resolved)

### Final Validation
- [ ] Full playthrough (0 → death from starvation)
- [ ] Full playthrough (0 → death from cold)
- [ ] Full playthrough (normal survival gameplay)
- [ ] All 46 issues verified fixed
- [ ] No regressions in existing features

---

**END OF TECHNICAL CONTEXT**
