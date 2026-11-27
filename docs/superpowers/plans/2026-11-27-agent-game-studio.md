# Agent Game Studio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 저장소 루트에 Orchestrator + 6 에이전트 팀과 도구를 만들고, 샘플 게임 하나로 기획→구현→리뷰→QA→GitHub Actions Unity 검증까지 한 바퀴를 실제로 돌린다.

**Architecture:** 메인 Claude Code 세션이 Orchestrator, `.claude/agents/*.md`가 전문 에이전트. 작업 상태는 `Games/<G>/Tasks/*.json`을 `studio/scripts/task.py`로 관리. 게임 로직은 UnityEngine 비참조 순수 C#(`Assets/Game/Core`)으로 두고 같은 테스트 소스를 dotnet과 Unity EditMode 양쪽에서 실행. Unity 컴파일·테스트·Windows 빌드는 GameCI GitHub Actions에서만.

**Tech Stack:** Python 3 (stdlib + pytest) 스크립트, .NET 8 SDK + NUnit 3, Unity 6000.3.29f1 (Unity 6.3 LTS), GameCI `unity-test-runner@v4` / `unity-builder@v4`, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-27-agent-game-studio-design.md` (원본: `docs/reference/original-agent-design.pdf`)

## Global Constraints

- Unity 버전: `6000.3.29f1` (GameCI 이미지 `unityci/editor:ubuntu-6000.3.29f1-windows-mono-4` 존재 확인됨). 모든 게임 동일.
- Core C#: `netstandard2.1` 호환, `LangVersion 9`, `using UnityEngine` 금지 (asmdef `noEngineReferences: true`).
- 빌드 타깃: `StandaloneWindows64`, 스크립팅 백엔드 Mono.
- 테스트 프레임워크: NUnit (dotnet은 `NUnit 3.14.0`, `NUnit3TestAdapter 4.6.0`, `Microsoft.NET.Test.Sdk 17.11.1`).
- 게임 이름: 정규식 `^[A-Z][A-Za-z0-9]{1,31}$`.
- 작업 상태: `BACKLOG, ASSIGNED, IN_PROGRESS, REVIEW, QA, AWAITING_UNITY, COMPLETED, FIX_REQUIRED, BLOCKED, CANCELLED`.
- 에이전트 이름: `orchestrator, game-designer, architect, developer, content-designer, code-reviewer, qa-engineer`.
- verification 항목: `core-tests, review, unity-compile, unity-editmode, unity-playmode, unity-build`.
- 재시도: 같은 담당자로 FIX_REQUIRED→IN_PROGRESS 최대 2회. 그 이후는 재배정(`--agent`) 또는 BLOCKED.
- 비밀 정보는 저장소에 쓰지 않는다. CI는 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` 시크릿만 사용.
- 개발 브랜치: `claude/serene-fermat-4bwjlo`. 작업 단위 커밋.
- 에이전트 프롬프트는 영어(원본 PDF 문구 기반), 사용자용 문서(README, 게임 Docs 템플릿)는 한국어.

## Review Focus

1. 잘못된 게임 이름(공백, 한글, 소문자 시작) 또는 이미 있는 게임 이름 → `new_game.py`가 아무것도 만들지 않고 exit 1. (Task 4 테스트)
2. 의존성 순환·존재하지 않는 dependency·잘못된 status/agent → `task.py validate`가 각각 오류 메시지와 exit 1. (Task 1 테스트)
3. `file_scope`에 폴더(`.../Gameplay/`)를 지정했을 때 그 하위 파일과 그 `.meta`는 허용, 이름만 비슷한 형제 폴더(`.../GameplayExtra/`)는 위반. (Task 2 테스트)
4. 폴더를 복사해 `.meta` GUID가 중복된 경우 → `check_meta.py`가 두 경로를 모두 보고. (Task 3 테스트)
5. Unity 시크릿이 없는 포크/환경 → `unity` 워크플로가 실패가 아니라 skip 요약으로 끝남. (Task 5 검증)

---

### Task 1: 작업(Task) 관리 도구

