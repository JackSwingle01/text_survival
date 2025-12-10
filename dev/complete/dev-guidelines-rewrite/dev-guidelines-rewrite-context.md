# Dev Guidelines Rewrite - Context & Key Information

**Last Updated: 2025-11-01 19:50 (TASK COMPLETE)**

---

## Current Session Status - FINAL

### Implementation Progress (100% COMPLETE)
- ✅ **Phase 1 Complete**: Extracted cross-applicable patterns
- ✅ **Phase 2 Complete**: Renamed folder, rewrote main SKILL.md (427 lines)
- ✅ **Phase 3 Complete**: All 10 resource files created
  - ✅ action-system.md (~600 lines)
  - ✅ body-and-damage.md (~400 lines)
  - ✅ survival-processing.md (~350 lines)
  - ✅ crafting-system.md (~500 lines)
  - ✅ effect-system.md (~600 lines)
  - ✅ builder-patterns.md (~450 lines)
  - ✅ composition-architecture.md (~450 lines)
  - ✅ factory-patterns.md (~400 lines)
  - ✅ error-handling-and-validation.md (~350 lines)
  - ✅ complete-examples.md (~150 lines)
- ✅ **Phase 4 Complete**: Configuration files created
  - ✅ skill-rules.json (valid JSON, comprehensive triggers)
- ✅ **Phase 5 Complete**: Cleanup and documentation
  - ✅ Old Node.js resources deleted (10 files)
  - ✅ CLAUDE.md updated with living document instructions
  - ✅ All cross-references verified

### Files Created/Modified This Session (ALL COMPLETE)

