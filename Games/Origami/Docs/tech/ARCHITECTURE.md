# Origami 시스템 구조

> 담당: architect · 기준 태스크: ORIGAMI-002 · 기준 명세: `Docs/design/` (F-001~F-007, R-001~R-015, CONTENT_SPECS)
> 시그니처·동작의 정본은 `INTERFACE_CONTRACTS.md`, 데이터 정의는 `DATA_STRUCTURE.md`, 규칙은 `CODING_CONVENTION.md`.

## 1. 모듈과 책임

| 모듈 | 위치 (`Unity/Assets/Game/…`) | 어셈블리 / 네임스페이스 | 책임 | 의존 |
|---|---|---|---|---|
| Core.Content | `Core/Content/` | `Origami.Core` / `Origami.Core.Content` | 작품 5종·일차별 인기 작품·엔딩 5종 정의(코드 카탈로그), 동작·등급 열거형 | 없음 (System만) |
| Core.Rules | `Core/Rules/` | `Origami.Core` / `Origami.Core.Rules` | 규칙 상수, 순수 규칙 함수(등급·가격·해금·엔딩 판정), 완성 수 표 | Content |
| Core.Run | `Core/Run/` | `Origami.Core` / `Origami.Core.Run` | 런 상태와 명령(NewRun/Start/Fold/Hint/Abandon/EndDay), 결과·거부 사유, 스냅샷, 정산 결과 | Rules, Content |
| Core.Flow | `Core/Flow/` | `Origami.Core` / `Origami.Core.Flow` | F-007 화면 상태 기계(엔진 없음), 키→명령 매핑, 모든 표시 문자열(한국어) | Run, Rules, Content |
| Runtime.Gameplay | `Scripts/Gameplay/` | `Origami.Runtime` / `Origami.Runtime.Gameplay` | `OrigamiController`: `OrigamiFlow` 단독 소유, 레거시 Input 폴링 → `FlowKeys` | Core.Flow |
| Runtime.UI | `Scripts/UI/` | `Origami.Runtime` / `Origami.Runtime.UI` | `OrigamiView`: IMGUI로 `FlowTexts.ScreenLines` + 버튼 그리기, `ModelIcons`: 코드 생성 도형 텍스처 | Runtime.Gameplay, Core |
| Bootstrap | `Scripts/Bootstrap.cs` | `Origami.Runtime` | 카메라·루트 오브젝트 생성 (기존, 변경 없음) | — |

의존 방향은 한쪽뿐이다:
`Runtime.UI → Runtime.Gameplay → Core.Flow → Core.Run → Core.Rules → Core.Content`.
Core는 Runtime을 모른다. Core 하위 네임스페이스끼리도 위 순서의 역방향 참조 금지(순환 없음). `RunEndReason`이 `Rules`에 있는 이유도 이것이다(엔딩 판정 입력이므로 Rules가 Run을 참조하지 않게).
Core 하위 폴더는 별도 asmdef 없이 `Origami.Core` 어셈블리 하나에 들어간다(작은 게임이라 분리 이득 없음).

## 2. 구성도

```
 Unity 프레임                                Core (엔진 없음, 결정적, dotnet 테스트)
 ┌──────────────────────────┐ FlowKeys    ┌────────────────────────┐ 명령      ┌──────────────────┐
 │ OrigamiController         │ ──────────▶ │ OrigamiFlow (Core.Flow) │ ────────▶ │ OrigamiRun        │
 │ (Scripts/Gameplay)        │ ProcessKeys │  화면 상태 기계          │ Command-  │ (Core.Run)        │
 │  Update: ReadKeys()       │             │  Screen / Snapshot /    │ Result    │  상태 + 명령       │
 │  → ProcessFrame(keys)     │             │  Message / Settlement   │ ◀──────── │  GetSnapshot()    │
 └───────────┬──────────────┘             └──────────┬─────────────┘           └───┬──────────────┘
             │ Flow                         버튼 → 같은 │ 메서드                    │ 순수 함수
             ▼                                          │                           ▼
 ┌──────────────────────────┐ ScreenLines  ┌──────────┴─────────────┐   ┌──────────────────────────────┐
 │ OrigamiView (Scripts/UI)  │ ◀─────────── │ FlowTexts (Core.Flow)   │   │ Grading·Pricing·Unlocking·    │
 │  IMGUI OnGUI: 텍스트+버튼 │              │  한국어 문자열 순수 함수 │   │ EndingRules·RuleConstants     │
 │ ModelIcons: 도형 텍스처   │              └────────────────────────┘   │ (Core.Rules) + OrigamiCatalog │
 └──────────────────────────┘                                            └──────────────────────────────┘
```

