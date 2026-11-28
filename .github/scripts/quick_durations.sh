#!/usr/bin/env bash
# Quick tests stay quick: no test outside the LongRun and Slow tiers takes over 5 s.
# Usage: quick_durations.sh [--warn]   (run from the repo root, after a quick-tier run wrote TestResults/*.trx)
#
# A time in the parallel run includes waiting on other tests, so it only screens: what it flags runs
# again alone and serially, and that run gates. Tiered compilation is off for that run: a short process
# otherwise runs the sim's hot code unoptimized for its first seconds.
# --warn (the PR check): findings are ::warning:: annotations and the exit is 0. The nightly run gates.
set -euo pipefail

warn=()
[ "${1:-}" = "--warn" ] && warn=(--warn)

# The screen never warns: a parallel run's times are inflated, so only the serial re-run's count.
check=(python3 .github/scripts/test_durations.py --max-seconds 5)
mapfile -t trx < <(grep -l "Sim.Tests.dll" TestResults/*.trx)
"${check[@]}" "${trx[@]}" && exit 0
mapfile -t over < <("${check[@]}" --names-only "${trx[@]}")
[ ${#over[@]} -gt 0 ] || exit 1  # failed for another reason, shown above
filter=$(printf 'FullyQualifiedName=%s|' "${over[@]}")
echo "Re-running ${#over[@]} flagged test(s) alone and serially:"
DOTNET_TieredCompilation=0 dotnet test rebuild/Sim.Tests -c Release --no-build --no-restore --filter "${filter%|}" \
  --logger "trx;LogFileName=$PWD/TestResults/isolated/alone.trx" -- xUnit.ParallelizeTestCollections=false
"${check[@]}" "${warn[@]}" TestResults/isolated/alone.trx
