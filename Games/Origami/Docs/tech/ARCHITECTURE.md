# Origami 시스템 구조 (v2)

> 담당: architect · 기준 태스크: ORIGAMI-008 (v2 기술 설계) · 이전 판: ORIGAMI-002 (v1, 방향키 접기 — **대체됨**)
> 기준 명세: `Docs/design/` v2 (GAME_RULES R-001~R-021, FEATURE_SPECS F-001~F-013, CONTENT_SPECS, `balance_check.py`).
> 시그니처·동작의 정본은 `INTERFACE_CONTRACTS.md`(C-1xx), 데이터 정의는 `DATA_STRUCTURE.md`(D-1xx), 규칙은 `CODING_CONVENTION.md`.
> 외부 3D 에셋 파이프라인: §14 (ORIGAMI-017).
> 기준 구현: 승인된 시험판 `Games/Fold3DSpike/` (Core `Folding/`, Scripts `FoldSpikeController`·`PaperMeshBuilder`·`SpikeMaterials`).

## 0. 요약

- **Core(엔진 없음)** 에 게임의 모든 규칙과 계산을 둔다: 접기 엔진(전체 겹·위 N겹·접었다 펴기·뒤집기·되돌리기), 놓기 자석,
  윤곽 판정, 작품 카탈로그·책 쪽 배치, 경제·7일·엔딩, 튜토리얼 가이드, 책 도해 데이터, 한국어 문구, 화면 흐름, 방 이동·충돌 수학.
  전부 `dotnet test`로 검증한다.
- **Runtime(Unity)** 은 입력 읽기, 3D 그리기(방·캐릭터·종이 메시·애니메이션), 카메라, IMGUI 화면만 한다.
  모든 시간 의존 동작은 `ProcessFrame(frame, dt)` 하나로 들어오므로 PlayMode 테스트가 프레임 시간과 무관하게 동기 호출로 검증한다.
- `balance_check.py`는 **접기·자석의 참조 구현**이다. C# 엔진은 그 알고리즘과 상수를 그대로 옮긴다(C-103, C-106).
  판정 IoU는 격자 대신 **행 주사(scanline) 적분**으로 계산한다(C-110, 정확값과의 오차 ≤ 0.002, `CONTENT_SPECS.md` §4와 ±0.01 이내 — §9 근거).
- v1 코드(Core `Content/Rules/Run/Flow`, Scripts `OrigamiController/OrigamiView/ModelIcons/OrigamiButton(s)`, v1 테스트)는 v2가 대체한다.
  **삭제는 마지막 통합 작업(ORIGAMI-014)** 에서 한다 — v1 Unity 화면이 v1 Core를 쓰므로, 그 화면을 대체하는 작업 전에는 지울 수 없다(§10).

## 1. 모듈과 책임

### 1.1 Core (`Unity/Assets/Game/Core/`, 어셈블리 `Origami.Core`, `noEngineReferences: true`)

| 모듈 | 폴더 / 네임스페이스 | 책임 | 의존 (Core 안) | 구현 작업 |
|---|---|---|---|---|
| Geometry | `Core/Geometry/` · `Origami.Core.Geometry` | `Vec2`(double, 시험판 복사) | 없음 | 009 |
| Folding | `Core/Folding/` · `Origami.Core.Folding` | 종이 상태(조각·겹·앞뒤·자국), `FoldPlanner`(R-007 전체/위 N겹/접었다 펴기/뒤집기), `FoldSession`(되돌리기·처음으로·횟수), `DropSnap`(R-021 기준점·자국 선·자석), `FoldGesture`(잡기 자석·겹 수 고르기·미리보기), 도해 선(`LineOnPaper`), 기준 순서 단계(`FoldStep`) | Geometry | 009 |
| Recognition | `Core/Recognition/` · `Origami.Core.Recognition` | 윤곽 행 주사(`Outline`), D4 대칭, 일치도(IoU), `RecognitionCatalog`(기준 윤곽 미리 계산), `Recognizer.Recognize` 순수 함수(R-011~R-013), `Grade`·`Grading`(97/93/85) | Geometry, Folding | 009 |
| Catalog | `Core/Catalog/` · `Origami.Core.Catalog` | 작품 5종 정의(값·기준 접기 순서·책 설명), 인기 작품 표, 엔딩 정의(이름·문구), 책 쪽 배치(`BookLayout` 44쪽), 상호작용 대상 ID | Folding, Recognition | 009 |
| Economy | `Core/Economy/` · `Origami.Core.Economy` | 규칙 수치(`RuleConstants`), 판매가(R-015), 완성 수 표, 종료 사유, 엔딩 판정(R-018) | Catalog, Recognition | 010 |
| Tutorial | `Core/Tutorial/` · `Origami.Core.Tutorial` | 종이비행기 6단계 가이드 목표(카탈로그에서 생성), 단계 점수(시험판 `FoldScoring` 공식) | Folding, Catalog | 010 |
| Game | `Core/Game/` · `Origami.Core.Game` | `OrigamiGame`: 공방 모드(R-003), 모든 명령(GAME_RULES "명령" 표)·거부 순서(R-019), 종이·튜토리얼·책 상태·판매·정산·파산·엔딩, 불변 스냅샷 | 위 모두 | 010 |
| Book | `Core/Book/` · `Origami.Core.Book` | 책 쪽 내용 데이터(목차·접기 기호·표지·단계 도해·완성·봉인) — `BookPages.Get(page, owned)` 순수 함수 | Geometry, Folding, Catalog | 011 |
| Texts | `Core/Texts/` · `Origami.Core.Texts` | 모든 한국어 표시 문구(HUD, 안내, 거부 사유, 판정 결과, 정산·판매대·결과 화면, 겹 표시) | Game, Book, Catalog, Economy, Folding, Recognition, Tutorial | 011 |
| Screens | `Core/Screens/` · `Origami.Core.Screens` | `ScreenFlow`: `OrigamiGame` 단독 소유, 화면 덮개(일시정지·새로 시작 확인·정산·결과), 키→명령 매핑(모드별, 한 프레임 한 키), 1회성 안내, 최근 메시지, 종이 변화 알림 | Game, Texts, Folding, Geometry | 011 |
| Room | `Core/Room/` · `Origami.Core.Room` | 방 배치 수치(CONTENT §6), 원(반지름 0.3)–상자 충돌 이동 `RoomMotion.Step`, 상호작용 후보 `Interaction.Nearest`(R-004) | Geometry, Catalog | 014 |
| (루트) | `Core/GameInfo.cs` · `Origami.Core` | 게임 이름·버전 | 없음 | 014(버전 2.0.0) |

### 1.2 Runtime (`Unity/Assets/Game/Scripts/`, 어셈블리 `Origami.Runtime`)

