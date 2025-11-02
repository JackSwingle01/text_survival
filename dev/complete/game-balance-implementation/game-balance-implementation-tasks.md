# Game Balance Implementation - Task Checklist

**Last Updated**: 2025-11-02
**Status**: Ready to Start
**Estimated Total Time**: 15-20 hours

---

## Phase 1: Fire-Making System Redesign (6-8 hours)

### Task 1.1: Create FireMakingTool Item Class
**Effort**: M (1-2 hours)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Create new file `Items/FireMakingTool.cs`
- [ ] Inherit from Item base class
- [ ] Add properties: MaxUses, RemainingUses, BaseSuccessChance, RequiredSkill, SkillCheckDC
- [ ] Implement UseOnce() method (decrements uses, returns break status)
- [ ] Add durability display to item description (e.g., "Hand Drill (3/5 uses)")
- [ ] Test: Create instance, verify UseOnce() decrements correctly
- [ ] Test: Verify break detection when RemainingUses reaches 0

**Dependencies**: None
**Acceptance Criteria**:
- ✓ Class compiles without errors
- ✓ UseOnce() correctly tracks durability
- ✓ Description shows current/max uses
- ✓ Inherits crafting properties from Item

**Files Modified**: `Items/FireMakingTool.cs` (new)

---

### Task 1.2: Add Fire-Making Tool Recipes
**Effort**: M (2 hours)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] In `Crafting/CraftingSystem.cs`, locate `CreateFireRecipes()` method (line 165)
- [ ] Comment out or remove existing fire recipes (HandDrill, BowDrill, FlintSteel)
- [ ] Add tool crafting recipes using RecipeBuilder:
  - [ ] `hand_drill_tool`: Wood(0.5), 20min → Hand Drill Tool (5 uses, 30% base, DC 0)
  - [ ] `bow_drill_tool`: Wood(1.0) + Binding(0.1), 45min → Bow Drill Tool (8 uses, 50% base, DC 1)
  - [ ] `flint_steel_tool`: Flint(0.2) + Stone(0.3), 5min → Flint & Steel Tool (15 uses, 90% base, no DC)
- [ ] Verify no BaseSuccessChance set (null = 100% success)
- [ ] Add to ItemFactory: MakeHandDrillTool(), MakeBowDrillTool(), MakeFlintSteelTool()
- [ ] Test: Craft each tool, verify 100% success, check durability display

**Dependencies**: Task 1.1 (FireMakingTool class must exist)
**Acceptance Criteria**:
- ✓ Tool recipes always succeed (no RNG)
- ✓ Crafted tools have correct durability values
- ✓ Old fire recipes removed or disabled
- ✓ Tools appear in crafting menu when materials available

**Files Modified**:
- `Crafting/CraftingSystem.cs` lines 165-221
- `Items/ItemFactory.cs` (add tool factory methods)

---

### Task 1.3: Create Start Fire Action
**Effort**: L (3-4 hours)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] In `Actions/ActionFactory.cs`, add new method `StartFire(GameContext ctx)`
- [ ] Implement availability check (.When):
  - [ ] Check for FireMakingTool in inventory
  - [ ] Check for Tinder (0.05 kg minimum)
  - [ ] Check for Kindling/Flammable (0.3 kg minimum)
- [ ] Implement fire starting logic (.Do):
  - [ ] Get best available tool from inventory
  - [ ] Calculate success chance (tool base + skill modifier)
  - [ ] Clamp to 5-95% range
  - [ ] Roll for success
  - [ ] **On Success**:
    - [ ] Consume tinder (0.05 kg)
    - [ ] Consume kindling (0.3 kg)
    - [ ] Remove tool from inventory
    - [ ] Create HeatSourceFeature at location
    - [ ] Display success message with success %
    - [ ] Grant 3 XP to Firecraft skill
  - [ ] **On Failure**:
    - [ ] Consume tinder (0.05 kg)
    - [ ] Use tool once (decrement durability)
    - [ ] Remove tool if broken
    - [ ] Display failure message with success % and remaining durability
    - [ ] Grant 1 XP to Firecraft skill
  - [ ] Advance time by 15 minutes
- [ ] Add action to main crafting menu
- [ ] Test: Multiple fire attempts, verify material consumption
- [ ] Test: Tool durability tracking across attempts
- [ ] Test: Tool break scenarios (last use)
- [ ] Test: Skill XP granted correctly

