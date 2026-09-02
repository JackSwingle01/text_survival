# NPC Survival Simulation — Harness Design and Hypotheses

*Hand-off document. Part 1 is what the code does today (verified by reading, not yet by running). Part 2 is the harness to build. Part 3 is the hypotheses to test with it, in order. Part 4 is the evaluation protocol and report format. Part 5, added after implementation, is what actually happened when the harness and hypotheses were built and run.*

The symptom being chased: the starting NPC walks back and forth between tiles, then sits still, and never gets the camp fire built or fed.

---

## Part 1 — What the code does today

All line numbers refer to `Actors/NPC/NPC.cs` unless stated.

### 1.1 The per-minute loop (`NPC.Update`, lines 83–145)

`GameContext.UpdateInternal` calls `npc.Update(1, ...)` once per NPC per minute (`Actions/GameContext.cs:535–538`). Each minute:

1. `ShouldInterrupt()` — a threat at this tile, or a critical need higher than the one being handled, clears the current action.
2. If there is a current action, `ContinueAction()` ticks it one minute and, when `MinutesSpent >= DurationMinutes`, calls `Complete(npc)`. **Work is applied only on completion** — a 60-minute forage yields nothing for 59 minutes.
3. Otherwise: clear a satisfied need, `DetermineNeed()`, then `DetermineActionForNeed()` picks one action and it starts.

Note the `return` at line 123 exits the whole `for` loop; harmless now because `minutes` is always 1, but any caller passing more would only tick one minute of action.

### 1.2 Need selection (lines 614–656)

- Critical (`GetCriticalNeed`): Warmth < .25, Water < .20, Rest < .10, Food < .05.
- Satisfy (`DecideSatisfyNeed`): Warmth < .5, Water < .5, Rest < .3, **Food < .05** (same as critical — the NPC never eats until it is starving).
- `DecideSatisfyNeed` uses a chain of independent `if`s that each overwrite `need`, so the **last** matching check wins: Food beats Rest beats Water beats Warmth. That is the reverse of the documented priority (Warmth > Water > Rest > Food).
- Satisfied thresholds (`IsCriticalNeedSatisfied`): Warmth > .7, Water > .5, Rest > .5, Food > .3.

### 1.3 Resource gathering is blocked on almost every tile

This is the root cause I expect the harness to confirm.

- `ForageFeature.IsNearlyDepleted()` is `ResourceDensity() < 1.2` (`Environments/Features/ForageFeature.cs:53,569`).
- Terrain tiles are created with base density Forest 1.0, Clearing 0.72, Plain/Rock/Marsh/Water 0.52, Hills 0.40, then `RandomNormal(base, 0.32)` clamped to 0.1–2.0 (`Environments/Factories/FeatureFactory.cs:176–194`).
- Every NPC gathering path requires `!forage.IsNearlyDepleted()`: `DetermineGetSpecificResource` (line 682), `GetResourceAtCurrentLocation` (line 857), and `GetAccessibleResources` (line 816, used to judge adjacent tiles).

So a fresh, untouched plain tile is already "nearly depleted" in the NPC's eyes. Only named locations with density ≥ 1.2 (Decent tier and up) are ever forageable. Around camp, that is usually none.

Meanwhile `ResourceMemory.RememberLocation` records `Location.ListResourcesHere()`, which uses `ProvidedResources()`, which only requires `!IsDepleted()` (density ≥ 0.4). So memory says "sticks are at tile X", the NPC walks to X, `GetResourceAtCurrentLocation` refuses to forage, and it falls through to a random move. That mismatch is the back-and-forth.

### 1.4 Exploration is a memoryless random walk

- `TryExplore` (line 593) and the boldness fallback in `DetermineGetResource` (line 772) and `DetermineGetSpecificResource` (line 719) pick a **uniformly random adjacent tile**, including the one just left.
- Nothing records "I was here and found nothing"; `ResourceMemory` only adds, it never marks a tile as tried.

### 1.5 The freeze

When every branch returns null, `DetermineIdle` (line 1238) returns `NPCRest(5–30 min)` regardless of body temperature or whether there is a fire here. A cold NPC standing on a plain with nothing to do rests for up to 30 minutes, gets colder, and repeats.

### 1.6 The fire is never fed as work

- `DetermineWork` (line 919) only stockpiles into the camp **cache** (`NPCStash`). Fuel goes into storage, never into the fire.
- The fire is tended only inside `HandleWarmthNeed`, and only when the NPC is already cold at the fire and the fire is judged not to be warming it fast enough (`IsFireWarmingEffectively`, 90-minute ETA).
- `CanTendFire` needs sticks or logs **in the NPC's inventory**; the cache is not consulted.
- `CanSleep` refuses to sleep when the fire has under 2 burning hours, but nothing then adds fuel, so the NPC falls to idle rest instead.

### 1.7 Starting conditions (`NPCFactory.CreateTestNPC`, `GameContext.CreateNewGame`)

- NPC spawns on a tile adjacent to camp with `Camp` set, 15 kg capacity, a hand drill, **one** stick (0.5 kg) and **one** tinder (0.1 kg).
- Camp has an unlit `HeatSourceFeature` holding 2 kg of kindling, and an empty cache.
- One failed hand-drill attempt consumes the tinder (`FireHandler.AttemptStartFire`), leaving the NPC with a tool and no tinder, and §1.3 blocks getting more.
- `IsEnoughStockpiled` wants 40 kg of fuel and 6 L of water in the cache before any other work.

### 1.8 Other things worth knowing when building the harness

- `Utils` uses one unseeded static `Random`; `GridWorldGenerator` and `ForageFeature` have their own. Runs are not reproducible; use many runs and report distributions.
- The NPC logs its decisions with `Console.WriteLine` — dozens of lines per decision. Capturing `Console.Out` is the cheapest way to get a decision trace into the log file.
- `GameContext.UpdateInternal` removes dead NPCs from `ctx.NPCs` (line 557); keep your own reference. `NPCBodyFeature.DetermineDeathCause(npc)` gives the cause.
- `Directory.Build.props` sets `TreatWarningsAsErrors`; CI runs `dotnet format --verify-no-changes`. New test code must build warning-free and be formatted.
- `text_survival.csproj` has `InternalsVisibleTo` for the test project, so `internal` NPC members are callable from tests.
- `text_survival.Tests/Actions/GameLoopSmokeTests.cs` shows how to create a full world (`GameContext.CreateNewGame()`) and attach a `ScriptedUi`.

