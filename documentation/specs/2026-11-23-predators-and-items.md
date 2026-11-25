# Predators and items: piles, caches, scent and raids

Spec for issue #51, part of the map #49. Written 2026-11-23 from the grilling with Jack. A dated record of
what was planned; lasting terms are in `GLOSSARY.md` (pile, cache, ability, exposed, scent, diet, dirty),
and it builds on the animal-life spec (`documentation/specs/2026-11-22-animal-life.md`) and the decision
records "Animals read the slow world true and sense what moves", "No special cases", "Conditions
derived, never stored", "No effect needs a treatment the game doesn't have" and "The stomach holds a
fixed amount".

## Problem Statement

In the rebuild, food you leave is safe from everything but one abstract raven flock. Any tile's pile is
storage, unlimited and unprotected, yet wolves and bears never touch it. The flock raids it on a dice
roll about once every eight hours and never while a person stands there. Nothing in the world smells
anything: a hungry pack senses any person within 12 tiles whatever the wind, and the carcass scent the
Sim computes is read by nothing. Carrying meat is a flat boldness bonus that ignores cooked meat and
the wind alike.

The old game had real stakes here (predators ate meat on the ground and in unprotected caches), but its
rules were special cases: an Ice Crevasse that was predator-safe, a Rock Cleft and a 1000 kg camp cache
that protected nothing, weatherproof and preserves flags that nothing read, and no scent for food at all.
A player could not learn a rule from it, only the exceptions.

## Solution

Food left anywhere is part of the world's food. An animal whose diet takes it will come for it if it
smells it or happens onto its tile, and will eat until full unless a cache keeps it out. The player
protects food by building caches. Each kind of cache keeps out every animal except those with an
ability it lists (flies, climbs, digs, strong), and the kinds trade off against each other:

- a hung bag beats wolves but spreads its scent on the wind;
- a pit beats everything but bears and hides the smell, and keeps meat frozen;
- food buried in snow is free but diggers find it.

Scent is one rule for every source: food, carcasses, blood, bodies. It carries further downwind than
upwind, so the wind decides who smells your camp, and whether caribou smell you coming. People give off a
little scent of their own, more when dirty or bloody. Washing removes both at the cost of getting wet in
the cold.

A raid is something you can read and prevent: prints at the cache, a cause on what was taken, a torn-open
cache. A bear raiding a sleeping camp has a cause the player could have seen: meat in a pile, the wind
toward the bear's country, no fire.

## User Stories

### Food left in the world

1. As a player, I want meat left in a pile to be eaten by wolves, bears, foxes and ravens, so that leaving
   food lying around is a real risk.
2. As a player, I want what an animal eats to follow from its diet (carnivore, omnivore, herbivore), so
   that a bear raids my berries and dried fish while a wolf ignores the berries.
3. As a player, I want herbivores to leave my piles alone, so that a herd grazing past my camp is no
   threat to my food.
4. As a player, I want a carcass to follow the same rule as a pile, so that I learn one rule, not two.
5. As a player, I want food lying in a pile to have no limit and no protection, so that a pile stays the
   free, obvious default and a cache is a deliberate choice.
6. As a player, I want "pile" to be the word everywhere for what lies on a tile, so that the UI says
   what it is.
7. As a player, I want an animal to eat until its stomach is full and leave the rest, so that a raven
   takes a little and a bear takes a lot.
8. As a player, I want a raid to come from an animal's choice to feed, not a dice roll, so that I can
   work out why it happened.

### Caches

9. As a player, I want to build a tree-hung cache, so that wolves, hyenas and foxes can't reach my
   meat.
10. As a player, I want a tree-hung cache to need a tree on the tile and cordage and to hold little, so
    that it's a choice of place as well as of effort.
11. As a player, I want ravens to reach a tree-hung cache, so that hanging food isn't a full answer.
12. As a player, I want to dig a pit under stones, so that only a bear can get at what's inside.
13. As a player, I want digging a pit to be slow, and much slower in frozen ground, so that the best
    protection costs real time.
14. As a player, I want food buried in snow to be protected from animals that can't dig, so that the
    snow is a cache I already have.
15. As a player, I want bears and foxes to dig up buried food, so that burying it isn't safe from
    everything.
