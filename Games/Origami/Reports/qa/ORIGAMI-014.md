# QA ORIGAMI-014 — v2 Unity workshop integration + v1 deletion

- QA: qa-engineer · Branch `claude/serene-fermat-4bwjlo` · Implementation `bd68375`, `e9706bc`, `85a2295`. Review: APPROVED_WITH_WARNINGS (`Reports/review/ORIGAMI-014.md`)
- **Result: PASS**, with one item PENDING (user playtest): OnGUI drawing (review W-1, DECISIONS 2026-11-27)
- Unity CI: run 46041182289, job `unity (Origami)` success. Details are in `Reports/build/ORIGAMI-014.md`.

## Commands
| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | PASS, 735/735 (728 before + 7 QA tests) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| CI artifact `Origami-test-results` (XML parsed by QA) | EditMode 728/728. PlayMode 103: 102 Passed, 1 Inconclusive, 0 Failed. 0 `error CS` |
| CI job log | `Build Finished, Result: Success.`. `Origami-windows-build` artifact 15221105864 (34,295,759 bytes) |

## Acceptance criteria
| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Room, character (RoomMotion in Core), camera (lazy), HUD, overlays and counter integrated. Single Update / OnGUI | PASS | Workshop PlayMode tests (33) pass. `CameraRig_AcquiresTheMainCameraLazily_OrCreatesOne` and `RuntimeHook_CreatesOneAppWithGui_AndKeepsTheBootstrapCamera` pass. The review grep found exactly one Update and one OnGUI |
| 2a | F-002 AC-01~06 | PASS | Each value was checked against the spec, and every check passed in CI. **Core:** RoomMotionTests and RoomLayoutTests (start (−1.6, 0), +3.0 m in 1 s, diagonal 1.5 m, west wall −3.7, bed −2.2, bookshelf 2.25). **PlayMode:** WorkshopMovementTests with dt 0.02 (tolerances ±0.15 / ±0.1 / ±0.05, no movement outside walking ±0.01, camera offset (0, 6, −5.5) ±0.2, no mouse rotation) |
| 2b | F-003 AC-05~07 | PASS | **AC-05:** prompt and sit at 1.1 m; nothing at 1.3 m. **AC-06:** not seated at 0.3 s, seated view by about 0.6 s (`SitTransition` = 0.5). The paper is centred (dot > 0.9999). Q stands the character at (2.5, 0.2) facing south (180°). **AC-07:** Enter → settlement → Enter → (−1.6, 0.0) facing east, "2일차 / 7" |
| 2c | F-007 AC-06 | PASS | `F007_AC06_BookshelfDoesNotMove_EscCloses`, `F007_AC06_SeatedBookOnTheLeft_FoldingContinues` |
| 2d | F-010 AC-06 | PASS | Display slots fill in order. Trash shows 1 ball per failure, capped at 10 |
| 2e | F-011 AC-07 | PASS | `F011_AC07_CounterShowsPopularPricesAndTotal` |
| 2f | F-012 AC-09 | PASS | "빈손" and the CONTENT §8 E-01 text are checked verbatim, "최종 돈 20원" is shown, and Enter starts a new run |
| 2g | F-013 AC-03 | PASS (Unity part) / **PENDING (user)** Korean glyphs | The walk hint shows only before the first sit (PlayMode). "한글이 깨지지 않는다" is defined in the spec as a user-playtest item |
| 3 | One-day smoke PlayMode test (sit → 6 tutorial steps → finish → sleep → settlement) | PASS | `Smoke_OneDay_Sit_Tutorial6Steps_Finish_Sleep_Settlement` Passed in CI (0.071 s). It is driven only through `ProcessFrame` input |
| 4 | v1 deletion exactly as listed in §10; KoreanFont untouched; GameInfo 2.0.0 | PASS | Review §6 (filtered `--name-status`). `GameInfo_IsVersion2` (dotnet + EditMode). The EditMode count fell from 972 to 728 by removing v1 tests only, with 0 compile errors |
| 5 | Public surfaces follow INTERFACE_CONTRACTS; no asserts on 0.849/0.848 or J percentages | PASS | RoomSurfaceTests and review §3. QA grep of the Workshop tests finds no `0.849`, `0.848` or J percentages |
| 6 | verify_core, check_meta, Unity CI compile, EditMode, PlayMode and Windows build | PASS | See the commands above and the build report. PlayMode has 0 failures; the single Inconclusive result is item 8 |
| 7 | F-003 pause, new-game and sleep dialog texts verbatim, checked as strings | PASS | `F003_PauseAndNewRunConfirm_TextsVerbatim` and `F003_SleepConfirm_TextsVerbatim_EscIsNo` pass. They match F-003 and the DECISIONS row |
| 8a | A rejected book purchase shows `GameTexts.RejectMessage` | PASS | `W3_RejectedBookPurchaseShowsTheRejectMessage` |
| 8b | At least one smoke PlayMode test calls `BookPanel.Draw` from a real OnGUI | **PENDING (user)**, neither PASS nor FAIL | `OnGui_DrawsTheBookWithBookPanelDraw` is Inconclusive with the message "OnGUI was not called in this environment (batch mode without repaint)". Batch-mode CI cannot run it (review W-1, DECISIONS 2026-11-27). It moves to the user playtest of the Windows build: "HUD·버튼·책·대화상자가 오류 없이 그려진다" |

