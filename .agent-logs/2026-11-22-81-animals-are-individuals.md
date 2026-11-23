# #81: Animals are individuals; herds are derived

Hypothesis:
- Changes: `Agents` (Spawn makes one animal, SpawnGroup makes N familiar ones; SplitOff goes),
  `Fauna` (HerdMind per animal with sparse Familiarity; cohesion: follow departing kin, seek kin when
  alone, a drift away from kin chosen 1 + cohesion times less often; lame split gone; pack hunts and
  noticing read the derived herd), `Combat` (one `Side` read replaces member counts and
  `SendMembers`), `Renewal` (births and arrivals spawn agents; per-animal starve, thin), `Populate`,
  `Memory` (one sighting per derived herd), `Report`, `Verbs`/`LocationFeatures` (one Hunt per herd),
  the Web observer, `Save` (format 21, fixture regenerated), species content (`closedGroup` for wolf
  and hyena).
- Affects: herd movement (each animal decides, groups hold through following), track and trail head
  counts (now one stamp per animal crossing, summing to what the herd made), pack-hunt sides, prey
  noticing, boldness from numbers, renewal per animal, the player's stalk (a herd's weakest), what
  NPCs and the player remember seeing, the populated budget (baseline measured today, seed 17, 30
  days, Release: 4064 MB, 5.1 s, 8249 moves, save 886 KB, 29 herds).
- Stays green: TracksTests, CombatTests' fight rules, NpcTests, SaveTests (after regenerating the
  fixture), HerdGoldenTests' noticing rates (group sizes now derived), the fast suite.
- First red: a new `IndividualsTests.APopulatedWorld_HasOneAgentPerAnimal` fails on herds of 3-15
  members.

Revised (review): every animal rolling today's drift moves a herd N times as often → an animal's drift chance is divided by the animals in its herd, and by (1 + cohesion) when the step's tile has no kin on it or crossing to it; the herd as a whole then drifts no faster than today's herd did.
Revised (review): the cohesion cost was judged on the tile the animal stands on → judged on where it is going: no kin there or inbound costs (1 + cohesion); a lone animal with kin crossing toward it waits.
Revised (review): "weighs following against staying" → with today's state mind there is no minutes cost to weigh, so it follows unless its own state holds it (feeding, hunting, a fight, too lame); a weighing that makes followers stay behind a lone leader leaves herds that never move (HerdLongRunTests). The planner tickets turn this into a real cost comparison.
Revised (review): a sighting keyed by the herd's lowest id would read as a new herd whenever that animal steps off → a glance replaces every remembered sighting that shares an animal with the herd it sees, and "nearer" compares against those.
Revised (review): wolves deciding one after another could start a second hunt in the minute their packmates fought → a wolf whose side fought this minute does not hunt.
Revised (review): Side excludes animals another fight holds, so a drawn side never fails Combat.Admit.
Revised (review): starve and thin now roll per animal; thinning loses the animal that rolled, not the herd's leanest (no herd to pick from). Body starvation replaces both in #83.
Revised: seams decided without a person (unattended run): seam 1, the world (World.New or a hand-built world with Agents.Spawn/SpawnGroup, Sim.Advance, read the world), for every criterion; Combat.Side is read only through fights it draws, not tested directly; the 30-day budget test for allocation; SaveTests and the fixture for the format.
Revised: the 30-day budget allocated 16 GB (4x) with one agent per animal; most of it was `Body.GetCapacities` and `Effects.CapacityDeltas` allocating arrays on every call, which every animal's life and legs read every minute → capacities on the stack, no deltas array for a body without effects, `Sim.IsAlive` without LINQ, and the per-minute kin reads count instead of listing; 4.4 GB now.
Revised: a pack's guard held a carcass against the pack's own wolves (Tags.Occupants left out only the viewer) → it leaves out the viewer's kin.
Revised: a fleeing herd scattered, each animal drawing its own way from the threat → an animal fleeing takes the way its kin are already running (Fauna.Flee reads Following).
Revised: CombatFlightTests.NotCaught scanned seeds 1-60 for a caribou outrunning two wolves; with each wolf its own agent the first such seed is 158 → the scan is 150-450.
Revised: HerdTests' lame-member, split-off and member-list tests (ALameMember_IsLeftBehind, Gate_AWoundedAnimalThatGetsAway_IsItsOwnHerd, AFleeingPack_RunsOneTile), CombatTests' SendMembers/SplitOff/eight-member tests, and KillChainTests' member counts → rewritten as behaviour over individuals (the lame one stays and the herd moves on; the escapee runs alone and its herd stays; the pack flees one tile together; a side draws only familiar wolves; births, starvation and thinning counted in animals). CombatTests.SplitOff_MovesOneMemberOut_AndRefusesMidFight is deleted: there is nothing to split.
Revised: the cohesion gate failed on seed 23 (megaloceros alone 30% of the time; seed 17 at 9.5%) because Populate draws megaloceros herds of 1-3 and arrivals of 1-3: a herd of one is alone from minute zero with no kin within sight, while every herd placed together held together for the 60 days (logged per seed) → megaloceros start and arrive two or three strong. A population choice, reversible, flagged in the PR for Jack.
Revised: CoreTests.InvalidInput_Throws stepped agent 99 as one that does not exist; a populated valley now has more than 99 agents → it steps the next id the world would make.
