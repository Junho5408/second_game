# Review: SANDBOX-004 점수 카운터 화면 연결 (ScoreCounterController + IMGUI ScoreView + PlayMode tests)

- Reviewer: code-reviewer
- Base commit: b20b8304e40a9b5bae974a21aa09f0dd8a2efa58
- Reviewed commits: b5d12a4 (implementation + tests), e6bff8c (status), 23d8f65 (orchestrator: IMGUI decision, docs only)
- **Result: APPROVED_WITH_WARNINGS**

## Changed files
- Games/Sandbox/Unity/Assets/Game/Scripts/Gameplay/ScoreCounterController.cs (+.meta, new, 109 lines)
- Games/Sandbox/Unity/Assets/Game/Scripts/UI/ScoreView.cs (+.meta, new, 86 lines)
- Games/Sandbox/Unity/Assets/Game/Tests/PlayMode/Scoring.meta (new folder meta)
- Games/Sandbox/Unity/Assets/Game/Tests/PlayMode/Scoring/ScoreCounterControllerTests.cs (+.meta, new, 181 lines)
- Games/Sandbox/Tasks/SANDBOX-004.json (status/result/notes)
- Games/Sandbox/Docs/DECISIONS.md (Orchestrator commit, not developer scope)

## Commands run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-004` | OK |
| `bash studio/scripts/verify_core.sh Sandbox` | PASS (87/87, 0 failed) |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` | OK |

Unity compile / PlayMode / build were NOT run (no Unity here). Everything below about Unity behaviour is static review only.

## Checklist
1. **Spec / AC compliance** - met.
   - AC "스페이스/클릭 입력 시 Core 호출": `Update` reads `Time.unscaledTimeAsDouble` once, then `ProcessFrame(t, GetKeyDown(R), GetKeyDown(Space), GetMouseButtonDown(0))`. Order is reset, Space press, click press (two separate `Press(t)` with the same t), then `Current = GetState(t)` on every frame, including frames without input (R-004 expiry shown). Matches C-004 "프레임 처리 순서" exactly. Key-down only, so holding is not repeated input.
   - AC "점수, 콤보, 배율 표시": `ScoreView.Format` -> `"Score: {0}\nCombo: {1}\nx{2}"` with invariant culture; multiplier shown as `x{Multiplier}` per C-004.
   - AC "PlayMode 테스트로 입력->점수 반영": 12 tests cover initial state, first press, 1.0 boundary kept, >1.0 break, x2 at combo 5, NaN/+Inf/backwards ignored, reset then earlier press is first press (AC-16 analogue), Space+click same frame = 2 presses (AC-13), reset-before-press same frame = (1,1,1), no-input expiry at exactly 1.0 vs 1.5 (AC-11 analogue), Format, frame-driven view update, single runtime instance with view. I re-derived every expected value against C-001; all correct.
   - R-1: synchronous tests assert immediately after SubmitPress/ProcessFrame (no frame in between); the frame test bases times on `Time.unscaledTimeAsDouble`. Satisfied (see W-1 for one assertion).
   - R-2: `Current` initialised to `ScoreSnapshot.Initial`; view reads in `LateUpdate`. Satisfied.
   - W-1 of SANDBOX-003 / DECISIONS: boundary tests use 10.0, 10.5, 11.0, 11.5, 12.0, 12.5 - all exactly representable. Satisfied.
   - IMGUI: accepted exception in DECISIONS.md (2026-11-27). `com.unity.modules.imgui` is in manifest.json. `GUI.skin` is only touched inside `OnGUI` and the `GUIStyle` is cached, so no per-frame allocation besides IMGUI's own.
2. **Architecture / conventions** - namespaces `Sandbox.Runtime.Gameplay` / `Sandbox.Runtime.UI` per ARCHITECTURE; dependency direction UI -> Gameplay -> Core.Scoring; Gameplay never references UI. Controller is sole owner of `ScoreCounter` (`private readonly`). Bootstrap untouched; creation via own `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` with duplicate protection (static instance + `GameObject.Find`), independent of Bootstrap ordering, as C-004 requires.
3. **Dependencies / boundaries / compile surface** - no new asmdef or package. Both scripts are under `Scripts/` -> `Sandbox.Runtime` asmdef (references Sandbox.Core). Test folder is under `Tests/PlayMode/` -> `Sandbox.Tests.PlayMode` asmdef, which references `Sandbox.Runtime`, `Sandbox.Core`, the TestRunner assemblies and `nunit.framework.dll`, so `Sandbox.Runtime.Gameplay`, `Sandbox.Runtime.UI`, `Sandbox.Core.Scoring`, `UnityEngine.TestTools` all resolve. Static name checks done: `Object` in the test resolves to `UnityEngine.Object` (no `using System;`, no `Sandbox.*.Object` type); `DontDestroyOnLoad` is the inherited static; `Time`, `Input`, `KeyCode`, `GUI`, `GUIStyle`, `Rect`, `Color` have no Sandbox-namespace clashes; `Text`/`Source` on a MonoBehaviour hide nothing. Two classes each with a `private static void Init()` hook is legal. I found nothing that should fail to compile in 6000.3.
4. **Null / invalid states** - `Instance` and `Source` normalise Unity fake-null to real null; `OnDestroy` clears the static only if it is this instance; `EnsureInstance` uses Unity `==` so a destroyed stale static (e.g. Enter Play Mode Options without domain reload) is recreated. `ScoreView.Refresh` keeps the last text when the source is gone. Invalid times are delegated to Core, which ignores them.
5. **Performance / memory** - per frame: one `GetState` (struct) and one `string.Format` in LateUpdate (small string allocation per frame; acceptable for a sandbox, could be cached on change - not required).
6. **Abstractions / duplication** - see "Extra public API" below. No duplicated rule logic; no rule numbers hard-coded in Runtime.
7. **Breakage** - only new files. The persistent `[ScoreCounter]` object now exists during all PlayMode tests (including BootstrapTests); it polls input that is always false in batchmode and logs nothing, so it does not interfere with other tests - unless legacy Input throws (R-3, W-3).
8. **Tests** - sufficient for the Core call wiring and ordering. Not covered: the `Update` -> `Input.*` key mapping itself (cannot be simulated without the Input System package; accepted by C-004) and `OnGUI` drawing (not called reliably in batchmode).

## Extra public API beyond C-004 (judgement)
C-004 lists names/signatures as "권장" (recommended), and its "public 시그니처는 고정" rule targets the Core types, so additions are allowed, but they should be recorded in the contract.
- `ProcessFrame(double, bool, bool, bool)`: **justified.** It is the only way to test the C-004 frame order (reset first, Space+click = 2 presses at the same t, expiry on input-less frames) without simulating real input. Note it calls `_counter` directly rather than `SubmitPress`/`SubmitReset` as C-004's prose suggests; behaviour is equivalent (one `GetState` per frame instead of several) and arguably closer to the contract's single-`t`-per-frame rule.
- `EnsureInstance()` / `Instance` / `ObjectName`: **justified.** They make creation idempotent and independent of the order of the two AfterSceneLoad hooks (Unity does not guarantee order). `Instance` is only used by tests today; acceptable.
- `ScoreView.Format`, `Text`, `Source`, `Bind`: **justified** - pure formatter and read-only state give a deterministic test of "화면 텍스트" without IMGUI.
None of these leaks the `ScoreCounter` itself; ownership stays single.

## Findings

### W-1 (warning) View_ShowsCurrentAfterFrame relies on frame timing; comment states the wrong phase order
- Location: `Tests/PlayMode/Scoring/ScoreCounterControllerTests.cs`, `View_ShowsCurrentAfterFrame`, `yield return null; // controller Update, then view LateUpdate` and `Assert.AreEqual(ScoreView.Format(c.Current), view.Text);`
- Problem: a UnityTest coroutine resumes after `yield return null` **after Update and before LateUpdate** of the next frame. So at the assert, `c.Current` is the value from this frame's `Update` (GetState(t1)), while `view.Text` is from the *previous* frame's LateUpdate (Format(3,3,1)). They are equal only if `t1 - t0 <= 1.0`. The class comment claims frame tests "only assert values that do not depend on frame duration", which is not true for this line.
- Impact: passes in practice (batchmode frames are milliseconds), but would flake on a >1 s hitch (first frame after domain reload, GC, slow CI runner), and the test does not actually prove that LateUpdate ran after this frame's Update.
- Correction: `yield return new WaitForEndOfFrame();` (or yield twice) before comparing Text to Format(Current), or keep only the duration-independent `StringAssert.StartsWith("Score: 3\n", view.Text)` and fix the comment.

