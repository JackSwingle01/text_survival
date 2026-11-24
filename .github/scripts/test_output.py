#!/usr/bin/env python3
"""What tests printed, from the trx files `dotnet test --logger trx` wrote.

  test_output.py RESULTS_DIR NAME    prints the output of every test whose name contains NAME, in name order

The nightly run reads the cohesion gate's lines this way, to publish them as main's baseline.
"""
import glob
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def main(results, name):
    found = {}
    for trx in sorted(glob.glob(f"{results}/**/*.trx", recursive=True)):
        for r in ET.parse(trx).getroot().iterfind(".//t:UnitTestResult", NS):
            if name in r.get("testName", ""):
                found[r.get("testName")] = (r.get("outcome"), r.findtext("t:Output/t:StdOut", "", NS))
    for test in sorted(found):
        outcome, out = found[test]
        print(f"{test}: {outcome}")
        for line in out.splitlines():
            if line.strip():
                print(line.rstrip())


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
