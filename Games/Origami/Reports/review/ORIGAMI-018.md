# Review ORIGAMI-018: CC0 workshop assets (Kenney furniture, Quaternius character, ambientCG surfaces)

- Reviewer: code-reviewer, 2026-11-28
- Base commit: 678e264 · Reviewed: 9329425 (impl), 3213748 (status) on `claude/serene-fermat-4bwjlo`
- **Result: APPROVED_WITH_WARNINGS**

## Verification run

| Check | Result |
|---|---|
| `check_scope.py Origami ORIGAMI-018` | OK (all changes under `Art/` + own task file) |
| `check_meta.py Games/Origami/Unity` | OK (every new folder/file has a minimal GUID-only `.meta`) |
| `verify_core.sh Origami` | PASS, 735/735 (no Core change) |
| `git lfs ls-files` | empty; `.gitattributes` marks `*.fbx`/`*.jpg` `binary` (Window.fbx: `binary: set`) |
| Size | art total (excl. .meta/.md) 3,803,936 B; 018 adds 2,145,755 B; largest file 1,612,396 B (Crafter.fbx). All 24 byte counts in CREDITS.md match `stat` |
| Licences (source pages fetched 2026-11-28) | opengameart `furniture-kit`: "License(s): CC0"; opengameart `animated-characters-pack`: "License(s): CC0"; ambientCG `WoodFloor051` / `Plaster001` pages + docs.ambientcg.com/license: "All ambientCG assets are provided under the Creative Commons CC0 1.0 Universal" |
| Unity CI run 46093130343 (push, head 3213748 contains 9329425), job `unity (Origami)` | success. `error CS` 0. EditMode 735/735 passed. PlayMode 110: 109 passed, 0 failed, 1 inconclusive (`WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw`, known batch-mode OnGUI case, same as 017 run 46090446550). Windows build step OK (Window.fbx 5.9 kb in build report) |
| CI asset import (playmode.log) | all 18 `Workshop/*.fbx`, `Characters/Crafter.fbx` (FBXImporter) and both `Surfaces/*.jpg` (TextureImporter) logged "Start importing ... -> (artifact id ...)" with no warning/error. Only noise: `libxml2.so.16` fallback warning (shader-compiler environment, printed during the first FBX import, not asset-related) and Crafter's normal "Keyframe reduction" lines (identical to `_PipelineSample/CasualMale`). No "Failed to import", no FBX warnings, `Window.fbx` imported in 0.088 s cleanly |

Evidence files: `/tmp/claude-0/-home-user-second-game/636884ad-8c21-5cae-9861-9592d708d5ff/scratchpad/ci3/` (artifacts `Origami-test-results`, `job.log`).

## Acceptance criteria

| # | Criterion | Verdict |
|---|---|---|
| 1 | One consistent cozy low-poly CC0 style: room (floor, wall, window, door), table/chair, bookshelf, counter, bed, lights/props, one human with idle/walk (sit if possible) | PASS. Kenney Furniture Kit (flat colour) for all furniture/props incl. Window, Door, Paneling, two lamps, plants, rug, box, coat rack; Quaternius Casual_Male with `Idle`, `Walk`, `SitDown`, `StandUp`; ambientCG floor/plaster textures for code-built planes. See W-2 on the textures |
| 2 | Table of target size/pivot per model fitted to Core RoomLayout boxes, incl. scale | PASS. `ASSETS.md` has one row per `RoomLayout.Furniture` entry (table, chair, bookshelf, counter, bed, trash, window, door) with native size, approx. bottom-centre offset, fit mode + scale, result size and rotation; I re-computed the fits (e.g. table 8.415×0.1664 = 1.40, 3.267×0.2296 = 0.75, 4.474×0.2012 = 0.90; window 6.641×0.1506 = 1.00; door 4.86×0.2058 = 1.00) and the rotations (chair 180° so the seat faces the table at +z; bookshelf/bed backs at native +z = north wall) are consistent. See W-1 on non-uniform scale |
| 3 | CREDITS.md per asset: name, author, URL, CC0, date; no source zips; total ≤ 30 MB | PASS. Sources table + per-file table (original file, bytes, changes, date). No zips committed. 3.8 MB of 30 MB |
| 4 | 017 pipeline rules (.meta, folders, Resources path), check_meta OK, CI compile/EditMode/PlayMode pass | PASS. `Art/<Set>/Resources/<Set>/<Name>` with PascalCase names, keys `Workshop/*`, `Characters/Crafter`, `Surfaces/*`; textures 512 px; CI green (above) |

