# Review ORIGAMI-014 — v2 Unity workshop integration + v1 deletion

- Reviewer: code-reviewer
- Base: 5384f02 · Impl commits: bd68375 (Core Room + GameInfo), e9706bc (Runtime integration, Workshop PlayMode tests, v1 deletion), 85a2295 (status). Branch claude/serene-fermat-4bwjlo
- **Result: APPROVED_WITH_WARNINGS**

## Verification run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-014` | OK |
| `git diff --name-only 5384f02..HEAD` | Implementation files are only in `Core/Room/`, `Core/GameInfo.cs`, `Scripts/Gameplay/`, `Scripts/UI/` (KoreanFont untouched), `Tests/EditMode/Core/Room/`, `Tests/PlayMode/Workshop/` and the delete-only paths. The other paths (Docs, Reports/*-016, `Qa/ContractDocsV2QaTests.cs`, Tasks/016) come from ORIGAMI-016 / orchestrator commits in the range, not from 014 commits |
| `bash studio/scripts/verify_core.sh Origami` | PASS, 728/728 (978 → 728 after the v1 tests were removed) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| Unity CI `unity` run 46041182289 (head dbb1057, includes all 014 commits), job `unity (Origami)` | success. EditMode 728/728 Passed. PlayMode 103 total: 102 Passed, **1 Inconclusive** (`WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw`), 0 failed. All 33 Workshop tests ran. Windows build step: success. The only `LogError` lines in playmode.log are editor `ScriptableSingleton already exists` noise, not game code |

## Checklist
1. **Spec / AC.**
   - **F-002 AC-01~06.** The numbers come from Core `RoomMotion`. The dotnet tests use the spec values: 1.4, 3.0±0.15, 1.5±0.1, −3.7, −2.2, 2.25, the four walls and each blocking furniture face. PlayMode checks the same values through `ProcessFrame` with dt 0.02. They also check that movement stays put (±0.01 / exact) in Seated, Bookshelf, Counter, SleepConfirm and the pause overlay, the walking camera offset (±0.2), the look-at-head direction, and that the mouse does not rotate the camera.
   - **F-003 AC-05~07.** Prompt and sit at 1.1 m; nothing at 1.3 m (the snapshot reference is unchanged). The seated view is not reached at 0.32 s and is reached at 0.62 s. The paper is centred (dot > 0.9999). Near clip is 0.01 seated and 0.1 walking. Q stands the character at (2.5, 0.2) facing south. Sleep → Enter → settlement → Enter → (−1.6, 0) on day 2.
   - **F-007 AC-06.** The bookshelf view is full screen, the character does not move, and Esc closes it. Seated + B puts the book in the left 45 %, and a drag still folds.
   - **F-010 AC-06.** Slots fill in order with the player's shape mesh. The trash shows 1 ball per failure, capped at 10 (11 failures → 10).
   - **F-011 AC-07.** The counter shows the popular model, every `CounterLines` line and the total (computed with `Pricing`).
   - **F-012 AC-09.** The E-01 name and the CONTENT §8 text are checked literally, along with the records. Enter starts a new run.
   - **F-013 AC-03.** The walk hint shows only before the first sit.
   - **F-003 dialog texts (ORIGAMI-011 W-2).** The texts match F-003 (pause "계속 / 새로 시작", sleep "Enter = 예, Esc = 아니오", settlement "다음 날") and the DECISIONS row. Tests check them with their own literals.
   - **ORIGAMI-013 W-3.** (a) A rejected `BuyPage` shows `RejectMessage` ("돈이 모자라요", "이미 가진 페이지예요"). (b) The OnGUI test exists but CI could not run it (W-1).
   - **ORIGAMI-011 N-2.** A new run clears the leftover toast (tested).
   - **ORIGAMI-009 I-1.** `ModelCatalog.Recognition` is pre-warmed in `Build()`.
   - **ORIGAMI-008 N-2.** The camera is obtained lazily on the first Tick.
   - No test asserts 0.849/0.848 or a J percent. Toasts are checked with `StartsWith` before the percent.
2. **Smoke test is real.** It drives only `ProcessFrame` input: D held to walk, E to sit, Space, six guided steps as mouse drags through `FoldTable` (Ctrl on crease steps) → `FoldHostAdapter` → `ScreenFlow`, Enter to finish, Q, walk to the bed, E, Enter, Enter. Each stage is checked through GUI `VisibleLines`, `Workshop.DisplayedPieceCount` and the camera. The drag uses the guide's grab/drop coordinates and injects paper points directly. `ScreenToPaper` is not exercised (batch mode has no mouse), which is acceptable and matches the 012 tests.
3. **Contracts / dependencies.**
   - Public surfaces match C-180, C-193 and C-194. `BookHostAdapter`, `WorldConvert` and `IAppView` are internal.
   - `Origami.Runtime.Gameplay` has no `Origami.Runtime.UI` / `OrigamiGui` reference (grep). The GUI registers through the internal `IAppView` (`Bind` sets `app.View`), so the direction UI → Gameplay holds.
   - Room uses only Geometry and Catalog. Folding and Book views still do not reference Game, Screens or Gameplay.
   - D-130 literals match, including the obstacle boxes written as literals.
4. **Unity 6000.3 runtime risks.** CI compiled the code and ran it, so these are now confirmed.
   - There is one `Update` (OrigamiApp) and one `OnGUI` (OrigamiGui). Children have none (grep).
   - Two AfterSceneLoad hooks: `OrigamiGui.Init` calls `OrigamiApp.EnsureInstance()` (idempotent) and adds the GUI only if it is missing, so the hooks can run in either order. The camera is acquired on the first Tick (inside Update, after every AfterSceneLoad hook, including Bootstrap), so there is no duplicate camera. `RuntimeHook_…` checks one `[Origami]` app with its GUI, the Bootstrap root and a main camera, and it passed in CI.
   - IMGUI: GUI calls happen only inside `OnGUI` and `BookPanel.Draw`, and `GUI.skin` is read lazily in `EnsureStyles`. There is no GUILayout, so no Layout/Repaint control mismatch.
   - Hit rects are recorded on Repaint only. `PointerOverGui` flips y and undoes the scale correctly.
   - Legacy Input calls match v1 / the prototype.
   - No CreatePrimitive, Collider or Physics. Meshes and materials are tracked and destroyed in `OnDestroy`, and the owned camera is destroyed.
   - `InternalsVisibleTo("Origami.Tests.PlayMode")` matches the asmdef name. Asmdefs, Bootstrap, Editor/StudioBuild and KoreanFont are unchanged (empty diff).
5. **Null / invalid input.**
   - NaN or negative dt → 0 in App, Rig and GUI.
   - NaN or infinite move → 0. `RoomMotion` also turns NaN components into 0 and caps dt at 600 s, so it always terminates.
   - The adapters throw on a null flow. `ScreenToPaper` with a null camera returns false. A null snapshot gives an empty room.
6. **v1 deletion.**
   - Exactly the §10 list is deleted: Core/Content (8), Rules (7), Run (9), Flow (4); Tests Contracts (4), Flow (5), Rules (4), Run (8); PlayMode/Flow (2); the 6 named Qa files; OrigamiController, OrigamiView, ModelIcons, OrigamiButton and OrigamiButtons (Scripts/Gameplay and Scripts/UI, inside the scope and listed in §10). No other files are deleted (filtered `--name-status`).
   - The .meta files were deleted with them. The 7 entries git shows as "renamed" are new metas that all got new GUIDs.
   - There are no leftover references to `Origami.Core.{Content,Rules,Run,Flow}`, OrigamiRun, OrigamiFlow, OrigamiController or the other removed types anywhere under Games/Origami code, including Editor/.
   - The Core.Tests csproj globs the folder and builds.
7. **Spec gaps the developer filled.** All are reasonable and recorded in DECISIONS:
   - the dialog wording ("일시정지", "Esc = 계속, Enter = 새로 시작", "새로 시작할까요?", "결과 보기");
   - a toast for an invalid drag (`LastError` is reset at drag start, so an Esc-cancel shows no stale toast);
   - no pointer input while walking;
   - the 600 s cap and NaN → 0 (see W-3).

## Findings

### W-1 (Medium, verification gap) — the OnGUI book test is Inconclusive in CI
- **Where:** `Tests/PlayMode/Workshop/WorkshopSmokeTests.cs`, `OnGui_DrawsTheBookWithBookPanelDraw`.
- **Problem:** In run 46041182289 the test ended **Inconclusive** after 0.03 s. Batch mode never sent a Repaint, so `OnGUI` never ran and `BookPanel.Draw` and the GUI drawing code (`OrigamiGui.DrawAll`) were not exercised. The acceptance item "BookPanel.Draw를 실제 OnGUI에서 호출하는 스모크 PlayMode 테스트 1개 이상" is therefore not proven by CI. The test is correctly written and is not flaky; the environment simply cannot run it. Under batch mode, GUI functions throw outside OnGUI, so there is no alternative test path.
- **Impact:** An IMGUI runtime error in `DrawAll` or `BookPanel.Draw` (layout, styles, font) would show up only in the Windows build or the editor.
- **Fix (Orchestrator / QA, not the developer):** Accept the test as written. Record "OnGUI draws HUD, buttons, book and overlays without errors" as a check in the user playtest of the Windows build (next to F-007 AC-07 and the Korean-font check). QA should list it as PENDING (user), not PASS.

### W-2 (Low, performance) — the GUI and accessors allocate every frame
- **Where:** `OrigamiGui.Refresh`, which builds a new `List<string>`, array, `ReadOnlyCollection`, `GuiButton` objects with lambdas and the HUD strings every frame. `OrigamiApp.Table/Book/…`, which allocate a closure per access through `Built(() => …)`; `InputReader.Read` uses them every frame.
- **Impact:** This is small GC churn, a few KB per frame. It is not a correctness issue and has no visible effect at this scale.
- **Fix (optional, later):** Rebuild the lines and buttons only when the snapshot reference, `Message.Serial`, overlay, mode, interact candidate or toast state changes. Replace `Built(...)` with a plain `Build(); return _table != null ? _table : null;`.

### W-3 (Low, docs) — RoomMotion behaviour that C-180 does not mention
- **Where:** `Core/Room/RoomMotion.cs`: `MaxSimulatedTime = 600` (private) and NaN components → 0.
- **Problem:** C-180 says only "dt를 ⌈dt / MaxSubstep⌉개 같은 조각으로". The cap changes the result only for dt > 600 s, and the NaN handling is extra input sanitising. Both are sensible and covered by tests, but neither is in the contract.
- **Fix:** In a later architect doc follow-up, add one line to C-180: "dt > 600 → 600, NaN 성분 → 0". No code change.

### N-1 (Info)
- Every `WorkshopView` sets the global `RenderSettings` ambient values, so test rigs repeat it. This is harmless with one view in the game.
- The GameInfo 2.0.0 value is asserted only in the PlayMode smoke test. There is no EditMode/dotnet assertion, which is acceptable.

## Verdict
**APPROVED_WITH_WARNINGS.**
- The code meets the contracts and dependency rules.
- The v1 deletion is exactly the §10 list.
- The ACs are tested with values taken from the spec.
- Unity CI compile, EditMode, PlayMode (no failures) and the Windows build are green.
- W-1 is a CI-environment gap that belongs in the user playtest. W-2 and W-3 are non-blocking follow-ups.