| 모듈 | 폴더 / 네임스페이스 | 책임 | 구현 작업 |
|---|---|---|---|
| Systems | `Scripts/Systems/` · `Origami.Runtime.Systems` | `Materials`(시험판 `SpikeMaterials` 이식: "Legacy Shaders/Diffuse"·"Sprites/Default"), `MeshFactory`(상자·원기둥·구·캡슐 코드 메시) | 012 |
| Folding (view) | `Scripts/Folding/` · `Origami.Runtime.Folding` | `FoldTable`: 테이블 위 종이 메시·끌기 입력·겹 수 고르기·미리보기(유령/들어 올림)·접힘/접었다 펴기/뒤집기 애니메이션·자국 선·자석 강조·가이드 표시·앉은 시점 카메라 궤도. `PaperMeshBuilder`/`OverlayBuilder`/`SphereMesh`(시험판 이식). `IFoldTableHost`(명령 연결 인터페이스), `PointerFrame` | 012 |
| Book (view) | `Scripts/Book/` · `Origami.Runtime.Book` | `BookPanel`: IMGUI 두 쪽 펼침 그리기(목차 클릭·사기 버튼), `DiagramRaster`: Core 도해 데이터 → `Texture2D`(코드 래스터화, 캐시) | 013 |
| Gameplay | `Scripts/Gameplay/` · `Origami.Runtime.Gameplay` | `OrigamiApp`(루트, `ScreenFlow` 단독 소유, 프레임 분배), `InputFrame`/`InputReader`(레거시 Input 읽기), `WorkshopView`(방·가구·판매대 진열·쓰레기통·창문), `CharacterView`, `CameraRig`(걷기 추적·앉기 전환), `FoldHostAdapter`(`IFoldTableHost` 구현) | 014 |
| UI | `Scripts/UI/` · `Origami.Runtime.UI` | `OrigamiGui`(유일한 `OnGUI`: HUD·버튼·안내·토스트·덮개 화면·책 패널·커서 겹 표시 호출 순서), `KoreanFont`(**v1 유지**) | 014 (`KoreanFont`는 변경 없음) |
| Bootstrap | `Scripts/Bootstrap.cs` · `Origami.Runtime` | 카메라·`[Bootstrap]` 루트 생성 (**v1 유지, 수정 없음**) | — |
| Editor | `Editor/StudioBuild.cs`, `Editor/ArtImportRules.cs` | 빈 씬 생성·Windows 빌드 (**유지**). 외부 모델·텍스처 가져오기 설정을 코드로 고정(`AssetPostprocessor`, §14) | — / 017 |

## 2. 의존 방향 (순환 없음)

정본은 §1.1 표의 "의존" 열이다. 아래 그림은 요약이다.

```
Core:   Geometry ◀─ Folding ◀─ Recognition ◀─ Catalog ◀─ Economy ◀─┐
                      ▲  ▲          ▲            ▲  ▲               │
                      │  └──────────┼── Tutorial ┘  │               │
                      │             └──── Book ─────┘               │
                      └──────────────────────────────────── Game ───┘ (Folding·Recognition·Catalog·Economy·Tutorial 모두 참조)
                                                    Texts ──▶ Game, Book, …
                                                    Screens ─▶ Game, Texts
          Geometry ◀─ Room ─▶ Catalog (InteractTargets만)

Runtime: UI(OrigamiGui) ─▶ Gameplay(OrigamiApp) ─▶ Folding view, Book view, Systems ─▶ Core
         Folding view ─▶ Core.Folding/Geometry만 (Game/Screens 모름 — IFoldTableHost로 연결)
         Book view    ─▶ Core.Book/Catalog/Folding/Geometry (+ Core.Texts 허용), Runtime.UI.KoreanFont (Game/Screens 모름 — IBookHost로 연결)
```

- Book view → `Runtime.UI.KoreanFont`는 공유 글꼴 도우미 하나만 쓰는 예외다(CODING_CONVENTION의 `KoreanFont.Get` 규칙). `KoreanFont`는 Book view를 모르므로 순환이 없다
  (UI의 `OrigamiGui` → Book view 호출과는 다른 클래스, 같은 `Origami.Runtime` 어셈블리). Folding/Geometry는 `DiagramRaster`가 도해(`StepDiagram`·`PaperState`)를 그리려고 쓴다(ORIGAMI-013 W-2).
  책 화면 전용 표시 라벨은 Runtime `Scripts/Book/BookTexts.cs`(internal)에 둔다 — 게임 규칙 문구는 Core/Texts(C-170, C-192, DECISIONS).
- 화살표 = "참조한다". 역방향 `using` 금지. Book은 Game을 모른다(보유 목록을 인자로 받음). Room은 Game을 모른다.
- Runtime `Folding`은 `ScreenFlow`·`OrigamiGame`을 직접 부르지 않는다. 명령은 `IFoldTableHost`(C-191)를 통해서만 낸다.
  그래서 012(접기 테이블)는 009만 끝나면 010/011과 **병렬로** 만들 수 있고, 테스트는 `FoldSession`을 감싼 가짜 host로 한다.
- Core 전체는 한 어셈블리(`Origami.Core`)다. 폴더별 asmdef는 만들지 않는다(작은 게임, v1과 같음). 방향 규칙은 리뷰가 `using`으로 검사한다.

## 3. 구성도

```
 Unity 프레임 (OrigamiApp.Update)                                   Core (결정적, dotnet 테스트)
 ┌──────────────────────────────────┐ InputFrame     ┌──────────────────────────────────────────────┐
 │ InputReader.Read() (레거시 Input) │──────────────▶ │ OrigamiApp.ProcessFrame(frame, dt)            │
 └──────────────────────────────────┘                │  1. 덮개/모드별 키 → ScreenFlow.ProcessKeys    │
                                                     │  2. 걷기: RoomMotion.Step → CharacterView      │
 ┌─────────────────────┐  IFoldTableHost(Fold/Crease)│  3. 앉음: FoldTable.ProcessFrame(pointer, dt)  │
 │ FoldTable (앉음)     │◀──────────── FoldHostAdapter│  4. CameraRig.Tick(dt), 진열·창문 갱신          │
 │  FoldGesture·DropSnap│──────────────────────────▶ └──────────────┬───────────────────────────────┘
 │  PaperMeshBuilder    │                                            │ 명령 메서드
 └─────────────────────┘                             ┌──────────────▼───────────────┐   ┌────────────────┐
 ┌─────────────────────┐ GameSnapshot·Texts           │ ScreenFlow (Core.Screens)     │──▶│ OrigamiGame     │
 │ OrigamiGui (OnGUI)   │◀────────────────────────────│  덮개·키 매핑·안내·메시지      │   │ (Core.Game)     │
 │  HUD·버튼·덮개·책    │  버튼 → 같은 ScreenFlow 메서드│  LastSheetChange              │◀──│  Snapshot (불변) │
 │  BookPanel·Diagram   │                              └───────────────────────────────┘   └──┬─────────────┘
 └─────────────────────┘                                                                     │ 순수 함수
                                       FoldPlanner·FoldSession·DropSnap·Recognizer·Pricing·EndingRules·BookPages·GameTexts
```

## 4. 소유와 생명주기

- `OrigamiGame` 인스턴스는 런타임에 하나, `ScreenFlow`가 생성자에서 만들어 단독 소유한다(외부에는 `Snapshot`만). 런 재시작은 객체 재생성이 아니라 `NewRun()`.
- `ScreenFlow`는 `OrigamiApp`이 `Awake`에서 만들어 앱 종료까지 소유한다. 다른 Runtime 코드는 `app.Flow`로만 접근한다.
- 런타임 오브젝트: `"[Origami]"` GameObject(DontDestroyOnLoad) 하나에 `OrigamiApp`, `OrigamiGui`. 자식으로 `WorkshopView`(방), `CharacterView`, `FoldTable`(테이블 위 종이 루트), `BookPanel`.
  `OrigamiApp`의 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 훅이 `EnsureInstance()`를 부른다(v1·시험판과 같은 패턴). `Bootstrap.cs`는 수정하지 않는다.