**Main Skill Files**:
1. ✅ `.claude/skills/dev-guidelines/SKILL.md` - Rewritten (427 lines, C# focused, under 500 limit)
2. ✅ `.claude/skills/dev-guidelines/skill-rules.json` - Created with comprehensive triggers

**Resource Files (All New)**:
1. ✅ `action-system.md` - ActionBuilder, menu flow (~600 lines)
2. ✅ `body-and-damage.md` - Body hierarchy, damage system (~400 lines)
3. ✅ `survival-processing.md` - Pure function processing (~350 lines)
4. ✅ `crafting-system.md` - Property-based recipes (~500 lines)
5. ✅ `effect-system.md` - Status effects, buffs/debuffs (~600 lines)
6. ✅ `builder-patterns.md` - Cross-cutting builder guide (~450 lines)
7. ✅ `composition-architecture.md` - Actor/Player/NPC patterns (~450 lines)
8. ✅ `factory-patterns.md` - Content creation (~400 lines)
9. ✅ `error-handling-and-validation.md` - C# exceptions (~350 lines)
10. ✅ `complete-examples.md` - Full feature examples (~150 lines)

**Documentation Updates**:
- ✅ `CLAUDE.md` - Added living document section with skill instructions

**Cleanup**:
- ✅ Deleted 10 old Node.js resource files (architecture-overview.md, async-and-errors.md, configuration.md, database-patterns.md, middleware-guide.md, routing-and-controllers.md, sentry-and-monitoring.md, services-and-repositories.md, testing-guide.md, validation-patterns.md)

### Key Decisions Made This Session
1. **Main SKILL.md structure**: 10 core principles, 10 resource files, 427 lines (under 500 limit ✅)
2. **Resource file depth**: First 3 files comprehensive (~400-600 lines), remaining files balanced (~200-450 lines)
3. **Context management**: Successfully created all 10 files within context budget (57% usage at completion)
4. **Cross-applicable content preserved**: Error handling and validation patterns adapted from Node.js skill to C#
5. **Code examples**: Used actual codebase examples (ActionFactory.cs, ActionBuilder.cs, ItemProperty.cs, RecipeBuilder.cs, EffectBuilder.cs, etc.)
6. **complete-examples.md**: Kept concise (~150 lines) due to context constraints, focuses on key patterns
7. **Living document approach**: Added explicit instructions to CLAUDE.md about keeping skill synchronized

---

## Project Context

### What We're Building
Transforming a Node.js/TypeScript backend development skill into a C# game development skill for the Text Survival RPG project.

### Why This Matters
The existing `backend-dev-guidelines` skill is completely misaligned with this codebase's technology and architecture. It provides guidance for Express microservices when we need guidance for a console-based survival game with unique architectural patterns.

---

## Key Files and Locations

### Source Files (Read-Only References)

#### Existing Skill
- `.claude/skills/backend-dev-guidelines/SKILL.md` - Structure template
- `.claude/skills/backend-dev-guidelines/resources/async-and-errors.md` - Cross-applicable error patterns
- `.claude/skills/backend-dev-guidelines/resources/validation-patterns.md` - Validation concepts to adapt

#### Meta-Documentation
- `.claude/skills/skill-developer/SKILL.md` - How to write good skills
- `.claude/skills/skill-developer/PATTERNS_LIBRARY.md` - Pattern examples
- `.claude/skills/skill-developer/SKILL_RULES_REFERENCE.md` - Trigger configuration guide

#### Game Architecture
- `CLAUDE.md` - Complete architectural overview
- `ActionFactory.cs` - Action system implementation
- `ActionBuilder.cs` - Builder pattern example
- `Body.cs` - Body system core
- `DamageProcessor.cs` - Damage system
- `SurvivalProcessor.cs` - Survival processing
- `RecipeBuilder.cs` - Crafting system
- `EffectBuilder.cs` - Effect system
- `Player.cs` - Player composition example
- `Actor.cs` - Base actor class

### Target Files (To Be Created/Modified)

#### Main Skill Files
- `.claude/skills/dev-guidelines/SKILL.md` - Main skill file (REWRITE)
- `.claude/skills/dev-guidelines/skill-rules.json` - Trigger config (CREATE)

#### Resource Files (All NEW)
- `.claude/skills/dev-guidelines/resources/action-system.md`
- `.claude/skills/dev-guidelines/resources/body-and-damage.md`
- `.claude/skills/dev-guidelines/resources/survival-processing.md`
- `.claude/skills/dev-guidelines/resources/crafting-system.md`
- `.claude/skills/dev-guidelines/resources/effect-system.md`
- `.claude/skills/dev-guidelines/resources/builder-patterns.md`
- `.claude/skills/dev-guidelines/resources/composition-architecture.md`
- `.claude/skills/dev-guidelines/resources/factory-patterns.md`
- `.claude/skills/dev-guidelines/resources/error-handling-and-validation.md`
- `.claude/skills/dev-guidelines/resources/complete-examples.md`

---

## Architecture Decisions

### Decision 1: Keep 10 Resource Files
**Rationale**:
- Existing skill has 11, working well
- 10 provides good coverage without overwhelming
- Progressive disclosure best practice
- Each file focuses on specific system

**Alternatives Considered**:
- Fewer files (8) - Too broad, harder to navigate
- More files (12+) - Too fragmented

### Decision 2: Preserve Cross-Applicable Patterns
**Rationale**:
- Error handling principles are universal
- Validation concepts apply to C# too
- Testing approaches translate
- SOLID principles are language-agnostic

**Implementation**:
- Create error-handling-and-validation.md
- Adapt patterns from async-and-errors.md and validation-patterns.md
- Use C# syntax instead of TypeScript

### Decision 3: Emphasize Builder Pattern
**Rationale**:
- Builder pattern is THE dominant pattern in this codebase
- ActionBuilder, EffectBuilder, RecipeBuilder all use it
- Fluent API consistency across systems
- Warrants dedicated resource file

**Implementation**:
- Create builder-patterns.md for cross-cutting guidance
- Reference in individual system files
- Show consistency across different builders

### Decision 4: Player vs NPC Distinction Gets Dedicated Section
**Rationale**:
- Critical architectural rule (skills are player-only)
- Frequently violated anti-pattern
- Requires composition-architecture.md

**Implementation**:
- composition-architecture.md explains Actor/Player/NPC
- Emphasizes manager pattern
- Shows proper separation of concerns

### Decision 5: Ice Age Thematic Guidance Not Separate File
**Rationale**:
- While important, not complex enough for dedicated file
- Better as section in SKILL.md Core Principles
- Can reference in complete-examples.md

**Implementation**:
- Include in Core Principles (#10)
- Show in naming examples
- Emphasize in complete-examples.md

---

## Key Principles to Document

### From CLAUDE.md (Must Be Emphasized)

1. **Builder Pattern Everywhere** - Actions, Effects, Recipes all use fluent builders
2. **Composition Over Inheritance** - Player/NPC use managers, not deep hierarchies
3. **Pure Functions for Processing** - SurvivalProcessor is stateless
4. **Single Damage Entry Point** - Only Body.Damage(), never direct modification
5. **Property-Based Crafting** - Items contribute via properties, not item-specific checks
6. **Action Menu Flow** - Use .ThenShow() and .ThenReturn(), never manual loops
7. **Player vs NPC Distinction** - Skills are player-only, NPCs derive from body
8. **Explicit Time Passage** - Actions must call World.Update(minutes)
9. **Ice Age Thematic Consistency** - Use period-appropriate names (flint, hide, bone)
10. **Factory Pattern for Content** - ItemFactory, NPCFactory, etc.

### Cross-Applicable from Old Skill

1. **Try-Catch Best Practices** - Always wrap risky operations
2. **Error Propagation** - Let errors bubble up appropriately
3. **Input Validation** - Validate at boundaries (guard clauses)
4. **Separation of Concerns** - Each class/method has one responsibility
5. **Code Organization** - Consistent file structure and naming
6. **Testing** - Unit test business logic, integration test systems

---

## Dependencies and Requirements

### Knowledge Dependencies
- Understanding of C# .NET 9.0 syntax
- Familiarity with builder pattern
- Understanding of composition over inheritance
- Game architecture knowledge from CLAUDE.md

### File Dependencies
- CLAUDE.md must exist (it does) - source of truth
- Game source code must be readable (it is)
- skill-developer skill must exist (it does)

### Technical Dependencies
- None - this is documentation only
- No code changes required
- No external tools needed

---

## Risks and Constraints

### Constraints
1. **500 Line Limit** - Main SKILL.md must stay under 500 lines
2. **Anthropic Best Practices** - Must follow skill-developer guidelines
3. **Accuracy** - Code examples must match actual game patterns
4. **Completeness** - Must cover all 6 core systems

### Risks
See Risk Assessment section in main plan document

---

## Success Criteria

### Must Have
- ✅ Skill activates on C# file edits
- ✅ All 10 resource files created
- ✅ SKILL.md under 500 lines
- ✅ 50+ code examples total
- ✅ All cross-references working
- ✅ Cross-applicable patterns preserved

### Should Have
- Each resource file >200 lines
- Table of contents on files >100 lines
- Code examples are copy-pasteable
- Examples reference actual game code

### Nice to Have
- Diagrams or ASCII art for architecture
- More than 5 examples per file
- Testing guidance section
- Performance tips

---

## Open Questions

### Resolved
- ✅ Should we have 10 or 11 resource files? → 10 (thematic guidance integrated)
- ✅ Should error handling be separate file? → Yes, adapted from old skill
- ✅ Do we need builder-patterns.md? → Yes, builder pattern is critical

### Unresolved
- None currently

---

## References

### Anthropic Documentation
- skill-developer SKILL.md - Main guidelines
- PATTERNS_LIBRARY.md - Pattern examples
- SKILL_RULES_REFERENCE.md - Trigger config

### Game Documentation
- CLAUDE.md - Architecture overview
- README.md - Project overview

### Code Examples to Reference
- ActionFactory.cs - Action organization
- ActionBuilder.cs - Builder implementation
- ActionBuilderExtensions.cs - Extension methods
- RecipeBuilder.cs - Crafting builder
- EffectBuilder.cs - Effect builder
- Player.cs - Composition example
- SurvivalProcessor.cs - Pure function example

---

## Notes for Future Reference

### When Updating This Skill Later
1. Check CLAUDE.md for any architectural changes
2. Review new game systems added
3. Update code examples if APIs changed
4. Add new anti-patterns discovered
5. Expand complete-examples.md with new features

### Maintenance Indicators
- If CLAUDE.md changes significantly → Review all resource files
- If new core system added → May need new resource file
- If builder pattern changes → Update builder-patterns.md
- If C# version upgrades → Review syntax in examples

---

### Task Completion Summary

**Status**: ✅ COMPLETE - All phases finished
**Total Time**: ~3.5 hours across 2 sessions
**Lines Written**: ~4,700 lines of documentation
**Code Examples**: 60+ complete examples
**Validation**: JSON valid, line counts verified, all files present

**Key Achievements**:
- Transformed Node.js backend skill into C# game development skill
- Preserved universal software principles while adapting to game architecture
- Created comprehensive documentation following Anthropic best practices
- Established living document workflow with CLAUDE.md integration
- All 10 resource files created with proper cross-references
- skill-rules.json configured for auto-activation

**No Outstanding Issues**: Task is production-ready

---

**Context Status**: TASK COMPLETE ✅
**Last Reviewed**: 2025-11-01 19:50
