# Build / CI verification — ORIGAMI-014

- Workflow: `unity` (`.github/workflows/unity.yml`), event `push`
- Run: 46041182289 — https://github.com/Junho5408/second_game/actions/runs/46041182289 (attempt 1)
- Head commit: `dbb1057cc6592b8878b6fc71ff9b81a484022513` (orchestrator: record ORIGAMI-014 UI text decisions), branch `claude/serene-fermat-4bwjlo`.
  It contains all ORIGAMI-014 implementation commits: `bd68375`, `e9706bc` and `85a2295` (`git merge-base --is-ancestor e9706bc dbb1057` → true).
  The later commits `a43b217` (review report) and `bf29a91` (DECISIONS) change no code.
- Job: `unity (Origami)` (id 140649071956): **success**, 17:21:37 → 17:28:25 UTC. Job `detect` = success. Job `skipped` = skipped, which is normal when the license exists.

| Step | Result |
|---|---|
| Unity tests (EditMode + PlayMode), Unity 6000.3.29f1 | success (17:21:48 → 17:26:06) |
| Upload `Origami-test-results` (artifact 15220052623, 78,103 bytes) | success |
| Windows build (`StudioBuild.Build`, StandaloneWindows64) | success (17:26:07 → 17:28:18). Log: `Build Finished, Result: Success.` / `[StudioBuild] result=Succeeded errors=0 size=90462318 output=.../Origami.exe` |
| Upload `Origami-windows-build` (artifact **15221105864**, 34,295,759 bytes, 144 files, sha256 84b3e477…f9ea0, expires 2027-02-25) | success |

## Test counts (from the result XMLs in `Origami-test-results`, downloaded and parsed by QA)
| Mode | Total | Passed | Failed | Inconclusive | Skipped |
|---|---|---|---|---|---|
| EditMode | 728 | 728 | 0 | 0 | 0 |
| PlayMode | 103 | 102 | 0 | 1 | 0 |

- PlayMode breakdown:
  - Workshop: WorkshopModeTests 11/11, WorkshopMovementTests 10/10, WorkshopScreensTests 9/9, WorkshopSmokeTests 2 Passed + 1 Inconclusive. That is 33 tests in total.
  - The one-day smoke test `Smoke_OneDay_Sit_Tutorial6Steps_Finish_Sleep_Settlement` **Passed** (0.071 s).
  - `RuntimeHook_CreatesOneAppWithGui_AndKeepsTheBootstrapCamera` Passed.
  - BookPanelTests 23/23, DiagramRasterTests 13/13, FoldTable (Drag 12, Host 11, View 10) 33/33, BootstrapTests 1/1.
- The only non-pass is `Origami.Tests.PlayMode.Workshop.WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw`. It is **Inconclusive** with the message "OnGUI was not called in this environment (batch mode without repaint)". It is not a failure (DECISIONS 2026-11-27, review W-1); it is a user playtest item.
- Compile: 0 `error CS` lines in `editmode.log`, `playmode.log`, `COMBINE_RESULTS.log` and the full job log.

## Log notes (not code failures)
- PlayMode runner: `Test run completed. Exiting with code 2 (Failed). One or more tests failed.`
  - The result XML says `result="Passed" failed="0" inconclusive="1"`, so Unity reports the Inconclusive result as exit code 2.
  - The step conclusion was still success. That conclusion therefore does not reflect the Unity exit code; QA judges the result from the XML counts.
- At 17:26:05 a `# Failure #` banner from game-ci follows the code-coverage combine launch (COMBINE_RESULTS). It happens after both test modes finished and the step is success. This is environment/tooling output, not a test or compile failure.
- In `playmode.log`, the `LogError` lines are editor `ScriptableSingleton already exists` messages (package-manager noise), not game code.
- `Licensing::Module Error: Access token is unavailable` lines appear, and activation then succeeds.

## Not yet run in Unity
- QA added `Tests/EditMode/Core/Qa/RoomSpecQaTests.cs` (7 tests). It is verified by dotnet here (735/735) and will run in the next Unity EditMode run.

Windows build: download the artifact `Origami-windows-build` from https://github.com/Junho5408/second_game/actions/runs/46041182289/artifacts/15221105864
