# Effects verification

How effects code is held to the bar in `emergent-world-design.md` §3.3: property tests in the
normal suite, a reference model, and mutation testing as a separate command.

## Running it

```bash
dotnet test rebuild/Sim.Tests                                   # property tests run with everything else
dotnet test rebuild/Sim.Tests --filter "FullyQualifiedName~EffectsPropertyTests|FullyQualifiedName~Survival.Tests.Verification"

dotnet tool restore                                             # once: Stryker from .config/dotnet-tools.json
cd rebuild/Sim.Tests
dotnet stryker                                                  # Effects.cs, fails under 90%
dotnet stryker --config-file stryker-config.body.json           # BodySurvival.cs, report only
```

Each Stryker run takes under a minute for `Effects.cs` and several minutes for
`BodySurvival.cs`. Reports land in `rebuild/Sim.Tests/StrykerOutput/` (ignored by git). Open
`reports/mutation-report.html` to see the mutants that survived.

**Replaying a failure.** CsCheck uses a new random seed on every run and, when a property fails,
shrinks the input and prints it with its seed:
`Set seed: "asQ2YGi3KzQ1" or -e CsCheck_Seed=asQ2YGi3KzQ1 to reproduce`. Rerun that one test
with `CsCheck_Seed=asQ2YGi3KzQ1 dotnet test ... --filter <name>` and it sees the same input.

## Files

- `rebuild/Sim.Tests/Verification/Gens.cs` has the generators and the `Check` helper (400
  iterations, single-threaded).
- `rebuild/Sim.Tests/EffectsPropertyTests.cs` has the laws for the registry in `Effects.cs`.
- `rebuild/Sim.Tests/Verification/BodySurvivalPropertyTests.cs` has the laws for wetness, drying
  and core temperature in `BodySurvival.cs`, and the wetness reference model.
- `rebuild/Sim.Tests/stryker-config.json` and `stryker-config.body.json` are the two Stryker runs.
- `rebuild/Sim.Tests/mutation-paths.txt` lists what CI's mutation run depends on besides each config's `mutate` sources; each new Stryker config adds its tests and content data there.

## Generators and their ranges

Every range is one the simulation can actually hand the body, so every failure is a case the game
can reach.

| Generator | Range | Why |
|---|---|---|
| `Kind` | every `EffectKind` | |
| `Severity` | (0, 1] | a held effect; zero removes it |
| `RawSeverity` | mostly [0,1], sometimes [-1,2], with 0 and 1 weighted | what callers pass; Add and Set clamp |
| `EffectOps` | 0-40 Add/Set/Decay calls, Decay 0-240 min | the reachable effect states |
| `Minutes` | 0-2880 | up to two days; the game decays one minute at a time |
| `BodyInputs.CoreTempF` | 88-103 °F | deep hypothermia to high fever; every threshold sits inside |
| `BodyInputs` wet, buffer | [0, 1] each | |
| `BodyInputs` kcal, ml, energy | 300-2000, 500-4000, 490-960 | above the regeneration gates and never zero, so no rng-driven organ damage fires |
| `BodyInputs.InjuryAmount` | 0, or a 1-20 blunt blow to the left leg | something for sleep to heal |
| `AirTempF` | -50 to 95 °F | the coldest ice-age night with wind chill to a midsummer afternoon |
| `WindLevel`, `FireDryingWindLevel` | [0, 1]; the pit wind is null or at least `WindLevel` | Sim drops only the shelter term from it |
| `FireBonusF` | 0, or 0-100 °F | nothing to a roaring pit in a held shelter |
| `TorchHeatF` | 0, or 0-15 °F | |
| `ClothingClo`, `ClothingWeightKg` | 0-4 clo, 0-10 kg | naked to full megafauna furs |
| waterproofing, cover, ground protection, bedding quality | [0, 1] | |
| `Precipitation`, `PrecipitationPct` | every kind; the intensity is 0 when nothing falls | |
| `GroundWettingPctPerMinute` | 0, or 0-0.12 | `Ground.ContactWettingRate`'s largest rate, walking ankle-deep water |
| `ActivityFactor` | 0.5-2, and 0.5 when asleep | asleep to hard labour |

