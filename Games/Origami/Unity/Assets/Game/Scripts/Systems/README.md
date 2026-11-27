Unity 쪽 공통 시스템 코드 (`Origami.Runtime.Systems`, C-190).

- `Materials` — 코드로 만든 머티리얼 (`Lit`/`Unlit`, 셰이더 대체 순서, 로그 없음, 다 실패해도 머티리얼 반환).
- `MeshFactory` — 상자·원기둥·구·캡슐 코드 메시 (`GameObject.CreatePrimitive` 대신 — 3D physics 모듈 없음).
