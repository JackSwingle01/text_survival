# Development Backlog - Text Survival RPG

**Last Updated:** 2025-11-02
**Purpose:** Tracking deferred tasks, technical debt, and future improvements

---

## 🔴 High Priority (Deferred from Active Sprints)

### Late-Game Testing (from crafting-foraging-overhaul Phase 9)

**Reason for Deferral:** Early game needs polish before late-game content is viable

#### Task 43: Balance Tool Effectiveness
**Effort:** Medium (~3-4 hours)
**Prerequisites:** Early-game polish complete, combat system refined

**Testing Scope:**
- Test damage values in combat (16 weapon/tool recipes)
- Test harvesting yields per tier (Sharp Rock → Flint → Bone → Obsidian)
- Verify progression feels rewarding and meaningful
- Compare to design specs

**Tool Tiers to Test:**
- Knives: Sharp Rock, Flint Knife, Bone Knife, Obsidian Blade
- Spears: Sharpened Stick, Fire-Hardened, Flint-Tipped, Bone-Tipped, Obsidian-Tipped
- Clubs: Heavy Stick, Stone-Weighted, Bone-Studded
- Axes: Stone Hand Axe, Flint Hand Axe

**Success Criteria:**
- Each tier feels meaningfully stronger than previous
- Damage/yield progression is balanced
- Crafting costs align with power level

---

#### Task 44: Test Shelter Warmth Progression
**Effort:** Small (~2 hours)
**Prerequisites:** Temperature system stable, early-game balance complete

**Testing Scope:**
- Verify temperature bonuses work: +2°F, +5°F, +8°F, +15°F
- Test survival time improvements at each tier
- Verify each tier feels meaningfully better
- Test in different weather conditions (future feature)

**Shelter Tiers to Test:**
- Tier 1: Windbreak (+2°F, emergency shelter at current location)
- Tier 2: Lean-to (+5°F, moderate protection)
- Tier 3: Debris Hut (+8°F, good protection)
- Tier 4: Log Cabin (+15°F, excellent protection)

**Success Criteria:**
- Each shelter provides stated warmth bonus
- Crafting time investment feels appropriate for benefit
- Progression encourages upgrading shelters

**Note:** Fire warmth verified (+15°F burning, +5.25°F embers)

---

#### Task 45: Test Biome Viability
**Effort:** Medium (~4-6 hours)
**Prerequisites:** Early-game balance complete, day-1 survival path refined

**Testing Scope:**
- Test starting in each biome (5 biomes total)
- Verify each has materials for day-1 fire
- Verify each has materials for day-1 shelter
- Document biome-specific strategies

**Biomes to Test:**
- ✅ Forest: VALIDATED (2025-11-02) - Forgiving, all essentials
- ⏳ Plains: Risk/reward hunting grounds
- ⏳ Riverbank: Water/stone specialization
- ⏳ Cave: **Advanced biome** (intentionally NO organics, see README)
- ⏳ Hillside: Balanced stone/organic mix

**Cave Design Note (from README):**
- NOT a starting biome - requires preparation
- Low food/plants (bring supplies)
- Excellent weather protection
- Rare materials (obsidian, crystal)
- Test: Arrival with pre-gathered supplies (verify protection/materials work)

**Success Criteria:**
- Forest/Plains/Riverbank/Hillside: Day-1 survival possible
- Cave: Survivable with preparation, rare materials accessible
- Each biome feels distinct and encourages strategic planning

---

#### Task 41 (Partial): Balance Material Spawn Rates
**Effort:** Medium (~2-3 hours)
**Prerequisites:** Other biome testing complete

**Completed:**
- ✅ Forest biome balance (baseResourceDensity 1.6, buffed abundances)
- ✅ Expected: ~3-4 items per 15-min forage (~70% hit rate)

**Remaining:**
- Plains spawn rates (grassland materials)
- Riverbank spawn rates (water/stone materials)
- Cave spawn rates (stone-only materials)
- Hillside spawn rates (balanced materials)
- Verify rare materials feel special across all biomes

---

## 🟠 Medium Priority (Technical Debt)

