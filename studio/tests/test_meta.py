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


def _native_project(tmp_path):
    proj = tmp_path / "Unity"
    art = proj / "Assets" / "Game" / "Art" / "Set"
    art.mkdir(parents=True)
    for name in ("Chair.fbx", "Rock.OBJ", "Wood.png", "Sky.JPG", "Cloth.tga", "Step.wav", "Body.ttf"):
        (art / name).write_bytes(b"\x00binary")
    (art / "notes.usdc").write_bytes(b"x")
    return proj, art


def test_gen_native_assets_get_minimal_meta(tmp_path):
    proj, art = _native_project(tmp_path)
    ensure_meta(proj / "Assets")
    for name in ("Chair.fbx", "Rock.OBJ", "Wood.png", "Sky.JPG", "Cloth.tga", "Step.wav", "Body.ttf"):
        text = (art / (name + ".meta")).read_text()
        lines = text.splitlines()
        # fileFormatVersion + guid only: Unity chooses ModelImporter/TextureImporter/... by extension.
        assert lines[0] == "fileFormatVersion: 2", name
        assert GUID_RE.search(text), name
        assert len(lines) == 2, (name, text)
        assert "DefaultImporter" not in text and "TextScriptImporter" not in text, name
    assert text.endswith("\n")


def test_gen_unknown_extension_and_folders_still_default(tmp_path):
    proj, art = _native_project(tmp_path)
    ensure_meta(proj / "Assets")
    assert "DefaultImporter" in (art / "notes.usdc.meta").read_text()
    folder = (art.parent / "Set.meta").read_text()
    assert "folderAsset: yes" in folder and "DefaultImporter" in folder


def test_gen_does_not_touch_existing_native_meta(tmp_path):
    proj, art = _native_project(tmp_path)
    existing = "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nModelImporter:\n  serializedVersion: 22200\n"
    (art / "Chair.fbx.meta").write_text(existing)
    created = ensure_meta(proj / "Assets")
    assert art / "Chair.fbx.meta" not in created
    assert (art / "Chair.fbx.meta").read_text() == existing


def test_gen_native_guids_unique_and_check_meta_ok(tmp_path):
    proj, art = _native_project(tmp_path)
    ensure_meta(proj / "Assets")
    guids = [GUID_RE.search(m.read_text()).group(1) for m in art.glob("*.meta")]
    assert len(guids) == len(set(guids)) == 8
    r = check(proj)
    assert r.returncode == 0, r.stdout


def test_gen_existing_cs_and_text_importers_unchanged(tmp_path):
    proj = make_project(tmp_path)
    core = proj / "Assets" / "Game" / "Core"
    (core / "data.JSON").write_text("{}")
    ensure_meta(proj / "Assets")
    assert "MonoImporter" in (core / "Score.cs.meta").read_text()
    assert "TextScriptImporter" in (core / "data.JSON.meta").read_text()
