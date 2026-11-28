# Origami 인터페이스 계약 (v2)

> 담당: architect · 기준 태스크: ORIGAMI-008. v1 계약 C-001~C-031(ORIGAMI-002)은 **폐기**되었다(v1 코드는 ORIGAMI-014에서 삭제, ARCHITECTURE §10).
> v2 계약 번호는 C-1xx다. public 시그니처는 고정이다. 구현자는 private/internal 멤버를 자유롭게 추가할 수 있지만
> public 표면을 바꾸거나 늘리려면 계약 변경 요청(Orchestrator → architect)이 필요하다. 각 구현 작업은 자기 계약의 public 표면을
> 리플렉션 테스트(`<모듈>SurfaceTests`)로 고정한다(v1 `SurfaceContractTests`와 같은 방식).
> 시그니처의 `IReadOnlyList<T>`는 읽기 전용 복사본(`ReadOnlyCollection<T>`)이다. "null 가능"이라고 쓰지 않은 참조 반환값은 null이 아니다.

## 계약 목록

| ID | 이름 | 위치 (`Unity/Assets/Game/…`) | 구현 작업 |
|---|---|---|---|
| C-101 | `Vec2` | `Core/Geometry/` | 009 |
| C-102 | `Facet`, `CreaseSegment`, `PaperState` | `Core/Folding/` | 009 |
| C-103 | `FoldLine`, `FoldError`, `FoldPlan`, `FoldPlanner`, `FoldStep`, `FoldStepKind` | `Core/Folding/` | 009 |
| C-104 | `FoldSession` | `Core/Folding/` | 009 |
| C-105 | `LineOnPaper` (도해·가이드 선) | `Core/Folding/` | 009 |
| C-106 | `DropSnap`, `SnapResult`, `SnapKind`, `MarkPiece` (R-021) | `Core/Folding/` | 009 |
| C-107 | `FoldGesture` (잡기·겹 수·놓기) | `Core/Folding/` | 009 |
| C-108 | `FoldTexts` (접기 관련 한국어 문구) | `Core/Folding/` | 009 |
| C-110 | `Outline`, `D4`, `Similarity`, `RecognitionCatalog`, `Recognizer`, `RecognitionResult`, `Grade`, `Grading` | `Core/Recognition/` | 009 |
| C-120 | `ModelIds`, `ModelDefinition`, `ModelCatalog`, `EndingIds`, `EndingDefinition`, `EndingCatalog`, `InteractTargets`, `BookLayout`, `BookPageKind` | `Core/Catalog/` | 009 |
| C-130 | `RuleConstants`, `Pricing`, `CompletionCounts`, `RunEndReason`, `EndingRules` | `Core/Economy/` | 010 |
| C-140 | `GuideStep`, `PlaneGuide`, `StepScore`, `StepScoring` | `Core/Tutorial/` | 010 |
| C-150 | `OrigamiGame` 명령과 거부 순서 | `Core/Game/` | 010 |
| C-151 | `WorkshopMode`, `RejectReason`, `CommandResult`, `SaleLine`, `SettlementResult`, `DisplayedPiece`, `SheetSnapshot`, `GameSnapshot` | `Core/Game/` | 010 |
| C-160 | `BookPages`, `BookPage`, `StepDiagram`, `CoverInfo`, `ContentsEntry` | `Core/Book/` | 011 |
| C-170 | `GameTexts`, `HudData` | `Core/Texts/` | 011 |
| C-171 | `ScreenFlow`, `GameKeys`, `Overlay`, `FlowMessage`, `MessageKind`, `SheetChange`, `SheetChangeKind` | `Core/Screens/` | 011 |
| C-180 | `RoomLayout`, `Box`, `FurnitureSpec`, `RoomMotion`, `Interaction` | `Core/Room/` | 014 |
| C-190 | `Materials`, `MeshFactory` | `Scripts/Systems/` | 012 |
| C-191 | `PointerFrame`, `IFoldTableHost`, `FoldTableGuide`, `FoldTableChangeKind`, `FoldTable`, `PaperMeshBuilder` | `Scripts/Folding/` | 012 |
| C-192 | `IBookHost`, `BookPanel`, `DiagramRaster` | `Scripts/Book/` | 013 |
| C-193 | `InputFrame`, `InputReader`, `OrigamiApp`, `FoldHostAdapter`, `CameraRig`, `CharacterView`, `WorkshopView` | `Scripts/Gameplay/` | 014 |
| C-194 | `OrigamiGui` | `Scripts/UI/` | 014 |

**공통(Core)**: C# 9 / netstandard2.1, `UnityEngine` 비참조, 시계·난수·정적 가변 상태 금지(R-001). 좌표는 `double`. 돈·가격·개수는 `int`.
**예외 정책**: 플레이어 명령(`OrigamiGame`, `ScreenFlow`의 명령 메서드)은 규칙 위반으로 예외를 던지지 않는다(R-019, 거부 반환).
접기 엔진의 무효 접기도 예외가 아니라 `FoldPlan.Valid == false` + `FoldError`다. 순수 함수의 **잘못된 인자**(null, 범위 밖)는 아래에 적은 `ArgumentException` 계열을 던진다.
**참조 구현 동등성**: C-102~C-107은 `Docs/design/balance_check.py`의 같은 이름 함수(`_split`, `plan`, `fold`, `crease`, `flip`, `_hinged`, `_overlap_len`, `_overlap_area`, `mark_pieces`, `visible`, `landmarks`, `snap`, `line_on_paper`)를
**같은 순서·같은 상수·같은 비교 연산자**로 옮긴다. 아래 알고리즘 설명과 Python이 다르면 Python이 정본이다(리뷰가 대조한다).

---

## C-101 `Vec2` (`Origami.Core.Geometry`)

Fold3DSpike `Vec2`를 그대로 복사하고 네임스페이스만 바꾼다.

```csharp
public readonly struct Vec2 : IEquatable<Vec2>
{
    public readonly double X; public readonly double Y;
    public Vec2(double x, double y);
    // + - (단항 -) * (스칼라 양쪽), Length, LengthSq, Normalized((0,0) 유지), Perp (-y, x)
    public static double Dot(Vec2 a, Vec2 b); public static double Cross(Vec2 a, Vec2 b);
    public static double Distance(Vec2 a, Vec2 b); public static Vec2 Lerp(Vec2 a, Vec2 b, double t);
    public static Vec2 ClosestOnSegment(Vec2 p, Vec2 a, Vec2 b);
    // Equals = 비트 단위 X==X && Y==Y, GetHashCode, ToString = InvariantCulture "(x, y)" 소수 3자리
}
```

---

## C-102 종이 상태 (`Origami.Core.Folding`) — 데이터 의미는 DATA_STRUCTURE D-101~D-103

```csharp
public sealed class Facet
{
    public Facet(IEnumerable<Vec2> world, IEnumerable<Vec2> paper, int layer, bool frontUp);
    //   목록을 복사한다. null → ArgumentNullException, 개수 < 3 또는 world/paper 개수 다름 → ArgumentException.
    //   방향은 검사하지 않는다(엔진 출력은 항상 world 반시계, 테스트가 만드는 날것의 상태는 아무 방향 허용 — 판정은 방향 무관).
    public IReadOnlyList<Vec2> World { get; }   // 테이블 위 위치(종이 좌표 단위)
    public IReadOnlyList<Vec2> Paper { get; }   // 같은 인덱스의 펼친 종이 위 점
    public int Layer { get; }                   // 전체 쌓임 순서, 0 = 맨 아래
    public bool FrontUp { get; }                // 색 면(앞면)이 위. 처음 종이는 false(흰 면 위)
    public double SignedArea();                 // world 다각형 부호 넓이(반시계 +)
    public double Area { get; }                 // |SignedArea|
    public bool Contains(Vec2 p, double eps = 1e-6);         // Python Facet.contains: 모든 변에서 Cross(b−a, p−a) ≥ −eps (world 반시계 가정)
    public bool StrictlyContains(Vec2 p, double eps = 1e-6); // Python Facet.strictly: Cross(b−a, p−a)/|b−a| > eps (모든 변)
    public Vec2 PaperToWorld(Vec2 paperPoint);  // Python to_world: 처음 세 꼭짓점의 아핀 사상
}

public readonly struct CreaseSegment : IEquatable<CreaseSegment>
{
    public CreaseSegment(Vec2 a, Vec2 b);
    public Vec2 A { get; } public Vec2 B { get; }   // 원래(펼친) 종이 좌표
    public double Length { get; }
}

public sealed class PaperState
{
    public static PaperState Initial { get; }    // 조각 1개 [−1,1]², world = paper = (−1,−1),(1,−1),(1,1),(−1,1), Layer 0, FrontUp false, 자국 0. 같은 불변 인스턴스
    public PaperState(IEnumerable<Facet> facets, IEnumerable<CreaseSegment> creases);
    //   목록 복사. facets null → ArgumentNullException, 조각 0개 → ArgumentException. creases null = 자국 없음.
    public IReadOnlyList<Facet> Facets { get; }
    public IReadOnlyList<CreaseSegment> Creases { get; }
    public int MaxLayer();                       // 조각 Layer 최댓값
    public double TotalArea();                   // Σ |조각 넓이| (접어도 4.0)
    public int StackCountAt(Vec2 p);             // Contains(p, 1e-6)인 조각 수 = 그 점의 겹 수 M
    public int TopFacetAt(Vec2 p);               // Contains(p, 1e-6)인 조각 중 Layer 최대의 인덱스, 없으면 −1
    public bool TrySnapGrab(Vec2 p, double cornerRadius, double edgeRadius, out Vec2 grab, out bool isCorner);
    //   시험판과 같음: 꼭짓점 ≤ cornerRadius 중 가장 가까운 것(같은 거리면 나중 것) → 변 위 최근접점 ≤ edgeRadius → 종이 위면 p → 실패 false
    public double FlipAxisX();                   // 모든 world 꼭짓점 x의 (최소+최대)/2
}
```

- 불변: 생성 뒤 바뀌지 않는다. 모든 엔진 연산은 새 `PaperState`를 만든다. 그래서 스냅샷·미리보기·되돌리기 기록이 상태를 공유해도 안전하고, Runtime은 **참조가 바뀌었을 때만** 메시를 다시 만든다.

---

## C-103 접기 엔진 (`Origami.Core.Folding`) — R-007

