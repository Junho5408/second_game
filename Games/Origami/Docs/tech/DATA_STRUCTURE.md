# Origami 데이터 구조 (v2)

> 담당: architect · 기준 태스크: ORIGAMI-008. 이전 판(ORIGAMI-002, v1 작품·런 상태)은 **대체**되었다.
> 타입 시그니처의 정본은 `INTERFACE_CONTRACTS.md`(C-1xx). 수치의 정본은 `Docs/design/CONTENT_SPECS.md`와 `GAME_RULES.md`이고, 이 문서는 그것을 코드 데이터로 옮기는 방법을 정한다.

## 0. 데이터 정의 방식 (v1 결정 A-1 유지)

- 작품·기준 접기 순서·책 설명·인기 작품표·엔딩·방 배치는 **Core의 코드 정의**(`static readonly`, 읽기 전용 컬렉션)다. JSON(`Assets/Game/Data/`)·ScriptableObject는 쓰지 않는다.
  이유: Core(netstandard2.1)에 패키지 없이 쓸 JSON 파서가 없고, 코드 정의면 dotnet 테스트가 실제 데이터로 돈다. 데이터가 작다(5작품, 7일, 5엔딩, 가구 8개).
- **기준 상태·도해 그림·판정 기준 윤곽은 하드코딩하지 않는다.** 모두 기준 접기 순서(D-111)를 Core 엔진에 적용해 계산한다(F-009 "하드코딩 윤곽 아님").
- 값 변경 순서: CONTENT_SPECS·`balance_check.py` 수정(game-designer) → 카탈로그 코드 수정 → 카탈로그 테스트(`Tests/EditMode/Core/Catalog/`) 갱신. 카탈로그 테스트가 CONTENT 표와 1:1 대조한다.

## 1. 좌표와 단위

| 공간 | 단위 | 축 | 쓰는 곳 |
|---|---|---|---|
| 종이 좌표 (paper) | 1 = 10cm, 처음 종이 [−1, 1]² | x 오른쪽, y 앉은 사람에게서 먼 쪽. 펼친 종이 위의 점(자국 `CreaseSegment`, `Facet.Paper`) | Core 전체 |
| 테이블 좌표 (world of paper) | 종이 좌표와 같은 단위 | 같은 축. 접힌 조각의 현재 위치(`Facet.World`), grab/drop, 자석, 윤곽 | Core 전체, `FoldTable` |
| 방 좌표 | 미터, 바닥 (x, z) | x 동쪽, z 북쪽(카메라에서 먼 쪽), Unity y 위 | `Core/Room`, Runtime |
| 테이블 → 월드 | `FoldTable` 루트 = 테이블 윗면 중심 (2.5, 0.75, 1.6), 배율 0.1 | 종이 (x, y) → 로컬 (x, 겹 높이, y) → 월드 (2.5 + 0.1x, 0.75 + …, 1.6 + 0.1y) | Runtime |

- 좌표·넓이는 Core에서 `double`, Unity로 넘길 때만 `float`로 바꾼다. Core에 `float` 금지.

## D-101 종이 상태 `PaperState`

| 필드 | 의미 |
|---|---|
| `Facets` | 평평한 **볼록** 조각 목록. 조각 = (World 꼭짓점들, Paper 꼭짓점들 — 같은 인덱스, Layer, FrontUp). 엔진은 World를 항상 반시계로 만든다 |
| `Creases` | 접은 자국 = **원래 종이 좌표**의 선분 목록(`CreaseSegment(A, B)`). 종이에 붙어 있어서 이후 접기·뒤집기 때 따로 옮기지 않는다. 화면 위치는 조각마다 `PaperToWorld`로 계산(C-106 `MarkPieces`) |