## Independent spec checks (QA)
These expected values were recomputed from FEATURE_SPECS, CONTENT §6, DATA_STRUCTURE D-130 and C-180:
- **Constants:** speed 3.0, radius 0.3, range 1.2 (inclusive).
- **Positions:** interaction points table (2.5, 0.8), bookshelf (0, 1.9), counter (2.5, −1.5), bed (−2, 2); start (−1.6, 0) facing east; stand-up (2.5, 0.2) facing south.
- **Camera and timing:** walking camera (0, 6, −5.5) looking at the head (0, 0.8, 0); sit transition 0.5 s, accepted up to 0.6 s.
- **Ending text:** E-01 text.

Every developer test uses these values. One minor note, not a defect: the AC-06 test samples arrival at 0.6 s plus the sit frame (about 0.62 s), slightly over the 0.6 s limit. The rig itself arrives at exactly 0.5 s (`SitTransition`), so the behaviour meets the spec.

## Tests added
`Unity/Assets/Game/Tests/EditMode/Core/Qa/RoomSpecQaTests.cs` (7 tests, dotnet PASS; they will run in the next Unity EditMode run):
1. `CONTENT6_StartDistances_Bed2Point04_Bookshelf2Point48`: the start is more than 1.2 m from every target (CONTENT §6 says bed 2.04, bookshelf 2.48).
2. `F002_EveryTargetIsReachableOnFootFromTheStart_AndFromStandingUp`: walking frame by frame with `RoomMotion.Step` only, each target's prompt appears within 4 s, and the character never enters a wall or an expanded obstacle.
3. `CONTENT6_SpawnStandAndInteractPointsAreFreeFloor`: the start, stand-up and interaction points are walkable. Stand-up is 0.6 m south of the chair point. The chair position equals the chair point.
4. `CONTENT6_ObstaclesAreTheBlockingFurnitureFootprints_InsideTheRoom`: the D-130 literal boxes equal centre ± size/2 of the blocking furniture, they lie inside the room, and the chair does not block.
5. `R004_ExactRange1Point2IsInside_ForEveryTarget_1Point21IsNot`: covers all four targets, including the floating-point case 3.7 − 2.5 = 1.2000000000000002 (9-decimal rounding).
6. `R004_TieBetweenBookshelfAndBed_GoesToBookshelf`: a second tie pair (the developer tested table/counter).
7. `F002_SpeedIs3MetresPerSecondInEveryDirection_InOpenFloor`: 16 directions × 0.2 s = 0.6 m each. One 1 s call equals 50 frames of 0.02 s.

No new PlayMode tests were added; the existing 33 cover the [Unity] ACs.

## Bugs
None found.

## Untested / pending
- **PENDING (user playtest of the Windows build):**
  - OnGUI draws the HUD, buttons, book and dialogs without errors (AC 8b, W-1).
  - Korean glyphs render (F-013 AC-03).
  - F-007 AC-07 (house ★3 / tulip ★2 from diagrams alone).
- Real mouse → `ScreenToPaper` picking is not exercised in batch mode. The smoke test injects paper points (same as the 012 tests).
- Window focus loss → move (0, 0) is covered only by the `InputReader` code path (`Application.isFocused`). The test feeds a (0, 0) move directly.
- Review W-2 (per-frame GUI allocations) is not measured; it is performance only.
