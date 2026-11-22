# Animal life: shared body needs and a need-seeking planner

Spec for issue #50, part of the map #49. Written 2026-11-22 from the grilling with Jack. A dated record of
what was planned; lasting decisions are in `documentation/decisions/` ("Animals are individuals; a herd
is derived", "Animals read the slow world true and sense what moves", and "Stat tiers decide priority
before the stat hierarchy" as amended) and lasting terms in `GLOSSARY.md`.

## Problem Statement

The valley's animals don't live; they run scripts. A herd is one agent following a hand-written state
machine for its behaviour kind, switched by hunger thresholds. It grazes a tile that has been eaten
bare and still fills up. It never drinks, never gets tired, and sleeps only when a state happens to
say "rest". Only wolves can starve, and that's a renewal rule, not their bodies. It stays inside a
fixed list of tiles. Ravens, a fox and a wolverine are folded into one 3 kg walking "flock". The night
is one flat boldness bonus for every species.

Jack expects the world to come out of animals travelling to water and food, sleeping at their natural
times, avoiding predators and seeking prey. None of that can come out of the current code, because
the decisions aren't made from needs at all. NPCs, meanwhile, have their own separate way of finding
food, water and rest. That's two copies of the same idea that will drift apart.

## Solution

Animals and people share one body model and one way of looking for what they need.

- Every animal is its own agent with its own body. Its body gets hungry, thirsty and tired, and it can
  starve, die of thirst or collapse from exhaustion the same way a person can. Animals don't track
  warmth for now.
- When a stat comes due, an animal (or an NPC) finds a **source** for it: grazing, open water, snow, a
  den, a carcass, prey. It weighs each source by how long the trip takes plus how long the need takes
  to meet there, against staying put. It goes only if the trip is clearly better.
- Herds aren't stored; they're what you see when animals of one species stand together. Animals keep
  together because company means less time with their heads up watching. Big herds break up because
  a crowd eats a tile out. Wolf and hyena packs are animals that know each other, and a rival pack is
  strangers, so territories come out of packs avoiding each other.
- Animals know the lie of the land, the water and the grazing by heart. They know prey, predators and
  carcasses only by sensing them.
- Each species sleeps at its own hours (diurnal, crepuscular or nocturnal) and for its own length of
  time. A herd that was run all night will sleep in the day.
- Snow quenches thirst at a real calorie cost, so mammoths look for open water in winter and caribou
  hardly care. Rain and thaw ponds count as water.
- Ravens fly and leave no prints. Foxes walk and do.
- The player sees all of this the way the world really is: caribou drift off a grazed slope, wolves
  sleep through the middle of the day, the herd at the pond in spring is there because the river is
  still frozen.

## User Stories

### The player reading the world

1. As a player, I want caribou to leave a slope once they've grazed it down, so that I can read where
   the herd will be next from where it has been.
2. As a player, I want animals to walk to water when they're thirsty, so that rivers, lakes and ponds
   become places where I can expect animals and danger.
3. As a player, I want herds to scatter onto meltwater ponds and puddles after a thaw or rain, so that
   the weather changes where animals are.
4. As a player, I want mammoths to seek open water in deep winter while caribou don't bother, so that
   the size of an animal shows in how it behaves.
5. As a player, I want wolves to be active at dawn and dusk and asleep in the middle of the day, so
   that the time I choose to travel changes my risk.
6. As a player, I want hyenas to move by night, so that night is dangerous in a way that depends on
   what lives near me.
7. As a player, I want grazers to sleep little and predators to sleep a lot, so that the valley is
   alive at different hours.
8. As a player, I want a herd that was chased all night to be resting in the day, so that I can read
   what happened to it.
9. As a player, I want animals to die of hunger and thirst like I can, so that a hard winter thins the
   herds and leaves carcasses for scavengers and for me.
10. As a player, I want the weakest caribou to starve first while the rest go on, so that a herd
    shrinks the way a real one does.
11. As a player, I want herds to be bigger on rich summer ground and to break into small bands on thin
    winter ground, so that the season shows in the animals.
12. As a player, I want a lone animal to look for others of its kind, so that a straggler on its own
    stands out as vulnerable.
13. As a player, I want two wolf packs to keep apart, so that I can find where one pack's ground ends
    by reading their prints.
14. As a player, I want a young wolf to leave a big hungry pack, so that I may meet a lone wolf that
    behaves differently from a pack.
15. As a player, I want animals to avoid ground where they sense a predator, so that the presence of
    wolves changes where the caribou go.
16. As a player, I want caribou to go to the far lake because wolves hold the near one, so that I can
    read the danger from the prey.
17. As a player, I want ravens to fly straight across lakes and rough ground and leave no prints, so
    that a raven circling tells me something without a trail to follow.
18. As a player, I want foxes to walk and leave paw prints, so that a fox is tracked like any small
    animal.
19. As a player, I want a herd moving through deep snow to go slower and keep to worn trails and bare
    ground, so that winter shapes where animals walk.
20. As a player, I want animals not to twitch between two equally good spots, so that their movement
    looks purposeful and I can predict it.
21. As a player, I want animals to commit to a trip and finish it unless something happens, so that a
    herd I see heading for the lake gets there.

### Hunting and danger

22. As a hunter, I want a herd's watchfulness to come from how many animals share the watch, so that
    stalking a big herd and a lone animal feel different for a reason.
23. As a hunter, I want a pack's will to hunt big prey to come from its size, so that a pair of wolves
    leaves bison alone and a pack of eight doesn't.
24. As a hunter, I want hungry animals to range further and take more risks, so that late winter is
    more dangerous.
25. As a hunter, I want predators to find prey by sensing it, not by knowing where every animal is, so
    that hiding and staying downwind can matter.
26. As a hunter, I want a pack defending a kill to decide from each wolf's own boldness and how many
    of its pack are there, so that a kill with two wolves on it is a different problem from one with
    six.
27. As a hunter, I want animals fleeing the same threat to flee together, so that a herd breaks and
    runs as one when I'm seen.

### NPCs

28. As a player sharing the valley with NPCs, I want NPCs to choose where to get water, food and rest
    the same way animals do, so that one rule explains everyone's movement.
29. As a player, I want NPCs to keep refusing trips that would leave them too cold, so that they still
    respect the fire-tether.
30. As a player, I want NPCs to drink from ponds when they're open, so that rain and thaw help them as
    they help animals.

### Designer and developer

31. As the designer, I want body needs to be the same components for people and animals, so that a
    rule about hunger or thirst is written once.
32. As the designer, I want which components a kind of body has set in code, so that species don't
    vary by data in ways that don't matter.
33. As the designer, I want each species' activity pattern and sleep hours in content data, so that a
    new species is a row, not code.
34. As the designer, I want the flat night bonus gone, so that night is what each species' pattern
    makes it.
35. As the designer, I want the planner split into need, sources, routes and choice, so that each
    piece is testable and replaceable on its own.
36. As the designer, I want each resource's owning system to forecast what its sources pay, so that
    no planner copies a system's rules.
37. As the designer, I want one route search for people and animals on one terrain formula, so that
    snow, slope and trails slow everyone the same way.
38. As the designer, I want the planner's costs in one currency (minutes) with calories converted at
    the agent's own earning rate, so that there are no hand-tuned weights between needs.
39. As the designer, I want the planner to plan for the top-ranked stat only, so that it never weighs
    one stat against another.
40. As the designer, I want threats to be hard refusals in the planner, not weights, so that danger
    avoidance is legible and needs no tuning.
41. As the designer, I want herd size to come out of vigilance against crowding, so that there are no
    split or merge rules to maintain.
42. As the designer, I want packs to be animals familiar with each other, so that packs, rivals and
    dispersal come from one number.
43. As the designer, I want familiarity to be one sparse number per pair, used only by species that
    need it, so that it costs nothing for caribou.
44. As the designer, I want the fixed territory removed, so that where an animal lives comes from its
    needs and its den.
45. As the designer, I want ravens and foxes as real species instead of the carrion fold, so that each
    has its own body, diet and way of moving.
46. As the designer, I want the herd-only behaviours (fleeing, hunting together, defending a kill) to
    be rules in each animal's own mind, so that there's one mind for every animal.
47. As the designer, I want a gate that fails the individual model if herds scatter, merge into blobs
    or packs dissolve, so that we find out before building more on it.
48. As the designer, I want a measured comparison of stored paths against re-planning, so that we keep
    stored paths only if they pay.
49. As a developer, I want the change to stay within a set allocation budget, so that long sims and
    sweeps stay usable.
50. As a developer, I want old saves to fail loudly and the save format version bumped, so that format
    changes are deliberate.
51. As a developer, I want the tests that pinned the old herd mind rewritten as behaviour tests, so
    that coverage of what the player sees isn't lost.
52. As a developer, I want runs to stay deterministic for a seed, with animals deciding in a fixed
    order, so that golden and gate tests still mean something.

## Implementation Decisions

### Bodies

- Hunger, thirst and rest are shared body components, the same code for people and animals. Warmth
  stays person-only for now. Which components a kind of body carries is set in code, not data.
- Animals tick the same body update as people, minus warmth. That replaces the animal-only tick and
  the "shortfall ignored" floor in animal metabolism. A calorie shortfall past both reserve floors
  damages organs for animals too. Thirst damages organs at zero hydration as it does for people.
- The wolves-only starvation rule in renewal goes. Deaths from hunger and thirst come from the body,
  one animal at a time.
- Rest drains awake and refills asleep at rates set by each species' **sleep hours per day** (new
  species field). People keep their current rates, about 8 hours.