- 처음 종이 `PaperState.Initial`: 조각 1개, World = Paper = (−1,−1), (1,−1), (1,1), (−1,1), Layer 0, FrontUp = false(**흰 면 위**, 색 면이 아래), 자국 0.
- 조각 목록의 **순서도 상태의 일부**다(Python과 같은 순서를 유지해야 겹 동점·자석 기준점 중복 제거가 같아진다): 접기 결과 = 남는 조각(원래 순서) 뒤에 넘어간 조각(원래 순서). 뒤집기·접었다 펴기는 순서 유지.
- 예: `Fold((−1,1),(−1,−1))` 뒤 조각 2개(남은 아래 절반 Layer 0, 넘어온 위 절반 Layer 1 FrontUp true), 윤곽 [−1,1]×[−1,0](넓이 2.0), 조각 넓이 합 4.0.
- 예(F-005 AC-11): 이어서 `Crease((−1,0),(1,0))` → 조각 수 2 그대로, 자국 (0,−1)–(0,0), (0,1)–(0,0) 두 선분(합 2.0), `CreaseCount` 1.

### D-102 겹 (Layer)

- `Layer`는 조각 전체의 쌓임 순서(0 = 테이블에 닿은 맨 아래). 접기·뒤집기 뒤 항상 0..k로 빈틈없이 다시 매긴다. 엔진 규칙상 서로 다른 조각은 서로 다른 Layer를 갖는다(같은 조각에서 갈라진 움직임·남음 부분은 한쪽이 넘어가며 새 Layer를 받음).
- "그 점의 겹 수 M" = `StackCountAt(p)`(그 점을 경계 포함 1e-6으로 포함하는 조각 수). 위 N겹의 "위에서부터"는 Layer 내림차순.
- 판정(R-011)은 Layer·FrontUp·자국을 쓰지 않는다(윤곽만). Runtime 그림과 자석 가시성만 Layer를 쓴다.

### D-103 기준 순서의 상태 수 (참조 구현 출력 — 엔진 동등성 테스트 기대값)

`balance_check.py` 엔진으로 각 작품 기준 순서를 한 동작씩 적용했을 때 (조각 수, 자국 선분 수, 접은 횟수):

| 작품 | 단계별 (조각, 자국 선분, 접기) | 최종 조각 넓이 합 |
|---|---|---|
| plane | (1,1,0) (2,1,1) (3,1,2) (5,1,3) (7,1,4) (8,1,5) | 4.0 |
| house | (2,0,1) (2,2,1) (2,4,1) (4,4,2) (6,4,3) | 4.0 |
| tulip | (2,0,1) (4,0,2) (6,0,3) (8,0,4) (10,0,5) | 4.0 |
| dog | (2,0,1) (2,2,1) (2,4,1) (4,4,2) (6,4,3) (8,4,4) | 4.0 |
| cicada | (2,0,1) (4,0,2) (6,0,3) (6,4,3) (6,10,3) (8,10,4) (10,10,5) (12,10,6) (20,10,7) (28,10,8) | 4.0 |

- "자국 수"(R-007, `CreaseCount`)는 `Crease` 동작 수다(위 표의 자국 **선분** 수와 다르다: house 2단계 뒤 선분 2개 = 자국 1).
- 009는 이 표와 FEATURE_SPECS F-005 AC-12~15의 조각·겹·넓이를 정확히(개수는 같게, 넓이 ±1e-6) 재현해야 한다. 더 자세한 기대값(각 조각 꼭짓점)이 필요하면 `balance_check.py`의 `apply`/`fold`로 계산해 테스트에 넣는다(참조 구현이므로 허용).

### D-104 동작 `FoldStep`과 되돌리기 기록

| 종류 | 필드 | 뜻 |
|---|---|---|
| `Fold` | Grab, Drop, Mountain, Layers | `Fold(grab, drop, mountain, layers)`, Layers 0 = 전체 겹 |
| `Crease` | Grab, Drop, Layers | `Crease(grab, drop, layers)` |
| `Flip` | — | 뒤집기 |

- `FoldSession` 기록 = 이 종이에 적용된 `FoldStep` 목록(+ 각 직전 상태). 종이를 꺼낼 때·`Unfold` 뒤 비어 있다. `AppliedSteps`를 처음 종이에 `ApplyAll`하면 현재 상태와 같다(결정성 — 저장 D-170의 근거).

### D-105 윤곽 `Outline`

