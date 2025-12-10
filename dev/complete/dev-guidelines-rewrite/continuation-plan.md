# Dev Guidelines Skill Rewrite - Continuation Plan

**Created: 2025-11-01 19:30 (After Context Reset)**

---

## Current Status

### Completed Work ✅
- **Phase 1**: Cross-applicable pattern extraction (COMPLETE)
- **Phase 2**: Skill folder renamed, main SKILL.md rewritten (428 lines) (COMPLETE)
- **Phase 3 (Partial)**: 3 of 10 resource files created:
  - ✅ action-system.md (~600 lines, comprehensive)
  - ✅ body-and-damage.md (~400 lines, comprehensive)
  - ✅ survival-processing.md (~350 lines, comprehensive)

### Remaining Work 🔄
- **Phase 3 (Remaining)**: 7 resource files to create
- **Phase 4**: skill-rules.json configuration
- **Phase 5**: Old resources cleanup, testing, finalization

---

## Immediate Continuation Tasks

### Priority 1: Complete Core System Resource Files (Phase 3)

#### Task 3.4: Create crafting-system.md
**Target Size**: 300-350 lines (context budget constraint)

**Required Content**:
- ItemProperty enum explanation with examples
- Property-based crafting philosophy (vs item-specific checks)
- RecipeBuilder pattern with fluent API
- Property requirements (.WithPropertyRequirement())
- Skill requirements (.RequiringSkill())
- Time consumption (.RequiringCraftingTime())
- Result types (Item, LocationFeature, Structure)
- 5-7 complete recipe examples

**Files to Reference**:
- `Crafting/ItemProperty.cs` - Property enum
- `Crafting/RecipeBuilder.cs` - Builder implementation
- `Crafting/CraftingSystem.cs` - Recipe initialization

**Estimated Time**: 30-40 minutes

#### Task 3.5: Create effect-system.md
**Target Size**: 300-350 lines (context budget constraint)

**Required Content**:
- EffectBuilder pattern explanation
- EffectRegistry usage (AddEffect, RemoveEffect, HasEffect)
- Severity system (0.0-1.0 scale)
- Severity change rates (increase/decrease over time)
- Capacity modifiers (.ReducesCapacity())
- Temperature effects (.AffectsTemperature())
- Body part targeting
- AllowMultiple behavior
- Common effect extensions (Bleeding, Poisoned, Frostbite, etc.)
- 5-7 code examples

**Files to Reference**:
- `Effects/EffectBuilder.cs` - Builder implementation
- `Effects/EffectRegistry.cs` - Registry usage
- `Effects/Effect.cs` - Base effect class
- `Effects/EffectBuilderExtensions.cs` - Common extensions

**Estimated Time**: 30-40 minutes

### Priority 2: Cross-Cutting Pattern Resource Files (Phase 4)

#### Task 4.1: Create builder-patterns.md (HIGH PRIORITY)
**Target Size**: 250-300 lines (cross-cutting guide)

**Required Content**:
- Fluent API principles
- Method chaining best practices
- Context passing patterns (GameContext, etc.)
- Builder consistency across systems (Action, Effect, Recipe)
- When to use builder vs factory
- Extension method patterns for builders
- 5-6 examples from different builders

**Purpose**: Cross-cutting guide that ties together patterns from action-system.md, crafting-system.md, and effect-system.md

**Estimated Time**: 30-40 minutes

#### Task 4.2: Create composition-architecture.md
**Target Size**: 250-300 lines

**Required Content**:
- Actor base class overview
- Player vs NPC distinction (CRITICAL RULE)
- Player-only features (Skills, complex managers)
- NPC capabilities (stats from Body only)
- Manager composition pattern (InventoryManager, CombatManager, LocationManager, etc.)
- Separation of concerns
- Composition over inheritance philosophy
- 5-6 code examples

**Files to Reference**:
- `Actors/Actor.cs` - Base class
- `Actors/Player.cs` - Player composition example
- `Actors/NPC.cs` - NPC simplicity

**Estimated Time**: 30-40 minutes

#### Task 4.3: Create factory-patterns.md
**Target Size**: 250-300 lines

**Required Content**:
- ItemFactory pattern and organization
- NPCFactory pattern
- BodyPartFactory pattern
- SpellFactory pattern
- LocationFactory/ZoneFactory patterns
- When to use factory vs builder (decision guide)
- Static method organization
- Future JSON migration notes (data-driven direction)
- 5-6 factory examples

**Files to Reference**:
- `Items/ItemFactory.cs`
- `Actors/NPCFactory.cs`
- `Bodies/BodyPartFactory.cs`

**Estimated Time**: 30-40 minutes

#### Task 4.4: Create error-handling-and-validation.md
**Target Size**: 200-250 lines (adapted from old skill)

