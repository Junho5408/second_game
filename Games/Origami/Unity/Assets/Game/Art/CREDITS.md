# Origami: art credits (master list)

Every art file shipped with Origami, one row per file. **All assets are CC0 1.0** (public-domain dedication; attribution not
required, given anyway). Rules: ARCHITECTURE §14.8. Original zips are **not** committed: only the files listed here.
Owner: content-designer. Asset table with Resource keys, sizes and placement: `Art/ASSETS.md`.

## Sources

| Id | Pack | Author | Source page | License (how verified) | Download route (fetched 2026-11-28) |
|---|---|---|---|---|---|
| K | "Furniture Kit" (zip: *Furniture Pack 1.0*) | Kenney (www.kenney.nl) | https://opengameart.org/content/furniture-kit | CC0 1.0: page field "License(s): CC0" + `License.txt` in the zip ("Creative Commons Zero, CC0") | `https://opengameart.org/sites/default/files/kenney_furniturePack.zip` (5,580,414 B) |
| Q | "Ultimate Animated Character Pack - Nov 2019" | Quaternius | https://opengameart.org/content/animated-characters-pack | CC0 1.0: page field "License(s): CC0" | `https://opengameart.org/sites/default/files/ultimate_animated_character_pack_by_quaternius.zip` (48,430,012 B) |
| A1 | "Wood Floor 051" | ambientCG (ambientcg.com) | https://ambientcg.com/view?id=WoodFloor051 | CC0 1.0: https://docs.ambientcg.com/license/ (all ambientCG assets) | `https://ambientcg.com/get?file=WoodFloor051_1K-JPG.zip` (5,104,319 B) |
| A2 | "Plaster 001" | ambientCG (ambientcg.com) | https://ambientcg.com/view?id=Plaster001 | CC0 1.0: same | `https://ambientcg.com/get?file=Plaster001_1K-JPG.zip` (7,414,156 B) |
| A3 | "Paper 003" | ambientCG (ambientcg.com) | https://ambientcg.com/view?id=Paper003 | CC0 1.0: same | `https://ambientcg.com/get?file=Paper003_1K-JPG.zip` (2.5 MB) |

## Files

| Repository file (under `Art/`) | Src | Original file | Bytes | Changes | Added |
|---|---|---|---|---|---|
| `Workshop/Resources/Workshop/Table.fbx` | K | `Models/table.fbx` | 24,614 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Chair.fbx` | K | `Models/chairCushion.fbx` | 28,023 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Bookshelf.fbx` | K | `Models/bookcaseClosedWide.fbx` | 44,501 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Books.fbx` | K | `Models/books.fbx` | 19,460 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/DisplayShelf.fbx` | K | `Models/bookcaseOpenLow.fbx` | 26,397 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Bed.fbx` | K | `Models/bedSingle.fbx` | 40,651 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Nightstand.fbx` | K | `Models/cabinetBedDrawerTable.fbx` | 29,572 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/TrashCan.fbx` | K | `Models/trashcan.fbx` | 21,667 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Door.fbx` | K | `Models/doorway.fbx` | 36,950 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Window.fbx` | K | `Models/wallWindow.fbx` | 15,885 | **edited**: kept only the child mesh `window` (frame + glass). Removed the wall mesh `wallWindow`, its geometry, its two unused materials (`metalDark`, `_defaultMat`) and their connections; `window` re-parented to the root with translation = parent + child (48.052426, 3.927084, 40.17498); line endings CRLF to LF. Geometry, UVs and the `glass`/`wood` materials untouched | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/LampTable.fbx` | K | `Models/lampRoundTable.fbx` | 20,495 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/LampFloor.fbx` | K | `Models/lampSquareFloor.fbx` | 17,251 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Rug.fbx` | K | `Models/rugRounded.fbx` | 30,261 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/PottedPlant.fbx` | K | `Models/pottedPlant.fbx` | 23,829 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/PlantSmall.fbx` | K | `Models/plantSmall1.fbx` | 25,486 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Box.fbx` | K | `Models/cardboardBoxClosed.fbx` | 17,647 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/Paneling.fbx` | K | `Models/paneling.fbx` | 15,583 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Workshop/Resources/Workshop/CoatRack.fbx` | K | `Models/coatRackStanding.fbx` | 33,353 | renamed only | 2026-11-28 (ORIGAMI-018) |
| `Characters/Resources/Characters/Crafter.fbx` | Q | `FBX/Casual_Male.fbx` | 1,612,396 | renamed only (byte-identical to `_PipelineSample/CasualMale.fbx`, which already passed the ORIGAMI-017 pipeline test) | 2026-11-28 (ORIGAMI-018) |
| `Surfaces/Resources/Surfaces/WoodFloor.jpg` | A1 | `WoodFloor051_1K-JPG_Color.jpg` (1024²) | 40,030 | downscaled to 512×512 (Lanczos), JPEG q85 | 2026-11-28 (ORIGAMI-018) |
| `Surfaces/Resources/Surfaces/PlasterWall.jpg` | A2 | `Plaster001_1K-JPG_Color.jpg` (1024²) | 21,704 | downscaled to 512×512 (Lanczos), warm tint baked in (RGB × 1.00/0.94/0.84), JPEG q85 | 2026-11-28 (ORIGAMI-018) |
| `_PipelineSample/Resources/_PipelineSample/Chair.fbx` | K | `Models/chair.fbx` | 25,338 | renamed only | 2026-11-28 (ORIGAMI-017) |
| `_PipelineSample/Resources/_PipelineSample/CasualMale.fbx` | Q | `FBX/Casual_Male.fbx` | 1,612,396 | renamed only | 2026-11-28 (ORIGAMI-017) |
| `_PipelineSample/Resources/_PipelineSample/PaperSwatch.png` | A3 | `Paper003_1K-JPG_Color.jpg` | 20,447 | downscaled 1024 to 256 (Lanczos), saved as PNG | 2026-11-28 (ORIGAMI-017) |

## Size budget (DECISIONS 2026-11-28: ≤ 5 MB per file, ≤ 30 MB art per game)

- ORIGAMI-018 adds 2,145,755 B of asset data (≈ 2.1 MB; largest file `Crafter.fbx` 1.6 MB).
- With the ORIGAMI-017 samples (1,658,181 B), the art total is 3,803,936 B (≈ 3.8 MB) of the 30 MB budget (excluding `.meta`/`.md`).
- If ORIGAMI-019 deletes `_PipelineSample/` (with its tests), the duplicate 1.6 MB character goes away.