- 행 k(y = (k + 0.5)·0.001)마다 서로 겹치지 않는 정렬된 구간 [lo, hi] 목록. 넓이·무게중심·교집합은 이 표현에서 계산(C-110).
- `RecognitionCatalog`는 작품마다 "무게중심 정렬 → D4 g0..g7 꼭짓점 변환 → 주사"한 윤곽 8개를 갖는다(5 × 8 = 40개, 윤곽마다 행 수 = 세로 길이 ÷ 0.001).

## D-110 작품 (`ModelDefinition`, `ModelCatalog.Models`) — CONTENT_SPECS §1

| Order | Id | DisplayName | BasePrice | PagePrice | InitiallyOwned | Difficulty | ActionCount (Fold + Crease) | Techniques | CoverRotationDeg | 기준 윤곽 넓이 (±0.01) |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `plane` | 종이비행기 | 6 | 0 | true | 2 | 6 (5 + 1) | 자국, 자국 자석 | 0 | 0.845 |
| 2 | `house` | 집 | 8 | 0 | true | 1 | 5 (3 + 2) | 자국 2개로 지붕 선 맞춤 | 0 | 1.500 |
| 3 | `tulip` | 튤립 | 12 | 20 | false | 2 | 5 (5 + 0) | 위 2겹 접기(꽃잎) | 45 | 1.488 |
| 4 | `dog` | 강아지 얼굴 | 18 | 50 | false | 3 | 6 (4 + 2) | 자국 교차점(턱) | 0 | 1.477 |
| 5 | `cicada` | 매미 | 26 | 150 | false | 4 | 10 (8 + 2) | 자국 2개(대각선·세로), 위 2겹(날개), 산접기 | 45 | 0.690 |

- `Techniques` 문구는 CONTENT §1 "쓰는 기법" 열을 줄인 표시용 문자열이다(content-designer가 다듬을 수 있음 — 테스트는 비어 있지 않음만 검사).

## D-111 기준 접기 순서 (`ModelDefinition.Steps`, `StepTexts`) — CONTENT_SPECS §2

표기: F(g, d[, 산][, 위 n겹]) = `FoldStep.Fold`, C(g, d) = `FoldStep.Crease`. C = (−1, −1). 기본은 골접기·전체 겹.
**k = 1 − √2, x₀ = 2·tan(π/8)은 코드에서 계산한 double**(반올림 금지). 튤립·매미의 소수 6자리 놓는 점은 **리터럴 그대로**.

| 작품 | Steps |
|---|---|
| plane | C((−1,1),(1,1)) · F((−1,1),(0,0)) · F((1,1),(0,0)) · F((−1,0),(0,k)) · F((1,0),(0,k)) · F((−x₀,−1),(x₀,−1)) |
| house | F((−1,1),(−1,−1)) · C((−1,0),(1,0)) · C((−1,−1),(−1,0)) · F((−1,0),(−0.6,−0.8)) · F((1,0),(0.6,−0.8)) |
| tulip | F((1,1),C) · F((−1,1),C) · F((1,−1),C) · F(C,(0.689655,−0.275862), 위 2겹) · F(C,(−0.275862,0.689655), 위 2겹) |
| dog | F((1,1),C) · C((−1,1),(1,−1)) · C(C,(0,0)) · F((−1,1),(−1.4,0.0)) · F((1,−1),(0.0,−1.4)) · F(C,(−0.5,−0.5)) |
| cicada | F((1,1),C) · F((−1,1),C) · F((1,−1),C) · C((−1,0),(0,−1)) · C((−1,0),(0,0)) · F(C,(0.453858,−0.108926), 위 2겹) · F(C,(−0.108926,0.453858), 위 2겹) · F(C,(−0.38,−0.38)) · F((−1,0),(−0.5,−0.5), 산) · F((0,−1),(−0.5,−0.5), 산) |

- `StepTexts[i]` = CONTENT §2 각 표의 "책 설명" 열 문장 그대로(한국어).
- 카탈로그 테스트(009): 각 단계의 `LineOnPaper`가 CONTENT §2 "선"과 ±1e-4, 각 단계의 기준 입력에 대한 `DropSnap.Snap` 종류가 "자석" 열(P/L/M/−)과 같고 접는 선이 바뀌지 않음(±1e-5, `balance_check.py recog`의 "자석" 줄).

