# Open the PR without pushing when the build already pushed

Hypothesis: the #84 build pushed its branch, then had `gh pr create` refused when its app token
expired. The "Open the PR" fallback always merged main before pushing, and main had moved during the
94-minute build, so the merge conflicted (rebuild/Sim.Tests/ArchitectureTests.cs) and the issue was
parked with no PR, although only the PR was missing. When the branch on GitHub is already at the
build's commit, the fallback should open the PR and stop; any conflict with main belongs on the PR.

Checked: actionlint; the pushed-branch check against a local bare remote in three cases (never pushed
→ merge and push; pushed at HEAD → PR only; pushed, then more commits → merge and push).
Not checked: a live fallback. It only runs after a build longer than an hour.

Follow-up: a branch pushed and then given more local commits still takes the merge path, and can
conflict the same way. Rare: the build pushes once, at the end.
