# QA ORIGAMI-009 — v2 Core folding engine, magnets, recognition, catalogue

- QA: qa-engineer · Branch: `claude/serene-fermat-4bwjlo` · Tested at: `25c9eae` (review commit) + QA tests
- Review: APPROVED_WITH_WARNINGS (W-1 parity generator location, W-2 CONTENT §2 house-3 line order are Orchestrator/doc follow-ups, not QA failures)
- **Result: PASS** — no defects found. Status → COMPLETED (verification items are `core-tests` + `review` only).

## Method

The expected values were typed by QA straight from the spec documents. They were not taken from the developer's tests or from `ModelCatalog`.
- `Qa/V2SpecBook.cs` holds an independent transcription of CONTENT §2: action, layers, line ends and magnet letter for all 32 steps, plus the D-103 table.
- The black-box tests replay these through the public API (`FoldSession`, `FoldGesture`, `DropSnap`, `LineOnPaper`, `Recognizer`, `ModelCatalog`, `BookLayout`).

## Verification run

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` (before QA tests) | PASS 469/469 |
| `bash studio/scripts/verify_core.sh Origami` (with QA tests) | PASS 595/595 (126 new QA cases, all v1 tests included) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-009 --role qa` | OK |

## Acceptance criteria

| AC | Result | Evidence (QA test) |
|---|---|---|
| CONTENT §2 sequences (grab/drop/mountain/layers) | PASS | `FoldingSpecQaTests.Content2_CatalogueStepsMatchQaTranscription` (5 models, ±1e-12) |
| CONTENT §2 diagram lines ±1e-4 | PASS | `Content2_DiagramLineEndsWithin1e4` (32 steps; ends compared unordered, see W-2) |
| CONTENT §2 magnet kinds, same fold line after snapping (±1e-5) | PASS | `Content2_ExactDropHitsListedMagnetAndKeepsFoldLine`. Every P/L/M/− letter matched, and every "−" step returns None |
| D-103 per-step (facets, crease segments, folds), area 4.0 | PASS | `D103_SessionCountsPerStep`. Also checks CreaseCount = number of Crease actions and ActionCount |
| ReferenceState / StateBeforeStep = FoldSession replay | PASS | `Session_MatchesReferenceAndStateBeforeStep` (bitwise), `Session_UndoWalksBackThroughEveryReferenceStep` |
| F-005 AC-06 (mountain vs valley) | PASS | `F005_AC06_…`, 4 cases (incl. diamond). Outlines are identical (intersection = area). Moved facets are on top for valley and at the bottom for mountain |
| F-005 AC-11 | PASS | `F005_AC11_CreaseThroughTwoLayers`. Segments (0,−1)–(0,0) and (0,1)–(0,0), length 2.0, 2 facets, 1 fold |
| F-005 AC-12 | PASS | `F005_AC12_DiamondBaseShape` (6 facets, layers 0–5, areas 1,1,.5×4, outline 1.0, M = 6 at C). `F005_AC12_TulipPetalTopTwoVersusAllLayers`: top-2 moves 2 facets (0.70), 8 facets, outline 1.244; all layers moves 3.143, 12 facets, outline 0.786 |
| F-005 AC-13 | PASS | `F005_AC13_HingedLayersFoldTogether`. Layers 1 is identical to layers 0, outline 1.804 |
| F-005 AC-14 | PASS | `F005_AC14_…` with layers 6, 9 and 1000, each identical to layers 0 |
| F-005 AC-15 | PASS | `F005_AC15_MountainTopTwoLayerOrder`. Moved {4,5}, rest of the same flap {6,7}, other flap {2,3}, bottom {0,1} |
| F-005 AC-16 | PASS | `F005_AC16_DropMagnets`. Results M (0,0.2), P (0,0), L (−0.6,−0.8) and None (−0.62,−0.78), with anchor counts. Guide magnet is covered separately |
| F-005 AC-18 (Core part, supporting) | PASS | `Gesture_LayerCycleOnDiamond`. StackCount 6, two notches show "겹: 위 2 / 6", wraps forward and backward, M = 1 ignores the wheel, a miss returns null |
| F-009 AC-01 (J-01..J-18, result + grade, score ±0.01) | PASS | `RecognitionSpecQaTests.J01…J18`. Inputs were rebuilt from the §4 "입력" column (Var(dy), J-07 wrong sides, J-15 wing all layers, J-17 top-1, …). Percent is asserted only for J-01 (table "100%") |
| F-009 AC-02 (cross table ±0.01, off-diagonal < 0.85) | PASS | `AC02_CrossTableBothDirections`, both directions. `Similarity.Centered` = `catalogue.Score` (1e-9) |
| F-009 AC-03 (invariance on raw states) | PASS | `AC03_SpecTransformsScoreOne`: (+0.3,−0.2), 90° and mirror. `AC03_RandomD4PlusTranslationScoreOne`: 60 fresh random D4 + translation (±3) states. Both also scramble facet order, vertex direction, layers and FrontUp. Creases are ignored |
| F-009 AC-04 (percent floor + grade boundaries) | PASS | `AC04_PercentAndGrade` covers 0.97/0.969/0.93/0.929/0.85/0.849 plus 1.0 and 0. `AC04_FromPercentThresholds` covers 0..100 |
| F-009 AC-05 (tie → catalogue order) | PASS | `AC05_TieGoesToEarlierCatalogueEntry`. Catalogue A,B gives A and B,A gives B. The owned order does not matter, and only owned IDs are candidates. Unknown, duplicate and null owned IDs are ignored. With no candidates the best ID is null and the score 0 |
| F-009 AC-06 (determinism) | PASS | `AC06_HundredRepeatsBitIdentical` (100×, bitwise, plus a freshly built catalogue gives the same bits). `Fuzz_RandomSessionsPreserveAreaAndReplayDeterministically`: 60 random sessions, rejections leave the state reference unchanged, area 4.0, layers ranked 0..n−1, ApplyAll replay is bitwise equal |
| F-007 AC-01 (BookLayout) | PASS | `CatalogV2SpecQaTests.F007_AC01_PageLayoutAllPages` checks the kind, model and step of all 44 pages. `F007_ClampToSpread` covers −1→0, 17→16, 31→30, 43→42, 99→42 and the int extremes |
| CONTENT §1 model table | PASS | `Content1_ModelRow` ×5: order, name, actions, folds/creases, prices, owned, difficulty, cover rotation, reference outline area ±0.01 |
| CONTENT §5 popular table (+ price table from base prices) | PASS | `Content5_PopularByDay`, `Content5_PriceTableFromCatalogueBasePrices` (30 cells, R-015 with GRADE_PCT 150/100/60) |
| CONTENT §4.2 shortcut policy (paid models < 0.91) | PASS | `Shortcut_AllBookSubsequencesWithFewerFoldsStayBelowPolicy`. Valid/total counts equal the table (30/31, 6/7, 25/31, 14/15, 158/255). Best scores are tulip 0.715, dog 0.868 and cicada 0.891; only "< 0.91" is asserted |
| Layer-choice / rejection cases | PASS | `Cicada_WingsWithAllLayersBlockSteps7And8` (NothingGrabbed / NothingToFold, Korean texts, state unchanged). `Cicada_WingTopOneEqualsTopTwo_Hinged`. `Rejections_DoNotChangeSession` |
| Input-layer playthrough | PASS | `Gesture_PlayingTheBookThroughFoldGestureGivesThreeStars` ×5. Each step goes TryBegin at the book grab → wheel to the book layers → MoveTo book drop (magnets on) → Preview → commit with the same arguments. Preview equals commit, and the result is ★3 of that model |

