# Review: ORIGAMI-001 (종이접기 판매 게임 기획)

- Reviewer: code-reviewer
- Base commit: d31fef8 · reviewed commits: 95ae2a0, 86dbd96 (DECISIONS.md change a052142 is an orchestrator commit)
- Result: **APPROVED_WITH_WARNINGS**

## Verification run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-001` | OK |
| `bash studio/scripts/verify_core.sh Origami` | PASS (1 template test; no code in this task) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| Independent Python re-simulation of the rules (scratchpad, not committed) | all recomputed values match (see below) |

## Recomputed numbers (independent simulation of R-001..R-015)
- Price table (30 values, `floor(base*grade*demand/10000)`): all 30 match CONTENT_SPECS.
- F-006 AC-10 perfect run: money after each EndDay = 70, 209, 479, 749, 1019, 1289, 1709; finish, crane 3, no plane ★3 -> E-03. Matches.
  1709 is also the true maximum: 9 prerequisite completions (boat3/frog3/crane3) fit only in days 1-2 with 1 dragon left over, and dragon ★3 (net 54 / 84 popular) dominates every other attempt from day 3.
- Balance rows: dragons ★2 from day 3 -> 1159, ★1 -> 719; max cranes days 2-7 = 4 + 25 = 29. Match.
- F-001 AC-05/06/07/08, F-002 AC-06/07, F-003 AC-03..AC-13, F-004 AC-04, F-005 AC-01..AC-07: all reproduce exactly (for example AC-06 bankruptcy: day 5, money 0, failures 20, E-01; AC-07: sale 45, money 45, day 5).
- The shortest bankruptcy (4 days) is correct: any non-wasted completion earns more than its paper cost (boat ★1 = 3 vs cost 1), so 5 per day is the maximum net loss.

## Checklist
1. Spec/AC: all task ACs are met. The core loop (fold -> sell -> money -> ending) is in GAME_DESIGN. Fold controls, the 5 models with prices, sale rules, end conditions and ending branches all have numbers. Every feature F-001..F-007 has testable ACs, written as explicit command sequences.
2. Conventions: the rules are engine-free and deterministic (R-001: no clock, no random, fixed popular table). Inputs are explicit commands, so a Core-only implementation is straightforward.
3. Boundaries: F-007 is presentation only and makes no rule decisions. This fits the Core/Runtime split.
4. Invalid states: the rejection order is fully specified (R-004 with 6 reasons, EndDay, In/Hint/Abandon). It is consistent across R-004, R-015 and F-002 AC-04/AC-05 (FoldInProgress before Locked, Locked before NoAttemptsLeft). Money can never go negative. Day 7 ends without a bankruptcy check. Bankruptcy is checked only at the start of a new day. NewRun during a run discards state and no ending is judged.
5-6. N/A (spec). There is no duplicated rule text that conflicts, except W-1.
7. Core concept preserved (DECISIONS row 1): the player folds works, sells them, and the ending depends on both what was folded (E-02 cranes, E-04 all-★3) and the money (E-01, E-03).
8. Ending priority ties are covered: E-01 over E-02 (F-006 AC-09), E-02 over E-03 (AC-03/AC-07), E-03 over E-04 (AC-07). Boundary values 20/21, 999/1000, 14/15 are covered.

## Findings (warnings, none blocking)

### W-1 GAME_DESIGN contradicts R-013 on when bankruptcy happens
- Location: `Games/Origami/Docs/design/GAME_DESIGN.md`, 핵심 게임 루프 step 4: "도중에 돈이 0이 되어 종이를 살 수 없으면 즉시 파산으로 끝난다."
- Problem: R-013, F-001 (edge case and AC-07) and CONTENT_SPECS say that money reaching 0 during the day is **not** bankruptcy. Bankruptcy is checked only when a new day starts.
- Impact: someone who reads only GAME_DESIGN could implement an immediate bankruptcy. AC-07 would catch it, but the docs disagree.
- Fix: reword to "새 날이 시작될 때 돈이 0이면(종이를 살 수 없으면) 파산으로 끝난다 (R-013)."

### W-2 Some F-006 ending ACs leave inputs implicit
- Location: `FEATURE_SPECS.md` F-006 AC-04, AC-05, AC-06, AC-09.
- Problem: only AC-02 says "그 외 0". AC-05 "완주, 돈 1000" and AC-06 "5종 모두 ★3 ≥ 1" do not state the crane count. AC-06 needs at least 1 crane ★3 but fewer than 15 cranes, and AC-09 "파산, 학 15" does not state the money.
- Impact: a test writer has to guess. All reasonable guesses give the stated ending, so the risk is low.
- Fix: add one line to 공통 표기: "판정 입력에서 명시하지 않은 완성 수는 조건을 만족하는 최소값(나머지 0), 파산 시 돈 0".

### W-3 Out-of-domain inputs not specified
- Location: R-004 / R-005 / R-015.
- Problem: there is no rule for `Start(null)` (only the empty string is covered) or for an action value outside U/D/L/R/F (for example an undefined enum value). It is unclear whether these are rejected or count as a mistake.
- Impact: low. The architect can settle it in INTERFACE_CONTRACTS, but the rule should come from design.
- Fix: state "null ID -> UnknownModel; 정의되지 않은 동작 값은 거부(상태 불변, 실수 아님)" or leave it explicitly to the architect.