- **Update는 `OrigamiApp` 하나만** 가진다. 자식 컴포넌트는 자체 `Update`가 없고 `OrigamiApp.ProcessFrame`이 정해진 순서로 부른다(순서 결정적, 테스트가 같은 경로를 탄다).
  예외: `FoldTable`은 012 단독 테스트를 위해 `ProcessFrame(PointerFrame, dt)`를 공개하지만, 게임에서는 `OrigamiApp`만 그것을 부른다.
- `OnGUI`는 `OrigamiGui` 하나만. 책·커서 표시·덮개는 그 안에서 정해진 순서로 `Draw…`를 호출한다.
- 씬 전환 없음(단일 빈 씬). 메시·머티리얼·텍스처는 코드로 만들고 `OnDestroy`에서 파괴한다.

## 5. 통신 방식 (이벤트 없음)

- **명령-결과 + 폴링.** Core 타입은 C# event/콜백을 노출하지 않는다(v1 원칙 유지).
  - Runtime → Core: `ScreenFlow` 메서드(키·버튼·끌기 놓기). 키와 버튼은 같은 메서드로 수렴한다.
  - Core → Runtime: 명령은 `CommandResult`를 반환하고, Runtime은 매 프레임 `ScreenFlow.Snapshot`(불변, 명령이 수락될 때만 새 객체)을 읽는다.
    변화 감지는 **참조 비교**(스냅샷·`PaperState`는 불변) 또는 일련번호(`ScreenFlow.Message.Serial`, `LastSheetChange.Serial`, `SettlementSerial`)로 한다.
- 접기 명령은 **놓는 순간 Core에 적용**하고, 화면은 `FoldPlan`(접기 전 움직이는 조각·접은 뒤 조각)으로 0.4초 애니메이션만 한다(시험판의 "애니메이션 뒤 확정"과 다름 — 상태가 늘 Core와 같다).
  애니메이션 중에는 새 끌기를 시작하지 않는다. `F` 뒤집기·`Undo` 등은 `LastSheetChange.Kind`를 보고 연출한다(뒤집기 0.6초, 나머지 즉시).
- 시간: Core 규칙은 시간 인자를 받지 않는다(R-001). 유일한 예외는 표현용 순수 함수 `RoomMotion.Step(pos, move, dt)`(규칙 상태를 바꾸지 않음).
  토스트 2초·카메라 전환 0.5초·애니메이션은 Runtime이 `dt`로 진행한다.

## 6. 모드별 화면과 입력 (F-002, F-003, F-005, F-007, F-013)

| Core 모드 (`WorkshopMode`) | 카메라 (`CameraRig`) | 입력 처리 | 화면 (`OrigamiGui`) |
|---|---|---|---|
| `Walking` | 캐릭터 기준 (0, 6.0, −5.5), 머리 (0, 0.8, 0)를 봄, 부드럽게 추적, 회전 없음 | WASD/방향키 이동(`RoomMotion`), E = `Interact(Interaction.Nearest)`, Esc = 일시정지 | HUD, "E: …" 안내, 걷기 안내(첫 앉기 전) |
| `Seated` | 0.5초 동안 접기 시점으로 전환, 이후 오른쪽 끌기 궤도·휠 확대(끌지 않을 때) | 마우스 = `FoldTable`(끌기 중 Shift·Ctrl·휠·[ ]·Esc), 키 Space/Enter/X/Z·Backspace/R/F/B/G/Q, 책 펼침이면 ←/→ | HUD + 앉음 버튼, 책 펼침이면 왼쪽 45%에 `BookPanel`, 커서 옆 "겹: …" |
| `Bookshelf` | 걷기 카메라 유지 | ←/→ 쪽, Esc/E/B 닫기, 목차 클릭·사기 버튼 | 전체 화면 `BookPanel` |
| `Counter` | 걷기 카메라 | Esc/E 닫기 | 판매대 보기 패널(오늘 인기·예상가·합계) |
| `SleepConfirm` | 걷기 카메라 | Enter = 예, Esc = 아니오 | 확인 창 |
| `Ended` | 걷기 카메라 | Enter = 새로 시작 | 결과 화면 |
| (덮개) 정산 | — | Enter/버튼 = 확인 | 정산 화면 (`Sleep(yes)` 직후, 종료 시 정산 → 결과) |
| (덮개) 일시정지·새로 시작 확인 | — | Esc = 닫기, Enter = 확인 | 메뉴 |

- 키 → 명령 매핑과 우선순위의 정본은 `ScreenFlow.ProcessKeys`(C-171). 한 프레임에 한 키만 처리한다(v1 A-4와 같은 이유).
- 끄는 중(`FoldTable.IsDragging`)에는 `ScreenFlow` 키를 넘기지 않는다(Esc는 끌기 취소로 소비). 애니메이션 중에도 키 명령은 받는다(상태는 Core가 정함, 애니메이션은 즉시 끝냄).
- 창 포커스를 잃으면(`Application.isFocused == false`) 이동 입력 0(F-002 엣지 케이스).

## 7. 3D 표현 (Runtime)

- 좌표: 방은 월드 (x, z) 미터(CONTENT §6), y 위. 종이 좌표 1 = 10cm → `FoldTable` 루트를 테이블 위 (2.5, 0.75, 1.6)에 두고 **균일 배율 0.1**, 종이 (x, y) → 로컬 (x, 겹 높이, y)(종이 +y = 앉은 사람에게서 먼 쪽 = 월드 +z).
  시험판 메시 상수(겹 간격 0.0035 등)는 종이 단위 그대로 쓰고 배율이 줄인다.
- 카메라 근평면: 걷기 0.1, 앉음 0.01(겹 간격 0.35mm의 z-fighting 방지 — 걷기 시점 8m 거리에서 깊이 정밀도 확보). `CameraRig`가 모드 전환 때 바꾼다. Bootstrap 카메라(직교)는 `CameraRig`가 원근으로 설정한다(Bootstrap 수정 없음).
- 렌더 파이프라인: 내장(Built-in). 코드로 만드는 머티리얼은 `Materials`(시험판 셰이더 순서). 외부 모델은 모델 안에 묻힌 "Standard" 머티리얼을 그대로 쓴다(§14). **`GameObject.CreatePrimitive`·Collider·Rigidbody·Physics 쓰지 않음** — Origami 매니페스트에는 3D physics 모듈이 없다(2D 템플릿으로 생성). 메시는 `MeshFactory`, 충돌은 Core `RoomMotion`(상자 수학), 마우스 선택은 광선–평면 수학.
- 조명: 방향광 1개(그림자 부드럽게) + 평면 주변광(시험판 값). 창문 빛은 머티리얼 색만 바꾼다(그날 쓴 종이 수 0~5).
- 판매대 진열: `GameSnapshot.Display[i].Shape`(플레이어가 접은 상태)를 `PaperMeshBuilder`로 칸 i에 그림. 쓰레기통 종이 공 = min(실패 수, 10)개 구.

## 8. 외부 의존 / 엔진 기능