---

## Part 2 — The harness

### 2.1 Feasibility

Yes, with the current architecture. `GameContext.UpdateWithoutEvents(1, activity)` advances the whole simulation one minute synchronously with no UI: survival, weather, locations, herds, NPCs. Nothing awaits the player, so the harness is a plain loop. The only thing to neutralise is the player, who otherwise dies of cold at camp; reset their body stats every tick so they are a ghost.

### 2.2 Files

```
text_survival.Tests/Support/NPCSimulation.cs        the harness
text_survival.Tests/Support/SimulationFactAttribute.cs   gating attribute
text_survival.Tests/Actors/NPCSimulationTests.cs    scenarios + assertions
```

Gate: `SimulationFactAttribute : FactAttribute` sets `Skip = "Set NPC_SIM=1 to run"` unless the environment variable `NPC_SIM` is set. These runs are slow and nondeterministic, so they do not run in CI by default. Put the test class in `[Collection("NPCSimulation")]` with a `[CollectionDefinition(..., DisableParallelization = true)]` because the harness redirects `Console.Out`.

Run with:

```bash
NPC_SIM=1 dotnet test --filter "FullyQualifiedName~NPCSimulation" --logger "console;verbosity=normal"
```

Logs go to `$NPC_SIM_LOG_DIR` if set, otherwise `text_survival.Tests/bin/<config>/net10.0/npc-sim/`. Print the log path via `ITestOutputHelper`.

### 2.3 Harness API

```csharp
public sealed class NPCSimulation
{
    public GameContext Ctx { get; }
    public NPC Npc { get; }                         // own reference; ctx.NPCs drops it on death
    public List<NPCSnapshot> Snapshots { get; }     // one per minute

    public static NPCSimulation Create(SimulationScenario scenario);
    public void Run(int minutes);                   // stops early on NPC death
    public SimulationSummary Summarize();
    public string WriteLog(string name);            // returns the path
}

public enum SimulationScenario
{
    Baseline,        // exactly CreateNewGame(): fire unlit, NPC adjacent to camp
    FireLit,         // harness calls campfire.IgniteAll() at minute 0 (2 kg kindling burns ~1–2 h)
    NpcAtCamp        // NPC moved onto the camp tile before starting
}
```

`Create`:
1. `GameContext.CreateNewGame()`, `ctx.Ui = new ScriptedUi()`.
2. Take `ctx.NPCs[0]` as the subject (throw if none — never silently simulate nothing).
3. Apply the scenario.

`Run`, per minute:
1. Swap `Console.Out` for a `StringWriter`.
2. Ghost the player: `BodyTemperature = 98.6`, `Energy = MAX_ENERGY_MINUTES`, `Hydration = MAX_HYDRATION`, `CalorieStore = MAX_CALORIES`.
3. `Ctx.UpdateWithoutEvents(1, ActivityType.Resting)`.
4. Restore `Console.Out`; take the captured text as this minute's `Lines`.
5. Record the snapshot (below). Stop when `!Npc.IsAlive`, recording `DeathCause`.

Wrap in `try/finally` so `Console.Out` is always restored.

### 2.4 Snapshot (one per minute)

```csharp
public sealed record NPCSnapshot(
    int Minute, DateTime GameTime,
    GridPosition Pos, string LocationName, TerrainType Terrain,
    string? Action, int ActionMinutesSpent, int ActionDuration, NeedType? Need,
    double WarmPct, double BodyTempF, double HydratedPct, double EnergyPct, double FullPct,
    double AmbientTempF,
    int Sticks, int Tinder, int Logs, double WaterL, int FoodItems, double CarryKg, double CarryMaxKg,
    bool CampFireActive, double CampFireBurningKg, double CampFireUnburnedKg, double CampFireHoursLeft, double CampFireTempF,
    bool HereFireActive,
    double CacheFuelKg, double CacheWaterL, double CacheFoodKg,
    bool IsAlive, string? DeathCause,
    IReadOnlyList<string> Lines);
```

Sources: `Npc.CurrentAction?.Name / MinutesSpent / DurationMinutes`, `Npc.CurrentNeed`, `Npc.Body.*Pct`, `Npc.CurrentLocation.GetTemperature()`, `Npc.Inventory.Count/Weight/CurrentWeightKg/MaxWeightKg`, `Ctx.Camp.GetFeature<HeatSourceFeature>()` (`IsActive`, `BurningMassKg`, `UnburnedMassKg`, `TotalHoursRemaining`, `GetCurrentFireTemperature()`), `Ctx.Camp.GetFeature<CacheFeature>()!.Storage.GetWeight(category)`.

### 2.5 Tile view (logged on every action change or move)

For the current tile and each cardinal neighbour from `Map.GetTravelOptionsFrom`:

```
name | terrain | passable | forage density | NearlyDepleted? | CanForage? | provided resources | wooded? | harvestable? | water? | fire?
```

`ForageFeature.ResourceDensity()` is private; either expose it as `internal double Density => ResourceDensity();` (one line, acceptable) or log `BaseResourceDensity` + `NumberOfHoursForaged` and compute it in the harness. This view is what proves or disproves §1.3 at a glance.

### 2.6 Log file format

Plain text, one file per run, human-readable so a specific minute can be found by searching:

```
=== NPC Simulation: Baseline run 3 ===
world seed: n/a   npc: Grog   camp: (41,52)   start: Day 1 06:00

[0000 06:00] (42,52) Plain        act=-                  need=-      warm=1.00 hyd=0.75 en=0.94 full=0.75 T=-12F  inv: st=1 ti=1 w=0.0 kg=4.1/15  campfire: OFF 0.0/2.0kg
  --- tile view ---
  here  (42,52) Plain    density=0.48 nearlyDepleted=YES canForage=YES  [Tinder, Stick, Berries, PlantFiber]
  N     (42,51) Forest   density=1.05 nearlyDepleted=YES canForage=YES  [...]
  ...
  > [NPC:Grog] Picked: Resting for need
  >     [GetResource] Looking for category: Fuel
  >     ...
[0001 06:01] ...
```

Compact state line every minute; captured NPC console lines indented with `>`; tile view only when the action or position changed. At the end, append the summary block (§2.7).

### 2.7 Summary metrics (`SimulationSummary`)

These are what the hypotheses are scored on.

