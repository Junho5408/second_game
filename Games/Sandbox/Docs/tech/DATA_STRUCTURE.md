# Sandbox 데이터 구조

> 담당: architect

## 데이터 모델

### ScoreCounter 내부 상태 (Core/Scoring, private)

| 필드(개념) | 타입 | 초기값 | 의미 |
|---|---|---|---|
| score | `int` | 0 | 누적 점수, 0 이상, 리셋 외에는 감소하지 않음 (R-006) |
| combo | `int` | 0 | 마지막 유효 입력 직후의 콤보 수 (만료 여부는 조회 시 계산, 저장하지 않음) |
| lastPress | `double?` (또는 `bool hasLastPress` + `double`) | 없음 | 직전 유효 입력 시각(초). 유한값만 저장 |

- 만료(R-004)는 상태가 아니라 조회 결과다. `GetState(now)`는 상태를 바꾸지 않는다.
- 필드 이름·표현은 구현자(SANDBOX-003) 재량. 관찰 가능한 동작은 INTERFACE_CONTRACTS.md C-001을 따른다.

### ScoreSnapshot (Core/Scoring, public 값 타입)

| 속성 | 타입 | 범위 |
|---|---|---|
| Score | `int` | ≥ 0 |
| Combo | `int` | ≥ 0 (0 = 입력 없음/리셋/만료) |
| Multiplier | `int` | 1~4, Combo 0이면 1 |

### 규칙 수치 (ScoreRules 상수)

| 상수 | 값 | 출처 |
|---|---|---|
| BasePoints | 1 | GAME_RULES.md BASE_POINTS |
| ComboWindowSeconds | 1.0 | GAME_RULES.md COMBO_WINDOW |
| MaxMultiplier | 4 | GAME_RULES.md 배수 상한 |
| 배수표 | ≤4→1, 5~9→2, 10~19→3, ≥20→4 | GAME_RULES.md 콤보 배수 단계 |

수치는 코드 상수로 둔다(데이터 파일 없음). 규칙이 자주 바뀌게 되면 JSON으로 옮기는 것을 재검토한다.

## 데이터 파일 (JSON) 형식

- F-001: 없음. 저장 데이터·외부 데이터 파일 없음(최고 점수 저장은 범위 밖).
- 향후 저장이 필요하면 JSON(`UnityEngine.JsonUtility`는 Runtime에서만, Core는 직렬화 형식에 의존하지 않는 DTO만 정의).
