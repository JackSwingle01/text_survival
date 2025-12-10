# Gameplay Fixes Sprint - Task Checklist

## Phase 1: Critical System Fixes (BLOCKER)

### Issue 1.1: Fire-Making Skill Crash ⚡
**Effort**: S (30 min) | **Priority**: P0
- [ ] Change line 370 in ActionFactory.cs: "Fire-making" → "Firecraft"
- [ ] Change line 411 in ActionFactory.cs: "Fire-making" → "Firecraft"
- [ ] Test: Fire-making action executes without crash
- [ ] Test: Firecraft skill gains XP on success
- [ ] Test: Firecraft skill gains XP on failure
- [ ] Commit: "Fix fire-making skill name crash"

---

### Issue 1.2: Survival Stat Consequences Not Working 💀
**Effort**: M (1 day) | **Priority**: P0
- [ ] Add damage check in Body.cs Update() method (line ~145)
- [ ] Implement starvation damage (0.1% health/min at 0 calories)
- [ ] Implement dehydration damage (0.2% health/min at 0 hydration)
- [ ] Add warning messages for critical stats
- [ ] Write unit test: Test_UpdateAppliesDamageAtZeroCalories()
- [ ] Write unit test: Test_UpdateAppliesDamageAtZeroHydration()
- [ ] Test: Player takes damage when food = 0%
- [ ] Test: Player takes damage when water = 0%
- [ ] Test: Energy at 0% reduces capacities (no damage)
- [ ] Test: Death occurs after realistic starvation time
- [ ] Commit: "Add damage for zero food/water stats"

---

### Issue 1.3: Hypothermia/Temperature Damage Not Working ❄️
**Effort**: M (1 day) | **Priority**: P0
- [ ] Add DamagePerHour field to EffectBuilder.cs
- [ ] Add WithDamagePerHour() method to EffectBuilder
- [ ] Update Hypothermia effect with damage (0.05/hr)
- [ ] Update Frostbite effect with damage (0.10/hr)
- [ ] Modify EffectRegistry.Update() to apply damage from effects
- [ ] Write unit test: Test_HypothermiaEffectAppliesDamage()
- [ ] Write unit test: Test_FrostbiteEffectAppliesDamage()
- [ ] Test: Hypothermia effect appears at temp < 95°F
- [ ] Test: Hypothermia causes gradual health loss
- [ ] Test: Frostbite appears at temp < 89.6°F
- [ ] Test: Frostbite causes faster health loss
- [ ] Test: Warming removes effects
- [ ] Commit: "Add damage to temperature effects"

---

### Issue 1.4: Death System Missing 💀
**Effort**: S (2 hours) | **Priority**: P0
- [ ] Add death check in Program.cs main loop
- [ ] Implement death screen UI
- [ ] Add World.StartTime field
- [ ] Show survival time statistics on death
- [ ] Show final stats on death
- [ ] Exit game gracefully after death
- [ ] Write unit test: Test_IsDestroyedWhenHealthZero()
- [ ] Test: Game ends when health = 0
- [ ] Test: Death screen displays correctly
- [ ] Test: Cannot take actions after death
- [ ] Commit: "Add death system and game over screen"

---

## Phase 2: High-Priority Bugs

### Issue 2.1: Message Spam 📢
**Effort**: M (4 hours) | **Priority**: P1
- [ ] Modify Body.Rest() to suppress messages during sleep
- [ ] Add sleep summary message after completion
- [ ] Remove per-minute cold status messages from GenerateColdEffects()
- [ ] Test: No spam during long sleep
- [ ] Test: Summary appears after sleep
- [ ] Test: Cold messages limited during normal gameplay
- [ ] Test: 100+ hour sleep completes in <1 second
- [ ] Commit: "Suppress spam messages during sleep"

---

### Issue 2.2: Duplicate Inspect/Drop Menu 🔄
**Effort**: S (15 min) | **Priority**: P1
- [ ] Remove duplicate DescribeItem() in DecideInventoryAction (line 871)
- [ ] Rename DropItem action from "Inspect {item}" to "Drop {item}" (line 727)
- [ ] Test: Item menu shows Use, Inspect, Drop, Back (no duplicates)
- [ ] Test: Drop action labeled correctly
- [ ] Test: All three actions work
- [ ] Commit: "Fix duplicate Inspect and wrong Drop label"

---

### Issue 2.3: Sleep Exploit ⏰
**Effort**: S (30 min) | **Priority**: P1
- [ ] Add bounds to Sleep input: ReadInt(1, 24)
- [ ] Calculate recommended sleep duration
- [ ] Display suggestion to player
- [ ] Test: Invalid input rejected (0, 25, 9999)
- [ ] Test: Max 24 hours accepted
- [ ] Test: Suggestion matches tiredness level
- [ ] Commit: "Add sleep duration limits and suggestions"

