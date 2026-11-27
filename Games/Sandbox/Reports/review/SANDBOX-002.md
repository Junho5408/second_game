# Review: SANDBOX-002 점수 카운터 인터페이스 설계 (architecture docs + Core skeleton)

- Reviewer: code-reviewer
- Base commit: 0075b01a028a862dabf140eebf5403a59a15c77f
- Reviewed commits: c15fbde (contract + skeleton), 1dbd773 (status)
- **Result: APPROVED_WITH_WARNINGS**

## Changed files
- Games/Sandbox/Docs/tech/ARCHITECTURE.md (+42/-4)
- Games/Sandbox/Docs/tech/CODING_CONVENTION.md (+4/-2)
- Games/Sandbox/Docs/tech/DATA_STRUCTURE.md (+33)
- Games/Sandbox/Docs/tech/INTERFACE_CONTRACTS.md (+147)
- Games/Sandbox/Unity/Assets/Game/Core/Scoring.meta (new folder meta)
- Games/Sandbox/Unity/Assets/Game/Core/Scoring/ScoreCounter.cs (+.meta)
- Games/Sandbox/Unity/Assets/Game/Core/Scoring/ScoreRules.cs (+.meta)
- Games/Sandbox/Unity/Assets/Game/Core/Scoring/ScoreSnapshot.cs (+.meta)
- Games/Sandbox/Tasks/SANDBOX-002.json (status/result/notes/base_commit only)