16. As a player, I want bears to break into a pit but not to climb to a hung bag, so that I pick my cache
    by which predators live near me.
17. As a player, I want the Ice Crevasse and the Rock Cleft to be caches nothing gets into, so that
    finding one matters.
18. As a player, I want to take from my own caches with no extra time, so that protecting food doesn't
    tax every meal.
19. As a player, I want an animal to take time to break into a cache, so that I have a chance to notice
    and drive it off.
20. As a player, I want a cache to have a capacity, so that I can't protect a whole winter's meat in one
    hung bag.
21. As a player, I want a cache an animal broke into to stay torn open until I repair it, so that I find
    out what happened and must fix it.
22. As a player, I want a pit to keep meat colder and more covered than a bag hung in the sun, so that
    where I store food changes how long it keeps.
23. As a player, I want the shelter of the tile to add to the cache's own cover, so that a pit in a
    sheltered wood keeps better than one on a bare ridge.

### Scent

24. As a player, I want raw meat, cooked meat and fish to smell strongly, dried meat less, and berries,
    nuts and roots hardly at all, so that what I store decides what comes for it.
25. As a player, I want a pit or buried food to hold most of its scent in, so that hiding food hides its
    smell.
26. As a player, I want a hung bag to keep all of its scent, so that a hung bag draws animals in even
    though it keeps most out.
27. As a player, I want scent to carry further downwind than upwind and the same all round in calm air,
    so that the wind I can see on screen decides who smells my camp.
28. As a player, I want animals to find piles and caches only by smelling them or by walking onto their
    tile, so that a pile of roots isn't spotted from across the valley but can still be stumbled on.
29. As a player, I want carcasses to be seen from afar as well as smelled, so that ravens gather over a
    kill the way they do.
30. As a player, I want carrying meat or fish, cooked or raw, to make me smell, so that walking home with
    a kill draws predators along the wind.
31. As a player, I want bleeding and being Bloody to smell strongly, so that a wound left open in wolf
    country is dangerous.
32. As a player, I want a clean, healthy sleeper to give off almost nothing, so that a death in my sleep
    needs a cause I could have read.
33. As a player, I want each species' sense of smell to scale how far it smells, so that a bear finds my
    cache from further than a raven does.

### Dirt and washing

34. As a player, I want to grow dirty over days, faster with hard work and butchering, so that my own
    smell is something I manage.
35. As a player, I want being dirty to do nothing but add to my scent, so that it's a stealth cost, not
    a health penalty.
36. As a player, I want to wash with water or snow to remove Dirty and Bloody together, so that one act
    makes me hard to smell.
37. As a player, I want washing to leave me wet, so that washing in deep cold is a choice with a cost.

### Hunting and being hunted

38. As a hunter, I want caribou and other prey to smell me when I come from upwind, so that approaching
    with the wind in my face matters.
39. As a hunter, I want prey to smell wolves by the same rule, so that the predators I watch face the
    same wind I do.
40. As a player, I want the flat "carrying meat" boldness bonus gone, so that meat makes me easier to find
    rather than mysteriously braver to attack.

### Raids with people present

41. As a player, I want an animal to come for food on my tile only if its boldness beats the threat it
    judges me to be, so that a starving bear may come and a raven won't while I'm awake beside it.
42. As a player, I want my fire to keep animals off my food the way it keeps them off me, so that a
    strong fire guards my camp.
43. As a player, I want sleeping to make me less of a threat to a raider, so that a camp with meat in a
    pile, downwind of bear country and with no fire, is a real danger at night.

### Reading what happened

44. As a player, I want a raid to leave prints at the pile or cache, so that I can tell what came.
45. As a player, I want food that was eaten to carry its cause, such as "taken by a bear", so that the
    game can tell me what I lost and to what.
46. As a player, I want a torn-open cache to show as a fact on the cache, so that I see it on return.

### Animals choosing food

47. As an animal, I want a pile or cache I know of to be a food source in the shared planner, so that I
    weigh it against grazing, carcasses and prey by trip and payout.
48. As an animal, I want the break-in time to count as part of the work at a source, so that I go to the
    easy food first.
49. As an animal, I want no hunger threshold before I consider food I've found, so that feeding comes
    from my priorities like every other need.
50. As an animal, I want a pile I've smelled kept on my sightings list until I see it gone, as a carcass
    is, so that I come back to it.

