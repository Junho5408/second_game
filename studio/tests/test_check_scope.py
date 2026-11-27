from conftest import git, run_script

from check_scope import in_scope

TF = "Games/Demo/Tasks/DEMO-001.json"
GP = "Games/Demo/Unity/Assets/Game/Scripts/Gameplay/"


def test_folder_scope_allows_children_and_meta():
    assert in_scope(GP + "Player.cs", [GP], TF)
    assert in_scope(GP + "Player.cs.meta", [GP], TF)


def test_folder_scope_rejects_similar_sibling():
    assert not in_scope("Games/Demo/Unity/Assets/Game/Scripts/GameplayExtra/X.cs", [GP], TF)


def test_folder_own_meta_allowed():
    assert in_scope(GP.rstrip("/") + ".meta", [GP], TF)


def test_exact_file_and_its_meta():
    f = "Games/Demo/Unity/Assets/Game/Core/Score.cs"
    assert in_scope(f, [f], TF)
    assert in_scope(f + ".meta", [f], TF)
    assert not in_scope("Games/Demo/Unity/Assets/Game/Core/Other.cs", [f], TF)


def test_glob():
    assert in_scope("Games/Demo/Docs/design/GAME_RULES.md", ["Games/Demo/Docs/design/*.md"], TF)


def test_own_task_file_always_allowed_other_rejected():
    assert in_scope(TF, [], TF)
    assert not in_scope("Games/Demo/Tasks/DEMO-002.json", [], TF)


