# Text Survival

An Ice Age survival game: a person alone in a valley, surviving until the pass clears. These are the
terms the design and the simulation share.

## Needs

**Stat**:
One of the things a body must keep up: warmth, water, food, rest. Every person (player or NPC) has
all four; an animal has water, food and rest.

**Priority**:
How urgent a stat is right now: Critical, Need or Want. Priority decides first; within a priority the
stats rank warmth, then water, then rest, then food. An agent acts on its top-ranked stat.
_Avoid_: tier, level, urgency score

**Critical**:
The priority where a stat means imminent death: drop whatever you are doing.
_Avoid_: urgent, emergency

**Need**:
The priority for what it takes to survive the day, or to bring a stat back up to its baseline.
_Avoid_: wanted, required

**Want**:
The priority for anything beyond the day's survival: stockpiling, a surplus, comfort.
_Avoid_: optional, luxury

**Source**:
A place where a stat can be met right now, with what it offers there: grazing, open water, snow, a
den, a carcass, a pile, a cache.
_Avoid_: offer, patch, spot

**Route**:
The way an agent will walk to a source: the tiles in order, with the minutes and calories it costs.
_Avoid_: path, trip

**Plan**:
What an agent has decided to do about its top-ranked stat: a source, the route there and the work it
expects there, kept until something interrupts it.
_Avoid_: goal, errand, task

## Things in the world

**Exposure**:
What the world is doing to a body, an item or a location feature in one place right now: air
temperature (fire included), wind at it, precipitation, ground wetting, sun, and how covered it is.
_Avoid_: environment, weather effects

**Effect**:
A state that builds up and fades under exposure and time, on a body, an item or a location feature:
wet, frozen, spoiling, and for bodies only bleeding, pain, shivering. The same kind of state for all
three.
_Avoid_: status, debuff

**Discomfort**:
An effect that wears a body down without harming it: hungry, thirsty, tired. Their losses stack with diminishing returns, so together they never take a
capacity below about a fifth; wounds, blood loss and the cold take what they take on top.
_Avoid_: debuff, need effect

**Condition**:
What a verb sees about a location feature or an item: a fact read from its effects and its place
(frozen to the ground, buried under snow, occupied), never stored. A blocking condition stands in
front of the verb and has its own clearing verb; a modifying condition blocks nothing and only
changes how the verb goes.
_Avoid_: tag, flag, status

**Occupied**:
The blocking condition of a location feature another agent is using right now: wolves feeding on a carcass,
a bear in a den, another person at a fire.
_Avoid_: guarded, claimed, owned

**Contest**:
The clearing verb for an occupied location feature: making a claim on it. The occupant's own mind answers:
it yields, holds and threatens, or fights.
_Avoid_: drive off, take, challenge

**Ask**:
Contest, toward an ally: the same claim, answered by that person's mind and their opinion of you.

**Interact**:
The single verb shown for a location feature that offers several (a fire, a carcass, a pile, a
cache, a shelter); it opens that feature's menu.
_Avoid_: use, open

**Pile**:
Whatever items lie on a tile, outside any cache: no limit and no protection. Any animal whose diet
takes a food in it can eat it.
_Avoid_: stash, supplies, ground items

**Cache**:
A location feature that holds food up to a capacity and keeps out every animal except those with an
ability it lists (flies, climbs, digs, strong, hands). Built or found; food buried in snow is a cache that
diggers get into.
_Avoid_: storage, stash, camp storage

**Ability**:
What a species' body lets it do to get at food: flies, climbs, digs, strong, hands. People have hands,
and every cache lets hands in.
_Avoid_: capability (that is what tools give a person), access

**Dirty**:
An effect on a body that builds with time, sweat and carcass work and only adds to its scent. It does
not fade on its own; washing removes it, with Bloody.
_Avoid_: sweaty, grimy, unwashed

**Exposed**:
Of food: reachable by a given animal, because it lies in a pile or in a cache that animal gets into.
Worked out each time, never stored.
_Avoid_: unprotected, unguarded

**Scent**:
What a source gives off to an animal's nose: a strength (per food, for blood and bleeding, for a
carcass) scaled by a cache's scent multiplier, carried further downwind than upwind and read through
the species' sense of smell. Piles and caches are found only by scent or by standing on their tile.
_Avoid_: smell radius, signal (signal is the wider set: noise, scent, fire light)

