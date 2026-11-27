import json

from conftest import git, run_script

ZERO = "0" * 40


def setup_games(root, *names):
    for n in names:
        (root / "Games" / n / "Unity").mkdir(parents=True)
        (root / "Games" / n / "Unity" / "a.txt").write_text("1")
    git(root, "add", "-A"); git(root, "commit", "-qm", "games")
    return git(root, "rev-parse", "HEAD")


def changed(root, base, head):
    r = run_script("changed_games.py", "--base", base, "--head", head, cwd=root, env_root=root)
    assert r.returncode == 0, r.stderr
    return json.loads(r.stdout)


def test_detects_changed_game(studio_repo):
    base = setup_games(studio_repo, "Alpha", "Beta")
    (studio_repo / "Games/Beta/Unity/a.txt").write_text("2")
    git(studio_repo, "commit", "-qam", "beta")
    assert changed(studio_repo, base, git(studio_repo, "rev-parse", "HEAD")) == ["Beta"]


def test_core_tests_change_counts(studio_repo):
    base = setup_games(studio_repo, "Alpha")
    (studio_repo / "Games/Alpha/Core.Tests").mkdir()
    (studio_repo / "Games/Alpha/Core.Tests/x.csproj").write_text("x")
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", "c")
    assert changed(studio_repo, base, "HEAD") == ["Alpha"]


def test_zero_base_returns_all(studio_repo):
    head = setup_games(studio_repo, "Alpha", "Beta")
    assert changed(studio_repo, ZERO, head) == ["Alpha", "Beta"]


def test_unknown_base_returns_all(studio_repo):
    head = setup_games(studio_repo, "Alpha")
    assert changed(studio_repo, "deadbeef" * 5, head) == ["Alpha"]


def test_ignores_non_game_paths(studio_repo):
    base = setup_games(studio_repo, "Alpha")
    (studio_repo / "Games/Alpha/Docs").mkdir()
    (studio_repo / "Games/Alpha/Docs/x.md").write_text("x")
    (studio_repo / "README.md").write_text("y")
    git(studio_repo, "add", "-A"); git(studio_repo, "commit", "-qm", "docs")
    assert changed(studio_repo, base, "HEAD") == []
