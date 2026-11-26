# #124: Piles feed animals

Hypothesis:
- Changes: `Items` (one take operation, `Items.TakeFromPile`, replacing `MoveFromPile`, raising
  `PickedUp` with a `Cause`); `Scent` (a pile's scent, the strongest food in it, shared with a
  person's carried food); `Needs` (a pile on the sightings list, found only by scent or on its tile,
  kept until seen gone; a pile food source in `FoodAt`, public forecast `Fauna.PileSource`); `Fauna`
  (`Fauna.Eats` reads the diet's food list; `Raid` becomes feeding at a pile: eats what the diet takes
  until full, tearing a cut; `Eat` and `Idle` pick it like a carcass); `Scavenging` (raid roll, raid
  rate, `RaidFood` and "someone is keeping it" removed); `Work` (`PickUp` goes through the take).
- Affects: today ravens alone raid piles on a 0.002/minute roll, 0.05 kg a member a minute of meat
  and fish, never with a living person on the tile, ignoring room; wolves, bears and foxes never touch
  a pile; no animal plans toward a pile; a person's pick-up is the only `PickedUp`.
- Stays green: `KillChainTests` (bar the raid test), `FoodTests`, `PlannerTests`, `ScentTests`,
  `PredatorEngagementTests`, `SaveTests` after the fixture is regenerated, `MemoryTests`, `WorkTests`,
  `NpcFireTests`, the fast suite.
- First red: a new `PileFeedingTests.AWolf_EatsMeatFromAPile_AndLeavesTheBerries` fails: the wolf on
  the pile's tile does nothing to the pile.

Revised (review): `Access.Refusal` refuses anything over a tile away ("out of reach") → the pile forecast asks access only on the animal's own tile; a distant pile is read by diet alone (gates are off in play).
Revised (review): the planner's best rate stopped at max(graze, carcass), so a richer pile (nuts, 600 kcal a minute) would be cut from the search → the food ask's best rate is also FeedKgPerMinute x the richest kcal/kg in the species' diet list.
Revised (review): an animal's take raises one `PickedUp` a minute, each notable, and would push a camp's fire and deaths out of its 32-entry journal → `Journal.File` folds a take onto the tile's last entry when it is the same taker's take of the same resource.
Revised (review): a forecast of 0.1 kg of the richest food overstates a thin pile → one bite rule, richest first up to FeedKgPerMinute, shared by the forecast and the feeding.
Revised (review): a separate Raid activity misses `Cohere`, `Fauna.Eating` and the observer, which know only Feed → feeding at a pile is the Feed activity: it eats carcass meat, then a body (opportunists), then the pile; `Raid` and its save registration go.
Revised (review): keying a pile sighting by pile id adds nothing (one pile per tile, a refilled pile gets a new id) → `Sensed.Pile` is a flag on the tile.
Revised (review): scanning `World.Tiles` each Sense would walk every tracked tile → an unsaved index of pile tiles, as `Carcasses.TilesWith`, kept by `Items.PileAt` and `Items.TidyPile`.
Revised: the ticket says "a sleeper is judged through the existing threat assessment", and `ResolveToward` has no sleep term → no new term: `Combat.WouldEnter` decides, and the starving bear criterion passes through boldness and fire alone.
Revised: animals have no stomach; full is `RoomKcal` reaching zero, the rule `Nourish` and carcass feeding already use → a feeding minute never takes more than the room left, and idle feeding at a pile stops at `SatedHunger` as at a carcass.
Revised: a whole cut can overfill a small animal → an animal's take tears what it eats off the lightest cut; a remnant under the resource's least piece goes with it.
Follow-up: no sleep term in the threat assessment: a hungry, not starving bear beside an awake person may still come for a pile. Needs a decision on whether sleep lowers the judged threat (spec "People on the tile").
Follow-up: a raven or fox at a carcass beside a person is not checked by `Combat.WouldEnter`; the spec's "carcasses follow the same rule" would extend the pile's check to carcasses.
Revised: the first strip parked the player in a corner, and a starving wolf came for them instead of the pile → the pile tests put a person on the strip only when the test is about people.
Revised: a bear at hunger 0.6 has no food due, so it eats from its tile's pile through `Idle` and never senses it → the save test's bear starts at 0.85, so the pile is on its sightings list.
Revised: changed pre-existing tests: `KillChainTests.Ravens_RaidAnUnwatchedPile_ThroughAccess` became `Ravens_FeedAtAnUnwatchedPile_ThroughAccess` (Feed, and the people refusal's new reason); `SaveTests.AHerdMidRaid_SavesAndLoads` became a mid-feed save and load that eats on the same; `TheFormat_ReachesEverySavedType` reads the Feed discriminator; `ConservationItemsTests` raids became feeding at a pile, bounded by the diet and 0.1 kg (+0.05 kg remnant) a raven a minute; `SubstrateTests` and `KillChainTests` take through `Items.TakeFromPile`; `WhereThingsGoTests` drops Raid; the Access call count moves from Scavenging.cs to Fauna.cs.
Revised: the regenerated fixture moved its clock from 189 to 145 → traced `SampleWorld` step by step on main and on the branch: both end at 145, so the committed fixture was from an older run, not a change in behaviour.
Revised: the comment ratchet put Activity, Fauna, Needs, Scavenging and Scent over 10% → shortened the new comments and two old ones.
