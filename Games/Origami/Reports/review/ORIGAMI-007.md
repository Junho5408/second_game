# Review — ORIGAMI-007 (v2 redesign docs + balance_check.py)

- Reviewer: code-reviewer · Date: 2026-11-27 · Base: `2d96775` · Commits reviewed: `fc40063`, `cd4c773`
- Changed (task commits): `Docs/design/{GAME_DESIGN,GAME_RULES,FEATURE_SPECS,CONTENT_SPECS}.md`, `Docs/design/balance_check.py`, `Tasks/ORIGAMI-007.json` (status/result/notes/base_commit only). `Docs/DECISIONS.md` changed in orchestrator commit `734024a` (not part of this task).

## Result: **APPROVED_WITH_WARNINGS**

The v2 spec set is complete, internally consistent on nearly all numbers, and every user request recorded in DECISIONS is covered.
The recognition rule is precise enough to implement and test. All reference fold sequences work on the Fold3DSpike flat-fold engine.
Nothing here blocks the architect task. W-1 (the glider shortcut) and W-2 (whether this matches the user's "complex, realistic folding" wish) need an Orchestrator decision before the content and balance work. The user may need to confirm W-2.

## Verification run

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-007` | `OK` |
| `python3 Games/Origami/Docs/design/balance_check.py econ` | `경제 검증 OK` — 967 / 562 / 242, no-book ★3 437 (matches CONTENT_SPECS §9) |
| `python3 Games/Origami/Docs/design/balance_check.py` (full, ~7 min) | `판정 검증 OK` — 17/17 cases OK, 0 BAD; similarity matrix matches CONTENT §4 exactly (max off-diagonal 0.586); 7m04s |
| `bash studio/scripts/verify_core.sh Origami` | PASS (283 tests; v1 Core unchanged) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` (no Unity files touched) |
| Reviewer scripts (scratchpad; import balance_check functions) | glider shortcut, skipped-step cases, Flip/translation invariance, tulip-garden money; see findings |

Hand-checked against the spec text: every economy AC in FEATURE_SPECS. These are F-001 AC-05/06, F-004 AC-01..06, F-008 AC-05, F-011 AC-01..03, F-012 AC-01..07 and F-013 AC-02. All arithmetic is correct.
Each crease in CONTENT_SPECS §2 was recomputed from its grab/drop pair. The "접는 선이 지나는 점" values are right, including house x=2y, tulip 3x−y+2=0, dog-ear x+3y=0.5 (3.5 cm / 5 cm text) and dog chin x+y=−√2 (4.1 cm).
Book pagination was checked: 1+7+5+5+6+8 = 32, and F-007 AC-01/02/03 use the right page indices.
Room layout was checked: the gaps between interaction points are at least 1.5 m, and the F-002 AC-02 path stays clear.

## Checklist

1. **Spec / acceptance criteria**: met. AC-1 loop: GAME_DESIGN core loop + R-003/R-005/R-016. AC-2: R-008/F-006 tutorial, R-010/F-007 book, R-011..R-013/F-009 recognition with thresholds. AC-3: CONTENT §6 room, F-002/F-003 movement, catalogue of 5 flat-foldable models. AC-4: every F-xxx has numbered [Core]/[Unity] ACs with explicit command sequences and expected values.
2. **User requests (DECISIONS)**: character moves in 3D (F-002, fixed quarter-view, reasons given); sitting at the table opens drag folding (R-003, F-003 AC-06); guide only on the run's first sheet (R-008, F-005 AC-09, F-006); method only in the book (F-007, no on-paper hints after the tutorial); the player folds without choosing a model first (R-009/R-011..R-013); endings depend on folded works and money (E-02 tulips, E-03 all ★3, E-04 ≥600). Covered. See W-2 for the "complex, realistic" wish.
3. **Recognition rule**: precise and implementable. Outline = union of facets. Translate by area centroid, take the max over the D4 symmetries, floor(s·100+1e-9), grade at 95/90/85. Ties go to catalogue order. Candidates = pages owned. All J-cases are ≥0.018 from a grade boundary, so the ±0.01 tolerance cannot flip an expected outcome. The reference state is computed from the §2 sequence, not hard-coded.
4. **Engine achievability**: `balance_check.fold` is the same algorithm as `FoldPlanner.Plan`/`Split` (all-layer 180° reflection, eps 1e-9, min area 1e-7, moving side = grab side). Every grab point in §2 is a vertex or edge point of the state it acts on, so `TrySnapGrab` can pick it. The spike runtime does not limit the drop point (`FoldSpikeController.SnapDrop`), so off-paper drops work, e.g. dog ear (−1.3, 0.1). Flip uses the spike's semantics (mirror about the bounds' centre x, reverse layers); my check gave Flip and translate(+0.3, −0.2) ≥0.993 at a coarse grid (J-13, F-009 AC-03).
5. **Determinism**: R-001 explicit; recognition is a pure function (F-009 AC-06); popular-model table fixed; Runtime-only frame time.
6. **Phase boundaries**: clear (GAME_DESIGN 페이즈 table, "범위" list, CONTENT §7, F-xxx 범위 밖).
7. **Duplication / unnecessary abstraction**: none. balance_check.py is a design calculator in Docs/, not game code.
8. **Regression risk**: docs only; v1 Core and tests untouched (verify_core PASS). `Docs/tech/` still describes v1. That is expected, because the architect task comes next.

