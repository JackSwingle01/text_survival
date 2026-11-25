#!/usr/bin/env bash
# retry.sh <command...>: run the command until it succeeds, RETRY_TRIES times at most (default 4),
# waiting 10, 30 and 90 seconds between tries. Exits with the last try's status.
#
# For calls to GitHub from the issue workflows. Its API answered 502 and 500 for some minutes on
# 2026-11-25, and each call that failed once took a finished build's pull request, its issue's
# labels or the next run with it. Every failure is retried, a refusal too: the gh CLI's exit status
# does not tell a 4xx from a 5xx, and a refusal costs two minutes more. So wrap only commands that
# are safe to repeat, or check what the failed try left behind before the next one.
set -uo pipefail
tries=${RETRY_TRIES:-4}
wait=${RETRY_FIRST_WAIT:-10}
for ((try = 1; ; try++)); do
  "$@" && exit 0
  status=$?
  if ((try >= tries)); then
    echo "retry.sh: gave up after $try tries (exit $status): $*" >&2
    exit "$status"
  fi
  echo "retry.sh: try $try failed (exit $status), next in ${wait}s: $*" >&2
  sleep "$wait"
  wait=$((wait * 3))
done
