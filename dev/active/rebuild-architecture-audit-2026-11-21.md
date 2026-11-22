# Rebuild architecture audit: rebuild/Sim (2026-11-21)

Branch `rebuild-phase4-overnight`, read-only. I checked the 54 `.cs` files in `rebuild/Sim` (17,736
lines) against `principles-code.md`, every decision record in `documentation/decisions/` and
`GLOSSARY.md`.

How to read the metrics:

- **cmt%**: lines that begin with `//` or `///`.
- **diary**: comment lines that tell the story of a past failure. The search looked for these terms:
  `this rebuild`, `seed N`, `probe`, `bug, fixed`, `review seeds`, `day N.N`, `body review`,
  `before review`, `tried:`.
- **prov**: provenance notes such as "old Foo" or "old game: ...". These are a lighter kind of
  history comment.

Line numbers refer to the files as they stand on this branch.

---

## 0. Headline

The diary problem is concentrated, not spread out. Npc.cs holds **105 of the 203 diary comment
lines** in Sim. It also holds 45 of the 47 lines that name a seed by number. Every other file's
history comments fall into two groups:

- Provenance notes ("Old WoodedAreaFeature.Update ...").
- One-line "old game: X (bug, fixed)" notes, or references to body review A1..A13.

Both are comment noise. Neither is a rule bolted onto the code to rescue one failure.

The architectural problems are wider than Npc.cs, though. Four of the decision records dated
2026-11-21 are not yet reflected in the code, and parts of it contradict them:

- Conditions should replace tags. Tags.cs and Access.cs still use "Guarded".
- Effects should apply to bodies and things alike. Effects are body-only, and carcass spoilage is
  its own model.
- Crafting and building should be one Make activity. Shelters still has its own Build path.
- No effect should need a treatment the game lacks. Frostbite still has a treatment floor.

The clean-room doc's central promise is that "NPCs run the player's code" (Verbs, Sim.Refusal, the
same activities). Npc.cs only half keeps it.

---

## 1. Ranked list (most severe first)

| # | File(s) | Verdict | Why, in one line |
|---|---|---|---|
| 1 | Npc.cs | **Needs redesign** | A 1,739-line decision tree with about 70 failure-patch rules. It re-implements Verbs, refusals, routing, thermal physics and fire catch rules. Its one-paragraph description has to be "it is a list of exceptions". |
| 2 | Tags.cs + Access.cs | **Needs redesign** (replace) | Superseded by conditions-derived-never-stored. The "Guarded" vocabulary is banned by the glossary. `Access.Wired = Gate.None`, so every gate is switched off in production. |
| 3 | Fauna.cs (+ Carrion.cs, Renewal.cs, Populate.cs: one 1,460-line partial class) | **Needs refactor** | Four modules under one name: herd minds, population dynamics, world-gen placement, and person hunts. Per-species numbers sit in code switches. Its routing and flee helpers are duplicated by Npc. |
| 4 | Carcass.cs | **Needs refactor** | Tracks EffectiveHours and LastKnownTempF, both derivable from DiedMinute and the decay timeline. Three parallel ledgers. Its own spoilage scale is separate from Remains stages and from the Spoiling effect the decisions call for. |
| 5 | Effects.cs + BodySurvival.cs | **Needs refactor** | The "effects for bodies and things" kernel is not built: Effects is body-only, and wetness and blood are set by bespoke processes in BodySurvival. Frostbite keeps `treatmentAboveSeverity: 0`, against the no-missing-treatment decision. BodySurvival has 21 diary lines. |
| 6 | Shelter.cs | **Needs refactor** (scheduled) | Build, StructureProject and StructureRecipe duplicate Make, MakeProject and Recipe. This is acknowledged as roadmap 8. |
| 7 | Knowledge.cs | **Needs refactor** (split) | Four modules in one file: Beliefs, Sight, Knowledge and Journal. Its Sightings already hold what NpcMind.ThreatTile/ThreatMinute store again. |
| 8 | Fire.cs | **Needs refactor** (light) | Lazy catch-up pushes the "catch it up first" duty onto 7+ call sites, and `Verbs.RequireFiresCurrent` throws when one forgets. There are three burn-time estimators: FuelHours, BurningHoursRemaining and HoursItWillBurn. |
| 9 | Sim.cs | **Needs trim** (+ small refactor) | Stop reasons are strings used as state ("died", "done", the `Finishing` set). The activity-to-system dispatch is written out five times: Start, ActivityRefusal, ReadsTheFire, Live and HoldOf. |
| 10 | Ground.cs | **Needs trim** | Good physics. The interface is wide: about 25 one-line `new Column(s, sub).X` wrappers, X and XNow pairs, and `StepMinutes == MaxStepMinutes`. |
| 11 | Combat.cs, CombatAI.cs | **Needs trim** | `Combat.Boldness` has no production caller (tests only). `FireDeterrence` is a stub that returns 0. Formation by species is hard-coded. |
| 12 | Body.cs | **Needs trim** | Deep module. It has 17 diary lines (body-review history). |
| 13 | Work.cs | **Needs trim** | The night rule (`hour < 5 \|\| hour >= 21`) is one of three copies. |
| 14 | Weather.cs, Pools.cs, Items.cs, Travel.cs, Map.cs, Tracks.cs, Remains.cs, Populate.cs, Renewal.cs, Exposure.cs | **Needs trim** | Provenance and "bug, fixed" comments only. The structure is sound. |
| 15 | Make.cs, Verbs.cs, Trip.cs, Tiers.cs, MapGen.cs, Content.cs, Content.Tables.cs, Report.cs, Agent.cs, World.cs, Species.cs, Signs.cs, Things.cs, Nearby.cs, Narration.cs, Names.cs, Skills.cs, Odds.cs, Activity.cs, WorldEvent.cs, Ids.cs, TileSet.cs, Rng.cs, Save.cs, CombatAftermath.cs, CombatObserver.cs, BodyMotion.cs | **Clean** (minor notes per file below) | |

