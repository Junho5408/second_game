# QA ORIGAMI-016: v2 기술 문서 후속 정리(계약 보완)

- QA: qa-engineer · Branch: worktree-agent-ac6dc9a17fb336bf1 · Base 5384f02 · Head reviewed fac4949
- Review: APPROVED_WITH_WARNINGS (W-1 Low doc precision, N-1 info)
- Verification items: `review` only (no unity-* items)
- Result: **PASS → COMPLETED**

## Commands run

| Command | Result |
|---|---|
| `git diff --name-only 5384f02 HEAD -- Games/Origami/Unity` | 0 files. The task commit 20b605f only touches `Docs/tech/{ARCHITECTURE,CODING_CONVENTION,INTERFACE_CONTRACTS}.md` |
| `bash studio/scripts/verify_core.sh Origami` (before adding QA tests) | PASS, 972/972 |
| `bash studio/scripts/verify_core.sh Origami` (with the new QA tests) | PASS, 978/978 |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |

## Acceptance criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | C-150 NaN/∞ → InvalidFold(NothingToFold) | PASS | `OrigamiGame.FoldOrCrease` (Core/Game/OrigamiGame.cs:389-411) runs CheckSheetCommand (RunEnded → WrongMode → NoSheet), then `layers < 0` → InvalidLayers, then `!IsFinite(grab) \|\| !IsFinite(drop)` → NothingToFold. This happens before the engine call and before `StepScoring`. `IsFinite` checks NaN and ±∞ on X and Y (line 413). The doc row and the "좌표 방어" bullet give this order and outcome. The text `FoldTexts.ErrorMessage(NothingToFold)` = "접히는 부분이 없어요" (FoldTexts.cs:17). The new QA tests confirm this at runtime (below). |
| 2 | C-170 NoSheet silent + line formats | PASS | `GameTexts.RejectMessage` has no NoSheet case, so it falls to `default: ""`. FoldTexts default "" covers None/InvalidLayers. I compared the three line tables with `SettlementLines`/`CounterLines`/`ResultLines` (GameTexts.cs, lines 111-170): headers, per-line formats, ` (인기)`, empty lines (`팔린 작품이 없어요`/`진열된 작품이 없어요`), totals, the three end variants, `ModelCatalog.Models` order, `★3 a · ★2 b · ★1 c`, `Stars` ("★n", None → ""), `Num` (InvariantCulture), `Won` (`N원`) and the null → ArgumentNullException guards. All match. The existing `GameTextsTests` (`[TestCase(RejectReason.NoSheet, "")]`) and the new QA test confirm that NoSheet is silent. |
| 3 | C-191 `PaperMeshBuilder.Build(IReadOnlyList<Facet>)` | PASS | Scripts/Folding/PaperMeshBuilder.cs:56 has `Build(Mesh mesh, IReadOnlyList<Facet> facets, VertexXform xf, bool topOnly, bool edges, float yOffset, Color edgeColor)`. In the code: mesh null → ArgumentNullException, xf null → Identity, facets null → skipped, and it returns `s_verts.Count == 0 ? 0f : minY`. The other listed members (BaseY/LayerGap/Thickness, LayerTop, LayerBottom, Identity, NewMesh, AddCreaseLines) exist with the documented signatures. |
| 4 | ARCHITECTURE §2 Book deps + BookTexts | PASS | The `using` directives in Scripts/Book/*.cs are Core.Book, Core.Catalog, Core.Folding (DiagramRaster, BookTexts), Core.Geometry (DiagramRaster) and Runtime.UI (BookPanel). `grep "Core\.Game\|Core\.Screens\|Core\.Texts" Scripts/Book/` finds nothing. The diagram arrow matches. Review W-1 holds: the explanatory bullet credits Folding only to DiagramRaster, but BookTexts also uses it. This is Low, doc-only, and does not make the arrow wrong. |
| 5 | Docs only, no code change | PASS | 0 files under `Games/Origami/Unity` changed between 5384f02 and fac4949. |

## Tests added

The tests are in `Games/Origami/Unity/Assets/Game/Tests/EditMode/Core/Qa/ContractDocsV2QaTests.cs` (+ .meta). There are 6 test cases. They cover review N-1: the existing `F005_AC03_NonFiniteCoordinatesAreRejectedNotThrown` checks only `RejectReason.InvalidFold`, not the documented `FoldError`.

- `C150_NonFiniteFoldAndCrease_AreNothingToFold` ×4 (NaN/±∞ in each coordinate, on a tutorial sheet with the guide showing). Fold and Crease both return InvalidFold + NothingToFold. The snapshot is unchanged, there are no plan or guide score, ActionCount stays 0 and GuideScores is empty. The toast text is "접히는 부분이 없어요".
- `C150_NegativeLayersCheckedBeforeNonFiniteCoordinates`: NaN together with n = −1 → InvalidLayers, and the text is "".
- `C150_NoSheetCheckedBeforeNonFiniteCoordinates`: NaN with no sheet → NoSheet, FoldError.None, and the text is "".

All pass under dotnet. These Core tests also run in Unity EditMode, but this task has no unity-* item. The next Unity CI run for Origami will include them.

## Bugs

None.

## Open / untested areas

- Review W-1 (Low, docs): the ARCHITECTURE §2 bullet should also say that BookTexts uses Core.Folding for the fold-kind label. Leave it for the next architect doc pass.
- The table rows in criterion 2 were checked by reading the code. They are already fixed by ORIGAMI-011 `GameTextsTests`/`TextsV2SpecQaTests`. I did not re-derive every line with a new test.
- C-191 Build behaviour (Unity `Mesh`) was checked by reading the code only. There is no Unity run in this task.
