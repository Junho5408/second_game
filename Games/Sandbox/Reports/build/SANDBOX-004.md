# Build / Unity CI verification: SANDBOX-004

- Run: https://github.com/Junho5408/second_game/actions/runs/45928856363 (workflow `unity.yml`)
- Job: `unity (Sandbox)`, id 140276583022. All steps succeeded.
- Commit: `5e3a048f0822e16910022120348aa8d2f317385a`. Its ancestors include b5d12a4 (implementation) and 7d26984 (BUG-SANDBOX-004-1 fix), so it contains all SANDBOX-004 code.
- Unity 6000.3.29f1. Licence activation succeeded (`Successfully returned ULF license`).
- **Result: PASS** (compile, EditMode, PlayMode, Windows build)

## Compile
- Script compilation succeeded in the test editor (`*** Tundra build success (12.60 seconds), 289 items updated, 290 evaluated`) and in the build editor. There are no `error CS` lines in the job log, `editmode.log` or `playmode.log`. `Sandbox.Runtime.dll` was compiled (`Csc Library/Bee/artifacts/2400b0aEDbg.dag/Sandbox.Runtime.dll`).
- The single `ExitCode: 4` from `bee_backend` (playmode.log line 242) is Unity's normal "DAG out of date, rebuild" signal. The successful Tundra build right after it confirms that.

## Tests (from the `Sandbox-test-results` artifact XML and the job log)
| Suite | total | passed | failed | skipped | inconclusive |
|---|---|---|---|---|---|
| EditMode (`editmode-results.xml`) | 87 | 87 | 0 | 0 | 0 |
| PlayMode (`playmode-results.xml`) | 14 | 14 | 0 | 0 | 0 |

Job log: `✅ editmode-results.xml - 87/87 - Passed`, `✅ playmode-results.xml - 14/14 - Passed`, `✅ Sandbox Unity Test Results - 101/101 - Passed in 0.543s`.

All 14 PlayMode test cases have `result="Passed"`:
- `Sandbox.Tests.PlayMode.BootstrapTests.BootstrapCreatesCamera`
- `Sandbox.Tests.PlayMode.Scoring.ScoreCounterControllerTests`, 13 tests: Current_StartsAtInitial, SubmitPress_FirstPress_ScoresOne, SubmitPress_GapExactlyOneSecond_KeepsCombo, SubmitPress_GapOverOneSecond_RestartsCombo, SubmitPress_FifthPressInCombo_UsesMultiplierTwo, SubmitPress_InvalidTimes_AreIgnored, SubmitReset_ReturnsToInitial_AndNextPressIsFirst, ProcessFrame_SpaceAndClickSameFrame_CountAsTwoPresses, ProcessFrame_ResetAndPressSameFrame_ResetFirst, ProcessFrame_NoInput_ShowsComboExpiry, Format_ShowsScoreComboAndMultiplier, View_ShowsCurrentAfterFrame, RuntimeInstance_IsCreatedOnce_WithView

This count matches the source: 1 `[UnityTest]` in BootstrapTests.cs plus 13 `[Test]`/`[UnityTest]` in ScoreCounterControllerTests.cs. The 87 EditMode tests match the dotnet Core run (87/87).

### Check run conclusion "neutral"
Check run "Sandbox Unity Test Results" (id 140277577987, head 5e3a048) is `completed` with conclusion `neutral`. Its own output is title `✅ Sandbox Unity Test Results - 101/101 - Passed in 0.543s` and summary `87/87 Passed` + `14/14 Passed`. So "neutral" is the reporter's presentation of the result, not a failure or a zero-test run. The XML files above are the primary evidence.

## Legacy Input (review W-3 / R-3)
- `InvalidOperationException` and `UnityEngine.Input` exceptions do not appear in the job log, `playmode.log` or the PlayMode XML. The only `<output>` in the XML is "Saving results to ...".
- Positive evidence: `ScoreCounterController.Update` calls `Input.GetKeyDown(R)`, `GetKeyDown(Space)` and `GetMouseButtonDown(0)` *before* `ProcessFrame`. `View_ShowsCurrentAfterFrame` passed, and it asserts `Current == (3,0,1)` after two frames. That value can only be produced by `ProcessFrame` inside `Update`. So the legacy `Input` calls returned normally in PlayMode (Editor, batchmode). W-3 is resolved for the Editor/PlayMode. The standalone player was built but not run.

## Windows build
- `game-ci/unity-builder@v4`, target StandaloneWindows64, method `StudioBuild.Build`.
- `Build Finished, Result: Success.` / `[StudioBuild] result=Succeeded errors=0 size=90286249 output=/github/workspace/build/StandaloneWindows64/Sandbox.exe` / `Build succeeded` / `-rwxr-xr-x 1 root root 652K Nov 27 02:37 Sandbox.exe`

## Artifacts
| Name | ID | Size | Contents |
|---|---|---|---|
| `Sandbox-test-results` | 15167239490 | 63,435 B | editmode-results.xml, playmode-results.xml, editmode.log, playmode.log, COMBINE_RESULTS.log |
| `Sandbox-windows-build` | 15167484564 | 34,214,495 B | 144 files from build/StandaloneWindows64 (Sandbox.exe, Sandbox_Data/...) |

## Not verified by CI
- Running `Sandbox.exe` (real keyboard/mouse input, `OnGUI` rendering in the player). Only a person playing the build can check these.
