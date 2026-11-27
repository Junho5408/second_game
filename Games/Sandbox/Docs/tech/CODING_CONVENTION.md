# Sandbox 코딩 규칙

> 담당: architect

## Core (Assets/Game/Core)
- `using UnityEngine` 금지 (asmdef noEngineReferences).
- C# 9, netstandard2.1 범위의 API만 사용.
- 네임스페이스: `Sandbox.Core`. 하위 폴더는 하위 네임스페이스를 쓴다(예: `Core/Scoring/` → `Sandbox.Core.Scoring`).
- 시각은 `double` 초 단위 인자로 받는다. Core에서 `DateTime.Now`, `Stopwatch` 등 시계 읽기 금지(R-001).
- 규칙 수치는 한 곳(예: `ScoreRules`)의 상수로 정의하고 다른 곳에 하드코딩하지 않는다.
- Core 테스트는 `Assets/Game/Tests/EditMode/Core/`에 작성 (dotnet과 Unity 양쪽에서 실행됨). UnityEngine 사용 금지.

## Runtime (Assets/Game/Scripts)
- 네임스페이스: `Sandbox.Runtime`. 하위 폴더는 `Sandbox.Runtime.Gameplay`, `Sandbox.Runtime.UI` 등.
- 게임 규칙은 Core에 두고, 여기는 표시·입력·연출만.
- 씬/프리팹 YAML 대신 코드로 오브젝트 생성.
- 입력: 레거시 Input Manager(`UnityEngine.Input`). `com.unity.inputsystem` 패키지는 쓰지 않는다(추가는 architect 작업). 입력 판정과 Core 호출은 분리해 테스트에서 호출할 수 있게 한다(C-004 `ProcessFrame`).
- 시각: `Time.unscaledTimeAsDouble`을 프레임당 한 번 읽어 Core에 넘긴다. `Time.time`(float) 금지.

## UI
- 기본값: UI Toolkit(UXML/USS 텍스트 파일 또는 코드 생성).
- **예외 — Sandbox는 IMGUI(`OnGUI`)를 쓴다.** 에디터 없이 코드만으로 UI Toolkit 패널을 만들려면 PanelSettings/테마 에셋이 필요하기 때문이다.
  근거와 결정자: `Docs/DECISIONS.md` (2026-11-27, "Sandbox 화면 표시는 IMGUI(OnGUI)로 한다", SANDBOX-004). 적용 예: `Scripts/UI/ScoreView.cs` (C-004).
- IMGUI 규칙: 표시 문자열은 순수 정적 함수로 만들고(`CultureInfo.InvariantCulture`) 테스트가 읽을 수 있는 프로퍼티에 보관한다(예: `ScoreView.Format`/`Text`). `GUIStyle`은 `GUI.skin`이 `OnGUI` 안에서만 유효하므로 `OnGUI`에서 지연 생성한다. 상태 갱신은 `Update`/`LateUpdate`에서 하고 `OnGUI`는 그리기만 한다.
- 새 UI를 UI Toolkit으로 바꾸려면 DECISIONS.md에 새 결정을 기록한 뒤 진행한다.

## 공통
- 새 Unity 파일을 만들면 `python3 studio/scripts/gen_meta.py <경로>` 실행.
