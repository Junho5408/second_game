# Review ORIGAMI-019: CC0 models in the workshop (furniture, Crafter animation, lighting)

- Reviewer: code-reviewer, 2026-11-28
- Branch `claude/serene-fermat-4bwjlo`, base `fbd6de7`, implementation `6407a02` (+ `e45619a` status)
- **Result: CHANGES_REQUESTED**

## Verification evidence

| Command / source | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-019` | `OK` |
| `bash studio/scripts/verify_core.sh Origami` | PASS, 735/735 (Core unchanged) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` (the deleted CasualMale.fbx took its .meta with it, no orphans) |
| CI `unity` run 46095821852 (push, head `e45619a`, job `unity (Origami)` 140826831481) | **failure**. Compile OK. EditMode 735/735 passed. PlayMode 147 tests: 145 passed, **1 failed**, 1 inconclusive. The inconclusive one is `WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw` ("OnGUI was not called in this environment"); it was also inconclusive in the 018 run 46093130343. The Windows build step was **skipped** because the tests failed, so there is no build artifact |
| Evidence files | `/tmp/claude-0/-home-user-second-game/636884ad-8c21-5cae-9861-9592d708d5ff/scratchpad/ci4/` (`art/Origami-test-results/*.xml,*.log`, `job.log`, `prev/` = run 46093130343) |

The existing suites still pass in CI: Workshop (Mode, Movement, Screens, Smoke), FoldTable (33), Book (36), AssetPipeline (now run against `Characters/Crafter`) and Bootstrap. The 37 new tests run as well: all 23 `WorkshopAssetsTests` (19 model keys, 2 textures, Crafter clips, Window `glass`+`wood`) and 13 of the 14 `WorkshopArtTests`.

## Findings

### F-1 (blocking): the Crafter is drawn about 1.6 cm tall, because the fit uses SkinnedMeshRenderer bounds that are 100× the skeleton
- **Where:** `Scripts/Systems/ModelFit.cs` `TryWorldBounds` (the `SkinnedMeshRenderer` branch uses `r.bounds`). It is used by `Scripts/Gameplay/CharacterView.cs` `BuildModel` through `FitSpec.Height(1.6)`.
- **CI evidence:** `Character_IsTheCrafter_FittedTo1_6m_FrontMeasured` failed at line 247:
  `standing head height of a 1.6 m character, Expected: in range (1.3,1.75) But was: 0.0134256929f`.
  Printed: `native height 328.67, scale 0.00487, idle hips (0.000, 0.004, 0.000), sit hips (0.000, 0.003, -0.002), sit offset (0.000, 0.577, -0.048), walk clip ground speed 1.50`.
  `WorkshopAssetsTests` and `AssetPipelineTests` both report the Crafter's renderer bounds as 186.9 × 328.7 × 132.5 units. The skeleton, though, is about 3.3 units tall: Head_end is at 0.0134 / 0.00487 ≈ 2.76 units, and the hips are at ≈ 0.8 units. That matches ASSETS.md (the OBJ bind pose is ≈ 3.29 units). The FBX mesh/armature carries a ×100 unit factor. `SkinnedMeshRenderer.bounds`, which comes from the imported `localBounds`, is in the ×100 space. The skinned vertices follow the bones, so the drawn character is ≈ 3.29 × 0.00487 ≈ **0.016 m** tall.
- **Impact:** the player character is practically invisible in the game. The user-facing goal of the task is not met. Two other results are misleading because of this:
  - `Character_SitsDownOnTheChairSeat_AndStandsUpToIdle` passed only because `SitOffset` lifted the tiny model by +0.577 m, which is within `MaxSitLift` 0.8.
  - `Lighting_..._PaperOutsideTheSeatedShadow` passes trivially, because the bones are millimetres apart.
  `MeasureStride` also fell back to the 1.5 m/s default, because the foot travel was ≤ 0.05.
- **Fix:**
  1. Measure the character from the posed skeleton or skinned vertices, not from `Renderer.bounds`. Either:
     - In `ModelFit.TryWorldBounds`, for a `SkinnedMeshRenderer`, `BakeMesh` into a temporary mesh after the current pose and measure its vertex bounds in world space. Destroy the temp mesh afterwards. Check the `useScale` / matrix combination against the head-bone assert.
     - Or, in `CharacterView.BuildModel`, after `_clips.Pose(Idle, 0)`, take the native height from the bones (`Head_end` world y minus the model origin y, at wrapper scale 1, plus a small margin for the hair if the bake is not used). Then set the wrapper scale to `CharacterHeight / nativeHeight`.
     The expected result is a scale of ≈ 0.49 (ASSETS.md) and a standing `Head_end` at about 1.5 m.
  2. Because the imported `localBounds` stay ×100, set `smr.updateWhenOffscreen = true` (one character, cheap), or assign `smr.localBounds` from the measured pose. Otherwise culling and shadow-caster bounds stay 100× too large. The current code explicitly sets it to `false`.
  3. Harden the tests so a degenerate scale cannot pass again:
     - In `WorkshopArtTests`, assert `IdleHips.y` is in about [0.6, 1.0] m.
     - Assert `|SitOffset.y|` < ~0.35 m, so the sit correction is a small adjustment and not a lift.
     - Consider lowering `MaxSitLift` from 0.8 to ≈ 0.35 in `CharacterView`.
     - Assert `ClipGroundSpeed` ≠ the default, or that it lies in a plausible range (for example 0.5 to 3 m/s).
  4. After the fix, re-read the printed `sit hips` / `sit offset` and the `toes point east` assert. That assert never ran, because the test stopped at line 247. Confirm that the seated hips really land on the 0.48 m seat with a small correction.

