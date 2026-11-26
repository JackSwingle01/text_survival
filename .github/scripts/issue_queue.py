#!/usr/bin/env python3
"""The issue agent's two queues: which open issues triage (.github/workflows/issue-triage.yml) and
the build (.github/workflows/issue-build.yml) may pick up, oldest first. Every rule that keeps an
issue out of either queue lives here, so the selectors and the per-issue recheck cannot disagree.
The `ready-for-agent` label is the hand-off: triage adds it, the build takes only issues carrying it.

  issue_queue.py list [N]   triage's queue: JSON array of eligible issue numbers, oldest first (only N
                            if given). Each skipped issue and its reason goes to stderr.
  issue_queue.py check N    exit 0 if N is still eligible for triage, else print why and exit 1.
  issue_queue.py ready [N]  the build's queue: JSON {"eligible": [...], "stale": [...]}, oldest first
                            (only N if given). Stale issues carry the label but fail a rule now (a PR
                            opened, a blocker linked); the build takes the label off them.
  issue_queue.py brief N    the issue and its comments from people with write access, then the facts
                            an agent would otherwise fetch: referenced issues and their state, GitHub
                            links, open PRs, whether each repo path it names exists, and, for a
                            `main-red` issue, the failed jobs' log. Markdown.
  issue_queue.py pr N       URL of an open pull request for N, or nothing.

Needs GH_TOKEN and GITHUB_REPOSITORY. Calls the API through the gh CLI.
"""

import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request

REPO = os.environ["GITHUB_REPOSITORY"]
NEEDS_USER = "needs-user"
READY = "ready-for-agent"
WORKING = "agent-working"
# Only people who can already push may spend the plan; the repo is public.
TRUSTED = {"OWNER", "MEMBER", "COLLABORATOR"}
# Issues this repo's workflows file with its GITHUB_TOKEN: the daily ratchet run's batches
# (.github/workflows/ratchet.yml), the one issue open while main's CI is red (ci.yml's main-red job),
# and a failing nightly run's one issue (.github/workflows/nightly.yml).
# Only this repo's workflows act as that bot here, and no workflow runs untrusted code with write
# access, so its issues carrying one of these labels are as trusted as the main they came from.
WORKFLOW_BOT, MAIN_RED = "github-actions[bot]", "main-red"
WORKFLOW_LABELS = {"ratchet", MAIN_RED, "nightly"}


# GitHub's own failures, as the gh CLI words them on stderr: a 5xx status, or no answer at all.
# A 4xx is an answer (a missing blocked-by endpoint, a refusal) and is never retried.
TRANSIENT = re.compile(
    r"HTTP 5\d\d|Bad Gateway|Something went wrong|timeout|timed out|connection re(set|fused)|EOF|TLS handshake",
    re.IGNORECASE,
)
# Seconds to wait before each further try. GitHub's API failed for minutes on 2026-11-25, and one
# failed lookup ended the build's Report step before it had commented on the issue.
RETRY_WAITS = (5, 15, 45)


def gh_api(path, sleep=time.sleep):
    """`gh api --paginate` output, tried again while GitHub itself is failing."""
    for wait in (*RETRY_WAITS, None):
        try:
            return subprocess.run(
                ["gh", "api", "--paginate", path], check=True, capture_output=True, text=True
            ).stdout
        except subprocess.CalledProcessError as e:
            if wait is None or not TRANSIENT.search(e.stderr or ""):
                raise
            print(f"{path}: {e.stderr.strip()}; trying again in {wait}s", file=sys.stderr)
            sleep(wait)


def api(path):
    """Every item of a (paginated) list endpoint, or the single object of an item endpoint."""
    out = gh_api(path)
    # --paginate concatenates one JSON document per page.
    docs, decoder, i = [], json.JSONDecoder(), 0
    while i < len(out):
        if out[i].isspace():
            i += 1
            continue
        doc, i = decoder.raw_decode(out, i)
        docs.append(doc)
    if len(docs) == 1 and isinstance(docs[0], dict):
        return docs[0]
    return [item for doc in docs for item in doc]


def open_issues():
    items = api(f"repos/{REPO}/issues?state=open&sort=created&direction=asc&per_page=100")
    return [i for i in items if "pull_request" not in i]


def open_blockers(issue):
    """Open issues linked to this one with GitHub's native "blocked by". Blocking written only in
    the issue's text is triage's to read, not this script's."""
    try:
        return [b["number"] for b in api(f"repos/{REPO}/issues/{issue['number']}/dependencies/blocked_by")
                if b.get("state") == "open"]
    except subprocess.CalledProcessError as e:
        print(f"#{issue['number']}: native blocked-by lookup failed: {e.stderr.strip()}", file=sys.stderr)
        return []


