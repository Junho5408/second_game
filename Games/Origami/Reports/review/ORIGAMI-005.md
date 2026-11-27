# Review ORIGAMI-005 — Unity 컨트롤러·화면

- Reviewer: code-reviewer
- Base commit: f9b0a8fd71ce0fb6b52bfa97d9dd456a63d27e02
- Commits reviewed: e493187 (implementation), b047b9a (set REVIEW)
- Result: **APPROVED_WITH_WARNINGS**

## Verification run

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-005` | OK |
| `bash studio/scripts/verify_core.sh Origami` | PASS (283 passed, 0 failed) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK (new metas: MonoImporter for .cs, folderAsset for `Tests/PlayMode/Flow`; no duplicate GUIDs) |
| grep for asset loading (`Resources.Load`, `AssetDatabase`, `LoadImage`, `.png`, `.ttf`) in `Scripts/` | none |

Unity compile / EditMode / PlayMode / build: **not verified here** (no Unity). They stay open for the `unity` CI run.

## Changed files (all in scope)

- `Scripts/Gameplay/OrigamiController.cs` (C-030)
- `Scripts/UI/OrigamiView.cs`, `ModelIcons.cs` (C-031), `KoreanFont.cs` (internal), `OrigamiButton.cs`, `OrigamiButtons.cs` (public helpers, extension of C-031), `README.md`
- `Tests/PlayMode/Flow/OrigamiRuntimeTests.cs`, `OrigamiWiringTests.cs`, plus `.meta` files

## Checklist

1. **Spec / acceptance criteria**
   - Task AC-1 (C-030/C-031, one `[Origami]` object, buttons and keys call the same Flow methods): met. `ProcessFrame` is just `Flow.ProcessKeys`. Every button in `OrigamiButtons.For` holds a direct Flow method call (`SelectModel`, `EndDay`, `StartSelected`, `ClosePreview`, `Fold(a)`, `Hint`, `RequestAbandon`, `ConfirmAbandon`, `CancelAbandon`, `Acknowledge`, `Restart`), and these match the C-020 key table. `ReadKeys` follows the C-030 mapping exactly and uses `GetKeyDown` only. `EnsureInstance` follows the order Instance → Find → new + DontDestroyOnLoad. Controllers added by hand never replace `Instance`, and `OnDestroy` clears the instance only if it is this object. The AfterSceneLoad hooks on both classes make creation independent of hook order (same pattern as Sandbox `ScoreView`).
   - Task AC-2 / F-007 AC-07 (code-drawn shapes only): met. Shapes follow CONTENT_SPECS: plane is a sky-blue triangle, boat a blue trapezoid, frog a green circle, crane a white diamond with a red dot, dragon a red 5-point star. All are 64×64 RGBA, transparent background, cached, `HideAndDontSave`, and rebuilt if destroyed. `Paper` is a solid square. The Folding screen uses Paper plus a progress bar (i/N); the Notice screen (completed only) and the workbench model buttons use the model icons, with locked models tinted. The only other texture is `Texture2D.whiteTexture`, which is built in. No assets are loaded.
   - Task AC-3 (timing-independent PlayMode tests for AC-01/05/08 and a single runtime instance): met. The wiring tests are synchronous `[Test]`s that call `ProcessFrame`, `Invoke` and `Refresh` directly. The one `[UnityTest]` yields 2 frames, but none of its assertions depend on the frame count: nothing drives the runtime flow, and `Init` → `Bind` has already refreshed it. AC-01 (lines 0/1), AC-05 (each action key vs button, a full fold sequence with mistake + hint + complete + acknowledge, the abandon sequence, Preview Enter/Esc), AC-08 (Esc/Esc/Esc+Enter, ↑/Space/H ignored on the confirm screen, Esc+Enter in the same frame), and the A-4 multi-key case (`Enter|FoldUp` on Folding does nothing, as intended per the task note) are all covered. AC-02/03/04/06/09 are already covered at text level by the Core Flow tests (ORIGAMI-004).
   - Task notes: KoreanFont asks only for fonts the OS reports as installed, catches all exceptions without logging, and tries once per domain. This avoids the W-5 risk of error logs failing PlayMode tests.
2. **Architecture / conventions**: UI → Gameplay → Core, one direction only (A-1..A-7 respected). There are no rule decisions in Runtime: texts come from `FlowTexts`, and the Preview start label reads `PaperCost` from the catalog. The locked check for the icon tint uses `Snapshot.IsUnlocked`, which is display only; selecting a locked model goes through `Flow.SelectModel` (A-7). Styles and the font are created lazily inside `OnGUI` only. `GUI.matrix` is scaled from 1280×720 and restored together with `GUI.color`. `Bootstrap.cs`, asmdefs, Packages and ProjectSettings are untouched. Namespaces match folders.
3. **Dependencies**: no new packages. Asmdefs are fine: `Origami.Runtime` → `Origami.Core`; `Origami.Tests.PlayMode` → Runtime, Core, TestRunner, nunit (existing).
4. **Null / invalid states**: Unity fake null is handled (`Source`, `Instance`, `_source == null`, cached-texture `!= null`). `ModelIcons.Get(null/""/unknown)` returns null. `OrigamiButtons.For(null)` throws ArgumentNullException. A button left over from a previous screen returns false (tested). Notice draws an icon only for `FoldCompleted`.
5. **Performance**: see W-2 and N-1 (minor).
6. **Abstractions**: `OrigamiButtons`/`OrigamiButton` is one button list that both `OnGUI` and the tests use, so it is not duplication (see W-1).
7. **Regression risk**: low. Existing `BootstrapTests` are independent. Test objects use names other than `[Origami]`, so `GameObject.Find(ObjectName)` and `CountRuntimeControllers` (filtered by name + `scene.IsValid()`, which excludes prefab/asset objects) are not affected.
8. **Tests**: sufficient for the Unity wiring, given that the screen logic is Core-tested.

### Unity 6000.3 API / batchmode review (nothing found that should fail compile)
- APIs used exist and are not obsolete: `Font.GetOSInstalledFontNames()`, `Font.CreateDynamicFontFromOSFont(string[], int)`, `Resources.FindObjectsOfTypeAll<T>()`, `Scene.IsValid()`, `Texture2D(int,int,TextureFormat,bool)`, `SetPixels32`, `Apply(false)` (keeps the texture readable, so `GetPixels32` in the tests is valid), `Texture2D.whiteTexture`, `GUI.matrix`, `Matrix4x4.TRS`, `GUIStyle.CalcHeight`. The deprecated `FindObjectsOfType` is not used.
- Name resolution: no Core type clashes with `Screen`, `Input`, `Font`, `Object`, `Color`. Test files do not import `System`, so `Object.Destroy` is not ambiguous. Value-tuple deconstruction and a private nested delegate in a static class are valid C# 9.
- Batchmode: game-ci runs the editor under xvfb. CPU-side Texture2D creation and readback work even without a GPU device. `OnGUI` may or may not be called, and both are harmless: font failure is silent, and there are no clicks. No test depends on `OnGUI`.
- DontDestroyOnLoad / test isolation: the runtime object persists for the PlayMode session as intended. Every object a test creates is destroyed in TearDown. Static caches (`s_instance`, icon cache, font) tolerate destroyed objects.

## Findings

### W-1 (Low, documentation/contract) Public `OrigamiButtons`/`OrigamiButton` are not in C-031
- Location: `Scripts/UI/OrigamiButtons.cs`, `Scripts/UI/OrigamiButton.cs`; `Docs/tech/INTERFACE_CONTRACTS.md` C-031.
- Problem: these add public surface beyond the C-031 contract table. INTERFACE_CONTRACTS is architect-owned, so the developer correctly did not edit it, but the extension is recorded only in the class remarks and the task note.
- Impact: the contract docs no longer list all of the public Runtime UI surface. A later task could duplicate the button list or bypass it.
- Recommendation: the Orchestrator should have the architect add `OrigamiButtons.For(flow)` / `OrigamiButton` (labels as consts, `Invoke` = the single Flow call) to C-031 and to its change history. Keep the types; the design is sound because it keeps AC-05 testable without OnGUI.

### W-2 (Low, performance) Allocations on every OnGUI event
- Location: `OrigamiView.DrawButtons` → `OrigamiButtons.For(flow)`, and `DrawLines` (`new GUIContent` per line).
- Problem: on every OnGUI event (at least Layout and Repaint each frame), the code allocates a list, a ReadOnlyCollection, up to 7 button objects and closures, plus one GUIContent per line.
- Impact: small, steady GC churn. It does not matter for this game's scale.
- Recommendation (optional): cache the button list in `Refresh()` when `flow.Screen` changes (or every Refresh), and reuse one `GUIContent`.

### W-3 (Low, runtime behaviour, playtest) Korean font filtered by reported OS font names
- Location: `Scripts/UI/KoreanFont.cs` (`available.Contains(candidate)`).
- Problem: if Windows reports the Malgun Gothic family under a different or localized name, the code falls through to "Arial" or the default font. Hangul display then depends on Unity's OS glyph fallback.
- Impact: Korean text could show as boxes on some systems. CI cannot detect this (A-5).
- Recommendation: no code change is required now. Add an explicit item to the user playtest to confirm Hangul shows on Windows. If it fails, try `CreateDynamicFontFromOSFont` with the full candidate list as a second attempt (still silent).

### W-4 (Info) Unity verification pending
- Unity compile/EditMode/PlayMode/build have not run. The developer compiled against stubs only. This review found no compile or batchmode risks, but the `unity-*` items must come from a `unity (Origami)` CI run that contains commit e493187.

### N-1 (Nit) Per-pixel polygon allocation
- Location: `ModelIcons.Create`, plane/boat lambdas (`new[] { new Vector2(...) }` inside the pixel function).
- Problem: this allocates 4096 small arrays per icon, once per icon. Negligible.
- Recommendation: hoist the vertex arrays out of the lambda, as was already done for the star.

### N-2 (Nit) Paper test assertion loop
- Location: `OrigamiRuntimeTests.ModelIcons_Paper_IsSolidSquare`.
- Problem: it makes 4096 `Assert.AreEqual` calls, each building a message string. This is correct, only slow-ish.
- Recommendation: find the first differing index, then assert once.

## Conclusion
The implementation meets F-007 (Unity-side AC-01/05/07/08), C-030/C-031, A-1..A-7 and the task notes. The tests do not depend on frame timing. **APPROVED_WITH_WARNINGS**. W-1 needs a contract-doc update by the architect, W-3 needs a playtest item, and the unity-* items are pending CI.
