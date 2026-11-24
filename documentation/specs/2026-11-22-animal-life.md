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
  together because their species' cohesion makes going alone costly. Big herds break up because a
  crowd eats a tile out. Wolf and hyena packs are animals that know each other, and a rival pack is
  strangers, so territories come out of packs avoiding each other.
- Animals know the lie of the land, the water and the grazing by heart. They know prey, predators and
  carcasses only by sensing them.
- Each species sleeps at its own hours (diurnal, crepuscular or nocturnal) and for its own length of
  time. A herd that was run all night will sleep in the day.
- Snow quenches thirst at a real calorie cost, so mammoths look for open water in winter and caribou
  hardly care. Rain and thaw ponds count as water.
- Ravens fly and leave no prints on the way, only where they land to feed. Foxes walk and leave
  prints as they go.
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
17. As a player, I want ravens to fly straight across lakes and rough ground and leave prints only
    where they land, so that bird prints around a carcass tell me something fed there recently.
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
22a. As a player, I want a caribou dying of thirst to walk past the wolves to drink, so that
     desperation shows and creates moments I remember.
22b. As a player, I want a bedding ground or a grazed pasture to be trampled, so that I can read where
     a herd spent its time, not only where it passed.
23. As a hunter, I want a pack's will to hunt big prey to come from its size, so that a pair of wolves
    leaves bison alone and a pack of eight doesn't.
24. As a hunter, I want hungry animals to range further and take more risks, so that late winter is
    more dangerous.
25. As a hunter, I want predators to find prey by sensing it, not by knowing where every animal is, so
    that hiding and staying downwind can matter. When they sense nothing, they roam.
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
35. As the designer, I want the planner split into priorities, sources, routes and choice, so that each
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
41. As the designer, I want herd size to come out of cohesion against crowding, so that there are no
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
- Rest is a real stat in the priorities. If its priority rises above everything else, the animal sleeps
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
  - Familiarity is seeded: a newborn inherits its mother's familiarity, and an arriving pack starts
    familiar with each other.
  - There is deliberately no persistent pack id. A feature that needs "that pack" kept over time,
    such as "the wolves that stalked you yesterday", would have to add one.
  - Familiarity is also the base for people's relationships later. What a person thinks of another
    (the old game's relationship memory) builds on top of it; it isn't a second system.
- Combat owns one **"who is on my side"** read: the same species on the tile for open-group species,
  the familiar animals for closed groups. Boldness from numbers, noticing, and which animals join a
  fight read it, replacing member counts and drawing members from one herd. Rival packs meeting on a
  tile are two sides.
- Open-group species (caribou, bison, megaloceros, mammoth) count any animal of their species on the
  tile as company. Closed-group species count only familiar animals. Unfamiliar animals of their own
  species are competitors and, for predators, a danger.

### Cohesion and crowding

- **Cohesion** reuses the species' existing `cohesion` field, already read by combat as morale. It
  becomes one number for how strongly an animal sticks with its group, read by combat and the planner.
- **Going alone costs more.** For a source with none of the animal's kin on its tile or the 8 tiles
  around it (standing there, or crossing onto one of them), the choice's total minutes are multiplied
  by (1 + cohesion). A caribou at cohesion 1 would rather take twice as long with the herd. A bear at
  0 doesn't care. Closed-group species count only familiar animals as kin. Counting the tiles around
  lets a herd spread over several tiles while it grazes and stay one herd.
- **Crowding: a tile capacity, counted by intake** (amended 2026-11-24, #83 and #132; it replaces
  "yield ÷ grazers", which capped herds at about 2.5).
  - **Load.** Each grazer standing on the tile (or stepping onto it) that eats a resource growing
    there adds its intake: its species' grazing rate × (its weight ÷ its species' weight)^0.75. A
    mammoth loads a tile like about 7 caribou. Animals heading there later don't count; a whole herd
    re-deciding in the same minute and moving together is intended: that's following the herd.
  - **Capacity.** 7 caribou's intake (7 × 13 = 91 kcal/min) on a tile of average forage, scaled by
    the forage left on it now: × the tile's forage density ÷ the valley's mean ungrazed density × the
    mean (1 − grazed level) over the resources the grazer eats there. A fresh rich meadow holds more
    than 7, a half-eaten one fewer.
  - **Payout.** The full rate × (1 − grazed level) × min(1, capacity ÷ load). Below capacity company
    costs nothing; past it everyone's share falls.
  - If animals turn out to leave tiles too eagerly, the fallback is capacity by the tile's richness,
    without the grazed level.
