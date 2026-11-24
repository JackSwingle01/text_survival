#!/usr/bin/env python3
"""Reads the plan usage a Claude Code run reported, for the issue agent (issue-triage.yml, issue-build.yml).

  plan_limits.py EXECUTION_FILE MAX_UTILIZATION

Claude Code emits `rate_limit_event` messages with each usage window's utilization (0..1) and a
status that is "rejected" once a limit is hit; a run refused for a limit also ends with
api_error_status 429. Prints two step outputs to stdout:

  rejected=true|false   the run hit a usage limit
  headroom=true|false   every window is below MAX_UTILIZATION, so another long run may start

and a one-line summary to stderr. A run that reported no usage (another auth method, or a format
change) gets headroom=true with a warning, so the queue is not stopped by a missing report.
"""

import json
import sys


def messages(path):
    """The execution file is a JSON array of messages, or one message per line."""
    with open(path) as f:
        text = f.read()
    try:
        doc = json.loads(text)
        return doc if isinstance(doc, list) else [doc]
    except json.JSONDecodeError:
        return [json.loads(line) for line in text.splitlines() if line.strip()]


def main(path, max_utilization):
    try:
        msgs = messages(path)
    except (OSError, ValueError) as e:
        msgs = []
        print(f"::warning::could not read the execution file {path}: {e}", file=sys.stderr)

    events = [m["rate_limit_info"] for m in msgs if m.get("type") == "rate_limit_event"]
    refused = any(m.get("type") == "result" and m.get("api_error_status") == 429 for m in msgs)

    if not events:
        rejected = refused
        print(
            f"::warning::no usage report in {path}; {'limit hit (429)' if refused else 'assuming headroom'}",
            file=sys.stderr,
        )
        print(f"rejected={str(rejected).lower()}\nheadroom={str(not rejected).lower()}")
        return

    # Events may report one window each, so fold them in order: the latest reading per window.
    windows = {}
    for event in events:
        if event.get("unifiedWindows"):
            windows.update(event["unifiedWindows"])
        else:
            windows[event.get("rateLimitType", "window")] = event
    rejected = refused or any(w.get("status") == "rejected" for w in windows.values())
    used = {name: w.get("utilization", 0.0) for name, w in windows.items()}
    headroom = not rejected and all(u < max_utilization for u in used.values())

    resets = max((w.get("resetsAt") or 0) for w in windows.values()) if rejected else None
    usage = ", ".join(f"{name} {u:.0%}" for name, u in used.items())
    print(
        f"Plan usage: {usage} (limit for a new build: {max_utilization:.0%})"
        + (f"; limit hit, resets at unix {resets}" if rejected else ""),
        file=sys.stderr,
    )
    print(f"rejected={str(rejected).lower()}\nheadroom={str(headroom).lower()}")


if __name__ == "__main__":
    main(sys.argv[1], float(sys.argv[2]))