### Time Handling Pattern Inconsistency
**Severity:** Medium - Architectural Consistency
**Source:** Code Review 2025-11-02 (Issue #1 Important)
**Status:** Documented in ISSUES.md

**Issue:**
- ForageFeature.Forage() internally calls `World.Update(minutes)`
- Fire actions (StartFire, AddFuelToFire) manually call `World.Update()`
- Violates standard ActionBuilder pattern (should use `.TakesMinutes()`)

**Impact:**
- Code works correctly but mixes responsibilities
- Makes time-handling logic harder to track/debug
- Future developers may not know which pattern to follow

**Recommended Fix:**
1. Refactor ForageFeature.Forage() to NOT call World.Update() internally
2. Update forage action to use `.TakesMinutes(minutes)` in ActionFactory
3. Update fire actions to use `.TakesMinutes(20)` instead of manual updates
4. Standardize all actions to use `.TakesMinutes()` for consistency

**Effort:** Medium (~2-3 hours)
**Prerequisites:** None (can be done anytime)

---

### Crafting Preview Duplication Bug
**Severity:** Low - Display Bug (Cosmetic)
**Source:** Code Review 2025-11-02 (Issue #2 Important)
**Status:** Documented in ISSUES.md

**Issue:**
- PreviewConsumption() shows duplicate entries when consuming partial items
- Example: Shows "Dry Grass (0.02kg), Large Stick (0.48kg), Dry Grass (0.02kg), Large Stick (0.03kg)"
- Actual consumption is correct (only 1x Dry Grass, 1x Large Stick consumed)

**Impact:**
- Players see misleading preview (looks like 4 items when only 2 consumed)
- Reduces trust in crafting system accuracy
- Actual crafting works correctly (display-only issue)

**Recommended Fix:**
- Modify PreviewConsumption() to group partial consumptions of same item
- Display: "Dry Grass (0.02kg), Large Stick (0.5kg total)"

**Effort:** Small (~1 hour)
**Prerequisites:** None (can be done anytime)

---

### Material Properties Display Inconsistency
**Severity:** Medium - UX Bug
**Source:** Playtest 2025-11-02
**Status:** Active in ISSUES.md

**Issue:**
- "Show My Materials" menu shows `Tinder: 0.0 total`
- Craft preview shows `Tinder: 0.5/0.1` (correct)
- Inconsistent display of same materials

**Recommended Fix:**
- Investigate how "Show My Materials" calculates property totals
- Ensure both use same calculation method

**Effort:** Small (~1-2 hours)
**Prerequisites:** None (can be done anytime)

---

## 🟡 Low Priority (Code Quality)

### Magic Numbers in Fire Embers System
**Source:** Code Review 2025-11-02 (Issue #1 Minor)
**Location:** HeatSourceFeature.cs

**Issue:**
- Ember duration threshold (0.25) is magic number
- Ember heat percentage (0.35) is magic number
- Should extract to named constants

**Recommended Fix:**
```csharp
private const double EMBER_DURATION_RATIO = 0.25;  // 25% of burn time
private const double EMBER_HEAT_RATIO = 0.35;      // 35% of full heat
```

**Effort:** Trivial (~15 minutes)

---

### Critical Message Detection Configuration
**Source:** Code Review 2025-11-02 (Issue #2 Minor)
**Location:** Output.cs

**Issue:**
- Critical message detection uses hardcoded string checks
- Could be more configurable for future expansion

**Recommended Fix:**
- Extract to configuration list or enum
- Consider message priority system (Critical, Important, Normal, Spam)

**Effort:** Small (~1 hour)

---

### SurvivalProcessor.Sleep() Data Copy Verbosity
**Source:** Code Review 2025-11-02 (Issue #3 Minor)
**Location:** SurvivalProcessor.cs

**Issue:**
- Manual property copying is verbose
- Could use Clone() pattern or C# records

**Recommended Fix:**
- Consider making SurvivalData a record type
- Or implement ICloneable interface

**Effort:** Small (~30 minutes)

---

### LocationFeature Display Logic Separation
**Source:** Code Review 2025-11-02 (Issue #4 Minor)
**Location:** ActionFactory.cs

**Issue:**
- LocationFeature display logic embedded in ActionFactory
- Violates separation of concerns
- Consider moving to Feature classes or separate formatter

**Recommended Fix:**
- Add `GetDisplayString()` method to LocationFeature interface
- Each feature implements own display logic

**Effort:** Medium (~2 hours)

---

### RecipeBuilder Fluent API Consistency
**Source:** Code Review 2025-11-02 (Issue #5 Minor)
**Location:** RecipeBuilder.cs

**Issue:**
- Some builder methods may not follow consistent patterns
- Review for API consistency

**Recommended Fix:**
- Audit all builder methods
- Ensure consistent naming and return patterns

**Effort:** Small (~1 hour)

---

## 📦 Future Features (Not Urgent)

### Phase 10 Task 48: Crafting Guide (Optional)
**Effort:** Small (~2 hours)
**Status:** Optional task from crafting-foraging-overhaul

**Description:**
- Player-facing reference document
- Material sources by biome (table format)
- Recipe progression trees (visual)
- Early-game survival checklist

**Format Options:**
- Markdown file in project root
- In-game help system (future)
- Wiki-style documentation

---

### Suggestions from SUGGESTIONS.md

**Low Priority QoL Improvements:**
- Add material weight to forage results display (~30 min)
- Configurable message batching thresholds (~1 hour)
- Improved fire warmth visibility in status display (may be done already)

---

## 📋 Completed Sprints (Archive Reference)

### Architecture Review Sprint (2025-11-02)
**Status:** ✅ COMPLETE - All critical issues resolved
**Location:** `dev/active/architecture-review/` (ready to move to complete)
**Outcome:** 8.5/10 grade, approved for merge
**Documentation:** REVIEW-SUMMARY-2025-11-02.md, comprehensive-code-review-2025-11-02.md

### Bug-Fixing Sprint (2025-11-02)
**Status:** ✅ COMPLETE - All tasks done
**Location:** `dev/active/bug-fixing-sprint/` (ready to move to complete)
**Outcome:** 3 bugs fixed (message spam, campfire visibility, crafting transparency)
**Documentation:** SPRINT-SUMMARY.md, bug-fixing-sprint-tasks.md

---

## 🔄 Backlog Review Schedule

**Next Review:** After Phase 10 (Polish) complete
**Review Frequency:** After each major milestone or sprint completion
**Promotion Criteria:**
- High priority items move to active when prerequisites met
- Medium/low priority items batch into refactor sprints
- Future features considered for roadmap planning

---

**Note:** This backlog is a living document. Items may be reprioritized based on:
- User feedback
- Testing discoveries
- Architectural changes
- New feature requirements
