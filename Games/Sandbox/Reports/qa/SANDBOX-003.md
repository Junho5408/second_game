# QA: SANDBOX-003 점수 카운터 Core 구현 (ScoreCounter + ScoreRules.MultiplierForCombo)

- QA: qa-engineer
- Base commit: 303dce1 · tested commits: 24a9ad6 (implementation + tests), 97b0b8f (status), c5aef2b (review APPROVED_WITH_WARNINGS), bafc2c3 (decisions)
- Verification items in the task: `core-tests`, `review` (no `unity-*` items)
- **Result: PASS → COMPLETED** (0 bugs)

## Commands run

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Sandbox` (before QA tests) | PASS, 64/64 |
| `bash studio/scripts/verify_core.sh Sandbox` (with QA tests) | PASS, 87/87 (64 + 23 new) |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` (after gen_meta) | OK |
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-003` | OK |
| `grep -rn UnityEngine .../Core .../Tests/EditMode/Core` | no matches |
| Scratch mutation run (outside repo; QA test file alone against 5 mutants of `ScoreCounter.cs`) | every mutant killed (see below) |

## Acceptance criteria

Task criterion: "FEATURE_SPECS의 점수 카운터 수용 기준 전부를 Core 테스트로 검증". I read each F-001 AC alongside its test in
`ScoreCounterTests.cs`. Each test asserts the full `(score, combo, multiplier)` snapshot at the query time the spec gives,
or at `now = t_prev` where the spec gives none. I also checked the code against C-001.

| AC | Result | Evidence (test) |
|---|---|---|
| AC-01 new counter (0,0,1) | PASS | `AC01_NewCounter_...` |
| AC-02 one press → (1,1,1) | PASS | `AC02_...` |
| AC-03 0/0.5/1.0 → score 3 combo 3 | PASS | `AC03_...` |
| AC-04 exact 1.0 gap keeps combo | PASS | `AC04_...`; QA `ChainOfExactOneSecondGaps_NeverBreaks` (24 consecutive 1.0 gaps) |
| AC-05 1.001 breaks | PASS | `AC05_...` |
| AC-06 5th press +2, total 6, x2 | PASS | `AC06_...` (score 4 → 6); QA `LongRun_...` checks every per-press delta 1..60 |
| AC-07/08/09 17 / 48 / 68, x3/x4/x4 | PASS | `AC07..09_...`; QA `LongRun_...` |
| AC-10 combo 7 + 1.5 s → combo 1, +1 | PASS | `AC10_...`; QA `BreakAtEachTierBoundary_...` (break at combo 4,5,9,10,19,20,30) |
| AC-11 expiry only after > 1.0 | PASS | `AC11_...` |
| AC-12 backwards press ignored | PASS | `AC12_...`; QA `TinyBackwardsStep_IsRejected`, `BackwardsPressAfterExpiry_...` |
| AC-13 same time counts twice | PASS | `AC13_...`; QA `HundredPressesAtIdenticalTimestamp_...` (100 presses at t=3.0 → 368, combo 100, x4) |
| AC-14 NaN/±Inf press changes nothing | PASS | `AC14_...`, `AC14_N3_...` |
| AC-15 reset from (17,10) | PASS | `AC15_...` |
| AC-16 press after reset earlier is valid | PASS | `AC16_...`; QA `AlternatingResetAndPress_...` (50 cycles) |
| AC-17 reset on initial | PASS | `AC17_...` |
| AC-18 determinism | PASS | `AC18_...`; QA randomized differential test (4 seeds × 5000 ops) |
| AC-19 now < t_prev not expired | PASS | `AC19_...`; QA `QueriesFarInFutureAndPast_...` (now = −MaxValue) |
| AC-20 non-finite query → last valid state; new counter NaN → initial | PASS | `AC20_*` (3 values × 2 + no-mutation test) |
| AC-21 negative finite times valid | PASS | `AC21_...` |
| AC-22 invalid press between valid ones keeps t_prev | PASS | `AC22_...` (NaN, +Inf, −Inf) |

C-002 `MultiplierForCombo`: PASS (`ScoreRulesTests`: every tier edge, 0, negatives, int.Min/Max).
C-003 invariant (`Score>=0`, `Combo>=0`, `1<=Multiplier<=4`, `Combo==0 → x1`): PASS, checked on every random-test query.

## Tests added — `Unity/Assets/Game/Tests/EditMode/Core/Scoring/ScoreCounterQaTests.cs` (+ .meta), 23 cases

Expected values come from an independent closed-form score table written from GAME_RULES. They do not come from `ScoreRules`.

| Test | Scenario |
|---|---|
| `LongRun_EveryPressFrom1To60_DeltaAndTotalFollowTable` | 60 in-window presses: snapshot and per-press delta at every boundary 4/5, 9/10, 19/20 and beyond |
| `ChainOfExactOneSecondGaps_NeverBreaks` | 25 presses at integer seconds (every gap exactly 1.0) → (68,25,4); expiry at +1.0 vs +1.25 |
| `BreakAtEachTierBoundary_RestartsAtX1_AndRebuilds` ×7 | combo 4/5/9/10/19/20/30, break, rebuild 20: ladder restarts at x1, score accumulates |
| `HundredPressesAtIdenticalTimestamp_...` | 100 presses at one timestamp |
| `AlternatingResetAndPress_EachPressIsAFirstPress` | 50 × (press at jumping/backwards time, reset); far-future query after reset → Initial |
| `ResetMidCombo_DoesNotCarryComboOrScore` | reset at combo 12, rebuild 5 within the old window → (6,5,2) |
| `QueriesFarInFutureAndPast_...` | now = 1e300, MaxValue (expired), −MaxValue (not expired), +Inf; queries don't mutate |
| `ExtremeFiniteTimes_...` | press −MaxValue then MaxValue (gap overflows to +Inf → break, valid), same-time press at MaxValue, +Inf press rejected |
| `LargeButPreciseTimes_KeepCombo` | t ≈ 1e15 with 0.5 steps: combo/expiry still exact |
| `NegativeZeroAfterZero_...` | Press(0.0), Press(-0.0) → same time, combo 2 |
| `TinyBackwardsStep_IsRejected` | 1.0 then 1.0 − 1e-15 rejected |
| `BackwardsPressAfterExpiry_...` | backwards press is rejected even after the combo has expired; the next forward press breaks the combo |
| `ExpiryQueryAndNextPress_AgreeOnTheBoundary_ForDecimalTimes` | for t = −20.0..20.0 step 0.1: `GetState(t+1.0)` expiring ⇔ `Press(t+1.0)` breaking (W-1 consistency) |
| `RandomSequences_MatchIndependentModel_AndKeepInvariants` ×4 | seeded 5000-op mix of presses (gaps incl. 0, 1.0, backwards, NaN/±Inf), resets with clock rewind, and queries (incl. non-finite). Compares Press return values and snapshots against an independent model; checks the C-003 invariant and that score never decreases without a reset |

**How sensitive the tests are.** I copied the repo to a scratch folder and ran only the QA file against mutants of
`ScoreCounter.cs`:
- `<=` → `<` at the window: 6 failures
- expiry without the `IsFinite(now)` guard: 6
- backwards check disabled: 6
- `Reset` keeps `_hasLastPress`: 4
- combo capped at 25: 4

The unmodified code passes 23/23.

## Bugs

None found.

## Observations (not bugs)

- W-1 (from the review, and in DECISIONS 2026-11-27) is confirmed to be consistent: on the 1.0 s boundary, `Press` and
  `GetState` use the same double expression, so the display never contradicts the next press (QA test above).
- When timestamps are very large (≥ ~1e16), adding 0.5 s gives back the same double. The press then counts as same-time
  (gap 0). This is how double arithmetic works and it is outside the spec. `Time.unscaledTimeAsDouble` will never get near it.
- Score `int` overflow is not handled, as R-006/C-001 specify. At least ~5.4e8 presses would be needed, so I did not test it.
- The existing AC tests would not catch a combo counter capped above 25 (only AC-09 goes as far as 25). The new QA tests
  `HundredPressesAtIdenticalTimestamp` and `LongRun` now cover this.

## Untested areas

- Unity-side behaviour (C-004, SANDBOX-004): out of scope, and this task has no `unity-*` items. The new test file has no
  `UnityEngine` dependency. It sits under `Tests/EditMode/Core/` and will also compile in the Unity EditMode assembly, but
  Unity CI has not run it yet.
- Thread safety: not required by the contract.
