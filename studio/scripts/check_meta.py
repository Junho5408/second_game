#!/usr/bin/env python3
"""Check a Unity project's Assets for missing/orphan .meta files and duplicate GUIDs.

Usage: check_meta.py <UnityProjectDir>
"""
import re
import sys
from collections import defaultdict
from pathlib import Path

from meta_common import is_ignored

GUID_RE = re.compile(r"^guid: ([0-9a-fA-F]{32})\s*$", re.M)


def main():
    if len(sys.argv) != 2:
        print(__doc__, file=sys.stderr); sys.exit(2)
    proj = Path(sys.argv[1])
    assets = proj / "Assets"
    missing, orphan, guids = [], [], defaultdict(list)
    for p in sorted(assets.rglob("*")):
        rel = p.relative_to(proj)
        if any(is_ignored(Path(part)) for part in rel.parts):
            continue
        if p.name.endswith(".meta"):
            target = p.with_name(p.name[:-5])
            if not target.exists():
                orphan.append(rel.as_posix())
            m = GUID_RE.search(p.read_text(encoding="utf-8", errors="replace"))
            if m:
                guids[m.group(1).lower()].append(rel.as_posix())
        elif not p.with_name(p.name + ".meta").exists():
            missing.append(rel.as_posix())
    dups = {g: ps for g, ps in guids.items() if len(ps) > 1}
    ok = True
    if missing:
        ok = False; print("Missing .meta:"); [print("  " + m) for m in missing]
    if orphan:
        ok = False; print("Orphan .meta:"); [print("  " + o) for o in orphan]
    if dups:
        ok = False; print("Duplicate GUID:")
        for g, ps in sorted(dups.items()):
            print(f"  {g}"); [print("    " + x) for x in ps]
    if not ok:
        sys.exit(1)
    print("OK")


if __name__ == "__main__":
    main()
