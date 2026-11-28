# Origami: workshop asset table (ORIGAMI-018)

Read this when you place the imported models in the workshop (ORIGAMI-019). Sources and licences: `Art/CREDITS.md`.
Pipeline rules: ARCHITECTURE §14, contract C-195. Room numbers: DATA_STRUCTURE D-130 / `Core/Room/RoomLayout.cs`.

**Style.** Kenney furniture and Quaternius characters are both flat-colour, cozy low-poly. Neither uses textures; each
material has one diffuse colour (Kenney `wood` = (0.83, 0.65, 0.42)). The two ambientCG surfaces are optional, low-contrast
512 px textures for the code-built floor and walls. If they clash with the flat models in playtest, drop them and use flat colours.

## Sets and Resource keys

| Set (folder `Art/<Set>/Resources/<Set>/`) | Source | Keys |
|---|---|---|
| `Workshop` | Kenney Furniture Kit (CC0) | `Workshop/Table`, `Workshop/Chair`, `Workshop/Bookshelf`, `Workshop/Books`, `Workshop/DisplayShelf`, `Workshop/Bed`, `Workshop/Nightstand`, `Workshop/TrashCan`, `Workshop/Door`, `Workshop/Window`, `Workshop/LampTable`, `Workshop/LampFloor`, `Workshop/Rug`, `Workshop/PottedPlant`, `Workshop/PlantSmall`, `Workshop/Box`, `Workshop/Paneling`, `Workshop/CoatRack` (18 models, `Resources.Load<GameObject>`) |
| `Characters` | Quaternius Ultimate Animated Character Pack (CC0) | `Characters/Crafter` (skinned + Legacy clips, `Resources.Load<GameObject>`) |
| `Surfaces` | ambientCG (CC0) | `Surfaces/WoodFloor`, `Surfaces/PlasterWall` (512×512 JPG, `Resources.Load<Texture2D>`) |

## How to read the numbers

- **Native size** is W × H × D in Unity axes (x × y × z) after import. Kenney FBX files are Y-up with `UnitScaleFactor` 100,
  so 1 file unit = 1 Unity unit (`useFileScale`, `globalScale` 1). The kit is built at about **0.2 m per unit**
  (a wall is 12.9 units ≈ 2.5 m and a door 10.1 units ≈ 2 m), so a plain scale of 0.2 keeps the kit's own proportions.
- **Pivot.** Every Kenney model has its origin far from the mesh. Asset Forge exports each part with a large
  `Lcl Translation` (for example, the table mesh sits around x ≈ 2.6, z ≈ −46.7). The "bottom centre" column is approximate:
  it was read from the ASCII FBX, with x negated for Unity's left-handed import. **Always fit with `Renderer.bounds`**
  (ARCHITECTURE §14.7): wrap the instance in an empty parent, compute the combined bounds at scale 1, apply the scale, then
  move the bottom centre of the bounds onto the target point. All models stand on y = 0 (min y = 0), except `Books`,
  `PlantSmall` and `Window`, which have min y > 0 (the bounds fit takes care of it).
- **Fit.** There are three modes:
  - `Box(sx, sy, sz)`: per-axis scale so the model fills the Core box exactly. Use it where a height matters (table top 0.75
    under the paper, counter top 1.0 under the display slots).
  - `Uniform(s)`: one scale. The resulting size is listed.
  - `Height(h)`: uniform, so the bounds height equals h.
- **Front.** Front is the side a user faces. Kenney `chair` and `bookcase*` have their back at native +z.
  **Rot** is the extra yaw (degrees about +y) to apply after fitting.

## Furniture: one row per Core `RoomLayout.Furniture` entry

