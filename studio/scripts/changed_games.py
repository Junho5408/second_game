#!/usr/bin/env python3
"""Print a JSON array of games whose Unity project or Core.Tests changed between two commits.

Usage: changed_games.py --base SHA --head SHA
If base is all zeros or unknown to git, every existing game (with a Unity folder) is listed.
"""
import argparse
import json
import subprocess

from studio_common import repo_root


def all_games(root):
    games = root / "Games"
    return sorted(p.name for p in games.iterdir() if (p / "Unity").is_dir()) if games.is_dir() else []


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--base", required=True); p.add_argument("--head", required=True)
    a = p.parse_args()
    root = repo_root()
    known = set(a.base.strip("0")) and subprocess.run(
        ["git", "cat-file", "-e", f"{a.base}^{{commit}}"], cwd=root,
        capture_output=True).returncode == 0
    if not known:
        print(json.dumps(all_games(root)))
        return
    files = subprocess.run(["git", "diff", "--name-only", a.base, a.head], cwd=root,
                           capture_output=True, text=True, check=True).stdout.splitlines()
    games = set()
    for f in files:
        parts = f.split("/")
        if len(parts) >= 3 and parts[0] == "Games" and parts[2] in ("Unity", "Core.Tests"):
            if (root / "Games" / parts[1] / "Unity").is_dir():
                games.add(parts[1])
    print(json.dumps(sorted(games)))


if __name__ == "__main__":
    main()
