# Emergent World: Signs, Settling and Moments

*Design for the clean-room rebuild's content layer. It replaces tensions and most authored events
with content that falls out of the simulation. The plan (`clean-room-architecture.md`) points here
for Phase 4 onward.*

**Status:** design agreed in direction (2026-11-20); not yet built.
**Source:** adapted from the "Emergent Discovery System" handoff written for the corridor game
(text-survival-2), whose findings came from a throwaway world-gen POC. This doc keeps its findings
and changes what does not fit the rebuild: a 2D grid valley with a full per-minute agent sim.

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
| WoundUntreated, FeverRising | Body effects |
| DeadlyCold | Core temperature and distance to a fire |
| SmokeSpotted | A burning fire other agents can see |
| ShelterWeakened, Infested | A camp structure's condition |
| Disturbed, Shaken | Fear effects on your body, learned fear in a herd's mind |

So tensions go. What they gave the player that raw state does not is kept, by other means:

- **Perception.** Tension is the player's *belief* about a real thing. A pack following unseen is
  not tension; fresh prints over your own trail are. Beliefs are plan decision 7.
- **Pacing.** A director that manages attention, never facts (§7).
- **Legibility.** Every sign carries one hop of cause (§3.2), so a moment can say why.

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
3. **One hop of cause.** Every sign points to its source and its cause. Investigate answers "what
   made this". Sagas, provenance epics and deep history are out of scope.
4. **Emergence comes from chains through shared currencies, not from coverage.** Interactions read
   and write five currencies: **meat, occupancy, territory/scent, information, structure.** An
   interaction earns its place only if it touches one. Deliberate nulls are listed and stay null.
5. **Facts never move after settling for pacing reasons.** Pacing biases what the player notices
   and when, never what exists.
6. **No language model in this system for now.** Text comes from templates over fact records.

---

## 3. Data model

Extends what the rebuild has: plain records, IDs only, static systems, `Refusal` functions,
anything derivable computed on read. No ECS, event bus, rules DSL or inheritance tree.

### 3.1 One reference to any thing

Signs, access checks, verbs and discovery all need to point at any kind of object:

```csharp
public enum ThingKind { Agent, Carcass, Pool, Stash, Fire, Shelter, Track, Sign }
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

### 3.3 Tags and capabilities are computed bitsets

```csharp
[Flags] public enum Tag : ulong
{
    Occupied, Owned, Guarded, Sealed, Buried, FrozenIn, Elevated, Unstable, ScentSource, Edible,
    Harvestable, Sheltering, Burnable, Fresh, Contested, Ripe, Depleted, Beacon, Crossing  // ≤ 20, powers of two
}
[Flags] public enum Capability
{
    Cutting, Striking, Prying, Digging, Smoke, Light, Reaching, Bait, Spark, Binding, Carrying, Piercing  // ≤ 12
}

public static Tag TagsOf(World w, ThingRef t);         // Ground says Buried, a nearby agent says Guarded
public static Capability CapabilitiesOf(Inventory p);  // OR over the GearDefs in the pack
```

- The enum is the closed vocabulary. Adding a bespoke tag for one situation is the failure mode;
  adding one is a visible code change.
- Tags are never stored, so they cannot go stale and do not touch save determinism.

### 3.4 Verbs from a small affordance table

The one place a data table pays off: about 14 rows, and its output is existing `Activity` records.

```csharp
sealed record Affordance(Tag Needs, Capability Uses, string Verb, Func<AgentId, ThingRef, Activity> Make);

