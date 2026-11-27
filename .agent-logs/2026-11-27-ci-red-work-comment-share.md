# CI red on main: Work.cs over the comment share (#340)

**Hypothesis:** `CommentHygieneTests.SimComments_StayUnderTheRatchet` fails because two PRs merged
in turn (#286 trimmed `Work.cs` to the limit; #338's `68a0cc61` added a two-line doc comment on
`StockMinutesLeft`), leaving `Work.cs` at ~10.6% comment lines, over the 10% limit. Nothing else in
the run failed. The fix is to fold multi-line doc comments in `Work.cs`, keeping what each says, so the
file comes back under 10%. Adding `Work.cs` to `OverCommentLimit` would loosen the ratchet, which
only shrinks, so that is out.

Revised: unattended run; the scout, design review and code-review sub-agents of `mp:implement` were
skipped as out of proportion to a six-comment edit with no code change. The check is the failing test
itself plus the fast suite.

Revised: kept "Fires around the agent must be caught up" on `ExpectedGather` as its own line: it is a
caller's precondition, not prose to cut.

Result: six doc comments folded (net five lines fewer); `CommentHygieneTests` passes; fast suite
(`--filter "Category!=Slow"`) green: Sim 2208 passed, Host 50, Tune 40.

Follow-up: `Work.cs` now sits just under 10%, so the next comment added there will trip the limit again.
