#!/usr/bin/env python3
"""The daily ratchet run (.github/workflows/ratchet.yml): reads dev/ratchets.md and files at most one
issue per ratchet for the issue agent to build. The tracker's top section says what a ratchet is and
how to add one.

  ratchets.py run [--dry-run]   for each ratchet with no open `ratchet` issue, run its remaining
                                command and file the next batch (or the retire step when nothing
                                is left). --dry-run prints the issues instead of filing them.
  ratchets.py list              each ratchet's name, batch and remaining count, without filing.

Needs GH_TOKEN and GITHUB_REPOSITORY to file (not for --dry-run or list). Calls GitHub through gh.
"""

import json
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TRACKER = ROOT / "dev" / "ratchets.md"
LABEL = "ratchet"


class TrackerError(Exception):
    pass


def parse(text):
    """The ratchets in the tracker, in file order. Everything before the first section after
    "## Adding a ratchet" is the file's own documentation."""
    sections = re.split(r"^## (.+)$", text, flags=re.M)
    ratchets = []
    for name, body in zip(sections[1::2], sections[2::2]):
        name = name.strip()
        if name == "Adding a ratchet":
            continue
        if not re.fullmatch(r"[a-z][a-z0-9-]*", name):
            raise TrackerError(f"section '{name}': a ratchet's name is lower-case letters, digits and hyphens")
        batch = re.search(r"^- \*\*batch:\*\* *(\d+) *$", body, flags=re.M)
        if not batch or int(batch.group(1)) < 1:
            raise TrackerError(f"{name}: needs a '- **batch:** N' line with N at least 1")
        block = re.search(r"^```bash\n(.*?)^```$", body, flags=re.M | re.S)
        if not block:
            raise TrackerError(f"{name}: needs a ```bash block with its remaining command")
        when_empty = re.search(r"^When empty:(.*)\Z", body, flags=re.M | re.S)
        if not when_empty or not when_empty.group(1).strip():
            raise TrackerError(f"{name}: needs a paragraph starting 'When empty:'")
        job = body[block.end():when_empty.start()].strip()
        if not job:
            raise TrackerError(f"{name}: needs the job, between its command and 'When empty:'")
        ratchets.append({
            "name": name,
            "batch": int(batch.group(1)),
            "remaining": block.group(1),
            "job": job,
            "when_empty": " ".join(when_empty.group(1).split()),
        })
    return ratchets


def remaining(ratchet):
    result = subprocess.run(["bash", "-eo", "pipefail", "-c", ratchet["remaining"]], cwd=ROOT,
                            capture_output=True, text=True)
    if result.returncode != 0:
        raise TrackerError(f"{ratchet['name']}: remaining command exited {result.returncode}:\n{result.stderr}")
    return [line.strip() for line in result.stdout.splitlines() if line.strip()]


def title_prefix(ratchet):
    return f"Ratchet: {ratchet['name']}"


TITLE_NAMES_MAX = 200  # GitHub refuses a title over 256 characters; the rest is the prefix.


def title_names(names):
    """The items' names for a title, cut to the first few with "and N more" when all of them are too long."""
    for shown in range(len(names), 0, -1):
        text = ", ".join(names[:shown]) + (f" and {len(names) - shown} more" if shown < len(names) else "")
        if len(text) <= TITLE_NAMES_MAX or shown == 1:
            return text
    return ""


def batch_issue(ratchet, items):
    taken = items[:ratchet["batch"]]
    left = len(items) - len(taken)
    names = title_names([Path(i).name for i in taken])
    body = "\n".join([
        f"Ratchet `{ratchet['name']}` (`dev/ratchets.md`): the next {len(taken)} items. "
        f"{left} more remain after this batch.",
        "",
        *[f"- `{i}`" for i in taken],
        "",
        "## The job",
        "",
        ratchet["job"],
        "",
        "## Acceptance criteria",
        "",
        "- Every item above is done as the job says, including its check.",
        f"- The `{ratchet['name']}` remaining command in `dev/ratchets.md` no longer prints any of them.",
        "- Nothing outside these items changes, except where the job says to (an exception list).",
        "",
        "Filed by the daily ratchet run (`.github/workflows/ratchet.yml`). The next batch is filed the day "
        "after this one closes.",
    ])
    return f"{title_prefix(ratchet)} — {names}", body


def retire_issue(ratchet):
    body = "\n".join([
        f"Ratchet `{ratchet['name']}` (`dev/ratchets.md`) has nothing left: its remaining command prints "
        "nothing. Make the check hold everywhere and retire the ratchet.",
        "",
        "## The job",
        "",
        ratchet["when_empty"][:1].upper() + ratchet["when_empty"][1:],
        "",
        f"Then delete the `## {ratchet['name']}` section from `dev/ratchets.md`.",
        "",
        "## Acceptance criteria",
        "",
        "- The step above is done, and the check it widens passes on `main` as it stands.",
        f"- `dev/ratchets.md` has no `{ratchet['name']}` section, and "
        "`python3 .github/scripts/ratchets.py list` still runs.",
        "",
        "Filed by the daily ratchet run (`.github/workflows/ratchet.yml`).",
    ])
    return f"{title_prefix(ratchet)} — retire", body


def gh(*args):
    return subprocess.run(["gh", *args], check=True, capture_output=True, text=True).stdout


def open_ratchet_titles():
    out = gh("issue", "list", "--repo", os.environ["GITHUB_REPOSITORY"], "--state", "open",
             "--label", LABEL, "--limit", "200", "--json", "title")
    return [i["title"] for i in json.loads(out)]


def run(dry_run):
    ratchets = parse(TRACKER.read_text())
    open_titles = [] if dry_run else open_ratchet_titles()
    failed = []
    for r in ratchets:
        in_flight = next((t for t in open_titles if t == title_prefix(r) or t.startswith(title_prefix(r) + " ")), None)
        if in_flight:
            print(f"{r['name']}: skipped, already open: {in_flight}")
            continue
        try:
            items = remaining(r)
        except TrackerError as e:
            # One broken command must not stop the other ratchets' batches.
            print(f"::error::dev/ratchets.md: {e}")
            failed.append(r["name"])
            continue
        title, body = batch_issue(r, items) if items else retire_issue(r)
        if dry_run:
            print(f"===== {title}\n{body}\n")
            continue
        url = gh("issue", "create", "--repo", os.environ["GITHUB_REPOSITORY"], "--title", title,
                 "--label", LABEL, "--body", body).strip()
        print(f"{r['name']}: filed {url} ({len(items)} remaining)")
    if failed:
        raise TrackerError("remaining command failed for " + ", ".join(failed))


def main(argv):
    try:
        if argv[:1] == ["run"] and argv[1:] in ([], ["--dry-run"]):
            run(dry_run=argv[1:] == ["--dry-run"])
        elif argv == ["list"]:
            for r in parse(TRACKER.read_text()):
                print(f"{r['name']}: batch {r['batch']}, {len(remaining(r))} remaining")
        else:
            sys.exit(__doc__)
    except TrackerError as e:
        sys.exit(f"dev/ratchets.md: {e}")


if __name__ == "__main__":
    main(sys.argv[1:])
