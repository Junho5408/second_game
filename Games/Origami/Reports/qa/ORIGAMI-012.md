# QA — ORIGAMI-012 (v2 Unity fold table)

- QA: qa-engineer · Branch `claude/serene-fermat-4bwjlo` · tested commit `1c4521f` (merge of `e35a16f` impl + `5562237` status + review `d23c9ae`)
- Review: APPROVED_WITH_WARNINGS (W-1 checked here)
- **Result: PASS → COMPLETED** (AC-19 user playtest pending, not a blocker)

## Verification run
| Command / source | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Origami` | PASS (803/803 dotnet; the working tree also holds another agent's uncommitted ORIGAMI-010 QA tests) |
| `python3 studio/scripts/check_meta.py Games/Origami/Unity` | OK |
| CI `unity` run [46028807396](https://github.com/Junho5408/second_game/actions/runs/46028807396), job `unity (Origami)`, head `1c4521f` | success (see `Reports/build/ORIGAMI-012.md`) |
| `playmode-results.xml` (artifact `Origami-test-results`) | 54/54 Passed, 0 inconclusive, 0 skipped; all 33 `Origami.Tests.PlayMode.FoldTableTests.*` Passed |
| `editmode-results.xml` | 735/735 Passed, 0 inconclusive, 0 skipped |
| `error CS` in editmode/playmode logs | 0 |

## Acceptance criteria
| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | IFoldTableHost contract, code-built meshes, ray–plane picking (no Physics/Collider/CreatePrimitive), Core applied on release and then the animation | PASS | Review §2–3 (static). `F005_AC17_CornerGrab…` asserts 0 host calls while dragging, 1 on release, Core FoldCount 1 at once, `IsAnimating` afterwards. The `ScreenToPaper_*` tests (ray–plane) Passed in CI |
| 2 | F-005 AC-17 | PASS | `F005_AC17_CornerGrab_Preview_Release_AnimatesThenFoldCountIsOne`: grab at (-0.9,0.9), which is 0.141 < 0.16 from the corner. Ghost and flap are active, release gives FoldCount 1, the animation runs at 0.2 s and ends at 0.45 s. `F005_AC17_EscapeWhileDragging_CancelsWithoutHostCall`: 0 calls, count 0. Both Passed in CI |
| 2 | F-005 AC-18 | PASS | `F005_AC18_DiamondBase_TwoWheelNotches…`: grab (−1,−1) on the 6-facet diamond base; two notches show "겹: 위 1 / 6" then "겹: 위 2 / 6"; release calls Fold with `LastLayers == 2`, giving 8 facets. `F005_AC18_CtrlRelease…`: 1 Crease call, 0 Fold calls, `CreaseLineCount` asserted. Both Passed in CI |
| 2 | F-006 AC-09 | PASS | `F006_AC09_GuideMarkersRenderOnlyWhileTheHostHasAGuide`: no guide → no markers; guide → markers and GuideBall shown; second sheet without a guide → none. `F006_AC09_NoSheet…` and `GuideMarkers_HiddenWhileAnimating` also cover it. I checked statically that the red dashed crease, the green ring/disc and the yellow ball are all drawn only inside the `hasGuide` branch of `FoldTable.RefreshOverlay`. All Passed in CI |
| 2 | F-005 AC-19 (user playtest) | PENDING | Needs the user's own check. Recorded here; not a blocker per the task |
| 3 | PlayMode tests pass dt directly; namespace does not hide FoldTable | PASS | All tests call `ProcessFrame(frame, dt)` and use `[Test]` only. The namespace is `Origami.Tests.PlayMode.FoldTableTests`, and it compiled in CI |
| 4 | Wheel ±1 per frame; Materials fall back silently | PASS | `N3_WheelCountsOnlyBySign_OneLayerPerFrame` and `N4_Materials_AlwaysReturnAMaterialWithAShader` Passed. No unexpected-log failures in PlayMode |
| 5 | Public surface per INTERFACE_CONTRACTS; no assertions on 0.849/0.848 or J-case percentages | PASS | Review §2 (one documented deviation: `IReadOnlyList`, N-2). `grep` of the FoldTable tests finds no 0.849, 0.848 or percent assertions |
| 6 | verify_core, check_meta, Unity CI compile, EditMode and PlayMode | PASS | Table above |

## Review W-1 (camera tests could be Inconclusive)
From `playmode-results.xml`, all three tests show `result="Passed"`, not Inconclusive:
- `FoldTableViewTests.ScreenToPaper_RoundTripsAPaperPoint` — Passed
- `FoldTableViewTests.ScreenToPaper_FollowsSetTablePose` — Passed
- `FoldTableViewTests.ScreenToPaper_FalseWithoutCameraOrLookingUp` — Passed

The run header reports `inconclusive="0" skipped="0"`. In the game-ci batch environment the camera had a non-empty viewport, so the `Assume` held. This is still environment-dependent: if the runner image changes, they could become Inconclusive again. The fixed `targetTexture` hardening suggested in the review stays optional.

## Bugs
None found.

## Tests added
None. The existing 33 PlayMode tests cover the owned ACs with the spec values, so I did not add anything that would need another CI run.

## Untested areas
- AC-19 feel and learnability: user playtest pending.
- Real mouse, keyboard and frame timing: the tests inject `PointerFrame`s. Real input wiring belongs to ORIGAMI-014.
- Visual appearance (colours, mesh shading, how the animation looks): not asserted. Only active state and counts are checked.
- Review W-2 (static mesh rebuilt on every drag frame): performance not measured.
