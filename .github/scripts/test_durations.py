#!/usr/bin/env python3
"""Fails when a test in a fast-suite run took longer than the limit.

Usage: test_durations.py RESULTS.trx [--max-seconds 5] [--top 15]

Reads the .trx that `dotnet test --logger "trx;LogFileName=..."` writes. Run it on a fast-suite run
(`--filter "Category!=Slow"`): every test in it is a fast test, so any that took longer than the
limit should either get quicker or be tagged `[Trait("Category", TestCategories.Slow)]`.
Prints the slowest tests either way, so a passing run still shows what is close to the line.
"""

import argparse
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def seconds(duration: str) -> float:
    # hh:mm:ss.fffffff
    h, m, s = duration.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("trx", nargs="+")
    ap.add_argument("--max-seconds", type=float, default=5.0)
    ap.add_argument("--top", type=int, default=15)
    args = ap.parse_args()

    results = []
    for path in args.trx:
        for r in ET.parse(path).getroot().iterfind(".//t:UnitTestResult", NS):
            if r.get("outcome") == "NotExecuted" or r.get("duration") is None:
                continue
            results.append((seconds(r.get("duration")), r.get("testName")))
    results.sort(reverse=True)

    total = sum(d for d, _ in results)
    print(f"{len(results)} tests, {total:.0f} s summed test time. Slowest:")
    for d, name in results[: args.top]:
        print(f"  {d:7.2f} s  {name}")

    over = [(d, n) for d, n in results if d > args.max_seconds]
    if not over:
        return 0
    print(f"\n{len(over)} fast test(s) took over {args.max_seconds:g} s. Make each quicker, or tag it "
          '[Trait("Category", TestCategories.Slow)] so it runs nightly:')
    for d, name in over:
        print(f"::error::{name} took {d:.1f} s (limit {args.max_seconds:g} s)")
    return 1


if __name__ == "__main__":
    sys.exit(main())
