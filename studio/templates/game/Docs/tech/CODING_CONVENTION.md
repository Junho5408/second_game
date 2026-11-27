# {{GAME}} 코딩 규칙

> 담당: architect

## Core (Assets/Game/Core)
- `using UnityEngine` 금지 (asmdef noEngineReferences).
- C# 9, netstandard2.1 범위의 API만 사용.
- 네임스페이스: `{{GAME}}.Core`.
- Core 테스트는 `Assets/Game/Tests/EditMode/Core/`에 작성 (dotnet과 Unity 양쪽에서 실행됨). UnityEngine 사용 금지.

## Runtime (Assets/Game/Scripts)
- 네임스페이스: `{{GAME}}.Runtime`.
- 게임 규칙은 Core에 두고, 여기는 표시·입력·연출만.
- 씬/프리팹 YAML 대신 코드로 오브젝트 생성. UI는 UI Toolkit.

## 공통
- 새 Unity 파일을 만들면 `python3 studio/scripts/gen_meta.py <경로>` 실행.
