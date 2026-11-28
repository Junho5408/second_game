# Review ORIGAMI-017 — 3D asset pipeline (no Unity Editor)

- Reviewer: code-reviewer · Date: 2026-11-28 · Branch: claude/serene-fermat-4bwjlo
- Base 7058d3f · impl c13e1c6 · head a9f6a02
- **Result: CHANGES_REQUESTED**

## Evidence

| Check | Result |
|---|---|
| `check_scope.py Origami ORIGAMI-017` | OK |
| `python3 -m pytest studio/tests -q` | 51 passed |
| `bash studio/scripts/verify_core.sh Origami` | PASS (735/735) |
| `check_meta.py Games/Origami/Unity` | OK; only new metas added (10, all `A` in diff), no existing meta modified |
| CI `unity` run **46089349441** (push, head a9f6a02), job `unity (Origami)` | **failure** — compile OK, EditMode 735/735 passed, PlayMode 110 total: 107 passed, **2 failed**, 1 inconclusive (known OnGUI, DECISIONS 2026-11-27); **Windows build skipped** |
| CC0 on source pages (fetched today) | furniture-kit: "License(s): CC0" (uploader Kenney); animated-characters-pack: "License(s): CC0" (uploader quaternius); ambientCG license page: "CC0 1.0 Universal License" — all confirmed |

CI AssetPipeline results (playmode-results.xml):

| Test | Result |
|---|---|
| StaticModel_LoadsFromResources_WithMeshAndMaterial | Passed (bounds 2.00×4.70×2.00) |
| Character_HasLegacyClipsOnAnimationComponent_WithoutAnimator | Passed (11 clips: Defeat, Idle, PickUp, Punch, RecieveHit, Shoot_OneHanded, SitDown, StandUp, Victory, Walk, Walk_Carry) |
| Character_ClipNames_LoseArmaturePrefix | Passed |
| Character_SamplingWalkAtTwoTimes_MovesBones_ViaAnimationComponent | Passed (80.57° Foot.R) |
| Character_ClipSampleAnimation_MovesBones | Passed (80.57°) |
| **Character_LoadsWithSkinnedMeshBonesAndMaterials** | **Failed**: `character smoothness Expected: <= 0.2001 But was: 0.309965223` (AssetPipelineTests.cs:61) |
| **Texture_LoadsFromResources_AsTexture2D** | **Failed**: `Resources.Load<Texture2D>("_PipelineSample/PaperSwatch") returned null` (AssetPipelineTests.cs:187) |

The import log shows the scripts (incl. ArtImportRules) compiled before the art import, PaperSwatch.png imported by
`TextureImporter` (GUID bd582ab2…, so the minimal meta works for textures) and both FBX by `FBXImporter`;
LFS checkout worked (local file is a real 256×256 PNG, FBX imported with 11 takes).

## Findings

### F-1 (blocking) Material rule does not take effect — `Editor/ArtImportRules.cs` `OnPostprocessMaterial`
- Problem: with `materialImportMode = ImportViaMaterialDescription`, the CasualMale materials end with `_Glossiness`
  0.31 although `OnPostprocessMaterial` clamps to 0.2. Either the callback is not invoked for description-imported
  embedded materials, or the built-in Standard material-description preprocessor writes the values after it.
  (Shader name "Standard" and metallic 0 pass, but both are also the built-in defaults, so they do not prove the rule ran.)
- Impact: acceptance criterion 3 (import settings fixed in code — material/shader) and criterion 5 (CI PlayMode) fail;
  ARCHITECTURE §14.4 row "머티리얼" is not true in practice.
- Fix: apply material rules in `OnPreprocessMaterialDescription(MaterialDescription, Material, AnimationClip[])`
  with `GetPostprocessOrder()` greater than the built-in description preprocessor (so it runs after it), or set the
  values in `OnPostprocessModel(GameObject)` on the renderers' (embedded) shared materials. Bump `Version`.
  Add a discriminating assertion (e.g. a value the default would not produce), and log the material values in the test output.

### F-2 (blocking) PNG does not load as `Texture2D` from Resources
- Problem: `Resources.Load<Texture2D>("_PipelineSample/PaperSwatch")` returns null in CI although the file was
  imported by TextureImporter. Root cause not determinable from the log (no import error/warning printed).
