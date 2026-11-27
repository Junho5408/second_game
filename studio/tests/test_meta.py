import re
import shutil

from conftest import run_script

from gen_meta import ensure_meta

GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)


def make_project(tmp_path):
    proj = tmp_path / "Unity"
    core = proj / "Assets" / "Game" / "Core"
    core.mkdir(parents=True)
    (core / "Score.cs").write_text("class S {}")
    (core / "Demo.Core.asmdef").write_text("{}")
    return proj


def test_gen_creates_meta_for_files_and_folders(tmp_path):
    proj = make_project(tmp_path)
    created = ensure_meta(proj / "Assets")
    core = proj / "Assets" / "Game" / "Core"
    names = {p.name for p in created}
    assert {"Game.meta", "Core.meta", "Score.cs.meta", "Demo.Core.asmdef.meta"} <= names
    assert "Assets.meta" not in names
    folder = (proj / "Assets" / "Game.meta").read_text()
    assert folder.startswith("fileFormatVersion: 2") and "folderAsset: yes" in folder
    assert "MonoImporter" in (core / "Score.cs.meta").read_text()
    assert "AssemblyDefinitionImporter" in (core / "Demo.Core.asmdef.meta").read_text()
    assert GUID_RE.search((core / "Score.cs.meta").read_text())


def test_gen_is_idempotent(tmp_path):
    proj = make_project(tmp_path)
    ensure_meta(proj / "Assets")
    meta = proj / "Assets" / "Game" / "Core" / "Score.cs.meta"
    before = meta.read_text()
    assert ensure_meta(proj / "Assets") == []
    assert meta.read_text() == before


def check(proj):
    return run_script("check_meta.py", proj)


def test_check_clean_project_exit0(tmp_path):
    proj = make_project(tmp_path)
    ensure_meta(proj / "Assets")
    r = check(proj)
    assert r.returncode == 0, r.stdout


def test_check_reports_missing(tmp_path):
    proj = make_project(tmp_path)
    ensure_meta(proj / "Assets")
    (proj / "Assets" / "Game" / "Core" / "New.cs").write_text("x")
    r = check(proj)
    assert r.returncode == 1 and "Assets/Game/Core/New.cs" in r.stdout


def test_check_reports_orphan(tmp_path):
    proj = make_project(tmp_path)
    ensure_meta(proj / "Assets")
    (proj / "Assets" / "Game" / "Core" / "Score.cs").unlink()
    r = check(proj)
    assert r.returncode == 1 and "Score.cs.meta" in r.stdout


def test_check_reports_duplicate_guid(tmp_path):
    proj = make_project(tmp_path)
    ensure_meta(proj / "Assets")
    game = proj / "Assets" / "Game"
    shutil.copytree(game / "Core", game / "Core2")
    shutil.copy(game / "Core.meta", game / "Core2.meta")
    r = check(proj)
    assert r.returncode == 1
    assert "Assets/Game/Core/Score.cs.meta" in r.stdout
    assert "Assets/Game/Core2/Score.cs.meta" in r.stdout


def test_check_ignores_hidden_and_tilde(tmp_path):
    proj = make_project(tmp_path)
    (proj / "Assets" / ".hidden").write_text("x")
    (proj / "Assets" / "Samples~").mkdir()
    ensure_meta(proj / "Assets")
    assert not (proj / "Assets" / ".hidden.meta").exists()
    assert check(proj).returncode == 0
