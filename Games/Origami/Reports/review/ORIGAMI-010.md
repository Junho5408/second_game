# Review ORIGAMI-010: v2 Core workshop rules, economy, tutorial

- Reviewer: code-reviewer
- Branch: claude/serene-fermat-4bwjlo
- Commits reviewed: c6fe01f, a3351fa (implementation), cf84148 (status). Diff base: c389e89..cf84148. The task's base_commit 7190c812 also reaches 5 earlier orchestrator/015 commits, so I reviewed only the task's own commits.
- **Result: APPROVED_WITH_WARNINGS**

## Verification run

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-010` | `OK` |
| `bash studio/scripts/verify_core.sh Origami` | PASS, 735/735 (netstandard2.1 / C# 9 build + dotnet test) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| `git diff --stat c389e89 cf84148` | 79 files. Only new files under Core/{Economy,Tutorial,Game}/ and Tests/EditMode/Core/{Economy,Tutorial,Game}/ (with .meta), plus the task file. Nothing under Folding/Recognition/Catalog/Geometry (ORIGAMI-009) or v1 Rules/Run/Content/Flow is touched. |
| Independent scratch simulation (C# console app that references `Core.Build`, driving `OrigamiGame` with `Ref`/`Night` commands. It does not use the developer's test helpers.) | All 5 scenarios match `balance_check.py` / CONTENT §9 (below) |

### Independent economy simulation (scratch, not committed)
Daily money after each settlement, from the game, compared with the `balance_check.py sim` log:

| Scenario | OrigamiGame | balance_check / spec | Ending |
|---|---|---|---|
| Best ★3 plan (dog day 2, cicada day 4) | 77,152,277,312,502,687,967 | 77,152,277,312,502,687,967 (CONTENT §9) | E-04 (expected E-04) |
| No books, all ★3 | 77,157,207,287,337,387,437 | `sim({},3)` = same, 437 | E-05 |
| Tulip only from day 2 (30 tulips, 627) | 77,137,262,342,422,547,627 | `sim({'tulip':2},only='tulip')` = same | E-02 (beats E-04: priority correct) |
| Master (all 5 models ★3 at least once) | ..., 900 | hand-checked day 4: 277−150−20−10+153 = 250 | E-03 |
| Shortest bankruptcy (all sheets wasted) | Bankrupt, day 4, money 0, failures 11 | CONTENT §9 / F-001 AC-05 | E-01 |

I also re-ran the F001_AC09 command sequence independently. It ends at money 161, E-05, which matches my hand calculation: day 3 sales house ★2 normal 8 + tulip ★3 popular 27 = 35, and 126 + 35 = 161.

## Checklist

1. **Spec / AC conformance: met.** Every owned AC has a test whose expected values come from FEATURE_SPECS/CONTENT, not from the code. Examples: money 12/2/0 and failures 11 (F-001 AC-05); 77, 27, 17, 152 (F-011 AC-01/02); lines (8,4,12) / 24 / 40 (F-011 AC-03/06); the 30 cells of the CONTENT §5 table written as literals; the popular-by-day table; E-01..E-05 cases AC-01..07; TurnPage −1→0, 17→16, 31→30, 43→42, 99→42; tulip purchase → money 0, book cost 20; dog → 23 after J-12 crumpled; tutorial score 75 for x = 0.125 offset (computed by hand: pos 0.5, angle 0). F-008 AC-03 (review W-2) is covered, including AlreadyOwned before NotEnoughMoney. F-006 AC-04 follows QA O-1 (Fold once, then Unfold).
2. **Contract conformance: met.** All public signatures in C-130/C-140/C-150/C-151 match. The surfaces are pinned by `EconomySurfaceTests`, `TutorialSurfaceTests` and `GameSurfaceTests` (enum values included). I compared every rejection order with the C-150 table line by line:
   - Interact: RunEnded → WrongMode → UnknownTarget → SheetOnTable (bed only).
   - CloseBook: Seated idempotent; Bookshelf → Walking.
   - TurnPage / BuyPage: "book visible" check; BuyPage then UnknownModel → AlreadyOwned → NotEnoughMoney (money = price is allowed).
   - TakeSheet: SheetOnTable → NoSheetsLeft → NotEnoughMoney only for a paid sheet.
   - Fold/Crease: NoSheet → InvalidFold, with layers<0 → InvalidLayers.
   - Undo/Unfold: CanUndo is the only condition (R-007 N-6).
   - Finish: NoFolds uses FoldCount, so crease-only or flip-only sheets are rejected.
   - SkipGuide: NoGuide also covers "no sheet" (A2-10).
   - All checks run before any mutation. A rejection never rebuilds the snapshot. The tests assert `AreSame` for this, as CODING_CONVENTION requires.
   - Settlement: sells in display order at the settled day's price, then day 7 ends the run (Completed, day stays 7). Otherwise day +1, 5 sheets, Walking, then bankruptcy if money < 2 (day stays at the new value, NextPopular null). EndingRules.Evaluate is called with the final money.
3. **Architecture / conventions: met.** No UnityEngine. Usings follow ARCHITECTURE §1.1: Economy → Catalog, Recognition; Tutorial → Folding, Catalog (+ Geometry); Game → all of these. There is no static mutable state (`PlaneGuide` uses `static readonly Lazy<T>`, which CODING_CONVENTION explicitly allows). Integer money arithmetic (multiply, then divide). Ordinal string comparisons. One public type per file. Lists are copied into `ReadOnlyCollection`.
4. **Null / invalid state: met.** null/""/unknown target or model IDs are rejections. Pure functions throw the documented Argument* exceptions. NaN/∞ coordinates are rejected (see W-1). `CommandsNeverThrow_OnOddInput` covers int.MaxValue layers and 1e300 coordinates.
5. **Performance: fine.** A snapshot is built once per accepted command; display, owned and score lists are copied (at most 5 to 6 items). `PaperState` is shared by reference because it is immutable.
6. **Abstractions / duplication: fine.** `StepScoring.Evaluate` is a line-for-line copy of Fold3DSpike `FoldScoring.Evaluate` plus KindMismatch, which is the porting ARCHITECTURE §10 asks for. Fold3DSpike itself is not referenced.
7. **Regression risk: low.** The task adds new folders only. 009 code and v1 code are unchanged.
8. **Tests: sufficient**, with the two weak spots in W-2.

## Developer assumptions

| # | Assumption | Assessment |
|---|---|---|
| 1 | NaN/∞ coordinates → InvalidFold + FoldError.NothingToFold | Acceptable and safer than passing them to the engine. It is not in the C-150 table, though. See W-1. |
| 2 | `Pricing.SalePrice(int base, …)` throws ArgumentOutOfRange for base < 0 | Acceptable. It follows the C-1xx exception policy (invalid arguments of a pure function → Argument* exceptions). It cannot be reached from the game, because catalogue prices are ≥ 0. |
| 3 | StepScoring kind mismatch: Score 0 and KindMismatch true, but the geometric fields are still filled | Matches C-140 ("kind ≠ target.Kind → Score 0, KindMismatch true"), and the fields are documented. Correct. |
| 4 | Percent asserted only for exact reference states (plane/house 100%); J-09 asserted through the score 0.728 ±0.01; no 0.849/0.848 dog-chin or J percent values | Correct. Follows W-6 / 008 W-3 and the task's acceptance criterion 3. |
| 5 | Angle tolerance 1e-5° for exact inputs | Correct. acos near 1 amplifies the rounding error to about sqrt(eps). The score itself is asserted exactly (100). |
| 6 | New snapshot object also for accepted no-op commands (OpenBook twice, CloseBook while closed) | Correct. CODING_CONVENTION says "스냅샷은 수락된 명령마다 새 객체" (a new snapshot object on every accepted command), and C-150 only forbids a new object on rejection. |

## Findings

### W-1 (Low, contract gap): NaN/∞ fold coordinates are an undocumented rejection
- Location: `Core/Game/OrigamiGame.cs` `FoldOrCrease` (the `IsFinite` check); test `F005FoldCommandTests.F005_AC03_NonFiniteCoordinatesAreRejectedNotThrown`.
- Problem: C-150 defines InvalidFold as "n < 0, or engine FoldError". The implementation adds a third cause (non-finite coordinates) and reports it as `FoldError.NothingToFold`. That value's player text is "접히는 부분이 없어요" (nothing folds), which does not describe the cause. The test also does not assert which FoldError is returned, so the documented behaviour is not pinned.
- Impact: Small. Runtime never produces NaN from the pointer. If Texts (011) maps FoldError to text, a NaN case would show a misleading message, and a later change to the FoldError value would go unnoticed.
- Recommendation: the Orchestrator should ask the architect to add one line to C-150 ("non-finite grab/drop → InvalidFold, FoldError = NothingToFold", or another value of their choice). The developer can then add `Assert.AreEqual(FoldError.NothingToFold, r.FoldError)` to the test. This does not block QA.

### W-2 (Low, test strength): two ending assertions take their expected value from the code
- Location: `Tests/EditMode/Core/Game/F001RunTests.cs` `F001_AC09_InvariantsAlongARichRun` (last line); `F012EndingGameTests.F012_EndingMatchesEndingRules_ForBankruptRun`.
- Problem: both compare `Snapshot.EndingId` with `EndingRules.Evaluate(...)` on the snapshot's own values. A wrong final money or wrong completion counts would still pass.
- Impact: Small. The owned ACs assert concrete endings elsewhere (F-001 AC-03/05 E-01, F-012 AC-01..08, R016 day-7 E-05), and the invariants are checked after every command.
- Recommendation: assert literals. My independent re-run of the AC09 sequence gives money 161 and ending "E-05"; the bankrupt case is "E-01", money 0. Any later 010 fix or the QA task can make this change.

### N-1 (Info): diff base
`base_commit` 7190c812 is older than the task branch point (c389e89). `git diff <base_commit>` therefore also shows ORIGAMI-015 and orchestrator commits. `check_scope.py` already reports OK, so no action is needed. The Orchestrator may want to set base_commit at the branch point for future tasks.

## Conclusion
The contracts, rejection orders, economy numbers, ending priority, tutorial flow (first sheet free and guided; guide only on that sheet; Undo/Unfold rewind it; SkipGuide is permanent per sheet; NoGuide without a sheet) and determinism are all correct. They are confirmed by the 735 passing tests and by an independent simulation that reproduces the balance_check economy exactly. W-1 and W-2 are low-severity follow-ups. → **QA**
