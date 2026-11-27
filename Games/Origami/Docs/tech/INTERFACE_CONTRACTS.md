# Origami 인터페이스 계약

> 담당: architect (ORIGAMI-002). 구현자는 변경이 필요하면 Orchestrator에 변경 요청.
> public 시그니처는 고정이다. 구현자는 private 필드·헬퍼를 자유롭게 추가할 수 있지만 public 표면을 바꾸거나 늘리려면 계약 변경이 필요하다.
> 표면은 `Tests/EditMode/Core/Contracts/SurfaceContractTests.cs`가 리플렉션으로 고정한다.

## 계약 목록

| ID | 이름 | 위치 (`Unity/Assets/Game/…`) | 상태 (ORIGAMI-002 시점) | 구현 |
|---|---|---|---|---|
| C-001 | `FoldAction`, `FoldActions`, `Grade` | `Core/Content/FoldAction.cs`, `FoldActions.cs`, `Grade.cs` | 완료 | — |
| C-002 | `ModelDefinition`, `EndingDefinition` | `Core/Content/ModelDefinition.cs`, `EndingDefinition.cs` | 완료 | — |
| C-003 | `OrigamiCatalog`, `ModelIds`, `EndingIds` | `Core/Content/OrigamiCatalog.cs`, `ModelIds.cs`, `EndingIds.cs` | 완료 (데이터 = CONTENT_SPECS) | — |
| C-004 | `RuleConstants` | `Core/Rules/RuleConstants.cs` | 완료 | — |
| C-005 | `CompletionCounts` | `Core/Rules/CompletionCounts.cs` | 완료 | — |
| C-006 | `Grading` | `Core/Rules/Grading.cs` | 골격 | Core 규칙 작업 |
| C-007 | `Pricing` | `Core/Rules/Pricing.cs` | 골격 | Core 규칙 작업 |
| C-008 | `Unlocking` | `Core/Rules/Unlocking.cs` | 골격 | Core 규칙 작업 |
| C-009 | `EndingRules`, `RunEndReason` | `Core/Rules/EndingRules.cs`, `RunEndReason.cs` | 골격 (enum 완료) | Core 규칙 작업 |
| C-010 | `OrigamiRun` | `Core/Run/OrigamiRun.cs` | 골격 | Core 규칙 작업 |
| C-011 | 결과·스냅샷 타입 | `Core/Run/RejectReason.cs`, `CommandOutcome.cs`, `CommandResult.cs`, `CompletedPiece.cs`, `SaleLine.cs`, `SettlementResult.cs`, `FoldSnapshot.cs`, `RunSnapshot.cs` | 완료 | — |
| C-020 | `OrigamiFlow`, `FlowScreen`, `FlowKeys` | `Core/Flow/OrigamiFlow.cs`, `FlowScreen.cs`, `FlowKeys.cs` | 골격 (enum 완료) | Core 화면 흐름 작업 |
| C-021 | `FlowTexts` | `Core/Flow/FlowTexts.cs` | 골격 | Core 화면 흐름 작업 |
| C-030 | `OrigamiController` | `Scripts/Gameplay/OrigamiController.cs` | 문서 계약만 | Unity 작업 |
| C-031 | `OrigamiView`, `ModelIcons` | `Scripts/UI/OrigamiView.cs`, `Scripts/UI/ModelIcons.cs` | 문서 계약만 | Unity 작업 |

공통(Core): C# 9 / netstandard2.1, `UnityEngine` 비참조, 시계·난수·정적 가변 상태 금지(R-001). 정수 연산만(돈·가격은 `int`).
**예외 정책**: 플레이어 명령(`OrigamiRun`, `OrigamiFlow`의 명령 메서드)은 규칙 위반으로 예외를 던지지 않는다(R-015, 거부로 반환).
순수 함수(`Grading`·`Pricing`·`Unlocking`·`EndingRules`·`CompletionCounts.With`)의 **잘못된 인자**(프로그래머 오류)는 아래 명시대로 `ArgumentException` 계열을 던진다.

---

## C-001 동작·등급

