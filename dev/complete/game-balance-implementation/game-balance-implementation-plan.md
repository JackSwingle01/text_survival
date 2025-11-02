# Game Balance Implementation - Comprehensive Plan

**Last Updated**: 2025-11-02
**Status**: Ready for Implementation
**Priority**: CRITICAL - Blocks mid/late-game playtesting

---

## Executive Summary

This plan addresses three cascading balance issues that create unwinnable early-game scenarios:
1. Fire-making RNG death spiral (tool crafting fails waste materials)
2. Resource depletion forces risky travel (insufficient starting resources)
3. Food scarcity prevents reaching hunting (starvation before tool progression)

**Core Solution**: Separate tool crafting (guaranteed) from tool usage (RNG), increase resource density, improve early-game food sources.

**Target Experience**: "Competent play succeeds" - players making reasonable choices should survive day 1 with 70-80% success rate (20-30% acceptable death rate from mistakes/bad luck).

---

## Current State Analysis

### Fire-Making System (Lines 165-221 in CraftingSystem.cs)
- **Current Flow**: Craft Hand Drill → RNG success check → On failure, materials consumed
- **Problem**: `BaseSuccessChance` (30-50%) applies to entire recipe, including material consumption
- **Issue**: Lines 39 and 63-66 consume ingredients BEFORE success check, creating death spiral
- **Death Spiral**: 5 attempts, 3 failures (60%) = ~12 items consumed before material exhaustion

### Resource System (ForageFeature.cs lines 11-36)
- **Current Formula**: `ResourceDensity = baseResourceDensity / (numberOfHoursForaged + 1)`
- **Depletion**: Exponential decrease - 100%, 50%, 33%, 25%, 20%...
- **Starting Location**: ~4 successful forages before hitting 40% density
- **No Respawn**: Once depleted, location permanently exhausted
- **Problem**: Fire failures consume 3-5x planned materials, depleting location before fire established

### Food System (ItemFactory.cs lines 15-56, SurvivalProcessor.cs)
- **Starting Food**: 50% (hardcoded in Player initialization)
- **Consumption**: 14%/hour (from SurvivalProcessor)
- **Timeline**: 50% → 0% in ~3.5 hours
- **Wild Mushroom**: 25 calories = ~1% food restoration (negligible)
- **Problem**: Time to hunting = 3+ hours minimum (fire + tools + hunt), player starves before reaching

### Current Recipe Structure
```csharp
// Fire-making recipes (lines 167-220)
HandDrill: BaseSuccessChance=0.3, SkillCheckDC=0, consumes Wood(0.5) + Tinder(0.05)
BowDrill: BaseSuccessChance=0.5, SkillCheckDC=1, consumes Wood(1.0) + Binding(0.1) + Tinder(0.05)
FlintSteel: BaseSuccessChance=0.9, no DC, consumes Firestarter(0.2) + Stone(0.3) + Tinder(0.05)
```

---

## Proposed Future State

### Fire-Making Redesign
**New Flow**: Craft tool (100% success) → Use tool + tinder (RNG) → Failure consumes tinder/time, not tool

```
Phase 1: Craft Hand Drill (guaranteed, 20 minutes, consumes Wood 0.5)
Phase 2: Use Hand Drill (RNG 30% + skill%, 15 min/attempt, consumes Tinder 0.05 per attempt)
  - Failure: Consume tinder, time, Hand Drill durability -1
  - Success: Start fire, consume tinder + kindling, destroy Hand Drill
  - Tool has 5-10 uses before breaking
```

### Resource Respawn System
- Slow respawn over 1-3 in-game days
- `lastForageTime` tracking in ForageFeature
- `RespawnRate` property (hours to full recovery)
- Starting location: 1.5-2x normal resource density
- Formula: `EffectiveDensity = min(baseDensity, depletedDensity + respawnProgress)`

### Food System Improvements
1. **Starting food**: 50% → 75-80%
2. **Gathered food values**: Mushroom 25cal → 120cal (10% food), Berries 120cal (new)
3. **New food sources**: Eggs (150cal), Grubs (80cal), Nuts (100cal), Roots (100cal - already exists)
4. **Hybrid spawning**: Visible items (berry bushes, nut trees) + forageable (grubs, mushrooms)

---

## Implementation Phases

### Phase 1: Fire-Making System Redesign (Highest Priority)

