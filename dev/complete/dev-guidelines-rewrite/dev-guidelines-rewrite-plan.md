# Dev Guidelines Skill Rewrite - Comprehensive Plan

**Last Updated: 2025-11-01**

---

## Executive Summary

Transform the Node.js/TypeScript `backend-dev-guidelines` skill into a C#-specific `dev-guidelines` skill tailored to the Text Survival RPG's unique architecture. This rewrite will replace web service patterns with game-specific architectural guidance while preserving universal software engineering principles.

**Scope**: Complete replacement of skill content with C# game development focus
**Timeline**: 4-6 hours
**Deliverables**:
- Renamed skill folder
- Rewritten SKILL.md (~450 lines)
- 10 new resource files
- skill-rules.json with auto-activation triggers

---

## Current State Analysis

### Existing Backend-Dev-Guidelines Skill

**Technology Focus**: Node.js/Express/TypeScript microservices
**Architecture**: Layered web architecture (Routes → Controllers → Services → Repositories → Database)
**Key Patterns**:
- BaseController inheritance
- Prisma ORM for database
- Zod validation
- Sentry error tracking
- Dependency injection

**Structure**:
- Main SKILL.md (303 lines)
- 11 resource files covering routing, controllers, services, validation, async patterns, testing, etc.
- Well-organized with progressive disclosure
- Follows Anthropic best practices (under 500 lines, good navigation)

