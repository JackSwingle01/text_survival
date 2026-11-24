# Derive the save format from the saved types

**Hypothesis.** Every save-format PR conflicts because two things are hand-kept: `Save.FormatVersion`
(each branch picks current + 1, so concurrent branches pick the same number) and the changelog
paragraph in `save-format.md` (one ~3,000-character line every branch appends to). Replace the number
with a hash of the saved types as the serializer sees them, plus a hand-set `MeaningChange` string for
same-shape meaning changes; drop the changelog; store the fixture without the envelope so the format
line is not a shared edit either. Old numbered saves are refused, which no-migrations already allows.

- `Save.Format`: SHA-256 of `Save.Describe(typeof(World))` and `MeaningChange`, first 12 hex.
  `Describe` walks `JsonTypeInfo` metadata: object properties (name, type, property converter,
  required), polymorphic discriminators, collection key/element, enum names and numbers, types with
  their own `[JsonConverter]` as that converter. Type names exclude assembly versions.
- `Save.WorldToJson`/`WorldFromJson` (internal) for the fixture; the fixture loses `Version` and `World`.
- `save-format.md` rewritten: the procedure, merging two format branches, and the values whose meaning
  lives in code (from the old changelog) under "What the types do not say".
- CLAUDE.md gets one line so the issue agent follows the new procedure.

Revised: `GetTypeInfo` on default options threw (no resolver) → options set
`TypeInfoResolver = new DefaultJsonTypeInfoResolver()`, the same resolver serialization used implicitly.

Revised: #141 and #157 merged mid-build → rebased; #141's version 28 and its changelog entry dropped
with the rest, fixture regenerated on the new main.

Follow-up: decision records 2026-11-21 and 2026-11-24 and spec 2026-11-22-animal-life still say
"raise `Save.FormatVersion`"; an agent PR does not edit them, so they are Jack's to reword.
