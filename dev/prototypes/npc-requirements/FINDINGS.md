# NPC requirements tree: prototype findings

PROTOTYPE, throwaway. The question: does the NPC design hold up for one goal, "a fire that lasts the
night"? The design is a tier ladder, a committed goal sized by a forecast, a requirements tree rebuilt
every step, refusals as data, and four interrupts. The goal is to avoid obviously dumb choices, and to
see how many special cases it takes to get there.

Run: `dotnet run -c Release --project dev/prototypes/npc-requirements -- [seed=N] [only=<scenario>] [cfg=<config prefix>] [trace] [bench]`.
Code: `Planner.cs` (the logic), `Harness.cs` (scenarios on the Sim), `Program.cs`. The traces quoted
below are in `traces/`.

## Verdict

**The design mostly holds, with three changes.** These are what turned a planner that froze, died,
and burned its tinder on hopeless strikes into one that kept a fire going until sunrise in 26 of 28
winter nights (7 scenarios × 4 seeds), with no deaths:

1. **The fire's site has to be a "one of" of the goal.** Without it, a camp on open plain 3 tiles
   from wood fails 13 of its 16 nights, and 2 NPCs die in the snow scenario. With it, all 16 succeed.
2. **The sources have to be filtered by a survival forecast, judged against staying put, not against
   a fixed line.** A fixed line froze the NPC at a cold camp until it died, because every trip "failed".
3. **Pace has to be chosen by forecast.** A quick crossing in December snow carries a 75% injury risk
   on every tile, against 0% for a careful one. Two runs bled to death before pace was chosen.

None of the three is a special case: each is a one-of or a forecast that was missing. But reaching
26/28 also took **21 extra rules**, and **about 10 of them are rules the old `Npc.cs` already had**, in
new clothes (listed below). The tree removes most of the old special cases. It does not remove the
ones that come from fire physics and carrying capacity. Those need forecasts and refusals the Sim does
not yet publish.

## Setup: the real Sim, driven through the player's verbs

- **The world.** `World.New(seed)` for seeds 5, 11, 17 and 23, with the clock set to Dec 15 15:00 or a
  later day whose real weather fits (`Weather.At`; nothing is pinned). The NPC is played through the
  player's agent with its `PlayerMind`, so the old NPC mind never runs. Every action is `Sim.Step` on
  an activity taken from `Verbs.At` or checked with `Sim.Refusal`. Herds are removed, because predators
  are a separate question.
- **What the NPC knows.** It knows only its `Beliefs`, kept current by the Sim's own
  `Knowledge.Witness`: remembered forage density, trees, stashes and fire hours, plus explored tiles.
  It also uses three kinds of public facts: content tables (forage table, felling, fuel props,
  recipes), the generated pool definitions (a place's table and base density, which is static), and
  the Sim's public forecasts (`Fire.HoursItWillBurn`, `Fire.FuelHours`, `Trip.Terms`, `Trip.Risk`,
  `Verb.Odds`, `Tiers.*`).
- **The kit.** The starting pack (a hand drill, 0.9 kg of kindling, 0.18 kg of tinder), a stone axe
  unless noted, 1 L of water and 0.3 kg of dried meat. Capacity is 15 kg, about 10.6 kg of it free.
- **Scenarios.** Each starts at 15:00 and runs to sunrise (06:00 or 08:00), so the night needs 15–17
  fuel-hours. The open pit holds 12 kg. Weather is the seed's own: clear at 2–19 °F; snow at -16, 1,
  0 and 16 °F.
  1. Forest camp, with an axe; 1b without.
  2. Plain camp, trees exactly 3 tiles away, with an axe; 2b without.
  3. Snowy night: the plain camp with snow falling.
  4. Wet wood: **not modelled.** Fuel has no wetness, and weather never touches a fire (`Fire.cs` has
     no precipitation term). Wetness reaches the fire only through the strike odds, via dexterity.
     Not run.
  5. Picked-over: a forest camp whose 5×5 neighbourhood was foraged hard (4 × 1 h per tile). The NPC
     still remembers it as fresh, at base density.
  6. No tinder or kindling, 8 kg of birch in the camp stash, with an axe.