| 의존 | 용도 | 상태 |
|---|---|---|
| 레거시 Input Manager (`Input.GetKey/GetKeyDown/GetMouseButton*/mousePosition/mouseScrollDelta`) | 모든 입력 (`InputReader`) | v1·시험판 CI PlayMode·Windows 빌드에서 동작 확인. `com.unity.inputsystem` 쓰지 않음 |
| IMGUI (`com.unity.modules.imgui`, 이미 있음) | HUD·책·덮개 | v1 결정 A-2 유지(UI Toolkit은 패널 에셋 필요) |
| 내장 셰이더 "Legacy Shaders/Diffuse", "Sprites/Default" | 종이·가구·오버레이 | 기본 Always Included 목록(시험판 Windows 빌드에서 사용자 확인) |
| `Font.CreateDynamicFontFromOSFont` | 한글 (`KoreanFont`, v1 유지) | CI로는 글리프 검증 불가 → 사용자 플레이테스트 |
| `Texture2D`(런타임 생성) | 책 도해 (`DiagramRaster`) | 외부 이미지 없음 |
| Unity 기본 모델·텍스처 가져오기(FBX/OBJ, PNG/JPG) + `AssetPostprocessor` | 외부 CC0 방·가구·캐릭터 (§14) | 패키지 없음. 017 샘플로 CI PlayMode 검증 |
| 레거시 `Animation` 컴포넌트 (`com.unity.modules.animation`, 이미 있음) | 캐릭터 애니메이션 (§14.6) | AnimatorController 에셋 없음 |
| 내장 셰이더 "Standard" | 가져온 모델의 머티리얼 | 모델 머티리얼이 참조하므로 빌드에 포함(쓰는 변형만). Always Included에 넣지 않음(§14.5) |
| Git LFS (`.gitattributes`: `*.fbx`, `*.png`, `*.jpg` …) | 바이너리 에셋 저장 | 푸시 전에 `git lfs push` 필요(§14.8). CI 체크아웃은 `lfs: true` |

**패키지·ProjectSettings 변경 없음.** 근거: 3D 물리 모듈이 필요한 기능(CreatePrimitive의 Collider, Physics.Raycast)을 쓰지 않도록 규칙으로 막았다(§7, CODING_CONVENTION).
만약 통합 CI에서 3D physics 모듈이 꼭 필요한 문제가 드러나면, 별도 architect 작업으로 `Unity/Packages/manifest.json`에 `"com.unity.modules.physics": "1.0.0"`을 추가한다(Fold3DSpike와 같은 구성, 검증 항목 unity-compile/unity-playmode/unity-build). 지금은 제안하지 않는다.

## 9. 판정 계산 방법 결정 (R-011 "계산 방법은 architect 결정")

- **행 주사 적분**(C-110): 윤곽(볼록 조각들의 합집합)을 높이 h = 0.001마다 가로선 y = (k + 0.5)·h로 자르고, 각 선에서 조각 구간의 합집합 길이를 정확히 구해 h를 곱해 더한다.
  교집합도 같은 행에서 두 구간 목록을 합쳐 구한다. 무게중심도 같은 적분. D4 대칭은 **꼭짓점 좌표를 정확히 변환**한 뒤 다시 주사한다.
- 이유: x 방향은 정확, y 방향만 중점 적분이라 격자(0.004)보다 훨씬 정확하고 빠르다. 외부 다각형 연산 라이브러리 없이 50줄 안팎. 결정적(같은 입력 → 같은 비트).
- 검증(architect 프로토타입, `balance_check.py`의 접기 엔진 위에서 이 방법을 Python으로 구현해 실행): h = 0.002 / 0.001 / 0.0005 결과가 서로 0.002 이내, `CONTENT_SPECS.md` §4 값과 모두 0.005 이내.

| 경우 | §4 (격자 0.004) | 행 주사 h = 0.001 |
|---|---|---|
| J-04 / J-05 / J-06 | 0.992 / 0.951 / 0.892 | 0.992 / 0.950 / 0.892 |
| J-07 / J-08 / J-09 / J-10 | 0.804 / 0.783 / 0.728 / 0.693 | 0.799 / 0.784 / 0.729 / 0.694 |
| J-11 / J-12 / J-14 / J-15 / J-18 | 0.677 / 0.477 / 0.714 / 0.648 / 0.782 | 0.677 / 0.477 / 0.717 / 0.647 / 0.783 |
| J-01·J-02·J-03·J-13·J-16·J-17 | 1.000 (J-13 0.994~1.000) | 1.000 |
| 작품끼리 최대 (tulip–dog) | 0.611 | 0.612 |
| 강아지 턱 없이 (리뷰 W-6) | 0.849 | 0.848 — **경계값: 테스트 기대값으로 쓰지 않는다** |

- 비용: 기준 5종 × 대칭 8개의 행 목록은 한 번만 계산해 `RecognitionCatalog`에 둔다. `Finish` 1회 = 플레이어 윤곽 1회 주사 + 40회 구간 병합(수 ms).

## 10. v1 → v2 이전 계획

원칙: v2 코드는 **v1과 다른 폴더·네임스페이스**에 새로 만든다(공존 중 이름 충돌·컴파일 깨짐 없음). v2 코드는 v1 네임스페이스를 `using`하지 않는다.
v1은 v1 Unity 화면이 살아 있는 동안 컴파일되어야 하므로, **v1 화면을 대체하는 ORIGAMI-014에서 v1 Core·화면·테스트를 한꺼번에 지운다**.

| v1 항목 | 처리 | v2 대체 | 삭제 시점 |
|---|---|---|---|
| `Core/Content/` (`ModelDefinition`, `OrigamiCatalog`, `FoldAction(s)`, `Grade`, `ModelIds`, `EndingIds`, `EndingDefinition`) | **교체** — 작품·순서·엔딩이 모두 바뀜. 패턴(코드 카탈로그, 읽기 전용 목록, 계약 테스트 대조)은 유지 | `Core/Catalog/`(009), `Grade` → `Core/Recognition/Grade`(009) | 014 |
| `Core/Rules/Pricing` | **이식** — 공식 동일(R-015). 코드를 v2 카탈로그 기준으로 옮김 | `Core/Economy/Pricing`(010) | 014 |
| `Core/Rules/CompletionCounts` | **이식** — 구조 동일(작품 × 3등급, 값 동등), v2 카탈로그 기준 | `Core/Economy/CompletionCounts`(010) | 014 |
| `Core/Rules/EndingRules`, `RunEndReason` | **교체** — 엔딩 조건·순서 변경(R-018). enum은 같은 값으로 재정의 | `Core/Economy/`(010) | 014 |
| `Core/Rules/RuleConstants` | **교체** — 수치 다수 변경 | `Core/Economy/RuleConstants`(010) + 모듈별 상수(C-103, C-106, C-110, C-180) | 014 |
| `Core/Rules/Grading`, `Unlocking` | **삭제** — 벌점 등급·해금 사슬 없음 | `Recognition/Grading`(009), 책 구입(010) | 014 |
| `Core/Run/` (`OrigamiRun`, 스냅샷·결과·거부 사유) | **교체** — 명령 체계가 다름. 거부 시 상태 불변·첫 사유 반환·불변 스냅샷 원칙 유지 | `Core/Game/`(010) | 014 |
| `Core/Flow/` (`OrigamiFlow`, `FlowTexts`, `FlowKeys`, `FlowScreen`) | **교체** — 화면 흐름·문구가 다름. "화면 흐름·문구를 Core에" 원칙(v1 A-3) 유지 | `Core/Screens/`, `Core/Texts/`(011) | 014 |
| `Core/GameInfo.cs` | **유지**, `Version` = "2.0.0" | — | 014에서 값만 변경 |
| `Scripts/Bootstrap.cs`, `Editor/StudioBuild.cs`, asmdef 4개 | **유지**(수정 없음) | — | — |
| `Scripts/UI/KoreanFont.cs` | **유지**(수정 없음, v2 GUI가 그대로 사용) | — | — |
| `Scripts/Gameplay/OrigamiController.cs` | **교체** | `Scripts/Gameplay/OrigamiApp`(014) | 014 |
| `Scripts/UI/OrigamiView.cs`, `ModelIcons.cs`, `OrigamiButton.cs`, `OrigamiButtons.cs` | **교체** | `Scripts/UI/OrigamiGui`(014), `Scripts/Book/`(013) | 014 |
| `Tests/EditMode/Core/Contracts/`, `Flow/`, `Rules/`, `Run/` | **삭제**(v1 계약·동작 테스트) | v2 테스트 폴더(§12) | 014 |
| `Tests/EditMode/Core/Qa/` v1 파일 6개(`FlowEndingPlaythroughQaTests`, `FlowFuzzQaTests`, `RunDifferentialQaTests`, `RunReferenceModel`, `ExtremeRunQaTests`, `CatalogSpecQaTests`) | **삭제** — 파일 이름으로만 지운다(같은 폴더의 v2 QA 테스트는 남김) | v2 QA 테스트 | 014 |
| `Tests/EditMode/Core/GameInfoTests.cs`, `Tests/PlayMode/BootstrapTests.cs` | **유지** | — | — |
| `Tests/PlayMode/Flow/` | **삭제** | `Tests/PlayMode/Workshop/`(014) | 014 |
| Fold3DSpike Core `Folding/` (`Vec2`, `Paper`, `FoldPlanner`, `Guide`, `FoldSession`) | **복사·확장**해 Origami로 가져온다(게임은 자기 완결 — Origami는 Fold3DSpike를 참조하지 않는다). 시험판 폴더는 건드리지 않는다 | `Core/Geometry/Vec2`, `Core/Folding/*`(위 N겹·자국·Python 동등 분할 규칙으로 변경), `Core/Tutorial/StepScoring`(= `FoldScoring.Evaluate`) | 시험판은 그대로 둠 |
| Fold3DSpike Scripts `PaperMeshBuilder`/`OverlayBuilder`/`SphereMesh`, `SpikeMaterials`, `FoldSpikeController` | **이식** — 네임스페이스만 바꾸고 기능 확장(자국 선, 겹 표시, 배율 0.1). `SpikeMaterials`의 CreatePrimitive 대체 경로는 빼고 "Standard" → unlit 순서만 남김 | `Scripts/Folding/`, `Scripts/Systems/Materials`(012) | 시험판은 그대로 둠 |