### W-2 (warning) C-004 does not document the added public surface
- Location: `Docs/tech/INTERFACE_CONTRACTS.md` C-004 (architect-owned, outside this task's scope).
- Problem: `ProcessFrame`, `EnsureInstance`, `Instance`, `ObjectName`, `ScoreView.Format/Text/Source/Bind` and the display string format exist only in code and task notes. CODING_CONVENTION still says "UI는 UI Toolkit" with no pointer to the DECISIONS exception.
- Impact: later tasks or QA may treat these as accidental API; contract and code drift.
- Correction: Orchestrator to have the architect add these to C-004 (plus a note in CODING_CONVENTION referencing the IMGUI decision). No developer change needed.

### W-3 (warning) Legacy Input dependency still unverified (R-3)
- Location: `ScoreCounterController.Update` (`Input.GetKeyDown`, `Input.GetMouseButtonDown`).
- Problem: the repo has no `ProjectSettings/ProjectSettings.asset`, so `activeInputHandler` is whatever Unity 6000.3 generates. If it resolves to "Input System Package (New)", every `Update` of the persistent `[ScoreCounter]` throws `InvalidOperationException`; Unity Test Framework fails tests on unhandled error logs, so **all** PlayMode tests (including BootstrapTests) would fail, not just these.
- Impact: possible red unity-playmode run unrelated to logic.
- Correction: none in this task; if CI shows the exception, follow R-3 (architect task to commit activeInputHandler = Input Manager or Both). QA should look specifically for this in the CI log.

### I-1 (info) Minor
- `EnsureInstance` adopt-existing branch (`GameObject.Find` hit) does not call `DontDestroyOnLoad`; harmless with a single scene, only matters if someone places a `[ScoreCounter]` object in a scene.
- `SubmitReset` reads `Time.unscaledTimeAsDouble` only to call `GetState`, which is always Initial after Reset; harmless.
- Per-frame `string.Format` allocation in `ScoreView.LateUpdate`; fine at this scale.

## Unverified (needs CI)
unity-compile, unity-playmode, unity-build. The task note correctly says only a stub-UnityEngine dotnet build was done.

## Re-review (fix pass 1)

- Reviewed commit: 7d26984 (test file + task status only). Also looked at what else changed since the first review (`git diff 0b28b76..HEAD`): QA report, orchestrator commits 20972b3 (task notes, SANDBOX-005 docs task) and 0ade70a (`.github/workflows/unity.yml` pin). None of those is a `SANDBOX-004:` commit or under this task's scope, so they are not part of this re-review. `Scripts/Gameplay/` and `Scripts/UI/` are unchanged since b5d12a4 (empty diff).
- **Result: APPROVED_WITH_WARNINGS** (W-1 and BUG-SANDBOX-004-1 resolved; W-2 and W-3 are still open, both outside this task's scope)

### Commands run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-004` | OK |
| `bash studio/scripts/verify_core.sh Sandbox` | PASS (87/87, 0 failed) |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` | OK |

Unity was still not run. The frame-order reasoning below comes from reading the code only.

### BUG-SANDBOX-004-1 / W-1: resolved
`View_ShowsCurrentAfterFrame` now:
1. presses 3 times at `past = Time.unscaledTimeAsDouble - 10.0`, then checks synchronously that `Current == (3,3,1)` and that the formatted text is `"Score: 3\nCombo: 3\nx1"`;
2. yields twice;
3. asserts `Current == (3,0,1)` and `view.Text == "Score: 3\nCombo: 0\nx1"` exactly.

Why the result no longer depends on frame timing:
- `Time.unscaledTimeAsDouble` never decreases, so every later controller `Update` uses some `t >= t0`, and `t - past >= 10 > 1.0`. By C-001 `GetState`, the snapshot is therefore always (3,0,1), however long the frames take. Floating-point error in `t - (t0 - 10)` is many orders of magnitude below the 9 s margin.
- `past` can be negative if the player has been running for less than 10 s. C-001 says negative finite times are valid presses ("음수 유한 시각도 유효"), so this is fine.
- Possible text values over time: Bind gives `Initial`. If Unity runs a LateUpdate in frame 0 for the newly added component, it gives (3,3,1), because no controller Update has run since the presses. From frame 1 on, Update runs before LateUpdate, so every refresh gives (3,0,1). After the second yield, frame 1's LateUpdate has run, so the text is (3,0,1) whatever happened in frame 0. That text differs from both earlier possible values, so the test now proves that LateUpdate read the result of a controller Update. This was the missing evidence in W-1.
- Yielding twice also removes the QA "secondary uncertainty" about whether a component added in the coroutine phase gets a LateUpdate in that same frame.
- `WaitForEndOfFrame` was avoided, as QA recommended for batchmode.

`RuntimeInstance_IsCreatedOnce_WithView` now yields a second time and asserts exact `Initial` for both `Current` and `Text`. This is deterministic: no code presses on the runtime instance and batchmode has no input, so every frame gives `Initial`. I checked the other tests: none touches `ScoreCounterController.Instance` or the `[ScoreCounter]` object. They all use their own `TestScoreCounter` GameObject, which TearDown destroys.

### Comments: now correct
- The class summary now says what Unity actually does: after `yield return null`, the coroutine resumes after Update and before LateUpdate. It also explains why frame tests yield at least twice and only assert states that are the same at every real time `>= t0`. The old false claim ("only assert values that do not depend on frame duration") is gone, and the test is now built to match the new wording.
- The inline comments `frame 1: resumes after Update (Current = (3,0,1)), before LateUpdate` and `frame 1 LateUpdate refreshed Text; frame 2 Update ran again` describe the phase order correctly.
- The added assumption "no real keyboard/mouse input during the run (always true in batchmode)" is accurate. It also applies to the test-owned controller, which polls `Input` in `Update`. It only matters if someone runs the tests interactively in the Editor and presses keys.

### Regression check
- No production code changed. The 10 synchronous tests are unchanged.
- The test asserts were made stricter, not weaker: an exact expected string replaced `StartsWith` plus a comparison that mixed values from different frames. No test was removed or skipped.
- Test file `.meta` files are unchanged, and check_meta reports OK.
- The new code uses no new APIs or `using` directives, so the compile surface is the same as in the first review.

### Remaining findings
- W-2 (C-004 does not document the added public surface): still open. It needs an architect/Orchestrator action. SANDBOX-005 appears to be the docs task for it; I did not review it here.
- W-3 (legacy `Input` / `activeInputHandler`): still open. It can only be settled by a CI log.
- I-2 (info, new): the determinism argument assumes Unity's usual behaviour that a MonoBehaviour added during frame N gets its first `Update` in frame N+1. This is standard Unity behaviour (Start runs before the first Update in the next frame), so no change is needed. If a CI run ever shows `Text` = (3,3,1) at the final assert, this assumption is the first thing to check.

### Unverified (needs CI)
unity-compile, unity-playmode, unity-build.
