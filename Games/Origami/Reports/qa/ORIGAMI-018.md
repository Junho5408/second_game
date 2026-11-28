# QA ORIGAMI-018: CC0 workshop assets (Kenney furniture, Quaternius character, ambientCG surfaces)

- QA: qa-engineer, 2026-11-28
- Branch `claude/serene-fermat-4bwjlo`. Tested: 9329425 (impl) + 3213748 (status). HEAD b192e47 (later commits change only Docs/Reports/Tasks, no `Art/` change).
- Review: APPROVED_WITH_WARNINGS (W-1 non-uniform `Box` fit vs ARCHITECTURE §14.7, W-2 photo textures). Both are Orchestrator/architect decisions for ORIGAMI-019 and are **not** treated as QA failures.
- **Result: PASS. 4/4 criteria pass, no bugs found.**

## Verification run

| Check | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | PASS, 735/735 (no Core change) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| Own ASCII FBX bounds parser (`scratchpad/qa18/fbxbounds.py`: Model hierarchy via `C: "OO"`, Lcl T/R/S, x negated for Unity) | 10 models parsed. Sizes and bottom centres match ASSETS.md (below) |
| CREDITS byte counts vs `stat` (python script over the Files table) | 24/24 rows match |
| Archives in repo (`git diff --name-only 678e264 HEAD`, `git ls-files`) | no `.zip/.7z/.rar/.tar/.gz` |
| LFS | `git lfs ls-files` empty; `.gitattributes` `*.fbx binary`, `*.jpg binary`; `git check-attr` Window.fbx `binary: set` |
| Licences (pages fetched by QA 2026-11-28) | opengameart `furniture-kit`: "License(s): CC0"; opengameart `animated-characters-pack`: "License(s): CC0"; docs.ambientcg.com/license: "Creative Commons CC0 1.0 ..." |
| Crafter clips (binary FBX, AnimStack names parsed) | `Defeat, Idle, PickUp, Punch, RecieveHit, Shoot_OneHanded, SitDown, StandUp, Victory, Walk, Walk_Carry`; `cmp` byte-identical to `_PipelineSample/CasualMale.fbx` |
| Unity CI run 46093130343, job `unity (Origami)` (head 3213748 contains 9329425) | success. See `Reports/build/ORIGAMI-018.md` |

## Acceptance criteria

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| 1 | One consistent cozy low-poly CC0 set: room (floor, wall, window, door), table + chair, bookshelf, counter, bed, lights/props, one human with idle/walk (sit if possible) | **PASS** | Files present: Table, Chair, Bookshelf (+Books), DisplayShelf (counter ×2), Bed, Nightstand, TrashCan, Door, Window, LampTable, LampFloor, Rug, PottedPlant, PlantSmall, Box, Paneling, CoatRack (Kenney, all flat-colour materials, 0 `Texture:` nodes in every Workshop FBX); WoodFloor/PlasterWall 512×512 JPG for code-built floor/walls; Crafter with `Idle`, `Walk`, `SitDown`, `StandUp` takes. Style note W-2 (photo floor) is a review warning, textures are documented as optional |
| 2 | Table of target size/pivot per Core `RoomLayout` box, incl. code scale | **PASS** | ASSETS.md has one row for each of the 8 `RoomLayout.Furniture` ids (table, chair, bookshelf, counter, bed, trash, window, door), Core sizes match `RoomLayout.cs` lines 25-32. Spot-check (QA parser): **Table** 8.415 × 3.267 × 4.474, bottom centre (2.61, 0, −46.66), Box scale 1.4/8.415 = 0.1664, 0.75/3.267 = 0.2296, 0.9/4.474 = 0.2012; **Bed** (3 parts) 5.710 × 3.750 × 11.250, (12.22, 0, −9.72), 1/5.71 = 0.1751 → 1.00 × 0.66 × 1.97; **Window** (hand-edited) 3.746 × 6.641 × 0.246, (−46.18, 3.93, 40.40), 1/6.641 = 0.1506 → 0.56 wide. Also matched: Door, DisplayShelf, Bookshelf, Chair, TrashCan, Books (min y 6.10), PlantSmall (min y 3.47). All identical to ASSETS.md to the listed precision |
| 3 | CREDITS.md complete (name, author, URL, CC0, date), no source zips, ≤ 30 MB total | **PASS** | Sources table K/Q/A1/A2/A3 with pack, author, page URL, licence + how verified, download route + date; per-file table with original name, bytes, changes, date. Largest file 1,612,396 B (≤ 5 MB), art total 3,803,936 B (≤ 30 MB), 018 adds 2,145,755 B. No archives, no LFS |
| 4 | 017 pipeline rules, check_meta OK, CI compile/EditMode/PlayMode pass, existing tests kept | **PASS** | Layout `Art/<Set>/Resources/<Set>/<Name>` with keys `Workshop/*`, `Characters/Crafter`, `Surfaces/*`; `ArtImportRules` applies to `Assets/Game/` (Legacy animation type). check_meta OK. CI: 0 `error CS`, EditMode 735/735, PlayMode 110 = 109 passed + 1 inconclusive (`WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw`, the known batch-mode OnGUI case, same as ORIGAMI-017), 0 failed. All 21 new assets logged "Start importing ... -> (artifact id ...)" with no import warning/error; Windows build OK |

## Bugs

None.

## Tests added

None (content-only task). As instructed, no PlayMode test was added now because it would need another CI run.

## Untested areas / recommendations

- **Runtime `Resources.Load` of the 21 new keys** is not tested by any test yet; evidence is only the clean CI import plus the byte-identical `CasualMale` covered by the 017 pipeline test. **Recommend for ORIGAMI-019**: a PlayMode test that loads every key in ASSETS.md "Keys for tests" (non-null, ≥ 1 `Renderer`; Crafter `Animation` has `Idle/Walk/SitDown/StandUp`; textures `Texture2D` ≤ 1024 px; Window has 1 `MeshRenderer` with `glass` + `wood`).
- Visual style consistency, glass transparency, character height/front axis, `SitDown` end pose vs. 0.48 m seat: not verifiable without placement (019) and screenshots.
- W-1 (uniform vs per-axis fit) and W-2 (photo textures) remain open decisions for the Orchestrator/architect before 019.
