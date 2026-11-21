# Emergent World: Signs, Settling and Conditions

*Design for the clean-room rebuild's content layer. It replaces tensions and most authored events
with content that falls out of the simulation. The plan (`clean-room-architecture.md`) points here
for Phase 4 onward.*

**Status:** design agreed in direction (2026-11-20); revised 2026-11-21 after a review session with
the user (§0). Steps 1-3a built.
**Source:** adapted from the "Emergent Discovery System" handoff written for the corridor game
(text-survival-2), whose findings came from a throwaway world-gen POC. This doc keeps its findings
and changes what does not fit the rebuild: a 2D grid valley with a full per-minute agent sim.

---

## 0. Decisions of 2026-11-21 (these override anything below that disagrees)

The hard-to-reverse ones are recorded with their reasons in `documentation/decisions/`.

1. **No special cases.** What a person can do about a situation comes from systems that exist for
   everyone: fighting, moving, waiting, fire, digging. Nothing scripts "what you can do when a bear
   is near". New behaviour arrives as small general mechanics (fire is a term in predator
   boldness), never as per-situation choices, so every situation updates when a mechanic does.
2. **No moments, no director.** There are no event popups. Discoveries and ordinary gameplay are
   the moments: things on the map and in the action list that the player answers with ordinary
   actions. Nothing manages the player's attention.
3. **Effects apply to bodies and things alike** (§3.3), driven by **one exposure calculation**
   (§3.4). Carcass decay becomes the Spoiling effect.
4. **Conditions replace tags** (§3.5): facts an action sees, read from effects and place, never
   stored. Blocking conditions offer one clearing action; modifying ones only change outcomes.
   Occupied blocks; its clearing action is **Contest** (or **Ask**, toward an ally).
5. **No Guarded gate.** A predator on its kill makes the kill Occupied; contesting it is answered by
   the predator's own mind through its boldness. Fire's heat and light is a boldness term.
6. **Crossing water is a chance with visible odds**, like fire-starting (§3.6).
7. **One hop of cause is a preference, not a rule.** Signs carry a real cause where one exists.
   Authored discovery content stays until the system produces the same thing, and may carry an
   authored cause. No "ghost camp" settling for now.
