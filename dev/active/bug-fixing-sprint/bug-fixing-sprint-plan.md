# Bug-Fixing Sprint: Phase 1 Critical UX Fixes

**Last Updated:** 2025-11-01
**Status:** 🟡 Active
**Estimated Duration:** 6-8 hours

---

## Executive Summary

This sprint addresses 3 critical UX bugs from ISSUES.md that impact core gameplay loops:
1. Message spam during foraging (15-20 identical "still feeling cold" messages)
2. Starting campfire invisible despite providing warmth
3. Material consumption unclear - players don't know which items will be used

**Approach:** Research → Fix → Test for each issue, prioritized by risk/complexity.

---

## Sprint Goals

### Primary Objectives
1. ✅ Fix campfire visibility (Issue #2) - LOW RISK
2. ✅ Fix message spam during long actions (Issue #1) - MEDIUM RISK
3. ✅ Add crafting material transparency (Issue #3) - MEDIUM RISK

### Secondary Objectives
- Comprehensive testing across all 3 fixes
- Update ISSUES.md with resolutions
- Document any new issues discovered

---

## Issues Overview

### Issue #1: "You are still feeling cold" Spam During Foraging

**Priority:** P0 (CRITICAL UX)
**Severity:** High - Makes foraging output unreadable
**Estimated Effort:** 2-3 hours

**Current Behavior:**
```
You forage for 1 hour
You are still feeling cold.
You are still feeling cold.
You are still feeling cold.
... (repeats 15-20 times)
You spent 1 hour searching and found: Dry Grass (1), Large Stick (1)
```

**Root Cause:**
- `World.Update(60)` calls `Player.Update()` 60 times (once per minute)
- Each call generates status message with 5% probability
- Expected: ~3 messages (5% of 60)
- Actual: 15-20 messages (seems higher than 5%?)
- **Research needed:** Why is actual spam rate higher than expected?

**Approved Solution:** Message deduplication at World.Update level
- Collect all messages from Player.Update() calls
- Deduplicate identical messages
- Show summary: "During your search, you felt cold (12 times)"
- ALWAYS display unique messages (status changes)
- ALWAYS display critical warnings (hypothermia onset, frostbite)

**Files to Modify:**
- `World.cs` - Add message collection and deduplication
- `Bodies/Body.cs` - Tag messages as "repeatable" vs "critical"
- `Survival/SurvivalProcessor.cs` - Add message metadata (type, priority)

**Success Criteria:**
- ✅ Foraging for 1 hour shows max 1-2 repeated status messages
- ✅ All unique status changes shown ("starting to feel cold" → "developing frostbite")
- ✅ Critical warnings ALWAYS show regardless of deduplication
- ✅ Summary shown at end: "During your search, you felt cold"

---

### Issue #2: Starting Campfire Not Visible in Location

**Priority:** P1 (High UX Impact)
**Severity:** Medium - Breaks immersion
**Estimated Effort:** 1-1.5 hours

**Current Behavior:**
```
Intro: "The last embers of your campfire are fading..."
Look around: "You see: Nothing..."
(Campfire provides warmth but is invisible)
```

**Root Cause:**
- `ActionFactory.LookAround()` only displays Items, Containers, NPCs
- LocationFeatures (HeatSourceFeature, ShelterFeature) not displayed

**Approved Solution:** Add LocationFeature display with edge case handling

**Display Rules:**
- ✅ HeatSourceFeature: Show if active ("Burning campfire") or with fuel ("Dying campfire")
- ✅ ShelterFeature: Always show ("Windbreak", "Lean-to", etc.)
- ❌ ForageFeature: Do NOT show (it's an action, not an object)
- ❌ EnvironmentFeature: Do NOT show (it's the location type itself)
- ✅ Inactive fire (0 fuel): Show as "Cold campfire" or hide entirely (TBD)

**Files to Modify:**
- `Actions/ActionFactory.cs` (lines 839-866) - Add LocationFeature display loop

**Success Criteria:**
- ✅ Starting campfire visible: "Dying Campfire (burning)"
- ✅ Shelters visible when built
- ✅ Forage feature NOT shown (remains hidden mechanic)
- ✅ Location with only ForageFeature still shows "Nothing..."

---

### Issue #3: Material Consumption Unclear

**Priority:** P1 (Affects player trust)
**Severity:** Medium - Confusing UX
**Estimated Effort:** 2-3 hours

**Current Behavior:**
```
Crafting: Sharpened Stick
Material properties needed:
- Wood: 0.5+ KG (consumed)

[Player crafts]
"You take the Bark Strips from your Bag"
(Unexpected! Recipe didn't mention Bark Strips)
```

**Investigation Notes:**
- Reviewer found: Sharpened Stick recipe ONLY requires Wood (0.5kg)
- Bark Strips have Tinder, Binding, Flammable - NOT Wood
- Current algorithm should NOT consume Bark Strips
- **Bug may not exist in current code** - need to verify
- **Real issue:** Players don't know WHICH items will be consumed

**Approved Solution:** Add transparency - show which items WILL be consumed before crafting
- Before confirming craft, show preview: "Will consume: Large Stick (0.5kg Wood)"
- Player can see exactly which items from inventory will be used
- No algorithm changes - just better UI/UX

**Design Decisions Needed:**
- Where to show preview? (Before "Do you want to craft?" or in recipe display?)
- How to format? (List items by name or by property?)
- Allow player to choose items? (Stretch goal - defer if complex)

**Files to Modify:**
- `Actions/ActionFactory.cs` (CraftItem action) - Add pre-craft preview
- `Crafting/CraftingRecipe.cs` - Add method to preview which items will be consumed
- `Crafting/CraftingSystem.cs` (optional) - Improve recipe display

**Success Criteria:**
- ✅ Before crafting, player sees: "Will consume: Large Stick (0.5kg)"
- ✅ Preview matches actual consumption
- ✅ Works for all recipe types (single property, multiple properties, tools)
- ✅ Tested with 5-10 different recipes

---

## Implementation Plan

### Phase 0: Setup (15 min)
- [x] Create sprint directory structure
- [x] Write plan, context, and tasks files
- [ ] Update CURRENT-STATUS.md

### Phase 1: Quick Win - Issue #2 (1-1.5 hrs)
**Rationale:** Easiest fix, boosts morale, low risk

1. [ ] Add HeatSourceFeature display to LookAround (30 min)
2. [ ] Add ShelterFeature display (15 min)
3. [ ] Handle edge cases (ForageFeature hidden, inactive fires) (30 min)
4. [ ] Test: Start game, look around, verify campfire visible (15 min)
5. [ ] Update ISSUES.md: Mark Issue #2 as RESOLVED (5 min)

**Checkpoint:** Campfire visible before continuing

---

### Phase 2: Message Deduplication - Issue #1 (2-3 hrs)
**Rationale:** Well-understood solution, high user impact

1. [ ] Research: Test foraging, count actual message frequency (30 min)
   - Why 15-20 messages instead of expected ~3 (5% of 60)?
   - Document findings in sprint context

2. [ ] Design message metadata system (30 min)
   - Decide: How to tag messages as "repeatable" vs "critical"?
   - Decide: Where to store metadata? (In message string? Separate class?)

3. [ ] Implement message collection in World.Update() (1 hr)
   - Collect all messages from Player.Update() calls
   - Deduplicate identical messages
   - Count repetitions
   - Display summary

4. [ ] Add critical message handling (30 min)
   - Ensure hypothermia onset always shows
   - Ensure frostbite start always shows
   - Ensure health drops always show

5. [ ] Test thoroughly (30 min)
   - Forage 1 hour: Verify max 1-2 repeated messages
   - Forage 3 hours: Verify critical events show (cross hypothermia threshold)
   - Sleep 8 hours: Verify works for sleep action too

6. [ ] Update ISSUES.md: Mark Issue #1 as RESOLVED (5 min)

**Checkpoint:** Message spam eliminated, critical warnings preserved

---

### Phase 3: Crafting Transparency - Issue #3 (2-3 hrs)
**Rationale:** Improves player trust, medium complexity

1. [ ] Design preview UI (30 min)
   - Decide: Show before confirmation or in recipe display?
   - Decide: Format as "Will consume: X, Y, Z" or show properties?
   - Mock up the display format

2. [ ] Implement preview method in CraftingRecipe (1 hr)
   - Add method: `GetItemsToConsume(Player player) → List<Item>`
   - Reuse existing ConsumeProperty logic but don't actually consume
   - Return list of items that WOULD be consumed

3. [ ] Add preview to crafting UI (30 min)
   - Display items before "Do you want to craft?"
   - Format clearly: "This will consume: Large Stick (0.5kg)"

4. [ ] Test with multiple recipes (30 min)
   - Sharpened Stick (single property)
   - Bow Drill (multiple properties: Wood + Binding)
   - Hand Drill Fire (Wood + Tinder)
   - Bark Chest Wrap (Hide + Binding)
   - Verify preview matches actual consumption

5. [ ] Update ISSUES.md: Mark Issue #3 as RESOLVED (5 min)

**Checkpoint:** Crafting shows clear material preview

---

### Phase 4: Integration Testing (0.5-1 hr)

1. [ ] Full playthrough (30 min)
   - Start game → Look around → Forage 3 hours → Craft items
   - Verify all 3 fixes working together
   - Check for any regressions

2. [ ] Edge case testing (15 min)
   - Forage in different biomes
   - Craft with edge case inventories (single item, many items)
   - Sleep for various durations

3. [ ] Document findings (15 min)
   - Update CURRENT-STATUS.md
   - Add any new issues to ISSUES.md
   - Mark sprint as complete

---

## Risk Assessment

### Issue #1 (Message Deduplication) - MEDIUM RISK
**Risks:**
- May suppress important status changes
- Critical events might be lost in deduplication
- Complex logic could introduce bugs

**Mitigation:**
- Tag messages as "critical" vs "repeatable"
- Always show critical events immediately
- Thorough testing with threshold-crossing scenarios

---

### Issue #2 (Campfire Visibility) - LOW RISK
**Risks:**
- May display features that should be hidden
- Could break "Nothing..." display logic

**Mitigation:**
- Explicit rules for each LocationFeature type
- Test with all feature combinations
- Simple additive change, easy to rollback

---

### Issue #3 (Crafting Transparency) - MEDIUM RISK
**Risks:**
- Preview logic may not match actual consumption
- Could expose bugs in ConsumeProperty algorithm
- UI changes could be confusing

**Mitigation:**
- Reuse existing ConsumeProperty logic (don't reinvent)
- Test with 10+ recipes to verify accuracy
- Clear, simple display format

---

## Success Criteria

### Functionality
- ✅ Foraging shows max 1-2 repeated status messages (not 15-20)
- ✅ Campfire visible in location display with status
- ✅ Crafting shows which items will be consumed before confirmation
- ✅ Critical warnings always displayed (no suppression)
- ✅ No regressions in existing features

### Code Quality
- ✅ Build succeeds with no warnings
- ✅ Changes follow existing architecture patterns
- ✅ Code is well-commented and documented

### Documentation
- ✅ ISSUES.md updated with all resolutions
- ✅ CURRENT-STATUS.md updated with sprint results
- ✅ Any new issues documented

---

## Timeline

**Optimistic:** 5-6 hours
**Realistic:** 6-8 hours
**Pessimistic:** 8-10 hours (if major issues discovered)

**Planned Start:** 2025-11-01 Evening
**Planned Completion:** 2025-11-02 Morning

---

## Questions & Blockers

**Questions for User Review (Morning):**
- [ ] Issue #1: Is message deduplication approach working as expected?
- [ ] Issue #2: Should inactive fires (0 fuel) be shown or hidden?
- [ ] Issue #3: Is preview format clear and helpful?

**Blockers:**
- None currently identified

**Issues Discovered During Sprint:**
- (Will document here as found)

---

## Related Documentation

- `ISSUES.md` - Source of all bugs being fixed
- `SUGGESTIONS.md` - Future improvements (Phase 2 QoL)
- `CURRENT-STATUS.md` - Development status tracking
- `dev/active/architecture-review/` - Architecture issues (separate from this sprint)

---

**Last Updated:** 2025-11-01