- v1 DECISIONS의 A-1(코드 카탈로그), A-2(IMGUI), A-3(흐름·문구를 Core에), A-4(한 프레임 한 키), A-5(OS 한글 글꼴)는 **v2에서도 유효**하다. A-6·A-7(v1 화면 단축키·잠긴 작품)은 v1 화면과 함께 폐기.
- 014 전까지 게임을 실행하면 v1 화면이 보인다(v2 Unity 부품은 자동 생성 훅이 없음). 014가 v1 훅을 지우고 v2 `OrigamiApp` 훅을 넣는다.

## 11. 저장 데이터 전략

- 페이즈 1은 저장/불러오기가 없다(GAME_DESIGN 범위 밖). 런은 항상 R-002 상태에서 시작.
- 나중(페이즈 3)에 넣을 때의 방침은 `DATA_STRUCTURE.md` D-170: **JSON**, Core가 엔진 없는 DTO(`[Serializable]` 공개 필드)를 만들고 Runtime이 `JsonUtility`로 쓰고 읽는다.
  테이블 위 종이는 조각 좌표가 아니라 **적용한 동작 목록(`FoldStep`)** 으로 저장하고 불러올 때 다시 적용한다(결정성 R-001, 작고 버전에 강함). 진열 작품의 모양도 동작 목록으로.

## 12. 아키텍처 결정 (Orchestrator가 `Docs/DECISIONS.md`에 옮겨 적을 것)

| ID | 결정 | 이유 |
|---|---|---|
| A2-1 | Core를 Geometry/Folding/Recognition/Catalog/Economy/Tutorial/Game/Book/Texts/Screens/Room 폴더로 나누고 한 어셈블리로 둔다. v2 폴더 이름은 v1(Content/Rules/Run/Flow)과 겹치지 않는다 | v1과 공존하는 동안 충돌 없이 단계적으로 대체 |
| A2-2 | 접기 엔진은 `balance_check.py`의 알고리즘·상수·순서를 그대로 옮긴다(분할 퇴화 규칙, 겹 재배치, 이어짐·얹힘, 자국, 뒤집기, 자석 동점 규칙) | 기획 수치(J 값, 자석 결과, AC 넓이)를 그대로 재현 |
| A2-3 | 판정 IoU = 행 주사 적분(h = 0.001), D4는 꼭짓점 변환, 기준 윤곽은 `RecognitionCatalog`가 한 번 계산 | 정확·빠름·결정적, 패키지 없음(§9) |
| A2-4 | 접기는 놓는 순간 Core에 적용하고 화면은 그 결과를 애니메이션만 한다 | 표현과 규칙 상태가 어긋나지 않음, 테스트가 애니메이션을 기다리지 않음 |
| A2-5 | 모든 시간 진행은 `OrigamiApp.ProcessFrame(InputFrame, dt)` 하나로. 자식 컴포넌트에 `Update` 없음. PlayMode 테스트는 `[Test]`에서 dt를 직접 넘김 | 프레임 시간과 무관한 결정적 PlayMode 테스트 |
| A2-6 | 방 충돌·이동·상호작용 후보는 Core 순수 함수(`RoomMotion`, `Interaction`), Physics·Collider·CreatePrimitive 금지 | dotnet으로 F-002 수치 검증, 3D physics 모듈 없이 동작(패키지 변경 없음) |
| A2-7 | `FoldTable`은 `IFoldTableHost`로만 명령을 낸다 | Unity 접기 테이블(012)을 Core 규칙(010/011)과 병렬 개발 |
| A2-8 | 겹 수 고르기: 휠 위로(`mouseScrollDelta.y > 0`) 또는 `]` = +1(전체 → 위 1 → … → 위 M−1 → 전체), 휠 아래·`[` = −1. M ≤ 1이면 항상 전체 | F-005 "휠(또는 [ / ])" 방향 미정 → 결정 |
| A2-9 | 튜토리얼 단계 점수의 "반대쪽 접기 허용"(시험판 `EitherSide`)은 종이비행기 1단계(가운데 세로 자국)와 6단계(반 접기)에만 둔다 | 두 단계는 좌우 어느 쪽을 접어도 같은 선 |
| A2-10 | `SkipGuide` 거부 순서: RunEnded → WrongMode(앉음 아님) → NoGuide(종이 없음 포함) | R-008/R-019 미정 부분 |
| A2-11 | 덮개(정산·일시정지·새로 시작 확인·결과)가 열려 있으면 `ScreenFlow`는 게임 명령을 Core에 보내지 않고 `WrongMode`로 거부한다 | 정산 화면 뒤에서 걷거나 접는 일 방지 |
| A2-12 | 책 도해는 Runtime에서 `Texture2D`로 코드 래스터화(쪽·보유 상태별 캐시), 내용 데이터는 Core `BookPages` | IMGUI에서 다각형을 그릴 수단이 텍스처뿐, 도해 내용은 dotnet 검증 |
| A2-13 | v1 Core·화면·테스트는 ORIGAMI-014(v2 화면 통합)에서 한꺼번에 지운다. `KoreanFont`, `Bootstrap`, `StudioBuild`, asmdef, `GameInfo`는 유지 | v1 화면이 v1 Core에 의존 — 대체하는 작업에서만 삭제 |
| A2-14 | 카메라 근평면 걷기 0.1 / 앉음 0.01 | 0.35mm 겹 간격의 z-fighting 방지 |
| A2-15 | 외부 3D 에셋은 FBX(애니메이션 포함)·OBJ(정적) + PNG/JPG만. glTF/GLB(glTFast 패키지 필요)·.blend(Blender 필요)는 쓰지 않는다. 내장 렌더 파이프라인 유지 | Unity 기본 가져오기만으로 CI에서 검증 가능, 패키지 추가 없음 (§14.1) |
| A2-16 | `.meta`는 GUID만 가진 최소 메타(gen_meta.py), 가져오기 설정은 `Editor/ArtImportRules.cs`(`AssetPostprocessor`)가 매 가져오기마다 강제 | 에디터 없이 설정을 코드 리뷰로 관리, Unity 버전별 메타 형식에 의존하지 않음 (§14.3·14.4) |
| A2-17 | 애니메이션은 Legacy(`Animation` 컴포넌트). 재생은 `AnimationState.speed = 0` + `time`을 `dt`로 진행 + `Animation.Sample()` | AnimatorController 에셋 불필요, A2-5(프레임 시간과 무관한 결정적 진행) 유지. Playables는 그래프 생명주기 코드가 더 많아 기각 (§14.6) |
| A2-18 | 머티리얼은 모델 안에 묻어 두고(추출 금지) "Standard"·metallic 0·smoothness ≤ 0.2로 통일. 런타임은 모델 셰이더를 `Shader.Find`하지 않는다 | 추출된 .mat은 CI에서 새 GUID로 생겨 커밋되지 않음. 한 가지 무광 스타일 (§14.4·14.5) |
| A2-19 | 로드 경로 = `Art/<Set>/Resources/<Set>/<Name>.<ext>` → `Resources.Load("<Set>/<Name>")`. 크기는 런타임이 렌더러 경계로 Core 상자에 맞춘다 | 세트 단위로 출처·크기·삭제 관리, 키 충돌 없음. 원본 배율이 키트마다 달라도 게임 규칙 수치(Core) 불변 (§14.2·14.7) |

