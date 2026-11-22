# Animals read the ground's weather-only estimate

An exception to "Animals read the slow world true". People and animals cross by one formula (`Trip.Terms`)
and route with one search (`Trip.Routes`), but animals read the ground through
`Ground.TraversalFactorEstimate`: the tile's ground class carried through the weather, as if nobody had
trodden or dug it. Reading each tile's true ground has two costs. It catches up that tile's lazy history,
up to a month of it, and it stores a ground state for every tile in the save. Measured on 2026-11-22
(#80, seeds 17 and 23, 30 days, Release), true ground for animals stored 1,372-1,399 tiles, pushed the save
to 1.36-1.51 MB against a 1 MB budget, and raised allocation to 6.9-9.3 GB against 6 GB. The estimate stored
none, kept the save at 835-873 KB and allocated 4.9-5.5 GB (main: 4.1-4.5 GB). The visible cost is small: a
caribou ignores the trench a person has packed or dug through the snow, but it still slows in fresh snow and
mud. Ponds and snow as water sources follow the same answer. Revisit this if ground state gets cheaper to
keep, or if animals ever need to read what people did to a tile.
