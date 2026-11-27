# Sandbox 인터페이스 계약

> 담당: architect. 구현자는 변경이 필요하면 Orchestrator에 변경 요청.

## 계약 목록

각 계약: 이름, 위치(파일), 시그니처, 동작, 소유자

| ID | 이름 | 위치 | 계약 소유자 | 구현 |
|---|---|---|---|---|
| C-001 | `ScoreCounter` | `Unity/Assets/Game/Core/Scoring/ScoreCounter.cs` | architect (SANDBOX-002) | developer (SANDBOX-003) |
| C-002 | `ScoreRules` | `Unity/Assets/Game/Core/Scoring/ScoreRules.cs` | architect (SANDBOX-002) | developer (SANDBOX-003) |
| C-003 | `ScoreSnapshot` | `Unity/Assets/Game/Core/Scoring/ScoreSnapshot.cs` | architect (SANDBOX-002) | 완료 (값 타입, SANDBOX-002) |
| C-004 | Unity 쪽 호출 규약 | `Unity/Assets/Game/Scripts/Gameplay/ScoreCounterController.cs`, `Unity/Assets/Game/Scripts/UI/ScoreView.cs` | architect (SANDBOX-002, 갱신 SANDBOX-005) | 완료 (developer, SANDBOX-004) |

공통(C-001~C-003): 네임스페이스 `Sandbox.Core.Scoring`, C# 9 / netstandard2.1, `UnityEngine` 비참조, 시각 단위는 초(`double`).
아래 public 시그니처는 고정이다. 구현자는 private 필드·헬퍼를 자유롭게 추가할 수 있지만 public 표면을 바꾸거나 늘리려면 계약 변경 요청이 필요하다.

---

### C-001 `ScoreCounter`

```csharp
namespace Sandbox.Core.Scoring
{
    public sealed class ScoreCounter
    {
        public ScoreCounter();
        public bool Press(double time);
        public ScoreSnapshot GetState(double now);
        public void Reset();
    }
}
```

내부 상태(개념): `score : int`, `combo : int`(마지막 유효 입력 직후 콤보), `lastPress : double?`(직전 유효 입력 시각, 없으면 null).

| 멤버 | 동작 | 규칙 / AC |
|---|---|---|
| `ScoreCounter()` | score 0, combo 0, lastPress 없음. | AC-01 |
| `bool Press(double time)` | **유효성**: `time`이 유한(`!double.IsNaN && !double.IsInfinity`)이고, lastPress가 없거나 `time >= lastPress`. 무효면 아무것도 바꾸지 않고 `false`. **유효**면: lastPress 없음 → combo = 1; `time - lastPress <= ComboWindowSeconds` → combo + 1; 초과 → combo = 1. 그다음 `score += BasePoints * MultiplierForCombo(combo)`, lastPress = time, `true`. 음수 유한 시각도 유효. 예외 없음. | R-001, R-002, R-003, AC-02~10, 12~14, 21, 22 |
| `ScoreSnapshot GetState(double now)` | 상태를 바꾸지 않는 순수 조회. lastPress 없음 → `ScoreSnapshot.Initial` (now 무관, NaN 포함). `now`가 유한이고 `now - lastPress > ComboWindowSeconds` → `(score, 0, 1)` (만료). 그 외(`now <= lastPress + 1.0`, `now < lastPress`, NaN, +∞, −∞) → `(score, combo, MultiplierForCombo(combo))` (마지막 유효 입력 직후 상태). 예외 없음. | R-001, R-004, AC-11, 19, 20 |
| `void Reset()` | 시각 인자 없음. score 0, combo 0, lastPress 없음. 초기 상태에서 호출해도 무변화·무예외(멱등). 이후 첫 유효 입력은 리셋 전 시각보다 이르더라도 "첫 입력"(combo 1, +1). | R-005, AC-15~17 |

