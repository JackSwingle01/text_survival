# Herd bench

A throwaway browser sim for #83 and #132: caribou deciding like the #83 food planner, at main's values (28-day linear
regrowth, grazing down by body mass, at least 5 minutes a tile, bodies ±15%), with water, thirst and one wolf pack.
It compares crowding and kin rules for how herds split, merge and flee. It is a model of the planner's rules, not the
game's code: one prey species, no rest, snow, terrain costs or days and nights, and a sketched wolf hunt.

- `core.js`: the simulation (also runs in Node: `require('./core.js')`).
- `page.html`: the page, with `/*CORE*/` where the core goes.
- `build.py`: writes `index.html` (the page with the core inlined). Open `index.html` in a browser.

What it showed, and the decisions taken from it, are on #132 and #83.
