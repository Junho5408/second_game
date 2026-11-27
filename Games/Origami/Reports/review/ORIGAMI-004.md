# Review ORIGAMI-004: Core screen flow (OrigamiFlow + FlowTexts)

- Reviewer: code-reviewer
- Base commit: c1cfd7c. Reviewed commits: 58694d0 (implementation and tests), d1110e4 (set REVIEW)
- Result: **APPROVED**

## Verification
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-004` | OK |
| `bash studio/scripts/verify_core.sh Origami` | PASS, 275/275 tests (netstandard2.1 / C# 9 build OK) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| `git diff --stat c1cfd7c` | only `Core/Flow/FlowTexts.cs`, `Core/Flow/OrigamiFlow.cs`, `Tests/EditMode/Core/Flow/**` (+ .meta) and the task file. `Core/Rules`, `Core/Run`, `FlowScreen.cs` and `FlowKeys.cs` are unchanged. |

## Checklist

1. **Spec and acceptance criteria.** All met.
   - C-020 screen x method table. Every method checks `Screen` first and returns `false` with no side effect on the wrong screen. The transitions match the table row by row: SelectModel handles unknown, locked and unlocked models; EndDay sets `Settlement`; StartSelected stays on Preview when Core rejects; Fold goes to Notice only on Completed or Ruined; Hint stays; RequestAbandon and CancelAbandon make no Core call; ConfirmAbandon goes to Notice; Acknowledge on Notice clears the selection, and on Settlement it goes to Result or Workbench and nulls `Settlement`; Restart calls NewRun and goes to Workbench. `FlowTransitionTests.EveryMethodOnEveryScreen_MatchesTable` covers the whole matrix.
   - Key table and A-6. `ProcessKeys` matches C-020 exactly. Enter works only on AbandonConfirm, Preview, Notice and Settlement. Esc works only on Folding, AbandonConfirm and Preview. Arrows, Space and H route to `Fold`/`Hint`, which return `false` outside Folding.
   - Priority (A-4). `TopKey` picks exactly one key in the order Esc > Enter > H > Up > Down > Left > Right > Space. Unknown bits are ignored.
   - The Esc confirm cannot be skipped. On Folding, Esc+Enter resolves to Esc and opens AbandonConfirm. On AbandonConfirm, Esc+Enter resolves to Esc and cancels. So Abandon always needs a separate Enter-only frame. This is tested in `EscPlusEnterInOneFrame_CannotSkipConfirm`, and every one of the 255 key combinations on every screen is compared with its single top key.
   - The fold screen never reveals remaining steps. I checked every path myself, not just the tests. `AddFolding` reads only `FoldSnapshot` (`ModelId`, `Progress`, `StepCount`, `CorrectSteps`, `Mistakes`, `Hints`, `ExpectedGrade`, `RevealedAction` when `IsCurrentStepRevealed`), and `FoldSnapshot` itself holds only the steps already done (C-011). The full `model.Steps` sequence appears only in `AddPreview`, and Preview is reachable only for unlocked models via SelectModel. The only message that can appear on the fold screen is a Core rejection text (for example InvalidAction), which contains no glyphs. AbandonConfirm reuses the same lines and adds the fixed question. The test `F007_AC04_...EveryModel` covers all 5 models, including hints, mistakes, AbandonConfirm and the InvalidAction message line.
   - Messages. Core rejections and flow-level blocks set the message. Accepted commands clear it (`Execute`), and so does every screen change (`GoTo`). Ignored calls change nothing, which is pinned by fingerprint comparison.
   - Locked selection (A-7). The flow checks `Snapshot.IsUnlocked`, shows the Locked message (the unlock condition), makes no Core call and opens no Preview. Lack of attempts or money is not blocked at selection.
   - Bankruptcy settlement (AC-09). The screen shows the sale lines, the total, money after settlement 0 and `BankruptNotice`, with no next-day popular model line. Enter then goes to Result, which shows E-01 "빈손". A day-7 settlement shows no extra line.
   - Text formats. All bold formats in C-021 match character for character, including the double spaces in the workbench list, the `"(인기)"` marker, the `"★3 a / ★2 b / ★1 c"` counts, `"실패 n회"` and the abandon question. Numbers use the invariant culture with no thousands separator.
2. **Architecture and conventions.** The code has no `UnityEngine`, uses namespace `Origami.Core.Flow`, has no static mutable state, and the public surface is unchanged (`SurfaceContractTests` pass; the new helpers and the question const are private).
3. **Module boundaries.** Flow uses only the public Core API (`OrigamiRun`, `RunSnapshot`, `Pricing`, `Unlocking`, `OrigamiCatalog`). No rule logic is duplicated: prices come from `Pricing.SalePrice(id, Star3, day)`, unlock progress from `Unlocking.Progress`, and endings from the snapshot.
4. **Nulls and invalid states.** `FindModel(null)` returns null. The helpers have defensive guards for unreachable states: a null fold or settlement, an unknown ending, an unexpected Notice outcome, and a Core rejection in `ConfirmAbandon`, which falls back to Folding and keeps the message. None of these can throw.
5. **Performance.** `ScreenLines` allocates a small list on each call, which is fine for one call per frame in IMGUI.
6. **Unnecessary abstraction.** None. The code is direct and readable.
7. **Regression risk.** None. Only the previous skeleton bodies were replaced.
8. **Tests.** 58 new tests. They include exhaustive screen x method and screen x key matrices, all 255 key combinations, key-equals-button fingerprints (AC-05), and text-level AC-01..06, 08, 09 and F-006 AC-11. They are sufficient.

## Developer choices for unspecified points (all accepted)
- `ScreenLines(null)` throws `ArgumentNullException`. This is consistent with the "programmer error" policy.
- `UnlockCondition` returns `""` for a null or initially unlocked model, and throws `ArgumentNullException` for a locked model with null counts. This mirrors `Unlocking.Progress`, and C-021 requires the `""` cases.
- The "맞힌 단계: " line joins glyphs with one space and shows `"-"` when there are none. This matches the preview glyph format.
- Line order: on Workbench, Preview, Folding and AbandonConfirm the message comes last before the AbandonConfirm question. In other words, AbandonConfirm shows the Folding lines including any message, then the question. This is reasonable and consistent with "Folding 줄 전부 + question".

## Unity NUnit compatibility
- A `Dictionary<(FlowScreen, FlowKeys), Func<...>>` key uses `System.ValueTuple`, which is part of netstandard2.1 and of the Unity 6 Mono/.NET Standard 2.1 profile. Its default equality comparer works with enum fields. This compiles under C# 9.
- `using static Origami.Tests.Flow.FlowScenarios` (C# 6) refers to an `internal static class` in the same `Origami.Tests.EditMode` assembly. Existing Run tests already use this pattern.
- Only classic NUnit 3 APIs are used: `Assert.AreEqual`, `IsTrue`, `That(bool, string)`, `Throws`, `CollectionAssert.AreEqual`/`Contains` and `StringAssert.Contains`/`DoesNotContain`. There is no `Assert.Multiple` and no newer constraints, so the tests are compatible with Unity's nunit 3.5. The method group `Select(FlowTexts.ActionGlyph)` is fine in C# 9.
- Not verified here: actual Unity compilation. The task's verification list has no `unity-*` item, and the next Unity CI run will confirm it.

## Findings
None blocking. Notes, with no change required:
- N-1 `FlowScenarios.Fingerprint` (Tests/EditMode/Core/Flow/FlowScenarios.cs). `"last="` uses `CommandResult.ToString()`, which records only outcome and reason, not piece or settlement contents. Those contents are covered indirectly by the `lines=` part, so the impact is negligible.
- N-2 The three unspecified-point decisions are recorded only in the task notes and in the `FlowTexts` XML remarks. Recommendation: the Orchestrator should copy them into `Docs/DECISIONS.md` or the C-021 change history, so that ORIGAMI-005 (Unity view) relies on documented behaviour.
