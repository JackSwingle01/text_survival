#!/usr/bin/env python3
"""The body of the nightly run's one issue: every failing test of the run, by job.

  nightly_report.py RESULTS_DIR    prints the issue body; exits 3 when no test failed

RESULTS_DIR holds one folder per nightly job (its name is the job's), each with the trx files that
job's `dotnet test` wrote. A job folder with no trx means the job failed before any test ran.

One issue for the whole run, not one per test: the issue agent builds each issue on its own branch,
so an issue per test had several agents fixing one cause in the same code.
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
# GitHub refuses a body over 65,536 characters; past the budget a test keeps its name and replay only.
BODY_BUDGET, MESSAGE_CHARS, STACK_CHARS = 55_000, 3_000, 2_000


def cut(text, limit):
    return text if len(text) <= limit else text[:limit] + f"\n... (cut at {limit} characters; the run's log has the rest)"


def failures(job_dir):
    """(test name, message, stack) for each failed result, or None when the job wrote no results."""
    trxs = glob.glob(f"{job_dir}/**/*.trx", recursive=True)
    if not trxs:
        return None
    failed = [r for trx in sorted(trxs) for r in ET.parse(trx).getroot().iterfind(".//t:UnitTestResult", NS)
              if r.get("outcome") == "Failed"]
    return sorted((r.get("testName"), r.findtext(".//t:ErrorInfo/t:Message", "", NS),
                   r.findtext(".//t:ErrorInfo/t:StackTrace", "", NS)) for r in failed)


def section(name, message, stack, full):
    method = name.split("(")[0]
    replay = f'dotnet test text_survival.sln -c Release --filter "FullyQualifiedName~{method}"'
    seeds = re.findall(r"CsCheck_Seed=([\w-]+)", message)
    if seeds:
        replay = f"CsCheck_Seed={seeds[0]} {replay}"
    out = f"### `{name}`\n\n```bash\n{replay}\n```\n\n"
    if not full:
        return out + "(Message left out for length; the run's log has it.)\n\n"
    return (out + f"```\n{cut(message, MESSAGE_CHARS)}\n```\n\n"
            f"<details><summary>Stack trace</summary>\n\n```\n{cut(stack, STACK_CHARS)}\n```\n</details>\n\n")


def main(results):
    sha, run = os.environ.get("GITHUB_SHA", "(unknown commit)"), os.environ.get("RUN_URL", "")
    jobs = sorted(d for d in os.listdir(results) if os.path.isdir(os.path.join(results, d)))
    parts, total, broken = [], 0, []
    for job in jobs:
        failed = failures(os.path.join(results, job))
        if failed is None:
            broken.append(job)
            parts.append(f"## {job}\n\nNo test results were written: the job failed before any test ran. See its log.\n\n")
        elif failed:
            total += len(failed)
            parts.append(f"## {job}: {len(failed)} failing\n\n")
            parts += [(name, message, stack) for name, message, stack in failed]
    if not total and not broken:
        return 3
    body = (f"The nightly run failed {total} test(s) at {sha}.\n\nRun: {run}\n\n"
            "Each test below has the command that replays it. Several may share one cause: find the causes "
            "first, then fix them together. The next failing nightly run rewrites this issue with its own failures.\n\n")
    for part in parts:
        body += part if isinstance(part, str) else section(*part, full=len(body) < BODY_BUDGET)
    print(body)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
