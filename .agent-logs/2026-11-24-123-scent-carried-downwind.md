# #123: Scent, carried downwind

Hypothesis:
- Changes: new `Sim/Scent.cs` (strengths: agents, carcasses, food; wind factor; reach) and the
  `Senses` an animal has now (sight budget x perception, sense of smell, the wind) beside it;
  `Fauna.Sense` (Needs.cs) finds carcasses and agents by sight or scent; `Fauna.PeopleSensed` and
  `WouldComeForSomeone` read the same `Senses` instead of the flat `detectionRangeTiles` radius, which
  goes from the species data; `species.json` gains `smellFactor` and `scentLevel` (required),
  `items.json` gives every food a `scentLevel`; `Combat.Boldness` loses `targetCarriesMeat` (CMB-16).
- Affects: a pack comes for a person by a flat 8-12 tile radius today (no sight); meat-eaters find
  carcasses only by sight; prey sense predators and people only by sight; `Carcasses.Scent` has no
  caller; a person carrying raw meat or fish gets +0.1 boldness against them, cooked gets nothing.
  Prey flee only a threat on their tile (or beside it, with no source), which sight on open ground
  always sees, so scent changes prey flight only where sight is short (forest, blizzard).
- Stays green: SightTests, PredatorEngagementTests, FoodTests, PackTests, IndividualsTests (fast),
  CombatGoldenTests, ContentStrictTests, the fast suite.
- First red: `ScentTests.Downwind_ReachesTwiceAsFar_UpwindHalf` fails to compile (no `Scent`).

Design:
- Wind heading names where the wind comes from (as a weather report's "north wind"); North is y-1.
- Wind factor 2^cos(angle between the way the wind blows and source->smeller): x2 straight downwind,
  x0.5 straight upwind, x1 across and in calm air.
- Reach tiles = ScentReachTiles (6) x strength level x sense of smell x wind factor; a source is
  smelled when the straight-line distance between tile centres is within it.
- A person's strength: base + 0.9 if bleeding + 0.8 x Bloody severity + the strongest carried food's
  level x min(1, kg), capped at 1. An animal's: its species base plus its blood terms.

Numbers (D: judged, sources):
- ScentReachTiles 6 (600 m at 100 m a tile) with wolf smell 2.5: a fresh carcass (0.8) reaches a wolf
  24 tiles (2.4 km) straight downwind, Mech 1966 (Isle Royale wolves scented moose about 2.4 km
  downwind; R, recalled).
- Sense of smell: bear 3 (bears out-smell dogs, R), wolf and hyena 2.5, fox 2, caribou, bison,
  megaloceros 1.5, mammoth 1.2, hare 1, human 0.3, raven 0.3 (ravens find food by sight, R). D.
- Base scent: human 0.1 (spec: low for people), raven 0.05, hare 0.1, fox 0.2, wolf, caribou,
  megaloceros, bison 0.3, bear, hyena, mammoth 0.4. D.
- Food: raw meat and raw fish 0.6, cooked meat and cooked fish 0.5, dried meat and dried fish 0.3,
  honey 0.1, berries 0.1, dried berries 0.05, nuts and roots 0.02 (spec's order). Bleeding 0.9,
  Bloody 0.8 x severity (spec: among the strongest). D.

Revised (review): prey flee only a threat on or beside their tile, which sight always sees on open ground, so scent cannot make caribou flee sooner there; the hunt's in-tile detection could carry scent but that retunes stalking, a balance choice → criterion 3 is shown where sight is short (a blizzard among trees), and the flight distance question is a Follow-up for Jack.
Revised (review): a straight-line reach misses diagonal sources in a Manhattan scan → scent reach is Manhattan, as sight's prefilter and flight are.
Revised (review): scanning every animal to a bleeding source's reach costs a bear Within(36) every 5 minutes → an animal's scent is its species' base scent (the spec sums blood and food for people only), agents are scanned to max(sight, the largest species scent's reach), and people, few, are scanned from their own list on the Nearby index.
Revised (review): a quarry herd known only by scent left a sighting on every tile it crossed → a quarry sighting is dropped when its tile is seen or within reach of that species' scent.
Revised (review): Heading is never Calm; WindDirection is, under 3 mph → the wind factor reads WindDirection.
Revised (review): Scavenging.Attractor finds meat by a flat 10-tile radius outside Sense → left as it is (ravens' sight x2 would reach 22 tiles; #124 reworks what scavengers feed on); Follow-up below.
Follow-up: Scavenging.Attractor (MeatSensed, CarrionRangeTiles) still finds carcasses by a flat radius, not by sight or scent.
Follow-up: the UI prints the wind as "North"; the Sim reads it as where the wind comes from. Say "wind from the north" in the web and CLI views.
Follow-up: Carcass.ScentBonus and ButcherModeProps.ScentIncrease are Deltas by principles-code.md; rename them.
Revised: seams decided without a person (unattended run): the spec's seam 3 (`Scent` and the animal's `Senses`, as numbers, `ScentTests` in the manner of SightTests: reach downwind against upwind, calm, strength, sense of smell); seam 1, the world through `Fauna.Decide` (a wolf downwind of a bleeding person comes for them, caribou flee, carried cooked meat); content through `ContentStrictTests`. No cache multiplier yet: caches are #125.
Revised: CaveBear and SaberTooth have rows too → smell 3 and 2, scent 0.4 (D: a bear's nose; a big cat hunts by sight).
Revised: IndividualsTests.AWolfBornIntoAPack_IsOfThePack read Fauna.DetectionRangeTiles, now gone → its spacing is written as the 20 tiles it was (12 + 8).
Revised: a blizzard among trees still shows a caribou the next tile (the ray pays only its own tile's half) → criterion 3's test is in a whiteout among trees (11 x 0.1 = 1.1 tiles of budget).
Revised: with no plan, prey that sensed a threat beside them lay down (GiveWay set FleeFrom to the threat's tile, and Flee only moves from its own tile), against GiveWay's "it runs from a threat it senses beside it" and the spec's "flee if threatened" → prey step a tile away from a threat beside them, as a pack does from a rival; one on their tile they flee as before.
Revised: budgets measured side by side, Release, seed 17: HerdTests.ThirtyPopulatedDays_StayCheap 6,177 MB on this branch against main's 6,131 (+0.75 %, under its 6,600 cap); Phase2GateTests.ThirtyDays_Headless 6,658 MB against main's 6,679 → scent does not make sensing measurably dearer, so nothing is re-baselined.
Revised: the cohesion gate passes on all four seeds (17, 23, 42, 123): 223, 240, 367 and 394 MB a day against the baselines branch's 232, 251, 391 and 327, under the 406 ceiling.
Follow-up: Phase2GateTests.ThirtyDays_Headless_CompletesAliveAndSmall fails on main already (6,679 MB against its 6,100 cap, Release, seed 17); it is Slow, so PR CI does not see it.
