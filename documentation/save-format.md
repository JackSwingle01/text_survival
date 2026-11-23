# The rebuild's save format

A save is the `World` as JSON inside an envelope that names its format:
`{"Version":N,"World":{...}}` (`rebuild/Sim/Save.cs`). `Save.FromJson` refuses a save in any
other format, or one with no version, rather than read half of it.

Until the first build someone else plays, there are no migrations
(`decisions/2026-11-21-save-compatibility-from-first-release.md`): an old save simply stops loading.
The version and the fixture exist so that no format change happens by accident.

## The fixture

`rebuild/Sim.Tests/Fixtures/sample-save.json` is a sample save in the current format, kept indented so a change reads as a
diff. `SaveFixtureTests` loads it and saves it again, and the bytes must match. A field added,
renamed, dropped or written differently fails that test. A field added with a "skip when null or
default" attribute leaves old saves byte-identical, and the test stays green.

## Changing the format

1. Make the change to the saved types.
2. Raise `Save.FormatVersion` by one.
3. Regenerate the fixture:
   `SURVIVAL_UPDATE_SAVE_FIXTURE=1 dotnet test rebuild/Sim.Tests --filter "FullyQualifiedName~SaveFixtureTests"`.
   This rewrites `sample-save.json` from `SaveFixtureTests.SampleWorld`.
4. Read its diff: it should show the new version and your change, and nothing else. If the
   simulation's results changed too, the sample's numbers move as well; that is expected, but check
   that the format change is the only structural one.
5. Commit the version, the fixture and the change together.

The sample world has a little of most saved state: a fire, a stash, a shelter and bedding, a
crossing, an NPC, a wolf pack, memory, explored and remembered ground, a remembered route, rng streams, a journal, a made spear
with its recorded parts and an unfinished one in the pack, a well-fed player and an NPC asleep where it dropped.

