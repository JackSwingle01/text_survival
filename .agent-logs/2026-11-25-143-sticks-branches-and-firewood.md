# #143: Sticks, branches and firewood

Hypothesis:
- Changes: `Resource` (Kindling → Stick; Pine/Birch/Oak → PineBranch/BirchBranch/OakBranch and PineFirewood/BirchFirewood/OakFirewood), every content table naming them (items, fire, names, forage, foraging, loot, discoveries, locations, terrain felling, shelters, recipes, species), `Pools.Chop` (a felled tree stays as a trunk in `TreeState`, cut into firewood piece by piece), `StockItem`/`FindItem` gain a per-item `RequiresTool` (the pool-level one goes), `Pools.WorkStock` takes the worker's pack, a new `Deadfall` find, a starting camp pile (`World.New`), `Npc` kindling rules, `Report` fire hours, Web icon/scenario, the save fixture.
- Affects: forage yields (branches where logs were), felling (today the whole tree lands in the pack at once and the stand's MinutesWorked resets; now it comes down at 40% of MinutesToFell with sticks and tinder, and firewood comes one piece per remaining share of the minutes, the trunk saved on the tile), axe-gated harvests (Ancient Hardwood was refused whole without an axe; now gives branches by hand), wood finds regrowing (Fallen Tree's wood regrew in 720 h; now wood never regrows), discovery draws (Deadfall appended at the end of the Forest and Clearing draws so earlier draws keep their rolls), save format (enum changes; regenerate fixture, no migration per DR).
- Stays green: the fast suite once tests are renamed; ContentStrictTests; conservation tests; PieceProblems over every yield table.
- First red: `WoodTests.Foraging_AForestTile_GivesSticksAndBranches_NeverFirewood` fails to compile on `Resource.Stick`.
Revised (review): a trunk's pieces alone don't say their species on a mixed stand → TreeState keeps TrunkWood beside Trunk.
Revised (review): one cycle time per stock pays branches at the firewood rate → kept one cycle per pool; a find's branch units are few (3-5), so the by-hand rate is short-lived and bounded by the find, not by the clock.
Revised (review): Keep spills a full pack's firewood onto the ground → cutting stops with the pack full before a piece that won't fit, which stays in the trunk; the Gather is refused while the lightest trunk piece won't fit.
Revised (review): when to count Felled → at the fall, as today; HasTrees also counts a trunk lying there, and Chop cuts a lying trunk before felling.
Revised (review): a cutting interval off the middle count makes the total drift from MinutesToFell → the interval comes from the trunk's own piece count (TrunkPieces), so the total stays MinutesToFell and progress only rises.
Revised (review): the per-item tool must reach SiteStock items, the end check, Expected, ExpectedGatherAt, NPC offers and Report → all read the worker's tools.
Revised (review): firewood from a find wore no axe → a stock cycle that yields an axe-gated item wears the axe as chopping does.
Revised (review): the trunk-cut-up reason must finish, not interrupt → added to Sim's finishing reasons.
Revised (review): nothing keeps firewood out of recipes and shelters → a content test checks recipes, outcomes and shelter materials name no firewood.
Revised (review): a Wood enum was proposed for species → kept separate resources; anyOf lists in one find share one draw, with a content check that they are the same length.
Revised: the starting firewood pile at camp broke every test that sets a scene on an empty camp tile → TestWorlds.Bare also empties the camp's pile (TestWorlds.EmptyCamp); TestWorldsTests' Bare contract test and MapTests' "Tiles":{} check changed to say so.
Revised: a whole tree's firewood (16-25 kg) outgrows a 15 kg pack, so the old chopping tests stopped "your pack is full" → WorkTests give the chopper two large bags (RoomForATree); the pack-full stop is its own WoodTests test.
Revised: changed pre-existing tests for the rename (expected strings "pine log" → "pine branch", "not enough kindling" → "not enough sticks", TreePools of firewood, ChopResult in PoolsTests/ForecastTests/ReportTests, the felling tests rewritten for fall-then-cut, the per-item RequiresTool in PoolsTests/WorkTests, ContentStrict rows renamed).
Revised: Report.cs's "Logs" local function was a ratchet entry in WhereThingsGoTests → inlined into TreeFireHours, entry taken off the list.
Revised: Pools.cs went over its comment ratchet (12% > 11%) → shortened the new comments.
Revised: Phase3GateTests' mixed run no longer felled a tree (seed 17's run now meets a predator mid-chop) → the run goes back to the axe up to four times after an interruption.
Revised: stick and tinder forage hints said "kindling" → "sticks" and "tinder"; branches "branches", firewood "firewood".
Revised: TreePool.Wood and the felling table are keyed by firewood resources (PineFirewood...), not by a species enum.
Follow-up: Npc.Offers still treats a tree as coming down whole (no trimming to the want), and does not plan for a trunk piece too big for its pack room (Do returns null on the PackFull refusal and it moves on); worth a look if the NPC bench shows idle chopping.
Revised: the fallen_log detail is renamed "Deadwood" (its lines said log and kindling), and the Fallen Tree's lines say "woodpile", not "kindling pile"; the CLI takes the new names (`fuel birchbranch 3` in Phase2GateTests).
Revised (review): Chop could cut more pieces in one call than the pack takes, spilling them → Chop takes the cutter's room and leaves a piece over it on the trunk.
Revised (review): the pack-room sum was written twice in Work, and the per-item stock loops three times in Pools → Work.RoomKg and Pools.ItemsLeft.
Revised (review): Content.Tables still named the felling weights logKg → firewoodKg; the architecture doc's forecast row names Pools.ExpectedChop.
Revised: the NPC bench showed an NPC at a camp pile of firewood swapping birch and pine back and forth for hours (Take made room by putting down the other wood its fire wanted) → Take keeps what the same requirement wants; NpcTests.AnNpcAtACampPile_DoesNotSwapItsFuelBackAndForth (red without the fix: 14 put-downs in 30 min).
Follow-up: the NPC bench (seeds 1, 17, 42, 303, 60 days) has the NPC dying on all four on this branch, and on origin/main 0d98680d too (thirst and cold, days 9-36), against the baselines branch's all-alive (main at c70b3c2b). The regression predates this branch; worth its own diagnosis ticket.
Follow-up: the sticks and tinder that fall with a tree go into the pack unchecked, so a nearly full pack spills them onto the ground (a kilo or two).
