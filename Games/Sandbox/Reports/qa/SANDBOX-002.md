# QA: SANDBOX-002 점수 카운터 인터페이스 설계 (architecture docs + Core skeleton)

- QA: qa-engineer
- Base commit: 0075b01 · tested commits: c15fbde (contract + skeleton), 1dbd773 (status), 111cb97 (review)
- Verification items in the task: `core-tests`, `review` (no `unity-*` items)
- **Result: PASS → COMPLETED**

Standard I applied: could SANDBOX-003 (Core implementation + tests) and SANDBOX-004 (Unity controller/UI +
PlayMode tests) be built and tested from these documents without guessing? Ambiguity that blocks either task
counts as a High bug.

## Commands run

| Command | Result |
|---|---|
| `python3 studio/scripts/check_scope.py Sandbox SANDBOX-002` | OK |
| `python3 studio/scripts/check_meta.py Games/Sandbox/Unity` (before and after adding QA tests) | OK |
| `bash studio/scripts/verify_core.sh Sandbox` (before QA tests) | PASS, 1/1 |
| `bash studio/scripts/verify_core.sh Sandbox` (with QA tests) | PASS, 16/16 |
| `grep -rn UnityEngine Games/Sandbox/Unity/Assets/Game/Core` | no matches; `Sandbox.Core.asmdef` has `noEngineReferences: true` |
| Scratch contract oracle, outside the repo (`dotnet test`, net8.0, C# 9) | PASS, 28/28 |

**The contract oracle.** I wrote my own `ScoreCounter`/`ScoreRules` from the text of INTERFACE_CONTRACTS.md
C-001/C-002 alone, without looking at anything else. I used the committed `ScoreSnapshot` with it and wrote a
test for every F-001 acceptance criterion, AC-01 to AC-22. AC-14, AC-20 and AC-22 were tested with NaN, +Inf and
−Inf. AC-20 also covers a new counter queried at NaN, +Inf and −Inf (the QA N-3 item), and I added a test of the
full multiplier table from combo −1 to 21. All 28 tests passed on the first run. Every time I had to make an
implementation choice, the contract already said which way to go:
- the validity order for `Press`;
- `<=` versus `>` at the 1.0 s boundary;
- expiry only when `now` is finite;
- `Initial` when there has been no press;
- `Reset` takes no time.

The oracle is QA evidence only. It is not committed.

## Acceptance criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | INTERFACE_CONTRACTS.md records the ScoreCounter contract (signatures and behaviour) | **PASS** | C-001 to C-004 give signatures, a behaviour table per member, boundaries, determinism, int range and threading. Every F-001 AC/edge case and R-001 to R-006 maps to a contract row, with no contradictions. The oracle built from the contract text alone passes all 22 ACs. |
| 2 | Core/Scoring has a C# skeleton that shows the contract and compiles (behaviour may be missing) | **PASS** | `ScoreCounter.cs`, `ScoreRules.cs` and `ScoreSnapshot.cs` compile under `verify_core.sh`. A reflection test pins the public surface to C-001/C-002, so nothing is added or missing. The constants are 1, 1.0 and 4. Unimplemented bodies throw `NotImplementedException("SANDBOX-003")`. `ScoreSnapshot` is complete, and its behaviour is verified by 12 QA tests. |

Docs consistency: ARCHITECTURE.md (module table, ownership, time source, self-creation), DATA_STRUCTURE.md
(internal state, ranges, constants) and CODING_CONVENTION.md (sub-namespaces, no clock reads in Core) all agree with
C-001 to C-004 and with GAME_RULES "기본 수치".

## Review warnings judged for downstream buildability

| Item | Blocks 003? | Blocks 004? | Judgement |
|---|---|---|---|
| W-1 PlayMode clock race | No | **No (has a workaround)** | C-004 says the time source is `Time.unscaledTimeAsDouble`, so a test that follows the contract passes real time to `SubmitPress`. A test can also assert `Current` straight after `SubmitPress` without yielding. This is a trap only for a test that uses synthetic times like 0.0 *and* yields frames. It is a test-design detail that SANDBOX-004 decides inside its own scope (`Tests/PlayMode/Scoring/`). Not a bug. Recommendation R-1 below. |
| W-2 `Current` initial value / view order | No | **No** | C-003 already says `default(ScoreSnapshot)` is invalid and that `ScoreSnapshot.Initial` is the initial value. `SubmitPress(time)` → `GetState(time)` is the only reading that agrees with "프레임당 한 번 읽은 t를 모든 Press/GetState에" ("read t once per frame and pass it to every Press/GetState"). After `SubmitReset`, `GetState(any)` returns `Initial` by C-001. The view refresh order is presentation, and C-004 leaves it explicitly to SANDBOX-004 ("문구·배치는 SANDBOX-004 결정", wording and layout are SANDBOX-004's call). A one-frame lag is a test-design detail. Not a bug. Recommendation R-2. |
| W-3 legacy Input Manager unverified | No | Risk, not ambiguity | The contract is clear: use legacy `Input`, and there is no `com.unity.inputsystem` package in `Packages/manifest.json`. Only `ProjectVersion.txt` is committed, so whether the `activeInputHandler` Unity generates allows the old Input Manager cannot be checked here without Unity. I have not seen it fail, so it is not a bug. SANDBOX-004 has `unity-compile` and `unity-playmode` CI items that will show it. There are two ways out: (a) an architect task commits `ProjectSettings.asset` with Input Manager or Both, which is outside SANDBOX-004's scope; (b) the controller confines the `Input.*` polling so a misconfiguration cannot hide the `SubmitPress` path. Recorded below as an untested area / risk. |
| N-1 duplicate-guard placement | No | No | C-004 puts the guard in the static `RuntimeInitializeOnLoadMethod` ("정적 메서드…에서…중복 생성을 막는다", the static method prevents duplicate creation), not in `Awake`. So a test can use the auto-created `"[ScoreCounter]"` instance after `SubmitReset()`, or add its own component. Note only. |
| N-2 UI Toolkit theme | No | No | C-004 allows "UI Toolkit 또는 코드 생성 UI" (UI Toolkit or code-built UI). uGUI and IMGUI modules are in the manifest, and a `.tss` under `Scripts/UI/` is inside SANDBOX-004's scope. This is an implementation hint, not an ambiguity. |

Conclusion: no ambiguity blocks SANDBOX-003 or SANDBOX-004. **No bugs filed.**

## Recommendations (not bugs; for the Orchestrator to pass to SANDBOX-004 or the architect)

- **R-1 (W-1):** PlayMode tests that yield frames should pass `Time.unscaledTimeAsDouble` to `SubmitPress`, or
  an offset from it that stays inside the 1.0 s window. Otherwise they should assert `Current` before yielding.
- **R-2 (W-2):** Set `Current = ScoreSnapshot.Initial` at construction. `SubmitPress(t)` refreshes with
  `GetState(t)`. Tests that read the on-screen text yield at least one frame first.
- **R-3 (W-3):** In the first SANDBOX-004 CI run, check the log for `InvalidOperationException` from
  `UnityEngine.Input`. If it appears, open an architect task that commits ProjectSettings with
  `activeInputHandler` set to 0 (old) or 2 (both). Do not treat it as a code failure.

## Tests added

`Games/Sandbox/Unity/Assets/Game/Tests/EditMode/Core/ScoringContractTests.cs` (+ `.meta`). These are engine-free and
run under both dotnet and Unity EditMode. There are 15 tests (15 of the 16 run results; the other one is
`GameInfoTests`). I put them in `Tests/EditMode/Core/`, not in `Core/Scoring/`, so they do not collide with
SANDBOX-003's file_scope.
- C-001/C-002: reflection pins the exact public surface and namespace of `ScoreCounter` (sealed) and
  `ScoreRules` (static). The constant values are 1, 1.0 and 4. Private members are not inspected, so
  SANDBOX-003 can add private fields freely.
- C-003 `ScoreSnapshot`:
  - it is a readonly struct and implements `IEquatable`;
  - `Initial == (0,0,1)`, and `default != Initial`;
  - the constructor puts its arguments in the right fields;
  - `Equals(T)`, `Equals(object)`, `==` and `!=` each compare all three fields;
  - `Equals(object)` returns false for null and for other types;
  - equal values give equal hashes, and permuted fields give different ones;
  - the hash does not throw at int extremes;
  - `ToString` gives "Score=6 Combo=5 x2".

None of these tests calls `Press`, `GetState`, `Reset` or `MultiplierForCombo`. Those bodies are SANDBOX-003's,
so the tests stay valid after the implementation lands.

## Untested areas / risks

- **Unity compilation of the skeleton** was not run here because there is no Unity and the task has no
  `unity-compile` item. The reviewer's scratch netstandard2.1 build and the dotnet run are the only compile
  evidence. The first Unity run will be in SANDBOX-004's CI.
- **W-3 `activeInputHandler`** cannot be checked without a Unity-generated ProjectSettings. See R-3.
- **C-004 runtime behaviour** has not been exercised: frame order, a single `t` per frame, self-creation and
  the duplicate guard. There is no Unity code yet; SANDBOX-004 implements it and its `unity-playmode` CI checks it.
- **ScoreCounter/ScoreRules behaviour** is not tested in the repo, by design: the bodies are stubs.
  SANDBOX-003 must cover AC-01 to AC-22, including NaN/+Inf/−Inf queries on a new counter (N-3). The contract
  oracle shows this can be done exactly from the contract text.
