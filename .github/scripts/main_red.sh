#!/usr/bin/env bash
# Keeps one issue open while main's CI is red (ci.yml's main-red job, on each push to main):
#
#   main_red.sh    reads NEEDS (the job's `needs` context as JSON), GITHUB_SHA, RUN_URL, GITHUB_REPOSITORY
#
# A check failed: opens the issue, labelled `main-red`, or comments the new run on the one already open.
# Every check passed: closes it. Anything else (a check skipped or cancelled) changes nothing.
# While the issue is open, issue-build.yml starts no build: a build branches from main, so its PR
# would carry main's red check.
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
      --description "main's CI is failing; the issue build waits" 2>/dev/null || true
    gh issue create --repo "$GITHUB_REPOSITORY" --title "CI is red on main" --label main-red --body "$line

Every PR branched from main inherits the failing check, and the issue build starts nothing while this issue is open. The next green push to main closes it; closing it by hand lets the builds go on."
  fi
elif [ "$green" = "true" ] && [ -n "$open" ]; then
  gh issue close "$open" --repo "$GITHUB_REPOSITORY" --comment "Green again at $sha. $RUN_URL"
fi
