# _PipelineSample — credits

Pipeline verification samples (ORIGAMI-017). All CC0 1.0 (public domain dedication, no attribution required;
credited anyway). Loaded by `Tests/PlayMode/AssetPipeline/` from `Resources` key `_PipelineSample/<Name>`.
The master list for the whole game is `Art/CREDITS.md` (content-designer); copy these rows there.

| File (`Resources/_PipelineSample/`) | Original | Author | Source page | License | Download route (2026-11-28) | Size | Changes |
|---|---|---|---|---|---|---|---|
| `Chair.fbx` | `Models/chair.fbx` from "Furniture Kit" (Furniture Pack 1.0) | Kenney (kenney.nl) | https://opengameart.org/content/furniture-kit | CC0 1.0 (page license field + `License.txt` in the zip) | `https://opengameart.org/sites/default/files/kenney_furniturePack.zip` (5.6 MB zip) | 25,338 B (ASCII FBX 7.3, 1 material "wood", no texture) | renamed only |
| `PaperSwatch.png` | `Paper003_1K-JPG_Color.jpg` from "Paper 003" | ambientCG | https://ambientcg.com/view?id=Paper003 | CC0 1.0 (https://docs.ambientcg.com/license/) | `https://ambientcg.com/get?file=Paper003_1K-JPG.zip` (2.5 MB zip) | 20,447 B (256×256 RGB PNG) | downscaled 1024→256 (Lanczos), saved as PNG |

Total: 45,785 B (Chair + PaperSwatch) of the 30 MB budget (DECISIONS 2026-11-28).

ORIGAMI-019 deleted `CasualMale.fbx` (1,612,396 B, Quaternius `Casual_Male.fbx`, CC0): it was byte-identical to
`Art/Characters/Resources/Characters/Crafter.fbx`, so the pipeline tests now load `Characters/Crafter` instead
(DECISIONS 2026-11-28; the character's credit row is in `Art/CREDITS.md`).
