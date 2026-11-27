# 에이전트 게임 스튜디오 설계 (A안)

작성일: 2026-11-27
상태: 초안 (사용자 승인 대기)

## 1. 의도

Claude Code 에이전트 팀으로 **Unity 인디 게임을 여러 개 연속으로 만들어 나가는 것**이 목적이다.
원본 설계(`docs/reference/original-agent-design.pdf` — ChatGPT 답변: Orchestrator + 전문 Agent 9개, Python Orchestrator, SQLite, Git Worktree)의
핵심 원칙은 유지하되, "에이전트 시스템을 만드는 일"이 "게임을 만드는 일"보다 커지지 않도록
Claude Code의 기본 기능으로 대체할 수 있는 것은 전부 대체한다.

원본에서 유지하는 핵심 원칙:
1. 자율 수행 — 일상적인 구현 승인은 사용자에게 묻지 않는다.
2. 충돌 방지 — 작업마다 수정 가능한 파일 범위(`file_scope`)를 정한다.
3. 독립 검증 — 구현과 리뷰/QA를 다른 에이전트가 한다.
4. 완료 기준 — 객관적 검증(테스트 결과) 없이 완료 처리하지 않는다.

## 2. 목표 / 비목표 / 반목표

**목표**
- 저장소 루트에 모든 게임이 공유하는 에이전트 팀(규칙 + 에이전트 정의 + 작업 템플릿)을 둔다.
- 새 게임은 `Games/<게임이름>/` 아래에 스크립트 한 번으로 시작할 수 있다.
- 게임 핵심 로직은 Unity 없이 클라우드 세션에서도 에이전트가 직접 테스트할 수 있다.
- Unity 쪽 검증(컴파일, EditMode/PlayMode 테스트, 빌드)은 사용자 PC에서 스크립트 한 번으로 돌릴 수 있고,
  선택적으로 GitHub Actions(GameCI)에서도 돌릴 수 있다.

**비목표**
- 별도 Orchestrator 프로그램(Python/Node), SQLite 상태 저장소, 멀티 LLM 실행 추상화 계층.
- 외부 스토어 배포 자동화.
- 첫 번째 실제 게임의 기획 — 이 설계는 "팀"까지이고, 게임은 팀이 만든다.

**반목표 (기술적으로 돌아가도 실패로 보는 것)**
- 에이전트가 테스트를 돌리지 않았거나 실패했는데 작업을 완료로 표시하는 것.
- 에이전트가 자기 `file_scope` 밖의 파일을 고치는 것.
- Unity 검증이 안 된 작업을 "Unity에서도 된다"고 보고하는 것.
- 규칙 문서와 작업 관리가 너무 무거워서 작은 기능 하나에도 의식(ceremony)이 과도한 것.

## 3. 제약

- 클라우드 세션 컨테이너에는 Unity 에디터/라이선스가 없다. 현재 `dotnet`도 설치되어 있지 않다
  (패키지 레지스트리 접근은 가능하므로 설치는 빌더가 해결한다).
- 사용자는 PC에서 Unity를 실행한다.
- 저장소(`junho5408/second_game`)는 비어 있다. 개발 브랜치는 `claude/serene-fermat-4bwjlo`.

## 4. 고려한 방안

| 방안 | 내용 | 판단 |
|---|---|---|
| **A (선택)** | Claude Code 기본 기능 + 6인 팀. 메인 세션이 Orchestrator, 작업은 파일(JSON), 병렬 격리는 Claude Code worktree 기능, 핵심 로직은 순수 C#로 분리해 `dotnet test`로 검증 | 게임을 바로 만들려는 목적에 맞고 가장 가볍다 |
| B | 원본 그대로 10인 팀을 Claude Code 에이전트로 구현 | 소규모 게임엔 역할이 너무 잘게 나뉘어 전달 비용만 늘어난다 |
| C | 원본대로 Python Orchestrator + SQLite + Agent Runtime 직접 구현 | 에이전트 시스템 자체가 프로젝트가 되어 게임 개발이 늦어진다 |