---

## 2. Per-file findings

The format for each file is: metrics; special cases; tracked-but-derivable state; duplication;
interface; what the module is; verdict.

### Npc.cs: 1,739 lines, 32% comments, diary 105 (45 lines name a seed), prov 74

- **Special cases.** About 70, listed by need in §4. Examples:
  - L151-153: drink when water is Critical, ahead of every tier.
  - L182: the hysteresis is released except for food (seed 17).
  - L351: go home only within 12 tiles.
  - L531-537: fetch makings instead of moving to them from a sheltered home (seed 256).
  - L641: turn home with 5 kg unless WoodGoal is set (seed 3).
  - L673: a one-line boolean that picks where home moves on a warming walk (115 of 117 review moves).
  - L984 and L995: no strike by day without makings, and none without 0.1 kg of kindling or pine
    (seeds 5 and 7).
  - L1719: `Butcher or Gather when me.Dark => null` skips Work.Refusal entirely.
- **Tracked but derivable:**
  - `ThreatTile`/`ThreatMinute` duplicate `Beliefs.Sightings`, which Knowledge.Observe writes every
    decision.
  - `ForageGoal`, `WoodGoal` and `Heading` are three slots for one idea, "the trip I am on". WoodGoal
    is overloaded to also mean "makings I am fetching" (L535).
  - `Camp` is rewritten from six places, as a side effect of plans: L133-135, L139, L543, L658, L676
    and Spawn.
  - `Opinions` is never written (L29).
  - `Me.StaysOut` and `Me.Warmth` are per-decision flags passed through a shared mutable context, not
    parameters. `Try` and `BudgetMinutes` read them invisibly.
- **Duplication across files:**
  - `Try` (L1711) re-implements `Sim.ActivityRefusal` instead of calling `Sim.Refusal`, and differs
    from it in the dark.
  - `Route` (L1668) is a BFS inside a box around both ends. Fauna.Route's own comment (Fauna.cs ~L745)
    explains that box routes oscillate.
  - `Flee` repeats `Fauna.FleeDirection`.
  - `Weapon` repeats the weapon pick in `Fauna.PersonHunt`.
  - `Hunt` calls `Fauna.BeginHunt` plus `ResolveHeadless` directly, bypassing the `Hunt` activity
    that already does this for a person.
  - `Light` orders starters and tinders differently from `Verbs.Light` (tinder order differs).
  - `ReadyFood`'s raw-meat rule and `WaterToHandL` duplicate `Tiers.Of`.
  - `Night` duplicates `Work.TooDarkToWork`. `HoursLeftToday` duplicates `Tiers.Of`.
  - `CoreAfterF` and `MinutesUntilCoreF` re-derive BodySurvival.ProcessTemperature's buffer order as
    a linear forecast.
  - `Lit()` (L607) runs its own simulation of the fire's catch rule.
  - `Spawn` duplicates `Agents.NewId`.
  - L238-255 are pass-through aliases of every Tiers constant.
  - Inside the file there are two walk-time estimators, `WalkMinutes` (averaged going) and
    `RouteMinutes` (true route). Different trip checks use different ones, and several diary entries
    record failures caused by that disagreement (seed 7: 63 vs 36 minutes).
  - There are four trip-feasibility checks: Afford (ordinary or warming), WarmingWalk, FetchWalk and
    Stranded.
  - There are three cooling forecasts.
- **Interface.** The public surface is tiny: `Spawn`, `Decide`, `AdaptTargetFat`, plus six
  `internal` test hooks. Under it sit about 105 private statics that call each other in a dense web,
  so every plan reads every other plan's side effects through `Me.Commits`. The module is deep in
  surface but not in design. It hides complexity, and it does not reduce any.
- **What it is.** "An NPC picks the highest-ranked stat by tier, then runs that stat's plan." That
  sentence is true for the first 200 lines. After that the honest description is: "a list of
  exceptions, each keeping one observed NPC from dying or looping, coupled through shared trip
  budgets and camp moves." I could not write a clean paragraph. **That is the signal.**
- **Note on scale.** The old game's NPC.cs was 1,816 lines (clean-room-architecture.md, table row 1).
  The rebuild has reproduced the problem it set out to remove.
- **Verdict: Needs redesign.**

### Tags.cs: 251 lines, 18%, diary 0

- **Special cases.** None as such. It is a vocabulary of 20 flags. 8 of them are documented as "not
  derivable yet" (Sealed, FrozenIn, Elevated, Unstable...).
- **Derivable.** It is all computed, which is good.
- **Duplication.** Guarded overlaps Occupied. The decision record and the glossary say Occupied
  replaced Guarded. `Verbs.ConditionName` translates `Gate.Guarded` to "occupied", which is a seam
  between two vocabularies.
- **Interface.** `Tags.Of` plus `Guards`. Moderate depth.
- **What it is.** The step-2 tag vocabulary, which the conditions decision said conditions would
  replace. It is still the source the conditions are read from.
- **Verdict: Needs redesign** (fold into conditions, delete the unused tags).

### Access.cs: 96 lines, 27%, diary 0

- `Wired = Gate.None` (L27): no gate is enforced in production. Every caller pays for a gate that
  always says yes, while tests switch the gates on. `GuardedReason` uses the banned word.
- **What it is.** One gate for reaching a thing. That is the right idea, but it is unwired, and it is
  keyed on the old tags rather than conditions with clearing actions.
- **Verdict: Needs redesign** (merge with Tags into one conditions module whose blocking conditions
  name their clearing verb).

### Fauna.cs: 847 lines, 19%, diary 6, prov 45. Also Carrion.cs 117, Renewal.cs 280, Populate.cs 216

