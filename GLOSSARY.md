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
