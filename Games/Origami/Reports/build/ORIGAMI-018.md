# Build / CI verification ORIGAMI-018

- Run: https://github.com/Junho5408/second_game/actions/runs/46093130343 (workflow `unity`, run #36, event push, attempt 1)
- Head commit: 32137487e09b87de45928c1a418bfaf949e2f6c2 ("ORIGAMI-018: set REVIEW"); contains impl commit 9329425 (`git merge-base --is-ancestor` true). Commits after it touch only Docs/Reports/Tasks.
- Job `unity (Origami)` (id 140818425972): **success**. `detect` success; `skipped` job skipped (Unity secrets present).

| Step | Result |
|---|---|
| Unity tests (EditMode + PlayMode) | success (01:26:56 to 01:31:27 UTC) |
| Compile | 0 `error CS` in job.log, editmode.log, playmode.log |
| EditMode (`editmode-results.xml`) | 735 total, 735 passed, 0 failed, 0 inconclusive, 0 skipped |
| PlayMode (`playmode-results.xml`) | 110 total, 109 passed, 0 failed, 1 inconclusive (`Origami.Tests.PlayMode.Workshop.WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw`, known batch-mode OnGUI case), 0 skipped |
| Asset import | 21/21 new assets (18 `Workshop/*.fbx`, `Characters/Crafter.fbx`, 2 `Surfaces/*.jpg`) imported with an artifact id, no import error/warning. Only noise: `libxml2.so.16` fallback (environment) and Crafter "Keyframe reduction" info lines. `Window.fbx` (hand-edited): `-> (artifact id: 'b6d953ed...') in 0.088 s` |
| Windows build | success; build report lists `Crafter.fbx` 998.1 kb and `Window.fbx` 5.9 kb |
| Artifacts | `Origami-test-results`, `Origami-windows-build` (contains `Origami.exe`) |

Verdict: unity-compile = pass, unity-editmode = pass, unity-playmode = pass. No code or environment failure.
