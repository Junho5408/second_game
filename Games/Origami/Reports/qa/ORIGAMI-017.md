# QA ORIGAMI-017 — 3D asset pipeline (no Unity Editor)

- QA: qa-engineer · Date: 2026-11-28 · Branch: claude/serene-fermat-4bwjlo · HEAD 86f00c3
- Task commits: c13e1c6 (impl), 53548ba (fix pass 1); review: APPROVED_WITH_WARNINGS (re-review, 86f00c3)
- CI: `unity` run 46090446550 (head de6333c, contains 53548ba; later commits touch only review report + task file)
- **Result: PASS**

## Acceptance criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Formats FBX/OBJ+PNG, no new packages, built-in RP kept | PASS | `git diff 7058d3f HEAD -- Games/Origami/Unity/Packages Games/Origami/Unity/ProjectSettings` → empty (manifest.json, graphics settings unchanged). ARCHITECTURE §14.1 records FBX/OBJ + PNG/JPG/TGA via native importers, no glTFast. |
| 2 | gen_meta: .fbx/.obj → ModelImporter, .png/.jpg/.tga → TextureImporter (or minimal meta), tests, pytest | PASS | `python3 -m pytest studio/tests -q` → 51 passed. Manual run on scratch `Assets/Art` copy (Chair.fbx, PaperSwatch.png, X.OBJ, Y.TGA, z.jpg, n.txt): art files get minimal meta (`fileFormatVersion: 2` + `guid` only, upper-case ext included), folder → `folderAsset: yes` DefaultImporter, .txt → TextScriptImporter; second run does not overwrite (`cmp` identical); `check_meta.py` on scratch dir → OK. CI import log confirms Unity picks FBXImporter/TextureImporter for the committed minimal metas and keeps their GUIDs (review, run 46089349441/46090446550). |
| 3 | AssetPostprocessor fixes scale, material/shader, animation (Legacy, no AnimatorController), shader kept in build | PASS | `Editor/ArtImportRules.cs`: `useFileScale`/`globalScale 1`, `ImportViaMaterialDescription` + material rules in `OnPreprocessMaterialDescription` (order 100) and `OnPostprocessModel`, `animationType = Legacy`, texture Default/Texture2D/max 1024/aniso 2, `GetVersion()=2`. CI playmode-results.xml (downloaded independently): every material `shader Standard, _Metallic 0, _Glossiness 0.15` (FBX default was 0.31 → rule demonstrably ran); PaperSwatch `256x256, mips 9, wrap Repeat, filter Bilinear, aniso 2, format DXT1`. Build: Windows build step success, "Compiling shader \"Standard\"" and "Build Finished, Result: Success." in job log. |
| 4 | Runtime load rules, license rule (CC0, CREDITS), size budget documented | PASS | ARCHITECTURE §14.2 (key `Art/<Set>/Resources/<Set>/<Name>` → `Resources.Load<T>("<Set>/<Name>")`), §14.7, §14.8; CODING_CONVENTION lines 86–96 (CC0 only, CREDITS fields, ≤30 MB per game, ≤5 MB per file, ≤1024 px, no LFS). CC0 re-checked today on source pages: opengameart furniture-kit "CC0", opengameart animated-characters-pack "CC0", docs.ambientcg.com/license "CC0 1.0 Universal License". Sizes: 25,338 + 1,612,396 + 20,447 = 1,658,181 B, matches CREDITS.md total; largest file 1.6 MB < 5 MB. |
| 5 | Samples (CC0 static + animated character) in _PipelineSample; PlayMode tests (Resources load, mesh/material, clip playback moves bones); CI compile/PlayMode/Windows build pass | PASS | Samples Chair.fbx, CasualMale.fbx, PaperSwatch.png (real blobs, not LFS pointers). CI run 46090446550 job `unity (Origami)` success: no `error CS` in logs; EditMode 735/735; PlayMode 110 total, 109 passed, 0 failed, 1 inconclusive (`Workshop.WorkshopSmokeTests.OnGui_DrawsTheBookWithBookPanelDraw`, known, DECISIONS 2026-11-27); AssetPipeline 7/7 passed; artifact `Origami-windows-build` 34,624,126 B. |

## Tests added
None. Existing AssetPipelineTests (7) discriminate the import rules (values differ from Unity/FBX defaults); gen_meta covered by studio/tests plus the manual scratch run above. No clear gap found.

## Bugs
None.

## Untested areas / notes
- `.obj` and `.tga` import in Unity: only the minimal meta was verified (gen_meta); no OBJ/TGA sample is imported in CI.
- The 1024 px texture cap is asserted as an importer setting only (sample is 256 px, the downscale itself is not exercised).
- Windows build was not launched (no Windows runner); only build success and artifact presence are verified.
- F-2 root cause (PNG null from Resources before fix) is not isolated (review W-3); diagnostics remain in the test.
- Character sample is ~329 units tall at file scale (review W-4): runtime bounds-fitting is mandatory for ORIGAMI-019.