**Objective**: Eliminate material waste death spiral by separating tool crafting from fire starting

#### 1.1: Create FireMakingTool Item Class
**File**: `Items/FireMakingTool.cs` (new file)
**Effort**: M (1-2 hours)

```csharp
public class FireMakingTool : Item
{
    public int MaxUses { get; set; }
    public int RemainingUses { get; set; }
    public double BaseSuccessChance { get; set; }
    public string RequiredSkill { get; set; }
    public int SkillCheckDC { get; set; }

    // Returns true if tool breaks after use
    public bool UseOnce()
    {
        RemainingUses--;
        return RemainingUses <= 0;
    }
}
```

**Acceptance Criteria**:
- Class inherits from Item with crafting properties
- Tracks durability (RemainingUses / MaxUses)
- Stores skill check parameters for fire starting
- UseOnce() method decrements uses and returns break status

#### 1.2: Add Fire-Making Tool Recipes (Guaranteed Crafting)
**File**: `Crafting/CraftingSystem.cs` lines 165-221
**Effort**: M (2 hours)

**Changes**:
- Remove existing fire recipes (HandDrill, BowDrill, FlintSteel)
- Add tool crafting recipes (no BaseSuccessChance = always succeed):
  - `hand_drill_tool`: Wood(0.5), 20min → Hand Drill Tool (5 uses, 30% base, DC 0)
  - `bow_drill_tool`: Wood(1.0) + Binding(0.1), 45min → Bow Drill Tool (8 uses, 50% base, DC 1)
  - `flint_steel_tool`: Flint(0.2) + Stone(0.3), 5min → Flint & Steel Tool (15 uses, 90% base, no DC)

**Acceptance Criteria**:
- Tool recipes have no BaseSuccessChance (null = 100% success)
- ResultingInItem returns FireMakingTool instances
- Tools display durability in description: "Hand Drill (3/5 uses)"

#### 1.3: Create Fire Starting Action
**File**: `Actions/ActionFactory.cs` - new section in Crafting namespace
**Effort**: L (3-4 hours)

**Implementation**:
```csharp
public static IGameAction StartFire(GameContext ctx)
{
    return CreateAction("Start Fire")
        .When(ctx => {
            // Has fire-making tool in inventory
            var tool = ctx.Player.inventoryManager.Items
                .SelectMany(s => s.Items)
                .OfType<FireMakingTool>()
                .FirstOrDefault();

            // Has tinder
            var hasTinder = ctx.Player.inventoryManager.HasProperty(ItemProperty.Tinder, 0.05);

            // Has kindling
            var hasKindling = ctx.Player.inventoryManager.HasProperty(ItemProperty.Flammable, 0.3);

            return tool != null && hasTinder && hasKindling;
        })
        .Do(ctx => {
            // Get tool
            var tool = ctx.Player.inventoryManager.Items
                .SelectMany(s => s.Items)
                .OfType<FireMakingTool>()
                .First();

            Output.WriteLine($"You attempt to start a fire using your {tool.Name}...");

            // Consume time
            World.Update(15); // 15 minutes per attempt

            // Calculate success chance
            var skill = ctx.Player.Skills.GetSkill(tool.RequiredSkill);
            double successChance = tool.BaseSuccessChance;

            if (tool.SkillCheckDC > 0)
            {
                double skillModifier = (skill.Level - tool.SkillCheckDC) * 0.1;
                successChance += skillModifier;
            }

            successChance = Math.Clamp(successChance, 0.05, 0.95);

            // Roll for success
            bool success = Utils.DetermineSuccess(successChance);

            if (success)
            {
                // SUCCESS: Consume tinder, kindling, destroy tool
                ConsumeProperty(ctx.Player, ItemProperty.Tinder, 0.05);
                ConsumeProperty(ctx.Player, ItemProperty.Flammable, 0.3);
                ctx.Player.inventoryManager.RemoveFromInventory(tool);

                // Create fire
                var fire = new HeatSourceFeature(ctx.Player.CurrentLocation, 10.0);
                fire.AddFuel(0.5);
                ctx.Player.CurrentLocation.Features.Add(fire);

                Output.WriteSuccess($"Success! ({successChance:P0} chance) You've started a fire!");
                ctx.Player.Skills.GetSkill(tool.RequiredSkill).GainExperience(3);
            }
            else
            {
                // FAILURE: Only consume tinder and durability
                ConsumeProperty(ctx.Player, ItemProperty.Tinder, 0.05);
                bool toolBroke = tool.UseOnce();

                if (toolBroke)
                {
                    ctx.Player.inventoryManager.RemoveFromInventory(tool);
                    Output.WriteWarning($"Your {tool.Name} broke after repeated use.");
                }
                else
                {
                    Output.WriteWarning($"Failed to start fire ({successChance:P0} chance). {tool.Name} has {tool.RemainingUses}/{tool.MaxUses} uses left.");
                }

                ctx.Player.Skills.GetSkill(tool.RequiredSkill).GainExperience(1);
            }
        })
        .ThenReturn();
}
```