8. **Place text never contradicts game state.** Static description only; live facts ("wolf tracks
   everywhere") come from real signs. Old hallucination scenes are cut.
9. **Stat tiers** (`GLOSSARY.md`): every stat is Critical (imminent death, drop everything), Need
   (survive the day, or back to baseline) or Want (stockpile, surplus). Tier ranks first; within a
   tier, warmth, then water, then food, then rest. The NPC and the player-facing alerts use it.
10. **Effects code is verified** to a stated bar (§3.3): a pure kernel, property tests, a
    reference model, and mutation testing.

---

## 1. What this replaces, and why

The old game authored its content: ~20 event registry partials, outcome templates, and **tensions**,
tracked records such as `Stalked`, `WoundUntreated` and `FoodScentStrong` that raised the odds of
scripted events. The authoring cost was the pain, and tensions broke "derive, don't track": a
tension could say you were stalked by a wolf that did not exist.

The rebuild now has the state tensions only described:

| Old tension | What it is in the rebuild |
|---|---|
| Stalked, Hunted | A real pack whose mind has picked you as prey and follows your prints |
| PackNearby, HerdNearby | Agents within range |
| WoundedPrey | The split-off wounded animal: its own agent, bleeding, leaving prints |
| FoodScentStrong | Meat in your pack, blood on you, a carcass nearby |
| WoundUntreated, FeverRising | Body effects; needs infection, fever and a Treat activity (not built yet) |
| DeadlyCold | Core temperature and distance to a fire |
| SmokeSpotted | A burning fire other agents can see |
| ShelterWeakened | A camp structure's condition |
| Infested | Vermin in the food stores: a small-game agent occupying a stash |
| Disturbed, Shaken | A Fear effect that carries its cause; learned fear in a herd's mind |
| ClaimedTerritory, PredatorTerritory | Scent posts and territory signs of a real pack or bear |
| MammothTracked, FreshTrail | Prints the player has read and is following |
| ScavengersWaiting | Scavengers tethered to a carcass or a weak body (Beacon) |
| SaberToothStalked | A solitary predator's mind targeting you |
| DamCollapsed | A dam's structure failing (beaver chain, step 5) |

So tensions go. What they gave the player that raw state does not is kept, by other means:

- **Perception.** Tension is the player's *belief* about a real thing. A pack following unseen is
  not tension; fresh prints over your own trail are. Beliefs are plan decision 7.
- **Pacing.** From the world itself: discoveries, renewal, attractors and the stat tiers' pressure.
  No director (§0.2).
- **Legibility.** Signs carry a cause where there is one (§3.2), so investigating can say why.

**The rule:** any event or discovery this system can replace, it replaces. Whatever is left is
catalogued (`documentation/authored-content-catalog.md`, created during build step 4's paper
exercise) and migrated later, case by case, as authored content. Nothing old is ported by default.

What is expected to be left: a few landmarks and set pieces (the pass, dens, caves) that pure
emergence cannot produce. Landmarks also serve as *attractors* that the sim's agents converge on
(§6.2).

---

## 2. Commitments (argued and settled; reopen only with cause)

1. **One simulation.** World-gen history is produced by running the real sim (`Sim.Advance`) on the
   real world, not by a second, simpler model. A coarse model of wolves beside `Fauna`'s real pack
   would drift from it, and what the player finds would not behave like the world they stand in.
   Distant agents step hourly to make this affordable (plan decision 3).
2. **The settling pass samples a content distribution; it is not a chronicle.** One to two sim
   years is enough. Shortcuts a historian could not take are allowed: seeding herds mid-story,
   extra arrivals into empty stretches.
3. **One hop of cause, where there is one.** A sign points to its source and cause when the sim made
   it; authored content may carry an authored cause (§0.7). Sagas and deep history are out of scope.
4. **Emergence comes from chains through shared currencies, not from coverage.** Interactions read
   and write five currencies: **meat, occupancy, territory/scent, information, structure.** An
   interaction earns its place only if it touches one. Deliberate nulls are listed and stay null.
5. **Facts never move for pacing.** Nothing creates, moves or hides a fact to pace the player.
6. **No language model in this system for now.** Text comes from templates over fact records.

---

## 3. Data model

Extends what the rebuild has: plain records, IDs only, static systems, `Refusal` functions,
anything derivable computed on read. No ECS, event bus, rules DSL or inheritance tree.

### 3.1 One reference to any thing

Signs, access checks, verbs and discovery all need to point at any kind of object:

```csharp
public enum ThingKind { Agent, Body, Carcass, Pool, Stash, Fire, Shelter, Track, Sign, Item }
public readonly record struct ThingRef(ThingKind Kind, TileId Tile, int Index);
```

Every switch over `ThingKind` is a switch expression with no default case, and CS8509
(non-exhaustive switch) is an error in `Sim`. Adding a kind fails the build everywhere it is not
handled: **the compiler shows the empty cell of the interaction table.** Deliberate nulls are
explicit arms with the reason:

```csharp
ThingKind.Track => null, // herds ignore prints: touches no currency
```

### 3.2 Signs are a view over existing state

Carcasses, fire pits, stashes, tracks, shelters and bodies already *are* evidence. One query
projects them into one shape:

```csharp
public sealed record Sign(ThingRef Thing, SignKind Kind, int MadeMinute, Cause? Cause);
public readonly record struct Cause(AgentId? Who, string What);  // "the Limping pack", "storm", "starvation"

public static class Signs
{
    public static IEnumerable<Sign> At(World w, TileId t);   // asks each system; sorted
}
```

- Only signs with no other home are stored: scat, claw marks, kill sites, gnawed stumps, picked-over
  bushes. One sorted list per tile, pruned with `Map.Tidy` like everything else.
- Age and fading derive from the erosion clock, as tracks already do. Nothing is updated per minute.
- New saved fields are only `Cause` on things that persist (carcass, stash, fire pit, body, stored
  signs). `Who` may name a dead or departed agent; it is a label, not a live reference.

### 3.3 Effects: one model for bodies and things

An effect is a state that builds up and fades under exposure and time: Wet, Frozen and Spoiling
on bodies and things; Bleeding, Pain, Shivering and the rest on bodies only.

- **One definition table** (data): each effect's severity range, how exposure drives it up, how it
  fades, label thresholds, whether it is body-only.
- **One pure kernel:** `Effects.Next(state, exposure, minutes) -> state`. No world, no RNG, no side
  effects. The body's survival step and every thing call it.
- **State is per stack in one place:** the tinder pile in a pack is one wetness. Merging stacks
  averages by weight; splitting copies.
- **Carried things follow their carrier:** packed, they take the carrier's exposure under pack
  cover; worn, they are the body's. Things on the ground or in a stash evolve from their tile's
  exposure. Every stack ticks every world minute, in one pass at the start of the minute (never
  inside an agent's turn, so a stack handed over mid-minute is ticked exactly once).
- **Where an effect's consequences live:** in the system that reads it (fire odds read wet tinder,
  butchering reads spoilage). An effect two systems share has one function, owned by the effect's
  physics, that both call.

**Verification bar** (effects have been a major source of bugs):

1. The kernel is pure and small; numbers live in the table.
2. Property tests (CsCheck) in the normal suite:
   - bounds: severity stays in range for any input;
   - time-splitting: N one-minute steps equal one N-minute step;
   - direction: more rain never dries, more heat never freezes faster;
   - fixed point: under steady exposure the state settles;
   - every input matters: changing any exposure input changes some effect, or is declared irrelevant;
   - merge laws: commutative, result between the inputs;
   - body and thing agree: the same exposure gives the same curve.
3. A closed-form reference model for steady exposure, compared within a tolerance.
4. Mutation testing (Stryker.NET) on the effects module, score at least 90%, run as a separate
   command. No Dafny for now: it proves over reals, and the bugs live partly in doubles.

### 3.4 Exposure: one calculation

`Exposure.At(world, tile, cover)` returns what the world does to a body or thing in one place:
air temperature with fire heat, wind at it, precipitation, ground wetting, sun. Cover says how
protected it is: open, shelter, pack, worn, under snow.

- **Wind at the body** is regional × tile × (1 − pit walls) × (1 − shelter), and every consumer
  (wind chill, convection, drying) reads that one value. Drying beside a fire omits the shelter
  term. Today tile wind reaches only the chill; that is the bug this fixes.
- No system computes exposure itself; an architecture test enforces it.

### 3.5 Conditions and actions

A condition is what an action sees about a thing: a fact read from its effects and its place,
never stored. Conditions replace the tags of build step 2.

- **Blocking conditions** stand in front of a thing's action and have one clearing action: Buried
  (Dig), Frozen to the ground (Chip free), Iced over (Cut a hole), Tied (Untie, or Cut), Out of reach
  (Climb, Knock down), Alive (Kill), **Occupied (Contest; Ask toward an ally)**.
- **Modifying conditions** block nothing and change the outcome or odds: Wet, Spoiled, Rotten,
  Cracked.
- **Three parts:** the definition is data (name, label thresholds, clearing action, tool tiers,
  order); deriving it lives in the system that owns the underlying state (ground owns Buried, heat
  owns Frozen); its effect on outcomes lives in the consumer.
- **A clearing action changes the world, never the condition directly.** Digging lowers the snow
  over the thing; chipping leaves "ice broken at minute X", which refreezes. When the world changes
  by itself (a thaw, a fire beside it, wind scouring the snow), the condition simply stops being
  true. That is how waiting or lighting a fire work around a condition without a special action.
- **Tool tiers set the method and cost:** `Dig (bare hands)` 1×, improvised 1.5×, shovel 2×, scaled
  by depth; chipping needs a striking or cutting tool.
- **One action shown:** the thing, its conditions, and one action. Blocked: the outermost blocking
  condition's clearing action (order is physical layering, snow over ice over lid over contents).
  Not blocked: the thing's action, or **Interact** when it has several (fire, carcass, stash,
  shelter), which opens its menu. Example: `[Old cache (buried, frozen)] - Dig (bare hands)`, then
  `Chip free (stone axe)`, then `Pick up`.