## 13. 구현 작업 분해 (제안, Orchestrator 확정)

경로는 `Games/Origami/Unity/Assets/Game/` 기준. 모든 작업의 `.meta`는 해당 폴더 범위에 포함. 공통 금지: `Scripts/Bootstrap.cs`, asmdef, `Editor/`, `Unity/Packages/`, `Unity/ProjectSettings/`, `Docs/`, 다른 작업의 폴더.

| ID (제안) | 제목 | 담당 | file_scope | 의존 | FEATURE_SPECS AC | verify |
|---|---|---|---|---|---|---|
| ORIGAMI-009 | v2 Core 접기 엔진·자석·판정·카탈로그 | developer | `Core/Geometry/`, `Core/Folding/`, `Core/Recognition/`, `Core/Catalog/`, `Tests/EditMode/Core/Folding/`, `Tests/EditMode/Core/Recognition/`, `Tests/EditMode/Core/Catalog/` | ORIGAMI-008 | F-005 AC-01~06·AC-10~16(엔진 수준: `FoldSession`/`FoldPlanner`/`DropSnap`), F-005 AC-18의 Core 부분(`FoldGesture` 겹 수·"겹: 위 2 / 6" 값), F-009 AC-01~06, F-007 AC-01(`BookLayout`), CONTENT §1 표·§2 순서(선 끝점 ±1e-4, 자석 종류)·§4 작품끼리 표·§5 인기 표 | core-tests, review |
| ORIGAMI-010 | v2 Core 공방 규칙·경제·튜토리얼 | developer | `Core/Economy/`, `Core/Tutorial/`, `Core/Game/`, `Tests/EditMode/Core/Economy/`, `Tests/EditMode/Core/Tutorial/`, `Tests/EditMode/Core/Game/` | ORIGAMI-009 | F-001 AC-01~09, F-003 AC-01~04, F-004 AC-01~06, F-005 AC-01~05·07~10(명령 수준), F-006 AC-01~08, F-007 AC-04~05, F-008 AC-01~02·04~05, F-010 AC-01~05, F-011 AC-01~06, F-012 AC-01~08 | core-tests, review |
| ORIGAMI-011 | v2 Core 책 도해·문구·화면 흐름 | developer | `Core/Book/`, `Core/Texts/`, `Core/Screens/`, `Tests/EditMode/Core/Book/`, `Tests/EditMode/Core/Texts/`, `Tests/EditMode/Core/Screens/` | ORIGAMI-010 | F-007 AC-02~03, F-008 AC-03, F-013 AC-01~02, F-003 키 표(모드×키, Core), F-010 결과 문구(R-012), F-011 AC-07·F-012 AC-09의 문구 데이터, F-006 안내 문구·F-013 1회성 안내 상태 | core-tests, review |
| ORIGAMI-012 | v2 Unity 접기 테이블 (종이 메시·끌기·겹 고르기·자석·애니메이션) | developer | `Scripts/Systems/`, `Scripts/Folding/`, `Tests/PlayMode/FoldTable/` | ORIGAMI-009 (010·011과 병렬 가능) | F-005 AC-17·AC-18, F-006 AC-09(가이드 표시: host가 가이드를 줄 때만), F-005 AC-19는 사용자 플레이테스트 기록 | unity-compile, unity-editmode, unity-playmode, review |
| ORIGAMI-013 | v2 Unity 책 화면 (두 쪽 펼침·도해 텍스처·목차·사기) | developer | `Scripts/Book/`, `Tests/PlayMode/Book/` | ORIGAMI-011 (012와 병렬 가능) | F-007 AC-06의 책 그리기 부분, F-008 사기 버튼 연결(콜백), F-007 AC-07은 사용자 플레이테스트 기록 | unity-compile, unity-editmode, unity-playmode, review |
| ORIGAMI-014 | v2 Unity 공방 통합 (방·캐릭터·카메라·HUD·덮개·판매대) + v1 삭제 | developer | `Core/Room/`, `Tests/EditMode/Core/Room/`, `Core/GameInfo.cs`, `Scripts/Gameplay/`, `Scripts/UI/`(`KoreanFont.cs`는 수정 금지), `Tests/PlayMode/Workshop/`; **삭제 전용**: `Core/Content/`, `Core/Rules/`, `Core/Run/`, `Core/Flow/`, `Tests/EditMode/Core/Contracts/`, `Tests/EditMode/Core/Flow/`, `Tests/EditMode/Core/Rules/`, `Tests/EditMode/Core/Run/`, `Tests/PlayMode/Flow/`, §10의 v1 Qa 파일 6개 | ORIGAMI-011, ORIGAMI-012, ORIGAMI-013 | F-002 AC-01~06(수치는 Core `RoomMotion` dotnet + PlayMode 연결), F-003 AC-05~07, F-007 AC-06, F-010 AC-06, F-011 AC-07, F-012 AC-09, F-013 AC-03, 전체 연결 스모크(1일 플레이: 앉기 → 튜토리얼 6단계 → 완성 → 잠 → 정산) | core-tests, unity-compile, unity-editmode, unity-playmode, unity-build, review |

순서와 병렬:

```
008 ─▶ 009 ─┬─▶ 010 ─▶ 011 ─┬─▶ 013 ─┐
            └─▶ 012 ────────┼────────┼─▶ 014
                            └────────┘
```

