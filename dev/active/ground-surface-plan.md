# Ground Surface: Snow, Wetness, and Standing Water

Status: proposed implementation plan. Replaces the original Ground Snow & Buried Discoveries plan. No runtime implementation is included in this document.

## Outcome and scope

Weather changes the ground, and the ground changes travel, footing, discovery, and player wetness. Snow accumulates, compacts, melts into water, and covers discoveries. Water infiltrates ground, pools when supply exceeds infiltration, evaporates, drains, and freezes over time. Conditions persist after precipitation stops.

Implement one concrete `GroundSurface` owned by each `Location`. It hides a small ordered layer model behind simple queries and operations. Callers never interpret layers or material combinations themselves.

In scope: snow; ground moisture; shallow standing water and its ice; temperature, sun, wind, and local cover effects; surface-related travel/hazard and contact wetting; snow burial of discoveries and targeted digging to regain access; save compatibility and simulation scheduling.

Deferred: soil erosion and sediment transport; litter deposition/decomposition; geological soil profiles; intertile runoff and wind redistribution; lake/river ice simulation and deeper flooding; replacing desire paths; route-specific snow packing; full body-part clothing wetness; herd migration changes; detailed snow-shelter construction and ground insulation. Do not add unused sediment or litter material implementations now.

## 1. Core decisions

### Ownership and public interface

`Location.Surface` is the single owner of surface state. Keep the existing terrain identity, terrain geometry, ecology, authored features, and permanent water bodies. Terrain supplies initial substrate properties; current surface conditions supply transient consequences.

Use a concrete deep module, not a material framework or a collection of services. Proposed interface (names may follow existing project conventions):

```csharp
void Advance(int minutes, SurfaceWeather weather);
void AddSnow(double waterEquivalentM);
void AddWater(double waterDepthM);
void Compact(double pressureFactor);

double SurfaceHeightM { get; }
double SnowDepthM { get; }
double StandingWaterDepthM { get; }
double WetnessPct { get; }
double TraversalFactor { get; }
double HazardDelta { get; }
double GetSearchFactor(double baseElevationM, double verticalExtentM);
double GetContactWettingRate(SurfaceContact contact);
```

`TraversalFactor` is a ground-only multiplier with dry baseline 1. `HazardDelta` is the additional surface footing hazard; it does not include cliffs or thin lake ice. The model owns the combination of mud, snow, water, and surface ice; consumers receive one result rather than multiply a separate penalty for each layer. Player speed, load, protection, authored edges, and route choice remain outside it.

`SurfaceHeightM` is the top of the modeled cover relative to a stable local substrate reference, not geographic elevation or a sum of unrelated effects. Keep snow and standing-water depth queries because they answer different gameplay questions; do not force every consumer to misuse one generic depth.

No parent `Location` references inside layers or the surface. `SurfaceWeather` is a compact value containing already localized physical weather inputs. Do not pass the whole game context. Serialized layer access may exist for persistence, but gameplay code must not use it.

### Layer state

Use the player's preferred word **compaction** throughout public and stored state:

```text
SurfaceLayer
  Material
  ThicknessM
  CompactionPct
  WaterSaturationPct
  FrozenFractionPct
```

Layers are ordered bottom to top. A finite active substrate layer stores ground moisture; deposits sit above it. Its initial thickness establishes the local height reference. Compaction of that substrate can lower the surface without moving the reference. Only snow and water deposits are implemented initially. Use existing terrain to select a small set of substrate properties, allowing named-location overrides.

Material definitions contain density/compaction relationships, pore capacity, permeability, retention, and applicable phase/settling rates. Compaction is not literally interchangeable with porosity: the material definition maps compaction to density and pore capacity. Avoid storing both compaction and derived porosity.

`WaterSaturationPct` measures pore capacity occupied by liquid plus frozen pore water. `FrozenFractionPct` is the frozen share of that stored pore water. Zero stored water implies zero frozen fraction. This prevents freezing from creating fictitious free capacity.

Snow has structural ice implied by thickness and compaction, plus optional water in its pores. Melting structural ice releases water and reduces the snow skeleton. Refrozen pore water remains frozen pore water and contributes to crust/footing properties; do not also count it as newly created structural snow.

A water layer uses thickness to encode its total water-equivalent amount and frozen fraction to split phases; saturation is canonically 1 and compaction 0. Derive physical water/ice thickness using density. Hide this encoding detail behind the surface API; do not sum raw `ThicknessM` blindly across unlike materials. Alternatively use an internal water-layer variant if that makes these invariants clearer without widening the API.

