# Review: ORIGAMI-008 — v2 technical design (architecture, contracts, data, conventions, task breakdown)

- Reviewer: code-reviewer
- Base commit: a500d87 · Reviewed commits: 88c0830 (docs), 0c6fda7 (status)
- Changed files: `Docs/tech/ARCHITECTURE.md`, `INTERFACE_CONTRACTS.md`, `DATA_STRUCTURE.md`, `CODING_CONVENTION.md` (all in `file_scope`)

## Result: **APPROVED_WITH_WARNINGS**

All 5 task acceptance criteria are met. The fold-engine and magnet contracts (C-102..C-107) match `balance_check.py`
step for step. I re-ran the architect's row-scan IoU method on my own, and it reproduces the §9 table exactly. No defect would block
implementation. The warnings are about AC traceability (non-unique and misplaced mappings) and one displayed percentage that
differs from the design text. The Orchestrator should fix these when creating ORIGAMI-009..014, or ask the architect for a
small doc pass.

## Verification run

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-008` | `OK` |
| `bash studio/scripts/verify_core.sh Origami` | PASS, 283/283 (v1 tests; docs-only change) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| scratch: `balance_check.apply` per step for D-103 | all 5 tables (facets, crease segments, folds) and area 4.0 match exactly |
| scratch: tip literals | tulip (0.689655, −0.275862)/(−0.275862, 0.689655), cicada (0.453858, −0.108926)/(−0.108926, 0.453858) = D-111 |
| scratch: stack count at C after tulip 1–3; AC-16 snaps | 6 of 6 facets; M (0, 0.2), P (0, 0), L (−0.6, −0.8), none (−0.62, −0.78) = C-106/C-107 |
| scratch: row-scan IoU written to C-110 text (h = 0.001 and 0.0002), compared with the `balance_check` grid judge, all J cases + dog-no-chin + model matrix | see §2 |

## Checklist

### 1. Acceptance criteria
| AC | Verdict | Evidence |
|---|---|---|
| 1 ARCHITECTURE modules + v1 migration | Met | §1.1 Core (Geometry/Folding/Recognition/Catalog/Economy/Tutorial/Game/Book/Texts/Screens/Room), §1.2 Runtime (room, character, seat camera, fold view, book UI), §10 keep/replace/delete table with deletion timing |
| 2 Fold engine API + Recognize | Met | C-103 (top-N with closure, R-007), C-104 Undo/Unfold, C-106 R-021 with 1e-9 key tie-breaks, C-110 `Recognizer.Recognize(PaperState, IEnumerable<string>, RecognitionCatalog)` → `RecognitionResult` |
| 3 DATA_STRUCTURE | Met | D-101..D-105 (facets, layer order, creases in paper coords), D-110..D-115 catalogue/book/popular/endings/guide, D-120..D-123 state, D-170 save format (7-day progress, step lists) |
| 4 Task breakdown | Met, with warnings W-1/W-2 | §13: ORIGAMI-009..014, disjoint scopes, DAG, AC numbers |
| 5 CODING_CONVENTION numeric rules | Met | 1e-9 key rounding (`DropSnap.Key`), named epsilons, Python-parity rule, `Grading.Percent` only, boundary-assertion ban (W-6), PlayMode dt injection |

All FEATURE_SPECS ACs F-001..F-013 have at least one owning task (I checked them one by one, including F-002 AC-04b, F-006 AC-09,
F-005/F-007 playtest ACs). There are no gaps. Several ACs are mapped twice; see W-1.

### 2. Contracts vs `balance_check.py` (spot checks)
- `_split`: the "no negative → static = original", "no positive → moving = original" and "degenerate part (< 3 points or |area| ≤ 1e-7) → whole facet to the other side" rules match, along with strict sign change for intersections and `≤0`/`≥0` membership.
- `plan`: the stack is sorted by layer descending with a stable index order (= Python `sorted` stable). `mv` = first `layers` entries that have a moving part. The closure (hinged, or higher layer with overlap > MIN_AREA) is order-independent because it is a monotone fixpoint. Static ordering, `LineOutsidePaper` and `Partial` match.
- `fold`: maxA/minA/maxM/minM, the mountain+partial shift, the three layer formulas, reversed-vertex reflection, FrontUp flip and rank renumbering are identical. `crease`: zero-side edges of moving parts, facets unchanged. `flip`: identical.
- `_hinged`, `_overlap_len`, `_overlap_area`/`_clip`, `mark_pieces` (Cyrus–Beck, 1e-12/1e-9/1e-6), `visible`, `landmarks` (dedupe 1e-6, first kept), `snap` (P → none-if-zero-drag → L pairs sorted by key, (sum, a.x, a.y, b.x, b.y) → M) and `line_on_paper` all match. The only intentional deviations are documented: layers < 0 → `InvalidLayers` checked first (Python has no such check; F-005 AC-03 only needs "InvalidFold"), the tutorial guide magnet step 0 (R-008/R-021), `LineOnPaper` unrounded with ±1e-4 tests, and anchors added to `SnapResult`.
- Row-scan IoU (C-110), my independent implementation of the written algorithm:
  - h = 0.001 reproduces every number in ARCHITECTURE §9 (e.g. J-05 0.9499, J-07 0.7994, J-14 0.7171, J-18 0.7825, tulip–dog 0.612, dog-no-chin 0.8476).
  - Against h = 0.0002 (≈ exact) the maximum difference is 0.0008, so the claim "≤ 0.002" holds.
  - Against the design grid judge (0.004) the maximum difference is 0.0059 (J-13 cicada, where §4 gives the range 0.994~1.000), then 0.0045 (J-07). All values are within the F-009 tolerance of ±0.01, and every result and grade is the same. The ARCHITECTURE §0/§9 wording "all within 0.005 of §4" is slightly loose for J-13 (a range), but this does not affect anything.
  - Reference areas: plane 0.843, cicada 0.687, both within D-110 ±0.01.

### 3. Engine/platform constraints
- Core: netstandard2.1/C# 9, no UnityEngine, `double` only, `Lazy<T>` for immutable caches. `Core.Build`/`Core.Tests` glob `Core/**` and `Tests/EditMode/Core/**` recursively, so the new folders compile with no csproj change.
- Physics: confirmed. `Games/Origami/Unity/Packages/manifest.json` has `com.unity.modules.physics2d` but **not** `com.unity.modules.physics` (Fold3DSpike has `physics`). The plan avoids everything that needs it:
  - meshes come from code (`MeshFactory`, `PaperMeshBuilder`; MeshFilter/MeshRenderer are CoreModule);
  - picking uses ray–plane math (`Camera.ScreenPointToRay` + `UnityEngine.Plane` are CoreModule);
  - collision uses Core `RoomMotion` box math;
  - the spike's `CreatePrimitive(Quad)` fallback in `SpikeMaterials` is explicitly dropped.

  The fallback (add the physics module through an architect task) is documented and is not being proposed now.
- Shaders: Origami has no `GraphicsSettings.asset`, the same as Fold3DSpike, so Unity's default Always Included list applies. That list contains "Legacy Shaders/Diffuse", "Sprites/Default" and "UI/Default". The same configuration shipped a Windows build that the user confirmed. "Standard" can be stripped from a build, but it is only a middle fallback. This is acceptable. N-4 covers the all-fail case.
- Legacy Input only in `InputReader`; IMGUI with a single `OnGUI`; `Bootstrap.cs` untouched, with an `AfterSceneLoad` hook + `EnsureInstance`; one `Update` (`OrigamiApp`), and every child takes a `dt` argument. Camera smoothing `1 − e^(−8dt)` and `RoomMotion` substeps (≤ 0.05 s) give the same results for 1×1.0 s and 50×0.02 s on the AC paths. PlayMode tests call `[Test]` + `ProcessFrame` directly, so they do not depend on frame timing.

### 4. v1 → v2 migration
- v1 Core types are all in the sub-namespaces `Origami.Core.Content/Rules/Run/Flow`. Only `GameInfo` is in `Origami.Core`. v2 uses disjoint folders and namespaces and must not `using` v1. Because no v1 type sits in the parent namespace `Origami.Core`, a simple name in v2 code (e.g. `Grade`) cannot silently bind to v1, so both versions compile side by side.
- The v1 `SurfaceContractTests` only checks named v1 types and "no UnityEngine reference". It does not enumerate the assembly, so adding v2 types in 009..011 will not break v1 tests outside those tasks' scopes.
- Nothing is deleted before its replacement exists: everything is deleted together in 014, which also replaces the v1 hooks. The 6 v1 QA files match the current `Tests/EditMode/Core/Qa/` contents exactly. `GameInfoTests` checks only `Name`, so changing the version to 2.0.0 is safe. asmdefs are unchanged, and the existing PlayMode asmdef references Runtime + Core, so it covers the new `FoldTable/`, `Book/` and `Workshop/` folders.

### 5. Architect-filled gaps A2-1..A2-14
All are reasonable and none contradicts the design.
- A2-4 (apply on release, then animate) changes "animation then commit" from F-005. F-005 AC-17 still holds (the count is checked after the animation), and the change removes the risk of Core and the view drifting apart.
- A2-8 (wheel direction) and A2-10 (SkipGuide order) fill spec gaps.
- A2-9 (EitherSide only for steps 1 and 6) is geometrically correct, because both are the x = 0 line.
- A2-11 (overlays reject commands with WrongMode) is the smallest safe rule.
- A2-14 (near clip 0.1/0.01) is consistent with the 0.35 mm layer gap at a 0.1 scale.

### 6. Parallel claims
- 012 ∥ 010/011: `IFoldTableHost` uses only `PaperState`, `Vec2` and `FoldError` (009). `FoldTableGuide` and `PointerFrame` are Runtime types in 012's own scope. `FoldTexts` was moved into Core/Folding (009) for exactly this reason. Tests use a fake host over `FoldSession`. The two tasks share no files, so this is safe.
- 013 ∥ 012: `IBookHost` uses only Book/Catalog/Texts (011). 013 needs nothing from `Scripts/Systems` (it uses Texture2D raster only) and only reads `KoreanFont`. This is safe.
- The scopes are disjoint, including Tests subfolders. Folder `.meta` siblings are covered by the AGENTS §6 ".meta of scope paths" rule.

## Findings

### W-1 (Medium, traceability) — several FEATURE_SPECS ACs are mapped to two tasks
- Location: ARCHITECTURE §13 table, column "FEATURE_SPECS AC".
- Problem: the following ACs appear in two tasks:
  - F-005 AC-01..05 and AC-10 (009 "engine level" and 010 "command level");
  - F-005 AC-18 (009 "Core part" and 012);
  - F-007 AC-06 (013 "book drawing part" and 014);
  - F-011 AC-07 and F-012 AC-09 (011 "text data" and 014).

  The split by layer is sensible, but no row says which task's QA settles each AC.
- Impact: QA may pass an AC on partial evidence (e.g. a FoldSession-only test for an AC written in `TakeSheet`/`NothingToUndo` game commands), or two QA runs may disagree about who owns it.
- Recommendation: when creating the task files, give each AC exactly one **owning** task and mark the others "supporting":
  - F-005 AC-01..05/10 → 010 (they are written as game commands); 009 adds engine-level parity tests;
  - F-005 AC-18 → 012;
  - F-007 AC-06, F-011 AC-07, F-012 AC-09 → 014.

### W-2 (Medium, mapping error) — F-008 AC-03 assigned to 011 instead of 010
- Location: ARCHITECTURE §13, ORIGAMI-011 row ("F-008 AC-03"), ORIGAMI-010 row ("F-008 AC-01~02·04~05").
- Problem: F-008 AC-03 (`BuyPage(dog)` on a new run → NotEnoughMoney, money 20) is a pure `OrigamiGame.BuyPage` rule, and that rule is implemented in 010 (C-150). 011 (Book/Texts/Screens) does not own this logic. The entry probably meant F-007 AC-03, which is already in the 011 row.
- Impact: 010 would be reviewed and QA'd without the NotEnoughMoney branch of `BuyPage` in its AC list, so a defect there would only surface in 011, outside the scope where it can be fixed.
- Recommendation: move F-008 AC-03 to ORIGAMI-010's acceptance list.

### W-3 (Low, design text vs scanline result) — J-05 displays 94%, not "95%"
- Location: ARCHITECTURE §9 table (J-05 shown as 0.950); design texts FEATURE_SPECS "공통 표기" (`Var(0.06)` = house ★2(95%)) and CONTENT_SPECS §4 J-05 "(95%)".
- Problem: the row-scan value is 0.94993, and `Grading.Percent` = floor(94.993 + 1e-9) = **94**. Rounding it to 0.950 in the table hides this. (The grid gave 0.9509 → 95.)
- Impact: the grade (★2) and the ±0.01 score are unaffected, so F-009 AC-01 and F-011 AC-03 still pass. A developer or QA who asserts the "(95%)" text, or a `RecognitionLine` string such as "집 ★2 (일치도 95%)", would get a failing test and might suspect the engine.
- Recommendation: add one line to C-110 or CODING_CONVENTION: "`Percent` values in the design's parentheses come from the 0.004 grid; tests assert the grade and the score ±0.01, never `Percent`, for J cases (J-05 = 94 under the row scan)." Optionally ask game-designer to write "94~95%" in the next design pass.

### N-1 (Low) — PlayMode folder namespace can shadow the `FoldTable` class
- Location: CODING_CONVENTION PlayMode folders `FoldTable/` (012). v1 precedent: `Origami.Tests.PlayMode.Flow`.
- Problem: following the folder = namespace convention gives `namespace Origami.Tests.PlayMode.FoldTable`. Inside it, the simple name `FoldTable` resolves to the namespace, so `AddComponent<FoldTable>()` fails with CS0118.
- Recommendation: tell 012 to use the namespace `Origami.Tests.PlayMode.FoldTables` (or `...FoldTableTests`), or a `using FoldTable = Origami.Runtime.Folding.FoldTable;` alias. This is cheap to state in the task file.

### N-2 (Low) — Camera acquisition order is unspecified
- Location: ARCHITECTURE §7 ("Bootstrap 카메라를 CameraRig가 원근으로 설정"), C-193 `CameraRig`.
- Problem: `Bootstrap.Init` and `OrigamiApp.EnsureInstance` are both `AfterSceneLoad` hooks, and Unity does not define their order across classes. `Camera.main` can be null when `OrigamiApp`/`CameraRig` awake.
- Recommendation: specify that `CameraRig` gets `Camera.main` lazily (on the first `Tick`), or creates a tagged camera when there is none, as Fold3DSpike `FoldSpikeController.SetupCamera` does. Bootstrap then skips creating its own.

### N-3 (Low) — wheel magnitude → layer notches not defined
- Location: C-191 `PointerFrame.Wheel` (float), "휠/[ ] → CycleLayers(±1)".
- Problem: `mouseScrollDelta.y` can be fractional or greater than 1 on high-resolution wheels and touchpads, and the contract does not say whether one frame gives ±1 by sign or `round(Wheel)` steps.
- Recommendation: state "per frame, `Wheel > 0` → +1, `< 0` → −1" (the simplest rule, and it matches F-005 AC-18's "two notches" when the test sends two frames).

### N-4 (Info) — `Materials` all-fallbacks-missing behaviour
- Location: C-190.
- Problem: with the CreatePrimitive fallback removed, the chain could end with no shader. `new Material(null)` throws.
- Recommendation: 012 should define a silent last resort (e.g. return the first non-null of the chain, else `Shader.Find("Hidden/InternalErrorShader")`) and test that `Materials.Lit/Unlit` return a material with `shader.isSupported`. Inclusion in a build stays a matter for the 014 unity-build and the user playtest.

### N-5 (Info) — ORIGAMI-014 is large
- It covers Core Room + Unity room/character/camera/HUD/overlays/counter + the v1 deletion + the full smoke test. That is acceptable, but if it fails review twice, split off "Core/Room + tests" (no dependency on 012/013) as a separate earlier task.

## Recommendation to the Orchestrator
Proceed to ORIGAMI-009. Apply W-1 and W-2 when you write the task files: give each AC one owning task and move F-008 AC-03 to 010.
Put W-3 and N-1..N-3 into the task notes for 009, 012 and 014, or schedule a small architect doc pass. None of these needs a re-review of this task.
