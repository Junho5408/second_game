# QA: ORIGAMI-011 — v2 Core 책 도해·문구·화면 흐름

- QA: qa-engineer · branch claude/serene-fermat-4bwjlo · tested HEAD ea1f100 (impl 5381350, dfa1ffa, 3313784)
- Inputs: task file, review report (APPROVED_WITH_WARNINGS), FEATURE_SPECS F-003/F-006/F-007/F-010/F-011/F-012/F-013,
  GAME_RULES R-010/R-012, CONTENT_SPECS §1/§2/§3/§5/§8, DATA_STRUCTURE D-103/D-110/D-112, INTERFACE_CONTRACTS C-160/C-170/C-171,
  DECISIONS 2026-11-27 (NoSheet silent, line formats, fixed book/dialog texts → 013/014)
- Verification items: core-tests, review (no `unity-*` items)

## Result: **PASS**

No defects found. All owned acceptance criteria pass black-box tests that QA wrote from the specs.

## Commands

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | PASS: 972 passed, 0 failed. That is 921 existing tests plus 51 new QA tests. |
| `dotnet test … --filter BookPagesV2SpecQaTests / TextsV2SpecQaTests / ScreenFlowV2SpecQaTests` | 7 / 32 / 12 passed |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-011 --role qa` | `OK` |

## Acceptance criteria

| # | Criterion | Result | Evidence (QA tests) |
|---|---|---|---|
| 1 | Book/Texts/ScreenFlow contracts implemented | PASS | All public members of C-160/C-170/C-171 are exercised by the tests below. The review's reflection tests pin the signatures. |
| 2a | F-007 AC-02 (page 6 diagram, page 3 crease, page 21 top 2 layers) | PASS | `BookPagesV2SpecQaTests.F007_AC02_Page6_Plane4_ExactValues`. Before-folds 2, before-creases 1, line (0,1)–(−0.8284,−1) in either order, grab (−1,0), drop (0,−0.4142), valley fold, all layers, "4 / 6", and the CONTENT §2 sentence verbatim. |
| 2b | F-007 AC-03 (sealing, cover, buy tulip, page 1 always visible) | PASS | `F007_AC03_SealingFollowsTheGame_NewRunThenBuyTulip`. This uses the real `OrigamiGame` ownership list. |
| 2c | F-007 AC-01 / R-010 / D-112: all 44 pages × all ownership combinations | PASS | `AllPages_AllOwnershipCombos_MatchLayoutSealingAndPayloads`: 8 combinations × 44 pages = 352 page checks. Each checks kind, model, sealed flag, step number/total/label, whether a diagram exists, and the cover payload (name, base and page price, difficulty, action count, rotation 0/45, owned/buyable, silhouette equal to the QA-replayed reference state). It also checks the complete-page shape and the contents entries (order, name, owned, page price, cover page). |
| 2d | Diagrams vs CONTENT §2 and D-103 (all 32 steps) | PASS | `EveryStepDiagram_MatchesContentSection2_AndD103`. Each step is checked for kind, grab, drop, mountain flag and layers. Before/After are compared with the QA replay of V2SpecBook (1e-6). Fold and crease counts are checked, as are the D-103 facet counts before and after each step and the crease segments before it. The line is checked against the "선" column (±1e-4, either order). The magnet is checked against the P/L/M/− column. Anchors: 1 for P (equal to the drop point), 2 for L (on the fold line), 0 otherwise. The text must match the "책 설명" column exactly. |
| 2e | F-013 AC-01 reject → text table | PASS | `TextsV2SpecQaTests.F013_AC01_RejectText`: 12 reasons × every FoldError. `InvalidFold` maps to `FoldTexts` and includes "잡은 곳에 종이가 없어요". The texts were also checked on real rejections from the game. |
| 2f | F-013 AC-02 HUD (2, 77, 5, "집") | PASS | `F013_AC02_HudAfterF011AC01` checks all four lines exactly. Also checked: the "접은 횟수 N · 자국 N" line appears only when seated with a sheet (not when walking with the sheet left on the table), and the popular-model name for each of days 1–7 matches CONTENT §5. |
| 2g | F-003 mode × key table (Core) and priority | PASS | `ScreenFlowV2SpecQaTests`. QA re-derived the cells from C-171 and F-003 and sampled them in every situation. Priority pairs tested: Esc>E, Enter>E (in walking this does nothing), Esc>Enter (seated and settlement do nothing; counter and sleep-confirm act on Esc), Enter>Space, Space>X, X>Z, Z>R>F, F>B>Q, B>G, G>Q>→, ←>→, Q>→, E/B close the bookshelf. Also tested: all bits set means Escape, unknown bits and `None` do nothing, every command is rejected with WrongMode under all 4 overlays without changing state, message or sheet change (A2-11), and `TurnPageBy(int.MaxValue / int.MinValue)` clamps to 42 / 0. |
| 2h | F-010 / R-012 result wording | PASS | `R012_RecognitionLine_PieceAndCrumpled` checks "집 ★3 (일치도 100%)", "종이비행기 ★3 (일치도 100%)" and "구겨진 종이 (가장 비슷한 작품: 집 xx%)". For ★2/★1 the percent is taken from `r.Percent` and not asserted. |
| 2i | F-006 guide texts | PASS | `F006_GuideTitles_ForAllSixSteps` and `F006_GuideTitle_FollowsTheLiveGuide`: "종이비행기 1 / 6 — Ctrl을 누른 채 놓으세요", then "종이비행기 n / 6". |
| 2j | F-013 one-time hints | PASS | Walk hint: shown until the first accepted sit, including on day 2 if the player has not sat yet, and shown again after a new run. "Space" hint: seated, no sheet, free sheet still available (also on day 2). Post-tutorial hint: set by Finish or Discard of the tutorial sheet, including a skipped or crumpled one. A rejected Finish or a rejected Interact(bookshelf) does not set or clear it. It lasts through Stand, counter, night and settlement. It is cleared by OpenBook (B), Interact(bookshelf), TakeSheet or a new run, and appears only once per run. |
| 2k | Support: F-011 AC-07 and F-012 AC-09 text data (owned by 014) | PASS (data) | Counter and settlement lines for F-011 AC-01/03/05, a full 7-day E-05 result, and E-01 for both the completed path (F-012 AC-08) and the bankrupt path (F-001 AC-05). Each was checked line by line against the formats recorded in DECISIONS. Ending names and texts match CONTENT §8 verbatim. |
| 3 | Surfaces follow INTERFACE_CONTRACTS; dog-chin and J-case percents not asserted | PASS | No test asserts a J-case or dog-without-chin percent. |
| 4 | verify_core passes, check_meta OK | PASS | See Commands. |

### Full scripted run (ScreenFlow, key presses only plus fold/crease mouse commands)

`FullSevenDayRun_ByKeysOnly_ThroughSettlementResultAndNewRun` covers this sequence:
1. Walk-hint checks, then sit with E (candidate table), then Space for the free tutorial sheet. Guide title for step 1 is checked.
2. Reject toasts: SheetOnTable, NoFolds, NothingToUndo. A repeated toast gets a new serial.
3. Plane guide, 6 steps, all scored 100. Enter produces the Recognition toast, and the post-tutorial hint appears.
4. Silent NoSheet/NoGuide keys still return true.
5. B, ←/→ and B: the hint is cleared and the spreads are 0 → 4.
6. A paid house sheet, then Q.
7. Counter: E opens, the preview lines show a 25 total, E closes.
8. Bookshelf: the page 4 is kept, then → to 8, Esc.
9. Bed: Esc means no, Enter means yes. The Settlement overlay shows the exact lines (13 popular + 12, money after 43). Every non-Enter key does nothing under the overlay. Enter closes it.
10. Days 2–7: one house per day, with prices from CONTENT §5 (popular house 18 on days 2 and 4).
11. Day-7 settlement ends with "7일이 모두 지났어요". Enter goes to Result, not to a new run. Result lines: E-05, "최종 돈 115원", house ★3 7, totals 109/14/0.
12. Enter starts a new run: the F-001 AC-01 state, sheet change Reset, and the tutorial is available again.

Pause flow: Esc → menu → Esc continues. Esc → Enter → Esc cancels the confirm and keeps the sheet. Esc → Enter → Enter starts a new run, with `Reset.Previous` set to the discarded sheet. The overlay methods do nothing outside their own overlays.

## Bugs

None.

## Observations (no defect)

- `NoSheet` is silent, which matches DECISIONS 2026-11-27 (review W-1). The QA run asserts it as recorded behaviour. C-170 still lacks the entry until the architect updates the contract.
- Two key combinations do nothing because the highest key has no meaning in the current situation: Esc+Enter on the Settlement overlay, and Enter+E while walking. This is the literal C-171 rule. In practice these are simultaneous presses in one frame.

## Untested areas

- Fixed texts in the book and dialogs (CONTENT §3 "봉인됨 — …", "판매대에서 기본 N원", pause, confirm and sleep dialogs). They are outside the C-170 surface and are owned by 013/014 (DECISIONS, W-2).
- Runtime rendering, the 2-second toast timing, Korean font rendering, and the mouse-drag to Fold mapping. These are [Unity] items for 013/014.
- `CoverInfo.Techniques` wording: only checked to be non-empty, per D-110.
- The exact order of a diagram line's two end points: compared in either order, as C-160 allows.
