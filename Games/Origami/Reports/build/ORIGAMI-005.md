# Build / CI verification ORIGAMI-005 — Unity 컨트롤러·화면

- Run: https://github.com/Junho5408/second_game/actions/runs/45948502253 (workflow `unity`, attempt 1)
- Job: `unity (Origami)` 140338673308 — https://github.com/Junho5408/second_game/actions/runs/45948502253/job/140338673308 — conclusion **success**
- Branch / head commit: `claude/serene-fermat-4bwjlo` @ `b047b9aa74179ee8cffc3184ce69dbe78ff37d94`
  (contains implementation commit e493187; `git log b047b9a..HEAD -- Games/Origami/Unity` is empty, so no later Unity change is untested)
- Unity 6000.3.29f1, batchmode, Linux runner (xvfb)

## Steps
| Step | Result |
|---|---|
| Unity tests (EditMode + PlayMode) | success (05:54:15–05:58:46Z) |
| upload test results | success |
| Windows build | success (05:58:47–06:01:16Z) |
| upload build artifact | success |

## Compile (unity-compile)
`playmode.log`: Bee/Csc built `Origami.Core.dll`, `Origami.Runtime.dll`, `Origami.Editor.dll`, `Origami.Tests.EditMode.dll`,
`Origami.Tests.PlayMode.dll` and copied them to `Library/ScriptAssemblies`; `*** Tundra build success`.
No `error CS` and no `warning CS` lines in `editmode.log` / `playmode.log`. Player build `ProducePlayerScriptAssemblies` succeeded.

## Tests (from artifact `Origami-test-results`, id 15175842038, 49,675 B — downloaded and parsed)
| Suite | total | passed | failed | inconclusive | skipped | result |
|---|---|---|---|---|---|---|
| EditMode (`editmode-results.xml`) | 283 | 283 | 0 | 0 | 0 | Passed |
| PlayMode (`playmode-results.xml`) | 21 | 21 | 0 | 0 | 0 | Passed |

- EditMode = the 283 Core tests (25 fixtures: Contracts, Flow, Qa, Rules, Run, GameInfo) — same count as `verify_core.sh` (283 passed).
- PlayMode fixtures: `BootstrapTests` 1, `Flow.OrigamiRuntimeTests` 4, `Flow.OrigamiWiringTests` 16 — all Passed.
  Includes `RuntimeInstance_IsCreatedOnce_WithBoundView`, `ModelIcons_FiveModels_DistinctCodeGeneratedTextures`,
  `ModelIcons_Paper_IsSolidSquare`, `ModelIcons_UnknownOrNull_ReturnsNull`, `F007_AC01_*`, four `F007_AC05_*`, two `F007_AC08_*`,
  `MultiKeyFrame_TopKeyIgnoredOnScreen_DoesNothing`, `ReadKeys_WithoutRealInput_IsNone`, `StaleButton_AfterScreenChange_IsIgnored`, etc.
- Test-case `<output>` contains only the runner's "Saving results to ..." line: no game log output.

## Error / exception scan
- `Debug.LogError` occurrences in `playmode.log` (4): all `ScriptableSingleton already exists` from
  `UnityEditor.PackageManager.UI.Internal.ServicesContainer` / `PackageManagerProjectSettings` during editor startup/domain reload —
  Unity editor internals, not game code (environment noise; also present before this task).
- One `LogWarning` from `CleanupVerificationTask` (immutable package files altered — CI Library cache), environment.
- No occurrence of `Exception`, `NullReference`, `KoreanFont`, `ModelIcons`, font or Texture2D errors, or Input errors in either log.
  Unity Test Framework fails a test on an unexpected error log; all 21 PlayMode tests passed while the runtime `[Origami]` object
  (Update → `Input.GetKeyDown`, OnGUI → `KoreanFont`/`ModelIcons`) was live, so no game error was logged.

## Windows build (unity-build)
- `Build Finished, Result: Success.`
- `[StudioBuild] result=Succeeded errors=0 size=90323254 output=/github/workspace/build/StandaloneWindows64/Origami.exe`
- Output dir: `Origami.exe` (652K), `Origami_Data/`, `UnityPlayer.dll` (36M), `UnityCrashHandler64.exe`, `MonoBleedingEdge/`, `D3D12/`;
  complete build size 86.1 MB. Build report lists `Scripts/Gameplay/OrigamiController.cs`, `Scripts/UI/OrigamiView.cs`,
  `ModelIcons.cs`, `KoreanFont.cs`, `OrigamiButtons.cs`, `OrigamiButton.cs`.
- Artifact **`Origami-windows-build`**, id 15176257837, 34,232,248 bytes (144 files), sha256
  `b3947778d8595a26261844706f61bac26cb3cf85ae6d97f8f95aea737f90b40c`, expires 2027-02-25.
  Download: https://github.com/Junho5408/second_game/actions/runs/45948502253/artifacts/15176257837

## Verdict
unity-compile **pass**, unity-editmode **pass** (283/283), unity-playmode **pass** (21/21), unity-build **pass**.
No code or environment failures.
