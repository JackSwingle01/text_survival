# Full test-suite benchmark

Three consecutive Release runs, including both opt-in performance tests. The solution was built once before timing; test runs used `--no-build --no-restore`. Normal xUnit parallelism was retained. Timings include test setup and assertions, and are elapsed time, not CPU time. Summed test durations overlap under parallel execution and must not be read as wall-clock savings.

## Before and after

| Configuration | Tests per run | Run times | Median |
|---|---:|---|---:|
| Previous baseline | 741 | 110.0, 114.2, 126.3s | 114.2s |
| Optimized fixtures, default concurrency | 752 | 35.2, 34.2, 33.6s | 34.2s |
| Optimized fixtures, four threads (adopted) | 752 | 33.2, 32.0, 30.8s | 32.0s |

The observed median fell by 72.0%. The working tree gained 11 tests and other concurrent changes since the baseline, so this is a before/after working-tree comparison, not an isolated attribution of every second to this patch. All six optimized full-suite trials passed, including the two performance guards. The four-thread experiment used a temporary assembly-specific xUnit runner JSON with diagnostics enabled; the committed configuration keeps the same cap without diagnostic logging.

## Changes

- Weather still starts from a real generated game. The 72-hour range test now advances Weather at the original one-minute resolution, without simulating unrelated actors and terrain.
- Local CLI, native keyboard UI, HUD, crafting, fire, and surface-serialization tests use independent small game graphs with explicit starting equipment as needed. Full-world initialization, travel, save/load, and settling integration coverage remains.
- The sleep test now starts an actual fire: the old fixture added pine logs and called cold ignition, which only ignites tinder/kindling. It verifies radiant heat while comfortable, no radiant heat just above the sweating threshold, and an active fire during all 480 simulated minutes.
- Surface serialization supplies an explicit hidden discovery and asserts nonzero saved search progress.
- Four parallel collections were slightly but consistently faster across the measured trials and are now configured for this test project.

## Selected test medians with optimized fixtures

The per-test comparison below uses the three optimized default-concurrency runs; the capped runs are an additional suite-level comparison.

| Test | Before | After |
|---|---:|---:|
| FirstThreeDays_StayWithinSeasonalRange | 50.622s | 4.885s |
| ANightBesideABigFire_DoesNotSweatAwayYourWater | 40.378s | 0.405s |
| SurfaceAndDiggingStateSurviveASaveAndLoad | 29.736s | 0.120s |
| WorkOptionsKeepDomainIdsAndEveryOptionIsRepresented | 25.353s | 0.003s |
| DepletedForage_IsShownDisabledWithAReason | 21.992s | 0.003s |

The detailed rankings below retain default concurrency so they can be compared against the original benchmark. Raw capped-run logs and TRX files are in `/tmp/text-survival-capped-benchmark/`.

## Runs

| Run | Command wall time | Passed | Failed | Total |
|---|---:|---:|---:|---:|
| 1 | 35.22s | 752 | 0 | 752 |
| 2 | 34.15s | 752 | 0 | 752 |
| 3 | 33.60s | 752 | 0 | 752 |

## Slowest individual test cases

