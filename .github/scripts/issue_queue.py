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
                            links, open PRs, and whether each repo path it names exists. Markdown.
  issue_queue.py pr N       URL of an open pull request for N, or nothing.

Needs GH_TOKEN and GITHUB_REPOSITORY. Calls the API through the gh CLI.
"""

import json
import os
import re
import subprocess
import sys

REPO = os.environ["GITHUB_REPOSITORY"]
NEEDS_USER = "needs-user"
READY = "ready-for-agent"
WORKING = "agent-working"
# Only people who can already push may spend the plan; the repo is public.
TRUSTED = {"OWNER", "MEMBER", "COLLABORATOR"}
# Issues the daily ratchet run (.github/workflows/ratchet.yml) files with this repo's GITHUB_TOKEN.
# Only this repo's workflows act as that bot here, and no workflow runs untrusted code with write
# access, so its issues carrying this label are as trusted as the tracker on main they came from.
RATCHET_BOT, RATCHET_LABEL = "github-actions[bot]", "ratchet"


def api(path):
    """Every item of a (paginated) list endpoint, or the single object of an item endpoint."""
    out = subprocess.run(
        ["gh", "api", "--paginate", path], check=True, capture_output=True, text=True
    ).stdout
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
    from_ratchet_run = issue["user"]["login"] == RATCHET_BOT and RATCHET_LABEL in labels
    if issue["author_association"] not in TRUSTED and not from_ratchet_run:
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


def brief(number):
    issue = api(f"repos/{REPO}/issues/{number}")
    out = [f"# #{number}: {issue['title']}\n", f"Opened by {issue['user']['login']} on {issue['created_at']}.\n",
           issue["body"] or "(no description)"]
    trusted_text = [issue["body"] or ""]
    comments = api(f"repos/{REPO}/issues/{number}/comments?per_page=100")
    untrusted = 0
    for c in comments:
        if c["author_association"] not in TRUSTED:
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
