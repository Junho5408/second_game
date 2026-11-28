# Origami 코딩 규칙 (v2)

> 담당: architect · 갱신 ORIGAMI-008 (v1: ORIGAMI-002), ORIGAMI-017(아트 에셋 규칙). v1 규칙 중 그대로 유효한 것은 그대로 두고 v2 모듈·수치 규칙을 더했다.

## Core (Assets/Game/Core)

### 언어·API
- `using UnityEngine` 금지(asmdef `noEngineReferences`). C# 9, netstandard2.1 범위의 API만(`Core.Build/`가 이 기준으로 컴파일). 금지 예: `record`, `init`, `Random.Shared`, `System.Text.Json`. `float`·`MathF`는 쓰지 않는다(Core는 `double`만).
- `Nullable` 비활성. null 의미는 XML 문서 주석에 적는다("없으면 null").
- 파일 하나에 public 타입 하나, 파일명 = 타입명. (작은 enum·struct도 각자 파일.)

### 네임스페이스와 의존 (ARCHITECTURE §1·§2)
- 네임스페이스 = 폴더: `Origami.Core.Geometry`, `.Folding`, `.Recognition`, `.Catalog`, `.Economy`, `.Tutorial`, `.Game`, `.Book`, `.Texts`, `.Screens`, `.Room`. 루트 `Origami.Core`(GameInfo).
- 허용된 방향만 `using`한다(ARCHITECTURE §1.1 "의존" 열). 역방향·순환 금지. 리뷰가 `using` 목록으로 검사한다.
- **v2 코드는 v1 네임스페이스(`Origami.Core.Content/Rules/Run/Flow`)를 참조하지 않는다.** v1 코드도 v2를 참조하지 않는다(ORIGAMI-014에서 v1 삭제).

### 결정성 (R-001)
- `DateTime.Now`, `Stopwatch`, `System.Random`, `Environment.TickCount`, 정적 가변 필드 금지.
- 정적 데이터는 `static readonly` + 읽기 전용 컬렉션. 계산이 비싼 불변 데이터(`ModelCatalog.Recognition`, `PlaneGuide.Steps`)는 `static readonly Lazy<T>`(기본 스레드 안전 모드)로 한 번만 만든다 — 결과가 상수에서 결정적으로 나오므로 허용.
- `Dictionary`/`HashSet`의 **열거 순서에 결과를 기대지 않는다.** 출력 순서가 있는 목록은 항상 카탈로그 순서·인덱스 순서로 만든다(예: `OwnedModelIds`). 정렬은 안정 정렬(`List.Sort`는 불안정 — 동점 키가 있으면 인덱스를 보조 키로 넣거나 LINQ `OrderBy` 사용).

### 수치 규칙 (결정적 비교)
- 좌표·넓이·일치도는 `double`. Core에 `float` 금지(Unity로 넘길 때만 변환).
- 비교 허용치는 상수로 이름을 붙여 한 곳에 둔다(`FoldPlanner.SideEps` 1e-9, `MinArea` 1e-7, `ContainsEps` 1e-6, `DropSnap.*`, `Outline.RowStep`). 리터럴 허용치를 코드 곳곳에 쓰지 않는다(참조 구현과 같은 값이 한 곳에서 보이게).
- **동점 처리는 반올림 키로**: 거리·좌표의 동점 비교는 `DropSnap.Key(v)` = `Math.Round(v, 9, MidpointRounding.ToEven)`(Python `round(v, 9)`)로 만든 키를 사전식으로 비교한다(R-021). 키 없는 `<`/`==` 비교로 후보를 고르지 않는다.
  (Python `round`와 .NET `Math.Round`는 이진→십진 반올림 방식이 미세하게 달라 정확히 반올림 경계에 걸린 값에서만 다를 수 있다. 그런 값은 실제 입력에서 나오지 않으므로 허용한다.)
- 일치도 %는 `Grading.Percent(s)` = `(int)Math.Floor(s * 100 + 1e-9)`(R-011)만 쓴다. 등급·인정 판단은 이 %로만 한다(원시 s와 0.85를 직접 비교하지 않음).
- **참조 구현 동등성**: 접기·자석 코드는 `balance_check.py`와 같은 비교 연산자(`<` vs `≤`), 같은 순회 순서(조각 인덱스 순·꼭짓점 순), 같은 퇴화 처리를 쓴다(INTERFACE_CONTRACTS C-103~C-106). "더 나은" 공식으로 바꾸지 않는다 — 바꾸면 기획 수치가 달라진다. 개선이 필요하면 game-designer 작업으로 Python과 문서를 먼저 고친다.
- 정수 연산(돈·가격)은 `int`, 곱셈 후 나눗셈(내림). 부동소수 금지.