```csharp
namespace Origami.Core.Content
{
    public enum FoldAction { U = 1, D = 2, L = 3, R = 4, F = 5 }   // 0 없음: default는 무효
    public static class FoldActions
    {
        public static readonly IReadOnlyList<FoldAction> All;     // U, D, L, R, F
        public static bool IsValid(FoldAction action);            // U..F만 true
        public static string Symbol(FoldAction action);           // "U".."F", 무효 "?"
    }
    public enum Grade { Star1 = 1, Star2 = 2, Star3 = 3 }          // 값 = 별 수, 0 = 등급 없음
}
```

- 정의되지 않은 `FoldAction` 값(0, 6, 음수, 캐스팅 값)은 InvalidAction 대상(R-005). Runtime은 `FoldAction`을 정의된 멤버로만 만든다. 테스트는 `(FoldAction)0`, `(FoldAction)99` 등으로 InvalidAction을 검증한다.

## C-002 / C-003 콘텐츠 정의와 카탈로그

```csharp
namespace Origami.Core.Content
{
    public sealed class ModelDefinition
    {
        public ModelDefinition(int order, string id, string displayName, FoldAction[] steps,
                               int paperCost, int basePrice, string unlockPrerequisiteId);
        public int Order { get; }                       // 1..5
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<FoldAction> Steps { get; } // 읽기 전용 복사본
        public int StepCount { get; }
        public int PaperCost { get; }
        public int BasePrice { get; }
        public string UnlockPrerequisiteId { get; }     // null = 처음부터 해금
        public bool IsInitiallyUnlocked { get; }
    }
    public sealed class EndingDefinition
    {
        public EndingDefinition(string id, int priority, string displayName, string text);
        public string Id { get; } public int Priority { get; } public string DisplayName { get; } public string Text { get; }
    }
    public static class ModelIds  { "plane", "boat", "frog", "crane", "dragon" 상수 Plane..Dragon }
    public static class EndingIds { "E-01".."E-05" 상수 EmptyHanded, CraneWish, Tycoon, Master, LocalShop }
    public static class OrigamiCatalog
    {
        public static readonly IReadOnlyList<ModelDefinition> Models;    // 카탈로그 순서
        public static readonly IReadOnlyList<string> PopularByDay;       // [0] = 1일차
        public static readonly IReadOnlyList<EndingDefinition> Endings;  // 우선순위 순
        public static ModelDefinition FindModel(string id);   // null/""/미지 → null. Ordinal, 대소문자 구분
        public static int IndexOf(string id);                 // 0-based, 미지 → -1
        public static string PopularModelId(int day);         // 1..7, 그 밖 → null
        public static EndingDefinition FindEnding(string id); // 미지 → null
    }
}
```

데이터 값은 DATA_STRUCTURE.md D-001~D-003. 규칙 코드는 작품 값(종이값·기본가·순서·선행 작품)을 하드코딩하지 않고 카탈로그에서 읽는다.

## C-004 `RuleConstants`

`Origami.Core.Rules.RuleConstants`의 `const int`: `TotalDays 7`, `StartMoney 20`, `DailyAttempts 5`, `MistakeLimit 3`, `MistakePenalty 2`, `HintPenalty 1`,
`PopularPct 150`, `NormalPct 100`, `UnlockCount 3`, `Star2MaxPenalty 2`, `GradePctStar3 150`, `GradePctStar2 100`, `GradePctStar1 60`, `PriceDivisor 10000`,
`EmptyHandedMoney 20`, `TycoonMoney 1000`, `CraneCount 15`. 규칙 수치는 이 상수만 쓴다.

## C-005 `CompletionCounts` (완료)

```csharp
public sealed class CompletionCounts : IEquatable<CompletionCounts>
{
    public static readonly CompletionCounts Empty;
    public int Get(string modelId, Grade grade);       // 미지 ID/null/무효 등급 → 0, 예외 없음
    public int Total(string modelId);                  // 등급 합 (R-010 "등급 무관")
    public CompletionCounts With(string modelId, Grade grade, int count);  // 새 객체. 미지 ID·무효 등급 → ArgumentException, count<0 → ArgumentOutOfRangeException
    public CompletionCounts Increment(string modelId, Grade grade);        // With(…, Get+1)
    // Equals/GetHashCode: 15칸 값 비교
}
```