- **A little noise in choices.** Each candidate's total in the choice is multiplied by 1 ± up to 5%,
  drawn from the world's seeded streams, so animals with the same view don't all choose alike in the
  same minute, and runs stay deterministic through save and load.
- **What splits a herd.** Past capacity, crowding cuts each animal's share until the next tile beats
  staying; as a tile is eaten its capacity shrinks. Herd size comes from kin reach, the land and
  predators, not from a split or merge rule. The cohesion gate measures whether this happens.
- **Vigilance stays in detection and combat.** The planner doesn't score it. It is one function owned
  by the detection code (today's prey-noticing curve), read by the hunt and by combat alike.
- Predators: the choice of hunting ground counts pack size through the kill-chance forecast below. A
  pack shares a kill among those present.
- When the kin an agent is with leave its tile, that's an interrupt: it re-weighs following against
  staying. Following reuses their destination.

### What animals know

- Per the decision record "Animals read the slow world true and sense what moves", animals have no
  Memory.
- **Read true, anywhere on the map:** anything that isn't an agent and changes over days or longer:
  terrain, water and whether it's frozen, ground (snow, ponds), how grazed a tile is, dens, trail
  wear. This is a pragmatic line, not a principled one.
- **Sensed only:** agents, and anything agents leave that matters within hours: carcasses, prints,
  fires, stashes. Sensing uses the same sight system as people, scaled by the species' perception.
- **What sensing keeps:** a sightings list. It keeps an agent for a few hours. It keeps a carcass
  until the animal sees it gone, so a pack that leaves its kill to drink comes back to it.
- **A knowledge view is a parameter.** Every source listing and the planner take a knowledge view:
  Memory for people, the true world plus sensing for animals. No system grows two variants of its
  source listing. The test that keeps NPCs to the public interface extends to the planner, so no NPC
  can be handed the true world.
- People keep Memory. Memory owns regrouping per-animal sightings into "a herd of N caribou here" for
  the player and NPCs.
- The visible cost: every caribou knows where the best pasture is. Distance and crowding are what
  keep them from all heading there.
- The fixed territory is removed. A den is a rest source for the species that has one. Carrying
  capacity in renewal becomes a per-species number across the map.

### The planner: priorities → sources → routes → choice

Four separate pieces, shared by NPCs and animals.

1. **Priorities.** Warmth > water > rest > food within a priority, rest before food as amended.
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
   - **Reading the ground is decided by a measurement.** Today herds deliberately skip snow and mud,
     because reading it catches up a tile's lazy ground history and stores ground state in the save.
     The route-search ticket measures that cost and the save growth for animals. One terrain formula
     for everyone stays only if it fits the budget. Otherwise animals read the ground's cheap
     weather-only estimate, as a recorded exception. Ponds and snow as water sources follow the same
     answer.
   - Flyers pay a flat cost per tile, ignore terrain and water, and can't cross the mountain wall.
   - The current planner forecast that refuses herds becomes the shared one.
4. **Choice.**
   - For each candidate: total minutes = travel + amount ÷ rate + kcal costs converted to minutes at
     the agent's current best food rate.
   - Take the minimum. Move only if it beats staying by about 25% (one named constant).
   - Predators are hard refusals: never route through, or pick a source on, a tile where a threat is
     sensed or freshly remembered.
   - **Critical drops the threat refusal.** A Critical stat weighs danger only then: a caribou dying
     of thirst walks past the wolves to drink. Below Critical, no plan means stay put, and flee if
     threatened.
   - **Predators with nothing to hunt explore.** With no prey sensed and none in their sightings,
     a predator picks a random tile at the edge of its sight range (seeded, so deterministic) and
     goes there. Revisit if this starves them.
   - Ties between sources and routes break on tile id, so results don't depend on dictionary order.
   - NPCs keep their warmth filter on top (refuse a trip whose forecast leaves them too cold).
   - The output is a **plan**: a destination, its path, and the expected work there.

- **Stored paths.** A plan's path is stored on the agent and saved. It is re-planned only when:
  - a threat is sensed on or near the path;
  - the next step is refused;
  - a stat rises to a higher priority than the plan's;
  - the source turns out to be gone on arrival;
  - the animals it's travelling with leave.

  The first planner ticket benchmarks this against re-planning every decision (Testing Decisions), and
  stored paths stay only if they clearly pay.
- The planner uses only the Sim's public interface (verbs, refusals, forecasts, Memory or the animal
  knowledge read), per the decision record on NPC AI.