### 명령·오류
- 플레이어 명령(`OrigamiGame`, `ScreenFlow`)은 규칙 위반에 예외를 던지지 않고 `CommandResult`(거부 사유)를 반환하며 상태를 바꾸지 않는다(R-019). **모든 검사를 통과한 뒤에만** 상태를 바꾼다(검사 중 부분 변경 금지).
- 무효 접기는 `FoldPlan.Valid == false` + `FoldError`(예외 아님). 기준 순서처럼 "반드시 유효해야 하는" 적용(`FoldPlanner.Apply`)만 `InvalidOperationException`.
- 순수 함수의 잘못된 인자는 `ArgumentNullException`/`ArgumentOutOfRangeException`/`ArgumentException`(INTERFACE_CONTRACTS에 적힌 대로).
- 공개 데이터 타입은 불변(읽기 전용 프로퍼티, 생성자에서 목록 복사, `ReadOnlyCollection<T>`). 스냅샷은 수락된 명령마다 새 객체.
- 규칙 수치는 각 모듈의 상수 holder(`RuleConstants`, `FoldPlanner`, `DropSnap`, `Grading`, `RoomLayout`)에서만 읽고, 작품 값은 `ModelCatalog`에서만 읽는다(리터럴 하드코딩 금지).
- 문자열 비교는 `StringComparison.Ordinal`. 숫자 서식은 `CultureInfo.InvariantCulture`. 표시 문구는 `Core/Texts`(`GameTexts`)와 `Core/Folding/FoldTexts`, 카탈로그 문자열(작품·엔딩·책 설명)에만 둔다. 예외: 책 화면 전용 표시 라벨은 Runtime `Scripts/Book/BookTexts.cs`(C-192, DECISIONS).
- public 표면 변경 = 계약 변경 요청(Orchestrator → architect). 각 작업의 `<모듈>SurfaceTests`가 표면을 고정한다.

## Core 테스트 (Assets/Game/Tests/EditMode/Core)

- NUnit, UnityEngine 금지(dotnet과 Unity EditMode 양쪽에서 실행). 네임스페이스 `Origami.Tests.<폴더>`.
- 폴더 = 작업 소유: `Folding/`·`Recognition/`·`Catalog/`(009), `Economy/`·`Tutorial/`·`Game/`(010), `Book/`·`Texts/`·`Screens/`(011), `Room/`(014). QA는 `Qa/`에 쓴다(v1 Qa 파일 6개는 014가 삭제).
- 테스트 이름에 명세 ID: `F005_AC12_TopTwoLayersTulipPetal`, `R021_TieBreakIsOrderIndependent`, `J05_HouseVar006IsStar2`. 시나리오 헬퍼(`Sit`, `Ref(game, id)`, `Var(game, dy)`, `Waste`, `Night`)는 각 테스트 폴더 안 `internal static` 클래스(FEATURE_SPECS "공통 표기"의 줄임말과 같은 이름).
- **허용 오차는 FEATURE_SPECS 공통 표기를 따른다**: 종이 좌표 1e-6, 넓이 1e-6, CONTENT 6자리 리터럴 1e-4, 일치도 ±0.01. 정확해야 하는 값(조각 수, 겹 번호, 횟수, 돈)은 정확히 비교한다.
- **경계값 단언 금지**: 판정 경계(0.85/0.93/0.97)에서 0.01 이내인 실제 접기 결과의 등급·인정 여부를 단언하지 않는다. 특히 강아지 턱 없이(1~5단계, 0.849) 결과는 어떤 테스트도 기대값으로 쓰지 않는다(리뷰 ORIGAMI-007 W-6). 등급 함수 자체의 경계 검사(F-009 AC-04)는 `Grading.FromScore`에 s를 직접 넣어 한다.
- 지름길 결과(강아지 1단계 뺌 0.867, 매미 2·7단계 뺌 0.892)는 정책 검사로만 `< 0.91`을 단언한다.
- 참조 구현 기대값: 문서에 없는 세부 기대값(조각 꼭짓점 등)은 `balance_check.py`의 함수로 계산해 테스트에 리터럴로 넣을 수 있다. 출처(함수·입력)를 주석에 적는다.
- "거부 시 상태 불변"은 명령 전후 `Snapshot` 참조가 같음(`Assert.AreSame`)으로 검증한다.
- F-006 AC-04 주의(QA O-1): 새 종이에서 `SkipGuide` 직후의 `Unfold`는 기록이 비어 거부된다. "`Unfold` 후에도 가이드 안 보임"은 `Fold` 1회 뒤 `Unfold`로 검사한다.
- 판정 테스트는 비싸지 않다(수 ms). 그래도 `ModelCatalog.Recognition`을 재사용하고 테스트마다 카탈로그를 새로 만들지 않는다(가짜 카탈로그 테스트 제외).

