#!/usr/bin/env python3
"""Report files changed since a task's base commit that fall outside its file_scope.

Usage: check_scope.py <Game> <TaskID> [--base REF]
Exit 0 = OK, 1 = violations (printed one per line), 2 = no base available.
"""
import argparse
import fnmatch
import json
import subprocess
import sys

from studio_common import game_dir, repo_root


def _match(path, scope):
    for entry in scope:
        if entry.endswith("/"):
            if path.startswith(entry) or path == entry.rstrip("/"):
                return True
        elif path == entry or fnmatch.fnmatchcase(path, entry):
            return True
    return False


def in_scope(path: str, scope: list, task_file: str) -> bool:
    if path == task_file:
        return True
    if _match(path, scope):
        return True
    if path.endswith(".meta"):
        return _match(path[: -len(".meta")], scope)
    return False


def changed_files(base):
    root = repo_root()
    def run(*args):
        return subprocess.run(["git", *args], cwd=root, capture_output=True,
                              text=True, check=True).stdout.splitlines()
    files = set(run("diff", "--name-only", base))
    files |= set(run("ls-files", "--others", "--exclude-standard"))
    return sorted(f for f in files if f)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("game"); p.add_argument("task_id"); p.add_argument("--base")
    a = p.parse_args()
    tpath = game_dir(a.game) / "Tasks" / f"{a.task_id}.json"
    task = json.loads(tpath.read_text(encoding="utf-8"))
    base = a.base or task.get("base_commit")
    if not base:
        print("no base: pass --base or move the task to IN_PROGRESS first", file=sys.stderr)
        sys.exit(2)
    task_file = tpath.relative_to(repo_root()).as_posix()
    bad = [f for f in changed_files(base) if not in_scope(f, task.get("file_scope", []), task_file)]
    for f in bad:
        print(f)
    if bad:
        sys.exit(1)
    print("OK")


if __name__ == "__main__":
    main()