- **Contest** is answered by the occupant's mind: it yields, holds and threatens, or fights, through
  its ordinary boldness. The same for a pack on a kill, a bear in a den, a stranger at a fire.
- `Capability` stays: gear capabilities pick the tool tier. `Access.Refusal` remains the one place
  an action asks whether it is blocked, and it answers with the clearing action.

### 3.6 Crossing water

Anywhere can be crossed. Before committing the player sees odds, as for fire-starting: ice
thickness at that spot, open water, depth and current, a feature (a fallen log, stepping stones),
load, legs. Outcomes: **dry**, **wet** (feet and legs soaked, heavy cooling) or **through** (fully
soaked, cold shock, injury chance on thin ice). Open water can't come out dry. Better crossing
places are real tile content, found by searching the bank.

### 3.7 Indexes that are never saved

- **Nearby:** `TileId → sorted agent ids`, rebuilt lazily when agents move. A year-long settle is
  mostly "what is within r tiles" queries.
- **Carrying capacity:** derived per species per territory from its pools; renewal reads it daily.

---

## 4. Interactions

Behaviour stays in the minds (`Fauna`, `Npc`) and the systems; it is not moved into a rules
language. The table below is the **design and audit target**: each row is either handled in code
(and named in a test) or a declared null. The exhaustive `ThingKind` switches (§3.1) keep code
and table honest.

