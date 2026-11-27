# Review ORIGAMI-013 — v2 Unity book screen (BookPanel / DiagramRaster / IBookHost)

- Reviewer: code-reviewer
- Base: 31100835 · Impl commit: 14257ae (branch claude/serene-fermat-4bwjlo)
- **Result: APPROVED_WITH_WARNINGS**

## Verification run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Origami ORIGAMI-013` | OK |
| `git diff --name-only 3110083` | only `Scripts/Book/**`, `Tests/PlayMode/Book/**` (+ .meta) and the task file; Core and other Runtime code unchanged |
| `bash studio/scripts/verify_core.sh Origami` | PASS, 972/972 |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| Unity CI `unity` run 46036542487 (head 0ed8137) | in_progress while I reviewed. Unity items are still open and QA must settle them |

## Checklist
1. **Spec / AC.**
   - The two-page spread is (even, odd). The host page is clamped with `BookLayout.ClampToSpread`.
   - The contents page has one button per model. The label is name + "보유"/"N원" + cover page, and clicking it calls `TurnPage(CoverPage)`.
   - Page 1 always shows the symbols.
   - An unowned cover shows a grey silhouette, page price and "사기" → `BuyPage`.
   - Step pages show "n / N", the §2 sentence, the before-state, the line type (valley dashes / mountain dash-dot / crease dots with a two-way arrow), the grab→drop arrow with a grab ring, the "위 n겹" glyph label, the anchor rings (only when `Anchors` is non-empty) and the after-inset at the bottom right.
   - The complete page shows "판매대에서 기본 N원". Sealed pages show only the sealed text.
   - The fixed CONTENT §3 texts are verbatim: `봉인됨 — 표지에서 페이지를 사세요`, `판매대에서 기본 N원`, `보유`, `N원`. The tests check them with their own literals (not by reading `BookTexts`), as the ORIGAMI-011 W-2 / DECISIONS row asks.
   - No test asserts 0.849/0.848 or a J-case percent. No test expects a null RightPage (QA-3).
   - F-007 AC-07 is left to the user playtest.
2. **Contract C-192.**
   - The public surface is exactly as specified. `IBookHost` has 5 members. `BookPanel` has Bind / Prepare / Draw / LeftPage / RightPage / DiagramTexture / ClickContents / ClickBuy / ClickTurn. `DiagramRaster` has Render / RenderShape.
   - Everything else is `internal`: BookPageView, BookButton, BookTexts, PixelCanvas, DiagramGlyphs, RenderSymbols and the palette.
   - `InternalsVisibleTo("Origami.Tests.PlayMode")` matches the `name` in `Tests/PlayMode/Origami.Tests.PlayMode.asmdef` exactly.
   - Every button goes through `Press` → `Click…` → host only. The panel never changes game state.
3. **Unity API / runtime.**
   - Textures are created with `new Texture2D(w,h,RGBA32,false)`, then wrap Clamp, filter Bilinear, `SetPixels32`, `Apply(false,false)`. They stay readable and use `HideFlags.DontSave`.
   - Using `DestroyImmediate` on runtime-only textures in `OnDestroy` is fine.
   - There is no Update, OnGUI, Awake, Start or RuntimeInitializeOnLoad, so nothing self-starts.
   - GUIStyles and `KoreanFont` are created lazily inside `Draw` only.
   - Paper +y maps to texture +y (row 0 = bottom). A test checks this, including that a 90° CCW rotation puts the top half on the left.
   - All drawing is on the CPU, so it does not depend on a GPU or fonts in headless CI.
4. **Null / invalid.**
   - Without a host: Prepare clears, the Clicks do nothing, DiagramTexture returns null.
   - Pages outside 0..43 → null.
   - Unknown or null IDs in ClickContents are ignored. ClickBuy forwards them, and Core rejects them (F-008 AC-02).
   - ClickTurn saturates instead of overflowing.
   - Render / RenderShape throw `ArgumentNullException` for null and `ArgumentOutOfRangeException` for a size outside 16..2048.
