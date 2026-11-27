# QA ORIGAMI-004: Core screen flow (OrigamiFlow + FlowTexts)

- QA: qa-engineer · Date: 2026-11-27
- Base: c1cfd7c · Task commits 58694d0, d1110e4 · Review 2311306 (APPROVED) · Decisions 5aa4803
- **Result: PASS → COMPLETED** (the verification items are `core-tests` and `review` only, with no `unity-*` items)
- Defects: 0 Critical / 0 High / 0 Medium / **1 Low** (BUG-ORIGAMI-004-1). It is a gap in the spec (C-020/A-6), not in the code. The code matches the contract.

## Verification

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` (before QA tests) | PASS, 275 / 275 |
| `bash studio/scripts/verify_core.sh Origami` (with QA tests) | `== Origami: PASS ==`, 283 / 283 (275 existing + 8 new QA tests) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| Mutation checks (scratchpad copy, 16 mutants of `OrigamiFlow.cs` / `FlowTexts.cs`, only the new QA tests run) | 15 / 16 killed. The 1 survivor is equivalent (see below). |
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-004 --role qa` | `OK` |

## Task acceptance criteria

| AC | Result | Evidence |
|---|---|---|
| AC-1: C-020 and C-021 implemented (transitions, key table, one key per frame, text formats) | PASS | The independent reference flow (`FlowFuzzQaTests.FlowReference`) was written straight from the C-020 tables and runs its own `OrigamiRun`. It agrees with `OrigamiFlow` on every one of about 300,000 random frames: return value, screen, selection, Message, LastResult (including piece and settlement), Settlement, and the full snapshot. Key priority was checked with all 256 masks. The fixed C-021 formats were checked in the playthroughs: Settlement total and money, every Result line, the AbandonConfirm question, Notice lines and the BankruptNotice block. |
| AC-2: F-007 AC-01..06, 08, 09 and F-006 AC-11 tested at text level | PASS | The implementer's `F007ScreenTextTests` have a named test for each AC. QA confirmed each one independently (table below). |
| AC-3: The folding screen never shows remaining steps | PASS | This was checked on every frame where the screen was Folding or AbandonConfirm. The glyphs (`↑↓←→F`) across all lines equal exactly `steps[0..progress)` + `steps[progress]` when hinted. There is exactly 1 `"힌트: "` line when the step is revealed and 0 otherwise. Glyphs never appear on Workbench, Notice, Settlement or Result. On Preview they are exactly the full sequence of the selected model. There were 0 violations. Mutants that leak the next step, show the hint line without a hint, or hint a later step are all killed. |

## Feature ACs

| AC | Result | Evidence |
|---|---|---|
| F-007 AC-01 start workbench | PASS | `F007_AC01_*`. Also, `AssertResultAndRestart` checks `"일차 1/7   돈 20원   시도 5/5"` and that the restarted lines equal those of a fresh flow. |
| F-007 AC-02 five models, locked progress | PASS | `F007_AC02_*`. The fuzz test asserts the Locked message text against its own formula on every Locked selection. |
| F-007 AC-03 progress and mistakes | PASS | The fuzz test checks the `"진행 i/N"` and `"실수 m/3"` lines against the snapshot every frame, and the snapshot against the reference run. |
| F-007 AC-04 no remaining steps | PASS | See AC-3 above. |
| F-007 AC-05 key equals button | PASS | `FlowKeyTests` fingerprints. The fuzz test mixes key frames and button calls against one shared reference, with 0 mismatches. |
| F-007 AC-06 settlement total and money | PASS | `F007_AC06_*`. All 7 playthroughs assert `"합계 …"` and `"정산 후 돈 …"` on every settlement. |
| F-007 AC-07 code-only graphics | n/a | This is a Unity view item for ORIGAMI-005 and is not part of this task. |
| F-007 AC-08 Esc confirm | PASS | `F007_AC08_*`. In the fuzz test, every abandon (7,533) happened on the AbandonConfirm screen, from a frame with Enter and without Esc or from the confirm button. All 1,762 Esc+Enter frames on Folding only opened the confirm screen, with the failure count unchanged. |
| F-007 AC-09 bankrupt settlement → E-01 | PASS | `F007_AC09_*`. `E01_Bankrupt_ByKeys_*` asserts that the day-4 lines are exactly `["4일차 정산","합계 0원","정산 후 돈 0원",BankruptNotice]` (no next-day line), and that Enter leads to Result E-01. |
| F-006 AC-11 result screen and restart | PASS | `F006_AC11_*`. For each ending, the playthroughs assert all 11 Result lines: name, text, final money, the 5 count lines, failures, sales and paper. They also assert that no key leaves Result and that Restart returns to the AC-01 state. |

## Exploratory testing

### `Qa/FlowFuzzQaTests.cs`: seeded random streams compared with a reference flow
- There are 300 streams. 200 are "normal" (600 frames each) and 100 are "spendthrift" (1,800 frames each, mostly abandons, so money runs out). This gives about 300,000 frames. The PRNG is an in-house xorshift, so the streams are identical under .NET and Mono.
- Each frame is one of three kinds:
  - key frames: all 256 masks, single keys, or "smart" keys (the correct step, H, Esc/Enter), sometimes with extra noise bits;
  - button calls on any screen, including the wrong one: SelectModel with plane through dragon, `null`, `""`, `"PLANE"` and `"swan"`, Fold with undefined actions 0 and 6, and every other method;
  - Restart.
