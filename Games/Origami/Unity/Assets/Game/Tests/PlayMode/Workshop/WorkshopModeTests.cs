using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Game;
using Origami.Core.Geometry;
using Origami.Core.Room;
using Origami.Core.Screens;
using Origami.Core.Texts;
using Origami.Runtime.Gameplay;
using Origami.Runtime.UI;
using UnityEngine;

namespace Origami.Tests.PlayMode.Workshop
{
    /// <summary>
    /// ORIGAMI-014: interaction and mode changes in the integrated workshop — F-003 AC-05~07, F-007 AC-06, the F-003 dialog texts
    /// (verbatim, review ORIGAMI-011 W-2) and F-013 AC-03. Driven by ProcessFrame with dt 0.02.
    /// </summary>
    public class WorkshopModeTests
    {
        private WorkshopRig _rig;

        [SetUp]
        public void SetUp() => _rig = new WorkshopRig();

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _rig = null;
        }

        private Vector2 Pos => _rig.App.CharacterPosition;

        [Test]
        public void F003_AC05_At1Point1m_PromptAndESits()
        {
            var chair = RoomLayout.InteractPoint(InteractTargets.Table);
            _rig.Place(chair.X - 1.1, chair.Y);
            _rig.Idle(0.02f);
            Assert.AreEqual(InteractTargets.Table, _rig.App.InteractCandidate);
            Assert.IsTrue(_rig.Shows("E: 앉기"));
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Seated, _rig.Mode);
            Assert.AreEqual((float)RoomLayout.ChairPosition.X, Pos.x, 0.05f, "moved onto the chair");
            Assert.AreEqual((float)RoomLayout.ChairPosition.Y, Pos.y, 0.05f);
            Assert.IsTrue(_rig.App.Character.Sitting);
            Assert.IsFalse(_rig.Shows("E: 앉기"));
        }

        [Test]
        public void F003_AC05_At1Point3m_NoPromptAndENothing()
        {
            var chair = RoomLayout.InteractPoint(InteractTargets.Table);
            _rig.Place(chair.X - 1.3, chair.Y);
            _rig.Idle(0.02f);
            Assert.IsNull(_rig.App.InteractCandidate);
            Assert.IsFalse(_rig.Shows("E: 앉기"));
            var before = _rig.Snap;
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            Assert.AreSame(before, _rig.Snap, "nothing was sent to Core");
        }

        [Test]
        public void R004_PromptTextsForEachTarget()
        {
            void Check(string target, string text)
            {
                var p = RoomLayout.InteractPoint(target);
                _rig.Place(p.X, p.Y);
                _rig.Idle(0.02f);
                Assert.IsTrue(_rig.Shows(text), text);
            }
            Check(InteractTargets.Table, "E: 앉기");
            Check(InteractTargets.Bookshelf, "E: 책 읽기");
            Check(InteractTargets.Counter, "E: 판매대 보기");
            Check(InteractTargets.Bed, "E: 잠자기");
        }

        [Test]
        public void F003_AC06_SeatedViewIn0Point5s_PaperCentred_QStandsAt2Point5_0Point2()
        {
            _rig.Sit();
            _rig.Key(GameKeys.TakeSheet);
            var rig = _rig.App.CameraRig;
            _rig.Idle(0.3f);
            Assert.IsFalse(rig.InSeatedView, "still moving at 0.3 s");
            _rig.Idle(0.3f);   // 0.62 s since sitting ≤ 0.5 + 0.1
            Assert.IsTrue(rig.InSeatedView);
            _rig.App.Table.GetSeatedCameraPose(out var pose, out var rot);
            Assert.Less(Vector3.Distance(pose, rig.Position), 1e-4f);
            Assert.Less(Quaternion.Angle(rot, rig.Rotation), 0.01f);
            var paperCentre = new Vector3(2.5f, 0.75f, 1.6f);
            var toPaper = (paperCentre - rig.Position).normalized;
            Assert.Greater(Vector3.Dot(rig.Rotation * Vector3.forward, toPaper), 0.9999f, "the paper is in the screen centre");
            Assert.AreEqual(CameraRig.SeatedNearClip, _rig.Camera.nearClipPlane, 1e-6f);

            _rig.Key(GameKeys.StandUp);
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            Assert.AreEqual(2.5f, Pos.x, 0.05f);
            Assert.AreEqual(0.2f, Pos.y, 0.05f);
            Assert.AreEqual(180f, _rig.App.CharacterYaw, 0.01f, "faces south");
            Assert.IsFalse(rig.InSeatedView);
            _rig.Idle(2f);
            var target = new Vector3(Pos.x, 0f, Pos.y) + CameraRig.WalkOffset;
            Assert.Less(Vector3.Distance(target, rig.Position), 0.2f, "back to the walking camera");
            Assert.AreEqual(CameraRig.WalkNearClip, _rig.Camera.nearClipPlane, 1e-6f);
            Assert.IsNotNull(_rig.Snap.Sheet, "the sheet stays on the table");
        }

        [Test]
        public void F003_AC07_SleepEnter_Settlement_Enter_NewDayAtStart()
        {
            _rig.Place(-1.5, 1.5);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.SleepConfirm, _rig.Mode);
            _rig.Key(GameKeys.Enter);
            Assert.AreEqual(Overlay.Settlement, _rig.Flow.Overlay);
            Assert.IsTrue(_rig.Shows("1일차 정산"));
            Assert.IsTrue(_rig.Shows("팔린 작품이 없어요"));
            Assert.AreEqual(1, _rig.Gui.PanelButtons.Count);
            Assert.AreEqual("다음 날", _rig.Gui.PanelButtons[0].Label);
            _rig.Key(GameKeys.Enter);
            Assert.AreEqual(Overlay.None, _rig.Flow.Overlay);
            Assert.AreEqual(2, _rig.Snap.Day);
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            Assert.AreEqual(-1.6f, Pos.x, 0.05f);
            Assert.AreEqual(0.0f, Pos.y, 0.05f);
            Assert.AreEqual(90f, _rig.App.CharacterYaw, 0.01f);
            Assert.IsTrue(_rig.Shows("2일차 / 7"));
        }

        [Test]
        public void F003_SleepConfirm_TextsVerbatim_EscIsNo()
        {
            _rig.Place(-1.5, 1.5);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual("잠자기 확인", _rig.Gui.PanelTitle);
            Assert.IsTrue(_rig.Shows("잠자기 확인"));
            Assert.IsTrue(_rig.Shows("Enter = 예, Esc = 아니오"));
            CollectionAssert.AreEqual(new[] { "예", "아니오" }, Labels(_rig.Gui.PanelButtons));
            _rig.Key(GameKeys.Escape);
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            Assert.AreEqual(1, _rig.Snap.Day);
            Assert.AreEqual("", _rig.Gui.PanelTitle);

            // The "예" button is the same command as Enter.
            _rig.Key(GameKeys.Interact);
            Assert.IsTrue(_rig.Gui.Press("예"));
            Assert.AreEqual(Overlay.Settlement, _rig.Flow.Overlay);
        }

        [Test]
        public void F003_PauseAndNewRunConfirm_TextsVerbatim()
        {
            _rig.Key(GameKeys.Escape);
            Assert.AreEqual(Overlay.PauseMenu, _rig.Flow.Overlay);
            Assert.AreEqual("일시정지", _rig.Gui.PanelTitle);
            Assert.IsTrue(_rig.Shows("일시정지"));
            Assert.IsTrue(_rig.Shows("Esc = 계속, Enter = 새로 시작"));
            CollectionAssert.AreEqual(new[] { "계속", "새로 시작" }, Labels(_rig.Gui.PanelButtons));

            _rig.Key(GameKeys.Escape);
            Assert.AreEqual(Overlay.None, _rig.Flow.Overlay, "Esc = 계속");
            Assert.IsTrue(_rig.Gui.PanelTitle.Length == 0);

            _rig.Key(GameKeys.Escape);
            Assert.IsTrue(_rig.Gui.Press("새로 시작"));
            Assert.AreEqual(Overlay.ConfirmNewRun, _rig.Flow.Overlay);
            Assert.AreEqual("새로 시작할까요?", _rig.Gui.PanelTitle);
            Assert.IsTrue(_rig.Shows("새로 시작할까요?"));
            Assert.IsTrue(_rig.Shows("Enter = 예, Esc = 아니오"));
            CollectionAssert.AreEqual(new[] { "예", "아니오" }, Labels(_rig.Gui.PanelButtons));

            _rig.Key(GameKeys.Escape);
            Assert.AreEqual(Overlay.None, _rig.Flow.Overlay, "아니오");

            _rig.Hold(1f, 0f, 0.5f);
            _rig.Key(GameKeys.Escape);
            _rig.Key(GameKeys.Enter);
            _rig.Key(GameKeys.Enter);
            Assert.AreEqual(Overlay.None, _rig.Flow.Overlay);
            Assert.AreEqual(SheetChangeKind.Reset, _rig.Flow.LastSheetChange.Kind, "new run");
            Assert.AreEqual(-1.6f, Pos.x, 0.05f, "back at the start position");
        }

        [Test]
        public void F007_AC06_BookshelfDoesNotMove_EscCloses()
        {
            _rig.Place(0.0, 1.5);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Bookshelf, _rig.Mode);
            Assert.AreEqual(OrigamiGui.BookPlacement.Full, _rig.Gui.Book);
            var p = Pos;
            _rig.Hold(1f, 1f, 1f);
            Assert.AreEqual(p, Pos);
            _rig.Key(GameKeys.PageRight);
            Assert.AreEqual(2, _rig.Snap.BookPage, "→ turns the page");
            _rig.Key(GameKeys.Escape);
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            Assert.AreEqual(OrigamiGui.BookPlacement.None, _rig.Gui.Book);
        }

        [Test]
        public void F007_AC06_SeatedBookOnTheLeft_FoldingContinues()
        {
            _rig.Sit();
            _rig.Key(GameKeys.TakeSheet);
            _rig.Key(GameKeys.Book);
            Assert.IsTrue(_rig.Snap.BookOpen);
            Assert.AreEqual(OrigamiGui.BookPlacement.Left, _rig.Gui.Book);
            Assert.AreEqual(OrigamiGui.SeatedBookWidth / OrigamiGui.RefWidth, 0.45f, 1e-6f);
            _rig.Drag(new Vec2(-1, 1), new Vec2(1, -1));
            Assert.AreEqual(1, _rig.Snap.Sheet.ActionCount, "the drag folded with the book open");
            _rig.Settle();
            Assert.IsTrue(_rig.Snap.BookOpen);
            _rig.Key(GameKeys.Book);
            Assert.AreEqual(OrigamiGui.BookPlacement.None, _rig.Gui.Book);
        }

        [Test]
        public void F013_AC03_WalkHintOnlyBeforeTheFirstSit()
        {
            _rig.Idle(0.1f);
            Assert.IsTrue(_rig.Shows(GameTexts.WalkHint));
            _rig.Sit();
            Assert.IsFalse(_rig.Shows(GameTexts.WalkHint));
            Assert.IsTrue(_rig.Shows(GameTexts.TakeSheetHint), "first sheet hint while seated");
            _rig.Key(GameKeys.StandUp);
            _rig.Idle(0.1f);
            Assert.IsFalse(_rig.Shows(GameTexts.WalkHint));
        }

        [Test]
        public void F013_SeatedButtons_AreTheSameCommandsAsKeys()
        {
            _rig.Sit();
            CollectionAssert.AreEqual(new[] { "종이 꺼내기", "책", "일어나기" }, Labels(_rig.Gui.SideButtons));
            Assert.IsTrue(_rig.Gui.Press("종이 꺼내기"));
            Assert.IsNotNull(_rig.Snap.Sheet);
            CollectionAssert.AreEqual(new[] { "완성", "버리기", "되돌리기", "처음으로", "뒤집기", "책", "일어나기", "가이드 끄기" },
                Labels(_rig.Gui.SideButtons));
            Assert.IsTrue(_rig.Gui.Press("뒤집기"));
            Assert.AreEqual(SheetChangeKind.Flipped, _rig.Flow.LastSheetChange.Kind);
            Assert.IsTrue(_rig.Snap.Sheet.CanUndo);
            Assert.IsTrue(_rig.Gui.Press("가이드 끄기"));
            Assert.IsFalse(_rig.Snap.Sheet.GuideVisible);
            Assert.IsTrue(_rig.Gui.Press("버리기"));
            Assert.IsNull(_rig.Snap.Sheet);
            Assert.AreEqual(1, _rig.Snap.FailureCount);
            Assert.IsTrue(_rig.Gui.Press("일어나기"));
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            _rig.Idle(0.02f);
            Assert.AreEqual(2.5f, Pos.x, 0.05f, "a button stand-up also moves the character");
            Assert.AreEqual(0.2f, Pos.y, 0.05f);
        }

        private static string[] Labels(System.Collections.Generic.IReadOnlyList<OrigamiGui.GuiButton> buttons)
        {
            var a = new string[buttons.Count];
            for (int i = 0; i < a.Length; i++) a[i] = buttons[i].Label;
            return a;
        }
    }
}
