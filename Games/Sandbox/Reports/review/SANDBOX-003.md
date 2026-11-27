# Review: SANDBOX-003 점수 카운터 Core 구현 (ScoreCounter + ScoreRules.MultiplierForCombo + tests)

- Reviewer: code-reviewer
- Base commit: 303dce1ccb8384878542323dbb32046ba9b34441
- Reviewed commits: 24a9ad6 (implementation + tests), 97b0b8f (status)
- **Result: APPROVED_WITH_WARNINGS**

## Changed files
- Games/Sandbox/Unity/Assets/Game/Core/Scoring/ScoreCounter.cs (+22/-3)
- Games/Sandbox/Unity/Assets/Game/Core/Scoring/ScoreRules.cs (+4/-1)
- Games/Sandbox/Unity/Assets/Game/Tests/EditMode/Core/Scoring.meta (new folder meta)
- Games/Sandbox/Unity/Assets/Game/Tests/EditMode/Core/Scoring/ScoreCounterTests.cs (+.meta, new, 252 lines)
- Games/Sandbox/Unity/Assets/Game/Tests/EditMode/Core/Scoring/ScoreRulesTests.cs (+.meta, new, 36 lines)
- Games/Sandbox/Tasks/SANDBOX-003.json (status/result/notes; base_commit set by task.py)

