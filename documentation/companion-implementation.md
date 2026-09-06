# Autonomous companions: implementation and verification

Implemented on `main` in successive commits, starting with executable acceptance tests. The detailed design and scenario catalogue remain in [companion-system-plan.md](companion-system-plan.md).

See the [reliability pass report](companion-reliability-report.md) for subsequent fixes, expanded tests, and before/after evidence.

## Ownership

| Responsibility | Owner | Boundary |
| --- | --- | --- |
| Following agreement and remembered evidence | `Actor.Following`, `Following` | Works with arbitrary actors; does not execute actions or read hidden target destinations |
| Human needs, work, voluntary recruitment | `NPC` | Needs precede pursuit; current work finishes; pursuit precedes optional camp errands |
| Sight geometry | `Sight` | Pure observer-local queries; player fog is a consumer |
| Route legality and costs | `NavigationView` | Shared crossing rules and actor travel costs |
| Search algorithm | `IPathfinder` | Deterministic Dijkstra by default; replace `GameMap.Pathfinder` for A* |
| Physical crossing | `ActorMovement` | Revalidates passage and records movement once; player adapter owns fog and map cursor |
| Physical trail evidence | `TrackRegistry` | Bounded anonymous crossings, weather erosion, local reads |
| Social consent, transfers, requests | `CompanionInteractions` | Actor-to-actor rules; UI and NPC responses are adapters |
| Action progress and settlement | `NPCAction`, `NPCActionState` | Explicit partial-work versus atomic cancellation, idempotent settlement, durable resources/progress |
| Encounter membership and ownership | `CompanionCombat` | One battlefield, joins at minute boundaries, ordinary work yields to combat |
| Tactical behavior | Existing combat AI | Help/retreat calls adjust bounded morale; actors retain control |
| Encounter persistence | `EncounterState` | Background fights resume with rosters, positions, morale and ownership |

World updates use minute boundaries, including batched actions, so observation, cooldowns and background combat see advancing game time consistently. NPC combat no longer recursively resolves an entire fight from an ordinary action completion. Existing non-NPC headless combat entry points remain available.

## Player loop

Open the NPC overlay to see opinion, personality, current work/need, following target, and inventory. “Come with me” requests an agreement; “Go your own way” ends it. Supply buttons give items or request them subject to the owner's needs and willingness.

An NPC that needs a movement detour can ask its nearby target. Player responses are let them go, provide useful supplies where available, or ask them to stay briefly. Requests expire and have cooldowns; survival proceeds while the player sleeps or cannot answer. NPC recipients use the same resource-consent rules automatically. Letting someone fetch supplies retains their reunion intention.

Companions can participate in hunts without treating prey as hostile. Late arrivals join the existing fight. Help and retreat calls nudge morale rather than issuing orders. After player escape, remaining actors continue in a background fight while escape into the world spends real crossing time.

## Policy defaults

- Searching allows 90 minutes of active effort; evidence older than 360 minutes is rejected when pursuit resumes. Self-care does not spend effort or refresh evidence.
- Unreachable routes back off for five minutes and terminate after three failures. Computational exhaustion uses up to six attempts with increasing, capped budgets. A newly sighted destination resets route failures.
- Recent passage storage is bounded at 4,000 crossings. Existing aggregate track rendering remains intact.
- Invitations have a 60-minute cooldown; item requests 30 minutes; need requests 60 minutes.
- Reassurance lasts ten minutes and cannot override an emergency. Accepting a costly delay imposes a small relationship consequence once per incident; refusing does not automatically do so.
- Meaningful need-related gifts can earn a memory at most once per six hours. Tiny transfers do not earn memories.
- Passive familiarity contributes at most +0.2 opinion; serious incidents still matter.
- Combat calls have a five-minute cooldown and replace, rather than stack, their five-minute morale effect.
- Background encounters have a 120-round bound. There is no party-size cap.

These are initial gameplay defaults, not a claim that the full survival economy has been balanced.

## Verification

The first committed acceptance suite had 22 cases: 10 passing and 12 failing. It exposed missing pursuit, routing, activity ownership and persistence. The initial implementation contained 53 companion tests; the reliability pass brings the suite to 81 tests, including real world ticks, JSON round trips and a complete player retreat/escape through the orchestrator. No tests are skipped.

The initial fixture accidentally supplied terrain hazard through a positional location-constructor argument. It was corrected to explicitly use safe terrain and short crossings. Production travel costs were retained. Tests now cover timed crossing separately from leader positioning used to arrange scenarios.

The committed tree was also built and tested in an isolated archive to check independence from unrelated local crafting and rendering work. The shared workspace includes 11 additional local tests, which explains its higher total test count.

### Fixed-seed smoke runs

The existing solo baseline used seeds 1 and 2 for one day: seed 1 died of cold at approximately 0.23 days and seed 2 survived. After survival integration these outcomes and resource totals were preserved; seed 2 had one fewer tile reversal.

The extended harness accepts `--follow`, establishing real NPC-to-NPC agreements before ordinary simulation. Two-day runs with seeds 1 and 2 produced:

| Actors | Seed 1 alive | Seed 2 alive | Mean final cache fuel | Mean final cache water |
| --- | ---: | ---: | ---: | ---: |
| 1 | 0 | 0 | 7.2 kg | 1.5 L |
| 2 | 0 | 2 | 12.3 kg | 2.0 L |
| 4 | 2 | 4 | 27.3 kg | 3.5 L |
| 8 | 7 | 6 | 87.8 kg | 8.5 L |

While agreements were active, the four-actor groups were together for approximately 38% and 29% of follower-minutes; the eight-actor groups approximately 38% and 33%. This is evidence of substantial survival detours and work separation. It warrants further gameplay observation before choosing tighter following thresholds. These runs do not demonstrate that large groups are unstable: shared labor helped them survive. Faction splits and a broader group economy remain outside this implementation.

Reproduce with:

```sh
dotnet test text_survival.Tests/text_survival.Tests.csproj
dotnet run --project tools/NpcSim -c Release -- run --seeds 1-2 --days 2 --group 4 --follow --out /tmp/companions.csv
```

## Deliberate limits

- Actor-neutral sight, evidence, following and routing support non-human contracts. A dog controller/species behavior is not implemented.
- Prints remain anonymous. Intersections may produce false leads; there is no guaranteed identification or omniscient recovery.
- The supply UI currently transfers resources, not equipped gear. Useful-resource departure offers cover water and ready-to-eat foods; warmth/rest requests offer departure or limited reassurance.
- Background fights are saved. Saving during the active interactive combat screen is explicitly rejected because its awaited UI turn is not a resumable checkpoint.
- Recipe checkpoints use the existing recipe name; renaming recipes will require a migration or adoption of stable recipe IDs when the separate crafting work lands.
- The scenario catalogue includes broader combinations and balancing questions beyond the executable examples. Passing the suite establishes its tested contracts, not exhaustive coverage of every combination or completed manual UI playtesting.
