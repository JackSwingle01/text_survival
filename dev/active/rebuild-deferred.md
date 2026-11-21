# Rebuild: what is deferred

Everything the clean-room rebuild (`rebuild/Sim`) knowingly leaves out or simplifies, one line each,
with where the code says so and how to pick it up. Paths are under `rebuild/Sim/`. Judgment calls
that are settled (a stone weighs 0.3 kg, no leap years, Rng modulo bias) are not listed.

## Content and crafting

- `Fire.cs:36`, `Items.cs:77`, `Species.cs:35` - fuel, resource, gear and species tables live in code.
  Upgrade: move the plain rows to `Content/*.json` beside locations and loot, checked by `Content.Parse`.
- `Shelter.cs:58` - only the simplest camp recipes exist (bedding, frames, snow shelter, pitching a tent);
  no general crafting, no hide tent recipe. Upgrade: port `NeedCraftingSystem` as its own system; `Recipe` becomes a content row.
- `Npc.cs:48`, `Npc.cs:439` - NPCs spawn with an axe and knife and cannot make a fire starter, standing in
  for the old NPC's crafting. Upgrade: once crafting exists, shrink the kit to the player's and craft at these points.
- `Shelter.cs:74` - old `CacheFeature` (capacity, predator/weather protection, food preservation) not ported;
  no branch-to-log frame rebuild (old 60% salvage), no reclaim from an abandoned project. Upgrade: port with scavengers that raid stashes.
- `Content/loot.json:16` - the old ancient cache's fishing rod or net is left out. Upgrade: add with fishing.
- `Content.cs:61`, `Pools.cs:150` - a site's small game, caches, `FirstVisitEvent` and `SiteEvent` are carried as data only.
  Upgrade: small game as a density to hunt in; events fire on arrival and in `Pools.Search` when found (emergent-world moments).
- `Content.cs:12` - a site's `Edges` (old climb on all sides) is carried, not applied (the old game never read it);
  `Escape` and `Vantage` wait for combat escape terrain and scouting. Upgrade: apply in `Map.EdgeBetween` if climbing sites should cost.

## Water, ice and ground

- `Content.cs:54`, `Map.cs:199`, `Work.cs:155` - site water ice is static: no ice holes, fishing, or ice that grows and thaws;
  a frozen lake gives water without a hole. Upgrade: give Ground ice thickness, then port `IceCuttingStrategy`.
- `Work.cs:306` - melting snow never checks there is snow (old: "assumed freely available", even in July).
  Upgrade: take it from `Ground`'s snow depth on the tile.
- `Ground.cs:214` - burial access and digging (old `GroundSurface.Excavation`, `DigUpStrategy`) not ported.
  Upgrade: per-placement elevation, footprint and hole depth on `TileState`.
- `Ground.cs:216` - no route packing (`Compact` is ported, nothing calls it); the old game deferred it too.
- `Map.cs:87` - no seasons on edges (the old river could flood in spring).
- `Travel.cs:114` - herds ignore snow and mud when they walk. Upgrade: read the ground if deep snow should slow herds
  (costs a ground catch-up and save state on every tile a herd crosses).

## Animals

- `Fauna.cs:47` - herds never come for the player or an NPC (old `ShouldEngagePlayer`, stalking, pack attacks on NPCs).
  Upgrade: encounters through `Combat.Boldness`, which is ready for them.
- `Fauna.cs:187` - a lame member that mends never rejoins its herd; it lives on as its own herd.
- `Fauna.cs:303` - old hungry wolves chased escaping prey 40% of the time; here they patrol on.
- `Fauna.cs:527` - old hyenas shadowed feeding predators and wolf packs; only carcasses draw them here.
- `Populate.cs:8` - old territory details (tracks, droppings, bones as pools) not ported; herds leave real prints instead.
  Upgrade: emergent-world signs.
- `Body.cs:523` - the inner-organ hit threshold (5) predates the 0-1 damage scale, so only huge hits reach organs.
  Upgrade: rescale when combat damage is tuned.
- `Carcass.cs:298` - cut meat left waiting on a carcass does not rot further.

## Combat and NPCs

- `Combat.cs:436`, `Npc.cs:771` - only the player fights round by round; an NPC's own fight resolves at once,
  its rounds lived by its wounds alone. Upgrade: let other agents step `Fight` when combat allows it.
- `Combat.cs:490` - the remainder of a fight the player left resolves at once (old: a background fight, a round a minute).
- `Combat.cs:636` - no Fighting skill added to blows. Upgrade: with progression.
- `Combat.cs:724`, `CombatAI.cs:37`, `Npc.cs:26` - relationship opinions are never written, so companions'
  help and pack cohesion ignore them. Upgrade: feed `Opinions` from the `WorldEvent` stream (plan decision 4).