## Findings

### W-1 (High, balance/design) — Glider, the most expensive model, can be folded exactly in 3 folds instead of 6
- Location: `CONTENT_SPECS.md` §1 (glider: 6 folds, ★★★★, page 150, base 26) and §2 glider; `GAME_RULES.md` R-011 ("순서 무관, 같은 모양이면 같은 작품").
- Problem: R-011 compares outlines only. This sequence produces the glider reference outline exactly:
  `Fold((−1,0),(1,0))` (vertical half) → `Fold((0.5,1),(0.5,0))` (top down to y=0.5) → `Fold((1,0.5),(0,−0.5))` (corner on crease (0,0.5)–(1,−0.5)).
  Reviewer script result: glider **1.000** (all other models ≤0.58). The completed silhouette is printed on the book cover, so a player can work this out.
- Impact: the top-tier, 150-coin page becomes the easiest sheet in the game (3 folds, mostly at landmarks). Dog (4 folds, no shortcut found) becomes harder than glider. The ★★★★ difficulty label and the progression story stop matching what the player experiences, and the case for "complex folding" gets weaker (see W-2).
- Recommendation: game-designer should either (a) redesign glider, or any model priced above plane/house, so its outline cannot be reached with fewer folds, or (b) accept this as "real origami" and change price and difficulty to match the shortest known path. Also add a "shortest known sequence" column to CONTENT §1. Add a balance_check case that guards it, e.g. one that asserts the 3-fold sequence above scores below 0.85 after a redesign, or records it on purpose if accepted. Shortcuts I tried that did **not** reach 0.85: plane in 2 folds (0.52), plane skipping steps 3-4 (0.53), glider skipping steps 4-5 (0.68), one tulip petal (0.58), dog without ears (0.72).

### W-2 (High, product risk — Orchestrator/user) — Phase 1 is unlikely to feel like "complex, realistic folding"
- Location: `GAME_DESIGN.md` 페이즈 구분 / v1에서 바뀐 것; `CONTENT_SPECS.md` §1–§2, §7; DECISIONS 2026-11-27 rows 18 and 25.
- Problem: phase 1 has 5 models of 3–6 folds each. Every fold is an all-layer valley fold, and no model needs a mountain fold, layer selection, pre-creases, squash/petal/reverse folds or any 3D shaping. Recognition ignores layers, faces and mountain/valley (assumption 4), so `Shift` (mountain) and `Flip` never matter in phase 1. House, tulip and dog face are kindergarten-level models. Everything the user would recognise as complex origami (crane, boat, frog, i.e. T-1..T-7) is in phase 2, and phase 2 also needs a new recognition design (CONTENT §7 last line). DECISIONS row 18 also recorded "복잡한 기법은 안내된 동작" (complex techniques = guided actions). That conflicts with the v2 rule "guide only in the tutorial" for phase 2, and nobody has resolved it yet.
- Impact: the user asked again for realism and complexity after playing v1. A playable phase 1 of very simple models, where one model can be shortcut (W-1), risks another rejection of the concept after a full implementation cycle.
- Recommendation: Orchestrator should (1) tell the user plainly what phase 1 includes and that the crane etc. come in phase 2, and get confirmation before the implementation tasks start (this is close to the core concept); (2) consider moving T-1 (fold only the top layer(s)) and T-2 (crease marks / fold-and-unfold) into phase 1. They are cheap extensions of the spike engine and would allow more realistic models and visible landmarks (see W-3); (3) record how phase 2 complex techniques reconcile with "guide only at first".