### The designer

51. As the designer, I want who gets into a cache to be one check shared by animals and people, so that
    no system grows two variants.
52. As the designer, I want food to leave a pile or cache through one take operation that records its
    cause, so that every loss is accounted for in one place.
53. As the designer, I want people to get into caches by a hands ability, not by an exception for
    people, so that the rule has no special case.
54. As the designer, I want the old "weatherproof" and "preserves" flags replaced by each cache's cover
    and exposure, so that preservation falls out of the effects that already exist.
55. As the designer, I want the camp cache dropped and flagged as a simplification, so that the porting
    rules' "flag, never silently drop" holds.
56. As the designer, I want the Rock Cleft's protection flagged as a change from the old game, which
    gave it none.
57. As the designer, I want the raven raid roll and the "no raid while a person is present" rule removed,
    so that raids come from the same mind and boldness as everything else.
58. As the designer, I want Hare's diet fixed to Herbivore and Fox's to Omnivore, so that the data says
    what the animals are.

## Implementation Decisions

### Food on a tile

- **One rule.** Any food on a tile is food for every animal whose diet takes that resource, unless a
  cache on that tile keeps that animal out. "Exposed" is derived for a given animal each time, never
  stored. Carcasses keep their own body, clock and butchering, and follow the same rule.
- **The pile** is what the rebuild now calls a stash: one unlimited, unprotected collection of items per
  tile, with a cause on what leaves it. It is renamed "pile" in the glossary, the UI words and the code.
- **Diet** has three values: Carnivore, Omnivore, Herbivore. Each maps to the resources it eats from
  piles, caches and carcasses, as content data; Herbivore's list is empty, so herbivores leave piles
  and caches alone. Browser and Grazer go as diets. What a herbivore grazes
  from the ground becomes a per-species list in the species data (caribou, megaloceros and mammoth keep
  what browsers eat today, and bison what grazers eat), so grazing and carrying capacity behave as
  before.
- Data fixes: Hare becomes Herbivore, Fox and Raven Omnivore.

### Caches

- **A cache is a location feature.** It holds items up to a capacity and lists which abilities get in.
  It has a break-in time for animals, a scent multiplier, and the cover it sets for what it holds. It
  offers its verbs through Interact, like any location feature.
- **Abilities** are a per-species set in the species data: flies, climbs, digs, strong, hands. They are
  separate from a person's tool capabilities. Assignments: Raven flies; Bear digs and is strong but
  doesn't climb; CaveBear is strong; Fox digs; Human has hands. Wolves, hyenas and sabre-tooth have none
  of these, so they get only into piles.
- **The kinds:**

  | Cache | Who gets in | Built how | Trade-off |
  |---|---|---|---|
  | Pile | everyone | nothing | no limit; full scent |
  | Buried in snow | digs, hands | burying in snow | free; only in snow; heavily damped scent |
  | Tree-hung | flies, climbs, hands | cordage, a tile with trees | small; full scent; out in sun and wind |
  | Pit under stones | strong, hands | stones and digging; much slower in frozen ground | strongly damped scent; covered and cold |
  | Ice Crevasse (found) | hands | at its location | frozen through |
  | Rock Cleft (found) | hands | at Sheltered Valley | sheltered |

- Food buried under snow is a cache by this rule, so the existing Buried gate becomes the same access
  check, read through abilities rather than a person's Digging capability.