- 009가 끝나면 010과 012를 병렬(worktree)로, 011이 끝나면 013을 012와 병렬로 돌릴 수 있다(file_scope가 겹치지 않고 012는 Core 규칙을 참조하지 않음 — A2-7).
- 각 작업은 자기 폴더에만 쓴다. 다른 작업의 public 표면이 바뀌어야 하면 구현하지 말고 Orchestrator에 계약 변경을 요청한다(architect가 INTERFACE_CONTRACTS 갱신).
- 테스트 작성 주의(리뷰 W-6, QA O-1): 강아지 턱 없이(0.849/0.848) 결과는 어떤 테스트도 단언하지 않는다. F-006 AC-04의 `Unfold`는 먼저 `Fold` 1회를 한 뒤 검사한다(빈 기록의 `Unfold`는 R-007대로 거부).
- 콘텐츠 문구 다듬기(책 설명·엔딩 문구)가 필요하면 나중에 content-designer 작업으로 `Core/Catalog/` 문자열만 배정한다(이번 분해에는 넣지 않음).

## 14. 외부 3D 에셋 파이프라인 (ORIGAMI-017)

목표: 아무도 Unity 에디터를 열지 않는 상태에서 CC0 모델·텍스처·애니메이션을 저장소에 넣고, CI(GameCI, Unity 6000.3.29f1)의 가져오기 결과가
항상 같게 한다. 규칙은 `CODING_CONVENTION.md` "아트 에셋"에 요약한다. 결정 A2-15~A2-19(§12).

### 14.1 형식

| 종류 | 허용 형식 | 비고 |
|---|---|---|
| 모델(정적) | `.fbx`(바이너리·ASCII), `.obj`(+`.mtl`) | Kenney·Quaternius 키트가 FBX/OBJ를 같이 준다. FBX 우선(재질 색 포함) |
| 모델(애니메이션) | `.fbx` 한 파일에 메시+뼈대+클립(take) | 같은 FBX 안의 클립만 쓴다(Legacy는 다른 뼈대로 옮기기(retarget) 불가) |
| 텍스처 | `.png`(기본), `.jpg` | 1024 px 이하로 가져옴(§14.4). 손으로 줄여 넣을 때는 256~1024 |
| 금지 | `.glb/.gltf`(glTFast 패키지 필요), `.blend`(CI에 Blender 없음), `.max/.ma/.mb` | 원본이 glTF뿐이면 다른 에셋을 고른다. 꼭 필요하면 패키지 도입은 별도 architect 작업 |

렌더 파이프라인은 내장(Built-in) 그대로. 새 패키지·ProjectSettings 변경 없음.

### 14.2 폴더와 Resources 키

```
Assets/Game/Art/<Set>/                       세트 하나 = 출처 하나(키트 하나). content-designer 소유
  CREDITS.md                                 (선택) 세트별 출처 — 게임 전체 목록은 Art/CREDITS.md
  Resources/<Set>/<Name>.fbx|.png            런타임이 읽는 파일만. 빌드에 전부 들어감
```

- 런타임 로드 키 = `"<Set>/<Name>"`(확장자 없음): `Resources.Load<GameObject>("Room/Chair")`, `Resources.Load<Texture2D>("Room/Floor")`.
  세트 이름을 키 앞에 붙이므로 Resources 폴더가 여러 개여도 키가 겹치지 않는다. `<Set>`·`<Name>`은 PascalCase ASCII(공백·`|` 없음).
- `Resources/` 밖(`Art/<Set>/` 바로 아래)에 둔 파일은 빌드에 안 들어간다 — 원본 라이선스 문서·미사용 원본은 저장소에 넣지 않는 것이 원칙(크기 예산), 넣더라도 `Resources/` 밖.
- 키 문자열은 Runtime 한 곳(예: `Scripts/Systems/ArtKeys.cs`, ORIGAMI-019 developer 소유)에 상수로 둔다. 다른 코드에 리터럴 키를 쓰지 않는다.
- 샘플: `Art/_PipelineSample/Resources/_PipelineSample/{Chair.fbx, CasualMale.fbx, PaperSwatch.png}` (017, 출처 `Art/_PipelineSample/CREDITS.md`).
  샘플도 빌드에 들어간다(≈1.6 MB). 실제 에셋이 들어온 뒤 지울지는 ORIGAMI-019가 정한다(지우면 `Tests/PlayMode/AssetPipeline/`도 같이 — 그 전까지 이 테스트가 파이프라인 회귀 검사).

### 14.3 .meta

- `gen_meta.py`는 모델(`.fbx .obj .dae .3ds .dxf`)·텍스처(`.png .jpg .jpeg .tga .psd .tif .tiff .bmp .gif .exr .hdr`)·오디오(`.wav .ogg .mp3 .aif .aiff`)·글꼴(`.ttf .otf`)에
  **GUID만 있는 최소 메타**(`fileFormatVersion: 2` + `guid:`)를 쓴다. Unity는 확장자로 가져오기 도구(ModelImporter/TextureImporter/…)를 고르고 기본값을 채운다.
  (DefaultImporter 메타를 쓰면 모델·텍스처로 가져와지지 않는다 — 017 이전 gen_meta의 문제.)
- CI의 Unity가 메타를 기본값으로 다시 쓰는 것은 정상이다(작업 공간 안에서만, GUID 유지, 커밋되지 않음). 설정의 정본은 메타가 아니라 `ArtImportRules`다.
- 기존 메타는 절대 고치지 않는다. 같은 에셋을 다시 넣을 때도 기존 `.meta`를 그대로 둔다(GUID 유지).

### 14.4 가져오기 설정 — `Editor/ArtImportRules.cs` (`AssetPostprocessor`, `Assets/Game/` 아래 전부)

| 대상 | 설정 | 이유 |
|---|---|---|
| 모델 | `useFileScale = true`, `globalScale = 1` | 파일 단위 → 미터. 최종 크기는 런타임 맞춤(§14.7) |
| 모델 | 카메라·조명·가시성 가져오지 않음, `addCollider = false`, `isReadable = false`, 메시 압축 끔 | 3D physics 모듈 없음(§7), 메모리 |
| 모델 | `materialImportMode = ImportViaMaterialDescription`, `materialLocation = InPrefab` | 머티리얼을 모델의 하위 에셋으로 묻음. 추출하면 CI에서 새 `.mat`+새 GUID가 생기고 커밋되지 않는다 |
| 모델 | `animationType = Legacy`, `importAnimation = true` | `Animation` 컴포넌트가 모델 루트에 붙고 클립을 바로 재생. AnimatorController 에셋 불필요 |
| 클립 | 이름의 `"…|"` 접두사 제거(`CharacterArmature|Walk` → `Walk`). 이름이 겹치면 원래 이름 유지. `clipAnimations`를 직접 정한 모델은 건드리지 않음 | 키트마다 다른 뼈대 이름과 무관하게 `"Walk"`로 찾음 |
| 머티리얼 (모델 가져오기에서 생긴 것) | 셰이더 "Standard"(없으면 "Legacy Shaders/Diffuse"), 색·주 텍스처 유지, `_Metallic` 0, `_Glossiness` ≤ 0.2 | 한 가지 무광 "아늑한 로우폴리" 모습 |
| 텍스처 | Default 형식(파일 이름이 `_Normal`/`_NormalGL`/`_nrm`로 끝나면 NormalMap), sRGB, 밉맵, 최대 1024, 압축, Bilinear, Repeat | 빌드·메모리 예산, 타일링 바닥·벽 |

