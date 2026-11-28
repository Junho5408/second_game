# QA ORIGAMI-019: CC0 3D assets in the workshop (models, Crafter animation, lighting)

- QA: qa-engineer, 2026-11-28
- Branch `claude/serene-fermat-4bwjlo`, base `fbd6de7`, implementation `6407a02` + fix `5aab520`, CI head `ddcf6d0`
  (only review/task-file changes after it: `git diff --stat ddcf6d0 HEAD` = Reports/review + Tasks JSON)
- Review: APPROVED_WITH_WARNINGS (re-review fix pass 1)
- **Result: PASS** (automated criteria). Visual items are PENDING (user playtest), listed below.

## Verification evidence

| Command / source | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | `== Origami: PASS ==`, 735/735 |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| `git diff 7058d3f HEAD -- Games/Origami/Unity/Packages Games/Origami/Unity/ProjectSettings` | empty |
| `git diff --stat fbd6de7 HEAD -- Games/Origami/Unity/Assets/Game/Core Games/Origami/Unity/Assets/Game/Tests/EditMode` | empty (Core and Core tests untouched) |
| CI `unity` run [46096898759](https://github.com/Junho5408/second_game/actions/runs/46096898759), job `unity (Origami)` 140830250495, head `ddcf6d0` | conclusion **success**; steps tests + Windows build success |
| `editmode-results.xml` | total 735, passed 735, failed 0 |
| `playmode-results.xml` | total 147, passed 146, failed 0, inconclusive 1 (`WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw`, "OnGUI was not called", known since 018) |
| job log | 0 `error CS`, 0 `warning CS`; `Build Finished, Result: Success.` / `Build succeeded` |
| Regression diff vs 018 run 46093130343 PlayMode (110 tests) | 0 tests missing, 0 regressed; 37 new tests (24 WorkshopAssetsTests, 13 WorkshopArtTests) |

The game-ci "Failure / exit code not very descriptive" banner at the end of the test step comes from the inconclusive
test; the step and job conclusions are `success` (same as the 018 run).

## Acceptance criteria

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| 1 | Furniture replaced by models, position/size = Core RoomLayout, collision/interaction unchanged, fallback to code meshes | **PASS** | CI log (`WorkFurniture_…`, `OtherFurniture_…`): table 1.400×0.750×0.900 centre (2.5, 1.6) = RoomLayout 1.4×0.9×0.75 @ (2.5, 1.6); bookshelf 1.6×2.0×0.4 @ (0, 2.75) = spec; counter 2.0×1.0×0.6 @ (2.5, −2.3) = spec; chair 0.5×0.92×0.5 @ (2.5, 0.8) (seat 0.48 per ASSETS.md); bed 1.0×0.657×1.97 @ (−3, 2) inside 1.0×2.0 footprint; trash 0.355×0.731×0.40 @ (3.6, 0.6) inside 0.4×0.4; door 1.0×2.077 centred z −3.0; window 1.128×1.0, y 1.0–2.0, centre x −1.8. All bottoms at y 0 (window 1.0). Core untouched → collision/interaction identical; Workshop Mode/Movement/Screens tests pass. `Fallback_WithoutModels_…` passes |
| 2 | Character model, Idle/Walk switching by speed, face move direction, sit at table | **PASS** | `Character_SwitchesIdleToWalkAndBack_WithMovement_ShortCrossfade` (Idle→Walk cross-fade, faces north, back to Idle), `Character_WalkPose_IsDeterministicFromDt`, `Character_SitsDownOnTheChairSeat_AndStandsUpToIdle` pass. Measured: scale 0.48119, mesh height 3.325 → 1.60 m, Head_end 2.757 → 1.33 m, idle hips 0.431 m, walk ground speed 0.84 m/s (measured, not default), seated hips (2.5, 0.580, 0.75) = seat 0.48 + 0.10 |
| 3 | Warm lighting, built-in RP, no new packages, paper visible | **PASS** (automated part) | `Lighting_WarmSoftShadowSun_TrilightAmbient_NoFog_PaperOutsideTheSeatedShadow` passes with a real-size character; Packages/ProjectSettings diff empty. Look and feel: PENDING (user) |
| 4 | Existing Workshop/FoldTable/Book tests kept and passing; load + animation tests added, not frame-timing dependent | **PASS** | Regression diff vs 018: 0 missing, 0 regressed; tests drive `ProcessFrame`/`Tick` with fixed dt (see `WorkshopRig`), `Animation` state speed asserted 0 |
| 5 | Unity CI compile, EditMode, PlayMode, Windows build | **PASS** | run 46096898759 (above); artifact `Origami-windows-build` id 15243504838, 34,885,393 B |
| 6 | Follow ASSETS.md; DECISIONS fit rule (furniture per-axis ±40 %, others uniform); character front/height measured at runtime | **PASS** | Per-axis scales from CI native and fitted sizes, deviation from the mean of the three: table 0.166/0.230/0.201 (−16/+15/+1 %), chair 0.25/0.20/0.25 (+7/−14/+7 %), bookshelf 0.200/0.253/0.160 (−2/+24/−22 %), each counter shelf 0.25/0.25/0.24 (≤ 3 %) — all ≤ 40 % (`ModelFit.MaxAxisDeviation = 0.4`). Uniform: bed 0.1751 ×3, trash 0.171 ×3, door 0.2058 ×2, each window casement 0.1506 ×3, Crafter `Height(1.6)` uniform 0.481. `ModelFit_BoxFitsPerAxis_WithinTheRule_ElseUniform_…` passes. Front: "front yaw 0 (measured True)"; height from `sharedMesh.bounds` × SMR transform with Head_end cross-check |
| 7 | Door 2.08 m vs 0.5 m south wall, sit end pose vs seat 0.48, window glass via MaterialPropertyBlock; decisions in Gameplay README | **PASS** | `Door_IsCutAway_WhileTheCharacterStandsBehindIt`, `Window_GlassTintedByPropertyBlock_…`, sit test pass; `Scripts/Gameplay/README.md` table rows 창문 / 문 (2.08 m) vs 남쪽 벽 (0.5 m) / 앉기 / 앉은 시점 카메라 |
| 8 | Floor and walls flat colour | **PASS** (code inspection) | `WorkshopView.BuildRoom` uses `Lit(FloorColor)` / `Lit(WallColor)` (`Materials.Lit(Color)`, no texture); `ArtKeys.WoodFloor/PlasterWall` are referenced only in `ArtKeys` (test key list). No automated test asserts "no texture"; final colour choice PENDING (user) |
| 9 | PlayMode test: all 21 ASSETS.md keys load; Crafter has Idle/Walk/SitDown/StandUp | **PASS** | 19 `Model_LoadsFromResources_WithARenderer(...)` + 2 `Texture_LoadsAsTexture2D_AtMost1024(...)` = 21 cases passed; `ArtKeys_ListsExactlyTheAssetsMdKeys` passed; `Crafter_HasAnimation_…`: Idle 4.167 s, Walk 1.250 s, SitDown 0.958 s, StandUp 1.292 s |
| 10 | `_PipelineSample/CasualMale.fbx` deleted, AssetPipeline tests use Characters/Crafter | **PASS** | `_PipelineSample/Resources/_PipelineSample/` contains only Chair.fbx, PaperSwatch.png (+ .meta); `AssetPipelineTests.CharacterKey = "Characters/Crafter"`; 7/7 AssetPipelineTests passed; no `CasualMale` reference outside a doc comment |

## Bugs

None found.

## Tests added by QA

None. The criteria are covered by the developer's CI tests; criterion 8 is verified by code inspection (low-risk gap:
no test asserts that the floor/wall materials carry no texture).

## Playtest items — PENDING (user)

| Item | Status |
|---|---|
| Overall look of the room (style consistency, scale of props) | PENDING (user) |
| Window glass transparency (Standard shader may import opaque; review W-2) | PENDING (user) |
| Sit pose on the chair and feet floating ≈ 0.2 m under the table (review W-5) | PENDING (user) |
| Door cut-away while the character stands behind the 2.08 m door | PENDING (user) |
| Walk foot sliding (clip ground speed 0.84 m/s vs movement 3.0 m/s, playback rate scaled) | PENDING (user) |
| Colours (floor brown, cream walls, warm light) | PENDING (user) |

## Untested / open

- Visual items above (no rendering comparison in CI).
- `OnGui_DrawsTheBookWithBookPanelDraw` stays inconclusive in headless CI (pre-existing, not caused by this task).
- Review W-1 (INTERFACE_CONTRACTS C-193/C-195, ARCHITECTURE §14.2/§14.7 outdated) needs an architect doc task.
