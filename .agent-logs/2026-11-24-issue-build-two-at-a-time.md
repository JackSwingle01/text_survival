# Issue build: two builds at a time

Hypothesis: issues wait longer for a runner than they take to build (908 minutes queued against 844
building, 22 to 24 Nov), because one build runs at a time. A run that builds its two oldest ready
issues side by side, as a matrix, halves that wait at best, and keeps the one concurrency group, so
picking issues and clearing stale `agent-working` labels stay free of races.

- Changes: `select` outputs up to MAX_BUILDS (2) issues; `build` is a matrix over them; dispatching the
  next run moves to a `next` job that runs once every build has ended.
- Affects: a short build's place stays empty until the longer one of its run ends. Two builds of one run
  both spend the plan, and each checks headroom only before it starts.

Revised: `next` dispatched only when every build succeeded -> it dispatches unless a build's "Stop the
queue" step failed, read from the run's jobs. The hourly schedule is no reliable backstop (see
2026-11-24-issue-build-allow-bot-dispatch.md), so a build that failed some other way must not end the chain.

Checked: actionlint; the selection jq against queues of three, one and no ready issues; the stop check
against a past run that stopped the queue (1) and one that did not (0).
Not checked: a live run. Dispatch workflows run from main, so it is checked after merge.

Follow-up: two builds of one run can change the same files; the second PR to merge has the conflict.