**Acceptance Criteria**:
- Action visible when player has tool + tinder + kindling
- Failure consumes tinder + tool durability, NOT kindling
- Success consumes tinder + kindling + destroys tool
- Tool durability displayed after failed attempts
- Skill XP granted on both success (3) and failure (1)

#### 1.4: Fix Multiple Campfire Bug
**File**: `Crafting/CraftingSystem.cs` lines 84-89
**Effort**: S (30 minutes)

**Change**:
```csharp
case CraftingResultType.LocationFeature:
    if (recipe.LocationFeatureResult != null)
    {
        // Check for existing HeatSourceFeature
        var existingFire = _player.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (existingFire != null)
        {
            // Add fuel and relight if needed
            existingFire.AddFuel(0.5);
            if (!existingFire.IsActive)
                existingFire.SetActive(true);

            Output.WriteSuccess($"You add fuel to the existing fire.");
        }
        else
        {
            var feature = recipe.LocationFeatureResult.FeatureFactory(_player.CurrentLocation);
            _player.CurrentLocation.Features.Add(feature);
            Output.WriteSuccess($"You successfully built: {recipe.LocationFeatureResult.FeatureName}");
        }
    }
    break;
```

**Acceptance Criteria**:
- Starting fire when one exists adds fuel instead of creating new campfire
- Fire relit if embers still present
- Message indicates fuel added vs new fire created

---

### Phase 2: Resource Balance & Respawn System (High Priority)

**Objective**: Prevent resource exhaustion dead-ends and balance starting location

#### 2.1: Implement Resource Respawn Mechanics
**File**: `Environments/LocationFeatures.cs/ForageFeature.cs`
**Effort**: M (2 hours)

**Changes**:
```csharp
public class ForageFeature(Location location, double resourceDensity = 1) : LocationFeature("forage", location)
{
    private readonly double baseResourceDensity = resourceDensity;
    private double numberOfHoursForaged = 0;
    private DateTime lastForageTime = World.CurrentTime;
    private double respawnRateHours = 48; // 2 days to full respawn
    private Dictionary<Func<Item>, double> resourceAbundance = [];

    private double ResourceDensity
    {
        get
        {
            // Calculate depletion
            double depletedDensity = baseResourceDensity / (numberOfHoursForaged + 1);

            // Calculate respawn
            double hoursSinceLastForage = (World.CurrentTime - lastForageTime).TotalHours;
            double respawnProgress = (hoursSinceLastForage / respawnRateHours) * baseResourceDensity;

            // Effective density = depleted + respawned (capped at base)
            return Math.Min(baseResourceDensity, depletedDensity + respawnProgress);
        }
    }

    public void Forage(double hours)
    {
        // ... existing forage logic ...

        // Update last forage time
        if (itemsFound.Count > 0)
        {
            lastForageTime = World.CurrentTime;
            numberOfHoursForaged += hours;
        }
    }
}
```

**Acceptance Criteria**:
- Resources respawn over 48 hours (2 in-game days)
- Respawn progress proportional to time elapsed
- Density never exceeds original baseResourceDensity
- Respawn visible to player (success rates improve over time)

#### 2.2: Increase Starting Location Resource Density
**File**: `Environments/LocationFactory.cs` or wherever starting location is created
**Effort**: S (30 minutes)

**Changes**:
- Find starting location creation code
- Change ForageFeature density from 1.0 to 1.75 (1.75x normal)
- Verify this supports 5-8 fire attempts before hitting 40% density

**Calculation**:
```
Attempt 1: 1.75/1 = 175% (guaranteed finds)
Attempt 2: 1.75/2 = 87.5%
Attempt 3: 1.75/3 = 58%
Attempt 4: 1.75/4 = 44%
Attempt 5: 1.75/5 = 35%
```

