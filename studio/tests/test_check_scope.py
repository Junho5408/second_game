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
