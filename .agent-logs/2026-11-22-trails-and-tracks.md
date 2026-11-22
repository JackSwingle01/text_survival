# terrain-trails: Trails and tracks on the web map

Hypothesis:
- Changes: Sim/Memory.cs (Memory.Trails: last-seen tier per worn edge, kept in Explore), Sim/Save.cs (format 14) and the save fixture and save-format doc, Web/Host/ViewBuilder.cs and Dto.cs (MapDto.Trails: live tier in sight, remembered tier out of sight; TrackDto gains heading, pattern, freshness), Web/Host/Scenarios.cs (a worn-route scenario for screenshots), wwwroot/terrain.js (trails into the baker and the chunk signature; print marks filtered and cached), wwwroot/map.js (prints instead of the track icons on the map), tools tests.
- Affects: the map's ground near worn routes (drawn where nothing was), the map's track icons (replaced by prints; popup and receipt icons stay, and the PNGs stay for the desktop game), saves (format 13 refused after the bump), every view's size (one entry per worn edge beside a known tile).
- Stays green: dotnet test rebuild/Web/Host.Tests; dotnet test rebuild/Sim.Tests --filter "Category!=Slow" (save fixture regenerated); node --test tools/terrain.test.mjs tools/page.test.mjs; shots --all with no console errors.
- First red: MemoryTests, a route seen and then worn further out of sight still reads at its seen tier, fails on Memory having no Trails.

Revised (review): a per-edge dictionary on Memory grows with herd wear and saves string keys → the tier of each tile's east and south edges lives in GroundMemory's spare flag bits, written only when both tiles are seen
Revised (review): a route could reach into a tile never seen → an edge is remembered and sent only when both its tiles are in the player's memory
Revised (review): every glance read four tiers per tile in view, each working out erosion → erosion worked out once per look, edges with no wear skipped before any arithmetic, each edge read once
Revised (review): tier sent as a player-facing word and pattern as a code → heading, pattern and tier all go as lower-case enum codes, with a host test holding them to what the page accepts
Revised (review): a trails scenario walked to a tier pins balance → the scenario writes wear directly
Revised (review): the page's trail key rebuilt every frame → trail codes per cell built once per map
Follow-up: a view that shrinks between glances (dusk, snow) shows its dropped tiles' ground and routes as of the last glance, up to 15 minutes old; a look whenever the view is cast anew would fix both, which changes Sim timing
Revised: prints are laid one line per animal, and the map's tracks did not say how many → TrackDto carries Traffic
Revised: prints drew over the tree sprites, and seed 1's camp is in forest → sprite placement is one rule (spriteAt) used to bake and to tell what a tree covers; prints show only between trees
Follow-up: the AOT build is untested (no wasm-tools workload on this machine); shots ran on the interpreter build, every state desktop and phone, no console errors
Follow-up: three simulated days of seed 17 take 0.34 s against 0.31 s before (Release, best of 3, measured beside a running test suite); a Release long sim before and after would size the cost of NPCs remembering routes
Revised (review): the host merged live and remembered trails itself, repeating Explore's neighbour walk → Memories.Trails is the one read (in sight as they are, else as last seen), sharing SeenNeighbours with Explore
Revised (review): "route" and "trail" named one thing, and GroundMemory.TrailAt clashed with the wear threshold Tracks.TrailAt → trail throughout the code (TrailTierAt, RememberTrailTier, trailsOf), the two-bit mask named
Revised (review): Code sat beside inline lower-casing of other enums → ViewBuilder sends every enum code through Code
Revised (review): no test showed prints beyond reading distance are left out → a host test does
Revised: main now bakes each cell alone in a worker from its neighbourhood, and the page has no baker → each cell's bake gets the trails with an end within REACH (trailsNear), they key the cell's signature (trailsSignature), and prints are masked by a baker of the same map made on the page when first asked
Revised: main moved the save format to 18 → trails as last seen are format 19, fixture regenerated
