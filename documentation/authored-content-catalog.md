# Authored Content Catalog

*The paper exercise from `emergent-world-design.md` §9 step 4. Every authored event and discovery in the
old game, marked **Replaced** (and by what), **Left** (cannot come from the system; migrate later) or
**Cut** (redundant, never fires, or against the current design). The rule: anything the system can replace,
it replaces. Nothing old is ported by default.*

**Status:** paper exercise, 2026-11-20. Nothing here is built. "Exists" means it exists in `rebuild/Sim` today.

Mechanism words used below: **real state** (an agent or system already holds the fact), **sign** (`Signs.At`
over existing state), **moment** (a real dynamic crosses a threshold the player can perceive), **verb** (an
affordance row, Tag x Capability), **universal verb** (investigate, take / take some, wait and watch, avoid,
press on, detour), **world-gen** (placement by cause or as an attractor), **settling pass** (the sim run forward
at world-gen).

---

## 1. Summary

**301 entries:** 244 replaced, 12 left, 45 cut. (Was 17 left, 40 cut: the five false-perception entries were cut on
2026-11-21, because the player is never shown something that is not there; see challenge 2.)
Plus 22 old tension types, all derived (§4).

| Source | Entries | Replaced | Left | Cut |
|---|---|---|---|---|
| Random and arc events (GameEventRegistry partials, edge, tension, predator factories) | 167 | 133 | 7 | 27 |
| First-visit events | 12 | 10 | 2 | 0 |
| Discovery events (DiscoveryEventFactory) | 21 | 19 | 1 | 1 |
| Discovery pools (DiscoveryGenerator) | 36 | 36 | 0 | 0 |
| Environmental details | 16 | 11 | 0 | 5 |
| Named-location content (locations.json) | 6 | 4 | 2 | 0 |
| Building blocks (Situations, Variants, OutcomeTemplates) | 43 | 31 | 0 | 12 |

Most of the Replaced pile collapses. About a dozen body and weather events become five moments (cold,
hungry, thirsty, exhausted, infection). Some twenty predator events become one follower dynamic seen at
different distances. The discovery pools become settling-pass output.

### Systems the replacements depend on, ranked by entries unlocked

Moments and the minimal director (build step 4 itself) sit under 37 entries and are not ranked as a gap.
Counts are Replaced entries that name the dependency. One entry can need several.

| Rank | Dependency | Entries | In the plan? |
|---|---|---|---|
| 1 | NPC travellers in the settling pass: camps, stashes, fire pits, graves (step 6; needs NPC competence) | 24 | step 6 |
| 2 | Small game as a live system: encounters from `SiteSmallGame` density, occupancy of burrows/dens, prints (carried in content, no system) | 16 | no |
| 3 | A predator mind that picks the player as prey and follows prints (extends the star row 'pack follows fresh prey prints') | 15 | partly (star row is prey prints, not the player) |
| 4 | Wound infection and fever effects, plus a Treat activity and medicine use | 9 | no (effects comment: 'arrive with the events port') |
| 5 | Dens and occupancy: bear claims a den for winter, torpor, vacated dens (step 5) | 9 | step 5 |
| 6 | Contested kills: pack scavenges another's carcass, yields to a bear, scavengers strip yours (step 5 star rows) | 8 | step 5 |
| 7 | Fishing: ice holes, fish stock, seasonal runs, nets | 8 | no |
| 8 | Lake and river ice that grows and thaws (thickness drives Unstable and Crossing) | 7 | no (Content.cs: 'wait for ice that grows and thaws') |
| 9 | Repair activity for gear and shelters (crafting-adjacent) | 7 | no (crafting) |
| 10 | Human body pipeline: body -> skeletal -> bones, frozen bodies, loot, bury, graves (design 5) | 6 | design 5, no step |
| 11 | Missing effect rows: Fear/Shaken (with Cause), Sprain, Nausea | 6 | no (same comment) |
| 12 | Trapping: snare lines, catches, bait | 6 | no |
| 13 | Scavenger Beacon (ravens over a carcass or body, findable at range) (step 3) | 5 | step 3 |
| 14 | Predators/scavengers raid stashes, snares and camps (star rows) | 4 | step 5 |
| 15 | Navigation error: believed position drifts from true position in whiteout/fog/low consciousness | 3 | no |
| 16 | Fire spread (embers, lightning) and burn-scar succession | 3 | design 5 'only matters once wildfire exists' |
| 17 | Food that sickens: spoiled meat, toxic look-alike fungi | 3 | no |
| 18 | Bleeding agents leave blood signs that thin as the bleeding slows | 3 | no |
| 19 | Perception by sound at range (herds, trumpeting, the silence before a predator) | 3 | no |
| 20 | Fire smoke under overhead cover (Breathing), and fires visible at range to other minds | 2 | no |
| 21 | Escape terrain that large predators will not path into | 2 | no |
| 22 | Megafauna herd structure: lone bulls, matriarch threat display before a charge | 2 | no |
| 23 | Storms create deadfall with Cause 'storm' | 1 | no |
| 24 | Uncertain perception: sightings carry confidence (plan decision 7) | 1 | plan decision 7 |
| 25 | A fleeing herd tramples what is in its path | 1 | no |
| 26 | Rut: antler sheds along the real path | 1 | design 4 row, unstarred |

### Vocabulary additions the catalog asks for

The closed vocabulary covered more than expected: Occupied x Smoke, Occupied x Bait, Sealed x Prying,
Buried x Digging, Edible x Cutting, Guarded x Light, Elevated, Unstable, Crossing, ScentSource, Contested,
Owned and Beacon all have old events waiting for them. The gaps are mostly about **agents as things** and
the **actor's own body**:

| Addition | Entries |
|---|---|
| **Tag `Hostile`** (or widen `Guarded` to the agent itself): an agent whose mind is targeting the viewer. Every 'wave a torch at the wolf', 'shout and stand tall' and 'drop meat' choice is aimed at the animal, not at a thing it guards. Without it the design's own torch example has no tag to match. | 15 |
| **Self-directed choices**: moments about your own body offer eat, drink, rest, treat, warm hands and press on. These are activities on the actor, not `Verbs(thing)`. Moments need `Verbs(present things)` plus the actor's relevant self-activities, stated as a rule. | 13 |
| **`ThingKind.Item`** (carried gear): repair, sharpen, patch act on a thing in your pack, which `ThingRef(Kind, Tile, Index)` cannot name. | 5 |
| **`Occupied` without an agent**: bees in a hive, rabbits in a burrow, something in the deadfall. Small game is a density, not agents, so 'a nearby agent says Occupied' never fires. Either `Occupied` derives from pool parameters or small game becomes agents. | 4 |
| **`ThingKind.Body`**: design 5 has a body pipeline and 4 has 'bury a body' and 'loot a body', but `ThingKind` has no Body. A human body is not a Carcass (not Edible to the player, can be buried, carries kit). | 3 |
| **Universal verb `follow`** for a Track or a seen agent (or a capability-free row Track x none). 'Follow them' is the commonest choice in the old catalog and is not in the universal list. | 3 |
| **Universal verb `intimidate` / make noise** toward an agent: needs no capability, so it cannot be an affordance row unless rows may have no capability. | 3 |
| **Tag `Unaware`**: a resting, feeding or sleeping agent that has not noticed you (combat already has `Awareness.Unaware`). 'Strike while it sleeps', ambush a grazing herd. | 2 |
| **Tag `Wounded`**: a weakened or bleeding agent. 'Finish it', 'hunt the stragglers'. | 2 |
| **Capability-free rows** (climb, wade, take shelter): `HasFlag(0)` is always true in C#, so a row with no capability works, but the design should say rows may need none. The real gate is the body (Moving, Manipulation), which belongs in `Access.Refusal`, not in `Capability`. | 2 |

**Budget conflict.** `Tag` is capped at 20 and has 19. `Hostile`, `Unaware` and `Wounded` would make 22.
Candidates to fold: `Ripe` into `Harvestable` plus a `Fresh`-like state; `Crossing` is a property of an
edge or tile, not a thing, and could leave the thing vocabulary. Decide before the enum is written.
No new **Capability** is required. Fishing gear maps to Piercing (spear) and Binding (line, net), and a
torch is Light. Firestarters are Spark.

### What challenges the design

1. **Self-directed moments (13 entries).** §7 says a moment's choices are `Verbs` over what is present.
   But the body moments (shakes, thirst, frostbite, fever, festering wound) offer eat, drink, rest, treat,
   warm your hands. Those are activities on the actor, not affordances of a thing. Either the actor is a
   Thing with body tags (none are in the vocabulary), or the rule becomes `Verbs(present) + SelfActivities(cause)`.
   Say which.
2. **Hallucinations and false percepts (5 entries, now Cut).** *Decided 2026-11-21: false perception is
   scrapped and all five are Cut. What the player perceives is always real.* The original challenge: The fever and fear content (Fire Illusion,
   Footsteps Outside, Paranoia, Night Terrors, the hallucination pools) needs the player to perceive what
   is not there. Commitment 5 forbids creating facts, but a false belief is not a fact, and plan decision 7
   says beliefs can be wrong. Recommendation: allow fever and fear to add *confidence noise to real
   percepts* (a real fox heard as a wolf, a dying fire seen as out). Do not allow inventing things. That
   replaces Footsteps Outside and Shadow Movement and most of the rest. The old IllnessVariant pools were
   already weighted toward real threats ("sometimes the fever dream is real").
