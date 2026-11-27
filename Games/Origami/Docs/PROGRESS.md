# Origami 진행 상황

> 담당: Orchestrator

## 현재 단계

## 완료

## 진행 중 / 막힘

## 2026-11-27 — 첫 플레이 가능 버전

| 작업 | 담당 | 결과 |
|---|---|---|
| ORIGAMI-001 기획 | game-designer | COMPLETED (QA 반려 1회 → 수정) |
| ORIGAMI-002 구조·인터페이스 | architect | COMPLETED |
| ORIGAMI-003 Core 규칙 | developer | COMPLETED (무작위 100만 명령 독립 모델 대조 0 불일치) |
| ORIGAMI-004 Core 화면 흐름 | developer | COMPLETED |
| ORIGAMI-005 Unity 화면 | developer | COMPLETED (CI run 45948502253: EditMode 283/283, PlayMode 21/21, Windows 빌드 성공) |
| ORIGAMI-006 키보드 전용 단축키 | architect | BACKLOG |

사용자 플레이테스트 대기: 한글 표시, 화면 배치, 실제 입력, 그래픽, 7일 완주.

## 2026-11-27 — v2 재기획 (3D 공간·캐릭터·책·자유 접기)

| 작업 | 담당 | 결과 |
|---|---|---|
| ORIGAMI-006 키보드 단축키 | architect | CANCELLED (v2로 대체) |
| ORIGAMI-007 v2 기획 | game-designer | COMPLETED — 리뷰 3회(2회 반려 후 APPROVED_WITH_WARNINGS), QA PASS, scope 3종 OK |
| ORIGAMI-008 v2 기술 설계 | architect | COMPLETED — 리뷰 APPROVED_WITH_WARNINGS, QA PASS, scope 3종 OK |
| ORIGAMI-009 Core 접기 엔진·판정 | developer | COMPLETED — 리뷰 W(독립 대조 900열 0 불일치), QA 595/595, Unity CI 46023021200 성공 |
| ORIGAMI-010 Core 공방 규칙·경제·튜토리얼 | developer | COMPLETED — 리뷰 W, QA 803/803 (7일 플레이 5종 고정값 일치) |
| ORIGAMI-015 문서 정리 | game-designer | COMPLETED |
| ORIGAMI-012 Unity 접기 테이블 | developer | COMPLETED — Unity CI 46028807396: EditMode 735/735, PlayMode 54/54, Windows 빌드 성공 (손맛 AC-19는 플레이테스트 대기) |
| ORIGAMI-011 Core 책·문구·화면 흐름 | developer | COMPLETED — 리뷰 W, QA 972/972 (키 표 143칸, 7일 키 입력 플레이) |
| ORIGAMI-013 Unity 책 화면 | developer | COMPLETED — Unity CI 46036542487: EditMode 972/972, PlayMode 90/90, 빌드 성공 |
| ORIGAMI-014 공방 통합·v1 삭제·빌드 | developer | 진행 |
| ORIGAMI-016 기술 문서 후속 정리 | architect | COMPLETED — 리뷰 W, QA PASS (NaN 계약 테스트 6개 추가) |

다음 문서 정리 때 반영: W-5(매미 겹 기법 근거 표현), O-1(F-006 AC-04 시험 순서), O-2(GAME_DESIGN 규칙 범위 R-021).