**Dependencies**: Task 1.2 (tool recipes must exist)
**Acceptance Criteria**:
- ✓ Action visible when player has tool + tinder + kindling
- ✓ Failure only consumes tinder + tool durability
- ✓ Success consumes tinder + kindling + destroys tool
- ✓ Fire created at location on success
- ✓ Clear messaging on durability and success rates
- ✓ XP granted appropriately (3 success, 1 failure)

**Files Modified**:
- `Actions/ActionFactory.cs` (add StartFire method)
- May need helper method for ConsumeProperty if not accessible

---

### Task 1.4: Fix Multiple Campfire Bug
**Effort**: S (30 minutes)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] In `Crafting/CraftingSystem.cs`, locate `Craft()` method (line 25)
- [ ] Find `CraftingResultType.LocationFeature` case (line 83)
- [ ] Add check for existing HeatSourceFeature before creating new one
- [ ] If existing fire found:
  - [ ] Add fuel to existing fire (0.5 kg)
  - [ ] Relight if inactive
  - [ ] Display "You add fuel to the existing fire" message
- [ ] If no existing fire:
  - [ ] Create new campfire as before
  - [ ] Display "You successfully built: Campfire" message
- [ ] Test: Start fire, try to start another, verify fuel added instead

**Dependencies**: None (independent of other tasks)
**Acceptance Criteria**:
- ✓ Starting fire when one exists adds fuel instead of creating duplicate
- ✓ Inactive fires relit when fuel added
- ✓ Message clearly indicates fuel added vs new fire
- ✓ Multiple campfire clutter eliminated

**Files Modified**: `Crafting/CraftingSystem.cs` lines 83-89

---

## Phase 2: Resource Balance & Respawn System (4-5 hours)

### Task 2.1: Implement Resource Respawn Mechanics
**Effort**: M (2 hours)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] In `Environments/LocationFeatures.cs/ForageFeature.cs`, add fields:
  - [ ] `lastForageTime` (DateTime, initialized to World.CurrentTime)
  - [ ] `respawnRateHours` (double, default 48)
- [ ] Modify `ResourceDensity` property (line 11):
  - [ ] Calculate depleted density (existing formula)
  - [ ] Calculate hours since last forage
  - [ ] Calculate respawn progress (hours / respawnRate * baseDensity)
  - [ ] Return min(baseDensity, depletedDensity + respawnProgress)
- [ ] Update `Forage()` method (line 13):
  - [ ] Set lastForageTime = World.CurrentTime when items found
  - [ ] Verify depletion still increments correctly
- [ ] Test: Forage, wait in-game time, forage again, verify density improved
- [ ] Test: Full respawn after 48 hours
- [ ] Test: Partial respawn at 24 hours (~50%)

**Dependencies**: None
**Acceptance Criteria**:
- ✓ Resources respawn over 48 in-game hours
- ✓ Respawn progress proportional to time elapsed
- ✓ Density never exceeds original baseDensity
- ✓ Player-visible (success rates improve over time)
- ✓ No respawn if location never foraged

**Files Modified**: `Environments/LocationFeatures.cs/ForageFeature.cs` lines 6-36

---

### Task 2.2: Increase Starting Location Resource Density
**Effort**: S (30 minutes)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Locate starting location creation code (likely LocationFactory or World initialization)
- [ ] Find ForageFeature instantiation for starting location
- [ ] Change resource density from 1.0 to 1.75
- [ ] Verify other locations remain at 1.0 (normal density)
- [ ] Add comment: "Starting location 1.75x normal - supports 5-8 fire attempts"
- [ ] Test: Forage starting location, verify high success rates initially
- [ ] Test: Calculate depletion curve (1.75, 0.875, 0.583, 0.438, 0.35)

**Dependencies**: None
**Acceptance Criteria**:
- ✓ Starting location has 1.75x resource density
- ✓ Other locations unaffected (1.0 density)
- ✓ Supports 4-5 successful forages before dropping below 50%
- ✓ Documented in code comments

**Files Modified**: `Environments/LocationFactory.cs` or `World.cs` initialization

---

### Task 2.3: Add Visible Resource Items
**Effort**: M (2 hours)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Create harvestable items in ItemFactory:
  - [ ] `MakeBerryBush()` - special Item with "harvestable" flag
  - [ ] `MakeNutTree()` - similar harvestable structure
- [ ] Add berry bush to starting location (visible, not foraged)
- [ ] Create harvest action in ActionFactory:
  - [ ] Check for harvestable items at location
  - [ ] Yield 3-5 berries or 2-3 nuts per harvest
  - [ ] Mark item as "harvested" with respawn timer
  - [ ] Display harvest results