Mud is wet, soft, unfrozen substrate, not a new material produced by consuming soil. Slush is a wet snow condition. Ice on standing water is its frozen portion. No pairwise material-reaction registry.

### Conservation and bounded storage

Fractions are a convenient representation, not permission to lose quantities. Before compaction, phase change, merging, or layer removal, derive material mass and water amounts. Preserve them through the operation and recalculate fractions afterward.

Compaction reduces thickness while preserving material mass. Displace excess liquid if pore capacity shrinks. Restrict compaction if frozen contents prevent the requested volume reduction. Melt/refreeze conserves total water across structural snow ice, liquid, and frozen water. Drainage and evaporation are explicit sinks; precipitation is an explicit source.

Merge compatible adjacent deposits while conserving quantities; repeated snowfall into equivalent snow must not create an entry every tick. Remove empty layers. Set a small tested layer budget and a documented conservative coarsening rule for pathological alternation. Preserve any surface crust that materially affects footing. Never drop material to enforce a depth or list-length cap. No unaccounted max-depth clamp.

## 2. Weather and tick integration

### Local forcing

`Location` transforms shared weather into surface weather once per update: air temperature including `TemperatureDeltaF`, wind scaled by `WindFactor`, sunlight reduced by actual overhead exposure, and precipitation reaching the ground after canopy interception. Apply each attenuation once. Do not double-count canopy through both a terrain snow-catch reduction and overhead cover.

Do not use `GetTemperature(ActivityType.Idle)` for melt. Its human wind-chill, clothing/shelter, and fire terms are not ground temperature. Wind and sunlight affect surface exchange rates directly. Keep the model approximate and rate-based; it is not a thermal solver. A short dip below freezing does not freeze an entire water layer instantly.

Snow-bearing precipitation adds snow by water equivalent. Liquid precipitation adds water. Whiteout without snowfall adds no precipitation mass. Sun and wind affect drying and melt/exchange rates. Defer wind scour/drift until there is explicit transfer or an accounted export boundary; sheltered tiles must not create snow from nothing.

Use 32 F as the water phase reference with finite melt/freeze rates. Existing track erosion's 34 F threshold is a separate heuristic, not a reason to make surface water freeze at 34 F. Keep existing precipitation classification unless a concrete bug requires changing it.

### Internal update rules

Advance deposition, phase change, infiltration/drainage, evaporation, and settling using elapsed time. Liquid entering a layer is limited by permeability and available pore capacity. It may continue into lower layers. If supply exceeds infiltration, allow water to pool even before the substrate saturates. Frozen occupancy reduces storage and effective permeability. Water retained inside pores does not create extra cover height until swelling is explicitly modeled later.

Persistent water supply and drainage are substrate/site parameters, not a permanent wetness clamp. Marsh profiles retain water and drain slowly; ordinary ground can also become wet. Lake/river tiles remain managed by their permanent water features: meltwater may leave into that reservoir, but do not initialize a lake as a shallow puddle or duplicate its ice in this model.

Analytic updates are preferred for simple rates. Where layered transfers or phase exhaustion require subdivision, use bounded time/event steps. Do not demand exact batch equivalence from arbitrary sequential nonlinear formulas. Specify a tolerance and verify conservation and convergence instead.

### Production scheduling prerequisites

Call `Map.AdvanceGround(minutes, Weather)` once per simulated interval after weather updates to restore the existing track/trail aging clock. Do not advance tracks or trails a second time inside surfaces.

Add `GameMap.AllLocations` and update terrain-only locations as well as named locations. Make carcass/body temperature computation lazy. Update hidden features' physical aging as well as revealed features, exactly once per feature, without revealing them.

Audit `UpdateWithoutEvents`: an eight-hour batch must not apply its final weather retrospectively over all eight hours. Split world updates at weather transitions, or use bounded weather sampling intervals with documented approximation. Keep event suppression intact. All systems must consume each interval once, and clock progression must remain correct. Do not introduce a per-minute inner loop per tile merely to support surface updates.

Terrain-only forage now regenerates too; explicitly check that resulting forage availability remains sensible. Profile the full-grid update rather than assuming that traversal cost implies negligible feature-update cost.

## 3. Gameplay integrations

### Travel and footing

