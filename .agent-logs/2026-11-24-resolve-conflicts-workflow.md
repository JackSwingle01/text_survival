# Resolve-conflicts workflow

Asked for: a Claude Code action that resolves merge conflicts on PRs automatically.

Hypothesis: one workflow, triggered by pushes to `main`, finds conflicting PRs through the API's
`mergeable` field and runs claude-code-action on each PR's branch to merge `main` in and push.

Revised: claude-code-action (pinned sha, `src/github/context.ts`) throws on `push` events. The push
run only dispatches the workflow again, as triage dispatches builds.

Revised: Claude pushes with the action's app token rather than the workflow pushing with
`GITHUB_TOKEN`, because a `GITHUB_TOKEN` push starts no CI on the PR. The workflow can't inspect the
merge before it is pushed, so it checks the branch afterwards (old head and merged `main` both
ancestors of the new head) and parks the PR when that fails.

Decided without asking: merge, not rebase; drafts, forks and Dependabot PRs are skipped; a failed
PR is parked with `needs-user` (the issue agent's label) so it is not retried on every push; Opus.

Not verified: no run. The workflow only works from `main` (dispatch, and the Claude app's token
check), and actionlint could not be run in the session; CI's `actionlint` job checks it on a PR.
Checked here: the YAML parses, and the PR filter on sample data.

Follow-up: no fallback when the resolve outlives the app token (capped at 45 min instead).
