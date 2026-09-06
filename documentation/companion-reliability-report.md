# Companion reliability pass

The pass adds explainable separation diagnostics, timed traveling-leader tests, bounded search, durable social incidents, continued-willingness departures, and concurrent escape/background combat. It preserves independent survival behavior and the existing scalar relationship model.

## What changed

- NPC decisions report why they are executing work, self-care, pursuit, waiting, or combat. Exhausted critical needs have a separate `BlockedNeed` reason. Opt-in transition traces include stable run-local actor labels, evidence age, remembered destination, action progress, pursuit status, and termination reason. Separation reports retain deaths and unfinished episodes.
- Recruitment captures the current sighting and game minute immediately, so late-game voluntary agreements do not expire as time-zero evidence.
- Controlled travel fixtures exercise real crossing costs for player and NPC leaders with one or four followers. They bound reunion by unfinished work plus route cost and scheduler overhead; they do not grant faster travel or hidden target knowledge.
- Arriving at a last-known location without the target no longer starts optional work. Actors investigate local evidence and stop after a bounded search.
- Search allows 90 minutes of active effort and rejects evidence older than 360 minutes when pursuit resumes. Self-care does not spend active-search effort; neither does it refresh evidence. Effort survives saved pursuit actions. These are explicit initial policy limits, not tuned guarantees of recovery.
- Route retry delay, proven no-route, and computational exhaustion are distinct. No-route stops after three attempts. Budget exhaustion gets up to six attempts, five minutes apart, increasing the node budget from 10,000 to a maximum of 80,000.
- Water acquisition distinguishes known-resource lookup from exploration. A known fire is considered before wandering; the controlled water-detour test verifies fetching, drinking, and reunion through real ticks.
- Emergency needs invalidate pending waits and cannot create a new blocking request. Newly available local self-care clears an obsolete need request. Incident IDs prevent a response to an older prompt resolving a newer one; saved resolutions cannot reroll persuasion or duplicate a pressure memory.
- Ready-to-eat supplies include foods beyond cooked meat. A refused request to stay does not automatically impose a pressure memory; agreeing to the costly delay does, once.
- Continued human willingness is evaluated at 60-minute social boundaries. Staying tolerates opinion down to zero, while invitations require positive opinion. Nearby dissatisfied followers warn their target and allow a response; those unable to communicate can leave independently. Explicit departure/dismissal suppresses immediate recruitment for 120 minutes.
- Tactical escape transfers the unfinished fight to background ownership before world travel. NPCs and players use a shared legal-exit policy based on local tactical threat direction and travel cost. Remaining combat advances during the crossing and owns its eventual aftermath.
- A cold NPC at camp no longer attempts to travel to that same tile. More importantly, exhausting critical survival options while following cannot fall through to pursuit or optional errands: the actor retries locally instead. Independently traveling NPCs retain their existing fallback behavior.

## The regression the simulation caught

The first revised five-actor run for seed 7 went from five survivors to zero near the end of day two. Aggregate average survival concealed that drop.

The before/after decision traces first diverged at minute 2623, when emergency self-care correctly proceeded without a request delay. Later the revised trace showed critically cold actors alternating between camp and a neighboring tile, including pursuit and optional travel after warmth handling had exhausted its options. A focused test reproduced that fallthrough.

The critical-need guard fixes the unsafe handoff without changing survival costs or guaranteeing warmth. The replay then retained all five actors through two days. An initially broader guard also changed solo survival; the final guard is scoped to the companion scheduling handoff. The final seed-7 replay retained all five actors without changing the solo policy. The entire comparison matrix was rerun after this fix.

## Verification and reproducibility

The companion suite contains 81 passing cases, up from 53 at the start of this pass. The final isolated full suite passed all 573 tests; seed summaries are recorded below. Earlier full workspace verification also passed; unrelated local event/predator tests make workspace totals differ from the committed tree.

Baseline: commit `e565773` (diagnostics and timed travel fixtures, before behavior changes). Final production behavior: commit `e33e6e1`. Both were built from isolated committed files, independently of concurrent workspace edits. Two-day trials use seeds 1–10 for one actor and groups of 2, 3, and 5 actors; groups of 9 use seeds 1–2 as a larger-group smoke check. Group size includes the leader. There is one leader with 1, 2, or 4 followers in the usual companion trials.

Each simulation process is serial. Separate group processes may run concurrently; wall-clock timings from these runs are not a calibrated performance benchmark. Do not use the harness's in-process `--parallel` option for reproducibility.

```sh
dotnet test text_survival.Tests/text_survival.Tests.csproj --filter 'FullyQualifiedName~Companions'
dotnet test text_survival.Tests/text_survival.Tests.csproj
dotnet run --project tools/NpcSim -c Release -- run --seeds 1-10 --days 2 --group 5 --follow --out /tmp/companions.csv
dotnet run --project tools/NpcSim -c Release -- run --seeds 7 --days 2 --group 5 --follow --trace --out /tmp/companion-trace.csv
```

`--follow --out` writes a `.seedN.companions.json` sidecar containing reason counts and separation episodes. `--trace` adds transition records. Diagnostics do not consume RNG or drive decisions; the paired sampling test verifies gameplay state and RNG remain equal with sampling enabled/disabled.

