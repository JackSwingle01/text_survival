# Issue build: background tasks on, 30 minutes a command

Hypothesis: the builds wait badly because `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` takes away
`run_in_background`, which CLAUDE.md tells them to wait with. #84 called it and got a validation error;
#87 polled with `sleep` 15 times (98 of its 123 minutes, on an NPC bench whose "before" run never
finished). Claude Code's docs now say a headless run stays open after its last turn for background
commands and sub-agents and takes a turn with each result, so the flag can go.

- Changes: the flag is removed from the Implement step; `CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS` is 30
  minutes (the idle wait for sub-agents; the default 10 drops them unheard); the prompt says to run long
  commands in the background with `timeout: 1800000`, that 30 minutes is the most one command gets, and
  that a longer measurement is left for a person and named in the PR's Evidence.
- Affects: a build no longer runs a bench that takes over 30 minutes, whatever the ticket's criteria say.
  `claude-review.yml` keeps the flag.

Revised: `BASH_DEFAULT_TIMEOUT_MS=1800000` to make 30 minutes the background default -> not set: the same
variable is the foreground default, so a hung foreground command would block 30 minutes instead of 2.
The agent passes the timeout on each call instead; one that forgets is stopped at 10 minutes and told why.
Revised: a hook that reminds an idle run to check its background tasks -> none: the docs list no timer
or idle hook event, and a Stop hook's input does not list running tasks. The command's time limit does
the job: at 30 minutes it is stopped and the agent gets a turn saying so.

Checked: the workflow parses as YAML.
Not checked: a live run. Dispatch workflows run from main, so the first build after merge is the test:
its transcript should show `run_in_background` calls, no `sleep` polls, and a result after the last turn.

Follow-up: if that build exits before a background sub-agent reports (what `claude-review.yml` saw), put
the flag back and tell the build to wait in the foreground with `timeout` and `wait`.
Follow-up: `NpcBenchReport` prints nothing until every seed has finished, so one stuck seed hides the rest.

Revised: background tasks on -> off again, the first follow-up's fallback. The first build after the
merge (#107) started its scout sub-agent in the background, wrote "Waiting for the scout's report" and
ended its turn; a run with `--json-schema` is told to call StructuredOutput the moment a turn ends, so
it answered needs_user 74 seconds in, with no code. The docs' "the run stays open for background work"
does not hold once structured output is enforced. The flag is back; `BASH_MAX_TIMEOUT_MS` is 30 minutes
(the default limit stays 2), and the prompt says to pass a `timeout` and wait in the foreground, with
`&` and `wait` for commands side by side. The 30-minute rule for measurements stays.
Not checked: a live run, again; the next build's transcript should show long commands with a `timeout`
and no `sleep` polls.
