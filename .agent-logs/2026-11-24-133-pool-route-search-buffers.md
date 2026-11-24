# #133: Route search reuses its buffers instead of allocating per search

Hypothesis:
- Changes: Trip.Routes and its Pricer (rebuild/Sim/Trip.cs), RouteMap's backing, a new internal scratch on World (not saved).
- Affects: every route search (Fauna.NearestRoute, Planner.Choose, Npc Mind.Routes); Npc's Mind holds several RouteMaps at once across later searches for one Decide, so RouteMap must own a copy, not a view.
- Stays green: ForecastTests Routes_*, TroddenEdgesTests, PlannerTests, the determinism tests, SaveTests and the fixture, the fast suite.
- First red: none expected; RouteSearchTests (a plain-dictionary oracle written first and green on main) must stay green through the change.
- Baseline, warm, caribou on World.New(17): short (60 min) 103 tiles 91,048 bytes (884 B a tile); long (whole reach) 5,097 tiles 3,681,952 bytes (722 B a tile).
Revised: re-entrancy, not threads, is the shared buffer's hazard (Sim is single-threaded by ArchitectureTests); no callback starts a search today → Routes takes the scratch off World while it runs, so a search begun inside a callback builds its own instead of corrupting this one.
Revised: RouteMap copies out the settled minutes (in settle order, so Minutes enumerates as before) and each settled tile's previous, presized; Npc's Mind holds several maps through one Decide, so a view was ruled out.
Revised: generation stamps (reached, settled, half) instead of a touched list; a wrap at int.MaxValue clears them once.
Revised: the pooled search alone only halved allocation (long search 3.68 MB → 1.76 MB); a probe showed Map.BaseTraversalMinutes allocating 276 B a call (TraversalModifier seeds a LegacyRng) on every half priced → cached per tile on GeneratedMap (immutable, pure function of the map), a change the ticket didn't name but inside its aim; long search now 378 KB (74 B a tile).
Revised: the Pricer's halves dictionary goes to the scratch; StepMinutes' one-crossing Pricer prices its two halves directly, with no cache.
Revised: MapGen.Neighbors' per-tile array replaced in the search by a static step table in the same N, E, S, W order.
Follow-up: Tracks.IsTrodden boxes its EdgeId in SortedDictionary.ContainsKey (32 B a crossing priced, even with no trails); a non-boxing lookup on World.Trails would take the long search's remaining 74 B a tile down by about half.
