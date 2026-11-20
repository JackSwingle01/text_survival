# Full test-suite benchmark

Three consecutive Release runs, including both opt-in performance tests. The solution was built once before timing; test runs used `--no-build --no-restore`. Normal xUnit parallelism was retained. Timings include test setup and assertions, and are elapsed time, not CPU time. Summed test durations overlap under parallel execution and must not be read as wall-clock savings.

## Runs

| Run | Command wall time | Passed | Failed | Total |
|---|---:|---:|---:|---:|
| 1 | 110.02s | 741 | 0 | 741 |
| 2 | 114.23s | 741 | 0 | 741 |
| 3 | 126.32s | 741 | 0 | 741 |

## Slowest individual test cases

| Test | Median | Min–max |
|---|---:|---:|
| [Environments.WeatherSeasonTests.FirstThreeDays_StayWithinSeasonalRange](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/WeatherSeasonTests.cs:58) | 50.622s | 47.923–72.416s |
| [Survival.SleepByFireTests.ANightBesideABigFire_DoesNotSweatAwayYourWater](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Survival/SleepByFireTests.cs:18) | 40.378s | 38.105–42.534s |
| [Environments.Surface.BuriedDiscoveryTests.SurfaceAndDiggingStateSurviveASaveAndLoad](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Surface/BuriedDiscoveryTests.cs:256) | 29.736s | 25.072–37.380s |
| [Persistence.SerializationTests.SerializeDeserialize_Herds_PreserveState](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/SerializationTests.cs:564) | 28.250s | 22.737–30.801s |
| [Actions.FireHandlerTests.ManageFire_StartingFromEmber_PutsTheFireOnTheLocation](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Actions/FireHandlerTests.cs:18) | 27.516s | 15.620–30.846s |
| [Environments.Surface.GroundTickProfileTests.AFullDayOfWorldTimeStaysAffordable](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Surface/GroundTickProfileTests.cs:14) | 26.358s | 23.819–29.032s |
| [Desktop.UI.HudTests.WorkOptionsKeepDomainIdsAndEveryOptionIsRepresented](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Desktop/UI/HudTests.cs:125) | 25.353s | 25.337–29.097s |
| [Actions.FireChanceTests.AttemptStartFire_RollsTheChanceTheScreenShows(hour: 3)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Actions/FireHandlerTests.cs:101) | 25.134s | 24.034–25.753s |
| [Crafting.CraftingOverhaulTests.ExistingShelterBlocksBeforeWorkAndLeavesMaterialsUntouched](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Crafting/CraftingOverhaulTests.cs:120) | 24.481s | 21.086–28.407s |
| [Persistence.StatefulSerializationTests.FullGame_PreservesStatefulSystemsAndSharedReferences](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/StatefulSerializationTests.cs:135) | 22.966s | 20.834–26.297s |
| [Desktop.UI.DepletedForageActionTests.DepletedForage_IsShownDisabledWithAReason](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Desktop/UI/DepletedForageActionTests.cs:14) | 21.992s | 21.674–23.858s |
| [Actions.GameLoopSmokeTests.RunAsync_WaitsTravelsAndQuits](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Actions/GameLoopSmokeTests.cs:16) | 21.627s | 19.957–25.984s |
| [Environments.Grid.SettlingPassTests.HerdTrafficWearsTrailsIn(seed: 42)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:19) | 20.119s | 18.727–23.599s |
| [Cli.GameCliTests.Transfer_UseScreenCallbacks](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Cli/GameCliTests.cs:249) | 16.658s | 16.650–18.089s |
| [Persistence.SerializationTests.SerializeDeserialize_NewGame_RoundTrips](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/SerializationTests.cs:19) | 14.938s | 13.931–19.542s |
| [Crafting.CraftingOverhaulTests.MissingSupplyNavigationReturnsToTheOriginalRecipe](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Crafting/CraftingOverhaulTests.cs:64) | 13.384s | 13.296–16.762s |
| [Actions.FireHandlerTests.ManageFire_AddingFuel_ReachesTheLocationFire](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Actions/FireHandlerTests.cs:46) | 12.036s | 8.358–13.596s |
| [Environments.WeatherSeasonTests.NewGame_StartsInItsOwnSeasonsTemperatureRange](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/WeatherSeasonTests.cs:30) | 11.233s | 11.091–18.853s |
| [Companions.CompanionReliabilityTests.SearchAtAnEmptyLeadTerminatesThroughRealTicks](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Companions/CompanionReliabilityTests.cs:244) | 10.565s | 10.189–11.089s |
| [Environments.Grid.SettlingPassTests.HerdTrafficWearsTrailsIn(seed: 7)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:19) | 10.166s | 7.923–11.876s |
| [Actions.FireChanceTests.AttemptStartFire_RollsTheChanceTheScreenShows(hour: 14)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Actions/FireHandlerTests.cs:101) | 9.072s | 9.010–21.932s |
| [Environments.Grid.SettlingPassTests.HerdTrafficWearsTrailsIn(seed: 1)](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Environments/Grid/SettlingPassTests.cs:19) | 9.041s | 7.809–14.496s |
| [Cli.GameCliTests.Crafting_UseScreenCallbacks](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Cli/GameCliTests.cs:279) | 8.832s | 8.185–16.419s |
| [Persistence.SerializationTests.SerializeDeserialize_DeserializedGame_CanContinuePlaying](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Persistence/SerializationTests.cs:238) | 8.116s | 8.057–8.646s |
| [Cli.GameCliTests.Saves_AreIsolatedAndOnlyAllowedAtMapDecisions](/Users/jackswingle/Documents/GitHub/text_survival/text_survival.Tests/Cli/GameCliTests.cs:324) | 7.592s | 7.504–9.657s |

