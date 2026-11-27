# Sandbox 시스템 구조

> 담당: architect

## 모듈과 책임

| 모듈 | 위치 | 책임 | 의존 |
|---|---|---|---|
| Core | Assets/Game/Core | 게임 규칙·상태 (UnityEngine 비참조) | 없음 |
| Runtime | Assets/Game/Scripts | 화면·입력·연출, Core 호출 | Core |

## 이벤트 / 통신 방식

## 저장 데이터 전략