세부 규약:
- **경계**: 비교는 `double` 그대로 한다(엡실론 없음). 간격 정확히 `1.0` → 유지(AC-04), `1.001` → 끊김(AC-05). 만료도 `> 1.0`일 때만(AC-11: now=1.0은 콤보 1).
- **같은 시각**: `time == lastPress`는 유효, 간격 0 → 콤보 +1(AC-13).
- **+∞ 조회**: 만료로 보지 않는다(R-001). `now - lastPress > 1.0` 검사를 유한 `now`에만 적용해 구현한다.
- **반환값 `bool`**: 테스트·디버깅용 정보다. Runtime은 반환값에 따라 로직을 분기할 필요가 없다(무시해도 됨).
- **결정성**: 같은 호출 열 → 같은 결과(AC-18). 시계·난수·정적 가변 상태 사용 금지.
- **정수 범위**: score는 `int`. R-006에 따라 오버플로는 고려하지 않는다(기본 unchecked 산술, 별도 처리 없음).
- **스레드**: 스레드 안전하지 않다. 단일 소유자가 메인 스레드에서만 호출.

### C-002 `ScoreRules`

```csharp
namespace Sandbox.Core.Scoring
{
    public static class ScoreRules
    {
        public const int BasePoints = 1;
        public const double ComboWindowSeconds = 1.0;
        public const int MaxMultiplier = 4;
        public static int MultiplierForCombo(int combo);
    }
}
```

- 상수 값은 GAME_RULES.md "기본 수치"와 일치해야 한다. 값 변경은 game-designer의 규칙 변경을 거친다.
- `MultiplierForCombo(combo)`: `combo <= 4` → 1 (0·음수 포함), `5..9` → 2, `10..19` → 3, `>= 20` → 4(`MaxMultiplier`). 순수 함수, 예외 없음.
- `ScoreCounter`는 이 함수와 상수만 사용해 점수·배수를 계산한다(규칙 수치를 다른 곳에 하드코딩 금지).

### C-003 `ScoreSnapshot`

```csharp
namespace Sandbox.Core.Scoring
{
    public readonly struct ScoreSnapshot : System.IEquatable<ScoreSnapshot>
    {
        public static readonly ScoreSnapshot Initial;            // (0, 0, 1)
        public ScoreSnapshot(int score, int combo, int multiplier);
        public int Score { get; }
        public int Combo { get; }
        public int Multiplier { get; }
        public bool Equals(ScoreSnapshot other);                 // 세 필드 모두 같으면 true
        public override bool Equals(object obj);
        public override int GetHashCode();
        public static bool operator ==(ScoreSnapshot left, ScoreSnapshot right);
        public static bool operator !=(ScoreSnapshot left, ScoreSnapshot right);
        public override string ToString();                       // "Score=6 Combo=5 x2" (디버그용, 표시 형식 아님)
    }
}
```

- 불변 값 타입. `GetState`가 반환하는 값의 불변식: `Score >= 0`, `Combo >= 0`, `1 <= Multiplier <= 4`, `Combo == 0`이면 `Multiplier == 1`, 그 외 `Multiplier == MultiplierForCombo(Combo)`.
- 값 비교를 지원하므로 테스트는 `Assert.AreEqual(new ScoreSnapshot(6, 5, 2), counter.GetState(2.0))` 형태로 쓸 수 있다.
- `default(ScoreSnapshot)`은 `(0, 0, 0)`이라 불변식을 만족하지 않는다. 초기값이 필요하면 `ScoreSnapshot.Initial`을 쓴다.

### C-004 Unity 쪽 호출 규약 (SANDBOX-004)

> SANDBOX-005에서 SANDBOX-004가 실제로 출하한 public 표면에 맞춰 갱신했다. 아래 시그니처가 이제 고정 계약이다
> (C-001~C-003과 같은 규칙: public 표면 변경·추가는 계약 변경 요청).

| 타입 | 파일 / 네임스페이스 | 역할 |
|---|---|---|
| `ScoreCounterController : MonoBehaviour` | `Scripts/Gameplay/ScoreCounterController.cs`, `Sandbox.Runtime.Gameplay` | `ScoreCounter` 단독 소유, 입력 폴링, 프레임 스냅샷 제공, 런타임 인스턴스 생성 |
| `ScoreView : MonoBehaviour` | `Scripts/UI/ScoreView.cs`, `Sandbox.Runtime.UI` | 컨트롤러의 `Current`를 텍스트로 만들어 IMGUI로 표시 |

의존 방향: `ScoreView → ScoreCounterController → Core.Scoring`. Gameplay는 UI를 참조하지 않는다.

#### `ScoreCounterController` public 표면