사용자가 A안을 선택했다.

## 5. 설계

### 5.1 팀 구성 (Orchestrator + 6)

| 에이전트 | 원본 대응 | 책임 | 쓰기 권한 |
|---|---|---|---|
| Orchestrator (메인 세션) | Orchestrator | 요청 분석, 작업 분해(DAG), 배정, 병렬 판단, 결과 검증, 병합, 재시도/재배정 | `Tasks/`, `Docs/PROGRESS.md`, `Docs/DECISIONS.md`. 소스 코드 직접 수정 금지 |
| `game-designer` | Game Planner | 콘셉트, 규칙, 기능·콘텐츠 명세, 수용 기준 | `Docs/design/` |
| `architect` | Game Architect | 모듈 경계, 인터페이스 계약, 데이터 구조, 코딩 규칙, 패키지/ProjectSettings | `Docs/tech/`, 공유 인터페이스 파일, `Packages/`, `ProjectSettings/` |
| `developer` | Gameplay/System/UI Developer | 기능 구현 + 해당 테스트. 작업마다 `file_scope`로 영역 제한 | 작업의 `file_scope`만 |
| `content-designer` | Content/Asset Designer | ScriptableObject 데이터, 프리팹, 아트/오디오 연결 | `Data/`, `Art/`, `Audio/`, 지정된 프리팹 |
| `code-reviewer` | Code Reviewer | 원본 체크리스트 8항목으로 리뷰, 상태 4종(APPROVED / APPROVED_WITH_WARNINGS / CHANGES_REQUESTED / BLOCKED) | `Reports/review/`만. 코드 수정 금지 |
| `qa-engineer` | QA Engineer + Build/Release | 명세 대비 검증, 엣지 케이스/회귀 테스트 작성·실행, 버그 리포트(원본 양식), Unity 검증 결과 판정 | `Tests/` 계열, `Reports/qa/`. 제품 코드 수정 금지 |

에이전트 정의는 `.claude/agents/*.md`(Claude Code 서브에이전트 형식)로 두고,
원본 PDF의 역할별 상세 프롬프트(책임 / 규칙 / 제한 / 완료 기준)를 압축해 옮긴다.
리뷰어와 QA는 쓰기 도구를 제한해 "코드 수정 금지"를 도구 수준에서도 강제한다.

### 5.2 저장소 구조

```
/
├── AGENTS.md                  # 모든 에이전트 공통 규칙 (원본 8장 기반)
├── CLAUDE.md                  # Orchestrator(메인 세션) 지침 + @AGENTS.md 포함
├── .claude/agents/            # 6개 에이전트 정의
├── studio/
│   ├── templates/             # 새 게임용 문서·작업 템플릿, task 스키마
│   └── scripts/               # new-game, verify-core, verify-unity(로컬), task 상태 도구
├── .github/workflows/         # core 테스트(항상), Unity 테스트/빌드(시크릿 있을 때만)
└── Games/
    └── <GameName>/
        ├── Docs/
        │   ├── design/        # GAME_DESIGN, GAME_RULES, FEATURE_SPECS, CONTENT_SPECS
        │   ├── tech/          # ARCHITECTURE, INTERFACE_CONTRACTS, DATA_STRUCTURE, CODING_CONVENTION
        │   ├── DECISIONS.md
        │   └── PROGRESS.md
        ├── Tasks/             # GAME-001.json ... (상태는 파일 안의 status 필드)
        ├── Reports/{review,qa,build}/
        ├── Core.Tests/        # dotnet 테스트 프로젝트 (Unity 없이 실행)
        └── Unity/             # Unity 프로젝트
            └── Assets/Game/
                ├── Core/      # 순수 C# (asmdef: noEngineReferences=true)
                ├── Scripts/{Gameplay,Systems,UI}/   # Unity 의존 코드 (MonoBehaviour 등)
                ├── Data/ Art/ Audio/ Prefabs/ Scenes/
                └── Tests/{EditMode,PlayMode}/
```

### 5.3 핵심 로직 분리 (가장 중요한 변경)