### Observation (no change required)
- E-03 has priority over E-04, so a player who folds everything ★3 and plays optimally gets 종이 재벌 rather than 명인. E-04 is reachable only if the player deliberately ends below 1000. This seems consistent with the recorded priority decision, but the trade-off is not written down anywhere. The Orchestrator may want to note it in DECISIONS.md.

## Conclusion
The numbers are internally consistent and were verified independently. The ACs are concrete and deterministic. The edge cases called out in the request (rejection order, unlocks, bankruptcy, day-7 end, ending ties) are all specified. The three warnings are documentation-clarity issues and can be fixed alongside or before ORIGAMI tech/impl tasks.

## Re-review (fix pass 1)

- Reviewed commit: d8828b9 (design fix). Also read: 724d0e8 (task status), 9fc4b3d (orchestrator DECISIONS rows, outside the designer's scope and not part of this review).
- Result: **APPROVED_WITH_WARNINGS** (two Low notes, neither blocks)

### Verification run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-001` | OK |
| `bash studio/scripts/verify_core.sh Origami` | PASS (1/1, no Core code in this task) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| `git diff 965045a d8828b9 -- Games/Origami/Docs/design` | only FEATURE_SPECS, GAME_DESIGN, GAME_RULES changed; CONTENT_SPECS untouched |

### Finding status
| Finding | Status | Evidence |
|---|---|---|
| BUG-1 / W-1 bankruptcy timing | Resolved | GAME_DESIGN step 4 now says that money 0 during the day is not bankruptcy, that the check happens when a new day starts (R-013), and that day 7 has no check. This matches R-013 and F-001 edge cases, AC-06 and AC-07. |
| BUG-2 / W-3 out-of-domain inputs | Resolved | R-004 #3 covers null and the empty string as UnknownModel. R-005 adds InvalidAction, which is not a mistake. R-015 adds InvalidAction to the reason list. F-002 AC-03 and F-003 AC-15 test both. |
| BUG-3 / W-2 implicit ending inputs | Resolved | FEATURE_SPECS 공통 표기 now has a rule (mentioned counts at their minimum, unmentioned counts 0, bankruptcy means money 0). Each F-006 AC re-derived under it: AC-04 E-03, AC-05 E-03/E-05, AC-06 (crane ★3 1 < 15, money 500) E-04, AC-07 E-03/E-02, AC-08 (crane 1) E-05, AC-09 E-01/E-01. Every result matches the stated ending. |
| BUG-4 cumulative records | Resolved | R-002 and F-001 AC-01 now list 총 판매액 0 and 총 종이값 0 |
| F-007 Esc confirm | Resolved | Enter or the button confirms. Esc again or the button cancels. Other inputs are ignored while the dialog is open. Covered by AC-08. |
| F-007 bankruptcy settlement | Resolved | On bankruptcy the screen shows the settlement and a bankruptcy notice, no next-day popular model, then the result screen. AC-09 matches F-001 AC-06 (day-4 sales 0, money 0, E-01). |

### Numbers
No existing number changed. Every removed line reappears with the same values. The new ACs reuse values already verified in the first review: F-002 AC-03 (money 20, attempts 5); F-003 AC-15 (boat progress 0/4, MISTAKE_LIMIT 3); F-007 AC-09 (F-001 AC-06: total 0, money 0, E-01).

### Rejection-order consistency (new InvalidAction reason)
- `In(x)`: R-005, R-015, the F-003 edge case and F-003 AC-15 all give RunEnded → NoFold → InvalidAction. Hint and Abandon stay RunEnded → NoFold, so splitting the old shared line in R-015 lost nothing.
- `Start(m)`: null is handled at step 3 of R-004, after RunEnded and FoldInProgress. F-002 AC-03 (`Start(boat)` then `Start(null)` gives FoldInProgress) matches that order.
- F-001 rule 4 (after the run ends, everything except NewRun gives RunEnded) still holds for both new inputs.
- InvalidAction cannot be produced through the UI, because keys and buttons map only to the 5 actions. The F-007 per-reason message rule still has nothing to contradict.

### New notes (Low, no fix required for this task)
- N-1 `GAME_RULES.md` line 39 (보조 동작) and the F-007 조작 line list the keys ↑/W … Esc but not **Enter**. Enter is now used to confirm abandoning. Impact: low, because F-007 defines Enter explicitly. Recommendation: add "포기 확인: Enter" to the GAME_RULES 보조 동작 line the next time the docs are touched.
- N-2 The last sentence of F-007 AC-08 ("확인 창이 떠 있을 때 ↑ 키는 …") follows the Enter step, after which the dialog is closed. Impact: a test writer must reopen the dialog first, which is obvious. Recommendation: reword to "확인 창을 다시 연 상태에서 ↑ 키 …" or move it to after the first Esc.

### Conclusion
All four QA bugs, the three review warnings and both F-007 UI notes are resolved. No number changed. The new InvalidAction reason has the same check order in every document. The two Low wording notes can be fixed later.
