# QA: ORIGAMI-008 — v2 technical design (ARCHITECTURE / INTERFACE_CONTRACTS / DATA_STRUCTURE / CODING_CONVENTION)

- QA: qa-engineer · Branch `claude/serene-fermat-4bwjlo` · Reviewed docs at commit c675897 (docs 88c0830)
- Task type: docs-only, verification item `review` (passed: APPROVED_WITH_WARNINGS, W-1..W-3, N-1..N-5 — the Orchestrator carries these into the ORIGAMI-009..014 task files; they are not re-judged here)
- Perspective: could a developer write the C# and the tests for 009..014 using only these docs? Are the four tech docs consistent with each other and with `Docs/design/`?

## Result: **PASS** (COMPLETED)

All 5 acceptance criteria are met. I found no blocking defect. Three new Low/Info observations (QA-1..QA-3) are listed for the Orchestrator to add to task notes. They need no doc pass.

## Regression

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | `== Origami: PASS ==`, 283/283 passed (v1 tests; no code changed) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |

## Acceptance criteria

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| 1 | ARCHITECTURE: v2 modules (Core Folding/Recognize/Economy/Day·Ending/Tutorial·Book; Runtime room·character·seat·fold view·book UI) + v1 keep/replace/delete plan | **Pass** | §1.1 lists 11 Core modules, each with its folder, namespace, dependencies and owning task. §1.2 lists Runtime Systems/Folding/Book/Gameplay/UI. §10 is a 20-row v1 table (action, replacement, deletion timing; everything is deleted in 014). I checked the v1 namespaces in code: `Origami.Core.{Content,Flow,Rules,Run}`, `Origami.Tests.{Contracts,Flow,Qa,Rules,Run,PlayMode.Flow}`. No v1 type is named after a v2 namespace (grep for `class/struct/enum Geometry\|Folding\|…\|Room`: none). The only bare `Folding` tokens are `FlowScreen.Folding` member accesses, so adding `Origami.Core.Folding` etc. cannot shadow v1 code during coexistence. |
| 2 | INTERFACE_CONTRACTS: fold engine API on Fold3DSpike (top-N with R-007 closure, R-021 crease magnets with a deterministic tie rule, Unfold/Undo) + `Recognize(state, owned, catalogue)` signature and return value | **Pass** | C-103 `PlanFold/PlanCrease(state, grab, drop, [mountain,] layers)` → `FoldPlan` (Valid, `FoldError` enum with 6 values, Moving/Static/Moved/NewCreases/Result) uses the 8-step algorithm, the closure rule and named constants. C-104 `FoldSession` has `Undo/Unfold` → bool and counts. C-106 `DropSnap.Snap` (P → zero-drag → L → M) uses `Key` = round 1e-9 keys and documents its expected values. C-110 `Recognizer.Recognize(PaperState, IEnumerable<string>, RecognitionCatalog)` → `RecognitionResult` (ModelId/Grade/IsPiece/Score/Percent/BestModelId/BestScore), with strict-greater tie = catalogue order. Every argument error names its exception type. |
| 3 | DATA_STRUCTURE: paper state (facets, layer order, creases in paper coordinates), catalogue / book pages / outline data, save state (7-day progress) | **Pass** | D-101 (facets, world+paper, ordered list as state), D-102 (layers), D-103 (per-step facet/crease/fold counts), D-104 (`FoldStep` record), D-105 (row-scan `Outline`), D-110..D-115 (catalogue, steps, book layout, popular, endings, guide), D-120..D-123 (game state with start values, sheet, display, settlement), D-130 (room), D-140 (tolerances), D-170 (JSON save format: day/money/…/display as step lists). |
| 4 | Implementation task breakdown: non-overlapping file_scopes, dependency order, FEATURE_SPECS AC numbers per task | **Pass** (W-1/W-2 from review are carried by the Orchestrator) | ARCHITECTURE §13 gives 6 tasks with disjoint folder scopes (Core/Tests subfolders per task, Runtime Systems+Folding / Book / Gameplay+UI). The DAG 008→009→{010→011→013, 012}→014 has no cycle. Each row has an AC list and a verify list. `Scripts/Systems/` currently holds only `README.md(.meta)`, which is no conflict for 012. |
| 5 | CODING_CONVENTION updated; deterministic numeric comparison rule (1e-9 rounding etc.) | **Pass** | §"수치 규칙": named epsilons in one place, `DropSnap.Key` = `Math.Round(v, 9, ToEven)` for every tie comparison, a ban on raw `<`/`==` candidate selection, `Grading.Percent` only, the Python-parity rule and a stable-sort rule. The test section adds FEATURE_SPECS tolerances, a boundary-assertion ban and the PlayMode `dt` injection rule. |

## Testability spot checks (independent of the review's checks)

Scratch scripts are in `/tmp/claude-0/-home-user-second-game/636884ad-8c21-5cae-9861-9592d708d5ff/scratchpad/qa8/` (`room.py`, `chk.py`). Except for `room.py`, they use `balance_check.py` as the reference implementation.