- Recipe inputs, build times and capacities for the built kinds follow "Recipes that make sense" (#53);
  this spec fixes only the kinds and who gets in. The stone cairn was considered and cut: it offered
  nothing the pit doesn't.
- **Taking out costs a person nothing extra.** An animal pays the cache's break-in time once, before it
  starts eating. Breaking in leaves the cache torn open: a stored fact, since it is damage, not a
  condition read off effects. A torn-open cache lets in everyone until a person repairs it.
- **Cover is additive with the tile.** The cache's cover and exposure combine with the tile's own
  shelter and weather when the item exposure is worked out. Spoiling and freezing then come from the
  existing item effects. There is no weatherproof or preserves flag.
- The old camp cache (1000 kg, protecting nothing) is dropped, flagged as a simplification: the pile is
  storage at camp.

### One access check and one take

- **Access:** one check answers "can this agent get into this pile or cache?" from the cache's list
  and the agent's abilities. It is used by the animal mind, the planner's source listing and the
  person's verbs.
- **Take:** one Sim operation removes food from a pile or cache for any agent and records the cause
  (who took it, and for an animal its species). The animal mind decides to feed; the take operation
  does the taking. Whether animals act through activities in general is left to the NPC-mind review the
  roadmap plans after the shared planner.

### Scent

- **Strengths.** Every scent source has a strength:
  - each food resource, in content data: raw and cooked meat and fish high, dried meat medium, berries
    low, nuts and roots near zero;
  - a carcass, from the carcass scent the Sim already computes;
  - bleeding and Bloody, among the strongest;
  - each species' base scent, low for people;
  - Dirty.

  A pile's or cache's strength is the strength of what it holds, times the cache's scent multiplier.
  A person's is their base plus Dirty, Bloody, bleeding and the food they carry.
- **Reach.** A source is smelled by an animal when the distance is within a reach set by its strength,
  the animal's sense of smell (per species, in the species data, like perception for sight), and a wind
  factor. The wind factor comes from the angle between the valley's one wind heading and the line from
  the source to the animal:
  - about twice as far straight downwind;
  - about half as far straight upwind;
  - the same in every direction in calm air.
- **Scent sits beside sight in the one sensing function**, so the hunt, combat engagement, source listing
  and the sightings list all read one answer.
- **What each sense finds:**
  - piles and caches: only by scent, or when the animal is on their tile;
  - carcasses: by scent, and also by sight from afar;
  - agents: by sight and by scent.

  This changes the animal-life spec, which had stashes sensed by sight. A sensed pile or cache stays on
  the sightings list until the animal sees it gone, as a carcass does.
- **Prey smell hunters and predators** by the same rule, so approaching from upwind can startle a herd.
- **CMB-16:** the flat +0.1 boldness for carrying raw meat or fish is removed. Carried food raises scent
  instead, cooked meat and fish included.

### Dirty and washing

- **Dirty** is a new body effect, in effects data. It builds slowly with time, faster with sweat (fed
  from the existing sweat flow) and with butchering and carcass work. It only adds scent: no capacity
  loss, no discomfort. It does not fade on its own, so by the decision "No effect needs a treatment the
  game doesn't have" it ships with washing or after it.
- **Wash** is the FIR-12 action, widened. Water or snow plus a few minutes removes Dirty and Bloody
  together and wets the body by an amount scaled to how much was washed. Bloody keeps drying on its own
  as now.

### Feeding as a planner source

- Piles and caches the animal knows of (sensed, or on its tile) are food sources in the shared planner.
  Each gets a forecast from the food its diet takes there, with the break-in time counted as part of the
  work, so easier sources rank first.
- There is no raid roll and no hunger threshold. An animal eats until its stomach is full (decision "The
  stomach holds a fixed amount"), and the rest stays. Nothing is carried off.
- **People on the tile:** whether an animal goes for food on a tile where people are is decided as
  engagement is: its boldness against the threat it judges them to be, with fire deterrence applied.
  The hard refusal while a living person is on the tile is removed. A sleeping person is judged a lower
  threat than an alert one, through the threat assessment that already exists.
- The raven flock's attractor, raid chance and raid rate go with the carrion fold (animal-life spec);
  ravens and foxes feed through this source like every other species.

### What a raid leaves

- Feeding at a pile or cache is activity on the tile, so it adds prints by the animal-life spec's rule.
  For ravens, these are the only prints.
- Food taken carries its cause through the take operation.
- A cache that was broken into is torn open (above).
- How these show on screen, including a sign for a raided cache, belongs to "What the player reads" (#52).

### Save

- New saved state: caches (kind, contents, torn open), Dirty on bodies, and pile-or-cache entries on
  animals' sightings lists. The rename from stash to pile and the removal of raven raid state go into
  the same save format change. Old saves are refused, not migrated
  (decision "No save migrations until the rebuild is finished", 2026-11-24).

### Blocking

- This builds on the animal-life implementation (#78): the shared planner and its food sources, Raven
  and Fox as species, and the sightings list. It is planned to land after those tickets.
- Washing (FIR-12) is part of this spec, since Dirty can't ship without it.

## Testing Decisions

- **What a good test is here:** it drives the world through its public seam and checks what a player or
  an observer could see: which food is gone and with what cause, whether a cache is torn open, prints on
  the tile, where animals went, who died of what. It never checks a mind's internal state or a planner's
  intermediate scores.
- **Seam 1, the world.** A hand-built world with `Agents.Spawn`, piles, caches and a fixed weather
  sample, then `Sim.Advance`, then read the world. Prior art: `KillChainTests`, `HerdTests`,
  `PredatorEngagementTests`. Covers:
  - a wolf eats meat from a pile and ignores berries; a bear eats both; caribou eat neither;
  - a wolf can't reach a tree-hung cache, a raven can;
  - a bear digs up meat buried in snow, a wolf doesn't;
  - a bear breaks into a pit, after the break-in time, and leaves it torn open;
  - nothing gets into the Ice Crevasse;
  - a wolf downwind of a meat pile comes to it, and one at the same distance upwind doesn't;
  - an animal doesn't head for a pile of roots it can't smell, but eats it on stepping onto the tile;
  - a raven finds a carcass by sight from beyond its smell;
  - an animal eats to a full stomach and leaves the rest;
  - food taken carries the right cause;
  - feeding at a pile leaves prints there;
  - a raven doesn't feed beside an awake person; a starving bear does come for a pile beside a
    sleeper with no fire, and doesn't when a strong fire burns;
  - caribou flee a person approaching from upwind sooner than one approaching from downwind;
  - an animal with a pile and a pit equally far takes the pile first;
  - a person takes from a cache with no break-in time;
  - Dirty builds with work and washing clears it and Bloody, leaving the person wet;
  - a pit keeps meat frozen longer than a hung bag in the sun on the same day.
- **Seam 2, forecasts.** In `ForecastTests`: the forecast of a pile or cache as a food source (payout
  for the animal's diet, break-in time included) agrees with what the feeding then does.
- **Seam 3, sensing.** Scent sits beside sight in the one sensing function, tested directly as numbers
  in the manner of `SightTests`: reach downwind against upwind, calm air, strength, sense of smell and
  a cache's scent multiplier. It is used only where the world seam can't separate cases cheaply.
- **Seam 4, verbs.** Washing and taking from a pile or cache, through `Verbs` and `Sim.Step`, as in
  `VerbsTests` and `WorkTests`, including that the take is refused for anything a person can't carry.
- **Content.** `ContentStrictTests` covers the new species data (abilities, sense of smell, base scent,
  grazing list) and food scent strengths, so a missing value fails at load.
- **Determinism and save:** same seed, same world; save and load mid-raid, with a torn-open cache and a
  pile on a sightings list.
- Fast tests only for these. The existing populated budget tests are re-baselined if scent makes sensing
  measurably dearer, with the number stated in the work log.

## Out of Scope

- Hyenas shadowing packs and feeding predators (CMB-15).
- NPCs taking from other people's piles or caches, and any ownership of food.
- Animals acting through activities in general (left to the NPC-mind review after the shared planner).
- Recipe inputs and build times for the built caches (#53).
- How raids, torn caches and scent show on screen (#52).
- Herbivore grazing lists and balance, including resources only animals graze: a follow-up issue under
  the Animal life map (#78).
- Scent trails that animals follow along a route; scent here is from where a source is now.
- Spoilage beyond what the existing item effects already do.

## Further Notes

- **Flagged changes from the old game**, per the porting rules:
  - the camp cache is dropped (the pile is storage);
  - the Rock Cleft now protects, where it didn't;
  - natural caches lose the unused weatherproof and preserves flags;
  - food finding by scent is new, since the old game had predators find food only on their own and
    adjacent tiles when hungry;
  - omnivores now raid plant food, where the old game counted only meat and fish;
  - cooked meat counts again (CMB-16), through scent, not a bonus;
  - ravens and foxes as real raiders are new to the port (the old game's ravens were text only).
- Audit items resolved: ITM-14 (camp storage and caches) and CMB-16 (cooked meat).
- **Numbers to set in the tickets:** capacities, break-in times, scent strengths, scent multipliers,
  each species' sense of smell and base scent, the wind factors, Dirty's build-up rates and the wash's
  water and wetting. Each ticket states where its numbers came from in its work log.
