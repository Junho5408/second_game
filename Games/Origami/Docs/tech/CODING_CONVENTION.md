# Origami 코딩 규칙

> 담당: architect · 갱신 ORIGAMI-002

## Core (Assets/Game/Core)
- `using UnityEngine` 금지 (asmdef noEngineReferences).
- C# 9, netstandard2.1 범위의 API만 사용 (`Core.Build/`가 이 기준으로 컴파일해서 검사함). 금지 예: `record`, `init` 접근자, `Random.Shared`, `System.Text.Json`. (`System.HashCode`는 netstandard2.1에 있으므로 사용 가능.)
- `Nullable` 비활성. null 의미는 XML 문서 주석에 적는다(예: "없으면 null").
- 네임스페이스 = 폴더: `Core/Content` → `Origami.Core.Content`, `Core/Rules` → `Origami.Core.Rules`, `Core/Run` → `Origami.Core.Run`, `Core/Flow` → `Origami.Core.Flow`. 루트는 `Origami.Core`(GameInfo).
- 의존 방향 `Flow → Run → Rules → Content`만 허용(역방향 `using` 금지). ARCHITECTURE.md §1.
- 파일 하나에 public 타입 하나, 파일명 = 타입명.
- 결정성(R-001): `DateTime.Now`, `Stopwatch`, `Random`, 정적 가변 필드 금지. 정적 데이터는 `static readonly` + 읽기 전용 컬렉션(`ReadOnlyCollection<T>`).
- 규칙 수치는 `RuleConstants`, 작품 값은 `OrigamiCatalog`에서만 읽는다(리터럴 하드코딩 금지).
- 플레이어 명령은 규칙 위반에 예외를 던지지 않고 `CommandResult.Rejected(reason)`를 반환하며 상태를 바꾸지 않는다(R-015). 상태 변경은 모든 검사를 통과한 **뒤에만** 한다.
- 순수 함수의 잘못된 인자는 `ArgumentNullException`/`ArgumentOutOfRangeException`/`ArgumentException`(INTERFACE_CONTRACTS.md에 명시된 대로).
- 공개 데이터 타입은 불변(읽기 전용 프로퍼티, 생성자에서 목록 복사). 스냅샷은 매번 새 객체.
- 문자열 비교는 `StringComparison.Ordinal`. 숫자 서식은 `CultureInfo.InvariantCulture`.
- public 표면 변경 = 계약 변경 요청(Orchestrator → architect). `Tests/EditMode/Core/Contracts/`가 표면을 고정한다.

## Core 테스트 (Assets/Game/Tests/EditMode/Core)
- NUnit, UnityEngine 사용 금지 (dotnet과 Unity EditMode 양쪽에서 실행됨). 네임스페이스 `Origami.Tests.<폴더>`.
- 폴더: `Contracts/`(architect), `Rules/`·`Run/`(Core 규칙 작업), `Flow/`(Core 화면 흐름 작업).
- 테스트 이름에 명세 ID를 넣는다: 예 `F001_AC06_BankruptAfterFourWastedDays`, `R004_StartRejectOrder`. 시나리오 헬퍼(`Make(run, id)`, `Waste(run, id)`)는 각 테스트 폴더 안 internal static 클래스로 둔다.
- 검사는 스냅샷 필드 값으로 한다. "거부 시 상태 불변"은 명령 전후 스냅샷 필드를 비교해 검증한다.

## Runtime (Assets/Game/Scripts)
- 네임스페이스: `Origami.Runtime.Gameplay`, `Origami.Runtime.UI` (폴더 = 네임스페이스).
- 게임 규칙·화면 전이·문구 조립은 Core에 있다. Runtime은 입력 폴링(`OrigamiController.ReadKeys`)과 그리기(`OrigamiView`)만.
- 입력: 레거시 Input Manager(`Input.GetKeyDown`). `com.unity.inputsystem` 쓰지 않음. 입력 판정(`ReadKeys`)과 처리(`ProcessFrame(FlowKeys)`)를 분리해 테스트에서 처리만 직접 호출한다.
- 씬/프리팹 YAML 금지. 오브젝트는 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 훅 + `EnsureInstance()`로 코드 생성. `Bootstrap.cs`는 수정하지 않는다.
- Unity 객체 null 검사는 `!= null`(가짜 null 주의), public 프로퍼티는 진짜 null로 정규화.

## UI
- 기본값은 UI Toolkit이지만 **Origami는 IMGUI(`OnGUI`)를 쓴다** (ARCHITECTURE.md 결정 A-2, DECISIONS.md에 기록 요청).
- 상태·문자열 갱신은 `LateUpdate`(`Refresh`)에서, `OnGUI`는 그리기와 버튼 → `Flow` 메서드 호출만. 버튼 핸들러에서 Core를 직접 부르지 않는다.
- `GUIStyle`/`Font`는 `OnGUI` 안에서 지연 생성(`GUI.skin`은 OnGUI 안에서만 유효). 한국어 글꼴: 결정 A-5.
- 그래픽은 코드 생성 `Texture2D`(`ModelIcons`)와 `GUI.DrawTexture`/색 사각형만. 외부 이미지·폰트 에셋 없음(F-007 AC-07).
- 화면 좌표·크기·색은 private 상수이며 계약이 아니다. 기준 해상도 1280×720, 작으면 `GUI.matrix` 스케일 허용.

## PlayMode 테스트 (Assets/Game/Tests/PlayMode)
- Origami 규칙·화면 흐름은 시간 무관하므로 프레임 대기 없이 `[Test]`로 `ProcessFrame`/`Flow`/`Refresh()`/`Lines`를 동기 검증한다.
- `[UnityTest]`는 런타임 인스턴스 생성 확인 등 꼭 필요할 때만, 프레임 수·시간에 의존하는 단언 금지(배치 모드에서 실제 키 입력 없음을 전제).
- 테스트에서 만든 GameObject는 TearDown에서 파괴.

## 공통
- 새 Unity 파일·폴더를 만들면 `python3 studio/scripts/gen_meta.py <경로>` 실행, `check_meta.py`로 확인.
- 주석·XML 문서는 영어 또는 한국어(파일 안에서 통일). 명세 ID(R-xxx, F-xxx AC-yy, C-0xx)를 근거로 적는다.
