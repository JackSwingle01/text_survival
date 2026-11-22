# Text Survival

An Ice Age survival game: a person alone in a valley, surviving until the pass clears. These are the
terms the design and the simulation share.

## Needs

**Stat**:
One of the four things a person must keep up: warmth, water, food, rest. Every person (player or NPC)
has all four.

**Tier**:
How urgent a stat is right now: Critical, Need or Want. Tier decides first; within a tier the stats
rank warmth, then water, then food, then rest.
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
What an action sees about a thing: a fact read from its effects and its place (frozen to the
ground, buried under snow, occupied), never stored. A blocking condition stands in front of the
thing's action and has its own clearing action; a modifying condition blocks nothing and only
changes how the action goes.
_Avoid_: tag, flag, status

**Occupied**:
The blocking condition of a thing another actor is using right now: wolves feeding on a carcass,
a bear in a den, another person at a fire.
_Avoid_: guarded, claimed, owned

**Contest**:
The clearing action for an occupied thing: making a claim on it. The occupant's own mind answers:
it yields, holds and threatens, or fights.
_Avoid_: drive off, take, challenge

**Ask**:
Contest, toward an ally: the same claim, answered by that person's mind and their opinion of you.

**Interact**:
The single action shown for a thing that has several actions (a fire, a carcass, a stash, a
shelter); it opens that thing's menu.
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
_Avoid_: feature for something lying at a place (that is a thing)

**Thing**:
Anything at a place that can be seen, used or carried: a fire, a stash, a carcass, a shelter, a tree.
_Avoid_: feature, object, entity

**System**:
The one owner of a set of rules about the world, such as fire, body, ground, weather, animals, fights,
making or travel. Its rules live only there; everything else asks it.
_Avoid_: manager, module, service

**Verb**:
Something a person could do here and now, offered with its refusal, odds and cost.
_Avoid_: action, option, command

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