## The laws and where they live

**Bounds.** Any call sequence leaves one effect per kind, each in (0, 1]
(`Bounds_AnyCallSequence_...`). Any run of conditions keeps wetness and the clothing buffer in
[0, 1], every effect in range, and the core finite (`Bounds_HoldThroughAnyRunOfConditions`).

**Time-splitting.** Which paths step by minutes:

- `Effects.Decay(minutes)` takes a duration and splits exactly, up to rounding: N one-minute
  steps equal one N-minute step, and any split in two equals the whole
  (`Decay_TimeSplits_...`).
- `Body.Process` and `Body.ProcessTemperature` take `minutes`, but each is one explicit step
  whose state-dependent switches are evaluated at the start. Wetness splits exactly out of a sweat
  (`TimeSplitting_Wetness_IsExactOutOfASweat`). In a sweat it is forward Euler, within the
  bound ½N²B|rate|(1+B)^N (`..._DriftsInASweat_WithinTheEulerBound`). The core splits only
  away from its switches: the buffer gate at 98.6 °F, fire withheld at 99 °F, and vasomotor tone
  between 97 and 99 °F (`TimeSplitting_CoreTemperature_HoldsAwayFromTheSwitches`). Across them a
  long step is wrong by more than a degree.
- `Body.Update`, `Body.Tick` and `Body.TickWounds` are one minute only. The game calls nothing
  else, so the multi-minute forms are reached only from tests.

**Direction.** More precipitation never dries you. More wind, at the body or past the pit,
never dries you slower. Warmer air never dries you slower. More cover or bedding never wets you
more, and wetter ground never dries you. Colder air never warms the core faster, and more fire
heat never cools it faster. Decay never raises a severity or starts an effect. Add never lowers
one. "More fire or torch heat never dries slower" holds only at or below 60 °F air. The
full-range version is skipped until the drying bug below is fixed.

**Fixed point.** Left alone, every fading effect is gone, every effect that needs treatment rests
at its floor, wetness holds, and further decay changes nothing (`Decay_SettlesAtAFixedPoint`).
Under steady exposure wetness moves one way only and stays at whichever end it reaches
(`FixedPoint_WetnessIsMonotone_AndItsEndsAbsorb`). Within two days the core has either settled
or is leaving the living range (`FixedPoint_TheCoreSettlesOrLeaves_ItNeverOscillates`).

**Every input matters.** For each `SurvivalContext` property there are states where changing it
alone changes what one minute does to the body: the `Process` delta, plus the healing that
`Update` applies (`EveryInput_ChangesSomething`). `EveryInput_IsListed` fails when a property is
added to `SurvivalContext` and nobody lists it, either with a way to vary it or in `Irrelevant`
with a reason. This is the test that would have caught heavy snow that never wet you, bedding
warmth that was never read, and shelter wind cover that was never read.

**Merge.** `Add` with an existing effect keeps the worse of the two, except `Bleeding`, which
sums and caps at 1. Both are commutative, including whether the effect needs treatment. The max
always equals one of its inputs. The bleeding sum is at least the worse input and at most 1
(`Add_Merges_Commutatively_WithinItsBounds`). `Set` is a level: the last write wins and zero
removes the effect. An effect needs treatment once it has been strictly above its table's
threshold, and it keeps needing it until it is removed (`Add_NeedsTreatment_...`,
`Add_TreatmentThreshold_IsStrict`, `Set_IsTheLevel_...`). The readouts (`CapacityDeltas`,
`CoreTempFPerHour`, `BleedMlPerHour`) are each effect's table row scaled by severity and summed
(`Readouts_AreSeverityScaledSumsOfTheTable`).

