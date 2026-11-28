# Ratchet: comments, CombatAI.cs and CombatAftermath.cs (#354)

**Hypothesis.** Both files go to 10% or under by cutting comments alone, as #321 did: section markers,
doc lines that restate their member or the constants, narration. Reasons the code can't give stay,
shortened: band hysteresis, why a torch off the grid counts for nothing, the chase instinct firing
once, ResolveToward having to follow Resolve, the order of reads in Aftermath. No code token changes,
so no behaviour can change.

Revised: skipped the scout, design, adversarial-review and code-review sub-agents, as the #262 and #321
ratchets did. The change touches comments only; a script strips comments and whitespace from each
file before and after and compares them, which is the whole risk.
Revised: cutting a comment line also shrinks the non-blank count, so CombatAI.cs needed 41 comment
lines or fewer (from 123) and CombatAftermath.cs 10 or fewer (from 31). I cut every `// ---- section ----`
marker, the class header's band numbers (the constants say them), and doc comments that restate their
member. In Aftermath the method's narration became one line on why a dead person stays an agent.
Revised: FireDeterrence's kept line named `d` once the formula it defined was cut; it now says "distance".

- CombatAI.cs: 123/494 comment lines (24.9%) went to 36/411 (8.8%). CombatAftermath.cs: 31/124 (25.0%) went to 10/103 (9.7%).
- Both are off `OverCommentLimit`; the ratchet's remaining command no longer prints them.
- Code with comments and whitespace stripped is byte-identical before and after for both files.
- `CommentHygieneTests`: 4 passed. Fast suite (`Category!=Slow`, Release): 2208 passed, 0 failed, 2 skipped.

Follow-up: CombatAftermath.cs is at the limit with 10 comment lines; one more pushes it over.