불변. 엔딩 판정 테스트 입력 예: `CompletionCounts.Empty.With("crane", Grade.Star1, 15)`.

## C-006 `Grading` (R-009)

| 멤버 | 동작 |
|---|---|
| `static int Penalty(int mistakes, int hints)` | `mistakes × MistakePenalty + hints × HintPenalty`. 음수 인자 → `ArgumentOutOfRangeException`. |
| `static Grade GradeFor(int penalty)` | 0 → Star3, 1..2 → Star2, ≥3 → Star1. 음수 → `ArgumentOutOfRangeException`. |
| `static int GradePct(Grade grade)` | Star3 150, Star2 100, Star1 60. 무효 등급 → `ArgumentOutOfRangeException`. |

## C-007 `Pricing` (R-011)

| 멤버 | 동작 |
|---|---|
| `static int DemandPct(bool popular)` | popular → 150, 아니면 100. |
| `static int SalePrice(int basePrice, Grade grade, bool popular)` | `basePrice * GradePct(grade) * DemandPct(popular) / PriceDivisor` (정수 나눗셈 = floor, 값은 모두 ≥ 0). |
| `static int SalePrice(string modelId, Grade grade, int day)` | 카탈로그 기본가, `popular = (PopularModelId(day) == modelId)`. 미지 모델 → `ArgumentException`, day ∉ 1..7 → `ArgumentOutOfRangeException`. |

CONTENT_SPECS 판매가 표 30개 값과 일치해야 한다(F-005 AC-04). 작업대의 "오늘 ★3 예상가" = `SalePrice(id, Star3, snapshot.Day)`.

## C-008 `Unlocking` (R-010)

| 멤버 | 동작 |
|---|---|
| `static bool IsUnlocked(string modelId, CompletionCounts counts)` | 선행 없음 → true. 아니면 `counts.Total(prereq) >= UnlockCount`. null/미지 ID → false. counts null → `ArgumentNullException`. |
| `static int Progress(string modelId, CompletionCounts counts)` | `min(Total(prereq), UnlockCount)`; 처음부터 해금 → `UnlockCount`; null/미지 → 0. counts null → `ArgumentNullException`. |

런 안에서 완성 수는 줄지 않으므로 해금은 완성 수의 순수 함수다(별도 해금 집합 상태 없음). 완성 직후 다음 명령부터 즉시 반영된다.

## C-009 `EndingRules` (R-014, F-006)

```csharp
public enum RunEndReason { None = 0, Completed = 1, Bankrupt = 2 }     // Origami.Core.Rules
public static string EndingRules.Evaluate(RunEndReason reason, int finalMoney, CompletionCounts counts);
```

위에서부터 첫 만족: E-01 `reason == Bankrupt || finalMoney <= 20` → E-02 `counts.Total("crane") >= 15` → E-03 `finalMoney >= 1000`
→ E-04 카탈로그 5종 모두 `counts.Get(id, Star3) >= 1` → E-05. 반환값은 `EndingIds` 상수.
`reason == None`(또는 미정의 값) → `ArgumentOutOfRangeException`, counts null → `ArgumentNullException`.
판정은 이 세 입력만 쓴다(F-006 AC-01~09는 `OrigamiRun` 없이 이 함수로 테스트).

## C-010 `OrigamiRun`

```csharp
namespace Origami.Core.Run
{
    public sealed class OrigamiRun
    {
        public OrigamiRun();                       // = NewRun 상태
        public CommandResult NewRun();
        public CommandResult Start(string modelId);
        public CommandResult Fold(FoldAction action);
        public CommandResult Hint();
        public CommandResult Abandon();
        public CommandResult EndDay();
        public RunSnapshot GetSnapshot();          // 매 호출 새 불변 객체
    }
}
```

