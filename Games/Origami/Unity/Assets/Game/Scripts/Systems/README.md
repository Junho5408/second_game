Unity 쪽 공통 시스템 코드 (`Origami.Runtime.Systems`, C-190).

- `Materials` — 코드로 만든 머티리얼 (`Lit`/`Unlit`, 셰이더 대체 순서, 로그 없음, 다 실패해도 머티리얼 반환).
- `MeshFactory` — 상자·원기둥·구·캡슐 코드 메시 (`GameObject.CreatePrimitive` 대신 — 3D physics 모듈 없음).
- `ArtKeys` — 가져온 아트의 Resources 키 상수(유일한 위치, ARCHITECTURE §14.2), Crafter 클립 이름, 창문 유리 머티리얼 이름 (ORIGAMI-019)
- `ModelFit` / `FitSpec` / `FitMode` — `Resources.Load` + 렌더러 경계로 Core 상자에 맞춤(Box 축별 ±40% 규칙 → 넘으면 균등, Inside, Height, Scale),
  바닥 가운데 정렬(또는 원점 고정), 실패 시 null(던지지·로그 남기지 않음) → 호출자가 코드 메시로 대체 (ORIGAMI-019)