**Files:**
- Create: `studio/scripts/task.py`, `studio/scripts/studio_common.py`, `studio/tests/test_task.py`, `studio/tests/conftest.py`
- Create: `.gitignore`, `.gitattributes` (루트; Unity Library/Temp/Obj/Build/Logs/UserSettings, `bin/`, `obj/`, `__pycache__/`, 생성 씬 `Games/*/Unity/Assets/Game/Scenes/Generated/`; `.gitattributes`는 `* text=auto`, `*.unity *.prefab *.asset *.meta merge=unityyamlmerge eol=lf`, png/jpg/wav/mp3/ogg/fbx/psd → `filter=lfs diff=lfs merge=lfs -text`)

**Interfaces:**
- Produces (CLI, 저장소 루트에서 실행):
  - `task.py new <Game> --title T --agent A [--desc D] [--priority critical|high|medium|low] [--dep ID]... [--scope PATH]... [--deliverable F]... [--accept TEXT]... [--verify V]...` → `Games/<Game>/Tasks/<GAME>-NNN.json` 생성, 표준출력에 ID. `<GAME>` = 게임 이름 대문자, NNN은 3자리 순번.
  - `task.py set <Game> <ID> --status S [--agent A] [--result KEY=VALUE]... [--note TEXT]` → 전이 검증 후 저장. IN_PROGRESS 진입 시 `base_commit`에 `git rev-parse HEAD` 기록(최초 1회).
  - `task.py list <Game> [--status S]`, `task.py ready <Game>` (status BACKLOG이고 deps 전부 COMPLETED인 ID들), `task.py validate <Game>`.
- Produces (Python, `studio_common.py`): `repo_root() -> Path`, `game_dir(name: str) -> Path`, `STATUSES`, `AGENTS`, `VERIFICATIONS`, `GAME_NAME_RE`.
- 작업 JSON 필드: `task_id, title, description, agent, priority, status, dependencies[], file_scope[], deliverables[], acceptance_criteria[], verification[], retry_count, base_commit|null, result{}, notes[]`. `result`의 값은 `pass|fail|pending` 또는 근거 경로 문자열.

- [ ] **Step 1: 테스트 작성** — `conftest.py`에 임시 git 저장소 + `Games/Demo/Tasks/`를 만드는 fixture(`studio_repo`, 환경변수 `STUDIO_ROOT`로 루트 지정). `test_task.py`:
  - `test_new_creates_sequential_ids`: new 두 번 → `DEMO-001`, `DEMO-002`, 파일 존재, status `BACKLOG`, retry_count 0.
  - `test_valid_transition_chain`: BACKLOG→ASSIGNED→IN_PROGRESS→REVIEW→QA→AWAITING_UNITY→COMPLETED 모두 exit 0, IN_PROGRESS 후 `base_commit`이 40자 hex.
  - `test_invalid_transition_rejected`: BACKLOG→COMPLETED exit 1, 파일 불변.
  - `test_retry_limit`: FIX_REQUIRED→IN_PROGRESS 2회 후 3번째 exit 1 (메시지에 `retry limit`), `--agent developer`로 재배정하면 허용되고 retry_count 0.
  - `test_validate_detects_cycle`, `test_validate_detects_unknown_dependency`, `test_validate_detects_bad_status_and_agent`: 각각 exit 1, 해당 ID가 출력에 포함.
  - `test_ready_lists_unblocked`: A(COMPLETED), B(dep A, BACKLOG), C(dep B, BACKLOG) → ready = `[B]`.
- [ ] **Step 2: 실패 확인** — Run: `python3 -m pytest studio/tests/test_task.py -q` / Expected: FAIL (모듈 없음)
- [ ] **Step 3: 구현** — 허용 전이 표:
  `BACKLOG→ASSIGNED`, `ASSIGNED→IN_PROGRESS`, `IN_PROGRESS→REVIEW`, `REVIEW→QA|FIX_REQUIRED`, `QA→COMPLETED|AWAITING_UNITY|FIX_REQUIRED`, `AWAITING_UNITY→COMPLETED|FIX_REQUIRED`, `FIX_REQUIRED→IN_PROGRESS`, `BLOCKED→ASSIGNED`, 완료/취소 외 모든 상태→`BLOCKED|CANCELLED`. FIX_REQUIRED→IN_PROGRESS는 retry_count+1(2 초과 금지), `--agent` 변경 시 0으로 리셋. 파일 쓰기는 정렬된 키, 들여쓰기 2, UTF-8(`ensure_ascii=False`).
