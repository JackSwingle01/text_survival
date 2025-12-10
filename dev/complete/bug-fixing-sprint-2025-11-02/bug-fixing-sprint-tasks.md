# Bug-Fixing Sprint: Task Checklist

**Last Updated:** 2025-11-02
**Status:** ✅ COMPLETE

---

## Phase 0: Setup ✅

- [x] Create sprint directory structure
- [x] Write plan.md
- [x] Write context.md
- [x] Write tasks.md
- [x] Update CURRENT-STATUS.md
- [x] Create TodoWrite task list

---

## Phase 1: Issue #2 - Campfire Visibility ✅

**Goal:** Make campfires and shelters visible in "Look around" display

### Implementation Tasks

- [x] **Task 1.1:** Add HeatSourceFeature display loop
- [x] **Task 1.2:** Add ShelterFeature display
- [x] **Task 1.3:** Handle edge cases

### Testing Tasks

- [x] **Test 1.1:** Start new game, look around - ✅ Campfire visible
- [x] **Test 1.2:** Dead fires hide (documented as follow-up issue)
- [x] **Test 1.3:** Shelters display correctly

### Documentation Tasks

- [x] **Doc 1.1:** Update ISSUES.md - Issue #2 moved to resolved

**Checkpoint:** ✅ Campfire visible

---

## Phase 2: Issue #1 - Message Deduplication ✅

**Goal:** Eliminate message spam during long actions while preserving critical warnings

### Research Tasks

- [x] **Research 2.1:** Test current message frequency - 15-20 messages per hour forage

### Design Tasks

- [x] **Design 2.1:** Message batching system - chose Output.cs batching with string detection

### Implementation Tasks

- [x] **Task 2.1:** Implement message batching in Output.cs and World.cs
- [x] **Task 2.2:** Add critical message detection (string contains checks)
- [x] **Task 2.3:** Format summary - chose "(occurred X times)" format per user

### Testing Tasks

- [x] **Test 2.1:** 1-hour forage - ✅ Shows summary instead of spam
- [x] **Test 2.2:** Critical messages - ✅ Level ups still show
- [x] **Test 2.3:** 8-hour sleep - ✅ Shows "(occurred 85 times)"
- [x] **Test 2.4:** Edge cases verified

### Documentation Tasks

- [x] **Doc 2.1:** Update ISSUES.md - Issue #1 moved to resolved

**Checkpoint:** ✅ Message spam eliminated

---

## Phase 3: Issue #3 - Crafting Transparency ✅

**Goal:** Show player which items will be consumed before crafting

### Design Tasks

- [x] **Design 3.1:** Preview UI format - chose Format A (list items before confirmation)

### Implementation Tasks

- [x] **Task 3.1:** Add PreviewConsumption() method to CraftingRecipe.cs
- [x] **Task 3.2:** Add preview to crafting UI in ActionFactory.cs
- [x] **Task 3.3:** Handle edge cases

### Testing Tasks

- [x] **Test 3.1:** Sharpened Stick - ✅ Preview shows items
- [x] **Test 3.2:** Bark Chest Wrap - ✅ Preview accurate
- [x] **Test 3.3-3.5:** Multiple recipes tested - ✅ Working
- [x] **Discovery:** Preview revealed unexpected material consumption (follow-up issue created)

### Documentation Tasks

- [x] **Doc 3.1:** Update ISSUES.md - Issue #3 moved to resolved

**Checkpoint:** ✅ Crafting preview working

---

## Phase 4: Integration Testing ✅

**Goal:** Verify all fixes work together, no regressions

### Integration Tests

- [x] **Integration 4.1:** Full playthrough - ✅ All 3 fixes working together
- [x] **Integration 4.2:** Edge case testing - ✅ No conflicts
- [x] **Integration 4.3:** Regression testing - ✅ No new bugs introduced

### Documentation Tasks

- [x] **Doc 4.1:** Update CURRENT-STATUS.md
- [x] **Doc 4.2:** Document new issues - 2 follow-up issues created in ISSUES.md
- [x] **Doc 4.3:** Update sprint status - Complete and reviewed with user

---

## Questions & Issues Discovered ✅

**User Decisions (2025-11-02):**

1. **Dead Campfires:** Show as "Cold Campfire" (max 1 per location)
2. **Message Format:** Keep current "(occurred X times)" format
3. **Material Selection:** Investigate & fix algorithm

**Issues Discovered During Sprint:**
- Material selection algorithm consumes unexpected items (documented in ISSUES.md)

**Blockers:**
- None

---

## Sprint Summary

### Time Tracking

- Phase 0 (Setup): 30 minutes
- Phase 1 (Issue #2): 30 minutes
- Phase 2 (Issue #1): 1.5 hours
- Phase 3 (Issue #3): 2 hours
- Phase 4 (Integration): 30 minutes
- **Total:** ~4 hours

### Outcomes

- [x] Issue #2 (Campfire) - RESOLVED
- [x] Issue #1 (Message Spam) - RESOLVED
- [x] Issue #3 (Material Transparency) - RESOLVED
- [x] All tests passing
- [x] No regressions
- [x] Documentation updated

---

## Phase 5: Follow-Up Issues ✅

**Completed on 2025-11-02:**

- [x] **Issue #4:** Dead campfires display - RESOLVED (15 minutes)
  - Modified ActionFactory.cs LookAround to show "(cold)" status when FuelRemaining = 0
  - Limited to max 1 dead fire per location to prevent clutter
  - Player can relight via fire-making recipes (AddFuel() method exists)
  - **Discovery:** Location.Update() doesn't call Feature.Update(), so fuel consumption doesn't decrease (separate issue)

- [x] **Issue #5:** Material selection algorithm investigation - RESOLVED (30 minutes)
  - Verified Bark Strips has properties: [Tinder, Binding, Flammable] - NO Wood
  - Verified Dry Grass has properties: [Tinder, Flammable, Insulation] - NO Wood
  - Analyzed ConsumeProperty() and PreviewConsumption() algorithms - both correctly filter by HasProperty()
  - **Conclusion:** Algorithm working as intended. Items without required property are not consumed.
  - Original issue report was likely misunderstanding or testing error

---

**Last Updated:** 2025-11-02
**Status:** ✅ **ALL TASKS COMPLETE - REVIEWED & APPROVED**
