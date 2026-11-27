# QA ORIGAMI-003: Core rules (Grading, Pricing, Unlocking, EndingRules, OrigamiRun)

- QA: qa-engineer · Date: 2026-11-27
- Base: a3d2f63 · Task commits 122896b, 33183c1, f685214 · Review eb4790f (APPROVED)
- **Result: PASS → COMPLETED** (verification items `core-tests`, `review` only; no `unity-*` items)
- Defects found: **none** (0 Critical / 0 High / 0 Medium / 0 Low). Two test-suite gaps in the implementer's tests (Info, below) are now covered by the QA tests.

## Verification

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` (before QA tests) | PASS, 192 / 192 |
| `bash studio/scripts/verify_core.sh Origami` (with QA tests) | `== Origami: PASS ==`, Passed 217 / Failed 0 (192 existing + 25 new QA) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| `grep -rnE "UnityEngine\|DateTime\|Random\|Stopwatch" Core/` | no matches (R-001: no clock, no randomness) |
| `git diff a3d2f63 eb4790f -- Core/` public lines | no `public` signature added/removed; only the 5 implementation files changed; contract types and `Tests/EditMode/Core/Contracts/` untouched; `SurfaceContractTests` pass |
| Mutation checks (scratchpad copy, 28 mutants) | 28 / 28 killed by the QA tests; the differential test alone kills 28 / 28 (below) |
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-003 --role qa` | `OK` |

## Acceptance criteria (task)

| AC | Result | Evidence |
|---|---|---|
| AC-1 C-006..C-010 implemented, public API unchanged | PASS | Bodies of Grading/Pricing/Unlocking/EndingRules/OrigamiRun implemented; diff shows no public-surface change; SurfaceContractTests (reflection) pass. Every contract behaviour (reject orders, transitions, settlement fields, bankruptcy timing, ending order, argument exceptions) agrees with the independent reference model over 1,000,000 commands. |
| AC-2 F-001..F-006 ACs verified by Core tests (30 prices, F-006 AC-10 money 70/209/479/749/1019/1289/1709) | PASS | Implementer tests exist and pass for every AC: F-001 AC-01..10, F-002 AC-01..07, F-003 AC-01..15, F-004 AC-01..07, F-005 AC-01..07 (AC-04: 30 `TestCase`s in `PricingTests` + settlement test), F-006 AC-01..11 (AC-10 asserts the exact money sequence). I re-derived the expected values by hand / from the reference model; all match the spec. |
| AC-3 Rejected commands do not change the snapshot | PASS | Implementer `RejectionOrderTests`; QA differential test compares the full snapshot before/after every rejected command (all 8 reject reasons are produced; coverage gate below) — zero differences, and `Reason != None` for every rejection (review W-4). |

## Feature AC spot-check (F-001..F-006)

All 57 feature ACs are covered by named tests (`F00x_ACyy_*`) and pass. Independently confirmed through the reference model / QA scenarios: start state (R-002), day advance + attempt reset (R-003), R-004 reject order incl. `null`/`""`/case variants (`"Plane"`, `" frog"`) → UnknownModel, exact-money start, R-005 InvalidAction (values 0, 6, −1, 255, int.Min/Max) not counted as a mistake and checked after NoFold, R-006 hint once per step + reveal persists after mistake + cleared on next step, R-007 ruin at the 3rd mistake (incl. on the last step), R-009 grading, R-010 same-day unlock, R-011 prices, R-012 settlement order/popularity of the selling day, R-013 bankruptcy only at a new day (not after day 7), R-014 priority, R-015 RunEnded first for every command.

## Exploratory / abnormal testing

### Differential testing against an independent model (`Qa/RunReferenceModel.cs`, `Qa/RunDifferentialQaTests.cs`)
- `RunReferenceModel` re-implements R-001..R-015 from GAME_RULES/CONTENT_SPECS using only ints/strings with hand-copied tables — no Core type or constant — so a shared mistake cannot hide.
- 2,500 seeded streams × 400 commands = **1,000,000 commands** (own xorshift PRNG, identical under .NET and Mono). Profiles vary skill (correct-input rate 30–100 %), hints, abandons, early day ends, bogus IDs, undefined action values, wrong-state commands, mid-run `NewRun`, and model preference (greedy, crane lover, random/locked, plane only, rotate-all for E-04).
- After every command: accept/reason/outcome, completed piece, full settlement (day, every line incl. popularity/price, total, money after, end reason, next popular), full snapshot (incl. fold details, revealed action, penalty, expected grade) and invariants (money ≥ 0, attempts 0..5, day 1..7, mistakes ≤ 2 while folding, ending ⇔ ended, money = 20 − paper + sales).
- Coverage gate (asserted, so the comparison cannot become vacuous): commands 1,000,000; completed runs 3,515; bankrupt 507; endings E-01..E-05 all reached; max money 1709 (= documented theoretical maximum); max cranes 29; ruin on last attempt 3,163; ruin on last step 3,760; completion with 2 mistakes 11,823; start of a model unlocked the same day 12,716; money 0 during a day 14,665; NewRun mid-fold 217; all 8 reject reasons and all 10 outcomes.
- Result: **0 mismatches**. Runtime ≈ 4 s under dotnet.
- Also: two `OrigamiRun` instances fed the same 200 streams interleaved → identical results/snapshots (R-001, no shared static state); `EndingRules.Evaluate` vs model on > 4,000 boundary combinations (money 0/1/19/20/21/…/999/1000/1001, crane 0/1/14/15/16/29, all 32 ★3 patterns, ★1/★2 fillers, both reasons); `Pricing` vs exact floor for base 0..500 and all models × grades × days.