Not asserted, by instruction (review W-6, ORIGAMI-008 W-3): the dog-without-chin score and the J-case Percent values other than the J-01 100%.

## Tests added (all new, v1 Qa files untouched)

- `Games/Origami/Unity/Assets/Game/Tests/EditMode/Core/Qa/V2SpecBook.cs` (QA transcription of CONTENT §2 / D-103 + helpers)
- `Games/Origami/Unity/Assets/Game/Tests/EditMode/Core/Qa/FoldingSpecQaTests.cs`
- `Games/Origami/Unity/Assets/Game/Tests/EditMode/Core/Qa/RecognitionSpecQaTests.cs`
- `Games/Origami/Unity/Assets/Game/Tests/EditMode/Core/Qa/CatalogV2SpecQaTests.cs`
- and their `.meta` files (gen_meta.py)

## Bugs

None found.

## Notes / untested areas

- **QA test fix**: the first run of the shortcut test failed with plane 31/31 instead of 30/31. The cause was in the QA test itself: `balance_check.py subsequences()` skips states with 0 folds. The test was corrected to match the reference; this is not an implementation defect.
- **W-2 confirmed**: CONTENT §2 house step 3 lists the line ends in the reverse order of the engine output. QA compares the ends as an unordered pair, so this needs only a doc follow-up.
- **Unity**: compilation of these files is not proven by this task (no `unity-*` item). The QA tests use only NUnit 3.5 APIs and C# 9. Random seeds are fixed integers; `string.GetHashCode` is not used.
- **Out of scope**:
  - Session-level rules (F-005 AC-01–05, AC-07–10) are owned by 010.
  - Runtime/UI (AC-17–19) is owned by 012.
  - Human-error simulation (§4.1) is a balance model, not Core behaviour.
- **Not exercised**: `DropSnap` performance on very large facet counts (> 28 facets).
