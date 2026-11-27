# Review ORIGAMI-016 — v2 기술 문서 후속 정리(계약 보완)

- Reviewer: code-reviewer
- Base: 5384f02 · Head: 20b605f (+ f94e8b0 orchestrator task-file commit)
- Result: **APPROVED_WITH_WARNINGS**

## Checks run

| Command | Result |
|---|---|
| `check_scope.py Origami ORIGAMI-016` | OK |
| `git diff 5384f02 HEAD --stat` | only `Docs/tech/{ARCHITECTURE,CODING_CONVENTION,INTERFACE_CONTRACTS}.md` + task file; no code changed |
| `verify_core.sh Origami` | PASS (972/972) |
| `check_meta.py Games/Origami/Unity` | OK |

## Acceptance criteria

1. **C-150 NaN/∞ → InvalidFold(NothingToFold): met.** `OrigamiGame.FoldOrCrease` (Core/Game/OrigamiGame.cs:390-413): CheckSheetCommand (RunEnded → WrongMode → NoSheet) → `layers < 0` → InvalidLayers → `!IsFinite(grab) || !IsFinite(drop)` → NothingToFold, before the engine call and before any guide-score recording. The doc row and the new "좌표 방어" bullet state that order, the outcome (no exception, no state change, no score) and the text `FoldTexts.ErrorMessage(NothingToFold)` = "접히는 부분이 없어요" (FoldTexts.cs:17). Matches the DECISIONS row and ORIGAMI-010 W-1.
2. **C-170 NoSheet silent + line formats: met.** `RejectMessage` has no case for NoSheet → `default: ""` (GameTexts.cs:55). FoldTexts default "" covers None/InvalidLayers, as the doc says. I compared the three tables with the code character for character:
   - SettlementLines: `N일차 정산`, `이름 ★n P원` + ` (인기)`, `팔린 작품이 없어요`, `합계 N원`, `정산 후 돈 N원`, then Completed / Bankrupt / default `내일의 인기: 이름` — matches lines 111-127.
   - CounterLines: `N일차 판매대`, `오늘의 인기: 이름`, `이름 ★n 일치도 xx% · 오늘 팔면 P원` with P = `Pricing.SalePrice(id, grade, s.Day)`, `진열된 작품이 없어요`, `합계 예상 N원` (sum of P) — matches lines 131-145.
   - ResultLines: ending name + text only when an ending is found, `최종 돈 N원`, per `ModelCatalog.Models` `이름: ★3 a · ★2 b · ★1 c`, `실패 N`, `총 판매액/총 종이값/총 책값 N원` — matches lines 149-170.
   - "★n" and Grade.None → "" (`Stars`), InvariantCulture without separators (`Num`), `N원` (`Won`), null → ArgumentNullException for all six listed functions, read-only copy (`ReadOnlyCollection` over `ToArray`) — all correct.
3. **C-191 `PaperMeshBuilder.Build(IReadOnlyList<Facet>)`: met.** The listed public members (`BaseY/LayerGap/Thickness`, `LayerTop`, `LayerBottom`, `Identity(double,double,float)`, `NewMesh(string)`, `Build(Mesh, IReadOnlyList<Facet>, VertexXform, bool, bool, float, Color)`, `AddCreaseLines`) match Scripts/Folding/PaperMeshBuilder.cs. The behaviour note is accurate too: mesh null → ArgumentNullException, xf null → Identity, facets null → empty, returns minY or 0 when empty (line 133).
4. **ARCHITECTURE §2 / C-192 Book deps + BookTexts: met.** The `using` directives in Scripts/Book/*: Core.Book, Core.Catalog (BookPanel, BookPageView), Core.Folding (DiagramRaster, BookTexts), Core.Geometry (DiagramRaster), Runtime.UI (BookPanel → `KoreanFont.Get` in `EnsureStyles`, which only `Draw` calls). No Core.Game / Core.Screens / Core.Texts reference, inline or by `using`. "Core.Texts 허용" is a permission, not a claim, so that is fine. `KoreanFont` is `internal` in Scripts/UI and does not reference Book, so there is no cycle. `BookTexts` is `internal static class` in Scripts/Book with the CONTENT §3 fixed strings verbatim. The CODING_CONVENTION exception sentence is consistent.
5. **Docs only: met.**

## Findings

### W-1 (Low, doc precision) — ARCHITECTURE.md §2 bullet credits Core.Folding only to DiagramRaster
- Location: Games/Origami/Docs/tech/ARCHITECTURE.md, the new bullet under the dependency diagram ("Folding/Geometry는 `DiagramRaster`가 도해…를 그리려고 쓴다").
- Problem: `Scripts/Book/BookTexts.cs` also has `using Origami.Core.Folding` (`FoldStepKind`, line 77, for the fold-kind label).
- Impact: none at runtime. The arrow in the diagram (Book view → Core.Folding) is still correct. Only the explanation is incomplete, so a later reviewer might flag BookTexts as a violation.
- Fix: at the next architect doc pass, add "BookTexts(접기 종류 표기)" to the reason, e.g. "Folding은 DiagramRaster(도해)와 BookTexts(접기 종류 표기), Geometry는 DiagramRaster가 쓴다".

### N-1 (Info) — outstanding test follow-up from DECISIONS
- The ORIGAMI-010 DECISIONS row also asks for stronger test assertions ("테스트 단언 강화는 그때 함께", ORIGAMI-010 W-1/W-2). This task is docs-only and cannot change tests, so the item is still open. The Orchestrator should track it in a developer/QA task, if it is not already covered.

## Checklist summary
Spec/AC conformance ✓ · conventions ✓ · module boundaries (doc matches code) ✓ · null/invalid states documented correctly ✓ · performance n/a · no duplication ✓ · no breakage (no code changed, Core 972/972) ✓ · tests n/a (docs only).

## Verdict
**APPROVED_WITH_WARNINGS** → QA.
