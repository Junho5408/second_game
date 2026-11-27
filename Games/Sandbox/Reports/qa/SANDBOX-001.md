# QA: SANDBOX-001 점수 카운터 기획 (spec task)

- QA: qa-engineer
- Commits verified: 32cf38a (docs), 804b0f9 (status), 5304945 (review report)
- Inputs: `Docs/design/{GAME_DESIGN,GAME_RULES,FEATURE_SPECS,CONTENT_SPECS}.md`, `Reports/review/SANDBOX-001.md`,
  downstream tasks `SANDBOX-002.json` (architect: interface contract) and `SANDBOX-003.json` (developer: "FEATURE_SPECS 수용 기준 전부를 Core 테스트로 검증")
- Verification criterion for a spec task: the spec must let SANDBOX-002/003 build and test F-001 **without guessing behavior**.
- **Result: FAIL -> FIX_REQUIRED** (2 High bugs: BUG-SANDBOX-001-1, BUG-SANDBOX-001-2)

## Commands run
| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Sandbox` | PASS (1/1; regression only, no code in this task) |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` | OK |
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-001` | lists `AGENTS.md`, `Games/Sandbox/Tasks/SANDBOX-004.json`, `studio/scripts/check_scope.py`, `studio/tests/test_check_scope.py` - all from Orchestrator commit 7f7bb35, not from this task's commits (32cf38a touches only `Docs/design/` + own task file) |
| `python3 -I model.py` (scratch reference model of R-001..R-006, not committed) | AC-01..AC-18 all consistent with the rules when queried at `now = t_prev` (19/19 checks PASS) |

## Acceptance criteria
| Criterion | Verdict | Evidence |
|---|---|---|
| 점수 증가·콤보·리셋 규칙이 수치와 함께 정의됨 | **FAIL** | Numbers are defined (BASE_POINTS=1, COMBO_WINDOW=1.0s inclusive, multiplier table, cap x4) and the arithmetic is correct (reference model). But the **reset rule is defined inconsistently across documents** (BUG-1): its interface/invalid-time behavior cannot be derived from the spec. |
| 엣지 케이스와 테스트 가능한 수용 기준 포함 | **FAIL** | 8 edge cases + 18 ACs, all internally consistent. But the NaN/±Inf **query** edge case is specified without a result (BUG-2), so it is neither implementable nor testable without guessing; several specified rules have no AC (BUG-4); AC query time is implicit (BUG-3). |

## Assessment of review warnings
| Warning | Forces SANDBOX-002/003 to guess behavior? | QA verdict |
|---|---|---|
| W-1 AC query time implicit | No. R-004 defines the reported state for every `now`; each AC's stated values hold only for `now` within the window, so the intended reading is recoverable. It does make test authors pick a query time. | BUG-SANDBOX-001-3, **Medium** |
| W-2 Does reset take a time? | **Yes.** FEATURE_SPECS and GAME_RULES contradict each other on the signature the architect must record; if reset takes a time, its NaN/backward behavior is undefined. | BUG-SANDBOX-001-1, **High** |
| W-3 Rules without AC / NaN query result | **Yes** for the NaN/±Inf query (return value unspecified). The `now < t_prev` query is fully specified, only untested. | BUG-SANDBOX-001-2 **High**; BUG-SANDBOX-001-4 **Low** |

## Bugs

### BUG-SANDBOX-001-1 - Reset time argument contradicts between documents
- Affected feature: F-001 reset (R-005), interface for SANDBOX-002
- Severity: **High** (blocking ambiguity for SANDBOX-002)
- Preconditions: architect writes the `ScoreCounter` contract in `INTERFACE_CONTRACTS.md` (SANDBOX-002 AC 1)
- Reproduction steps:
  1. Read FEATURE_SPECS.md line 19: "입력·**리셋**·상태 조회에 쓰이는 시각(초)은 **반드시 호출자가 명시적으로 전달**한다(R-001)."
  2. Read GAME_RULES.md R-001 line 26: "모든 **입력과 상태 조회**에는 시각 ... 이 인자로 주어진다" (reset not listed); R-005 uses no time.
  3. Read R-001 예외 상황 line 28: NaN/±Inf invalidates "그 입력/조회" only.
- Expected result: one unambiguous answer - either reset takes no time, or reset takes a time and its NaN/±Inf/`t < t_prev` behavior is defined.
- Actual result: `Reset()` violates FEATURE_SPECS ("반드시"); `Reset(double now)` complies but `Reset(NaN)` / `Reset(t < t_prev)` behavior (reset or ignore?) is undefined by any rule. The architect must guess.
- Evidence: quoted lines above (FEATURE_SPECS.md:19, GAME_RULES.md:26, :28, :48-51).
- Reproducibility: always (document inspection).
- Suggested fix: remove "리셋·" from FEATURE_SPECS.md:19 (reset is time-independent per R-005), or extend R-001 to reset with explicit invalid-time behavior and an AC.

### BUG-SANDBOX-001-2 - Result of a NaN/±Inf state query is unspecified
- Affected feature: F-001 state query (R-001, R-004)
- Severity: **High** (blocking ambiguity for SANDBOX-002 contract and SANDBOX-003 tests)
- Preconditions: counter has at least one valid input (e.g. t=0.0)
- Reproduction steps:
  1. Input at t=0.0.
  2. Query state at `now = NaN` (or `+Inf`, `-Inf`).
- Expected result: the spec states what the query returns (e.g. combo as of last input, or combo 0, or an exception).
- Actual result: R-001 says the query "무효 처리되고 상태는 변하지 않는다" - this describes mutation, but a query has no mutation and must return something. Plausible implementations differ:
  - `now - t_prev > 1.0` (NaN comparison false) -> reports combo 1 (not expired);
  - `!(now - t_prev <= 1.0)` -> reports combo 0 (expired);
  - for `+Inf`, R-004 literally gives combo 0, while "무효 처리" suggests ignoring it;
  - throwing `ArgumentException` is also compatible with "무효".
  No AC exercises it (AC-14 covers NaN/Inf **input** only).
- Evidence: GAME_RULES.md:28 and :44-46; FEATURE_SPECS.md:33, :52. Reference model (`now - tp > 1.0`) returns combo 1 for `q(NaN)`; the equally valid negated form returns 0.
- Reproducibility: always (document inspection).
- Suggested fix: define the return explicitly (reviewer's suggestion: "NaN/±무한대 시각 조회는 마지막 유효 입력 시점의 상태를 보고한다") and add an AC (e.g. AC-20).

### BUG-SANDBOX-001-3 - AC query time is implicit
- Affected feature: F-001 acceptance criteria AC-02..AC-10, AC-12..AC-16
- Severity: **Medium** (workaround: query at the last input time)
- Preconditions: writing tests for the ACs (SANDBOX-003)
- Reproduction steps: implement AC-04 (t=0.0, 1.0 inputs) and query at `now = 100`.
- Expected result: AC states the query time, so the assertion is unique.
- Actual result: the AC says only "콤보 2"; a query at now=100 correctly yields combo 0 per R-004.
- Evidence: reference model `run([0,1.0]).q(100) -> (score 2, combo 0, multiplier 1)`; `q(1.0) -> combo 2`.
- Reproducibility: always.
- Suggested fix: one line before the AC list: "별도 명시가 없으면 상태는 마지막 유효 입력 시각(now = t_prev)에 조회한다."

### BUG-SANDBOX-001-4 - Specified behaviors without an acceptance criterion
- Affected feature: F-001 R-004 exception, negative-time edge case
- Severity: **Low** (behavior is specified; only test coverage is missing)
- Preconditions: SANDBOX-003 tests only "FEATURE_SPECS 수용 기준 전부"
- Reproduction steps: search FEATURE_SPECS ACs for (a) a query with `now < t_prev` (R-004 예외 상황), (b) a negative finite input time (edge case "음수 시각도 유한값이면 유효"), (c) `t_prev` unchanged after a NaN input.
- Expected result: each specified rule has at least one AC.
- Actual result: none of (a)-(c) has an AC; they will not be required tests and may drift.
- Evidence: FEATURE_SPECS.md:29-37 (edge cases) vs :38-56 (ACs); GAME_RULES.md:46.
- Reproducibility: always.
- Suggested fix: add ACs, e.g. "t=2.0 입력 후 now=1.0 조회 시 콤보 1, 배수 1", "t=-1.0 입력은 유효하여 점수 1, 콤보 1", "t=0.0, NaN, 0.5 입력 후 콤보 2".

## Verified OK
- Arithmetic of AC-06..AC-10, AC-15 (reachable state 17/10) independently recomputed: correct.
- Boundary semantics consistent across documents: `<= 1.0` keeps combo (input), `> 1.0` expires (query), AC-11 boundary now=1.0 -> combo 1.
- Ordering "combo update, then score with new multiplier" consistent in GAME_DESIGN / GAME_RULES / FEATURE_SPECS.
- Backward-time input, simultaneous inputs, reset at initial state, reset then earlier time, multiplier cap: specified and covered by ACs.
- Scope: only `Docs/design/` changed by the task's commit.

## Tests added
None (spec task; no production code exists for F-001 yet).

## Untested areas
- No executable verification is possible until SANDBOX-003; the reference model is a scratch QA aid, not a deliverable.
- UI presentation (score/combo/multiplier on screen) belongs to SANDBOX-004; review N-1 (SANDBOX-004 AC lacked multiplier) appears addressed by commit 7f7bb35, not re-verified here.
- CONTENT_SPECS cosmetic ordering (review N-1) not treated as a bug.
