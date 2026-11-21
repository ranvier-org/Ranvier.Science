"""Checks that every snippet of docs/GUIDE.md equals the body of its test in GuideTests.fs.

Run from the repository root: python -I tests/guide-snippets.py
Standard library only. Exits 1 and names each mismatch, or exits 0 printing "12 snippets match".
"""

import re
import sys

GUIDE = "docs/GUIDE.md"
TESTS = "tests/Ranvier.Science.Tests/GuideTests.fs"
EXPECTED = 12


def read_lines(path):
    with open(path, encoding="utf-8") as f:
        return f.read().replace("\r\n", "\n").split("\n")


def test_body(lines, name):
    """The lines between `test "<name>" {` and its closing brace, dedented and stripped of trailing spaces."""
    header = 'test "%s" {' % name
    starts = [i for i, line in enumerate(lines) if line.strip() == header]
    if len(starts) != 1:
        return None
    start = starts[0]
    indent = len(lines[start]) - len(lines[start].lstrip())
    for end in range(start + 1, len(lines)):
        if lines[end] == " " * indent + "}":
            return [line[indent + 4:].rstrip() if line.strip() else "" for line in lines[start + 1:end]]
    return None


def guide_snippets(lines):
    """(name, block lines) for each `Test: "<name>"` line and the fsharp block after it."""
    found = []
    for i, line in enumerate(lines):
        m = re.match(r'Test: "(.+)" in ', line)
        if not m:
            continue
        j = i + 1
        while j < len(lines) and lines[j].strip() == "":
            j += 1
        if j >= len(lines) or lines[j].strip() != "```fsharp":
            found.append((m.group(1), None))
            continue
        k = j + 1
        while k < len(lines) and lines[k].strip() != "```":
            k += 1
        block = [l.rstrip() for l in lines[j + 1:k]]
        indents = [len(l) - len(l.lstrip()) for l in block if l.strip()]
        cut = min(indents) if indents else 0
        found.append((m.group(1), [l[cut:] if l.strip() else "" for l in block]))
    return found


def main():
    guide = read_lines(GUIDE)
    tests = read_lines(TESTS)
    snippets = guide_snippets(guide)
    problems = []
    for name, block in snippets:
        body = test_body(tests, name)
        if body is None:
            problems.append('"%s": no test of that name in %s' % (name, TESTS))
        elif block is None:
            problems.append('"%s": no fsharp block follows its Test line in %s' % (name, GUIDE))
        elif block != body:
            problems.append('"%s": the guide and the test differ' % name)
    if len(snippets) != EXPECTED:
        problems.append("%d snippets in %s, expected %d" % (len(snippets), GUIDE, EXPECTED))
    if problems:
        for p in problems:
            print("mismatch: " + p)
        return 1
    print("%d snippets match" % len(snippets))
    return 0


sys.exit(main())
