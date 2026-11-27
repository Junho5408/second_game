# QA: SANDBOX-005 C-004 / CODING_CONVENTION / ARCHITECTURE update (docs only)

- QA: qa-engineer
- Base commit: 25758b7 · Tested at: 9869d13 (review APPROVED)
- Task commits: 67028be, 7104d9b, acb1675
- Verification items: `review` only. No code changed, so there are no Unity items.
- **Result: PASS → COMPLETED** (0 bugs. 2 Low documentation observations, not blocking.)

## Commands run
| Command | Result |
|---|---|
| `bash studio/scripts/verify_core.sh Sandbox` | PASS (87/87) |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` | OK |
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-005` | OK |

## Acceptance criteria
| # | Criterion | Result | Evidence |
|---|---|---|---|
| AC-1 | C-004 records the public API added by SANDBOX-004 (ProcessFrame, EnsureInstance, Instance, ObjectName, ScoreView.Format/Bind/Text/Source) and the display format | PASS | INTERFACE_CONTRACTS.md C-004 lists both classes with full signatures. Against `ScoreCounterController.cs` and `ScoreView.cs`, every public member is present, nothing is invented, and the class modifiers (`public sealed ... : MonoBehaviour`) and `private set` notes are correct. Format `"Score: {0}\nCombo: {1}\nx{2}"` uses InvariantCulture. The examples `(6,5,2)` → `"Score: 6\nCombo: 5\nx2"` and Initial → `"Score: 0\nCombo: 0\nx1"` match the PlayMode test `Format_ShowsScoreComboAndMultiplier` exactly. The C-003 ctor order (score, combo, multiplier) is right. |
| AC-2 | CODING_CONVENTION records the IMGUI exception and points to DECISIONS.md | PASS | New "UI" section: the default is UI Toolkit, the exception is IMGUI, and it cites the DECISIONS.md entry by date, title and SANDBOX-004 (that entry exists word for word). |

## Spot checks: docs vs. code vs. PlayMode tests (Tests/PlayMode/Scoring/ScoreCounterControllerTests.cs)
| Documented behaviour | Code | Test | OK |
|---|---|---|---|
| `Current` starts as `Initial` | property initializer | `Current_StartsAtInitial` | yes |
| `SubmitPress`: Press, then GetState(time). Invalid times are ignored | yes | `SubmitPress_InvalidTimes_AreIgnored` (NaN, backwards, +Inf) | yes |
| `SubmitReset` → always Initial | Reset + GetState(unscaled). C-001 `GetState` returns Initial with no last press (ScoreCounter.cs:47) | `SubmitReset_ReturnsToInitial_AndNextPressIsFirst` | yes |
| `ProcessFrame` order: reset → Space → click → always GetState | yes | `..._SpaceAndClickSameFrame_CountAsTwoPresses`, `..._ResetAndPressSameFrame_ResetFirst` (1,1,1), `..._NoInput_ShowsComboExpiry` | yes |
| Input mapping: R / Space / left mouse via `GetKeyDown`/`GetMouseButtonDown`, time read once | `Update` | (not testable in batchmode) | yes |
| `Instance` is normalised to real null and cleared in OnDestroy; `EnsureInstance` is idempotent; Find, then adopt, then create with DDOL | yes | `RuntimeInstance_IsCreatedOnce_WithView` | yes |
| A test controller made with `new GameObject().AddComponent` is independent of `Instance` | yes (the name "TestScoreCounter" ≠ ObjectName) | the test helper uses exactly this pattern | yes (see review I-1 for the ObjectName edge case) |
| `Bind` refreshes Text at once. LateUpdate refresh. Null Source keeps the last Text. Awake auto-binds a controller on the same GameObject | yes | `View_ShowsCurrentAfterFrame` | yes |
| Both AfterSceneLoad hooks go through `EnsureInstance`, so hook order does not matter | yes | `RuntimeInstance_IsCreatedOnce_WithView` (view.Source == controller) | yes |
| IMGUI: lazy GUIStyle inside OnGUI, OnGUI only draws. Rect/font are private, not part of the contract | yes | n/a | yes |
| ARCHITECTURE: UI → Gameplay → Core, Gameplay never references UI | only ScoreView has `using Sandbox.Runtime.Gameplay`. The controller has no UI reference | n/a | yes |
| Legacy Input: CI-verified in Editor PlayMode, standalone checked by user playtest. `activeInputHandler` not committed | Reports/build/SANDBOX-004.md, DECISIONS.md row (run 45928856363). `Unity/ProjectSettings/` holds only ProjectVersion.txt | n/a | yes |

Extensibility check: a developer can add a new input or view from the docs alone. The fixed public surface, the change-request rule, the format-change-is-contract-change rule, the dependency direction and where to record a UI-technology change are all stated.

## Observations (Low, documentation only, not blocking)
1. **OBS-1 (Low): no warning that a test controller's own `Update` keeps running.** C-004 "테스트 가능성" says tests call `SubmitPress`/`ProcessFrame` directly and that a test controller is independent of `Instance`. It does not say that the test controller is still an active MonoBehaviour. Every frame its `Update` overwrites `Current` with `GetState(real unscaled time)`. It also does not say that a `UnityTest` coroutine resumes after `Update` but before `LateUpdate`, so `ScoreView.Text` lags by one frame. This is exactly the pitfall behind BUG-SANDBOX-004-1. Right now the only documentation of it is the XML comment in the test file. A developer can work it out from "Update → ProcessFrame → always GetState", so this is not a wrong statement. Suggestion: in a future docs touch, add one line to "테스트 가능성": assert synchronously, or for frame tests yield twice and use time-independent states.
2. **OBS-2 (Low): ARCHITECTURE "생성 위치" says the `"[ScoreCounter]"` object is DontDestroyOnLoad without qualification.** That holds only when `EnsureInstance` creates the object. An adopted pre-existing object is not marked. C-004 states this precisely, so the contract is correct. Same edge case as review I-1.

Review I-2 (Bind with a null controller keeps the previous Text) is confirmed in the code (`Refresh` returns early). The following bullet in C-004 covers it.

## Tests added
None. The task is docs only and its only verification item is `review`. New PlayMode tests (for example, Awake auto-bind with no Bind call, or Bind(null)) would need a Unity CI run that this task does not have. They can be done in a later Unity task.

## Untested areas
- Standalone-player input and rendering: not verifiable by CI. Covered only by the user playtest recorded in DECISIONS.md.
- No Unity run was made for this task, and none was needed because no code changed.
