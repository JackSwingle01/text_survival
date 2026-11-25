# Ratchet: comments, BodyMotion.cs and BodySurvival.cs (#262)

**Hypothesis.** Both files go to 10% or under by cutting comments alone: restated code, narration,
cross-reference trails, and doc lines that repeat a member's name. Reasons the code can't give
(sources, the curve choices, the forecast and save-meaning notes, the decision record) stay,
shortened. No code token changes.

Revised: skipped the scout, design, adversarial-review and code-review sub-agents. The change touches
comments only. A check strips comments and whitespace from each file before and after: the code is
identical in both. A sub-agent would only repeat that check.
Revised: kept the citations (Skiba 2012, Kleiber against Katch-McArdle) and the "not decided" note on
the healing rate. Cut "Net cost of transport (Taylor 1982)" and "research §5.2": the header names the
research document they come from, and the 10% budget needed the lines.

- BodyMotion.cs: 20/91 comment lines (22%) went to 7/77 (9%). BodySurvival.cs: 84/560 (15%) went to 52/529 (9.8%).
- Both are off `OverCommentLimit`. The ratchet's remaining command no longer prints them.
- `CommentHygieneTests`: 4 passed. Fast suite (`Category!=Slow`, Release): 1967 passed, 0 failed, 2 skipped.

Follow-up: BodySurvival.cs is right at the limit (52 of 529). A new comment there needs one cut elsewhere.