내부 상태(개념): day, money, attemptsLeft, completions(`CompletionCounts`), failureCount, totalSales, totalPaperCost, todayPieces(목록), fold(모델, progress, mistakes, hints, revealed) 또는 없음, endReason, endingId.

**거부 규칙**: 아래 순서로 검사해 첫 사유로 `CommandResult.Rejected(reason)` 반환, 상태 전혀 불변(R-015).

| 명령 | 검사 순서 | 수락 시 동작 | Outcome |
|---|---|---|---|
| `NewRun()` | 없음(항상 수락, 런 종료 후 포함) | R-002 상태로 초기화: day 1, money 20, attempts 5, completions Empty, failure 0, totalSales 0, totalPaperCost 0, today 비움, fold 없음, endReason None, endingId null. 진행 중 접기·오늘 작품 폐기, 엔딩 판정 없음. | `RunStarted` |
| `Start(id)` | RunEnded → FoldInProgress → UnknownModel(null/""/미지) → Locked → NoAttemptsLeft(attempts 0) → NotEnoughMoney(money < 종이값) | money −= 종이값, attempts −= 1, totalPaperCost += 종이값, fold = (id, progress 0, mistakes 0, hints 0, 공개 없음). | `FoldStarted` |
| `Fold(a)` | RunEnded → NoFold → InvalidAction(`!FoldActions.IsValid(a)`) | 정답(`a == Steps[progress]`): progress+1, 공개 해제. progress == N이면 완성: grade = `GradeFor(Penalty(mistakes, hints))`, today에 `(id, grade)` 추가, completions `Increment`, fold 없음 → `CommandResult.Completed(piece)`. 오답: mistakes+1(공개 유지). mistakes == 3이면 망침: failure+1, fold 없음. | `StepCorrect` / `FoldCompleted` / `StepMistake` / `FoldRuined` |
| `Hint()` | RunEnded → NoFold | 현재 단계가 아직 공개 안 됐으면 hints+1, 공개 = `Steps[progress]`. 이미 공개면 변화 없음(그래도 수락). | `HintRevealed` |
| `Abandon()` | RunEnded → NoFold | failure+1, fold 없음(환불 없음). | `FoldAbandoned` |
| `EndDay()` | RunEnded → FoldInProgress | 정산(아래) 후 day==7 → endReason Completed. 아니면 day+1, attempts 5, 그 뒤 `money < (해금된 작품 중 최소 종이값)`(= money 0)이면 endReason Bankrupt. 종료 시 endingId = `EndingRules.Evaluate(endReason, money, completions)`. | 종료 → `RunFinished`, 아니면 `DayEnded` (둘 다 `CommandResult.Settled`) |

**정산**(R-011/R-012): today 순서대로 `SaleLine(id, grade, popular = id == PopularModelId(정산일), price = Pricing.SalePrice(기본가, grade, popular))`.
total = 합, money += total, totalSales += total, today 비움.
`SettlementResult(settledDay = 정산한 일차, lines, total, moneyAfter = 정산 후 돈, endReason, nextPopularModelId = 계속이면 PopularModelId(새 일차), 종료면 null)`.

**불변식**: money ≥ 0, 0 ≤ attemptsLeft ≤ 5, 1 ≤ day ≤ 7, 종료 후 day는 그대로(완주 7, 파산 시 새 날 값), fold 중 mistakes ≤ 2.
**결정성**: 같은 명령 열 → 같은 스냅샷 값(F-001 AC-10). 스냅샷 비교는 필드별 + `CompletionCounts.Equals`.

## C-011 결과·스냅샷 타입 (완료)