## D-112 책 쪽 배치 (`BookLayout`) — R-010, CONTENT §3

- 쪽 = [0 목차][1 접기 기호] + 작품마다 카탈로그 순서로 [표지, 1단계 … N단계, 완성]. N = ActionCount.

| 작품 | 표지 | 단계 쪽 | 완성 |
|---|---|---|---|
| plane | 2 | 3~8 | 9 |
| house | 10 | 11~15 | 16 |
| tulip | 17 | 18~22 | 23 |
| dog | 24 | 25~30 | 31 |
| cicada | 32 | 33~42 | 43 |

- 총 44쪽. 단계 n의 쪽 = 표지 + n, 완성 = 표지 + N + 1. 펼침 = (짝수, 홀수). `ClampToSpread(p)`: q = clamp(p, 0, 43), 결과 q − q mod 2: −1 → 0, 17 → 16, 31 → 30, 43 → 42, 99 → 42.
- 책의 "현재 쪽"은 게임 상태(`GameSnapshot.BookPage`, 항상 짝수, `NewRun` 시 0, 닫았다 열어도 유지). 쪽 **내용**(`BookPage`)은 상태가 아니라 `BookPages.Get(쪽, 보유)`로 매번 계산한다(Runtime이 캐시).

## D-113 인기 작품 (`ModelCatalog.PopularByDay`) — CONTENT §5

| 일차 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| 인기 | plane | house | tulip | house | dog | tulip | cicada |

판매가 표(R-015, 30칸)는 데이터가 아니라 `Pricing` 공식의 결과다. 테스트(010)가 CONTENT §5 표 30칸과 대조한다.

## D-114 엔딩 (`EndingCatalog.Endings`) — CONTENT §8

| Priority | Id | DisplayName | Text |
|---|---|---|---|
| 1 | E-01 | 빈손 | 종이는 다 떨어졌지만, 종이접기는 취미로 남았다. |
| 2 | E-02 | 튤립 꽃밭 | 공방 창가가 종이 튤립으로 가득 찼다. 사람들이 꽃밭 공방이라 부르기 시작했다. |
| 3 | E-03 | 종이접기 명인 | 책 없이도 손이 먼저 움직인다. 사람들은 당신을 명인이라 부른다. |
| 4 | E-04 | 종이 재벌 | 종이 한 장으로 시작해 거리의 명물 공방이 되었다. |
| 5 | E-05 | 동네 종이접기 공방 | 소박하지만 단골이 생긴 동네 공방. 다음 주도 문을 연다. |

조건은 데이터가 아니라 코드(`EndingRules.Evaluate`, C-130)다.

## D-115 튜토리얼 가이드 (`PlaneGuide.Steps`) — R-008, F-006

- plane 기준 순서 6단계에서 만든다: Number n, Kind = Steps[n−1].Kind, Grab/Drop = Steps[n−1], LineA/LineB = `LineOnPaper(StateBeforeStep(plane, n), Grab, Drop)`, Text = StepTexts[n−1], EitherSide = (n == 1 ∨ n == 6).
- 예: 1단계 Crease (−1,1)→(1,1), 선 (0,−1)–(0,1). 2단계 Fold (−1,1)→(0,0).

## D-120 게임 상태 (`OrigamiGame` 내부, `GameSnapshot`으로만 노출)

| 개념 | 스냅샷 필드 | 타입 | 시작값 (R-002) |
|---|---|---|---|
| 일차 | `Day` | int 1..7 | 1 |
| 돈 | `Money` | int ≥ 0 | 20 |
| 오늘 남은 종이 | `SheetsLeft` | int 0..5 | 5 |
| 모드 | `Mode` | `WorkshopMode` | Walking |
| 책 | `BookOpen` / `BookVisible` / `BookPage` | bool / bool / int(짝수) | false / false / 0 |
| 보유 페이지 | `OwnedModelIds` | 카탈로그 순서 목록 | plane, house |
| 첫 종이 무료 남음 | `FreeSheetAvailable` | bool | true |
| 튜토리얼 끝 | `TutorialFinished` | bool | false |
| 앉은 적 있음 | `HasSat` | bool | false |
| 테이블 위 종이 | `Sheet` | `SheetSnapshot` 또는 null | null |
| 오늘 진열 | `Display` | `DisplayedPiece` 목록(완성 순, 최대 5) | 비어 있음 |
| 완성 수 | `Completions` | `CompletionCounts` (5 × ★1~★3) | Empty |
| 실패 수 | `FailureCount` | int (구겨진 종이 + 버린 종이) | 0 |
| 총 판매액 / 총 종이값 / 총 책값 | `TotalSales` / `TotalPaperCost` / `TotalBookCost` | int | 0 / 0 / 0 |
| 오늘의 인기 작품 | `PopularModelId` | string | plane |
| 종료 | `EndReason` / `EndingId` / `IsEnded` | `RunEndReason` / string / bool | None / null / false |