- [ ] Implement harvest respawn (1 harvest per in-game day)
- [ ] Test: Harvest berry bush, verify berries added to inventory
- [ ] Test: Try to harvest again, verify cooldown message
- [ ] Test: Wait 24 hours, verify bush harvestable again

**Dependencies**: Task 2.1 (respawn system framework)
**Acceptance Criteria**:
- ✓ Berry bush visible in starting location
- ✓ Player can harvest without RNG (guaranteed yield)
- ✓ Bush respawns after 24 hours
- ✓ Harvest action appears in location menu
- ✓ Similar system for nut trees in forest locations

**Files Modified**:
- `Items/ItemFactory.cs` (add harvestable item factories)
- `Actions/ActionFactory.cs` (add harvest action)
- `Environments/LocationFactory.cs` (add berry bush to starting location)

---

## Phase 3: Food System Improvements (2-3 hours)

### Task 3.1: Increase Starting Food Percentage
**Effort**: S (15 minutes)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Locate player initialization code (Player constructor or World.Initialize())
- [ ] Find starting food assignment (currently 50% or 0.5)
- [ ] Change to 75% (0.75)
- [ ] Add comment: "75% starting food = ~5.3 hours to starvation at 14%/hour"
- [ ] Test: Start new game, verify food starts at 75%
- [ ] Calculate timeline: 75 / 14 = 5.36 hours

**Dependencies**: None
**Acceptance Criteria**:
- ✓ New players start with 75% food
- ✓ Extends survival timeline from 3.5h to 5.3h
- ✓ Documented in code comment
- ✓ Simple, single-value change

**Files Modified**: `Player.cs` or `World.cs` (player initialization)

---

### Task 3.2: Improve Gathered Food Values
**Effort**: S (30 minutes)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] In `Items/ItemFactory.cs`, update existing food factories:
  - [ ] `MakeMushroom()` line 17: Change 25 calories → 120 calories
  - [ ] Verify `MakeBerry()` line 50: Already 120 calories (no change)
  - [ ] Verify `MakeRoots()` line 60: Already 100 calories (no change)
- [ ] Add new food item factories:
  - [ ] `MakeNuts()`: 100 calories, 20 hydration, 0.15 kg
  - [ ] `MakeEggs()`: 150 calories, 50 hydration, 0.2 kg, ItemProperty.RawMeat
  - [ ] `MakeGrubs()`: 80 calories, 10 hydration, 0.05 kg, ItemProperty.RawMeat
- [ ] Add descriptive flavor text for each
- [ ] Test: Consume each food item, verify calorie restoration (~8-12%)
- [ ] Calculate: 1200 calories = 100% food, so 120 cal = 10%

**Dependencies**: None
**Acceptance Criteria**:
- ✓ All gathered foods provide 7-12% food restoration
- ✓ Mushroom improved from 1% to 10%
- ✓ New food variety adds strategic options
- ✓ Raw meat items can be cooked for bonuses (future)

**Files Modified**: `Items/ItemFactory.cs` lines 15-66 (update existing, add new)

---

### Task 3.3: Add New Food Sources to Forage Tables
**Effort**: M (1 hour)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Locate forage table initialization (likely LocationFactory)
- [ ] Find starting location forage resources
- [ ] Add new food sources with abundance values:
  - [ ] Mushroom: 0.6 abundance (60% initially) - existing, may need adjustment
  - [ ] Berries: 0.4 abundance (via forage + visible bush)
  - [ ] Nuts: 0.3 abundance
  - [ ] Grubs: 0.4 abundance
  - [ ] Eggs: 0.2 abundance (rarer)
- [ ] Balance material abundance (sticks, wood) to not be crowded out
- [ ] Verify total abundance reasonable (~2.5-3.0 total)
- [ ] Test: Forage 10 times, verify food variety found
- [ ] Test: Still finding materials (sticks, etc.) regularly

**Dependencies**: Task 3.2 (new food items must exist)
**Acceptance Criteria**:
- ✓ Player has 60-70% chance to find SOME food per 15-min forage
- ✓ Food variety increases (not just mushrooms)
- ✓ Materials still available (wood, stone, etc.)
- ✓ Balanced across starting location resource density

**Files Modified**: `Environments/LocationFactory.cs` (forage table initialization)

---

## Phase 4: Documentation & Validation (3-4 hours)