```csharp
public readonly struct FoldLine
{
    public FoldLine(Vec2 point, Vec2 normal);           // normal은 정규화해 저장
    public Vec2 Point { get; } public Vec2 Normal { get; }   // Normal: 움직이는 쪽(잡은 점 쪽, 음수) → 남는 쪽(양수)
    public Vec2 Direction { get; }                      // Normal.Perp
    public double Side(Vec2 p);                         // Dot(p − Point, Normal), |값| < 1e-9 이면 0 (Python Line.side)
    public double RawSide(Vec2 p);                      // 0 보정 없는 값 (자석 L 거리용, Python abs(_dot(q − pt, n)))
    public Vec2 Reflect(Vec2 p);                        // p − 2·RawSide(p)·Normal
    public double Distance(Vec2 p);                     // |RawSide(p)|
    public static FoldLine FromGrabDrop(Vec2 grab, Vec2 drop);   // Point = (g+d)/2, Normal = (d−g) 정규화
}

public enum FoldStepKind { Fold = 1, Crease = 2, Flip = 3 }

public sealed class FoldStep     // 기준 순서·되돌리기 기록·저장용 동작 하나 (불변)
{
    public static FoldStep Fold(Vec2 grab, Vec2 drop, bool mountain = false, int layers = 0);
    public static FoldStep Crease(Vec2 grab, Vec2 drop, int layers = 0);
    public static FoldStep Flip();
    public FoldStepKind Kind { get; } public Vec2 Grab { get; } public Vec2 Drop { get; }
    public bool Mountain { get; } public int Layers { get; }     // Flip은 Grab/Drop (0,0), false, 0
}

public enum FoldError
{
    None = 0,
    DragTooShort,       // "조금 더 끌어 주세요"        (거리 < MinDrag)
    NothingGrabbed,     // "잡은 곳에 종이가 없어요"     (layers ≥ 1인데 grab을 포함하는 조각 없음)
    NothingToFold,      // "접히는 부분이 없어요"        (움직이는 집합 비어 있음)
    LineOutsidePaper,   // "접는 선이 종이 밖이에요"     (남는 조각 없음)
    InvalidLayers,      // layers < 0 (UI는 만들지 않음, 문구 "")
}

public sealed class FoldPlan     // 불변 결과. Valid == false면 Error만 의미 있고 목록은 비어 있으며 Result는 null
{
    public bool Valid { get; } public FoldError Error { get; }
    public bool IsCrease { get; } public Vec2 Grab { get; } public Vec2 Drop { get; }
    public bool Mountain { get; } public int Layers { get; }
    public FoldLine Line { get; }
    public bool Partial { get; }                        // 움직인 조각 수 < 잡은 쪽 조각 수 (Python partial)
    public IReadOnlyList<Facet> Moving { get; }         // 움직이는 부분, 접기 전 위치·겹 (애니메이션 시작)
    public IReadOnlyList<Facet> Static { get; }         // 남는 부분, 접기 전 겹
    public IReadOnlyList<Facet> Moved { get; }          // 접은 뒤의 움직인 부분 (Result 안의 같은 객체). 접었다 펴기는 비어 있음
    public IReadOnlyList<CreaseSegment> NewCreases { get; }   // 접었다 펴기가 남긴 자국(접기는 비어 있음)
    public PaperState Result { get; }
}

public static class FoldPlanner
{
    public const double MinDrag = 0.03;      // GAME_RULES MIN_DRAG
    public const double HingeMin = 0.02;     // GAME_RULES HINGE_MIN
    public const double SideEps = 1e-9;      // Python EPS
    public const double MinArea = 1e-7;      // Python MIN_AREA
    public const double ContainsEps = 1e-6;

    public static FoldPlan PlanFold(PaperState state, Vec2 grab, Vec2 drop, bool mountain, int layers);
    public static FoldPlan PlanCrease(PaperState state, Vec2 grab, Vec2 drop, int layers);
    public static PaperState Flip(PaperState state);
    public static PaperState Apply(PaperState state, FoldStep step);
    //   step 하나를 적용. 무효 접기 → InvalidOperationException(FoldError를 메시지에). 기준 순서 계산용(카탈로그·책·테스트).
    public static PaperState ApplyAll(IEnumerable<FoldStep> steps, PaperState start = null);   // start null = Initial
    // state/steps null → ArgumentNullException (모든 메서드)
}
```

### 움직이는 집합 정하기 (Python `plan`) — `PlanFold`/`PlanCrease` 공통

