#!/usr/bin/env bash
# Tries the issue build's Claude step before a change to it merges. issue-build.yml runs only from
# main, so a change to that step's `env` or `claude_args` is otherwise first run by a real build.
#
#   .github/scripts/try_build_step.sh    exits 0 when the toy build answered `built`
#
# It reads the step's env and its --json-schema from the workflow in this checkout and runs
# `claude -p` with them, on Haiku, on a toy task with the two things a build depends on: a sub-agent's
# report it needs before it can go on, and a command longer than the two-minute default. About four
# minutes. Needs `claude` signed in, and PyYAML. The run's events are kept in the file it names.
set -euo pipefail
cd "$(dirname "$0")/../.."
out=$(mktemp -d)
python3 - "$out" <<'PY'
import json, re, shlex, sys, yaml
steps = yaml.safe_load(open(".github/workflows/issue-build.yml"))["jobs"]["build"]["steps"]
step = next(s for s in steps if s.get("id") == "implement")
with open(f"{sys.argv[1]}/env.sh", "w") as f:
    for k, v in step.get("env", {}).items():
        f.write(f"export {k}={shlex.quote(str(v))}\n")
schema = re.search(r"--json-schema '(.*?)'", step["with"]["claude_args"], re.S).group(1)
json.dump(json.loads(schema), open(f"{sys.argv[1]}/schema.json", "w"))
PY
echo "The step's env:"; sed 's/^export /  /' "$out/env.sh"
# shellcheck disable=SC1091
source "$out/env.sh"
prompt='This is a test of a harness, not real work. Do exactly these steps in order, then answer.
1. Use the Agent tool (subagent_type general-purpose) to have a sub-agent run `sleep 45; echo scout-done` in Bash and report the output. You need its report for step 3.
2. Run `sleep 150; echo long-done` in Bash yourself. A command is stopped at two minutes unless you pass Bash a `timeout`; pass what it needs.
3. Answer through the structured output: outcome "built" if you saw both scout-done and long-done, else "needs_user"; note says which you saw.'
(cd "$out" && timeout 900 claude -p "$prompt" --model claude-haiku-5-5 --allowedTools "Bash,Agent,Task" \
  --json-schema "$(cat schema.json)" --output-format stream-json --verbose < /dev/null > events.jsonl) || true
python3 - "$out/events.jsonl" <<'PY'
import json, sys
outcome = None
for line in open(sys.argv[1]):
    try:
        e = json.loads(line)
    except ValueError:
        continue
    if e.get("type") == "result" and e.get("structured_output"):
        outcome = e["structured_output"]
        print(f"Answered after {e.get('duration_ms', 0) // 1000} s: {outcome}")
print(f"Events: {sys.argv[1]}")
if not outcome or outcome.get("outcome") != "built":
    sys.exit("FAILED: the toy build did not answer built. A build with these settings would end the same way.")
print("OK: the sub-agent reported inside the turn and the long command finished.")
PY
