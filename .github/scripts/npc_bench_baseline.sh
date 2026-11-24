#!/usr/bin/env bash
# Prints main's newest NPC bench (the nightly run's `npc-bench` artifact): its commit, date and one line a
# seed. Compare a change to NPCs with this instead of running a "before" bench. Needs `gh`, signed in.
set -euo pipefail
repo="${GITHUB_REPOSITORY:-JackSwingle01/text_survival}"
dir=$(mktemp -d)
for run in $(gh run list --repo "$repo" --workflow nightly.yml --branch main --limit 10 --json databaseId --jq '.[].databaseId'); do
  if gh run download "$run" --repo "$repo" --name npc-bench --dir "$dir" 2>/dev/null; then
    cat "$dir/npc-bench.txt"
    exit 0
  fi
done
echo "No npc-bench artifact in main's last 10 nightly runs (they are kept 30 days)." >&2
exit 1