- [ ] **Step 4: 통과 확인** — Run: `python3 -m pytest studio/tests -q` / Expected: all passed
- [ ] **Step 5: 커밋** — `git add .gitignore .gitattributes studio && git commit -m "feat(studio): task state tool"`

### Task 2: 수정 범위 검사 (`check_scope.py`)

**Files:**
- Create: `studio/scripts/check_scope.py`, `studio/tests/test_check_scope.py`

**Interfaces:**
- Consumes: `studio_common.game_dir`, 작업 JSON의 `file_scope`, `base_commit`.
- Produces: `check_scope.py <Game> <ID> [--base REF]` — 변경 파일 = `git diff --name-only <base>` ∪ 추적 안 된 파일(`git ls-files --others --exclude-standard`). 위반 파일을 한 줄씩 출력하고 exit 1, 없으면 `OK` exit 0. `--base` 없고 `base_commit`도 null이면 exit 2.
- Produces (Python): `in_scope(path: str, scope: list[str], task_file: str) -> bool`.

- [ ] **Step 1: 테스트 작성** — `test_in_scope_*` 단위 테스트:
  - 폴더 항목 `Games/Demo/Unity/Assets/Game/Scripts/Gameplay/` → 하위 `Player.cs`, `Player.cs.meta` 허용; `.../GameplayExtra/X.cs` 거부.
  - 파일 항목 정확 일치 허용, glob `Games/Demo/Docs/design/*.md` 허용.
  - 파일 항목의 `.meta` 자동 허용; 폴더 자신의 `.meta`(`.../Gameplay.meta`) 허용.
  - 자기 작업 파일(`Games/Demo/Tasks/DEMO-001.json`) 항상 허용, 다른 작업 파일 거부.
  - 통합 테스트 `test_cli_reports_violation`: fixture 저장소에서 IN_PROGRESS로 base_commit 기록 → 범위 안/밖 파일 생성 → exit 1, 범위 밖 경로만 출력.
- [ ] **Step 2: 실패 확인** — Run: `python3 -m pytest studio/tests/test_check_scope.py -q` / Expected: FAIL
- [ ] **Step 3: 구현** — `/`로 끝나는 항목은 접두사 일치, 나머지는 `fnmatch.fnmatchcase`. 경로 끝 `.meta`를 떼고 한 번 더 판정.
- [ ] **Step 4: 통과 확인** — Run: `python3 -m pytest studio/tests -q` / Expected: all passed
- [ ] **Step 5: 커밋** — `git commit -m "feat(studio): file scope checker"`

### Task 3: `.meta` 생성·검사

**Files:**
- Create: `studio/scripts/gen_meta.py`, `studio/scripts/check_meta.py`, `studio/tests/test_meta.py`

**Interfaces:**
- Produces: `gen_meta.py <path>...` — 각 경로(파일 또는 폴더, 폴더면 재귀)에서 `Assets/` 아래의 `.meta` 없는 항목에 `.meta` 생성, 생성한 경로 출력. GUID = `uuid.uuid4().hex`. 템플릿: 폴더 → `folderAsset: yes` + `DefaultImporter`; `.cs` → `MonoImporter`; `.asmdef` → `AssemblyDefinitionImporter`; `.json/.txt/.md` → `TextScriptImporter`; 그 외 → `DefaultImporter`. 모든 meta는 `fileFormatVersion: 2` 첫 줄.
- Produces: `check_meta.py <UnityProjectDir>` — `Assets/` 아래(숨김/`~`로 끝나는 항목 제외) 누락 meta, 짝 없는 meta, 중복 GUID를 섹션별로 출력, 문제 있으면 exit 1.
- Produces (Python): `gen_meta.ensure_meta(path: Path) -> list[Path]`.

- [ ] **Step 1: 테스트 작성** — `test_gen_creates_meta_for_files_and_folders`(폴더·cs·asmdef 각각 importer 문자열 확인, GUID 32자 hex), `test_gen_is_idempotent`(두 번째 실행 시 생성 0개, 기존 GUID 불변), `test_check_reports_missing`, `test_check_reports_orphan`, `test_check_reports_duplicate_guid`(복사한 meta 두 경로 모두 출력), `test_check_clean_project_exit0`.
- [ ] **Step 2: 실패 확인** — Run: `python3 -m pytest studio/tests/test_meta.py -q` / Expected: FAIL
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인** — Run: `python3 -m pytest studio/tests -q` / Expected: all passed
- [ ] **Step 5: 커밋** — `git commit -m "feat(studio): unity meta generator and checker"`

