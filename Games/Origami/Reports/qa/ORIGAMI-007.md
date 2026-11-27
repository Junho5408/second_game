# QA — ORIGAMI-007 (v2 redesign: 3D room, character, book, free folding)

- QA: qa-engineer · Date: 2026-11-27 · Branch: `claude/serene-fermat-4bwjlo` · HEAD at QA: `ae4f67f`
- Scope under test: `Games/Origami/Docs/design/{GAME_DESIGN,GAME_RULES,FEATURE_SPECS,CONTENT_SPECS}.md`, `balance_check.py`
- Review input: `Reports/review/ORIGAMI-007.md` (fix pass 2: APPROVED_WITH_WARNINGS, W-5, W-6)
- Verification items in the task: `review` only. There are no `unity-*` items, so no CI is needed.

## Verdict: **PASS** → COMPLETED

All 4 acceptance criteria are met. I spot-checked the acceptance-criteria numbers against the design's own geometry engine (`balance_check.py`) and by hand arithmetic, and they all agree. I found no contradiction that would block a developer. I found 4 non-blocking observations (Low/Info) and carry over the reviewer's W-5 and W-6.

## Regression / verification runs

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | `== Origami: PASS ==`. 283 passed, 0 failed (v1 Core unchanged) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| `python3 Games/Origami/Docs/design/balance_check.py econ` | `경제 검증 OK`, 0.19 s. Results: 967 / 562 / 242, no-book 437, tulip-only ★1 197 (30 tulips), 44 pages with covers 2/10/17/24/32, play time 38/61/90 min. These match CONTENT §5 and §9 exactly |
| Full `balance_check.py` (not re-run, by instruction) | I read the reviewer's output `scratchpad/rv2/bc_out.txt`. Every J-01..J-18 line is `OK`, plus invariance, magnets and `판정 검증 OK` |
| QA script `scratchpad/qa7/ac_check.py` | Uses the balance_check engine to check the F-005/F-007/F-009/F-010 AC numbers (below) |
| QA script `scratchpad/qa7/j_check.py` | Recomputes J-04/05/06/13/16/17/18 and the §1 outline areas |

## Acceptance criteria

### AC-1 v2 core loop: move character → sit at table → 3D drag folding → (sell) → 7 days → ending — **PASS**
- GAME_DESIGN "핵심 게임 루프" steps 1–7 describe the loop.
- GAME_RULES covers each part: R-003 mode table (walk → `Interact(table)` → sit), R-006/R-007 (sheet, folding), R-009 (finish/display), R-016 (sleep = settlement + next day), R-017 (bankruptcy) and R-018 (endings).
- FEATURE_SPECS F-001..F-003 and F-010..F-012 turn this into command sequences.
- I checked the arithmetic of the loop ACs by hand:
  - F-001 AC-05: 12 → 12 → 2 → day 3 → 0 → bankruptcy on day 4, 11 failures. Correct.
  - F-001 AC-06: house ★3 sold normally (the day-3 popular model is tulip) = 12. Correct.
  - F-011 AC-01: 4 × 2 paper = 12, then 5 × 13 = 65 → 77. Correct.
  - F-011 AC-02: 77 − 50 − 10 = 17, then 5 × 27 (day-2 popular is house) → 152. Correct.
  - F-011 AC-03: 8 + 4 + 12 = 24 → 40. Correct.
  - F-008 AC-05: 77 − 2 − 2 − 50 = 23. Correct.
  - F-013 AC-02: (2, 77, 5, "집"). Correct.
  - F-012 AC-01..07: ending priority checked against R-018. Correct.

### AC-2 Tutorial (first fold only), book, rules and numbers for judging unguided results — **PASS**
- **Tutorial**: R-008 and F-006 cover it.
  - Only the run's first sheet gets the guide.
  - The guide shows while a < 6, where a = folds + creases. This matches plane's 6 actions.
  - Also covered: `SkipGuide`/NoGuide, Undo/Unfold rewinding the guide step, a free sheet even on day 2+, and the step score using the prototype `FoldScoring` formula.
- **Book**: R-010, F-007 and CONTENT §3 cover it.
  - 44 pages and even-page spreads; I checked the `TurnPage` clamping examples.
  - Unowned chapters are sealed; buying a page uses R-014 / F-008.
  - Page numbering: plane 2–9, house 10–16, tulip 17–23, dog 24–31, cicada 32–43. The section sizes are 8, 7, 7, 8 and 12 pages, which matches §2's action counts.
- **Judging**: R-011..R-013 and CONTENT §4 cover it.
  - Matching uses outline IoU with centroid alignment and the maximum over the 8 symmetries (D4).
  - Percent is `floor(s×100+1e-9)`. Thresholds are 97/93/85. Only owned pages are candidates, and ties go to catalogue order.
  - There are 18 reference cases with values, an invariance table and a cross-similarity matrix (max 0.611 < 0.85).
  - QA recomputation results:
    - Var(0.01/0.06/0.15) = 0.992 / 0.951 / 0.892 (doc 0.992 / 0.951 / 0.892)
    - J-13 flip = 0.997 / 1.000 / 0.999 / 1.000 / 0.994
    - J-16 = 1.000, J-17 = 1.000, J-18 = 0.782
    - J-09 / F-010 AC-03 = 0.728 (doc "72~73%")
    - J-12 = 0.477
    - Grade function at s = 0.97 / 0.969 / 0.93 / 0.929 / 0.85 / 0.849 → ★3/★2/★2/★1/★1/crumpled (F-009 AC-04)
  - Margins: every J value is ≥ 0.02 from a grade boundary, except J-05, which uses the documented 0.95 ± 0.002 exception. With the ±0.01 implementation tolerance, J-05 lies anywhere in 0.941–0.961, which is still ★2. So the J-table ACs stay stable under the allowed tolerance.