Apply `Surface.TraversalFactor` once in `TravelProcessor`, while retaining actor speed, encumbrance, geometry, and authored edge modifiers. Add surface hazard once through the existing location hazard aggregation. Ensure the existing coupling between hazard and travel does not accidentally charge twice for the same new surface penalty; make surface travel and injury effects distinct at that aggregation point.

Audit fixed marsh/water penalties and named-location overrides. Replace only the portion representing current mud or surface slipperiness. Retain roughness, vegetation, terrain shape, and permanent water hazards. `WaterFeature` remains the sole authority for existing lake/river thin-ice hazards. Do not also apply shallow-surface ice hazards over that same water body.

Standing water in this pass is capped at `MaxStandingWaterDepthM = 0.3048` (one foot) of liquid water. Infiltration into the modeled substrate happens first, limited by permeability and remaining capacity; retained water continues draining downward over time. Only residual liquid exceeding the cap is exported through an explicit overflow sink into the unmodeled surroundings. Record that export in the water budget; do not force excess into saturated or impermeable substrate or label overflow as infiltration. Ice remains conserved through freezing; expansion is not discarded to enforce the liquid cap. Reapply the liquid cap after thaw and transfers. Existing `DeepWater` passability is unchanged. Deeper flooding and water-dependent passability are explicit follow-up TODOs.

### Contact wetting

Add ground contact to `SurvivalContext` and `SurvivalProcessor.ProcessWetness`. The surface returns a wetting rate for a small concrete contact category (walking, working on ground, resting on ground). Existing clothing/waterproofing and bedding apply protection downstream, once. Overhead cover reduces rain reaching the surface, but must not suppress contact with ground that is already wet.

Use the existing aggregate player wetness effect for now. Make walking transfer less than prolonged direct ground contact; do not introduce per-garment wetness storage in this pass. Near the one-foot standing-water cap, ordinary unprotected traversal should leave the player significantly wet, clearly more than walking on damp soil. Tune the contact rate against the actual time to cross a tile; do not require the player to stand in the water for hours. Wetness persists and dries through existing survival behavior. A frozen surface can remain hazardous while supplying little liquid for contact wetting.

### Burial and discovery

Retain the random `RevealAtHours` target and unify the duplicate threshold generator, preserving seeded worldgen behavior.

Give placed discoveries/objects persistent placement metadata, retained after reveal: base elevation relative to the stable local reference and vertical extent. Use concrete feature defaults plus per-instance overrides; do not add dimensions to every inventory item. Newly deposited objects begin at the current surface; old authored finds use explicit ground-level defaults. Do not continuously reset placement when snow falls or melts. Settling of objects deposited onto snow is deferred, and must not be implied to exist in this pass.

`GetSearchFactor` computes burial internally from placement and cover. Map burial linearly to a positive minimum search factor. Fully covered discoveries remain possible; use a small tunable floor, initially 0.1, and tune through play. An overhang can have an unaffected default. Avoid applying standing water, snow, and opacity as arbitrary stacked penalties without a defined rule.

Accumulate condition-weighted search effort per hidden discovery:

```text
effectiveSearchHours += perceptionWeightedHours * surfaceSearchFactor
reveal when effectiveSearchHours >= RevealAtHours
```

Read terrain visibility through one existing/shared search modifier, not duplicated at callers. If changed during this work, apply it to new search effort, never by revaluing historical effort. Newly created discoveries start with zero effort and do not inherit years of tile-wide exploration.

Keep tile exploration/history separate. Do not regress earned progress when snow falls. UI can show remembered exploration and a short current condition such as "Deep snow makes searching difficult." Audit exact-completion UI so it does not promise no future discoveries in a changing world.

Once found, an object remains known. Environmental cover must not toggle `NPCBodyFeature.IsBuried`: that flag currently represents a separate action and disables decay/interactions. Decouple any discovery path that bypasses hidden-feature search before claiming NPC remains follow this rule. Discovery reveals the identity/location, but does not grant access through physical cover. Targeted excavation is in scope as described below. Contents of a body, cache, or ground-item pile share that placed target's access gate; individual contents do not each require separate excavation.

### Targeted digging and access

Offer **“Dig up {target name}”** for a discovered target whose local solid cover blocks access. Keep the target visible and remembered while suppressing pickup, looting, harvesting, and other actions requiring physical access. Apply the same access validation when executing actions and awarding discovery-event rewards, not just when building menus. Undiscovered targets must not leak their names through digging options.