static readonly Affordance[] Table =
[
    new(Tag.Occupied, Capability.Smoke,    "drive out with smoke", (a, t) => new DriveOut(a, t)),
    new(Tag.Occupied, Capability.Bait,     "lure out",             (a, t) => new Lure(a, t)),
    new(Tag.FrozenIn, Capability.Striking, "chip free",            (a, t) => new ChipFree(a, t)),
    new(Tag.Sealed,   Capability.Prying,   "force open",           (a, t) => new Force(a, t)),
    new(Tag.Buried,   Capability.Digging,  "dig out",              (a, t) => new Dig(a, t)),
    new(Tag.Guarded,  Capability.Light,    "drive off with fire",  (a, t) => new Contest(a, t)),
    new(Tag.Edible,   Capability.Cutting,  "butcher",              (a, t) => new Butcher(a, ...)),
    // ...
];

public static IEnumerable<(string Verb, Activity Act)> Verbs(World w, AgentId who, ThingRef t)
    => Table.Where(r => TagsOf(w, t).HasFlag(r.Needs) && CapabilitiesOf(PackOf(w, who)).HasFlag(r.Uses))
            .Select(r => (r.Verb, r.Make(who, t)))
            .Concat(Universal(who, t));
```

**Universal verbs, always present** (a find never offers zero verbs): investigate (surfaces `Cause`
and connections), take / take some (taking less leaves less trace for an owner to notice),
wait and watch, avoid, press on, detour.

The player's menu, a moment's choices and the NPC's options all come from `Verbs`, so they are
always the same set. "Wave a torch at the wolf" exists only if you carry a burning torch.

### 3.5 One access gate

```csharp
public static string? Access.Refusal(World w, AgentId who, ThingRef t);
```

Refused if buried deeper than the actor can dig, guarded by a live agent within its guard radius
(unless the activity is the confrontation), frozen in without striking or cutting, or across water
without a crossing. Every existing `Refusal` (`Work`, `Fire`, `Combat`, `Traversal`) calls it
first. This removes the class of silent absurdity, such as looting through a metre of snow, that
the corridor POC hit.

### 3.6 Indexes that are never saved

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

## 7. Runtime: discovery, moments and pacing

- **Discovery:** keep the scouting accumulator. Hidden finds are built from the settled world, and
  each find's reveal difficulty is computed from state, not rolled:
  `size(kind) × burial × openness(vegetation) × freshness(age) ÷ vantage(observer)`.
  `Knowledge.SearchHours` generalizes from `PoolId` to `ThingRef`.
- **Connection surfacing:** when a find is revealed and an already-revealed one shares its
  `Cause.Who`, a perception roll also gives the link: "these tracks match the sign by the river".
  One lookup; the most valuable sentence the system produces.
- **Moments** replace events. A moment fires when a real dynamic crosses a threshold the player can
  perceive: a pack closes to sight range, the fire dies with a predator near, a storm front reaches
  you mid-crossing. Its choices are `Verbs` (§3.4) over what is present. Outcomes go through the
  same activities and effects as everything else.
- **Director:** manages attention only. It may lower the reveal threshold of the next real thing
  after a dead stretch, or time a sound during a lull. It never creates a fact.
- **Legibility:** packs and travellers get names and traits (the Limping pack), checked for
  collisions; the trait shows in their prints and signs.

---

## 8. Tools (build before content)

1. **Find dump:** a flat text list of every find in a settled world: tile, kind, cause, age, reveal
   difficulty, its line of text. The primary QA instrument: ratios that are obvious in a list are
   invisible on a map.
2. **Distribution report:** finds per region by category, ratios against targets, population curves
   per species over the settle. The caribou-crash class of bug is a glance at a chart.
3. **Content-density metrics** as tests, the way the forage retune was measured: real decisions per
   play hour, distinct moment types per 10 days, signs met per trip. Fail the build if they drop.
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
4. **Affordances and moments:** tag and capability vocabulary, the affordance table, universal
   verbs, moments with a minimal director. Paper-exercise the old discovery and event catalog
   against the vocabulary first: each entry is marked replaced (and by what) or left. The left
   entries form `authored-content-catalog.md`, migrated later.
   *Gate:* no find ever offers zero verbs; a torch makes "drive off with fire" appear.
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