- **Forecast configurations.** A is crude everywhere: 1 kg of fuel = 1 h of fire, yield without
  thinning, 5 min per tile of walking, a flat 1 °F/h of cooling below freezing. B uses the Sim's own
  forecasts, plus thinning integrated from the Pools rule and cooling of (60 − wind-chill °F)/40 °F
  per hour. The five ablations each switch one part of B to crude.

## Results, 28 nights per configuration

| config | fire at sunrise | dead | mean min fire out (21:00–sunrise) | trips mean/max | reversals | goals per night |
|---|---|---|---|---|---|---|
| A (all crude) | 19/28 | 3 | 145 | 1.4 / 3 | 2 | 4.9 |
| **B (Sim forecasts)** | **26/28** | **0** | **11** | **0.9 / 3** | **2** | **2.1** |
| B, fire forecast 1 kg = 1 h | 26/28 | 0 | 16 | 0.9 / 3 | 2 | 2.2 |
| B, yield without thinning | 25/28 | 0 | 60 | 1.0 / 3 | 3 | 2.1 |
| B, source by travel only | 24/28 | 0 | 66 | 0.7 / 2 | 0 | 1.5 |
| B, no shared-trip credit | 26/28 | 0 | 8 | 0.9 / 3 | 2 | 2.2 |
| B, fire stays at home (no site one-of) | 18/28 | 2 | 102 | 1.2 / 3 | 2 | 4.9 |

Per scenario, B succeeded in: 1: 4/4; 1b: 3/4; 2: 4/4; 2b: 4/4; 3: 4/4; 5: 3/4; 6: 4/4.
- 1b missed sunrise by 23 min. That is the half-hour slack of RULE 21.
- The 2b miss, on seed 5, still had a fire at sunrise but was out 67 min in the night.
- The picked-over misses had the fire out for 104–105 min. Picked-over ground is the hardest case.

A per scenario: snowy night 0/4, with 3 dead; picked-over 1/4. The rest went roughly as in B.

## Answers

### 1. Does it trip-loop?

**No.** B makes 0 to 3 trips per goal-night (mean 0.9). Two one-tile reversals occurred in 28 nights,
both in the picked-over scenario, both when the NPC stepped onto a tile it remembered as fresh, saw it
was thinned, and turned to the next one. That is learning, not looping.

The loop that does exist is in goals, not trips. The time-budget interrupt fired 94 times in 28 B
nights, and nearly every time the very same goal was re-adopted in the same step with a new, shorter
forecast. In one picked-over night that happened 13 times. No behaviour changes, so as written the
budget interrupt is noise.

### 2. Which refusals were states rather than items?

Every refusal the Sim returned in 196 runs was a state. No item refusal ever reached the Sim, because
the planner checked shortfalls first (`LightShortfalls`, which lists every missing item as data). There
was no drift between that check and `Fire.Refusal`.

| refusal or interruption | count | kind | maps to |
|---|---|---|---|
| "the fire is not hot enough for oak" | 29 | state | A verb (add kindling or pine, or wait for the heat), but sometimes a dead end: a small fire never reaches oak's 500 °F. The forecast counted oak's 6 h/kg as usable, so it chose oak trees it could not burn. **Needs a Fire forecast: "would this fuel catch on this fire"** (`WillTakeALog` is private). |
| "your pack is full" (unstash) | 4 | state | Verb: put something down (RULE 13). Clean. |
| "you can barely move" | 6 refusals + 3 interruptions | state (body) | Dead end: rest. Clean. |
| "exhausted" (interruption) | 9 | state | Verb: rest or sleep. Clean. |
| "too dark to work" (chop) | 0 reached | state | Verb: torch, which needs a torch item and a flame (depth 3). The planner avoided it by forecasting the dark hours, which meant copying `Work.TooDarkToWork`'s 05:00 and 21:00 (RULE 3). **Needs a public "dark at minute m here" forecast.** |
| "the fire is full" | 0 reached | state | Verb: time (wait or sleep). But `AddFuel` accepts slivers into a nearly full pit, so the refusal arrives too late to be useful (RULE 15). |
| "fire already burning" | — | state | Dead end for Light. Not needed. |