- Invariants are checked after every frame:
  - The screen is a defined value.
  - Folding or AbandonConfirm ⇔ `Snapshot.IsFolding`.
  - Settlement screen ⇔ `Settlement != null`.
  - Selection is non-null ⇔ the screen is Preview, Folding, AbandonConfirm or Notice. The selection equals the folded model, and the selected model is unlocked.
  - Run ended ⇒ the screen is Settlement or Result. Result ⇒ the run has ended.
  - There is no Message on Notice, Settlement or Result. When there is a Message, it is the last line (before the question on AbandonConfirm).
  - On Result, the first two lines equal the name and text of `EndingRules.Evaluate(...)`.
  - The no-leak rule from AC-3 holds.
- A coverage gate is asserted, so the test cannot pass without exercising these paths:
  - all 7 screens;
  - the Locked, UnknownModel, NoAttemptsLeft, NotEnoughMoney and InvalidAction messages;
  - 7,533 abandons, 1,288 completions, 485 ruins, 6,357 hints and 537 restarts;
  - 156 bankrupt runs and 389 seven-day runs;
  - 1,762 Esc+Enter frames;
  - endings E-01 and E-05. E-02..E-04 need planned play and are covered by the playthroughs.
- Result: **0 mismatches and 0 invariant violations.** It runs in about 8 s under dotnet.

### `Qa/FlowEndingPlaythroughQaTests.cs`: whole runs by keyboard
- Every command that has a key in C-020 is sent as a key. These are Enter/Esc on Preview, fold keys, H, Esc→Enter for abandon, and Enter on Notice and Settlement. Only SelectModel and EndDay (Workbench) and Restart (Result) are button calls, because C-020 gives them no key.
- The endings reached are:
  - E-01 by bankruptcy;
  - E-01 by finishing day 7 with 20 money;
  - E-02 with 29 cranes;
  - E-03 with 1709 money (same as F-006 AC-10);
  - E-04 with ★3 for all 5 models;
  - E-05 with a hinted ★2 plane, checking the line `"종이비행기 ★3 0 / ★2 1 / ★1 0"` and money 25.
- For each ending, the Result text matches `EndingRules`, the catalog, and the snapshot.

### Mutation check (scratchpad copy; only `Origami.Tests.Qa.Flow*` run)
- 15 mutants were killed:
  - the fold line leaks the next step;
  - the hint line is shown without a hint;
  - the hint shows a later step;
  - Enter has priority over Esc;
  - RequestAbandon abandons with no confirm;
  - Settlement never goes to Result;
  - Message is not cleared on an accepted command;
  - Result shows the wrong ending;
  - a ruin stays on Folding;
  - the H key folds;
  - a locked model can be previewed;
  - Notice keeps the selection;
  - the confirm line contains a glyph;
  - the bankrupt text is changed;
  - ← is mapped to R.
- 1 mutant survived, and it is equivalent: Restart not clearing `_selectedModelId`. The selection is already null on Result, because Notice→Workbench clears it and Settlement is only reached from Workbench.

## Bugs

### BUG-ORIGAMI-004-1: The game cannot be played with the keyboard alone (spec gap)
- **Affected:** F-007 controls, C-020 key table, ARCHITECTURE A-6, DECISIONS 2026-11-27 ("키보드만으로 진행 가능하게"). The code matches C-020 exactly.
- **Severity:** Low. Every action is still possible with the on-screen buttons, and F-007 only requires keys for folding, H and Esc.
- **Preconditions:** a new `OrigamiFlow` (Workbench), or any flow on the Result screen.
- **Steps:** call `ProcessKeys` with each of the 256 `FlowKeys` masks on Workbench, then do the same on Result.
- **Expected:** A-6 says its rationale is "키보드만으로도 플레이 가능하게 하는 최소 확장", so a player using only the keyboard would expect to be able to select a model, end the day and restart.
- **Actual:** every mask returns `false` and the screen does not change. Model selection, "하루 마치기" and "다시 시작" have no key, so a run cannot start or progress past Workbench without the mouse.
- **Evidence:** `FlowEndingPlaythroughQaTests.KeyboardAlone_CannotLeaveWorkbenchOrResult` and `AssertResultAndRestart` (256 masks on Result → `false`) both pass, which confirms the behaviour.
- **Reproducibility:** 100 %.
- **Recommendation (for the Orchestrator):** this does not block, because the code is correct against the contract. If keyboard-only play is wanted, an architect or game-designer task should extend C-020, for example with 1–5 for SelectModel, a key such as E for EndDay on Workbench, and Enter for Restart on Result. Otherwise, reword the A-6 rationale.

## Not tested / limits
- Unity compilation and running the new tests under Unity NUnit 3.5 / Mono have not been tested, because this task has no `unity-*` item. The new tests use only classic NUnit asserts (`AreEqual`, `IsTrue/IsFalse`, `Greater/Less/GreaterOrEqual`, `CollectionAssert`, `StringAssert.StartsWith`). They use no `TestContext` and no `System.Random`. The next Unity CI run will confirm this. The fuzz test takes about 8 s under dotnet and may take longer under Mono.
- F-007 AC-07 and everything visual or input-related (IMGUI buttons, `ReadKeys`, fonts) belong to ORIGAMI-005.
- The Workbench lines, apart from the glyph and message checks, rely on the implementer's text tests: the price column and the order of the locked-model lines.