Rows marked ★ build first (most chains per row). ✓ already exists in the rebuild.

**Predators (packs, bears)**

| Interaction | Currency | Status |
|---|---|---|
| Pack hunts prey herd → carcass with cause | meat | ✓ |
| Pack feeds on and holds its kill | meat, occupancy | ✓ |
| Starving pack ranges wider | — (drive) | ✓ |
| Pack scavenges another's carcass | meat | ★ |
| Pack eats a body → scatters its kit as drops | meat, information | ★ |
| Pack or bear digs out a stash with meat | meat | ★ |
| Pack follows fresh prey prints | information | ★ |
| Packs clash at a territory edge → displaced, scent posts | territory | ★ |
| Pack yields its kill to a bear → contested carcass | meat, occupancy | ★ |
| Bear claims and part-buries a carcass | meat, occupancy | |
| Bear raids a camp at night for food | meat | ★ |
| Bear strips a ripe berry stock → bare bush, scat | information | ★ |
| Bear marks trees → claw-mark signs | territory | ★ |
| Bear claims a den for winter | occupancy | ★ |

**Prey herds**

| Interaction | Currency | Status |
|---|---|---|
| Graze → grazed-bare ground | information | ✓ |
| Wear trails → game trail | structure | ✓ (trail wear) |
| Rut → antler sheds along the real path | structure, information | |
| Yard up in conifer cover in deep snow | information | ★ |
| Calving / renewal toward capacity | — (homeostasis) | ★ required (§6.3) |

**Scavengers** (new lightweight kind: ravens, fox, wolverine folded together)

| Interaction | Currency | Status |
|---|---|---|
| Circle a fresh carcass or body → **Beacon**: findable at range by everyone | information | ★ build first |
| Pick at carcass or body → speeds it to bones | meat | ★ |
| Raid a stash | meat | |
| Follow a predator → ravens over wolves read as predator sign | information | ★ |

**People (player and NPCs alike)**

| Interaction | Currency | Status |
|---|---|---|
| Forage, chop, butcher, stash, make camp | meat, structure | ✓ |
| Reuse an old camp → camps cluster into waystations | structure | ★ |
| Loot a stash, body or drop that is not theirs | meat, structure | |
| Bury a body → grave (permanent, readable) | structure | |
| Read predator sign → detour | information | ★ |
| Meet another person: trade, travel together, rob, pass | information, meat | |
| Defend against a predator | — | ✓ |

**Deliberate nulls:** herd × stash, herd × camp, herd × prints, scavenger × herd, mineral pools ×
anything but harvesting.