```csharp
namespace Sandbox.Runtime.Gameplay
{
    public sealed class ScoreCounterController : MonoBehaviour
    {
        public const string ObjectName = "[ScoreCounter]";
        public static ScoreCounterController Instance { get; }
        public ScoreSnapshot Current { get; }                 // private set
        public static ScoreCounterController EnsureInstance();
        public void SubmitPress(double time);
        public void SubmitReset();
        public void ProcessFrame(double time, bool resetDown, bool spaceDown, bool clickDown);
    }
}
```

| 멤버 | 동작 |
|---|---|
| `ObjectName` | 런타임 컨트롤러가 붙는 GameObject 이름 `"[ScoreCounter]"`. |
| `Instance` | `EnsureInstance`가 만든(또는 찾은) 런타임 컨트롤러. 없거나 파괴됐으면 `null`(Unity의 "가짜 null"이 아닌 진짜 `null`로 정규화). 테스트 등에서 수동으로 `AddComponent`한 컨트롤러는 `Instance`를 대체하지 않는다. 파괴되면 `OnDestroy`에서 해제. |
| `EnsureInstance()` | 멱등. `Instance`가 있으면 그대로 반환. 없으면 `GameObject.Find(ObjectName)`로 찾고(컴포넌트가 없으면 추가), 그것도 없으면 `ObjectName` GameObject를 새로 만들어 `DontDestroyOnLoad` 후 컴포넌트를 추가. 중복 생성하지 않는다. |
| `Current` | 마지막 `GetState` 결과. 초기값 `ScoreSnapshot.Initial`. |
| `SubmitPress(time)` | `counter.Press(time)` 후 `Current = counter.GetState(time)`. 무효 시각(NaN, ±∞, 역행)은 Core가 무시한다(C-001). |
| `SubmitReset()` | `counter.Reset()` 후 `Current = counter.GetState(Time.unscaledTimeAsDouble)` → 항상 `Initial`. |
| `ProcessFrame(time, resetDown, spaceDown, clickDown)` | 이미 해석된 한 프레임 입력을 계약 순서대로 처리: `resetDown`이면 `Reset()` → `spaceDown`이면 `Press(time)` → `clickDown`이면 `Press(time)` → 항상 `Current = GetState(time)`. `Update`가 실제 입력으로 호출하고, 테스트는 직접 호출한다. |

**입력 매핑** (`Update`, 같은 프레임에 여러 개가 와도 각각 처리):

| 동작 | 입력 | `ProcessFrame` 인자 |
|---|---|---|
| 리셋 | `Input.GetKeyDown(KeyCode.R)` | `resetDown` |
| 입력(press) | `Input.GetKeyDown(KeyCode.Space)` | `spaceDown` |
| 입력(press) | `Input.GetMouseButtonDown(0)` | `clickDown` (Space와 같은 프레임이면 2회 → R-002) |

- 입력 백엔드: 레거시 Input Manager. `com.unity.inputsystem` 패키지는 없고 추가하지 않는다. Unity CI PlayMode(Editor, batchmode)에서
  `Input` 호출이 예외 없이 동작함이 확인됐다(Reports/build/SANDBOX-004.md, 리뷰 W-3 해소). 단독 실행 플레이어의 실제 입력·표시는 CI가 검증할 수 없으며, 사용자 플레이테스트(Windows 빌드)로 수동 확인됐다(DECISIONS.md).
- 키 반복(누르고 있기)은 입력이 아니다: `GetKeyDown`/`GetMouseButtonDown`(눌린 프레임만)으로 판정한다.

**시각 원천**: `double t = Time.unscaledTimeAsDouble;`
- `Update`에서 프레임당 **한 번** 읽고 그 프레임의 모든 `Press`와 `GetState`에 같은 `t`를 넘긴다(같은 프레임 Space+클릭 → 간격 0, AC-13과 일치).
- unscaled를 쓰는 이유: `Time.timeScale` 변경(일시정지 등)에 콤보 판정이 흔들리지 않게. 단조 증가하므로 시각 역행(R-003 예외)은 실제 플레이에서 발생하지 않는다.
- `Time.time`(float) 사용 금지: 장시간 실행 시 정밀도 손실.

**프레임 처리 순서**: `Update()` → `ProcessFrame(t, R, Space, click)` (위 표 순서).
리셋을 입력보다 먼저 처리해 같은 프레임의 입력이 버려지지 않게 한다(그 프레임 결과: 점수 1, 콤보 1).
입력이 없는 프레임에도 `GetState`를 수행해야 콤보 만료(R-004)가 화면에 반영된다.