- **Special cases:**
  - L431-435 (stealth) and L778 (detection range): per-species numbers in code switches.
  - L642: scavengers flee from a hard-coded species list. That should come from Role or Threat.
  - Renewal L30-45: a species table in code, with a comment defending it. The other species data is
    in species.json.
  - `IsMegafauna` is a hard-coded species list.
  - "Judgment, not in the old game" rules: Foray (L265), the pack returning to its kill, and pack
    hunts taking one target. These are acceptable as named judgments. They are general rules, not
    seed rescues.
- **Derivable.** Good: hunger is derived from calorie stores. `HerdMind.State` plus `StateSinceMinute`
  is an explicit, legible FSM. `LastFedMinute` is partly derivable.
- **Duplication:**
  - DecidePrey's and DecideSolitary's grazing branches are copies.
  - `Mend` repeats Body healing, on a separate rule for animals.
  - `PersonHunt`'s weapon pick is repeated in Npc.
  - `Route` and `FleeDirection` are repeated in Npc.
- **Interface.** `Decide`, `Tick`, `Minute`, `Refusal` and `Begin` are reasonable. But the partial
  class also owns world population (Populate), population dynamics (Renewal), the carrion flock, and
  the *player's* hunt setup (`BeginHunt`, `SightingDetectionFactor`).
- **What it is.** "Herd minds as one small state machine per behaviour, plus the population model."
  That is writable, but only by naming four things.
- **Verdict: Needs refactor.** Split it into herd mind, population (Renewal + Populate) and hunting.
  Move the species numbers into species.json.

### Carcass.cs: 370 lines, 24%, diary 7, prov 26

- **Special cases.** L298: the bare-hands yield. Mammoth ivory is special-cased in
  CombatAftermath.cs L75 rather than coming from species data.
- **Derivable:**
  - `EffectiveHours` and `LastUpdatedMinute` cache what `Remains.EffectiveHours(DiedMinute, now)`
    derives.
  - `LastKnownTempF` exists only for `IsFrozen` and can be read from the DecayTimeline.
  - `ScentBonus` is tracked.
- **Duplication.** `DecayLevel` (fresh to spoiled) and `Remains.StageOf` (flesh, skeletal, bones,
  gone) are two decay scales on the same clock. The decision says carcass decay becomes the Spoiling
  effect.
- **Interface.** About 51 public members. Three ledgers (CutKg, YieldKg, Pending) plus Remaining are
  exposed.
- **What it is.** "What a dead animal still yields, and how long butchering takes." Clear enough.
- **Verdict: Needs refactor.**

### Effects.cs: 108 lines, 12%

- Body-only (`BodyState`). The 2026-11-21 decision says "Wet, frozen and spoiling are effects on
  bodies and things alike, run by one pure kernel from one definition table, every effect reads one
  Exposure.At". None of the thing side exists, and Exposure.cs says so ("things are the next step").
- `RequiresTreatment` and `TreatmentFloorPct` survive. effects.json gives Frostbite
  `treatmentAboveSeverity: 0`, so frostbite never heals. That is the exact case the
  no-missing-treatment decision names ("frostbite never healed for the same reason").
- **What it is.** "Body effect store with drift and capacity deltas." Clean as far as it goes.
- **Verdict: Needs refactor** (build the kernel the decision describes; drop Frostbite's floor).

### BodySurvival.cs: 606 lines, 23%, diary 21

- **Special cases.** None that rescue a seed. There are many "Old (this rebuild): ... (bug, fixed; body
  review An)" notes (L71, 138, 259, 347, 394, 436, 452, 462, 483, 538, 556).
- **Derivable.** Wet and Bloody are stored as effect severities that the tick sets directly. That is a
  state variable living in the effects list (effects.json says "Severity IS the stored wetness").
  It is acceptable, but it means the effects table is not the kernel.
- **Duplication.** Its temperature model is re-derived as a forecast in Npc.CoreAfterF. The body
  should expose `ForecastCoreF(minutes, ctx)` itself.
- **Interface.** `Process`, `Update`, `Tick` and `TemperatureChangeFPerHour` are deep.
- **What it is.** "A minute of a body's heat, water, food, wetness and wounds." Clean.
- **Verdict: Needs trim** (move the review history to body-review-2026-11-21.md), and expose a
  forecast.

### Shelter.cs: 462 lines, 20%, diary 4, prov 30

- `StructureProject`, `StructureRecipe` and `Build` form a second recipe/project shape. The
  one-make-activity decision says they fold into Make, and the code acknowledges that (roadmap 8).
- `CampOverhang` is hard-coded in code, also acknowledged.
- `WaitingShelters` in World is tracked state for "a find revealed while the tile was taken".
- **What it is.** "Shelters, bedding, and building them." Clear.
- **Verdict: Needs refactor** (as scheduled).

### Knowledge.cs: 503 lines, 21%, diary 2

- **Contents.** Beliefs/TileMemory (data), Sight (ray casting), Knowledge (Observe/Glance/Witness/
  Receipt) and Journal (tile event log), all in one file.
- **Derivable.** `TileMemory.FireHours` is aged by its readers (Npc.CampFireHours), not by Knowledge.
- **Duplication.** Sightings are duplicated by NpcMind.Threat*.
- **What it is.** "What a person believes and how looking updates it." That is true of three of its
  four parts. Sight and Journal are their own modules.
- **Verdict: Needs refactor** (split the file; the logic is sound).

### Fire.cs: 688 lines, 15%, diary 8

- **Special cases.** None. It has physics bands (TempSizeFactor, BurnSizeBands) and a fallback
  temperature of 450.
