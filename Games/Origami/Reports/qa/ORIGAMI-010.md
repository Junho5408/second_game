# QA ORIGAMI-010: v2 Core workshop rules, economy, tutorial

- QA: qa-engineer · Branch: claude/serene-fermat-4bwjlo · Base: ce8583f (review APPROVED_WITH_WARNINGS)
- Verification items: `core-tests`, `review` (no `unity-*` items → no CI run required)
- **Result: PASS → COMPLETED** (no bugs found)

## Commands run

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` (before QA tests) | PASS 735/735 |
| `bash studio/scripts/verify_core.sh Origami` (with QA tests) | PASS 803/803 (68 new QA tests) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| `python3 Games/Origami/Docs/design/balance_check.py econ` | 967 plan days 77,152,277,312,502,687,967; no books 437; "경제 검증 OK" |
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-010 --role qa` | OK |

## Tests added (black-box, `Unity/Assets/Game/Tests/EditMode/Core/Qa/`, all new files)

They drive only the public `OrigamiGame` / `Pricing` / `EndingRules` surface. Fold sequences come from the QA transcription
of CONTENT §2 (`V2SpecBook`, ORIGAMI-009 QA, used read-only), not from ModelCatalog or the developer's `GameScenarios`.
Every command is followed by the C-150 invariant check; every rejection checks the reason, `AreSame` snapshot, null
payloads and `FoldError.None` unless InvalidFold.

| File | Content |
|---|---|
| `WorkshopQaDriver.cs` | QA helpers Sit/Stand/Ref/Var/Waste/Night/Buy, Ok/Rej with invariants |
| `WorkshopEconomyRunQaTests.cs` | full 7-day runs with literal daily money, per-day sales totals and endings; review W-2 literal tests; day-7 no bankruptcy check; settlement order/popular flag; script determinism |
| `WorkshopRejectionOrderQaTests.cs` | C-150 rejection-order matrix per command |
| `WorkshopTutorialQaTests.cs` | tutorial edge cases |
| `WorkshopFuzzQaTests.cs` | seeded random command fuzz vs an independent shadow model + replay determinism |
| `WorkshopAcSpecQaTests.cs` | remaining owned [Core] ACs (F-001/003/004/005/007/008/010/011/012) |

### Scripted 7-day runs (expected values hand-derived from CONTENT §5/§9, not from EndingRules)

| Run | Daily money after settlement (asserted) | Daily sales (asserted) | Final / ending |
|---|---|---|---|
| Best ★3 (dog day 2, cicada day 4) | 77,152,277,312,502,687,967 | 65,135,135,195,200,195,290 | 967, **E-04**; sales 1215, paper 68, books 200 |
| No books, plane then house | 77,157,207,287,337,387,437 | 65,90,60,90,60,60,60 | 437, **E-05** |
| Tulip only from day 2 (★3) | 77,137,262,342,422,547,627 | 65,90,135,90,90,135,90 | 627, 30 tulips, **E-02** (beats E-04) |
| All five ★3 (day 4: buy cicada+tulip; house, tulip, cicada×3) | 77,152,277,250,435,620,900 | 65,135,135,153,195,195,290 | 900, **E-03** (beats E-04) |
| Shortest bankruptcy (all sheets wasted) | 12, 2, then 0 | 0 | Bankrupt day 4, money 0, failures 11, paper 20, **E-01**, NextPopular null |
| Bankrupt with money 1 | 31 → day 2 tulip page + 5 wasted → 1 | — | Bankrupt day 3, **E-01**; also NoSheetsLeft before NotEnoughMoney |
| Idle 7 days | 20 every day | 0 | Completed, day 7, **E-01** |
| Day 7 with money 0 | — | — | Completed (no bankruptcy check after day 7), E-01 |

### Review W-2
Both sequences named in W-2 are re-asserted with literals in new QA tests:
`ReviewW2_RichRunSequence_EndsAt161_EndingE05` (day 3 lines house ★2 normal 8 + tulip ★3 popular 27 = 35 → 161, failures 1, E-05)
and `ReviewW2_BankruptRun_EndingIsLiteralE01` (money 0, E-01). The developer's files are unchanged; W-2 is closed by these tests.

