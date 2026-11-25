# 2026-11-25 Docs still name Save.FormatVersion (#260)

Hypothesis: docs only. Four named places, plus the predators-and-items spec ("save version bump"),
still describe a version number that #159 replaced with a format derived from the saved types.
`emergent-world-design.md` item 9 orders food before rest; `GLOSSARY.md` has rest before food.
No code, no tests affected.

Revised: skipped the scout and design-review sub-agents and TDD; a grep of `documentation/`,
`dev/active/` and `CLAUDE.md` is the whole blast radius of a six-file wording change.
Revised: the two decision records keep their decision and date; only the mechanism sentence changed,
plus one closing line pointing to #159 and `documentation/save-format.md`.
Revised: left `companion-system-plan.md` ("save version/migration path"; it plans the old game) and
`dev/active/rebuild-roadmap.md` ("versioned saves", history, still true of the derived format) alone.

Checks: `grep -rn "FormatVersion" --include=*.md .` outside `.agent-logs/` and `dev/complete/` finds
nothing; item 9 reads warmth, water, rest, food; `git diff --stat` touches only `.md` files.