---

## 5. Signs, decay and bodies

- **Item decay:** each item kind has `{ LifeDays, DecaysTo }`, for example
  `stone_axe → axe_head → flint`. Frozen slows it, buried organics slow it, trampling speeds it.
  When crafting arrives, a recipe's inputs give the materials and the chain falls out of what the
  thing was made from.
- **Body pipeline:** body → skeletal → bones → gone. Scavengers speed it; graves are exempt; a body
  kept frozen by the local cold lasts far longer (the frozen traveller). Without this, a long settle
  produces a corridor of corpses (the POC went from 341 bodies to 4 over 10 years once this was in).
- **Snow and burial:** keep the rebuild's `Ground` model, which is richer than the POC's. Use the
  POC's vegetation catch and drift factors only as tuning inputs.
- **Succession:** burn scar → fireweed → birch → the pre-burn stand. Without it, fire is a one-way
  ratchet. Only matters once wildfire exists.

---

## 6. World generation

### 6.1 Pipeline

1. Terrain, water, vegetation (existing `MapGen`), then pools emitted by cause: flint where the
   geology has it, berries on willow margins, deadfall in forest, dens on high rock.
2. Named places and landmarks (Phase 4a).
3. **Settling pass:** `Sim.Advance` for one to two years. Herds, packs, bears, scavengers and (once
   competent) NPC travellers live through it. Signs accumulate, decay and get buried.
4. **Validation:** floors and distribution checks (§8). Reroll a degenerate world with the next seed
   derivative.
5. The settled world is the starting save. Terrain still regenerates from the seed; everything the
   settle produced is deltas, as now.

### 6.2 Attractors

Emergence needs crowding. World generation places features where life converges: river fords,
game-trail bottlenecks between valleys, overhangs, mineral licks, berry thickets, the pass approach.
The sim's own agents then pile history up at a few predictable places, and that is where the
stories are. Geology and ecology are not filler: they decide where things happen.

### 6.3 Homeostasis

Renewal is proportional to the shortfall, `p = base × max(0, (capacity − current) / capacity)`,
for herds, packs, scavengers and traveller arrivals. **Required before any long settle:** today the
rebuild has no calving or renewal, so prey only declines (8-13% a month in the Phase 3 runs) and a
year's settle would empty the valley. Flat respawn rates drift to extremes; tie them to the deficit.

---

## 7. Runtime: discovery and perception

- **Discovery:** keep the scouting accumulator. Hidden finds are built from the settled world, and
  each find's reveal difficulty is computed from state, not rolled:
  `size(kind) × burial × openness(vegetation) × freshness(age) ÷ vantage(observer)`.
  `Knowledge.SearchHours` generalizes from `PoolId` to `ThingRef`.
- **Connection surfacing:** when a find is revealed and an already-revealed one shares its
  `Cause.Who`, a perception roll also gives the link: "these tracks match the sign by the river".
- **No moments and no director** (§0.2). What would have been a moment is the world on the map and
  in the action list, answered with ordinary actions.
- **Perception** is sight first; sound is a second channel with its own range, added when a real
  need appears. Perception can be uncertain (a fox heard as a wolf), but never invents a source.
- **Legibility:** packs and travellers get names and traits (the Limping pack), checked for
  collisions; the trait shows in their prints and signs.

---

## 8. Tools (build before content)

1. **Find dump:** a flat text list of every find in a settled world: tile, kind, cause, age, reveal
   difficulty, its line of text. The primary QA instrument: ratios that are obvious in a list are
   invisible on a map.
2. **Distribution report:** finds per region by category, ratios against targets, population curves
   per species over the settle. The caribou-crash class of bug is a glance at a chart.
3. **World-density checks** (open question 7): what a player can actually run into per in-game day
   near a typical route, measured in headless runs. Which measures, and their floors, are not
   decided; the short-loop plan's day 1-3 cadence is the likely source, not the old game.
4. **Deterministic replay:** same seed gives a byte-identical settled save (the existing determinism
   contract, extended to world generation).

