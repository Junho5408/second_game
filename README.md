# second_game — 에이전트 게임 스튜디오

Claude Code 에이전트 팀이 Unity 인디 게임을 기획부터 빌드까지 만들어 나가는 저장소입니다.
Unity 에디터 없이 개발하고, Unity 컴파일·테스트·Windows 빌드는 GitHub Actions에서 자동으로 돕니다.

## 팀 구성

| 역할 | 하는 일 |
|---|---|
| Orchestrator (Claude Code 메인 대화) | 요청 분석, 작업 분해·배정, 병렬 판단, 결과 검증, 병합 |
| `game-designer` | 콘셉트, 규칙, 기능·콘텐츠 명세, 수용 기준 |
| `architect` | 구조, 인터페이스 계약, 데이터 구조, 패키지/설정 |
| `developer` | 게임플레이·시스템·UI 구현 + 테스트 (작업마다 수정 범위 지정) |
| `content-designer` | 게임 데이터(JSON), 기본 아트/오디오 |
| `code-reviewer` | 독립 코드 리뷰 (코드 수정 불가) |
| `qa-engineer` | 명세 대비 검증, 버그 리포트, CI의 Unity 결과 판정 (제품 코드 수정 불가) |

규칙: [`AGENTS.md`](AGENTS.md) · Orchestrator 지침: [`CLAUDE.md`](CLAUDE.md) · 에이전트 정의: `.claude/agents/`
설계 배경: `docs/superpowers/specs/2026-11-27-agent-game-studio-design.md`

## 사용 방법

Claude Code 대화에서 그냥 요청하면 됩니다.

- 새 게임: "2D 로그라이크 카드 게임 만들어줘"
- 기능 추가: "Sandbox에 일시정지 메뉴 추가해줘"

Orchestrator가 작업을 나누고(`Games/<게임>/Tasks/`), 기획 → 설계 → 구현 → 리뷰 → QA → CI 검증 순서로 진행합니다.
진행 상황은 `Games/<게임>/Docs/PROGRESS.md`, 결정 기록은 `Docs/DECISIONS.md`에 남습니다.

## 저장소 구조

```
AGENTS.md / CLAUDE.md / .claude/agents/   팀 규칙과 에이전트
studio/scripts/                           작업 관리·검사 도구
studio/templates/game/                    새 게임 템플릿
.github/workflows/                        core-tests(항상), unity(Unity 파일 변경 시)
Games/<게임>/
  Docs/  Tasks/  Reports/
  Core.Tests/                             Unity 없이 도는 dotnet 테스트
  Unity/                                  Unity 프로젝트 (Assets/Game/Core = 엔진 독립 로직)
```

## Unity 라이선스 등록 (한 번만)

Unity 에디터는 설치할 필요 없고, 라이선스 파일을 만들기 위해 **Unity Hub만** 설치합니다.

1. Unity Hub 설치 후 로그인 → `Preferences → Licenses → Add → Get a free personal license`
2. 라이선스 파일 열기: `C:\ProgramData\Unity\Unity_lic.ulf` (숨김 폴더일 수 있음)
3. GitHub 저장소 → `Settings → Secrets and variables → Actions → New repository secret`
   - `UNITY_LICENSE`: `.ulf` 파일 내용 전체
   - `UNITY_EMAIL`: Unity 계정 이메일
   - `UNITY_PASSWORD`: Unity 계정 비밀번호
4. Unity Hub에서 로그아웃하면 Personal 라이선스 좌석이 반납됩니다.

시크릿이 없으면 `unity` 워크플로는 실패하지 않고 "skipped"로 끝납니다.
비밀번호·라이선스는 시크릿에만 넣고, 파일이나 대화에 붙여넣지 마세요.

## 빌드 받아서 플레이하기

1. GitHub 저장소 → `Actions` → `unity` 워크플로 → 최근 성공한 실행
2. 아래 `Artifacts`에서 `<게임이름>-windows-build` 다운로드 → 압축 해제 → `<게임이름>.exe` 실행
3. 특정 게임만 다시 빌드하려면 `unity` 워크플로의 `Run workflow`에서 게임 이름 입력

## 참고

- Unity 버전은 저장소 전체 **6000.3.29f1**(Unity 6.3 LTS)로 고정입니다. 나중에 에디터를 설치한다면 같은 버전을 설치하세요.
- 비공개 저장소의 GitHub Actions 무료 사용 시간은 월 2,000분입니다. Unity 작업은 Unity 파일이 바뀐 게임만, 캐시를 써서 돕니다.
- 로컬(클라우드 세션)에서 Core 테스트: `bash studio/scripts/verify_core.sh [게임]` (.NET 8 SDK 필요)