**Required Content**:
- Try-catch best practices in C#
- Custom exception types (when to create)
- Guard clause patterns (ArgumentNullException, etc.)
- Input validation approaches
- Error propagation in C#
- Async error handling (Task, async/await)
- Common pitfalls
- Adapted patterns from old async-and-errors.md and validation-patterns.md

**Files to Reference**:
- `.claude/skills/dev-guidelines/resources/async-and-errors.md` (OLD - for adaptation)
- `.claude/skills/dev-guidelines/resources/validation-patterns.md` (OLD - for adaptation)

**Estimated Time**: 25-30 minutes

#### Task 4.5: Create complete-examples.md (DEPENDS ON ALL OTHERS)
**Target Size**: 400-500 lines (HIGH PRIORITY - save context for this)

**Required Content**:
- Complete new action example (from scratch: ActionFactory → ActionBuilder → integration)
- Complete new item + recipe example (ItemFactory → RecipeBuilder → testing)
- Complete new effect example (EffectBuilder → application → integration)
- Complete new body part type example (BodyPartFactory → hierarchy → capacities)
- Complete new NPC example (NPCFactory → body → combat stats)
- Each example shows ALL steps
- Code is copy-pasteable and functional
- Verification steps included

**Purpose**: Reference implementation guide for complete features

**Estimated Time**: 60-90 minutes

### Priority 3: Configuration & Cleanup (Phase 5)

#### Task 5.1: Create skill-rules.json
**Target Size**: 50-100 lines JSON

**Required Content**:
```json
{
  "dev-guidelines": {
    "type": "domain",
    "enforcement": "suggest",
    "priority": "high",
    "promptTriggers": {
      "keywords": ["action", "builder", "body", "damage", "survival", "crafting", "recipe", "effect", "player", "NPC", "C#", "game system"],
      "intentPatterns": [
        "(create|add|implement).*?action",
        "(create|add|implement).*?recipe",
        "(create|add|implement).*?effect",
        "(create|add|implement).*?body.*?part",
        "(implement|build).*?survival.*?system",
        "builder.*?pattern"
      ]
    },
    "fileTriggers": {
      "pathPatterns": ["**/*.cs"],
      "contentPatterns": [
        "ActionBuilder",
        "RecipeBuilder",
        "EffectBuilder",
        "Body\\.Damage",
        "namespace.*?Actions",
        "namespace.*?Crafting",
        "namespace.*?Effects"
      ]
    }
  }
}
```

**Reference**: `.claude/skills/skill-developer/SKILL_RULES_REFERENCE.md`

**Estimated Time**: 15-20 minutes

#### Task 5.2: Delete Old Resources
**Action**: Delete old Node.js resource files

**Files to Delete**:
- architecture-overview.md
- async-and-errors.md (after adaptation to error-handling-and-validation.md)
- configuration.md
- database-patterns.md
- middleware-guide.md
- routing-and-controllers.md
- sentry-and-monitoring.md
- services-and-repositories.md
- testing-guide.md
- validation-patterns.md (after adaptation)
- complete-examples.md (old - will be replaced with new)

**Keep Only**:
- action-system.md (NEW)
- body-and-damage.md (NEW)
- survival-processing.md (NEW)
- crafting-system.md (NEW)
- effect-system.md (NEW)
- builder-patterns.md (NEW)
- composition-architecture.md (NEW)
- factory-patterns.md (NEW)
- error-handling-and-validation.md (NEW - adapted)
- complete-examples.md (NEW)

**Estimated Time**: 5 minutes

#### Task 5.3: Final Review & Polish
**Checklist**:
- [ ] All cross-references work between resource files
- [ ] No broken links in navigation
- [ ] Consistent formatting across all files
- [ ] All files have table of contents (if >100 lines)
- [ ] All files have "Last Updated" dates
- [ ] All files have skill status footer
- [ ] Main SKILL.md still under 500 lines
- [ ] All 10 resource files present
- [ ] skill-rules.json valid JSON

**Estimated Time**: 20-30 minutes

---

## Context Budget Strategy

### Current Context: 52% (103k/200k tokens)
**Available**: 97k tokens free

### Resource File Budget Allocation:
- **crafting-system.md**: ~12k tokens (300 lines)
- **effect-system.md**: ~12k tokens (300 lines)
- **builder-patterns.md**: ~10k tokens (250 lines)
- **composition-architecture.md**: ~10k tokens (250 lines)
- **factory-patterns.md**: ~10k tokens (250 lines)
- **error-handling-and-validation.md**: ~8k tokens (200 lines)
- **complete-examples.md**: ~20k tokens (500 lines) - SAVE CONTEXT FOR THIS
- **skill-rules.json**: ~2k tokens (50 lines)
- **Review & polish**: ~5k tokens