| Core id (centre, W×D×H m) | Key | Native W×H×D (units) | ≈ bottom centre (pivot offset) | Fit | Result W×H×D (m) | Rot | Notes |
|---|---|---|---|---|---|---|---|
| `table` (2.5, 1.6), 1.4×0.9×0.75 | `Workshop/Table` | 8.415 × 3.267 × 4.474 | (2.61, 0, −46.66) | `Box(0.1664, 0.2296, 0.2012)` | 1.40 × 0.75 × 0.90 | 0 | Top board native y 2.78 to 3.27, so the top is at 0.750 m (`FoldTable` root y 0.75). Uniform 0.166 would make it 0.54 m high, so do not use uniform here. Material: `wood` |
| `chair` (2.5, 0.8), 0.5×0.5×0.45 | `Workshop/Chair` | 2.000 × 4.600 × 2.000 | (−26.80, 0, −39.80) | `Box(0.25, 0.20, 0.25)` | 0.50 × 0.92 × 0.50 | **180** | Kenney `chairCushion`. Backrest at native +z. The seated character faces +z (table), so rotate 180°. Cushion top native 2.4, so the seat is at 0.48 m (Core display height 0.45). Materials: `wood`, `carpet` (orange cushion) |
| `bookshelf` (0, 2.75), 1.6×0.4×2.0 | `Workshop/Bookshelf` | 8.000 × 7.900 × 2.500 | (−50.63, 0, 6.90) | `Box(0.20, 0.2532, 0.16)` | 1.60 × 2.00 × 0.40 | 0 | Kenney `bookcaseClosedWide`: back panel at native +z (against the north wall), open front faces −z (into the room). 3 bays. Shelf top surfaces at native y 0.7 / 3.1 / 5.5 / 7.9, which fit to 0.18 / 0.78 / 1.39 / 2.00 m. Inner depth ≈ 0.34 m. Fill the shelves with `Workshop/Books` (below) |
| `counter` (2.5, −2.3), 2.0×0.6×1.0 | `Workshop/DisplayShelf` **× 2** | 4.000 × 4.000 × 2.500 | (−70.63, 0, −0.20) | each `Box(0.25, 0.25, 0.24)` | each 1.00 × 1.00 × 0.60 | 0 | Two open low shelves side by side, centres **(2.0, −2.3)** and **(3.0, −2.3)**, together 2.0 × 0.6. Top surface native y 4.0, so it fits to **1.00 m** = `CounterTop`. The display slots (x 1.7 to 3.3, z −2.3) sit on top. Open on both sides (no back), so the shop view from the south camera shows the shelf boards. Material: `wood` |
| `bed` (−3.0, 2.0), 1.0×2.0×0.5 | `Workshop/Bed` | 5.710 × 3.750 × 11.250 | (12.22, 0, −9.72) | `Uniform(0.1751)` | 1.00 × 0.66 × 1.97 | 0 | Kenney `bedSingle`. Headboard and pillow at native +z, so the head is at the north wall. Mattress/cover top native 2.6, which is 0.455 m. Parts: `bedSingle`, `cover 1`, `pillow 1`. Materials: `wood`, `carpet` (orange cover), `carpetWhite`, `metal` |
| `trash` (3.6, 0.6), 0.4×0.4×0.5 | `Workshop/TrashCan` | 2.077 × 4.279 × 2.341 | (−26.76, 0, −18.48) | `Box(0.1926, 0.1168, 0.1709)` | 0.40 × 0.50 × 0.40 | 0 | Squat wastebasket, so the paper balls keep their current heights. For a taller bin use `Uniform(0.1709)` (0.36 × 0.73 × 0.40). Materials: `metal`, `metalDark` |
| `window` (−1.8, 3.0), 1.2×1.0 (centre y 1.5 in `WorkshopView`) | `Workshop/Window` **× 2** | 3.746 × 6.641 × 0.246 | (−46.18, 3.93, 40.40) | each `Uniform(0.1506)` | each 0.56 × 1.00 × 0.04 | 0 | A double casement: two frames, centres x **−2.082** and **−1.518**, bottom y **1.0**, z = 3.0 − 0.03 (in front of the north wall). Together 1.13 × 1.0. Extracted from Kenney `wallWindow` (only the window part, see CREDITS). Sub-materials `glass` (0.70, 0.83, 0.77; FBX opacity 0.5) and `wood`. To keep the day-to-sunset sky, tint the **`glass`** slot with `MaterialPropertyBlock` `_Color` = `WorkshopView.WindowColor` (`renderer.SetPropertyBlock(mpb, glassIndex)`), or keep the unlit sky quad behind and hide the glass slot. Check the glass transparency in CI/playtest |
| `door` (−3.0, −3.0), 1.0×2.1 | `Workshop/Door` | 4.860 × 10.095 × 1.134 | (−18.08, 0, −5.38) | `Uniform(0.2058)` | 1.00 × 2.08 × 0.23 | 0 | Kenney `doorway`: frame + closed door (parts `doorway`, `door 3`). Centre the depth on the wall plane z = −3.0. The south wall is only 0.5 m high (camera side), so a full 2.08 m door stands above it. Options: keep it as a free-standing door (reads well), or use the model only when the camera does not look through it. Developer's call. Materials: `wood`, `metal` (handle) |

