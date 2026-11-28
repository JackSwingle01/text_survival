# The issue agent fixes a red main when the fix is obvious

- Task: Jack asked that the `main-red` issue go through the issue queue: fixed when the fix is trivial
  and obvious, tagged for a person when it is not.
- Hypothesis: the issue was kept out twice. The queue trusts the bot's issues only when labelled
  `ratchet`, and the build's select step builds nothing while a `main-red` issue is open. Triage also
  could not have judged it: the issue names the failed checks and links the run, and triage has no
  GitHub access.
- Changes:
  - `issue_queue.py`: the bot's `main-red` issues are trusted like its `ratchet` ones; on an issue a
    workflow filed, the bot's comments are part of the brief (each later red run); a `main-red` brief
    ends with the failed jobs' log excerpts from the newest linked run.
  - `main_red.sh`: the issue body is a ticket carrying the rule (ready only for a small, obvious fix,
    else a person; never skip, delete or loosen a test), and the script dispatches triage for the new
    issue, since an issue opened with GITHUB_TOKEN starts no workflows.
  - `issue-build.yml`: while main is red, the select step builds the `main-red` issue or nothing.
  - `ci.yml`, `issue-triage.yml`, `issue-build.yml`: `actions: write` to dispatch, `actions: read` for
    the logs.
- Affects: "needs human" is the existing `needs-user` label; both triage and the build already park
  with it. A parked `main-red` issue still holds the rest of the queue, as before.

Revised: the rule lives in the issue's body, not in the triage and build prompts. Both already treat
the issue as the spec, and one prompt stays true for every issue.

Revised: Jack asked for the nightly issue too → the bot's `nightly` issues are trusted the same way,
`nightly_report.py` puts the same rule in the body (with "takes a number" among the reasons for a
person), and the report job dispatches triage for a new issue. It needs no log in its brief (the body
has each message and replay command) and holds no queue (the Slow tests gate no PR). A rewritten issue
keeps its labels: one parked as `needs-user` stays parked under new failures.

Checked: against a stub `gh`. `issue_queue.py` `list`, `ready`, `check` and `brief` with a `main-red`
issue (queued; ready only with the label; skipped with `needs-user`; the brief takes the run from the
bot's newest comment, ignores a run linked by someone without write access, and reports a log it
cannot fetch). `main_red.sh`, the five cases of the first log, plus the dispatch after a create.
`nightly_report.py` on a sample trx (the rule is in the body); `list` queues a bot `nightly` issue.
actionlint on every workflow.
Not checked: on a runner. All of it runs from main. Unproven there: that the jobs' logs can be read
with `actions: read` while the CI run's last job is still going, and how Sonnet applies the body's rule.
Watch: the first red main after the merge: the issue's body, the triage run it dispatches (its brief's
log section and its verdict), and the build's select step.

Revised (first run on main, #250): triage parked the issue, "log could not be fetched (escape
sequences)". The runner's `gh api` refuses to print a response with terminal escape sequences, and a
job's log has its colour codes; the local `gh` (2.89) has no such check, and the stub printed no
colour codes → `job_log` asks the API itself, follows the redirect to the signed URL without the
token, and `log_excerpt` strips the colour codes. Checked against a local server that redirects,
refuses a token on the second request and serves a log with a BOM and colour codes.
What the first run did prove: the queue let the bot's issue through, the dispatch input worked, the
brief reported the failed fetch, and triage parked on it rather than guess.

Revised (second run on main, #250): parked again, "log wasn't available". The transcript shows the
brief said "Run 45687073200. No job of it failed.": that is triage's own run, linked in the footer of
the comment that parked the issue the first time, and the brief took the newest run linked anywhere
in the text → it takes only runs on a "CI failed on main at ..." line, which is what `main_red.sh`
writes. The fetch itself worked: no error, and `log_excerpt` on the real job's log shows the failing
test and its message.

Revised (third run on main, #250): triage marked it ready and the build selected #250 alone, then
started nothing: weekly plan usage was at 80%, the build's limit. Jack raised the build's
`MAX_PLAN_UTILIZATION` to 0.9. `resolve-conflicts.yml` keeps its own 0.8.

Revised (first unassisted red main, #311, 2026-11-26): the issue had the rule, CI dispatched triage,
and the brief held the failing test and its message; triage parked it (an NPC pacing regression, not
a small fix), which the rule asks for. But the dispatched run itself failed before the model ran:
the action refuses a workflow started by github-actions, and triage's step did not name the bot as
the build's does. A second triage run, started 56 s later by another issue's event, did the work
→ `allowed_bots: github-actions` on the Triage step. Half the excerpt was the runner's post-job git
cleanup → `log_excerpt` drops everything from "Post job cleanup." on.
Follow-up: the failing nightly of 2026-11-26 rewrote #215 with the rule and 12 failures, and it stayed
`needs-user` from its triage the day before, as designed. So no nightly issue has been triaged under
the rule yet. Whether a rewrite should take `needs-user` off and requeue the issue is Jack's call.
