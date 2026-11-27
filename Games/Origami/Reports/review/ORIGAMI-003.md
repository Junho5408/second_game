# Review: ORIGAMI-003 — Core 규칙 구현

- Reviewer: code-reviewer
- Base commit: a3d2f63ee8117cf40e20380a8eda84ced2542e00 → HEAD f685214 (commits 122896b, 33183c1, f685214)
- **Result: APPROVED**

## Verification run

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-003` | OK |
| `bash studio/scripts/verify_core.sh Origami` | PASS — 192 passed, 0 failed (55 existing + 137 new) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| `grep DateTime/Random/Stopwatch/UnityEngine/static mutable` in Core/Rules, Core/Run | no hits |
| Test count by attribute | Rules 71 (Ending 11, Grading 19, Pricing 34, Unlocking 7), Run 66 — matches the corrected note |

## Scope / contract

- Changed Core files: only the bodies of `Grading.cs`, `Pricing.cs`, `Unlocking.cs`, `EndingRules.cs`, `OrigamiRun.cs`.
  All read-only contract files (RuleConstants, CompletionCounts, RunEndReason, CommandResult, CommandOutcome, CompletedPiece,
  SaleLine, SettlementResult, FoldSnapshot, RunSnapshot, RejectReason) are untouched in the diff. Only private fields/helpers
  were added; `SurfaceContractTests` still pass.
- Every `CommandResult.Rejected(...)` call uses a non-None reason; the test helper `AssertRejected` also asserts `Reason != None`
  and `Outcome == Rejected`, `Settlement == null`, plus a field-by-field snapshot comparison (W-4 satisfied).
- New test folders `Rules/`, `Run/` and every new file have `.meta`; namespaces `Origami.Tests.Rules` / `Origami.Tests.Run` and
  spec-ID test names follow CODING_CONVENTION.

## Checklist

1. **Spec / AC** — satisfied (details below).
2. **Conventions** — Core is engine-free, C# 9 / netstandard2.1, Ordinal comparisons, values from `RuleConstants`/`OrigamiCatalog`
   (no hard-coded model values; the only literal semantics, "cheapest unlocked paper", is computed from the catalog).
   Dependency direction Run → Rules → Content respected.
3. **Boundaries** — no out-of-scope changes.
4. **Null / invalid state** — null/""/unknown IDs → UnknownModel in `Start`; pure functions throw the documented exception types
   (null counts, invalid grade, reason None/undefined, day ∉ 1..7, negative penalty inputs). `Fold` indexes `Steps[_foldProgress]`
   only while folding (progress < N is guaranteed because completion clears the fold).
5. **Performance / memory** — trivial allocations per snapshot/settlement; no issue.
6. **Abstractions / duplication** — none superfluous. Settlement computes `popular` inline and calls `Pricing.SalePrice(base, grade, popular)`
   exactly as C-010 prescribes (the flag is needed for `SaleLine`).
7. **Regression risk** — none; previously skeleton methods only.
8. **Tests** — sufficient; each AC spot-checked below.

### Rejection order (all commands, recomputed against R-004/R-005/R-015/C-010)
- `Start`: RunEnded → FoldInProgress → UnknownModel → Locked → NoAttemptsLeft (`<= 0`) → NotEnoughMoney (`money < cost`, equality allowed). Matches.
  Tests cover each adjacent pair: folding+unknown/empty/locked → FoldInProgress; unknown/null + 0 attempts → UnknownModel;
  locked + 0 attempts → Locked; locked + money 1 → Locked; 0 attempts + money 0 → NoAttemptsLeft; frog with money 1 → NotEnoughMoney.
- `Fold`: RunEnded → NoFold → InvalidAction (`!FoldActions.IsValid`) — tested with 0, 6, 42, 99, -1, both not folding (NoFold) and after run end (RunEnded).
- `Hint`/`Abandon`: RunEnded → NoFold. `EndDay`: RunEnded → FoldInProgress. `NewRun`: always accepted (also after bankruptcy/completion).
- RunEnded is tested first for every command on both a completed and a bankrupt run.

### State changes on accept
- Start: money −cost, attempts −1, totalPaperCost +cost, fold reset (0/0/0, not revealed). No refund on ruin/abandon (AC-05/AC-11 assert money 19, attempts 4).
- Fold correct: progress +1, reveal cleared; completion grade = `GradeFor(Penalty(m, h))`, piece appended to today, `Increment`, fold cleared; money unchanged until settlement (F-003 AC-01 asserts 19 and TotalSales 0).
- Fold wrong: mistakes +1, reveal kept (F-003 AC-10); at 3 → failure +1, fold cleared, no piece/completion (AC-05, AC-06 at progress 3).
- Hint: counted once per step via `_foldRevealed` (AC-07: 1 → 1 → after correct step not revealed → 2).
- Unlocks derived from counts (no stored set), so same-day unlock (F-004 AC-01) and NewRun re-lock (AC-07) follow automatically.

### Settlement / pricing (recomputed)
- Formula `base × GradePct × DemandPct / 10000`, integer floor. All 30 table values tested both via base price and via (model, day)
  where the day is chosen so that popularity matches. Recomputed sample: crane ★1 popular 18×60×150/10000 = 16.2 → 16; boat ★1 popular 6×60×150/10000 = 5.4 → 5. Matches table.
- Popularity is the settled day's model (F-005 AC-06: day-1 boat sells 9, not 13).
- F-006 AC-10 recomputed: D1 20 − 7 + (3×9 + 2×15) = 70; D2 70 − 17 + (15 + 3×27 + 60) = 209; D3..D6 +300 − 30 = +270 → 479/749/1019/1289; D7 dragon popular 5×90 − 30 = +420 → 1709. Test asserts exactly this sequence, RunFinished, E-03, plane ★3 = 0, crane = 3.
- F-001 AC-07: day 4 boat popular 13×3 + plane normal 6 = 45 (asserted line-by-line). F-005 AC-03: 9 + 6 + 3 = 18, money 35. AC-07: sales 9, paper 2, money 27.

### Bankruptcy / end of run
- Checked only after `day + 1` on days 1..6 using cheapest unlocked paper cost; day 7 sets Completed without the check.
- Tests: AC-06 (bankrupt at new day 5, money 0, failures 20, E-01, settlement total 0, NextPopular null); money 0 during the day is not bankruptcy
  (AC-07, `R013_MoneyZeroDuringDay_CommandsStillAccepted`, evening sale rescue); money 0 on day 7 → Completed, not Bankrupt (`R013_..._NotAfterDay7`).
- Day stays 7 after completion and the new day after bankruptcy; further commands → RunEnded; ending fixed once.

### Ending evaluation
- Order E-01 (Bankrupt || money ≤ 20) → E-02 (crane total ≥ 15) → E-03 (money ≥ 1000) → E-04 (all 5 catalog models ★3 ≥ 1) → E-05. Matches R-014/C-009.
- F-006 AC-01..09 tested with the FEATURE_SPECS defaults (minimum counts, others 0); boundaries 20/21, 999/1000, 14/15 covered; AC-08 (dragon only ★2) → E-05; extra: Bankrupt with money 5000 → E-01.

### Determinism
- No clock/random/static mutable state; F-001 AC-10 compares two runs with the AC-07 sequence field-by-field, plus reused-run-after-NewRun vs fresh run.

## Findings

None blocking. Informational only (no action required):

- I-1 `Core/Rules/Pricing.cs` `SalePrice(int basePrice, …)`: negative `basePrice` is not validated. C-007 only states that values are ≥ 0 and does not
  require an exception, and all callers pass catalog values, so this is acceptable as is.
- I-2 `Core/Run/OrigamiRun.cs` `CheapestUnlockedPaperCost()` would return `int.MaxValue` if no model were unlocked; unreachable because plane and boat are
  initially unlocked per the catalog. Acceptable.
