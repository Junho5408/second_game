# QA ORIGAMI-002: Origami architecture and interface design

- QA: qa-engineer · Date: 2026-11-27
- Base: 4e0718e · Task commits 47329c2, 3b832d8 · Review 9f6b4e4 (APPROVED_WITH_WARNINGS) · Orchestrator a2029ec (A-6/A-7, task notes)
- **Result: PASS → COMPLETED** (verification items `core-tests`, `review` only; no `unity-*` items)

## Verification

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | `== Origami: PASS ==`, Passed 55 / Failed 0 (41 contract tests + 14 QA tests) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| `grep -rnE "UnityEngine\|DateTime\|Random\|Stopwatch" Core/` | no matches |
| Independent script (scratchpad `verify.py`): parses CONTENT_SPECS.md tables and string-matches every `OrigamiCatalog.cs` entry; recomputes the price table and balance targets | all OK (below) |
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-002 --role qa` | `OK` |

## Acceptance criteria

| AC | Result | Evidence |
|---|---|---|
| AC-1 Docs/tech 4 files: module boundaries, Core public API (commands, reject reasons, state query, settlement, ending), data definition (5 models, popular table, endings), Unity screen structure + input mapping | PASS | ARCHITECTURE §1–§6 (modules, dependency direction, screens, key table), INTERFACE_CONTRACTS C-001..C-031, DATA_STRUCTURE D-001..D-003, CODING_CONVENTION (incl. PlayMode rules). |
| AC-2 Compilable C# skeleton in Core, netstandard2.1, no UnityEngine | PASS | Core.Build (netstandard2.1 / C# 9) builds; `Origami.Core.asmdef` `noEngineReferences: true`; `SurfaceContractTests.Core_HasNoUnityReference` passes; grep clean. |
| AC-3 Work split with non-overlapping file scopes | PASS | ARCHITECTURE §9; Tasks ORIGAMI-003/004/005 scopes are disjoint (see below). |

## Catalog vs CONTENT_SPECS (independent check)

- Models: all 5 rows (order, ID, name, step count, step sequence, paper cost, base price, prerequisite) match exactly.
- Popular table: plane, boat, frog, boat, crane, frog, dragon — matches.
- Endings: ID, priority, name and **result text** of E-01..E-05 match character for character.
- Price table: all 30 CONTENT_SPECS values equal `floor(base × gradePct × demandPct / 10000)` with catalog base prices.
- Balance targets (GAME_RULES/CONTENT_SPECS): F-006 AC-10 money sequence recomputed as 70/209/479/749/1019/1289/1709 (paper paid on start, money never negative, unlock chain reachable on day 2); dragon ★2 from day 3 → 1159, ★1 → 719. All match the documented targets.

## Implementability of later tasks from the contracts

- **ORIGAMI-003 (Core rules)**: C-006..C-010 specify every rejection order (matches R-004/R-005/R-015), every accepted transition, settlement fields, bankruptcy timing, ending order and argument exceptions. Every F-001..F-006 AC maps to an observable snapshot/result field (`RunSnapshot`, `FoldSnapshot`, `SettlementResult`, `CommandResult`). No guessing needed.
- **ORIGAMI-004 (Core flow)**: C-020 screen × method table, key table and priority, return-value and Message rules; C-021 fixes the tested string formats and line contents for every screen. F-007 AC-01..06/08/09 and F-006 AC-11 are expressible as text-level assertions on `ScreenLines` / `Snapshot`.
- **ORIGAMI-005 (Unity)**: C-030/C-031 give names, idempotent instance rules, `ProcessFrame`/`Refresh`/`Lines` for frame-independent PlayMode tests. Existing `Origami.Runtime` asmdef references `Origami.Core`; `Origami.Tests.PlayMode` references `Origami.Runtime` + `Origami.Core`, so no asmdef change is needed. Legacy Input works with default ProjectSettings (only `ProjectVersion.txt` present, same as Sandbox, which passed CI) — relies on Sandbox evidence, re-verified only when ORIGAMI-005 runs in CI.

Minor unspecified points (implementer discretion, not defects; Info):
- `FlowTexts.ScreenLines(null)` / `UnlockCondition(model, null)` behaviour is not stated (programmer error; ArgumentNullException is the natural choice per the exception policy).
- Separator for glyphs in "맞힌 단계:" and Preview is shown only by example (`"↑ ← →"`, space-separated); not a bold/tested format.

## file_scope coverage of later tasks

| Task | Needs to write | Scope covers it |
|---|---|---|
| ORIGAMI-003 | bodies of `Core/Rules/{Grading,Pricing,Unlocking,EndingRules}.cs`, `Core/Run/OrigamiRun.cs`; tests in new `Tests/EditMode/Core/Rules/`, `Run/` (+ folder `.meta`, allowed by check_scope's `.meta` rule) | yes. Read-only contract files are listed in the task notes (W-2). |
| ORIGAMI-004 | `Core/Flow/OrigamiFlow.cs`, `FlowTexts.cs`; tests in `Tests/EditMode/Core/Flow/` | yes |
| ORIGAMI-005 | `Scripts/Gameplay/OrigamiController.cs`, `Scripts/UI/OrigamiView.cs`, `ModelIcons.cs`; `Tests/PlayMode/Flow/` | yes; no asmdef/ProjectSettings/Bootstrap change required |

No overlaps between the three scopes; none includes `Core/Content/` or `Tests/EditMode/Core/Contracts/`. My new folder `Tests/EditMode/Core/Qa/` is outside all three scopes.

## Review warnings follow-up

- W-1: A-6/A-7 present in `Docs/DECISIONS.md` (a2029ec). Resolved.
- W-2, W-4: notes on ORIGAMI-003 present. W-5: note on ORIGAMI-005 present.
- W-3: resolved by QA test `EndingText_MatchesContentSpecs` (texts pinned).
- W-6: intended (A-4), not reported.

## Tests added

`Unity/Assets/Game/Tests/EditMode/Core/Qa/CatalogSpecQaTests.cs` (+ `.meta`, folder `Qa.meta`), 14 test cases:
- `EndingText_MatchesContentSpecs` ×5 — pins E-01..E-05 texts (W-3).
- `PriceTable_FollowsFromCatalogAndConstants` ×5 — 30 spec prices from catalog + RuleConstants (independent of `Pricing`).
- `Catalog_IdsUniqueAndPopularTableUsesCatalogIds`, `UnlockChain_PrerequisitesAreEarlierCatalogModels`,
  `BankruptcyEquivalence_CheapestInitialPaperIsOneAndAllCostsPositive` (pins the assumption behind C-010's bankruptcy check), `StepSequences_UseOnlyValidActions`.

## Bugs

None.

## Untested areas

- Rule/flow behaviour (bodies throw `NotImplementedException`) — belongs to ORIGAMI-003/004.
- C-030/C-031 exist only as documents; Unity compile/PlayMode and Korean font display (A-5) untested until ORIGAMI-005 CI and user playtest.
- Unity EditMode compilation of the new QA test file is not verified here (no `unity-*` item on this task); it uses only NUnit, System.Linq and `Origami.Core`, which the EditMode asmdef references.
