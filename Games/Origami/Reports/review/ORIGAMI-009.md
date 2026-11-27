# Review ORIGAMI-009 — v2 Core folding engine, magnets, recognition, catalogue

- Reviewer: code-reviewer · Branch: `claude/serene-fermat-4bwjlo` · Base: `aeca340` · Commits: `31ee77c`, `e6bb825` (impl), `d6ba7c6` (status)
- **Result: APPROVED_WITH_WARNINGS**

## Verification run by the reviewer

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-009` | `OK` |
| `bash studio/scripts/verify_core.sh Origami` | PASS, 469/469 (v1 tests included) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| `git diff --stat aeca340 HEAD` | only new files under the 7 scoped folders (+ .meta) and the task file. No v1 file changed |
| Independent parity harness (see below) | 900 random sequences, 2,684 states, 528 rejections, 5,400 snaps: 0 mismatches, max coordinate diff 8.9e-15 |

### Independent Python-parity check (beyond the committed PythonParityData)

I wrote my own generator and harness in the reviewer scratchpad (`rv9/gen.py`, `rv9/h/Program.cs`, `rv9/cmp.py`). They are not committed.
- The generator imports `balance_check.py` and calls `bc.act`, `bc.plan` (to get `partial`), `bc.snap`, `bc.landmarks`, `bc.mark_pieces` directly. It uses three seeds (7/11/23) × 300 sequences.
- Start states are Initial, the diamond base (tulip 1–3), cicada 1–5, dog 1–3, house 1–3 and plane 1–3. These are multi-layer states with creases, chosen to stress top-N/partial/mountain layering and mark-based landmarks.
- Layers were drawn from {0,1,2,3,4,5,9}, with 40 % mountain folds, creases and flips.
- Drops were targeted: about half are reflections of the grab across a random landmark pair (hits L) and the rest lie near mark pieces (hits M). Totals over 3 runs: L ≈ 1,500 and M ≈ 1,200 snaps. The committed data has only 16 L and 8 M.
- The C# harness compiles the Core sources and replays each case with `PlanFold`/`PlanCrease`/`Flip`/`DropSnap.Snap`. The comparison checks:
  - facet order, layer and FrontUp for each facet;
  - vertex count and coordinates (≤ 1e-9);
  - the crease list;
  - `Partial`;
  - error enum (incl. `NothingToFold`, which the committed random data never hits);
  - snap kind and point.

  Everything matched.

### Is `gen_parity.py` really derived from `balance_check.py`?

Yes.
- It imports the unmodified `balance_check` module.
- It produces the REF/RND states with `bc.act`, LM with `bc.landmarks`, MP with `bc.mark_pieces`, SN with `bc.snap` and errors with `bc.FoldError`.
- The only re-implementation is `line_unrounded`, a verbatim copy of `line_on_paper` without the final `_r6`. That is justified, because C-105 says the result is not rounded.
- Nothing in it reads C# output. The `.bak`/`DumpTmp.cs` files next to it are empty or unrelated backups, and the data file header names the seeds.

## Checklist

1. **Spec / AC**: all contract types C-101..C-108, C-110 and C-120 exist with the fixed public surface. Reflection surface tests pin members, enum values and the namespaces' type lists.
   - Owned ACs are covered by named tests: F-005 AC-06, AC-11..16; F-009 AC-01..06 (J-01..J-18 grades and ±0.01, matrix, invariance on raw states, tie, 100× bitwise); F-007 AC-01 (covers 2/10/17/24/32, 44 pages).
   - D-103 triples, CONTENT §1 table, §2 lines ±1e-4 and magnet kinds, and §5 popular days all match. I diffed all 32 §2 book texts and the 32 magnet letters against CONTENT_SPECS.md mechanically: identical.
2. **Conventions**: Core has no `UnityEngine`, uses `double` only, and has one public type per file with file name = type name. `Polygon` is internal.
   - Tolerances are named constants (`FoldPlanner.*`, `DropSnap.*`, `LineOnPaper.*`, `Grading.PercentEps`).
   - The `Lazy<T>` caches use the default thread-safe mode.
   - Static initialiser order in `ModelCatalog` is fine (K/X0/C are declared before `ModelArray`). `BookLayout` depends on `ModelCatalog` only one way.
3. **Boundaries**: Recognition → Folding/Geometry; Catalog → Folding/Recognition/Geometry. No v1 namespace is referenced. v1 `Origami.Core.Rules.Grading` and `Origami.Core.Content.Grade` coexist in other namespaces, and no file imports both.
4. **Null / invalid states**:
   - Every public entry throws `ArgumentNullException` on null.
   - The rejection order is InvalidLayers → DragTooShort → (NothingGrabbed) → NothingToFold → LineOutsidePaper (tested, incl. InvalidLayers with a zero drag).
   - Invalid plans have empty lists and a null `Result`.
5. **Performance** (Release, net8):
   - `DropSnap.Snap` takes about 0.19 ms per call on the cicada step-10 state (28 facets). It allocates small key arrays per candidate.
   - `Recognize` takes about 6–13 ms.
   - The first `ModelCatalog.Recognition` access takes about 0.35 s (40 outlines + JIT). See I-2.
6. **Abstractions**: there are no superfluous abstractions or duplicated engines. `Polygon` is shared by the planner, LineOnPaper and DropSnap.
7. **Regression risk**: additive only. v1 is untouched and all 283 pre-existing tests still pass.
8. **Tests**: meaningful, not tautological.
   - Expected values come from CONTENT/FEATURE tables or Python output, never from the C# itself.
   - W-6 / 008 W-3 are respected. No test asserts the dog-without-chin score (0.849/0.848) or any J percent value. The shortcut cases only assert `< 0.91`.
   - The `0.849` in `GradingTests` is the AC-04 synthetic threshold input (`FromScore(0.849)` → None). That is required by F-009 AC-04 and is not the dog case.

## Contract conformance details checked line by line against balance_check.py

- `_split`: `≤0` goes to the moving side and `≥0` to the staying side, with a strict sign change for intersections. The `<3` points or `|area| ≤ MIN_AREA` fallback sends the whole facet to the other side.
- `plan`:
  - The stack is a stable descending sort (LINQ `OrderByDescending`, so Python `sorted` tie order holds).
  - The closure loop iterates a growing `mv` list. Python iterates `list(mv)` of a set, but the result does not depend on order because adding a j is an "any" test.
  - `Partial = mv < grabSide`.
- `fold`: maxA/minA/maxM/minM, mountain+partial shift, three layer formulas, reversed reflected vertices, rank via SortedSet. Dictionary is used for lookup only, never enumerated.
- `crease`: same facet objects; marks follow facet then edge order.
- `flip`: axis = (min+max)/2 of world x, reversed order, `mx − layer`.
- `mark_pieces` / `visible` / `landmarks`: Cyrus–Beck with 1e-12/1e-9, span > 1e-6, identity check via ReferenceEquals, crossings with 1e-9 slack, 1e-6 dedup keeping the first.
- `snap`:
  - P key `(Key(d),Key(x),Key(y),x,y)`.
  - Zero-drag guard placed after P.
  - L pairs: stable order by key, `(Key(Σ),Key(a),Key(b),a,b)`, `_refl_ab`.
  - M key as specified.
  - Strict `Less` keeps the first minimum, like Python `min`.
- `line_on_paper`: [−10,10], CCW, `t1 > t0 + 1e-9`, a = min / b = max, unrounded.
- Outline (A2-3): rows `(k+0.5)h`, k range `floor(minY/h−0.5)..ceil(maxY/h−0.5)`, half-open edge rule, ≥2 crossings and `hi > lo`, `(lo,hi)` sort and merge, two-pointer intersection, centroid formulas. `Similarity.Centered` equals the catalogue score to within 1e-12 (tested).

## Findings

### W-1 (Warning, process) — the parity generator is not in the repository
- **Where**: `Tests/EditMode/Core/Folding/PythonParityData.cs` header ("regenerate from balance_check.py … gen_parity.py").
- **Problem**: the generator exists only in an ephemeral session scratchpad (`…/scratchpad/dev9/gen_parity.py`).
- **Impact**: if `balance_check.py` changes (ORIGAMI-007 has had two revisions already), nobody can regenerate the 180 KB data file. A future agent may hand-edit it or drop the test.
- **Recommendation**: the Orchestrator should keep the script in a durable place it owns or assigns. Examples are `Games/Origami/Docs/design/` (game-designer scope, next to `balance_check.py`) or a `studio/` tool. Record this in DECISIONS. No code change is needed in 009.

### W-2 (Warning, doc inconsistency) — CONTENT §2 house step 3 line end order
- **Where**: `Docs/design/CONTENT_SPECS.md` §2 house row 3 lists `(−1,−0.5)–(1,−0.5)`.
- **Problem**: `balance_check.py steps` (re-run by the reviewer) prints `(1.0, −0.5)–(−1.0, −0.5)`, and C-105 orders a = min and b = max parameter, which yields the Python order.
- **Developer handling (agreed)**:
  - `Content2_DiagramLinesMatchWithin1e4` compares CONTENT ends as an unordered pair.
  - `Content2_LineEndOrderFollowsPython` and `PythonParityTests.LineOnPaper_MatchesUnroundedPython` pin the ordered Python result.
  - Python is canonical per INTERFACE_CONTRACTS ("Python이 정본").
- **Impact**: none at runtime, because a diagram line is a segment. Only a doc reader could be confused.
- **Recommendation**: the Orchestrator should ask game-designer to swap the two ends in CONTENT §2 house 3, or add a note that "선" ends are unordered.

### I-1 (Info) — QA-2 and silent-contract choices: all acceptable
- `LineOnPaper.TryGet` returns false when `Distance(grab, drop) < 1e-9`. This matches the 009 task note and is tested (exact zero and 5e-10).
- `FoldLine.FromGrabDrop(g, g)` returns Normal (0,0). It cannot "return false", and the note's wording was loose. `PlanFold` rejects the same input with `DragTooShort` first (tested), and `Snap` returns before it builds L.
- Other choices the contract does not specify, all of them conservative argument checks or documented behaviour:
  - `PaperState` rejects a null facet (ArgumentException).
  - `DropSnap.IsVisible` throws ArgumentOutOfRange for a bad index.
  - `Outline.FromPolygons` throws on a null polygon and skips polygons with fewer than 2 points.
  - `RecognitionCatalog.Score(null id)` throws ArgumentNullException, which is an ArgumentException subtype and so still matches "모르는 ID → ArgumentException".
  - `Recognizer` ignores null owned IDs.
  - `FoldPlan.Invalid` echoes the inputs with `Line = default`.

### I-2 (Info, for 012/014) — first-use cost of `ModelCatalog.Recognition`
- **Cost**: about 0.35 s on first access (5 × 8 outlines, ~2,000–3,000 rows each, plus JIT).
- **Impact**: a hitch on the first `Finish` if it is first touched there.
- **Recommendation**: the Runtime bootstrap (014) should touch it once at load. No Core change is needed.

### I-3 (Info) — floating-point details
- `Vec2.Length` uses `Math.Sqrt(x²+y²)` where Python uses `math.hypot`/`math.dist`, so values can differ by about 1 ulp.
- `DropSnap.Key` uses `Math.Round(v, 9, ToEven)`, which is what the contract specifies. Python's `round` is correctly rounded in decimal, so the two could differ at an exact binary halfway point.
- Observed differences are ≤ 9e-15 across about 8,000 compared states and snaps. No decision (side clamp 1e-9, MIN_AREA, key ties) changed. No action.

### I-4 (Info) — Unity compile risk: low
- The 180 KB verbatim string (`PythonParityData.Text`, longest line 3,953 chars) is far below Roslyn/metadata limits (user-string heap 16 MB). It splits on `\n` and `Trim()`s, so CRLF checkouts are fine.
- The 10-element ValueTuple deconstruction (`Content1_ModelTable`), local functions and `TestCaseSource(nameof(...))` are all C# 7–9 features available in Unity.
- The NUnit APIs used (`Is.InRange`, `CollectionAssert.IsSupersetOf`, `TestContext.WriteLine`, …) all exist in Unity's NUnit 3.5.
- Every new folder and file has a `.meta`. The Core folders fall under `Origami.Core.asmdef` (`noEngineReferences: true`) and the tests under `Origami.Tests.EditMode.asmdef`. There are no MonoBehaviours, so file/class names only matter for the convention, and they match.
- The task's verification list has no `unity-*` item. Unity compilation of these files will first be proven by the CI run of a later task (012+).

### I-5 (Info) — surface tests do not pin default parameter values
- **Problem**: `Facet.Contains(…, eps = 1e-6)` and the `FoldStep.Fold/Crease` defaults are in the contract but are not reflected in `SurfaceAssert`.
- **Impact**: negligible. They are correct today.

## Verdict

**APPROVED_WITH_WARNINGS.**
- The engine, magnets, line-on-paper and recognition reproduce `balance_check.py`. Both the committed data and an independent, more adversarial harness confirm this.
- The catalogue matches CONTENT/DATA_STRUCTURE.
- The tests are meaningful and respect W-6 / W-3.
- W-1 and W-2 are Orchestrator/doc follow-ups and do not require changes to this task's code.
