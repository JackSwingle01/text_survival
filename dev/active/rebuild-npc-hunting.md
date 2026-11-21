# NPC hunting: go find prey, don't wait for it

**Status:** planned (noted 2026-11-20 during the clean-room rebuild, phase 3)
**Where:** `rebuild/Sim/Npc.cs` (`FindFood`, hunt candidates), `rebuild/Sim/Fauna.cs` (`BeginHunt`), `rebuild/Sim/Tracks.cs` (`Read`).

## What happens now

An NPC only considers hunting prey that is already within 3 tiles in daylight while it wants food.
In every 14- and 30-day run on the gate seeds that never happened: 0 hunts attempted. Prey herds
live tens of tiles away and the NPC never goes looking. Its food is forage alone.

## What it should do

A hungry, armed, fit NPC treats a herd it knows about as a food source it can travel to:

- **Knowing where prey is.** From what it has seen (herds in sight) and from prints it reads on
  tiles it crosses (`Tracks.Read`: maker, heading, freshness). Not from `world.Agents` truth - the
  current hunt candidates are a `ponytail:` truth read.
- **Following.** Fresh hoof prints give a heading; the NPC follows the trail tile by tile with the
  shared `Travel` activity, re-reading prints as it goes, and gives up when the trail goes cold or
  the walk home would leave it too cold / too far from its fire.
- **Scoring.** Same scored list as foraging, in expected kcal per hour: kill odds x meat it can
  carry / (hours to find + stalk + kill + butcher + carry home). Risk weighted by the predator
  threat and the NPC's condition, accepting more risk the bigger its fat shortfall.
- **Which animal is worth it is computed, not by species** (user, 2026-11-21). Today the NPC uses
  a species-role rule (caribou when hungry, bison and megaloceros when starving, never a mammoth
  alone). Replace it with odds worked out from the target animal's own stats and the hunter's:
  size, condition, wounds, herd size, weapon. A wounded or small animal counts as easier; a healthy
  bull as harder. The same estimate should feed the player's hunt odds sheet.
- **The hunt itself** stays `Fauna.BeginHunt` -> combat -> carcass -> `Butcher`, the player's path.

## Why

Foraging should let a focused forager scrape by; hunting is where the big returns and the risk are
(principles.md: tradeoffs, preparation should matter). Wolves already range wider when starving
(phase 3b fix); the NPC should do the same for the same reason.

## Done when

- On the six gate seeds over 30 days, a hungry NPC attempts hunts and some succeed.
- A test: NPC with a fat shortfall, a herd 10+ tiles away and fresh prints between them follows
  the prints and attempts a hunt; with no prints and no sight of the herd it does not.
- Hunting decisions read only the NPC's own knowledge (remove the `ponytail:` truth read).