```csharp
namespace Origami.Core.Run
{
    public enum RejectReason { None, RunEnded, FoldInProgress, UnknownModel, Locked, NoAttemptsLeft, NotEnoughMoney, NoFold, InvalidAction }
    public enum CommandOutcome { Rejected, RunStarted, FoldStarted, StepCorrect, StepMistake, FoldCompleted, FoldRuined, HintRevealed, FoldAbandoned, DayEnded, RunFinished }
    public readonly struct CommandResult
    {
        public static CommandResult Rejected(RejectReason reason);
        public static CommandResult Accepted(CommandOutcome outcome);
        public static CommandResult Completed(CompletedPiece piece);                 // FoldCompleted
        public static CommandResult Settled(CommandOutcome outcome, SettlementResult s); // DayEnded / RunFinished
        public bool IsAccepted { get; }  public RejectReason Reason { get; }  public CommandOutcome Outcome { get; }
        public CompletedPiece Piece { get; }  public SettlementResult Settlement { get; }
    }
    public readonly struct CompletedPiece : IEquatable<CompletedPiece> { ModelId; Grade; ==, !=; ToString "boat ★3" }
    public readonly struct SaleLine { ModelId; Grade; bool Popular; int Price }
    public sealed class SettlementResult { SettledDay; IReadOnlyList<SaleLine> Lines; Total; MoneyAfter; RunEndReason EndReason; bool RunEnded; string NextPopularModelId }
    public sealed class FoldSnapshot { ModelId; StepCount; Progress; IReadOnlyList<FoldAction> CorrectSteps; Mistakes; Hints;
                                       bool IsCurrentStepRevealed; FoldAction RevealedAction; Penalty; Grade ExpectedGrade }
    public sealed class RunSnapshot  { Day; Money; AttemptsLeft; PopularModelId; IReadOnlyList<string> UnlockedModelIds; CompletionCounts Completions;
                                       FailureCount; TotalSales; TotalPaperCost; IReadOnlyList<CompletedPiece> TodayPieces; FoldSnapshot Fold; bool IsFolding;
                                       RunEndReason EndReason; bool IsRunEnded; string EndingId; bool IsUnlocked(string id) }
}
```

- 생성자 시그니처는 소스 파일이 정본. 목록 인자는 복사되어 읽기 전용으로 보관된다.
- `FoldSnapshot`은 남은 정답을 노출하지 않는다(맞힌 단계와 힌트로 공개된 현재 단계만). `RevealedAction`은 미공개 시 `default`(0). `ExpectedGrade = GradeFor(Penalty)`.
- `RunSnapshot.UnlockedModelIds`는 카탈로그 순서, `PopularModelId`는 현재 일차의 인기 작품. 접는 중이 아니면 `Fold == null`.
- `default(CommandResult)`는 `Rejected`/`None`으로 읽힌다(명령 결과로 반환되지 않음).

## C-020 `OrigamiFlow` (F-007 화면 상태 기계)

```csharp
namespace Origami.Core.Flow
{
    public enum FlowScreen { Workbench, Preview, Folding, AbandonConfirm, Notice, Settlement, Result }
    [Flags] public enum FlowKeys { None = 0, FoldUp = 1, FoldDown = 2, FoldLeft = 4, FoldRight = 8, Flip = 16, Hint = 32, Escape = 64, Enter = 128 }
    public sealed class OrigamiFlow
    {
        public OrigamiFlow();                             // 새 OrigamiRun, Workbench, Message ""
        public FlowScreen Screen { get; }
        public RunSnapshot Snapshot { get; }              // 매 Core 호출 뒤 갱신
        public string SelectedModelId { get; }            // Preview/Folding/AbandonConfirm/Notice에서 대상 작품, 그 외 null
        public CommandResult LastResult { get; }          // 마지막 Core 명령 결과
        public SettlementResult Settlement { get; }       // Settlement 화면에서만 non-null
        public string Message { get; }                    // 거부 안내 1줄, 없으면 ""
        public bool SelectModel(string modelId);
        public bool ClosePreview();
        public bool StartSelected();
        public bool Fold(FoldAction action);
        public bool Hint();
        public bool RequestAbandon();
        public bool ConfirmAbandon();
        public bool CancelAbandon();
        public bool EndDay();
        public bool Acknowledge();
        public bool Restart();
        public bool ProcessKeys(FlowKeys keys);
    }
}
```

**반환값**: 현재 화면에서 유효한 메서드면 `true`(Core가 거부해도 `true`, 사유는 `Message`), 화면에 맞지 않으면 아무것도 하지 않고 `false`.
**Message**: Core 거부 또는 Flow 차단(아래) 시 `FlowTexts.Rejection(reason, 대상 모델, Snapshot.Completions)`. 수락된 명령이나 화면 전환 시 `""`로 지움.