---

### Issue 2.4: Water Harvesting Not Accessible 💧
**Effort**: M (3 hours) | **Priority**: P1
- [ ] Create HarvestResources() action in ActionFactory.Survival
- [ ] Add check for HarvestableFeature in When clause
- [ ] Implement harvest logic (calls feature.Harvest())
- [ ] Add to MainMenu options list
- [ ] Set time cost (5 minutes)
- [ ] Test: "Harvest Resources" appears when puddle present
- [ ] Test: Harvesting gives water item
- [ ] Test: Resource quantity decreases
- [ ] Test: Depleted resources show correctly
- [ ] Test: Resources respawn over time
- [ ] Commit: "Add harvest resources action for water"

---

### Issue 2.5: Hunting Mechanics Broken 🏹
**Effort**: M (4 hours) | **Priority**: P1
**NOTE**: Needs investigation first
- [ ] Read StealthManager.cs implementation
- [ ] Read Animal.cs detection logic
- [ ] Read HuntingManager approach mechanics
- [ ] Identify issue (stealth check? distance calc? RNG?)
- [ ] Implement fix (TBD based on investigation)
- [ ] Write tests for stealth mechanics
- [ ] Test: Player can approach without instant detection
- [ ] Test: Stealth skill affects detection
- [ ] Test: Distance affects detection
- [ ] Commit: "Fix hunting stealth detection mechanics"

---

### Issue 2.6: Foraging Logic Error 🌿
**Effort**: S (1 hour) | **Priority**: P1
- [ ] Remove Output.WriteLine from ForageFeature.Forage() (lines 77-83)
- [ ] Move message display to ActionFactory.Forage action
- [ ] Track items found before/after foraging
- [ ] Display correct message based on results
- [ ] Test: "Found nothing" only when truly nothing found
- [ ] Test: Item collection menu appears AFTER message
- [ ] Test: Message accurately describes items
- [ ] Commit: "Fix foraging message ordering"

---

## Phase 3: Medium-Priority UX/Balance

### Issue 3.1: Equipment Auto-Equip Without Choice 👕
**Effort**: S (30 min) | **Priority**: P2
- [ ] Add confirmation prompt in Player.TakeItem()
- [ ] Add AUTO_EQUIP_GEAR config option
- [ ] Respect config setting
- [ ] Show "equipped" confirmation
- [ ] Test: Player prompted before equipping
- [ ] Test: Config disables auto-equip
- [ ] Test: Unequipped items go to inventory
- [ ] Commit: "Add confirmation for auto-equip gear"

---

### Issue 3.2: Cold Status Processing Performance ❄️
**Effort**: S (1 hour) | **Priority**: P2
**NOTE**: Covered by Issue 2.1 fixes
- [ ] Verify sleep performance after 2.1 fix
- [ ] Optional: Process sleep in hourly chunks
- [ ] Test: 1000+ hour sleep completes in <1 second
- [ ] Test: Survival processing remains accurate
- [ ] Commit: "Optimize long sleep performance" (if needed)

---

### Issue 3.3a: Cooking System Missing 🍖
**Effort**: M (4-6 hours) | **Priority**: P2
**NOTE**: Needs design review first
- [ ] Check if CookingFeature exists in codebase
- [ ] Design cooking action flow
- [ ] Implement cooking menu
- [ ] Add to MainMenu when fire present
- [ ] Create cooking recipes (if needed)
- [ ] Test: Cooking action accessible
- [ ] Test: Can cook raw food
- [ ] Test: Cooked food has better stats
- [ ] Commit: "Add cooking system" (if implemented)

---

### Issue 3.3b: Equipment Screen Missing 🛡️
**Effort**: S (1 hour) | **Priority**: P2
- [ ] Create ViewEquipment() action in ActionFactory.Inventory
- [ ] Implement DescribeEquipment() in InventoryManager (if needed)
- [ ] Add to MainMenu options
- [ ] Test: Equipment screen shows all equipped items
- [ ] Test: Shows stats (armor, warmth, damage)
- [ ] Test: Accessible from main menu
- [ ] Commit: "Add equipment viewing screen"

---

### Issue 3.4a: Stat Drain Balance ⚖️
**Effort**: None | **Priority**: P2
**DECISION**: Working as designed, not a bug
- [x] Verify math: 30,000 hours = realistic timeframe
- [x] Document in README
- [x] Close as "not a bug"

---

