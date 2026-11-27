접기 테이블 화면 (`Origami.Runtime.Folding`, C-191, ORIGAMI-012).

- `FoldTable` — 종이 메시·끌기·겹 고르기·자석 강조·미리보기·애니메이션·가이드 표시·앉은 시점 궤도. 자체 `Update` 없음: 주인(`OrigamiApp`, 014)이 `ProcessFrame(PointerFrame, dt)`를 부른다.
- `IFoldTableHost` — 명령 연결(게임은 `FoldHostAdapter`, 테스트는 `FoldSession` 가짜 host). `PointerFrame`, `FoldTableGuide`, `FoldTableChangeKind`.
- `PaperMeshBuilder`, `OverlayBuilder`, `SphereMesh`, `VertexXform` — Fold3DSpike 이식 + 자국 선(`AddCreaseLines`).
- 자동 생성 훅 없음(통합은 ORIGAMI-014).