| 화면 | 메서드 | 동작 → 다음 화면 |
|---|---|---|
| Workbench | `SelectModel(id)` | 미지/null → Message(UnknownModel), 머묾. 잠김(`!Snapshot.IsUnlocked`) → Message(Locked, 해금 조건), 머묾, Core 호출 없음. 해금 → `SelectedModelId = id`, **Preview**. (시도·돈 부족은 여기서 막지 않음 — 접기 시작 때 Core가 거부) |
| Workbench | `EndDay()` | Core `EndDay`. 수락 → `Settlement = result.Settlement`, **Settlement**. 거부 → Message. |
| Preview | `StartSelected()` | Core `Start(SelectedModelId)`. 수락 → **Folding**. 거부(NoAttemptsLeft, NotEnoughMoney 등) → Message, 머묾. |
| Preview | `ClosePreview()` | **Workbench**, `SelectedModelId = null`. |
| Folding | `Fold(a)` | Core `Fold(a)`. `FoldCompleted`/`FoldRuined` → **Notice**. 그 외 수락 → 머묾. 거부(InvalidAction) → Message, 머묾. |
| Folding | `Hint()` | Core `Hint()`, 머묾. |
| Folding | `RequestAbandon()` | **AbandonConfirm** (Core 호출 없음, 상태 불변). |
| AbandonConfirm | `ConfirmAbandon()` | Core `Abandon()` → **Notice**. |
| AbandonConfirm | `CancelAbandon()` | **Folding** (상태 불변). |
| Notice | `Acknowledge()` | **Workbench**, `SelectedModelId = null`. |
| Settlement | `Acknowledge()` | `Settlement.RunEnded` → **Result**, 아니면 **Workbench**. `Settlement = null`. |
| Result | `Restart()` | Core `NewRun()` → **Workbench** (F-001 AC-01 상태). |

다른 조합은 모두 `false`(무시). 특히 AbandonConfirm에서 `Fold`/`Hint`는 무시(F-007), Folding 중 `SelectModel`/`EndDay`는 무시(F-007 "접는 중에는 작업대 동작 안 함").
Notice 화면이 보여 줄 내용은 `LastResult`(FoldCompleted → `Piece`, FoldRuined, FoldAbandoned)와 `SelectedModelId`.

**`ProcessKeys(keys)`**: `keys`에서 우선순위 `Escape > Enter > Hint > FoldUp > FoldDown > FoldLeft > FoldRight > Flip`의 첫 키 **하나만** 골라 아래 표대로 해당 메서드를 호출하고 그 반환값을 돌려준다. 표에 없는 조합·`None` → `false`.

| 키 | Folding | AbandonConfirm | Preview | Notice | Settlement | Workbench / Result |
|---|---|---|---|---|---|---|
| FoldUp/FoldDown/FoldLeft/FoldRight/Flip | `Fold(U/D/L/R/F)` | — | — | — | — | — |
| Hint | `Hint()` | — | — | — | — | — |
| Escape | `RequestAbandon()` | `CancelAbandon()` | `ClosePreview()` | — | — | — |
| Enter | — | `ConfirmAbandon()` | `StartSelected()` | `Acknowledge()` | `Acknowledge()` | — |

F-007 AC-08 순서 예: Start 후(Folding) `Escape` → AbandonConfirm, `Escape` → Folding, `Escape` → AbandonConfirm, `Enter` → Abandon → Notice. AbandonConfirm에서 `FoldUp` → `false`, 상태 불변.

## C-021 `FlowTexts` (표시 문자열)

모두 순수 정적 함수, `CultureInfo.InvariantCulture`, 천 단위 구분 없음. 아래 **굵은** 형식은 테스트 대상(고정), 나머지 줄 구성은 구현 재량이되 아래 내용을 모두 포함해야 한다.