### AC-3 3D space (room, table, book, sales spot), character control and model list redefined for the v2 folding scope — **PASS**
- **Room**: CONTENT §6 gives the room (8 × 6 m) and furniture centres, sizes, interaction points and prompts.
  - Interaction points are ≥ 1.5 m apart (min 2.0 m, bookshelf–bed).
  - The spawn point (−1.6, 0) is outside every 1.2 m range (bed 2.04 m, bookshelf 2.48 m). Checked by hand.
- **Character and camera**: F-002 covers them.
  - Speed is 3.0 m/s. AC-04 expects x = −3.7 at the wall; nothing blocks the z = 0 row.
  - AC-04b blocking positions: bed east face −2.5 + 0.3 = −2.2, bookshelf south face 2.55 − 0.3 = 2.25. Both are consistent with §6 sizes.
  - Camera offset (0, 6.0, −5.5).
- **Model list**: plane, house, tulip, dog and cicada.
  - All of them use only phase-1 operations: all-layer folds, top-N-layer folds, crease, mountain/valley fold and flip.
  - Phase-2 techniques (T-3..T-8) and their candidate models are listed separately (CONTENT §7).
- **Folding-feature ACs**: I recomputed them with the design engine. All match:
  - F-005 AC-01: outline [−1,1] × [−1,0], facet area sum 4.0
  - F-005 AC-03: short drag and out-of-paper line are rejected
  - F-005 AC-10: crease (0,−1)–(0,1), 1 facet, 0 folds
  - F-005 AC-11: 2 crease segments, 2 facets, 1 fold
  - F-005 AC-12: diamond with 6 facets, layer areas 1/1/0.5×4, outline 1.0. layers=2 → 2 moved, area 0.70, 8 facets, outline 1.244. layers=0 → 3.143, 12 facets, 0.786
  - F-005 AC-13: layers=1 at the hinge gives the same result as layers=0 (outline 1.804)
  - F-005 AC-14: layers=9 gives the same result as layers=0
  - F-005 AC-15: mountain + top-2 layer order 4,5 (moved) / 6,7 (rest of flap) / 2,3 / 0,1
  - F-005 AC-16 magnets: M (0,0.2), P (0,0), L (−0.6,−0.8), free (−0.62,−0.78)
  - F-007 AC-02: pre-state 2 folds + 1 crease, line (−0.828427,−1)–(0,1)

### AC-4 Testable acceptance criteria for every feature — **PASS**
- All 13 features F-001..F-013 have numbered ACs: 100 in total, 80 [Core] and 20 [Unity].
- Each [Core] AC is a concrete command sequence with expected state values, numbers or rejection reasons. The common notation defines tolerances (paper 1e-6, area 1e-6, match ±0.01, world ±0.05 m / movement ±0.15 m).
- The [Unity] ACs have measurable positions, times and visibility.
- Three ACs are explicitly user playtests: F-005 AC-19, F-007 AC-07 and F-013 AC-03 (Korean font rendering). They are labelled as such, and each of those features also has objective ACs.
- W-6 check: no AC asserts the outcome of dog without the chin (0.849). The only 0.849 in FEATURE_SPECS is a direct grade-function input (F-009 AC-04), and that is well-defined.

## Observations (non-blocking, no bug filed)

| ID | Severity | Location | Observation | Suggestion |
|---|---|---|---|---|
| O-1 | Low | FEATURE_SPECS F-006 AC-04 | Wording: "`Unfold` 후에도 가이드 안 보임" right after `SkipGuide` on a fresh sheet. By R-007, that `Unfold` is rejected (NothingToUndo: the history is empty). The assertion holds either way, but the AC does not say whether a fold comes first | The test author should do one `Fold` before `Unfold`. Clarify in the next docs pass |
| O-2 | Low | GAME_DESIGN line 4 | Header says `GAME_RULES.md`(R-001~R-020), but R-021 (drop magnets) exists | Change to R-001~R-021 in the next docs pass |
| O-3 | Info | GAME_DESIGN 타깃 경험 / CONTENT §9 | The "fast" play time computes to 38 min, while the docs say "약 40~90분" | Wording only; "약" covers it |
| O-4 | Info | Docs/DECISIONS.md (Orchestrator-owned) | Earlier rows (grades 95/90, glider model) are superseded by later rows (97/93/85, cicada). The design docs use the current values consistently | The architect should take the latest DECISIONS rows; no change needed |
| W-5 | Low (reviewer) | GAME_DESIGN 판정 bullet | Cicada "★2 needs top-N" is worded more strongly than the evidence (R-011 is phrased correctly). No AC depends on it | As the reviewer recommends |
| W-6 | Info (reviewer) | CONTENT §4.2 | Dog without the chin = 0.849 depends on the boundary. Confirmed that no AC tests it | Future Core tests must not assert it |

No Critical, High or Medium defects were found.

## Untested areas
- Unity ACs: movement, camera, UI, rendering and playtests. These cannot be verified in a design task; they become verification items of later implementation tasks.
- The long `balance_check.py` modes (`recog` full, `errors`, `shortcut` beam) were not re-run, by instruction. I relied on the reviewer's 9m32s run output, and my own recomputations agree with it.
- R-021 magnet tie-break rules beyond the four F-005 AC-16 cases.
- The subjective "realistic, complex folding" goal (review W-2): user playtest only.

## Status
`task.py set Origami ORIGAMI-007 --status COMPLETED --result qa=pass`