- **Derivable.** Phase and IsActive are derived, which is good. `LastUpdatedMinute` is lazy catch-up:
  callers must call `CatchUpAround` (Sim.Start, LiveBodies, Scheduler.Plan, Npc.Surroundings, ...),
  and `Verbs.RequireFiresCurrent` throws if they did not. Pull this down. Reads that need a current
  fire should catch it up themselves, or fires should tick with the minute like item effects do.
- **Duplication.** `FuelHours` (reckoned), `BurningHoursRemaining` and `HoursItWillBurn` (simulated)
  are three answers to "how long will it burn". Npc adds a fourth (`FireHours` + `Lit`).
- **Interface.** About 59 public members. Fairly wide.
- **What it is.** "A two-mass fuel fire, its heat and light, and starting it." Clean.
- **Verdict: Needs refactor** (light).

### Sim.cs: 452 lines, 22%, diary 2

- Stop reasons are strings. `"died"` is compared in Live (L245-248). `Finishing` is a set of literals
  gathered from five systems. The state machine of "how an activity ended" is therefore spread
  across string constants. A `Stop` record (kind + reason) would make it obvious.
- The activity-to-owner dispatch is written five times (Start, ActivityRefusal, ReadsTheFire, Live,
  HoldOf), and Npc.Try makes a sixth. One table mapping activity type to {owner system, hold}
  would remove them.
- Scheduler and Minds are clean seams.
- **What it is.** "Run one activity a minute at a time, and the world's other minds with it." Clean.
- **Verdict: Needs trim** (+ small refactor).

### Ground.cs: 851 lines, 14%, diary 4

- Strong conservation-ledger physics.
- The interface duplicates itself: world-level (`TraversalFactor`, `TraversalFactorNow`) and
  state-level overloads, all one-line wrappers over `Column`.
- The observe and peek modes are a footgun. `Traversal.CrossingMinutes` observes, which writes ground
  into World.Tiles, and Npc's planning (MinutesOff, RouteMinutes, PaceFor) uses observing reads for
  every neighbour it weighs. HerdCrossingMinutes avoids this on purpose (Travel.cs L120-122).
- **What it is.** "The snow, water and mud column on each tile." Clean.
- **Verdict: Needs trim.**

### Combat.cs: 974, 18%, diary 4. CombatAI.cs: 443, 20%, diary 0. CombatAftermath.cs: 112. CombatObserver.cs: 63

- An explicit fighter state machine with derived Down and Gone. Good.
- `Combat.Boldness` (Combat.cs L264) has no production caller. Only CombatGoldenTests and
  HerdBudgetTests call it. It is dead code, or a reserved seam that should be marked as one.