Use the existing work-action pipeline (`IWorkStrategy`, work options, time, exertion, interruption, and impairment handling) for a `DigUpStrategy`. Digging by hand is allowed; a working `ToolType.Shovel` doubles progress, matching the existing digging-project convention. A missing or broken shovel gives no bonus. Reuse existing tool wear conventions, and recheck tool availability during work. Do not apply the root-foraging frozen-ground rejection to snow excavation.

The surface module supplies access status, remaining excavation effort, and an operation to apply digging effort. Keep cover-depth, material, compaction, and frozen-crust rules internal. Work increases with the target's access footprint and blocking cover; use small explicit feature/target size defaults instead of requiring full item geometry. A partly visible object may still need excavation to free it: exposure for discovery is distinct from access to retrieve/use it. Loose snow takes less work than dense snow or a crust. Liquid standing water alone is not diggable burial; access through shallow water uses wetting, not a “dig water” action. Excavating underlying soil or graves remains deferred.

Digging clears only the target's immediate footprint. Keep this as a sparse target-local surface patch, created when local clearing first differs from the tile, using the same layer rules and localized weather. The patch represents an area carved out of the tile-average surface, not an extra supply of snow. Transfer removed snow/ice to the surrounding tile area with area-weighted accounting; conserve its water and material. Do not clear the whole location, expose neighboring discoveries, or change route travel. This narrowly scoped object patch does not implement route packing or a sub-tile simulation grid.

Advance active target patches exactly once with local weather. Partial digging survives interruption and save/load as changed local cover. New snowfall can refill the patch, and thaw can reduce the remaining work naturally. Reevaluate access against current cover; no permanent `IsDugUp` bypass flag or timer that unlocks an unchanged snow-covered object. Once enough blocking cover is removed, normal actions become available. Later snowfall can require digging again without removing discovery knowledge. Target placement and access state must survive the move from hidden to revealed features and support already-known dropped-item piles as well as generated finds.

## 4. Desire paths and compaction

Keep `TrailWear` in this implementation. It stores persistent vegetation/ground wear on canonical edges, drives rendering, and fades on a different timescale from snow. A tile-wide compaction scalar cannot preserve route direction or the difference between a path and untouched ground. Fresh snowfall also must not erase the underlying path permanently.

`RecordMove` continues stamping tracks and adding edge wear. Do not call tile-wide `Surface.Compact` for every crossing: that would expose discoveries and alter forage across the entire roughly 100 m tile. Implement and verify material compaction in the surface module, but defer ordinary walking integration until route-local surface state is supported. Targeted digging uses only the object-local patch described above; it does not authorize a general-purpose whole-tile snow clearing action.

Existing path savings apply to the underlying route, not as a multiplier on the new snow penalty. Compute the existing base route benefit first (preserving authored edge and minimum-crossing semantics), then add the surface excess travel cost computed from the baseline tile segments. Thus a deep snow penalty is not automatically discounted by a vegetation path, and the existing five-minute floor still applies to the final total. Do not add the original plan's "extra trail bonus in deep snow" yet; it requires actual packed-route state.

Later, represent the route surface using the same surface model or a sparse route override. One crossing can then affect persistent substrate wear and temporary cover compaction within one route module. Only retire `TrailWear` when this replacement preserves edge locality, bare-ground persistence, regrowth, rendering, and save migration. These coexist now because they represent different state, not competing path authorities.

Leave track substrate scaling unchanged in this pass. Do not encode snow depth by changing apparent animal weight/traffic; a later track integration should retain maker characteristics and model the imprint separately.

### Explicit follow-up TODOs

- [ ] Route-local snow packing: crossings compact only the traveled route; return trips benefit, snowfall fills it again, and lasting desire-path wear remains underneath. Evaluate replacing `TrailWear` only when all existing route behavior can be preserved.
- [ ] Deeper standing water and flooding: replace the temporary one-foot cap/overflow boundary with water routing, depth-dependent movement, and passability. Preserve downward infiltration as the first normal destination for surface water.

## 5. Persistence, files, and implementation order

Store primary layer/placement/search state only. Derived height, wetness, hazards, and multipliers are ignored by JSON. Missing surface fields in old saves initialize deterministically from terrain/site defaults. Older saves have no reconstructed snowfall history; do not pretend to recover it. Validate loaded finite values and fraction bounds, with explicit migration behavior rather than silently hiding corrupt state.

