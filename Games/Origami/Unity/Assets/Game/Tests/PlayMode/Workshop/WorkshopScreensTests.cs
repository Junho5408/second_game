using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;
using Origami.Core.Recognition;
using Origami.Core.Screens;
using Origami.Core.Texts;

namespace Origami.Tests.PlayMode.Workshop
{
    /// <summary>
    /// ORIGAMI-014: counter display and trash (F-010 AC-06), counter screen (F-011 AC-07), result screen (F-012 AC-09), toasts
    /// (F-013 2 s, rejected book purchase — review ORIGAMI-013 W-3, cleared on a new run — review ORIGAMI-011 N-2).
    /// </summary>
    public class WorkshopScreensTests
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

        /// <summary>FEATURE_SPECS <c>Ref(m)</c> through the flow (seated): TakeSheet → the CONTENT §2 sequence → Finish.</summary>
        private void Ref(string modelId)
        {
            var f = _rig.Flow;
            Assert.IsTrue(f.TakeSheet().Accepted, "TakeSheet");
            foreach (var step in ModelCatalog.Find(modelId).Steps)
            {
                CommandResult r;
                switch (step.Kind)
                {
                    case FoldStepKind.Crease: r = f.Crease(step.Grab, step.Drop, step.Layers); break;
                    case FoldStepKind.Flip: r = f.Flip(); break;
                    default: r = f.Fold(step.Grab, step.Drop, step.Mountain, step.Layers); break;
                }
                Assert.IsTrue(r.Accepted, modelId + " step");
            }
            Assert.IsTrue(f.Finish().Accepted, "Finish");
            _rig.Idle(0.02f);
        }

        private void Waste()
        {
            Assert.IsTrue(_rig.Flow.TakeSheet().Accepted);
            Assert.IsTrue(_rig.Flow.Discard().Accepted);
        }

        private void Night()
        {
            var f = _rig.Flow;
            if (f.Snapshot.Mode == WorkshopMode.Seated) f.StandUp();
            Assert.IsTrue(f.Interact(InteractTargets.Bed).Accepted);
            Assert.IsTrue(f.Sleep(true).Accepted);
            f.AcknowledgeSettlement();
            _rig.Idle(0.02f);
        }

        [Test]
        public void F010_AC06_DisplayShowsFoldedShapesInSlotOrder_TrashShowsFailures()
        {
            _rig.Sit();
            Ref(ModelIds.House);
            var w = _rig.App.Workshop;
            Assert.AreEqual(1, w.DisplayedPieceCount);
            Assert.IsTrue(w.Slot(0).activeSelf);
            Assert.IsFalse(w.Slot(1).activeSelf);
            Assert.Greater(w.SlotMesh(0).vertexCount, 0, "the folded shape is drawn");
            int houseVerts = w.SlotMesh(0).vertexCount;

            Ref(ModelIds.Plane);
            Assert.AreEqual(2, w.DisplayedPieceCount);
            Assert.IsTrue(w.Slot(1).activeSelf);
            Assert.AreEqual(houseVerts, w.SlotMesh(0).vertexCount, "slot 1 keeps the first piece");
            Assert.AreNotSame(_rig.Snap.Display[0].Shape, _rig.Snap.Display[1].Shape);
            Assert.AreEqual(0, w.TrashBallCount);

            // Crumpled sheet → trash.
            Assert.IsTrue(_rig.Flow.TakeSheet().Accepted);
            Assert.IsTrue(_rig.Flow.Fold(new Vec2(-1, 1), new Vec2(-1, -1), false, 0).Accepted);
            var r = _rig.Flow.Finish();
            Assert.IsFalse(r.Recognition.IsPiece);
            _rig.Idle(0.02f);
            Assert.AreEqual(2, w.DisplayedPieceCount, "no display change");
            Assert.AreEqual(1, w.TrashBallCount);
        }