Two states never appeared as refusals at all. They are where the old special cases came from:

- **Kindling.** `StartFire` with 0 kg of kindling is allowed. The fire lights on tinder, dies, and the
  logs are refused later as "not hot enough". The light refusal never names what a goal fire needs.
  The tree had to add kindling itself (RULE 7, old rule #23).
- **Cold.** Nothing refuses a walk into a lethal night. Cold reaches the planner only through the
  cooling forecast, and through strike odds that sink to 10–22% with cold hands (s11 picked-over: five
  failed strikes in a row). The odds are public data, so sizing the tinder by them is general (RULE 6,
  old rule #42).

### 3. How crude can the forecasts be?

- **Fire forecast:** crude is fine. "1 kg = 1 h for any fuel", inside B, scored 26/28 like B. Its one
  bad night was the s17 snowy night, out 207 min: the NPC slept on a kindling-and-pine fire it
  reckoned at 1 h/kg, and kindling burns at 1.5 kg/h. In A, before the later fixes, the same error let
  a fire die at about 04:00 while the NPC slept through (forest, no axe). So 1 kg = 1 h is good enough
  for choosing; `Fire.HoursItWillBurn` is worth having for timing sleep.
- **Yield:** it must thin. Without thinning, the picked-over scenario left the fire out 214–673 min on
  all four seeds, because the NPC stays on a thinning tile expecting the first hour's rate. The
  thinning integral had to be a private copy of `Pools.ForageDensity`'s 1/(1+2h) rule (RULE 4).
  **Pools should expose an expected-yield forecast.**
- **Travel:** must not be 5 min per tile. The real crossing was 17–26 min in clear weather, 26–130 min
  in snow, and grows as the body cools. A's flat 5 min killed the NPC on the snowy night on 3 of 4
  seeds. In s5, it set out for wood 3 tiles away, forecasting 15 min each way, and took 26, 46 and 46
  min out, then 79, 92 and 130 min back; its core fell from 98.6 to 79.9 °F (`traces/s5-A-3.txt`).
  `Trip.Terms` is needed; caching it per edge per game hour costs nothing in accuracy (see 6).
- **Cooling:** crude is fine. (60 − wind chill)/40 °F/h and a flat 1 °F/h gave the same decisions
  wherever travel was right. Accuracy matters less than having a cooling forecast at all, because
  without one there is no way-home-dangerous interrupt and no survival filter.

### 4. Does "quickest source" need travel and gathering time?

**It needs both.** Travel only scored 24/28, and on picked-over ground it scored 1/4: the NPC picked
the nearest tile whatever its yield, and foraged thinned ground for hours (`traces/s5-Btravel-5.txt`,
out 560 min). On fresh forest, travel only was as good or slightly better (1b: 4/4 against 3/4),
because there every tile's yield is the same. The gathering time also has to thin (see 3).

### 5. Items in different places

**Nearest place first is enough here.** But these scenarios rarely spread a list: a mixed-forest forage
tile yields tinder, kindling and wood together, so lists were usually "camp stash plus one forage
tile" or "forage here, chop there".
- Switching off co-location credit, so a second item on an already-listed tile no longer travels
  free, changed nothing (26/28 against 26/28, night-out 8 against 11 min).
- Nearest-first ordering produced no reversals beyond the two picked-over ones.
- Untested: items truly scattered, such as flint at the rocks and wood in the forest. That needs a
  recipe goal.

### 6. Cost per re-plan

Measured in Release, after warm-up, averaged over 200 calls:

| call | µs |
|---|---|
| one tree build (`Build`: two Dijkstras over a radius of 12, about 300 tiles, plus all sources) | **106–169** |
| of that, `Verbs.At` for the strike odds | 56–90 |
| one Dijkstra, edge minutes cached | 5–24 |
| one Dijkstra, uncached `Trip.Terms` per edge | **~1,000** |
| decision with a new goal (site choice: up to 7 builds) | **960–1,470** |
| ordinary step decision (1–2 builds plus `Verbs.At`) | ~300 |

- An NPC re-plans about 38 times a night.
- For 20 NPCs that is about 760 plans a night: 0.25–1.1 s of planning per game night, about 0.3–1.3 ms
  per game minute.
- For comparison, the Sim itself costs about 0.2 ms per game minute (12 ms per game hour, no NPCs).

**This is fine for 10–20 NPCs, if two things hold:**
- `Trip.Terms` edge minutes are cached per game hour and shared by all planners. Uncached, a decision
  with site choice costs 7–12 ms, roughly 10 s per game night for 20 NPCs.
- Site choice runs only when a goal is made, as here.

### 7. What had to be special-cased

These are every rule added beyond the general mechanisms. "Old" names the matching rule in the
architecture audit, §4.5.

| # | rule | kind | old |
|---|---|---|---|
| R1 | A forage source is planned for at most 1.5 h per trip | tuning constant | — |
| R2 | Re-plan at least every 30 min of work | tuning constant | — |
| R3 | Copy of `TooDarkToWork`'s hours (05–21) | **interface gap**: no public darkness forecast | #69 |
| R4 | Thinning integral copied from Pools | **interface gap**: no expected-yield forecast | — |
| R5 | Mixed-wood shares copied, to get an expected yield per tree | **interface gap**: `MixedWoodAt` only samples | — |
| R6 | Tinder sized by strike odds: 1.5/p strikes, 2 to 8 | forecast use (general) | #42 |
| R7 | Lighting needs 1 kg of kindling, though the refusal never asks for it | **interface gap**: light refusal vs a goal fire | #23, #43 |
| R8 | A tree is no source if felling it would end after dark, without a torch | state forecast | #29 |
| R9 | Once the goal is done: feed the fire from the stash, sleep between. Lighting isn't covered, which cost scenario 6 a 15-min idle wait. | maintenance, outside the tree | #14 |
| R10 | Sleep until the pit has room for 1 kg, or until 30 min before the fire dies; to sunrise if it will last | maintenance timing | **#15** |
| R11 | Stoke with the most hours per kg the fire accepts; never put the tinder on | fuel choice | #36, #40 |
| R12 | Fire too cold for logs: add kindling, else wait 5 min | state → verb | #37 |
| R13 | Before a trip, put down the logs in the pack; at home, logs count as free capacity | capacity | #35, #44 |
| R14 | Light only once this trip's list is in hand, unless the core is below the Need line | all-of execution order | **#41** |
| R15 | Stoke only with room for a full kilo | **Sim quirk**: `AddFuel` takes slivers | — |
| R16 | Stashed fuel goes on the fire from the stash; only makings are unstashed early | capacity | #38 |
| R17 | Wood counts as warmth only if a fire can be had with it (burning, or makings in hand) | **interface gap**: `Tiers.WarmthTier` takes raw fuel hours | **#33** |
| R18 | A source is unsafe only against staying: with no fire to stay by, any trip may go | survival forecast | §4.3 Afford / #4 |
| R19 | Pace: quick or careful, by minutes plus injury odds priced at 60 min | forecast use (an exchange rate) | — |
| R20 | The site moves only if under 0.75× the home cost, minus 15 min | hysteresis | **#79** (the 1.25 margin) |
| R21 | Half an hour short of the night counts as a night | slack for rough forecasts | #45-ish |
| SITE | The goal's fire site is a one-of; committing to it moves home | general (a one-of) | #4, #22, #28, #30 |

Of the 21, five are interface gaps (R3, R4, R5, R7, R17) and one is a Sim quirk (R15). Those six
vanish once the Sim publishes the forecasts and refusals listed below. Of the rest:
- Four are tuning constants or exchange rates (R1, R2, R19, R21).
- Four are maintenance and execution-order rules that the tree does not express (R9, R10, R14, R16).
- The remaining few re-derive old rules from fire physics.

The old rules that came back are #15, #23, #33, #41, #42 and #79, plus the camp-move family. **They
were not accidents of the old design. They are facts about fires and packs that any planner has to
know, and the place for that knowledge is the Sim's forecasts, not NPC code.**

## Surprises

- **Every quick crossing in December snow injures, at 75%.** The bench never noticed. Whatever the
  AI, this is worth a look in the Sim (`Trip.Risk`).
- **Lighting first is dumb; lighting last is right, unless cold now.** The all-of's consuming verb
  (`StartFire`) has to wait until its inputs are in hand. This is RULE 14: an ordering the tree must
  state, and the old #41 again.
- **A goal can be "done" in fuel-hours and still unreachable.** Eight kilos of birch and no tinder read
  as warmth None, and the NPC waited (scenario 6, before R17). The tier must take its fuel hours from
  the tree, not from raw kilos.
- **Interrupts without alternatives do nothing.** "Way home dangerous" fired 49 times in B, and the
  re-plan usually picked the same thing, because the only better option, a fire here, did not exist
  until the site one-of was added. An interrupt is only as good as the tree's alternatives.
- **The survival filter must compare against staying.** As a fixed line, it froze an NPC at a cold camp
  and let it die, because every trip failed the line and so did staying. Judged against staying, it
  lets the NPC go.
- **A 12 kg pit means waking every 25–40 min through the night to add a kilo** (trace s5 B 2, about 14
  wake-ups). Correct, given the pit, but worth knowing before NPCs sleep near the player.
- **The site one-of jittered between adjacent forest tiles** for 5-minute differences until R20 was
  added, and once moved away from a burning 8.4 h fire because a 0.1 h shortfall at home was
  "impossible" (s11, before the fix).
- **Health has no tier.** Before pace was fixed, two NPCs collected an injury on almost every crossing
  and died of blood loss beside a burning fire, with nothing on the ladder to interrupt for it.

## What to change before the rewrite

In the Sim (order item 2 on the roadmap: forecasts):
1. **`Fire`:** a forecast of whether a fuel will catch on this fire (wrap `WillTakeALog`), and the light
   refusal's shortfall should cover what a fire that takes logs needs, not just the strike. This
   removes R7, R12 and the oak dead end.
2. **`Pools`:** expected yield over h hours of foraging at the current density, and expected yield per
   tree. This removes R4 and R5.
3. **`Work`:** whether it is dark at a minute and tile. This removes R3.
4. **`Fire.AddFuel`:** refuse, or report, a top-up under a useful size, so "full" is a real refusal.
   This removes R15.
5. **`Tiers.WarmthTier`:** take the hours of a fire the person can have, not raw kilos. This removes R17.
6. **`Trip`:** a cheap per-edge crossing time for planners, cacheable per hour; uncached, it is 98% of
   the planner's cost.
7. Look at the 75% winter crossing injury risk.

In the design:
1. **Make the goal's site a "one of"** (home, or any known wood) and commit to it with the goal. This is
   the camp-move family, done once.
2. **Judge the survival filter against staying, and price pace with `Trip.Risk`.**
3. **Make the time-budget interrupt act.** On overrun, re-forecast with the rate actually observed, or
   don't re-adopt the same goal; today it only resets itself.
4. **Give all-of nodes an execution rule:** the consuming verb runs when its inputs are in hand.
   Maintenance after a goal (feed, relight, sleep) needs a home of its own: a standing "keep" goal,
   not a fallback.
5. **Give health a tier**, or the ladder can't interrupt for injury.
6. **Expect about 20 rules, not 86.** Most of the remaining ones are tuning and exchange rates. Each
   should live beside the forecast it tunes.

Full traces for every scenario are in `traces/`.
