# Text Survival

An Ice Age survival game: a person alone in a valley, surviving until the pass clears. These are the
terms the design and the simulation share.

## Needs

**Stat**:
One of the four things a person must keep up: warmth, water, food, rest. Every person (player or NPC)
has all four.

**Tier**:
How urgent a stat is right now: Critical, Need or Want. Tier decides first; within a tier the stats
rank warmth, then water, then rest, then food.
_Avoid_: level, priority, urgency score

**Critical**:
The tier where a stat means imminent death: drop whatever you are doing.
_Avoid_: urgent, emergency

**Need**:
The tier for what it takes to survive the day, or to bring a stat back up to its baseline.
_Avoid_: wanted, required

**Want**:
The tier for anything beyond the day's survival: stockpiling, a surplus, comfort.
_Avoid_: optional, luxury

## Things in the world

**Exposure**:
What the world is doing to a body or thing in one place right now: air temperature (fire
included), wind at it, precipitation, ground wetting, sun, and how covered it is.
_Avoid_: environment, weather effects

**Effect**:
A state that builds up and fades under exposure and time, on a body or a thing: wet, frozen,
spoiling, and for bodies only bleeding, pain, shivering. One kind of thing for bodies and items.
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
The single verb shown for a location feature that offers several (a fire, a carcass, a stash, a
shelter); it opens that feature's menu.
_Avoid_: use, open

## Making things

**Recipe**:
How one thing is made: what goes in, the tools by capability, what the place must have, the work, the
result, and what can go differently. Crafting and building are both recipes.
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

## How the game is built

**Feature**:
A piece of gameplay the player meets, such as a new location, weather type, animal, recipe, effect,
condition, item, verb, terrain or discovery.
_Avoid_: feature on its own for something at a place (say location feature)

**Location feature**:
Anything at a place that can be seen, used or taken from: a fire, a shelter, a stash, a carcass, a
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

**Discovery**:
A location feature or items hidden at a place until searching turns them up.
_Avoid_: find, hidden feature

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

**Forecast**:
A system's rough, probabilistic expectation of how its own part of the world goes on, assuming
everything else stays as it is now; the same for the player and every other person.
_Avoid_: prediction, simulation, estimate

**Agent**:
Anything that decides and moves in the world as one: a person, or a herd.
_Avoid_: actor, entity, unit

**Herd**:
Animals of one species that move and decide as one, from a lone bear to a hundred caribou. Pack is
an alias for a herd of predators.
_Avoid_: group, flock

**Camp**:
A place a person has set up to live from for now, with a fire, a stash or a shelter. Camps are
temporary, and a person may have several.
_Avoid_: home, base

**Memory**:
What a person has seen and remembers of the world: their map, the ground as last seen, stashes,
fires, dangers. It can be out of date or wrong.
_Avoid_: beliefs, knowledge, explored

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