- Each species carries an **activity pattern**: diurnal, crepuscular or nocturnal, in content data.
  - Wolf, Megaloceros, SaberTooth and Fox are crepuscular.
  - Hyena is nocturnal.
  - Caribou, Bison, Mammoth, Bear, CaveBear and Raven are diurnal.
  - People are diurnal.
- Outside its active hours an animal prefers to rest and is less watchful and warier. Inside them it
  is bolder. This replaces the flat night boldness bonus. Light is still read through the one night
  rule.
- Rest is a real stat on the tier ladder. If its tier rises above everything else, the animal sleeps
  in its active hours.

### Agents and herds

- Every animal is its own agent with one body and its own mind. Per the decision record "Animals are
  individuals; a herd is derived", a **herd** is the animals of one species on one tile, computed when
  needed and never stored.
- Combat sides, the head counts for tracks and trail wear, boldness from numbers and the UI's herd
  view all read the derived herd.
- Agents decide in a fixed order (by id) so runs stay deterministic.
- Lame animals already split off today. That rule disappears, because each animal is already alone in
  its decisions.
- Spawning and population code creates N one-animal agents where it now creates one N-member herd.
- **Familiarity**: one number per ordered pair of agents, stored sparsely on the agent and only for
  species whose company term uses it (wolves and hyenas). It grows with time spent on the same tile,
  fades apart, and drops when one attacks the other. A **pack** is the wolves familiar with each
  other, derived and never stored.
