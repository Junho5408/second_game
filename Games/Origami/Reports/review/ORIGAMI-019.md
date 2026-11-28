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

## Re-review (fix pass 1)

- Reviewer: code-reviewer, 2026-11-28
- Fix commit `5aab520` (+ `ddcf6d0` status), head `ddcf6d0`
- **Result: APPROVED_WITH_WARNINGS**: F-1 is fixed and verified in real Unity. W-1 (doc task) and W-2 (glass playtest) stay open. They are non-blocking.

### Verification evidence

| Command / source | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-019` | `OK` |
| `bash studio/scripts/verify_core.sh Origami` | PASS, 735/735 |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | `OK` |
| CI `unity` run 46096898759 (push, head `ddcf6d0`, job `unity (Origami)` 140830250495) | **success**. Compile OK, 0 `error CS`/`warning CS` lines in the job log. EditMode 735/735 passed. PlayMode 147 tests: 146 passed, 0 failed, 1 inconclusive. The inconclusive one is the known `WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw` ("OnGUI was not called"). The Windows build reports `Build succeeded` |
| Build artifact `Origami-windows-build` | 34,885,393 B zip, against 35,132,574 B for 018 (run 46093130343): **−247,181 B** compressed. In the Unity build report, the uncompressed size went from 88.9 MB to 88.0 MB: Meshes 1.5 MB → 891.2 kB and Animations 586.7 kB → 293.4 kB. That is the removed duplicate CasualMale. The uncompressed saving is ≈ −0.9 MB of processed assets, not the −1.6 MB of the raw FBX file. This is consistent, and W-3 is closed |
| Evidence files | `/tmp/claude-0/-home-user-second-game/636884ad-8c21-5cae-9861-9592d708d5ff/scratchpad/ci5/` (`art/Origami-test-results/*`, `job.log`, `artifacts.json`) |

### F-1: measured in CI (`WorkshopArtTests` log)

`Crafter native height 3.325 (mesh), head_end 2.757, scale 0.48119, front yaw 0 (measured True), walk clip ground speed 0.84 m/s, idle hips (0.000, 0.431, 0.025), sit hips (0.000, 0.274, -0.202), sit offset (0.000, 0.306, 0.152)`
`seated hips (2.500, 0.580, 0.750)`

| Check | Value | Verdict |
|---|---|---|
| Scale | 0.481. The test requires 0.3–0.7 | ok, matches ASSETS.md (≈ 0.49) |
| Standing Head_end | 2.757 × 0.481 ≈ **1.33 m**. The test requires 1.1–1.6; the top of the hair is at 1.6 | ok |
| Height source / mesh-to-head ratio | `mesh`, 3.325 / 2.757 = 1.21. The range is 1.0–1.5, so the head-bone fallback did not trigger | ok |
| Idle hips | 0.431 m, 27 % of the height | plausible for the big-head chibi proportions; see below |
| Walk ground speed | **0.84 m/s**, measured. It is not the 1.5 default, and the test asserts it differs from the default | ok |
| Seated hips | y 0.580 = seat 0.48 + `HipAboveSeat` 0.1. x 2.5, z 0.75 = chair centre − `SeatBack` | ok |
| Sit offset | (0, +0.306, +0.152). \|y\| ≤ 0.35 and the horizontal 0.152 ≤ 0.3, so the limits are not hit | ok |
| Toes-east assert | it now runs past the head assert, and the test passed | ok |
| `updateWhenOffscreen` | asserted true for every SMR | ok |

The `Lighting_…PaperOutsideTheSeatedShadow` test now passes with a real-size seated character, so it is no longer passing trivially.

### Code review of the fix

- **`ModelFit.TryWorldBounds`, skinned branch** (`sharedMesh.bounds` × `smr.transform.localToWorldMatrix`):
  - This is correct for the bind pose. Unity's importer builds `bindposes[i] = bone.worldToLocal × smr.localToWorld`, so with the bones at rest the skinned vertex is `smr.localToWorld × v`.
  - Rotation or scale on the bones themselves therefore does not enter the measurement.
  - Rotation or scale on the SMR node is in the matrix. `TransformBounds` takes all 8 corners, so a rotated node gives a conservative AABB, which is fine.
  - Limitation: it measures the bind pose, not the current animated pose, and it ignores blend shapes. For a standing height this is close enough, and it is backed by the Head_end cross-check, which is the right safety net. The CI ratio of 1.21 shows that both agree.
  - Furniture has no SMRs, so it is unaffected. The furniture fit tests still pass.
- **`CharacterView.CheckHeightAgainstHead`:**
  - It runs after `Pose(Idle, 0)` and rescales the wrapper about its origin. The origin is anchored at the feet, so the feet stay put.
  - It then recomputes the front, stride and seat measurements, in order and after the rescale. This is correct.
  - Minor: the cross-check only uses `Head_end`. If a model has only `Head`, the check is silently skipped, although `_head` falls back to `Head` for the test/shadow line. This is acceptable for the single Crafter model and needs no action.
- **Constants:**
  - `MaxSitLift/Drop` 0.35 and `MaxSitShift` 0.3: the measured correction of 0.306 / 0.152 is inside these limits with a small margin. If a future clip needs more, the correction is clamped and the pose is used as is, as documented in the README.
  - `HeadEndToTop` 1.19 matches the 1.21 measured in CI.
  - `MinTopToHead/MaxTopToHead` 1.0–1.5 is a reasonable plausibility band.
- **Test note (minor, no action):** the `NativeHeight / HeadNativeHeight` in-range assert always holds, because the fallback forces a ratio of 1.19. The real guards against a degenerate scale are the asserts on the scale, Head_end, hips, ground speed and sit offset, and these are sufficient.
- **W-4:** `CharacterClips.WalkLength` replaces the direct `anim[ClipWalk]`, and `RenderSettings` is written only when a value differs. Resolved. The `ambientIntensity = 1` write was dropped, which keeps the project default of 1, so there is no behaviour change.

### Developer's notes

- **Idle hips 0.3–1.0 m:** the justification holds. The Crafter is a stylised big-head model: the hips are at 0.90 of 3.33 units in the FBX bind pose, which is 27 %. My earlier suggested range of [0.6, 1.0] assumed realistic proportions. The measured 0.431 m fits the model, and a lower bound of 0.3 still rejects the degenerate 0.004 case.
- **Feet hanging under the table:**
  - This is acceptable. The hips sit correctly on the 0.48 m seat. With short legs (the SitDown end pose has the hips 0.27 m above the feet at scale), the feet float ≈ 0.2 m above the floor under the table.
  - This is hidden in the seated view and only visible from the walk camera's angle while seated. It is already on the README playtest list ("앉은 자세가 의자와 맞는지").
  - **W-5 (playtest):** if it is visible in play, a later art task could lower the seat for the character or add a footrest. No code change is needed now.

### Open (non-blocking)

- **W-1:** INTERFACE_CONTRACTS C-193/C-195 and ARCHITECTURE §14.2/§14.7 still need an architect doc task.
- **W-2:** glass transparency, playtest.
- **W-5:** floating feet while seated, playtest.
