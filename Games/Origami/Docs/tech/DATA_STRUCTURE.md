# Origami 데이터 구조

> 담당: architect · 기준 태스크: ORIGAMI-002. 타입 시그니처는 `INTERFACE_CONTRACTS.md`가 정본.

## 콘텐츠 정의 방식 (결정 A-1)

- 작품·일차별 인기 작품·엔딩은 **Core의 코드 카탈로그** `Unity/Assets/Game/Core/Content/OrigamiCatalog.cs`(정적 불변 데이터)로 정의한다.
- JSON 파일(`Assets/Game/Data/`)과 ScriptableObject는 쓰지 않는다. 이유: Core(netstandard2.1)에 패키지 없이 쓸 JSON 파서가 없고, `JsonUtility`는 UnityEngine이라 dotnet 테스트가 불가능하다. 데이터가 작다(5 + 7 + 5행).
- 정본은 여전히 `Docs/design/CONTENT_SPECS.md`. 값 변경은 CONTENT_SPECS 수정(game-designer) → 카탈로그 수정 → `Tests/EditMode/Core/Contracts/CatalogContractTests.cs` 갱신 순서. 카탈로그 계약 테스트가 표와 값을 1:1 대조한다.
- 표시 문자열 중 작품 이름·엔딩 이름·엔딩 문구는 카탈로그에, 그 밖의 UI 문구는 `Core/Flow/FlowTexts.cs`(C-021)에 둔다.

### D-001 작품 (`ModelDefinition`, `OrigamiCatalog.Models`)

| 필드 | 타입 | 의미 | 값 (plane / boat / frog / crane / dragon) |
|---|---|---|---|
| `Order` | int | 목록 순서(1-based) | 1 / 2 / 3 / 4 / 5 |
| `Id` | string | 고정 ID (`ModelIds`) | `plane` / `boat` / `frog` / `crane` / `dragon` |
| `DisplayName` | string | 한국어 이름 | 종이비행기 / 종이배 / 개구리 / 학 / 용 |
| `Steps` | `IReadOnlyList<FoldAction>` | 정답 순서 | U L R / D U F U / L R F D D U / U L F R D F L U / R R U F L D F U L D |
| `StepCount` | int | = `Steps.Count` | 3 / 4 / 6 / 8 / 10 |
| `PaperCost` | int | 종이값 | 1 / 1 / 2 / 3 / 6 |
| `BasePrice` | int | 기본가 | 4 / 6 / 10 / 18 / 40 |
| `UnlockPrerequisiteId` | string | 선행 작품(완성 3개로 해금), null = 처음부터 | null / null / boat / frog / crane |

### D-002 일차별 인기 작품 (`OrigamiCatalog.PopularByDay`, `PopularModelId(day)`)

| 일차 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| 인기 작품 ID | plane | boat | frog | boat | crane | frog | dragon |

### D-003 엔딩 (`EndingDefinition`, `OrigamiCatalog.Endings`)

| Priority | Id | DisplayName | 조건 (코드: `EndingRules.Evaluate`, C-009) |
|---|---|---|---|
| 1 | E-01 | 빈손 | 파산 또는 최종 돈 ≤ 20 |
| 2 | E-02 | 학의 소원 | 학 완성 합계 ≥ 15 |
| 3 | E-03 | 종이 재벌 | 최종 돈 ≥ 1000 |
| 4 | E-04 | 종이접기 명인 | 5종 모두 ★3 ≥ 1 |
| 5 | E-05 | 동네 종이접기 가게 | 기본 |

`Text`는 CONTENT_SPECS의 결과 문구 그대로. 조건은 데이터가 아니라 코드(C-009)다 — 조건 종류가 5개뿐이고 각기 달라 데이터화 이득이 없다.

## 런 상태 모델 (`OrigamiRun` 내부, 스냅샷으로만 노출)

| 개념 | 스냅샷 필드 | 타입 | 시작값 (R-002) |
|---|---|---|---|
| 일차 | `RunSnapshot.Day` | int 1..7 | 1 |
| 돈 | `Money` | int ≥ 0 | 20 |
| 남은 시도 | `AttemptsLeft` | int 0..5 | 5 |
| 오늘의 인기 작품 | `PopularModelId` | string | plane |
| 해금 작품 | `UnlockedModelIds` | 카탈로그 순서 목록 (= `Unlocking.IsUnlocked` 결과) | plane, boat |
| 완성 수 | `Completions` | `CompletionCounts` (5 작품 × 3 등급) | Empty |
| 실패 수 | `FailureCount` | int (망침 + 포기) | 0 |
| 총 판매액 / 총 종이값 | `TotalSales` / `TotalPaperCost` | int | 0 / 0 |
| 오늘 완성 작품 | `TodayPieces` | `IReadOnlyList<CompletedPiece>` (완성 순) | 비어 있음 |
| 진행 중 접기 | `Fold` | `FoldSnapshot` 또는 null | null |
| 종료 사유 | `EndReason` | `RunEndReason` | None |
| 엔딩 | `EndingId` | string 또는 null | null |

진행 중 접기(`FoldSnapshot`): 모델 ID, 전체 단계 N, 진행 i(= 맞힌 단계 수), 맞힌 단계 기호, 실수(0..2), 힌트 수, 현재 단계 공개 기호(없으면 0), 벌점, 예상 등급.

## 수치 표현

- 돈·가격·개수는 모두 `int`. 가격 계산은 정수 곱셈 후 `/ 10000`(내림). 최대값(이론 최대 돈 1709, 최대 곱 40×150×150 = 900000)은 int 범위 안.
- 등급은 `Grade`(값 = 별 수), 동작은 `FoldAction`(1..5, 0 = 무효).

## 저장 데이터

- 없음(이번 버전 범위 밖). 추가 시 JSON, ARCHITECTURE.md §7.
