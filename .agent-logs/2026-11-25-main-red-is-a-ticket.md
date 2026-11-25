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

Checked: against a stub `gh`. `issue_queue.py` `list`, `ready`, `check` and `brief` with a `main-red`
issue (queued; ready only with the label; skipped with `needs-user`; the brief takes the run from the
bot's newest comment, ignores a run linked by someone without write access, and reports a log it
cannot fetch). `main_red.sh`, the five cases of the first log, plus the dispatch after a create.
actionlint on the three workflows.
Not checked: on a runner. All of it runs from main. Unproven there: that the jobs' logs can be read
with `actions: read` while the CI run's last job is still going, and how Sonnet applies the body's rule.
Watch: the first red main after the merge: the issue's body, the triage run it dispatches (its brief's
log section and its verdict), and the build's select step.
