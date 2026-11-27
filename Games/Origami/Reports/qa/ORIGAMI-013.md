# QA — ORIGAMI-013 (v2 Unity book screen: BookPanel / DiagramRaster / IBookHost)

- QA: qa-engineer · Branch `claude/serene-fermat-4bwjlo` · tested commit `0ed8137` (contains impl `14257ae`)
- Review: APPROVED_WITH_WARNINGS. W-1..W-3 are Orchestrator follow-ups and do not block this task.
- **Result: PASS → COMPLETED.** F-007 AC-07 and the on-screen IMGUI layout are user-playtest items and are pending; they are not blockers.

## Verification run
| Command / source | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | PASS, 972/972 |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| CI `unity` run [46036542487](https://github.com/Junho5408/second_game/actions/runs/46036542487), job `unity (Origami)`, head `0ed8137` | success (see `Reports/build/ORIGAMI-013.md`) |
| `playmode-results.xml` | 90/90 Passed, 0 inconclusive, 0 skipped. All 36 `Origami.Tests.PlayMode.BookTests.*` Passed |
| `editmode-results.xml` | 972/972 Passed, 0 inconclusive, 0 skipped |
| `error CS` in the CI logs | 0 |
| Windows build step | success, artifact `Origami-windows-build` |

## Acceptance criteria
| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | IBookHost contract: two-page spread, diagram textures, contents, buy-button callback (F-008) | PASS | All of these Passed in CI.<br>• **Spread:** `F007_FirstSpread_IsContentsAndSymbols` (0/1 = contents/symbols), `R010_OddHostPage_ShowsItsSpread`, `TurnButtons_CallHostWithCurrentPlusMinusTwo`, `ClickTurn_Saturates…`<br>• **Contents:** `F007_Contents_ShowsOwnedOrPagePriceAndCoverPage_FixedTexts` (5 entries: name, "보유"/"N원", cover page 2/10/17/24/32, matching CONTENT §1/§3), `R010_ContentsClick_TurnsToCoverSpread` (TurnPage(17) → spread 16/17, cover tulip), `ClickContents_UnknownOrNull…`<br>• **Cover / buy:** `F008_UnownedCover_HasBuyButton_ThatCallsHostBuyPage` (page price 20, base price 12), `F008_OwnedCover_HasNoBuyButton`, `F008_WithScreenFlow_BuyFromCover_UnsealsPagesAndRemovesButton` and `…NotEnoughMoney_StaysSealed` (a real ScreenFlow; F-008 AC-01/AC-03)<br>• **Diagrams:** `F007_PlaneStep4_ValleyLineArrowBothSidesAndAfterInset` (F-007 AC-02, page 6), `F007_CreaseStep…` (page 3, fold and unfold), `F007_TopLayersStep…` (tulip 21, top 2 layers, label in both the view and the raster), `F007_MountainStep…`, `F007_AnchorCircles…`, `EveryOwnedPicturePage_Renders`, `Symbols_ContainEveryNotation`, `RenderShape_Grey_IsUniformSilhouette`, `RenderShape_PaperYIsTextureUp…` |
| 2 | Supports F-007 AC-06 (the book-drawing part; owned by 014). F-007 AC-07 goes to the user playtest | PASS (supporting part) / PENDING (AC-07) | The panel draws on request only. It has no Update or OnGUI of its own (review §3), so 014 can show it at the left while folding. AC-06 input/mode and AC-07 are verified outside this task. AC-07 waits for the user playtest |
| 3 | PlayMode tests do not depend on frame timing | PASS | All 36 are plain `[Test]` methods. There is no `[UnityTest]` or yield (grep), and nothing is inconclusive in CI |
| 4 | Public surface follows INTERFACE_CONTRACTS C-192; no assertions on 0.849/0.848 or J-case percentages | PASS | Review §2 checked the surface. `grep -n "0.849\|0.848\|%"` over `Tests/PlayMode/Book/` finds no such assertion. No test expects a null RightPage (QA-3) |
| 5 | check_meta OK; Unity CI `unity (Origami)` compile, EditMode and PlayMode pass | PASS | Table above |
| 6 | CONTENT §3 fixed texts are used verbatim and checked as strings | PASS | The tests use their own literals and do not read `BookTexts`:<br>• `"봉인됨 — 표지에서 페이지를 사세요"` (const `SealedText`, `F007_SealedStepAndCompletePages_ShowOnlyTheFixedSealedText`)<br>• `"판매대에서 기본 6원"` and `"…8원"` (`F007_CompletePage_ShowsFixedBasePriceText`)<br>• `"보유"` and `"20원"`/`"50원"`/`"150원"` (contents test)<br>All of them match CONTENT_SPECS lines 94/104/105 character for character |

## Static coverage review (spec → tests)
- **Spread layout (R-010, F-007 AC-01/AC-04):** covered as listed above. Clamping beyond page 43 is a Core concern (`BookLayout.ClampToSpread`, dotnet-tested). The panel's saturation is tested.
- **Sealed pages (F-007 AC-03):** steps and the complete page show only the sealed text and have no texture. After buying they show the diagram.
- **Cover info (CONTENT §3 line 96):** the name, page price, base price and buy button are asserted. The grey silhouette for an unowned cover is checked at raster level.
  - ★ difficulty, move count and technique lines are present in `BookTexts`, but not every one is asserted individually. The wording is a Runtime label (review W-1), so this is Low.
- **Diagram content:** line types (valley, mountain, fold and unfold), arrows, layer label, anchors and after-inset are covered.
- **Decision on new tests:** I added no QA tests. I found no clear gap against the task's criteria, and adding tests would need another CI run.

## Observation (not a bug)
- The task notes, the review and the QA brief all state "37" Book PlayMode tests. The source declares **36**. `grep -c "\[Test\]"` counts 24 in BookPanelTests.cs because the doc comment on line 12 ("Plain [Test]s") also matches; DiagramRasterTests.cs has 13. CI ran 36/36. No test was dropped.

## Untested / pending
- `BookPanel.Draw(Rect)` IMGUI layout (text overflow, picture size): not exercised (review W-3). A smoke test or playtest check is recommended in ORIGAMI-014.
- No feedback in the book for a rejected purchase (review W-3b): this belongs to 014.
- F-007 AC-07 (folding house ★3 and tulip ★2 from diagrams only): user playtest, pending.
- I did not run the Windows build artifact. Only the CI build step result is known.

## Bugs
None found.