def test_cli_reports_violation(task_cli, studio_repo):
    tid = task_cli("new", "Demo", "--title", "t", "--agent", "developer",
                   "--scope", "Games/Demo/Src/").stdout.strip()
    for s in ["ASSIGNED", "IN_PROGRESS"]:
        assert task_cli("set", "Demo", tid, "--status", s).returncode == 0
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", "task")
    # re-record base after committing the task file itself is not needed: own task file is allowed
    (studio_repo / "Games/Demo/Src").mkdir(parents=True)
    (studio_repo / "Games/Demo/Src/ok.cs").write_text("x")
    (studio_repo / "README.md").write_text("changed")
    r = run_script("check_scope.py", "Demo", tid, cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 1
    assert r.stdout.split() == ["README.md"]


def test_cli_no_base_exit2(task_cli, studio_repo):
    tid = task_cli("new", "Demo", "--title", "t", "--agent", "developer").stdout.strip()
    r = run_script("check_scope.py", "Demo", tid, cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 2


def test_same_game_reports_allowed_other_game_reports_rejected():
    # Review/QA agents write Reports/ during the pipeline; they must not count against the implementer.
    assert in_scope("Games/Demo/Reports/review/DEMO-001.md", [], TF)
    assert in_scope("Games/Demo/Reports/qa/DEMO-002.md", [], TF)
    assert not in_scope("Games/Other/Reports/qa/X.md", [], TF)


def test_cli_ignores_other_commits_on_branch(task_cli, studio_repo):
    tid = task_cli("new", "Demo", "--title", "t", "--agent", "developer",
                   "--scope", "Games/Demo/Src/").stdout.strip()
    for s in ["ASSIGNED", "IN_PROGRESS"]:
        assert task_cli("set", "Demo", tid, "--status", s).returncode == 0
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", "orchestrator: plan")
    # Orchestrator (or another task) changes an unrelated file after the task started.
    (studio_repo / "README.md").write_text("orchestrator edit")
    git(studio_repo, "commit", "-qam", "orchestrator: unrelated")
    # The task's own commit, in scope.
    (studio_repo / "Games/Demo/Src").mkdir(parents=True)
    (studio_repo / "Games/Demo/Src/ok.cs").write_text("x")
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", f"{tid}: implement")
    r = run_script("check_scope.py", "Demo", tid, cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 0, r.stdout


def test_cli_flags_task_commit_outside_scope(task_cli, studio_repo):
    tid = task_cli("new", "Demo", "--title", "t", "--agent", "developer",
                   "--scope", "Games/Demo/Src/").stdout.strip()
    for s in ["ASSIGNED", "IN_PROGRESS"]:
        assert task_cli("set", "Demo", tid, "--status", s).returncode == 0
    (studio_repo / "LICENSE").write_text("x")
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", f"{tid}: oops")
    r = run_script("check_scope.py", "Demo", tid, cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 1 and r.stdout.split() == ["LICENSE"]


def test_cli_handles_non_ascii_paths(task_cli, studio_repo):
    tid = task_cli("new", "Demo", "--title", "t", "--agent", "game-designer",
                   "--scope", "Games/Demo/Docs/design/").stdout.strip()
    for s in ["ASSIGNED", "IN_PROGRESS"]:
        assert task_cli("set", "Demo", tid, "--status", s).returncode == 0
    d = studio_repo / "Games/Demo/Docs/design"
    d.mkdir(parents=True)
    (d / "기획.md").write_text("x")
    r = run_script("check_scope.py", "Demo", tid, cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 0, r.stdout          # uncommitted
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", f"{tid}: 기획")
    r = run_script("check_scope.py", "Demo", tid, cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 0, r.stdout          # committed


def _started_task(task_cli, scope="Games/Demo/Src/"):
    tid = task_cli("new", "Demo", "--title", "t", "--agent", "developer", "--scope", scope).stdout.strip()
    for s in ["ASSIGNED", "IN_PROGRESS"]:
        assert task_cli("set", "Demo", tid, "--status", s).returncode == 0
    return tid


def test_role_qa_flags_production_code(task_cli, studio_repo):
    tid = _started_task(task_cli)
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", "orchestrator: start")
    prod = studio_repo / "Games/Demo/Unity/Assets/Game/Core"
    prod.mkdir(parents=True)
    (prod / "X.cs").write_text("x")
    tests = studio_repo / "Games/Demo/Unity/Assets/Game/Tests/EditMode"
    tests.mkdir(parents=True)
    (tests / "XTests.cs").write_text("x")
    (studio_repo / "Games/Demo/Reports/qa").mkdir(parents=True)
    (studio_repo / f"Games/Demo/Reports/qa/{tid}.md").write_text("x")
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", f"qa({tid}): PASS")
    r = run_script("check_scope.py", "Demo", tid, "--role", "qa", cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 1
    assert r.stdout.split() == ["Games/Demo/Unity/Assets/Game/Core/X.cs"]
    # implementer view is unaffected by qa commits
    r = run_script("check_scope.py", "Demo", tid, cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 0, r.stdout


def test_role_review_only_own_report(task_cli, studio_repo):
    tid = _started_task(task_cli)
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", "orchestrator: start")
    (studio_repo / "Games/Demo/Reports/review").mkdir(parents=True)
    (studio_repo / f"Games/Demo/Reports/review/{tid}.md").write_text("x")
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", f"review({tid}): APPROVED")
    r = run_script("check_scope.py", "Demo", tid, "--role", "review", cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 0, r.stdout
    (studio_repo / "Games/Demo/Src").mkdir(parents=True)
    (studio_repo / "Games/Demo/Src/hack.cs").write_text("x")
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", f"review({tid}): oops")
    r = run_script("check_scope.py", "Demo", tid, "--role", "review", cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 1 and r.stdout.split() == ["Games/Demo/Src/hack.cs"]


def test_commit_body_line_does_not_count(task_cli, studio_repo):
    tid = _started_task(task_cli)
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", "orchestrator: start")
    (studio_repo / "LICENSE").write_text("x")
    git(studio_repo, "add", "-A")
    git(studio_repo, "commit", "-qm", "orchestrator: unrelated", "-m", f"{tid}: mentioned in body")
    r = run_script("check_scope.py", "Demo", tid, cwd=studio_repo, env_root=studio_repo)
    assert r.returncode == 0, r.stdout