### W-3 (Medium) — ★3 needs about 2 mm accuracy on drop points that have no visible landmark
- Location: `CONTENT_SPECS.md` §4 tolerance table and interpretation; §2 drop points.
- Problem: many reference drops are not vertices, so the 0.06 vertex magnet does not help: house steps 2-3 (−0.6, −0.8), tulip 2-3, dog 2-4 (two of them off the paper), plane 3-4 (0, k). Phase 1 has no crease marks. By the table, ★3 needs ≤2 mm error per fold on a 20 cm sheet (about 1 % of the sheet). The 967 / 600-tycoon economy assumes the player gets ★3 almost every time.
- Impact: tycoon (E-04) and master (E-03) may be out of reach in practice. That cannot be checked in CI; only a user playtest can show it.
- Recommendation: keep GAME_DESIGN assumption 8, and also add a playtest checkpoint (Unity AC, as in F-007 AC-07) for "★3 achievable on house and plane without the guide". Where possible, choose reference drops that land on a vertex or an existing edge ("edge to edge", as real diagrams do). Alternatively, show creases once T-2 exists.

### W-4 (Low, internal inconsistency) — Play-time estimate does not add up
- Location: `GAME_DESIGN.md` 타깃 경험 ("20~40분 (7일 × 하루 최대 5장, 1장 1~4분)"); `CONTENT_SPECS.md` §9 last row.
- Problem: 35 sheets × 1–4 min = 35–140 min, which is more than 20–40 min.
- Recommendation: either change the estimate (e.g. 40–90 min) or base it on expected use (fewer sheets per day, or 30–60 s per fold for 3–6-fold models).

### N-1 (Low) — Two numbers in the docs cannot be reproduced from balance_check.py
- Location: `CONTENT_SPECS.md` §9 "튤립만 ★1로 접어도 최종 193"; §4 random-error table ("작품별 10회, 시드 12345").
- Problem: the script has no code for either. My recomputation of the tulip-only ★1 run (buy tulip on day 2, plane ★1 on day 1) gives 197, or 192 with house ★1 on day 1. Neither is 193. The rest of the claim holds: 30 tulips are possible, and the result is not E-01.
- Recommendation: add both computations to balance_check.py, or label them as hand estimates and fix 193.

### N-2 (Low) — J-13 is not checked by the script
- Location: `CONTENT_SPECS.md` §4 J-13; `balance_check.py` `recognition()`.
- Problem: the task note says "17/17 reference cases". The Flip case (J-13) and the translate/rotate/mirror invariance cases (F-009 AC-03) are not in the script. They hold in my check (≥0.993 at grid 0.008).
- Recommendation: add them so the script covers every J-row the ACs cite.

### N-3 (Low, ambiguity for architect) — Book page model with two-page spreads
- Location: R-010, F-007 AC-04, command `TurnPage(쪽)`.
- Problem: `TurnPage` takes an absolute page number that is clamped (AC-04: −1→0, 40→31), but R-010 says pages are shown in pairs and talks about "이전/다음". The spec does not say whether the current page can be odd, or what a spread shows when the current page is 20 versus 31.
- Recommendation: state that "current page" is the left page of the spread, or that any page is allowed and the spread is page & ~1. Also state that ←/→ moves ±2 in the Runtime.

### N-4 (Low, UX) — Bed prompt shows at spawn
- Location: CONTENT §6: spawn (−2.0, 1.2), bed interaction point (−2.0, 2.0); F-013 first-walk hint.
- Problem: the spawn point is 0.8 m from the bed point, which is inside the 1.2 m range. So "E: 잠자기" appears on the first frame, right next to the hint "go to the table". Also, F-002 AC-04 (hold A for 5 s, x ≥ −3.75) starts in the bed's z-band, so the bed stops the character at x≈−2.2. The test passes without ever testing the west wall.
- Recommendation: move the spawn point or the bed interaction point (content-designer has ±0.3 m), and give AC-04 a starting z outside the furniture band, e.g. z = −1.0.

### N-5 (Info) — Testability notes for the architect
- F-009 AC-05 (fake catalogue with duplicate outlines) and AC-03 (translated state) need the Core recognition function to accept an injected catalogue and a raw paper state. The architect should make it a pure function over (state, owned list, catalogue).
- R-011 lets a grid-sampled implementation have error ≤0.005. The J-01 "(100%)" ACs still hold because the player's state and the reference are built from identical facets.

## Status
`task.py set Origami ORIGAMI-007 --status QA --result review=pass`

## Re-review (fix pass 1)

