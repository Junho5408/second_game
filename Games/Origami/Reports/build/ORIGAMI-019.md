# Build verification ORIGAMI-019

| Item | Value |
|---|---|
| Workflow / run | `unity` run 46096898759 — https://github.com/Junho5408/second_game/actions/runs/46096898759 |
| Job | `unity (Origami)` 140830250495 — conclusion success (https://github.com/Junho5408/second_game/actions/runs/46096898759/job/140830250495) |
| Branch / commit | `claude/serene-fermat-4bwjlo` @ `ddcf6d04c88eb7bde0de59bd0472a97d84a4e11f` (contains impl `6407a02` and fix `5aab520`; later commits touch only review/task files) |
| Compile | OK, 0 `error CS`, 0 `warning CS` in the job log |
| EditMode | 735 / 735 passed |
| PlayMode | 147 total: 146 passed, 0 failed, 1 inconclusive (`WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw`, OnGUI not called headless; pre-existing) |
| Windows build | `Build Finished, Result: Success.` / `Build succeeded` |
| Build artifact | `Origami-windows-build`, id 15243504838, 34,885,393 B (zip), expires 2027-02-26 |
| Test artifact | `Origami-test-results`, id 15242824657, 90,001 B |
| Size vs ORIGAMI-018 (run 46093130343, 35,132,574 B) | −247,181 B (duplicate CasualMale removed) |

Failure excerpts: none. The game-ci "Failure / exit code not very descriptive" banner after the test step is caused by
the inconclusive test; the step conclusion is success.

Result: unity-compile = pass, unity-editmode = pass, unity-playmode = pass, unity-build = pass.