- 불변식: `Money = 20 + TotalSales − TotalPaperCost − TotalBookCost`(R-020), `Money ≥ 0`.
- 스냅샷은 불변이고 수락된 명령마다 새로 만든다. 거부된 명령 뒤에는 **같은 객체**다(테스트: `Assert.AreSame` 또는 필드 비교).

### D-121 테이블 위 종이 (`SheetSnapshot`)

| 필드 | 의미 |
|---|---|
| `State` | 현재 `PaperState` (불변 — Runtime은 참조가 바뀌면 메시를 다시 만든다) |
| `FoldCount` / `CreaseCount` / `ActionCount` / `CanUndo` | `FoldSession` 값 (C-104) |
| `IsTutorial` | 런의 첫 `TakeSheet`로 나온 종이 |
| `GuideVisible` / `Guide` | 가이드 보임(C-150 정의) / 현재 가이드 단계(`PlaneGuide.Steps[ActionCount]`) |
| `GuideScores` | 가이드가 보일 때 한 동작의 단계 점수(0~100). `Undo`로 마지막 것이 지워지고 `Unfold`로 모두 지워진다 |

### D-122 진열 작품 (`DisplayedPiece`)

- (ModelId, Grade ★1~★3, Score s, Percent = floor(s·100 + 1e-9), Shape = 완성 순간의 `PaperState`). 판매대는 Shape를 그린다(기준 상태가 아니라 플레이어가 접은 모양, CONTENT §6).

### D-123 정산 (`SettlementResult`, `SaleLine`)

- `SaleLine(ModelId, Grade, Popular, Price)` × 진열 순, `Total`, `MoneyAfter`, `SettledDay`, `EndReason`, `NextPopularModelId`(계속이면 다음 날 인기, 종료면 null).
- 예(F-011 AC-03/06): 1일차 house ★2·★1·★3 → 줄 3개 (8, 4, 12), 합계 24, 정산 후 40.

## D-130 방 배치 (`RoomLayout`) — CONTENT §6, F-002

| Id | 중심 (x, z) | 폭 × 깊이 × 높이 (m) | 바닥 상자 (minX, minZ)–(maxX, maxZ) | 막음 | 상호작용 지점 |
|---|---|---|---|---|---|
| `table` | (2.5, 1.6) | 1.4 × 0.9 × 0.75 | (1.8, 1.15)–(3.2, 2.05) | 예 | (2.5, 0.8) (의자) |
| `chair` | (2.5, 0.8) | 0.5 × 0.5 × 0.45 (높이는 표시값) | — | 아니오(앉는 자리) | — |
| `bookshelf` | (0.0, 2.75) | 1.6 × 0.4 × 2.0 | (−0.8, 2.55)–(0.8, 2.95) | 예 | (0.0, 1.9) |
| `counter` | (2.5, −2.3) | 2.0 × 0.6 × 1.0 | (1.5, −2.6)–(3.5, −2.0) | 예 | (2.5, −1.5) |
| `bed` | (−3.0, 2.0) | 1.0 × 2.0 × 0.5 | (−3.5, 1.0)–(−2.5, 3.0) | 예 | (−2.0, 2.0) |
| `trash` | (3.6, 0.6) | 0.4 × 0.4 × 0.5 | (3.4, 0.4)–(3.8, 0.8) | 예 | — |
| `window` | 북쪽 벽 x = −1.8 | 1.2 × (벽) × 1.0 | — | 아니오(장식) | — |
| `door` | 남서쪽 벽 (−3.0, −3.0) | 1.0 × (벽) × 2.1 | — | 아니오(장식) | — |

