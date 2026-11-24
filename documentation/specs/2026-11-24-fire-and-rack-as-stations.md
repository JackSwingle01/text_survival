# The fire and the curing rack as stations

Spec for issue #54, part of the map #49. Written 2026-11-24 from the grilling with Jack. A dated record of
what was planned; lasting terms are in `GLOSSARY.md` (transformation, station, recipe), and the lasting
decision is the record "Transformations are recipes worked by a station, at a rate set by exposure". It
builds on the specs "Recipes that make sense" (`documentation/specs/2026-11-24-recipes-that-make-sense.md`)
and "Predators and items" (`documentation/specs/2026-11-23-predators-and-items.md`), and on the decision
records "One Make activity for crafting and building", "Resources are discrete or continuous", "Effects
apply to bodies and things, through one exposure calculation", "No special cases" and "The UI shows what
the game knows". Audit item: ITM-21.

## Problem Statement

In the rebuild, a fire does nothing unless I stand at it. Cooking is 15 minutes of my own time for every
kilo, melting snow is 10 minutes a litre, and nothing happens at camp while I am away, so coming back to
camp pays nothing. Dried meat, dried fish and dried berries exist as foods but nothing makes them. A hide
is cured by four hours of my own work beside a fire, one hide at a time, where the old game had a curing
rack that worked for two days on its own.

The old rack had its own problems. It was a list of timers that ignored the weather and the fire beside
it: meat dried the same in a blizzard as on a still day. It was a second way of making things beside
crafting, and food on it was safe from every animal.

## Solution

A fire and a curing rack do their own work. I put meat at the fire or a hide on the rack, which takes a
moment, and walk away. When I come back the receipt tells me what finished and when, the tile shows a
mark, and the cooked meat is waiting at the fire for me to take.

How fast the work goes depends on the place, by one rule. A hot fire cooks at the time the menu shows; a
weak one is slower, embers slower still, and a dead fire stops without ruining anything. A rack dries at
its stated time on a dry, still day, up to twice as fast with a fire burning beside it, and not at all
while rain or snow falls on it uncovered. So keeping a fire going by the rack is a way to spend fuel for
time, and building the rack under cover is a way to dry through a storm.

A fire has fixed room, set by its pit: an open fire takes two things at once, a mound pit three, a stone
ring four. A rack takes four. Nothing is collected for me, and what sits at a fire or on a rack is
exposed like anything in a pile: a wolf that smells my drying meat can come and eat it.

Cooking and melting snow by hand go away. Drying a hide by hand at a fire goes away too: a rack is the
only way to cure a hide or dry food.

## User Stories

### Setting something going

1. As a player, I want to put raw meat at a burning fire and walk away, so that cooking does not cost me
   fifteen minutes of standing still for each piece.
2. As a player, I want to set snow melting at the fire and leave, so that I come back to water.
3. As a player, I want to put raw fat at the fire to render into tallow, so that rendering works like
   every other thing a fire does on its own.
4. As a player, I want putting something at a fire or on a rack to take about a minute, so that setting
   up is quick but not free.
5. As a player, I want the fire's and the rack's menus to list what I am carrying that they can work, so
   that I do not have to remember which things go where.
6. As a player, I want to be refused, with the reason, when the fire or rack has no room, so that I know
   to take something off or build another.
7. As a player, I want to put food at a fire that has gone out, so that I can lay it ready before
   lighting, and I want the menu to say "the fire is out" so that I know nothing is happening.
8. As a player, I want teas and fire-hardening to stay things I do with my hands at the fire, so that
   work needing attention still takes my attention.

### The fire

9. As a player, I want an open fire to take two things at once, a mound pit three and a stone ring four,
   so that building a better pit pays off in what I can cook.
10. As a player, I want a fire's room never to change while things are on it, so that nothing I set going
    is pushed off or paused because the fire burned down a little.
11. As a player, I want everything at a fire to go at the same rate, set by how hot the fire is, so that I
    have one thing to read, the fire.
12. As a player, I want a recipe's stated time to be its time at a good fire, so that the number on the
    menu is what I get when I do it properly.
13. As a player, I want a weak fire to cook slower and embers slower still, so that banking the fire
    overnight still gets something done.
14. As a player, I want a fire that has gone cold to stop the work and lose nothing, so that a dead fire
    costs me time and not food.
15. As a player, I want the work to pick up where it stopped when I relight the fire, so that relighting
    is all I have to do.
