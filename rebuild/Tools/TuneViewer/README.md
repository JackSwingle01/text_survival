# Tune viewer

Watches a combat tuning run, live or finished, and fights any fight of it again.

```
rebuild/Tools/TuneViewer/tune-viewer rebuild/Tools/Tune/runs/<run> [--port 5177]
```

Open http://127.0.0.1:5177/. The run folder may be given relative to the current directory or to
`rebuild/Tools/Tune`. The page polls `progress.jsonl` every two seconds, so start it beside a running
`Tune combat` to watch generations arrive. A run made before the log (the 2026-11-21 shakedown) shows its
`convergence.tsv` only.

What it shows: fitness (best, median, worst, best so far) and sigma by generation; per scenario, the best
candidate's endings over generations and each band's metric against its target; each parameter's mean,
normalised to its range, with the outer 2% flagged; a generation scrubber that drives all of it; and
replays.

## Replays

A replay is fought again on request, by `Tune replay` in a process of its own (overrides load once per
process), from (generation, candidate or mean, scenario, fight index). The seed is the tuner's:
`Seeds.Fight(generationSeed, scenario, index)`, with `generationSeed = Seeds.Mix(runSeed, generation)`, on
map seed 1000. Fights are pure functions of content, scenario and seed, so this is the fight the tuner scored.

The server refuses (HTTP 409, shown on the page) unless the code here is the run's code: the same commit
and the same SHA-256 over the fight code (`rebuild/Sim`, `rebuild/Tools/Tune`, less `bin`, `obj`, `runs`,
`specs`). The hash, not the dirty flag, decides: a dirty tree whose fight code is byte for byte the run's
fights the same fights. The dirty flag is recorded and shown.

For the best candidate the log has every fight's outcome, so the page shows them as a strip to pick from,
and a replay says whether it matches. The mean is never fought during the search; replaying it fights it
now, on that generation's seeds, and says so.

Round-by-round playback exists today only for fights the harness drives through `Sim.Step` (hunts, and a
person against animals with a scripted policy): `Fights` reads the grid between steps. Headless fights
(`Combat.ResolveHeadless`: packs against prey, AI people) show their outcome and a note until Sim has the
hook below.

## The progress log (`progress.jsonl`, schema 1)

One JSON object per line, appended and flushed as the run goes. Seeds are strings (64-bit).

Line 1, the header:

```
{ "type": "header", "schema": 1, "started", "command",
  "provenance": { "commit", "dirty", "sourceHash", "tool" },
  "seeds": { "runSeed", "generationSeed": rule, "fightSeed": rule, "mapSeed", "fightsPerScenario" },
  "populations": [ { "id": "combat", "search": "cmaes", "size",
                     "parameters": [ { "name", "paths", "min", "max", "log", "integer", "start" } ] } ],
  "scenarios": { name: { "setup", "weapon", "cond", "policy", "watchable",
                         "bands": [ { "name", "kind": "band"|"penalty", "metric", "min", "max", "scale", "weight", "scored" } ] } },
  "orders": [ { "name", "metric", "scenarios", "scored" } ],
  "metrics": [ "aDies", "bKilled", "aFled", "bFled", "aWinsOrDrivesOff", "bLeaves", "rounds" ],
  "endings": { "V": "Victory", "D": "Defeat", "F": "Fled", "A": "AnimalFled", "X": "AnimalDisengaged", "U": "Unfinished", "?": "?" },
  "specs": { "bands": <band spec as given>, "params": <parameter spec as given> } }
```

Every later line, one generation:

```
{ "type": "generation", "generation", "evaluations", "seconds", "generationSeed",
  "populations": [ {
     "id": "combat",
     "fitness": { "best", "median", "worst", "bestSoFar" },
     "sigma", "axisRatio",
     "mean": { "unit": [...], "values": [...] },                       // no fitness: never fought
     "candidates": [ { "fitness", "unit": [...], "values": [...] } ],  // every candidate, in ask order
     "best": { "candidate", "fitness",
               "scenarios": { name: { "metrics": { metric: mean }, "results": { ending: count },
                                      "fights": "V2.14 D1.9 ...",       // fight i is the i-th code
                                      "bands": [ { "name", "value", "distance" } ] } },
               "orders": [ { "name", "value", "distance" } ] } } ] }
```

