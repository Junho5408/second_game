# Sandbox 시스템 구조

> 담당: architect

## 모듈과 책임

| 모듈 | 위치 | 어셈블리 / 네임스페이스 | 책임 | 의존 |
|---|---|---|---|---|
| Core | Assets/Game/Core | `Sandbox.Core` / `Sandbox.Core` | 게임 규칙·상태 (UnityEngine 비참조) | 없음 |
| Core.Scoring | Assets/Game/Core/Scoring | `Sandbox.Core` / `Sandbox.Core.Scoring` | F-001 점수 카운터 규칙 (R-001~R-006) | 없음 (System만) |
| Runtime | Assets/Game/Scripts | `Sandbox.Runtime` / `Sandbox.Runtime` | 화면·입력·연출, Core 호출 | Core |
| Runtime.Gameplay | Assets/Game/Scripts/Gameplay | `Sandbox.Runtime` / `Sandbox.Runtime.Gameplay` | 입력 폴링 → `ScoreCounter` 호출, 카운터 인스턴스 소유 | Core.Scoring |
| Runtime.UI | Assets/Game/Scripts/UI | `Sandbox.Runtime` / `Sandbox.Runtime.UI` | `ScoreSnapshot` 표시 (IMGUI, DECISIONS.md) | Runtime.Gameplay (`ScoreCounterController.Current`), Core.Scoring (스냅샷 타입만) |

의존 방향은 한쪽뿐이다: `Runtime.UI → Runtime.Gameplay → Core.Scoring`. Core는 Runtime을 모른다. 순환 없음.
Core/Scoring은 별도 asmdef를 두지 않고 `Sandbox.Core` 어셈블리에 포함된다(작은 게임이라 분리 이득 없음).

## 점수 카운터 (F-001) 구성

```
 Unity 프레임 (Update)                       Core (엔진 없음, 결정적)
 ┌───────────────────────────┐   Press(t)    ┌──────────────────────────┐
 │ ScoreCounterController     │ ───────────▶ │ ScoreCounter              │
 │ (Scripts/Gameplay)         │   Reset()     │  score, combo, lastPress  │
 │  t = Time.unscaledTime-    │ ───────────▶ │                            │
 │      AsDouble (프레임당 1회)│ GetState(t)   │ ScoreRules (상수·배수표)  │
 │                            │ ◀─────────── │ ScoreSnapshot (값 타입)   │
 └────────────┬──────────────┘ ScoreSnapshot └──────────────────────────┘
              │ snapshot
              ▼
 ┌───────────────────────────┐
 │ ScoreView (Scripts/UI)     │  점수 / 콤보 / 배수 텍스트 (IMGUI OnGUI)
 └───────────────────────────┘
```

- **소유**: `ScoreCounter` 인스턴스는 런타임에서 하나뿐이며 `ScoreCounterController`(SANDBOX-004)가 `new`로 생성·소유한다. 다른 코드는 카운터를 직접 갖지 않고 컨트롤러가 내놓는 `ScoreSnapshot`만 읽는다.
- **시각 원천**: Core는 시계를 읽지 않는다(R-001). Runtime이 프레임마다 `Time.unscaledTimeAsDouble`을 한 번 읽어 그 프레임의 모든 `Press`/`GetState`에 같은 값을 넘긴다. 자세한 호출 규약은 INTERFACE_CONTRACTS.md C-004.
- **생성 위치**: `Bootstrap.cs`는 SANDBOX-004의 file_scope 밖이므로, 컨트롤러와 뷰가 각자 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 훅에서 `ScoreCounterController.EnsureInstance()`를 불러 `"[ScoreCounter]"` GameObject(DontDestroyOnLoad) 하나에 컨트롤러와 `ScoreView`를 붙인다. 훅 순서와 무관하다(C-004 "생성"). Bootstrap 수정이 필요해지면 Orchestrator가 범위를 배정한다.
- **화면 표시**: `ScoreView`가 `LateUpdate`에서 `Current`를 `ScoreView.Format`으로 문자열화하고 IMGUI(`OnGUI`)로 그린다. UI Toolkit 기본값의 예외이며 근거는 DECISIONS.md, 규칙은 CODING_CONVENTION.md "UI".

## 외부 의존 / 엔진 기능

| 의존 | 용도 | 상태 |
|---|---|---|
| 레거시 Input Manager (`UnityEngine.Input`) | Space / 마우스 왼쪽 / R 키 다운 판정 (`ScoreCounterController.Update`) | Unity CI PlayMode(Editor, batchmode)에서 예외 없이 동작 확인 — Reports/build/SANDBOX-004.md (리뷰 W-3 해소). 단독 실행 플레이어의 실제 키 입력은 CI가 검증할 수 없고, 사용자 플레이테스트(Windows 빌드, run 45928856363)로 수동 확인됨 — DECISIONS.md. `com.unity.inputsystem` 패키지는 없음. `ProjectSettings`의 `activeInputHandler`는 커밋하지 않았고 Unity 기본값으로 동작한다. 바뀌어 예외가 나면 architect 작업으로 Input Manager(또는 Both)를 명시한다. |
| IMGUI (`OnGUI`, `GUI.Label`) | 점수 표시 (`ScoreView`) | 추가 에셋·패키지 불필요. 플레이어 렌더링은 CI 미검증, 사용자 플레이테스트로 점수 표시 정상 확인됨 — DECISIONS.md. |

## 이벤트 / 통신 방식

- 점수 카운터는 이벤트를 쓰지 않는다. Runtime이 매 프레임 `GetState(now)`로 **풀링(pull)** 한다. 콤보 만료(R-004)는 입력 없이 시간만으로 바뀌므로 이벤트보다 매 프레임 조회가 단순하고 정확하다.
- Core 타입은 C# event/콜백을 노출하지 않는다. 필요해지면 계약 변경으로 추가한다.

## 생명주기

- `ScoreCounter`는 컨트롤러 생성 시 만들어지고 앱 종료까지 유지된다. 씬 전환 없음(단일 빈 씬).
- 리셋은 객체 재생성이 아니라 `Reset()` 호출이다.

## 저장 데이터 전략

- F-001은 저장 데이터가 없다(최고 점수 저장은 범위 밖). 저장이 생기면 기본 방침은 JSON(`DATA_STRUCTURE.md`).
