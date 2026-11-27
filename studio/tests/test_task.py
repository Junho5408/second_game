import json

from conftest import read_task


def new(cli, title="t", agent="developer", *extra):
    r = cli("new", "Demo", "--title", title, "--agent", agent, *extra)
    assert r.returncode == 0, r.stderr
    return r.stdout.strip()


def set_status(cli, tid, status, *extra):
    return cli("set", "Demo", tid, "--status", status, *extra)


def test_new_creates_sequential_ids(task_cli, studio_repo):
    assert new(task_cli) == "DEMO-001"
    assert new(task_cli) == "DEMO-002"
    t = read_task(studio_repo, "Demo", "DEMO-001")
    assert t["status"] == "BACKLOG"
    assert t["retry_count"] == 0
    assert t["base_commit"] is None


def test_valid_transition_chain(task_cli, studio_repo):
    tid = new(task_cli)
    for s in ["ASSIGNED", "IN_PROGRESS", "REVIEW", "QA", "AWAITING_UNITY", "COMPLETED"]:
        r = set_status(task_cli, tid, s)
        assert r.returncode == 0, (s, r.stderr)
        if s == "IN_PROGRESS":
            bc = read_task(studio_repo, "Demo", tid)["base_commit"]
            assert len(bc) == 40 and all(c in "0123456789abcdef" for c in bc)
    assert read_task(studio_repo, "Demo", tid)["status"] == "COMPLETED"


def test_invalid_transition_rejected(task_cli, studio_repo):
    tid = new(task_cli)
    before = read_task(studio_repo, "Demo", tid)
    r = set_status(task_cli, tid, "COMPLETED")
    assert r.returncode == 1
    assert read_task(studio_repo, "Demo", tid) == before


def test_retry_limit(task_cli, studio_repo):
    tid = new(task_cli)
    for s in ["ASSIGNED", "IN_PROGRESS", "REVIEW", "FIX_REQUIRED"]:
        assert set_status(task_cli, tid, s).returncode == 0
    for _ in range(2):
        assert set_status(task_cli, tid, "IN_PROGRESS").returncode == 0
        assert set_status(task_cli, tid, "REVIEW").returncode == 0
        assert set_status(task_cli, tid, "FIX_REQUIRED").returncode == 0
    assert read_task(studio_repo, "Demo", tid)["retry_count"] == 2
    r = set_status(task_cli, tid, "IN_PROGRESS")
    assert r.returncode == 1
    assert "retry limit" in (r.stdout + r.stderr)
    r = set_status(task_cli, tid, "IN_PROGRESS", "--agent", "architect")
    assert r.returncode == 0, r.stderr
    t = read_task(studio_repo, "Demo", tid)
    assert t["retry_count"] == 0 and t["agent"] == "architect"


def write_raw(root, tid, **fields):
    base = {"task_id": tid, "title": "x", "description": "", "agent": "developer",
            "priority": "medium", "status": "BACKLOG", "dependencies": [],
            "file_scope": [], "deliverables": [], "acceptance_criteria": [],
            "verification": [], "retry_count": 0, "base_commit": None,
            "result": {}, "notes": []}
    base.update(fields)
    (root / "Games" / "Demo" / "Tasks" / f"{tid}.json").write_text(json.dumps(base))


def test_validate_detects_cycle(task_cli, studio_repo):
    write_raw(studio_repo, "DEMO-001", dependencies=["DEMO-002"])
    write_raw(studio_repo, "DEMO-002", dependencies=["DEMO-001"])
    r = task_cli("validate", "Demo")
    assert r.returncode == 1
    assert "cycle" in r.stdout and "DEMO-001" in r.stdout


def test_validate_detects_unknown_dependency(task_cli, studio_repo):
    write_raw(studio_repo, "DEMO-001", dependencies=["DEMO-099"])
    r = task_cli("validate", "Demo")
    assert r.returncode == 1
    assert "DEMO-099" in r.stdout


def test_validate_detects_bad_status_and_agent(task_cli, studio_repo):
    write_raw(studio_repo, "DEMO-001", status="DONE")
    write_raw(studio_repo, "DEMO-002", agent="wizard")
    r = task_cli("validate", "Demo")
    assert r.returncode == 1
    assert "DEMO-001" in r.stdout and "DEMO-002" in r.stdout


def test_validate_clean_exit0(task_cli):
    new(task_cli)
    r = task_cli("validate", "Demo")
    assert r.returncode == 0, r.stdout


def test_ready_lists_unblocked(task_cli, studio_repo):
    write_raw(studio_repo, "DEMO-001", status="COMPLETED")
    write_raw(studio_repo, "DEMO-002", dependencies=["DEMO-001"])
    write_raw(studio_repo, "DEMO-003", dependencies=["DEMO-002"])
    r = task_cli("ready", "Demo")
    assert r.returncode == 0
    assert r.stdout.split() == ["DEMO-002"]


def test_new_rejects_unknown_agent(task_cli):
    r = task_cli("new", "Demo", "--title", "t", "--agent", "wizard")
    assert r.returncode != 0


def test_set_result_and_note(task_cli, studio_repo):
    tid = new(task_cli)
    r = task_cli("set", "Demo", tid, "--status", "ASSIGNED", "--result", "core-tests=pass", "--note", "hi")
    assert r.returncode == 0, r.stderr
    t = read_task(studio_repo, "Demo", tid)
    assert t["result"] == {"core-tests": "pass"} and t["notes"] == ["hi"]