class Repo:
    """Open pull requests and branches, fetched once per run."""

    def __init__(self):
        self.prs = api(f"repos/{REPO}/pulls?state=open&per_page=100")
        self.branches = [b["name"] for b in api(f"repos/{REPO}/branches?per_page=100")]

    def pr_for(self, number):
        closes = re.compile(
            rf"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+(?:{re.escape(REPO)})?#{number}\b", re.IGNORECASE
        )
        for pr in self.prs:
            if pr["head"]["ref"].startswith(f"{number}-") or closes.search(pr["body"] or ""):
                return pr["html_url"]
        return None

    def branch_for(self, number):
        return next((b for b in self.branches if b.startswith(f"{number}-")), None)


def skip_reason(issue, repo):
    """Why an agent must not pick this issue up now, or None if it may. Only facts GitHub records
    (author, labels, PRs, branches, native links) are checked here. Whether the issue itself is
    ready, a design ticket, a map or blocked in its text is triage's judgement."""
    n = issue["number"]
    labels = labels_of(issue)
    if issue["author_association"] not in TRUSTED and not from_workflow(issue):
        return f"opened by {issue['user']['login']}, who has no write access"
    if NEEDS_USER in labels:
        return f"labelled {NEEDS_USER}"
    if pr := repo.pr_for(n):
        return f"already has an open pull request, {pr}"
    if branch := repo.branch_for(n):
        return f"branch {branch} exists: someone is working on it"
    if blockers := open_blockers(issue):
        return "blocked by open " + ", ".join(f"#{b}" for b in blockers)
    return None


def labels_of(issue):
    return {label["name"] for label in issue["labels"]}


def from_workflow(issue):
    return issue["user"]["login"] == WORKFLOW_BOT and bool(WORKFLOW_LABELS & labels_of(issue))


def triage_skip_reason(issue, repo):
    """An issue triage already found ready waits for the build; remove the label to triage it again.
    An issue a build is working on has no pushed branch yet, so only its label says it is taken. A label
    left by a killed build holds its issue back until the next build run clears it, at most an hour."""
    labels = labels_of(issue)
    if READY in labels:
        return f"labelled {READY}: waiting for the build"
    if WORKING in labels:
        return f"labelled {WORKING}: a build is working on it"
    return skip_reason(issue, repo)


def try_api(path, what):
    """For context the brief can do without: a failure is reported in the brief and on stderr."""
    try:
        return api(path), None
    except subprocess.CalledProcessError as e:
        note = f"({what} could not be fetched: {e.stderr.strip()[:200]})"
        print(f"brief: {note}", file=sys.stderr)
        return None, note


def mentioned_issues(text, number):
    """Issue numbers in this repo written as #N or owner/repo#N. Over-matching is harmless here:
    the brief only adds context, and a miss costs the agent one `gh issue view`."""
    found = set()
    for m in re.finditer(r"(?<![\w/#&])(?:([\w.-]+/[\w.-]+))?#(\d+)\b", text):
        if m.group(1) is None or m.group(1).lower() == REPO.lower():
            found.add(int(m.group(2)))
    found.discard(number)
    return sorted(found)


def mentioned_paths(text):
    """Repo paths the text names: GitHub blob/tree links to this repo, and `code` spans with a slash."""
    paths = set()
    for m in re.finditer(rf"github\.com/{re.escape(REPO)}/(?:blob|tree)/[^/\s]+/([^\s)#?]+)", text, re.IGNORECASE):
        paths.add(m.group(1))
    for m in re.finditer(r"`([^`\s]+)`", text):
        token = m.group(1).rstrip(".,:;")
        if "/" in token and not token.startswith(("/", "http", "@")) and ":" not in token:
            paths.add(token.removeprefix("./"))
    return sorted(paths)


MENTIONED_TEXT_CHARS = 8000


def mentioned_text(item):
    """A mentioned issue's body and trusted comments, cut to MENTIONED_TEXT_CHARS so one long map
    can't crowd out the rest."""
    n = item["number"]
    if item["author_association"] not in TRUSTED:
        return f"### #{n}: {item['title']}\n\n(opened by someone without write access; text left out)"
    parts = [item["body"] or "(no description)"]
    comments, note = try_api(f"repos/{REPO}/issues/{n}/comments?per_page=100", f"comments on #{n}")
    if note:
        parts.append(note)
    for c in comments or []:
        if c["author_association"] in TRUSTED:
            parts.append(f"Comment by {c['user']['login']} on {c['created_at']}:\n\n{c['body']}")
    text = "\n\n".join(parts)
    if len(text) > MENTIONED_TEXT_CHARS:
        text = text[:MENTIONED_TEXT_CHARS] + f"\n\n(cut at {MENTIONED_TEXT_CHARS} characters)"
    return f"### #{n}: {item['title']}\n\n{text}"