## 3. 소유와 생명주기

- `OrigamiRun` 인스턴스는 런타임에 하나뿐이며 `OrigamiFlow`가 생성자에서 `new`로 만들어 단독 소유한다. 외부에 노출하지 않는다(화면 상태와 런 상태가 어긋나지 않게). 런 재시작은 객체 재생성이 아니라 `NewRun()`(Flow의 `Restart()`).
- `OrigamiFlow`는 `OrigamiController`가 `Awake`에서 만들어 앱 종료까지 소유한다. 다른 코드는 `controller.Flow`를 통해서만 접근한다.
- 런타임 오브젝트: `"[Origami]"` GameObject(DontDestroyOnLoad) 하나에 `OrigamiController`와 `OrigamiView`. 두 클래스의 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 훅이 각자 `OrigamiController.EnsureInstance()`를 불러 순서와 무관하게 하나씩만 생긴다(Sandbox와 같은 방식). `Bootstrap.cs`는 수정하지 않는다.
- 씬 전환 없음(단일 빈 씬). 저장/불러오기 없음(F-001 범위 밖).

## 4. 이벤트 / 통신 방식

- **이벤트 없음, 명령-결과 + 풀링.** Core 타입은 C# event/콜백을 노출하지 않는다.
  - Runtime → Core: Flow 메서드 호출(버튼) 또는 `ProcessKeys`(키). 둘 다 같은 Flow 메서드로 수렴하므로 "키와 버튼은 같은 명령"(F-007 AC-05)이 구조적으로 보장된다.
  - Core → Runtime: 명령은 `CommandResult`(수락/거부 사유/결과 종류/완성 작품/정산)를 반환하고, Flow는 매 명령 뒤 `RunSnapshot`을 새로 받아 보관한다. View는 `LateUpdate`에서 `FlowTexts.ScreenLines(flow)`로 문자열을 다시 만든다.
- 시간·프레임에 의존하는 규칙이 없다(R-001, 타이머 없음). 따라서 Core에는 시각 인자도 없고, Runtime도 `Time`을 Core에 넘기지 않는다.

## 5. Unity 쪽 화면 구조 (F-007)

| FlowScreen | F-007 화면 | 내용 (텍스트는 `FlowTexts.ScreenLines`, 형식은 C-021) | 버튼 |
|---|---|---|---|
| `Workbench` | 1 작업대(작품 선택) | 일차 n/7, 돈, 시도 k/5, 오늘의 인기 작품, 5종 목록(단계 수·종이값·오늘 ★3 예상가 / 잠김+해금 조건 진행), 오늘 완성 목록, 안내 문구 | 작품 5개(각 `SelectModel`), "하루 마치기" |
| `Preview` | 2 도안 미리보기 | 작품 이름, 정답 순서 전체(기호), 종이값, 안내 문구 | "접기 시작 (n원)", "닫기" |
| `Folding` | 3 접기 화면 | 종이(단색 정사각형+진행 표시), 진행 i/N, 맞힌 단계 기호, 실수 m/3, 힌트 수, 예상 등급, 힌트 공개 시 현재 단계 기호 — **남은 정답은 표시하지 않음** | 동작 5개, "힌트", "포기" |
| `AbandonConfirm` | 3의 포기 확인 창 | 접기 화면 + 확인 문구 | "포기한다", "계속 접기" |
| `Notice` | 4 완성/망침 알림 | 작품 도형·등급(★) 또는 "종이를 망쳤다"/"접기를 포기했다" | "확인" |
| `Settlement` | 5 정산 | 작품별 가격, 합계, 정산 후 돈, 다음 날 인기 작품(완주·파산 시 제외), 파산 시 파산 안내 | "확인" |
| `Result` | 6 결과 | 엔딩 이름·문구, 최종 돈, 5종 ★3/★2/★1 개수, 실패 수, 총 판매액, 총 종이값 | "다시 시작" |