5. **Cache / memory.**
   - The key is (page, owned state of that page's model). This is exactly what a picture depends on, so it is equivalent to the contract's "(쪽, 보유 목록)" and has fewer misses.
   - A texture that was destroyed outside the panel is rebuilt (Unity-null check).
   - At most about 49 textures × 256 KB are kept. All of them are destroyed in OnDestroy, and a test checks this.
6. **Tests.**
   - There are 37 plain `[Test]`s with no frame waits.
   - Pixels are checked with exact palette colours on hard-edged interiors. This is safe with the 2× box downsample.
   - `FlowBookHost` drives a real `ScreenFlow` for F-008 AC-01/AC-03 and F-007 AC-03.
   - I checked the page numbers and prices in the tests against CONTENT §1/§3: plane 2..9, house 10..16, tulip 17..23 (step 4 = 21, top 2 layers), cicada step 9 = 41 (mountain), tulip 20/12, dog 50, cicada 150, plane 6, house 8.

## Findings

### W-1 (Low, convention) — display labels beyond the sanctioned fixed texts live in Runtime
- **Where:** `Scripts/Book/BookTexts.cs`. This covers "난이도", "동작 N번", "기법:", "기본가", "페이지 가격", "가진 돈", "골접기 · 전체" / "· 위 n겹", "N쪽", "← 앞" / "뒤 →", the symbol row labels and "<이름> 완성".
- **Problem:** CODING_CONVENTION says display texts live only in `Core/Texts`, `FoldTexts` and the catalog. The DECISIONS row of 2026-11-27 only allows copying the CONTENT §3 fixed sentences into Runtime. Core has no book labels at all, so the developer had to choose this wording.
- **Impact:** The wording is not dotnet-tested and is spread outside Core. It does not affect behaviour.
- **Fix:** Orchestrator either records a decision that book presentation labels are Runtime-owned, or schedules a later Core/Texts task that moves them to `GameTexts`. The wording itself matches CONTENT §3. The symbol rows follow the §3 list.

### W-2 (Low, architecture text) — the book view references `Runtime.UI.KoreanFont` and Core.Folding/Geometry
- **Where:** `BookPanel.cs` (`using Origami.Runtime.UI`, `KoreanFont.Get`) and `DiagramRaster.cs` (Folding/Geometry).
- **Problem:** ARCHITECTURE §2 says "Book view ─▶ Core.Book/Texts/Catalog만", and UI → Book view, so Book view → UI is the reverse direction. However, CODING_CONVENTION requires `KoreanFont.Get`, and task note QA-1 allows Folding/Geometry. Both are in one assembly, so it compiles.
- **Impact:** The documentation does not match the code. There is no cycle at assembly level.
- **Fix:** Architect updates the §2 arrows in the next doc pass: Book view → Core.Folding/Geometry and the shared `KoreanFont` helper. No code change.

### W-3 (Low, coverage) — `Draw(Rect)` is not exercised, and the buy button is always enabled on unowned covers
- **Where:** `BookPanel.Draw` / `DrawPage` and `BookPageView.Build` (`c.Buyable`).
- **Problem:** The IMGUI layout (text overflow at 45 % width, picture size) is not tested, which is unavoidable without OnGUI. The "사기" button also shows when money is short. Core rejects the purchase with NotEnoughMoney (spec: "보유하지 않았으면 … '사기' 버튼"), but the book itself gives no feedback.
- **Impact:** Layout problems would show up only in 014 or the playtest. A rejected purchase is silent unless the host shows the reject message.
- **Fix:** ORIGAMI-014 should (a) show `GameTexts.RejectMessage` for a rejected BuyPage while the book is open, and (b) include a smoke PlayMode test that calls `Draw` from a test OnGUI host, or record a playtest check.

### N-1 (Info, performance)
- The first view of a spread renders up to 2 textures synchronously. Each uses a 512² supersampled buffer (about 1 MB of temporary allocation, plus `DropSnap.MarkPieces`).
- This is acceptable (a one-off hitch per page, then cached). Pre-warming is optional.

### N-2 (Info)
- `DiagramTexture(1)` returns the internal `RenderSymbols` texture. C-192 says "도해/실루엣 텍스처", and the symbols picture is a reasonable reading of that. No public surface is added.

## Verdict
**APPROVED_WITH_WARNINGS.**
- The code conforms to C-192 and covers the owned ACs.
- The fixed CONTENT §3 texts are verbatim and checked as strings.
- I found no Unity 6000.3 API use that is a compile or runtime risk, and the tests do not depend on frame timing.
- Final acceptance still requires `unity (Origami)` compile, EditMode and PlayMode to pass in CI. Run 46036542487 was still in progress during this review.