16. As a player, I want nothing ever to burn or overcook, so that a two-hour forage is never punished.
17. As a player, I want melted water to collect at the fire up to about four litres and then stop, so that
    a fire left for a day does not make endless water.
18. As a player, I want melting snow to take one of the fire's places, so that water competes with food
    for the fire.
19. As a player, I want cooking to take the same time for a big cut as a small one, so that I count
    pieces and not kilos.

### The curing rack

20. As a player, I want to build a curing rack from four branches, four sticks and a rope, so that curing
    and drying are an investment I make at a camp.
21. As a player, I want a rack to hold four things, so that I choose what is worth the space.
22. As a player, I want one place on a rack to take one piece of meat, one fish, one hide, or up to half a
    kilo of berries, so that I know what fits.
23. As a player, I want to build several racks on one tile, so that after a big kill my limit is the wood
    and rope I can spare.
24. As a player, I want a scraped hide to cure on the rack in about two days with no work from me, so that
    I can hunt and forage while it cures.
25. As a player, I want mammoth hide to cure on the rack like any hide, so that I learn one rule for
    hides.
26. As a player, I want meat to dry in about a day, fish in about sixteen hours and berries in about
    twelve, so that I can turn a kill into food that travels.
27. As a player, I want dried food to weigh much less than what went on, so that drying is how I make
    food light enough to carry far.
28. As a player, I want the rack to be the only way to cure a hide or dry food, so that there is no
    faster hand recipe that makes the rack pointless.
29. As a player, I want a rack never to wear out or be broken by a raid, so that it is a thing I build
    once per camp.

### Weather, heat and the rack

30. As a player, I want a rack to dry at its stated time on a dry, still day, so that the menu's number
    means something.
31. As a player, I want a rack beside a burning fire to dry faster, up to twice as fast, so that I can
    spend fuel to get a cured hide in a day.
32. As a player, I want wind to speed drying a little, so that an open, breezy spot is a slightly better
    place for a rack.
33. As a player, I want rain or snow falling on an uncovered rack to stop the drying, so that weather
    matters to where I build it.
34. As a player, I want a rack under cover to keep drying through a storm, so that a shelter protects my
    work as it protects me.
35. As a player, I want cold alone not to slow the rack, so that the rack still works through an Ice Age
    winter.
36. As a player, I want a stopped rack to lose nothing and carry on when the weather clears, so that a
    storm delays my hide and does not ruin it.

### Knowing how it is going

37. As a player, I want the fire's and rack's menus to show when each thing is expected to finish, so
    that I can plan a trip around it.
38. As a player, I want that time to be an expectation from the fire's fuel and the weather forecast, so
    that I am told what the game knows and no more.
