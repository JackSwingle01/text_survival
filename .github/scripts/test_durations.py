#!/usr/bin/env python3
"""Fails when a fast test took longer than the limit.

Usage: test_durations.py RESULTS.trx [--max-seconds 5] [--allow FILE] [--top 15]

Reads the .trx that `dotnet test --logger "trx;LogFileName=..."` writes. Run it on a quick-tier run
(`--filter "Category!=Slow&Category!=LongRun"`) in Release, as CI does: any test in it that took longer
than the limit should either get quicker or be tagged `[Trait("Category", TestCategories.LongRun)]`.
The tests named in --allow (one per line, `#` comments, theory parameters left off) are known breaches.
Prints the slowest tests either way, so a passing run still shows what is close to the line; without
--max-seconds it only prints.

A test's time inside a parallel run includes waiting on the others (CI measured 0.1 s tests at 7 s), so
CI screens the parallel run, re-runs what it flags alone and serially (--names-only lists them), and
gates on that run.
"""

import argparse
import re
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def seconds(duration: str) -> float:
    # hh:mm:ss.fffffff
    h, m, s = duration.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def method(test_name: str) -> str:
    # A theory case is "Class.Method(arg: value, ...)"; the allow list names the method.
    return re.sub(r"\(.*$", "", test_name)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("trx", nargs="+")
    ap.add_argument("--max-seconds", type=float, default=float("inf"))
    ap.add_argument("--allow", help="file of test names allowed over the limit")
    ap.add_argument("--top", type=int, default=15)
    ap.add_argument("--names-only", action="store_true",
                    help="print only the over-limit tests' method names, one per line, and exit 0")
    ap.add_argument("--warn", action="store_true",
                    help="report over-limit tests as ::warning:: annotations and exit 0")
    args = ap.parse_args()

    allowed: set[str] = set()
    if args.allow:
        with open(args.allow, encoding="utf-8") as f:
            allowed = {line.strip() for line in f if line.strip() and not line.startswith("#")}

    results = []
    for path in args.trx:
        for r in ET.parse(path).getroot().iterfind(".//t:UnitTestResult", NS):
            if r.get("outcome") == "NotExecuted" or r.get("duration") is None:
                continue
            results.append((seconds(r.get("duration")), r.get("testName")))
    results.sort(reverse=True)
    over = [(d, n) for d, n in results if d > args.max_seconds and method(n) not in allowed]

    if args.names_only:
        print("\n".join(dict.fromkeys(method(n) for _, n in over)))
        return 0

    total = sum(d for d, _ in results)
    print(f"{len(results)} tests, {total:.0f} s summed test time. Slowest:")
    for d, name in results[: args.top]:
        mark = "  (allowed)" if method(name) in allowed else ""
        print(f"  {d:7.2f} s  {name}{mark}")

    if not over:
        if args.max_seconds != float("inf"):
            print(f"\nNo test{' outside ' + args.allow if args.allow else ''} took over {args.max_seconds:g} s.")
        return 0
    print(f"\n{len(over)} quick test(s) took over {args.max_seconds:g} s. Make each quicker (a test that steps "
          "World.New(seed) pays for every animal in it), or tag it "
          '[Trait("Category", TestCategories.LongRun)] so it runs in the long-run check:')
    for d, name in over:
        level = "warning" if args.warn else "error"
        print(f"::{level}::{name} took {d:.1f} s (limit {args.max_seconds:g} s)")
    return 0 if args.warn else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except BrokenPipeError:  # piped into head
        sys.exit(0)
