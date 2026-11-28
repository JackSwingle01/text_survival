# Web checks in CI: the page's node tests on every PR, shots.mjs nightly

Hypothesis:
- Changes: `.github/workflows/ci.yml` (`rebuild quick` runs `node --test rebuild/Web/tools/*.test.mjs`), `.github/workflows/nightly.yml` (a `web` job: AOT publish, `shots.mjs --run --replay`, `shots.mjs --all --phone`, screenshots as an artifact; the `report` job reads it), `.github/scripts/nightly_report.py` (reads a check's result besides trx), docs (`rebuild/Web/README.md`, `documentation/verification.md`, `CLAUDE.md`).
- Affects: no CI job runs any web check today (no `node`, `.mjs` or Chrome in any workflow); `rebuild quick` measures 72 s of its 120 s; the nightly issue lists only trx failures, with a `dotnet test` replay each.
- Stays green: the five `.mjs` suites (65 tests, 36 s here, all but 1 s of it `terrain.test.mjs`); `shots.mjs --run --replay` passes here on Linux as root against a non-AOT publish (100 s); `nightly_report.py`'s existing trx output unchanged.
- First red: a fixture job folder holding only a failed web check's result file is reported as "failed before any test ran" instead of naming the check and its replay command.
