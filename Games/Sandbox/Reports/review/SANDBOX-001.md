# Review: SANDBOX-001 점수 카운터 기획 (documentation task)

- Reviewer: code-reviewer
- Base commit: 54d9699a459760d9afda6e88fd220d64781721cc
- Reviewed commits: 32cf38a (docs), 804b0f9 (status)
- **Result: APPROVED_WITH_WARNINGS**

## Changed files
- Games/Sandbox/Docs/design/GAME_DESIGN.md (+14)
- Games/Sandbox/Docs/design/GAME_RULES.md (+50/-1)
- Games/Sandbox/Docs/design/FEATURE_SPECS.md (+46)
- Games/Sandbox/Docs/design/CONTENT_SPECS.md (+2)
- Games/Sandbox/Tasks/SANDBOX-001.json (status/base_commit only)

## Commands run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-001` | OK (no out-of-scope files) |
| `bash studio/scripts/verify_core.sh Sandbox` | PASS (1/1 tests; regression check only, no code changed) |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` | OK |

## Checklist (adapted for a spec)
1. **Task acceptance criteria** — met.
   - "점수 증가·콤보·리셋 규칙이 수치와 함께 정의됨": BASE_POINTS=1, COMBO_WINDOW=1.0s (inclusive), multiplier table 1-4/5-9/10-19/20+ -> x1/x2/x3/x4, reset semantics (R-001..R-006).
   - "엣지 케이스와 테스트 가능한 수용 기준 포함": 8 edge cases, 18 numbered ACs with concrete inputs and expected values.
2. **Internal consistency GAME_RULES <-> FEATURE_SPECS <-> GAME_DESIGN** — consistent on window boundary (<= 1.0 keeps), multiplier table, combo-then-score ordering, reset, expiry (> 1.0), out-of-scope list. One minor mismatch, see W-2.
3. **Arithmetic of ACs** — independently recomputed: AC-06 = 1+1+1+1+2 = 6; AC-07 = 4 + 10 + 3 = 17; AC-08 = 4 + 10 + 30 + 4 = 48; AC-09 = 48 + 20 = 68; AC-15 state (17, combo 10) is reachable via AC-07. All correct. Test times (multiples of 0.5, 1.0, 1.001) are exactly or safely representable in binary floating point, so boundary ACs are not float-fragile.
4. **Invalid states** — backward time, NaN/±Inf, reset at initial state, reset followed by earlier timestamp, negative finite time, simultaneous inputs, multiplier cap: all covered.
5. **Scope fit** — only `Docs/design/` touched; feature is small, engine-free, and deterministic (R-001 explicit time), which maps cleanly onto SANDBOX-002/003 Core work.
6. Perf/memory, abstractions, breaking existing functionality — N/A (docs only; core tests still pass).

## Findings

### W-1 (warning) AC query time is implicit
- Location: Games/Sandbox/Docs/design/FEATURE_SPECS.md, F-001 수용 기준 AC-02..AC-10, AC-12, AC-13, AC-16
- Problem: R-001/R-004 say every state query takes an explicit `now`, and the reported combo depends on it (combo becomes 0 when `now - t_prev > 1.0`). Most ACs state "점수 n, 콤보 n" without saying at what `now` the state is read. A test that queries at, e.g., `now = 100` would legitimately see combo 0.
- Impact: Ambiguity for SANDBOX-003 test authors; two correct implementations/tests could disagree on what AC-04/AC-05 assert.
- Recommendation: Add one line before the AC list: "별도 명시가 없으면 상태는 마지막 유효 입력 시각(now = t_prev)에 조회한다." (or explicitly state that combo here means the internal, non-expired value).

### W-2 (warning) Does reset take a time argument?
- Location: FEATURE_SPECS.md F-001 요구사항 1 ("입력·리셋·상태 조회에 쓰이는 시각") vs GAME_RULES.md R-001 ("모든 입력과 상태 조회") and R-005 (reset is time-independent).
- Problem: FEATURE_SPECS implies reset receives a time; GAME_RULES does not, and no rule uses a reset time.
- Impact: The architect (SANDBOX-002) must guess the `Reset` signature; if it takes time, NaN/backward handling for reset is unspecified.
- Recommendation: Remove "리셋" from that sentence in FEATURE_SPECS (reset needs no time), or add reset to R-001 with its invalid-time behavior.

### W-3 (warning) Rules without an acceptance criterion
- Location: GAME_RULES.md R-004 예외 상황 (`now < t_prev` query reports last-input combo) and R-001 예외 상황 (NaN/±Inf **query**); FEATURE_SPECS AC-14 covers only NaN/Inf **input**.
- Problem: These behaviors are specified but no AC exercises them. R-001 also does not say what a NaN-time query *returns* ("무효 처리, 상태 불변" describes mutation, but a query has no mutation).
- Impact: SANDBOX-003 ("수용 기준 전부를 Core 테스트로 검증") will not be required to test them; behavior may drift.
- Recommendation: Add e.g. "AC-19: t=2.0 입력 후 now=1.0 조회 시 콤보 1, 배수 1." and "AC-20: NaN 시각 조회는 직전 상태(마지막 입력 시점 값)를 보고한다." (or whatever the designer intends). Optionally extend AC-14: after a NaN input, a following t=0.5 input after t=0.0 still yields combo 2 (t_prev unchanged).

### N-1 (nit / informational, not this task's scope)
- SANDBOX-004 acceptance criteria say only "현재 점수와 콤보가 화면 텍스트로 표시", while F-001 requires score, combo **and multiplier** on screen. Orchestrator may want to align SANDBOX-004 before it is assigned.
- CONTENT_SPECS.md: the new "해당 없음" note is placed above the "담당" header line; purely cosmetic.

## Verdict
The spec is complete, numerically correct, internally consistent and testable enough to start SANDBOX-002. The warnings are clarifications that should be folded in (ideally before SANDBOX-003 writes tests) but do not block design work.
