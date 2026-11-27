# QA: SANDBOX-004 점수 카운터 화면 연결 (ScoreCounterController + IMGUI ScoreView + PlayMode tests)

- QA: qa-engineer (cloud-side QA, no Unity available)
- Base commit: b20b830 · tested commits: b5d12a4 (implementation + tests), e6bff8c (status), 23d8f65 (IMGUI decision), 0b28b76 (review APPROVED_WITH_WARNINGS)
- Verification items in the task: `review`, `unity-compile`, `unity-playmode`, `unity-build`
- **Result: FAIL → FIX_REQUIRED** (1 bug: BUG-SANDBOX-004-1, Medium. It is a test defect that can make `unity-playmode` fail when the code is correct.)

## Commands run

| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Sandbox` | PASS, 87/87 |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` | OK |
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-004` | OK |
| GitHub Actions `unity.yml`, branch `claude/serene-fermat-4bwjlo` (list runs / jobs / job log) | latest run 45925656829 failed at license activation: environment failure (see below) |

Unity compile, PlayMode and build were not run, because Unity is not available here. Everything about Unity behaviour below comes from reading the code.

## Acceptance criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| AC-1 | 스페이스/클릭 입력 시 Core ScoreCounter 호출 | PASS (static) / Unity UNVERIFIED | `Update` reads `Time.unscaledTimeAsDouble` once. It calls `ProcessFrame(t, GetKeyDown(R), GetKeyDown(Space), GetMouseButtonDown(0))`, which does Reset, then Press for Space, then Press for click, then `Current = GetState(t)`. This matches the C-004 frame order and input mapping. The `ProcessFrame_*` tests cover the wiring. The real `Input.*` mapping cannot be simulated without the Input System package, so it is untested (accepted by C-004). Whether legacy Input works at runtime is still open (R-3 / review W-3), and only CI can answer it. |
| AC-2 | 현재 점수, 콤보, 배율이 화면 텍스트로 표시 | PASS (static) / Unity UNVERIFIED | `ScoreView.Format` returns `"Score: {0}\nCombo: {1}\nx{2}"` (invariant culture). It is refreshed in `LateUpdate` and drawn with `GUI.Label` in `OnGUI`. The `Format_ShowsScoreComboAndMultiplier` test is deterministic. Nobody can verify the `OnGUI` drawing here or in batchmode. |
| AC-3 | PlayMode 테스트로 입력→점수 반영 검증 | FAIL | Ten synchronous tests are correct and deterministic. I re-derived every expected snapshot against C-001: initial, first press, 1.0 boundary, >1.0 break, x2 at combo 5, NaN/backwards/+Inf ignored, reset then earlier first press, Space+click = 2, reset before press in the same frame, expiry at exactly 1.0 vs 1.5. The frame test `View_ShowsCurrentAfterFrame` is the only test that checks the input → screen text path through real frames, and its main assertion can fail spuriously: see BUG-SANDBOX-004-1. Until it is fixed, a PlayMode result cannot be trusted as evidence for this criterion. |

Other checks:
- R-1: met for the synchronous tests. They assert immediately after `SubmitPress`/`ProcessFrame`.
- R-2: `Current` starts at `ScoreSnapshot.Initial`, and the view reads it in `LateUpdate`.
- Exactly representable times (SANDBOX-003 W-1): all synchronous tests use multiples of 0.5.
- `RuntimeInstance_IsCreatedOnce_WithView`: its last assertion compares `Format(controller.Current)` with `view.Text`, which has the same phase mismatch as BUG-1. Here it is safe: nothing presses on the runtime instance and batchmode input is always false, so `Current` stays `Initial` in every frame. It is not affected by frame duration.

## Review W-1 decision

Review W-1 is a **real defect, not just a style warning**. The test can fail when the production code is correct, and that would turn `unity-playmode` red for the wrong reason. It is logged as BUG-SANDBOX-004-1 and blocks the task: FIX_REQUIRED before Unity CI is relied upon. W-2 (C-004 docs for the added public API) and W-3 (legacy Input / `activeInputHandler`) are not code defects of this task. W-2 is for the Orchestrator/architect. W-3 can only be judged from a CI log.

## Bugs

### BUG-SANDBOX-004-1: `View_ShowsCurrentAfterFrame` depends on frame duration (spurious PlayMode failure)
- Affected feature: F-001 screen display, PlayMode test `Tests/PlayMode/Scoring/ScoreCounterControllerTests.cs` → `View_ShowsCurrentAfterFrame`
- Severity: **Medium**. Gameplay is not affected. The test can fail by chance, which blocks `unity-playmode` (a required verification item) and makes the result useless as AC-3 evidence. The workaround (re-running CI) hides the problem.
- Preconditions: a Unity PlayMode run where the frame after the test body runs starts more than 1.0 s (unscaled) after `t0`. This can happen on a slow or shared CI runner, during a GC or JIT hitch, or on the first frame after a domain reload or asset import.
- Reproduction steps:
  1. The test binds a view, reads `t0 = Time.unscaledTimeAsDouble`, and calls `SubmitPress(t0)` 3 times. `Current` = (3,3,1).
  2. `yield return null`. A UnityTest coroutine resumes in the next frame **after `Update` and before `LateUpdate`**. So in that frame the controller's `Update` has already set `Current = GetState(t1)`, while `view.Text` still holds the value from an earlier `Refresh` (`Format(3,3,1)`).
  3. If `t1 - t0 > 1.0`, then `GetState(t1)` = (3,0,1) (R-004 expiry).
  4. `Assert.AreEqual(ScoreView.Format(c.Current), view.Text)` compares `"Score: 3\nCombo: 0\nx1"` with `"Score: 3\nCombo: 3\nx1"`.
- Expected result: the test passes whenever the code is correct, regardless of frame timing, and it proves that the view refreshed after the controller's update.
- Actual result: the assertion fails whenever the frame gap is more than 1.0 s. When it passes (gap ≤ 1.0 s), it still does not prove that `LateUpdate` ran after this frame's `Update`, because the expected and actual values come from different frames. The code comment on that line ("controller Update, then view LateUpdate") gets the order wrong: the coroutine resumes before `LateUpdate`. The class summary says frame tests "only assert values that do not depend on frame duration", which is false for this line.
- Evidence: static analysis of the Unity frame order (Update → `yield null` coroutines → LateUpdate) against the expiry rule in C-001 `GetState`: `now - lastPress > 1.0` → combo 0. Lines in the test file:
  ```csharp
  yield return null; // controller Update, then view LateUpdate
  Assert.AreEqual(3, c.Current.Score, "score survives frames regardless of combo expiry");
  Assert.AreEqual(ScoreView.Format(c.Current), view.Text);   // timing-dependent
  ```
  CI has not run the test yet (license failure below), so no Unity log exists.
- Reproducibility: intermittent. The test fails only when frames take more than 1 s, so it is unlikely on a fast machine but possible on CI. It is deterministic once the frame gap exceeds 1.0 s.
- Possible fixes (developer's choice):
  - (a) Press at a time that is already expired, for example `SubmitPress(t0 - 10.0)` ×3, and yield twice so a full `LateUpdate` has run after a controller `Update`. Then assert the exact text `"Score: 3\nCombo: 0\nx1"`. This proves that the view refreshed (it differs from both the Bind-time Initial text and the pre-frame (3,3,1)) and does not depend on frame duration.
  - (b) Keep only frame-independent assertions (`StartsWith("Score: 3\n")` after two yields) and correct the comments.
  - Avoid `WaitForEndOfFrame` in batchmode/-nographics runs. It is a less reliable choice than yielding twice.
- Secondary uncertainty, not a confirmed bug: if Unity does not call `LateUpdate` on a component added in the same frame (from the coroutine phase), `view.Text` would still be `Format(Initial)` when the test resumes, and the `StartsWith("Score: 3\n")` assertion would fail every time. Yielding twice, as in fix (a)/(b), removes this risk too.

## Unity CI state (environment, not code)

- Latest run for this branch: https://github.com/Junho5408/second_game/actions/runs/45925656829 (run #8), head `23d8f656365799ced52a7844dafd465a2b12f63b`. Its ancestors include b5d12a4, so it contains the SANDBOX-004 code.
- Job `unity (Sandbox)` (id 140266289582) failed in the step "Unity tests (EditMode + PlayMode)". The "Windows build" step was skipped, and no `Sandbox-test-results` artifact was produced ("No files were found with the provided path: test-results").
- Log excerpt:
  ```
  [Licensing::Module] Loading manual activation license file UnityLicenseFile.ulf.
  [Licensing::Client] Error: Code 400 while processing request (status: TimeStamp validation failed)
  Unclassified error occured while trying to activate license.
  ##[error]Test run failed with exit code 1
  ```
- Classification: **environment failure** (Unity license activation). It is not a code failure. The project was never compiled and no test ran. The license secrets are present (the `detect` job succeeded and the real job ran rather than `skipped`), but the license was rejected. Earlier runs #4–#7 on this branch also ended in `failure`. The repository owner must renew or refresh the license file or secret. Agents must not read or change secrets.
- Consequence: `unity-compile`, `unity-playmode` and `unity-build` stay unverified. No `Reports/build/SANDBOX-004.md` was written, because no Unity verification ran.

## Tests added

None. Fixing the defective test belongs to the developer. QA did not modify the implementer's test, so it would not weaken or rewrite it on their behalf.

## Untested areas

- Real keyboard/mouse mapping in `Update` (`Input.GetKeyDown(R/Space)`, `GetMouseButtonDown(0)`). PlayMode cannot simulate it without the Input System package.
- `OnGUI` rendering (font size, position, colour). Nothing checks it visually.
- Compiling on Unity 6000.3 and the runtime `activeInputHandler` behaviour (review W-3 / R-3). Watch the first successful CI log for `InvalidOperationException` from `UnityEngine.Input`.
- Windows player build.
