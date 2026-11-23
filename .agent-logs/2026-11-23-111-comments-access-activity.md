# #111: Ratchet: comments — Access.cs, Activity.cs

Hypothesis:
- Changes: comments only in rebuild/Sim/Access.cs (22%) and rebuild/Sim/Activity.cs (25%); both entries leave OverCommentLimit in CommentHygieneTests.
- Affects: nothing at run time. Parameter and record property names stay (Activity records are saved as JSON; SubstrateTests greps for `confronting: true`), so no renames.
- Stays green: CommentHygieneTests, then the fast suite with no test changed but the two list entries.
- First red: none; this is a comment sweep, checked by CommentHygieneTests (SimComments_StayUnderTheRatchet fails until the entries go, once the files are under 10%).

Revised: scout and adversarial design-review sub-agents skipped, decided unattended → a two-file comment sweep with no behaviour change; the scout's questions (who reads the names, what the check counts) were answered by two greps and reading CommentHygieneTests.CommentShare.
Revised: tdd seams, decided unattended → none; the existing CommentHygieneTests is the check, the fast suite guards behaviour.
Revised: Fight's "Target indexes Encounter.Units" note goes, though only a rename could say it → Fight is saved as JSON and its property names are its format; keeping the note would put Activity.cs over 10%.
Checked: CommentHygieneTests 4/4; the ratchet's remaining command prints neither file; fast suite 1551/1551 (Release).
Revised (review): Wired's, PutDownRefusal's, Build's and Make's rewrites misread the code, and the header said every agent's activity is saved (the player's is not) → reworded; PutDownRefusal's reason for a new pile and Block's "Gate.None means plain distance" are restored, shortened.
Revised (review): Gather's "Pool-th pool (Pools.At)" and Butcher's carcass index go without a rename, for the same reason as Fight's Target → the names are the save format.
Follow-up: Fight.Target, Gather.Pool and Butcher.Carcass are bare int indexes; a typed index would say what each indexes once a save-format change is due.
