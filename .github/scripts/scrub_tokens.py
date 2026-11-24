#!/usr/bin/env python3
"""Replaces credentials in every file under a directory, in place, before it is uploaded.

Usage: scrub_tokens.py DIR

Scrubs GitHub tokens, Anthropic keys and OAuth tokens, credentials in URLs, and the exact value of
the OAUTH environment variable when it is set (a token whose format the patterns miss).
"""

import os
import re
import sys
from pathlib import Path

PATTERNS = [
    re.compile(r"\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{20,}"),
    re.compile(r"\bgithub_pat_[A-Za-z0-9_]{20,}"),
    re.compile(r"\bsk-ant-[A-Za-z0-9_\-]{20,}"),
    # https://user:secret@host
    re.compile(r"(?<=://)[^/\s:@\"']+:[^/\s@\"']+(?=@)"),
    # Authorization: Bearer|token|Basic <value>
    re.compile(r"(?i)(?<=authorization: )(?:bearer|token|basic) [A-Za-z0-9._~+/=\-]+"),
]


def main() -> int:
    root = Path(sys.argv[1])
    exact = [v for v in [os.environ.get("OAUTH", "")] if len(v) >= 8]
    changed = 0
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        text = path.read_text(encoding="utf-8", errors="surrogateescape")
        new = text
        for value in exact:
            new = new.replace(value, "[REDACTED]")
        for pattern in PATTERNS:
            new = pattern.sub("[REDACTED]", new)
        if new != text:
            path.write_text(new, encoding="utf-8", errors="surrogateescape")
            changed += 1
    print(f"Scrubbed credentials in {changed} file(s) under {root}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