For existing hidden finds, initialize effective search effort from the old tile discovery progress once during migration, preserving the earned progress; later-created finds start at zero. Preserve stored reveal thresholds and known/discovered status. Round-trip compaction, moisture, frozen contents, ordering, nonzero search effort, target placement after discovery, and partially excavated local patches. Existing revealed targets receive deterministic placement defaults and no invented excavation history.

Implementation order:

1. Add the concrete surface/layer/material definitions and invariant-focused unit tests.
2. Add local weather input construction, all-location scheduling, weather batching, and missing ground-clock call.
3. Add persistence migration and hidden physical aging.
4. Integrate travel/hazard and ground contact wetting; reconcile fixed penalties and water-feature ownership.
5. Integrate placement-based discovery, accumulated search effort, targeted digging/access gates, and condition text.
6. Profile, run the existing suite, and play through snow/thaw/refreeze and legacy-save scenarios.

Likely files: new surface types under `Environments`; `Environments/Location.cs`; `Environments/Grid/TerrainType.cs`; `Environments/Grid/GameMap.cs`; `Actions/GameContext.cs`; `Environments/TravelProcessor.cs`; `Bodies/SurvivalContext.cs`; `Survival/SurvivalProcessor.cs`; `Environments/Features/HiddenFeature.cs`; discovery/forage search call sites; work-option and access/reward handlers; a new `DigUpStrategy` alongside existing work strategies; `GroundItemsFeature`, `NPCBodyFeature`, and other affected placed targets; `Environments/Factories/DiscoveryGenerator.cs` and `LocationFactory.cs`; persistence loading; `Desktop/UI/ForageOverlay.cs` and `TilePopup.cs`. Keep `TrailWear`'s stored representation. Check current method locations before editing; original plan line numbers have drifted.

## 6. Verification and acceptance

Run `dotnet build` and `dotnet test`. Add focused behavioral tests, not one test per getter:

- Snowfall creates snow on terrain-only tiles. Cover interception applies once; whiteout alone creates none.
- Compaction decreases depth while preserving snow mass/water; excess pore liquid is displaced.
- Thaw releases snow water into substrate/puddles; refreeze conserves water and occurs over time.
- Water pools when input exceeds infiltration even over unsaturated ground; permeable ground drains sooner. Frozen occupancy cannot absorb an extra full capacity.
- Infiltration fills available substrate storage before overflow. Residual liquid never exceeds 0.3048 m; exported overflow is accounted for, frozen expansion is conserved, and thaw reapplies the liquid cap.
- Drying and drainage account for removed water. No negative depths, nonfinite state, or out-of-range fractions.
- Repeated deposits/phase cycles keep layer storage bounded without losing water or material.
- Fixed-weather batch and short-step updates agree within a stated tolerance; weather changes during an eight-hour interval affect the correct portions. Ground clocks and feature aging run once.
- Walking on wet ground increases player wetness; dry/frozen ground transfers less; bedding/protection reduces direct contact. A roof does not dry puddles instantly.
- Crossing near one foot of water causes significant unprotected player wetness within normal traversal time, more than damp soil; protection still matters.
- Snow and standing water change movement/hazard once; existing water ice and trail benefits are not double-counted.
- Deeper burial reduces new search progress linearly to the positive floor. Previously earned progress stays earned; new finds inherit none. Hidden bodies age without revealing themselves.
- Discovered buried targets offer “Dig up X” and block direct pickup/loot/reward bypasses until accessible. Unknown targets do not leak names.
- A working shovel doubles digging progress; broken/missing tools do not. Partial work survives interruption/save/load. Digging conserves displaced material and affects only the target footprint.
- Snowfall can rebury a cleared target; melt can restore access; neither erases knowledge. Shallow liquid water alone does not demand digging.
- Old and new save round-trips preserve discovery and surface invariants. No invented historical snow or reset path wear.

Play: snowfall makes ground harder to cross and search; thaw leaves wet footing after snow disappears; a cold night creates slippery surface ice rather than deleting water; the next thaw restores liquid. Compare a clearing and a marsh under the same forcing. Cross a one-foot puddle and verify substantial wetting. Discover a buried cache, interrupt digging, resume with a shovel, then retrieve its contents only after access is cleared; verify nearby finds remain covered. Verify mapwide forage respawn after the tick fix. Profile eight-hour sleep over the full map and record wall time and layer counts. Longer history simulation performance is a future requirement, not a claim that this pass simulates ten years efficiently.

Done means these conditions are observable, saves migrate, tests pass, and all consumer calculations use the surface's simple interface. It does not require sedimentation, an ecology rewrite, or replacement of desire paths.