| Contract example | Doc value | Computed | OK |
|---|---|---|---|
| C-180 `RoomMotion.Step` written exactly as the C-180 text (substeps ≤ 0.05, x then z, widened boxes) | start (1,0) 1 s → x 1.4; (−1.6,2) west 2 s → −2.2; (0,1) north 2 s → z 2.25; west 5 s → −3.7 (F-002 AC-02/04/04b) | 1.4 / −2.2 / 2.25 / −3.7. Each result is identical for 1×1.0 s and 50×0.02 s. Diagonal 0.5 s moves 1.5 m (AC-03) | yes |
| D-130 interaction points | start → bed 2.04, bookshelf 2.48 (F-002 AC-01); points ≥ 1.5 m apart (CONTENT §6) | 2.040 / 2.484; all 4 points lie outside the widened obstacles (reachable); min pairwise 2.002 | yes |
| C-130 `Pricing` vs CONTENT §5 (30 cells) | table | all 30 cells equal | yes |
| C-120 `BookLayout` | 44 pages, covers 2/10/17/24/32; `ClampToSpread` −1→0, 17→16, 31→30, 43→42, 99→42 | same (from `REF` step counts) | yes |
| C-160 F-007 AC-02 (page 6 = plane step 4) | before: 2 folds, 1 crease; line (0,1)/(−0.8284,−1); grab (−1,0), drop (0,−0.4142); p3 Crease; p21 layers 2 | folds 2, crease segments 1, line (−0.828427,−1)–(0,1), drop (0, −0.414214); p3 `C`; p21 = tulip step 4, layers 2 | yes |
| C-107 F-005 AC-18 | diamond: StackCount at (−1,−1) = 6 | 6 of 6 facets contain C | yes |
| C-110 `Grading.Percent` (F-009 AC-04) | 0.97★3, 0.969★2, 0.93★2, 0.929★1, 0.85★1, 0.849 None | floor(s·100+1e-9) gives 97/96/93/92/85/84, the expected grades | yes |
| F-010 AC-03 "house 72~73%" with the row scan (J-09 0.729) | 72~73 | Percent 72 | yes |
| C-140 `StepScoring` vs Fold3DSpike `FoldScoring.Evaluate` | same formula | spike: offset/angle tolerances 0.25/20, `round(100·(0.5pos+0.5ang))`, wrong side ×0.5, EitherSide mirror. Matches the contract text; exact plane step 1 → 100 (F-006 AC-02) | yes |
| C-102 `TrySnapGrab` tie rule | "같은 거리면 나중 것" | spike uses `d <= best`, so the later candidate wins | yes |
| Economy scenarios F-008 AC-05 / F-011 AC-02/03, D-123 | 23 / 152 / 40 | 77−2−2−50 = 23; 77−50−10+135 = 152; 20−4+24 = 40 | yes |

Cross-document consistency: I compared the C-150 rejection orders with R-003/R-006/R-007/R-009/R-014/R-019. The C-171 key table matches F-003. C-170 strings match F-013. D-111 steps match `balance_check.REF` (including `_tip` 6-digit literals). D-114 matches CONTENT §8. D-120 start values match R-002. Guide visibility and score truncation (C-150) match R-008 and F-006 AC-02/04/07. I found no contradiction.

## Observations (new, non-blocking)

No bugs (no BUG-ORIGAMI-008-n). These are clarifications that test authors in later tasks will need:

- **QA-1 (Low, doc inconsistency) — Runtime Book view dependency list is too narrow.**
  - ARCHITECTURE §2 says "Book view ─▶ Core.Book/Texts/Catalog만".
  - C-192 `DiagramRaster.Render(StepDiagram)` / `RenderShape(PaperState, …)` must read `PaperState.Facets`, `Facet.World/Layer/FrontUp`, `FoldStep`, `SnapKind`, `DropSnap.MarkPieces` (Core.Folding) and `Vec2` (Core.Geometry).
  - Impact: a strict 013 review could flag a needed `using Origami.Core.Folding`.
  - Recommendation: in the 013 task note, allow Core.Folding/Geometry for `Scripts/Book/`.
- **QA-2 (Low, unspecified edge) — zero-length line.**
  - `FoldLine.FromGrabDrop(g, g)` gives Normal (0,0) (`Normalized` keeps zero). The Python reference divides by zero.
  - `LineOnPaper.TryGet(state, g, g, …)` would then return true with a == b == g. No contract says what it should do.
  - Callers that follow the contracts never pass this: catalogue/guide steps all have drag ≥ 0.03, `Snap` returns before L, and `PlanFold` rejects with `DragTooShort` first.
  - Recommendation: the 009 note should say either "TryGet returns false when Distance(grab, drop) < 1e-9" or "no test asserts this case".
- **QA-3 (Info) — `BookPanel.RightPage` null branch is unreachable.**
  - C-192 says the right page is null "43쪽 다음". With 44 pages the last spread is 42·43, so every spread has a right page.
  - Recommendation: 013 tests should not expect null there.

## Untested / out of scope

- Unity-side contracts (C-190..C-194: shader fallback, IMGUI layout, camera, PlayMode timing) cannot be checked for a docs task. They will be verified by the `unity-*` items of 012/013/014.
- Fold engine parity (C-102..C-107) and the row-scan IoU table (ARCHITECTURE §9) were checked step by step by the review. I re-checked only the examples above, not every J case.
- AC ownership mapping (W-1/W-2) and the J-05 percent (W-3) are left to the Orchestrator, as instructed.