**Diet**:
Which foods an animal eats from piles, caches and carcasses: carnivore, omnivore or herbivore. What a
herbivore grazes from the ground is its own list per species.
_Avoid_: browser, grazer (as diets)

## Items

**Item**:
Anything a person can carry: a resource or gear.
_Avoid_: thing, object, loot

**Resource**:
A carried material, either discrete (counted in pieces, each with its own weight: a branch, a stick,
a stone) or continuous (measured as one amount: water, tinder, resin).
_Avoid_: divisible, bulk, stackable, stack

**Piece**:
One unit of a discrete resource.

**Stick**:
A small piece of dead wood, about a hand's span to an arm's length: kindling, and the handle of a small
tool.
_Avoid_: kindling (that is a stick's job in a fire), twig

**Branch**:
A dead limb or pole, gathered by hand: the wood for spears, hafts and frames, and fuel before you have an
axe.
_Avoid_: log, pole, limb

**Hide strip**:
A piece cut from a cured hide, a few to a hide: for small garments and for mending.
_Avoid_: scrap, patch, leather

**Firewood**:
Wood cut to burn, only ever made with an axe, from a felled tree or big deadfall.
_Avoid_: log, chopped wood, split wood

**Gear**:
A tool, weapon, garment or accessory, held one at a time, with its own condition that wears down.
_Avoid_: equipment, kit

## Making things

**Recipe**:
How one thing is made: what goes in, the tools by capability, what the place must have, the work, the
result, and what can go differently. The work is done by a person or by a station. Crafting and
building are both recipes.
_Avoid_: blueprint, formula, craft option

**Result**:
What a recipe makes when nothing goes differently: the thing the menu shows.
_Avoid_: output, product

**Outcome**:
One way a finished piece of work can turn out besides the plain result, with its base odds: a
success or failure, plus any effects (wasted or saved material, an extra product, an injury, a broken
tool). One outcome is rolled per finished piece of work; whatever odds are left are the plain result.
_Avoid_: roll, event

**Step outcome**:
How one step of an activity ended: finished (it did what it set out to, or ran its minutes), refused
(it could not start, and no time passed), or interrupted (something cut it short), with the reason in
the system's words. Not an Outcome, which is how a piece of making turns out.
_Avoid_: result, status

**Project**:
Work on a recipe that is not finished yet, keeping its progress: on the tile for something placed, as
an unfinished item in the pack for something carried.
_Avoid_: job, task, build in progress

**Transformation**:
Inputs at a station being turned into a recipe's result by the station, with no person working: meat
cooking at a fire, a hide curing on a rack. It makes a different thing and never goes back, which is
what tells it from an effect. Its rate comes from the exposure at the station.
_Avoid_: job, project (a project is a person's work), process

**Station**:
A location feature that works transformations, with room for a set number at once: a fire, a curing
rack.
_Avoid_: workbench, slot (say a station's room), crafting station

## How the game is built

**Feature**:
A piece of gameplay the player meets, such as a new location, weather type, animal, recipe, effect,
condition, item, verb, terrain or discovery.
_Avoid_: feature on its own for something at a place (say location feature)

**Location feature**:
Anything at a place that can be seen, used or taken from: a fire, a shelter, a pile, a cache, a carcass, a
stand of trees, a berry bush, forage ground, a lake, a rock overhang. Location features are told apart
by the verbs they offer, not by sub-kinds.
_Avoid_: thing, structure, landmark, site feature, object

**Harvestable**:
A location feature you gather from again and again, whose supply runs down and may regrow: a berry
bush, deadfall, a stand of trees, forage ground.
_Avoid_: pool, patch, resource node

**Shelter**:
A location feature that keeps weather off a person, built or natural: a lean-to, a pitched tent, a
rock overhang, a cave.
_Avoid_: structure, building

**Surface**:
The ground at a place as it is: snow depth, the top snow's packing, slush, crust, mud, dampness,
standing liquid and its ice. One read gives it (underfoot at a place), and every effect of the
ground (traversal, footing hazard, search, wetting, drinkable water) is worked out from it alone.
_Avoid_: surface summary, ground state (the column the physics steps, which only Ground and its tests read)

**Trodden**:
How packed by feet a place's snow is, from 0 to 1. An edge's trodden amount comes from completed
crossings, each walker by its weight, saturating so the first count most; it settles away in about a
month, and snow fallen on it since its last stamp hides it until the next. Read at an edge's half of a
tile, it raises the top snow's packing. Trail tiers (trace, path, trail) are derived from it.
_Avoid_: path wear, trail wear

**Discovery**:
A location feature or items hidden at a place until searching turns them up.
_Avoid_: find, hidden feature

**Clue**:
What a forager notices before searching a place, drawn from what grows there, the ground, the sky, the
hour and the animals near: an authored line, not a trace an agent left. A resource clue points to a
focus and pays off when that focus is chosen; a scavenge clue leaves remains when the search ends.
_Avoid_: hint, sign (a sign is a real trace)

**Sign**:
A trace an agent left at a place that tells you about it: tracks, scat, a kill, a cold fire.
_Avoid_: clue, evidence

**System**:
The one owner of a set of rules about the world, such as fire, body, ground, weather, animals, fights,
making or travel. Its rules live only there; everything else asks it.
_Avoid_: manager, module, service

**Verb**:
Something a person could do here and now, offered with its refusal, odds and cost in time. A move in
a fight is a verb too, costing seconds rather than minutes.
_Avoid_: action, combat action, option, command

**Activity**:
A verb a person has chosen and is carrying out.
_Avoid_: task, job, action

**Refusal**:
Why a verb can't start now, naming what is missing and how much where getting it would help.
_Avoid_: error, block, requirement

**Tutorial**:
A once-per-game teaching line, kept so it never repeats (the day-1 line about gathering fuel before
dark). Not a refusal, a status or narration.
_Avoid_: hint, tip

**Forecast**:
A system's rough, probabilistic expectation of how its own part of the world goes on, assuming
everything else stays as it is now; the same for the player and every other person.
_Avoid_: prediction, simulation, estimate

**Agent**:
Anything that decides and moves in the world on its own: a person or an animal.
_Avoid_: actor, entity, unit

**Opportunist**:
An animal that neither hunts nor holds a kill and eats what it finds: forage, piles and carrion (carcass
meat and a dead person's flesh). Ravens and foxes. Ravens perch over a kill a predator holds; foxes keep off it.
_Avoid_: carrion (that is the food, not the animal), flock

**Herd**:
The animals of one species on one tile, from a lone bear to a hundred caribou: what they make
together, not a thing of its own. A pack is a herd of predators familiar with each other.
_Avoid_: group, flock

**Cohesion**:
How strongly an animal sticks with its group, from a bear's none to a caribou's strong pull: going
somewhere its kin aren't costs it more, and under attack it holds with them.
_Avoid_: herding instinct, sociality

**Camp**:
A place a person has set up to live from for now, with a fire, a pile, a cache or a shelter. Camps are
temporary, and a person may have several.
_Avoid_: home, base

**Memory**:
What a person has seen and remembers of the world: their map, the ground as last seen, piles and caches,
fires, dangers. It can be out of date or wrong.
_Avoid_: beliefs, knowledge, explored

**Familiarity**:
How well one agent knows another particular agent: it grows with time spent together, fades apart,
and falls when one attacks the other. A wolf pack is the wolves familiar with each other.
_Avoid_: relationship, opinion, bond

**Observer mode**:
A development view of a world with no player in it: the whole map as it truly is, not anyone's
Memory, with time run at a chosen rate and people, animals or things placed by hand to watch what
the world does.
_Avoid_: debug mode, god mode, spectator, fog-of-war off

## Bodies

**Damage**:
How hard a blow is, measured in tissue condition: a tissue of toughness 1 in a body of reference
size loses one whole condition for every unit it takes. Weapons, bites and falls are all on this one
unit.
_Avoid_: damage scale, hit points, 0-1 damage

**Toughness**:
How many units of damage a tissue takes to destroy. It grows with the size of the body. How much of
it one blow meets depends on the kind of blow: a point meets little of the soft tissue it passes, a
club little of the flesh and all of the bone. What a blow does not meet, it passes on.
_Avoid_: armor, hardness

## Danger

**Boldness**:
How much an animal is willing to risk right now, from its nature, hunger, numbers and what it is
defending.
_Avoid_: morale, courage, aggression

**Threat**:
How dangerous something looks to an animal weighing whether to press it.
_Avoid_: danger score, strength

**Resolve**:
What is left of an animal's boldness once threat, fire and pressure are weighed against it. High
resolve presses; when it is gone the animal backs off or flees.
_Avoid_: margin, nerve, morale

**Encounter**:
One meeting between agents close enough to act on each other, from the moment they are aware of
each other until one side leaves or dies. Combat is the system that runs it; an encounter may end
without a blow.
_Avoid_: fight, battle, combat (for the meeting itself)
