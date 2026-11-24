# #155: Route search: look up trodden edges without boxing

Hypothesis:
- Changes: Tracks (IsTrodden reads a never-saved set of trodden edges on World, kept by Tracks' own writes), World (the set's field)
- Affects: Trip.Pricer.Minutes and Trip.Terms (whether a crossing is priced on its trodden halves); every result stays the same, the save format and fixture unchanged
- Stays green: RouteSearchTests, TracksTests, TroddenEdgesTests, SaveTests, SaveFixtureTests, determinism tests, fast suite
- First red: a TracksTests test that IsTrodden allocates nothing fails on 32 B a call

Revised: a probe shows SortedDictionary<EdgeId,_>.ContainsKey allocates 32 B a call (the boxed key in its null guard) only until the JIT tiers it up; after 2 M calls it allocates 0 B, so the 74 B a tile is mostly tier-0 code in a once-warmed search and a 60-day gate should barely move → build the lookup anyway (the issue asks for it and the report shows it), measure the gate before and after on main and this branch, and say so in the PR
Revised: World.Trails' type is in the save format's hash (Save.Describe names each property's type), so a custom dictionary type would change the format → keep the SortedDictionary and add a side index (internal field, never saved)
Revised: tests and Scenarios write World.Trails directly, so the index can't rely on Tracks' writes alone → it is rebuilt whenever Trails' count differs from the count it was built at, and Tracks keeps it current on its own adds and removes; membership only, so overwriting an edge's amount needs nothing
Revised: TroddenAt/TroddenLevel's TryGetValue on an edge already known trodden stays on Trails: a value mirror could go stale on a direct overwrite, and it runs only for trodden crossings
Revised: scouted inline rather than by a sub-agent: the change is one lookup with two writers, all found by grep
Revised: steady state (30 searches in, the JIT tiered up) a long search allocates 71.2 B a tile on main and on this branch alike; what is left is the RouteMap's two result dictionaries (Trip.Routes), copied on purpose by #137 → nothing more for this ticket
Revised: the cohesion gate on main is already under its 406 MB band (seed 23: 251, seed 123: 327 MB a day, both passing) and this branch gives the same numbers → reported as measured, nothing tuned to move them
Follow-up: a long route search's steady 71 B a tile is the RouteMap copy (Dictionary<TileId,int> and Dictionary<TileId,TileId>); a flat-array RouteMap view would remove it, if the gate's allocation ever needs it
Revised (review): Tracks wrote Trails before it fetched the index, so every new or swept edge made the counts differ and rebuilt the whole set → the index is fetched before the write
Revised (review): the TrodEdges wrapper only passed calls through to its set → a bare HashSet<EdgeId> on World (TroddenEdges), and the comment now says what the count check misses (a remove and an add with no read between)
Follow-up: Phase3GateTests.MixedRun_SavedAndLoadedEveryFewSteps_IsByteIdenticalToAStraightRun (Slow) fails "no tree felled" on origin/main 5d2e9a9b as on this branch; matched by a "~Save" filter here, not related to trails