### Issue 3.4b: Forage Success Rate Balance ⚖️
**Effort**: None | **Priority**: P2
**DECISION**: Working as designed
- [x] Review foraging probabilities in Program.cs
- [x] Verify "found nothing" is normal/expected
- [x] Document intended behavior
- [x] Close as "working as intended"

---

## Phase 4: Low-Priority Polish

### Issue 4.1: Input Validation Consistency ✅
**Effort**: S (1 hour) | **Priority**: P3
- [ ] Standardize ReadInt(low, high) error message
- [ ] Standardize ReadInt() error message
- [ ] Make Y/N input case-insensitive
- [ ] Make directional input (N/E/S/W) case-insensitive
- [ ] Test: All validation messages consistent
- [ ] Test: Case-insensitive where appropriate
- [ ] Commit: "Standardize input validation messages"

---

### Issue 4.2: Menu Navigation Improvements 🧭
**Effort**: M (4 hours) | **Priority**: P3
**NOTE**: Needs design review
- [ ] Design breadcrumb system
- [ ] Standardize "Back" vs "Return" vs "Cancel"
- [ ] Implement breadcrumb display
- [ ] Add ESC key support (if desired)
- [ ] Test: Breadcrumbs show correct path
- [ ] Test: Back/Return naming consistent
- [ ] Commit: "Improve menu navigation UX"

---

### Issue 4.3: Message Formatting Consistency 📝
**Effort**: S (2 hours) | **Priority**: P3
- [ ] Create message style guide
- [ ] Audit all Output.WriteLine calls
- [ ] Standardize item name capitalization
- [ ] Standardize sentence capitalization
- [ ] Standardize punctuation
- [ ] Test: Messages follow style guide
- [ ] Commit: "Standardize message formatting"

---

## Testing Tasks

### Unit Tests
- [ ] SurvivalProcessorTests: Test_StarvationDamage
- [ ] SurvivalProcessorTests: Test_DehydrationDamage
- [ ] BodyTests: Test_UpdateAppliesDamageAtZeroStats
- [ ] BodyTests: Test_IsDestroyedWhenHealthZero
- [ ] EffectRegistryTests: Test_DamageFromEffects
- [ ] EffectRegistryTests: Test_HypothermiaDamage
- [ ] All tests pass

### Integration Tests
- [ ] Scenario: Starvation → death
- [ ] Scenario: Dehydration → death
- [ ] Scenario: Hypothermia → death
- [ ] Scenario: Sleep 24 hours → wake correctly
- [ ] Scenario: Harvest water → get item
- [ ] Scenario: Hunt animal → stealth works
- [ ] Scenario: Forage → messages correct

### Regression Tests
- [ ] Fire-making still works
- [ ] Crafting system unchanged
- [ ] Combat system unchanged
- [ ] Foraging mechanics work
- [ ] Movement/travel works

---

## Documentation Tasks

- [ ] Update CURRENT-STATUS.md
- [ ] Update ISSUES.md (mark all 46 issues resolved)
- [ ] Update README (if design changed)
- [ ] Update relevant dev docs
- [ ] Document new config options
- [ ] Document death system

---

## Sprint Completion Checklist

### Must Have (Sprint Goal)
- [ ] All Phase 1 issues fixed
- [ ] All Phase 2 issues fixed
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] No game-breaking bugs

### Should Have
- [ ] 80%+ Phase 3 issues fixed
- [ ] Performance acceptable
- [ ] No regressions

### Nice to Have
- [ ] Phase 4 polish completed
- [ ] Code coverage >60%
- [ ] All documentation updated

---

## Daily Standup Template

**What I did yesterday:**
- [List completed tasks]

**What I'm doing today:**
- [List planned tasks]

**Blockers:**
- [List any blockers]

**Issues found:**
- [New bugs discovered during implementation]

---

## Progress Tracking

### Week 1 (Days 1-2): Phase 1
- [ ] Day 1: Issues 1.1, 1.2
- [ ] Day 2: Issues 1.3, 1.4

### Week 2 (Days 3-5): Phase 2
- [ ] Day 3: Issues 2.1, 2.2, 2.3
- [ ] Day 4: Issues 2.4, 2.5
- [ ] Day 5: Issue 2.6 + Phase 2 testing

### Week 3 (Days 6-8): Phase 3
- [ ] Day 6: Issues 3.1, 3.2
- [ ] Day 7: Issue 3.3 (cooking, equipment)
- [ ] Day 8: Phase 3 testing

### Week 4 (Days 9-10): Phase 4
- [ ] Day 9: Issues 4.1, 4.2
- [ ] Day 10: Issue 4.3 + final validation

---

**SPRINT START**: ___________
**SPRINT END**: ___________
**STATUS**: Not Started / In Progress / Complete

---

**END OF TASK LIST**