| Test | Median | Min–max |
|---|---:|---:|
| [Cli.GameCliTests.RealLoop_InspectionDoesNotTick_WaitDoes_TravelUsesTheRealRunner](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Cli/GameCliTests.cs:86) | 16.250s | 12.134–17.300s |
| [Persistence.SerializationTests.SerializeDeserialize_Herds_PreserveState](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/SerializationTests.cs:564) | 15.155s | 13.245–15.600s |
| [Persistence.StatefulSerializationTests.FullGame_PreservesStatefulSystemsAndSharedReferences](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/StatefulSerializationTests.cs:135) | 15.116s | 15.068–16.641s |
| [Actions.GameLoopSmokeTests.RunAsync_WaitsTravelsAndQuits](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Actions/GameLoopSmokeTests.cs:16) | 13.523s | 12.903–14.143s |
| [Environments.Grid.SettlingPassTests.HerdTrafficWearsTrailsIn(seed: 42)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:19) | 13.100s | 12.524–13.921s |
| [Environments.Surface.GroundTickProfileTests.AFullDayOfWorldTimeStaysAffordable](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Surface/GroundTickProfileTests.cs:14) | 12.052s | 11.479–17.500s |
| [Environments.WeatherSeasonTests.NewGame_StartsInItsOwnSeasonsTemperatureRange](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/WeatherSeasonTests.cs:30) | 10.179s | 9.566–15.683s |
| [Cli.GameCliTests.Saves_AreIsolatedAndOnlyAllowedAtMapDecisions](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Cli/GameCliTests.cs:325) | 7.858s | 6.924–9.537s |
| [Cli.BackgroundCliTests.DetachedWorker_PreservesPrompts_DeduplicatesRequests_RejectsStaleDecisions_Stops](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Cli/BackgroundCliTests.cs:24) | 6.184s | 6.147–6.573s |
| [Persistence.SerializationTests.SerializeDeserialize_DeserializedGame_CanContinuePlaying](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/SerializationTests.cs:238) | 5.806s | 4.779–6.103s |
| [Companions.CompanionReliabilityTests.SearchAtAnEmptyLeadTerminatesThroughRealTicks](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Companions/CompanionReliabilityTests.cs:244) | 5.090s | 4.731–5.345s |
| [Environments.Grid.SettlingPassTests.HerdTrafficWearsTrailsIn(seed: 7)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:19) | 4.945s | 4.489–5.260s |
| [Persistence.SerializationTests.SerializeDeserialize_NewGame_RoundTrips](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/SerializationTests.cs:19) | 4.937s | 4.513–6.219s |
| [Environments.WeatherSeasonTests.FirstThreeDays_StayWithinSeasonalRange](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/WeatherSeasonTests.cs:58) | 4.885s | 3.499–5.813s |
| [Environments.Grid.SettlingPassTests.HerdTrafficWearsTrailsIn(seed: 1)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:19) | 4.410s | 3.949–5.159s |
| [Persistence.SerializationTests.SerializeDeserialize_FullGameState_PreservesAllCriticalData](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/SerializationTests.cs:57) | 3.647s | 3.568–3.838s |
| [Persistence.SerializationTests.Serialize_FullGame_CompletesQuickly](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/SerializationTests.cs:275) | 3.554s | 2.770–3.698s |
| [Environments.WeatherSeasonTests.SeasonMatchesStartDate](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/WeatherSeasonTests.cs:45) | 3.542s | 3.300–3.744s |
| [Environments.Grid.SettlingPassTests.PredationDoesNotEmptyTheWorld(seed: 7)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:34) | 3.273s | 2.921–3.276s |
| [Environments.Grid.SettlingPassTests.PredationDoesNotEmptyTheWorld(seed: 42)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:34) | 3.137s | 2.846–3.262s |
| [Cli.GameCliTests.HiddenTiles_DoNotExposeNamesFeaturesOrAnimals](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Cli/GameCliTests.cs:313) | 3.101s | 2.795–3.373s |
| [Companions.CompanionReliabilityTests.TimedLeaderTravel_ReunitesAfterCommittedWork(kind: Npc, count: 4)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Companions/CompanionReliabilityTests.cs:297) | 2.681s | 2.248–2.773s |
| [Environments.Grid.SettlingPassTests.SettlingLandsExactlyOnStartTime](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:46) | 2.146s | 1.911–2.298s |
| [Environments.Grid.SettlingPassTests.PredationDoesNotEmptyTheWorld(seed: 1)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:34) | 2.009s | 1.842–2.071s |
| [Companions.CompanionReliabilityTests.TimedLeaderTravel_ReunitesAfterCommittedWork(kind: Player, count: 4)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Companions/CompanionReliabilityTests.cs:297) | 1.945s | 1.912–1.990s |

## Most expensive test classes

Totals below sum each class’s test durations within a trial, then take the median across trials.

| Class | Cases | Median summed time |
|---|---:|---:|
| Environments.Grid.SettlingPassTests | 7 | 32.684s |
| Persistence.SerializationTests | 18 | 32.602s |
| Cli.GameCliTests | 18 | 26.160s |
| Environments.WeatherSeasonTests | 3 | 19.736s |
| Persistence.StatefulSerializationTests | 9 | 15.419s |
| Actions.GameLoopSmokeTests | 1 | 13.523s |
| Environments.Surface.GroundTickProfileTests | 1 | 12.052s |
| Companions.CompanionReliabilityTests | 20 | 11.303s |
| Cli.BackgroundCliTests | 2 | 6.446s |
| Animals.PredatorInteractionTests | 47 | 4.393s |
| Environments.Grid.GridWorldGeneratorTests | 10 | 4.266s |
| Environments.Grid.ValleyWorldTests | 5 | 4.029s |
| Combat.EncounterTacticsTests | 19 | 3.456s |
| Environments.Grid.CaveContentTests | 7 | 1.440s |
| Companions.FollowingAcceptanceTests | 8 | 1.144s |

## Failures

All tests passed in all three runs.

## Reproduction

All-test settings override the project’s default performance exclusion:

```xml
<RunSettings>
  <RunConfiguration>
    <TestCaseFilter>FullyQualifiedName~text_survival.Tests</TestCaseFilter>
  </RunConfiguration>
</RunSettings>
```

```sh
dotnet build text_survival.sln -c Release --no-restore
dotnet test text_survival.Tests/text_survival.Tests.csproj -c Release --no-build --no-restore \
  --settings /tmp/text-survival-optimized-benchmark/all.runsettings \
  --logger "trx;LogFileName=trial.trx" --results-directory /tmp/text-survival-optimized-benchmark
```

The captured trials additionally enabled normal console logging and a 120-second inactivity hang collector. Raw TRX files and console logs: `/tmp/text-survival-optimized-benchmark/`. Complete per-test and per-class measurements: [timings.json](/Users/jackswingle/Documents/GitHub/text_survival/dev/benchmarks/test-suite-optimized-2026-09-24/timings.json).

## Final project-configured validation

`dotnet test text_survival.sln -c Release --no-restore` passed all 750 functional tests with the checked-in four-thread runner configuration. The reported test duration was 28 seconds. The two opt-in performance tests passed in every full benchmark trial above.