## Commands run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-003` | OK |
| `bash studio/scripts/verify_core.sh Sandbox` | PASS (64/64, 0 failed) |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` | OK |
| scratch `dotnet build` of `Core/**/*.cs` for `netstandard2.1`, LangVersion 9, TreatWarningsAsErrors (outside repo) | 0 warnings, 0 errors |
| `grep -rn UnityEngine Unity/Assets/Game/Core` | no matches |
| python float probe: `a/10 + 1.0 - a/10` for a=1..199 | 8 of 199 gaps evaluate to > 1.0 (e.g. 1.2 -> 2.2 = 1.0000000000000002); see W-1 |

## Checklist
1. **Spec / AC compliance** - met. Implementation follows C-001 line by line:
   - Press validity: `IsFinite(time)` first, then `time < _lastPress` rejection only when a previous press exists; invalid path returns `false` before any field write, so score/combo/t_prev are untouched (AC-12, AC-14, AC-22).
   - Ordering: `_combo` is updated first, then `_score += BasePoints * MultiplierForCombo(_combo)` uses the updated combo (expected behaviour 2). AC-06 test would fail if reversed (5th press would add +1, total 5).
   - Boundary: `time - _lastPress <= ComboWindowSeconds` and expiry `now - _lastPress > ComboWindowSeconds`, plain double, no epsilon, exactly as the contract's "경계" rule. Gap 0 keeps combo (AC-13).
   - GetState: pure (reads fields only, returns a new struct). No press -> `ScoreSnapshot.Initial` for any `now` including NaN. Expiry check guarded by `IsFinite(now)`, so +Inf/-Inf/NaN report last-press state (AC-20); `now < lastPress` is not expired (AC-19). Expired path returns `(score, 0, MultiplierForCombo(0)=1)`, satisfying the C-003 invariant.
   - Reset: zeroes score/combo, clears `_hasLastPress`; idempotent; next press is a first press at any finite time (AC-15..17).
   - Overflow edge: finite `time - lastPress` can only overflow to +Inf, which correctly counts as a break.
   - `MultiplierForCombo`: <=4 -> 1 (0 and negatives), 5..9 -> 2, 10..19 -> 3, >=20 -> MaxMultiplier. Constants unchanged and match GAME_RULES "기본 수치".
2. **Architecture / conventions** - Core has no UnityEngine, namespace `Sandbox.Core.Scoring`, no clock/random/static mutable state (determinism, AC-18). Rule numbers live only in `ScoreRules`. Public surface unchanged (ScoringContractTests from SANDBOX-002 still pass). The unused `using System;` was correctly removed.
3. **Dependencies / boundaries** - no new asmdef/package; tests live under `Tests/EditMode/Core/` and are picked up by both Core.Tests.csproj (recursive glob) and the existing `Sandbox.Tests.EditMode` asmdef.
4. **Null / invalid states** - NaN/+-Inf on Press and GetState handled; no reference-type state; `_lastPress` is guarded by `_hasLastPress` (not a sentinel value), so negative times (AC-21) and t=0 work.
5. **Performance / memory** - O(1), no allocations (struct snapshot).
6. **Abstractions / duplication** - none added; small private `IsFinite` helper is appropriate (`double.IsFinite` is also available in netstandard2.1, either is fine).
7. **Breakage** - only stubbed bodies replaced; no other code touched. All 16 pre-existing tests still pass.
8. **Tests** - every AC has a dedicated test that asserts what the AC states (checked individually):
   - AC-01..05 full snapshot asserts; AC-04 (exact 1.0) and AC-05 (1.001) both covered.
   - AC-06 asserts score 4 before and 6 after the 5th press (the "+2" delta), plus combo 5 / x2.
   - AC-07/08/09 assert 17/48/68 and multiplier 3/4/4 (also combo count). `Evenly()` uses `i * 0.5` (exact binary values), so no float noise.
   - AC-10 asserts the precondition (combo 7, x2, score 10) and the +1 delta (10 -> 11, combo 1).
   - AC-11 now=1.0 -> combo 1, now=1.5 -> (1,0,1). AC-19 now=1.0 after t=2.0 -> (1,1,1).
   - AC-12 asserts `Press(1.0)` returns false, state kept, then 2.5 -> combo 2.
   - AC-14 for NaN/+Inf/-Inf on a populated and a new counter (no phantom t_prev). AC-22 for all three non-finite values (spec names NaN and +Inf; -Inf is a bonus).
   - AC-15 asserts the precondition (17, 10) before Reset. AC-16, AC-17 (double Reset, no throw).
   - AC-18 replays a mixed sequence incl. backwards and NaN presses and compares at two query times.
   - AC-20 all three non-finite queries with DoesNotThrow + new-counter NaN case, plus an explicit "GetState does not mutate" test (queries at NaN, 100.0 (expired), +Inf, then the combo continues 5 -> 6).
   - AC-21 negative finite times.
   - ScoreRulesTests cover every threshold edge (4/5, 9/10, 19/20), 0, negatives, int.MinValue/MaxValue, and the 1..MaxMultiplier range.

## Findings

### W-1 (warning, informational - no change required in this task): exact-boundary comparison vs. real frame timestamps
- Location: `Core/Scoring/ScoreCounter.cs` `Press` (`time - _lastPress <= ScoreRules.ComboWindowSeconds`) and `GetState` (`now - _lastPress > ...`).
- Problem: the contract mandates a raw double comparison with no epsilon, and the implementation complies. However, decimal-looking timestamps are not exact in binary: for 8 of 199 tested pairs `t` and `t + 1.0` (e.g. 1.2 -> 2.2 gives 1.0000000000000002) the gap evaluates to > 1.0 and breaks the combo. The AC tests use exactly representable values (0.0, 1.0, 0.5 steps), so they pass deterministically.
- Impact: none in practice for gameplay (a human cannot hit a 1.0 s gap to 1e-16 s precision; `Time.unscaledTimeAsDouble` values are arbitrary), but a future test author writing e.g. `Press(1.2); Press(2.2)` expecting "boundary kept" would get a surprising failure.
- Recommendation: keep code as is (spec-compliant). Orchestrator may ask the architect to add one sentence to C-001 "경계" noting that the boundary guarantee applies to the computed double difference, and SANDBOX-004/QA tests should use exactly representable times for boundary cases.

### N-1 (note): AC-14 test does not by itself prove t_prev is unchanged
- Location: `ScoreCounterTests.AC14_NonFinitePress_ChangesNothing`.
- It checks score/combo after the invalid press at now=0.5 but does not follow up with a press to show t_prev did not move. This is covered by `AC22_InvalidPressBetweenValidOnes_DoesNotMoveTPrev` and `AC14_N3_...` (next press is a first press), so coverage is complete overall. No action needed.

No CHANGES_REQUESTED or BLOCKED findings.
