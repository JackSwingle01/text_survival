#!/usr/bin/env bash
# Keeps one issue open while main's CI is red (ci.yml's main-red job, on each push to main):
#
#   main_red.sh    reads NEEDS (the job's `needs` context as JSON), GITHUB_SHA, GITHUB_REF_NAME, RUN_URL,
#                  GITHUB_REPOSITORY
#
# A check failed: opens the issue, labelled `main-red`, or comments the new run on the one already open.
# Every check passed: closes it. Anything else (a check skipped or cancelled) changes nothing.
# While the issue is open, issue-build.yml builds nothing but that issue: any other build branches from
# main, so its PR would carry main's red check.
# The issue is a ticket for the issue agent, and its body is the agent's rule: fix it when the fix is
# small and obvious, else park it as `needs-user`. An issue opened with GITHUB_TOKEN starts no
# workflows, so this dispatches triage for it; the hourly triage covers a dispatch that fails.
set -euo pipefail
failed=$(jq -r '[to_entries[] | select(.value.result == "failure") | .key] | join(", ")' <<< "$NEEDS")
green=$(jq -r 'all(.[]; .result == "success")' <<< "$NEEDS")
open=$(gh issue list --repo "$GITHUB_REPOSITORY" --state open --label main-red --limit 1 --json number --jq '.[0].number // empty')
sha=${GITHUB_SHA:0:7}
if [ -n "$failed" ]; then
  line="CI failed on main at $sha: $failed. $RUN_URL"
  if [ -n "$open" ]; then
    gh issue comment "$open" --repo "$GITHUB_REPOSITORY" --body "$line"
  else
    gh label create main-red --repo "$GITHUB_REPOSITORY" --color B60205 \
      --description "main's CI is failing; the issue build builds only this" 2>/dev/null || true
    url=$(gh issue create --repo "$GITHUB_REPOSITORY" --title "CI is red on main" --label main-red --body "$line

Every PR branched from main inherits the failing check, and the issue build builds nothing else while this issue is open. The next green push to main closes it; closing it by hand lets the builds go on.

## For the issue agent

Make the failed checks pass on main again. The failed jobs' log is at the end of your brief.

- **Ready** only when the log shows the cause and the fix is small and obvious: a few lines that restore what the failing check expects. For example, two merged PRs that disagree (one renamed what the other reads), or a formatting failure.
- **Needs a person** otherwise: the log does not show the cause, the failure may be flaky, the fix changes what the game does, it takes a choice between two PRs' intents, or it would skip, delete or loosen a test.
- **Acceptance:** the tests that failed pass, and so does the fast suite (\`CLAUDE.md\`, Build and Test).
- The build answers needs_user, with no pull request, if the fix turns out not to be small and obvious.")
    echo "$url"
    gh workflow run issue-triage.yml --repo "$GITHUB_REPOSITORY" --ref "$GITHUB_REF_NAME" -f issue="${url##*/}" ||
      echo "Triage was not dispatched; its hourly run picks the issue up."
  fi
elif [ "$green" = "true" ] && [ -n "$open" ]; then
  gh issue close "$open" --repo "$GITHUB_REPOSITORY" --comment "Green again at $sha. $RUN_URL"
fi