- `Fire.cs:137` - fuel hours as a person reckons them (`Fire.FuelHours`, the NPC's camp fuel and remembered fire hours)
  ignore a stone pit's efficiency and a small fire's slower burn (both under 15%).
- `Knowledge.cs:89`, `Npc.cs:699`, `Map.cs:128` - no sight rays or fog of war: everyone's sight is `Sight.Visible`, a fixed
  5-tile radius blind to terrain, weather and dark, and NPC threat checks read herds' true positions.
  Upgrade: port old `GameMap.UpdateVisibility` behind `Sight.Visible`; nothing else needs to change.
- What an NPC still reads from the world rather than its beliefs, deliberately (plan decision 7; the camp's fire
  and stash now come from memory out of sight, `Npc.CampFuelHours`): `Npc.cs:597` the forage pool kind and full
  density of a remembered tile (the land itself, fixed content: "the look of the country"); `Npc.cs:806` passability
  for its walks (terrain, cliffs and ravines, which never change and every map shows); the crossing off its own tile
  and the weather overhead (felt, not seen); herds within 1-3 tiles (the sight-rays entry above).
  Upgrade: route the first two through `Beliefs` once fog of war means a person can not know the ground.
- `Npc.cs:570` - the NPC's foraging trip is weighed for cold by its walking chill at the tile it decides on
  (`WalkingCoolingFPerHour`); out and back for food, it can still turn home because the walk tired or chilled it more
  than reckoned (seed 17: 3 such trips in 30 days). Upgrade: reckon the trip's chill along the path, not the first tile.
- `Travel.cs:201` - old sprains also added SprainedAnkle / Clumsy effects; there are no such effect kinds yet.

## Knowledge, receipts and skills

- `Knowledge.cs:18` - a bare tile seen from a distance is not remembered (only tiles stood on or with something notable),
  so memory cannot draw "explored but empty" ground. Upgrade: a seen-tiles bitset per person when the map needs fog.
- `Knowledge.cs:236` - receipts leave out human prints: yours and another person's are the same mark.
  Upgrade: remember a tile's human traffic as of the visit and report only what was added since.
- `Knowledge.cs:122` - the player's camp, where a receipt tells everything, is the generated camp; moving camp does not move it.
  Upgrade: a camp the player chooses (a tent pitched, a shelter built) once camps can move.
- `Knowledge.cs:145` - an NPC's receipts are computed and dropped; the old `WitnessedTheft` / `StoleFrom` memories wait on them.
  Allies do not share beliefs yet (short-loop step 8). Herds keep their learned fear on `HerdMind`, outside `Beliefs`.
- `Knowledge.cs:58` - `Danger` has Hurt and Den; no thin ice, since Ground models no ice thickness yet.
- `Npc.cs:36` - an NPC's last close threat (`ThreatTile`) stays its own decision state, not a `Sighting`: it keeps the
  flight logic exactly as tuned. Upgrade: derive it from `Beliefs.Sightings` if the two should agree.
- `Shelter.cs:459`, `Skills.cs:62` - Crafting teaches but pays nothing (crafting has no failures to spare yet); Cooking pays
  in speed only (cooking has no bad results yet). Upgrade: crafting luck and cooking quality (short-loop feature 8).
- `Odds.cs` - odds come as factors for fire-starting, throws, injury on hazardous ground and forage yield. Not yet:
  stalking detection (`Combat.DetectionChance`, a power curve the stalk redesign replaces), dodge, shove and the flat
  90% blow (no player choice rides on them yet), butchering yield (one product per resource; a sheet when butchering
  becomes a trickle bar), a forage find's own chance (it hangs on a luck roll made inside the minute).

## Shelter and bedding

- `Shelter.cs:250` - snow shelter melt rate (0.01 of each level per hour per °F above freezing) is invented;
  the old hook was a stub. Upgrade: tune in playtest.
- `Shelter.cs:328` - old bedding `HasWindProtection` / `HasGroundInsulation` flags dropped (nothing read them).
  Upgrade: give them numbers when a sleeper's wind is modelled.
- `Shelter.cs:94` - the camp's Overhang and bedding are placed from code by `World.New`; the camp is not a content site.
  Upgrade: make the starting camp a `Content/locations.json` site, so `Map.PlaceSiteShelters` places them.
- `Sim.cs:254` - a known physics question: a shelter's wind cover cuts both the wind chill and the body's own wind
  (convection, wind through clothing, ambient drying), as a fire pit's walls do, but a tile's wind factor reaches
  only the chill (old game). One of them is wrong. Drying by a fire is decided: it reads the open wind less the
  pit's walls, never the shelter's (old game parity, `SurvivalContext.FireDryingWindLevel`).
  Upgrade: decide what a tile's wind factor does to a body.

## Performance and portability

- `Sim.cs:279` - every agent steps every minute. Upgrade: distant agents step hourly at hour-aligned minutes (plan decision 3).
- `Tracks.cs:174` - fading tracks are swept by a full pass, at most hourly. Upgrade: index by expiry if saves grow.
- `MapGen.cs:8` - `Math.Sin/Cos/Pow` are libm calls, so terrain is exact per platform, not across platforms.
  Upgrade: a portable sin if saves must move between operating systems.

## Reports

- `Report.cs:41` - a region with fewer than a third of the median finds fails; the design doc gives only the 3x ceiling.
  Upgrade: tune against playtests.
- `Report.cs:239` - the fuel floor is 12 hours of fire from an hour's work by hand on every tile of a region (a night
  at about a kilo an hour). Standing trees, and stock that needs a tool, are felled fuel: counted apart (`fuel_h`
  vs `felled_h`) and not toward the floor, since the floor is about surviving and nobody starts with an axe. About
  a quarter to a third of regions fail it (seed 17: 26 of 115; 22 if trees counted), open steppe, rock and fringe
  squares with little deadfall.
  Upgrade: tune against playtests, or weigh a region by its passable tiles if fringe squares should not count.
