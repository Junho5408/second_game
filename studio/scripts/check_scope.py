#!/usr/bin/env python3
"""Report files a task touched outside what its role may modify.

Usage: check_scope.py <Game> <TaskID> [--base REF] [--role impl|review|qa]

Roles (which commits count, and what they may touch):
  impl   (default) commits whose subject starts with "<ID>:"  -> the task's file_scope
  review commits whose subject starts with "review(<ID>):"    -> Reports/review/<ID>.md
  qa     commits whose subject starts with "qa(<ID>):"        -> Unity/Assets/Game/Tests/, Reports/qa/, Reports/build/
Uncommitted and untracked changes always count. The task's own file is always allowed.
Exit 0 = OK, 1 = violations (printed one per line), 2 = no base available.
"""
import argparse
import fnmatch
import json
import subprocess
import sys

from studio_common import game_dir, repo_root

ROLE_PREFIX = {"impl": "{id}:", "review": "review({id}):", "qa": "qa({id}):"}


def _match(path, scope):
    for entry in scope:
        if entry.endswith("/"):
            if path.startswith(entry) or path == entry.rstrip("/"):
                return True
        elif path == entry or fnmatch.fnmatchcase(path, entry):
            return True
    return False


def in_scope(path: str, scope: list, task_file: str) -> bool:
    """Implementer rule: file_scope (+ .meta), own task file, and the game's Reports/."""
    if path == task_file:
        return True
    # Pipeline reports (review/QA/build) of the same game are written by other agents during the task.
    game_root = task_file.split("/Tasks/")[0]
    if path.startswith(game_root + "/Reports/"):
        return True
    if _match(path, scope):
        return True
    if path.endswith(".meta"):
        return _match(path[: -len(".meta")], scope)
    return False


def role_scope(role, game_root, task_id, file_scope):
    if role == "review":
        return [f"{game_root}/Reports/review/{task_id}.md"]
    if role == "qa":
        return [f"{game_root}/Unity/Assets/Game/Tests/", f"{game_root}/Reports/qa/",
                f"{game_root}/Reports/build/"]
    return file_scope


def allowed(path, role, scope, task_file):
    if role == "impl":
        return in_scope(path, scope, task_file)
    if path == task_file or _match(path, scope):
        return True
    return path.endswith(".meta") and _match(path[: -len(".meta")], scope)


def _git_z(*args):
    """Run git with NUL-separated output so non-ASCII paths are not quoted/escaped."""
    out = subprocess.run(["git", *args], cwd=repo_root(), capture_output=True, check=True).stdout
    return [p.decode("utf-8") for p in out.split(b"\0") if p]


def changed_files(base, task_id, role="impl"):
    prefix = ROLE_PREFIX[role].format(id=task_id)
    files = set()
    log = subprocess.run(["git", "log", "--format=%H%x00%s", f"{base}..HEAD"], cwd=repo_root(),
                         capture_output=True, check=True).stdout.decode("utf-8")
    for line in log.splitlines():
        sha, _, subject = line.partition("\0")
        if subject.startswith(prefix):  # subject only — body lines never count
            files.update(_git_z("diff-tree", "--no-commit-id", "-r", "--name-only",
                                "--no-renames", "-z", sha))
    files.update(_git_z("diff", "--name-only", "--no-renames", "-z", "HEAD"))
    files.update(_git_z("ls-files", "--others", "--exclude-standard", "-z"))
    return sorted(files)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("game"); p.add_argument("task_id"); p.add_argument("--base")
    p.add_argument("--role", choices=sorted(ROLE_PREFIX), default="impl")
    a = p.parse_args()
    tpath = game_dir(a.game) / "Tasks" / f"{a.task_id}.json"
    task = json.loads(tpath.read_text(encoding="utf-8"))
    base = a.base or task.get("base_commit")
    if not base:
        print("no base: pass --base or move the task to IN_PROGRESS first", file=sys.stderr)
        sys.exit(2)
    task_file = tpath.relative_to(repo_root()).as_posix()
    game_root = task_file.split("/Tasks/")[0]
    scope = role_scope(a.role, game_root, a.task_id, task.get("file_scope", []))
    bad = [f for f in changed_files(base, a.task_id, a.role)
           if not allowed(f, a.role, scope, task_file)]
    for f in bad:
        print(f)
    if bad:
        sys.exit(1)
    print("OK")


if __name__ == "__main__":
    main()
