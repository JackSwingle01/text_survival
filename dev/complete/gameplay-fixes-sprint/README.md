# Gameplay Fixes Sprint

## Overview
Comprehensive sprint plan to fix all 46 issues identified in gameplay testing sessions.

## Sprint Files

### 📋 [gameplay-fixes-sprint-plan.md](./gameplay-fixes-sprint-plan.md) (37 KB)
**Complete strategic plan** with:
- 46 issues organized into 4 phases by severity
- Specific code changes for each issue (file paths, line numbers, before/after code)
- Acceptance criteria for validation
- Effort estimates (S/M/L/XL)
- Dependencies between fixes
- Testing strategy
- Risk assessment
- Success metrics

**Use this for**: Understanding what needs to be done and how

### 🔧 [gameplay-fixes-sprint-context.md](./gameplay-fixes-sprint-context.md) (12 KB)
**Technical context and decisions** including:
- Key architectural decisions and rationale
- Code patterns used
- Data flow analysis
- Critical code locations
- Performance considerations
- Compatibility notes
- Rollback plans

**Use this for**: Understanding why decisions were made and technical implementation details

### ✅ [gameplay-fixes-sprint-tasks.md](./gameplay-fixes-sprint-tasks.md) (11 KB)
**Actionable checkbox task list** with:
- Granular tasks for each issue
- Progress tracking checkboxes
- Daily standup template
- Week-by-week roadmap
- Testing tasks
- Documentation tasks
- Sprint completion checklist

**Use this for**: Day-to-day tracking and checking off completed work

## Quick Start

1. **Read**: gameplay-fixes-sprint-plan.md (focus on Phase 1 first)
2. **Reference**: gameplay-fixes-sprint-context.md when implementing
3. **Track**: gameplay-fixes-sprint-tasks.md (check off tasks as you complete them)

## Sprint Summary

**Total Issues**: 46
**Total Effort**: 15-20 development days (2-3 weeks)
**Priority Breakdown**:
- 🔴 Phase 1 (Critical): 4 issues - 5-6 days
- 🟠 Phase 2 (High): 6 issues - 4-5 days  
- 🟡 Phase 3 (Medium): 4 issues - 3-4 days
- 🟢 Phase 4 (Low): 3 issues - 2-3 days

## Critical Fixes (Phase 1)

1. **Fire-making skill crash** (30 min) - "Fire-making" should be "Firecraft"
2. **Survival stats do nothing** (1 day) - 0% food/water has no consequences
3. **Temperature damage not working** (1 day) - Hypothermia doesn't cause damage
4. **No death system** (2 hours) - Player can't die

## High-Priority Fixes (Phase 2)

1. **Message spam** (4 hours) - "You are still feeling cold" x269,914 times
2. **Duplicate menus** (15 min) - "Inspect" appears twice
3. **Sleep exploit** (30 min) - Can sleep 30,000 hours
4. **Water harvesting missing** (3 hours) - Puddle visible but can't harvest
5. **Hunting broken** (4 hours) - Animals flee instantly
6. **Foraging message bug** (1 hour) - "Found nothing" then shows items

## File Organization

```
dev/active/gameplay-fixes-sprint/
├── README.md (this file)
├── gameplay-fixes-sprint-plan.md (strategic plan)
├── gameplay-fixes-sprint-context.md (technical details)
└── gameplay-fixes-sprint-tasks.md (task checklist)
```

## Implementation Order

**Week 1**: Phase 1 (Critical blockers)
**Week 2**: Phase 2 (High-priority bugs)
**Week 3**: Phase 3 (Medium-priority UX)
**Week 4**: Phase 4 (Low-priority polish)

## Key Files That Need Changes

- **Actions/ActionFactory.cs** - 8 issues
- **Survival/SurvivalProcessor.cs** - 3 issues
- **Bodies/Body.cs** - 3 issues
- **Effects/EffectBuilder.cs** - 1 issue
- **Effects/EffectRegistry.cs** - 1 issue
- **Player.cs** - 1 issue
- **Program.cs** - 1 issue

## Testing Strategy

- **Unit Tests**: Add to test_survival.Tests/ for calculation systems
- **Integration Tests**: Use TEST_MODE=1 and play_game.sh for gameplay flows
- **Regression Tests**: Verify existing features still work

## Success Criteria

### Must Have
✅ All Phase 1 issues fixed (game not broken)
✅ All Phase 2 issues fixed (playable experience)
✅ Unit tests pass
✅ Integration tests pass

### Should Have
✅ 80%+ Phase 3 issues fixed
✅ Performance acceptable (sleep <1 sec)
✅ No new bugs introduced

### Nice to Have
✅ Phase 4 polish completed
✅ Code coverage >60%
✅ Documentation updated

## Related Files

- **Source**: `/Users/jackswingle/Documents/GitHub/text_survival/dev/active/gameplay.txt` - Original gameplay test report with all 46 issues
- **Issues**: `/Users/jackswingle/Documents/GitHub/text_survival/ISSUES.md` - Will be updated as issues are resolved

## Notes

- All code changes include specific file paths and line numbers
- Each issue has clear acceptance criteria
- Dependencies between fixes are documented
- Risk assessment included for high-risk changes
- Rollback plans available if needed

---

**Created**: 2025-11-03
**Status**: Ready to start
**Estimated Completion**: 2-3 weeks
