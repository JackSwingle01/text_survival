# #192: Animals eat from fires and racks

Hypothesis:
- Changes: `Stations` (what sits at a tile's stations, working inputs as the input and finished results, read as one
  inventory; an animal's eating off them, tearing a bite off a piece, a piece eaten whole ending its transformation,
  a finished amount eaten away freeing its places; the station feature an animal's access check reads); `Items`
  (`TakeFromPile` takes into a mouth from the pile, then from the tile's stations, one `PickedUp` per source with its
  cause; the exposed food of a tile, pile and stations, for the readers); `Fauna`/`Needs` (`PileBite`, `PileRefusal`,
  `SensePiles` read the tile's exposed food, and sensing walks station tiles too); `Scent` unchanged
  (`OfFoodIn` of the exposed inventory, multiplier 1); `PickedUp` gains where it was taken from, and `Narration`
  says "from the rack" / "from the fire".
- Affects: today food at a fire or on a rack is never touched by any animal, never smelled, never sensed and no
  planner source; `ReadyAmounts` leaves only through `TakeFinished`; every animal take says "from the pile".
- Stays green: `PileFeedingTests`, `ForecastTests`, `StationTests`, `RackTests`, `StationSteppingTests`,
  `ConservationItemsTests`, `ConservationMakeTests`, `MemoryTests`, `SaveTests`/`SaveFixtureTests` after the fixture
  is regenerated (PickedUp's new field), `WhereThingsGoTests`, `CommentHygieneTests`, the fast suite.
- First red: a new `StationFeedingTests.AWolf_DownwindOfMeatOnARack_ComesAndEatsIt` fails: the wolf neither senses
  the rack's tile nor eats from it.

Revised (review): fire deters only through people on the tile (`WouldFeedAmong` → `Combat.WouldEnter`), so an unattended strong fire keeps no wolf off a rack; the ticket says "nothing new is added for stations" and the predators spec's fire keeps animals off food "the way it keeps them off me" → the criterion is tested at camp with a sleeper, with and without a strong fire, so the fire is the cause; no new deterrence on empty tiles.
Revised (review): `SensePiles` read `world.Tiles[t].Pile!`, which throws on a station-only tile → it reads the tile's exposed food.
Revised (review): `Fire.PitAt` walks every pit ever lit, once per bite, and may miss the station a walk of `world.Stations` found → station food is read by walking `world.Stations` and `world.Racks` by tile, with a fast path when there are none.
Revised (review): Fauna.cs (9.95%), Needs.cs (9.6%) and Items.cs (14.6 of 15) are at their comment limits → the exposed-food reads live in `Stations`, with their comments there.
Revised (review): `Stations.Eat` returns what it took per station kind, and `TakeFromPile` raises the events, so the cause and Taken rule stay in one place.
Revised (review): a partly eaten input keeps working as the smaller piece, in its place in `Working` (the NPC reads `cooking[0]`), with its progress: "a transformation whose input was eaten is gone" read as eaten whole; a piece torn under its least piece goes whole.
Revised (review): a merged finished amount holds all its places until it is all gone (`ReadyAmounts` cannot say how many places a part holds); the ticket's criterion is eating all of it.
Revised: seams, decided without a person: the world (Sim.Advance, Sim.Step, Fauna.Decide) as PileFeedingTests uses, Fauna.PileSource for the forecast, Memories.Receipt for what the player reads, the station verbs for room.
Revised: with a sleeper and no fire, the starving wolf comes for the sleeper, not the rack (`ComeForPeople`), so "with no fire it eats" cannot be shown beside a person → the fire test shows the wolf on the tile with no fire, and with a strong fire off it, the meat whole and no fight, though it smelled the meat.
Revised: a lone wolf is sated by about 0.4 kg of cooked meat → the cold-fire test asks that the wolf ate from the cut, not that it emptied the fire.
Revised: the receipt test's bear in sight ends the player's Wait at once, and a receipt needs a tile the player stood on → the trip away is `Sim.Advance`, and the camp is looked at first.
Revised: `WhereThingsGoTests.SavedEnums_AreWrittenByName` refused `StationKind` saved as a number in `PickedUp.From` → `StationKind` is written by name.
Revised: changed pre-existing test: `ConservationItemsTests.FeedingAtAPile_TakesOnlyWhatTheDietEats` scenes may add a cold pit on the tile with raw meat on it and a cooked cut waiting, bounded by the same rule.
Revised: the regenerated save fixture is byte-for-byte the same: `PickedUp.From` is left out when null and the sample has no take from a station; the format hash changes by itself.