### W-1: interface contract and architecture docs are behind the code (needs a doc task; the developer had no doc scope)
- `INTERFACE_CONTRACTS.md` C-193 lists `CharacterView` with `SetPose` only and `WorkshopView` with `Refresh` / counts. The change adds the public `CharacterView.Tick(float dt, float speed)` and `WorkshopView.UpdateCutaway(Vector2)`, the internal `ApplyCameraCutaway`, and new view-only steps in the `ProcessFrame` order (after (3) and (5)). The `OrigamiApp` XML doc describes them, but the contract does not.
- C-195 still lists the sample key `_PipelineSample/CasualMale`. ARCHITECTURE §14.2 still lists `CasualMale.fbx` in the sample folder. §14.7 still describes uniform fitting only; the DECISIONS 2026-11-28 ±40 % per-axis rule is now implemented in `ModelFit`.
- Dependency direction is fine: Gameplay → Systems (`ArtKeys`, `ModelFit`), and Core is untouched. `CharacterView` reads `WorkshopView.ChairSeatHeight`. This is a sibling coupling inside Gameplay; it is acceptable, but the constant could live with the seat data.
- **Fix:** an architect doc task that updates C-193, C-195, §14.2 and §14.7. No code change is needed for this.

### W-2: window glass alpha only takes effect if the glass imports as transparent
- **Where:** `WorkshopView.SetWindowColor`, `GlassAlpha = 0.55`.
- The `MaterialPropertyBlock` is applied correctly per material index (`SetPropertyBlock(mpb, index)`), and the CI test reads it back for both casements. However, with the Standard shader in Opaque mode, the alpha is ignored, and the opaque glass then simply shows the sky colour. The README documents this and lists it for playtest. No action needed now.

### W-3: build size could not be verified
- The Windows build was skipped, so the size compared with the 018 artifact (35,132,574 B zip) is unknown.
- The duplicate is gone from the tree: `Art/_PipelineSample/Resources/_PipelineSample/CasualMale.fbx` and its `.meta` are deleted. `_PipelineSample` keeps only `Chair.fbx` and `PaperSwatch.png`, and its CREDITS.md is updated.
- Check the artifact size on the next green run. The expected change is about −1.6 MB uncompressed.

### W-4 (minor)
- `CharacterView.MeasureStride` reads `anim[ArtKeys.ClipWalk]` directly, while `CharacterClips.Find` also accepts `"…|Walk"`. For consistency, take the length from the driver (for example, expose the walk length on `CharacterClips`).
- `WorkshopView.BuildLights` sets `RenderSettings` (Trilight) globally on every build. This is acceptable with a single `WorkshopView`. Note it if more scenes are added later.

## Checklist

1. **Spec / acceptance criteria:** met except the character criterion. Furniture fits match RoomLayout in CI to within 0.02: table 1.40×0.75×0.90 with the top at 0.750, bookshelf 1.6×2.0×0.4, counter 2.0×1.0×0.6 with the top at 1.000 = slots, chair 0.5×0.92×0.5, bed 1.0×0.657×1.97, trash uniform 0.355×0.731×0.40, door 1.0×2.077 centred on z −3.0, window 1.128×1.0 from y 1.0. Other criteria are in place: fallback, glass `MaterialPropertyBlock`, door cut-away, lighting, flat floor/walls, the 21-key test, the clip test, and the sample deletion. **The character criterion fails (F-1).** The Unity CI criterion is not met.
2. **Conventions:** Core untouched. No `Shader.Find`, no fog (asserted in a test), no `Time.*` in the views. No packages or ProjectSettings changes. Keys live only in `ArtKeys`. Legacy driving follows §14.6: speed 0, `time` from dt, `Sample()`, weights cross-faded in code.
3. **Boundaries:** collision and interaction stay in Core. The models are visual only; there are no colliders. The `ProcessFrame` additions are view-only and do not change the Workshop movement/mode tests, which pass.
4. **Null / invalid states:** `ModelFit` returns null on a missing key, no renderer, an invalid scale or an exception, destroys the half-built wrapper, and does not log (CI test). Per-furniture fallback works (CI test). NaN/negative dt and speed are clamped.
5. **Performance:** setup cost only. The bounds measurement and refine passes run once. `UpdateCutaway` and `ApplyCameraCutaway` are O(renderers) only when the state changes.
6. **Abstractions:** `ModelFit` / `FitSpec` are justified (used by about 40 placements). No duplication beyond the fallback, which is required.
7. **Regressions:** none in CI apart from F-1.
8. **Tests:** they drive `ProcessFrame` with a fixed dt, are synchronous and are not timing-dependent. A determinism test exists. Gaps: the sit and shadow tests did not catch the degenerate scale (F-1, fix 3).

## Required for re-review
F-1 fixed and the tests hardened. Then a green `unity` run containing `unity (Origami)`: compile, EditMode, PlayMode with 0 failures, and the Windows build with its artifact size recorded.