| Metric | Definition |
|---|---|
| `SurvivedMinutes` | minutes until death or run end |
| `Died`, `DeathCause` | |
| `MinWarmPct`, `MinutesBelowWarm25` | severity of cold exposure |
| `CampFireActiveMinutes` | minutes the camp fire was burning |
| `MinutesAtActiveFire` | minutes the NPC stood at any active fire |
| `FireStartAttempts`, `FireStartSuccesses` | count `Starting Fire` completions; success = camp fire inactive→active on that minute |
| `TendFireCount` | `Tending Fire` completions |
| `ActionMinutes` | dictionary: action name → minutes spent |
| `ActionStarts` | number of distinct action starts (churn) |
| `Reversals` | moves where the destination is the tile left on the previous move (A→B→A) |
| `ColdIdleMinutes` | minutes with action `Resting` while `WarmPct < .5` and no active fire here |
| `ForageMinutes`, `ForageYieldKg` | time spent and inventory delta on forage completions |
| `SticksGathered`, `TinderGathered` | inventory count deltas on forage/harvest completion |
| `CacheFuelKgFinal`, `CacheWaterLFinal` | what reached storage |
| `StuckStreakMax` | longest run of consecutive minutes with the same `(Pos, Action==Resting)` while any need is unsatisfied |

### 2.8 Tests to write

1. `Baseline_72h_WritesLog` — `Baseline`, 4320 minutes, always passes, prints summary. This is the observation tool.
2. `FireLit_72h_WritesLog` — same with `FireLit`.
3. `Batch_Baseline_10runs` — 10 runs, prints a table of the summary metrics with mean and median. This is the benchmark used for every hypothesis.
4. Later, once behaviour is acceptable: `NPC_Survives24h_AtCamp` as a real assertion (`SurvivedMinutes >= 1440` in ≥ 8 of 10 runs). Only promote it to an always-on test if it is stable; otherwise keep it gated.

---

## Part 3 — Hypotheses

Ordered by expected impact. Apply them **cumulatively** (each on top of the accepted ones before it), run the 10-run batch after each, and keep or revert based on the metrics. Every change is small and local to `Actors/NPC/`.

### H1 — The nearly-depleted check blocks all gathering (root cause)

**Change:** in `NPC.cs`, replace the three `!forage.IsNearlyDepleted()` checks (lines 682, 816, 857) with `forage.CanForage()`. This makes the NPC's judgement consistent with what `ResourceMemory` records.

**Expect:** `ForageMinutes` and `SticksGathered` go from ~0 to substantial; `FireStartSuccesses` > 0 in most runs; `Reversals` fall sharply; `SurvivedMinutes` rises a lot. If this alone does not move the numbers, the diagnosis in §1.3 is wrong and the tile view in the logs should say why.

### H2 — Feed the fire as work, from inventory and from the cache

