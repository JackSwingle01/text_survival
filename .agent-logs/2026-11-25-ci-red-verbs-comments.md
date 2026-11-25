# CI red on main: Verbs.cs comment share (#250)

Hypothesis: `CommentHygieneTests.SimComments_StayUnderTheRatchet` fails because `rebuild/Sim/Verbs.cs`
has 32 comment lines of 290 non-blank (12%, ceiling 11%). Merging two comments that restated record
fields, and one that recorded history ("the old forage screen"), brings it under without touching code.

Revised: skipped the scout, design-review and code-review sub-agents. The change is three comment
edits in one file, the issue (Jack's comment) names the file and the fix, and the test plus the fast
suite are the whole check. A sub-agent would cost more than the change.

Revised: Verbs.cs now 30 comment lines of 288 (10.4%, rounds to 11%). It stays on OverCommentLimit
because it is still over the 10% limit; the ceiling is not raised.

Evidence: `CommentHygieneTests` pass; fast suite (`Category!=Slow`) green: Sim.Tests 1898 passed,
2 skipped; Tune.Tests 40; Host.Tests 50.
