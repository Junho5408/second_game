# Build / CI verification — ORIGAMI-013

- Workflow: `unity` (`.github/workflows/unity.yml`), event `push`
- Run: 46036542487 — https://github.com/Junho5408/second_game/actions/runs/46036542487 (run #30, attempt 1)
- Head commit: `0ed8137dcd5c11d1df3b378c7a8a28265368e033` (orchestrator: ORIGAMI-013 to REVIEW), branch `claude/serene-fermat-4bwjlo`. It contains `14257ae` (ORIGAMI-013 implementation); `git merge-base --is-ancestor 14257ae 0ed8137` → true
- Job: `unity (Origami)` (id 140633315578) — **success**, 16:47:30 → 16:54:54 UTC. Jobs `detect` = success; `skipped` = skipped, which is normal when the license exists

| Step | Result |
|---|---|
| Unity tests (EditMode + PlayMode), Unity 6000.3.29f1 | success (16:47:38 → 16:52:30) |
| Upload `Origami-test-results` (artifact 15219170403) | success |
| Windows build | success (16:52:31 → 16:54:48) |
| Upload `Origami-windows-build` (artifact 15218895854, 34,293,897 bytes) | success |

## Test counts (from the result XMLs in `Origami-test-results`)
| Mode | Total | Passed | Failed | Inconclusive | Skipped |
|---|---|---|---|---|---|
| EditMode | 972 | 972 | 0 | 0 | 0 |
| PlayMode | 90 | 90 | 0 | 0 | 0 |

- PlayMode breakdown:
  - `Origami.Tests.PlayMode.BookTests.*`: 36/36 Passed (BookPanelTests 23, DiagramRasterTests 13)
  - `FoldTableTests.*`: 33/33
  - `Flow.*`: 20/20
  - other: 1/1
- The brief and task notes say "37" Book tests. That is a miscount: `grep -c "\[Test\]"` on BookPanelTests.cs returns 24, but one match is the doc comment on line 12 ("Plain [Test]s"). The source declares 36 test methods, and every one of them ran and passed.
- Compile: 0 `error CS` lines in `editmode.log`, `playmode.log` and `COMBINE_RESULTS.log`.
- Failure excerpts: none.

Windows build: download the artifact `Origami-windows-build` from the run page above.
