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