- Open-group species (caribou, bison, megaloceros, mammoth) count any animal of their species on the
  tile as company. Closed-group species count only familiar animals. Unfamiliar animals of their own
  species are competitors and, for predators, a danger.

### Company and crowding

- An animal's food payout at a source accounts for who else is there:
  intake ≈ (1 − vigilance(n)) × min(its own rate, the tile's supply ÷ n).
  - Vigilance falls as n grows and levels off, reusing the existing prey vigilance curve.
  - The share of the tile falls with n.
  - Together they give an optimal group size that depends on the season and the ground.
- Predators use the same shape. The benefit is the chance of making a kill by pack size, a new Combat
  forecast. The cost is each one's share of the kill.
- The combination (company term) belongs to the animal mind, the system making the decision. It reads
  the vigilance and kill forecasts published by their owners.
- When the animals an agent is travelling with leave its tile, that's an interrupt: it re-weighs
  following against staying. Following reuses their destination, and company keeps its value, so a
  group mostly moves as one.

### What animals know

- Per the decision record "Animals read the slow world true and sense what moves", animals have no
  Memory.
- They read the true state anywhere on the map of things that stay put or change slowly: terrain,
  water, whether it's frozen, ponds, dens, how grazed a tile is.
- They know things that move only by sensing them now: prey, predators, carcasses, fresh prints. On
  top of that they keep a short expiring sightings list. Sensing uses the same sight system as people,
  scaled by the species' perception.
- People keep Memory. Both feed the same source listing.
- The fixed territory is removed. A den is a rest source for the species that has one. Carrying
  capacity in renewal becomes a per-species number across the map.

### The planner: need → sources → routes → choice

Four separate pieces, shared by NPCs and animals.

1. **Need.** The tier ladder: warmth > water > rest > food within a tier, rest before food as amended.
   It says which stat is due and how much it takes to bring it back to its seen-to level, using the
   existing hysteresis. Plans are made for the top-ranked stat only.
2. **Sources.** The system that owns each resource lists the candidate sources the agent knows of,
   from Memory for people and from the true world plus sensing for animals. Each source comes with a
   **forecast**: its payout rate (kcal, ml or rest per minute, counting company and crowding) and any
   calorie cost per unit. The agent's own tile is always a candidate. The sources:
   - **Food:** grazing pools for the diet, carcasses, prey (for predators, via the hunt), and stashes
     and pools for people.
   - **Water:** rivers, lakes, site water, unfrozen standing water, and snow.
     - Snow pays water at about 117 kcal per litre, the cost of melting it and warming it to body
       heat, for every species.
     - People keep their current rule: ice hole or snowmelt, and drinking open water.
     - Standing water becomes a source for people too.
   - **Rest:** the den if the species has one, otherwise where the agent stands.
3. **Routes.** One Dijkstra from the agent's tile on the one terrain formula, with ground state
   included, for people and animals.
   - It returns travel minutes and kcal to every candidate in one pass.
   - It stops expanding once travel alone exceeds the best total found so far.
   - People route over the tiles they've explored. Animals route over any tile.
   - Animals cross at the pace of the agent's body.
   - Flyers pay a flat cost per tile, ignore terrain and water, and can't cross the mountain wall.
   - The current planner forecast that refuses herds becomes the shared one.
4. **Choice.**
   - For each candidate: total minutes = travel + amount ÷ rate + kcal costs converted to minutes at
     the agent's current best food rate.
   - Take the minimum. Move only if it beats staying by about 25% (one named constant).
   - Predators are hard refusals: never route through, or pick a source on, a tile where a threat is
     sensed or freshly remembered.
   - NPCs keep their warmth filter on top (refuse a trip whose forecast leaves them too cold).
   - The output is a **plan**: a destination, its path, and the expected work there.

- **Stored paths.** A plan's path is stored on the agent and saved. It is re-planned only when:
  - a threat is sensed on or near the path;
  - the next step is refused;
  - a stat rises to a higher tier than the plan's;
  - the source turns out to be gone on arrival;
  - the animals it's travelling with leave.

  The first planner ticket benchmarks this against re-planning every decision (Testing Decisions), and
  stored paths stay only if they clearly pay.
- The planner uses only the Sim's public interface (verbs, refusals, forecasts, Memory or the animal
  knowledge read), per the decision record on NPC AI.

