#!/usr/bin/env python3
"""Task state tool. Usage: task.py {new,set,list,ready,validate} <Game> ..."""
import argparse
import json
import subprocess
import sys

from studio_common import (AGENTS, MAX_RETRIES, PRIORITIES, STATUSES, VERIFICATIONS,
                           game_dir, repo_root)

TERMINAL = {"COMPLETED", "CANCELLED"}
TRANSITIONS = {
    "BACKLOG": {"ASSIGNED"},
    "ASSIGNED": {"IN_PROGRESS"},
    "IN_PROGRESS": {"REVIEW"},
    "REVIEW": {"QA", "FIX_REQUIRED"},
    "QA": {"COMPLETED", "AWAITING_UNITY", "FIX_REQUIRED"},
    "AWAITING_UNITY": {"COMPLETED", "FIX_REQUIRED"},
    "FIX_REQUIRED": {"IN_PROGRESS"},
    "BLOCKED": {"ASSIGNED"},
}


def tasks_dir(game):
    return game_dir(game) / "Tasks"


def load_all(game):
    d = tasks_dir(game)
    out = {}
    for p in sorted(d.glob("*.json")):
        t = json.loads(p.read_text(encoding="utf-8"))
        out[t.get("task_id", p.stem)] = t
    return out


def save(game, task):
    p = tasks_dir(game) / f"{task['task_id']}.json"
    p.write_text(json.dumps(task, indent=2, ensure_ascii=False, sort_keys=True) + "\n",
                 encoding="utf-8")


def fail(msg, code=1):
    print(msg, file=sys.stderr)
    sys.exit(code)


def cmd_new(a):
    d = tasks_dir(a.game)
    if not game_dir(a.game).is_dir():
        fail(f"unknown game: {a.game}")
    d.mkdir(exist_ok=True)
    prefix = a.game.upper()
    nums = [int(p.stem.rsplit("-", 1)[1]) for p in d.glob(f"{prefix}-*.json")
            if p.stem.rsplit("-", 1)[1].isdigit()]
    tid = f"{prefix}-{(max(nums) if nums else 0) + 1:03d}"
    for v in a.verify:
        if v not in VERIFICATIONS:
            fail(f"unknown verification: {v}")
    task = {
        "task_id": tid, "title": a.title, "description": a.desc, "agent": a.agent,
        "priority": a.priority, "status": "BACKLOG", "dependencies": a.dep,
        "file_scope": a.scope, "deliverables": a.deliverable,
        "acceptance_criteria": a.accept, "verification": a.verify,
        "retry_count": 0, "base_commit": None, "result": {}, "notes": [],
    }
    save(a.game, task)
    print(tid)


def cmd_set(a):
    tasks = load_all(a.game)
    if a.id not in tasks:
        fail(f"unknown task: {a.id}")
    t = tasks[a.id]
    cur = t["status"]
    if a.status and a.status != cur:
        new = a.status
        allowed = set(TRANSITIONS.get(cur, set()))
        if cur not in TERMINAL:
            allowed |= {"BLOCKED", "CANCELLED"}
        if new not in allowed:
            fail(f"{a.id}: transition {cur} -> {new} not allowed")
        if cur == "FIX_REQUIRED" and new == "IN_PROGRESS":
            if a.agent and a.agent != t["agent"]:
                t["retry_count"] = 0
            elif t["retry_count"] >= MAX_RETRIES:
                fail(f"{a.id}: retry limit ({MAX_RETRIES}) reached; reassign with --agent or set BLOCKED")
            else:
                t["retry_count"] += 1
        if new == "IN_PROGRESS" and not t.get("base_commit"):
            t["base_commit"] = subprocess.run(
                ["git", "rev-parse", "HEAD"], cwd=repo_root(), capture_output=True,
                text=True, check=True).stdout.strip()
        t["status"] = new
    if a.agent:
        t["agent"] = a.agent
    for kv in a.result:
        if "=" not in kv:
            fail(f"--result expects KEY=VALUE, got {kv}")
        k, v = kv.split("=", 1)
        t.setdefault("result", {})[k] = v
    for n in a.note:
        t.setdefault("notes", []).append(n)
    save(a.game, t)
    print(f"{a.id}: {t['status']}")


def cmd_list(a):
    for tid, t in load_all(a.game).items():
        if a.status and t["status"] != a.status:
            continue
        print(f"{tid}\t{t['status']}\t{t['agent']}\t{t['title']}")


def cmd_ready(a):
    tasks = load_all(a.game)
    for tid, t in tasks.items():
        if t["status"] == "BACKLOG" and all(
                tasks.get(d, {}).get("status") == "COMPLETED" for d in t["dependencies"]):
            print(tid)


def find_cycles(tasks):
    state, cyc = {}, set()

    def visit(n, stack):
        state[n] = 1
        for d in tasks[n].get("dependencies", []):
            if d not in tasks:
                continue
            if state.get(d) == 1:
                cyc.update(stack[stack.index(d):])
            elif d not in state:
                visit(d, stack + [d])
        state[n] = 2

    for n in tasks:
        if n not in state:
            visit(n, [n])
    return sorted(cyc)


def cmd_validate(a):
    tasks = load_all(a.game)
    errors = []
    for tid, t in tasks.items():
        if t.get("status") not in STATUSES:
            errors.append(f"{tid}: invalid status {t.get('status')!r}")
        if t.get("agent") not in AGENTS:
            errors.append(f"{tid}: invalid agent {t.get('agent')!r}")
        if t.get("priority") not in PRIORITIES:
            errors.append(f"{tid}: invalid priority {t.get('priority')!r}")
        for v in t.get("verification", []):
            if v not in VERIFICATIONS:
                errors.append(f"{tid}: invalid verification {v!r}")
        for d in t.get("dependencies", []):
            if d not in tasks:
                errors.append(f"{tid}: unknown dependency {d}")
    cyc = find_cycles(tasks)
    if cyc:
        errors.append("dependency cycle: " + ", ".join(cyc))
    for e in errors:
        print(e)
    if errors:
        sys.exit(1)
    print(f"OK ({len(tasks)} tasks)")


def main():
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    n = sub.add_parser("new"); n.add_argument("game")
    n.add_argument("--title", required=True)
    n.add_argument("--agent", required=True, choices=AGENTS)
    n.add_argument("--desc", default="")
    n.add_argument("--priority", default="medium", choices=PRIORITIES)
    for flag in ["dep", "scope", "deliverable", "accept", "verify"]:
        n.add_argument(f"--{flag}", action="append", default=[])
    s = sub.add_parser("set"); s.add_argument("game"); s.add_argument("id")
    s.add_argument("--status", choices=STATUSES)
    s.add_argument("--agent", choices=AGENTS)
    s.add_argument("--result", action="append", default=[])
    s.add_argument("--note", action="append", default=[])
    l = sub.add_parser("list"); l.add_argument("game"); l.add_argument("--status")
    for name in ["ready", "validate"]:
        sub.add_parser(name).add_argument("game")
    a = p.parse_args()
    {"new": cmd_new, "set": cmd_set, "list": cmd_list,
     "ready": cmd_ready, "validate": cmd_validate}[a.cmd](a)


if __name__ == "__main__":
    main()