## Most expensive test classes

Totals below sum each class’s test durations within a trial, then take the median across trials.

| Class | Cases | Median summed time |
|---|---:|---:|
| Cli.GameCliTests | 18 | 74.760s |
| Environments.WeatherSeasonTests | 3 | 63.775s |
| Persistence.SerializationTests | 18 | 60.996s |
| Environments.Grid.SettlingPassTests | 7 | 54.111s |
| Crafting.CraftingOverhaulTests | 12 | 44.085s |
| Actions.FireHandlerTests | 2 | 41.112s |
| Survival.SleepByFireTests | 1 | 40.378s |
| Actions.FireChanceTests | 2 | 34.763s |
| Environments.Surface.BuriedDiscoveryTests | 15 | 29.744s |
| Environments.Surface.GroundTickProfileTests | 1 | 26.358s |
| Desktop.UI.HudTests | 14 | 25.470s |
| Persistence.StatefulSerializationTests | 9 | 22.996s |
| Desktop.UI.DepletedForageActionTests | 1 | 21.992s |
| Actions.GameLoopSmokeTests | 1 | 21.627s |
| Desktop.UI.CombatKeyboardTests | 12 | 19.603s |

## Failures

All tests passed in all three runs.

## Isolated checks

Each of the three slowest cases was then run once by itself in a fresh test process, with the same Release binary. These are single observations, not isolated medians. All three passed.

| Test | Full-suite median | Isolated |
|---|---:|---:|
| Environments.WeatherSeasonTests.FirstThreeDays_StayWithinSeasonalRange | 50.622s | 28.194s |
| Survival.SleepByFireTests.ANightBesideABigFire_DoesNotSweatAwayYourWater | 40.378s | 6.448s |
| Environments.Surface.BuriedDiscoveryTests.SurfaceAndDiggingStateSurviveASaveAndLoad | 29.736s | 5.412s |

The large differences indicate substantial parallel-load overhead. Use full-suite times to describe this run, and do not assume deleting one test saves its full listed duration.

## Next optimization candidates

- WeatherSeasonTests.FirstThreeDays_StayWithinSeasonalRange advances the entire game for 72 hours but only asserts weather temperature. Preserve the initialization regression check, then consider advancing Weather directly for the long-range check. It still costs 28.2 seconds alone.
- SleepByFireTests simulates 480 minutes of a full world to inspect one sleeper and one fire. A small game with explicitly matched clothing, temperature, and fire conditions could preserve that regression coverage.
- BuriedDiscoveryTests.SurfaceAndDiggingStateSurviveASaveAndLoad generates and serializes a whole world to inspect the local surface and digging state. A small map with an explicit hidden discovery is a candidate.
- HUD, depleted-forage UI, and fire-chance tests create whole worlds for narrow assertions. They are good candidates for the existing SmallGame fixture.
- GameCliTests has the largest summed class time: 74.8 seconds across 18 cases. Its World helper creates a full new game repeatedly; preserve real-loop coverage while reviewing which overlay/log/prompt cases need that setup.

These are recommendations based on source inspection, not measured savings from changes. No test or production code was changed during this benchmark.

## Environment

macOS arm64, .NET SDK 10.0.400, .NET test runtime 10.0.11. The successful incremental Release build reported 0.81 seconds and is excluded from trial wall times. The initial sandbox attempts could not bind the runner’s local socket and executed no tests; they are excluded. Measured trials ran with the required local process permissions.

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
  --settings /tmp/text-survival-full-benchmark/all.runsettings \
  --logger "trx;LogFileName=trial.trx" --results-directory /tmp/text-survival-full-benchmark
```

The captured trials additionally enabled normal console logging and a 120-second inactivity hang collector. Raw TRX files and console logs: `/tmp/text-survival-full-benchmark/`. Complete per-test and per-class measurements: [timings.json](/Users/jackswingle/Documents/GitHub/text_survival/dev/benchmarks/test-suite-2026-09-24/timings.json).