### Rejection-order matrix (C-150)
- RunEnded first for all 22 command/argument variants after completion and after bankruptcy (incl. bad arguments).
- In Walking/Bookshelf/Counter/SleepConfirm: WrongMode before NoSheet/InvalidFold (layers −1, NaN), before NoGuide, UnknownTarget, UnknownModel, NotEnoughMoney, AlreadyOwned.
- Seated: `Interact(*)`, `CloseCounter`, `Sleep`, `TurnPage`/`BuyPage` with closed book → WrongMode; `CloseBook` closed → accepted.
- Interact: null/""/"Table"/" bed" → UnknownTarget; UnknownTarget before SheetOnTable; only bed checks the sheet.
- TurnPage: int.MinValue → 0, int.MaxValue → 42, 21 → 20; page kept across close/open; NewRun → 0.
- BuyPage: UnknownModel → AlreadyOwned (money 0 < 20) → NotEnoughMoney; money = price accepted; buying while folding keeps the sheet.
- TakeSheet: SheetOnTable before NoSheetsLeft (5th sheet on table); NoSheetsLeft before NotEnoughMoney (money 1); free first sheet with money 0 accepted.
- Fold/Crease: NoSheet before InvalidFold; layers −1 / int.MinValue → InvalidFold + InvalidLayers; short drag / line outside → InvalidFold with engine FoldError.
- Flip/Undo/Unfold/Finish/Discard: NoSheet first; NothingToUndo; NoFolds for empty, crease-only, crease+flip; flip-only Unfold accepted.
- Seated-only commands with a sheet after standing → WrongMode.
- SkipGuide: NoGuide without sheet, after 6 actions, on a non-tutorial sheet.

### Tutorial edge cases
Guide steps 1..6 match CONTENT §2 plane (kind, grab, drop); exact steps score 100; Undo after step 6 shows step 6 again;
7th action records no score; Flip neither advances nor scores, Undo of a flip keeps scores; Undo/Unfold rewind step and scores,
Unfold clears creases; wrong kind → 0 + KindMismatch; mirrored step 1 (EitherSide) → 100; step 2 folded from the other side → 50
(WrongSide); SkipGuide survives Undo, Unfold, Stand/Sit, second SkipGuide → NoGuide, sheet stays free; first sheet on day 2 is free
tutorial; tutorial sheet as house → house ★3 and no guide on any later sheet/day; Discard ends tutorial; NewRun restores it.

### Fuzz
12 seeds × 600 commands (35% fully random, otherwise mode-biased, plus whole-book macros so pieces get sold). A QA shadow model
predicts every rejection reason (except the engine's InvalidFold for n ≥ 0, which must carry a FoldError) and every snapshot value:
day, money, sheets, mode, book open/visible/page, free sheet, tutorial finished, owned (catalogue order), display (id, grade), completion
counts per model/grade, failures, sales/paper/book totals, popular model, end reason, ending (R-018 priority), fold/action count, CanUndo,
guide visible/step, guide score count, paper area 4.0. Settlement lines are checked against the QA price table. Also: no command throws,
rejection keeps the snapshot reference, acceptance publishes a new snapshot, Settlement/Recognition only on Sleep(yes)/Finish, and replaying
the command list on a fresh game gives identical values. Coverage per seed: ~300 accepted, 13–32 settlements, 3–8 recognised pieces, max day 3–7.

## Acceptance criteria

| Criterion | Result | Evidence |
|---|---|---|
| Commands/rejection table, economy, tutorial contracts implemented | PASS | matrix + fuzz + runs above |
| F-001 AC-01..09 | PASS | AcSpec F001_*, ShortestBankruptcy, Idle7, fuzz/replay (AC-08), invariants every command (AC-09) |
| F-003 AC-01..04 | PASS | AcSpec F003, matrix |
| F-004 AC-01..06 | PASS | AcSpec F004, Tutorial day-2, matrix TakeSheet, ShortestBankruptcy |
| F-005 AC-01..05, AC-07..10 (command level) | PASS | AcSpec F005_*, matrix Fold/Undo/Unfold |
| F-006 AC-01..08 | PASS | Tutorial fixture (AC-04 via Fold then Unfold) |
| F-007 AC-04..05 | PASS | matrix TurnPage, AcSpec F007 |
| F-008 AC-01..05 | PASS | matrix BuyPage (incl. dog → NotEnoughMoney), AcSpec F008_AC05 (money 23, failures 1) |
| F-010 AC-01..05 | PASS | AcSpec F010, matrix NoFolds |
| F-011 AC-01..06 | PASS | runs (AC-01/02: 77, 152), AcSpec F011 (24/40, 30 price cells, popular table, empty night) |
| F-012 AC-01..08 | PASS | AcSpec F012 table + game runs ending literals |
| No asserts on dog-without-chin / J-case percents | PASS | QA tests assert only recognition model/IsPiece/★3 100% for reference sequences |
| verify_core pass, check_meta OK | PASS | 803/803, OK |

## Bugs
None found.

## Notes / untested areas
- Review W-1 (NaN/∞ coordinates → InvalidFold, FoldError NothingToFold not in C-150) not re-asserted: QA only checks that a NaN fold in a
  wrong mode is WrongMode and that the fuzz never throws. Contract wording is pending with the architect.
- The F-010 AC-03 "house 72~73%" value is not asserted (only crumpled + best model house), per the instruction to avoid J-style percent asserts.
- R-008 guide target magnet (0.08) and all [Unity] ACs (F-002, F-003 AC-05..07, F-005 AC-17..19, F-006 AC-09, F-007 AC-06/07, F-010 AC-06,
  F-011 AC-07, F-012 AC-09) are Runtime/playtest scope, not tested here.
- Balance targets "all ★2 562 / all ★1 242" need graded variants for every model; only house variants (Var) exist in the spec, so not scripted.
