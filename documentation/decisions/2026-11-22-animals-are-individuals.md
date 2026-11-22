# Animals are individuals; a herd is derived

Every animal is its own agent, with its own body, plan and mind. A herd is the animals of one species
standing on one tile, worked out when something needs it and never stored. Herds hold together
because each species' cohesion makes going somewhere without kin cost more (the total is multiplied
by 1 + cohesion), and they split because a grazing source's yield is shared among the grazers on the
tile. There are no split or merge rules for herds. Packs are closed groups made by familiarity, with
rules of their own: how familiarity grows, fades, drops after an attack, and is seeded at birth and
arrival. There is deliberately no persistent pack id; a feature that needs "that pack" over time
would have to add one.

The alternative was the herd as the agent, with rules to split it and merge it: one decision for
many bodies, and cohesion guaranteed. We turned it down because a tile is 100 m, so the only
difference the player can see is herds splitting and joining across tiles, and individuals give that
without a rule of its own. Measured on 2026-11-22 with today's cheap mind, one agent per animal costs
about 2-2.5x the animal side of a sim day; the real planner was not in that measurement and is gated
on its own.

This stands only if the cohesion gate passes, per species: open-group animals do not scatter, herds
do not grow into one blob, herds do split and join, packs hold and do not merge, and cost stays
within budget. If it fails, the fallback is herd agents with split and merge rules. The body needs,
sources, route search, planner and knowledge view survive that; only the grouping changes.