### Grazing

- Intake scales with what's left of the resources this diet eats on the tile: the full rate times
  the mean (1 − grazed level) over them, so a fresh tile pays the full rate (#83), times the crowding
  factor above.
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
- Flying is one property on the species: a flyer's crossing cost is flat per tile.
- **Prints come from crossings and from activity on a tile.** Time spent active on a tile (feeding,
  perching, raiding, grazing, bedding down) adds traffic there, for every agent. Flyers leave no
  crossing prints or trail wear, only prints where they land and act.
- A new print shape, **Bird**, joins Human, Paw and Hoof. Depth already scales with body mass, so bird
  prints are shallow and fade within hours: a sign of something recent.

### The animal mind

- One mind for every animal, replacing the herd mind and its states:
  - **Fleeing:** each animal flees what it senses. A group flees together because its members sense
    the same thing and follow.
  - **Hunting together:** a wolf joins a hunt a packmate starts if its own food priority agrees.
  - **Pack cohesion:** the company term.
  - **Defending a kill:** each animal decides from its own boldness, with the number of packmates
    present (wiring the `defending` boldness input that exists but has no caller).
- Species behaviour still comes from role and content rows, never from switching on a species name.

### NPCs

- The last ticket moves NPC source-finding onto the shared priorities → sources → routes → choice pieces.
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
    - a Critical-thirsty animal drinks past a predator, and a merely thirsty one doesn't;
    - a pack with nothing sensed roams;
    - a raven that crosses leaves no prints, and one that feeds leaves Bird prints;
    - a herd grazing a tile all day leaves more traffic there than one passing through;
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
  - **The cohesion gate:** 60 days on several seeds, in Release, tagged Slow. Every metric is per
    species. It fails if:
    - more than 10% of an open-group species' animals are alone on average;
    - any herd holds over 60% of its species' living animals once there are more than 20;
    - herds never split or join (a minimum rate of splits and joins, so a frozen lockstep fails);
    - animals change tile far more often than they need to (tile changes per animal per day, to catch
      oscillation);
    - an outsized share of a species stands on the single busiest water tile;
    - predator kills per week fall outside a band;
    - starvation and thirst deaths per species fall outside a band;
    - the number of wolf packs isn't stable, or rival packs merge, or a pack present on day 1 isn't
      still 2 or more familiar wolves on day 60 unless it was killed off;
    - allocation is over 3× today's herd baseline, measured with the real planner (route searches,
      source listing, ground reads and familiarity included).

    The bands are set in the ticket from a first run and stated in its work log.

    For the gate a herd is the animals of a species on a connected group of occupied tiles (8
    neighbours), not one tile: "alone" is a herd of one, and the biggest herd is the largest such
    group. Combat sides and everything else that reads the derived herd stay per tile.

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
  - Combat's kill chance by pack size;
  - vigilance: what the hunt and combat use is the one detection function.
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
  about 2-2.5× as much, about 0.15 ms per game minute. That measured the number of agents with
  today's cheap mind, not the planner: route searches, source listing, ground reads and familiarity
  are not in it, which is why the gate re-measures with the real planner. The settling pass (one
  simulation for world history) multiplies whatever this costs. Per-animal saves added about 3 KB
  each. Full Memory per animal would have made this tens of MB, which is why animals don't have it.
- If the cohesion gate fails, the fallback is herd agents with split and merge rules. The body needs,
  sources, route search, planner and knowledge view all survive it; only the grouping changes.
- Not handled by this design and worth knowing: with map-wide knowledge and no territory, nothing
  makes an animal leave the map. Migration would need an off-map source.
- This spec blocks #51, #59, #61 and #62 on the map.
- Suggested ticket order (for `/to-tickets`), each a thin vertical slice:
  1. Shared body needs for animals (thirst, rest, body starvation), plus activity patterns and sleep
     hours.
  2. The shared route search for everyone (one Dijkstra, ground state measured, flyers).
  3. Sources and their forecasts, plus the planner choice for one stat (water) with individuals, plus
     the cohesion gate and the stored-path benchmark.
  4. Food and rest sources, grazing by what's left, company and crowding.
  5. Familiarity and packs, plus predators (hunting together, kill forecast, defending, avoidance).
  6. Raven and Fox replace the carrion fold; prints from activity and the Bird shape.
  7. Territory and the old herd mind removed, renewal on map-wide capacity, save bump.
  8. NPCs onto the shared planner.
