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

**Condition**:
A state of a thing, read from the world rather than stored, that stands between a person and
using it: buried under snow, frozen to the ground. It stops being true when the world changes
(a thaw, a fire beside it, the snow blowing off), with no special action needed.
_Avoid_: tag, flag, status

**Occupied**:
The condition of a thing another actor is using right now: wolves feeding on a carcass, a bear
in a den, another person at a fire. What happens if you use it too is up to that actor's mind.
_Avoid_: guarded, claimed, owned
