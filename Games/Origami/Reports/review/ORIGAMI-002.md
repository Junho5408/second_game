# Review ORIGAMI-002: Origami architecture and interface design

- Reviewer: code-reviewer
- Base commit: 4e0718e. Reviewed commits: 47329c2, 3b832d8 (task commits). b176186 is an orchestrator commit and was not part of this review.
- **Result: APPROVED_WITH_WARNINGS**

## Verification

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-002` | `OK` |
| `bash studio/scripts/verify_core.sh Origami` | `== Origami: PASS ==`, 41 passed / 0 failed (netstandard2.1 / C# 9 build + dotnet test) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| `grep -rn UnityEngine Core/` | no matches; `Origami.Core.asmdef` has `noEngineReferences: true` and no references |

## Checklist

1. **Spec / AC coverage.** All three task ACs are met. Docs/tech has 4 files covering module boundaries, the public Core API (commands, reject reasons, snapshots, settlement, ending), the data definition method (code catalog), the Unity screen structure and the key mapping. Core has a compilable skeleton. ARCHITECTURE §9 proposes a work split whose file scopes do not overlap (ORIGAMI-003/004/005 are serial). I traced every rule R-001..R-015 and every F-001..F-007 AC to a contract:
   - **Rejection order (C-010 table vs R-004/R-005/R-015).** It matches exactly:
     - Start: RunEnded → FoldInProgress → UnknownModel (null/""/unknown) → Locked → NoAttemptsLeft → NotEnoughMoney (strict `<`, so money equal to the cost is allowed).
     - Fold: RunEnded → NoFold → InvalidAction (`!FoldActions.IsValid`; not counted as a mistake).
     - Hint and Abandon: RunEnded → NoFold.
     - EndDay: RunEnded → FoldInProgress.
     - NewRun is always accepted.
     - The `RejectReason` XML doc repeats the same orders.
     - F-002 AC-03/AC-04 (`Start(null)` while folding → FoldInProgress; locked while folding → FoldInProgress) follow from this order.
   - **Bankruptcy check (C-010 `money < min unlocked paper cost` vs R-013 "money is 0").** The two checks give the same result in every reachable state:
     - (a) `plane` has no prerequisite, so `Unlocking.IsUnlocked` is true for every count.
     - (b) Completions only grow within a run, so no model is ever locked again.
     - (c) Every catalog paper cost is an integer ≥ 1, and plane costs exactly 1 (pinned by `CatalogContractTests.Model_CostsPricesAndUnlockChain`).
     - (d) `money ≥ 0` is a C-010 invariant.
     - So the minimum unlocked cost is always exactly 1, and `money < 1` holds exactly when `money == 0`. R-013 itself states both forms ("가장 싼 종이값보다 적으면, 즉 돈이 0이면"), so there is no contradiction.
     - The check runs only after day+1 on days 1–6. Day 7 ends as Completed with no bankruptcy check, and on bankruptcy the day stays at the new value. This matches R-012/R-013 and F-001 AC-06/AC-08.
   - **Folding.**
     - A wrong action keeps the hint reveal; advancing a step clears it (F-003 AC-07/AC-10).
     - Repeating Hint on the same step is accepted with no count change (R-006).
     - The third mistake ruins the paper.
     - Unlocks are a pure function of the counts, so they apply immediately (R-010, F-004 AC-01).
   - **Settlement.**
     - Popularity is taken from the settled day (F-005 AC-06).
     - `TotalPaperCost` is added on Start (F-005 AC-07).
     - `NextPopularModelId` is null when the run ends (F-007 screen 5).
   - **Ending.** The E-01..E-05 conditions, their inclusive bounds, and the rule that only the three inputs are used all match R-014 and F-006.
   - **Flow (C-020) against F-007.**
     - The Esc confirm step works: Esc opens it, Esc again cancels, Enter confirms.
     - While the confirm window is open, arrows, Space and H are ignored.
     - Picking a model and ending the day are ignored while folding.
     - Locked models open no preview (A-7).
     - The bankrupt settlement shows `BankruptNotice` instead of the next popular model.
     - The Result screen has Restart, which returns to the F-001 AC-01 state.
   - **Catalog values.** All values match CONTENT_SPECS exactly: 5 models (ID, name, steps, paper cost, base price, prerequisite), the popular table plane/boat/frog/boat/crane/frog/dragon, and the ending IDs, priorities, names and texts. I compared the texts by hand.
2. **Architecture / conventions.**
   - Core uses namespace = folder, one public type per file and immutable data types.
   - There is no clock, no randomness and no static mutable state.
   - Rule numbers live in `RuleConstants`. The dependency direction Flow → Run → Rules → Content is respected (`RunEndReason` is in Rules for this reason).
3. **Signatures against INTERFACE_CONTRACTS.** The C-001..C-011 and C-020/C-021 signatures in the source match the document. `SurfaceContractTests` pins the surfaces of OrigamiRun, Grading, Pricing, Unlocking, EndingRules, OrigamiFlow and FlowTexts, plus the enum member lists.
4. **Null and invalid states.**
   - `FindModel`/`IndexOf` handle null, "" and wrong case.
   - `CompletionCounts.Get` never throws.
   - The snapshot and settlement constructors copy their lists and check for null.
   - `default(FoldAction)` is invalid.
5. **Performance.** Nothing to report: the data is tiny, and snapshots are allocated once per command.
6. **Abstractions.** They are appropriate. There are no events, only command → result plus polling, and no separate unlock state.
7. **Risk to existing code.** None. `Bootstrap.cs`, the asmdefs, `Packages/` and `ProjectSettings/` are untouched.
8. **Tests.** 41 contract tests cover catalog values, constants, enums, value types and the API surfaces. Rule behaviour tests are correctly left to ORIGAMI-003/004.

### Unity contract (C-030/C-031): PlayMode testability
- C-030/C-031 follow the Sandbox pattern that was already proven in CI: `ObjectName`, an idempotent `EnsureInstance`, true-null `Instance`, test controllers that never replace `Instance`, and `OnDestroy` release.
- Reading input (`ReadKeys`) is separate from processing it (`ProcessFrame(FlowKeys)`), and `OrigamiView` exposes `Refresh()` and `Lines`. A PlayMode `[Test]` can therefore do `AddComponent` (Awake runs synchronously) → `ProcessFrame`/Flow → `Refresh()` → assert `Lines` with no frame waits.
- Origami has no time-based rules, so the Sandbox BUG-SANDBOX-004-1 class of problem (assertions that depend on frame order) does not apply. CODING_CONVENTION's PlayMode section states this explicitly. The contract can be implemented and tested.

### Decisions A-1..A-7
- **A-1 (code catalog), A-2 (IMGUI), A-3 (engine-free flow and texts in Core).** Sound. They are justified by netstandard2.1 / no JSON parser, the lack of UI Toolkit assets, and dotnet testability of F-007.
- **A-4 (legacy Input, one key per frame).** Sound. It prevents Esc+Enter in the same frame from skipping the confirm step.
- **A-5 (OS font).** Sound, and its limit is stated honestly: CI cannot verify glyphs, so it falls to a playtest.
- **A-6 (Enter/Esc in Preview, Notice and Settlement).** A minimal extension that does not conflict with F-007.
- **A-7 (lock gate in Flow).** Sound.

## Findings (warnings, none blocking)

| ID | Location | Problem | Impact | Recommendation |
|---|---|---|---|---|
| W-1 | `Docs/DECISIONS.md` (orchestrator) | Only A-1..A-5 are recorded. A-6 (extra key bindings not in F-007) and A-7 are missing, although ARCHITECTURE §8 asks for all decisions to be copied. | A-6 extends the spec. If it is not recorded, QA or the designer may see Enter/Esc outside the folding screen as unexpected behaviour. | The Orchestrator adds A-6 and A-7 to DECISIONS.md. |
| W-2 | `ARCHITECTURE.md` §9 / ORIGAMI-003 `file_scope` | `Core/Rules/` and `Core/Run/` contain files that are already final contracts: `RuleConstants`, `CompletionCounts`, `RunEndReason` (C-004/C-005/C-009 enum) and all C-011 types. Nothing says they are frozen. The contract tests pin only part of them (for example, the `CompletionCounts` surface is not checked by reflection). | ORIGAMI-003 could change a "completed" contract type without a contract-change request. | Add a note to the ORIGAMI-003 task that the C-004/C-005/C-011 files and the enum are read-only unless a contract change is requested, or extend `SurfaceContractTests` to cover them in a later architect task. |
| W-3 | `Tests/EditMode/Core/Contracts/CatalogContractTests.cs` `Endings_AreFiveInPriorityOrder` | Ending texts are only checked to be non-empty, not compared with CONTENT_SPECS. The current values match. | A typo would go unnoticed. This is acceptable because CONTENT_SPECS marks the texts as examples that the content-designer may revise. | Optional: pin the texts, or record in DATA_STRUCTURE D-003 that they are intentionally left unpinned. |
| W-4 | `Core/Run/CommandResult.cs` `Rejected(RejectReason)` | The doc says the reason "must not be None", but the code does not enforce it. `Rejected(None)` gives a rejected result with no reason, so the Flow would show an empty message. | Low. A programmer error would be silent. | In ORIGAMI-003, never pass None. Optionally have the factory throw `ArgumentException` for None. This is an architect change because the type is a contract. |
| W-5 | C-031 / A-5 (ORIGAMI-005 guidance) | `Font.CreateDynamicFontFromOSFont` and the GUIStyle are created lazily in `OnGUI`. In batch-mode CI, OnGUI may run with no OS CJK font. Any exception or `LogError` there would fail PlayMode tests through unhandled-log detection. | It could make CI unreliable even though no test checks fonts. | In ORIGAMI-005, make font creation failure-tolerant: fall back to the default font and emit no error logs. PlayMode tests must not depend on OnGUI. |
| W-6 | C-020 `ProcessKeys` | The highest-priority key is used even when the current screen ignores it. For example, Enter+↑ in the same frame on Folding drops the ↑. | Rare lost input. It is documented and intentional (A-4). | No change needed. QA should not report it as a bug. |

Info: the name `FoldSnapshot.CorrectSteps` could be read as "the answer sequence". Its XML doc clarifies that it means the steps already folded correctly. This is not a finding.
