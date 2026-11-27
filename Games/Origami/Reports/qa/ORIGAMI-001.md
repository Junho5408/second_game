# QA: ORIGAMI-001 (종이접기 판매 게임 기획)

- QA: qa-engineer · Commit under test: 965045a (design commits 95ae2a0, 86dbd96; review 965045a)
- Standard applied (Orchestrator): can architect, developer and QA build Core/Unity screens and write tests **without guessing**?
  Any contradiction between documents, or behavior a test must assert but cannot derive, is a High bug.
- Result: **FIX_REQUIRED** (1 High bug: BUG-ORIGAMI-001-1)

## Verification run
| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | PASS (1 template test, no Core code in this task) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| Independent Python simulation of R-001..R-015 (scratchpad `sim.py`, not committed) | every numeric AC value reproduced (below) |

Values reproduced by the simulation: all 30 price-table values; F-001 AC-03/04/06/07/08; F-002 AC-06/07;
F-003 AC-03/04/06/08/09; F-005 AC-03 (18, 35); F-006 AC-10 (70, 209, 479, 749, 1019, 1289, 1709, finish, E-03).
Balance rows checked by hand: dragons ★2 from day 3 gives 1159, ★1 gives 719, and the maximum number of cranes is 29. All match CONTENT_SPECS.

## Acceptance criteria
| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | 핵심 루프(접기→판매→돈→엔딩) 정의 | **FAIL** (contradiction) | GAME_DESIGN 핵심 게임 루프 1-5 defines the loop, but step 4 contradicts R-013 on bankruptcy (BUG-1) |
| 2 | 접기 조작, 작품 종류·가격, 판매 규칙, 종료 조건, 엔딩 분기가 수치와 함께 정의 | PASS | GAME_RULES 기본 수치/R-004..R-014, CONTENT_SPECS tables; numbers verified by simulation. The end condition is fully defined only in R-013 (see BUG-1) |
| 3 | 각 기능에 테스트 가능한 수용 기준과 엣지 케이스 | PASS with issues | F-001..F-007 each have edge cases and command-sequence ACs; out-of-domain inputs (BUG-2) and implicit ending inputs (BUG-3) remain |

## Bugs

### BUG-ORIGAMI-001-1 — GAME_DESIGN contradicts R-013 on when bankruptcy happens (High)
- Affected feature: F-001 run progression / bankruptcy (R-013)
- Severity: **High**. The documents contradict each other on a run-ending rule. Under the Orchestrator's standard, this blocks completion. (The review rated it W-1, a warning.)
- Preconditions: none (documentation)
- Reproduction: `grep -n "파산" Games/Origami/Docs/design/GAME_DESIGN.md`
- Expected: all documents agree that bankruptcy is checked only when a new day starts (R-013, F-001 edge case and AC-07, DECISIONS row 5).
- Actual: GAME_DESIGN.md line 23: "도중에 돈이 0이 되어 종이를 살 수 없으면 즉시 파산으로 끝난다." R-013 says "낮 동안 돈이 0이 되어도 즉시 파산하지 않는다". Under GAME_DESIGN's wording, F-001 AC-07 (money 0 during day 4, then 45 after EndDay) would end the run with bankruptcy.
- Reproducibility: always
- Fix: reword step 4, for example "새 날이 시작될 때 돈이 0이면(종이를 살 수 없으면) 파산으로 끝난다 (R-013)."

### BUG-ORIGAMI-001-2 — Out-of-domain command inputs have no specified result/reason (Medium)
- Affected feature: F-002 / F-003 (R-004, R-005, R-015). This is the review's W-3.
- Preconditions: Core API receives `Start(null)` or an action value outside U/D/L/R/F (for example an undefined enum value).
- Expected: the spec states the result so that an edge-case test can assert it.
- Actual: R-015 implies "거부, 상태 불변", but no listed reason fits an invalid action. It is not stated whether such an input counts as a mistake (R-005 treats any non-matching action as a mistake). `Start(null)` is not mentioned; only `""` is.
- Why Medium, not High: no feature AC depends on it, and the architect can define it in INTERFACE_CONTRACTS. Fix it in the same pass because it is a one-line rule, for example "null ID → UnknownModel; 정의되지 않은 동작 값은 거부(InvalidAction 또는 아키텍트 정의), 실수 아님, 상태 불변".

### BUG-ORIGAMI-001-3 — Some F-006 ending ACs leave inputs implicit (Low)
- Affected feature: F-006 AC-04/05/06/09. This is the review's W-2.
- Actual: the crane count is not stated in AC-05 and AC-06 (AC-06 needs at least 1 crane ★3 and fewer than 15 cranes). Money is not stated in AC-09 "파산, 학 15".
- Why Low: a test writer can pick values from R-014 that give the stated ending, so the expected result is still derivable. Fix: add to 공통 표기 "명시하지 않은 완성 수는 조건을 만족하는 최소값(나머지 0), 파산 시 돈 0".

### BUG-ORIGAMI-001-4 — R-002 start state omits cumulative records (Low)
- Affected feature: F-001 AC-01 / F-005 AC-07 / F-006 AC-11
- Actual: R-002 lists the start state but not 총 판매액 or 총 종이값 (the cumulative records in F-005 and the result screen). A reset to 0 on `NewRun` follows only from "이전 런과 무관하게".
- Fix: add "총 판매액 0, 총 종이값 0" to R-002 and to F-001 AC-01.

### Minor UI notes (Low, no fix required for this task)
- F-007 Esc confirmation: the key or button that confirms or cancels is not specified.
- F-007 settlement screen on bankruptcy: it is not stated whether the next day's popular model is shown (7일차 제외 covers only day 7). It can be derived as "run ended → result screen".

## Tests added
None. This is a design-only task with no code under `Unity/Assets/Game/`, so there is nothing to test.

## Untested areas
- Unity and presentation behavior (F-007): this task has no implementation.
- Play-time target (10-20 minutes) and "첫 작품 1분 안에": these need playtesting and cannot be checked from documents.

## Decision
FIX_REQUIRED. game-designer fixes BUG-1 (required). BUG-2, BUG-3 and BUG-4 should be fixed in the same pass; each is a one-line edit.