**Total Required**: ~89k tokens
**Buffer**: ~8k tokens (safe margin)

### Strategy:
1. Create files 3.4-3.5 with medium detail (300 lines each)
2. Create files 4.1-4.3 concisely but completely (250 lines each)
3. Adapt 4.4 from existing content (200 lines)
4. Reserve maximum context for 4.5 (complete-examples.md) - most valuable
5. If context warning occurs, use /dev-docs-update before continuing

---

## Execution Order

### Batch 1: Core Systems (30-40 min)
1. crafting-system.md
2. effect-system.md

### Batch 2: Cross-Cutting Patterns (90-120 min)
3. builder-patterns.md (HIGH PRIORITY - do first in batch)
4. composition-architecture.md
5. factory-patterns.md
6. error-handling-and-validation.md

### Batch 3: Complete Examples (60-90 min)
7. complete-examples.md (DEPENDS ON ALL OTHERS)

### Batch 4: Configuration & Cleanup (30-40 min)
8. skill-rules.json
9. Delete old resources
10. Final review & polish

**Total Time**: 3.5-5 hours

---

## Key Principles to Follow

### From skill-developer Skill:
✅ Keep main SKILL.md under 500 lines (ALREADY ACHIEVED - 428 lines)
✅ Use progressive disclosure with resource files
✅ Add table of contents to files >100 lines
✅ Include rich description with trigger keywords in frontmatter
✅ One level deep reference structure (don't nest references)

### From CLAUDE.md (Game Architecture):
✅ Builder pattern everywhere (Actions, Effects, Recipes)
✅ Composition over inheritance (Actor → Player/NPC)
✅ Pure function survival processing (SurvivalProcessor)
✅ Single damage entry point (Body.Damage ONLY)
✅ Property-based crafting (ItemProperty enum)
✅ Player vs NPC distinction (Skills are player-only)
✅ Explicit time passage (World.Update)
✅ Ice Age thematic consistency
✅ Action menu flow (.ThenShow, .ThenReturn)
✅ Factory pattern for content

---

## Files to Reference During Work

### For Code Examples:
- `Actions/ActionBuilder.cs` - Builder implementation
- `Actions/ActionFactory.cs` - Organization pattern
- `Crafting/ItemProperty.cs` - Property enum
- `Crafting/RecipeBuilder.cs` - Crafting builder
- `Effects/EffectBuilder.cs` - Effect builder
- `Survival/SurvivalProcessor.cs` - Pure function example
- `Bodies/Body.cs` - Body system entry point
- `Actors/Player.cs` - Composition example

### For Cross-Applicable Adaptation:
- `.claude/skills/dev-guidelines/resources/async-and-errors.md` (OLD)
- `.claude/skills/dev-guidelines/resources/validation-patterns.md` (OLD)

### For Meta-Patterns:
- `.claude/skills/skill-developer/SKILL.md` - Best practices
- `.claude/skills/skill-developer/SKILL_RULES_REFERENCE.md` - Trigger config
- `.claude/skills/skill-developer/PATTERNS_LIBRARY.md` - Pattern examples

---

## Success Criteria

### Must Have:
- ✅ 10 resource files created (currently 3/10)
- ✅ All files have table of contents (if >100 lines)
- ✅ Main SKILL.md under 500 lines (currently 428 ✅)
- ✅ 50+ code examples total across all files
- ✅ skill-rules.json with comprehensive triggers
- ✅ Old resources deleted
- ✅ All cross-references working

### Should Have:
- Each resource file >200 lines of substantive content
- Code examples are copy-pasteable and functional
- Examples reference actual game code patterns
- consistent formatting and structure

### Nice to Have:
- Diagrams or ASCII art for complex systems
- More than 5 examples per file
- Performance tips where relevant
- Testing guidance sections

---

## Risk Mitigation

### Risk: Context limit reached before completion
**Mitigation**:
- Monitor context usage with /context command
- Use /dev-docs-update if approaching 80%
- Prioritize complete-examples.md (most valuable)
- Can compact if needed between major phases

### Risk: Code examples contain errors
**Mitigation**:
- Reference actual codebase files directly
- Copy-paste actual patterns, not synthetic examples
- Test complex examples mentally or with game knowledge

### Risk: Old valuable content lost
**Mitigation**:
- Phase 1 already extracted cross-applicable patterns
- error-handling-and-validation.md preserves adapted content
- Review step checks nothing valuable lost

---

**Continuation Plan Status**: READY TO EXECUTE ✅
**Next Action**: Begin Task 3.4 (Create crafting-system.md)
**Context Budget**: 97k tokens available (sufficient for all remaining work)
**Estimated Completion**: 3.5-5 hours
