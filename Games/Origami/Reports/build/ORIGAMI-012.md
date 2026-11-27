# Build / CI verification — ORIGAMI-012

- Workflow: `unity` (`.github/workflows/unity.yml`), event `push`
- Run: 46028807396 — https://github.com/Junho5408/second_game/actions/runs/46028807396 (run #26, attempt 1)
- Head commit: `1c4521f2e3c0677ced3e8b1346d663bcd01bd6e8` (orchestrator: merge ORIGAMI-012 …), branch `claude/serene-fermat-4bwjlo`. It contains `e35a16f` (ORIGAMI-012 implementation)
- Job: `unity (Origami)` (id 140607859354) — **success**, 15:54:50 → 16:02:48 UTC

| Step | Result |
|---|---|
| Unity tests (EditMode + PlayMode), game-ci/unity-test-runner v4.3.2, Unity 6000.3.29f1 | success |
| Upload `Origami-test-results` (artifact 15212554969) | success |
| Windows build (StandaloneWindows64, `StudioBuild.Build`) | success |
| Upload `Origami-windows-build` (artifact 15212934602, 34,275,210 bytes) | success |

## Test counts (from the result XMLs)
| Mode | Total | Passed | Failed | Inconclusive | Skipped |
|---|---|---|---|---|---|
| EditMode | 735 | 735 | 0 | 0 | 0 |
| PlayMode | 54 | 54 | 0 | 0 | 0 |

- PlayMode `Origami.Tests.PlayMode.FoldTableTests.*`: 33/33 Passed, including the three ScreenToPaper camera tests (review W-1).
- Compile: 0 `error CS` lines in `editmode.log` and `playmode.log`.
- Failure excerpts: none.

Windows build: download the artifact `Origami-windows-build` from the run page above.
