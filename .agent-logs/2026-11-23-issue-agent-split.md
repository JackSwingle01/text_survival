# Split the issue agent into triage and build

Hypothesis: one workflow that triages and builds the whole queue in a single run keeps new issues
waiting behind long builds and runs stale scripts for hours. Splitting it in two, with the
`ready-for-agent` label as the hand-off and one build per run that dispatches the next, keeps every
run short and fresh, and lets triage answer within minutes.

Revised: "concurrency 1 queues the builds" does not hold. A concurrency group keeps one pending run
and cancels the rest, so the build is never told which issue to take: it picks the oldest ready
issue itself, and a cancelled trigger costs nothing.

Revised: a label added with GITHUB_TOKEN starts no workflows, so `issues: labeled` only fires for a
label a person adds. Triage dispatches the build with `gh workflow run` (workflow_dispatch is
exempt), and the build's hourly run at :53 covers a missing dispatch.

Revised: the old headroom check read the usage from triage's call, which the build no longer has.
The build makes a one-turn Haiku call first, only for its usage report. Triage itself now runs at
any usage and stops only when refused; the 0.8 cap guards builds, which are what use the plan.

Revised: the branch name came from triage's structured output. Triage no longer hands the build
anything but the label, so the build slugs the issue title itself.

Revised: no triage on `issues: edited` or comments. A parked issue carries `needs-user` and stays
out of the queue until the label comes off, so `issues: unlabeled` is the event that matters.

Revised (review): a run joins the concurrency group when queued, before `select`'s author or label check,
so an event that would skip could cancel a real waiting run and then do nothing. Skipping events now
get a group of their own (`issue-triage-skip-<run id>`, `issue-build-skip-<run id>`).

Revised (review): a failed usage probe read as headroom, so a Claude outage would fail every build in turn
and park the whole ready queue. A failed probe now stops the queue with the label kept.

Revised (review): triage did not skip `agent-working` issues, so removing `ready-for-agent` mid-build (or
an old issue-agent run at cutover) could get an issue built twice. Triage now skips them.

Checked: actionlint on both workflows; the `list` and `ready` queues against fake issues (ready,
ready with `needs-user`, ready with an open PR, ready from someone without write access, blocked, agent-working).
Not checked: a live run. Both workflows only take effect once on main.

Follow-up: a build refused at the Haiku probe because usage is high still marks the run red ("Stop
the queue" exits 1), every hour until usage drops, as the old workflow did. Worth deciding whether
that should be a warning instead.
