# Let the issue build run when a workflow dispatched it

Hypothesis: every overnight build failed at the usage probe because claude-code-action refuses a run
whose actor is a bot ("Workflow initiated by non-human actor: github-actions (type: Bot)"). Every
build is dispatched with GITHUB_TOKEN, by triage or by the build before it, so its actor is
github-actions. Naming that bot in `allowed_bots` on both Claude steps lets the dispatched builds run.

Revised: the hourly schedule that was meant to retry fired for no workflow in the repo overnight
(Nightly, Ratchet and the old Issue agent show the same gap; the last scheduled run was at 01:02 UTC).
So a stopped queue stayed stopped. That is GitHub's scheduler, not this change, and the schedule is
no reliable backstop.

Checked: actionlint; the action's actor check at the pinned commit (src/github/validation/actor.ts)
compares lowercased logins with `[bot]` stripped, and consults allowed_bots only for non-User accounts.
Not checked: a live dispatched build. Dispatch workflows run from main, so it is checked after merge.

Follow-up: the queue has no backstop when the scheduler stalls. A stopped build stays stopped until a
person dispatches one or adds a label.
