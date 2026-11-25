# The conflict resolver's pushes start CI

- Task: `resolve-conflicts.yml` pushes its merge commits as `github-actions[bot]`, so CI on them
  waits as "action required" (runs 489, 490, 492, 493 on 2026-11-25, and every resolver push before).
- Hypothesis: the Claude action's own credentials are not the ones git uses for the push.
- Found: `actions/checkout@v7` keeps `GITHUB_TOKEN` in a file that `.git/config` pulls in through
  `includeIf.gitdir:...`. The pinned claude-code-action (acfe799) clears the header only from the local
  config and from `include.path` files, so its log says "No existing authentication headers to remove",
  and git sends the header ahead of the action's credential helper.
- Changes: the resolve job's checkout sets `persist-credentials: false`; the Conflicts step reads the
  checkout's `origin/main` where it fetched main again (git has no credentials there now).

Revised: none.

Follow-up: `issue-build.yml` has the same checkout before its Claude step. Its PRs start CI because
`gh pr create` uses the app token, but a `git push` there is also made as `github-actions[bot]`.
Follow-up: proven only on `main` (dispatch workflows run from the default branch): read the first
resolver run after the merge, and the actor of the CI run on the PR it pushes to.