### Task 4.1: Update Game Documentation
**Effort**: M (1-2 hours)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Update `documentation/fire-management-system.md`:
  - [ ] Document tool durability system
  - [ ] Explain separation of crafting vs usage
  - [ ] Update fire-starting instructions
  - [ ] Add tool durability tracking section
- [ ] Update `documentation/crafting-system.md`:
  - [ ] Update fire-making recipe examples
  - [ ] Document guaranteed crafting (no BaseSuccessChance = 100%)
  - [ ] Explain tool item type
- [ ] Update `documentation/skill-check-system.md`:
  - [ ] Clarify skill checks apply to usage, not crafting
  - [ ] Add fire-starting as example
  - [ ] Note: Tool crafting is always successful
- [ ] Review all three docs for consistency

**Dependencies**: Phase 1-3 complete (know actual implementation)
**Acceptance Criteria**:
- ✓ Documentation reflects new fire-making system
- ✓ Examples updated with tool durability
- ✓ Clear distinction between crafting and usage
- ✓ No contradictions with other docs

**Files Modified**:
- `documentation/fire-management-system.md`
- `documentation/crafting-system.md`
- `documentation/skill-check-system.md`

---

### Task 4.2: Write Balance Specification Document
**Effort**: M (2 hours)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Create `dev/active/game-balance-implementation/balance-specification.md`
- [ ] Write sections:
  - [ ] Executive Summary (changes at a glance)
  - [ ] Industry Research Summary (The Long Dark, RimWorld, Don't Starve)
  - [ ] Mathematical Analysis (probability tables, timeline charts)
  - [ ] Proposed Changes (with before/after comparisons)
  - [ ] Implementation Guide (files modified, code snippets)
  - [ ] Validation Criteria (success metrics)
- [ ] Include spreadsheet-style probability calculations
- [ ] Add timeline modeling (food, resources, fire success)
- [ ] Reference clarified requirements document

**Dependencies**: Phase 1-3 complete (actual values to document)
**Acceptance Criteria**:
- ✓ Comprehensive spec with all sections
- ✓ Mathematical justification for values
- ✓ Clear before/after comparisons
- ✓ Usable as reference for future balance work

**Files Modified**: `dev/active/game-balance-implementation/balance-specification.md` (new)

---

### Task 4.3: Create Playtesting Protocol
**Effort**: S (1 hour)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Create `dev/active/game-balance-implementation/playtest-protocol.md`
- [ ] Write step-by-step playtest procedure:
  - [ ] Start new game
  - [ ] Forage for materials (track attempts)
  - [ ] Craft fire-making tool
  - [ ] Attempt fire starting (track attempts, durability)
  - [ ] Forage for food (track items, calories)
  - [ ] Craft hunting tools
  - [ ] Hunt and survive
- [ ] Define metrics to track:
  - [ ] Time to first fire (minutes)
  - [ ] Fire attempts before success
  - [ ] Materials consumed for fire
  - [ ] Food level at key milestones
  - [ ] Survival outcome
- [ ] Create data collection template (table format)
- [ ] Include success criteria checklist

**Dependencies**: None (can be written in parallel)
**Acceptance Criteria**:
- ✓ Clear step-by-step instructions
- ✓ Metrics defined and trackable
- ✓ Data collection template ready
- ✓ Success criteria explicit

**Files Modified**: `dev/active/game-balance-implementation/playtest-protocol.md` (new)

---

### Task 4.4: Run Validation Playtests
**Effort**: L (3-4 hours)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Playtest 1: Track all metrics, record results
- [ ] Playtest 2: Track all metrics, record results
- [ ] Playtest 3: Track all metrics, record results
- [ ] Playtest 4: Track all metrics, record results
- [ ] Playtest 5: Track all metrics, record results
- [ ] Compile results in spreadsheet or markdown table
- [ ] Analyze success rates:
  - [ ] Fire establishment (target: 4/5)
  - [ ] Reach hunting (target: 3/5)
  - [ ] Survive 6+ hours (target: 3-4/5)
- [ ] Document issues found in ISSUES.md
- [ ] Compare to success criteria
- [ ] Write summary report in balance specification doc

**Dependencies**: Tasks 1.1-3.3 complete (all changes implemented)
**Acceptance Criteria**:
- ✓ 5 complete playtests conducted
- ✓ All metrics tracked and recorded
- ✓ Results compiled and analyzed
- ✓ Success criteria evaluated (pass/fail)
- ✓ Issues documented for iteration

**Files Modified**:
- `ISSUES.md` (add new issues found)
- `balance-specification.md` (add results section)

---

## Phase 5: Finalization & Handoff (1 hour)

### Task 5.1: Update CURRENT-STATUS.md
**Effort**: S (15 minutes)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Open `dev/active/CURRENT-STATUS.md`
- [ ] Add game balance implementation summary
- [ ] List all files modified with brief descriptions
- [ ] Note playtest results and success rate
- [ ] Document any known issues or follow-ups needed
- [ ] Update "Last Modified" date

**Dependencies**: All other tasks complete
**Acceptance Criteria**:
- ✓ CURRENT-STATUS.md reflects completed work
- ✓ All modified files listed
- ✓ Summary accurate and concise

**Files Modified**: `dev/active/CURRENT-STATUS.md`

---

### Task 5.2: Close or Update Issues in ISSUES.md
**Effort**: S (15 minutes)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Open `ISSUES.md`
- [ ] Mark resolved issues:
  - [ ] 🔴 Fire-Making RNG Death Spiral → ✅ Resolved
  - [ ] 🟠 Resource Depletion Forces Risky Travel → ✅ Resolved
  - [ ] 🟡 Food Scarcity Prevents Reaching Hunting → ✅ Resolved
  - [ ] 🟢 Multiple Campfires Bug → ✅ Resolved
- [ ] Add resolution notes (reference this task folder)
- [ ] Keep any new issues discovered during playtesting
- [ ] Update severity levels if needed

**Dependencies**: All other tasks complete
**Acceptance Criteria**:
- ✓ Original issues marked resolved
- ✓ Resolution notes added
- ✓ New issues properly categorized
- ✓ ISSUES.md up to date

**Files Modified**: `ISSUES.md`

---

### Task 5.3: Move Task Folder to Complete
**Effort**: S (5 minutes)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Verify all tasks checked off in this file
- [ ] Move entire folder from `dev/active/` to `dev/complete/`
- [ ] Update any references in other docs
- [ ] Commit all changes to git

**Dependencies**: All other tasks complete
**Acceptance Criteria**:
- ✓ Folder moved to dev/complete/
- ✓ All tasks marked complete
- ✓ Git commit includes all changes
- ✓ Task history preserved

**Files Modified**: Folder location

---

### Task 5.4: Create Git Commit
**Effort**: S (15 minutes)
**Status**: ⬜ Not Started

**Subtasks**:
- [ ] Stage all modified files
- [ ] Write comprehensive commit message:
  ```
  Implement game balance fixes for early-game survival

  Three-phase fix addressing critical balance issues:

  Phase 1: Fire-Making System Redesign
  - Separate tool crafting (guaranteed) from fire starting (RNG)
  - Add FireMakingTool class with durability tracking
  - Create StartFire action (failure consumes tinder, not tool)
  - Fix multiple campfire bug

  Phase 2: Resource Balance & Respawn
  - Implement slow respawn (48h to full recovery)
  - Increase starting location density to 1.75x
  - Add visible berry bush (guaranteed food source)

  Phase 3: Food System Improvements
  - Increase starting food from 50% to 75%
  - Improve gathered food values (mushrooms 25→120 cal)
  - Add new food sources (nuts, eggs, grubs)

  Playtest Results: [X/5] survived to fire, [X/5] reached hunting

  Fixes #[issue numbers if using issue tracker]

  🤖 Generated with Claude Code
  Co-Authored-By: Claude <noreply@anthropic.com>
  ```
- [ ] Review diff before committing
- [ ] Commit to feature branch
- [ ] Consider creating PR if using PR workflow

**Dependencies**: All implementation complete
**Acceptance Criteria**:
- ✓ All files staged and committed
- ✓ Commit message comprehensive
- ✓ Git history clean
- ✓ Branch ready for merge if applicable

---

## Summary Statistics

**Total Tasks**: 21
**Completed**: 0
**In Progress**: 0
**Not Started**: 21

**Estimated Time by Phase**:
- Phase 1 (Fire): 6-8 hours
- Phase 2 (Resources): 4-5 hours
- Phase 3 (Food): 2-3 hours
- Phase 4 (Docs/Testing): 3-4 hours
- Phase 5 (Finalization): 1 hour

**Total Estimate**: 16-21 hours

**Critical Path**: 1.1 → 1.2 → 1.3 (all other tasks independent)

---

## Progress Tracking

Mark tasks as:
- ⬜ Not Started
- 🔄 In Progress
- ✅ Complete
- ⚠️ Blocked (note blocker)

Update "Last Updated" date when marking tasks complete.

---

**End of Task Checklist**
