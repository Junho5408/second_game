# Sandbox 인터페이스 계약

> 담당: architect. 구현자는 변경이 필요하면 Orchestrator에 변경 요청.

## 계약 목록

각 계약: 이름, 위치(파일), 시그니처, 동작, 소유자

| ID | 이름 | 위치 | 계약 소유자 | 구현 |
|---|---|---|---|---|
| C-001 | `ScoreCounter` | `Unity/Assets/Game/Core/Scoring/ScoreCounter.cs` | architect (SANDBOX-002) | developer (SANDBOX-003) |
| C-002 | `ScoreRules` | `Unity/Assets/Game/Core/Scoring/ScoreRules.cs` | architect (SANDBOX-002) | developer (SANDBOX-003) |
| C-003 | `ScoreSnapshot` | `Unity/Assets/Game/Core/Scoring/ScoreSnapshot.cs` | architect (SANDBOX-002) | 완료 (값 타입, SANDBOX-002) |
| C-004 | Unity 쪽 호출 규약 | `Unity/Assets/Game/Scripts/{Gameplay,UI}/` | architect (SANDBOX-002) | developer (SANDBOX-004) |

공통: 네임스페이스 `Sandbox.Core.Scoring`, C# 9 / netstandard2.1, `UnityEngine` 비참조, 시각 단위는 초(`double`).
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

권장 구성(이름은 권장, 위치는 task file_scope 안):

| 타입 | 위치 / 네임스페이스 | 역할 |
|---|---|---|
| `ScoreCounterController : MonoBehaviour` | `Scripts/Gameplay/`, `Sandbox.Runtime.Gameplay` | `ScoreCounter` 단독 소유, 입력 폴링, 프레임 스냅샷 제공 |
| `ScoreView` | `Scripts/UI/`, `Sandbox.Runtime.UI` | 스냅샷을 텍스트로 표시 |

**입력 매핑** (같은 프레임에 여러 개가 와도 각각 처리):

| 동작 | 입력 | Core 호출 |
|---|---|---|
| 입력(press) | `Space` 키 다운 | `Press(t)` |
| 입력(press) | 마우스 왼쪽 버튼 다운 | `Press(t)` (Space와 같은 프레임이면 2회 → R-002) |
| 리셋 | `R` 키 다운 | `Reset()` |

- 입력 백엔드: 프로젝트에 `com.unity.inputsystem` 패키지가 없으므로 레거시 Input Manager(`Input.GetKeyDown(KeyCode.Space)`, `Input.GetMouseButtonDown(0)`, `Input.GetKeyDown(KeyCode.R)`)를 쓴다. 패키지 추가는 하지 않는다.
- 키 반복(누르고 있기)은 입력이 아니다: `GetKeyDown`/`GetMouseButtonDown`(눌린 프레임만)으로 판정한다.

**시각 원천**: `double t = Time.unscaledTimeAsDouble;`
- 프레임당 **한 번** 읽고 그 프레임의 모든 `Press`와 `GetState`에 같은 `t`를 넘긴다(같은 프레임 Space+클릭 → 간격 0, AC-13과 일치).
- unscaled를 쓰는 이유: `Time.timeScale` 변경(일시정지 등)에 콤보 판정이 흔들리지 않게. 단조 증가하므로 시각 역행(R-003 예외)은 실제 플레이에서 발생하지 않는다.
- `Time.time`(float) 사용 금지: 장시간 실행 시 정밀도 손실.

**프레임 처리 순서** (`Update()` 한 곳에서):
1. `t` 읽기.
2. 리셋 키 다운이면 `Reset()`.
3. Space 다운이면 `Press(t)`; 마우스 왼쪽 다운이면 `Press(t)`.
4. `Current = counter.GetState(t)` 저장 → UI가 이 값을 표시.

리셋을 입력보다 먼저 처리해 같은 프레임의 입력이 버려지지 않게 한다(그 프레임 결과: 점수 1, 콤보 1).
입력이 없는 프레임에도 4단계를 수행해야 콤보 만료(R-004)가 화면에 반영된다.

**테스트 가능성 (PlayMode)**: 실제 키 입력을 흉내 내기 어려우므로 컨트롤러는 입력 판정과 Core 호출을 분리한
public 메서드를 둔다. 권장 시그니처:

```csharp
public ScoreSnapshot Current { get; }       // 마지막 GetState 결과
public void SubmitPress(double time);       // counter.Press(time) 후 Current 갱신
public void SubmitReset();                  // counter.Reset() 후 Current 갱신
```

`Update()`는 위 메서드를 호출할 뿐이고, PlayMode 테스트는 `SubmitPress`/`SubmitReset`을 직접 호출해 `Current`와 화면 텍스트를 검증한다.

**표시**: 점수, 콤보 수, 배수(`x{Multiplier}`) 세 값을 텍스트로 표시한다. 문구·배치는 SANDBOX-004 결정. UI는 UI Toolkit 또는 코드 생성 UI.

**생성**: `Bootstrap.cs`는 SANDBOX-004 범위 밖이다. 컨트롤러는 `Scripts/Gameplay` 안의 정적 메서드
`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`에서 자기 GameObject(예: `"[ScoreCounter]"`)를 만들고,
중복 생성을 막는다(`GameObject.Find` 또는 정적 인스턴스 확인). `Bootstrap.RootName` 오브젝트와의 생성 순서에 의존하지 않는다.

## 변경 이력

| 날짜 | 계약 | 변경 | 작업 |
|---|---|---|---|
| 2026-11-27 | C-001~C-004 | 최초 정의 | SANDBOX-002 |
