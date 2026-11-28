# Ratchet: comments, Carcass.cs and Combat.cs (#321)

**Hypothesis.** Both files go to 10% or under by cutting comments alone: section markers, restated
code, doc lines that repeat a member's name, and narration. Reasons the code can't give (why a
ledger is scaled, why rosters never shrink, why wounds tick once per minute, the tuning notes on
detection) stay, shortened. No code token changes, so no behaviour can change.

Revised: skipped the scout, design, adversarial-review and code-review sub-agents, as the #262 ratchet
did. The change touches comments only. A script strips comments and whitespace from each file before
and after, and the code is identical in both. A sub-agent would only repeat that check.
Revised: Combat.cs needed 209 comment lines down to 85 or fewer, since cutting a comment line also
shrinks the non-blank count. I cut all seven `// ---- section ----` markers and every doc comment
that restates its member (Flee, Hearers, Sweep, OpeningState, CombatTarget...). I kept the class
header's time rules, the Admit release rule, the wound-tick rule and the detection tuning numbers.
Each is shortened.

- Carcass.cs: 35/303 comment lines (11.6%) went to 28/296 (9.5%). Combat.cs: 209/980 (21.3%) went to 78/855 (9.1%).
- Both are off `OverCommentLimit`. The ratchet's remaining command no longer prints them.
- Code with comments and whitespace stripped is byte-identical before and after for both files.
- `CommentHygieneTests`: 4 passed. Fast suite (`Category!=Slow`, Release): 2200 passed, 0 failed, 2 skipped.

Follow-up: Combat.cs has room for 7 more comment lines, Carcass.cs for 1.