**Reference model.** `WetnessReference` restates the wetness physics: water from the sky and the
ground, sweat that cannot evaporate, and drying paid for in latent heat. With the core held, the
one-minute step is the linear recurrence w' = w(1+B) + A. Its closed form is
w_n = (w0 + A/B)(1+B)^n - A/B, or w0 + nA when B is 0. It is clamped once, which is correct
because the trajectory is monotone. The real code matches it within 1e-9 over a day, stepped
minute by minute (`ReferenceModel_StepByStepWetness_...`) and through `Body.Update` out of a
sweat (`ReferenceModel_UpdateWetness_...`). The constants are the model's. When the exposure
model changes, the reference is updated deliberately, as the spec.

## The bar

The effects kernel's mutation score must be at least 90%. `stryker-config.json` sets
`break: 90`, so `dotnet stryker` fails below it. Each surviving mutant is either killed by a new
test or excused as equivalent, with a one-line note in the PR. `BodySurvival.cs` is reported, not
gated, until the exposure kernel replaces its wetness and drying. That kernel is then held to 90%
too.

### Scores (2026-11-21)

- `Effects.cs`: 98.15% on the first run. The one survivor was the text of the NaN exception
  message. It is now 100% (54 of 54 killed), with no equivalent mutants to excuse.
- `BodySurvival.cs` baseline is 18.4% for the whole file. The verification tests do not aim at
  starvation, regeneration or `Tick`. By region: wetness and drying 93% (40 of 43), the heat
  balance 37%, `ProcessTemperature` 10.5%. The laws only constrain the direction of the core
  temperature change, not how the clothing buffer works.
- Wetness/drying survivors:
  - `AirTempF >= 32` is equivalent: at 32 °F the air contributes 0 kcal either way.
  - `exposedPct >= 0` is equivalent: zero exposure multiplies the precipitation term to 0.
  - `FireBonusF > 0 ? pit wind : wind` replaced by `true` survives, and it is not equivalent.
    Killing it needs no fire, a pit wind above the body wind, warm dry air and wet clothes all at
    once, which the generators reach too rarely.

## Adding a new effect

1. Add the `EffectKind` and its row in `Content/effects.json`. `EveryEffectKind_HasADefinition`
   fails until you do. The generators pick up the new kind on their own (`Gen.Enum`), so bounds,
   decay, merge, threshold and readout laws all cover it with no test changes.
2. If its severity is a level that the step sets each minute (like `Wet`), give it a direction law
   for each input that drives it, and an entry in the reference model if its steady state has a
   closed form.
3. If it reads a new exposure input, add the input to `SurvivalContext` (or `Exposure`).
   `EveryInput_IsListed` then fails until the input has a variation in `Inputs`, and
   `EveryInput_ChangesSomething` proves that the input reaches the body.
4. If it merges differently from max (as bleeding sums), add the rule to
   `Add_Merges_Commutatively_WithinItsBounds`.
5. Run `dotnet stryker` and kill or excuse every survivor.

Save migration tests are deliberately not part of this setup: save compatibility starts at the first playable release.

## Evidence of effectiveness (2026-11-21)

The first afternoon of property and conservation testing found 11 real bugs that the ~1,100
hand-written tests had not:

| Found by | Bug |
|---|---|
| Effect properties | A treatment flag depended on the order an effect worsened; NaN severity was accepted and stuck; negative decay minutes raised severity |
| Body properties | `ProcessTemperature(N)` was wrong for N > 1 (switch points fixed at the step's start); a fire or torch dried you slower than warm air alone above about 62 °F |
| Random action sequences | A crossing's outcome depended on how the walk was cut into steps (the "barely move" rule was checked only at the start) |
| Conservation laws | A blade wasted more bone than bare hands in quick-strip butchering; adding fuel shortened a fire (mixed fuels burned at the averaged rate); fuel under 1 g was deleted uncounted; a mammoth could never be butchered by hand |

A blind retro-check against older commits with known bugs was planned and dropped: the property
writer had seen the old bug list in its brief and in code comments, so it would not have been
blind, and the suite's API does not compile against old commits without rewriting. The new finds
above are the evidence instead.