- Impact: criterion 5 ("Resources에서 로드") and the texture half of criteria 2/3 unproven; ORIGAMI-018/019 floor/wall
  textures would silently be missing.
- Fix: diagnose in the test under `#if UNITY_EDITOR` (print `AssetImporter.GetAtPath(path)` type/textureType and the types
  of `AssetDatabase.LoadAllAssetsAtPath(path)`, and try `Resources.Load(key)` without a type / `Resources.Load<Texture>`),
  then correct the import rule (e.g. explicitly set `textureShape = Texture2D`, `npotScale`, `alphaSource`, check the
  project's default behaviour mode/Sprite import) until the test passes. Do not weaken the test.

### F-3 (blocking, consequence) Windows build not verified
- The `Windows build` step was skipped because tests failed, so `unity-build` (criterion 5, "빌드에 쓰는 셰이더가 빠지지
  않게") is unverified. Re-run CI after F-1/F-2.

### W-1 Test discrimination — `AssetPipelineTests.cs`
- `Texture_LoadsFromResources_AsTexture2D` checks mipmaps and Repeat, which are TextureImporter defaults, so it cannot
  tell whether `OnPreprocessTexture` ran (the 256 px sample cannot exercise the 1024 cap either). Consider also
  asserting the importer settings under `#if UNITY_EDITOR` (`TextureImporter.maxTextureSize == 1024`, `textureType`),
  and for models `ModelImporter.animationType == Legacy`, `materialLocation == InPrefab`. Otherwise the tests are
  deterministic (synchronous `[Test]`, explicit sample times, TearDown destroys instances) and meaningful — good.

### W-2 `.obj`/`.tga` get native minimal metas but are not LFS-routed — already documented in §14.8; no change required.

## Judgements requested by the Orchestrator

- **gen_meta minimal metas**: correct approach — Unity selects the native importer from the extension and keeps the GUID
  (confirmed by CI log: TextureImporter / FBXImporter with the committed GUIDs). Existing metas untouched; tests cover
  case-insensitive extensions, folders/unknown → DefaultImporter, no overwrite, unique GUIDs, check_meta OK.
- **AssetPostprocessor API (Unity 6000.3)**: `OnPreprocessModel`, `OnPreprocessAnimation` (clipAnimations from
  `defaultClipAnimations`), `OnPreprocessTexture`, `GetVersion()` are correct and demonstrably ran (Legacy + renamed clips).
  `OnPostprocessMaterial` is the wrong hook for ImportViaMaterialDescription (F-1). The `Assets/Game/` prefix scope is fine.
- **Standard shader strategy**: sound. Embedded materials of Resources models pull in only the used Standard variants;
  not adding Standard to Always Included avoids compiling all variants and touching ProjectSettings. Runtime must not
  `Shader.Find("Standard")` — documented. Needs the build to actually run (F-3).
- **Fog ban**: correct — with automatic fog-mode stripping evaluated against the (empty) build scene, enabling
  `RenderSettings.fog` at runtime would render differently only in builds. Keep it.
- **Legacy Animation**: right choice without an Editor: no AnimatorController asset, deterministic `state.time` + `Sample()`
  matches A2-5; CI proves bones move. Limitation (no retargeting; clips must live in the same FBX) is documented.
- **Resource key convention `Art/<Set>/Resources/<Set>/<Name>` → key `<Set>/<Name>`**: good; avoids key collisions between
  several Resources folders, single constants file (ArtKeys) planned. Note everything in those folders ships in the build
  (documented).
- **Git LFS vs plain git — recommendation: switch art to plain git** (Orchestrator/architect task; `.gitattributes`
  and `unity.yml` predate this task and are outside its scope):
  - `actions/checkout` with `lfs: true` downloads *all* LFS objects of the repo (every game) on every `unity` job, and
    Actions downloads count against the LFS bandwidth quota. GitHub Free includes only a small monthly LFS bandwidth
    quota (1 GiB on the legacy plan; 10 GiB under the current metered billing); when exceeded, LFS is disabled for the
    repo and the CI checkout fails.
  - Observed load: 33 `unity` runs on 2026-11-27. At the 30 MB budget that is ≈1 GB/day, i.e. far over either quota
    within days (today 1.6 MB → ≈50 MB/day, already ~1.5 GB/month).
  - Plain git cost: ≤30 MB added once to history (each file ≤5 MB, well under GitHub's 50/100 MB per-file limits); clones
    stay small; it also removes the manual `git lfs push` step (no pre-push hook here), which is an error trap.
  - If LFS is kept: cache `.git/lfs` with `actions/cache` keyed on `git lfs ls-files -l` and restrict to the game
    (`git lfs pull --include "Games/<G>/**"`), and monitor quota.
  - Migration (recommended): remove the `*.fbx`/`*.png`/... LFS rules (or scope them away from `Games/*/Unity/Assets/Game/Art/`),
    re-add the 3 sample files as normal blobs (`git rm --cached` + `git add --renormalize`), keep the same `.meta` GUIDs.

## Acceptance criteria

| # | Criterion | Status |
|---|---|---|
| 1 | Formats FBX/OBJ+PNG, no new packages, built-in RP | met (§14.1) |
| 2 | gen_meta native importers via minimal meta + pytest | met |
| 3 | AssetPostprocessor fixes scale/material/shader/animation; shaders kept in build | **partly** — material rule ineffective (F-1), build unverified (F-3) |
| 4 | Load rules, CC0 + CREDITS, size budget documented | met (§14.2, §14.8, CODING_CONVENTION; CC0 verified) |
| 5 | Samples + PlayMode tests; CI compile/PlayMode/Windows build pass | **not met** — 2 PlayMode failures, build skipped (run 46089349441) |

## Re-review (fix pass 1)

Reviewed: fix commit `53548ba` (head `de6333c`, branch `claude/serene-fermat-4bwjlo`).
**Verdict: APPROVED_WITH_WARNINGS**

### CI evidence — `unity` run 46090446550 (push, head de6333c, conclusion success)

| Job | Result |
|---|---|
| detect | success |
| unity (Origami) (job 140809884082) | success. All steps passed: checkout (lfs: false), Unity tests, upload, **Windows build**, upload |
| unity (Fold3DSpike) | success, test results + Windows build artifacts present |
| unity (Sandbox) | success, test results + Windows build artifacts present |

- Origami EditMode: 735 total / 735 passed / 0 failed.
- Origami PlayMode: 110 total / 109 passed / 0 failed / 1 inconclusive. The inconclusive one is
  `Workshop.WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw`, which was inconclusive before this task too (OnGUI checks moved to
  user playtest, ORIGAMI-014 W-1). Unity's test runner returns exit code 2 because of it, but the workflow step accepts that, as in earlier runs.
- `AssetPipeline` fixture: **7/7 passed** (Static model, Character load, Legacy clips/no Animator, clip-name prefix, ClipSampleAnimation
  moves bones, Animation-component sampling moves bones, Texture load).
- Windows build: log shows `Compiling shader "Standard"` (FORWARD, d3d11, 664 → 8 variants after built-in stripping) and
  `Build Finished, Result: Success.` Artifact `Origami-windows-build` is 34,624,126 bytes.
- Artifacts downloaded to `/tmp/claude-0/-home-user-second-game/636884ad-8c21-5cae-9861-9592d708d5ff/scratchpad/ci2/results/`
  (editmode/playmode XML and logs). Job log is in `.../scratchpad/ci2/job.log`.

### Local checks
- `python3 -m pytest studio/tests -q`: 51 passed
- `python3 studio/scripts/check_meta.py Games/Origami/Unity`: OK
- `bash studio/scripts/verify_core.sh Origami`: 735 passed, PASS
- `python3 studio/scripts/check_scope.py Origami ORIGAMI-017`: OK

### Findings status

- **F-1: resolved.** `ApplyMaterialRules` now runs in `OnPreprocessMaterialDescription` (with `GetPostprocessOrder() = 100`)
  and again in `OnPostprocessModel` on the renderers' shared (embedded) materials. `Version` is now 2. The test asserts the exact values
  Standard / `_Metallic` 0 / `_Glossiness` 0.15, and requires both properties to exist (an absent property no longer passes silently).
  The CI output shows every material at `shader Standard, _Metallic 0, _Glossiness 0.15, _GlossMapScale 0.15`: Shirt, Skin, Pants,
  Belt, Face and Hair on CasualMale, and wood on Chair. Before the fix the value was 0.31, so the rule demonstrably ran.
  `ApplyMaterialRules` is idempotent and null-safe, and it keeps colour and main texture when it switches shader.
- **F-2: resolved.** `Resources.Load<Texture2D>("_PipelineSample/PaperSwatch")` now returns the texture. CI output:
  `256x256, mips 9, wrap Repeat, filter Bilinear, aniso 2, format DXT1`. The test now also checks the `TextureImporter` settings
  (Default, Texture2D, maxTextureSize 1024, aniso 2).
  Root cause: **not isolated.** Two changes landed together: explicit `textureShape`/`spriteImportMode`/`npotScale`/`alphaSource` and
  the samples changing from LFS pointers to plain blobs. Because the test passed, the diagnostic branch never ran, so it cannot say
  which change mattered. This does not block. The importer settings are now pinned and asserted, and the diagnostics remain to explain
  any future regression (see W-3).
- **F-3: resolved.** The Windows build step succeeded and the artifact exists. "Standard" is compiled into the build from the
  embedded materials, without using Always Included. This confirms the shader strategy.
- **W-1: resolved.** The new assertions only pass if the rules ran: smoothness 0.15 (FBX-derived default was 0.31), anisoLevel 2
  (Unity default 1), maxTextureSize 1024 (default 2048). `AssertModelImporterRules` checks Legacy, ImportViaMaterialDescription,
  InPrefab, no collider, and no cameras or lights.
- **LFS removal: done correctly.**
  - `.gitattributes`: the 7 `filter=lfs` lines are replaced by `binary` lines for png/jpg/jpeg/tga/psd/fbx/obj/wav/mp3/ogg.
    `git check-attr` on PaperSwatch.png gives `binary: set`, `text/diff/merge: unset`.
  - The samples are real blobs: CasualMale.fbx is 1,612,396 B (`Kaydara FBX Binary`), Chair.fbx is 25,338 B (ASCII `; FBX 7.3.0`),
    and PaperSwatch.png is 20,447 B (`\x89PNG`). `git lfs ls-files` is empty, and no tracked png/fbx/jpg/wav/ogg/mp3 blob in the repo
    starts with an LFS pointer header. `.meta` files and GUIDs are unchanged.
  - `unity.yml`: the only changed line is `lfs: true` → `lfs: false`. The other games' jobs (Fold3DSpike, Sandbox) pass in the same run,
    so nothing regressed.
  - Docs are updated consistently: ARCHITECTURE §12 table, A2-18, §14.4, §14.8; CODING_CONVENTION; INTERFACE_CONTRACTS §(asset contract).
    They match DECISIONS 2026-11-28 ("Git LFS를 쓰지 않는다").

### Remaining warnings (non-blocking)

- **W-2** (from the first review): no change.
- **W-3** `Tests/PlayMode/AssetPipeline/AssetPipelineTests.cs` `TextureDiagnostics()`. The F-2 root cause is not recorded.
  Impact: a future texture-load failure in ORIGAMI-018/019 would start from zero. Recommendation: no code change. Keep the
  diagnostics. If a texture ever fails to load again, read the `[AssetPipelineTests] PaperSwatch diagnostics` block in playmode.log first.
- **W-4** (note for ORIGAMI-019): the CasualMale sample is about 329 units tall at file scale, against 4.7 for Chair (cm vs m
  export units). This was expected under §14.7 (runtime fits to `Renderer.bounds`), but it shows that bounds fitting is mandatory,
  not optional, for character kits.

### Acceptance criteria (updated)

| # | Status |
|---|---|
| 1 | met |
| 2 | met |
| 3 | met: scale, material (exact values proven in CI), shader in build (build log), Legacy animation |
| 4 | met: docs updated for no-LFS storage |
| 5 | met: run 46090446550 — compile OK, PlayMode AssetPipeline 7/7, Windows build success |