0. `layers < 0` → `InvalidLayers`.
1. `Distance(grab, drop) < MinDrag` → `DragTooShort`.
2. `L = FoldLine.FromGrabDrop(grab, drop)`.
3. **분할** (각 조각 i, 인덱스 순, Python `_split`): 꼭짓점마다 `s = L.Side(w)`.
   - 음수가 하나도 없으면 → (움직임 없음, 남음 = **원래 조각 그대로**). 양수가 하나도 없으면 → (움직임 = 원래 조각, 남음 없음). (모든 s = 0이면 앞의 경우 = 남음.)
   - 그 밖: 꼭짓점을 돌며 `s ≤ 0`이면 움직이는 쪽에, `s ≥ 0`이면 남는 쪽에 넣고, 부호가 **엄격히** 바뀌는 변(`s_i < 0 < s_j` 또는 `s_i > 0 > s_j`)에서 `t = s_i / (s_i − s_j)`로 world·paper를 같이 보간한 점을 양쪽에 넣는다.
     한 쪽 부분이 꼭짓점 3개 미만이거나 |넓이| ≤ `MinArea`이면 그 쪽은 없음으로 보고 **원래 조각 전체**를 다른 쪽으로 보낸다(시험판 C#과 다름 — Python이 정본). 두 부분 모두 원래 조각의 Layer·FrontUp.
4. `grabSide` = 움직이는 부분이 있는 조각 인덱스(오름차순). 움직이는 집합 `mv = grabSide`.
5. `layers ≥ 1`이면(**위 N겹**): `stack` = `Contains(grab, 1e-6)`인 조각을 Layer 내림차순으로(같은 Layer는 인덱스 오름차순).
   `stack`이 비면 → `NothingGrabbed`. `layers < stack.Count`이면 `mv` = stack의 앞 `layers`개 중 움직이는 부분이 있는 것, 그다음 **닫힘**:
   더 늘지 않을 때까지 `grabSide`의 각 j(오름차순, mv에 없는 것)에 대해, mv의 어떤 i와 `Hinged(m_i, m_j, L)` 이거나 (`m_j.Layer > m_i.Layer` 그리고 `OverlapArea(m_i, m_j) > MinArea`)이면 j를 넣는다. (m = 움직이는 부분)
   `layers ≥ stack.Count`이면 전체 겹과 같다.
6. `mv`가 비면 → `NothingToFold`.
7. `Moving` = mv에 든 조각의 움직이는 부분(인덱스 순). `Static` = 인덱스 순으로, mv에 든 조각은 남는 부분(있으면), 안 든 조각은 **원래 조각**. `Static`이 비면 → `LineOutsidePaper`.
8. `Partial = mv.Count < grabSide.Count`.

- `Hinged(a, b, L)` (Python `_hinged`): a의 각 변 (a.W[i], a.W[i+1]) 중 두 끝 모두 `L.Side == 0`인 변은 건너뛰고, 남은 각 변의 **paper** 선분과 b의 각 **paper** 변의 `OverlapLen ≥ HingeMin`이면 true.
- `OverlapLen(a, b, c, d)` (Python `_overlap_len`): u = b − a, lu = |u|. lu < 1e-9 이거나 |Cross(u, c−a)|/lu > 1e-6 이거나 |Cross(u, d−a)|/lu > 1e-6 이면 0. 아니면 t1 = Dot(c−a, u)/lu, t2 = Dot(d−a, u)/lu, 결과 = min(lu, max(t1,t2)) − max(0, min(t1,t2)).
- `OverlapArea(a, b)` (Python `_overlap_area`): a.world(반시계로 맞춤)를 b.world(반시계로 맞춤)의 각 변으로 Sutherland–Hodgman 자르기(Cross ≥ 0 유지, 엄격한 부호 변화에서 교점). 3점 미만이면 0, 아니면 |넓이|.

### 접기 결과 (Python `fold`)

- 모든 조각(Moving + Static)의 Layer 최대 maxA·최소 minA, Moving의 최대 maxM·최소 minM.
- 출력 = Static 복사(같은 순서) 뒤에 넘어간 조각(Moving 순서).
- 산접기 + Partial: Static 중 Layer ≥ minM인 것은 Layer += maxM − minM + 1.
- 넘어간 조각 Layer: 골접기 `maxA + 1 + (maxM − f.Layer)` · 산접기 + Partial `minM + (maxM − f.Layer)` · 산접기 + 전체 `minA − 1 − (f.Layer − minM)`.
  World = `L.Reflect`를 **역순** 꼭짓점에 적용(반시계 유지), Paper = 역순, FrontUp 반전.
- 마지막에 서로 다른 Layer 값을 오름차순 0, 1, 2…로 다시 매긴다(Python `rank`). 자국 목록은 그대로.

### 접었다 펴기 결과 (Python `crease`)

- 같은 1~8단계 뒤, Moving의 각 조각의 각 변 (W[i], W[i+1]) 중 두 끝 모두 `L.Side == 0`인 변마다 `CreaseSegment(Paper[i], Paper[i+1])`를 (조각 순, 변 순으로) 만든다.
- `Result` = 조각 목록은 입력과 같은 객체·순서, 자국 = 기존 자국 + 새 자국. `Moved`는 빈 목록.

### 뒤집기 (Python `flip`)

- cx = `FlipAxisX()`, mx = `MaxLayer()`. 각 조각(순서 유지): World = 역순 꼭짓점의 (2·cx − x, y), Paper = 역순, Layer = mx − Layer, FrontUp 반전. 자국 그대로(종이 좌표라 같이 뒤집힌다).

---

## C-104 `FoldSession` (`Origami.Core.Folding`) — 종이 한 장의 상태와 되돌리기

```csharp
public sealed class FoldSession
{
    public FoldSession();                        // State = PaperState.Initial, 기록 비움
    public PaperState State { get; }
    public int FoldCount { get; }                // 현재 상태에 적용된 Fold 수 (R-007 "접은 횟수")
    public int CreaseCount { get; }              // 적용된 Crease 수 (R-007 "자국 수", 여러 선분이어도 1)
    public int ActionCount { get; }              // FoldCount + CreaseCount (R-008 가이드 단계 기준, Flip 제외)
    public bool CanUndo { get; }                 // 기록이 비어 있지 않음 (Undo·Unfold 허용 조건, R-007)
    public IReadOnlyList<FoldStep> AppliedSteps { get; }   // 기록 순서의 동작 (Undo로 줄고 Unfold로 비워짐)
    public FoldPlan Fold(Vec2 grab, Vec2 drop, bool mountain, int layers);   // 무효면 상태 불변, plan 반환
    public FoldPlan Crease(Vec2 grab, Vec2 drop, int layers);
    public void Flip();                          // 항상 적용, 기록에 추가
    public bool Undo();                          // 기록 비었으면 false(불변). 아니면 마지막 Fold/Crease/Flip 하나 취소(자국·횟수 포함)
    public bool Unfold();                        // 기록 비었으면 false(불변). 아니면 Initial, 횟수 0, 기록 비움
}
```

---

## C-105 `LineOnPaper` (`Origami.Core.Folding`) — 도해·가이드에 그리는 접는 선 (Python `line_on_paper`)

```csharp
public static class LineOnPaper
{
    public static bool TryGet(PaperState state, Vec2 grab, Vec2 drop, out Vec2 a, out Vec2 b);
}
```
- L = FromGrabDrop, d = (−L.Normal.Y, L.Normal.X). 각 조각 world(반시계로 맞춤)로 매개변수 구간 [−10, 10]을 Cyrus–Beck으로 자르고(분모 |den| < 1e-12 이면 num < −1e-9일 때 그 조각 제외), t1 > t0 + 1e-9인 조각의 구간 끝을 모은다.
  a = Point + d·min, b = Point + d·max(이 순서). 모은 것이 없으면 false. 결과는 반올림하지 않는다(`CONTENT_SPECS.md` §2 "선"은 6자리 반올림 값 — 테스트는 ±1e-4로 비교).

---

## C-106 놓기 자석 (`Origami.Core.Folding`) — R-021 (Python `mark_pieces`, `visible`, `landmarks`, `snap`)

```csharp
public enum SnapKind { None = 0, Guide = 1, Point = 2, Line = 3, Mark = 4 }   // GAME_RULES 표기 −, (튜토리얼), P, L, M

public readonly struct SnapResult
{
    public Vec2 Point { get; }        // 고친 놓는 점
    public SnapKind Kind { get; }
    public int AnchorCount { get; }   // 강조할 기준: Guide/Point 1(그 점), Line 2(두 기준점, 정렬된 앞·뒤), Mark 2(자국 선분 양 끝), None 0
    public Vec2 AnchorA { get; } public Vec2 AnchorB { get; }
}

public readonly struct MarkPiece { public Vec2 A { get; } public Vec2 B { get; } public int FacetIndex { get; } }   // 테이블 위(world) 자국 조각

public static class DropSnap
{
    public const double SnapPoint = 0.06, SnapLine = 0.04, SnapMark = 0.04, LinePairMin = 0.2;   // GAME_RULES
    public const double GuideSnap = 0.08;                                                       // 튜토리얼 목표 자석 (Fold3DSpike)
    public static IReadOnlyList<MarkPiece> MarkPieces(PaperState state);
    public static bool IsVisible(PaperState state, Vec2 q, int facetIndex);
    public static IReadOnlyList<Vec2> Landmarks(PaperState state);
    public static SnapResult Snap(PaperState state, Vec2 grab, Vec2 drop);
    public static SnapResult Snap(PaperState state, Vec2 grab, Vec2 drop, Vec2 guideDrop);   // 가이드가 보일 때
    public static double Key(double v);   // = Math.Round(v, 9, MidpointRounding.ToEven) — 동점 비교 키 (Python round(v, 9))
}
```

- `MarkPieces`: 조각 순 × 자국 순. 조각 paper 다각형(넓이 > 0이 아니면 역순)으로 자국 선분 a→b(d = b − a)를 Cyrus–Beck으로 [0,1] 안에서 자른다(|den| < 1e-12이면 num < −1e-9일 때 제외, 아니면 건너뜀; den > 0이면 t0 = max, 아니면 t1 = min).
  t1 − t0 > 1e-6이면 `MarkPiece(PaperToWorld(a + d·t0), PaperToWorld(a + d·t1), 조각 인덱스)`.
- `IsVisible(q, f)`: f가 아닌 조각 g 중 `g.Layer > f.Layer` 이고 `g.StrictlyContains(q, 1e-6)`인 것이 없으면 true.
- `Landmarks`: (1) 모든 조각의 world 꼭짓점(조각 순·꼭짓점 순) (2) 각 MarkPiece의 A, B 중 보이는 것 (3) MarkPiece 쌍 (i < j)의 교점: den = Cross(b−a, d−c), |den| < 1e-9면 건너뜀, t·u가 [−1e-9, 1+1e-9] 안이고 교점이 i 또는 j 조각에서 보이면 추가. 마지막에 이미 있는 점과 1e-6 이하로 가까운 점은 버린다(먼저 나온 것 유지).
- `Snap` (먼저 맞는 것 하나, 한 번만):
  0. (guideDrop 판) `Distance(drop, guideDrop) ≤ GuideSnap` → (guideDrop, Guide).
  1. lms = Landmarks 중 `Distance(q, grab) > 1e-6`. 그중 `Distance(q, drop) ≤ SnapPoint`인 것을 (Key(거리), Key(x), Key(y)) 최소로 → (q, Point).
  2. `Distance(grab, drop) < 1e-9` → (drop, None).
  3. L = FromGrabDrop(grab, drop). cand = lms 중 `L.Distance(q) ≤ SnapLine`(lms 순서). 모든 쌍(cand 순서의 i < j) 중 `Distance ≥ LinePairMin`인 쌍을 (Key(x), Key(y)) 순으로 정렬해 (앞, 뒤)로 두고
     (Key(거리 합), Key(앞.x), Key(앞.y), Key(뒤.x), Key(뒤.y)) 최소 쌍 → 놓는 점 = 앞–뒤 직선에 대한 grab의 대칭점(Python `_refl_ab`), (점, Line, 앞, 뒤).
  4. 각 MarkPiece에서 drop의 최근접점 q(선분 매개변수 [0,1]로 자름), d = 거리 ≤ SnapMark 이고 `IsVisible(q, 조각)`인 것을 (Key(d), Key(q.x), Key(q.y)) 최소로 → (q, Mark, 조각 A, B).
  5. (drop, None).
- 순수 함수, 결정적. Core 명령(`Fold`/`Crease`)은 자석을 적용하지 않는다 — 자석은 입력 단계(`FoldGesture`)에서만 쓴다(R-021 예외 상황).
- 기대(F-005 AC-16): plane 1단계 후 grab (−1,1), d (0.03,0.2) → Mark (0,0.2). plane 1·2단계 후 grab (1,1), d (0.04,−0.03) → Point (0,0). house 1~3단계 후 grab (−1,0), d (−0.62,−0.78) → Line (−0.6,−0.8). house 1단계만 → None. 각 작품 기준 입력은 자석 뒤에도 같은 접는 선(±1e-5).

---

## C-107 `FoldGesture` (`Origami.Core.Folding`) — 끌기 한 번의 입력 상태 (F-005 기대 동작)

```csharp
public sealed class FoldGesture
{
    public const double GrabCornerRadius = 0.16, GrabEdgeRadius = 0.08;   // Fold3DSpike 잡기 자석
    public static FoldGesture TryBegin(PaperState state, Vec2 pointer);   // TrySnapGrab 실패면 null. state null → ArgumentNullException
    public Vec2 Grab { get; }
    public bool GrabbedCorner { get; }
    public int StackCount { get; }       // 잡을 때의 StackCountAt(Grab) = M (끄는 동안 고정)
    public int Layers { get; }           // 0 = 전체, 1..M−1 = 위 n겹. 시작 0
    public SnapResult Drop { get; }      // 시작: (Grab, None)
    public void CycleLayers(int notches);
    //   M ≤ 1이면 항상 0. 아니면 Layers = ((Layers + notches) mod M + M) mod M  (전체 → 위 1 → … → 위 M−1 → 전체)
    public SnapResult MoveTo(PaperState state, Vec2 pointer);                    // Drop = DropSnap.Snap(state, Grab, pointer)
    public SnapResult MoveTo(PaperState state, Vec2 pointer, Vec2 guideDrop);    // 가이드 표시 중
    public FoldPlan Preview(PaperState state, bool mountain, bool crease);
    //   crease ? PlanCrease(state, Grab, Drop.Point, Layers) : PlanFold(state, Grab, Drop.Point, mountain, Layers)
}
```

- 놓을 때 Runtime은 `Preview`와 **같은 인자**(Grab, Drop.Point, mountain, Layers)로 host의 Fold/Crease를 부른다. 그래서 미리보기와 결과가 같다.
- 기대(F-005 AC-18 Core 부분): 다이아몬드 기본형에서 (−1,−1)을 잡으면 StackCount 6, `CycleLayers(1)` 두 번 → Layers 2, `FoldTexts.LayerLabel(2, 6)` = "겹: 위 2 / 6".

## C-108 `FoldTexts` (`Origami.Core.Folding`)

```csharp
public static class FoldTexts
{
    public static string ErrorMessage(FoldError error);     // C-103 표의 문구, None·InvalidLayers → ""
    public static string LayerLabel(int layers, int stackCount);   // layers 0 → "겹: 전체", n ≥ 1 → "겹: 위 {n} / {stackCount}"
    public const string CreaseLabel = "접었다 펴기";
}
```
(Runtime 접기 테이블이 Core 규칙·문구 모듈(011)을 기다리지 않도록 여기 둔다. `GameTexts`는 이것을 그대로 쓴다.)

---

## C-110 판정 (`Origami.Core.Recognition`) — R-011~R-013, F-009

```csharp
public enum Grade { None = 0, Star1 = 1, Star2 = 2, Star3 = 3 }

public static class Grading
{
    public const int MatchMin = 85, Grade2Min = 93, Grade3Min = 97;   // GAME_RULES (%)
    public static int Percent(double score);        // floor(score × 100 + 1e-9)
    public static Grade FromPercent(int percent);   // ≥97 ★3, ≥93 ★2, ≥85 ★1, 그 밖 None
    public static Grade FromScore(double score);    // FromPercent(Percent(score))
}

public static class D4
{
    public const int Count = 8;
    public static Vec2 Apply(int g, Vec2 p);
    //   g0 (x,y) g1 (−y,x) g2 (−x,−y) g3 (y,−x) g4 (−x,y) g5 (−y,−x) g6 (x,−y) g7 (y,x). 범위 밖 → ArgumentOutOfRangeException
}

public sealed class Outline      // 볼록 다각형들의 합집합을 행 주사로 표현 (불변)
{
    public const double RowStep = 0.001;
    public static Outline FromPolygons(IEnumerable<IReadOnlyList<Vec2>> polygons);   // 방향 무관
    public static Outline FromState(PaperState state);                               // 모든 조각 World
    public double Area { get; }
    public Vec2 Centroid { get; }
    public static double IntersectionArea(Outline a, Outline b);
}

public static class Similarity
{
    public static double Centered(PaperState a, PaperState b);   // = R-011 일치도 s (0..1): 두 윤곽을 각자 무게중심으로 옮기고, b에 D4 8개를 적용한 IoU의 최댓값
}

public sealed class RecognitionCatalog
{
    public RecognitionCatalog(IEnumerable<KeyValuePair<string, PaperState>> references);
    //   순서 = 카탈로그 순서(동점 우선순위). null → ArgumentNullException, ID null/빈 문자열/중복 또는 상태 null → ArgumentException.
    //   생성 시 각 기준 윤곽을 무게중심으로 옮긴 뒤 D4 8개 변환의 Outline을 미리 계산한다.
    public IReadOnlyList<string> ModelIds { get; }
    public double Score(PaperState player, string modelId);   // 모르는 ID → ArgumentException
}

public sealed class RecognitionResult
{
    public string ModelId { get; }      // 인정된 작품, 구겨진 종이면 null
    public Grade Grade { get; }         // 구겨진 종이면 None
    public bool IsPiece { get; }        // ModelId != null
    public double Score { get; }        // = BestScore
    public int Percent { get; }         // = Grading.Percent(BestScore)
    public string BestModelId { get; }  // 가장 비슷한 후보 (후보가 없으면 null)
    public double BestScore { get; }    // 후보 없으면 0
}

public static class Recognizer
{
    public static RecognitionResult Recognize(PaperState state, IEnumerable<string> ownedModelIds, RecognitionCatalog catalogue);
    //   null → ArgumentNullException. 후보 = 카탈로그 순서로, owned에 든 ID만(모르는 ID·중복 무시).
    //   최고 = 점수가 **엄격히 큰** 것만 갱신(동점 = 카탈로그 앞 작품, R-012). IsPiece ⇔ Grading.FromScore(BestScore) ≠ None.
}
```

### 행 주사 적분 (A2-3, ARCHITECTURE §9)

- 행: 정수 k에 대해 y_k = (k + 0.5)·h, h = `RowStep`. 다각형마다 k = floor(minY/h − 0.5) … ceil(maxY/h − 0.5).
- 한 행에서 볼록 다각형의 구간: 각 변 (a, b)가 `(a.Y ≤ y < b.Y) 또는 (b.Y ≤ y < a.Y)`이면 교점 x를 구해, 교점이 2개 이상이고 max > min이면 [min, max].
- 행마다 구간을 (lo, hi) 오름차순 정렬해 `lo ≤ 현재 hi`이면 합친다. 넓이 = h·Σ(hi − lo). 무게중심 x = h·Σ(hi² − lo²)/2 ÷ 넓이, y = h·Σ(hi − lo)·y_k ÷ 넓이.
- 교집합 넓이 = 같은 k의 두 구간 목록을 병합해 겹친 길이의 합 × h.
- `Similarity.Centered(a, b)`: A = a의 World 다각형을 −Centroid(A)만큼 옮겨 다시 주사, B도 같게. g = 0..7마다 B의 **꼭짓점**에 `D4.Apply(g, ·)`를 적용해 주사하고 IoU = I ÷ (넓이A + 넓이gB − I). 최댓값.
- `RecognitionCatalog`는 B 쪽(기준 8개)을 미리 들고 있고, `Recognize`는 플레이어 윤곽을 한 번만 주사한다.
- 정확도: 정확값과 ≤ 0.002(ARCHITECTURE §9 프로토타입), MATCH_ERROR_MAX 0.005·`CONTENT_SPECS.md` §4 ±0.01 충족.

### 기대 (F-009)

- AC-01: J-01~J-18이 §4 표의 결과(작품/구겨진 종이, 등급)이고 일치도가 ±0.01. AC-02: 작품끼리 표 ±0.01, 대각선 밖 모두 < 0.85.
- AC-03: 기준 상태를 (+0.3, −0.2) 이동·90° 회전·좌우 반전한 **날것의 상태**(테스트가 `new PaperState(...)`로 꼭짓점을 직접 바꿈) → 1.0 ±0.01.
- AC-04: `Grading.FromScore` 0.97 → ★3, 0.969 → ★2, 0.93 → ★2, 0.929 → ★1, 0.85 → ★1, 0.849 → None. AC-05: 같은 기준 상태를 가진 가짜 작품 A, B(순서 A, B) → A. AC-06: 100회 반복 동일(비트 단위).
- **금지(리뷰 W-6)**: 강아지 1~5단계(턱 없이, 0.849) 결과를 단언하는 테스트를 만들지 않는다. 지름길 결과(강아지 1단계 뺌 0.867, 매미 2·7단계 뺌 0.892)는 "< 0.91"만 단언할 수 있다.

---

## C-120 카탈로그 (`Origami.Core.Catalog`) — 값은 DATA_STRUCTURE D-110~D-115 (= CONTENT_SPECS)

```csharp
public static class ModelIds { public const string Plane = "plane", House = "house", Tulip = "tulip", Dog = "dog", Cicada = "cicada"; }

public sealed class ModelDefinition      // 불변
{
    public int Order { get; }                         // 1..5 (카탈로그 순서 = 판정 동점 순서)
    public string Id { get; } public string DisplayName { get; }
    public int BasePrice { get; } public int PagePrice { get; } public bool InitiallyOwned { get; }
    public int Difficulty { get; }                    // 책 표시 ★ 수
    public string Techniques { get; }                 // 표지 "쓰는 기법" 문구
    public IReadOnlyList<FoldStep> Steps { get; }     // CONTENT §2 기준 순서
    public IReadOnlyList<string> StepTexts { get; }   // CONTENT §2 "책 설명" (Steps와 같은 길이)
    public int ActionCount { get; }                   // = Steps.Count (책 동작 수)
    public int FoldCount { get; }                     // Steps 중 Fold 수
    public double CoverRotationDeg { get; }           // 표지·완성 그림 회전 (튤립·매미 45, 그 밖 0)
}

public static class ModelCatalog
{
    public static IReadOnlyList<ModelDefinition> Models { get; }      // 카탈로그 순서
    public static ModelDefinition Find(string id);                    // 없거나 null → null
    public static int IndexOf(string id);                             // 없으면 −1
    public static PaperState ReferenceState(string id);              // ApplyAll(Steps). 모르는 ID → ArgumentException
    public static PaperState StateBeforeStep(string id, int stepNumber);   // 1-based: 1 → Initial, N+1 → ReferenceState. 범위 밖 → ArgumentOutOfRangeException
    public static RecognitionCatalog Recognition { get; }            // 5종 기준 상태로 만든 판정 카탈로그 (static readonly Lazy, 스레드 안전, 결정적)
    public static IReadOnlyList<string> PopularByDay { get; }        // [0] = 1일차
    public static string PopularModelId(int day);                    // 1..7 밖 → ArgumentOutOfRangeException
}

public static class EndingIds { public const string EmptyHanded = "E-01", TulipGarden = "E-02", Master = "E-03", Tycoon = "E-04", LocalWorkshop = "E-05"; }
public sealed class EndingDefinition { public string Id { get; } public int Priority { get; } public string DisplayName { get; } public string Text { get; } }
public static class EndingCatalog { public static IReadOnlyList<EndingDefinition> Endings { get; } public static EndingDefinition Find(string id); }

public static class InteractTargets
{
    public const string Table = "table", Bookshelf = "bookshelf", Counter = "counter", Bed = "bed";
    public static IReadOnlyList<string> All { get; }       // table, bookshelf, counter, bed (R-004 동점 순서)
    public static bool IsValid(string target);             // Ordinal 비교, null → false
}

public enum BookPageKind { Contents = 0, Symbols = 1, Cover = 2, Step = 3, Complete = 4 }

public static class BookLayout      // R-010 쪽 배치 (CONTENT §3)
{
    public const int ContentsPage = 0, SymbolsPage = 1;
    public static int PageCount { get; }                   // 2 + Σ(ActionCount + 2) = 44
    public static int CoverPage(string modelId);           // plane 2, house 10, tulip 17, dog 24, cicada 32. 모르는 ID → ArgumentException
    public static BookPageKind KindOf(int page);           // 범위 밖 → ArgumentOutOfRangeException
    public static string ModelAt(int page);                // 0·1 → null
    public static int StepAt(int page);                    // Step 쪽의 1-based 단계 번호, 그 밖 0
    public static int ClampToSpread(int page);             // p를 [0, PageCount−1]로 맞춘 뒤 p − p mod 2 (R-010 TurnPage)
}
```

- 기준 순서의 수치 표기(D-111): 종이비행기의 k = 1 − √2, x₀ = 2·tan(π/8)는 **계산값**(반올림 없음), 튤립·매미의 놓는 점은 CONTENT §2의 **6자리 리터럴**(Python `_tip`이 `_r6`로 반올림한 값)을 쓴다. 그래야 J 값·AC 넓이가 Python과 같다.

---

## C-130 경제 (`Origami.Core.Economy`) — R-015, R-017, R-018, R-020

```csharp
public static class RuleConstants
{
    public const int TotalDays = 7, StartMoney = 20, PaperCost = 2, DailySheets = 5;
    public const int GradePctStar3 = 150, GradePctStar2 = 100, GradePctStar1 = 60;
    public const int PopularPct = 150, NormalPct = 100, PriceDivisor = 10000;
    public const int EmptyHandedMoney = 20, TulipGardenCount = 12, TycoonMoney = 600;
}
public static class Pricing
{
    public static int GradePct(Grade grade);                         // None → ArgumentOutOfRangeException
    public static int SalePrice(int basePrice, Grade grade, bool popular);   // floor(base × GradePct × (150|100) / 10000)
    public static int SalePrice(string modelId, Grade grade, int day);       // 그날 인기 작품이면 popular. 모르는 ID → ArgumentException, day 1..7 밖 → ArgumentOutOfRangeException
}
public sealed class CompletionCounts : IEquatable<CompletionCounts>   // v1과 같은 구조, v2 카탈로그 5종 × ★1~★3
{
    public static readonly CompletionCounts Empty;
    public int Get(string modelId, Grade grade);    // 모르는 ID·None → 0
    public int Total(string modelId);
    public CompletionCounts With(string modelId, Grade grade, int count);   // 모르는 ID·None → ArgumentException, count < 0 → ArgumentOutOfRangeException
    public CompletionCounts Increment(string modelId, Grade grade);
}
public enum RunEndReason { None = 0, Completed = 1, Bankrupt = 2 }
public static class EndingRules
{
    public static string Evaluate(RunEndReason reason, int finalMoney, CompletionCounts counts);
    //   reason None → ArgumentOutOfRangeException, counts null → ArgumentNullException.
    //   E-01 (Bankrupt 또는 money ≤ 20) → E-02 (tulip Total ≥ 12) → E-03 (5종 모두 ★3 ≥ 1) → E-04 (money ≥ 600) → E-05
}
```

---

## C-140 튜토리얼 (`Origami.Core.Tutorial`) — R-008, F-006

```csharp
public sealed class GuideStep
{
    public int Number { get; }               // 1..6
    public FoldStepKind Kind { get; }        // 1단계 Crease, 2~6단계 Fold
    public Vec2 Grab { get; } public Vec2 Drop { get; }
    public Vec2 LineA { get; } public Vec2 LineB { get; }   // LineOnPaper(StateBeforeStep(plane, n), Grab, Drop)
    public bool EitherSide { get; }          // 1단계·6단계만 true (A2-9)
    public string Text { get; }              // plane StepTexts[n−1]
}
public static class PlaneGuide
{
    public const int StepCount = 6;
    public static IReadOnlyList<GuideStep> Steps { get; }    // ModelCatalog plane 기준 순서에서 생성 (static readonly)
}
public sealed class StepScore
{
    public int Score { get; }               // 0..100
    public double CreaseOffset { get; } public double AngleErrorDeg { get; }
    public double GrabError { get; } public double DropError { get; }
    public bool WrongSide { get; } public bool KindMismatch { get; }
}
public static class StepScoring
{
    public const double OffsetTolerance = 0.25, AngleTolerance = 20.0;
    public static StepScore Evaluate(GuideStep target, FoldStepKind kind, Vec2 grab, Vec2 drop);
    //   Fold3DSpike FoldScoring.Evaluate와 같은 공식(이상 선 = FromGrabDrop(target.Grab, target.Drop), 오프셋 = LineA·LineB의 플레이어 선 거리 평균,
    //   점수 = round(100 × (0.5·pos + 0.5·ang)), WrongSide면 절반, EitherSide면 거울 짝 오차 비교). kind ≠ target.Kind → Score 0, KindMismatch true.
}
```

---

## C-150 `OrigamiGame` (`Origami.Core.Game`) — 공방 규칙 상태

```csharp
public sealed class OrigamiGame
{
    public OrigamiGame();                          // = NewRun 직후 상태
    public GameSnapshot Snapshot { get; }          // 불변. 명령이 **수락**될 때만 새 객체로 바뀐다 (거부 시 같은 참조)
    public CommandResult NewRun();
    public CommandResult Interact(string target);
    public CommandResult StandUp();
    public CommandResult OpenBook();
    public CommandResult CloseBook();
    public CommandResult TurnPage(int page);
    public CommandResult BuyPage(string modelId);
    public CommandResult CloseCounter();
    public CommandResult Sleep(bool yes);
    public CommandResult TakeSheet();
    public CommandResult Fold(Vec2 grab, Vec2 drop, bool mountain, int layers);
    public CommandResult Crease(Vec2 grab, Vec2 drop, int layers);
    public CommandResult Flip();
    public CommandResult Undo();
    public CommandResult Unfold();
    public CommandResult Finish();
    public CommandResult Discard();
    public CommandResult SkipGuide();
}
```

### 거부 순서와 동작 (R-019: 모든 명령은 RunEnded → WrongMode 먼저, `NewRun` 제외. 거부 = 상태·스냅샷 참조 불변)

| 명령 | 검사 순서 (첫 사유로 거부) | 수락 시 |
|---|---|---|
| `NewRun` | 없음 | R-002 상태(D-120 시작값). 진행 중 종이·진열·튜토리얼 상태 폐기, 엔딩 판정 없음 |
| `Interact(t)` | RunEnded → WrongMode(≠Walking) → UnknownTarget(`!InteractTargets.IsValid`) → (bed만) SheetOnTable | table → Seated(책 닫힘), bookshelf → Bookshelf, counter → Counter, bed → SleepConfirm |
| `StandUp` | RunEnded → WrongMode(≠Seated) | Walking, 책 닫힘, 종이 그대로 |
| `OpenBook` | RunEnded → WrongMode(≠Seated) | 책 펼침(이미 펼침이면 변화 없이 수락) |
| `CloseBook` | RunEnded → WrongMode(Seated·Bookshelf 아님) | Seated: 책 닫힘(멱등). Bookshelf: Walking |
| `TurnPage(p)` | RunEnded → WrongMode(책이 안 보임: Bookshelf 또는 Seated+펼침이 아님) | 현재 쪽 = `BookLayout.ClampToSpread(p)` |
| `BuyPage(m)` | RunEnded → WrongMode(책 안 보임) → UnknownModel(null/""/미지) → AlreadyOwned → NotEnoughMoney(돈 < 가격) | 돈 −가격, 총 책값 +가격, 보유 추가 |
| `CloseCounter` | RunEnded → WrongMode(≠Counter) | Walking |
| `Sleep(yes)` | RunEnded → WrongMode(≠SleepConfirm) | no: Walking. yes: 정산(아래) |
| `TakeSheet` | RunEnded → WrongMode(≠Seated) → SheetOnTable → NoSheetsLeft → NotEnoughMoney(유료이고 돈 < 2) | 새 `FoldSession`. 첫 종이(FreeSheetAvailable)면 무료 + 튜토리얼 종이, 아니면 돈 −2·총 종이값 +2. 남은 종이 −1 |
| `Fold(g,d,m,n)` | RunEnded → WrongMode(≠Seated) → NoSheet → InvalidFold(n < 0 → `FoldError.InvalidLayers`; g·d 좌표 중 하나라도 NaN·무한대 → `FoldError.NothingToFold`; 그 밖 엔진 FoldError) | 적용. 가이드가 보였으면 단계 점수 기록 |
| `Crease(g,d,n)` | 같음 | 같음 (자국) |
| `Flip` | RunEnded → WrongMode → NoSheet | 적용 |
| `Undo` / `Unfold` | RunEnded → WrongMode → NoSheet → NothingToUndo(기록 빔) | 적용. 단계 점수를 남은 동작 수만큼으로 자름 / 비움 |
| `Finish` | RunEnded → WrongMode → NoSheet → NoFolds(FoldCount 0) | `Recognizer.Recognize(state, 보유, ModelCatalog.Recognition)`. 작품: 진열 추가(D-122), 완성 수 +1. 구겨진 종이: 실패 +1. 종이 사라짐. 튜토리얼 종이면 튜토리얼 끝 |
| `Discard` | RunEnded → WrongMode → NoSheet | 실패 +1, 종이 사라짐, 튜토리얼 종이면 튜토리얼 끝 |
| `SkipGuide` | RunEnded → WrongMode(≠Seated) → NoGuide(가이드가 안 보임 — 종이 없음 포함, A2-10) | 이 종이의 가이드 끔(다시 켤 수 없음) |

- **좌표 방어**(ORIGAMI-010 W-1, DECISIONS): `Fold`/`Crease`는 n 검사 다음, 엔진 호출 전에 `grab`·`drop`의 X·Y가 모두 유한한지 본다. NaN·±∞가 하나라도 있으면
  `InvalidFold` + `FoldError.NothingToFold`로 거부한다(예외 없음, 세션·스냅샷 불변, 단계 점수 기록 없음). Runtime 포인터는 이런 값을 만들지 않으므로 방어용이다.
  문구는 `FoldTexts.ErrorMessage(NothingToFold)` = "접히는 부분이 없어요".
- **가이드 보임** = 종이가 튜토리얼 종이 ∧ 이 종이에서 `SkipGuide` 안 함 ∧ `ActionCount < 6`. 그때 가이드 단계 = `PlaneGuide.Steps[ActionCount]`.
  가이드가 보이는 상태에서 Fold/Crease가 수락되면 `StepScoring.Evaluate(그 단계, 종류, g, d)`를 기록한다(Flip은 기록 없음).
- **정산**(`Sleep(yes)`, R-016): 진열 순서대로 `SaleLine(id, grade, popular = id == PopularModelId(오늘), price = Pricing.SalePrice(…))`, 합계를 돈·총 판매액에 더하고 진열 비움.
  오늘이 7일차면 종료(Completed, 일차 7 유지). 아니면 일차 +1, 남은 종이 5, Walking, 그 뒤 돈 < 2이면 종료(Bankrupt). 종료면 모드 Ended, EndingId = `EndingRules.Evaluate`.
  `CommandResult.Settlement` = `SettlementResult(정산한 일차, lines, total, 정산 후 돈, EndReason, 계속이면 다음 날 인기 작품 ID, 종료면 null)`.
- 불변식: 돈 ≥ 0, 돈 = 20 + 총 판매액 − 총 종이값 − 총 책값(R-020), 0 ≤ 남은 종이 ≤ 5, 1 ≤ 일차 ≤ 7, 현재 쪽 짝수.
- 결정성: 같은 명령 열 → 같은 스냅샷 값(F-001 AC-08). 기준 상태·판정 카탈로그는 `ModelCatalog` 공유 인스턴스.

## C-151 결과·스냅샷 타입 (`Origami.Core.Game`) — 모두 불변, 목록은 읽기 전용 복사본

```csharp
public enum WorkshopMode { Walking = 1, Seated = 2, Bookshelf = 3, Counter = 4, SleepConfirm = 5, Ended = 6 }
public enum RejectReason { None = 0, RunEnded, WrongMode, UnknownTarget, SheetOnTable, NoSheetsLeft, NotEnoughMoney,
                           NoSheet, InvalidFold, NothingToUndo, NoFolds, NoGuide, UnknownModel, AlreadyOwned }

public sealed class CommandResult
{
    public bool Accepted { get; }
    public RejectReason Reason { get; }              // 수락이면 None
    public FoldError FoldError { get; }              // InvalidFold일 때 엔진 사유(layers < 0 → InvalidLayers), 그 밖 None
    public FoldPlan Plan { get; }                    // 수락된 Fold/Crease의 계획, 그 밖 null
    public RecognitionResult Recognition { get; }    // 수락된 Finish, 그 밖 null
    public StepScore GuideScore { get; }             // 가이드 단계 점수가 기록된 Fold/Crease, 그 밖 null
    public SettlementResult Settlement { get; }      // 수락된 Sleep(yes), 그 밖 null
}
public sealed class SaleLine { public string ModelId { get; } public Grade Grade { get; } public bool Popular { get; } public int Price { get; } }
public sealed class SettlementResult
{
    public int SettledDay { get; } public IReadOnlyList<SaleLine> Lines { get; } public int Total { get; } public int MoneyAfter { get; }
    public RunEndReason EndReason { get; } public string NextPopularModelId { get; }   // 종료면 null
}
public sealed class DisplayedPiece
{
    public string ModelId { get; } public Grade Grade { get; } public double Score { get; } public int Percent { get; }
    public PaperState Shape { get; }                 // 완성 순간의 플레이어 종이 상태 (판매대 표시)
}
public sealed class SheetSnapshot
{
    public PaperState State { get; }
    public int FoldCount { get; } public int CreaseCount { get; } public int ActionCount { get; } public bool CanUndo { get; }
    public bool IsTutorial { get; }
    public bool GuideVisible { get; }
    public GuideStep Guide { get; }                  // GuideVisible이면 현재 단계, 아니면 null
    public IReadOnlyList<StepScore> GuideScores { get; }   // 현재 동작에 대응하는 기록 (Undo로 줄어듦)
}
public sealed class GameSnapshot
{
    public int Day { get; } public int Money { get; } public int SheetsLeft { get; }
    public WorkshopMode Mode { get; } public bool BookOpen { get; } public bool BookVisible { get; } public int BookPage { get; }
    public IReadOnlyList<string> OwnedModelIds { get; }     // 카탈로그 순서
    public bool FreeSheetAvailable { get; }                 // 런의 첫 종이를 아직 안 꺼냄
    public bool TutorialFinished { get; }                   // 튜토리얼 종이를 완성/버림
    public bool HasSat { get; }                             // 이번 런에서 Interact(table)이 한 번 이상 수락됨 (걷기 안내 F-013)
    public SheetSnapshot Sheet { get; }                     // 테이블 위 종이, 없으면 null
    public IReadOnlyList<DisplayedPiece> Display { get; }   // 오늘 진열 (완성 순)
    public CompletionCounts Completions { get; }
    public int FailureCount { get; } public int TotalSales { get; } public int TotalPaperCost { get; } public int TotalBookCost { get; }
    public string PopularModelId { get; }                   // 오늘
    public RunEndReason EndReason { get; } public string EndingId { get; }   // 진행 중이면 None / null
    public bool IsEnded { get; }
}
```

---

## C-160 책 쪽 내용 (`Origami.Core.Book`) — R-010, F-007, F-008, CONTENT §3

```csharp
public static class BookPages
{
    public static BookPage Get(int page, IEnumerable<string> ownedModelIds);
    //   page 범위 밖 → ArgumentOutOfRangeException, owned null → ArgumentNullException. 순수 함수(결정적, 같은 입력 = 같은 값)
}
public sealed class BookPage
{
    public int Index { get; } public BookPageKind Kind { get; }
    public string ModelId { get; }                    // 0·1쪽 null
    public bool Sealed { get; }                       // 보유하지 않은 작품의 Step/Complete 쪽
    public int StepNumber { get; } public int StepTotal { get; }   // Step 쪽만 (1-based / ActionCount), 그 밖 0
    public string StepLabel { get; }                  // "n / N", Step 쪽만, 그 밖 ""
    public StepDiagram Diagram { get; }               // Step 쪽이고 봉인 아님, 그 밖 null
    public CoverInfo Cover { get; }                   // Cover 쪽만 (봉인 없음 — 표지는 항상 보임)
    public PaperState CompleteShape { get; }          // Complete 쪽이고 봉인 아님 (기준 상태), 그 밖 null
    public IReadOnlyList<ContentsEntry> Contents { get; }   // 0쪽만 (5개), 그 밖 빈 목록
}
public sealed class StepDiagram
{
    public FoldStep Step { get; }
    public PaperState Before { get; } public PaperState After { get; }     // 직전 상태(자국 포함) / 동작 뒤 (오른쪽 아래 작은 그림)
    public int BeforeFolds { get; } public int BeforeCreases { get; }
    public Vec2 LineA { get; } public Vec2 LineB { get; }                   // LineOnPaper(Before, Grab, Drop)
    public SnapKind Magnet { get; }                                         // DropSnap.Snap(Before, Grab, Drop).Kind (책 기준 입력)
    public IReadOnlyList<Vec2> Anchors { get; }                             // Magnet Point → [점], Line → [앞, 뒤], 그 밖 [] (도해의 작은 원)
    public string Text { get; }                                             // StepTexts
}
public sealed class CoverInfo
{
    public string ModelId { get; } public string Name { get; } public PaperState Silhouette { get; } public double RotationDeg { get; }
    public int Difficulty { get; } public int ActionCount { get; } public string Techniques { get; }
    public int BasePrice { get; } public int PagePrice { get; } public bool Owned { get; } public bool Buyable { get; }   // Buyable = !Owned
}
public sealed class ContentsEntry { public string ModelId { get; } public string Name { get; } public bool Owned { get; } public int PagePrice { get; } public int CoverPage { get; } }
```
- 기대(F-007 AC-02): 6쪽 = plane 4단계: Before의 FoldCount 2·CreaseCount 1(BeforeFolds 2, BeforeCreases 1), LineA/LineB ≈ (0,1)·(−0.8284,−1)(순서 무관 비교 허용), Step.Grab (−1,0), Drop (0,−0.4142), Fold·골·전체, StepLabel "4 / 6". 3쪽 Step.Kind Crease. 21쪽 Step.Layers 2.
- 접기 기호 쪽(1쪽)의 내용은 고정 그림이라 데이터가 없다(Kind = Symbols). Runtime이 기호를 그린다.

---

## C-170 문구 (`Origami.Core.Texts`) — F-013, F-010, F-011, F-012

```csharp
public sealed class HudData { public int Day { get; } public int Money { get; } public int SheetsLeft { get; } public string PopularName { get; } public IReadOnlyList<string> Lines { get; } }
public static class GameTexts
{
    public static string ModelName(string modelId);                    // 카탈로그 이름, 모르면 ""
    public static string RejectMessage(RejectReason reason, FoldError foldError);
    //   SheetOnTable "테이블 위 종이를 먼저 완성하거나 버리세요" · NoSheetsLeft "오늘은 종이를 다 썼어요. 침대에서 쉬세요" · NotEnoughMoney "돈이 모자라요"
    //   NoFolds "아직 접지 않았어요" · NothingToUndo "되돌릴 것이 없어요" · AlreadyOwned "이미 가진 페이지예요" · InvalidFold → FoldTexts.ErrorMessage(foldError)
    //   NoSheet/NoGuide/WrongMode/UnknownTarget/UnknownModel/RunEnded/None → ""   (무음: 토스트 없음. NoSheet는 ORIGAMI-011 W-1, DECISIONS)
    //   InvalidFold의 FoldTexts 문구: None·InvalidLayers → "" (제스처는 음수 겹을 만들지 않음), NaN 좌표 → NothingToFold 문구 (C-150)
    public static HudData Hud(GameSnapshot s);        // Lines: "N일차 / 7", "돈 N원", "오늘 남은 종이 N / 5", "오늘의 인기: 이름" (+ 앉음+종이: "접은 횟수 N · 자국 N")
    public static string InteractPrompt(string target);   // "E: 앉기" / "E: 책 읽기" / "E: 판매대 보기" / "E: 잠자기", 그 밖 ""
    public const string WalkHint = "WASD/방향키로 걸어 작업 테이블로 가세요";
    public const string TakeSheetHint = "Space: 종이 꺼내기";
    public const string PostTutorialHint = "이제부터 접는 법은 책에서만 볼 수 있어요. 앉아서 B 또는 책장에서 E";
    public static string RecognitionLine(RecognitionResult r);   // "이름 ★n (일치도 xx%)" / "구겨진 종이 (가장 비슷한 작품: 이름 xx%)"
    public static string GuideTitle(GuideStep step);             // "종이비행기 n / 6" (+ Crease면 " — Ctrl을 누른 채 놓으세요")
    public static IReadOnlyList<string> SettlementLines(SettlementResult r);   // 줄 형식: 아래 표
    public static IReadOnlyList<string> CounterLines(GameSnapshot s);          // 줄 형식: 아래 표
    public static IReadOnlyList<string> ResultLines(GameSnapshot s);           // 줄 형식: 아래 표
}
```
- 문구의 정본은 FEATURE_SPECS F-013·CONTENT_SPECS §6·§8. 숫자는 `CultureInfo.InvariantCulture`, 자릿수 구분 기호 없음. 금액 = `N원`.
- 등급 표기는 **"★n"(별 하나 + 숫자, n = 1..3)** 이다(R-012 표기, DECISIONS). 등급 None은 빈 문자열.
- 화면 줄 형식(ORIGAMI-011 구현, `GameTextsTests`가 고정 — ORIGAMI-011 N-1, DECISIONS). 줄은 위에서 아래 순서, `[…]`는 조건부 줄, `이름` = `ModelName(id)`:

| 함수 | 줄 (순서대로) |
|---|---|
| `SettlementLines(r)` | `{정산한 일차}일차 정산` → 판매 줄마다 `이름 ★n P원`(인기 작품이면 뒤에 ` (인기)`), 판매가 없으면 `팔린 작품이 없어요` 한 줄 → `합계 N원` → `정산 후 돈 N원` → 마지막 한 줄: 계속이면 `내일의 인기: 이름`, Completed면 `7일이 모두 지났어요`, Bankrupt면 `돈이 모자라 공방 문을 닫았어요` |
| `CounterLines(s)` | `{일차}일차 판매대` → `오늘의 인기: 이름` → 진열 순서대로 `이름 ★n 일치도 xx% · 오늘 팔면 P원`(P = `Pricing.SalePrice(id, grade, 일차)`), 진열이 비면 `진열된 작품이 없어요` 한 줄 → `합계 예상 N원`(P의 합) |
| `ResultLines(s)` | [엔딩이 있을 때만: 엔딩 이름, 엔딩 문구 — 두 줄] → `최종 돈 N원` → `ModelCatalog.Models` 순서로 작품마다 `이름: ★3 a · ★2 b · ★1 c` → `실패 N` → `총 판매액 N원` → `총 종이값 N원` → `총 책값 N원` |

- 세 함수와 `Hud`·`RecognitionLine`·`GuideTitle`은 인자가 null이면 `ArgumentNullException`. 반환 목록은 읽기 전용 복사본.
- 책 화면 전용 표시 라벨은 이 클래스에 없다 — Runtime `BookTexts`(C-192, DECISIONS).

## C-171 화면 흐름 (`Origami.Core.Screens`)

```csharp
[Flags] public enum GameKeys
{
    None = 0, Escape = 1 << 0, Enter = 1 << 1, Interact = 1 << 2, TakeSheet = 1 << 3, Discard = 1 << 4, Undo = 1 << 5,
    Unfold = 1 << 6, Flip = 1 << 7, Book = 1 << 8, SkipGuide = 1 << 9, StandUp = 1 << 10, PageLeft = 1 << 11, PageRight = 1 << 12,
}
public enum Overlay { None = 0, PauseMenu, ConfirmNewRun, Settlement, Result }
public enum MessageKind { None = 0, Reject, Recognition, Info }
public readonly struct FlowMessage { public int Serial { get; } public MessageKind Kind { get; } public string Text { get; } }
public enum SheetChangeKind { None = 0, Taken, Folded, Creased, Flipped, Undone, Unfolded, Removed, Reset }
public readonly struct SheetChange { public int Serial { get; } public SheetChangeKind Kind { get; } public FoldPlan Plan { get; } public PaperState Previous { get; } }

public sealed class ScreenFlow
{
    public ScreenFlow();                                 // 내부 OrigamiGame 생성 (= NewRun 상태), Overlay None
    public GameSnapshot Snapshot { get; }                // 내부 게임의 Snapshot
    public Overlay Overlay { get; }
    public SettlementResult PendingSettlement { get; }   // Overlay Settlement일 때, 그 밖 null
    public FlowMessage Message { get; }                  // 가장 최근 메시지 (Serial 증가로 새 메시지 감지)
    public SheetChange LastSheetChange { get; }
    public bool WalkHintVisible { get; }                 // Walking ∧ !HasSat
    public bool TakeSheetHintVisible { get; }            // Seated ∧ Sheet == null ∧ FreeSheetAvailable (런의 첫 종이 전)
    public bool PostTutorialHintVisible { get; }         // 튜토리얼 종이 Finish/Discard 직후 ~ 다음 TakeSheet·OpenBook·Interact(bookshelf) 수락까지

    public bool ProcessKeys(GameKeys keys, string interactCandidate);   // 아래 표. 무언가 했으면 true
    // 게임 명령 (Overlay ≠ None이면 Core를 부르지 않고 WrongMode 거부 — A2-11). 반환은 Core 결과 그대로.
    public CommandResult Interact(string target); public CommandResult StandUp(); public CommandResult ToggleBook();
    public CommandResult OpenBook(); public CommandResult CloseBook(); public CommandResult TurnPage(int page); public CommandResult TurnPageBy(int delta);
    public CommandResult BuyPage(string modelId); public CommandResult CloseCounter(); public CommandResult Sleep(bool yes);
    public CommandResult TakeSheet(); public CommandResult Fold(Vec2 grab, Vec2 drop, bool mountain, int layers);
    public CommandResult Crease(Vec2 grab, Vec2 drop, int layers); public CommandResult Flip(); public CommandResult Undo();
    public CommandResult Unfold(); public CommandResult Finish(); public CommandResult Discard(); public CommandResult SkipGuide();
    // 덮개 조작
    public void OpenPause();             // Walking ∧ Overlay None일 때만
    public void ClosePause();            // PauseMenu/ConfirmNewRun → None
    public void RequestNewRun();         // PauseMenu → ConfirmNewRun
    public void ConfirmNewRun();         // ConfirmNewRun 또는 Result → NewRun, Overlay None
    public void AcknowledgeSettlement(); // Settlement → 종료면 Result, 아니면 None
}
```

- 메시지: 거부 결과 중 `GameTexts.RejectMessage`가 빈 문자열이 아닌 것 → (Reject, 문구). `Finish` 수락 → (Recognition, `RecognitionLine`). 그 밖 명령은 메시지를 바꾸지 않는다.
- `Sleep(yes)` 수락 → Overlay Settlement, PendingSettlement 설정. `NewRun` → Overlay None, SheetChange Reset.
- 종이 변화(`LastSheetChange`, Serial +1): TakeSheet → Taken, Fold → Folded(Plan), Crease → Creased(Plan), Flip → Flipped, Undo → Undone, Unfold → Unfolded, Finish/Discard → Removed, NewRun → Reset. `Previous` = 명령 직전 종이 상태(없으면 null).
  `FoldHostAdapter`는 Folded/Creased/Flipped를 같은 이름으로, 나머지를 `Replaced`로 옮긴다.
- `ProcessKeys`: 눌린 키 중 우선순위 **Escape > Enter > Interact > TakeSheet > Discard > Undo > Unfold > Flip > Book > SkipGuide > StandUp > PageLeft > PageRight**의 가장 높은 키 하나만 본다. 그 키가 현재 상황에서 아무 의미가 없으면 그 프레임은 아무 일도 없다(false).

| 상황 | Escape | Enter | Interact (E) | TakeSheet (Space) | Discard (X) | Undo (Z/⌫) | Unfold (R) | Flip (F) | Book (B) | SkipGuide (G) | StandUp (Q) | PageLeft / PageRight (← / →) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Overlay Settlement | — | AcknowledgeSettlement | — | — | — | — | — | — | — | — | — | — |
| Overlay Result (또는 Mode Ended) | — | ConfirmNewRun | — | — | — | — | — | — | — | — | — | — |
| Overlay PauseMenu | ClosePause | RequestNewRun | — | — | — | — | — | — | — | — | — | — |
| Overlay ConfirmNewRun | ClosePause | ConfirmNewRun | — | — | — | — | — | — | — | — | — | — |
| Walking | OpenPause | — | Interact(candidate) (null이면 —) | — | — | — | — | — | — | — | — | — |
| Seated | — (끌기 취소는 Runtime) | Finish | — | TakeSheet | Discard | Undo | Unfold | Flip | ToggleBook | SkipGuide | StandUp | 책 펼침이면 TurnPageBy(∓2) |
| Bookshelf | CloseBook | — | CloseBook | — | — | — | — | — | CloseBook | — | — | TurnPageBy(∓2) |
| Counter | CloseCounter | — | CloseCounter | — | — | — | — | — | — | — | — | — |
| SleepConfirm | Sleep(false) | Sleep(true) | — | — | — | — | — | — | — | — | — | — |

(— = 의미 없음. 거부된 명령도 "무언가 했음"으로 true를 반환하고 메시지 규칙을 따른다.)

---

## C-180 방 (`Origami.Core.Room`) — R-004, F-002, CONTENT §6 (값은 DATA_STRUCTURE D-130)

```csharp
public readonly struct Box { public double MinX { get; } public double MinZ { get; } public double MaxX { get; } public double MaxZ { get; } public Box(double minX, double minZ, double maxX, double maxZ); }
public sealed class FurnitureSpec { public string Id { get; } public Vec2 Center { get; } public double Width { get; } public double Depth { get; } public double Height { get; } public bool Blocks { get; } }
public static class RoomLayout
{
    public const double MinX = -4, MaxX = 4, MinZ = -3, MaxZ = 3, WallHeight = 2.5;
    public const double CharacterRadius = 0.3, WalkSpeed = 3.0, InteractRange = 1.2, CharacterHeight = 1.6;
    public static Vec2 StartPosition { get; }        // (−1.6, 0.0), 동쪽(+x)을 봄
    public static Vec2 ChairPosition { get; }        // (2.5, 0.8) 앉은 위치
    public static Vec2 StandUpPosition { get; }      // (2.5, 0.2), 남쪽을 봄
    public static IReadOnlyList<FurnitureSpec> Furniture { get; }
    public static IReadOnlyList<Box> Obstacles { get; }      // Blocks인 가구의 바닥 상자
    public static Vec2 InteractPoint(string target);         // 모르는 대상 → ArgumentException
}
public static class RoomMotion
{
    public const double MaxSubstep = 0.05;
    public static Vec2 Step(Vec2 position, Vec2 move, double dt);
}
public static class Interaction
{
    public static string Nearest(Vec2 position);   // 범위 안 대상 ID, 없으면 null
}
```
- `Step`: dt ≤ 0 → position. move 성분을 [−1, 1]로 자르고 길이 > 1이면 정규화(대각선 같은 속도), 길이 < 1e-6 → position. dt를 ⌈dt / MaxSubstep⌉개 같은 조각으로 나눠 각 조각에서
  x를 먼저 `x + m.X·WalkSpeed·조각` 으로 옮기고 벽 [MinX + r, MaxX − r]로 자른 뒤, 장애물을 r만큼 넓힌 상자 안(경계 제외)에 들면 이동 방향의 상자 면(minX − r 또는 maxX + r)으로 되돌린다. 그다음 z를 같은 방식으로.
- `Nearest`: `InteractTargets.All` 순서로 거리 d를 구해 k = `Math.Round(d, 9, MidpointRounding.ToEven)` ≤ 1.2인 것 중 k 최소, 같으면 앞 순서(table > bookshelf > counter > bed). (Room은 Folding을 참조하지 않는다.)
- 기대: F-002 AC-02/03/04/04b의 수치가 `Step`만으로 나온다(dotnet). 예: 시작에서 move (1,0), dt 1.0 → x = 1.4. (−1.6, 2.0)에서 (−1,0) 2초 → x = −2.2.

---

## C-190 Systems (`Origami.Runtime.Systems`)

```csharp
public static class Materials
{
    public static Material Lit(Color color);     // "Legacy Shaders/Diffuse" → "Standard" → Unlit 셰이더 (사용 가능 검사, 로그 없음)
    public static Material Unlit(Color color);   // "Sprites/Default" → "UI/Default" → "Legacy Shaders/Diffuse"
}
public static class MeshFactory
{
    public static Mesh Box(Vector3 size);                      // 중심 원점
    public static Mesh Cylinder(float radius, float height, int segments = 24);   // 바닥 중심 원점
    public static Mesh Sphere(float radius, int segments = 16);
    public static Mesh Capsule(float radius, float height, int segments = 16);    // 캐릭터 몸
}
```
- `GameObject.CreatePrimitive`·Collider·Physics 금지(ARCHITECTURE §7). 만든 Mesh/Material은 만든 컴포넌트가 `OnDestroy`에서 파괴.

## C-191 접기 테이블 (`Origami.Runtime.Folding`) — F-005, F-006 AC-09

```csharp
public struct PointerFrame
{
    public bool HasPaperPoint; public Vector2 PaperPoint;         // 마우스가 가리키는 종이 평면 위 점 (종이 좌표). OrigamiApp이 ScreenToPaper로 채우거나 테스트가 직접 넣음
    public bool LeftDown, LeftHeld, LeftUp;                       // 왼쪽 버튼 (이번 프레임 눌림 / 누르고 있음 / 뗌)
    public bool RightHeld; public Vector2 RightDragDelta;         // 시점 회전 (픽셀)
    public float Wheel;                                           // mouseScrollDelta.y
    public bool LayerUp, LayerDown;                               // ] / [
    public bool Shift, Ctrl, Escape;
    public bool OverGui;                                          // 마우스가 IMGUI 버튼·책 위 (잡기·확대 금지)
}
public readonly struct FoldTableGuide { public Vec2 Grab { get; } public Vec2 Drop { get; } public Vec2 LineA { get; } public Vec2 LineB { get; } public bool IsCrease { get; } public FoldTableGuide(Vec2 grab, Vec2 drop, Vec2 lineA, Vec2 lineB, bool isCrease); }
public enum FoldTableChangeKind { None = 0, Replaced, Folded, Creased, Flipped }

public interface IFoldTableHost
{
    PaperState Sheet { get; }                      // 테이블 위 종이, 없으면 null
    bool InputEnabled { get; }                     // false면 FoldTable은 포인터를 무시하고 끌기를 취소 (앉음이 아님·덮개 열림)
    bool TryGetGuide(out FoldTableGuide guide);    // 튜토리얼 가이드가 보일 때만 true
    int ChangeSerial { get; }                      // 종이가 바뀔 때마다 증가
    FoldTableChangeKind ChangeKind { get; }        // 마지막 변화 종류
    PaperState ChangePrevious { get; }             // 마지막 변화 직전 상태 (뒤집기 애니메이션용, 없으면 null)
    bool Fold(Vec2 grab, Vec2 drop, bool mountain, int layers, out FoldError error);
    bool Crease(Vec2 grab, Vec2 drop, int layers, out FoldError error);
}

public sealed class FoldTable : MonoBehaviour
{
    public const string ObjectName = "FoldTable";
    public const float PaperScale = 0.1f;                         // 종이 1 = 0.1m
    public const float FoldDuration = 0.4f, CreaseDuration = 0.6f, FlipDuration = 0.6f;
    public void Bind(IFoldTableHost host);                        // null 가능(연결 해제)
    public void SetTablePose(Vector3 tableTopCenter);             // 종이 루트 위치 (기본 (2.5, 0.75, 1.6))
    public void ProcessFrame(PointerFrame pointer, float dt);     // 입력·미리보기·애니메이션·메시 갱신. 자체 Update 없음
    public bool ScreenToPaper(Camera camera, Vector2 screenPosition, out Vector2 paperPoint);   // 광선–평면(맨 위 겹 높이)
    public void GetSeatedCameraPose(out Vector3 position, out Quaternion rotation);          // 궤도(오른쪽 끌기·휠 확대) 결과의 월드 자세
    public void FinishAnimationNow();
    public bool IsDragging { get; } public bool IsAnimating { get; }
    public FoldGesture Gesture { get; }                           // 끄는 중이면 현재 끌기, 아니면 null
    public string CursorLabel { get; }                            // 끄는 중: Ctrl이면 FoldTexts.CreaseLabel, 아니면 LayerLabel. 그 밖 ""
    public SnapKind CurrentSnap { get; }                          // 끄는 중 놓기 자석 종류 (초록 표시)
    public bool GuideMarkersVisible { get; }                      // 노란 공·초록 원·빨간 점선이 렌더링 중 (host 가이드가 있을 때만)
    public int PaperVertexCount { get; }                          // 정적 종이 메시 꼭짓점 수 (테스트용)
    public int CreaseLineCount { get; }                           // 그려진 자국 선분 수 (MarkPieces 중 보이는 부분)
    public FoldError LastError { get; }                           // 마지막으로 놓았는데 무효였던 사유 (토스트용), 없으면 None
}
```
- 끌기: `LeftDown ∧ HasPaperPoint ∧ !OverGui ∧ InputEnabled ∧ !IsAnimating` → `FoldGesture.TryBegin`. 끄는 동안 `MoveTo`(가이드가 있으면 guideDrop 판), 휠/[ ] → `CycleLayers(±1)`(A2-8), 미리보기 = `Preview(sheet, Shift, Ctrl)`.
  `LeftUp` 또는 `!LeftHeld` → 미리보기가 유효하면 host.Fold/Crease(같은 인자), 아니면 LastError. `Escape` → 끌기 취소(host 호출 없음).
- host의 `ChangeSerial`이 바뀌면: 자기가 낸 Fold/Crease의 결과면 그 계획으로 애니메이션(접기 0.4초, 접었다 펴기 0.6초 = 접혔다 펴짐), `Flipped`면 `ChangePrevious`에서 0.6초 뒤집기, 그 밖은 즉시 다시 그림.
- 012 PlayMode 테스트는 `FoldSession`을 감싼 테스트 전용 host(테스트 폴더 안)로 `ProcessFrame`을 직접 부른다(프레임 대기 없음, dt를 넘김).

```csharp
public static class PaperMeshBuilder   // Fold3DSpike PaperMeshBuilder/OverlayBuilder/SphereMesh 이식 (네임스페이스 변경, Build 인자 형식 1곳 변경 + 자국 선 그리기)
{
    public const float BaseY = 0.003f, LayerGap = 0.0035f, Thickness = 0.002f;
    public static float LayerTop(int layer); public static float LayerBottom(int layer);
    public static Vector3 Identity(double x, double z, float y);
    public static Mesh NewMesh(string name);
    public static float Build(Mesh mesh, IReadOnlyList<Facet> facets, VertexXform xf, bool topOnly, bool edges, float yOffset, Color edgeColor);
    //   시험판은 IList<Facet>. Core v2가 IReadOnlyList<Facet>를 내주므로 IReadOnlyList로 바꿈 (ORIGAMI-012 N-2). 동작 같음:
    //   facets(null = 빈 메시)를 xf(null = Identity)로 옮겨 채우고 가장 낮은 꼭짓점 y(비면 0)를 돌려줌. mesh null → ArgumentNullException
    // 추가:
    public static void AddCreaseLines(OverlayBuilder overlay, PaperState state, float halfWidth, Color color);   // DropSnap.MarkPieces의 보이는 부분만
}
```

## C-192 책 화면 (`Origami.Runtime.Book`) — F-007, F-008

```csharp
public interface IBookHost
{
    int CurrentPage { get; }                         // 펼침 왼쪽(짝수) 쪽
    IReadOnlyList<string> OwnedModelIds { get; }
    int Money { get; }
    void TurnPage(int page);                         // ScreenFlow.TurnPage
    void BuyPage(string modelId);                    // ScreenFlow.BuyPage
}
public sealed class BookPanel : MonoBehaviour
{
    public void Bind(IBookHost host);
    public void Prepare();                           // 현재 펼침의 BookPage 2개와 텍스처를 준비 (OnGUI 밖에서 호출 가능, 캐시)
    public void Draw(Rect area);                     // OnGUI 안에서만. 두 쪽·목차 버튼·사기 버튼·← → 버튼
    public BookPage LeftPage { get; } public BookPage RightPage { get; }     // 마지막 Prepare 결과 (오른쪽이 없으면 null — 43쪽 다음)
    public Texture2D DiagramTexture(int page);       // 그 쪽의 도해/실루엣 텍스처, 그림 없는 쪽 null
    public void ClickContents(string modelId);       // 목차 항목 버튼과 같은 동작: host.TurnPage(CoverPage)
    public void ClickBuy(string modelId);            // 사기 버튼과 같은 동작: host.BuyPage
    public void ClickTurn(int delta);                // ← → 버튼과 같은 동작: host.TurnPage(CurrentPage + delta)
}
public static class DiagramRaster
{
    public static Texture2D Render(StepDiagram diagram, int size);    // 직전 상태(겹 순서대로 앞면 색/뒷면 흰색, 경계선, 자국 선) + 접는 선(골/산/접었다 펴기) + 화살표 + "위 n겹" + 기준점 원
    public static Texture2D RenderShape(PaperState shape, double rotationDeg, bool grey, int size);   // 표지 실루엣·완성 그림
}
```
- 버튼 핸들러는 `Click…` 메서드만 부른다(테스트가 같은 메서드를 직접 호출). 텍스처는 `(쪽, 보유 목록)` 키로 캐시하고 `OnDestroy`에서 파괴.
- 의존(ORIGAMI-013 구현, ARCHITECTURE §2): `Origami.Core.Book`·`Catalog`·`Folding`·`Geometry`(도해 래스터화)와 `Origami.Runtime.UI.KoreanFont`(글꼴, `Draw` 안에서만).
  `Origami.Core.Game`·`Screens`는 모른다 — 상태는 `IBookHost`로만 받는다.
- 책 화면 표시 문구는 `internal static class BookTexts`(`Scripts/Book/`)에 있다(DECISIONS, ORIGAMI-013 W-1). CONTENT §3 고정 문장
  (`봉인됨 — 표지에서 페이지를 사세요`, `판매대에서 기본 N원`, 목차 `보유` / `N원`)은 명세를 그대로 복사하고 PlayMode 테스트가 자기 문자열로 확인한다.
  그 밖 라벨(`난이도 ★…`, `동작 N번`, `기법: …`, `기본가 N원`, `페이지 가격 N원`, `가진 돈 N원`, 접기 종류·`위 n겹` 표기, 기호 설명, `← 앞`/`뒤 →`, `사기`, `목차`, `접기 기호`, `이름 완성`, 쪽 번호)은 표시 전용이다.
  게임 규칙 문구(거부·판정·정산·HUD)는 계속 Core `GameTexts`/`FoldTexts`(C-170, C-108).

## C-193 공방 통합 (`Origami.Runtime.Gameplay`)

```csharp
public struct InputFrame
{
    public Vector2 Move;              // x = 동(D/→ +1, A/← −1), y = 북(W/↑ +1, S/↓ −1), 반대 키 동시 = 0. 창 포커스 없으면 (0,0)
    public GameKeys Keys;             // 이번 프레임 GetKeyDown: Esc, Return/KeypadEnter, E, Space, X, Z/Backspace, R, F, B, G, Q, ←, →
    public PointerFrame Pointer;
}
public static class InputReader { public static InputFrame Read(OrigamiApp app); }   // 레거시 Input만. 포인터의 종이 점은 app.Table.ScreenToPaper

public sealed class OrigamiApp : MonoBehaviour
{
    public const string ObjectName = "[Origami]";
    public static OrigamiApp Instance { get; }                 // 진짜 null 정규화
    public static OrigamiApp EnsureInstance();                 // AfterSceneLoad 훅에서 호출, 멱등
    public ScreenFlow Flow { get; }                            // Awake에서 생성, 교체 없음
    public FoldTable Table { get; } public BookPanel Book { get; }
    public CharacterView Character { get; } public CameraRig CameraRig { get; } public WorkshopView Workshop { get; }
    public Vector2 CharacterPosition { get; }                  // 월드 (x, z)
    public float CharacterYaw { get; }                         // 도, 0 = 북(+z), 90 = 동(+x)
    public string InteractCandidate { get; }                   // Walking일 때 Interaction.Nearest, 그 밖 null
    public void ProcessFrame(InputFrame frame, float dt);      // Update는 ProcessFrame(InputReader.Read(this), Time.deltaTime)만
    public void SetCharacterPosition(Vector2 xz, float yaw);   // 테스트·배치용
}
public sealed class FoldHostAdapter : IFoldTableHost { public FoldHostAdapter(ScreenFlow flow); }   // InputEnabled = Mode Seated ∧ Overlay None
public sealed class CameraRig : MonoBehaviour
{
    public static readonly Vector3 WalkOffset = new Vector3(0f, 6.0f, -5.5f), LookOffset = new Vector3(0f, 0.8f, 0f);
    public const float FollowSharpness = 8f, SitTransition = 0.5f, WalkNearClip = 0.1f, SeatedNearClip = 0.01f;
    public void Tick(float dt);                                // 걷기: 위치 += (목표 − 위치)·(1 − e^(−8·dt)). 앉음 전환: 0.5초 smoothstep 후 FoldTable 자세 그대로
    public bool InSeatedView { get; }                          // 전환이 끝나 접기 시점에 있음
}
public sealed class CharacterView : MonoBehaviour { public void SetPose(Vector2 xz, float yaw, bool sitting, float walkPhase); }
public sealed class WorkshopView : MonoBehaviour { public void Refresh(GameSnapshot snapshot); public int DisplayedPieceCount { get; } public int TrashBallCount { get; } }
```
- `ProcessFrame` 순서: (1) 덮개가 없고 `Table.IsDragging`이면 키를 넘기지 않음, 아니면 `Flow.ProcessKeys(frame.Keys, InteractCandidate)` (2) Mode 변화 처리(앉으면 의자 위치, 일어서면 (2.5, 0.2) 남쪽, 정산·새 런이면 시작 위치) (3) Walking ∧ Overlay None이면 `RoomMotion.Step` (4) `Table.ProcessFrame` (5) `CameraRig.Tick` (6) `Workshop.Refresh`(스냅샷 참조가 바뀌었을 때).
- 이 순서와 `dt` 주입 때문에 PlayMode 테스트는 `ProcessFrame`을 정해진 dt(예: 0.02초 × 50번 = 1초)로 불러 F-002 수치를 검증한다.

## C-194 `OrigamiGui` (`Origami.Runtime.UI`) — 유일한 `OnGUI`

```csharp
public sealed class OrigamiGui : MonoBehaviour
{
    public void Bind(OrigamiApp app);
    public IReadOnlyList<string> VisibleLines { get; }   // 마지막 Refresh의 화면 문구(HUD·안내·토스트·덮개) — 테스트용
    public void Refresh(float dt);                         // OrigamiApp.ProcessFrame 끝에서 호출: 문구 다시 만들기, 토스트 시간(2초) 진행
    public bool PointerOverGui(Vector2 screenPosition);    // 직전 Repaint의 버튼·책 영역 (PointerFrame.OverGui)
}
```
- `OnGUI` 그리기 순서: 걷기/앉음 HUD → 앉음 버튼(종이 꺼내기·완성·버리기·되돌리기·처음으로·뒤집기·책·일어나기, 튜토리얼 중 가이드 끄기) → 책(앉음+펼침: 왼쪽 45%, 책장: 전체) → 커서 옆 `Table.CursorLabel` → 토스트 → 덮개(판매대·잠자기 확인·정산·결과·일시정지).
  버튼은 `ScreenFlow`의 같은 메서드만 부른다(키와 같은 명령). 한글 글꼴 `KoreanFont.Get`(v1 그대로), 기준 해상도 1280×720 + `GUI.matrix` 배율.

---

## C-195 아트 에셋 파이프라인 (Editor `Origami.EditorTools.ArtImportRules`, ORIGAMI-017) — ARCHITECTURE §14

코드 API가 아니라 **가져온 결과에 대한 약속**이다. Runtime(019)과 테스트는 이것에만 기대고 `ArtImportRules`를 참조하지 않는다(Editor 전용 어셈블리).

| 약속 | 내용 |
|---|---|
| 로드 | `Resources.Load<GameObject>("<Set>/<Name>")` = 모델 프리팹(루트 GameObject, 자식에 `MeshFilter`+`MeshRenderer` 또는 `SkinnedMeshRenderer`). `Resources.Load<Texture2D>("<Set>/<Name>")` = 텍스처(Default 형식·Sprite 아님, 밉맵, Repeat, Bilinear, aniso 2, ≤ 1024 px) |
| 머티리얼 | 모든 슬롯이 모델의 하위 에셋, 셰이더 "Standard", `_Metallic` 0, `_Glossiness` = 0.15(`ArtImportRules.Smoothness`). 색은 원본 파일의 확산색 |
| 크기·원점 | 파일 단위 → 미터 그대로(배율 1). 원점·크기는 키트마다 다름 → 호출자가 `Renderer.bounds`로 맞춘다 |
| 물리 | Collider 없음, 카메라·조명 없음 |
| 애니메이션 | 애니메이션이 있는 모델은 루트에 `Animation`(Legacy), `Animator` 없음. 클립은 `clip.legacy == true`, 이름은 take 이름의 마지막 `|` 뒤(`"Walk"`, `"Idle"`). 재생 방법은 ARCHITECTURE §14.6 |
| 샘플 키 | `_PipelineSample/Chair`(정적), `_PipelineSample/CasualMale`(애니메이션: Idle, Walk, SitDown, StandUp, PickUp, Punch, Defeat, RecieveHit, Shoot_OneHanded, Victory, Walk_Carry), `_PipelineSample/PaperSwatch`(256×256 텍스처) |

검증: `Tests/PlayMode/AssetPipeline/AssetPipelineTests`(CI).

## 변경 이력

| 날짜 | 작업 | 내용 |
|---|---|---|
| 2026-11-27 | ORIGAMI-002 | v1 계약 C-001~C-031 |
| 2026-11-27 | ORIGAMI-008 | v2 계약 C-101~C-194로 전면 교체. v1 계약은 폐기(코드는 ORIGAMI-014에서 삭제) |
| 2026-11-27 | ORIGAMI-016 | 구현에 맞춘 문서 보완(코드·시그니처 변경 없음): C-150 NaN·무한대 좌표 → InvalidFold(NothingToFold); C-170 NoSheet 무음, 정산·판매대·결과 줄 형식과 "★n" 표기; C-191 `PaperMeshBuilder.Build`는 `IReadOnlyList<Facet>`; C-192 의존·`BookTexts` |
| 2026-11-28 | ORIGAMI-017 | C-195 아트 에셋 파이프라인(가져온 결과의 약속: Resources 키, 머티리얼, Legacy 클립 이름). 기존 계약 변경 없음 |
