import pytest
from conftest import run_script


def new_game(root, *args):
    return run_script("new_game.py", *args, cwd=root, env_root=root)


@pytest.mark.parametrize("name", ["my game", "게임", "lower", "A"])
def test_rejects_bad_names(studio_repo, name):
    r = new_game(studio_repo, name)
    assert r.returncode == 1
    assert sorted(p.name for p in (studio_repo / "Games").iterdir()) == ["Demo"]


def test_rejects_existing(studio_repo):
    (studio_repo / "Games" / "Taken").mkdir()
    r = new_game(studio_repo, "Taken")
    assert r.returncode == 1
    assert list((studio_repo / "Games" / "Taken").iterdir()) == []


def test_creates_structure_2d(studio_repo):
    r = new_game(studio_repo, "Sky", "--mode", "2d")
    assert r.returncode == 0, r.stderr
    assert r.stdout.strip().splitlines()[-1] == "created Games/Sky"
    g = studio_repo / "Games" / "Sky"
    for rel in ["Docs/design/GAME_DESIGN.md", "Docs/tech/INTERFACE_CONTRACTS.md",
                "Docs/DECISIONS.md", "Docs/PROGRESS.md", "Tasks", "Reports/qa",
                "Core.Tests/Core.Tests.csproj",
                "Unity/ProjectSettings/ProjectVersion.txt", "Unity/Packages/manifest.json",
                "Unity/Assets/Game/Core/Sky.Core.asmdef", "Unity/Assets/Game/Core/GameInfo.cs",
                "Unity/Assets/Game/Scripts/Sky.Runtime.asmdef", "Unity/Assets/Game/Scripts/Bootstrap.cs",
                "Unity/Assets/Game/Editor/StudioBuild.cs",
                "Unity/Assets/Game/Tests/EditMode/Core/GameInfoTests.cs",
                "Unity/Assets/Game/Tests/PlayMode/BootstrapTests.cs"]:
        assert (g / rel).exists(), rel
    for p in g.rglob("*"):
        if p.is_file():
            assert "{{" not in p.read_text(encoding="utf-8"), p
    assert "6000.3.29f1" in (g / "Unity/ProjectSettings/ProjectVersion.txt").read_text()
    assert "orthographic = true" in (g / "Unity/Assets/Game/Scripts/Bootstrap.cs").read_text()
    assert run_script("check_meta.py", g / "Unity").returncode == 0


def test_mode_manifests(studio_repo):
    assert new_game(studio_repo, "Flat", "--mode", "2d").returncode == 0
    assert new_game(studio_repo, "Deep", "--mode", "3d").returncode == 0
    flat = (studio_repo / "Games/Flat/Unity/Packages/manifest.json").read_text()
    deep = (studio_repo / "Games/Deep/Unity/Packages/manifest.json").read_text()
    assert "com.unity.2d.sprite" in flat and "com.unity.modules.physics2d" in flat
    assert '"com.unity.modules.physics"' in deep and "com.unity.2d.sprite" not in deep
    assert "orthographic = false" in (studio_repo / "Games/Deep/Unity/Assets/Game/Scripts/Bootstrap.cs").read_text()