- 방 안쪽 x ∈ [−4, 4], z ∈ [−3, 3], 벽 높이 2.5(남쪽 벽은 0.5m 높이만 그림). 캐릭터 반지름 0.3, 키 1.6, 걷기 3.0 m/s, 상호작용 거리 ≤ 1.2.
- 시작·아침 위치 (−1.6, 0.0) 동쪽. 일어서면 (2.5, 0.2) 남쪽. 앉으면 (2.5, 0.8).
- 충돌: 막는 가구의 바닥 상자를 0.3만큼 넓힌 상자와 벽 [−3.7, 3.7] × [−2.7, 2.7]. 기대: (−1.6, 2.0)에서 서쪽 → x = −2.2, (0.0, 1.0)에서 북쪽 → z = 2.25, (−1.6, 0)에서 서쪽 5초 → x = −3.7.
- 판매대 진열 칸 5개: 판매대 윗면(y = 1.0) 위 x = 1.7, 2.1, 2.5, 2.9, 3.3, z = −2.3(Runtime 표시 값, 계약 아님 — content-designer가 ±0.3 조정 가능).

## D-140 수치 표현과 허용 오차

| 항목 | 규칙 |
|---|---|
| 돈·가격·개수 | `int`. 판매가 = 정수 곱셈 후 `/ 10000`(내림). 최대 곱 26 × 150 × 150 = 585,000 |
| 좌표·넓이·일치도 | `double`. 비교 상수: 분할 0 보정 1e-9, 조각 최소 넓이 1e-7, 포함 1e-6, 공선 1e-6, 중복 점 1e-6, 자석 동점 키 = 1e-9 반올림(`DropSnap.Key`) |
| 일치도 % | `Grading.Percent(s)` = floor(s × 100 + 1e-9) |
| 테스트 허용 오차 (FEATURE_SPECS 공통 표기) | 종이 좌표 1e-6, 넓이 1e-6, CONTENT 6자리 리터럴과의 비교 1e-4, 일치도 ±0.01, 월드 위치 ±0.05m(이동 ±0.15m) |
| 판정 경계 근처 | 경계(0.85/0.93/0.97)에서 0.01 이내인 결과는 등급·인정 여부를 단언하지 않는다(리뷰 W-6). J 표 값은 모두 0.02 이상 떨어져 있다(J-05 0.95 ± 0.002 예외, ±0.01이어도 ★2) |

## D-170 저장 데이터 (페이즈 3 예정 — 지금은 구현하지 않음)

- 페이즈 1에는 저장이 없다. 아래는 나중에 추가할 때의 형식이다(architect 작업으로 계약 추가).
- 형식: JSON. Core가 엔진 없는 DTO(`[System.Serializable]`, 공개 필드, 배열)를 만들고 Runtime이 `JsonUtility.ToJson/FromJson`과 `Application.persistentDataPath/origami_save.json`으로 읽고 쓴다.

```text
SaveData (version = 1)
  day, money, sheetsLeft, mode(Walking으로만 저장), bookPage
  ownedModelIds[]                        카탈로그 ID
  freeSheetAvailable, tutorialFinished, hasSat
  completions[]  {modelId, star1, star2, star3}
  failureCount, totalSales, totalPaperCost, totalBookCost
  display[]      {modelId, steps[]}      진열 작품 = 완성까지 적용한 동작 목록 (불러올 때 ApplyAll + Recognize로 등급 재계산)
  sheet          {steps[], isTutorial, guideSkipped} 또는 없음
  step           {kind, gx, gy, dx, dy, mountain, layers}
```

- 종이 모양은 조각 좌표가 아니라 **동작 목록**으로 저장한다(결정성 R-001: 같은 동작 → 같은 상태). 작고, 엔진 내부 표현이 바뀌어도 깨지지 않는다.
- 불러온 값은 `OrigamiGame` 명령 검사와 같은 불변식(D-120)을 확인하고, 어긋나면 새 런으로 시작한다.
