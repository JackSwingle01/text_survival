# ratchet-tracker: One tracker and one daily run for every ratchet

Hypothesis:
- Changes: dev/ratchets.md (the tracker: one section per ratchet with batch, remaining command, job, retire step), .github/scripts/ratchets.py (parses it, files one issue per ratchet), .github/workflows/ratchet.yml (daily), .github/scripts/issue_queue.py (trusts the run's issues), pointers in CLAUDE.md, ci.yml, issue-agent.yml, CommentHygieneTests.cs, rebuild-roadmap.md.
- Affects: the issue agent's queue (one ratchet issue per ratchet in flight); the comment cleanup, which the roadmap deferred to after the MVP and Jack asked for now.
- Stays green: `ratchets.py list` and `run --dry-run` print both ratchets' batches; CommentHygieneTests.
- First red: none; workflow and script, checked by dry run.

Revised: the tracker lists ratchets, not files → each ratchet's remaining command reads its exception list where the check keeps it (the test's dictionary, dotnet format's report), so the list has one home and cannot drift from the check
Revised: the issue agent skips issues from github-actions[bot] (author association NONE) → issue_queue trusts that bot's issues only with the `ratchet` label
Revised: the work goes to the issue agent as issues, not a second Claude job → one builder, one review path; a ratchet issue open means none is filed for it
Follow-up: the formatting ratchet covers the whole repo (31 files, 30 outside rebuild/), so its batches touch the old game; narrow its command to rebuild/ if formatting code that is being replaced isn't wanted
Follow-up: the WhereThingsGo lists (NotInActionGenYet, EnumsSavedAsNumbers, SpeciesNamedInCode, ...) are not in the tracker yet
Follow-up: actionlint not run locally (denied in this session); CI's actionlint job checks the new workflow