### Grazing

- Intake scales with what's left of the resources this diet eats on the tile: the full rate times
  (1 − grazed level), summed over them.
- A tile with no pool for the diet gives nothing.
- Leaving an eaten-out tile isn't a threshold. It falls out of the choice: grazing slowly here becomes
  more expensive than walking somewhere better.

### Species

- The carrion fold is replaced by two real species, each with its own body, diet and words in content
  data:
  - **Raven:** flies, leaves no prints or trail wear, scavenges.
  - **Fox:** walks, small omnivore and scavenger.
- The wolverine is dropped for now.
- "Carrion" becomes a kind of food source, not a behaviour. The scavenger rules that remain (perching
  while a predator holds a kill, raiding stashes) are rules in the animal mind.
- Flying is one property on the species. Track and trail recording skip flyers.

### The animal mind

- One mind for every animal, replacing the herd mind and its states:
  - **Fleeing:** each animal flees what it senses. A group flees together because its members sense
    the same thing and follow.
  - **Hunting together:** a wolf joins a hunt a packmate starts if its own food tier agrees.
  - **Pack cohesion:** the company term.
  - **Defending a kill:** each animal decides from its own boldness, with the number of packmates
    present (wiring the `defending` boldness input that exists but has no caller).
- Species behaviour still comes from role and content rows, never from switching on a species name.

### NPCs

- The last ticket moves NPC source-finding onto the shared need → sources → routes → choice pieces.
- NPCs keep:
  - the warmth filter;
  - the requirement tree for crafting needs;
  - committed goals with interrupts;
  - Memory.
- NPC and animal source-finding then exist in one copy.

### Save

- The save format version is bumped, and the fixture save is regenerated on purpose.
- The save gains: per-animal agents, each animal's mind (plan, path, sightings), familiarity, and the
  new body fields animals now use.
- The territory field is removed.

## Testing Decisions

- **What a good test is here:** it drives the world through its public seam and checks what a player
  or an observer could see. Where agents went, what they did, who died of what, prints on the ground,
  time and allocation. It never checks a mind's internal state or a planner's intermediate scores.