화면 전이·키 매핑의 정본은 INTERFACE_CONTRACTS.md C-020.

### 입력 매핑 (요약)

| 키 (레거시 Input, `GetKeyDown`) | `FlowKeys` | Folding | AbandonConfirm | Preview | Notice / Settlement | 그 외 |
|---|---|---|---|---|---|---|
| ↑ / W | `FoldUp` | `Fold(U)` | 무시 | 무시 | 무시 | 무시 |
| ↓ / S | `FoldDown` | `Fold(D)` | 무시 | 무시 | 무시 | 무시 |
| ← / A | `FoldLeft` | `Fold(L)` | 무시 | 무시 | 무시 | 무시 |
| → / D | `FoldRight` | `Fold(R)` | 무시 | 무시 | 무시 | 무시 |
| Space | `Flip` | `Fold(F)` | 무시 | 무시 | 무시 | 무시 |
| H | `Hint` | `Hint()` | 무시 | 무시 | 무시 | 무시 |
| Esc | `Escape` | `RequestAbandon()` | `CancelAbandon()` | `ClosePreview()` | 무시 | 무시 |
| Enter / 키패드 Enter | `Enter` | 무시 | `ConfirmAbandon()` | `StartSelected()` | `Acknowledge()` | 무시 |

한 프레임에 여러 키가 눌리면 우선순위 `Escape > Enter > Hint > FoldUp > FoldDown > FoldLeft > FoldRight > Flip` 중 **가장 높은 키 하나만** 본다(그 키가 현재 화면에서 무시되면 그 프레임은 아무 일도 없음). 같은 프레임 Esc+Enter로 확인 없이 포기되는 일을 막는다.

## 6. 외부 의존 / 엔진 기능

| 의존 | 용도 | 상태 / 근거 |
|---|---|---|
| 레거시 Input Manager (`UnityEngine.Input.GetKeyDown`) | 키 입력 (`OrigamiController.ReadKeys`) | Sandbox에서 Unity CI PlayMode(batchmode) 예외 없음 확인. `com.unity.inputsystem` 없음, 추가하지 않음. `ProjectSettings` 변경 없음. |
| IMGUI (`OnGUI`, `GUI.Label`, `GUI.Button`, `GUI.DrawTexture`) | 모든 화면 | UI Toolkit 기본값의 예외(에셋 필요 없음, 아래 결정 A-2). |
| `Font.CreateDynamicFontFromOSFont` | 한국어 글꼴 | 에셋 없이 OS 글꼴 사용(결정 A-5). CI는 글리프 표시를 검증할 수 없음 → 사용자 플레이테스트. |
| `Texture2D`(런타임 생성) | 작품 도형·종이 사각형 (`ModelIcons`) | 외부 이미지 없음(F-007 AC-07). |

패키지 추가 없음.

## 7. 저장 데이터 전략

- 이번 버전은 저장 데이터가 없다(저장/불러오기·최고 기록 범위 밖, GAME_DESIGN). 런은 항상 R-002 상태에서 시작.
- 나중에 저장이 생기면 기본 방침은 JSON. 대상은 `OrigamiRun` 내부 상태(일차·돈·시도·완성 수·실패 수·누적 기록·오늘 작품·진행 중 접기). 스냅샷 타입은 이미 엔진 없는 불변 데이터라 직렬화 DTO의 원형으로 쓸 수 있다. 그때 architect 작업으로 계약을 추가한다.

## 8. 아키텍처 결정 (Orchestrator가 `Docs/DECISIONS.md`에 옮겨 적을 것)