**Change:** at the top of `DetermineWork`, if at camp and the camp fire exists:
- If the fire is active and `TotalHoursRemaining < 2` and `FireHandler.CanTendFire(Inventory, fire)` → `NPCTendFire`.
- Else if the same but the cache has fuel → take fuel from the cache into inventory (new small action, or reuse `NPCTakeToolFromCache`'s pattern for resources), then tend next minute.
- If the fire is inactive and `FireHandler.CanStartFire(Inventory)` → `NPCStartFire`.

**Expect:** `CampFireActiveMinutes` rises toward the whole run; `TendFireCount` > 0; `ColdIdleMinutes` falls. This is the "contributes to the fire" behaviour the player expects to see.

### H3 — Never idle-rest while cold away from a fire

**Change:** in `DetermineIdle`, before the rest fallback: if `WarmPct < .5` and no active fire here, return `DecideToMove(Camp)` when camp is known, otherwise `DetermineGetResource(ResourceCategory.Fuel, urgent: true)`; only rest if both return null. Also cap the rest to 5 minutes when any satisfy-level need is unmet, so the loop re-evaluates sooner.

**Expect:** `ColdIdleMinutes` and `StuckStreakMax` drop; `MinWarmPct` rises.

### H4 — Need priority order and proactive eating

**Change:** rewrite `DecideSatisfyNeed` to return the first match in priority order (Warmth, Water, Rest, Food), and raise the Food satisfy threshold from .05 to .3 (critical stays .05).

**Expect:** fewer critical interrupts (`ActionStarts` churn down); Food-related actions appear before starvation. Small effect on survival in the first 72 h since calories start at 75%.

### H5 — Exploration with memory instead of a random walk

**Change:** add `HashSet<Guid> TriedAndEmpty` (or a `Dictionary<Location,DateTime>`) to `ResourceMemory`, filled when a forage completes with an empty result or `GetResourceAtCurrentLocation` returns null at a tile. In `TryExplore` and the boldness fallbacks, choose among adjacent tiles not in that set and not the tile just left; fall back to random only if none remain. Also seed `RememberLocation` with adjacent tiles' visible resources on arrival (`GetAccessibleResources` already computes them).

**Expect:** `Reversals` near zero; distance covered per useful find drops. Lower priority once H1 lands, because most tiles then become forageable and the random walk rarely triggers.

### H6 — Do not spend the last tinder on a hand drill in a cold snap

**Change (optional, test last):** in `HandleWarmthNeed`, before `NPCStartFire`, if `Tinders.Count == 1` and the NPC is not yet critical, prefer gathering one more tinder first when tinder is reachable within a tile. Prevents the "one failed attempt and now I have nothing" spiral seen in §1.7.

**Expect:** `FireStartSuccesses / FireStartAttempts` unchanged, but fewer runs with zero successes.

### Not hypotheses, but note in the report

- Line 123 `return` (§1.1): harmless today; change to `continue` for correctness if touched.
- `IsEnoughStockpiled` fuel target of 40 kg with 15 kg carry: fine as a long-term goal, but it means the NPC does nothing else for a long time. Consider a smaller first-night target (e.g. 10 kg) as a follow-up, not part of this pass.

---

## Part 4 — Evaluation protocol and report

1. Build the harness (Part 2). Run scenarios 1–3 with **no AI changes**. Save one Baseline log and one FireLit log as the "before" record. Confirm or refute §1.3 from the tile views and the `[GetResource]` lines in the log; state which it is.
2. For each hypothesis in order H1 → H6: apply it, run `Batch_Baseline_10runs` (72 h each), record the metric table, decide keep/revert, and say why in one line. Keep the change if it improves `SurvivedMinutes` or `CampFireActiveMinutes` without making `ColdIdleMinutes` or `Reversals` worse.
3. `dotnet build` and `dotnet test` (the normal suite, without `NPC_SIM`) must stay green after each accepted change; `dotnet format --verify-no-changes` must pass.
4. Final report, in this shape:

```
| Variant            | Survived (mean/median, of 4320) | Died | Fire active min | Sticks | Cold idle min | Reversals | Kept? |
| Baseline           |                                 |      |                 |        |               |           |  —    |
| +H1                |                                 |      |                 |        |               |           |       |
| +H2                |                                 |      |                 |        |               |           |       |
| ...                |                                 |      |                 |        |               |           |       |
```

Plus: the paths of the before/after logs, the diff of `Actors/NPC/*.cs`, and anything observed in the logs that none of the hypotheses explain.

---

## Part 5 — What actually happened

The harness (`text_survival.Tests/Support/NPCSimulation.cs`, `SimulationFactAttribute.cs`, `text_survival.Tests/Actors/NPCSimulationTests.cs`) was built as designed and confirmed §1.3 on the first run: every tile around a fresh camp reported `nearlyDepleted=True` even on tiles the NPC was actively standing on with `canForage=True`, so the NPC gathered nothing and died of cold at **369 minutes (6.2h)** with zero forage minutes, zero fire starts, and an empty cache. That log is saved as the "before" baseline.

Hypotheses were applied cumulatively, each measured against a 10-run, 72-hour batch (`Batch_Baseline_10runs`). Two things beyond the original H1–H6 list were found empirically and fixed; they're numbered H7/H8 to keep the change log traceable:

- **H1 (depleted-density threshold)** — implemented as specified: `!forage.IsNearlyDepleted()` → `forage.CanForage()` at the three call sites in `NPC.cs`. This alone took forage minutes from 0 to a mean of ~105/run across 10 runs and let fire-starting succeed in 7/10 runs, up from the guaranteed failure before. **Kept.**
- **H2 (feed the fire as work)** — implemented as specified: `NPCTakeResourceFromCache` (new action in `NPCActions.cs`) lets an NPC pull fuel from the camp cache instead of only stashing into it, and `NPC.DetermineWork` now calls a new `TryMaintainCampFire()` first, which tends or relights the camp fire proactively while at camp. **Kept**, though see H9 below for a gap it didn't close.
- **H3 (never idle-rest while cold away from fire)** — implemented as specified in `DetermineIdle`, plus a short-rest fallback (3–5 min instead of 5–30) whenever any need is still unmet. Drove `ColdIdleMinutes` and `StuckStreakMax` to ~0 in every subsequent batch. **Kept.**
- **H7 (new — "return to a weak fire empty-handed") — the actual second root cause.** Measuring H2+H3 together showed a *regression*: survival fell and forage time halved. The batch logs showed why: once H1 let an NPC reach a forage tile to fetch fuel, `HandleWarmthNeed`'s `notAtFire` branch re-ran on arrival, saw "not at a fire", and marched the NPC straight back to the known (still-weak) fire *without ever calling `GetResourceAtCurrentLocation`* — so it forfeited the tile it had just reached. This produced an exact camp↔forage-tile oscillation, confirmed line-by-line in `baseline-batch-00.log` (H2+H3 run): 40+ single-tile round trips, `SticksGathered: 0`. Fixed by checking, before heading to a known fire, whether the NPC actually has fuel to tend it (`FireHandler.GetFireMaterials(Inventory).HasKindling`); if not, it forages here first. **Kept** — this was the single highest-leverage fix found. Documented in `principles-code.md` terms: this is exactly a "different layer, different abstraction" violation — the warmth-need handler was re-deciding "go to fire" every minute with no memory of the sub-errand ("get fuel") already in progress.
- **A harness bug, not an AI bug**, was found and fixed alongside H7: `NPCTendFire` has a 1-minute duration, so it starts and completes inside a single `Update()` call and never appears as `NPCSnapshot.Action`. `TendFireCount`, `FireStartAttempts`, and `ActionStarts` were silently undercounting any 1-minute action. Fixed by parsing the `"] Picked: "` / `"] Completed: Tending Fire "` lines already captured in `snap.Lines` instead of diffing `snap.Action`. This is a caution for future use of the harness: any metric keyed off `snap.Action` alone will miss short actions; prefer the captured console lines for presence/count, and `snap.Action` only for "what's in progress right now" or coarse duration totals.
- **H8 (new — uncapped "urgent" gathering exposure).** With H7 fixed, the NPC would now correctly forage for fuel before returning to a weak fire — but `urgent: true` bypasses `CanSurviveAwayFromFire` entirely with no time limit, so a critically cold NPC could commit to a full 15–60 minute forage/harvest/chop session at −22°F. One trace showed exactly this: 48 minutes of foraging while `WarmPct=0.00`, ending only when the session completed on its own. Fixed with a new `UrgentGatherCapMinutes = 15` constant applied in all three branches of `GetResourceAtCurrentLocation` (forage, harvest, chop) — urgent still skips the safety check (the resource is still needed regardless of risk) but is capped to a short trip. **Kept** — measurably cut `MinutesBelowWarm25` and, combined with H2/H3/H7, produced the run's first 11–14.5 hour survivals with a continuously tended fire.
- **H4 (need priority order + proactive eating)** — implemented as specified: `DecideSatisfyNeed` rewritten as a nested `switch` returning the first match in priority order (Warmth > Water > Rest > Food) instead of a flat `if`-chain where Food always won by running last; Food's satisfy threshold raised from `.05` to `.3` (kept `.05` for the *critical* threshold in `GetCriticalNeed`, unchanged). Motivated by a clear failure mode that appeared once H2/H3/H7/H8 let NPCs sustain a fire: **4 of 10 runs in the H8 batch survived 11–14.5 hours and then died of dehydration**, with `CacheFinal water=0.0L` in every single run across every batch — water was never being pursued. **Kept**, though dehydration deaths didn't disappear (see H9).
- **H5 (exploration memory) and H6 (tinder conservation) were not implemented.** By the time H1/H7 landed, `Reversals` was already ~0 and `FireStartSuccesses` was already ~1.0/1.0 in most batches, so the failure modes these two hypotheses target were no longer the bottleneck. Revisit only if a future change reintroduces random-walk thrashing or repeated failed fire-starts.

### Benchmark progression (10-run, 72-hour batches; RNG is unseeded, so treat these as directional, not exact)

| Variant | Survived (mean/median, min) | Died | Fire active min (mean) | Sticks gathered (mean) | Cold idle min (mean) | Cache water final | Kept? |
|---|---|---|---|---|---|---|---|
| Before (single run, no fixes) | 369 / 369 | cold | 0 | 0 | 239 | 0.0L | — |
| +H1 | 498 / 422 | 10/10 | 247 | 3.4 | 132 | 0.0L | yes |
| +H2+H3 (H7 bug still present) | 359 / 328 | 10/10 | 160 | 0.8 | 0 | 0.0L | H7 needed |
| +H7 (return-to-weak-fire fix) | 488 / — | 10/10 | 226 | 4.7 | 1 | 0.0L | yes |
| +H8 (urgent-gather cap) | 466 / 294 | 10/10 (4 now dehydration, not cold) | 302 | 4.4 | 1 | 0.0L | yes |
| +H4 (need priority) | 363 / 274 | 10/10 (2 dehydration) | 181 | 3.9 | 1 | varies, still 0.0L most runs | yes |

Every variant still dies within the 72-hour window in every one of the 10 runs — this is not yet a "the NPC survives" result. But the qualitative shift is real and verified in the logs: before H1/H7/H8, the NPC never established a working fire loop at all (0 forage, 0 fire, dead in ~6h, every time). After them, the best runs sustain an actively tended fire for 11–14.5 hours — longer than any run managed before under any configuration — and the cause of death shifts from "never got warm" to "ran out of water while warm."

### H9 — new finding, not yet fixed: the cache never fills, ever

Across every batch, in every single run, `CacheFinal water=0.0L`. Often `fuel=0.0kg` too, except in the longest-surviving runs. This is why H4 reduced but did not eliminate dehydration deaths: raising Food's satisfy threshold didn't address that **Water stockpiling structurally never gets a turn.**

`NPC.DetermineWork()` gates stockpiling as a strict sequential chain: fuel target (40kg, from `IsEnoughStockpiled`'s `DAYS_RESERVE=2 * neededPerPersonDay=20`) is checked first, and only once it's satisfied does the method even look at water. A solo NPC with 15kg carry capacity needs three clean round trips just to hit the fuel target — and now that `TryMaintainCampFire` (H2) also runs first and returns non-null whenever the fire's `TotalHoursRemaining < 2`, which is most of the time once a fire is running, `DetermineWork` frequently never reaches the stockpile chain at all. Water accumulation in the cache was not observed once in any log from any batch.

This is scoped as a follow-up, not fixed in this pass, because it's a resource-throughput/balance question as much as an AI one: is the fix "let DetermineWork check all three categories and pick whichever is most urgent, not fuel-first-always" (an AI change), or "lower the fuel stockpile target for a solo NPC" (a balance change), or both? Recommend picking this up next with the same harness — `Batch_Baseline_10runs` already reports `CacheWaterLFinal`/`CacheFuelKgFinal`, so the fix can be measured immediately.

### Files changed

- `Actors/NPC/NPC.cs` — H1 (3 call sites), H2 (`TryMaintainCampFire`, cache pull in `HandleWarmthNeed`), H3 (`DetermineIdle`), H4 (`DecideSatisfyNeed`), H7 (`HandleWarmthNeed`'s `notAtFire` branch), H8 (`UrgentGatherCapMinutes`, 3 call sites in `GetResourceAtCurrentLocation`).
- `Actors/NPC/NPCActions.cs` — new `NPCTakeResourceFromCache` action (H2).
- `Environments/Features/ForageFeature.cs` — new `internal double CurrentDensity` accessor, for the harness's tile-view logging only; no behavior change.
- `text_survival.Tests/Support/NPCSimulation.cs`, `SimulationFactAttribute.cs`, `text_survival.Tests/Actors/NPCSimulationTests.cs` — the harness (Part 2), gated behind `NPC_SIM=1`.

`dotnet build` and the full `dotnet test` (346 passed, 4 skipped — the gated simulation tests) stay green. `dotnet format --verify-no-changes` shows pre-existing violations in files this pass never touched (`SurvivalProcessor.cs`, `Utils.cs`, and others already uncommitted before this session started); every file this pass authored or edited is clean.

---

## Part 6 — Seeding infrastructure, and Option 2 (persistent search)

The design consult in this pass's follow-up work asked a higher-tier model (Fable) for advice on a specific gap the harness surfaced: `CurrentNeed` persists as a de facto goal across many action-cycles, but the *search itself* (which tile to walk toward, within a still-active need) doesn't — every hop re-rolls a uniformly random adjacent tile with a fresh boldness coin-flip, no memory of tiles already tried, and no anti-backtracking. Three options were proposed: (1) new cursor fields on `NPC` tracking the in-progress search, (2) derive it from `ResourceMemory`, which already records every tile visited, and (3) a dedicated long-lived `NPCSearch` action. Option 3 was rejected as fighting the existing `NPCAction`/`ContinueAction`/`ShouldInterrupt` lifecycle for no real gain. Option 2 was recommended and chosen: no new tracked state, reuses writes `NPCMove.Complete` already makes.

**Implemented:**
- `ResourceMemory` gained a `_lastVisitedTick` counter (incremented on every `RememberLocation` call, which `NPCMove.Complete` already invokes for both the tile left and the tile arrived at) and a `LeastRecentlyVisited(candidates)` helper that prefers never-visited tiles, then the longest-stale one.
- The three boldness-gated random-adjacent-tile picks in `NPC.cs` (`TryExplore`, and the "unknown, explore" fallbacks in `DetermineGetSpecificResource` and `DetermineGetResource`) now call `ResourceMemory.LeastRecentlyVisited` instead of `Utils.GetRandomFromList` - this alone kills backtracking onto the tile just left.
- The per-hop `Utils.DetermineSuccess(Personality.Boldness)` re-roll (which could abort an in-progress, need-driven search purely by chance) was replaced with `IsBeyondExploreLeash()`: boldness now sets a fixed distance (`2 + Boldness * 8`, ~2-10 tiles) an NPC is willing to range from camp or a known active fire before an exploring search gives up. A redundant unconditional boldness roll Fable flagged in `DetermineGetResource` (harmless due to `??=`, but wasteful and misleading) was dropped in the same pass.

### A necessary detour: the RNG wasn't actually seedable

Testing "before vs after" on 3 fixed seeds required seeding to exist at all - it didn't. `Utils.random`, `GridWorldGenerator._rng`, and `HerdPopulator._rng` were all unseeded static/instance `Random` fields. Added: `Utils.Seed(int)`, an optional `seed` parameter on `GridWorldGenerator.Generate` and `HerdPopulator.Populate`, and `GameContext.CreateNewGame(int? seed = null)` threading a seed through all three plus calling `Utils.Seed` first.

That alone was not sufficient - the very first check (same seed, run twice in one process) passed, but the same seed run three times as **separate process invocations** produced three different results (`Grog`, `Ubik`, `Grog` as the NPC's name, from a single `Utils.RandInt` call at spawn). Bisection (probing `Utils.random`'s output immediately after world generation, across repeated process runs) traced this to `GridWorldGenerator.InitializeTerrainLocations` and `PlaceNamedLocations` both computing `HashCode.Combine(x, y, Width)` as a per-tile seed. **`System.HashCode.Combine` is deliberately randomized per process** (a documented anti-hash-flooding security feature - .NET's own docs warn the same inputs can produce different output across runs) - it is not a general-purpose deterministic hash, and using it as an RNG seed silently reintroduces process-level randomness under an apparently-seeded call. Replaced both call sites with a plain deterministic combine (`x * 374761393 + y * 668265263 + Width * 1274126177`, unchecked int arithmetic). After that fix, world layout, camp position, and the NPC's name/position were bit-identical across three separate process runs of the same seed. `HerdPopulator._rng` was a second, independent gap (unseeded, so herd count/composition still varied run to run even after the `HashCode.Combine` fix) - fixed the same way as the other two.

**Takeaway for future work with this harness:** `HashCode.Combine`/`object.GetHashCode()` must never be used as a seed or persisted identifier anywhere in generation code - it isn't one, by design. If a future change reintroduces "same seed, different result," this is the first thing to check, along with any other unseeded `static readonly Random` fields (`grep -rn "new Random()" --include=*.cs .` is the quick way to audit for them).

### Validated result: Option 2, 3 seeds, before vs after (now genuinely reproducible)

| Seed | Before: survived (min) | Before: cause | Before: sticks | After: survived (min) | After: cause | After: sticks |
|---|---|---|---|---|---|---|
| 1 | 227 | cold | 1 | 229 | cold | 2 |
| 2 | 294 | cold | 2 | 1045 | cold | 9 |
| 3 | 758 | dehydration | 10 | 1026 | cold | 15 |
| **Total** | **1279** | | | **2300** | | |

Seed 1 is essentially unchanged - a harsh world where the AI change doesn't have room to help. Seeds 2 and 3 improved substantially (3.6x and 1.4x respectively), with more fuel actually gathered in both. Total minutes survived across the fixed seed set rose ~80%. **Option 2 is kept.** This is a small sample (3 seeds) and should not be over-read as a precise multiplier, but the direction and the mechanism (fewer wasted hops, no backtracking, searches that don't abort mid-errand by chance) both match what was intended, and it cost no new tracked state.

### Where this leaves things relative to the actual target

The user's stated goal, given directly during this pass, is different from "the NPC survives 72 hours": an *interesting* AI with wide variance in outcomes is fine, even desirable, but the average survival time should be around **7 days** (10,080 minutes) rather than the current best-case of about 17 hours. Every run in every batch and every seeded comparison in this document, before and after every hypothesis, has died well under 24 hours. The user initially flagged dehydration as the suspected priority failure mode; **Part 7 below, with a larger sample, shows cold is actually the dominant killer** - see there before prioritizing H9.

---

## Part 7 — Behavioral fingerprint metrics, and the personality × seed test matrix

Two things were added on top of Part 6: a wider set of harness metrics aimed specifically at telling NPCs apart from each other (not just measuring whether they survived), and a repeatable test structure (personality profile × fixed seed set) for hill-climbing future AI changes with real statistical power instead of single-run anecdotes.

### New metrics on `SimulationSummary`

Beyond the survival/fire/forage metrics from Part 2, `NPCSimulation.cs` now tracks, per run:

| Metric | What it answers |
|---|---|
| `MaxDistanceFromCamp` | How far this NPC ever ranged from home - the direct test of "do bolder NPCs travel further" |
| `UniqueTilesVisited` | Actual ground covered, distinct from... |
| `TotalTilesMoved` | ...total path length including backtracking - restlessness vs. purposeful range |
| `WaterGatheredL` | Inventory water gained (mirrors `SticksGathered`/`TinderGathered`) |
| `ItemsCrafted` | Count of completed `NPCCraft` actions |
| `ShelterImprovements` | Count of completed `NPCImproveShelter` actions |
| `CombatEngagements`, `CombatVictories`, `FleeCount` | Fight-vs-flight behavior and outcomes |
| `HarvestMinutes`, `ChopMinutes` | Time in each work type, alongside `ForageMinutes` - a work-diversity profile, not just one number |

`NPCSimulation.Create` also gained an optional `Personality? personality` parameter that overrides the randomly-rolled one after creation (the random roll still happens first, so it doesn't shift any other draw's position in the seeded sequence) - this is what makes holding every other random factor fixed while varying only personality possible.

### Ideas not yet implemented, for when they're needed

A few more heuristics worth adding if a future hypothesis needs them, not built now because nothing yet calls for them:
- **Idle ratio** - fraction of lifetime spent in `Resting`/no-op vs. doing anything else; a "how listless is this NPC" number distinct from `ColdIdleMinutes` (which is specifically cold-while-idle).
- **Action-type diversity** - count of distinct action names used across a lifetime; a robotic AI repeats 2-3 actions forever, a varied one should show more.
- **Day/night activity split** - does the NPC actually rest more at night, or is behavior time-blind?
- **Near-miss count** - minutes spent below a critical threshold (e.g. `WarmPct < .1`) that did *not* end in death; measures resilience/recovery, not just eventual failure.
- **Cache utilization ratio** - final cache stock vs. `IsEnoughStockpiled`'s own target, to see how close stockpiling gets before something interrupts it (directly relevant to H9).

### Why only Boldness was varied

`Personality` has three fields - Boldness, Selfishness, Sociability - but Selfishness and Sociability currently have **no effect at all** in this single-NPC harness. Both only matter in multi-actor logic (`WouldDefend`, `DecideToHelpInCombat`, relationship memory), and there is only ever one NPC here with no one to be selfish or social toward. They were held at fixed midpoint values (0.35 / 0.65) across all profiles for this reason - varying them would have measured nothing. If a future test adds a second NPC or the player as a combat ally, those two traits become worth varying too.

### Result: `PersonalityMatrix_OneWeek` - 3 profiles × 10 seeds × 7 simulated days each (30 runs, ~9s)

Profiles: Timid (Boldness .15), Baseline (.50), Bold (.85); Selfishness/Sociability fixed at .35/.65 for all three.

| Profile | Survived (days) | Died | MaxDist (tiles) | Tiles visited | Tiles moved | Sticks | Water (L) |
|---|---|---|---|---|---|---|---|
| Timid | 0.4 | 100% | 2.8 | 11.1 | 25.0 | 7.2 | 13.5 |
| Baseline | 0.4 | 100% | 3.6 | 10.9 | 28.4 | 7.9 | 11.5 |
| Bold | 0.5 | 100% | 4.1 | 12.2 | 24.8 | 8.7 | 12.6 |

Death causes (10 seeds each):

| Profile | Cold | Dehydration | Other |
|---|---|---|---|
| Timid | 5 | 5 | — |
| Baseline | 5 | 4 | 1 (head trauma) |
| Bold | 8 | 2 | — |

**Two findings:**

1. **Personality already produces measurably different behavior**, in the expected direction, with no further changes: `MaxDistanceFromCamp` rises monotonically with Boldness (2.8 → 3.6 → 4.1), `SticksGathered` rises with it too (7.2 → 7.9 → 8.7, more range means more forage opportunity), and survival ticks up slightly (0.4 → 0.4 → 0.5 days). This is a real, if small, personality signal riding entirely on `IsBeyondExploreLeash`'s boldness-scaled leash from Part 6 - nothing else in this pass targeted "make bold and timid NPCs act differently."
2. **Cold is the dominant killer, not dehydration** - 18 of 30 runs (60%) died of cold, 11 of 30 (37%) of dehydration, and one single run produced a third, distinct cause ("head trauma," likely hazardous-terrain injury during travel, not investigated further). This directly contradicts the Part 6 closing note, which was based on a 3-seed sample. **H9 (water stockpiling never runs) is real but secondary** - fixing it would help roughly a third of runs, while the majority still die of cold, at survival times (0.4-0.5 days) nowhere near the 7-day target. The next hypothesis to test with this harness should target cold survival specifically, not water.

Per-seed logs for every one of the 30 runs are at `matrix-<profile>-seed<N>.log` under the harness's log directory, for reading transcripts to judge variety/"robotic-ness" directly rather than only from aggregate numbers.

---

## Part 8 — More fingerprint metrics, and group survival

### The remaining "not yet implemented" metrics from Part 7, now implemented

All added to `SimulationSummary` in `NPCSimulation.cs`:

- `IdleRatio` - fraction of lifetime spent `Resting` or with no action at all.
- `DistinctActionTypes` - count of distinct action *categories* used across a lifetime, via a `NormalizeActionType` helper that collapses `"Traveling to X"` for every destination `X` into one `"Traveling"` category first (otherwise this number is inflated by how many different places an NPC happened to walk to, not by actual behavioral variety).
- `NightRestPct` / `DayRestPct` - fraction of night-hours (before 5am or from 9pm) and day-hours respectively spent resting. A time-blind AI should show these roughly equal; a good one should rest far more at night.
- `NearMissCount` - times `WarmPct` dropped below .10 and later recovered to .30+ without the NPC dying. Resilience, not just eventual failure.
- `CacheUtilizationPct` - final cache stock against `IsEnoughStockpiled`'s own targets (Fuel 40kg, Water 6L, Food 2kg, duplicated as constants in the harness since the originals are private to `NPC.cs`), averaged and capped per category. Directly measures whether stockpiling ever gets anywhere - this is the metric H9 should move.

### `NPCGroupSimulation` - multiple NPCs sharing one camp

A new, separate harness class (`text_survival.Tests/Support/NPCGroupSimulation.cs`) rather than bolting group support onto `NPCSimulation` - group runs need a genuinely different shape (N members with independent outcomes, no single decision trace to log) and the existing single-NPC harness is exercised by several tests already, so it stays untouched.

`NPCGroupSimulation.Create(npcCount, seed, personality)` spawns the usual first NPC via `GameContext.CreateNewGame`, moves it onto the camp tile, and adds `npcCount - 1` more directly at camp via `NPCFactory.CreateTestNPC(camp, map, camp)` - each with their own independently-rolled inventory and (unless `personality` is given, which applies one profile to the whole group) their own independently-rolled personality. Nothing about "sharing a fire" needed new game logic: `GameContext.UpdateInternal` already updates every NPC in `ctx.NPCs` each tick, and every member's `Camp` is the same `Location` object, so they were always going to read and act on the exact same `HeatSourceFeature`/`CacheFeature` instances the moment more than one of them stood there. `Run()` tracks each member's own survival minute and death cause independently (keeping its own `NPC` references rather than relying on `ctx.NPCs` membership, since dead NPCs get removed from that list mid-run) and stops early once every member is dead.

### Result: `GroupSize_OneWeek` - sizes 1-4, 10 seeds, one simulated week each (40 runs, ~28s)

| Group size | Avg survived (days) | Any member survived the week | Avg cache fuel (kg) | Avg cache water (L) |
|---|---|---|---|---|
| 1 (solo) | 0.38 | 0% | 1.3 | 0.0 |
| 2 | 0.49 | 0% | 2.8 | 0.0 |
| 3 | 0.51 | 0% | 12.1 | 0.0 |
| 4 | 0.56 | 0% | 13.2 | 0.0 |

**Groups do help, and for a reason worth naming precisely: there is zero cooperation logic in this codebase.** Every NPC runs the identical solo decision loop from Parts 1-7, oblivious to what any other NPC in the group is doing. The improvement here is a pure emergent effect of having more independent agents attempting the same fire-tending/foraging loop at the same shared camp: when one NPC is off gathering fuel, another may already be back tending the fire, so `TryMaintainCampFire` doesn't win the race against `Stockpile` every single time the way it effectively does for a lone NPC. Average survival rises about 47% from 1 to 4 members, and average cache fuel jumps roughly 10x from size 1 to size 3 - a real, measurable effect from headcount alone.

**Water stockpiling stays at exactly 0.0L at every group size.** This is the strongest evidence yet for H9: it isn't bad luck or a solo-NPC resource-math problem that more hands can fix - `DetermineWork`'s stockpile chain structurally never reaches Water (Fuel is checked first, and `TryMaintainCampFire`, now also checked first as of Part 6's H2, keeps returning non-null whenever the fire needs attention, which is most of the time). Adding NPCs doesn't route around this bug because every NPC runs the exact same broken priority order - it just makes Fuel accumulate through sheer repetition while Water never gets attempted at all, by anyone.

**No group size gets anyone through a full week.** Even at 4 members, `AnySurvivedWk%` is 0 - the current AI's ceiling is under a day and a half on average regardless of headcount. Grouping is a real, useful lever (worth keeping in mind as a lategame/design feature, not just a test scenario) but it does not substitute for fixing the underlying AI. The next concrete step: fix `DetermineWork`'s stockpile ordering (or `TryMaintainCampFire`'s precedence over it) so Water gets attempted at all, then re-run both `PersonalityMatrix_OneWeek` and `GroupSize_OneWeek` to see how much of the remaining gap to 7 days that closes.

---

## Part 9 - H14-H18, and the measurement bug that invalidated Parts 5-8

### The harness was never actually reproducible

Before any of H14-H18 could be scored, an ablation of the same configuration against the
same seeds, run three times, produced **1.47 / 0.99 / 1.67 days**. A +/-35% spread on
identical inputs - larger than most effects being measured. Every comparative number in
Parts 5-8 was taken on that harness and should be treated as indicative only.

Root cause, found by bisecting on RNG draw counts: **16 unseeded `Random` instances**, plus
214 `Random.Shared` call sites. `Utils.Seed` only ever reseeded `Utils.random`, so most of
the simulation - forage yields, herd behaviour, combat, animals, event variants - drew from
streams that simply continued from wherever the previous run in the process had left them.
The Part 6 "cross-process determinism" check passed only because it compared NPC *names*,
which do come from the seeded stream.

A second, smaller source: `ResourceMemory` stored known locations in `HashSet<Location>`,
and `Location` has no `GetHashCode`, so iteration ran in allocation order and leaked into
every "closest known source" tie-break.

Fixes: all simulation randomness routed through `Utils.Rng` (world generation's deliberately
seeded RNGs and the UI particle renderer left alone); resource memory changed to an
insertion-ordered `List`; `GetClosestKnownResource` ties broken by map position. The
determinism check now returns **1.13 / 1.13 / 1.13** - a noise floor of exactly zero, so any
difference at all is now signal. `Determinism_SameConfigThrice` keeps it that way.

*Anything random added from here must draw from `Utils.Rng`. A single `Random.Shared` or
`new Random()` anywhere in the simulation silently destroys reproducibility for everything.*

### The three fixes (group size 2, 10 seeds, one simulated week, zero noise)

Named by what they do. Earlier parts of this document number hypotheses H1-H13; that
numbering is historical and not worth carrying forward.

| Variant | Avg survived | vs base | Cache water | Deaths |
|---|---|---|---|---|
| baseline | 0.42 d | - | 0.0 L | dehydration 11, cold 9 |
| hydration units | 0.69 d | +62% | 0.0 L | dehydration 10, cold 10 |
| critical interrupt | 0.41 d | -3% | 0.0 L | dehydration 11, cold 9 |
| water reserve | 0.50 d | +17% | 0.0 L | cold 12, dehydration 8 |
| **all three** | **1.19 d** | **+180%** | 0.0 L | **cold 17, starvation 3** |
| *(rejected)* independent stockpile order | 0.41 d | -3% | 1.4 L | cold 11, dehydration 9 |
| *(rejected)* need timeout | 0.42 d | 0% | 0.0 L | dehydration 11, cold 9 |

Leave-one-out from all five candidates: without the hydration fix 1.13 -> 0.43, without the
interrupt fix -> 0.86, without the water reserve -> 0.98; removing either rejected change
left the result the same or better.

**Drinking water did nothing. Adopted.** `Body.AddHydration` takes millilitres and maxes at
4000. The player path converts litres first (`x WaterHydrationPerLiter`); `NPCDrinkWater`
passed litres straight through, so an NPC drinking half a litre gained **0.5ml of 4000**.
NPCs could not rehydrate by drinking at all. The fallback that should have caught this was
broken too - `ConsumptionHandler.EatDrink` had no `case Resource.Water`, so it silently did
nothing. NPCs now drink to fill the room available, capped at the same `MaxDrinkLiters` the
player uses.

**Nothing could interrupt anything. Adopted.** The guard read
`if (CurrentNeed <= NeedType.Food) return false`, and `NeedType` runs `Warmth=0 .. Food=3`,
so it was true for every real need. Meant to stop critical-vs-critical thrash, it blocked
*all* interrupts, latching an NPC onto one need for up to an hour - long enough to keep
foraging while dying of thirst. Now only a strictly higher-priority need cuts in. Worth
little alone (-3%) but 24% of the combination.

**NPCs left camp with no water. Adopted.** Snow can only be melted at a fire, so an NPC that
melts only once already thirsty must happen to be standing at a fire the moment thirst
hits - which never happens out foraging. They now top up to 2L before leaving.

**Independent stockpile ordering. Rejected.** The only thing that ever got water into the
camp cache (1.4L vs 0.0L), but it cost ~5% survival: carried water is worth more than cached
water, because thirst strikes in the field. Worth revisiting if NPCs ever survive long
enough for a camp reserve to matter.

**Need timeout. Rejected.** Zero effect alone or in combination. The interrupt fix already
breaks the latch it was designed to break, so nothing ever reaches the timeout.

### What now kills them

Dehydration goes from 55% of deaths to **zero at the tuning target**, replaced by cold and
starvation - the varied, seed-dependent death profile that was the goal. Measured after
adopting the three fixes:

| Group size | Avg survived | Fuel gathered | Deaths |
|---|---|---|---|
| 1 | 0.39 d | 11.2 kg | cold 10 |
| 2 | 1.19 d | 72.3 kg | cold 17, starvation 3 |
| 3 | 1.53 d | 104.1 kg | cold 18, starvation 11, dehydration 1 |

Survival roughly triples at the tuning target and fuel gathered nearly triples, but 1.19
days is still well short of the 7-day goal and nobody survives a week at any group size.
**Cold is now the binding constraint**, so that is where the next hypotheses belong.