**Acceptance Criteria**:
- Starting location ForageFeature has density 1.75-2.0
- Player can make 4-5 successful forages before dropping below 50%
- Other locations remain at density 1.0 (normal)

#### 2.3: Add Visible Resource Items
**File**: `Environments/LocationFactory.cs` and `Items/ItemFactory.cs`
**Effort**: M (2 hours)

**Implementation**:
- Create ItemFactory methods for visible foods:
  - `MakeBerryBush()` → returns special Item that can be "harvested" (yields 3-5 berries)
  - `MakeNutTree()` → yields 2-3 nuts per harvest
- Add to starting location: 1 berry bush (guaranteed visible)
- Add harvest action to ActionFactory

**Acceptance Criteria**:
- Berry bush visible in starting location description
- Player can harvest berries without RNG (guaranteed 3-5 berries)
- Bush respawns slowly (1 harvest per day)
- Nuts found in forest/hillside locations similarly

---

### Phase 3: Food System Improvements (Medium Priority)

**Objective**: Extend survival timeline to allow fire → hunting progression

#### 3.1: Increase Starting Food Percentage
**File**: `Player.cs` or wherever player initialization occurs
**Effort**: S (15 minutes)

**Change**:
- Find player food initialization (likely `Player` constructor or `World.Initialize()`)
- Change starting food from 50% to 75%

**Acceptance Criteria**:
- New players start with 75% food
- Extends pre-starvation timeline from 3.5h to ~5.3h
- Documented in code comment: "75% starting food = ~5 hours to starvation"

#### 3.2: Improve Gathered Food Values
**File**: `Items/ItemFactory.cs` lines 15-66
**Effort**: S (30 minutes)

**Changes**:
```csharp
public static FoodItem MakeMushroom()
{
    var mushroom = new FoodItem("Wild Mushroom", 120, 5) // Was 25, now 120
    {
        Description = "A forest mushroom. Some varieties are nutritious, others are deadly.",
        Weight = 0.1F
    };
    // ... rest unchanged
}

// Berries already correct at 120 calories

public static FoodItem MakeNuts()
{
    var item = new FoodItem("Wild Nuts", 100, 20)
    {
        Description = "Nutritious nuts gathered from trees. Excellent source of fat and protein.",
        Weight = 0.15F,
        CraftingProperties = [ItemProperty.PlantFiber] // Can be crushed for oil
    };
    return item;
}

public static FoodItem MakeEggs()
{
    var item = new FoodItem("Bird Eggs", 150, 50)
    {
        Description = "Small bird eggs found in a nest. Fragile but nutritious.",
        Weight = 0.2F,
        CraftingProperties = [ItemProperty.RawMeat] // Can be cooked
    };
    return item;
}

public static FoodItem MakeGrubs()
{
    var item = new FoodItem("Fat Grubs", 80, 10)
    {
        Description = "Large beetle grubs found in rotting wood. High in fat and protein.",
        Weight = 0.05F,
        CraftingProperties = [ItemProperty.RawMeat]
    };
    return item;
}
```

**Food Value Summary**:
- Mushroom: 120cal = 10% food (vs 1% before) ✓
- Berries: 120cal = 10% food ✓
- Nuts: 100cal = 8% food (new)
- Eggs: 150cal = 12% food (new)
- Grubs: 80cal = 7% food (new)
- Roots: 100cal = 8% food (existing)

**Acceptance Criteria**:
- All gathered foods provide 7-12% food restoration
- Gathered food is viable stopgap strategy (not long-term solution)
- Hunting still incentivized (meat provides 30-50% per meal)

#### 3.3: Add New Food Sources to Forage Tables
**File**: `Environments/LocationFactory.cs` (wherever ForageFeatures are initialized)
**Effort**: M (1 hour)

**Changes**:
- Add to Forest starting location:
  - Mushroom: abundance 0.6 (60% per hour initially)
  - Berries: abundance 0.4 (via visible bush + forage)
  - Nuts: abundance 0.3
  - Grubs: abundance 0.4
  - Eggs: abundance 0.2 (rarer)
- Adjust stick/wood abundance if needed to maintain material availability

