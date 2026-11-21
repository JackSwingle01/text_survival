# Rebuild: what is deferred

Everything the clean-room rebuild (`rebuild/Sim`) knowingly leaves out or simplifies, one line each,
with where the code says so and how to pick it up. Paths are under `rebuild/Sim/`. Judgment calls
that are settled (a stone weighs 0.3 kg, no leap years, Rng modulo bias) are not listed.

## Content and crafting

- `Shelter.cs:90`, `Pools.cs:121`, `Map.cs:208` - the camp's tile (its Overhang, bedding, thin forage, wind, cover,
  visibility, name) is code, not content: it moves when the camp becomes a content site (below).
- `Content/terrain.json` felling, `shelters.json` materials, `discoveries.json` (Dry Wood Stack, deadfall), `forage.json` -
  a "log" weighs five ways: 2.0 kg as a shelter piece, 2.0-4.0 kg felled (by wood), 1.5 kg in a Dry Wood Stack, 2.5 kg
  in deadfall, 1.5-3.5 kg foraged. Values kept as ported. Upgrade: one log weight per wood (the felling row's `logKg`)
  that shelter pieces, stocks and forage read, re-baselined in one step with its pins listed.
- `Content/locations.json` (Deep Woods and the other named sites) - a site's forage lists its items inline rather than
  naming a `forage.json` table, so the every-row-is-used check (`Content.DeadRows`) does not reach them and two sites
  with the same forage repeat it. Upgrade: let a site's forage name a table (and a density), inline items only for a
  site whose forage is its own.
- `Content/forage.json` - the old `Barren` forage table (density 0.32) was only the default arm for impassable terrain,
  which never forages; it was not carried over. Upgrade: add a table and a terrain row if a barren passable terrain appears.
- `Content/species.json` - rabbits and foxes have the old defaults, herd behaviour Prey and diet Carnivore (they never form
  herds yet), and the role-and-behaviour check (`Content.HerdBehaviorProblem`) exempts small game and people for it.
  Upgrade: make `behavior` and `diet` optional (null for a species that never forms a herd, refused for one that does),
  then give rabbits and foxes real rows with small game.
- `Shelter.cs:60` - only the simplest camp recipes exist (bedding, frames, snow shelter, pitching a tent; `Content/shelters.json`);
  no general crafting, no hide tent recipe. Upgrade: port `NeedCraftingSystem` as its own system, its recipes born as content rows.
- `Npc.cs:48`, `Npc.cs:519` - NPCs spawn with an axe and knife and cannot make a fire starter, standing in
  for the old NPC's crafting. Upgrade: once crafting exists, shrink the kit to the player's and craft at these points.
- `Shelter.cs:81` - old `CacheFeature` (capacity, predator/weather protection, food preservation) not ported;
  no branch-to-log frame rebuild (old 60% salvage), no reclaim from an abandoned project. The carrion flock raids
  unwatched stashes now (`Carrion.cs`), so a cache's protection has something to protect against. Upgrade: port it.
- `Content/loot.json:16` - the old ancient cache's fishing rod or net is left out. Upgrade: add with fishing.
- `Content.cs:61`, `Pools.cs:162` - a site's small game, caches, `FirstVisitEvent` and `SiteEvent` are carried as data only.
  Upgrade: small game as a density to hunt in; events fire on arrival and in `Pools.Search` when found (emergent-world moments).
- `Content.cs:12` - a site's `Edges` (old climb on all sides) is carried, not applied (the old game never read it);
  `Escape` and `Vantage` wait for combat escape terrain and scouting. Upgrade: apply in `Map.EdgeBetween` if climbing sites should cost.

## Water, ice and ground

- `Content.cs:54`, `Map.cs:184`, `Work.cs:155` - site water ice is static: no ice holes, fishing, or ice that grows and thaws;
  a frozen lake gives water without a hole. Upgrade: give Ground ice thickness, then port `IceCuttingStrategy`.
- `Work.cs:310` - melting snow never checks there is snow (old: "assumed freely available", even in July).
  Upgrade: take it from `Ground`'s snow depth on the tile.
- `Ground.cs:208` - burial access and digging (old `GroundSurface.Excavation`, `DigUpStrategy`) not ported.
  Upgrade: per-placement elevation, footprint and hole depth on `TileState`.
- `Ground.cs:210` - no route packing (`Compact` is ported, nothing calls it); the old game deferred it too.
- `Map.cs:77` - no seasons on edges (the old river could flood in spring).
- `Travel.cs:118` - herds ignore snow and mud when they walk. Upgrade: read the ground if deep snow should slow herds
  (costs a ground catch-up and save state on every tile a herd crosses).

## Animals

- `Fauna.cs:47` - herds never come for the player or an NPC (old `ShouldEngagePlayer`, stalking, pack attacks on NPCs).
  Upgrade: encounters through `Combat.Boldness`, which is ready for them.
- `Fauna.cs:165` - a lame member that mends never rejoins its herd; it lives on as its own herd.
- `Fauna.cs:281` - old hungry wolves chased escaping prey 40% of the time; here they patrol on.
- `Fauna.cs:505` - old hyenas shadowed feeding predators and wolf packs; only carcasses draw them here. The carrion
  flock (`Carrion.cs`) is the one that follows predators. Hyenas stay the old scavenger: they fight, and never starve
  (`Renewal.cs`, judgment: the old hyena never went hungry).
- `Populate.cs:8` - old territory details (tracks, droppings, bones as pools) not ported; herds leave real prints instead.
  Upgrade: emergent-world signs.
- `Body.cs:523` - the inner-organ hit threshold (5) predates the 0-1 damage scale, so only huge hits reach organs.
  Upgrade: rescale when combat damage is tuned.
- `Carcass.cs:298` - cut meat left waiting on a carcass does not rot further until the carcass goes to bones, when
  it rots away with the rest (`Carcasses.RotAway`).

## Combat and NPCs

- `Combat.cs:436`, `Npc.cs:965` - only the player fights round by round; an NPC's own fight resolves at once,
  its rounds lived by its wounds alone. Upgrade: let other agents step `Fight` when combat allows it.
- `Combat.cs:490` - the remainder of a fight the player left resolves at once (old: a background fight, a round a minute).
- `Combat.cs:636` - no Fighting skill added to blows. Upgrade: with progression.
- `Combat.cs:724`, `CombatAI.cs:37`, `Npc.cs:26` - relationship opinions are never written, so companions'
  help and pack cohesion ignore them. Upgrade: feed `Opinions` from the `WorldEvent` stream (plan decision 4).
- `Fire.cs:126` - fuel hours as a person reckons them (`Fire.FuelHours`, the NPC's camp fuel and remembered fire hours)
  ignore a stone pit's efficiency and a small fire's slower burn (both under 15%).
- `Knowledge.cs:89`, `Npc.cs:883`, `Map.cs:118` - no sight rays or fog of war: everyone's sight is `Sight.Visible`, a fixed
  5-tile radius blind to terrain, weather and dark, and NPC threat checks read herds' true positions.
  Upgrade: port old `GameMap.UpdateVisibility` behind `Sight.Visible`; nothing else needs to change.
- What an NPC still reads from the world rather than its beliefs, deliberately (plan decision 7; the camp's fire
  and stash now come from memory out of sight, `Npc.CampFuelHours`): `Npc.cs:714` the forage pool kind and full
  density of a remembered tile (the land itself, fixed content: "the look of the country"); `Npc.cs:1046` passability
  for its walks (terrain, cliffs and ravines, which never change and every map shows); the crossing off its own tile
  and off camp, snow and mud included (`Npc.MinutesOff`: felt underfoot, and camp's ground is the ground it lives on),
  and the weather overhead (felt, not seen); herds within 1-3 tiles (the sight-rays entry above).
  Upgrade: route the first two through `Beliefs` once fog of war means a person can not know the ground.
- `Npc.cs:957` - every NPC trip and every stint of work away from camp is held to one budget (`Npc.Afford`: home
  warm enough, rested enough, by the home-by hour), but it is reckoned by its walking chill where it stands
  (`WalkingCoolingFPerHour`, work counted as walking, the clothes' stored heat in, a straight line from the rate
  now) and a walk of the first crossing off its own tile, the rest at the going off camp (`Npc.WalkMinutes`), not
  along the path. Rough ground between camp and the goal is only found on the way, so now and then a forage trip
  set out on is given up one tile out (seed 17: once in 16 days). Only `Stranded` and the one-way warming walks
  (`Npc.WarmingWalk`, to makings) go by the path (`Npc.RouteMinutes`, `Npc.cs:1177`). Upgrade: reckon every trip's
  time and chill along the path (one BFS per candidate tile, so cache routes per decision first).
- `Npc.cs:285` (`Warm`) - cold beside a small fire with no wood, tired, by day: the trip budget refuses the walk for
  wood while its core is under the wanted line (the walk home would bring it home colder than it wants), so it keeps
  the fire fed by groping the camp ground fifteen minutes at a time (seed 17 scenario: core 96.7 to 95.8 over four
  hours, energy 0.35 to 0.10). The wood plan's half-hour fallback no longer stands in for the walk, but the outcome
  over those hours is about the same. Upgrade: weigh the walk for wood against what staying costs (the fire's hours
  left against the trip's), rather than against the wanted line alone; or warm first at a fire built up from the
  reserve, then go.
- `Npc.cs:517`, `Npc.cs:533` - `HuntKcalPerHour` uses a fixed 40% kill chance for any species and caps meat at pack
  room; which herds a lone NPC takes on is by species role (`Npc.WouldHunt`: fleeing prey whenever food is wanted,
  dangerous prey only starving, nothing over 2 t alone), not by odds. And it only looks 3 tiles out: on the 16 review
  seeds no caribou herd ever comes that close (nearest 26-132 tiles over 30 days on seeds 17, 42, 123, 5), so in 60
  days no NPC hunts at all. Upgrade: with NPC hunting (`rebuild-npc-hunting.md`), a kill-odds estimate from the
  combat model, and tracking or remembered herds (`Sighting`s) to reach them.
- `Npc.cs:389` (`ColdBed`) - beside a cold pit with nothing to light it, an NPC sleeps a full stretch only if the
  cold will not take its core under the freezing line, but it reckons that from the weather now: seed 11 (day 30,
  before round four) lay down at 00:46 judging 120 minutes safe, a cold rain front came through 25 minutes later and
  it slept through it, core 96.4 to 91.7. Its two upstream causes are fixed (`Stranded` no longer moves camp a tile
  from its own fire and shelter; the walk back to makings is a warming walk, not held to the trip budget). Upgrade:
  wake from a fireless sleep when the weather turns (re-decide hourly).
- `Npc.cs:957` - a warming walk (to makings, or from a camp with no fire to the trees) is judged against a lethal core
  line (`LethalCoreF` 93.5) and the energy the walk takes, not the critical lines (`FreezingCoreF`, `RestCriticalPct`)
  the review suggested: seed 11's state is at the critical rest line exactly (0.10), and an NPC already under the
  freezing line by a dead camp is the one most in need of the walk. Revisit with playtests.
- `Knowledge.cs:315` - a carcass going (butchered away) is not a notable journal entry: `Butchered` is a step's tally and
  is not filed. Upgrade: a once-only `CarcassGone` event when one leaves its tile.
- `Content/accidents.json` (Sprains) - old sprains also added SprainedAnkle / Clumsy effects; there are no such effect kinds yet.

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
- `Shelter.cs:407`, `Skills.cs:62` - Crafting teaches but pays nothing (crafting has no failures to spare yet); Cooking pays
  in speed only (cooking has no bad results yet). Upgrade: crafting luck and cooking quality (short-loop feature 8).
- `Odds.cs` - odds come as factors for fire-starting, throws, injury on hazardous ground and forage yield. Not yet:
  stalking detection (`Combat.DetectionChance`, a power curve the stalk redesign replaces), dodge, shove and the flat
  90% blow (no player choice rides on them yet), butchering yield (one product per resource; a sheet when butchering
  becomes a trickle bar), a forage find's own chance (it hangs on a luck roll made inside the minute).

## Shelter and bedding

- `Shelter.cs:240` - snow shelter melt rate (0.01 of each level per hour per °F above freezing) is invented;
  the old hook was a stub. Upgrade: tune in playtest.
- `Shelter.cs:293` - old bedding `HasWindProtection` / `HasGroundInsulation` flags dropped (nothing read them).
  Upgrade: give them numbers when a sleeper's wind is modelled.
- `Shelter.cs:90` - the camp's Overhang and bedding are placed from code by `World.New`; the camp is not a content site.
  Upgrade: make the starting camp a `Content/locations.json` site, so `Map.PlaceSiteShelters` places them.
- `Sim.cs:254` - a known physics question: a shelter's wind cover cuts both the wind chill and the body's own wind
  (convection, wind through clothing, ambient drying), as a fire pit's walls do, but a tile's wind factor reaches
  only the chill (old game). One of them is wrong. Drying by a fire is decided: it reads the open wind less the
  pit's walls, never the shelter's (old game parity, `SurvivalContext.FireDryingWindLevel`).
  Upgrade: decide what a tile's wind factor does to a body.

## Performance and portability

- `Sim.cs:279` - every agent steps every minute. Upgrade: distant agents step hourly at hour-aligned minutes (plan decision 3).
  Build step 3 part B. A populated year is about 23 s on seed 17 (1.9 s a sim month, up from 1.2: more herds live,
  since prey renew instead of dying out, plus the carrion flocks).
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

## Emergent-world substrate (build step 2)

- `Access.cs:27` - `Access.Wired` is `Gate.None`: buried (no Digging), guarded (a predator herd on the tile, unless
  confronting), frozen in (no Striking or Cutting) and across water are implemented and tested with `Gate.All`, but
  none was enforced before, so none is on. Only "out of reach" (anything not on your tile or a neighbour) always
  applies, and no caller passes another tile yet. Upgrade: switch a gate on in `Access.Wired` with the step that
  wants it (guarded carcasses, digging out a stash), and re-baseline its pins.
- `Tags.cs:49` - tags with no state to read yet: Sealed, FrozenIn, Elevated, Unstable (moves to edges), Occupied by
  small game (not a density yet), Buried for a carcass, stash or body (no placement elevation on TileState).
  Upgrade: derive each in `Tags.Of` when its system lands.
- `Tags.cs:29` - capabilities come only from working gear rows. A burning torch (a fire carried, not gear) gives no
  Light, meat gives no Bait, nothing gives Smoke. Upgrade: add those sources when the first verb needs them (step 4).
- `Things.cs:8` - `ThingKind.Sign` and `ThingKind.Item` are reserved: no stored signs exist, and carried gear has no
  id (a list position shifts). Upgrade: ids for stored signs (step 3/5) and for gear (step 4's first item verb).
- `Signs.cs:10` - a stash, fire pit, shelter or felled stand keeps no minute of its making, so its sign's
  `MadeMinute` is null (a body now has its death minute and cause). Upgrade: a made minute beside each Cause.
- `CombatAftermath.cs:76` - the side's-first-fighter cause is only the fallback now, for a body no blow of the fight
  landed on (each body keeps its last striker and weapon, `BodyState.WoundedBy`). A wounder is forgotten once the
  animal has mended; a person keeps theirs for good, and it names their death only if the wounds were mortal.

## Emergent-world kill chain (build step 3, part A)

- `Tags.cs` (Beacon) - the carrion flock circling flesh sets `Tag.Beacon`, read by `Report` ("ravens circling"),
  but nothing reveals a beacon at range yet: `Knowledge.Glance` sees a 5-tile diamond. Upgrade: part B's computed
  reveal reads `Beacon` (and `Fauna.CarrionRangeTiles`) for the find's vantage term.
- `Renewal.cs` - every renewal number is a judgment (the old game had no calving, recruitment, arrivals or starving
  herds): seasons, birth rates, capacity per forage score or prey kg, valley capacity, starvation after 14 days for
  wolves, arrivals at 5% a day at the full deficit. Tuned on five seeds over two years. Newborns are full-grown
  bodies (no growth model). Capacity reads generation's forage, not today's grazing. Saber-tooths neither breed nor
  starve (they forage); cave bears breed but never arrive; mammoths breed slowly and never arrive.
- `Renewal.cs` (Arrive) - arrivals land anywhere on open land outside camp's safe zone, packs over other packs'
  land included, not at the valley's edge or along a migration route. Upgrade: arrive from the map's edge and walk in.
- `Remains.cs` - skeletal at 240 effective hours for 70 kg (cube root of mass), scatter after 240 more, bones gone at
  1440 effective hours from death: judgments (the old body was skeletal at 72 and nothing ever went). Bones in a hard
  frost last up to ~600 days. A grave (no burial yet) should be skipped in `Remains.Hour`.
- `Remains.cs` (Scatter) - a dead person's kit goes into the tile's stash when the skeleton scatters, not strewn by
  whatever ate them (interaction table: "pack eats a body, scatters its kit", step 5).
- `Signs.cs` - bones that weather away leave nothing: no kill site sign yet (step 5's stored signs).
- `Carrion.cs` - the flock's members are small quadrupeds (no bird body plan) and leave no prints or wear (it flies).
  It raids at most about once per eight unwatched hours, 0.05 kg a member. Upgrade: split ravens (beacon) from
  wolverines (raiders) if the information side needs separate tuning (design open question 4).
- `Nearby.cs` - the index cannot see an agent replaced by hand in `World.Agents` under the same id (count and next
  id unchanged); every path in the sim makes or removes agents through `Agents`, which it does see.
- `Report.cs` - a day-10 report is about 17 ms (min of 15 runs, seed 17; HEAD before this step 15.7 ms by the same
  measure), not the 9 ms target: `Things.At` and `Signs.At` over every tile are most of it. Upgrade: skip tiles with
  no state, agents, fire, shelter or walked edge, if part B's validation runs it per settle.
