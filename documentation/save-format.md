# The rebuild's save format

A save is the `World` as JSON inside an envelope that names its format:
`{"Format":"<12 hex digits>","World":{...}}` (`rebuild/Sim/Save.cs`). `Save.FromJson` refuses a save in any
other format, one with no format, or one from before formats were derived (`"Version":N`), rather than read
half of it.

The format is not a number anyone picks. `Save.Format` is a hash of every type a save holds as the serializer
sees it (`Save.Describe`: each object's properties and their types and converters, each polymorphic base's
discriminators, each collection's key and element, each enum's names and numbers, each type written by its
own converter), plus `Save.MeaningChange`. A field added, renamed, dropped or retyped changes it by itself, so
two branches that both change the format never race for the same number, and none can forget to raise it.

Until the rebuild is finished, there are no migrations
(`decisions/2026-11-24-no-save-migrations-until-the-rebuild-is-finished.md`): an old save simply stops loading,
even when a ticket asks otherwise. The format and the fixture exist so that no format change
happens by accident.

## The fixture

`rebuild/Sim.Tests/Fixtures/sample-save.json` is a sample world in the current format, without the envelope,
kept indented so a change reads as a diff. Without the envelope, two branches changing different parts of the
format touch different lines of it. `SaveFixtureTests` loads it and saves it again, and the bytes must match. A
field added, renamed, dropped or written differently fails that test.

## Changing the format

1. Make the change to the saved types.
2. If the shape is unchanged but a saved value means something new (a unit, a scale, an enum's numbers, a
   custom converter's encoding), set `Save.MeaningChange` to the issue and a few words. Otherwise do nothing:
   the format follows the types.
3. Regenerate the fixture:
   `SURVIVAL_UPDATE_SAVE_FIXTURE=1 dotnet test rebuild/Sim.Tests --filter "FullyQualifiedName~SaveFixtureTests"`.
   This rewrites `sample-save.json` from `SaveFixtureTests.SampleWorld`.
4. Read its diff: it should show your change and nothing else. If the simulation's results changed too, the
   sample's numbers move as well; that is expected, but check that the format change is the only structural one.
5. Commit the change and the fixture together. When you add a new kind of saved state, extend `SampleWorld`
   so the fixture covers it.

A merge of two format-changing branches needs no renumbering: if the fixture's diffs touch different lines
they merge on their own, and CI's run of `SaveFixtureTests` on the merged result says whether it still holds.
If they touch the same lines, regenerate the fixture on the merged branch. There is no changelog to append to;
`git log -- rebuild/Sim.Tests/Fixtures/sample-save.json` is the history.

The sample world has a little of most saved state: a fire, a pile, a shelter and bedding, a
crossing, an NPC, a wolf pack, memory, explored and remembered ground, a remembered route, rng streams, a journal, a made spear
with its recorded parts and an unfinished one in the pack, a well-fed player and an NPC asleep where it dropped,
a caribou part way across a tile on its way to water with a wolf in mind, and a wolf with a caribou herd and the
camp's pile on its sightings list.

## What the types do not say

The hash sees shapes, not meanings or a custom converter's encoding. These are the saved values whose meaning
lives in code, and changing any of them is a `MeaningChange`:

- `Beliefs.Explored` is a `GroundMemory` written as `"WxH:seen:records"`: a base64 bitset of the tiles seen,
  then eleven bytes per tile seen (minute, snow and standing water in millimetres, mud in hundredths, crust and
  ice flags, and in the flags' spare bits the `TrailTier` of the tile's east and south edges, two bits each,
  then the tile's trodden level in hundredths).
- `EffectKind` and some other enums are written as numbers, so a new kind is appended, never inserted. Enums
  marked with `JsonStringEnumConverter` (`Stat`, `Priority`, `LuckTier`, `Tutorial`) are written by name.
- An `Asleep` severity is the sleep left in ninetieths of the longest collapse (`MaxCollapseMinutes`).
- `Tissue.Toughness` is units of damage to destroy the tissue whole; `BodyState.BloodPct` is of 70 ml a kilo.
- Ids (`AgentId`, `TileId`, `FireId`, `EdgeId`, `PoolId`, `StreamName`) are written by their own converters.
- An encounter's `FireCell` is always written, since (0,0) is a cell.

## After the rebuild is finished

Each format change then also ships a migration from the previous version, tested against real
saves. `FromJson` reads the format first so that a migration can be added there; migrations need an order, so
that is when each released format gets a name in a list (format hash to release), kept beside the migrations.