## Props (decoration; not in Core and not blocking)

Core collision only knows the D-130 boxes, so the character can walk through these. The suggested spots are corners and
places the character cannot or will not reach: the expanded obstacle boxes are +0.3 m and the walls clamp the centre to
±3.7 / ±2.7. The SE corner behind the counter, (3.75, −2.75), is **unreachable**. The others overlap only the
character's 0.3 m radius at most.

| Key | Use | Native W×H×D | Fit | Result (m) | Suggested placement (x, y, z), rot |
|---|---|---|---|---|---|
| `Workshop/Books` | books on bookshelf shelves | 1.504 × 1.039 × 0.945 (min y 6.1) | `Uniform(0.20)` | 0.30 × 0.21 × 0.19 | 2 or 3 per shelf on the `Bookshelf` at y 0.18 / 0.78 / 1.39, z ≈ 2.70, x ∈ [−0.65, 0.65]. Vary rot 0/180 and x. Replaces the coloured book boxes |
| `Workshop/Nightstand` | bedside table | 2.660 × 2.632 × 2.173 | `Uniform(0.175)` | 0.47 × 0.46 × 0.38 | (−2.2, 0, 2.78), rot 0 (drawer front at −z) |
| `Workshop/LampTable` | table lamp (warm light) | 1.520 × 3.143 × 1.756 | `Uniform(0.12)` | 0.18 × 0.38 × 0.21 | on the nightstand: (−2.2, 0.46, 2.78). Material `lamp` (1, 0.91, 0.59). An optional warm point light is the developer's call |
| `Workshop/LampFloor` | floor lamp | 1.200 × 8.600 × 1.200 | `Height(1.6)` = 0.186 | 0.22 × 1.60 × 0.22 | (0.95, 0, 2.85), east of the bookshelf |
| `Workshop/Rug` | rug in the walking area | 15.700 × 0.100 × 9.200 | `Uniform(0.15)` | 2.36 × 0.015 × 1.38 | (0.0, 0, 0.3), rot 0. Flat, so walkable. Materials `carpet`, `carpetDarker` (orange) |
| `Workshop/PottedPlant` | plant | 2.121 × 6.540 × 2.415 | `Height(1.1)` = 0.168 | 0.36 × 1.10 × 0.41 | NE corner (3.72, 0, 2.72) |
| `Workshop/PlantSmall` | small pot | 0.947 × 1.400 × 0.947 (min y 3.47) | `Uniform(0.20)` | 0.19 × 0.28 × 0.19 | on top of the bookshelf (0.55, 2.0, 2.75) |
| `Workshop/Box` | paper stock box | 2.125 × 2.810 × 2.125 | `Uniform(0.15)` | 0.32 × 0.42 × 0.32 | SE corner (3.75, 0, −2.75), unreachable |
| `Workshop/CoatRack` | coat rack | 2.728 × 7.700 × 2.728 | `Height(1.7)` = 0.221 | 0.60 × 1.70 × 0.60 | west wall (−3.7, 0, −1.2) |
| `Workshop/Paneling` | wooden wainscot (optional) | 5.000 × 5.948 × 0.300 | `Box(0.20, 0.15, 0.20)` | 1.00 × 0.89 × 0.06 | tile along the N/E/W walls every 1.0 m, z/x = wall − 0.03, faces the room (rotate 90/−90 on the E/W walls). Skip where furniture covers the wall |

Paper stacks: Kenney has no paper model. Use code-built thin boxes with `_PipelineSample/PaperSwatch`, or the existing
`Materials.Lit` paper colours, on the table or counter shelves.

