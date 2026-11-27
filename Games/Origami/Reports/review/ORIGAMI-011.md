# Review: ORIGAMI-011 — v2 Core 책 도해·문구·화면 흐름

- Reviewer: code-reviewer
- Branch: claude/serene-fermat-4bwjlo · base d515e61 · commits 5381350, dfa1ffa, 3313784 (impl), cc998af (status)
- Scope: Core/{Book,Texts,Screens}/, Tests/EditMode/Core/{Book,Texts,Screens}/ (+ .meta)

## Result: **APPROVED_WITH_WARNINGS**

## Verification (run by reviewer)

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-011` | `OK` |
| `bash studio/scripts/verify_core.sh Origami` | PASS: 921 passed, 0 failed (netstandard2.1 / C# 9 build + dotnet test) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| `grep UnityEngine` in the 6 scope folders | no matches |
| `git diff --name-only d515e61 HEAD -- Games/Origami/Unity` outside the 6 scope folders | none (no v1 changes, no changes to other tasks' code) |
| `balance_check.py steps` (scratchpad rv11) | all 32 step lines and magnets (P/L/M/−) match the test literals in `BookPagesTests.AllStepDiagrams_MatchContentSection2` |

## Checklist

1. **Spec / AC conformance: met.**
   - F-007 AC-02: page 6 has BeforeFolds 2, BeforeCreases 1, line (0,1)–(−0.828427,−1) (order-free, as C-160 allows), grab (−1,0), drop (0,−0.4142), valley fold, all layers, label "4 / 6", and the CONTENT §2 text verbatim. Page 3 is a Crease. Page 21 has Layers 2.
   - F-007 AC-03: page 18 is sealed with no diagram in a new run. The page-17 cover has the name, silhouette, price 20 and Buyable. Page 18 gets a diagram once tulip is owned. Page 1 is always visible.
   - Facet counts before each step match D-103 for all 5 models. I re-derived plane [1,1,2,3,5,7] and cicada [1,2,4,6,6,6,8,10,12,20].
   - F-013 AC-01/02: the reject-text table matches F-013/C-170 word for word. HUD after F-011 AC-01 is (2, 77, 5, "집").
   - F-006 titles: "종이비행기 1 / 6 — Ctrl을 누른 채 놓으세요", then "종이비행기 n / 6".
   - R-012 lines: "이름 ★n (일치도 xx%)" and "구겨진 종이 (가장 비슷한 작품: 이름 xx%)".
   - The three hint constants and the four "E: …" prompts match F-013 / CONTENT §6 exactly.
   - Ending names and texts for E-01 and E-05 are asserted as CONTENT §8 literals.
   - Task AC 3: the J/dog-chin percent values are not asserted. F010_AC03 builds the expected string from `r.Percent`.
2. **Contract conformance: met.** The public surfaces of C-160/C-170/C-171 are pinned by the `*SurfaceTests` reflection tests. GameKeys flag values, enum orders and member signatures match INTERFACE_CONTRACTS.
   - **Key table, re-derived by the reviewer from C-171 (and checked against F-003).** Checked rows: Settlement (Enter only), Result (Enter only), PauseMenu (Esc → ClosePause, Enter → RequestNewRun), ConfirmNewRun (Esc / Enter), Walking (Esc → OpenPause; E → Interact only with a candidate), Seated (Esc and E are "—"; page keys only when the book is open), Bookshelf (Esc/E/B → CloseBook, ←/→ → TurnPageBy(∓2), Enter "—"), Counter (Esc/E), SleepConfirm (Esc → Sleep(false), Enter → Sleep(true)). `ProcessKeys` checks the overlay first and then the mode, so the Settlement row correctly wins over Mode Ended on the last night.
   - **Key priority:** the `Priority` array equals Escape > Enter > Interact > TakeSheet > Discard > Undo > Unfold > Flip > Book > SkipGuide > StandUp > PageLeft > PageRight. If the highest pressed key means nothing in the situation, the result is false and nothing else is processed. Examples: Seated Esc+Space → false; Walking with no candidate, E+Space → false.
   - `KeyTableTests.F003_KeyTable_EveryCell` exercises 11 situations × 13 keys = 143 cells. Every "—" cell must leave the snapshot reference, overlay, message serial and sheet-change serial unchanged. Pairwise priority is also tested in Seated.
   - A2-11: with an overlay open, every one of the 19 commands returns WrongMode without calling Core.
3. **Boundaries: met.** The code is engine-free C# 9, in namespaces `Origami.Core.{Book,Texts,Screens}`. It uses only public/internal Core API (`CommandResult.Reject` is internal to the same assembly). There is no clock, random or mutable static state; `Priority` is a private static readonly array that is never written.
4. **Null / invalid state: met.**
   - `BookPages.Get` throws AOORE / ANE as documented, and ignores null, unknown and duplicate owned IDs.
   - The `GameTexts` functions throw ANE on null.
   - `FlowMessage.Text` is never null for the default struct.
   - `TurnPageBy` cannot overflow (the sum is computed as a long and clamped).
   - A missing fold line on a reference step throws `InvalidOperationException`. This is a catalogue invariant, not player input, so it is acceptable.
5. **Performance: acceptable.** `BookPages.Get` recomputes `LineOnPaper`/`DropSnap` per call over cached reference states. That is cheap, and C-192 has the Runtime cache by (page, owned).
6. **Abstractions / duplication: none unnecessary.** `SheetCommand` and `RemoveSheet` remove repetition across the 8 sheet commands.
7. **Regression risk: low.** The change only adds new folders and does not touch v1 or 009/010/012 code.
8. **Tests: sufficient, and the expected values come from the specs** (CONTENT literals, F-011 money values 65/77/24/40, CONTENT §5 prices 13/12/8/4, D-103 facet counts), not from the code.

## Developer assumptions — evaluation

| # | Assumption | Verdict |
|---|---|---|
| 1 | Line formats for settlement / counter / result | OK. C-170 §note leaves the line formats to 011, and the fixed sentences are kept. They cover everything F-011 AC-06/07 (name, grade, price, total, money after; popular model, per piece name/grade/match/today's price, expected total) and F-012 (ending name and text, final money, 5×3 counts, failures, the three totals) list. "★n" digit form follows R-012's literal "★n". The 014 GUI can print the lines as they are. |
| 2 | Sealed step pages keep StepNumber/StepTotal/StepLabel | OK. C-160 says "Step 쪽만", and a sealed step page is still Kind Step. Only `Diagram` is tied to "봉인 아님". 013 draws only "봉인됨 — …" when `Sealed`. |
| 3 | `ConfirmNewRun` also acts in Mode Ended without an overlay | OK. It matches the C-171 table row "Overlay Result (또는 Mode Ended) / Enter → ConfirmNewRun". Through ScreenFlow the state is unreachable (a run can only end through Sleep(yes) → Settlement → Result), so this is harmless. |
| 4 | `ToggleBook` = CloseBook if BookVisible, else OpenBook | OK. It matches F-003 "B 책 펴기/닫기" and the Bookshelf B → CloseBook cell. In Walking it is a silent WrongMode. |
| 5 | `TurnPageBy` = TurnPage(page + delta), clamped to the int range | OK. It matches R-010 "←/→ = TurnPage(현재 ∓ 2)", and Core does the [0,42] clamp. |
| 6 | `MessageKind.Info` is never produced | OK. C-171 defines no rule that produces Info, and the enum value is kept for the surface. |
| 7 | A new run keeps the last message (the serial does not change) | OK per C-171 ("그 밖 명령은 메시지를 바꾸지 않는다"). There is a minor UX note for 014, see N-2. |
| 8 | Rejections while an overlay is open are silent | OK. A2-11 says WrongMode, and WrongMode has no text (F-013). |

## Warnings

### W-1 (Low, contract/spec gap): `NoSheet` is silent, but no document says so
- Where: `Core/Texts/GameTexts.cs` `RejectMessage` (default branch); test `GameTextsTests` `[TestCase(RejectReason.NoSheet, "")]` and `F013_AC01_RealRejectionsMapToText`.
- Problem: F-013 and C-170 list a text for some reasons and "no text" for NoGuide/WrongMode/UnknownTarget/UnknownModel/RunEnded/None. `NoSheet` is in neither list. The implementation makes it silent, and this choice is not among the developer's recorded assumptions.
- Impact: Behaviour is sensible: Enter/X/Z/R/F with no sheet do nothing visible, and the 014 buttons are hidden without a sheet. But the rule is undocumented, so 014 or QA could treat it as a bug.
- Fix: The Orchestrator records it in `Docs/DECISIONS.md`, and the architect adds `NoSheet` to the C-170 "→ \"\"" list at the next contract update. No code change. (`FoldError.InvalidLayers` → "" comes from 009's `FoldTexts`. The gesture never produces negative layers, so it cannot be reached in play.)

### W-2 (Low, hand-off to 013/014): book and dialog fixed texts are not in `GameTexts`
- Where: C-170 surface (as designed). The texts are in CONTENT §3: "봉인됨 — 표지에서 페이지를 사세요", "판매대에서 기본 N원", contents "보유" / "N원", cover labels. F-003 also has pause menu "계속 / 새로 시작", the new-run confirm and the sleep confirm.
- Problem: C-170 does not include these, so 013 (`BookPanel`) and 014 (`OrigamiGui`) have to write them in Runtime, where dotnet tests cannot reach them.
- Impact: There is a risk of wording drift from CONTENT §3, and Unity-only verification.
- Fix: The Orchestrator adds an acceptance item to 013/014: copy these strings verbatim from CONTENT §3 / F-003. Alternatively, it requests a C-170 extension from the architect. Not a defect of 011.

## Notes (no action required from 011)
- **N-1:** Record the 011 line formats ("N일차 정산", "합계 N원", "정산 후 돈 N원", "내일의 인기: 이름", "N일차 판매대", "… · 오늘 팔면 P원", "합계 예상 N원", "이름: ★3 a · ★2 b · ★1 c", …) and the "★n" digit form in DECISIONS.md, so that 014 and the content designer know they are fixed by tests.
- **N-2 (for 014):** Suppose a reject toast is less than 2 s old when the player confirms a new run (Esc → Enter → Enter). It would keep showing over the new run, because the message is kept. 014 can clear the toast on `SheetChangeKind.Reset`. This is cosmetic.
- **N-3:** `WalkHintVisible` is also true behind the Settlement/Pause overlays while walking. 014 draws overlays last, so this is harmless.

## Status
APPROVED_WITH_WARNINGS → `task.py set Origami ORIGAMI-011 --status QA --result review=pass`
