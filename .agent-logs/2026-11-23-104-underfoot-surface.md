# #104: One surface: the ground's description, with every effect worked out from it

Hypothesis:
- Changes: Ground (Ground.cs: one public read `Ground.Underfoot(world, tile) → Surface`; Column builds a Surface;
  the world-side SurfaceNow/TraversalFactor/HazardDelta/WaterSeen/ContactWettingRate/SearchFactor/SnowDepthM go),
  a new Surface.cs (the record and the five effects worked out from it), GroundMemory (keeps its five-value
  projection under the name SeenSurface, built from a Surface), callers in Travel, Trip, Exposure, Pools, Tags,
  Clues, Memory, Needs/Planner/Work (WorldView.Ground returns Surface; GroundSeen goes), World (WaterSeenCache
  becomes a per-class per-minute Surface cache), Web/Host MapView.
- Affects: every ground read (crossing minutes and hazard, previews, exposure wetting, search, animal drinking,
  map surface, memory, buried tags, snow clues). The frozen-river hazard rule moves from Ground.HazardDelta to
  Traversal.SurfaceHazardDelta, beside the river ice it defers to. All numbers stay identical; save bytes too
  (GroundMemory stores the same five values), so no format bump.
- Stays green: GroundTests (goldens through the GroundState overloads, which now go through the Surface),
  SurfaceViewTests, TravelTests, TripTests, ExposureTests, PlannerTests, MemoryTests, SaveFixtureTests,
  determinism tests, GameHostTests, CommentHygieneTests.
- First red: SurfaceTests (new): each effect worked out from `Surface` equals today's Column effect over the
  golden script's states fails to compile (no Surface type).

Design:
- Surface(SnowDepthM, SnowPackingLevel, SlushLevel, CrustLevel, MudLevel, DampnessLevel, PondWaterM, PondIceLevel):
  each a description of the ground as it is; derived: Crusted, Iced, StandingLiquidM, IceM, WaterDepthM,
  HeightM, DrinkableWaterM. Effects are members: TraversalFactor, HazardDelta, SearchFactor(base, extent),
  ContactWettingRate(contact, wetness).
- The pond is carried as its water and frozen fraction (not liquid and ice depths) so liquid, ice, Iced and
  water depth come out by the very expressions the column uses today: bit-identical.
- Underfoot caches the Surface per ground class per world step (generalising WaterSeenCache); open water is
  per tile and stays outside the cache.

Revised (review): the FixedWeather setter clears GroundCache but not the per-minute cache, and tests pin the sky at minute 0 after World.New's glance has read the ground → the setter clears the Surface cache too.
Revised (review): a pond under two or more snow layers makes WaterDepthM + SnowDepthM differ from Column.SurfaceHeightM in the last bit → the Surface carries the column's own height of what lies on the ground (CoverDepthM); SearchFactor reads it.
Revised (review): the goldens compare within 1e-9, too loose to see a last-bit change → SurfaceTests assert exact equality against the column, on states with a pond under several snow layers, frozen and liquid, and on open water.
Revised (review): a memory-built WorldView (#87) could not fill a whole Surface → WorldView.Ground keeps the narrow GroundSeen, now built from the Surface (snow depth, drinkable water).
Revised (review): PondWaterM and PondIceLevel are the ledger's representation, not the ground as it is → the Surface carries StandingLiquidM, IceM and Iced, each by the column's own expression.
Revised: seams to confirm, unattended → decided: Ground.SurfaceOf(state, substrate) with each effect checked for exact equality against numbers recorded from main's column on 61 scripted states, and Ground.Underfoot(world, tile) checked against a SHA-256 of main's reads (every effect, the map's five values and the animal's water) over every passable tile of World.New(17) at three minutes. Both oracles were recorded from origin/main before any change.
Revised: tests called the world reads this ticket removes (SurfaceNow, TraversalFactor/ContactWettingRate/SnowDepthM on a world) → GroundTests, SurfaceViewTests, TravelTests, PlannerTests, MemoryTests and GameHostTests now read Ground.Underfoot and its members; SurfaceSummary became SeenSurface in MemoryTests and TravelTests' lake check (SeenSurface.Of(Underfoot)). Same assertions, same values. GroundTests' reads-store-nothing test lost its three per-effect calls, now one Underfoot read.
Revised: the GroundState overloads (TraversalFactor, HazardDelta, SearchFactor, ContactWettingRate) stay as one-liners through Ground.SurfaceOf, so the golden script and the physics tests run unchanged through the surface.
Revised: CommentHygieneTests holds a new file to 10% comments, and Surface.cs came in at 16% → its header shrank to one line and the comments on WaterDepthM, DrinkableWaterM and ContactCeiling went.
Revised: the 30-day run (HerdTests.ThirtyPopulatedDays_StayCheap, seed 17, Release) on main 9,749 MB (9,738 MB on an earlier run), 30.4 s, save 819 KB (839,076 bytes); on the branch 7,867 MB, 29.6 s, the same save byte for byte (SHA-256 8b9bd5c6…), the same 31,682 moves, 55 fight sides, 12 kills. The drop is the per-class Surface cache: every system now shares the per-step read that only animals' water had.
Revised (review): SurfaceViewTests' header still named Ground.SurfaceNow → now Ground.Underfoot.
Revised (review): SurfaceTests' comments and a test name told history ("recorded from main") → they say what the oracle is: the column's effects recorded before the surface existed.
Revised (review): HazardDelta repeated Crusted's threshold, as two ifs that were an if/else → it tests Crusted, in an if/else; same values (SurfaceTests exact).
Revised (review): CoverDepthM holds a height → renamed CoverHeightM.
Revised (review): GLOSSARY said nobody else reads the ground state, but Ground's tests do → "only Ground and its tests read".
Revised (review): the GroundState overloads of TraversalFactor, HazardDelta, SearchFactor and ContactWettingRate are pass-throughs to SurfaceOf → kept, so the golden and physics tests run unchanged as the ticket prefers; open question in the PR.
Revised (review): SeenSurface and GroundSeen are projections beside the Surface → kept: the ticket keeps memory's projection as today, and a memory-built WorldView (#87) cannot fill a whole Surface; open question in the PR.
Follow-up: nothing tests SurfaceCache directly (SetClassState then a read in the same minute); TestKit.EditGround scenes cover it only indirectly.