| 멤버 | 반환 |
|---|---|
| `const string BankruptNotice` | **`"돈이 없어 가게 문을 닫았습니다"`** |
| `DayText(day)` | **`"{day}/7"`** |
| `AttemptsText(k)` | **`"{k}/5"`** |
| `MoneyText(m)` | **`"{m}원"`** |
| `Stars(grade)` | **Star3 `"★★★"`, Star2 `"★★☆"`, Star1 `"★☆☆"`**, 무효 `""` |
| `ActionGlyph(a)` | **U `"↑"`, D `"↓"`, L `"←"`, R `"→"`, F `"F"`**, 무효 `"?"` |
| `ActionName(a)` | **`"위로 접기"`, `"아래로 접기"`, `"왼쪽으로 접기"`, `"오른쪽으로 접기"`, `"뒤집기"`**, 무효 `""` |
| `UnlockCondition(model, counts)` | **`"{선행 이름} 3개 완성 시 ({Unlocking.Progress}/3)"`** 예: `"종이배 3개 완성 시 (1/3)"`. 처음부터 해금 → `""`. model null → `""`. |
| `Rejection(reason, model, counts)` | 아래 표 |
| `ScreenLines(flow)` | 현재 화면의 텍스트 줄(버튼 제외), 위→아래 |

**거부 문구**: None `""` · RunEnded **`"런이 끝났습니다"`** · FoldInProgress **`"접는 중에는 할 수 없습니다"`** · UnknownModel **`"없는 작품입니다"`** ·
Locked **`UnlockCondition(model, counts)`** · NoAttemptsLeft **`"오늘은 더 접을 수 없습니다"`** · NotEnoughMoney **`"돈이 부족합니다"`** ·
NoFold **`"접는 중이 아닙니다"`** · InvalidAction **`"알 수 없는 동작입니다"`**.

**`ScreenLines` 내용** (각 항목 = 1줄 이상, 표기 `{…}`는 위 함수 결과):

| 화면 | 줄 |
|---|---|
| Workbench | **`"일차 {DayText}   돈 {MoneyText}   시도 {AttemptsText}"`** · **`"오늘의 인기 작품: {인기 이름}"`** · 작품마다 1줄: 해금 `"{순서}. {이름}  {N}단계  종이 {MoneyText(종이값)}  오늘 ★3 {MoneyText(예상가)}"`, 잠김 `"{순서}. {이름}  잠김 - {UnlockCondition}"` · `"오늘 완성: {이름 Stars, …}"` 또는 `"오늘 완성: 없음"` · Message(비어 있지 않으면) |
| Preview | `"{이름} 도안"` · 정답 순서 `ActionGlyph`를 공백으로 이은 줄(예 `"↑ ← →"`) · `"종이값 {MoneyText}"` · Message |
| Folding | `"{이름} 접는 중"` · **`"진행 {i}/{N}"`** · `"맞힌 단계: {glyph …}"`(없으면 `"-"`) · **`"실수 {m}/3"`** · `"힌트 {h}"` · `"예상 등급 {Stars}"` · 공개 시 `"힌트: {glyph} {ActionName}"` · Message. **남은 단계 기호는 절대 포함하지 않는다.** |
| AbandonConfirm | Folding 줄 전부 + **`"정말 포기할까요? (Enter: 포기한다 / Esc: 계속 접기)"`** |
| Notice | 완성 **`"{이름} 완성! {Stars}"`** · 망침 **`"종이를 망쳤다"`** · 포기 **`"접기를 포기했다"`** |
| Settlement | `"{정산일}일차 정산"` · 판매 줄마다 `"{이름} {Stars}{인기면 " (인기)"} {MoneyText(가격)}"` · **`"합계 {MoneyText}"`** · **`"정산 후 돈 {MoneyText}"`** · 계속이면 `"내일의 인기 작품: {이름}"`, 파산이면 `BankruptNotice`, 완주면 없음 |
| Result | **`"{엔딩 이름}"`** · 엔딩 문구 · **`"최종 돈 {MoneyText}"`** · 작품마다 **`"{이름} ★3 {a} / ★2 {b} / ★1 {c}"`** · **`"실패 {n}회"`** · `"총 판매액 {MoneyText}"` · `"총 종이값 {MoneyText}"` |