## Commands run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-002` | OK |
| `bash studio/scripts/verify_core.sh Sandbox` | PASS (1/1; skeleton compiles in Core.Tests, net8.0 / LangVersion 9) |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` | OK (3 .cs metas + folder meta, unique GUIDs) |
| scratch `dotnet build` of `Core/**/*.cs` for `netstandard2.1`, `LangVersion 9`, `TreatWarningsAsErrors` (outside repo) | Build succeeded, 0 warnings, 0 errors |
| `grep -rn UnityEngine Unity/Assets/Game/Core` | no matches (asmdef also has `noEngineReferences: true`) |

## Checklist
1. **Task AC**
   - "INTERFACE_CONTRACTS.md에 ScoreCounter 계약(시그니처·동작) 기록": met. C-001..C-004 cover signatures, behavior tables, boundaries, determinism, threading, int range.
   - "Core/Scoring에 계약을 나타내는 C# 골격(컴파일 가능)": met. Bodies throw `NotImplementedException("SANDBOX-003")`; `ScoreSnapshot` is complete (pure value type, permitted by C-003).
2. **F-001 coverage (rule/AC -> contract), checked one by one**
   - AC-01 ctor; AC-02..10 Press rules + `MultiplierForCombo` table (<=4->1, 5-9->2, 10-19->3, >=20->4); AC-04/05 `<=` 1.0 with no epsilon; AC-11 expiry only `> 1.0` and only for finite `now`; AC-12 `time >= lastPress` else ignored; AC-13 equal time valid, gap 0; AC-14/22 non-finite press changes nothing incl. lastPress; AC-15..17 Reset() takes no time, idempotent, next press is first press; AC-18 no clock/random/static mutable state; AC-19 `now < lastPress` not expired; AC-20 NaN/+Inf/-Inf query -> last-press state, new counter -> Initial; AC-21 negative finite valid. "조회 시각 규약" and R-006 (int, unchecked) are consistent.
   - No contradictions with GAME_RULES R-001..R-006 found. Constants match "기본 수치". Edge: finite `time - lastPress` can only overflow to +Inf (never NaN), which the contract correctly treats as a break / expiry.
3. **Signatures in code vs INTERFACE_CONTRACTS.md**: exact match for C-001, C-002 (const values 1 / 1.0 / 4), C-003 (`IEquatable` via `using System`, equality on all three fields, ==/!=, `ToString` "Score=6 Combo=5 x2", `Initial = (0,0,1)`). No extra public members. Namespace `Sandbox.Core.Scoring` per updated CODING_CONVENTION.
4. **Module boundaries**: Scoring lives inside the existing `Sandbox.Core` asmdef (no new asmdef, no package change). Dependency direction UI -> Gameplay -> Core is documented and acyclic.
5. **C-004 implementability within SANDBOX-004 file_scope** (`Scripts/Gameplay/`, `Scripts/UI/`, `Tests/PlayMode/Scoring/`): yes. `Sandbox.Runtime` asmdef (Scripts/) already references `Sandbox.Core`; `Sandbox.Tests.PlayMode` asmdef (Tests/PlayMode/) already references Runtime+Core, so tests under `Tests/PlayMode/Scoring/` compile without asmdef edits. Bootstrap.cs left untouched via self-creating `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` - same pattern Bootstrap already uses. `com.unity.modules.uielements`, `ugui`, `imgui` are present. Testability via `SubmitPress`/`SubmitReset`/`Current` is sound in principle; see W-1..W-3 for gaps.
6. Perf/memory, abstractions, breakage: struct snapshot (no per-frame alloc), no events, no new abstractions; existing code untouched.

## Findings

### W-1 (warning) PlayMode tests will race against the real clock in Update()
- Location: INTERFACE_CONTRACTS.md C-004, "프레임 처리 순서" step 4 and "테스트 가능성 (PlayMode)".
- Problem: `Update()` overwrites `Current = counter.GetState(Time.unscaledTimeAsDouble)` every frame. A PlayMode test that calls `SubmitPress(0.0)` (synthetic times like the Core ACs) and then `yield return null` to let the view refresh will see `Current` recomputed at the real clock (several seconds into play mode) -> combo 0 / x1 (expired), while score is kept. Combo/multiplier assertions on screen text then fail or become timing-dependent.
- Impact: Flaky or failing SANDBOX-004 PlayMode tests; the developer may misread it as a Core bug.
- Recommendation: Add to C-004: "PlayMode tests that yield frames must pass `t = Time.unscaledTimeAsDouble` (or offsets within the 1.0s window from it) to SubmitPress, or assert `Current` immediately after SubmitPress without yielding." Optionally allow an injectable time source (e.g. internal `Func<double>`), but that is a public-surface choice for the architect.

### W-2 (warning) Initial value of `Current` and view update order not specified
- Location: INTERFACE_CONTRACTS.md C-004 recommended signatures; ARCHITECTURE.md F-001 diagram.
- Problem: (a) `Current` is a `ScoreSnapshot`; if not initialized it is `default` = (0,0,0), which violates the C-003 invariant (Multiplier 1..4) and would display "x0" on the first frame / before the first Update. (b) Relative order of controller `Update` and `ScoreView` refresh is unspecified, so the view can lag one frame; `SubmitPress`/`SubmitReset` also do not say which `now` they use for the refresh (presumably `time`, and any value for Reset).
- Impact: Possible "x0" display and off-by-one-frame text assertions in PlayMode tests.
- Recommendation: State `Current` starts as `ScoreSnapshot.Initial`; `SubmitPress(time)` refreshes with `GetState(time)`; view reads in `LateUpdate` (or controller pushes to the view) and tests yield at least one frame before reading text.

### W-3 (warning) Legacy Input Manager availability is assumed, not verified
- Location: INTERFACE_CONTRACTS.md C-004 "입력 백엔드"; Unity/ProjectSettings/ (only ProjectVersion.txt is committed).
- Problem: C-004 mandates `UnityEngine.Input.GetKeyDown`/`GetMouseButtonDown`. These throw `InvalidOperationException` every frame if Player "Active Input Handling" is set to the Input System package only. ProjectSettings.asset is not in the repo, so the value is whatever Unity 6000.3 generates by default; nothing in this task proves it is "Input Manager (Old)" or "Both".
- Impact: If wrong, every frame logs an exception, and Unity Test Framework fails PlayMode tests on unhandled error logs - even tests that only use SubmitPress.
- Recommendation: Architect/Orchestrator confirm the generated `activeInputHandler` in the first `unity-compile`/`unity-playmode` CI run (or commit ProjectSettings via an architect task). Not blocking for SANDBOX-002 because no Unity code exists yet.

### N-1 (note) Duplicate guard placement vs tests
- Location: INTERFACE_CONTRACTS.md C-004 "생성".
- Problem: If the duplicate guard is implemented as a static singleton in `Awake` that destroys extra instances, a test that `AddComponent<ScoreCounterController>()` for an isolated instance would have it destroyed (the auto-created one already exists).
- Recommendation: Clarify that the guard lives in the static RuntimeInitializeOnLoadMethod only, or that tests should use the auto-created `"[ScoreCounter]"` instance and call `SubmitReset()` first.

### N-2 (note) UI Toolkit from code needs a theme
- Location: C-004 "표시".
- Problem: A code-created `PanelSettings` without a `themeStyleSheet` renders text poorly/not at all and logs a warning. A `.tss` under `Scripts/UI/` is within scope, as are uGUI `Text` with `LegacyRuntime.ttf` or IMGUI, so this is implementable; just worth a hint to SANDBOX-004.

## Verdict
Contract is complete and consistent with F-001 / R-001..R-006; Core skeleton matches it exactly, is engine-free and compiles for netstandard2.1 / C# 9; metas present; scope clean. Warnings concern the Unity-side contract (C-004) and should be addressed in INTERFACE_CONTRACTS.md before or at the start of SANDBOX-004 (W-1, W-2) and verified in CI (W-3).