LOG_CONTEXT_BEFORE, LOG_CONTEXT_AFTER, LOG_TAIL_LINES, LOG_CHARS = 3, 15, 40, 12000
LOG_FAILURE = re.compile(r"##\[error\]|\berror\b|\[FAIL\]|^\s*Failed\s", re.IGNORECASE)


def log_excerpt(log):
    """The lines of a job's log that say what failed: each line that reads as a failure with the lines
    around it (the assertion and the top of the stack trace follow the test's name), and the log's end.
    Cut to LOG_CHARS from the front, so a log that fails everywhere still shows its first failures."""
    log = re.sub(r"\x1b\[[0-9;]*[A-Za-z]", "", log)  # colour codes
    log = re.split(r"^.*Post job cleanup\.\s*$", log, maxsplit=1, flags=re.MULTILINE)[0]  # then only git's tidying
    lines = [re.sub(r"^\S*\d{4}-\d\d-\d\dT[\d:.]+Z ", "", line) for line in log.splitlines()]
    keep = set(range(max(0, len(lines) - LOG_TAIL_LINES), len(lines)))
    for i, line in enumerate(lines):
        if LOG_FAILURE.search(line):
            keep.update(range(max(0, i - LOG_CONTEXT_BEFORE), min(len(lines), i + LOG_CONTEXT_AFTER + 1)))
    out, last = [], None
    for i in sorted(keep):
        if last is not None and i != last + 1:
            out.append("[...]")
        out.append(lines[i])
        last = i
    text = "\n".join(out)
    return text if len(text) <= LOG_CHARS else text[:LOG_CHARS] + f"\n(cut at {LOG_CHARS} characters)"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def job_log(job_id):
    """A job's log as text. Not through gh: `gh api` refuses to print a response that has terminal
    escape sequences, and a log has its colour codes (#250's first triage was parked for this). The
    API answers with a redirect to a signed URL, which is fetched without the token."""
    api_url = os.environ.get("GITHUB_API_URL", "https://api.github.com")
    ask = urllib.request.Request(f"{api_url}/repos/{REPO}/actions/jobs/{job_id}/logs",
                                 headers={"Authorization": f"Bearer {os.environ['GH_TOKEN']}"})
    try:
        answer = urllib.request.build_opener(NoRedirect).open(ask, timeout=60)
    except urllib.error.HTTPError as e:
        if e.code not in (301, 302, 303, 307, 308):
            raise
        answer = urllib.request.urlopen(e.headers["Location"], timeout=60)
    return answer.read().decode("utf-8", errors="replace")


def failed_run_logs(text):
    """For a `main-red` issue: the failed jobs of the newest CI run its text links, each with its log's
    excerpt. The issue itself only names the checks, and triage has no GitHub access to read the run."""
    # Only the runs main_red.sh reported: the text also links triage's own runs (the footer of a comment
    # that parked the issue), and #250's second triage read one of those as the failing run.
    runs = re.findall(rf"CI failed on main at [^\n]*?github\.com/{re.escape(REPO)}/actions/runs/(\d+)", text,
                      re.IGNORECASE)
    if not runs:
        return "No failing CI run is linked."
    run = runs[-1]
    listing, note = try_api(f"repos/{REPO}/actions/runs/{run}/jobs?filter=latest&per_page=100", f"jobs of run {run}")
    if note:
        return note
    out = []
    for job in listing["jobs"]:
        if job["conclusion"] != "failure":
            continue
        steps = ", ".join(s["name"] for s in job.get("steps", []) if s.get("conclusion") == "failure")
        out.append(f"### {job['name']} (failed step: {steps or 'not recorded'})")
        try:
            out.append("```\n" + log_excerpt(job_log(job["id"])).replace("```", "~~~") + "\n```")
        except OSError as e:
            out.append(f"(log could not be fetched: {str(e)[:200]})")
            print(f"brief: log of job {job['id']} could not be fetched: {e}", file=sys.stderr)
    return f"Run {run}.\n\n" + ("\n\n".join(out) if out else "No job of it failed.")