3. **The vocabulary is thing-centric; half the old content is about the animal itself.** `Guarded` is
   a thing guarded by an agent. 'Wave a torch at the wolf' (the design's own example) targets the wolf.
   See `Hostile` above. It is the most-needed addition (15 entries).
4. **`ThingKind` has no Body and no Item.** Design 4 and 5 rely on bodies (bury, loot, the frozen
   traveller), and wear moments rely on carried gear. Both must be in the exhaustive switch from the start,
   or the compiler-shows-the-empty-cell guarantee misses them.
5. **The largest block of discovery content waits for the last build step.** 24 entries need travellers
   (step 6): old camps, stashes, fire pits, knapping scatters, hunter's blinds, human trail signs, and the
   Old Campsite stories. Step 6 depends on NPC competence. Until then those finds either stay as dice
   placement or vanish. Decide whether a degraded settle (static 'ghost traveller' camps with a Cause, but
   no live traveller) ships first. The ghost camps would be facts with an invented cause, so that is a real
   trade.
6. **Static place text can lie.** Eleven `discoveryText` strings in `locations.json` assert live dynamics:
   'Wolf tracks everywhere', 'Ravens circle overhead', 'Fresh dung steams'. That is the tension problem
   in miniature: the text says a wolf is there whether or not one exists. They must be read from state at
   the visit or rewritten as static description. *Handled 2026-11-21: all eleven rewritten as static
   description of the place (plus ten smaller live claims in other place texts); live facts come from real signs.*
7. **Two old mechanisms were already dead, and one design row assumes them.** No `TrapLineActive`
   tension is ever created, so all six trapping events never fire. `SmokeSpotted` is never created either,
   yet the §1 table maps it as if it worked. Its replacement needs fire visibility to other minds, which
   no plan step builds.
8. **Collapsing into moments cuts the count of distinct moment types.** About 12 body and weather events
   become 5 moments, and about 20 predator events become one follower dynamic. That is right, but the §8
   metric 'distinct moment types per 10 days' should be measured against the old game's baseline before
   it is used as a floor.
9. **Perception is sight-only.** Hearing (herd thunder, trumpeting, the silence) unlocks 3 entries.
   That is small, but it is the only sense the old content leans on that the sim lacks entirely.
10. **Small game is a density, not agents.** That blocks 16 entries: prints, burrows, occupancy and
    encounters. Folding small game into scavenger-style lightweight agents may be cheaper than
    special-casing a density everywhere.

---

## 2. The left pile (migrate later)

### Psychological and character moments

| Entry | Source | Why it is left |
|---|---|---|
| Intrusive Thought | `Actions/Events/GameEventRegistry.Expedition.cs:453` | Psychological scene over the Disturbed tension; no simulated dynamic beyond a fear scalar. |
| Lost Time | `Actions/Events/GameEventRegistry.Expedition.cs:496` | Psychological scene (dissociation); no simulated dynamic. |
| Processing | `Actions/Events/GameEventRegistry.Camp.cs:388` | Psychological scene (sit with grief); no simulated dynamic. |

*To migrate:* the Fear effect (with Cause). Night Terrors, Paranoia, The Fire Illusion, Footsteps Outside and the hallucination pools were here; they are Cut (false perception is scrapped, challenge 2). Intrusive Thought, Lost Time and Processing stay as authored text layered on a high-Fear state; they need a 'cause of fear' template, not a system. Also here: the 'familiar face in death' variant of The Frozen One (`DiscoveryEventFactory.cs:117`). A tribe member's body needs the player's backstory as data.

### Hydrology: the Beaver Dam chain

| Entry | Source | Why it is left |
|---|---|---|
| Weakening Structure (Beaver Dam) | `Actions/Events/GameEventRegistry.Locations.cs:410` | Hydrology: dam depletion draining a pond. Water routing is deferred by the Ground design; no beaver species. |
| What's Left Behind (Beaver Dam) | `Actions/Events/GameEventRegistry.Locations.cs:448` | Same dam chain. |
| The Lodge (Beaver Dam) | `Actions/Events/GameEventRegistry.Locations.cs:477` | Same dam chain; a lodge would be Sealed x Prying if beavers existed. |
| Dam Builders (beaver_activity) | `Actions/Events/DiscoveryEventFactory.cs:669` | Beavers are not a species; belongs to the Beaver Dam chain. |

*To migrate:* a structure pool whose depletion changes the tile's water (pond to mud flat), plus beavers as a species or an Occupied pool. Ground defers water routing between tiles, so this waits for that.

### Landmarks and set pieces

| Entry | Source | Why it is left |
|---|---|---|
| The Old Forest (Ancient Grove) | `Actions/Events/GameEventRegistry.FirstVisit.cs:109` | Landmark mood with no decision; migrate as the place's discovery text, not an event. |
| The Elephant Graveyard (Bone Hollow) | `Actions/Events/GameEventRegistry.FirstVisit.cs:198` | Landmark set piece ('something draws them'); needs a dying-megafauna attractor the sim does not have (no ageing). |
| discoveryText for static landmarks (the other ~75 places, the pass stages, caves) | `rebuild/Sim/Content/locations.json:15` | Place description, not an event; stays authored. The pass, dens and caves are the expected landmark pile. |
| Standing Stones, Rock Shelter paintings, Cairn Marker, Sacred Center / Offering Stones (ancient_grove) | `rebuild/Sim/Content/locations.json:841` | Human-made landmarks from long before the run: no agent in a 1-2 year settle made them. Landmark set pieces. |

*To migrate:* keep as authored place text in `locations.json`. The Elephant Graveyard could become a settle product if megafauna age and seek an attractor to die. The ancient human sites predate any settle, so they stay authored.

### Authored records

| Entry | Source | Why it is left |
|---|---|---|
| A Record (Old Campsite) | `Actions/Events/GameEventRegistry.Locations.cs:663` | An authored message from a predecessor; a traveller's own record is a saga, out of scope (commitment 3). |

*To migrate:* a traveller's own record is a saga, which commitment 3 rules out. It could become a one-hop template ('a tally: N days') over a settled traveller's stay. That would move it to Replaced once travellers exist.

---

## 3. Full table

Dependencies name systems the rebuild does not have yet (§1 ranking). Bold flags name vocabulary additions from §1.
Line numbers are the factory method (events) or the template line (pools).

### Weather (GameEventRegistry.Weather.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Storm Approaching | `GameEventRegistry.Weather.cs:11` | Replaced | Moment: a front's leading edge enters sight while away from shelter (Weather fronts are regional). Verbs: press on, take shelter (Sheltering x -), detour/turn back. | moments |
| Whiteout (via WeatherEventFactory) | `GameEventRegistry.Weather.cs:60` | Replaced | Moment on transition to Blizzard/Whiteout mid-travel. Verbs: press on, wait, make fire (Spark). Losing direction needs navigation error. | nav, moments |
| Cold Exposure (ExtremeCold/WetCold/WindChill variants) | `GameEventRegistry.Weather.cs:109` | Replaced | Merged into one cold moment at Hypothermia/Frostbite onset (heat model + effects exist). Variants become text keyed by the dominant heat-loss term. | moments |
| Lost in Fog (via WeatherEventFactory) | `GameEventRegistry.Weather.cs:240` | Replaced | Moment on visibility drop mid-travel; consequence is navigation error. | nav, moments |
| Sudden Clearing (via WeatherEventFactory) | `GameEventRegistry.Weather.cs:324` | Replaced | Real state: visibility rises, sight budget reveals tiles and agents; moment only if something real comes into view. | moments |
| Gear Holding Up | `GameEventRegistry.Weather.cs:363` | Cut | No decision; feedback belongs in the odds/HUD (short-loop: conditions shown inside the decision). | - |
| Soaked Through | `GameEventRegistry.Weather.cs:384` | Replaced | Moment at Wet effect threshold in rain. Verbs: take shelter, press on. | moments |

### Cold Snap arc (GameEventRegistry.ColdSnap.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| The Wind Shifts | `GameEventRegistry.ColdSnap.cs:16` | Replaced | Real state: FrontType.ColdSnap arrives; merged into the cold moment (far from fire = distance to nearest fire). | moments |
| Going Numb | `GameEventRegistry.ColdSnap.cs:75` | Replaced | Moment: Hypothermia/Frostbite onset (DeadlyCold derived). Verbs: make fire (Spark), take shelter, press on. | moments |
| Frostbite Setting In | `GameEventRegistry.ColdSnap.cs:138` | Replaced | Moment: Frostbite severity threshold. 'Sacrifice fingers / bivouac / final push' = self-verb warm hands, take shelter, press on. | moments; **self-verbs** |

### Expedition (GameEventRegistry.Expedition.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Treacherous Footing | `GameEventRegistry.Expedition.cs:14` | Replaced | Real state: Travel hazard injury with Quick/Careful pace (exists in rebuild Travel); tile reads Unstable. | - |
| Something Catches Your Eye | `GameEventRegistry.Expedition.cs:50` | Replaced | Discovery: scouting accumulator reveal; director may lower the next real find's reveal threshold. | moments |
| Minor Accident | `GameEventRegistry.Expedition.cs:77` | Replaced | Real state: existing hazard injury (Injured WorldEvent); moment only if it starts Bleeding. Injury variants become templates over Injured. | - |
| Glint in the Ashes | `GameEventRegistry.Expedition.cs:123` | Cut | Fabricates loot in your own fire's ash; nothing put it there. Charcoal collection is real fire state. | - |
| Old Campsite | `GameEventRegistry.Expedition.cs:156` | Replaced | Settling pass: traveller camps (fire pit, shelter, stash with Cause). Verbs: investigate, take/take some. | traveller |
| Water Source (registry copy) | `GameEventRegistry.Expedition.cs:207` | Cut | Not registered (dead code); duplicate of discovery water_source. | - |
| Unexpected Yield | `GameEventRegistry.Expedition.cs:247` | Cut | Random bonus loot during forage; forage yield already models it, and its 'following animal signs leads to finds' link has no cause. | - |
| Trail Goes Cold | `GameEventRegistry.Expedition.cs:278` | Replaced | Real state: prints erode on the erosion clock; moment when the trail you follow becomes unreadable. Universal verbs. | moments |
| Exposed Position | `GameEventRegistry.Expedition.cs:302` | Cut | Duplicates the heat model (tile wind/exposure) and the cold moment; the map already shows exposure before you go. | - |
| Natural Shelter Spotted | `GameEventRegistry.Expedition.cs:367` | Replaced | World-gen pools (NaturalShelter/RockOverhang shelters) revealed by scouting; improve = existing shelter build (Sheltering x Cutting/Binding). | - |
| Debris | `GameEventRegistry.Expedition.cs:416` | Replaced | Real state: storms add deadfall (windthrow) with Cause 'storm'; found by scouting. | windthrow |
| Intrusive Thought | `GameEventRegistry.Expedition.cs:453` | Left | Psychological scene over the Disturbed tension; no simulated dynamic beyond a fear scalar. | - |
| Lost Time | `GameEventRegistry.Expedition.cs:496` | Left | Psychological scene (dissociation); no simulated dynamic. | - |
| Facing the Source | `GameEventRegistry.Expedition.cs:527` | Replaced | Bury/search verbs on a body (design People row: bury a body -> grave). The 'resolve Disturbed' payoff drops; fear decays on its own. | body, effects; **+kind Body** |
| Dark Passage | `GameEventRegistry.Expedition.cs:596` | Replaced | Access.Refusal (dark without Light) + darkness travel hazard; verbs: wait, light torch (Spark/Light). | - |
| Water Crossing (registry copy) | `GameEventRegistry.Expedition.cs:643` | Cut | Superseded by EdgeEvents.WaterCrossing (comment says migrated) but still registered: fires twice. | - |
| Exposed on the Ridge | `GameEventRegistry.Expedition.cs:711` | Replaced | Real state: elevated tile sees and is seen (vantage both ways); moment when a predator detects you. Merged with Seen and Seeing. | moments |
| Ambush Opportunity | `GameEventRegistry.Expedition.cs:762` | Replaced | Real state: combat opening against Unaware prey in low visibility (Combat Awareness exists). Verb hunt on a resting/feeding herd. | **+tag Unaware** |
| Edge of the Ice | `GameEventRegistry.Expedition.cs:811` | Replaced | Real state: lake ice thickness; Crossing/Unstable tags. Verbs: investigate (test), detour, press on. | ice |
| Reading the Land (TrailSignEvent) | `GameEventRegistry.Expedition.cs:843` | Replaced | Signs.At: predator/prey signs already read real prints; human signs (now fabricated at weight 0.12) come from travellers; weather signs from Ground/erosion; danger signs = Unstable. | traveller |
| Massive Prints (TrackingSomething) | `GameEventRegistry.Expedition.cs:1041` | Replaced | World-gen placement: a mammoth herd's range within reach of the start (attractor). Sign: deep Hoof prints (depth derives from weight). Moment on first reading. Today it asserts prints that may not exist. | moments |

### Water (GameEventRegistry.Water.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Thin Ice (FallThroughIce) | `GameEventRegistry.Water.cs:18` | Replaced | Real state: ice thickness + traversal pace; Unstable tag. Verbs: investigate (test), detour, press on. | ice |
| Wet Footing | `GameEventRegistry.Water.cs:67` | Cut | Duplicates Ground contact wetting, which is continuous and already exists. | - |

### Camp (GameEventRegistry.Camp.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Vermin Raid | `GameEventRegistry.Camp.cs:12` | Replaced | Scavenger 'raid a stash' row (small scavenger raids camp food); moment when audible. Verbs: drive off, set snare. Replaces the Infested tension. | raid, snare |
| Shelter Groans | `GameEventRegistry.Camp.cs:74` | Replaced | Real state: Shelter.Damage (exists) from snow load/wind; moment at a condition threshold. Verbs: repair (Sheltering x Binding), avoid (evacuate). | repair |
| Choking Smoke | `GameEventRegistry.Camp.cs:124` | Replaced | Needs fire smoke under overhead cover (Breathing). Merged with Smoke Building. | smoke |
| Embers Scatter | `GameEventRegistry.Camp.cs:162` | Replaced | Needs fire spread (embers onto bedding/fuel in wind). | wildfire |
| Rustle at Camp Edge | `GameEventRegistry.Camp.cs:199` | Replaced | Real state: a predator/scavenger agent enters sight of camp at night (already sourced from a real herd). Verbs: drive off with fire (Light), Bait, wait and watch. | moments; **+tag Hostile** |
| Melting Reveal | `GameEventRegistry.Camp.cs:249` | Cut | Fabricates a buried find under your fire; nothing was buried there. If fire ever melts ground snow, real buried things would surface generically. | - |
| Nightmare | `GameEventRegistry.Camp.cs:288` | Replaced | Moment: Fear effect (with Cause) breaks sleep. Text from the Fear effect's cause. | effects, moments |
| Night Terrors | `GameEventRegistry.Camp.cs:339` | Cut | False perception at night. Cut: false perception is scrapped (user decision 2026-11-21; `emergent-world-design.md` §0 decision 8). | - |
| Processing | `GameEventRegistry.Camp.cs:388` | Left | Psychological scene (sit with grief); no simulated dynamic. | - |

### Threats and body (GameEventRegistry.Threats.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Fresh Carcass | `GameEventRegistry.Threats.cs:15` | Replaced | Settling/kill chain: a pack's kill with Cause (exists). Verbs: butcher (Edible x Cutting), take some, avoid; Guarded if the pack is near. | - |
| Tracks | `GameEventRegistry.Threats.cs:54` | Replaced | Signs.At over real prints (exists). Follow needs a verb. | **+verb follow** |
| Something Watching | `GameEventRegistry.Threats.cs:93` | Replaced | Real state: a predator whose mind targets you, detected at sight range. Moment on detection. | stalk, moments; **+tag Hostile** |
| Raven Call | `GameEventRegistry.Threats.cs:147` | Replaced | Scavenger Beacon: ravens over a carcass or body, findable at range. | beacon |
| Stalker Circling | `GameEventRegistry.Threats.cs:179` | Replaced | Real state: follower closes to reach. Verbs: drive off with fire (Light), make fire (Spark), confront, detour. | stalk, moments; **+tag Hostile** |
| The Predator Revealed | `GameEventRegistry.Threats.cs:257` | Replaced | Same follower dynamic at reach. | stalk, moments; **+tag Hostile** |
| Ambush | `GameEventRegistry.Threats.cs:283` | Replaced | Combat Opening.Ambush (exists) when an unseen predator attacks. No moment: the fight is the decision. | stalk |
| The Shakes | `GameEventRegistry.Threats.cs:316` | Replaced | Moment: Hungry severe (+cold). Choices are self-verbs: eat, warm at fire, press on. | moments; **self-verbs** |
| Gut Wrench | `GameEventRegistry.Threats.cs:358` | Replaced | Real state: eating Questionable/spoiled meat (carcass freshness exists) causes nausea; moment at onset; treat. | toxic, effects; **self-verbs** |
| Muscle Cramp | `GameEventRegistry.Threats.cs:400` | Cut | Random injury with no simulated cause; Tired/Exhausted already impair. | - |
| Vision Blur | `GameEventRegistry.Threats.cs:463` | Replaced | Merged into the Thirsty-severe moment (self-verb drink). | moments; **self-verbs** |
| Paranoia | `GameEventRegistry.Threats.cs:508` | Cut | False perception at camp. Cut: false perception is scrapped (user decision 2026-11-21; `emergent-world-design.md` §0 decision 8). | - |
| Moment of Clarity | `GameEventRegistry.Threats.cs:546` | Cut | Random buff, no cause. | - |
| The Wound Festers | `GameEventRegistry.Threats.cs:571` | Replaced | Real state: an untreated wound becomes infected; moment at onset. Verbs: treat, cauterize (Light/fire). | infection; **self-verbs** |
| Fever Sets In | `GameEventRegistry.Threats.cs:636` | Replaced | Infection -> fever effect threshold. | infection; **self-verbs** |
| Frozen Fingers | `GameEventRegistry.Threats.cs:685` | Replaced | Merged into the cold/frostbite moment. | moments; **self-verbs** |
| Old Ache | `GameEventRegistry.Threats.cs:732` | Cut | Random, no cause. | - |
| Toothbreaker | `GameEventRegistry.Threats.cs:775` | Cut | Random, no cause. | - |
| Fugue State | `GameEventRegistry.Threats.cs:813` | Replaced | Merged into the Exhausted moment (WorldEvent Exhausted exists). | moments |
| Distant Carcass Stench | `GameEventRegistry.Threats.cs:844` | Replaced | Real state: a carcass as ScentSource perceived at range, or its Beacon. | beacon |
| Shadow Movement | `GameEventRegistry.Threats.cs:881` | Replaced | A real follower perceived with low confidence: a belief with uncertainty (plan decision 7). | stalk, belief |
| Cut Off | `GameEventRegistry.Threats.cs:931` | Replaced | Real state: terrain bottleneck (Map edges, cliffs, water) with a predator within reach; moment. Verbs: press on, detour. | stalk, moments |
| Carcass Investigation | `GameEventRegistry.Threats.cs:991` | Replaced | Real Paw prints on the carcass tile (already read). Signs + investigate. | - |
| Scavenger Approach | `GameEventRegistry.Threats.cs:1029` | Replaced | Real scavenger agent approaches your carcass (Fauna scavengers exist). Verbs: Bait (throw meat), make noise, take some. | moments; **+verb intimidate** |
| Contested Kill | `GameEventRegistry.Threats.cs:1077` | Replaced | Contested tag on a carcass with a predator present. Verbs: drive off with fire, take some and go, confront. | contest |
| Carcass Claimed | `GameEventRegistry.Threats.cs:1129` | Replaced | Real state: scavengers ate your carcass while you were away; Cause on the carcass; investigate on return. | contest |

### Blood trail arc (GameEventRegistry.BloodTrail.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Blood in the Snow | `GameEventRegistry.BloodTrail.cs:14` | Replaced | Real state: WoundedEscaped split agent (exists) leaving prints and blood. Verbs: follow, avoid. | bloodsign; **+verb follow** |
| The Dying Animal | `GameEventRegistry.BloodTrail.cs:56` | Replaced | Wounded agent weakening. Verbs: finish it, wait and watch, drive off scavengers (Light). | bloodsign; **+tag Wounded** |
| Scavengers Converge | `GameEventRegistry.BloodTrail.cs:138` | Replaced | Real state: scavengers/predators reach the dying animal (carcass search radius exists) and its Beacon. | beacon, contest |

### Herd (GameEventRegistry.Herd.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Distant Thunder | `GameEventRegistry.Herd.cs:18` | Replaced | Real state: a moving prey herd within hearing range. Verbs: follow, detour. | hearing |
| Edge of the Herd | `GameEventRegistry.Herd.cs:59` | Replaced | Herd agent in range. Verbs: hunt the weakest member, wait for predator leftovers (wait and watch). | **+tag Wounded** |
| Stampede | `GameEventRegistry.Herd.cs:135` | Replaced | Needs a fleeing herd to trample what's in its path. Moment when it flees toward you. | stampede |
| The Followers | `GameEventRegistry.Herd.cs:209` | Replaced | Real follower (old PredatorInteractions.ObservedFollowing). | stalk; **+tag Hostile** |

### Pack arc (GameEventRegistry.Pack.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Pack Signs | `GameEventRegistry.Pack.cs:15` | Replaced | Signs.At: real Paw prints (already required). Universal verbs. | - |
| Eyes in the Treeline | `GameEventRegistry.Pack.cs:57` | Replaced | Follower pack enters sight. Verbs: drive off with fire, make noise, press on. | stalk; **+tag Hostile**, **+verb intimidate** |
| Circling | `GameEventRegistry.Pack.cs:99` | Replaced | Follower pack within reach; combat Surround formation exists. Verbs: make fire (Spark), take shelter at defensible ground, flee. | stalk; **+tag Hostile** |
| The Pack Commits | `GameEventRegistry.Pack.cs:134` | Replaced | Combat start. Verbs before it: feed fire (Light), drop meat (Bait), fight. | stalk; **+tag Hostile** |

### Den arc (GameEventRegistry.Den.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| The Find | `GameEventRegistry.Den.cs:16` | Replaced | World-gen dens on high rock + bear claims a den (occupancy). Signs: tracks, scat. | den |
| Assessing the Claim | `GameEventRegistry.Den.cs:56` | Replaced | Affordance rows straight from the design table: Occupied x Smoke 'drive out with smoke', Occupied x Bait 'lure out', wait and watch, confront. | den |
| The Confrontation | `GameEventRegistry.Den.cs:147` | Replaced | Combat with the occupant (Contest). | den |
| Claiming the Den | `GameEventRegistry.Den.cs:197` | Replaced | Real state: an unoccupied den is a Cave shelter; existing make camp. | den |

### Named-location events (GameEventRegistry.Locations.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Exposed (Burnt Stand) | `GameEventRegistry.Locations.cs:19` | Replaced | Mutual visibility on open ground: a predator detects you. No location name needed. | stalk |
| Shifting Logs | `GameEventRegistry.Locations.cs:57` | Replaced | Existing traversal hazard + pace (tile Unstable). | - |
| Smoke Building (Rock Overhang) | `GameEventRegistry.Locations.cs:90` | Replaced | Fire smoke under overhead cover (merge with Choking Smoke). | smoke |
| Seen and Seeing (Granite Outcrop) | `GameEventRegistry.Locations.cs:121` | Replaced | Elevated tag; vantage both ways. | stalk |
| Weather Turning (Meltwater Pool) | `GameEventRegistry.Locations.cs:160` | Replaced | Merged into Storm Approaching. 'Fill containers quickly' = take some. | moments |
| The Silence (Ancient Grove) | `GameEventRegistry.Locations.cs:195` | Replaced | Sign derived from predator proximity (small life goes quiet); listen = investigate. | hearing |
| Escape Route (Dense Thicket) | `GameEventRegistry.Locations.cs:225` | Replaced | Real state: escape terrain large predators will not enter (json 'escape' flag). | escape |
| Caught in the Brush (Dense Thicket) | `GameEventRegistry.Locations.cs:254` | Cut | Duplicates tile traversal cost and hazard. | - |
| Unstable Footing (Boulder Field) | `GameEventRegistry.Locations.cs:281` | Replaced | Existing traversal injury; needs a Sprain effect row. | effects |
| Ridge Wind (Rocky Ridge) | `GameEventRegistry.Locations.cs:309` | Cut | Duplicates the tile's wind factor in the heat model. | - |
| The Bear's Domain (Bear Cave) | `GameEventRegistry.Locations.cs:347` | Replaced | Sign: claw marks (design row 'bear marks trees') with Cause. | den |
| Winter Sleep (Bear Cave) | `GameEventRegistry.Locations.cs:372` | Replaced | Real state: bear claims a den for winter (torpor); combat opens Unaware. Verbs: hunt, avoid, take (Guarded radius refuses deep areas). | den; **+tag Unaware** |
| Weakening Structure (Beaver Dam) | `GameEventRegistry.Locations.cs:410` | Left | Hydrology: dam depletion draining a pond. Water routing is deferred by the Ground design; no beaver species. | - |
| What's Left Behind (Beaver Dam) | `GameEventRegistry.Locations.cs:448` | Left | Same dam chain. | - |
| The Lodge (Beaver Dam) | `GameEventRegistry.Locations.cs:477` | Left | Same dam chain; a lodge would be Sealed x Prying if beavers existed. | - |
| The Climb (The Lookout) | `GameEventRegistry.Locations.cs:511` | Replaced | Elevated tag + capability-free 'climb' row; vantage sight budget. | **capability-free row** |
| The View (The Lookout) | `GameEventRegistry.Locations.cs:549` | Replaced | Real state: vantage reveals tiles, the pass and agents. | - |
| Storm Building (The Lookout) | `GameEventRegistry.Locations.cs:579` | Replaced | Regional weather visible from height; moment. | moments |
| Movement Below (The Lookout) | `GameEventRegistry.Locations.cs:607` | Replaced | Vantage sight of real agents; moment when one enters sight. | moments |
| What Happened Here (Old Campsite) | `GameEventRegistry.Locations.cs:636` | Replaced | Settled traveller camp; investigate surfaces Cause. | traveller |
| A Record (Old Campsite) | `GameEventRegistry.Locations.cs:663` | Left | An authored message from a predecessor; a traveller's own record is a saga, out of scope (commitment 3). | - |
| Still Here (Old Campsite) | `GameEventRegistry.Locations.cs:689` | Replaced | The body's Cause.Who is a real agent still holding territory; connection surfacing links them. | body, traveller; **+kind Body** |
| Salvageable Shelter (Old Campsite) | `GameEventRegistry.Locations.cs:725` | Replaced | Damaged shelter structure (Shelter damage exists) left by a traveller; repair/salvage. | traveller, repair |

### First visits (GameEventRegistry.FirstVisit.cs, via Location.FirstVisitEvent)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Vantage Point (The Lookout) | `GameEventRegistry.FirstVisit.cs:23` | Replaced | Vantage reveal; the pass is visible by world-gen. 'Smoke to the north' must come from a real fire (traveller camp). | traveller |
| The Dark Mouth (Bear Cave) | `GameEventRegistry.FirstVisit.cs:43` | Replaced | Occupancy (Occupied/Guarded) + claw-mark signs; Dark needs Light. | den |
| Unexpected Warmth (Hot Spring) | `GameEventRegistry.FirstVisit.cs:65` | Replaced | Tile temperatureDeltaF in the heat model; real prints at a water attractor. | - |
| Someone Else's Story (Old Campsite) | `GameEventRegistry.FirstVisit.cs:87` | Replaced | Settled traveller camp; the three outcomes (tally, bones, orderly departure) are templates over the traveller's fate and stay length. | traveller, body |
| The Old Forest (Ancient Grove) | `GameEventRegistry.FirstVisit.cs:109` | Left | Landmark mood with no decision; migrate as the place's discovery text, not an event. | - |
| Exposed (Rocky Ridge) | `GameEventRegistry.FirstVisit.cs:131` | Replaced | Real wind/exposure + vantage; no event needed. | - |
| Ancient Fuel (Peat Bog) | `GameEventRegistry.FirstVisit.cs:153` | Replaced | Ground peat substrate (ponds, wets you) + Unstable + peat Harvest pool. | - |
| Frozen Sentinel (Ice Shelf) | `GameEventRegistry.FirstVisit.cs:175` | Replaced | Vantage sight of real agents. | - |
| The Elephant Graveyard (Bone Hollow) | `GameEventRegistry.FirstVisit.cs:198` | Left | Landmark set piece ('something draws them'); needs a dying-megafauna attractor the sim does not have (no ageing). | - |
| The Wind Gap | `GameEventRegistry.FirstVisit.cs:220` | Replaced | Real wind funnel (tile wind factor) + travel hazard; press on/detour. | - |
| The Crossing (Snowfield Hollow) | `GameEventRegistry.FirstVisit.cs:245` | Replaced | Attractor for small game + their prints. | smallgame |
| Borrowed Heat (Sun-Warmed Cliff) | `GameEventRegistry.FirstVisit.cs:265` | Replaced | Tile temperatureDeltaF + sun warming in the heat model. | - |

### Edge events (EdgeEvents.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Water Crossing | `EdgeEvents.cs:28` | Replaced | Crossing tag. Verbs: wade (Crossing x -), detour, improvise crossing (Crossing x Binding); Ground wetting exists. | **capability-free row** |
| Difficult Terrain (ClimbingHazard) | `EdgeEvents.cs:77` | Replaced | Existing Climb edge traversal with pace. | - |
| The Approach to X (FirstClimb) | `EdgeEvents.cs:119` | Replaced | Same as climbing hazard; no first-time special case needed. | - |

### Fever arc (GameEventRegistry.Fever.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Something Wrong | `GameEventRegistry.Fever.cs:19` | Replaced | Infection onset moment. | infection; **self-verbs** |
| Fever Takes Hold | `GameEventRegistry.Fever.cs:71` | Replaced | Fever threshold moment. Verbs: rest, treat, press on. | infection; **self-verbs** |
| The Fire Illusion | `GameEventRegistry.Fever.cs:120` | Cut | Hallucination. Cut: false perception is scrapped (user decision 2026-11-21; `emergent-world-design.md` §0 decision 8). | - |
| Footsteps Outside | `GameEventRegistry.Fever.cs:180` | Cut | Hallucination that is sometimes real. Cut: false perception is scrapped (user decision 2026-11-21; `emergent-world-design.md` §0 decision 8). | - |
| Crisis Point | `GameEventRegistry.Fever.cs:247` | Replaced | Fever crisis threshold. | infection; **self-verbs** |

### Equipment wear (GameEventRegistry.Equipment.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Boot Trouble | `GameEventRegistry.Equipment.cs:15` | Replaced | Real state: gear Durability (exists) crosses a threshold; moment. Verb field repair (Binding). | repair, moments; **+kind Item** |
| Gloves Fraying | `GameEventRegistry.Equipment.cs:62` | Replaced | Same. | repair, moments; **+kind Item** |
| Blade Dulling | `GameEventRegistry.Equipment.cs:107` | Replaced | Same; sharpen (Striking on stone). | repair, moments; **+kind Item** |
| Chest Wrap Failing | `GameEventRegistry.Equipment.cs:146` | Replaced | Same. | repair, moments; **+kind Item** |
| Firestarter Failing | `GameEventRegistry.Equipment.cs:197` | Replaced | Same; fire-start chance already shown. | repair, moments; **+kind Item** |

### Impairment (GameEventRegistry.Impairment.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Lost Your Bearings | `GameEventRegistry.Impairment.cs:12` | Replaced | Navigation error under low Consciousness. | nav |
| Fumbling Hands | `GameEventRegistry.Impairment.cs:47` | Cut | Manipulation already slows work. | - |
| Dulled Senses | `GameEventRegistry.Impairment.cs:81` | Cut | Perception already lowered by capacities. | - |
| Struggling to Keep Pace | `GameEventRegistry.Impairment.cs:115` | Cut | Moving capacity already slows travel. | - |

### Foraging (GameEventRegistry.Foraging.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Squirrel Cache (registry copy) | `GameEventRegistry.Foraging.cs:16` | Cut | Not registered (dead code); duplicate of discovery squirrel_cache. | - |
| Beehive Spotted | `GameEventRegistry.Foraging.cs:51` | Replaced | World-gen hive pool in old trees (warm season); Occupied x Smoke 'drive out with smoke'. | **Occupied without agent** |

### Fishing (GameEventRegistry.Fishing.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Ice Gives Way | `GameEventRegistry.Fishing.cs:17` | Replaced | Ice thickness under a worker; Unstable. | ice, fish |
| Bear at Fishing Hole | `GameEventRegistry.Fishing.cs:62` | Replaced | Real bear drawn to fish scent (ScentSource). | fish; **+tag Hostile** |
| Wolves at the Nets | `GameEventRegistry.Fishing.cs:100` | Replaced | Real pack at a net line. | fish; **+tag Hostile** |
| Fumble on Ice | `GameEventRegistry.Fishing.cs:143` | Cut | Random fumble; Manipulation already penalizes. | - |
| Lucky Catch | `GameEventRegistry.Fishing.cs:186` | Replaced | Seasonal fish-stock dynamics (design step 5). | fish |

### Small game (GameEventRegistry.SmallGame.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Rabbit Freeze | `GameEventRegistry.SmallGame.cs:17` | Replaced | Encounter sampled from SiteSmallGame density (carried, no system). Verbs: throw (Striking/Piercing), wait, let go. | smallgame |
| Birds Roosting | `GameEventRegistry.SmallGame.cs:55` | Replaced | Same. | smallgame |
| Fish Visible | `GameEventRegistry.SmallGame.cs:93` | Replaced | Fish stock under an ice hole. | fish |
| Track Intersection | `GameEventRegistry.SmallGame.cs:137` | Replaced | Small-game prints (small game is a density, not agents: no prints today). | smallgame; **+verb follow** |
| Grouse Flushed | `GameEventRegistry.SmallGame.cs:176` | Replaced | Small-game encounter while travelling. | smallgame |

### Trapping (GameEventRegistry.Trapping.cs) - none can fire today: the TrapLineActive tension is never created

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Snare Tampered | `GameEventRegistry.Trapping.cs:16` | Replaced | Dead today. Scavenger raids a snare (raid row) + prints at the line. | snare, raid |
| Predator at Trap Line | `GameEventRegistry.Trapping.cs:58` | Replaced | Dead today. Real predator drawn to bait/catch (ScentSource). | snare; **+tag Hostile** |
| Good Catch | `GameEventRegistry.Trapping.cs:109` | Cut | Dead today; a catch is state shown on arrival, no decision. | - |
| Trap Line Plundered | `GameEventRegistry.Trapping.cs:138` | Replaced | Dead today. Raid row + Cause on the snare. | snare, raid |
| Trap Snaps | `GameEventRegistry.Trapping.cs:184` | Cut | Dead today; random injury. | - |
| Unwanted Attention | `GameEventRegistry.Trapping.cs:236` | Replaced | Dead today. Baited snare is a ScentSource that draws predators. | snare |

### Scavengers (GameEventRegistry.Scavenger.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Circling Scavengers | `GameEventRegistry.Scavenger.cs:15` | Replaced | Beacon + scavengers trail meat carriers. | beacon |
| Contested Carcass | `GameEventRegistry.Scavenger.cs:64` | Replaced | Contested tag. Verbs: drive off with fire, Bait (throw a piece), take some and go. | contest |
| The Pack's Leavings | `GameEventRegistry.Scavenger.cs:125` | Replaced | Kill chain: a pack-fed carcass with scavengers on it. | contest |
| Opportunists | `GameEventRegistry.Scavenger.cs:172` | Replaced | Scavenger boldness vs your vulnerability at night (BoldnessToward exists). Verbs: make fire, Bait, detour toward camp. | **+tag Hostile** |
| Scavenger's Gambit | `GameEventRegistry.Scavenger.cs:242` | Replaced | Wolves Feeding (Guarded) + hyenas: take some while Access guard radius allows. | contest |

### Saber-tooth (GameEventRegistry.SaberTooth.cs, via MegafaunaStrategy)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Ancient Predator | `GameEventRegistry.SaberTooth.cs:17` | Replaced | Signs of a real saber-tooth (prints, kill with Cause). | - |
| Something Watches | `GameEventRegistry.SaberTooth.cs:66` | Replaced | Follower saber-tooth (SaberToothStalked tension -> its mind). | stalk; **+tag Hostile** |
| The Ambush | `GameEventRegistry.SaberTooth.cs:137` | Replaced | Combat Opening.Ambush. | stalk |

### Mammoth hunt (GameEventRegistry.Megafauna.cs, partly via MegafaunaStrategy)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Distant Trumpeting | `GameEventRegistry.Megafauna.cs:19` | Replaced | Mammoth herd within hearing range. | hearing |
| Fresh Spoor | `GameEventRegistry.Megafauna.cs:62` | Replaced | Signs (dung, deep prints). 'Build observation post' = wait and watch. | - |
| The Bull | `GameEventRegistry.Megafauna.cs:116` | Replaced | A lone old bull as its own agent (herd structure). | mega |
| Cold Snap During Hunt | `GameEventRegistry.Megafauna.cs:175` | Cut | Coincidence of two real states; the cold moment already fires. | - |
| Wolves Smell Blood | `GameEventRegistry.Megafauna.cs:223` | Replaced | Pack scavenges a mammoth carcass (design star row). | contest |
| The Herd | `GameEventRegistry.Megafauna.cs:267` | Replaced | Mammoth herd enters sight; moment. | moments |
| The Matriarch's Warning | `GameEventRegistry.Megafauna.cs:329` | Replaced | DangerousPrey threat display before a charge. | mega; **+tag Hostile** |
| The Herd Moves | `GameEventRegistry.Megafauna.cs:380` | Replaced | Herd travel seen at range; knowledge records last-seen position (replaces MammothTracked). | - |
| The Charge | `GameEventRegistry.Megafauna.cs:416` | Replaced | Combat with DangerousPrey (exists). | - |

### Generic predator scene and binder

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Movement Nearby (PredatorEventFactory.Create) | `PredatorEventFactory.cs:13` | Replaced | Already a moment-with-verbs prototype: Watch=wait and watch, Stop=avoid, Shout=intimidate, Raise torch=Guarded x Light, Light torch=LightTorch activity, Leave meat=Bait row, Retreat=detour, Confront=contest. Port as the template for moments. | moments; **+tag Hostile**, **+verb intimidate** |
| AuthoredPredatorScenes binder (SceneAction, Source, Eligible) | `AuthoredPredatorScenes.cs:44` | Cut | Infrastructure that rebinds authored outcomes to real herds; Verbs + Access.Refusal make it unnecessary. | - |

### Tension stage events (TensionEventFactory.cs) and notices

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Something Wrong (FeverCreated) | `TensionEventFactory.cs:64` | Replaced | Infection onset moment (duplicate of Fever.cs Something Wrong). | infection; **self-verbs** |
| Fever Rising / Fever Crisis (FeverStageEvent) | `TensionEventFactory.cs:83` | Replaced | Fever thresholds. | infection; **self-verbs** |
| Blood trail thinning warning (WoundedPreyStageEvent) | `TensionEventFactory.cs:130` | Replaced | Sign: the wounded agent's blood sign thins as its bleeding slows. | bloodsign |
| Cold is dangerous warning (DeadlyColdCreated) | `TensionEventFactory.cs:143` | Replaced | Log line on EffectStarted(Hypothermia) (exists). | - |
| Going Numb (DeadlyColdStageEvent) | `TensionEventFactory.cs:150` | Replaced | Merged into the cold moment. | moments; **self-verbs** |
| The Calm (CalmBeforeStormNotice) | `WeatherEventFactory.cs:59` | Replaced | FrontType.ProlongedBlizzard lead phase is a perceptible threshold: notice/moment. | moments |
| Threshold warnings (ThresholdWarnings) | `ThresholdWarnings.cs:47` | Replaced | Log templates over EffectStarted (Hungry/Thirsty/Tired) - exists as a stream. | - |

### Discovery events (DiscoveryEventFactory.cs, fired by EventTriggerFeature)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Abandoned Camp (old_campsite) | `DiscoveryEventFactory.cs:71` | Replaced | Settled traveller camp + Cause; 'signs of violence' = body with predator Cause; Disturbed -> Fear. | traveller, body, effects |
| The Frozen One (frozen_traveler) | `DiscoveryEventFactory.cs:117` | Replaced | Body pipeline: a body kept frozen by local cold (design 5). Verbs: take (loot), bury (Digging), remember. The 'familiar face' variant (your tribe) is authored and goes to the left pile. | body, traveller; **+kind Body** |
| Hidden Cache (hidden_cache) | `DiscoveryEventFactory.cs:157` | Replaced | Traveller stash: Sealed x Prying 'force open', Buried x Digging 'dig out'. | traveller |
| Bone Scatter (bone_scatter) | `DiscoveryEventFactory.cs:193` | Replaced | Carcass -> bones with predator Cause (kill chain step 3). | - |
| Old Kill Site (old_kill_site) | `DiscoveryEventFactory.cs:228` | Replaced | Settled pack kill (aged carcass); Guarded when the pack returns. | - |
| Water Source (water_source) | `DiscoveryEventFactory.cs:271` | Replaced | World-gen water by geology (hidden spring pool exists). | - |
| Beehive (beehive) | `DiscoveryEventFactory.cs:310` | Replaced | World-gen hive pool; Occupied x Smoke. | **Occupied without agent** |
| Squirrel Cache (squirrel_cache) | `DiscoveryEventFactory.cs:365` | Replaced | World-gen nut cache pool, Owned (take some leaves less trace). | - |
| Mushroom Patch (mushroom_patch) | `DiscoveryEventFactory.cs:399` | Replaced | Harvest pool + a toxic look-alike resource (player knowledge: which ones sicken you). | toxic, effects |
| Something in There (deadfall_den) | `DiscoveryEventFactory.cs:439` | Replaced | Small-game occupant: Occupied x Smoke, Occupied x Reaching. | smallgame; **Occupied without agent** |
| Someone Was Here (previous_use) | `DiscoveryEventFactory.cs:475` | Replaced | Settled traveller fire pit under the overhang (fire pits persist with Cause). | traveller |
| The Fire's Origin (fire_origin) | `DiscoveryEventFactory.cs:499` | Replaced | World-gen lightning-struck snag with Cause 'lightning'; later wildfire + succession. | wildfire |
| Bear's Cache (bear_cache) | `DiscoveryEventFactory.cs:521` | Replaced | Design row 'bear claims and part-buries a carcass': Buried, Owned, Guarded. | den, raid |
| Fresh Tracks (fresh_tracks) | `DiscoveryEventFactory.cs:559` | Replaced | Real prints on a real game trail (trail wear). WoundedPrey only if a real wounded agent passed. | - |
| See for Miles (see_for_miles) | `DiscoveryEventFactory.cs:597` | Replaced | Vantage reveal. | - |
| Need an Axe (need_an_axe) | `DiscoveryEventFactory.cs:621` | Cut | A tooltip: Access.Refusal on the trees pool already says it needs Cutting. | - |
| Sharp Edges (sharp_edges) | `DiscoveryEventFactory.cs:644` | Replaced | Tile hazard (sharp ground) in traversal injury. | - |
| Dam Builders (beaver_activity) | `DiscoveryEventFactory.cs:669` | Left | Beavers are not a species; belongs to the Beaver Dam chain. | - |
| Movement Below (spot_movement) | `DiscoveryEventFactory.cs:691` | Replaced | Vantage sight of real agents. | - |
| Deep Hole (deep_hole) | `DiscoveryEventFactory.cs:724` | Replaced | Water depth from world-gen: better fishing, thinner ice. | ice, fish |
| Fish Run (fish_run) | `DiscoveryEventFactory.cs:767` | Replaced | Seasonal fish stock at a rapids attractor. | fish |

### Discovery pools: resources (DiscoveryGenerator.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Berry Bush | `DiscoveryGenerator.cs:121` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable; Ripe/Depleted tags. | - |
| Mixed Deadfall | `DiscoveryGenerator.cs:124` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable. | - |
| Cattails | `DiscoveryGenerator.cs:127` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable. | - |
| Medicine Patch | `DiscoveryGenerator.cs:135` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable. | - |
| Resin Pocket | `DiscoveryGenerator.cs:138` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable. | - |
| Massive Deadfall | `DiscoveryGenerator.cs:170` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable. | - |
| Natural Shelter | `DiscoveryGenerator.cs:173` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable (shelter). | - |
| Meltwater Pool | `DiscoveryGenerator.cs:250` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable. | - |
| Flint Outcrop | `DiscoveryGenerator.cs:283` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable (geology). | - |
| Ice Cave | `DiscoveryGenerator.cs:308` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable (shelter). | - |
| Pyrite Seam | `DiscoveryGenerator.cs:311` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable (geology). | - |
| Hidden Spring | `DiscoveryGenerator.cs:315` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable (geology). | - |
| Marsh Water | `DiscoveryGenerator.cs:369` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable. | - |
| Ice Source | `DiscoveryGenerator.cs:399` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable; ice holes need ICE. | ice |
| Cave: Seep, Sleeping Recess, Loose Shale | `DiscoveryGenerator.cs:104` | Replaced | World-gen pool emitted by cause (6.1 step 1); already ported to rebuild Pools.DiscoveryTable (cave floors). | - |

### Discovery pools: small game habitat (DiscoveryGenerator.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Forest Game Trail | `DiscoveryGenerator.cs:131` | Replaced | Small-game density + real trail wear. | smallgame |
| Clearing Game Trail | `DiscoveryGenerator.cs:217` | Replaced | Same. | smallgame |
| Plain Small Game | `DiscoveryGenerator.cs:253` | Replaced | Same. | smallgame |
| Hills Small Game | `DiscoveryGenerator.cs:286` | Replaced | Same. | smallgame |
| Rocky Small Game | `DiscoveryGenerator.cs:337` | Replaced | Same. | smallgame |
| Marsh Waterfowl | `DiscoveryGenerator.cs:372` | Replaced | Same. | smallgame |

### Discovery pools: evidence of past agents (DiscoveryGenerator.cs) - placed by dice today, by the settling pass after

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Abandoned Den | `DiscoveryGenerator.cs:141` | Replaced | Den vacated by its occupant (occupancy). | den |
| Char Deposit | `DiscoveryGenerator.cs:144` | Replaced | Old fire pit with Cause (traveller) or burn scar (lightning). | traveller, wildfire |
| Antler Shed | `DiscoveryGenerator.cs:148` | Replaced | Design row 'rut -> antler sheds along the real path'. | rut |
| Old Butchering Site | `DiscoveryGenerator.cs:151` | Replaced | Traveller butchered here; carcass remains with Cause. | traveller |
| Tallow Pot | `DiscoveryGenerator.cs:154` | Replaced | Traveller stash (stash economy, open question 3). | traveller |
| Dry Wood Stack | `DiscoveryGenerator.cs:157` | Replaced | Traveller stash. | traveller |
| Cordage Bundle | `DiscoveryGenerator.cs:160` | Replaced | Traveller stash. | traveller |
| Tinder Cache | `DiscoveryGenerator.cs:163` | Replaced | Traveller stash. | traveller |
| Medicine Stash | `DiscoveryGenerator.cs:166` | Replaced | Traveller stash. | traveller |
| Abandoned Camp (salvage) | `DiscoveryGenerator.cs:177` | Replaced | Settled traveller camp. | traveller |
| Trapper's Stash | `DiscoveryGenerator.cs:181` | Replaced | Traveller stash near a snare line. | traveller |
| Hunter's Blind | `DiscoveryGenerator.cs:187` | Replaced | Traveller-built blind (structure with Cause). | traveller |
| Bone Pile | `DiscoveryGenerator.cs:247` | Replaced | Carcass -> bones (kill chain). | - |
| Frozen Small Game | `DiscoveryGenerator.cs:260` | Replaced | Small animal dead of cold, Cause 'cold'. | smallgame |
| Knapping Scatter | `DiscoveryGenerator.cs:299` | Replaced | Traveller knapped here (item decay: tool -> flakes). | traveller |

### Environmental details (EnvironmentalDetail.cs via EnvironmentalDetailFactory)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Fallen Log | `EnvironmentalDetail.cs:108` | Replaced | Pool (ported to rebuild Pools.Details). | - |
| Hollow Tree | `EnvironmentalDetail.cs:217` | Replaced | Pool (ported). | - |
| Stone Pile | `EnvironmentalDetail.cs:199` | Replaced | Pool (ported). | - |
| Scattered Bones | `EnvironmentalDetail.cs:235` | Replaced | Ported as a dice pool; should become settled bones with Cause. | - |
| Dry Grass Tussock | `EnvironmentalDetail.cs:253` | Replaced | Pool (ported). | - |
| Dead Reeds | `EnvironmentalDetail.cs:340` | Replaced | Pool (ported). | - |
| Frozen Puddle | `EnvironmentalDetail.cs:143` | Replaced | Pool (ported). | - |
| Forest Puddle | `EnvironmentalDetail.cs:157` | Replaced | Pool (ported). | - |
| Old Nest | `EnvironmentalDetail.cs:305` | Replaced | Pool (ported). | - |
| Bent Branches | `EnvironmentalDetail.cs:188` | Replaced | Sign kind over real large-animal passages (Hoof/Paw traffic). | - |
| Animal Burrow | `EnvironmentalDetail.cs:271` | Replaced | Small-game occupancy: Occupied. | smallgame; **Occupied without agent** |
| Windswept Depression | `EnvironmentalDetail.cs:289` | Cut | Ground snow depth already says where snow is thin. | - |
| Lichen-Covered Rocks | `EnvironmentalDetail.cs:324` | Cut | Examine-only flavour, nothing to take or decide. | - |
| Ice Formation | `EnvironmentalDetail.cs:358` | Cut | Examine-only flavour. | - |
| Animal Tracks | `EnvironmentalDetail.cs:126` | Cut | Unused by the factory; real tracks replace it. | - |
| Animal Droppings | `EnvironmentalDetail.cs:171` | Cut | Unused by the factory; stored scat signs replace it. | - |

### Building blocks: Situations (Situations.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Predator attraction (AttractiveToPredators, PredatorAttractionLevel) | `Situations.cs:17` | Replaced | ScentSource tag on the player (meat carried, Bloody effect). | - |
| Vulnerability (Vulnerable, VulnerabilityLevel) | `Situations.cs:47` | Replaced | Body capacities, already read by BoldnessToward. | - |
| Resource pressure (ResourceScarcity, SupplyPressure) | `Situations.cs:76` | Replaced | NPC needs + director inputs; never a fact. | - |
| Exposure (Exposed, HarshConditions, ExtremeColdCrisis) | `Situations.cs:99` | Replaced | Heat model. | - |
| Danger (UnderThreat, InCrisis, PackThreat) | `Situations.cs:111` | Replaced | Nearby index: hostile agents within r. | stalk |
| Favourable (FavorableConditions, WellEquipped, HuntingAdvantage, GoodForStealth) | `Situations.cs:123` | Replaced | Capabilities + combat odds. | - |
| Darkness (Detectable, NocturnalVulnerability, InDarkness, DarkPassageConditions) | `Situations.cs:146` | Replaced | Visibility + Light capability; Access.Refusal. | - |
| Impairment (CriticallyDepleted, PsychologicallyCompromised, CognitivelyImpaired) | `Situations.cs:180` | Replaced | Effects and capacities. | - |
| Trapping/fishing (TrapLineActive, Fishing*, CheckingNets, TrapLineAttractive) | `Situations.cs:206` | Replaced | Snare and fishing state. | snare, fish |
| Infection (InfectionRisk, HasUntreatedWound) | `Situations.cs:250` | Replaced | Wound treatment state. | infection |
| Structure (StructuralStress) | `Situations.cs:259` | Replaced | Shelter condition (exists). | - |
| Isolation (RemoteAndVulnerable, IsolationLevel) | `Situations.cs:273` | Replaced | Distance to camp/fire. | - |
| Terrain (AtTerrainTransition, TrappedByTerrain, TerrainHazardWeight, IceHazard) | `Situations.cs:294` | Replaced | Map edges, Unstable tag, ice. | ice |
| Storm (StormApproaching, InActiveStorm, StormDangerLevel) | `Situations.cs:329` | Replaced | Weather fronts (exist). | - |
| Load (HeavilyEncumbered) | `Situations.cs:359` | Replaced | Carry weight (exists). | - |
| Equipment (EquipmentDegraded, BootsWorn ... FirestarterCritical) | `Situations.cs:370` | Replaced | Gear Durability (exists). | - |
| Species dynamics (ScavengerWolfDynamics, SaberTooth*, Mammoth*) | `Situations.cs:463` | Replaced | Agents near + their HerdState. | - |
| Carcass (CarcassPresent, FreshCarcassPresent, CarcassContested) | `Situations.cs:552` | Replaced | Carcass tags Fresh, Contested, Guarded. | contest |
| Tracking beliefs (IsFollowingAnimalSigns, HasFreshTrail) | `Situations.cs:38` | Replaced | Player knowledge of real prints (decision 7). | - |
| TensionOlderThan | `Situations.cs:41` | Cut | Tension bookkeeping; tensions are gone. | - |

### Building blocks: Variants (Actions/Events/Variants/)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Injury pools x19 (TripStumble ... Gore) | `AccidentVariants.cs:16` | Replaced | Text templates over the Injured WorldEvent (region, damage type, cause): text matches mechanics by construction. Keep the strings. | - |
| Animal type variants x9 | `AnimalTypeVariant.cs:32` | Replaced | Species table text + sign templates; Squirrel/Grouse/Fish need small game/fish. | smallgame |
| Cold exposure variants x3 | `ColdExposureVariant.cs:37` | Replaced | Text for the cold moment keyed by the dominant heat-loss term. | - |
| Discovery variants x8 | `DiscoveryVariant.cs:25` | Replaced | loot.json tables + Cause text. | - |
| Equipment wear variants x7 | `EquipmentWearVariant.cs:22` | Replaced | Wear-moment text. | - |
| Escape scenarios x4 | `EscapeScenarioVariant.cs:36` | Replaced | Text keyed by terrain of the escape tile. | escape |
| Examination variants x3 (tracks, droppings, branches) | `ExaminationVariant.cs:18` | Replaced | 'Investigate' text over signs. | - |
| Forage clues x10 | `ForageClueVariant.cs:76` | Replaced | Clues must derive from real state (Ripe/Depleted stock, grazed ground, real prints, windthrow); Negative/Generic clues become templates. | - |
| Hunting encounter variants x6 | `HuntingEncounterVariant.cs:48` | Replaced | Derived from HerdState + vigilance (exist). | - |
| Illness onset pools x4 | `IllnessVariant.cs:36` | Replaced | Cause on the infection/poisoning effect. | infection, toxic |
| Hallucination pools x4 | `IllnessVariant.cs:88` | Cut | Cut: false perception is scrapped (user decision 2026-11-21; `emergent-world-design.md` §0 decision 8). | - |
| Scavenge scenarios x5 | `ScavengeScenarioVariant.cs:143` | Replaced | Carcass size/freshness/remaining (exist). | - |
| Trail signs x5 (predator, prey, human, weather, danger) | `TrailSignVariant.cs:42` | Replaced | Predator/prey read real prints today; human from travellers; weather from Ground; danger = Unstable/ice. | traveller, ice |

### Building blocks: Outcome templates (OutcomeTemplates.cs)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| Cold/weather (MinorCold ... StormBattered, FellThroughIce) | `OutcomeTemplates.cs:15` | Cut | Outcomes go through the same activities and effects as everything else (7); the heat model produces cold. | - |
| Fear (Unsettling ... Shaken) | `OutcomeTemplates.cs:36` | Cut | Fear is an effect row, applied by what caused it. | effects |
| Damage (MinorFall ... PredatorAttack) | `OutcomeTemplates.cs:53` | Cut | Body.Damage via combat/traversal. | - |
| Scent (MinorBloody ... HeavilyBloody) | `OutcomeTemplates.cs:93` | Cut | Bloody effect exists. | - |
| Costs/rewards (StartsFire ... FindsFish, MarksFrozenTraveler) | `OutcomeTemplates.cs:102` | Cut | Real pools, carcasses and stashes, not conjured loot. | - |
| Shelter/carcass creation (AddsShelter, CreatesCarcass) | `OutcomeTemplates.cs:183` | Cut | Shelter build and Combat.LeaveCarcass exist. | - |
| Equipment (DamagesEquipment ... ProperRepair) | `OutcomeTemplates.cs:193` | Cut | Durability use + repair activity. | repair |
| Tension helpers (Scavengers, SaberTooth, Mammoth, DiscoversPreyHerd, FollowsTracks) | `OutcomeTemplates.cs:245` | Cut | Tensions are gone. | - |
| Herd/mammoth (AlertsHerd, TriggersHerdFlee, KillsMammoth, MammothCharge) | `OutcomeTemplates.cs:287` | Cut | Herd minds and combat. | - |
| Shelter damage (DamagesRoof ... SevereShelterDamage) | `OutcomeTemplates.cs:312` | Cut | Shelter.Damage exists. | - |

### Named-location content (rebuild/Sim/Content/locations.json)

| Entry | Source | Verdict | Mechanism | Depends on |
|---|---|---|---|---|
| old_campsite 'stories' x5 | `locations.json:745` | Replaced | Five fates of a predecessor (taken by a predator, fled, meant to return, 37-day tally, long gone) become templates over a settled traveller's fate and stay length. | traveller, body |
| discoveryText that asserts live dynamics (wolf_den, ravens_perch, herd_crossing, salt_lick, game_trail, mammoth_wallow, deer_meadow, mineral_spring, kill_site, fox_earth, rabbit_warren) | `locations.json:214` | Replaced | Each is a world-gen attractor; the claim ('wolf tracks everywhere', 'ravens circle', 'fresh dung steams') must be read from real state at the visit or dropped, or it lies like tensions did. **Handled 2026-11-21:** the text now describes only the permanent place; live facts come from real signs. | beacon, smallgame |
| discoveryText for static landmarks (the other ~75 places, the pass stages, caves) | `locations.json:15` | Left | Place description, not an event; stays authored. The pass, dens and caves are the expected landmark pile. | - |
| firstVisitEvent hooks x12 | `locations.json:131` | Replaced | See First visits group; the hook goes once those are moments/verbs. | moments |
| event features x10 (need_an_axe, fire_origin, fresh_tracks, previous_use, spot_movement, sharp_edges, see_for_miles, bear_cache, beaver_activity, deadfall_den) | `locations.json:131` | Replaced | See Discovery events group (one Cut, one Left). The field goes once its rows land. | - |
| Standing Stones, Rock Shelter paintings, Cairn Marker, Sacred Center / Offering Stones (ancient_grove) | `locations.json:841` | Left | Human-made landmarks from long before the run: no agent in a 1-2 year settle made them. Landmark set pieces. | - |

Dependency keys: `moments` = Moments + minimal director; `traveller` = NPC travellers in the settling pass; `smallgame` = Small game as a live system; `stalk` = A predator mind that picks the player as prey and follows prints; `infection` = Wound infection and fever effects, plus a Treat activity and medicine use; `den` = Dens and occupancy; `contest` = Contested kills; `fish` = Fishing; `ice` = Lake and river ice that grows and thaws; `repair` = Repair activity for gear and shelters; `body` = Human body pipeline; `effects` = Missing effect rows; `snare` = Trapping; `beacon` = Scavenger Beacon; `raid` = Predators/scavengers raid stashes, snares and camps; `nav` = Navigation error; `wildfire` = Fire spread; `toxic` = Food that sickens; `bloodsign` = Bleeding agents leave blood signs that thin as the bleeding slows; `hearing` = Perception by sound at range; `smoke` = Fire smoke under overhead cover; `escape` = Escape terrain that large predators will not path into; `mega` = Megafauna herd structure; `windthrow` = Storms create deadfall with Cause 'storm'; `belief` = Uncertain perception; `stampede` = A fleeing herd tramples what is in its path; `rut` = Rut.

---

## 4. Tensions to derived state

The design's §1 table is confirmed with corrections, and extended to every tension the old code creates.
Source: `Actions/Tensions/ActiveTension.cs`, `CreateTension` call sites, and `Actions/Events/TensionEventFactory.cs`.

| Old tension | Derived replacement | Verdict on design §1 |
|---|---|---|
| Stalked, Hunted | A predator mind that picks you as prey and follows your prints | Confirm. These had already left the tension registry for `PredatorInteractions.FollowingPlayer` in the old game. The rebuild has no such mind yet; the star row 'pack follows fresh prey prints' must cover people. |
| PackNearby | Agents within range (Nearby index) | Confirm. Already gone from the old tension registry. |
| HerdNearby | Agents within range, plus the player's last-seen position and heading (knowledge) | Correct: the tension also carried *direction* and a closing window. That is a belief, not raw state. |
| WoundedPrey | The split-off wounded agent (`WoundedEscaped`, exists) | Confirm. Add a blood sign that thins as it stops bleeding (old 'trail thinning' warning). |
| FoodScentStrong | ScentSource: meat carried, Bloody effect (exists), a carcass on the tile | Confirm. Range is radius-based today (`CarcassSearchRadius`), with no wind. |
| WoundUntreated | A wound with no dressing, which progresses to infection | Correct: the rebuild has Bleeding and Pain but no infection row and no Treat activity. 'Body effects' undersells the work. |
| FeverRising | Fever effect driven by infection | Correct: no fever row exists yet. |
| DeadlyCold | Core temperature, Hypothermia effect, distance to the nearest fire | Confirm (exists). |
| SmokeSpotted | A burning fire other minds can see | Correct: **never created in the old game** (no call site). The replacement needs fire visibility to NPC and predator minds, which no plan step builds. |
| ShelterWeakened | Shelter condition (`Shelter.Damage`, melt; exists) | Confirm. Snow load is not modelled yet. |
| Infested | Scavengers raiding camp food (the 'raid a stash' row) | Correct: the design maps it to a structure's condition. It was vermin in the food stores (`VerminRaid`). |
| Disturbed | Fear effect with a Cause (the body that caused it) | Correct: there is no Fear row yet, and the tension carried a *source location*, so the effect needs Cause. |
| Shaken | Fear effect; learned fear in a herd's mind (exists for herds) | Confirm for herds; the player half needs the Fear row. |
| ClaimedTerritory | Den occupancy: Occupied/Owned/Guarded tags (step 5) | Missing from §1. |
| TrapLineActive | Snare state | Missing from §1. Never created in the old game, so every trapping event was dead. |
| MammothTracked | The player's beliefs: last-seen herd and prints read | Missing from §1. |
| FreshTrail | The player's beliefs about real prints (Track thing) | Missing from §1. |
| ScavengersWaiting | Scavenger agents near; Beacon | Missing from §1. |
| SaberToothStalked | Same as Stalked, for the saber-tooth's mind | Missing from §1. |
| PredatorTerritory (custom) | Territory scent posts and claw marks (star row 'packs clash, scent posts') | Missing from §1. Created by first-visit events. |
| DamCollapsed (custom) | Left with the Beaver Dam chain | Missing from §1. No replacement (hydrology). |
| HerdNearbyUrgent / WoundedPreyHigh / WoundedPreyCritical / DeadlyColdCritical / ClaimedTerritoryHigh / FeverHigh / FeverCritical (stage conditions) | Thresholds on the derived state above: they become moments | Stages go; a moment fires on a perceptible threshold instead. |
