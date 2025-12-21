## Location System Redesign — Summary

---

### Core Model

**Everything is a Location.** No separate Site/Path types. Locations connect to each other in a graph. Some have traversal time (trails, passages), some don't (clearings, caves). Some have features (foraging, harvestables, shelter), some don't.

**Connections are bidirectional.** If A connects to B, B connects to A.

**Zone is just a container.** Holds weather, theme, and a bag of locations. One zone for MVP.

**Camp is a pointer.** Points to whichever location has your fire. Could change if you move camp.

---

### Discovery

**Two states only:**
- **Unexplored** — Haven't been there, don't know what's there
- **Explored** — Been there, features revealed

**You only see what's adjacent.** From any location, you see its connections. Unexplored connections show a hint ("a winding trail north"). Explored connections show name and travel time.

**Arriving at a location explores it.** Features become visible, you learn the name.

---

### Player Activities

**From Camp:**
```
Go on Expedition
  → Gather
  → Hunt
  → Explore
Tend Fire
Craft
Rest
...
```

**Gather** — Go to a known location, get resources. Shows explored locations that have ForageFeature or HarvestableFeature. Auto-pathfinds. Committed round trip.

**Hunt** — Separate mechanics (tracking, combat). Deferred for now.

**Explore** — Discover new areas. Hop-by-hop navigation with decision at each location. Breadcrumb trail for return.

---

### Explore Flow

1. Player selects Explore from camp
2. System shows reachable unexplored locations (depth 1 first, then via known paths)
3. Player picks a direction
4. Travel time passes, arrive at new location
5. Location becomes Explored, features revealed
6. Player sees new connections from here
7. Choose: keep going, do something here, or return
8. If return, backtrack via breadcrumbs
9. Fire margin shown continuously (estimated return time)

**Key insight:** Exploration is not a planned expedition. It's freeform movement with decisions at each node.

---

### Gather Flow

1. Player selects Gather from camp
2. System shows explored locations with gather-able resources
3. Each location shows summary: travel time, forage categories, harvestables
4. Player picks destination
5. System finds path automatically (via TravelProcessor.FindPath)
6. Player picks work duration
7. Preview shows: round trip time, fire margin, what's available
8. Expedition runs: travel out → work → travel back
9. Work does both foraging and harvesting at that location
10. Return to camp with loot

---

### Expedition (for Gather/Hunt)

**State is derived from position in path.**

Path structure: `[Camp, Loc1, Loc2, Destination, Loc2, Loc1, Camp]`

Index position determines phase:
- 0 = Not started
- 1 to destIndex-1 = Traveling out
- destIndex = Working
- destIndex+1 to end-1 = Traveling back
- end = Complete

**Time tracked per location.** Compare `MinutesSpentAtLocation` against `BaseTraversalMinutes` (for travel) or `WorkTimeMinutes` (for work).

**Cancellation mirrors position.** If you're 2 steps out, jump to 2 steps from end on return path.

---

### Travel Time

**TravelProcessor** takes Location + Player, returns minutes.

Factors in:
- Location's BaseTraversalMinutes
- Terrain multiplier
- Weather (from location's zone)
- Player speed (from AbilityCalculator — incorporates injuries, encumbrance, body composition)

---

### Location Data

Each location has:
- Name
- Description (shown when explored)
- UnexploredHint ("a winding trail", "rocky ground")
- BaseTraversalMinutes (0 for places you stay, >0 for passages)
- Terrain (Clear, Rough, Snow, Steep, Water, Hazardous)
- Exposure (0-1, how much weather affects you)
- DiscoveryState (Unexplored/Explored)
- Features (ForageFeature, HarvestableFeature, ShelterFeature, HeatSourceFeature...)
- Connections (list of adjacent locations)
- Items (stuff on the ground)

---

### Feature Discovery

Features have their own `IsDiscovered` flag. When location is explored, all its features become discovered. This reveals:
- What you can forage for
- Harvestable resource nodes
- Shelter quality
- Etc.

---

### Graph Generation

ZoneFactory builds the location graph explicitly:
- Create locations with names, descriptions, hints, terrain
- Connect them bidirectionally
- Add features to locations
- Starting location is Explored, others Unexplored

---

### What We Removed

- Coordinate system (X/Y positions)
- LocationManager (side-effecty navigation)
- Hinted/Discovered distinction (just Unexplored/Explored now)
- Separate Site/Path types (all just Locations)
- Separate Forage/Harvest expedition types (unified as Gather)
- Complex TravelProcessor return type (just returns int)
- DiscoveryProcessor (inline on Location)
- Player owning location/camp (GameContext owns it)

---

### Key Principles Preserved

- Fire as tether (margin shown constantly)
- Knowledge is progression (you learn the map, character doesn't level up)
- Features are the extension point (locations are property bags)
- Processors for cross-cutting calculations (TravelProcessor)
- Runners for flow control (ExpeditionRunner, ExploreRunner?)
- Data objects hold state, minimal behavior

---

### Implementation Order

1. **Location refactor** — Add new fields, remove coordinates, simplify discovery states
2. **Camp simplification** — Just a pointer to a location
3. **TravelProcessor** — Simple version, Location + Player → minutes
4. **Zone/Graph generation** — Build explicit graph in ZoneFactory
5. **Explore flow** — New runner for hop-by-hop exploration
6. **Gather flow** — Unified expedition for forage + harvest
7. **Cleanup** — Delete old navigation code, LocationManager, etc.