Unity 의존 게임플레이 코드 (v2, ORIGAMI-014, C-193). 규칙 자체는 Core에.

- `OrigamiApp` — 루트(`[Origami]`), `ScreenFlow` 단독 소유, 유일한 `Update` → `ProcessFrame(InputFrame, dt)`
- `InputReader`/`InputFrame` — 레거시 Input 읽기(유일한 입력 읽는 곳)
- `WorkshopView`(방·가구·진열·쓰레기통·창문·조명), `CharacterView`, `CameraRig`(카메라 지연 획득)
- `FoldHostAdapter`(`IFoldTableHost`), `BookHostAdapter`(`IBookHost`), `IAppView`(GUI 등록용 internal)