## Runtime (Assets/Game/Scripts)

- 네임스페이스 = 폴더: `Origami.Runtime.Systems`, `.Folding`, `.Book`, `.Gameplay`, `.UI`, 루트 `Origami.Runtime`(Bootstrap).
- 게임 규칙·화면 전이·문구는 Core에 있다. Runtime은 입력 읽기, 그리기, 애니메이션, 카메라만. 버튼 핸들러는 `ScreenFlow` 메서드(또는 `BookPanel.Click…`)만 부른다.
- **Update는 `OrigamiApp` 하나.** 다른 컴포넌트는 `ProcessFrame`/`Tick`/`Refresh`를 공개하고 `OrigamiApp`이 정해진 순서로 부른다. 시간은 인자 `dt`로만 받는다(`Time.deltaTime`을 읽는 곳은 `OrigamiApp.Update` 한 곳).
- **OnGUI는 `OrigamiGui` 하나.** 다른 UI 컴포넌트는 `Draw(Rect)`만 공개한다.
- 입력: 레거시 Input Manager만(`Input.GetKey/GetKeyDown/GetMouseButton*/mousePosition/mouseScrollDelta`), 읽는 곳은 `InputReader` 한 곳. `com.unity.inputsystem` 쓰지 않음. 읽기(`Read`)와 처리(`ProcessFrame(InputFrame, dt)`)를 분리해 테스트는 처리만 호출한다.
- **`GameObject.CreatePrimitive`, Collider, Rigidbody, `Physics.*` 금지**(3D physics 모듈 없음, ARCHITECTURE §7). 메시는 `MeshFactory`/`PaperMeshBuilder`, 선택은 광선–평면 수학, 충돌은 Core `RoomMotion`.
- 코드로 만드는 머티리얼은 `Materials.Lit/Unlit`만(셰이더 이름을 다른 곳에 쓰지 않음). 가져온 모델은 묻힌 머티리얼을 그대로 쓴다(아래 "아트 에셋"). 만든 `Mesh`/`Material`/`Texture2D`/인스턴스는 만든 컴포넌트가 `OnDestroy`에서 `Destroy`.
- 씬·프리팹 YAML 금지. 루트는 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 훅 + `OrigamiApp.EnsureInstance()`. `Bootstrap.cs`·asmdef 수정 금지.
- Unity 객체 null 검사는 `!= null`(가짜 null 주의). public 프로퍼티는 진짜 null로 정규화.
- 로그(`Debug.LogError`/`LogException`)를 정상 경로에서 내지 않는다(PlayMode 테스트가 실패함 — v1 리뷰 W-5). 대체 경로(글꼴·셰이더 없음)는 조용히 기본값으로.
- `Vec2`(double) ↔ `Vector2/Vector3`(float) 변환은 경계 한 곳(헬퍼)에서. Core로 돌려보내는 좌표는 Runtime에서 반올림하지 않는다.

## UI (IMGUI)

- v1 결정 A-2 유지: **IMGUI(`OnGUI`)**. 문자열은 `Refresh`에서 미리 만들고 `OnGUI`는 그리기와 버튼만.
- `GUIStyle`/`Font`는 `OnGUI` 안에서 지연 생성. 한글 글꼴 `KoreanFont.Get(size)`(v1 파일 그대로, 수정 금지).
- 화면 좌표·크기·색은 private 상수이며 계약이 아니다. 기준 해상도 1280×720, `GUI.matrix` 배율 허용. 책은 앉음에서 화면 왼쪽 45%, 책장에서 전체.
- 도해·실루엣 그림은 `DiagramRaster`가 만든 `Texture2D`만(외부 이미지 없음). 쪽·보유 상태별 캐시.

## PlayMode 테스트 (Assets/Game/Tests/PlayMode)

- 폴더: `FoldTable/`(012), `Book/`(013), `Workshop/`(014), `AssetPipeline/`(017, 가져오기 파이프라인 회귀 검사). `BootstrapTests.cs`는 유지. 모두 `Origami.Tests.PlayMode` asmdef 하나(하위 폴더 자동 포함).
- **프레임 시간과 무관하게**: `[Test]`에서 컴포넌트를 만들고 `ProcessFrame(frame, dt)`를 정해진 dt로 직접 부른다(예: 이동 1초 = 0.02 × 50). `yield return null` 대기 수·실제 시간에 기대는 단언 금지.
  `[UnityTest]`는 자동 생성 훅 확인 등 꼭 필요할 때만.
