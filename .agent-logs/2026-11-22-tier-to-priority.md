# Rename Tier to Priority in the rebuild

**Hypothesis.** A pure rename: the `Tier` enum, `StatTiers`, the static `Tiers` class and its
per-stat functions, `Npc`'s locals and comments, the vitals DTO field and the page's reads of it,
and the docs that describe the code. No behaviour change, so every test passes unchanged apart from
names. `NpcGoal.Tier` is saved by property name, so the save format moves to 20 and the fixture
changes by that one key.

- `Tiers.cs` → `Priorities.cs`, `TiersTests.cs` → `PrioritiesTests.cs`; `Tier` → `Priority`,
  `StatTiers` → `StatPriorities`, `WarmthTier`/`CoreTier`/`HealthTier`/`WaterTier`/`FoodTier`/`RestTier`
  → `*Priority`, `Npc`'s `Tiers()`/`TiersNow` → `Priorities()`/`PrioritiesNow`, the interrupt reason
  "a worse tier" → "a worse priority".
- `VitalDto.Tier` → `VitalDto.Priority`; `main.js` reads `x.priority` and `PRIORITY_CLASS`.
- `Save.FormatVersion` 19 → 20; fixture diff is the version and `"Tier"` → `"Priority"`, nothing else.
- `Npc.Ladder` keeps its name: it is the stat order within a priority, and "ladder" is not in the
  glossary's avoid list.

Revised: "no Tier identifiers left in `rebuild/`" can't be read literally. `TrailTier` (Trace, Path,
Trail), `Tracks.TierOf`/`TierNow`/`TierAt`/`TierFor`, `GroundMemory.TierMask`/`RememberTrailTier`,
`LuckTier` and `Pools.TierOf`, and `TrailDto.Tier` are trail-wear and forage-luck tiers, different
concepts from a stat's priority. Renaming them to Priority would be wrong. Left as they are.

Revised: dated records keep the old word: the 2026-11-21 architecture audit, the save-format
history before version 20, and the roadmap's log entries. The decision record is untouched (PR #77
adds its rename note). `GLOSSARY.md` is untouched too: on `main` it still says Tier, and the rename
lands with PR #77.

Follow-up: `documentation/emergent-world-design.md` item 9 still orders the stats "food, then
rest"; the code and the glossary have rest before food (PR #77 amends the decision record).

Follow-up: PR #77 edits the same "Agent behaviour" row of `clean-room-architecture.md`; whichever
merges second resolves a one-line conflict.

Follow-up: the container had no .NET SDK and `dot.net` is blocked; Ubuntu's `dotnet-sdk-10.0` is
10.0.115, below `global.json`'s 10.0.400 pin, so tests ran from a directory outside the repo to
skip the pin. CI uses the pinned SDK.