**Cross-Applicable Content Identified**:
1. **Error handling philosophy** - Try-catch patterns, error propagation
2. **Validation patterns** - Input validation, guard clauses (adaptable to C#)
3. **Separation of concerns** - Layered architecture principles
4. **SOLID principles** - Implicit throughout
5. **Testing approaches** - Unit/integration testing concepts
6. **Async patterns** - Though tech differs, C# has async/await too
7. **Code organization** - File structure, naming conventions

**What Must Be Replaced**:
- All Node.js/Express/TypeScript specific code
- Routes/Controllers/Middleware → Action system
- Prisma/Database patterns → In-memory game state
- Zod schemas → C# validation patterns
- Web service concerns → Console game concerns

### Text Survival Game Architecture

**Technology**: C# .NET 9.0 console application
**Architecture**: Composition-over-inheritance with modular systems
**Core Namespaces** (9 primary):
- Actions/ - Builder pattern for dynamic actions
- Actors/ - Player/NPC hierarchy with composition
- Bodies/ - Hierarchical body parts with damage system
- Crafting/ - Property-based recipe system
- Effects/ - Buff/debuff system
- Environments/ - Zone/Location hierarchy
- Items/ - Item hierarchy with properties
- Level/ - Skill progression (player-only)
- Survival/ - Pure function survival processing

**Dominant Pattern**: Builder pattern everywhere (Actions, Effects, Recipes)
**Key Principles**:
- Skills are player-only
- Single damage entry point (Body.Damage)
- Pure survival processing
- Property-based crafting
- Explicit time passage
- Ice Age thematic consistency

---

## Proposed Future State

### New Dev-Guidelines Skill

**Purpose**: Comprehensive C# development guide for modular text-based survival RPG
**Target Users**: Developers working on game systems, features, and content
**Auto-Activation**: Triggers when working with C# files, builder patterns, game systems

**Content Structure**:

#### Main SKILL.md (~450 lines)
```
---
name: dev-guidelines
description: [Rich description with C# keywords]
---

# Dev Guidelines

## Purpose
## When to Use This Skill
## Quick Start
  - New Feature Checklist
  - New System Checklist
## Architecture Overview
  - Composition-based design
  - 9 core namespaces
  - Builder pattern philosophy
## Core Principles (10 rules)
## Common Patterns
## Quick Reference
## Anti-Patterns to Avoid
## Navigation Guide
## Resource Files
```

#### 10 Resource Files
1. **action-system.md** - ActionBuilder, menu flow, context, extensions
2. **body-and-damage.md** - Body hierarchy, damage processing, capacities
3. **survival-processing.md** - SurvivalProcessor, temperature, effects
4. **crafting-system.md** - RecipeBuilder, ItemProperty, property-based recipes
5. **effect-system.md** - EffectBuilder, EffectRegistry, severity
6. **builder-patterns.md** - Cross-cutting builder guide, fluent APIs
7. **composition-architecture.md** - Actor/Player/NPC, managers
8. **factory-patterns.md** - Content creation factories
9. **error-handling-and-validation.md** - C# exceptions, guard clauses, validation
10. **complete-examples.md** - Full feature implementations

#### skill-rules.json
Comprehensive trigger configuration for auto-activation

---

## Implementation Phases

### Phase 1: Preparation & Content Extraction
**Goal**: Preserve valuable cross-applicable content before replacement

#### Tasks

**1.1 Extract Cross-Applicable Patterns**
- **Description**: Review existing resources and extract universal software principles
- **Acceptance Criteria**:
  - Error handling patterns documented
  - Validation approaches noted
  - Testing strategies captured
  - SOLID principles identified
- **Effort**: S
- **Dependencies**: None
- **Status**: ✅ COMPLETED

**1.2 Map Old Content to New Structure**
- **Description**: Create mapping document showing which old concepts translate to C# equivalents
- **Acceptance Criteria**:
  - Routing → Action system mapping
  - Controller → Action factory mapping
  - Validation → C# validation mapping
  - Service/Repository → Manager pattern mapping
- **Effort**: M
- **Dependencies**: 1.1
- **Status**: PENDING

---

### Phase 2: Core Skill Transformation

#### Tasks

**2.1 Rename Skill Folder**
- **Description**: Rename `.claude/skills/backend-dev-guidelines/` to `.claude/skills/dev-guidelines/`
- **Acceptance Criteria**: Folder renamed successfully
- **Effort**: S
- **Dependencies**: None
- **Status**: PENDING

**2.2 Rewrite Main SKILL.md**
- **Description**: Complete rewrite focusing on C# game architecture
- **Acceptance Criteria**:
  - Under 500 lines
  - Rich frontmatter with C# keywords
  - 10 core principles clearly stated
  - Navigation links to all resource files
  - Quick Start checklists for common tasks
  - Anti-patterns section with 8+ examples
  - Skill status footer
- **Effort**: L
- **Dependencies**: 1.2, 2.1
- **Status**: PENDING

**2.3 Create skill-rules.json**
- **Description**: Configure auto-activation triggers
- **Acceptance Criteria**:
  - Keywords: C#, action, builder, body, survival, crafting, effect, etc.
  - Intent patterns: Regex for "create action", "implement survival", etc.
  - File path patterns: `**/*.cs`, namespace-specific globs
  - Content patterns: Detect builder classes, factory methods
- **Effort**: M
- **Dependencies**: 2.2
- **Status**: PENDING

---

### Phase 3: Resource File Creation (Part 1 - Core Systems)

#### Tasks

**3.1 Create action-system.md**
- **Description**: Complete guide to Action system with builder pattern
- **Acceptance Criteria**:
  - ActionBuilder pattern explained
  - .When(), .Do(), .ThenShow(), .ThenReturn() documented
  - GameContext usage
  - ActionFactory organization
  - Common extensions (ShowMessage, AndGainExperience, etc.)
  - Menu flow examples
  - 5+ complete code examples
  - Table of contents (if >100 lines)
- **Effort**: L
- **Dependencies**: 2.2
- **Status**: PENDING

**3.2 Create body-and-damage.md**
- **Description**: Body part hierarchy and damage system guide
- **Acceptance Criteria**:
  - Body hierarchy explained (Region → Tissue → Organ)
  - Capacity system documented
  - Body composition (fat/muscle) explained
  - Damage entry point emphasized (Body.Damage ONLY)
  - DamageProcessor flow documented
  - Healing mechanics
  - 5+ code examples
  - Table of contents
- **Effort**: L
- **Dependencies**: 2.2
- **Status**: PENDING

**3.3 Create survival-processing.md**
- **Description**: SurvivalProcessor and needs system guide
- **Acceptance Criteria**:
  - Pure function design emphasized
  - SurvivalProcessor.Process() signature documented
  - Temperature regulation explained
  - Calorie burn calculations
  - Hydration system
  - Effect generation from thresholds
  - Body composition impact on temperature
  - 5+ code examples
  - Table of contents
- **Effort**: L
- **Dependencies**: 2.2
- **Status**: PENDING

**3.4 Create crafting-system.md**
- **Description**: Property-based crafting and recipe system guide
- **Acceptance Criteria**:
  - ItemProperty enum explained
  - RecipeBuilder pattern documented
  - Property requirements vs item-specific checks
  - Result types (Item, LocationFeature, Structure)
  - Skill requirements
  - Time consumption
  - 5+ recipe examples
  - Table of contents
- **Effort**: M
- **Dependencies**: 2.2
- **Status**: PENDING

**3.5 Create effect-system.md**
- **Description**: Buff/debuff effect system guide
- **Acceptance Criteria**:
  - EffectBuilder pattern documented
  - EffectRegistry usage
  - Severity system explained
  - Capacity modifiers
  - Temperature effects
  - Body part targeting
  - AllowMultiple behavior
  - Common effect extensions (Bleeding, Poisoned, etc.)
  - 5+ code examples
  - Table of contents
- **Effort**: M
- **Dependencies**: 2.2
- **Status**: PENDING

---

### Phase 4: Resource File Creation (Part 2 - Cross-Cutting Patterns)

#### Tasks

**4.1 Create builder-patterns.md**
- **Description**: Cross-cutting guide to builder pattern philosophy
- **Acceptance Criteria**:
  - Fluent API principles
  - Method chaining best practices
  - Context passing patterns
  - Builder consistency across systems
  - Examples from Action, Effect, Recipe builders
  - When to use builder vs factory
  - Extension method patterns
  - Table of contents
- **Effort**: M
- **Dependencies**: 3.1, 3.4, 3.5
- **Status**: PENDING

**4.2 Create composition-architecture.md**
- **Description**: Actor composition and manager pattern guide
- **Acceptance Criteria**:
  - Actor base class explained
  - Player vs NPC distinction emphasized
  - Manager composition (InventoryManager, CombatManager, etc.)
  - Skills are player-only rule
  - NPC stats from body parts only
  - Separation of concerns
  - 5+ code examples
  - Table of contents
- **Effort**: M
- **Dependencies**: 2.2
- **Status**: PENDING

**4.3 Create factory-patterns.md**
- **Description**: Content creation factory guide
- **Acceptance Criteria**:
  - ItemFactory patterns
  - NPCFactory patterns
  - BodyPartFactory patterns
  - SpellFactory patterns
  - LocationFactory/ZoneFactory patterns
  - When to use factory vs builder
  - Static method organization
  - Future JSON migration notes
  - 5+ code examples
  - Table of contents
- **Effort**: M
- **Dependencies**: 2.2
- **Status**: PENDING

**4.4 Create error-handling-and-validation.md**
- **Description**: C# exception handling and input validation guide (adapted from old skill)
- **Acceptance Criteria**:
  - Try-catch best practices
  - Custom exception types
  - Guard clause patterns
  - Input validation approaches
  - Error propagation
  - Async error handling in C#
  - Common pitfalls
  - Adapted from async-and-errors.md and validation-patterns.md
  - 5+ code examples
  - Table of contents
- **Effort**: M
- **Dependencies**: 1.1, 2.2
- **Status**: PENDING

**4.5 Create complete-examples.md**
- **Description**: Full feature implementation examples
- **Acceptance Criteria**:
  - Complete new action example (from scratch)
  - Complete new item + recipe example
  - Complete new effect example
  - Complete new body part type example
  - Complete new NPC example
  - Each example shows ALL steps (factory, builder, integration)
  - Code is copy-pasteable and functional
  - Table of contents
- **Effort**: XL
- **Dependencies**: 3.1-3.5, 4.1-4.4
- **Status**: PENDING

---

### Phase 5: Cleanup & Finalization

#### Tasks

**5.1 Delete Old Resources Folder**
- **Description**: Remove Node.js-specific resources after confirming new ones are complete
- **Acceptance Criteria**: Old resources folder deleted
- **Effort**: S
- **Dependencies**: 4.5 (all resource files created)
- **Status**: PENDING

**5.2 Test Auto-Activation Triggers**
- **Description**: Verify skill-rules.json triggers activate the skill appropriately
- **Acceptance Criteria**:
  - Triggers on C# file edits
  - Triggers on "create action" intents
  - Triggers on builder pattern detection
  - Doesn't trigger on unrelated files
- **Effort**: M
- **Dependencies**: 2.3, all resource files
- **Status**: PENDING

**5.3 Review & Polish**
- **Description**: Final review of all content for consistency, accuracy, completeness
- **Acceptance Criteria**:
  - All cross-references work
  - No broken links
  - Consistent formatting
  - Code examples tested
  - Skill status footer on all files
  - Last Updated dates current
- **Effort**: M
- **Dependencies**: 5.1, 5.2
- **Status**: PENDING

**5.4 Update Project Documentation**
- **Description**: Update any references to the old backend-dev-guidelines skill
- **Acceptance Criteria**:
  - CLAUDE.md references updated if any
  - README references updated if any
  - Related skills references updated
- **Effort**: S
- **Dependencies**: 5.3
- **Status**: PENDING

---

## Risk Assessment and Mitigation Strategies

### Risk 1: Loss of Valuable Cross-Applicable Content
**Probability**: Medium
**Impact**: Medium
**Mitigation**:
- Phase 1 explicitly extracts and documents cross-applicable patterns
- error-handling-and-validation.md preserves these patterns adapted for C#
- Review step ensures nothing valuable was lost

### Risk 2: Skill Too Long (>500 lines)
**Probability**: Medium
**Impact**: Low
**Mitigation**:
- Plan limits SKILL.md to 10 core principles + quick reference
- Use progressive disclosure via resource files
- Monitor line count during Phase 2

### Risk 3: Incomplete C# Code Examples
**Probability**: Low
**Impact**: High
**Mitigation**:
- complete-examples.md has XL effort allocation
- Each resource file requires 5+ code examples
- Review phase includes code example testing

### Risk 4: Auto-Activation Triggers Too Broad/Narrow
**Probability**: Medium
**Impact**: Medium
**Mitigation**:
- Phase 5.2 dedicated to testing triggers
- Start with specific triggers, expand if needed
- Use skill-developer documentation as reference

### Risk 5: Inconsistent Pattern Documentation
**Probability**: Low
**Impact**: Medium
**Mitigation**:
- builder-patterns.md provides cross-cutting consistency guide
- Review phase checks for consistency
- Use existing CLAUDE.md as source of truth

---

## Success Metrics

### Completion Metrics
- [ ] All 10 resource files created with table of contents
- [ ] Main SKILL.md under 500 lines
- [ ] skill-rules.json created with 10+ keywords
- [ ] 50+ code examples across all files
- [ ] All cross-references functional
- [ ] Old resources folder deleted

### Quality Metrics
- [ ] Each resource file has 5+ code examples
- [ ] All files have "Last Updated" dates
- [ ] Skill status footer present
- [ ] Cross-applicable patterns preserved
- [ ] C# syntax is correct and idiomatic
- [ ] Examples reference actual game code patterns

### Functional Metrics
- [ ] Skill auto-activates when editing C# files
- [ ] Skill auto-activates on relevant intents ("create action", etc.)
- [ ] Navigation links work between files
- [ ] Examples are copy-pasteable and functional

---

## Required Resources and Dependencies

### Knowledge Resources
- ✅ Existing backend-dev-guidelines skill (for structure reference)
- ✅ skill-developer skill (for meta-patterns and best practices)
- ✅ CLAUDE.md (for game architecture and principles)
- ✅ Game codebase (for code examples and patterns)

### File Dependencies
- `.claude/skills/backend-dev-guidelines/` → Read for extraction
- `.claude/skills/skill-developer/` → Read for meta-patterns
- `CLAUDE.md` → Read for architecture
- Game source files → Read for code examples

### External Dependencies
- None (self-contained task)

---

## Timeline Estimates

### By Phase
- **Phase 1**: 30 minutes (Preparation)
- **Phase 2**: 1.5 hours (Core transformation)
- **Phase 3**: 3 hours (Core system resource files)
- **Phase 4**: 2.5 hours (Cross-cutting resource files)
- **Phase 5**: 1 hour (Cleanup and testing)

**Total Estimated Time**: 8.5 hours

### By Effort Level
- **S tasks**: 15 min each × 5 = 1.25 hours
- **M tasks**: 30 min each × 8 = 4 hours
- **L tasks**: 45 min each × 4 = 3 hours
- **XL tasks**: 1 hour each × 1 = 1 hour

**Adjusted Total**: 9.25 hours (includes buffer)

### Critical Path
1. Extract cross-applicable content (1.1)
2. Map old to new (1.2)
3. Rename folder (2.1)
4. Rewrite SKILL.md (2.2) - **BLOCKING**
5. Create all resource files (3.x, 4.x) - **PARALLEL POSSIBLE**
6. Create complete-examples.md (4.5) - **DEPENDS ON ALL OTHERS**
7. Cleanup and review (5.x)

**Minimum Timeline with Parallelization**: 6-7 hours

---

## Next Steps

1. Begin Phase 1, Task 1.1: ✅ COMPLETED
2. Proceed to Task 1.2: Map old content to new structure
3. Execute Phase 2: Core skill transformation
4. Implement Phases 3-4 in parallel where possible
5. Finalize with Phase 5

---

**Plan Status**: READY FOR EXECUTION ✅
**Created By**: Claude Code (Sonnet 4.5)
**Review Status**: Pending user approval
