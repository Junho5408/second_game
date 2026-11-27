# Review: SANDBOX-005 C-004 / CODING_CONVENTION / ARCHITECTURE update (docs only)

- Reviewer: code-reviewer
- Base commit: 25758b7ed355e2fdf539764195d1a7e2d8a2c114
- Reviewed commits: 67028be (docs), 7104d9b (playtest reference), acb1675 (status)
- **Result: APPROVED**

## Changed files (task commits)
- Games/Sandbox/Docs/tech/INTERFACE_CONTRACTS.md (C-004 rewritten, change log row)
- Games/Sandbox/Docs/tech/CODING_CONVENTION.md (Runtime input/time rules, new "UI" section with IMGUI exception)
- Games/Sandbox/Docs/tech/ARCHITECTURE.md (UI dependency row, creation/display notes, external dependency table)
- Games/Sandbox/Tasks/SANDBOX-005.json (status/notes)
- (Games/Sandbox/Docs/DECISIONS.md also differs from base, but only through Orchestrator commit 070e6c3 - not part of this task.)

## Commands run
| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-005` | OK |
| `bash studio/scripts/verify_core.sh Sandbox` | PASS (87/87) |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` | OK |

No code changed, so no Unity verification is needed.

## Docs vs. shipped code (line-by-line check)
`ScoreCounterController.cs`:
- Public surface in C-004 = `ObjectName` const `"[ScoreCounter]"`, static `Instance { get; }`, `Current { get; private set; }`, static `EnsureInstance()`, `SubmitPress(double)`, `SubmitReset()`, `ProcessFrame(double,bool,bool,bool)`. Exactly matches the code; no public member missing or invented. Class is `public sealed ... : MonoBehaviour` as documented.
- `Instance`: fake-null normalised to real null (`s_instance != null ? s_instance : null`), cleared in `OnDestroy` only for itself - matches.
- `EnsureInstance`: static check -> `GameObject.Find(ObjectName)` (add component if missing) -> new GameObject + `DontDestroyOnLoad` + AddComponent. Matches; doc correctly does not claim DontDestroyOnLoad for the adopt branch.
- `SubmitPress`: Press then `Current = GetState(time)`; `SubmitReset`: Reset then `GetState(Time.unscaledTimeAsDouble)` -> Initial (C-001 GetState with no lastPress returns Initial). Matches.
- `ProcessFrame` order reset -> Space press -> click press -> always GetState(time). Matches. `Update` reads `Time.unscaledTimeAsDouble` once and passes `GetKeyDown(R)`, `GetKeyDown(Space)`, `GetMouseButtonDown(0)` - matches input mapping table.

`ScoreView.cs`:
- Public surface = static `Format(ScoreSnapshot)`, `Text { get; private set; }`, `Source { get; }`, `Bind(ScoreCounterController)`. Exactly matches.
- Format string `"Score: {0}\nCombo: {1}\nx{2}"`, InvariantCulture; example `new ScoreSnapshot(6,5,2)` uses the real ctor order (score, combo, multiplier) -> `"Score: 6\nCombo: 5\nx2"`; Initial `(0,0,1)` -> `"Score: 0\nCombo: 0\nx1"`. Correct.
- Text initial `Format(Initial)`, Source fake-null normalised, LateUpdate refresh, keep last text when source null, Awake picks same-GameObject controller, OnGUI `GUI.Label` with Rect(16,16,480,140), font 28, white, lazily-created style. All match.
- Creation: both `AfterSceneLoad` `Init` hooks go through `EnsureInstance`; view hook adds `ScoreView` if missing and calls `Bind`. Matches.

## Acceptance criteria
1. C-004 records ProcessFrame, EnsureInstance, Instance, ObjectName, ScoreView.Format/Bind/Text/Source and the display format - **met** (plus Current/SubmitPress/SubmitReset).
2. CODING_CONVENTION records the IMGUI exception referencing DECISIONS.md - **met** ("UI" section cites the 2026-11-27 decision with its exact title and SANDBOX-004).

Review W-2 of SANDBOX-004 (C-004 missing public surface; CODING_CONVENTION lacked pointer to IMGUI decision) - **resolved** by both items above.

## Consistency checks
- C-001..C-003: untouched except the "공통" line now scoped to C-001~C-003 (correct - C-004 is not engine-free). C-004 references to C-001 invalid-time handling agree with C-001's Press/GetState rows.
- DECISIONS.md: IMGUI decision (Orchestrator, SANDBOX-004) and user playtest of run 45928856363 are cited accurately; nothing contradicts them.
- Reports/build/SANDBOX-004.md: docs claim legacy Input verified only in Editor/batchmode PlayMode, standalone player not run by CI - matches the report wording ("W-3 is resolved for the Editor/PlayMode. The standalone player was built but not run"). Run id 45928856363 matches.
- `activeInputHandler` "not committed": confirmed - `Unity/ProjectSettings/` contains only ProjectVersion.txt.
- ARCHITECTURE dependency table now lists UI -> Runtime.Gameplay, consistent with the stated direction `Runtime.UI -> Runtime.Gameplay -> Core.Scoring` and the code (`using Sandbox.Runtime.Gameplay` in ScoreView only).
- CODING_CONVENTION "OnGUI draws only" plus the explicit lazy-GUIStyle allowance matches ScoreView.

## Findings
No blocking or warning findings.

### I-1 (info) Instance independence has one edge case
- Location: INTERFACE_CONTRACTS.md C-004 `Instance` row ("수동으로 AddComponent한 컨트롤러는 Instance를 대체하지 않는다").
- Problem: if no runtime instance exists and a test creates a GameObject named exactly `"[ScoreCounter]"`, `EnsureInstance` adopts that controller via `GameObject.Find`.
- Impact: negligible (tests use `TestScoreCounter`); documentation is accurate for normal use.
- Correction: optional - add "(이름이 ObjectName이 아닌 경우)" in a future docs touch.

### I-2 (info) Bind(null) not spelled out
- Location: C-004 `Bind` row says it immediately sets `Text = Format(controller.Current)`.
- Problem: with a null/destroyed controller, the code keeps the previous Text instead. The following bullet ("Source가 null이면 마지막 Text를 유지") covers this implicitly.
- Impact: none in practice.
- Correction: optional wording tweak; no action required.