def brief(number):
    issue = api(f"repos/{REPO}/issues/{number}")
    out = [f"# #{number}: {issue['title']}\n", f"Opened by {issue['user']['login']} on {issue['created_at']}.\n",
           issue["body"] or "(no description)"]
    trusted_text = [issue["body"] or ""]
    comments = api(f"repos/{REPO}/issues/{number}/comments?per_page=100")
    untrusted = 0
    # On an issue a workflow filed, the workflows' own comments are part of it (each later red run).
    own_bot = WORKFLOW_BOT if from_workflow(issue) else None
    for c in comments:
        if c["author_association"] not in TRUSTED and c["user"]["login"] != own_bot:
            untrusted += 1
            continue
        trusted_text.append(c["body"] or "")
        out.append(f"\n---\n\n## Comment by {c['user']['login']} on {c['created_at']}\n\n{c['body']}")
    if untrusted:
        out.append(f"\n---\n\n({untrusted} comments from people without write access were left out.)")

    out.append("\n---\n\n# Fetched for you (current state, not part of the issue)")
    links = []
    for path, label in [(f"issues/{number}/dependencies/blocked_by", "Blocked by"),
                        (f"issues/{number}/dependencies/blocking", "Blocking"),
                        (f"issues/{number}/sub_issues", "Sub-issues")]:
        items, note = try_api(f"repos/{REPO}/{path}", label)
        if note:
            links.append(f"- {label}: {note}")
        elif items:
            links.append(f"- {label}: " + ", ".join(f"#{i['number']} ({i['state']})" for i in items))
    try:
        parent = api(f"repos/{REPO}/issues/{number}/parent")
    except subprocess.CalledProcessError as e:
        parent = None  # 404 is "no parent"; anything else is worth a line in the brief.
        if "404" not in e.stderr:
            links.append(f"- Parent: (could not be fetched: {e.stderr.strip()[:200]})")
    if parent:
        links.append(f"- Parent: #{parent['number']} {parent['title']} ({parent['state']})")
    out.append("\n## GitHub links\n\n" + ("\n".join(links) if links else "None."))

    mentioned = mentioned_issues("\n".join(trusted_text), number)
    rows, texts = [], []
    for n in mentioned:
        item, note = try_api(f"repos/{REPO}/issues/{n}", f"#{n}")
        if note:
            rows.append(f"| #{n} | {note} | | |")
            continue
        kind = "PR" if "pull_request" in item else "issue"
        state = item["state"] + (f" ({item['state_reason']})" if item.get("state_reason") else "")
        rows.append(f"| #{n} | {item['title']} | {kind} | {state} |")
        # The agents get no GitHub access, so the issues it mentions come with their text, filtered by
        # the same rule as this issue's: only what people with write access wrote.
        if kind == "issue":
            texts.append(mentioned_text(item))
    out.append("\n## Issues and PRs it mentions\n\n" +
               ("| # | Title | Kind | State |\n|---|---|---|---|\n" + "\n".join(rows) if rows else "None."))
    if texts:
        out.append("\n## Text of the issues it mentions\n\n" + "\n\n".join(texts))

    prs, note = try_api(f"repos/{REPO}/pulls?state=open&per_page=100", "Open pull requests")
    if note:
        out.append(f"\n## Open pull requests\n\n{note}")
    else:
        out.append("\n## Open pull requests\n\n" + ("\n".join(
            f"- #{p['number']} {p['title']} (`{p['head']['ref']}`{', draft' if p['draft'] else ''})" for p in prs)
            if prs else "None."))

    root = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True).stdout.strip()
    paths = mentioned_paths("\n".join(trusted_text))
    if root and paths:
        out.append("\n## Repo paths it names (checked in this checkout)\n\n" + "\n".join(
            f"- `{p}`: {'exists' if os.path.exists(os.path.join(root, p)) else 'MISSING'}" for p in paths))
    if from_workflow(issue) and MAIN_RED in labels_of(issue):
        out.append("\n## The failing CI run (log excerpts)\n\n" + failed_run_logs("\n".join(trusted_text)))
    return "\n".join(out)


def main(argv):
    command, number = argv[1], int(argv[2]) if len(argv) > 2 else None
    if command == "list":
        repo, eligible = Repo(), []
        for issue in open_issues():
            if number is not None and issue["number"] != number:
                continue
            reason = triage_skip_reason(issue, repo)
            print(f"#{issue['number']} {issue['title']}: {reason or 'queued'}", file=sys.stderr)
            if reason is None:
                eligible.append(issue["number"])
        print(json.dumps(eligible))
    elif command == "ready":
        repo, eligible, stale = Repo(), [], []
        for issue in open_issues():
            if READY not in labels_of(issue) or (number is not None and issue["number"] != number):
                continue
            reason = skip_reason(issue, repo)
            print(f"#{issue['number']} {issue['title']}: {reason or 'ready'}", file=sys.stderr)
            (stale if reason else eligible).append(issue["number"])
        print(json.dumps({"eligible": eligible, "stale": stale}))
    elif command == "check":
        issue = api(f"repos/{REPO}/issues/{number}")
        reason = "closed" if issue["state"] != "open" else triage_skip_reason(issue, Repo())
        if reason:
            print(f"#{number} is no longer eligible: {reason}")
            return 1
    elif command == "brief":
        print(brief(number))
    elif command == "pr":
        print(Repo().pr_for(number) or "")
    else:
        sys.exit(f"unknown command {command}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