- 규칙·전투·인벤토리·턴 진행 등 게임 로직은 `Assets/Game/Core/`에 **UnityEngine 참조 없이** 작성한다.
  Unity에서는 `noEngineReferences: true`인 asmdef로 컴파일된다.
- `Core.Tests/`의 .NET 테스트 프로젝트가 같은 소스 파일을 링크(glob include)해서 `dotnet test`로 실행한다.
  → 소스는 한 벌, 테스트는 Unity 없이 클라우드에서 실행 가능.
- C# 언어 버전/타깃은 Unity가 지원하는 범위(netstandard2.1, C# 9)에 맞춘다.
- `Scripts/`는 Core를 호출해 화면·입력·연출만 담당하는 얇은 층으로 유지한다.
- 결과적으로 대부분의 작업은 클라우드에서 끝까지 검증되고, Unity 검증은 연결부·씬·UI에 한정된다.

### 5.4 작업(Task) 데이터

원본 4-2의 JSON 구조를 그대로 쓴다. 필드:
`task_id, title, description, agent, priority, status, dependencies, file_scope, deliverables,
acceptance_criteria, verification, retry_count, notes, result`.

`verification` 항목은 다음 중에서 고른다:
`core-tests`(클라우드), `review`(클라우드), `unity-compile`, `unity-editmode`, `unity-playmode`, `unity-build`(로컬/CI).

상태: `BACKLOG → ASSIGNED → IN_PROGRESS → REVIEW → QA → COMPLETED`,
그 외 `FIX_REQUIRED`(→ IN_PROGRESS 복귀), `BLOCKED`, `CANCELLED`,
그리고 A안 추가 상태 **`AWAITING_UNITY`**: 클라우드 검증은 모두 통과했지만
Unity 검증 항목이 아직 실행되지 않은 상태. 사용자가 로컬 스크립트나 CI 결과를 넘겨줘야 COMPLETED가 된다.

### 5.5 작업 흐름

원본의 Scan → Plan → Implement → Verify → Review → Fix → Complete를 따른다.

1. **Scan/Plan** — Orchestrator가 요청과 해당 게임의 `Docs/`를 읽고 작업을 DAG로 분해해 `Tasks/`에 쓴다.
   명세가 없으면 `game-designer` → `architect` 작업이 먼저 온다(인터페이스 확정 전 병렬 구현 금지).
2. **Implement** — 의존성이 풀리고 `file_scope`가 겹치지 않는 작업은 병렬로 실행한다.
   병렬 실행 시 각 에이전트는 Claude Code의 worktree 격리로 돌고, Orchestrator가 결과 브랜치를 병합한다.
   같은 씬/프리팹/공유 인터페이스/ProjectSettings를 건드리는 작업은 직렬.
3. **Verify** — 구현 에이전트가 `core-tests`를 실행하고 결과를 작업 파일의 `result`에 기록.
   (예외 규칙: 각 에이전트는 **자기에게 배정된 작업 파일의 `status`/`result` 필드만** 갱신할 수 있다.
   작업 생성·배정·다른 필드 변경은 Orchestrator만 한다.)
4. **Review** — `code-reviewer`가 리포트 작성. CHANGES_REQUESTED/BLOCKED면 FIX_REQUIRED.
5. **QA** — `qa-engineer`가 수용 기준 대비 검증, 필요한 테스트 추가, 버그는 원본 양식으로 리포트.
6. **Fix** — 실패는 원래 담당 에이전트에게 재배정. 같은 원인으로 최대 2회, 이후 원인 분석 후 재배정 또는 BLOCKED
   (리뷰 반복 실패 → architect 재검토, QA 반복 실패 → designer 범위 재검토. 원본 7-2).
7. **Complete** — Unity 검증 항목이 없으면 COMPLETED, 있으면 AWAITING_UNITY로 두고 사용자에게
   실행할 명령을 한 줄로 알려준다. `PROGRESS.md`와 `DECISIONS.md` 갱신, 작업 단위 커밋.

### 5.6 Unity 검증