A fight code is the ending letter, a flags digit (1 A died, 2 B killed, 4 A fled), a dot and the rounds:
`D1.12` is a defeat in which A died, after 12 rounds. Fitness is what CMA-ES saw: band distance plus the
penalty for leaving the unit box.

The shape is a search's, not CMA-ES's. A training run with one policy per species writes one population per
species, its parameters the policy's weights, and the same scenarios, metrics and bands.

Nothing in the log costs a fight: it is written from results the tuner computes anyway.

## The replay stream (`GET /api/replay?gen=&candidate=<index|mean>&scenario=&fight=`)

NDJSON. First line:

```
{ "type": "replay", "gen", "candidate", "scenario", "fight", "seed", "outcome": code, "recorded": code|null,
  "matchesRecord": bool|null, "gridSize": 50, "traceNote": string|null, "mean": bool }
```

then one frame per round (round 0 is the opening placement):

```
{ "round", "fire": { "x", "y" } | null, "events": [ "Wolf hurt: Left Leg, Pierce 0.31", ... ],
  "units": [ { "index", "team", "species", "member", "x", "y", "state", "action", "target",
               "margin", "boldness", "vitality", "blood", "reserve", "wounds" } ] }
```

Grid y runs north (up). Numbers a fighter no longer has (a herd member removed when it died) are null.

## The combat trace hook Sim needs

One observer on the encounter, called once at placement and once at the end of every round, by every fight
loop (headless and stepped). Used for replays only, never in tuning or play.

```csharp
namespace Survival;

/// Watches a fight round by round. It must only read: no random draws, no writes. With no observer
/// attached a fight allocates nothing for it, and a fight with one is the same fight.
public interface ICombatObserver
{
    void Round(CombatRoundView round);
}

/// The grid at the end of a round. Round 0 is the placement, before anyone acts. Events are the
/// WorldEvents this round raised (Injured, Killed, Died, FightEnded...).
public sealed record CombatRoundView(int Round, IReadOnlyList<FighterView> Fighters, Cell? Fire,
    IReadOnlyList<WorldEvent> Events);

/// One unit of Encounter.Units, by its index there. Action and Target are what it did this round
/// (null in round 0, or if it did nothing); State is Combat.StateOf. Margin is
/// Combat.Margin toward its target (null with no target). The body numbers are read from its body as
/// the round ends, or null once the body is gone (a herd member removed when it died).
public sealed record FighterView(int Index, AgentId Agent, int Member, int Team, Species Species,
    Cell Position, FighterState State, CombatAction? Action, int? Target, double? Margin,
    double? Boldness, double? Vitality, double? BloodPct, double? SprintReservePct, double? WoundLevel);
```

Where it attaches and is called:

- `Encounter.Observer` — `[JsonIgnore] public ICombatObserver? Observer { get; set; }`. Set after
  `Combat.Create`, before the first round; not saved.
- Call with round 0 when the observer is set (or at the end of `Create`/`Place` if it is passed in).
- Call at the end of each round in `RunRounds` (after wounds tick and the sweep, before `enc.Rounds++`
  or right after it, with Round = rounds completed) and at the end of each stepped round
  (`Sim.Step`'s fight path), so headless and player fights report alike.
- `AiTurn` and the player's `Apply` record each unit's chosen action and target for the round, so the
  view can give every fighter's action, not only the driven person's.
- The fire's cell once fire on the grid lands (round 4); null until then.

Test with it: a fight with an observer attached has the same outcome, rounds and events as without, for a
headless fight and a stepped one; frames are 0..Rounds.

When it exists, `Fights.Run` sets `enc.Observer` from its `watch` argument and maps `FighterView` to
`TraceUnit`, and `Frame` in `Fights.cs` (today's between-steps reader) goes.

## Files

`Program.cs` (the server), `wwwroot/` (the page), `tools/shot.mjs` (headless Chrome screenshots:
`node tools/shot.mjs URL out.png [width] [height] [script] [waitMs] [light|dark] [full|view]`).
The log and replay types are in `rebuild/Tools/Tune/Progress.cs`, `Trace.cs` and `Provenance.cs`.
