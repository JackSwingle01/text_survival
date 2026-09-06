# Companion tests

These tests started before implementation: 22 acceptance cases, 10 passing and 12 failing. All are now implemented and passing, with 81 companion cases across the complete folder. No cases are skipped.

```sh
dotnet test text_survival.Tests/text_survival.Tests.csproj --filter 'FullyQualifiedName~Companions'
dotnet test text_survival.Tests/text_survival.Tests.csproj
```

See the [scenario catalogue](../../documentation/companion-system-plan.md) [implementation/audit notes](../../documentation/companion-implementation.md), and [reliability report](../../documentation/companion-reliability-report.md).

| Test group | Contracts |
| --- | --- |
| Following acceptance | Finish forage, catch up before optional work, player/NPC/animal targets, personal water, critical interruptions, timed crossings |
| Tracking acceptance | Hidden relocations cannot redirect behavior, last-sighting investigation, no player fog leakage |
| Navigation acceptance and spatial contracts | Detours, seasonal and impassable edges, pure queries, weighted paths, budgets, observer-local sight, crossing revalidation |
| Activity ownership | Combat pauses ordinary work while physiology continues; cancellation cannot grant unpaid work; save/load preserves action progress, target identity and reserved food; repeated settlement is harmless |
| Pursuit contracts | Cycles, local track turns, erosion, search expiry, saved evidence |
| Survival | Shared-cache water, voluntary NPC-to-NPC following, safe temporary rest |
| Interactions | Invitations, cooldowns, meaningful gifts, scarce supplies, owner refusal, temporary departure, familiarity limits, deterministic player prompt delivery |
| Combat | Purpose-aware hunts, late arrivals without duplicate membership, bounded calls, scheduled NPC defense, full player retreat and world escape |
| Reliability | Timed player/NPC travel, mixed needs, full packs, double-backs, direct-target chains, critical fallback, bounded budgets, diagnostic invariance |
| Departures | Relationship warnings, remote decisions, cooldowns, incident identity and saved resolution |
| Checkpoints | Background encounters and pending requests survive load; route failures terminate; non-human following and replaceable pathfinding work through the same interfaces |

`CompanionWorld` creates explicit small worlds and advances real production ticks. Its leader-positioning helper is scenario arrangement: completed physical crossings create evidence, but do not claim to simulate the leader's elapsed travel time. Timed follower movement is tested separately. Hidden-relocation tests deliberately alter only world truth without leaving evidence.

No pursuit algorithm is implemented in the fixture. Tests assert observable behavior and invariants, not exact random decisions. Use the serial `tools/NpcSim --follow` group experiments for balance and survival questions. The broader 38-scenario design catalogue is not an exhaustive automated test matrix; remaining combinations and manual gameplay observations are called out in the implementation notes.
