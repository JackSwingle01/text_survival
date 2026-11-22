#!/usr/bin/env python3
"""The issue agent's queue (.github/workflows/issue-agent.yml): which open issues an unattended
agent may pick up, oldest first. Every rule that keeps an issue out of the queue lives here, so the
selector and the per-issue recheck cannot disagree.

  issue_queue.py list [N]   JSON array of eligible issue numbers, oldest first (only N if given).
                            Each skipped issue and its reason goes to stderr.
  issue_queue.py check N    exit 0 if N is still eligible, else print why and exit 1.
  issue_queue.py brief N    the issue and its comments from people with write access, as Markdown.
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
# Only people who can already push may spend the plan; the repo is public.
TRUSTED = {"OWNER", "MEMBER", "COLLABORATOR"}


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
    labels = {label["name"] for label in issue["labels"]}
    if issue["author_association"] not in TRUSTED:
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


def main(argv):
    command, number = argv[1], int(argv[2]) if len(argv) > 2 else None
    if command == "list":
        repo, eligible = Repo(), []
        for issue in open_issues():
            if number is not None and issue["number"] != number:
                continue
            reason = skip_reason(issue, repo)
            print(f"#{issue['number']} {issue['title']}: {reason or 'queued'}", file=sys.stderr)
            if reason is None:
                eligible.append(issue["number"])
        print(json.dumps(eligible))
    elif command == "check":
        issue = api(f"repos/{REPO}/issues/{number}")
        reason = "closed" if issue["state"] != "open" else skip_reason(issue, Repo())
        if reason:
            print(f"#{number} is no longer eligible: {reason}")
            return 1
    elif command == "brief":
        issue = api(f"repos/{REPO}/issues/{number}")
        print(f"# #{number}: {issue['title']}\n")
        print(f"Opened by {issue['user']['login']} on {issue['created_at']}.\n")
        print(issue["body"] or "(no description)")
        comments = api(f"repos/{REPO}/issues/{number}/comments?per_page=100")
        untrusted = 0
        for c in comments:
            if c["author_association"] not in TRUSTED:
                untrusted += 1
                continue
            print(f"\n---\n\n## Comment by {c['user']['login']} on {c['created_at']}\n\n{c['body']}")
        if untrusted:
            print(f"\n---\n\n({untrusted} comments from people without write access were left out.)")
    elif command == "pr":
        print(Repo().pr_for(number) or "")
    else:
        sys.exit(f"unknown command {command}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