**Targets (starting points from the POC; re-validate here):** dead matter (bodies and bones) at most
35% of finds; resource finds at least 25%; no region below a floor or above 3x the median; traveller
survival 75-85% once NPC travellers exist.

**Floors per region:** water access, fuel, at least one shelter option. The sim layers freely on a
survivable skeleton.

---

## 9. Build order

Each step stops at a playable, testable state.

1. **Tools:** find dump and distribution report from a headless run.
2. **Substrate:** `ThingRef`, `Cause` on persisting things, `Signs.At`, `Access.Refusal`, the
   exhaustive-switch rule. No new content.
   *Gate:* existing tests pass; every existing refusal goes through `Access.Refusal`.
3. **Kill chain:** herd renewal (§6.3), scavengers with Beacon, hourly stepping for distant agents,
   settling pass (herds, packs, bears, scavengers only), body and bone pipeline, computed reveal.
   *Gate:* a settled valley yields carcasses of different ages with causes; connection surfacing
   links a kill's prints to its carcass; find-dump ratios within targets; a one-year settle runs in
   acceptable world-gen time.
4. **Effects, exposure and conditions:** the shared effect kernel and table (§3.3) to its
   verification bar, one `Exposure.At` (§3.4), conditions and clearing actions with tool tiers
   (§3.5), Contest/Ask, crossing odds (§3.6), fire as a boldness term. Tags fold into conditions.
   *Gate:* the §3.3 property tests and mutation score pass; no thing ever shows zero actions;
   `[cache (buried, frozen)]` walks Dig, Chip free, Pick up; a wolf pack on your kill answers
   Contest with its own boldness, and fire nearby changes the answer.
5. **Full interaction table:** the ★ rows above, dens and occupancy, contested kills, seasonal
   stock dynamics.
   *Gate:* the den arc, a contested carcass and a raided camp occur in settling logs with no
   dedicated code path.
6. **Travellers:** NPCs that survive a season, travelling through the valley and leaving camps,
   stashes, graves and bodies; human encounters with a narrow verb set (hail, trade, give, ask
   about, recruit, part). Depends on NPC competence (`dev/active/rebuild-npc-food-and-forage-review.md`,
   `rebuild-npc-hunting.md`).
   *Gate:* a settle produces camps, stashes, bodies and frozen travellers at target ratios; one
   traveller's log reads as an arc.

**Deferred, designed for:** the legacy run (the next run starts years later in the same valley,
with your old camp's traces decaying by their materials). Deterministic sim plus "the world is the
save" make it nearly free once step 6 exists.

---

## 10. Do not port (from the POC)

1. Interaction logic as scattered conditionals with no audit: a whole traveller kind could not eat,
   and nothing showed the empty cell. Here: exhaustive switches plus the §4 table in tests.
2. Distance-only eligibility: looting through snow, wolves across rivers. Here: `Access.Refusal`.
3. Global weather: the rebuild's weather is already regional by tile; keep it that way.
4. Flat respawn rates: tie renewal to the deficit.
5. Random reveal thresholds: compute them from state.
6. Bodies that never decay, fire that never recovers: port the fixed pipelines.
7. Names without collision checks: names are the legibility mechanism.
8. The corridor POC's structure: 1D route positions, the coarse rules table, the fine-clock
   promotion band. The rebuild's one 2D sim replaces all three.

---

## 11. Open questions

1. Settle length: is one sim year enough at the valley's densities? Measure with the distribution
   report.
2. Settle cost: a populated month is about 2 s at per-minute stepping. Hourly stepping for distant
   agents is assumed to bring a year into world-gen budget; measure before committing to two years.
3. Stash economy: decide how much gear should come from finds versus crafting, then tune stash
   creation and raid rates to it.
4. Scavenger granularity: one folded kind first; split ravens (beacon) from wolverines (raiders)
   only if the information side needs separate tuning.
5. Grave permanence: forever, or eventually claimed by sediment?
6. ~~Things' effects: per-minute tick or lazy catch-up?~~ Decided: every stack ticks every minute
   until that is a slowdown problem (decision record "Item effects tick every minute").
7. World-density checks: which measures and floors (parked by the user).
