# Architecture Review Summary

**Date**: 2025-11-01
**Reviewer**: code-architecture-reviewer agent
**Scope**: Full codebase
**Status**: ✅ Complete, **AWAITING USER DECISION**

---

## Executive Summary

Your Text Survival RPG codebase has **excellent architectural foundations** with strong adherence to design patterns. The ActionBuilder, EffectBuilder, and property-based crafting systems are exemplary. However, there are **3 critical issues** that need immediate attention before continuing development.

---

## Critical Issues (🔴 Must Fix)

### 1. Body.Rest() Double-Updates World Time
- **File**: Bodies/Body.cs:269
- **Problem**: Calls `World.Update(minutes)` AND triggers `Update()` which also processes time
- **Impact**: Time advances twice, causes double survival processing, drift in game state
- **Fix**: Remove one of the time update calls

### 2. SurvivalProcessor.Sleep() Mutates Input State
- **File**: Survival/SurvivalProcessor.cs:139
- **Problem**: Modifies `data.Hydration` and `data.Calories` directly, violates pure function design
- **Impact**: Breaks the pure function architecture constraint, makes testing harder, creates hidden side effects
- **Fix**: Return a new SurvivalData instance instead of modifying input

### 3. Actor.Update() Missing Null Check
- **File**: Actors/Actor.cs:52
- **Problem**: No null check on `CurrentLocation` before accessing `CurrentLocation.Temperature`
- **Impact**: Potential crash if actor is in invalid state
- **Fix**: Add null check with appropriate fallback or error handling

---

## High-Priority Concerns (🟠 Should Fix)

### 4. Inconsistent Time Passage Patterns
- **Issue**: Three different patterns across codebase:
  1. Action calls `World.Update()` directly
  2. Action uses `.TakesMinutes()` builder method
  3. Action relies on implicit time passage
- **Impact**: Hard to reason about time flow, easy to miss time updates
- **Recommendation**: Standardize on `.TakesMinutes()` pattern for all actions

### 5. GameContext Creates New CraftingSystem Every Time
- **File**: GameContext.cs:20
- **Problem**: Property getter creates new instance on each access
- **Impact**: Performance overhead, state is not preserved between accesses
- **Fix**: Make CraftingSystem a singleton or cached instance

### 6. Forage Action May Not Update Time
- **Issue**: Foraging action needs verification that it properly advances time
- **Impact**: Player could forage instantly without time cost
- **Fix**: Add explicit `.TakesMinutes(60)` to foraging actions

---

## Code Quality Issues (🟡 Nice to Fix)

### 7. Magic Numbers Scattered Throughout
- **Examples**: Temperature thresholds, damage values, skill multipliers
- **Impact**: Hard to balance, inconsistent values, no single source of truth
- **Recommendation**: Create GameBalance.cs with named constants

### 8. Player.cs Namespace Inconsistency
- **File**: Player.cs
- **Problem**: Not in Actors/ folder despite being an Actor subclass
- **Impact**: Inconsistent organization, harder to find related code
- **Fix**: Move to Actors/ namespace/folder

### 9. Missing Builder Validation
- **Issue**: RecipeBuilder and ActionBuilder don't validate required fields
- **Impact**: Can create invalid recipes/actions that fail at runtime
- **Fix**: Add validation in `.Build()` methods

---

## Positive Patterns (✅ Keep Doing!)

### Excellent Architecture Examples

1. **ActionBuilder Pattern** - Exemplary fluent API implementation
   - Clear, readable action definitions
   - Proper separation of concerns
   - Excellent use of `.When()/.Do()/.ThenShow()/.ThenReturn()`

2. **SurvivalProcessor.Process()** - Perfect pure function design
   - Stateless, returns new results
   - Easy to test and reason about
   - Only exception is Sleep() method (see Critical Issue #2)

3. **Property-Based Crafting** - Flexible and extensible
   - Items contribute via properties, not item-specific checks
   - Easy to add new materials and recipes
   - Follows open/closed principle

4. **EffectBuilder Pattern** - Excellent composable design
   - Fluent API for creating complex effects
   - Clear, readable effect definitions
   - Good separation of effect creation from application

5. **Hierarchical Body System** - Realistic and well-structured
   - BodyRegion → Tissues → Organs hierarchy
   - Single damage entry point (Body.Damage)
   - Good capacity calculation system

6. **EffectRegistry Duplicate Handling** - Works correctly after recent fix
   - Properly prevents stacking when `AllowMultiple(false)`
   - Fixed the frostbite infinite stacking bug

---

## Recommendations by Priority

### Priority 1: Fix Critical Issues (1-2 hours)
1. Fix Body.Rest() double time update
2. Make SurvivalProcessor.Sleep() pure
3. Add null check to Actor.Update()

### Priority 2: Standardize Time Patterns (2-3 hours)
1. Audit all actions for time passage
2. Standardize on `.TakesMinutes()` pattern
3. Document time passage requirements

### Priority 3: Code Quality Improvements (3-4 hours)
1. Create GameBalance.cs for constants
2. Move Player.cs to Actors/ folder
3. Add builder validation
4. Fix GameContext CraftingSystem creation

---

## Decision Required

**IMPORTANT**: No changes have been made. Please choose how to proceed:

### Option A: Fix Critical Issues First ⭐ RECOMMENDED
- Address the 3 critical bugs
- Standardize time passage patterns
- Then continue with Phase 4 (Tool Progression)
- **Estimated time:** 1-2 hours
- **Risk level:** Low (fixes prevent future bugs)

### Option B: Continue Phase 4, Fix Later
- Document the issues for later
- Continue implementing tool crafting
- Risk encountering time-related bugs during testing
- **Estimated time:** Saves 1-2 hours now
- **Risk level:** Medium (may need to rework later)

### Option C: Cherry-Pick Fixes
- Fix only the crash risk (Actor.Update null check)
- Continue development
- Defer other fixes to dedicated refactoring session
- **Estimated time:** 10 minutes
- **Risk level:** Low-Medium (addresses immediate crash risk)

---

## Full Review Document

Detailed review with code examples and specific recommendations:
**Location**: `dev/active/architecture-review/architecture-review.md`

---

## Related Documentation

- **Project Guidelines**: CLAUDE.md
- **Current Status**: dev/active/CURRENT-STATUS.md
- **Issue Tracker**: ISSUES.md
- **Architecture Docs**: documentation/ (action-system.md, body-and-damage.md, etc.)