The checked-in [per-seed results](companion-reliability-results.csv) retain survivor counts, mean survival, following/colocation minutes, lost agreements, separation reasons, and unresolved episodes. A lost agreement count can include death; it is not a relationship-abandonment count. Episode maxima describe closed episodes, including death or agreement termination; unresolved episodes are reported separately.

## Before/after outcomes

These are small fixed-seed experiments, not a claim of general balance improvement. Survivors are totals at two days across the seed set; mean survival is averaged across actors.

| Actors (leader + followers) | Seeds | Survivors before → after | Mean survival days before → after | Colocation while following before → after |
| --- | ---: | ---: | ---: | ---: |
| 1 | 10 | 4/10 → 4/10 | 1.553 → 1.553 | — → — |
| 2 | 10 | 9/20 → 6/20 | 1.512 → 1.437 | 37.6% → 40.6% |
| 3 | 10 | 17/30 → 22/30 | 1.680 → 1.657 | 41.8% → 37.9% |
| 5 | 10 | 39/50 → 39/50 | 1.806 → 1.781 | 35.9% → 35.9% |
| 9 | 2 | 15/18 → 16/18 | 1.993 → 1.974 | 36.4% → 37.6% |

The solo CSV matches the baseline exactly. Group results are mixed: the two-actor sample has fewer survivors and lower mean survival; three- and nine-actor groups have more survivors, while the five-actor survivor total is unchanged. Mean survival does not consistently rise. No per-seed thresholds or survival costs were tuned to force favorable results.

In the final companion trials, self-care plus blocked urgent needs account for approximately 85–92% of separated minutes. Long separations remain: the longest closed five-actor episode is 1,716 minutes, and some agreements remain separated at the two-day cutoff. Colocation is measured separately from visibility: actors can remain within sight on different tiles. The known-following handoff bugs are fixed, but these experiments do not prove a desirable travel feel across every survival situation.

The seed-7 five-actor run was repeated with transition tracing enabled; its CSV row, reason counts, and episode timings match the corresponding final matrix run. Run-local actor labels initially differed with tracing enabled; registration is now unconditional, with a regression proving label stability. This diagnostic-only correction does not affect the recorded survival metrics. The detailed trace identifies why the earlier implementation diverged; other survival differences remain balancing evidence rather than evidence of a guaranteed improvement.

## Scenario coverage

This maps the reliability plan's 20 cases to concrete evidence. It distinguishes contract coverage from a complete gameplay experiment.

| Plan cases | Evidence and limit |
| --- | --- |
| 1–2: finish work, then reunite | Timed player/NPC leader tests with 1/4 followers; existing forage settlement and catch-up tests |
| 3–4: continuous travel and faster leader | Multiple consecutive timed crossings, ordinary crossing-duration and hidden-target tests; long sustained journeys at deliberately unequal actor speeds remain a gameplay follow-up |
| 5: local supplies | Carried water, shared cache, pending-request food reservation, and ready-food offers |
| 6: survival detour | Controlled full water-fetch, drinking, and reunion journey through real ticks; movement request/let-go preserves intent; long self-care preserves effort but ages evidence; autonomous trials exercise real detours |
| 7: long rest/warming | Safe temporary rest, work persistence, bounded search effort, and critical-warmth fallback regression |
| 8: full pack | Full-pack catch-up does not divert to camp; optional-work priority regressions |
| 9: double-back | New sighting replaces the old lead and produces reunion after the committed crossing |
| 10–11: ambiguous/eroded evidence and loss | Local trail turns, erosion, hidden-world paired runs, empty-lead termination; broader intersection combinations remain possible |
| 12–13: route failure/budget | Illegal-crossing prevention, no-route termination, retry delay, expanding bounded budgets |
| 14: chains | A remains with direct target B while B works and C leaves; cycle rejection remains covered |
| 15: urgency during request | Pending emergency does not block the selected survival action; critical fallback while following never becomes optional travel |
| 16: competing supplies | Atomic transfer revalidation, stale incident rejection, inventory reservation settlement |
| 17: relationship departure | Nearby warning, remote independent departure, cooldown, stale-target invalidation, saved persuasion resolution |
| 18: escape continuity | Full scripted hunt/retreat, exact crossing-time handoff, remaining background ownership, legal threat-aware exits |
| 19: checkpoints | Unfinished work, reserved food, actor identity, evidence, active-search effort, pending/resolved requests, background encounters |
| 20: mixed needs | One actor's thirst does not freeze another's forage; timed groups and 1/2/4-follower survival trials |

## Desktop validation and remaining limits

An isolated desktop save was created with a friendly nearby NPC and supplies. The main game window rendered successfully through the native desktop runtime. Automated keyboard/click input did not open the companion panel despite focus and targeting retries. This does not establish an application input bug, and it does not count as a completed interactive playtest. Existing player saves were not used or overwritten.

The remaining manual pass should check invitation, departure and supply wording, follow a companion through a real detour, and retreat/reunite in ordinary play. Scripted UI tests establish prompt delivery and response effects, but cannot establish presentation quality or the effortless companion feel on their own.

This pass does not claim that autonomous survival balance, group cohesion, every F01–F38 combination, or all 20 plan scenarios are exhaustively validated. Gear lending, dog behavior, factions, A*, and interactive-combat saving remain outside scope. Existing active UI saves are still restricted; background fights are resumable.