- **Seam 1, the world:** `World.New(seed)` or a hand-built world with `Agents.Spawn`, then
  `Sim.Advance(world, minutes)`, then read the world. Prior art: `HerdTests`, `KillChainTests`,
  `NpcTests`, `ThirtyPopulatedDays_StayCheap`, `Phase3GateTests`. Covers:
  - Behaviours:
    - a thirsty animal goes to the nearer of two water sources;
    - a herd leaves an eaten-out tile;
    - an animal on snow stays watered and loses calories;
    - an animal in a dry, snowless place dies of thirst;
    - a starving animal dies from its body, the weakest first;
    - wolves rest in the middle of the day and are active at dusk;
    - a sleep-deprived animal rests in its active hours;
    - ravens leave no prints, and foxes do;
    - caribou avoid a source a predator holds;
    - packs avoid each other;
    - NPCs drink from an open pond.
  - **The cohesion gate:** 60 days on several seeds, in Release, tagged Slow. It fails if:
    - more than 10% of open-group animals are alone on average;
    - any herd holds over 60% of its species' living animals once there are more than 20;
    - a wolf pack present on day 1 isn't still 2 or more familiar wolves on day 60, unless it was
      killed off;
    - allocation is over 3× today's herd baseline.

    The first planner ticket must pass it before anything else builds on individuals. If it can't,
    the fallback is herd agents with split and merge rules, and this spec is revisited.
  - **The stored-path benchmark:** the same seeds, re-planning every decision against stored paths
    with the five interrupts. Compare time, allocation and moves, and record the result in the
    ticket's work log.
  - **The cost budget:** the existing 30-day budget test is re-baselined on purpose, with the new
    number stated.
  - **NPCs:** today's `NpcTests` behaviour must still pass after the NPC swap.
  - **Determinism:** same seed, same world, and save-and-load mid-run.
- **Seam 2, forecasts:** each new forecast gets an agreement test in `ForecastTests`, checking that
  what it predicts matches what its system then does:
  - grazing payout with company and crowding;
  - water payout, including snow and its calorie cost;
  - rest payout;
  - route minutes and kcal from the Dijkstra against `Travel`;
  - Combat's kill chance by pack size.
- **No planner test hook.** The planner's choices show through seam 1 and its accuracy through
  seam 2. A direct choose-for-this-agent hook would be a seam with one adapter. Add one only if a
  decision can't be phrased through `Sim.Advance`.
- **Save:** `SaveTests` and the fixture save, with the version bump.
- **Tests that are rewritten, not deleted:** the current tests pin the herd mind's states, territory,
  forays, full-feed grazing, the wolves-only starvation and the carrion fold. They're in `HerdTests`,
  `HerdBudgetTests`, `KillChainTests` and `HerdGoldenTests`. Each one is rewritten as a behaviour
  test through seam 1, or dropped with a note in the work log saying what replaced it.
- Run per the repo's rule: the class in hand each cycle, the fast suite before each commit, the full
  suite before handback. Long sims in Release, with seeds split across processes.

## Out of Scope

- Bears denning for the winter (a denning season with lowered metabolism). Follow-up.
- Population regulated by births from body condition, with a sex on bodies. Capacity-based renewal
  stays for now. Follow-up, to be tried in a sim first.
- Wolverines, and mammoth matriarch families.
- Warmth for animals.
- People eating snow raw at a body-heat cost. Flagged against FIR-13, not decided here.
- Exposed food, caches, scent and scavenger raids on camp. That's the "Predators and items" ticket
  (#51), which this blocks.
- Showing herds, signs and causes on screen. That's "What the player reads" (#52).
- Steps 3b-6 of the emergent-world design, still on hold.

## Further Notes

- Measured on 2026-11-22 (seeds 17 and 23, 30 days, Release), splitting every herd into one-animal
  agents with today's mind:

  | World | Agents | Time | Allocated |
  |---|---|---|---|
  | No animals | 0 | 0.9 s | 1.5 GB |
  | Herds | 27-32 | 3.1-3.6 s | 4.0-4.4 GB |
  | Individuals | 95-134 | 6.4-6.5 s | 6.2-7.0 GB |

  The individual runs made 4× the moves, because nothing held them together. The animal side costs
  about 2-2.5× as much. That's about 0.15 ms per game minute and invisible in play. Per-animal saves
  added about 3 KB each. Full Memory per animal would have made this tens of MB, which is why animals
  don't have it.
- This spec blocks #51, #59, #61 and #62 on the map.
- Suggested ticket order (for `/to-tickets`), each a thin vertical slice:
  1. Shared body needs for animals (thirst, rest, body starvation), plus activity patterns and sleep
     hours.
  2. The shared route search for everyone (one Dijkstra, ground state, flyers).
  3. Sources and their forecasts, plus the planner choice for one stat (water) with individuals, plus
     the cohesion gate and the stored-path benchmark.
  4. Food and rest sources, grazing by what's left, company and crowding.
  5. Familiarity and packs, plus predators (hunting together, kill forecast, defending, avoidance).
  6. Raven and Fox replace the carrion fold.
  7. Territory and the old herd mind removed, renewal on map-wide capacity, save bump.
  8. NPCs onto the shared planner.
