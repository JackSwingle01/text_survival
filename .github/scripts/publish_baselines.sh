#!/usr/bin/env bash
# Commits the nightly run's measurements of main to the `baselines` branch, where anything that can
# fetch the repo can read them (an artifact needs a token that may read Actions, and expires):
#
#   publish_baselines.sh NEW_DIR    NEW_DIR holds npc-bench.txt, food-bench.txt and cohesion-gate.txt, any may be missing
#
# A file replaces the branch's copy only when it holds measurements, so a night whose bench or gate
# produced nothing leaves the last good numbers in place; each file's first line names its commit.
# Run from a checkout whose `origin` it may push to. The branch holds only these files and its history.
set -euo pipefail
new=$(cd "$1" && pwd)
out=$(mktemp -d)
first="baselines-first-$$" # the local name of the branch's first commit, which has no parent
trap 'git worktree remove --force "$out" 2>/dev/null; git branch -qD "$first" 2>/dev/null; true' EXIT
if git fetch -q origin baselines 2>/dev/null; then
  git worktree add -q --detach "$out" FETCH_HEAD
else
  git worktree add -q --detach "$out"
  git -C "$out" checkout -q --orphan "$first"
  git -C "$out" rm -rfq --ignore-unmatch .
fi
# name:lines of header, so a file of headers alone is not a measurement
for spec in npc-bench.txt:2 food-bench.txt:1 cohesion-gate.txt:1; do
  file=${spec%%:*} header=${spec##*:}
  if [ -f "$new/$file" ] && [ "$(wc -l < "$new/$file")" -gt "$header" ]; then
    cp "$new/$file" "$out/$file"
  else
    echo "$file: nothing measured this run; the branch keeps its last one."
  fi
done
cat > "$out/README.md" <<'README'
# Baselines

The nightly run's measurements of `main` (`.github/workflows/nightly.yml`), committed by
`.github/scripts/publish_baselines.sh`. Compare a branch with these instead of measuring `main` again.

- `npc-bench.txt`: the NPC bench (`NpcBenchReport`), one line a seed.
- `food-bench.txt`: the food chain bench (`FoodBenchRun`): kills and kcal by species, per day and week.
- `cohesion-gate.txt`: what the cohesion gate printed (`CohesionGateSeed*Tests`), by seed.

Each file's first line names the commit and day it was measured. The branch's history is the trend.
README
git -C "$out" add -A
if git -C "$out" diff --cached --quiet; then
  echo "Baselines unchanged."
else
  git -C "$out" -c user.name="github-actions[bot]" -c user.email="41898282+github-actions[bot]@users.noreply.github.com" \
    commit -q -m "Baselines of main at ${GITHUB_SHA:-$(git rev-parse HEAD)}"
  git -C "$out" push -q origin HEAD:refs/heads/baselines
  echo "Baselines pushed."
fi