- **로컬(기본)**: `studio/scripts/verify-unity` — Unity를 batchmode로 실행해 컴파일, EditMode, PlayMode,
  (옵션) 빌드를 순서대로 돌리고 결과를 `Reports/build/`에 XML/로그로 남긴다.
  사용자는 결과를 커밋/푸시하거나 대화에 붙여 넣고, `qa-engineer`가 판정해 AWAITING_UNITY를 해소한다.
- **CI(선택)**: GameCI(`game-ci/unity-test-runner`, `game-ci/unity-builder@v4`) 워크플로를 넣되,
  `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` 시크릿이 없으면 Unity 잡은 건너뛴다.
  `dotnet test` 잡은 시크릿과 무관하게 항상 돈다.

### 5.7 새 게임 시작

`studio/scripts/new-game <GameName>`이 `Games/<GameName>/`에 문서 템플릿, 빈 `Tasks/`,
`Core.Tests` 프로젝트, 최소 Unity 프로젝트 골격(Assets/Game 구조, asmdef, `Packages/manifest.json`,
`ProjectSettings/ProjectVersion.txt`)을 만든다. 나머지 Unity 생성 파일은 사용자가 PC에서 처음 열 때 생성되고,
`.meta` 파일과 함께 커밋한다. `.gitignore`/`.gitattributes`(Force Text, LFS 대상)도 포함한다.

### 5.8 안전장치 (원본 9장 축약)

- 에이전트는 자기 `file_scope` 밖 수정 금지, 테스트 우회 금지, 강제 푸시 금지.
- **범위 강제 방식 (결정)**: 훅으로 막지 않고 규칙 + 기계적 확인으로 한다.
  `studio/scripts/check-scope <task_id>`가 작업 브랜치의 git diff를 해당 작업의 `file_scope`와 비교해
  벗어난 파일을 출력한다. `code-reviewer`는 리뷰 시작 시 반드시 이를 실행하고, 위반이 있으면 BLOCKED.
  (PreToolUse 훅 방식은 "현재 어떤 작업이 실행 중인지"를 추적하는 장치가 필요해 의식이 무거워지므로 제외.)
  리뷰어는 Write/Edit 도구 자체를 주지 않고, QA는 도구는 주되 규칙 + check-scope로 제한한다.
- 패키지 추가·ProjectSettings 변경은 architect 작업으로만.
- 파일 삭제는 작업에 명시된 경우만.
- 비밀 정보(라이선스, 비밀번호)는 저장소에 절대 쓰지 않는다 — GitHub 시크릿으로만.

## 6. 빌더에게 맡기는 것

(원본 PDF의 리뷰 체크리스트, 버그 리포트 양식, AGENTS.md 초안, Task JSON, 7-2 처리표는
`docs/reference/original-agent-design.pdf`에서 그대로 옮긴다. `priority`는 critical/high/medium/low,
`result`는 `{verification 항목: pass|fail|pending, 근거 경로}` 객체 — 빌더 판단.)


- 에이전트 프롬프트의 구체적 문장, 템플릿 문서의 세부 항목.
- 스크립트 언어(bash + Windows용 PowerShell 병행 여부 포함), 테스트 프레임워크(xUnit/NUnit) 선택.
- `dotnet` SDK 설치 방법.
- 작업 상태 갱신을 손으로 할지 작은 스크립트로 할지.

## 7. 완료 기준 (이 설계의 구현이 끝났다고 보는 조건)

원본 10-1의 1단계 완료 조건을 A안에 맞게 바꾼다:
- 샘플 게임 하나를 `new-game`으로 만들고, 작은 기능 1개를 designer → architect → developer → reviewer → qa
  전 과정으로 돌려 COMPLETED(또는 AWAITING_UNITY)까지 도달한 기록이 `Tasks/`와 `Reports/`에 남는다.
- 그 과정에서 `dotnet test`가 클라우드에서 실제로 실행되어 통과한다.
- 사용자가 PC에서 `verify-unity`를 실행할 수 있는 안내가 README에 있다.