## Room shell (code-built planes + optional textures)

| Key | Use | Native | Tiling suggestion | Notes |
|---|---|---|---|---|
| `Surfaces/WoodFloor` | floor (8 × 6 m) | 512×512 JPG, warm light oak parquet | the source covers 1.8 × 1.8 m, so `mainTextureScale` ≈ (4.4, 3.3) on the 8 × 6 floor (UVs 0..1) | ambientCG WoodFloor051, darker than the Kenney `wood`, so the furniture stands out. Repeat wrap (pipeline) |
| `Surfaces/PlasterWall` | N/E/W/S walls | 512×512 JPG, cream plaster (warm tint baked in) | ≈ 1 tile per 2 m: north/south (4, 1.25), east/west (3, 1.25) | ambientCG Plaster001, low contrast. Further tint via `_Color` if needed |

Texture materials for code-built planes: `Materials.Lit` + `mainTexture` (ARCHITECTURE §14.5). Do not use `Shader.Find("Standard")`.

## Character: `Characters/Crafter`

| Item | Value |
|---|---|
| File | Quaternius `Casual_Male.fbx` (binary FBX, 1.6 MB): friendly casual craftsman (brown hair, blue shirt, dark trousers, belt). Byte-identical to `_PipelineSample/CasualMale`, so it already passed the 017 PlayMode pipeline test (meshes, Standard materials, Legacy clips) |
| Materials (6, flat colours) | `Belt`, `Face` (eyes, light), `Hair`, `Pants`, `Shirt`, `Skin` (near black, the pack's signature style) |
| Fit | `Height(1.6)` (`RoomLayout.CharacterHeight`) from the bind pose / Idle t = 0. `SkinnedMeshRenderer.bounds` uses the renderer's `localBounds`, so measure after `Sample()` of Idle at t = 0 (or use `sharedMesh.bounds` × renderer transform). In the pack's OBJ export the bind pose is ≈ 3.29 units tall and 1.28 wide (T/A-pose arms), so the scale should come out ≈ 0.49 if the FBX uses the same units. **Measure at runtime** |
| Front | Expected **+z** (in the OBJ export the face/eyes are at +z). If a test shows otherwise, add a 180° constant next to `ArtKeys` (ARCHITECTURE §14.7) |
| Legacy clips (short names, C-195) | `Idle`, `Walk`, `SitDown`, `StandUp`, `PickUp`, `Punch`, `Defeat`, `RecieveHit` (sic), `Shoot_OneHanded`, `Victory`, `Walk_Carry` |
| Suggested use | standing: `Idle` (loop). Moving: `Walk` (loop; adjust speed so the feet roughly match `WalkSpeed` 3.0 m/s, which is a fast walk). Sitting at the table: `SitDown` once, then hold its last frame. Standing up: `StandUp` once. Taking a book: `PickUp`. Sale / good ending: `Victory`. `Walk_Carry` while carrying a finished model to the counter (optional). Clip lengths: read `AnimationState.length` at runtime |
| Seat alignment | It is not known whether the `SitDown` end pose is a chair or a floor sit. Align the hips to the chair seat (0.48 m with the `Box` fit above) in playtest. Open issue for ORIGAMI-019 |

## Keys for tests (ORIGAMI-019 developer / QA)

All of these must load non-null from `Resources` and contain at least one `Renderer`. `Characters/Crafter` must also have an
`Animation` with `Idle`, `Walk`, `SitDown` and `StandUp`. The two textures must be `Texture2D` ≤ 1024 px:

```
Workshop/Table Workshop/Chair Workshop/Bookshelf Workshop/Books Workshop/DisplayShelf Workshop/Bed Workshop/Nightstand
Workshop/TrashCan Workshop/Door Workshop/Window Workshop/LampTable Workshop/LampFloor Workshop/Rug Workshop/PottedPlant
Workshop/PlantSmall Workshop/Box Workshop/Paneling Workshop/CoatRack
Characters/Crafter
Surfaces/WoodFloor Surfaces/PlasterWall
```

`Workshop/Window` is the only hand-edited FBX (child mesh extracted from `wallWindow`). It is the first file to check in
the CI import (one `MeshRenderer`, materials `glass` + `wood`).