- 마우스·키는 `PointerFrame`/`InputFrame`을 직접 만들어 넣는다(배치 모드에는 실제 입력이 없음). 종이 위 점은 `PaperPoint`를 직접 지정한다.
- 애니메이션은 `FinishAnimationNow()` 또는 충분한 dt로 끝낸 뒤 상태를 확인한다. Core 상태는 놓는 순간 바뀐다(A2-4).
- 테스트에서 만든 GameObject·메시·텍스처는 TearDown에서 파괴. 런타임 싱글턴(`OrigamiApp.Instance`)과 독립된 인스턴스를 만들어 쓴다.
- 시각 확인이 필요한 AC(조작감, 한글 표시, 책을 보고 접기)는 사용자 플레이테스트 항목으로 보고서에 남기고 테스트로 대신하지 않는다.

## 아트 에셋 (Assets/Game/Art, ARCHITECTURE §14)

- **CC0만.** 출처 페이지에서 라이선스를 직접 확인하고, `Art/CREDITS.md`에 파일마다 저장소 파일·원본·작성자·출처 URL·라이선스·받은 URL·바이트 크기·바꾼 점을 적는다. 확인 못 한 에셋은 넣지 않는다.
- 스타일: 한 가지 "아늑한 로우폴리"(단색·팔레트 머티리얼 위주). 키트를 섞을 때는 같은 작가 계열을 우선한다.
- 형식: FBX(애니메이션 포함)·OBJ(정적) + PNG/JPG. glTF/GLB·.blend 금지(A2-15).
- 위치·키: `Art/<Set>/Resources/<Set>/<Name>.<ext>` → `Resources.Load<T>("<Set>/<Name>")`. 이름은 PascalCase ASCII. 빌드에 쓰는 파일만 `Resources/` 아래에 둔다. 키 상수는 Runtime 한 곳.
- 예산: 아트 합계 ≤ 30 MB, 파일 ≤ 5 MB(권장: 캐릭터 FBX ≤ 3 MB), 텍스처 ≤ 1024 px(가져오기에서도 1024로 제한). 큰 원본은 줄여서 넣고 CREDITS에 "바꾼 점"으로 적는다.
- 메타: `gen_meta.py`(모델·텍스처는 GUID만 있는 최소 메타). 가져오기 설정을 메타에 손으로 쓰지 않는다 — 정본은 `Editor/ArtImportRules.cs`. 규칙을 바꾸면 `ArtImportRules.Version`을 올린다.
- 텍스처: 항상 Default 형식(노멀맵 이름 규칙 제외)·Texture2D·Sprite 아님 — 2D UI용 스프라이트는 Art가 아니라 코드/UI Toolkit으로 만든다. 그대로 `Resources.Load<Texture2D>`.
- 머티리얼: 모델 안에 묻힘(추출 금지), "Standard"·metallic 0·smoothness 0.15 고정(`ArtImportRules`, `OnPreprocessMaterialDescription` + `OnPostprocessModel`). 런타임은 `Shader.Find`로 모델 셰이더를 찾지 않는다. 색은 `MaterialPropertyBlock`, 새 머티리얼이 필요하면 `new Material(sharedMaterial)` 복사 후 `OnDestroy`에서 파괴. 로드한 프리팹·공유 머티리얼·메시는 수정·파괴 금지. `RenderSettings.fog` 금지(§14.5).
- 크기: 원본 배율에 기대지 않는다. 런타임이 합친 `Renderer.bounds`로 Core 상자(`RoomLayout`)에 균일 배율·바닥 가운데 정렬. 충돌·상호작용은 계속 Core 상자 수학. Collider 없음.
- 애니메이션: Legacy `Animation`. `playAutomatically = false`, 상태 `speed = 0`, `Tick(dt)`에서 `state.time` 진행 후 `anim.Sample()`(A2-17). 클립은 짧은 이름(`"Walk"`, `"Idle"`)으로 찾는다. Animator·AnimatorController·Playables 쓰지 않음.
- 저장: **Git LFS 쓰지 않음** — 일반 git 블롭(`.gitattributes`에서 `binary`). 파일당 ≤ 5 MB, 게임당 아트 ≤ 30 MB를 커밋 전에 `du -b`로 확인. `git lfs ls-files`는 비어 있어야 한다(DECISIONS 2026-11-28).

## 공통

- 새 Unity 파일·폴더는 `python3 studio/scripts/gen_meta.py <경로>`, `check_meta.py`로 확인. 기존 `.meta` GUID를 고치거나 복사하지 않는다(Fold3DSpike에서 파일을 옮겨 올 때도 `.meta`는 새로 생성).
- 주석·XML 문서는 영어 또는 한국어(파일 안에서 통일). 근거로 명세 ID(R-xxx, F-xxx AC-yy, C-1xx, D-1xx, A2-x)를 적는다.
- 삭제는 작업이 명시한 파일만(ORIGAMI-014의 v1 목록).