### Task 4: 게임 템플릿 + `new_game.py`

**Files:**
- Create: `studio/scripts/new_game.py`, `studio/tests/test_new_game.py`
- Create: `studio/templates/game/` 아래 (`{{GAME}}` = 게임 이름, `{{GAME_UPPER}}`, `{{MODE}}` 치환):
  - `Docs/design/{GAME_DESIGN,GAME_RULES,FEATURE_SPECS,CONTENT_SPECS}.md`, `Docs/tech/{ARCHITECTURE,INTERFACE_CONTRACTS,DATA_STRUCTURE,CODING_CONVENTION}.md`, `Docs/{DECISIONS,PROGRESS}.md` — 한국어 섹션 제목만 있는 템플릿(원본 PDF 각 역할의 Required Outputs 항목 기준). `CODING_CONVENTION.md`에는 Core 규칙(UnityEngine 금지, C# 9, 네임스페이스 `{{GAME}}.Core`) 기본 기재.
  - `Tasks/.gitkeep`, `Reports/{review,qa,build}/.gitkeep`
  - `Core.Tests/Core.Tests.csproj` — `net8.0`, `LangVersion 9`, `Nullable disable`, `<Compile Include="../Unity/Assets/Game/Core/**/*.cs" />`, `<Compile Include="../Unity/Assets/Game/Tests/EditMode/Core/**/*.cs" />`, NUnit 패키지(Global Constraints 버전).
  - `Unity/ProjectSettings/ProjectVersion.txt` — `m_EditorVersion: 6000.3.29f1`
  - `Unity/Packages/manifest.json` — `com.unity.test-framework` `1.4.6`, `com.unity.ugui` `2.0.0`, 필수 `com.unity.modules.*`(ui, uielements, imgui, jsonserialize, physics 또는 physics2d는 MODE에 따라, audio, animation, ui, screencapture). 2D면 `com.unity.2d.sprite` 추가.
  - `Unity/Assets/Game/Core/{{GAME}}.Core.asmdef` (`"noEngineReferences": true`, `"rootNamespace": "{{GAME}}.Core"`), `Unity/Assets/Game/Core/GameInfo.cs` (`public static class GameInfo { public const string Name = "{{GAME}}"; public const string Version = "0.1.0"; }`)
  - `Unity/Assets/Game/Scripts/{{GAME}}.Runtime.asmdef` (refs `{{GAME}}.Core`), `Scripts/Bootstrap.cs` — `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Init()`: `Camera.main`이 없으면 `MainCamera` 태그 카메라 생성(MODE 2d면 orthographic), 이름 `"[Bootstrap]"`인 GameObject 생성 후 `DontDestroyOnLoad`. 빈 `Scripts/{Gameplay,Systems,UI}/` 폴더(.gitkeep 대신 `README.md` 한 줄).
  - `Unity/Assets/Game/Editor/{{GAME}}.Editor.asmdef` (Editor 플랫폼만, refs Runtime), `Editor/StudioBuild.cs` — `public static class StudioBuild { public static void Build() }`: `Assets/Game/Scenes/Generated/Bootstrap.unity`를 `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene)`로 만들어 저장, `EditorBuildSettings.scenes`에 등록, 커맨드라인 `-customBuildPath`(없으면 `Build/Windows/{{GAME}}.exe`)로 `BuildPipeline.BuildPlayer(StandaloneWindows64)`; 결과가 Succeeded가 아니면 `EditorApplication.Exit(1)`.
  - `Unity/Assets/Game/Tests/EditMode/{{GAME}}.Tests.EditMode.asmdef` (Editor 전용, refs Core, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `overrideReferences` + `nunit.framework.dll`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`), `Tests/EditMode/Core/GameInfoTests.cs` — `Assert.AreEqual("{{GAME}}", GameInfo.Name)` (UnityEngine 미사용: dotnet에서도 실행)
  - `Unity/Assets/Game/Tests/PlayMode/{{GAME}}.Tests.PlayMode.asmdef` (refs Runtime, Core, TestRunner, nunit), `Tests/PlayMode/BootstrapTests.cs` — `[UnityTest] BootstrapCreatesCamera`: 한 프레임 대기 후 `Camera.main != null`, `GameObject.Find("[Bootstrap]") != null`.
  - `Unity/Assets/Game/{Data,Art,Audio,Prefabs}/README.md`

**Interfaces:**
- Consumes: `studio_common.GAME_NAME_RE`, `game_dir`, `gen_meta.ensure_meta`.
- Produces: `new_game.py <Name> [--mode 2d|3d]` (기본 2d) → `Games/<Name>/` 생성, `Unity/Assets` 전체에 meta 생성, 마지막 줄에 `created Games/<Name>`. 이름 오류/이미 존재 시 아무것도 만들지 않고 exit 1.
- Produces (경로 규약, 이후 모든 작업이 사용): Core = `Games/<G>/Unity/Assets/Game/Core/`, 양쪽 실행 Core 테스트 = `Games/<G>/Unity/Assets/Game/Tests/EditMode/Core/`, 빌드 메서드 = `StudioBuild.Build`.

- [ ] **Step 1: 테스트 작성** — `test_rejects_bad_names`(`"my game"`, `"게임"`, `"lower"`, `"A"` 각각 exit 1, `Games/` 비어 있음), `test_rejects_existing`, `test_creates_structure_2d`(주요 파일 존재, `{{` 잔존 없음, `ProjectVersion.txt`에 `6000.3.29f1`, `check_meta.py` exit 0), `test_mode_3d_manifest_has_physics`(3d에 `com.unity.modules.physics`, 2d에 `com.unity.2d.sprite`).
- [ ] **Step 2: 실패 확인** — Run: `python3 -m pytest studio/tests/test_new_game.py -q` / Expected: FAIL
- [ ] **Step 3: 템플릿과 `new_game.py` 구현** (템플릿 디렉터리를 복사하며 파일 내용과 경로의 플레이스홀더 치환, 복사 실패 시 생성한 폴더 삭제)
- [ ] **Step 4: 통과 확인** — Run: `python3 -m pytest studio/tests -q` / Expected: all passed
- [ ] **Step 5: dotnet 설치 및 Core 테스트 실제 실행** — Run: `curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/claude-0/-home-user-second-game/636884ad-8c21-5cae-9861-9592d708d5ff/scratchpad/dotnet-install.sh && bash /tmp/claude-0/-home-user-second-game/636884ad-8c21-5cae-9861-9592d708d5ff/scratchpad/dotnet-install.sh --channel 8.0 --install-dir ~/.dotnet && export PATH=~/.dotnet:$PATH && python3 studio/scripts/new_game.py Sandbox --mode 2d && dotnet test Games/Sandbox/Core.Tests` / Expected: `Passed!  - Failed: 0, Passed: 1`
- [ ] **Step 6: `studio/scripts/verify_core.sh` 작성** — 인자로 게임 이름(없으면 모든 `Games/*/Core.Tests`) 받아 `dotnet test` 실행, 하나라도 실패하면 exit 1. Run: `bash studio/scripts/verify_core.sh Sandbox` / Expected: exit 0
- [ ] **Step 7: 커밋** — Sandbox 게임 포함: `git add studio Games/Sandbox && git commit -m "feat(studio): game template and new_game; add Sandbox sample"`

### Task 5: GitHub Actions 워크플로

**Files:**
- Create: `.github/workflows/core-tests.yml`, `.github/workflows/unity.yml`, `studio/scripts/changed_games.py`, `studio/tests/test_changed_games.py`

**Interfaces:**
- Produces: `changed_games.py --base SHA --head SHA` → 변경된 `Games/<G>/Unity/**` 또는 `Games/<G>/Core.Tests/**`가 있는 게임 이름의 JSON 배열 출력. base가 `0000000000000000000000000000000000000000`이거나 git에 없으면 존재하는 모든 게임.
- `core-tests.yml`: 모든 push/PR. `actions/setup-python@v5`(3.12) → `pip install pytest` → `pytest studio/tests -q`; `actions/setup-dotnet@v4`(8.0.x) → `bash studio/scripts/verify_core.sh`; 각 게임에 `check_meta.py`.
- `unity.yml`: push(paths `Games/**/Unity/**`) + `workflow_dispatch`(input `game`). job `detect`(changed_games → matrix, 시크릿 존재 여부를 `env.UNITY_LICENSE != ''`로 판단해 output `has_license`) → job `unity`(`if: has_license == 'true' && matrix 비어있지 않음`, `strategy.matrix.game`, `max-parallel: 1`): `actions/checkout@v4`(lfs: true) → `actions/cache@v4`(`Games/${{ matrix.game }}/Unity/Library`, key에 game과 `Packages/manifest.json` 해시) → `game-ci/unity-test-runner@v4`(`projectPath`, `testMode: all`, `artifactsPath: test-results`, `githubToken`) → `game-ci/unity-builder@v4`(`projectPath`, `targetPlatform: StandaloneWindows64`, `buildMethod: StudioBuild.Build`, `buildName: ${{ matrix.game }}`) → `actions/upload-artifact@v4` 두 개(`<game>-test-results`, `<game>-windows-build`). job `skipped`(`if: has_license != 'true'`): `$GITHUB_STEP_SUMMARY`에 "Unity secrets not configured — Unity jobs skipped" 기록 후 성공 종료.

- [ ] **Step 1: `changed_games.py` 테스트** — `test_detects_changed_game`, `test_zero_base_returns_all`, `test_ignores_non_game_paths`.
- [ ] **Step 2: 실패 확인** — Run: `python3 -m pytest studio/tests/test_changed_games.py -q` / Expected: FAIL
- [ ] **Step 3: 구현 후 통과 확인** — Run: `python3 -m pytest studio/tests -q` / Expected: all passed
- [ ] **Step 4: 워크플로 작성, YAML 문법 확인** — Run: `python3 -c "import yaml,sys;[yaml.safe_load(open(f)) for f in sys.argv[1:]]" .github/workflows/*.yml` / Expected: 오류 없음
- [ ] **Step 5: 커밋·푸시 후 CI 확인** — `git commit -m "ci: core tests and GameCI unity workflow" && git push`. GitHub MCP `actions_list`/`get_job_logs`로 이 커밋의 두 워크플로 결과 확인. Expected: `core-tests` 성공, `unity`에서 Sandbox의 EditMode 1개·PlayMode 1개 통과, `Sandbox-windows-build` 아티팩트 존재. 실패 시 superpowers:systematic-debugging으로 원인 수정(템플릿 쪽 수정이면 Task 4 템플릿에도 반영) 후 재푸시.

### Task 6: 공통 규칙과 에이전트 정의

**Files:**
- Create: `AGENTS.md`, `CLAUDE.md`, `.claude/agents/{game-designer,architect,developer,content-designer,code-reviewer,qa-engineer}.md`

**Interfaces:**
- Consumes: Task 1~5의 CLI 이름과 경로 규약, 원본 PDF 2장(역할별 프롬프트)·7-2(처리표)·8장(AGENTS.md)·9장(안전장치).
- Produces: 서브에이전트 이름 = 파일명(Global Constraints의 에이전트 이름과 동일).

내용 결정:
- `AGENTS.md`: 원본 8장 11개 절을 A안에 맞게 옮김. 추가: 경로 규약(Task 4), 상태 전이와 "자기 작업 파일의 status/result만 수정" 규칙, `task.py`/`check_scope.py`/`gen_meta.py`/`check_meta.py`/`verify_core.sh` 사용법, 코드 우선 Unity 규칙(스펙 5.7.1), "새 Unity 파일을 만들면 `gen_meta.py` 실행".
- `CLAUDE.md`: 첫 줄 `@AGENTS.md`. Orchestrator 역할(원본 2-1 프롬프트), Scan→Plan→Implement→Verify→Review→Fix→Complete 각 단계에서 호출할 명령과 에이전트, 병렬 판단 기준(원본 7장 병렬/직렬 조건; 병렬 시 Agent 도구 `isolation: "worktree"` 사용 후 결과 브랜치 병합), 7-2 처리표, 사용자에게 묻는 경우는 "핵심 게임 콘셉트 변경"과 "외부 배포"뿐. 새 게임 요청 시 `new_game.py` → game-designer 작업부터.
- 각 에이전트 파일: frontmatter `name`, `description`(Orchestrator가 언제 부르는지 한 문장), `tools`. 본문은 원본 PDF 해당 역할 프롬프트(Responsibilities / Rules / Restrictions / File Ownership / Completion Criteria) + 보고 형식(작업 ID, 변경 파일, 실행한 검증 명령과 결과).
  - `code-reviewer` tools: `Read, Grep, Glob, Bash` (Write/Edit 없음; 리포트는 Bash heredoc으로 `Reports/review/<ID>.md`에만 — 본문에 명시). 첫 단계 `check_scope.py`, 원본 8항목 체크리스트, 상태 4종.
  - `qa-engineer` tools: `Read, Grep, Glob, Bash, Write, Edit` + GitHub MCP 읽기 도구(`mcp__github__actions_list, mcp__github__actions_get, mcp__github__get_job_logs`). 원본 버그 리포트 양식·심각도 4단계, AWAITING_UNITY 판정 절차(이 커밋의 unity 워크플로 결과 확인 → 테스트 수/실패 기록 → 상태 변경).
  - `developer`: Gameplay/System/UI 세 역할 규칙을 합치고 "작업의 file_scope가 곧 내 소유 영역", Core 우선 + 테스트는 `Tests/EditMode/Core/`에.
- [ ] **Step 1: 작성**
- [ ] **Step 2: 검증** — Run: `python3 - <<'EOF'` 로 각 `.claude/agents/*.md` frontmatter를 파싱해 name이 파일명과 같고 description·tools가 있는지 확인 / Expected: 6개 모두 OK. `grep -L "check_scope" .claude/agents/code-reviewer.md` 출력 없음.
- [ ] **Step 3: 커밋** — `git commit -m "feat(studio): AGENTS.md, orchestrator guide, 6 agent definitions"`

### Task 7: README

**Files:**
- Create: `README.md`

- [ ] **Step 1: 작성 (한국어)** — 절: 이 저장소가 하는 일 / 팀 구성 표 / 새 게임 만들기(대화에서 "XX 게임 만들어줘"라고 요청하면 Orchestrator가 처리) / Unity 라이선스 시크릿 등록(Unity Hub만 설치 → Add → free personal → `C:\ProgramData\Unity\Unity_lic.ulf` 내용 → 시크릿 3개; Hub 로그아웃으로 좌석 반납) / 빌드 받아서 플레이하기(Actions → 실행 → `<game>-windows-build` 다운로드 → 압축 해제 → exe) / 나중에 에디터를 설치한다면 `6000.3.29f1` / Actions 사용 시간 주의.
- [ ] **Step 2: 커밋** — `git commit -m "docs: README"`

### Task 8: 파이프라인 한 바퀴 (완료 기준 검증)

Orchestrator(이 세션)가 `CLAUDE.md` 절차대로 Sandbox에 작은 기능 하나를 실제 에이전트로 처리한다.

기능: **"점수 카운터"** — Core에 `ScoreCounter`(점수 +1, 연속 입력 콤보 배수, 리셋), Unity 쪽은 스페이스/클릭 입력 시 점수 증가 후 화면 텍스트 표시. (구체 규칙은 game-designer가 문서로 정한다.)

- [ ] **Step 1:** `task.py`로 작업 생성: designer 명세 → architect 인터페이스 → developer Core 구현(+Core 테스트) → developer Unity 연결(verification에 unity-editmode/playmode/build) — 의존성 체인으로.
- [ ] **Step 2:** 각 작업을 해당 서브에이전트로 실행, 구현 작업마다 code-reviewer → qa-engineer 순서. 상태는 매 단계 `task.py set`.
- [ ] **Step 3:** 푸시 후 qa-engineer가 CI 결과를 읽어 AWAITING_UNITY 작업을 COMPLETED 또는 FIX_REQUIRED로 판정; FIX_REQUIRED면 재배정 루프.
- [ ] **Step 4: 검증** — Run: `python3 studio/scripts/task.py list Sandbox && python3 studio/scripts/task.py validate Sandbox && ls Games/Sandbox/Reports/review Games/Sandbox/Reports/qa` / Expected: 모든 작업 COMPLETED, validate exit 0, 각 구현 작업의 리뷰·QA 리포트 존재, 마지막 unity 워크플로 성공 + `Sandbox-windows-build` 아티팩트.
- [ ] **Step 5:** `Docs/PROGRESS.md`·`DECISIONS.md` 갱신 확인 후 커밋·푸시. 이 과정에서 발견한 규칙/프롬프트 문제는 `AGENTS.md`·에이전트 파일에 반영하고 같은 커밋에 포함.