**테스트 가능성 (PlayMode)**: 테스트는 `SubmitPress`/`SubmitReset`/`ProcessFrame`을 직접 호출해 `Current`와 `ScoreView.Text`를 검증한다.
테스트용 컨트롤러는 `new GameObject().AddComponent<ScoreCounterController>()`로 만들 수 있고 런타임 `Instance`와 독립이다.

#### `ScoreView` public 표면

```csharp
namespace Sandbox.Runtime.UI
{
    public sealed class ScoreView : MonoBehaviour
    {
        public static string Format(ScoreSnapshot snapshot);
        public string Text { get; }                           // private set
        public ScoreCounterController Source { get; }
        public void Bind(ScoreCounterController controller);
    }
}
```

| 멤버 | 동작 |
|---|---|
| `Format(snapshot)` | 순수 함수, `CultureInfo.InvariantCulture`. 표시 형식(아래)을 반환. |
| `Text` | 현재 화면에 그리는 문자열. 초기값 `Format(ScoreSnapshot.Initial)`. |
| `Source` | 읽는 컨트롤러. 바인딩 전이거나 파괴됐으면 `null`(진짜 `null`로 정규화). |
| `Bind(controller)` | `Source`를 설정하고 즉시 `Text = Format(controller.Current)`로 갱신. |

- 갱신 시점: `LateUpdate`에서 `Text = Format(Source.Current)` (컨트롤러 `Update` 이후). `Source`가 `null`이면 마지막 `Text`를 유지한다.
- `Awake`: 아직 바인딩되지 않았으면 같은 GameObject의 `ScoreCounterController`를 `Source`로 잡는다.
- 그리기: IMGUI `OnGUI` + `GUI.Label`, 위치 `Rect(16, 16, 480, 140)`, 글자 크기 28, 흰색. 이 값들은 private이며 계약이 아니다.

**표시 형식** (계약): 한 줄에 한 값, 줄바꿈 `\n`.

```
Score: {Score}
Combo: {Combo}
x{Multiplier}
```

예: `Format(new ScoreSnapshot(6, 5, 2))` → `"Score: 6\nCombo: 5\nx2"`, 초기 `"Score: 0\nCombo: 0\nx1"`.
형식 문자열 변경은 계약 변경이다(PlayMode 테스트가 이 문자열을 검증).

**UI 기술 선택 — IMGUI**: Sandbox 화면 표시는 UI Toolkit이 아니라 IMGUI(`OnGUI`)로 한다.
코드만으로 UI Toolkit 패널을 만들려면 PanelSettings/테마 에셋이 필요한데 이 프로젝트는 에디터를 쓰지 않으므로,
에셋이 필요 없는 IMGUI가 가장 안전하다. 근거: `Docs/DECISIONS.md` (2026-11-27, SANDBOX-004). 규칙상 예외는 CODING_CONVENTION.md "UI" 참고.

**생성**: `Bootstrap.cs`에 의존하지 않는다. 두 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 훅이 있다.
- `ScoreCounterController.Init` → `EnsureInstance()`.
- `ScoreView.Init` → `EnsureInstance()`로 컨트롤러를 얻고, 같은 GameObject에 `ScoreView`가 없으면 추가한 뒤 `Bind(controller)`.

두 훅 모두 `EnsureInstance`를 거치므로 훅 실행 순서와 무관하게 `"[ScoreCounter]"` GameObject 하나에 컨트롤러와 뷰가 하나씩 붙는다.
`Bootstrap.RootName` 오브젝트와의 생성 순서에도 의존하지 않는다.

## 변경 이력

| 날짜 | 계약 | 변경 | 작업 |
|---|---|---|---|
| 2026-11-27 | C-001~C-004 | 최초 정의 | SANDBOX-002 |
| 2026-11-27 | C-004 | SANDBOX-004 출하 public 표면(`ObjectName`, `Instance`, `EnsureInstance`, `Current`, `SubmitPress`, `SubmitReset`, `ProcessFrame`; `ScoreView.Format/Bind/Text/Source`), 표시 형식, IMGUI 선택, 생성 훅, 레거시 Input CI 확인 기록. 동작 변경 없음(문서를 코드에 맞춤) | SANDBOX-005 |
