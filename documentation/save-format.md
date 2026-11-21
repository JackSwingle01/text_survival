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
crossing, an NPC, a wolf pack, memory, explored and remembered ground, rng streams, a journal, a made spear
with its recorded parts and an unfinished one in the pack.

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
pit in a corner. The sample has no fight in it, so only its version moved. When you add
a new kind of saved state, extend `SampleWorld` so the fixture covers it. The `takeoff` activity (2026-11-21) is a new kind
an old build cannot read, but nothing saves it yet (only the player takes clothes off, and the player's
activity lives inside one step), so the version stands.

## After the first release

Each format change then also ships a migration from the previous version, tested against real
saves. `FromJson` reads the version first so that a migration can be added there.