## C-030 `OrigamiController` (Unity, `Origami.Runtime.Gameplay`)

```csharp
public sealed class OrigamiController : MonoBehaviour
{
    public const string ObjectName = "[Origami]";
    public static OrigamiController Instance { get; }     // EnsureInstance가 만든 것, 없거나 파괴 → 진짜 null
    public static OrigamiController EnsureInstance();      // 멱등: Instance → Find(ObjectName) → 새 GameObject(DontDestroyOnLoad)
    public OrigamiFlow Flow { get; }                       // Awake에서 new (필드 초기화도 가능), 교체 없음
    public bool ProcessFrame(FlowKeys keys);               // = Flow.ProcessKeys(keys)
    public static FlowKeys ReadKeys();                     // 레거시 Input.GetKeyDown → FlowKeys (아래 표)
}
```

- `Update()`는 `ProcessFrame(ReadKeys())`만 한다. 테스트는 `ProcessFrame`/`Flow` 메서드를 직접 호출한다(프레임·시간 무관).
- `ReadKeys` 매핑: `UpArrow|W → FoldUp`, `DownArrow|S → FoldDown`, `LeftArrow|A → FoldLeft`, `RightArrow|D → FoldRight`, `Space → Flip`, `H → Hint`, `Escape → Escape`, `Return|KeypadEnter → Enter`. `GetKeyDown`만 사용(누르고 있기 반복 없음).
- `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Init → EnsureInstance()`. 수동 `AddComponent`한 테스트용 컨트롤러는 `Instance`를 대체하지 않는다. `OnDestroy`에서 자신이면 해제.

## C-031 `OrigamiView`, `ModelIcons` (Unity, `Origami.Runtime.UI`)

```csharp
public sealed class OrigamiView : MonoBehaviour
{
    public OrigamiController Source { get; }               // 진짜 null 정규화
    public IReadOnlyList<string> Lines { get; }             // 마지막 Refresh의 FlowTexts.ScreenLines(Source.Flow); 초기 빈 목록
    public void Bind(OrigamiController controller);         // Source 설정 + Refresh()
    public void Refresh();                                  // Lines 갱신 (Source null이면 유지)
}
public static class ModelIcons
{
    public static Texture2D Get(string modelId);            // 모델별 코드 생성 64×64 텍스처(캐시), 미지 ID → null
    public static Texture2D Paper { get; }                  // 접는 중 종이(단색 정사각형)
}
```

- `LateUpdate` → `Refresh()`. `Awake`에서 같은 GameObject의 컨트롤러를 Source로 잡음. `Init` 훅: `EnsureInstance()` 후 뷰가 없으면 추가하고 `Bind`.
- `OnGUI`: `Lines`를 그리고 화면별 버튼(ARCHITECTURE.md §5 표)을 그린다. 버튼 클릭은 **해당 `Flow` 메서드 호출만** 한다(키와 같은 메서드). 규칙 판단·문자열 조립 금지(문자열은 `FlowTexts`).
- 버튼 레이블: 동작 버튼은 `FlowTexts.ActionGlyph + " " + ActionName`, 그 외 "하루 마치기", "접기 시작 ({MoneyText})", "닫기", "힌트", "포기", "포기한다", "계속 접기", "확인", "다시 시작", 작품 버튼은 표시 이름.
- 도형(CONTENT_SPECS 권장): plane 하늘색 삼각형, boat 파란 사다리꼴, frog 초록 원, crane 흰 마름모+빨간 점, dragon 빨간 별. Notice(완성) 화면과 작업대 목록에 사용. Folding 화면은 `Paper` + 진행 막대(i/N).
- 글꼴: ARCHITECTURE.md 결정 A-5. `GUIStyle`/글꼴은 `OnGUI`에서 지연 생성.

## 변경 이력

| 날짜 | 계약 | 변경 | 작업 |
|---|---|---|---|
| 2026-11-27 | C-001~C-031 | 최초 정의 | ORIGAMI-002 |