39. As a player, I want the menu to say why something is slow or stopped ("the fire is low", "the fire is
    out", "snow is falling on it"), so that I know what to fix.
40. As a player, I want a line in the log when something finishes while I am at camp, so that I do not
    have to keep opening the menu.
41. As a player, I want the receipt on returning to camp to list what finished and at what time, so that
    coming back pays off and I can see what my fire did.
42. As a player, I want the receipt to say when a fire went out with food still on it, so that I learn
    how much fuel a job needs.
43. As a player, I want the camp tile to show that something is working and to mark when something is
    ready, so that the map tells me where to go.

### Taking things

44. As a player, I want finished things to stay at the fire or rack until I take them, so that walking
    through camp with a full pack does not load me down.
45. As a player, I want one "Take" on the menu that takes everything finished, so that collecting is one
    step.
46. As a player, I want what does not fit in my pack to go to the pile, so that taking never fails
    silently.
47. As a player, I want to take something back before it is done and get exactly what I put on, so that I
    can change my mind, or break camp in a hurry.
48. As a player, I want taking something back early to lose its progress, so that there is no half-dried
    meat to keep track of.

### Risk

49. As a player, I want food at a fire or on a rack to be exposed like food in a pile, so that I learn one
    rule for food left out.
50. As a player, I want meat on the rack to smell like raw meat until it is done and like dried meat
    after, so that a day of drying is a day of risk and the result is safer to keep.
51. As a player, I want a burning fire to keep animals off the food at it as it keeps them off me, so
    that a rack beside a tended fire is safer as well as faster.
52. As a player, I want food an animal took from my rack to carry its cause, so that I can read what
    happened from the receipt.

### Skill

53. As a player, I want to earn Cooking experience when food I set finishes cooking, so that the skill
    still grows though the fire does the work.

### Other people

54. As a player, I want NPCs to cook and melt snow at a fire the same way I do, so that they follow the
    world's rules and I can read their camps.
55. As a player, I want food an NPC set at a fire to be takeable by anyone with hands, so that there is no
    hidden rule about whose it is, for now.

### For the people building it

56. As a developer, I want a recipe to say whether a person or a station works it, so that cooking,
    drying and crafting are one shape in one table.
57. As a developer, I want a transformation's rate to come from the one exposure calculation, so that a
    fire beside a rack or a shelter over it needs no rule of its own.
58. As a developer, I want a transformation's finish minute not to depend on who is watching or how the
    world is stepped, so that receipts and saves agree with play.
59. As a developer, I want content checks to refuse a station recipe that is malformed, so that a bad
    row fails at load.

## Implementation Decisions

### Recipes worked by a station

- **A recipe says who works it:** a person (every recipe today) or a kind of station (a fire or a curing
  rack). A station-worked recipe also says what drives it: heat or drying.
- **A station-worked recipe** has one input (one piece of a discrete resource, or an amount of a
  continuous one up to a stated cap), a result as a share of the input's weight, and a time at full
  rate. It has no tools and no outcome table: nothing is rolled, and nothing fails.
- **The table.** Times are at full rate.

  | Recipe | Station | Driven by | Time | Result |
  |---|---|---|---|---|
  | Cook meat | fire | heat | 15 min a piece | cooked meat, same weight |
  | Cook fish | fire | heat | 12 min a piece | cooked fish, same weight |
  | Melt snow | fire | heat | 10 min a litre, up to 4 L | water |
  | Render fat | fire | heat | 20 min a piece | tallow, 0.6 × the fat |
  | Cure hide | rack | drying | 48 h | cured hide, 0.9 × the scraped hide |
  | Cure mammoth hide | rack | drying | 48 h | cured mammoth hide, 0.9 × |
  | Dry meat | rack | drying | 24 h | dried meat, 0.4 × |
  | Dry fish | rack | drying | 16 h | dried fish, 0.25 × |
  | Dry berries | rack | drying | 12 h, up to 0.5 kg | dried berries, 0.3 × |

- **Food is pieces or an amount.** Raw, cooked and dried meat and fish are discrete; berries, dried
  berries, nuts, roots and honey are continuous. This was given to the discrete-resources ticket (#142)
  as a comment, since the recipes spec did not list food.
- **Melt snow** has no input. It takes one of the fire's places and adds water at the fire until 4 L
  waits there, then stops; taking water lets it go on. It needs no vessel. That snow is assumed to be
  there is an existing gap, unchanged.
- **Removed:** the Cook and Melt snow activities, the "Dry a hide by the fire" recipe, and the hand
  "Render fat" recipe planned in #150. Teas and fire-hardening stay hand recipes that need a fire.

### Stations

- **A station is a location feature** that works transformations and has room for a set number at once.
  It offers its verbs through Interact.
- **The fire** is the existing fire pit. Its room comes from its pit type, in the fire data: open 2,
  mound 3, stone 4. A carried torch is not a station.
- **The curing rack** is a new kind of location feature with its own id, placed by a Camp recipe made
  through the one Make activity: 4 branches, 4 sticks and 1 rope, 45 minutes. It has room for 4. Any
  number may stand on a tile. It has no condition, does not wear, and is not damaged by animals.
- **No queue.** What does not fit stays in the pack or the pile until someone puts it on.

### Transformations

- **State.** Each station holds its transformations: the recipe, the actual input put on (the piece with
  its weight, or the amount), the share of the work done, and who set it. It also holds finished
  results until taken. This is saved.
- **Who owns the rules.** The making system owns transformations, as it owns recipes. It asks the fire
  for its temperature and the exposure calculation for the rest; neither of those knows about recipes.
- **Rate.** One function answers "how fast is this transformation going now", from 0 to 2, where 1 is the
  recipe's stated time:
  - **heat:** from the temperature of the fire the transformation sits at. 1 at or above a reference
    temperature for a good fire, falling with temperature below it, low but above zero on embers, and 0
    when the fire is cold. It never exceeds 1.
  - **drying:** from the exposure at the rack. 1 on a dry, still day. Air warmer than a reference, fire
    heat included, raises it, to at most 2. Wind raises it a little. Precipitation falling on the rack
    sets it to 0; a rack under cover is not rained on, by the cover rule the predators spec gives items.
    Air colder than the reference does not lower it.
- **Stepping.** A transformation's progress and finish minute must not depend on who is watching or on
  how the world is stepped, the same property the decision "Item effects tick every minute" asks of
  item effects. A fire on the station's tile is caught up to the minute being worked out. A finish is
  an event stamped with the minute it happened.
- **Stopping.** At rate 0 nothing is lost. There is no burning, overcooking or spoiling in this spec.
- **Finishing** replaces the input with the result at the station, raises the event, and awards Cooking
  experience per piece to the person who set it, if they are alive, for recipes driven by heat that
  make food. The Cooking speed factor is removed; the skill pays nothing until cooking quality
  (short-loop feature 8), as Crafting does today.

### Verbs

- On a station's menu:
  - **Put on**, per recipe the station works and the person carries an input for. About a minute.
    Refused when the station has no room, naming the room.
  - **Melt snow**, at a fire. Refused with no room.
  - **Take**, for everything finished. What the pack cannot hold goes to the pile, by the existing
    receive rule.
  - **Take back**, for an unfinished input. It returns the input as it was; the progress is lost.
- Putting on does not need the fire to be burning. The menu states the fire is out.
- Each offer carries the expected finish time, and a line saying why it is slow or stopped.
- These go through the same step function for the player and NPCs.

### Forecast

- A station forecasts each transformation's finish minute, assuming everything else stays as it is: the
  fire's own forecast of its burn for heat, the weather forecast for drying. It is the same for the
  player and every other person, and it is what the menu shows.

### Exposed food

- What sits at a station, working or finished, counts as food in the tile's pile for the one access
  check and the one take operation of the predators spec (#124): a station keeps nothing out, and its
  scent multiplier is 1. An input smells as the input until it finishes.
- Fire deterrence applies as that spec has it. Nothing new is added for stations.
- Food taken from a station by an animal carries its cause through the take operation.
- This part lands after #124. Until then food at a station is as safe as food in a pile is today.

### NPCs

- The NPC mind cooks and melts snow by putting on and taking, through the same verbs. Its plans for
  water by snowmelt and for cooking raw meat use the station's forecast for their minutes.
- Anything at a station belongs to nobody. Ownership is deferred to the NPC work.

### Events and what the player sees

- New world events, each with its minute and tile: a transformation finished; a transformation stopped
  and why. Their words live with the other narration.
- The receipt and the tile journal include them. A finish while the player stands on the tile shows in
  the log.
- The view gives the tile a state for its stations: something working, something ready. How it is drawn
  belongs to the UI work.

### Content checks

- A station-worked recipe that has tools, an outcome table, more than one input, no driver, or a station
  kind that does not exist fails at load.
- Every pit type has room in the fire data.

### Save

- New saved state: curing racks, and each station's transformations and finished results. The Cook and
  Melt snow activities leave the saved activity list. The fixture is regenerated by
  `documentation/save-format.md`; old saves are not migrated (decision "No save migrations until the
  rebuild is finished").

### Blocking

- Discrete resources are pieces (#142): a station takes one piece.
- Sticks, branches and firewood (#143): the rack's recipe.
- Hides, strips and clothing (#146): scraped and cured mammoth hide. That ticket's "drying stays at a
  fire" ends here.
- Processing, lighting and carrying (#150): raw fat and tallow. Its hand "Render fat" becomes the fire's.
- Piles feed animals (#124): animals reaching food at a station.
- Nothing produces raw fish until small game and fishing (#62). The fish recipes are specified and
  tested with fish placed by hand.

## Testing Decisions

- **What a good test is here:** it puts things on a station through the verbs, advances the world, and
  checks what a player could see: what is at the station, what is in the pack, the minute on the event,
  the line on the receipt. It never reads a transformation's stored share of work done except through
  the forecast and the menu.
- **Seam 1, verbs.** `Verbs` and `Sim.Step`, as in `VerbsTests`, `WorkTests` and `MakeTests`. Covers:
  - the fire's menu offers Put on for carried raw meat and not for a stone;
  - putting on takes the piece from the pack and about a minute;
  - a third piece is refused at an open fire, naming the room, and accepted at a mound pit;
  - Take moves everything finished to the pack, and the overflow to the pile;
  - Take back returns the same piece, and putting it on again starts from nothing;
  - putting on at a cold fire is allowed and the offer says the fire is out;
  - Cook and Melt snow are no longer offered; a tea still is.
- **Seam 2, the world.** A hand-built world with a fire or a rack and a fixed weather sample, then
  `Sim.Advance`, then read the world. Prior art: `FireTests`, `ShelterTests`, `KillChainTests`. Covers:
  - meat at a good fire is cooked 15 minutes later, at the same weight;
  - the same meat at a weak fire takes longer, and on embers longer still;
  - a fire that dies leaves the meat raw, and relighting finishes it in the time that was left;
  - melting stops at 4 L and goes on after water is taken;
  - a 1 kg piece of meat on a rack on a dry, still day is 0.4 kg of dried meat 24 hours later;
  - a rack beside a burning fire finishes sooner than one without, and never in under half the time;
  - a rack in falling snow makes no progress, and one under cover does;
  - a rack in deep cold on a dry, still day takes its stated time;
  - a scraped mammoth hide cures to 0.9 of its weight;
  - the finish event carries the minute it happened when nobody was on the tile, and the receipt on
    return lists it;
  - Cooking experience goes to the person who set the meat.
- **Seam 3, forecasts.** In `ForecastTests`: a station's forecast finish agrees with what then happens,
  for a fire with enough fuel, a fire that will die first, and a rack under a fixed weather sample.
- **Stepping.** A property test beside the conservation tests: the same station advanced in one step,
  in many small steps, and with and without a person standing there, finishes at the same minute.
- **Conservation.** Weight in times the recipe's share is the weight out, and every discrete piece is
  whole, extending the existing conservation tests for Make.
- **Exposed food,** after #124, at the world seam: a wolf downwind eats meat off a rack with no fire, and
  does not when a strong fire burns beside it; the meat taken carries its cause.
- **NPCs.** `NpcTests`: an NPC that cooked and melted snow before still ends fed and watered, through
  the station verbs.
- **Content.** `ContentStrictTests` for the malformed station recipes and pit room.
- **Save.** Save and load with meat half cooked and a hide half cured; the finish minute is unchanged.
- Fast tests for all of these. A test that needs 48 hours uses a hand-built world with no animals, so it
  stays quick; if it cannot, it is tagged LongRun.

## Out of Scope

- Water needing a container, and containers having a capacity. A follow-up issue; the teas in the
  recipes spec and the unused waterskin wait on it.
- Ownership of what sits at a station, and NPCs taking each other's food. Deferred to the NPC work.
- Spoiling, freezing and wetness on things, and anything burning or overcooking.
- A smoked food as its own resource. A rack by a fire is faster; it makes the same dried food.
- Transformations that need no station (water freezing in a pile, meat drying where it lies). The
  definition allows them; nothing here builds them.
- Cooking quality and what the Cooking skill pays (short-loop feature 8).
- Unattended meat smell as its own edge: it is covered by food at a station being exposed.
- A source of raw fish (#62).
- How the tile icon, the menus and the finish time are drawn (the UI work, #52 and `ui-design.md`).
- Checking that there is snow to melt (an existing deferred item).

## Further Notes

- **Flagged changes from the old game**, per the porting rules:
  - cooking and melting by hand are removed; the fire does them;
  - the rack's hours depend on weather and a nearby fire, where the old rack's were fixed;
  - the campfire drying slot the roadmap planned is dropped; a rack by a fire is faster instead;
  - food on a rack or at a fire can be eaten by animals;
  - the rack is built from branches, sticks and rope, not logs, sticks and plant fibre;
  - one rack place takes up to 0.5 kg of berries, where the old rack took any weight as one item;
  - several racks may stand on one tile.
- **Changes to the short-loop plan §6:** no automatic collection; room fixed by pit type, not scaling
  with fire size; the meat-smell edge is not optional. The plan carries a dated note.
- **Audit items resolved:** ITM-21 (curing rack). FIR-9 (cooking in lumps) is settled for cooking: one
  piece at a time, set at the fire.
- **Numbers to set in the tickets:** the fire's reference temperature and its ember rate, the warm-air
  and wind terms of drying and their reference air, and the minute a Put on costs. Each ticket states
  where its numbers came from in its work log. The times, shares, room and the 0 to 2 range above are
  decided.