- `FireDeterrence => 0` (CombatAI L81) is the general mechanic that no-special-cases relies on ("fire's
  heat and light is a term in predator boldness"). It is not built.
- `SelectFormation` hard-codes Hyena and Wolf. Mammoth ivory is hard-coded in the aftermath.
- **What it is.** "One grid fight for everyone, headless or round by round." Clean.
- **Verdict: Needs trim.**

### Body.cs: 798, 15%, diary 17. BodyMotion.cs: 109, 27%, diary 0

- A deep module: Damage, capacities, abilities. It has body-review history comments (L174, 325, 382,
  401, 471, 644, 662).
- **Verdict: Body Needs trim; BodyMotion Clean.**

### Work.cs: 459, 11%, diary 2

- A clean refusal-and-minute structure. `TooDarkToWork` holds one of three copies of the night
  window (Combat.Boldness L279, Npc.Night L297).
- **Verdict: Needs trim.**

### Weather.cs: 433, 15%, diary 4, prov 19

- Fronts and timeline. Fine. **Needs trim.**

### Pools.cs: 586, 15%, diary 4, prov 33

- Merged forage, harvest, timber and stock pools. Regrowth is derived. It is heavy on provenance
  comments. **Needs trim.**

### Items.cs: 309, 22%, diary 3, prov 27

- **Needs trim.**

### Travel.cs: 234, 24%, diary 2. Trip.cs: 101, 23%

- One formula (`Trip.Terms`) shared by the crossing and the preview. Good single source of truth.
- **Travel Needs trim; Trip Clean.**

### Map.cs: 305, 23%. MapGen.cs: 655, 5%

- MapGen is a deliberate draw-for-draw port on LegacyRng. **Both Clean.** Map could lose
  provenance notes.

### Tracks.cs: 280, 22%

- An erosion accumulator, and tiers derived from wear. It matches the overview. **Needs trim** (minor).

### Remains.cs: 173, 28%, diary 1

- Everything is derived from the rot clock. This is a good model of "derive, don't track", and
  Carcass should follow it. **Clean.**

### Renewal.cs: 280, 30%

- A species table in code (see Fauna). Judgment notes are fine. **Needs trim** (move the table to
  data).

### Populate.cs: 216, 17%

- World-gen placement under `Fauna`. Species counts are inline. **Needs trim.**

### Exposure.cs: 164, 31%

- The one air/wind/precipitation model, as the effects decision asks. The carried-thing cover is
  "not built yet". **Clean.**

### Verbs.cs: 258, 18%

- The best-shaped module in Sim: one source of "what can I do here", deep, reads only, and listing
  refused verbs with reasons.
- Its doc claims it serves "the player and NPCs alike". Npc never calls it.
- `Light` duplicates Npc's starter and tinder ordering, with a different order.
- **Clean** (but see cross-cutting #1).

### Make.cs: 548, 15%

- The outcome-table parser and Making. It matches its decision. **Clean.**

### Tiers.cs: 122, 31%

- Small functions, one per stat. It matches the stat-tiers decision. `Tiers.Of` (the player's) has
  to-hand rules for water and food that Npc re-derives with differences (Npc counts the stash; Tiers
  does not). **Clean**, but it should own the "to hand" rule for both.

### Content.cs: 407. Content.Tables.cs: 728, 5%

- Loading and validation. **Clean.**

### Report.cs: 386, 9%

- Distribution report for world-gen targets. Tooling inside Sim. **Clean.**

### Agent.cs, World.cs, Species.cs, Signs.cs, Things.cs, Nearby.cs, Narration.cs, Names.cs, Skills.cs, Odds.cs, Activity.cs, WorldEvent.cs, Ids.cs, TileSet.cs, Rng.cs, Save.cs

- **Clean.** Small, single-purpose files with honest names.
- World.cs holds 8 `internal` derived caches (WeatherCache, ErosionCache, GeneratedMap, PoolCache,
  DecayCache, GroundCache, ValleyForageCache, OpenLandCache, CarcassTiles, Nearby). They are all
  correctly unsaved, but World is becoming the place where every module parks its memo. It is worth
  an owner-per-cache convention.

---

## 3. Cross-cutting problems

1. **NPCs do not run the player's code.** The clean-room doc's central promise ("NPCs run the
   player's code"; "every behavior is written twice" was the old problem) is broken in Npc.cs:
   - It never calls `Verbs.At`.
   - It never calls `Sim.Refusal`. `Npc.Try` re-implements the dispatch and skips Work.Refusal for
     gathering and butchering in the dark.
   - It builds its own StartFire (different tinder order from Verbs.Light).
   - It bypasses the `Hunt` activity to start fights directly.
   - It routes with its own BFS.

   The seam is in the wrong place. The NPC mind should **choose among `Verbs.At` results, plus a
   small set of trip goals**, and score them. It should not construct activities.

2. **No shared forecasting layer, so the NPC re-derives physics.** "Will I get home warm enough",
   "how long is this walk", "how long will this fire burn" and "will this wood catch" are each
   answered in Npc with private copies:
   - CoreAfterF/MinutesUntilCoreF copy BodySurvival's buffer order.
   - Lit() and FireHours copy Fire's catch rule.
   - WalkMinutes and RouteMinutes are two estimators of the same walk.

   Most of the seed diaries are disagreements between two of these copies, for example "warm a tile
   out, short at home" loops, or a 63-minute vs 36-minute walk. Each owning module should expose one
   forecast:
   - `Body.ForecastCoreF(body, ctx, minutes)`
   - `Fire.HoursItWillBurn(fire + added fuel)`
   - `Trip.RouteMinutes(world, who, to)` (one estimator)

   The NPC should then read those.

3. **The 2026-11-21 decisions are recorded but not yet in the code, and the old code keeps
   contradicting them:**
   - conditions-derived-never-stored: Tags and Access still use Guarded, with all gates unwired.
   - effects-for-bodies-and-things: Effects is body-only. Carcass spoilage, wetness and blood are
     bespoke.
   - one-make-activity: Shelters' Build is still separate.
   - no-effect-needs-a-missing-treatment: Frostbite floor at 0.
   - no-special-cases: `FireDeterrence` is 0, so the "general mechanic" the decision relies on does
     not exist. Npc patches around predators with `pinned`, `run`, FleeTiles and route avoidance
     instead.

   A reader cannot tell from the code which of these is the plan of record.

4. **Lazy catch-up is used three ways, and callers carry the burden.** Fire (callers must
   CatchUpAround; Verbs throws otherwise), Ground (observe vs Peek, X vs XNow), and Carcass (tracked
   EffectiveHours) are each lazy in their own way. Remains (fully derived from a clock) and Pools
   (regrowth derived) show the clean pattern. Planning code (Npc) uses the *observing* variants, so
   weighing a plan writes ground into the save and catches fires up.

   Pick one rule. Reads either derive from a timeline or catch themselves up, and no caller ever has
   to remember to.

5. **Module boundaries track old-game files, not concepts.**
   - `Fauna` is one partial class across four files covering herd minds, population, world
     population and person hunting.
   - `Combat` spans four files.
   - Knowledge.cs holds four modules.
   - Per-species numbers live in code switches across Fauna, Renewal, Populate, Combat and
     CombatAftermath, although species.json exists.

   Suggested regrouping:
   - **Herds** (minds)
   - **Population** (Renewal + Populate)
   - **Hunting** (BeginHunt, sightings, the Hunt activity for persons and packs)
   - **Sight**
   - **Journal**
   - species data in species.json

   Smaller cross-cutting items:

   - **Strings as state.** Sim's stop reasons and the `Finishing` set; `"died"` comparisons.
   - **Activity dispatch written six times.** Sim ×5, plus Npc.Try.
   - **Night window in three places, and two "to hand" rules.** The night window is in Work,
     Combat.Boldness and Npc. The "to hand" rules are in Tiers.Of and Npc.
   - **Comment conventions.** Repo-wide, about 19% of lines are comments, and roughly 600 of them are
     provenance or history. A convention would remove most of them:
     - History goes in commit messages and review docs.
     - A comment states the rule and why.
     - A failure that motivated a rule becomes a regression test named for it, not prose.

---

## 4. Npc.cs: the implicit decision model, written as documentation

### 4.1 State an NPC keeps (NpcMind)

| Field | Meaning | Written by |
|---|---|---|
| `Camp` | Home: where its fire and stash are and where it sleeps | Spawn; Decide (fled from a predator by an unlit camp; Stranded); FireMakings (walking to makings moves home there); Wood (felling at, or a warming walk to, new trees; NewCamp) |
| `Boldness` 0.3-0.7 | Range from camp (2-10 tiles), courage in a fight, willingness to defend | Spawn |
| `SeeingTo` | The stat held at Need until back over its "again" line (hysteresis) | Decide (set by the chosen option; cleared when satisfied, or when that stat's plan is null, except Food) |
| `Heading` | Exploration target | Explore |
| `ForageGoal` | Forage ground being walked to | Forage / GoHome |
| `WoodGoal` | Trees, woodpile or makings a trip set out for | Wood, FireMakings; cleared at camp and by GoHome |
| `TargetFatKg` / `TargetFatMinute` | Exponential average (3-day half-life) of its own fat, clamped to [10% floor, baseline]. Falling 0.3 kg short of it is food Need | AdaptTargetFat every decision |
| `ThreatTile` / `ThreatMinute` | Last close predator, remembered for 120 min | Decide |
| `Opinions` | Per-agent opinion (never written) | nobody |

### 4.2 The decision (Decide, every time the previous activity ends)

1. **Look.** Knowledge.Observe (memory of tiles, stashes, fires, sightings). Adapt the target fat.
2. **Held in someone else's fight:** wait one minute.
3. **Hysteresis.** If the stat being seen to is back over its line, let go.
4. **Read the stats as tiers** (Tiers.cs, shared with the player), each from the NPC's own beliefs:
   - **Warmth:** the *core it forecasts on arriving home* (not the core now). Critical at or below
     the hypothermia line; Need below 97.5 °F. If the core is fine: Need while camp fuel is less than
     hours-to-dawn + 2; Want while it is less than that + 12.
   - **Water:** Critical at or below 15%; Need below 50%, or with nothing to hand and today's loss
     taking it critical; Want with less than a day's water to hand.
   - **Food:** Critical only when starving (fat at 3% and stomach empty); Need below the fat floor, or
     0.3 kg short of target, or stomach below 30% with nothing ready to hand; otherwise Want.
   - **Rest:** Critical at or below 10%; Need below 30%; Want below 80%.
5. **Threat.** The biggest live predator herd within 1 tile, by true position, not by sight.
   - *Run* if it is on this tile, or if there is no fire here, the remembered threat is within 3
     tiles, and it is approaching or nothing is Critical.
   - *Pinned* if a fire burns here and the threat is adjacent.
6. **Home moves before planning:**
   - (a) It fled from a predator that has settled next to an unlit camp: here is home.
   - (b) Stranded: here is home. That means more than 2 tiles out, able to make a fire here, and the
     true-route walk home would exhaust it, or chill it past lasting harm with no fire waiting.
7. **Rank the options.** The tier sets the band (Critical 30, Need 20, Want 10). Within a band the
   order is warmth > water > evening > food > rest.

   | Rank | Option | Plan |
   |---|---|---|
   | 100 | Threat band, if running or an ally is fighting a predator here | RespondToThreat: join the ally; or fight if `Combat.WouldStand` says Pressing or Standoff; else flee one tile |
   | Critical, ahead of the whole band | Water Critical and carrying at least 0.1 L | Drink |
   | Need, ahead of the whole band | Hungry (stomach below 30%) and ready food in the pack or stash here | EatToHand |
   | warmth's tier | Core cold: **Warm**. Core fine: **Fuel** (wood for tonight or the stockpile) | |
   | water's tier (≥ Need) | **Water** | |
   | Need 1.5 | Evening (after home-by time, `min(19:00, sunset)`, or before sunrise) | **Night** |
   | food's tier (≥ Need) | **Food** | |
   | rest's tier (≥ Need) | **Tired** | |
   | Want, after everything | **Errands**: unload, tend fire, general forage | |

8. **Take the first option whose plan is not null.** A null plan releases the hysteresis hold, except
   for food. Two post-filters apply to plans ranked below the threat band:
   - A walk out from a burning camp first banks the fire to 3 kg.
   - Pinned: no walking away; keep the fire.
9. **Commit the plan's side effects.** These are `Me.Commits`: camp moves and goal slots. Nothing
   chosen: rest at camp, or go home.

### 4.3 The trip budget (one gate under every walk and every stint of work away from camp)

`Afford(to, work, home, margin, warming)` allows the walk out, the work, and the walk home only if
all three of these hold:

- **Warm:** home with core at or above 96.8 °F plus a margin (0.3 °F setting out from camp). For a
  warming trip the line is the 95 °F lethal line instead, with no walk home counted.
- **Rested:** energy left above the Need line (30%). For a warming trip, 45 minutes of energy kept in
  hand.
- **In time:** home by home-by time, unless the option outranks the evening (`StaysOut`). No walking
  out before sunrise.

Walk times come from `WalkMinutes` (averaged going). Cooling comes from `TripCoolingFPerHour` (each
tile's open air). `Try` also caps any gather or butcher stint at the minutes until the core reaches
Critical (15 minutes if already Critical), and holds work away from camp to Afford.

Separate walk gates exist alongside Afford, using `RouteMinutes` (true route):

- `WarmingWalk`: to warmth; legs + 45 min, and arriving above the lethal line.
- `FetchWalk`: there and back, same lines.
- `Stranded`: described in 4.2.

### 4.4 The plans, briefly

- **Warm:** fetch wood if out and camp fuel is under 4 h; at the WoodGoal, go home; go home if within
  12 tiles and a fire can be had there; at a burning fire, keep it at 5 kg, walk out for wood, grope,
  cook or melt; else light; else makings elsewhere; else wood by day; else grope.
- **Fuel:** at a cold camp, makings within 2 tiles first; else Wood.
- **Wood:** unload at camp; turn home with a load; pick up what home lacks; fell here; walk to the
  goal or nearest trees (and maybe move home); explore for timber; forage for fuel.
- **Water:** drink, unstash, melt snow, light a fire, walk to known water, go home if water is to be
  had there, explore.
- **Food:** cook, eat, take raw food to the fire, unstash, butcher, then FindFood. FindFood weighs
  forage against hunts by expected kcal per hour, with hunts discounted by danger, a discount that
  fades as the shortfall grows.
- **Night:** home; sleep if energy is under 80% (Bed); else keep the fire.
- **Bed:** bank 9 kg; sit up while logs catch; sleep in stretches timed to the fire; at a cold pit,
  ColdBed.
- **ColdBed:** makings within 2 tiles; sleep if 120 minutes would be safe; makings anywhere; wood by
  day; grope; else sleep for the safe minutes.
- **Errands:** unload, keep fire, forage.

### 4.5 Every special-case rule, grouped by the need it patches

The source shown after each rule is the failure the comment says it fixed.

**Warmth and fire (the bulk)**

1. L107-109, L1293. Warmth is judged by the forecast core *at home*, not now. *Source:* seed 17 day
   16, 10 reversals.
2. L110-113. Fuel tier at every hour, not 05:00-19:00. *Source:* the night had no fuel tier.
3. L115. WoodGoal cleared on arriving at camp.
4. L139, L1461-1470. Stranded moves home here, never within 2 tiles, never while camp has warmth.
   *Source:* seed 5; seeds 1000, 31, 7, 11.
5. L189. Bank the fire to 3 kg before any walk out of a burning camp. *Source:* judgment; a lost
   forage goal (bug).
6. L345-346. Out with camp fuel under 4 h (LowFuelHours): fetch wood under warming rules if camp is
   cold.
7. L348. At the WoodGoal tile, go home.
8. L351. Go home only if able to light or the camp fire burns, and within 12 tiles (an unexplained
   12).
9. L359. By a burning fire, walk for wood only as a *walk*, never Wood's grope fallback (WalkFor,
   L382). *Source:* seed 17, 4 hours groping.
10. L360. Fire under 1.5 kg: grope here.
11. L368. FireMakings before Wood or grope. *Source:* seed 11.
12. L374. Grope only with energy at or above 10%.
13. L379. Fuel at a cold camp checks makings within 2 tiles first.
14. L447-452. Bed: bank 9 kg; sit up 15 min while logs will not catch (energy > 0.15).
15. L458-460. Short sleeps that wake 15 min before the fire is out, timed by HoursItWillBurn, not
    FuelHours. *Source:* seed 5 day 4.
16. L479-488. ColdBed's ladder, with ColdSleepMarginF 0.9 °F and MinColdSleepMinutes 30. *Source:*
    seeds 11, 99, 777, 2025, 256, 5, 17.
17. L513-516. FireMakings: none when already standing on makings. *Source:* seed 11, walking between
    two stashes all night.
18. L523. Never from a camp worth keeping to a bare tile.
19. L526. Read remembered stashes as "at least" (memory minus half a 0.1 kg step). Also L761-773.
    *Source:* seed 2024 day 21, 66 loops.
20. L531-537. Sheltered home with bare makings 1 tile off: fetch, do not move. *Source:* seed 256 day
    8.69, 26 loops.
21. L538-542, L564-578. Provision: take tinder (to 1 kg) and kindling off the stash before walking to
    makings. *Source:* seed 7 day 29.
22. L543. Walking to makings moves home there.
23. L583-625. Makings = something to strike + kindling 0.1 or pine + 2 h of fuel a kindling fire can
    actually take. The `Lit()` mini-simulation exists for this. *Source:* probe P6; seeds 7, 2024,
    11.
24. L641. A load (5 kg) turns it home only on a trip with no WoodGoal; room under 1.5 kg turns it
    home. *Source:* seed 3 day 11.97, 59 reversals.
25. L648-649, L697-706. Pick up lying wood in LyingOrder: what home lacks to light first. *Source:*
    seed 99 day 22.
26. L656. No felling when warmth is Critical. *Source:* seed 31.
27. L670-671. Continue to WoodGoal while affordable. *Source:* 520 reversals per seed in 60 days.
28. L672-673. Where home moves on a wood walk: a one-line compound boolean over Sheltered, KeepCamp
    and NearTimberTiles (2). *Source:* 115 of 117 moves to bare tiles.
29. L684-689. Warming grope only at the trees; dark grope only if one hour finds more than one hour
    of fire. *Source:* seed 5.
30. L723-734. NewCamp: prefer the nearest shelter with timber within 2 tiles; leave for bare timber
    only when no timber is near and the camp is not worth keeping; never leave a camp with tonight's
    wood or a fire. *Source:* seed 5 (stand 24 tiles off); seed 7 day 13.
31. L746-753. KeepCamp: a shelter + something to strike + 2 h of any wood. *Source:* seed 31.
32. L796-804. ColdCamp: embers count only with fuel they will take; in sight, the truth; else
    memory. *Source:* seed 99, twice.
33. L1052-1058. CampFuelHours is 0 at a cold camp and counts only wood its kindling can light.
    *Source:* seed 99 day 22.
34. L1065-1070. A remembered fire's hours age from the last visit.
35. L1093-1104. Unload feeds the fire first; keeps up to 9 kg of wood in hand beside a fire; keeps 1
    kg kindling and 0.3 kg of each tinder. *Source:* seed 17.
36. L941-942. Stoke a small fire (under 1.5 kg) with the quickest-catching fuel first.
37. L950. The kindling reserve (0.6 kg) is released when logs wait. *Source:* seed 5.
38. L956. Take stash kindling no faster than the carried amount. *Source:* seed 17 day 4.
39. L966-969. Light: fetch tinder 0.3 or kindling 1.0 from the stash first.
40. L975-977. Shave sticks for tinder; keep 0.15 kg of tinder for Critical. *Source:* seed 5 day 3.
41. L984. By day, short of Critical, strike only over full Makings. *Source:* seed 5 days 0-3,
    eighteen fires.
42. L987-989, L1026-1032. Sliding strike-odds threshold: 20% warm, 6% wanting warmth, 2% freezing.
    *Source:* seed 5.
43. L995. No strike without 0.1 kg kindling or pine. *Source:* seed 7 day 27.
44. L1014-1019. TakeFromStash puts down the heaviest non-fire item when the pack is full. *Source:*
    seed 11 day 33.
45. L1437. TripHomeCoreF = 96.8 (not the 97.5 Need line). *Source:* seed 17 day 16.
46. L1503. WarmingReserveMinutes = 45. *Source:* seed 11.
47. L1481-1486. CampHasWarmth uses sight or aged memory; a shelter alone is no warmth. *Source:*
    probe P2b.
48. L1726-1730. Try caps gather and butcher at the minutes until Critical, or 15 minutes.

**Water**

49. L151-153. Drink carried water when Critical, ahead of all tiers. *Source:* died of thirst with 3
    L on its back.
50. L177-182. A null plan releases the hysteresis hold (except food). *Source:* seed 17 day 1.96, 98
    times.
51. L217-218. Open water underfoot counts as infinite water to hand.
52. L398-404. Search radius for known water: 15 tiles if home has water, else 30; a Critical walk is
    held to warming lines. *Source:* seed 5 day 7, died two tiles from water.
53. L408. Go home only if water is to be had there. *Source:* seed 17 day 1.96.
54. L412. Explore for water. *Source:* seed 5 day 10.7.
55. L1042-1043. AtFire melts a day's water to carry.

**Food**

56. L154-156. Eat ready food when hungry, a Need-band override. *Source:* judgment.
57. L224. FoodToHand counts ready food on the stash here. *Source:* seed 31 day 2, 13 loops.
58. L811-814. Raw meat is eaten raw only below 6% fat.
59. L825-827. Eat stomach room + fat shortfall, up to a second store.
60. L838-842. Butcher only in light or by a fire; no food plans in the dark; room under 1 kg means go
    home.
61. L1122-1126. A carcass counts only with work left for this butcher's tools.
62. L853. Hunt only with a weapon and vitality of at least 0.7.
63. L864-873. Hunt value = 0.4 × meat × kcal / hours × (1 − danger × (1 − urgency)).
64. L880-893. WouldHunt: Prey always; DangerousPrey only starving; never over 2,000 kg. *Source:*
    seeds 123 day 5 and day 34.9.
65. L207. Food is satisfied only once fat is back at the floor.

**Rest and night**

66. L163, L425-429, L1512. The evening band from min(19:00, sunset) to sunrise.
67. L437. Night: sleep under 80% energy.
68. L422. Sleep where it stands under 10% energy.
69. L304. `Lit` = sun up and outside 05:00-21:00, or a torch. *Source:* the summer 4 am walk.
70. L1427-1428. No walking out before sunrise unless StaysOut.
71. L1133, L1235. No new forage ground after 18:00; no exploring after 17:00.

**Threat**

72. L128-143. The run, approaching and pinned rules; FleeTiles 3. *Source:* fled one tile and went
    home beside the bear until it froze.
73. L133-135. Home moves to where it fled if the predator settles by an unlit camp.
74. L191. Pinned: no walk away from a fire with a predator adjacent.
75. L1675-1689. Routes avoid tiles within 3 of a fresh threat. *Source:* seed 5 day 15.9, nineteen
    times.
76. L1529. Herds already in a fight are not prey or threats. *Source:* a crash on the player's bison
    fight.
77. L1547-1556. WouldDefend formula: opinion, weapon, vitality, boldness, threat size, 0.35 cut.
78. L1522-1524. Threat is read from true herd positions within 1 tile, not from sight (acknowledged
    at L1520).

**Movement, foraging, exploring**

79. L1155-1184. ForageTarget: a 1.25 margin to move; the current goal is held unless beaten by the
    margin; a 0.3 °F set-out margin.
80. L1193-1208. WalkMinutes: first crossing off here, the rest at camp's averaged going. *Source:*
    froze in deep snow; one bog tile doubled trips.
81. L1651-1664. RouteMinutes, a second estimator used by Stranded, WarmingWalk and FetchWalk.
    *Source:* seed 7, 63 vs 36 minutes.
82. L1240-1259. Explore in rings of 6 tiles, ordered nearest to camp and then fewest visits; "seen"
    means sight rays. *Source:* seed 5 sweeping west; seed 5 day 8, behind a ridge.
83. L1284. Range from camp = 2 + 8 × boldness tiles.
84. L1619-1620. GoHome first picks up what it came for at the WoodGoal.
85. L1672. Route search box margin of 8 tiles.
86. L1719. Butchering or gathering in the dark skips Work.Refusal ("screened by the plans").

### 4.6 What the redesign grilling should probe

- **The trip abstraction.** Almost every loop in the diaries comes from "the trip I am on" being
  re-judged from scratch at each step, by a different estimator than the one that started it.
  ForageGoal, WoodGoal, Heading, StaysOut, warming and the camp moves are all patches for the missing
  concept: a committed trip (goal, purpose, budget, abort conditions) that is judged once and
  re-checked only against its own abort conditions.
- **Home as a decision, not a side effect.** Camp moves from six places, inside plans. Should
  choosing a camp be its own explicit, rare decision?
- **Fire as a need with stock.** About half the rules are about whether the NPC has the makings of a
  fire. That depends on a correct model of what a fire will take (Lit, Makings, FireHours,
  KeepCamp, ColdCamp, CampFuelHours). Fire.cs should answer "would this stock make a fire that lasts
  N hours" once, for everyone.
- **Tiers vs plans.** The tier layer is clean and shared. The plan layer is where special cases
  live. Is utility scoring over `Verbs.At` plus a handful of trip goals enough? Or does the NPC need
  explicit multi-step plans (go there, do X, carry Y home)?
- **Beliefs vs truth.** Threat, carcasses and fires in sight are read from the world, not from
  beliefs. The clean-room doc says NPCs can be wrong. Decide whether that is in scope.
- **Fire deterrence.** Under no-special-cases, "predator near my fire" should come from
  `FireDeterrence` in predator boldness. That would let rules 72-75 shrink to "run if a predator is
  coming."