## Window.fbx (hand-edited ASCII FBX) structure

Parsed with a script: one `Model::window` (Mesh), one `Geometry`, two `Material`s (`wood` 10122, `glass` 10130);
every `C: "OO"` connection refers to existing object IDs (model→root 0, geometry→model, both materials→model).
Vertices 192 (64 verts), max PolygonVertexIndex 63, 44 polygons, normals 396 = 132×3 (ByPolygonVertex), UV 128 / UVIndex 132 (max 63),
LayerElementMaterial ByPolygon 44 entries with indices 0..1 (0 = glass via connection order, 4 panes; 1 = wood frame). LF line endings,
balanced file end. Consistent and imported cleanly in CI.

## Findings

### W-1 (warning) Non-uniform `Box(sx, sy, sz)` fits deviate from ARCHITECTURE §14.7
- Location: `Art/ASSETS.md`, Furniture table rows `table`, `chair`, `bookshelf`, `counter`, `trash`, and Props row `Paneling`.
- Problem: ARCHITECTURE §14.7 / CODING_CONVENTION "크기" say the runtime fits a **uniform** scale (bottom-centre aligned). ASSETS.md recommends per-axis scales, some strongly non-uniform: trash (0.193, 0.117, 0.171) squashes height by ~40 %, table (0.166, 0.230, 0.201) stretches height by ~38 %, bookshelf (0.20, 0.253, 0.16).
- Impact: ORIGAMI-019 gets guidance that contradicts the architecture document; non-uniform scale visibly distorts the kit's proportions (thick/thin legs, squashed bin) and makes child parts with rotations shear. Not a defect in the delivered files.
- Recommendation: Orchestrator/architect record a decision before 019: either allow per-axis fit for furniture whose working height matters (table top 0.75, counter top 1.0) and update §14.7, or keep uniform and accept the visual box mismatch. For the trash, prefer the documented `Uniform(0.1709)` alternative.

### W-2 (warning) ambientCG photo textures vs. flat-colour models
- Location: `Art/Surfaces/Resources/Surfaces/{WoodFloor,PlasterWall}.jpg`, ASSETS.md "Room shell".
- Problem: all models are untextured flat colour; the floor is a photographic parquet. The plaster (low contrast, warm tint) is fine, the wood floor grain is the more likely clash with a flat low-poly look.
- Impact: mild style inconsistency against DECISIONS 2026-11-28 "one consistent cozy low-poly style"; ASSETS.md already marks the textures optional.
- Recommendation: in ORIGAMI-019 default to flat colours (e.g. a darker variant of Kenney `wood` for the floor, cream for walls) or use the textures with a strong `_Color` tint and low tiling contrast; decide from a CI screenshot/playtest and record it. Dropping them later costs only 62 KB.

### I-1 (info) FBX Definitions counts
- `Window.fbx` `Definitions` lists Model 1 / Material 1 / Texture 2 although it has 2 materials and no textures. The unedited Kenney exports (`Table.fbx`, `Door.fbx` with 2 models) have the same header, so it is an Asset Forge exporter quirk; the FBX SDK ignores these counts and CI imported it cleanly. No action.

### I-2 (info) Open placement questions for ORIGAMI-019 (already listed in ASSETS.md)
- Door 2.08 m high on a 0.5 m south wall; `SitDown` end pose vs. 0.48 m seat; character front axis (+z expected) and height scale must be measured at runtime; window glass tint via `MaterialPropertyBlock` per sub-material. Make these explicit acceptance items/tests in 019.
- `Characters/Crafter.fbx` is byte-identical to `_PipelineSample/CasualMale.fbx` (1.6 MB duplicate, both in build); documented, 019 decides whether to remove the sample with its tests.

## Checklist summary
1. Spec/acceptance: met (4/4). 2. Conventions: folder/key/meta/no-LFS/budget rules followed; §14.7 deviation is guidance only (W-1).
3. Boundaries: only `Art/` touched. 4. Invalid states: n/a (data only); ASSETS lists required test keys. 5. Performance/memory: 3.8 MB total, 512 px textures, small meshes.
6. Duplication: Crafter/CasualMale duplicate, documented. 7. Breakage: none, all existing CI tests pass. 8. Tests: no new tests required by this content task; key list provided for 019.
