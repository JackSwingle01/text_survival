# Companion acceptance tests

This is the first executable slice of the [companion system plan](../../documentation/companion-system-plan.md). It deliberately starts red for behavior the game does not implement. No tests are skipped, and no expected failure is converted into a passing assertion about the current bug.

Run the suite from the repository root:

```sh
dotnet test text_survival.Tests/text_survival.Tests.csproj --filter 'Suite=CompanionAcceptance'
```

Run the existing regression suite separately while implementing the new contract:

```sh
dotnet test text_survival.Tests/text_survival.Tests.csproj --filter 'Suite!=CompanionAcceptance'
```

An unfiltered test run includes the acceptance suite and will fail until its missing behavior is implemented. This is intentional test-first development, not a change to CI filtering.

## What the fixture does

`CompanionWorld` creates a small grid with explicit terrain, controlled starting conditions, a seeded RNG, and real actors. It advances `GameContext.UpdateWithoutEvents` one minute at a time. Foraging, consumption, movement records, survival, and serialization use production code. No following, tracking, or route-search algorithm is implemented in the tests.

The only new production contract is the optional `Actor.Following` / `FollowIntent.Target` state. Tests establish an already accepted agreement directly. Recruitment, persuasion, and cycle validation will be separate tests through the interaction API. Ordinary actors default to no following intention; this state currently has no behavior.

Leader movements are scenario setup: real completed-crossing operations create tracks while the tests control the independently acting leader. Those setup operations do not claim to advance leader travel time. A separate movement test exercises actual timed NPC travel. Hidden-relocation tests deliberately change only an unseen target's position without creating evidence, to detect information leaks.

Forage abundance guarantees that even a short lean session yields something, since the production feature only records depletion after a successful find. Tests assert elapsed work/depletion, not a particular random inventory yield or exact AI roll.

Route tests now call the shared `Navigation` interface. Spatial contract tests also cover search budgets, weighted routes, observer-local sight, and crossing revalidation.

## Coverage

| Scenario IDs | Executable coverage |
| --- | --- |
| F01, F05 | Current forage continues after departure; completed work is followed by reunion instead of further optional forage; NPC movement spends time and records one crossing |
| F36 | An already accepted human follower can target an NPC, player, or non-human actor; this does not implement a dog controller |
| F07 | Carried water is consumed autonomously without losing the following intention |
| F10, partial | Critical thirst interrupts ordinary work while preserving intention; reassurance/cooldown behavior still needs the interaction API |
| F02, partial | Last sighting supports investigation; opposite hidden target relocations cannot redirect the observer's actions |
| F25 | Updating followers outside player sight does not reveal the player's fog |
| F22, F23 | Detours, impassable and seasonal barriers, legal route steps, and route-query side effects |
| F16, partial | The world tick cannot also forage for a combat participant, while uninvolved NPCs and physiology keep advancing |
| F34, partial | Interrupting an unstarted transfer cannot award its completion |
| F33, partial | In-progress work and target identity survive save/load, including both NPC roster orders and a player target |

The paired hidden-target test is intentionally accompanied by positive reunion and last-seen-investigation tests. Standing still forever must not satisfy the suite's perception contract.

Initial verified results (2026-09-06): **22 acceptance cases: 10 pass, 12 fail, none skipped**. A second independent run reproduced every outcome and failure message. The existing regression suite passed all 477 tests after adding the data contract.

The 12 red cases expose five missing following/pursuit behaviors (including the three target kinds), three routing failures, two activity/cancellation failures, and two persistence failures. One persistence failure is the existing inability to deserialize an NPC relationship target before that target appears in the roster; the other is discarded unfinished work. Resolve these by implementing the intended behavior, not by weakening the expectations.

## Remaining scenarios

The entire 38-scenario plan is not executable yet. Add the next slices when their smallest callable contracts exist:

- Shared sight queries: independent observer answers, visibility while stationary, and observer-capability changes without fog mutation.
- Tracking evidence: turns, intersections, erosion, self-track loops, stale evidence, and bounded search/giving up. The current aggregate track API cannot express all of these faithfully.
- Interaction domain: invitation/voluntary agreement, NPC-to-NPC requests, meaningful offers, refusal, cooldowns, one-time resolution, temporary detours, and reassurance consequences.
- Encounter coordination: purpose-aware hunt participation, late entry, calls, player escape with allies still fighting, and one-time aftermath.
- Expanded persistence: observed knowledge, search effort, pending requests, cooldowns, and reserved resources.

Do not create skipped empty tests or fake domain implementations to make this list appear covered. Add executable cases as contracts become available. Preserve the scenario IDs so coverage can be audited against the plan.

## Determinism and scope

These tests check observable outcomes and invariants in bounded worlds, rather than exact random combat decisions, arbitrary relationship thresholds, or long-run survival rates. Use serial fixed-seed `tools/NpcSim` experiments for those balance questions. Keep randomness in the existing seeded stream.

The activity-ownership tests install a real `CombatScenario` and exercise world updates; they do not pretend to run the full combat orchestrator. Full encounter-entry/exit tests belong in the later coordination slice.
