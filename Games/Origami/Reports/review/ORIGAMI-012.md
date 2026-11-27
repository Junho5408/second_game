# Review — ORIGAMI-012 (v2 Unity fold table)

- Reviewer: code-reviewer · Branch `worktree-agent-aef06ce340b335eaf` · base `7190c81` · commits `e35a16f` (impl), `5562237` (status)
- **Result: APPROVED_WITH_WARNINGS**

## Verification run
| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | PASS (595/595 dotnet tests) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-012` | OK |
| `git diff --stat 7190c81..HEAD -- Core/ *.asmdef` | empty (Core and the asmdefs are unchanged) |

Unity compile, EditMode and PlayMode have not run yet. They are only verified in CI (`unity (Origami)`).

## Scope
All changed files are under `Scripts/Systems/` (Materials, MeshFactory, README), `Scripts/Folding/` (9 types + README) and `Tests/PlayMode/FoldTable/` (3 fixtures, 33 `[Test]`s + 2 helpers), plus their `.meta` files and the task file. Nothing is outside the scope.

## Checklist
1. **Spec / AC.** F-005 AC-17 is covered by `F005_AC17_CornerGrab_Preview_Release_AnimatesThenFoldCountIsOne`: the 0.141 corner grab is caught, ghost and flap are active, no host call happens while dragging, there is 1 host call on release, Core FoldCount is 1 at once, and the animation ends after 0.45 s but not after 0.2 s. `F005_AC17_EscapeWhileDragging…` checks that Esc makes no call and the count stays 0. AC-18 is covered by `F005_AC18_DiamondBase_TwoWheelNotches…`, which checks "겹: 위 2 / 6", `LastLayers == 2` and 8 facets, and by `F005_AC18_CtrlRelease_CallsCrease…`, which checks 1 Crease call, 0 Fold calls and `CreaseLineCount == 1`. F-006 AC-09 is covered by the guide tests: markers show only while the host gives a guide, never on a second sheet or with no sheet, and are hidden during animation. AC-19 is left to the user playtest, which is correct. W-6 / ORIGAMI-008 W-3 are respected: no test asserts 0.849/0.848 or a J-case percent.
2. **Contract C-190/C-191.** The public surface matches: `PointerFrame` fields, `FoldTableGuide`, `FoldTableChangeKind` values, all 8 `IFoldTableHost` members, the `FoldTable` constants/methods/properties and `PaperMeshBuilder` constants plus `AddCreaseLines`. `AddCreaseLinesCounted` is `internal`, so it adds no public surface. There is one documented deviation: `Build` takes `IReadOnlyList<Facet>` where the spike took `IList`, because Core v2 exposes IReadOnlyList (see N-2). The behaviour follows the bullets: drag start needs LeftDown ∧ HasPaperPoint ∧ !OverGui ∧ InputEnabled ∧ !IsAnimating. MoveTo uses the guide-drop overload when a guide exists. Release happens on LeftUp or !LeftHeld and calls the host with the preview's own arguments. Escape cancels the drag. On a serial change, the table's own result animates, Flipped animates from `ChangePrevious`, and every other change redraws at once. A new change ends any running animation.
3. **Boundaries.** Folding view uses only `Origami.Core.Folding/Geometry` (+ Runtime.Systems) and never touches Game/Screens. There is no `Update`/`OnGUI`/`RuntimeInitializeOnLoadMethod`, so nothing starts by itself (the only `Update` match is the private `UpdateCamera`). There is no `Input.*`, `Time.*`, `CreatePrimitive`, Collider, `Physics`, `Debug.Log*` or `DestroyImmediate`. Picking uses ray–plane math (`Plane.Raycast`). Shader names appear only in `Materials`. Meshes and materials are destroyed in `OnDestroy`.
4. **Unity API / compile risk (checked by hand, since the local run used a home-made stub).** All calls exist in Unity 6000.3 with these signatures: `Mesh.SetVertices/SetNormals/SetColors(List<>)`, `SetTriangles(List<int>, int[, bool])`, `indexFormat = IndexFormat.UInt32`, `MarkDynamic`, `subMeshCount`, `GetTriangles`, `RecalculateNormals/Bounds`; `MeshRenderer.sharedMaterials/shadowCastingMode (UnityEngine.Rendering)/receiveShadows`; `Camera.ScreenPointToRay(Vector3)`, `WorldToScreenPoint`, `pixelWidth/Height`; `Plane(Vector3, Vector3)`, `Plane.Raycast(Ray, out float)`; `Transform.TransformPoint/InverseTransformPoint/SetPositionAndRotation/Find(path)`; `Shader.Find`, `Shader.isSupported`, `new Material(Shader)`, `HideFlags.DontSave`; `Quaternion.Euler/Angle`; NUnit `Assume.That(bool, string)`. The language features are C# 9 safe (no file-scoped namespaces, no record struct, no global using). Core public type names (Facet, FoldLine, Vec2, SnapKind, …) do not collide with UnityEngine types, so there is no CS0104 ambiguity. Tests disambiguate `Object` with an alias where `using System;` is present (ViewTests). TableRig does not import `System`. `FoldTable` is the only MonoBehaviour, and its file name matches. The asmdefs are unchanged and sufficient: Runtime → Core; PlayMode tests → Runtime, Core, TestRunner, nunit, with `UNITY_INCLUDE_TESTS`. Mesh winding (Box/Cylinder/Sphere/Capsule) was checked by hand against Unity's front-face rule Cross(b−a, c−a), and the tests also assert it.
5. **Review notes from ORIGAMI-008.** N-1: namespace `Origami.Tests.PlayMode.FoldTableTests` ✓. N-3: the wheel moves one layer per frame by its sign, and wheel+bracket in the same frame is still clamped to ±1 (`N3_WheelCountsOnlyBySign_OneLayerPerFrame`) ✓. N-4: the chain is first usable → first existing in chain → (lit) unlit → `Hidden/InternalErrorShader`, with no logs and no throw (`N4_Materials_AlwaysReturnAMaterialWithAShader`) ✓.
6. **Null / invalid states.** Null host (Bind(null)), null sheet, NaN/negative/infinite dt, a host change during drag or animation, OverGui, disabled input, and a too-short drag (`LastError`) are all handled and tested.
7. **Tests.** They follow the PlayMode convention: `[Test]` only, direct `ProcessFrame(frame, dt)`, no frame waits, objects destroyed in TearDown, and an independent test host over `FoldSession` inside the test folder. They are sufficient for the owned ACs.

## Findings

### W-1 (Medium, CI coverage) — camera tests can become Inconclusive instead of failing
- Location: `Tests/PlayMode/FoldTable/FoldTableViewTests.cs` `SeatedCamera()`, the `Assume.That(cam.pixelWidth > 0 && cam.pixelHeight > 0, …)` line. Three tests depend on it: `ScreenToPaper_RoundTripsAPaperPoint`, `ScreenToPaper_FollowsSetTablePose` and `ScreenToPaper_FalseWithoutCameraOrLookingUp`.
- Problem: in batch mode (game-ci `unity-test-runner`, xvfb or no graphics) the screen size behind an untargeted camera is not guaranteed. If the viewport is 0×0, NUnit reports these tests as Inconclusive, the run still counts as green, and `ScreenToPaper` (the only real-mouse path used by 014) goes unverified. No Fold3DSpike PlayMode test used a camera, so there is no CI evidence either way.
- Impact: a silent gap in coverage, not a failure.
- Recommendation: QA must read the `unity (Origami)` PlayMode result XML and confirm these three tests show **Passed**, not Inconclusive/Skipped. If they are inconclusive, raise a fix: give the test camera a fixed viewport, e.g. `cam.targetTexture = new RenderTexture(640, 480, 16)`, destroyed in TearDown, so `pixelWidth/Height` no longer depend on the screen. Alternatively, test `ScreenToPaper` with a ray built from `cam.ViewportPointToRay`-equivalent math.

### W-2 (Low, performance) — static paper mesh is rebuilt every drag frame
- Location: `Scripts/Folding/FoldTable.cs` `DragFrame` (`_plan = _gesture.Preview(...)` every frame), `Render` (lines ~621–628), `SetStatic` (reference-key cache).
- Problem: `FoldGesture.Preview` returns a new `FoldPlan` each call. The `ReferenceEquals` cache in `SetStatic` therefore misses on every drag frame, and the static mesh, the crease overlay (with `DropSnap.MarkPieces`/`IsVisible`) and a new `PaperState` for the creases are all rebuilt. The flap and ghost are rebuilt too, which is expected.
- Impact: some extra CPU and GC per frame during a drag. The paper has at most a few dozen facets, so it is unlikely to be visible, and the spike behaved the same way. The cost grows with the crease count.
- Recommendation (optional, can go to 014 or a polish task): reuse `_plan` when grab, drop point, layers, shift and ctrl are unchanged from the previous frame, or key the static cache on (sheet, layers, line) instead of list identity.

### N-1 (Info) — `Material.color` on the last-resort shader
- Location: `Scripts/Systems/Materials.cs` `Create` (`m.color = color`).
- If all chains fail and the error shader is used, it has no `_Color`, and Unity may log a missing-property message, which would fail PlayMode via LogAssert. This only happens in an environment where even "Sprites/Default" is missing, which Always Included rules out. No change is needed. Optionally guard with `if (m.HasProperty("_Color"))`.

### N-2 (Info, contract text) — `PaperMeshBuilder.Build` parameter type
- C-191 says the spike signature stays "the same". The implementation uses `IReadOnlyList<Facet>` because Core v2 exposes `IReadOnlyList`. This is a reasonable change and is stated in the task notes. The Orchestrator should ask the architect to note it in INTERFACE_CONTRACTS C-191 during the next doc pass. No code change.

### N-3 (Info) — Vec2↔Vector conversion is not in one single helper
- Conversions are in `FoldTable.ToVec2/V3`, `PaperMeshBuilder.Identity/Emit` and `OverlayBuilder` call sites. Each one is a plain cast, and nothing is rounded before going back to Core. This is acceptable and not a change request.

## Verdict
**APPROVED_WITH_WARNINGS.** The code satisfies C-190/C-191, the owned ACs and the review notes N-1/N-3/N-4. Core is untouched, nothing in the scope self-starts, and I found no Unity API use that would compile against the stub but not against real Unity. Final acceptance still needs `unity (Origami)` compile, EditMode and PlayMode to pass, with W-1 checked in the result XML.