Version 2 (MVP 4, making things) added `Gear.MadeOf`, `Inventory.Projects`, the `make` and
`abandon` activities, and the `made` and `projectabandoned` events. Version 3 (the species round)
dropped `HerdMind.Hunger`, since a herd's hunger is now its members' calorie stores, and added
`BodyState.SprintSpentLevel` and `BodyState.Sex`, both skipped at their defaults. Version 4 (the fighter
state machine) gave a fight's units a `State`, an `AlertnessLevel`, a `Target` and `HasRun` in place of
`Awareness`, `JustDamaged`, `InitialOptimism`, `DodgeSet` and `BlockSet`, and an encounter its `Dropped`
weapons. Version 5 (flight and catch-up) added a fight's `PendingFlight`, its units' `FledHeading`, `FledTiles`,
`LeftChasing` and `RunDown`, and `WoundedEscaped.Heading`, all skipped at their defaults. Version 6
(remembered ground) made `Beliefs.Explored` a `GroundMemory`: every tile seen, with its ground as last
seen and the minute, saved as `"WxH:seen:records"`, a base64 bitset of the tiles seen and then ten bytes
per tile seen (minute, snow and standing water in millimetres, mud in hundredths, crust and ice flags),
13 characters per tile seen on top of the bitset. A look also catches up the kept ground in view, so the
sample's ground numbers moved forward. Version 7 (fire deterrence) gave an encounter its `FireCell`, where the
tile's fire pit stands on the grid: written always, since (0,0) is a cell, so a fight saved before it would put the
pit in a corner. The sample has no fight in it, so only its version moved. Version 8 (hunger, Well fed and exhaustion)
added `BodyState.CalorieBalanceKcal`, the running day's calorie balance behind Well fed, skipped at its
default, and the `Asleep` and `WellFed` effect kinds, appended to `EffectKind` (saved as numbers 14 and 15).
An `Asleep` severity is the sleep left in ninetieths of the longest one, so changing that longest sleep
reinterprets saves. The sample's bodies moved with the new hunger and the Well fed burn. Version 9 (Well fed
after a day without hunger) replaced `BodyState.CalorieBalanceKcal` with `BodyState.MinutesWithoutHunger`, the
minutes lived in a row without the `Hungry` effect, skipped at its default; Well fed holds once it reaches
`hungerFreeHours` in `effects.json`. The sample sets the player's count to a day so the player is still
Well fed; only the version and that field moved. Version 10 (one injury roll per crossing) replaced
`Crossing.OriginRiskPct` and `Crossing.DestRiskPct` with one `Crossing.RiskPct`, the chance rolled on arrival for
the tile walked onto; an older save taken mid-crossing would otherwise load with no risk at all. The sample has no
crossing under way; its quick walk onto forest now rolls, so it gained a `travel` rng stream, and regenerating it
also caught up the two bodies' core temperature, hydration and effects, which had drifted from `SampleWorld` before this change. Version 11 (damage in tissue condition)
changed no field but what a saved `Tissue.Toughness` means: units of damage to destroy it whole, not a divisor. Bone and
organ toughness in the body plans moved to that unit (a skull 20 became 4, a heart 5 became 0.5), so a body saved before
would keep the old numbers and shrug off every blow; `BodyState.BloodPct` is now of 70 ml a kilo, not a fixed 5,000 ml.
The sample's bodies carry the new toughness. Version 12 (glossary names) renamed saved names to the
glossary's: a person's memory is saved as `PersonMind.Memory` (was `Knows`, of type `Beliefs`), a herd's
`HerdMind.LastEncounterMinute` (was `LastFightMinute`), and the `fightended` event is `encounterended`
(`EncounterEnded`). Nothing else moved. Version 13 (the NPC mind on the tier ladder) reshaped `NpcMind`: it keeps
`Camp`, `TargetFatKg` and `TargetFatMinute`, drops `Boldness`, `SeeingTo`, `Heading`, `ForageGoal`, `WoodGoal`,
`Opinions`, `ThreatTile` and `ThreatMinute`, and adds `Goal` (an `NpcGoal`: its stat and tier by name, the minute it
was taken on and its minutes of budget). Versions 14 to 17 (the forage restore) added, each skipped at its
default: a work session's `LuckFactor` and `Clues` and a held `Doing.Held` (work carried between the steps of one
job), with `Gathered.Luck` and `Gathered.Density` (14); `TileMemory.SearchedHours` and `TileMemory.Foraged` (15);
`ForageState.ClueDraw` (16); `World.Tutorials`, with the `scavenged`, `tutorialshown` events and the `walkabout`
activity (17). `LuckTier` and `Tutorial` are written by name. The species renames (Rabbit to Hare, Homotherium to
SaberTooth) kept their enum positions, so saves hold the same numbers. Version 18 (the forage follow-ups) adds a work
session's `ClueDraw`, skipped at its default (a scavenge clue pays once a draw). Version 19 (trails as last seen) kept
the worn trails a person has seen in the spare bits of `GroundMemory`'s flags byte: the tier (`TrailTier`, 0 to 3) of
each seen tile's east and south edges, two bits each, so a trail costs a saved tile nothing. A trail is remembered only
between two seen tiles. NPC memories carry them too, since a person's memory is one structure. The sample wears a path
beside camp and the player looks at it. Version 20 (the glossary's priorities) renamed `NpcGoal.Tier` to `NpcGoal.Priority`; its values (`Critical`, `Need`, `Want`, by name) are unchanged, and nothing else moved. Version 21 (animals are individuals, #81) saves every animal as its own agent with one body
(`Members` holds one), its own `HerdMind` with its own copy of its land, and a closed-group animal's `Familiar`: the
animals it knows, by id, 0 to 1. A sighting in a person's memory is one per herd, keyed by its first animal, with
`Others` listing the rest. The sample's pack is two wolves that know each other. When you add
a new kind of saved state, extend `SampleWorld` so the fixture covers it. The `takeoff` activity (2026-11-21) is a new kind
an old build cannot read, but nothing saves it yet (only the player takes clothes off, and the player's
activity lives inside one step), so the version stands. The `raid` activity is the same: a raid lasts one minute, so no flock is between minutes in one, and the version stands. An animal also carries `LastLeft` (the tile, direction and minute of the last crossing it finished), written only once it has moved, so kin can follow a crossing too short to see under way. Version 22 (thirst, water and rest on the planner, #82) gives an animal's `HerdMind` a stored `Plan` (the stat by name, its priority by name, the goal tile, the tiles still to walk) and `Sightings` (the threats it sensed: who, where, the minute), each written only when set; an animal's body now spends and refills `HydrationMl` and `EnergyMinutes`, and a new animal starts full of both. The `watering` activity (an animal drinking) is new. The sample adds a caribou on its way to water with a wolf in mind. Version 23 (ground is a flyweight, #95) drops `TileState.Ground`: a tile's ground is its ground class's, recomputed from the seed and the sky, so a save carries none. The sample loses its three stored ground columns. Version 24 (trodden edges, #106) makes each `World.Trails` entry an `EdgeTrodden`: `Amount`, the edge's trodden amount from 0 to 1, and the `Minute` of its last stamp, in place of `TrailWear`'s `Wear` and `Minute`. The sample's remembered route is written at the new scale. Version 25 (prints fade by the real ground, #105) gives each `Track` its `SnowFallenM`, the snow its tile's ground class had taken when the print was made, so the snow since is a difference; it is written on every print. The sample's prints gain it.

## After the first release

Each format change then also ships a migration from the previous version, tested against real
saves. `FromJson` reads the version first so that a migration can be added there.