### Extreme scenarios (`Qa/ExtremeRunQaTests.cs`, hand-computed expectations)
| Scenario | Expected → Actual |
|---|---|
| Planes only, 5 ★3 per day for 7 days | money 60, 85, …, 210; E-05; frog never unlocked → PASS |
| Never fold, probe every command daily | all NoFold/Locked; 7/7 days; money 20; E-01 → PASS |
| Max cranes path | money 70, 179, 299, 419, 604, 724, 844; cranes 29; E-02 → PASS |
| E-02 beats E-03 in a real run | money 1414, cranes 15 → E-02 → PASS |
| E-04 in a real run | 65 → 163 → 217, all five ★3 → E-04 → PASS |
| 3rd boat on 5th attempt | `Start(frog)` → NoAttemptsLeft (proves immediate unlock); next day accepted → PASS |
| 2 boats, 0 attempts | `Start(frog)` → Locked (Locked before NoAttemptsLeft) → PASS |
| 3rd boat with 2 mistakes on last step | ★1, frog unlocked, startable same day → PASS |
| 3rd boat ruined on last step | 2 boats, frog locked → PASS |
| Ruin on last attempt | attempts 0, not folding, EndDay sells the 4 earlier boats (36) → PASS |
| EndDay with unfinished fold on day 7 with 0 attempts | FoldInProgress, fold progress/mistakes/hints/reveal unchanged; after finishing, RunFinished → PASS |
| Money 0 at end of day 7 | Completed (not Bankrupt), day 7, E-01 → PASS |
| Bankrupt at start of day 7 | settledDay 6, day 7, E-01; all commands RunEnded; NewRun accepted → PASS |
| New day with money 1 | not bankrupt, plane startable → PASS |
| Day-7 sale lifts money 19 → 25 | ending uses post-settlement money → E-05 → PASS |
| 1000 hints + 60 invalid actions on one step | hints 1, mistakes 0 → PASS |
| Hint on every dragon step | penalty 10, ★1, price 24 → PASS |
| Snapshot / settlement collections | read-only (`NotSupportedException`); older snapshot unchanged after settlement → PASS |

### Mutation checks (scratchpad copy only; production files never touched — `diff -r` identical afterwards)
28 single-point faults injected into the copied Core (scratchpad `mutate.py`); each run against the QA tests and the implementer's Rules/Run tests:

| Mutant | QA tests | Implementer tests |
|---|---|---|
| M01 Locked/NoAttempts order swapped · M02 `<=` on paper cost · M03 paper cost not summed · M04 hint counted every press · M05 reveal persists to next step · M06 mistake clears reveal · M07 ruin at 4 mistakes · M08 ruin not a failure · M09 next day's popularity · M11 attempts not reset · M12 ★2 max penalty 1 · M13 E-03 before E-02 · M14 E-01 at `< 20` · M15 unlock at `> 3` · M16 abandon keeps fold · M17 NewRun keeps today's pieces · M18 InvalidAction before NoFold · M19 total sales not summed · M20 two-stage rounding · M21 settlement lines reversed · M22 bankruptcy after day 7 · M24 invalid action counted as mistake · M25 E-04 ignores dragon · M26 NoAttempts at `< 0` · M27 grade ignores hints · M28 hint reveals the next step | killed | killed |
| **M10** bankrupt when money ≤ 1 (instead of < 1) | killed | **survived** |
| **M23** ending computed from pre-settlement money | killed | **survived** |

The differential test alone kills all 28 mutants.

## Bugs

None. No reproducible defect in ORIGAMI-003.

## Observations (Info, not defects)
- INFO-1: The implementer's suite did not pin the R-013 boundary "money 1 at a new day is not bankrupt" (M10) nor "the ending uses the money after the final settlement" (M23). Both are now pinned by `ExtremeRunQaTests.NewDayWithMoney1_IsNotBankrupt` and `Day7Settlement_LiftsMoneyAbove20_EndingUsesMoneyAfterSettlement` (and by the differential test).
- INFO-2: The differential test takes ≈ 4 s under dotnet; under Unity/Mono EditMode it will be slower (estimate 10–20 s). Acceptable; reduce `Streams` if CI time becomes an issue.

## Tests added (all under `Unity/Assets/Game/Tests/EditMode/Core/Qa/`, NUnit, no UnityEngine, + `.meta` via gen_meta.py)
- `RunReferenceModel.cs` — independent spec model (helper, no tests).
- `RunDifferentialQaTests.cs` — 4 tests: 1M-command differential + coverage gate, interleaved determinism, EndingRules boundary grid, Pricing floor/table.
- `ExtremeRunQaTests.cs` — 21 extreme/boundary scenario tests.

## Untested areas
- Unity compilation/execution of these tests (no `unity-*` items on this task; they will run in the Unity EditMode CI job of a later task). The files use only APIs available in netstandard2.1 and NUnit features present in Unity's test framework (classic `Assert.*`, `TestContext.WriteLine`).
- F-007 / OrigamiFlow / FlowTexts (ORIGAMI-004 scope) and Unity presentation (ORIGAMI-005) — out of this task.
- Arithmetic overflow is unreachable within 7 days (max money 1709) and was not tested.
