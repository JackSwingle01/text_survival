# Porting the old game: the rules

*Temporary. Delete this file when the port is done and the old project is retired.*

Decided by the user on 2026-11-22, after the parity audit found that the rebuild had quietly
simplified things the player meets while the checklist called them Done. The audit's 130 flagged
items are in [parity-audit-items.json](parity-audit-items.json).

## The rules

- **The old game is the default.** Its behaviour and its tuned numbers are the spec.
- **Flag, never drop.** Anything that could be improved or simplified goes on a review list for the
  user. It is never dropped or changed silently.
- **Only intentional design changes are excluded.** A change counts as intentional only if it is in a
  decision record (`documentation/decisions/`), or the user chose it in a session and it is written in
  the roadmap. A change an agent made, even after talking it over with the coordinator, goes on the
  review list.
- **Review system by system.** When a system is restored, its flagged improvements go to the user
  first, a handful at a time. Everything else in that system is restored to the old game.
- **Nothing reachable becomes unreachable.** Everything the player could get or do in the old game
  can be got or done.
- **The player sees what the game knows** (decision "The UI shows what the game knows").
- **The old game's choices come back:** forage focus and time; the fire-starting tool and tinder; how
  long to sleep, work or butcher; how much to move; which weapon to carry.
- **Consequences stay:** tools and clothing wear out, and death is final (decision "Death is final").
- **The UI prototype (`dev/prototypes/ui/index.html`) is a style guide,** and a guide for how to port
  the old UI. It is not a spec: it mixes in features that were never in the code.
- **Missing systems keep their roadmap order:** treatment, small game, curing, events, NPCs in the
  world. These rules apply when they are built.
- **Each built screen is compared side by side** with the old game, and with the prototype's style,
  before it counts as done.

- **A design session waits for its restores.** Camp storage, recipes and the fire as a workbench each
  have a session with the user (roadmap, "Design sessions with the user"). Restore what comes before
  it first; build nothing the session decides until it is held.
- **Mark the audit.** When the user decides an audit item, add a `decision` key to it in
  `parity-audit-items.json`.

## Work built before a design session (added 2026-11-22)

Steps 1-3a of `documentation/emergent-world-design.md` were built before a design session with the
user, who did not know they existed. Everything they built goes on the user's review list as flagged
changes:

- `2af01d6` QA tools (find dump, distribution report, population curves, CLI report);
- `11cedc4` the substrate (`ThingRef`, `Cause`, `Signs.At`, derived Tags and Capabilities,
  `Access.Refusal`);
- `9cbe8cc` the kill chain: herd renewal, ravens as scavengers with Beacons and raids, the carcass and
  body pipeline, kill attribution and causes of death.

A design-review session with the user comes before any more of that design is built. Until then no
further emergent-world step starts. The full list is in the roadmap, "The emergent-world work is on
hold".

## The restore order

1. Foraging and discovery.
2. Discrete and continuous resources (decision "Resources are discrete or continuous").
3. Reachability: recipes, gear, picking gear back up.
4. Consequences: wear, final death and autosave.
5. The remaining screens: fire, body, butchering, combat feedback, travel, the tile sheet.

The system decisions for each step are in `dev/active/rebuild-roadmap.md`, "Restoring the old game
(2026-11-22)".