| ID | 결정 | 이유 |
|---|---|---|
| A-1 | 작품·인기표·엔딩은 **Core의 코드 카탈로그**(`Core/Content/OrigamiCatalog.cs`)로 정의하고 JSON(`Data/`)은 쓰지 않는다. | netstandard2.1에는 패키지 없이 쓸 JSON 파서가 없고 `JsonUtility`는 UnityEngine이다. 코드 정의면 dotnet 테스트가 Unity 없이 실제 데이터로 돈다. 데이터는 5행·7칸·5행으로 작다. 값은 카탈로그 계약 테스트가 CONTENT_SPECS와 대조한다. |
| A-2 | UI는 **IMGUI**(`OnGUI`). UI Toolkit 기본값의 예외. | 에디터 없이 UI Toolkit 패널을 쓰려면 PanelSettings/테마 에셋이 필요. Sandbox에서 IMGUI가 CI·Windows 빌드 모두 동작함이 확인됨. |
| A-3 | F-007 화면 상태 기계(`OrigamiFlow`)와 모든 표시 문자열(`FlowTexts`)을 **엔진 없는 Core.Flow**에 둔다. Runtime은 키 폴링과 그리기만. | F-007 수용 기준(Esc 확인 단계, 거부 문구, 정산·결과 표시)을 Unity CI 없이 dotnet으로 검증. PlayMode는 연결(키→Flow, Flow→화면 문자열)만 확인하면 됨. |
| A-4 | 입력은 레거시 Input Manager, 키 하나/프레임(우선순위 표). | Sandbox 검증 결과. 동시 입력으로 확인 단계를 건너뛰지 않게. |
| A-5 | 한국어 표시는 `Font.CreateDynamicFontFromOSFont(new[] {"Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "NanumGothic", "Arial"}, size)`로 만든 글꼴을 GUIStyle에 지정. | 에셋 없이 Windows(맑은 고딕)에서 한글 표시. 글꼴이 없으면 기본 글꼴로 떨어지며 CI로는 검증 불가 → 사용자 플레이테스트로 확인. |
| A-6 | F-007에 명시되지 않은 키 편의 매핑: Preview에서 Enter=접기 시작, Esc=닫기 / Notice·Settlement에서 Enter=확인. 그 외 화면의 키는 무시. | 키보드만으로도 플레이 가능하게 하는 최소 확장. 방향키·Space·H는 F-007대로 접기 화면 밖에서 무효. 사용자가 원치 않으면 C-020 표만 바꾸면 됨. |
| A-7 | 잠긴 작품 선택은 Core 명령 없이 Flow가 `Snapshot.IsUnlocked`로 막고 Locked 문구(해금 조건)를 보여 준다. 도안은 보여 주지 않는다. | F-007 "잠긴 작품은 도안을 보여 주지 않는다". 상태 불변. |

## 9. 구현 작업별 파일 소유 (제안, Orchestrator 확정)

| 작업 | 담당 | file_scope | 소유 내용 |
|---|---|---|---|
| ORIGAMI-002 (이 작업) | architect | `Docs/tech/`, `Core/` 전체, `Tests/EditMode/Core/Contracts/` | 계약·골격·카탈로그 데이터·계약 테스트 |
| Core 규칙 | developer | `Core/Rules/`, `Core/Run/`, `Tests/EditMode/Core/Rules/`, `Tests/EditMode/Core/Run/` | `Grading`·`Pricing`·`Unlocking`·`EndingRules`·`OrigamiRun` 본문 (F-001~F-006) |
| Core 화면 흐름 | developer | `Core/Flow/`, `Tests/EditMode/Core/Flow/` | `OrigamiFlow`·`FlowTexts` 본문 (F-007 로직·문구) |
| Unity 컨트롤러·뷰 | developer | `Scripts/Gameplay/`, `Scripts/UI/`, `Tests/PlayMode/Flow/` | `OrigamiController`, `OrigamiView`, `ModelIcons`, PlayMode 테스트 |

- `Core/Content/`는 ORIGAMI-002 이후 architect 소유(계약). 문구 다듬기가 필요하면 content-designer 작업으로 `Core/Content/OrigamiCatalog.cs`의 문자열(DisplayName/Text)만 배정한다.
- 공통으로 `Tests/EditMode/Core/Contracts/`는 구현 작업이 수정하지 않는다(계약 변경 시 architect).
- `Scripts/Bootstrap.cs`, asmdef, `Packages/`, `ProjectSettings/`는 어느 구현 작업도 수정하지 않는다.
