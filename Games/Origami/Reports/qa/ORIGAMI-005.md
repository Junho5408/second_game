# QA ORIGAMI-005 — Unity 컨트롤러·화면

- QA: qa-engineer · Commit tested: b047b9a (impl e493187) · Review: APPROVED_WITH_WARNINGS
- CI evidence: `Reports/build/ORIGAMI-005.md` (run 45948502253, job `unity (Origami)` success)
- **Result: PASS → COMPLETED**

## Commands run
| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | PASS — 283 passed, 0 failed |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| CI artifact `Origami-test-results` (XML parsed) | EditMode 283/283, PlayMode 21/21, 0 failed/skipped |
| CI job log `unity (Origami)` | build Succeeded, `Origami.exe`, artifact `Origami-windows-build` 34.2 MB |

## Task acceptance criteria
| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | C-030/C-031 OrigamiController/OrigamiView/ModelIcons, exactly one `[Origami]` object, buttons and keys call the same Flow methods | **PASS** | Code: `ProcessFrame` = `Flow.ProcessKeys`; every `OrigamiButtons.For` entry is one Flow call; `OrigamiView.DrawButtons` draws only that list and calls `button.Invoke()`. CI: `RuntimeInstance_IsCreatedOnce_WithBoundView` (1 controller, 1 view, EnsureInstance idempotent, manual controller does not replace Instance), `F007_AC05_*` (4 tests), `Buttons_PerScreen_MatchContractLabels` — Passed |
| 2 | Only code-drawn shapes, no assets | **PASS** | Code: textures built by `ModelIcons` (SetPixels32) + built-in `Texture2D.whiteTexture`; no `Resources.Load`/`AssetDatabase`/`LoadImage`/`.png`/`.ttf` in `Scripts/` (review grep, re-checked). CI: `ModelIcons_FiveModels_DistinctCodeGeneratedTextures`, `ModelIcons_Paper_IsSolidSquare` Passed; build report shows no image/font assets of the game (user assets 3.2 MB = built-in splash/extra) |
| 3 | Timing-independent PlayMode tests for F-007 AC-01/05/08 and a single runtime instance | **PASS** | Wiring tests are synchronous `[Test]`s calling `ProcessFrame`/`Invoke`/`Refresh`; the one `[UnityTest]` only waits 2 frames for AfterSceneLoad hooks. 21/21 Passed in CI |

## F-007 (Unity-side)
| AC | Result | Evidence |
|---|---|---|
| AC-01 day "1/7", money 20, attempts "5/5", popular "종이비행기" right after start | **PASS (text)** | `F007_AC01_NewController_ViewShowsDay1Money20Attempts5PopularPlane` and the runtime-instance test assert `Lines[0]="일차 1/7   돈 20원   시도 5/5"`, `Lines[1]="오늘의 인기 작품: 종이비행기"` on the auto-created `[Origami]` object — Passed. Visual rendering of these lines: playtest |
| AC-05 button = key | **PASS** | `F007_AC05_EachActionKeyAndButton_GiveSameResult`, `_FullFoldByKeysAndByButtons_SameResultAtEveryStep`, `_AbandonByKeysAndByButtons_SameResult`, `_PreviewEnterAndEscape_MatchStartAndCloseButtons` — Passed |
| AC-07 code-drawn graphics, no external image assets | **PASS** | See task AC-2 |
| AC-08 Esc confirm / cancel / Esc+Enter abandon, ↑ ignored while confirm open | **PASS** | `F007_AC08_EscConfirm_CancelConfirm_ThenEnterAbandons`, `F007_AC08_EscAndEnterSameFrame_OnlyOpensConfirmation` — Passed |
| AC-02/03/04/06/09 | covered at text level by Core Flow tests (ORIGAMI-004; EditMode 283/283) | not re-tested here |

## Abnormal / edge scenarios checked
- No real input in batchmode: `ReadKeys_WithoutRealInput_IsNone` Passed; runtime object's `Update` polled Input during the PlayMode run with no error log.
- Font creation in batchmode: `KoreanFont` catches all exceptions and never logs; logs contain no font error; PlayMode would fail on an unexpected error log and did not.
- Stale button after screen change ignored; fold keys outside Folding do nothing; multi-key frame takes only top-priority key (A-4) — Passed.
- Unity editor `ScriptableSingleton already exists` LogErrors in the log are Package Manager internals (environment), not game code.

## Tests added by QA
None. The developer's 20 new PlayMode tests cover the Unity-side criteria; the rest is Core-tested.

## Bugs
None found (no Critical/High/Medium/Low defect reproduced).

## Untested — needs a user playtest on Windows (`Origami-windows-build`)
1. **Korean glyphs** (review W-3): Hangul shows correctly (not boxes) with Malgun Gothic; CI cannot check font rendering.
2. **Layout**: lines (left, 760 px) and buttons (right, from y=230, up to 7 on Folding) fit at 1280×720 and other resolutions/aspect ratios without overlap or clipping; long settlement/notice texts wrap and stay on screen.
3. **Real input**: arrow/WASD, Space, H, Esc, Enter/KeypadEnter via `Input.GetKeyDown`; mouse clicks on IMGUI buttons; no double action from key + focused button.
4. **Graphics appearance**: model icons (plane/boat/frog/crane/dragon), locked tint on workbench, paper + progress bar on Folding, icon on completion notice.
5. Full 7-day run to an ending in the built player (only simulated at Core level).

Non-blocking follow-ups (from review): W-1 contract doc (moved to ORIGAMI-006), W-2/N-1/N-2 minor performance.
