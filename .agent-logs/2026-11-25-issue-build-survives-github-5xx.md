# Issue build survives GitHub answering 5xx

**Hypothesis.** Issue build run 71 (2026-11-25, 15:42 UTC) failed only because GitHub's API answered
502 and 500 for some minutes: `gh pr create` for #189 (the build's own, twice, then the fallback
step's), the Report step's label removal and PR lookup, and the `next` job's dispatch. One retry
wrapper around those calls, and retries inside `issue_queue.py`'s `api()`, cover an outage of a
couple of minutes. Report runs under the default shell's `-e`, so its failed lookup ended the step
before the comment and `needs-user`: taking `-e` off lets it park the issue whatever fails.

- `.github/scripts/retry.sh`: four tries, waiting 10, 30, 90 s. Retries every failure, because gh's
  exit status does not tell a 4xx from a 5xx.
- `issue_queue.py`: `gh_api` retries only what gh's stderr marks as GitHub's own failure (5xx, no
  answer). A 404 on the blocked-by endpoint is an answer callers already handle, and triage makes
  that call for every issue.
- "Open the PR": `gh pr create` is not safe to repeat blindly (a 502 can still have opened the PR),
  so it looks for an open PR on the branch before each further try.
- Report: says so when it could not check for a PR, and exits non-zero only when the comment or the
  label could not be written.

Revised: none.

Checks: `actionlint` on the workflow; `retry.sh` and `gh_api` against a fake `gh` (two 502s then
success: 3 calls; 500 throughout: 4 calls, raises; 404: 1 call, raises); the Report step's script,
extracted from the YAML, against a fake `gh` that is down (comment and label each tried 4 times,
exit 1, the "could not be checked" note) and one with no PR (one call each, exit 0). Not run: the
workflow itself, which runs only from main.

Follow-up: an outage longer than the retries still leaves a built issue with a pushed branch and no
PR, and both queues skip an issue that has a `<issue#>-` branch. Nothing tells that branch from one
a person pushed to claim the ticket. A marker the build leaves (a label added before the push) would
let `select` reopen such a PR from the artifact.
Follow-up: `issue-triage.yml` and `resolve-conflicts.yml` dispatch with a bare `gh workflow run`;
their hourly schedules are the backstop today.