- Reviewer: code-reviewer · Date: 2026-11-27 · Commits reviewed: `eff5423` (docs + balance_check.py), `da1e491` (task file). Orchestrator commits `a119870` and `247852c` (DECISIONS) were read for context only.
- Result: **CHANGES_REQUESTED**

The user's choice (phase 1 adds layer-selective folds and crease marks) is now specified precisely and consistently, and all earlier findings are addressed.
R-007's moving-set rule, hinge closure and layer order match `balance_check.py`, and every new AC number I recomputed is correct except F-005 AC-11.
But the main goal of this fix pass is not met. The goal was that the paid models really need the new techniques and have no fewer-fold shortcut.
- The cicada's only "top 1 layer" step does not change the outline. Skipping it still gives cicada ★3 in 8 folds.
- An exhaustive skip-a-step check, which takes 4 seconds, finds shortcuts the beam search misses. They contradict CONTENT §1 and §4.2 for dog, cicada and plane.

These are small design and doc fixes. The architect can read R-007, R-021, F-005 and F-009 as they are.

### Verification run

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-007` | `OK` |
| `python3 Games/Origami/Docs/design/balance_check.py` (full) | 7m34s, exit 0. `경제 검증 OK` (967/562/242/437, tulip-only ★1 197, 44 pages, play time 38/61/90 min). `판정 검증 OK` (J-01..J-16 all OK, invariance 1.000, max off-diagonal 0.611, magnet lines unchanged for all 5). errors table identical to CONTENT §4.1. `지름길 검증 OK` (glider self-check 0.857) |
| `bash studio/scripts/verify_core.sh Origami` | PASS (283 tests, Core unchanged) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| Reviewer scripts (scratchpad, import balance_check) | `ac.py`: F-005 AC-01/03/04/10..16 recomputed. `layers.py`: technique-necessity variants. `subsets.py`: every subsequence of each book's folds. `nocrease.py`: error sim without crease steps. `deep.py`: all-layer beam to depth 6 for tulip/cicada |

### Earlier findings

| ID | Status | Evidence |
|---|---|---|
| W-1 glider shortcut | Resolved for the glider; partly recurs (R-1, R-2) | Glider replaced by cicada. The beam search finds the old glider shortcut (0.857), so it is a valid detector. Its range is too narrow, though: see R-2 |
| W-2 complexity / user confirmation | Resolved | User decision recorded in DECISIONS. The old "complex techniques = guided actions" row is retired, and CONTENT §7 agrees |
| W-3 ★3 accuracy | Resolved | Magnets (R-021), creases, a seeded error sim (§4.1, reproduced exactly), thresholds 97/93/85, and playtest AC F-007 AC-07 |
| W-4 play time | Resolved | 40–90 min, computed in `econ` (38/61/90) |
| N-1 | Resolved | `tulip_only_one_star()` = 197; error sim seeded `origami-<id>-<σ>` |
| N-2 | Resolved | J-13 (all 5) and invariance in `recog` |
| N-3 | Resolved | R-010 even-page spreads; `TurnPage` clamp + even; F-007 AC-04 values agree with page map |
| N-4 | Resolved | Spawn (−1.6, 0): bed 2.04 m, bookshelf 2.48 m; AC-04 on z = 0 (no furniture); AC-04b checks bed (x = −2.2) and bookshelf (z = 2.25) |
| N-5 | Resolved | F-009 `Recognize(state, owned, catalogue)`, injected catalogue, raw state |

### Requested consistency checks

- **R-007 moving set, hinge closure, layer order**: precise. The script `plan`/`fold` implements it word for word: seed = top n facets that contain grab; closure via a shared paper edge ≥ HINGE_MIN not on the fold line, or a higher overlapping grab-side part; non-members stay unsplit. Recomputed:
  - AC-12: diamond 6 facets, areas 1/1/0.5×4; top 2 moves 0.70 → 8 facets, outline 1.244; all layers 3.143 → 12 facets, 0.786.
  - AC-13: 1.804, identical facets.
  - AC-14: identical.
  - AC-15: layers 0,1 bottom / 2,3 other flap / 4,5 moved / 6,7 remainder.
  - Mountain + partial ("tucked under the lowest moving layer") is physically sensible for thin paper.
- **Crease vs Undo vs Unfold**: consistent across R-007, R-008, R-009 (crease-only → NoFolds), F-005 AC-05/AC-10, F-006 AC-07, GAME_DESIGN assumption 10 and F-013 HUD. One wording ambiguity: N-6.
- **R-021 magnets**: order P → L → M, constants in GAME_RULES and the script are the same, and the AC-16 values are reproduced exactly. The `recog` magnet line confirms that exact inputs keep the same fold line.
- **Recognition outline-only**: stated the same way in R-011, F-009 and GAME_DESIGN assumption 4.
- **Tutorial 6 steps**: R-008 (a < 6, Fold + Crease, Flip excluded), F-006 AC-01..03/07, CONTENT §2 plane, page map.
- **Cicada replaces glider**: complete. The only remaining "glider" mentions are change history and the detector self-check. E-03 list, F-012 AC-06/07 and R-015 example are updated.
- **Grades 97/93/85**: same in GAME_RULES, R-012, F-009 AC-04, GAME_DESIGN, script; J-04..06 / `Var` values and F-011 AC-03 updated.
- **Book**: 2 + 8 + 7 + 7 + 8 + 12 = 44. Cover pages are 2/10/17/24/32. F-007 AC-01..04 page indices match this, including plane page 6 = step 4 and tulip page 21 = step 4 (top 2 layers).

### Do the models really need the claimed techniques?

| Model | Claim | Finding |
|---|---|---|
| tulip | top-2-layer petals required | **Yes for ★2/★3.** J-14 (same steps, all layers) 0.696. "Top 1" also works (hinge pulls the partner layer), which R-007 allows. My all-layer beam to depth 6 reaches **0.848** at 6 folds. With finer drop points than the 0.25 grid, an all-layer ★1 tulip is likely (see R-3) |
| cicada | top 2 (wings), top 1 (head), mountain | Wings: **yes** (J-15 0.508). Head top 1 (step 7): **no**, see R-1. Mountain (9–10): valley gives 1.000 too. That follows from R-011 (mountain and valley are ignored), so mountain is shown in the book but is not a requirement |
| plane, house, dog, cicada | creases | Not needed for recognition, by design (R-011, J-16). Removing crease steps lowers ★3 % (seeded sim, σ 1 / 2 mm, with → without): plane 65→42 / 38→14, house 100→100 / 87→77, dog 97→95 / 54→42, cicada 69→46 / 22→10. Creases are a real accuracy aid, but house and dog barely depend on them |

### New findings

#### R-1 (High, design vs claim) — Cicada step 7 (the only top-1-layer fold) does not affect the outline; 8 folds give cicada ★3
- Location: `CONTENT_SPECS.md` §1 row cicada (technique "위 1겹(머리)", known shortest "4접기 0.812"), §2 cicada steps 7–8, §4.2 pass rule; `GAME_DESIGN.md` 진행 ("매미(전부 + 위 1겹 + 산접기)"); DECISIONS row "글라이더→매미(지름길 제거)".
- Problem: if the player skips step 7 and does step 8 with all layers, the result is cicada **1.000 ★3** in 8 folds. The book needs 9.
  - Step 7 folds the top layer's tip inward, to (−0.4, −0.4). Step 8 then trims the remaining layer at x + y = −1.7, and that trim alone sets the outline.
  - Skipping step 8 instead gives 0.923 (★1, 0.007 below the ★2 boundary).
  - Reproduce: `apply(REF["cicada"][:6] + REF["cicada"][7:])` → judge 1.000.
- Impact: the only "top 1 layer" use in the game is cosmetic. One of the two phase-1 techniques the user chose is weaker than the docs say. §1, §4.2 and GAME_DESIGN also contain a claim that is false. The economic gain (one fold saved) is negligible.
- Recommendation: game-designer redesigns the head so the top-1-layer fold changes the outline. For example, fold the top layer's tip outward past the body edge, or make step 8 not cover step 7. Otherwise remove "위 1겹" from the claims. Either way, add a J-case: "cicada without step 7" must be < 0.85, or recorded on purpose.

#### R-2 (Medium) — The shortcut guard misses trivial skip-step shortcuts; §1 / §4.2 numbers are wrong
- Location: `balance_check.py shortcut` (beam: grab on vertices or edge midpoints, drops on a 0.25 grid, depth ≤ 4); `CONTENT_SPECS.md` §1 "알려진 최단" column, §4.2 table and pass rule; GAME_DESIGN "지름길 방지".
- Problem: an exhaustive check over every subsequence of each book sequence (reviewer `subsets.py`, 4 s) finds:
  - **dog** without step 1 (no diagonal fold): 3 folds **0.867 → dog ★1** (paid, 50). §1 says "3접기 0.827 (인정 안 됨)".
  - **cicada** without steps 2 and 6: 7 folds **0.919 → ★1**. Without step 7: 8 folds 1.000 (R-1).
  - **plane** without step 2: 4 folds **1.000 → ★3**. §1 says "4접기로 0.903(★1)". Plane is free, so this is allowed, but the number is wrong.
  - Tulip and house: no skip-step shortcut (best 0.700 and 0.788).
- Impact: two of the three paid models break the rule "유료 작품은 책보다 적은 접기로 0.85 미만" (§4.2, GAME_DESIGN). The script still prints `지름길 검증 OK` because the beam never tries the book's own drop points with one step left out. Economic impact is small (★1 only), but the guard gives false confidence and the docs contain wrong data.
- Recommendation: add the exhaustive skip-step search to `shortcut` mode and enforce it. Correct §1 and §4.2. Then choose: (a) change dog or cicada so the skipped versions fall below 0.85, or (b) explicitly allow "★1 with fewer folds" for paid models and reword the rule, e.g. "책보다 적은 접기로 ★2 이상 불가".

#### R-3 (Low-Medium) — Results within 0.01 of the 0.85 boundary are not guarded
- Location: CONTENT §4 (J-case margin rule 0.015 applies only to listed J-cases); R-011 tolerance ±0.01.
- Problem:
  - Dog without the chin (skip step 6) = **0.849**, 0.001 below MATCH_MIN. An implementation that meets the allowed ±0.01 can turn it into dog ★1.
  - The tulip all-layer beam reaches **0.848** (depth 6, coarse drop grid), so the claim "위 2겹 필수" (§4.2, R-011) holds safely only for ★2 and above.
- Recommendation: apply the 0.015 margin rule to the skip-step results from R-2 as well. Either widen the margins by adjusting the models, or reword "필수" as "★2 이상에 필수". If dog without the chin must not be recognised, add it as a J-case with a margin.

#### R-4 (Low, AC error) — F-005 AC-11 facet count is wrong
- Location: `FEATURE_SPECS.md` F-005 AC-11 "… 조각 수 1·접은 횟수 1 그대로".
- Problem: after `Fold((−1,1),(−1,−1))` there are **2** facets (reviewer `ac.py`: 2 facets, 1 fold, marks (0,−1)–(0,0) and (0,1)–(0,0)). A test copied from this AC would fail.
- Recommendation: "조각 수 2·접은 횟수 1 그대로".

#### N-6 (Low) — `Unfold` rejection condition has two wordings
- Location: R-007 exceptions: "되돌릴 기록이 없으면 거부 … (`Unfold`는 조각 1·자국 0인 처음 상태에서 거부)".
- Problem: after a single `Flip`, the history is not empty, but the sheet has 1 facet and 0 marks. The two wordings give different answers.
- Recommendation: use one condition, e.g. "되돌리기 기록이 비어 있으면 거부". Note that a flip-only sheet can be unfolded.

#### N-7 (Low, cosmetic) — GAME_RULES 기본 수치 table is split
- Location: `GAME_RULES.md` 기본 수치. A blank line before `| MIN_DRAG |` leaves rows MIN_DRAG…LINE_PAIR_MIN without a header, so Markdown does not render them as a table.
- Recommendation: remove the blank line.

#### N-8 (Info, architect) — L-magnet tie-break
- R-021 L picks "the pair with the smallest distance sum". Ties are possible, for example with symmetric landmarks, and the order of the landmark list is not specified.
- Recommendation: architect fixes a deterministic order, e.g. by (sum, then lexicographic points), in the Core function.

### Checklist (delta)

1. Spec/AC: all four task ACs are still met. The user decision is fully specified, except that the "models need the techniques" claims are overstated (R-1..R-3).
2–3. Conventions/boundaries: docs and design script only, in scope.
4. Edge cases: R-007 rejections are complete (layers < 0, no paper at grab, short drag, line off paper).
5. Performance: full script 7.5 min (4 processes); `shortcut` is most of it. The skip-step check adds about 4 s.
6. Duplication: none.
7. Regression: v1 Core untouched (verify_core PASS).
8. Tests/verification: the script checks every J-case and invariant cited, but its shortcut guard is not exhaustive for the easiest class of shortcuts (R-2).

### Required for approval
R-1, R-2 (guard + corrected numbers + policy decision), R-4. R-3 and N-6/N-7 are recommended in the same pass.

### Status
`task.py set Origami ORIGAMI-007 --status FIX_REQUIRED --result review=fail`
