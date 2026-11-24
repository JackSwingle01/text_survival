# #197: A balance report of the faculties for reference bodies

## Hypothesis

Today the four faculties are `Body.Strength`, `Body.Speed`, `Body.Dexterity` and `Body.Perception`
(Body.cs), and vitality is `Body.Vitality`. Strength and speed carry a person's kilogram terms, so the
world reads them as a share of a fresh member (`SpeciesTable.StrengthPct`, `SpeciesTable.Running`).
The report divides each faculty by the same faculty of `SpeciesTable.Average(species, sex)`, builds
each reference body with the sim's own code (tissue condition for destroyed limbs and the brain,
`Body.Damage` for blood, `Body.Update` minute by minute for deprivation, with the other needs kept
topped up through `Body.Drink`/`Body.Eat`), and prints one row each. Nothing in `rebuild/Sim`
changes. The printing test is a `ReportFact("BALANCE_REPORT")`, like `NpcBenchReport`; a quick
`[Fact]` builds the table and checks every row's name appears, asserting no number.

Expected today: destroyed arms cut dexterity (manipulation) and a little strength; destroyed legs
cut speed and some strength; half blood cuts vitality hard (circulation fails at 50%); the head blow
cuts perception; awake and thirst show through Tired/Thirsty effects only once their stores are under
30%; starving shows through composition (strength, speed) and Hungry.

## Revised

- Revised: no scout or design-review sub-agent. The change is one new test file reading public
  Body functions, with no sim behaviour touched; I read the code they would have read. The
  code-review step still runs on the branch.
- Revised: the issue says "a test runs the report and checks every reference body appears" and "the
  report does not run in the PR checks unless asked for". Decided: the printing test is gated by
  `BALANCE_REPORT`; the row check is a quick ungated `[Fact]` that builds the table without printing
  it, so a renamed or broken reference body fails on the PR rather than in a report nobody runs. If
  it is too slow for a quick test (the starving body steps weeks of minutes) it moves behind the gate.
- Revised: energy has no sim call that refills it short of sleeping, so a body kept rested while
  another need is stepped has `EnergyMinutes` set to the maximum each hour. Water and food go
  through `Body.Drink` and `Body.Eat`.
- Revised: "fresh body of the same species and sex" is `SpeciesTable.Average` (the baseline man for
  the player, the species row for the wolf), the same body `SpeciesTable.StrengthPct` divides by.
  The player's own body in the world is `SpeciesTable.NewBody`, which draws its fat; the report uses
  the baseline so its numbers do not depend on a seed.
- Revised (review): strength and speed now read `SpeciesTable.StrengthPct` and
  `SpeciesTable.Running(...).SpeedPct`, the shares the world reads, instead of restating them. Speed
  therefore shows Running's 0.1 floor (half blood: 0.10, not 0.00). Dexterity and perception have no
  sim share, so they still divide by `SpeciesTable.Average`. `Destroyed` throws on a region name that
  matches nothing. Fields renamed to `...Pct`. The three stepping loops became one `Live` with a
  `Kept` record. Kept the ungated row check (353 ms) against the review's scope note: it is the
  issue's "a test runs the report and checks every reference body appears", and gating it too would
  mean it never runs.
- Revised: the result differs from the hypothesis in two places. Half its blood lost is death today
  (`CirculationFailsBloodPct` is 0.5, so every faculty and vitality are 0), not just "cut hard". And
  sixteen and forty hours awake print the same: the energy store is 960 minutes, exactly sixteen
  hours, so both bodies are fully drained and Tired is at 1.

## Follow-up

- Follow-up: the half-blood row is a dead body, so it says nothing about a bleeding survivor. If
  #201 keeps 50% as the line, a "40% of its blood lost" row would show the band before it.
- Follow-up: tiredness reaches its whole effect at the end of a sixteen-hour day, with nothing more
  for forty hours (Tired severity saturates). That belongs to #206 (Deprivation rethink).