- 규칙을 바꾸면 `ArtImportRules.Version`을 올린다(Unity가 해당 에셋을 다시 가져옴).
- 가져오기 설정을 메타에 손으로 쓰지 않는다. 특정 에셋만 다른 설정이 필요하면 `ArtImportRules`에 경로 규칙을 추가하는 architect 작업으로 한다.

### 14.5 셰이더와 빌드

- 모델 머티리얼은 "Standard"를 참조한다. Resources 폴더의 모델이 빌드에 들어가므로 Standard도 **그 머티리얼이 쓰는 변형만** 들어간다.
  Always Included Shaders에 Standard를 넣지 않는다(모든 변형을 컴파일해 CI 빌드 시간·크기가 크게 는다). ProjectSettings 변경 없음.
- 그래서 런타임은 모델 셰이더를 `Shader.Find("Standard")`로 찾지 않는다. 색을 바꿀 때는 `MaterialPropertyBlock`(`_Color`)을 쓰고,
  새 머티리얼이 꼭 필요하면 `new Material(renderer.sharedMaterial)`(복사, 만든 쪽이 `OnDestroy`에서 파괴).
  코드로 만드는 종이·오버레이 머티리얼은 지금처럼 `Materials.Lit/Unlit`(Always Included인 "Legacy Shaders/Diffuse"·"Sprites/Default").
- 셰이더 변형 자동 제거(Automatic)는 빌드 씬(빈 씬)을 기준으로 한다. **`RenderSettings.fog`를 켜지 않는다**(안개 변형이 제거되어 빌드에서만 다르게 보임). 실시간 방향광·그림자 변형은 남는다.

### 14.6 애니메이션 (Legacy)

- 모델 루트의 `Animation` 컴포넌트, 클립 이름은 짧은 이름(`Idle`, `Walk`, `SitDown`, `StandUp`, … — 키트의 take 이름).
- **재생은 `OrigamiApp.ProcessFrame` 경로에서 결정적으로**(A2-5): 생성 시 `anim.playAutomatically = false; anim.Stop();`, 재생할 상태는
  `state.enabled = true; state.weight = 1; state.speed = 0;` 그리고 `Tick(dt)`마다 `state.time += dt`(반복 클립은 `state.length`로 나머지) → `anim.Sample()`.
  엔진이 매 프레임 같은 시간으로 평가하므로 이중 진행이 없다. 두 클립 섞기는 두 상태의 `weight`를 코드로 보간한다.
  테스트는 `state.time`을 정해 `Sample()` 한 뒤 뼈 Transform을 읽는다(017 `AssetPipelineTests`).
- `AnimationClip.SampleAnimation(go, t)`도 같은 결과를 준다(단순 포즈 지정용).
- Humanoid/Generic + Animator는 쓰지 않는다(AnimatorController 에셋이 필요하고 에디터 없이 만들기 어렵다). Playables(`AnimationClipPlayable`)는 Generic 클립에서
  가능하지만 그래프 생성·파괴 코드가 늘고 이점이 없어 기각.

### 14.7 런타임 로드·크기 맞춤·소유

- `var prefab = Resources.Load<GameObject>(key); var go = Object.Instantiate(prefab, parent);` — 로드한 프리팹·공유 머티리얼·메시는 절대 수정·파괴하지 않는다
  (에디터에서는 에셋 자체가 바뀐다). 인스턴스는 만든 컴포넌트가 `OnDestroy`에서 `Destroy`.
- 키트마다 원본 배율·원점이 다르다(예: Kenney 의자는 원점이 모서리, 크기 2×4.7×2 단위). 그래서 **런타임이 렌더러 경계로 맞춘다**:
  단위 변환 없이 놓은 뒤 `Renderer.bounds`를 합쳐, Core 상자(`RoomLayout`)의 가로·깊이(또는 캐릭터는 키)에 맞는 균일 배율을 구하고, 바닥 가운데를 상자 바닥 가운데에 맞춘다.
  모양 그대로의 충돌·상호작용은 계속 Core 상자 수학(A2-6) — 모델은 보이는 것만 바꾼다. 맞춤 도우미는 019(developer)가 `Scripts/Systems/`에 만든다.
- 모델에는 Collider가 없다(`addCollider = false`). 캐릭터 방향: 키트의 앞쪽이 +z가 아니면 맞춤 도우미가 회전 보정 상수를 둔다(세트별 상수, ArtKeys 옆).

### 14.8 라이선스·출처·크기·LFS

- **CC0만.** 출처 페이지에서 라이선스를 확인한다(페이지의 라이선스 표시 또는 같이 받은 License.txt). CC-BY·CC-BY-SA·"무료"·라이선스 불명은 쓰지 않는다.
- `Art/CREDITS.md`(content-designer)에 파일마다 한 줄: 저장소 파일, 원본 파일·팩 이름, 작성자, 출처 페이지 URL, 라이선스, 받은 경로(URL), 바이트 크기, 바꾼 점.
- **예산**: 저장소에 더하는 아트 합계 ≤ 30 MB(DECISIONS 2026-11-28). 파일 하나 ≤ 5 MB, 텍스처 ≤ 1024 px, 캐릭터 FBX ≤ 3 MB 권장.
  확인: `git lfs ls-files -s -I 'Games/Origami/Unity/Assets/Game/Art/**'`(LFS 파일별 크기) + LFS가 아닌 파일은 `git ls-files -z 'Games/Origami/Unity/Assets/Game/Art/*' | xargs -0 du -cb | tail -1`(작업 트리에 LFS 포인터만 있으면 포인터 크기로 셈).
- 바이너리는 Git LFS(`.gitattributes`의 `*.fbx *.png *.jpg *.psd *.wav *.mp3 *.ogg`)로 저장된다. 이 작업 환경에는 LFS pre-push 훅이 없으므로
  **Orchestrator는 `git push` 전에 `git lfs push origin <branch>`** 로 LFS 객체를 올린다(안 올리면 CI의 `lfs: true` 체크아웃이 실패). 확인: `git lfs ls-files`.
  `.obj`·`.tga`는 LFS 대상이 아니다 → 1 MB 넘는 OBJ/TGA는 쓰지 말고 FBX/PNG로 넣는다.
- 받는 경로(2026-11 확인): kenney.nl·github.com 직접 접근은 막힘. opengameart.org(Kenney·Quaternius 팩 다수, `sites/default/files/<zip>` 직접 받기),
  ambientcg.com(`/get?file=<Id>_1K-JPG.zip`, API `api/v2/full_json`), polyhaven(api.polyhaven.com), cdn.jsdelivr.net(공개 GitHub 저장소·npm 파일)은 됨.

### 14.9 검증 범위

| 무엇 | 어떻게 |
|---|---|
| gen_meta 최소 메타, 기존 메타 불변 | `python3 -m pytest studio/tests -q` |
| 모델·텍스처가 Resources에서 로드되고 메시·머티리얼(Standard, 무광, 묻힌 하위 에셋)이 있음 | CI PlayMode `Tests/PlayMode/AssetPipeline/` |
| Legacy 클립 존재·짧은 이름·두 시간에서 뼈가 움직임(`Animation.Sample`, `SampleAnimation`) | 같은 테스트 |
| Windows 빌드에 Standard가 들어감 | CI `unity-build` 성공(빌드는 셰이더 누락으로 실패하지 않으므로, 보이는 모습은 사용자 플레이테스트로 확인) |
| 보기 좋음·스타일 일관성 | 사용자 플레이테스트(019) |