        [Test]
        public void F010_AC06_TrashBallsCapAt10()
        {
            _rig.Sit();
            for (int i = 0; i < 5; i++) Waste();
            Night();
            _rig.Flow.Interact(InteractTargets.Table);
            for (int i = 0; i < 5; i++) Waste();
            Night();
            _rig.Flow.Interact(InteractTargets.Table);
            Waste();
            _rig.Idle(0.02f);
            Assert.AreEqual(11, _rig.Snap.FailureCount);
            Assert.AreEqual(10, _rig.App.Workshop.TrashBallCount);
            Assert.AreEqual(0, _rig.App.Workshop.DisplayedPieceCount);
        }

        [Test]
        public void F011_AC07_CounterShowsPopularPricesAndTotal()
        {
            _rig.Sit();
            Ref(ModelIds.House);
            Ref(ModelIds.Plane);
            _rig.Key(GameKeys.StandUp);
            _rig.Place(2.5, -1.0);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Counter, _rig.Mode);
            foreach (var line in GameTexts.CounterLines(_rig.Snap)) Assert.IsTrue(_rig.Shows(line), line);
            Assert.IsTrue(_rig.Shows("1일차 판매대"));
            Assert.IsTrue(_rig.Shows("오늘의 인기: 종이비행기"));
            int total = Pricing.SalePrice(ModelIds.House, Grade.Star3, 1) + Pricing.SalePrice(ModelIds.Plane, Grade.Star3, 1);
            Assert.IsTrue(_rig.Shows("합계 예상 " + total + "원"));
            Assert.IsTrue(_rig.Gui.Press("닫기"));
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Counter, _rig.Mode);
            _rig.Key(GameKeys.Escape);
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
        }

        [Test]
        public void F012_AC09_ResultShowsEndingAndRecords_EnterStartsANewRun()
        {
            for (int day = 1; day <= 7; day++)
            {
                _rig.Place(-1.5, 1.5);
                _rig.Key(GameKeys.Interact);
                Assert.AreEqual(WorkshopMode.SleepConfirm, _rig.Mode, "day " + day);
                _rig.Key(GameKeys.Enter);
                Assert.AreEqual(Overlay.Settlement, _rig.Flow.Overlay);
                if (day < 7) _rig.Key(GameKeys.Enter);
            }
            Assert.IsTrue(_rig.Snap.IsEnded);
            Assert.AreEqual("결과 보기", _rig.Gui.PanelButtons[0].Label);
            Assert.IsTrue(_rig.Shows("7일이 모두 지났어요"));
            _rig.Key(GameKeys.Enter);
            Assert.AreEqual(Overlay.Result, _rig.Flow.Overlay);
            Assert.IsTrue(_rig.Shows("빈손"), "E-01 name (CONTENT §8)");
            Assert.IsTrue(_rig.Shows("종이는 다 떨어졌지만, 종이접기는 취미로 남았다."), "E-01 text (CONTENT §8)");
            Assert.IsTrue(_rig.Shows("최종 돈 20원"));
            Assert.IsTrue(_rig.Shows("실패 0"));
            Assert.IsTrue(_rig.Shows("종이비행기: ★3 0 · ★2 0 · ★1 0"));
            Assert.IsTrue(_rig.Shows("총 판매액 0원"));
            Assert.IsTrue(_rig.Shows("총 종이값 0원"));
            Assert.IsTrue(_rig.Shows("총 책값 0원"));
            Assert.AreEqual("새로 시작", _rig.Gui.PanelButtons[0].Label);

            _rig.Key(GameKeys.Enter);
            Assert.AreEqual(Overlay.None, _rig.Flow.Overlay);
            Assert.IsFalse(_rig.Snap.IsEnded);
            Assert.AreEqual(1, _rig.Snap.Day);
            Assert.AreEqual(-1.6f, _rig.App.CharacterPosition.x, 0.05f);
            Assert.IsTrue(_rig.Shows(GameTexts.WalkHint), "fresh run");
        }

        [Test]
        public void F013_RejectToastShowsFor2Seconds()
        {
            _rig.Sit();
            _rig.Key(GameKeys.TakeSheet);
            _rig.Key(GameKeys.TakeSheet);
            const string text = "테이블 위 종이를 먼저 완성하거나 버리세요";
            Assert.AreEqual(text, _rig.Gui.Toast);
            Assert.IsTrue(_rig.Shows(text));
            _rig.Idle(1.9f);
            Assert.IsTrue(_rig.Shows(text), "still shown at 1.9 s");
            _rig.Idle(0.2f);
            Assert.IsFalse(_rig.Shows(text), "gone after 2 s");
            Assert.AreEqual("", _rig.Gui.Toast);
        }

        [Test]
        public void F013_RecognitionResultIsShownAfterFinish()
        {
            _rig.Sit();
            Assert.IsTrue(_rig.Flow.TakeSheet().Accepted);
            Assert.IsTrue(_rig.Flow.Fold(new Vec2(-1, 1), new Vec2(-1, -1), false, 0).Accepted);
            _rig.Key(GameKeys.Enter);
            StringAssert.StartsWith("구겨진 종이 (가장 비슷한 작품: 집 ", _rig.Gui.Toast);
        }

        [Test]
        public void F013_InvalidDragReleaseShowsTheFoldError()
        {
            _rig.Sit();
            _rig.Key(GameKeys.TakeSheet);
            _rig.Key(GameKeys.SkipGuide);
            _rig.Drag(new Vec2(-1, 1), new Vec2(-0.99, 0.99));
            Assert.AreEqual(FoldError.DragTooShort, _rig.App.Table.LastError, "a too-short drag does not fold");
            Assert.AreEqual(0, _rig.Snap.Sheet.ActionCount);
            Assert.AreEqual("조금 더 끌어 주세요", FoldTexts.ErrorMessage(FoldError.DragTooShort));
            Assert.AreEqual("조금 더 끌어 주세요", _rig.Gui.Toast);
        }

        [Test]
        public void W3_RejectedBookPurchaseShowsTheRejectMessage()
        {
            _rig.Place(0.0, 1.5);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Bookshelf, _rig.Mode);
            _rig.App.Book.ClickBuy(ModelIds.Dog);   // 50원 > 20원
            _rig.Idle(0.02f);
            Assert.AreEqual(20, _rig.Snap.Money);
            string text = GameTexts.RejectMessage(RejectReason.NotEnoughMoney, FoldError.None);
            Assert.AreEqual("돈이 모자라요", text);
            Assert.AreEqual(text, _rig.Gui.Toast);
            Assert.IsTrue(_rig.Shows(text));

            _rig.App.Book.ClickBuy(ModelIds.Tulip);   // 20원 = 20원 → accepted
            _rig.Idle(0.02f);
            CollectionAssert.Contains(_rig.Snap.OwnedModelIds, ModelIds.Tulip);
            _rig.App.Book.ClickBuy(ModelIds.Tulip);
            _rig.Idle(0.02f);
            Assert.AreEqual("이미 가진 페이지예요", _rig.Gui.Toast);
        }

        [Test]
        public void N2_NewRunClearsALeftoverRejectToast()
        {
            _rig.Sit();
            _rig.Key(GameKeys.TakeSheet);
            _rig.Key(GameKeys.TakeSheet);
            Assert.AreNotEqual("", _rig.Gui.Toast);
            _rig.Key(GameKeys.Discard);
            _rig.Key(GameKeys.StandUp);
            _rig.Key(GameKeys.Escape);
            _rig.Key(GameKeys.Enter);
            Assert.AreNotEqual("", _rig.Gui.Toast, "still within 2 s");
            _rig.Key(GameKeys.Enter);
            Assert.AreEqual(SheetChangeKind.Reset, _rig.Flow.LastSheetChange.Kind);
            Assert.AreEqual("", _rig.Gui.Toast, "cleared by the new run");
        }
    }
}