**Acceptance Criteria**:
- Player has 60-70% chance to find SOME food per 15-min forage
- Food variety increases (not just mushrooms)
- Materials still available for tools/fire (don't crowd out)

---

### Phase 4: Documentation & Validation

**Objective**: Document changes and validate through playtesting

#### 4.1: Update Documentation
**Files**:
- `documentation/fire-management-system.md`
- `documentation/crafting-system.md`
- `documentation/skill-check-system.md`
**Effort**: M (1-2 hours)

**Updates**:
- Fire management: Document tool durability system, separate crafting/usage
- Crafting: Update fire-making examples
- Skill checks: Clarify when checks apply (usage, not crafting)

#### 4.2: Write Balance Specification Document
**File**: `dev/active/game-balance-implementation/balance-specification.md` (new)
**Effort**: M (2 hours)

**Contents**:
- Executive summary of changes
- Before/after comparisons (success rates, timelines)
- Mathematical modeling (spreadsheet-style probability tables)
- Validation criteria for playtesting
- Industry research summary (The Long Dark, RimWorld, Don't Starve)

#### 4.3: Create Playtesting Protocol
**File**: `dev/active/game-balance-implementation/playtest-protocol.md` (new)
**Effort**: S (1 hour)

**Contents**:
- Step-by-step playtest procedure
- Metrics to track (time to fire, forages needed, food at hunting, death rate)
- Success criteria (4/5 reach fire, 3/5 reach hunt, 70-80% survival)
- Issue reporting format

#### 4.4: Run Validation Playtests
**Effort**: L (3-4 hours for 5 playtests)

**Process**:
1. Run 5 full playtests from game start to first successful hunt
2. Track metrics: time to fire, material consumption, food level, survival
3. Document issues in ISSUES.md
4. Compare to success criteria

**Success Criteria**:
- 4/5 playtests reach established fire
- 3/5 playtests reach first successful hunt
- 3-4/5 playtests survive to 6+ hours
- No new death spirals or unwinnable states discovered

---

## Risk Assessment & Mitigation

### Risk 1: Tool Durability Too Generous/Stingy
**Likelihood**: Medium
**Impact**: Medium
**Mitigation**:
- Start conservatively (Hand Drill 5 uses, Bow Drill 8 uses)
- Adjust based on playtest feedback
- Document in balance spec for easy tuning

### Risk 2: Respawn Rate Incorrect
**Likelihood**: Medium
**Impact**: Low
**Mitigation**:
- 48-hour respawn is starting point, easy to adjust
- Players have multiple locations to forage
- Too fast = no exploration pressure; too slow = dead-ends return

### Risk 3: Food Changes Make Game Too Easy
**Likelihood**: Low
**Impact**: Medium
**Mitigation**:
- Keep food consumption rate at 14%/hour (time pressure maintained)
- Gathered food is stopgap (7-12%) vs hunting (30-50%)
- Monitor survival rate in playtests (target 70-80%, not 95%+)

### Risk 4: Fire-Making Action UX Issues
**Likelihood**: Medium
**Impact**: Medium
**Mitigation**:
- Clear messaging on tool durability
- Separate actions: "Craft Tool" vs "Start Fire"
- Tutorial text explaining new system
- Test thoroughly before full implementation

### Risk 5: Breaking Existing Saves
**Likelihood**: High (fire recipes changed)
**Impact**: Low (early development)
**Mitigation**:
- No save system currently exists (console game, session-based)
- If adding saves later, include migration logic
- Document breaking changes in CURRENT-STATUS.md

---

## Success Metrics

### Quantitative Metrics
1. **Fire Success Rate**: 80% of playtests establish fire within 3 attempts
2. **Material Sufficiency**: Starting location supports 5-8 fire attempts before depletion
3. **Food Timeline**: Players survive 5-6 hours with gathering (up from 3.5h)
4. **Hunting Accessibility**: 60% of playtests reach first successful hunt
5. **Overall Survival**: 70-80% of competent playtests survive to 6+ hours

### Qualitative Metrics
1. **Death Attribution**: Deaths feel like consequence of choices, not RNG
2. **Learning Curve**: Failures teach players, not punish arbitrarily
3. **Tension Maintained**: Game still challenging, not trivial
4. **Strategic Depth**: Resource management still matters
5. **Player Agency**: Players feel in control of their survival

---

## Required Resources & Dependencies

### Development Resources
- **Time Estimate**: 15-20 hours total
  - Phase 1 (Fire): 6-8 hours
  - Phase 2 (Resources): 4-5 hours
  - Phase 3 (Food): 2-3 hours
  - Phase 4 (Docs/Testing): 3-4 hours

### Technical Dependencies
- `World.CurrentTime` (DateTime) must be accessible from ForageFeature
- `ItemProperty.Tinder` and `.Flammable` must be distinct properties
- `InventoryManager.HasProperty()` method for checking property quantities
- No external library dependencies

### Knowledge Dependencies
- Understanding of skill check formula (30% base + 10%/level)
- ForageFeature depletion curve mechanics
- SurvivalProcessor food consumption rates
- Action builder pattern (ActionFactory)
- Recipe builder pattern (RecipeBuilder)

---

## Timeline Estimate

### Week 1: Core Fire System (8 hours)
- Day 1-2: Implement FireMakingTool class and recipes (4h)
- Day 3: Create StartFire action (3h)
- Day 4: Fix multiple campfire bug, initial testing (1h)

### Week 2: Resources & Food (6 hours)
- Day 5: Implement respawn mechanics (2h)
- Day 6: Adjust starting location density, add visible items (2h)
- Day 7: Update food values and forage tables (2h)

### Week 3: Documentation & Validation (6 hours)
- Day 8: Update documentation, write balance spec (3h)
- Day 9-10: Run 5 validation playtests, analyze results (3h)

**Total Time**: 20 hours over 10 days (2h/day average)

---

## Implementation Notes

### Code Style & Patterns
- Follow existing ActionBuilder pattern for StartFire action
- Use RecipeBuilder for tool crafting recipes
- Maintain pure function approach in SurvivalProcessor (no changes needed there)
- Property-based crafting system stays intact

### Testing Strategy
- Manual playtesting (no unit test infrastructure currently)
- Use TEST_MODE=1 for automated playtest runs
- Track metrics in spreadsheet or markdown table
- Document edge cases in ISSUES.md

### Rollback Plan
- Git branch for all changes: `feature/game-balance-fixes`
- Each phase gets separate commits for easy revert
- If validation fails, revert Phase 3 first (least critical)
- Phase 1 (fire) is foundation - must succeed or redesign needed

---

## Next Steps After Completion

1. **Monitor Player Feedback**: Gather feedback on difficulty feel
2. **Iterate on Numbers**: Tune tool durability, respawn rates, food values
3. **Extend to Other Biomes**: Apply resource balance to Plains, Hillside, Cave
4. **Advanced Food Systems**: Cooking bonuses, spoilage mechanics
5. **Skill Progression Tuning**: Adjust XP rates if needed

---

## Appendix: Mathematical Modeling Summary

### Fire-Making Probability Analysis

**Current System (before fix)**:
- Hand Drill: 30% success per attempt
- Expected attempts to success: 1/0.3 = 3.33 attempts
- Probability of 5+ attempts: 16.8%
- Material cost at 5 attempts: ~2.5 wood + 0.25 tinder

**New System (after fix)**:
- Tool crafting: 100% success, 0.5 wood
- Fire starting: 30% per attempt (same)
- Expected attempts: 3.33 (same)
- Material cost at 5 attempts: 0.5 wood (tool) + 0.25 tinder (attempts) = 0.75 total
- Reduction: 70% less material waste

### Resource Depletion Timeline

**Current System**:
```
Forage 1: 100% density (guaranteed finds)
Forage 2: 50% density
Forage 3: 33% density (critical threshold)
Forage 4: 25% density (very depleted)
```

**New System (1.75x starting density)**:
```
Forage 1: 175% density (guaranteed multiple finds)
Forage 2: 87.5% density
Forage 3: 58% density
Forage 4: 44% density (still viable)
Forage 5: 35% density (critical threshold)
```

**Result**: 2-3 additional successful forages before depletion

### Food Survival Timeline

**Current System**:
- Starting: 50%
- Consumption: 14%/hour
- Time to 0%: 50/14 = 3.57 hours
- Wild Mushroom: +1% (negligible)

**New System**:
- Starting: 75%
- Consumption: 14%/hour (unchanged)
- Time to 0%: 75/14 = 5.36 hours
- Gathered foods: +8-12% per item
- 3 successful forages: +~30% food → extends by 2+ hours

**Result**: 5-6 hour survival timeline vs 3.5 hours (57% increase)

---

**End of Plan Document**
