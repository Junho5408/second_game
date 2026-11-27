using System;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Screens;
using Origami.Core.Texts;
using static Origami.Tests.Screens.FlowScenarios;

namespace Origami.Tests.Screens
{
    // C-171 ScreenFlow — overlays (A2-11), messages, sheet changes, one-time hints (F-013), settlement/result flow (F-011/F-012).
    public class ScreenFlowTests
    {
        // ------------------------------------------------------------------------------------------------ start state

        [Test]
        public void NewFlow_StartsAtNewRunWithoutOverlayOrMessages()
        {
            var f = new ScreenFlow();
            Assert.AreEqual(1, f.Snapshot.Day);
            Assert.AreEqual(20, f.Snapshot.Money);
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
            Assert.AreEqual(Overlay.None, f.Overlay);
            Assert.IsNull(f.PendingSettlement);
            Assert.AreEqual(0, f.Message.Serial);
            Assert.AreEqual(MessageKind.None, f.Message.Kind);
            Assert.AreEqual("", f.Message.Text);
            Assert.AreEqual(0, f.LastSheetChange.Serial);
            Assert.AreEqual(SheetChangeKind.None, f.LastSheetChange.Kind);
            Assert.IsNull(f.LastSheetChange.Plan);
            Assert.IsNull(f.LastSheetChange.Previous);
        }

        [Test]
        public void Commands_ReturnTheCoreResultAndUpdateTheSnapshot()
        {
            var f = new ScreenFlow();
            Sit(f);
            Take(f);
            var r = f.Fold(V(-1, 1), V(-1, -1), false, 0);
            Assert.IsTrue(r.Accepted);
            Assert.IsNotNull(r.Plan);
            Assert.AreSame(r.Plan.Result, f.Snapshot.Sheet.State);
            var bad = f.Crease(V(0, 0), V(0, 0), 0);
            Assert.AreEqual(RejectReason.InvalidFold, bad.Reason);
            Assert.AreEqual(FoldError.DragTooShort, bad.FoldError);
            Assert.AreEqual(RejectReason.WrongMode, f.CloseCounter().Reason);
            Ok(f.OpenBook());
            Assert.AreEqual(RejectReason.UnknownModel, f.BuyPage("unicorn").Reason);
        }

        // ------------------------------------------------------------------------------------------------ overlays (A2-11)

        [Test]
        public void A2_11_PauseMenu_BlocksGameCommandsWithWrongMode()
        {
            var f = new ScreenFlow();
            f.OpenPause();
            Assert.AreEqual(Overlay.PauseMenu, f.Overlay);
            var before = f.Snapshot;
            Func<CommandResult>[] commands =
            {
                () => f.Interact(InteractTargets.Table), () => f.StandUp(), () => f.ToggleBook(), () => f.OpenBook(), () => f.CloseBook(),
                () => f.TurnPage(4), () => f.TurnPageBy(2), () => f.BuyPage("tulip"), () => f.CloseCounter(), () => f.Sleep(true),
                () => f.TakeSheet(), () => f.Fold(V(-1, 1), V(-1, -1), false, 0), () => f.Crease(V(-1, 1), V(1, 1), 0), () => f.Flip(),
                () => f.Undo(), () => f.Unfold(), () => f.Finish(), () => f.Discard(), () => f.SkipGuide(),
            };
            foreach (var c in commands)
            {
                var r = c();
                Assert.IsFalse(r.Accepted);
                Assert.AreEqual(RejectReason.WrongMode, r.Reason);
            }
            Assert.AreSame(before, f.Snapshot);
            Assert.AreEqual(0, f.Message.Serial);
            Assert.AreEqual(Overlay.PauseMenu, f.Overlay);
        }

        [Test]
        public void A2_11_SettlementOverlay_BlocksWalking()
        {
            var f = new ScreenFlow();
            Ok(f.Interact(InteractTargets.Bed));
            Ok(f.Sleep(true));
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
            Assert.AreEqual(RejectReason.WrongMode, f.Interact(InteractTargets.Table).Reason);
            f.AcknowledgeSettlement();
            Assert.IsTrue(f.Interact(InteractTargets.Table).Accepted);
        }

        [Test]
        public void OverlayOperations_OnlyActInTheirState()
        {
            var f = new ScreenFlow();
            f.ClosePause();
            f.RequestNewRun();
            f.AcknowledgeSettlement();
            f.ConfirmNewRun();
            Assert.AreEqual(Overlay.None, f.Overlay);
            Assert.AreEqual(0, f.LastSheetChange.Serial, "ConfirmNewRun without the dialog does nothing");

            Sit(f);
            f.OpenPause();
            Assert.AreEqual(Overlay.None, f.Overlay, "pause only while walking");
            Stand(f);
            f.OpenPause();
            Assert.AreEqual(Overlay.PauseMenu, f.Overlay);
            f.OpenPause();
            f.AcknowledgeSettlement();
            f.ConfirmNewRun();
            Assert.AreEqual(Overlay.PauseMenu, f.Overlay, "ConfirmNewRun needs the confirm dialog");
            Assert.IsTrue(f.Snapshot.HasSat, "no new run happened");
            f.RequestNewRun();
            Assert.AreEqual(Overlay.ConfirmNewRun, f.Overlay);
            f.RequestNewRun();
            f.OpenPause();
            Assert.AreEqual(Overlay.ConfirmNewRun, f.Overlay);
            f.ClosePause();
            Assert.AreEqual(Overlay.None, f.Overlay);
        }

        [Test]
        public void F001_AC07_NewRunFromThePauseMenuDiscardsTheRun()
        {
            var f = new ScreenFlow();
            Night(f);
            Sit(f);
            Take(f);
            Ok(f.Fold(V(-1, 1), V(-1, -1), false, 0));
            var sheet = f.Snapshot.Sheet.State;
            Stand(f);
            f.OpenPause();
            f.RequestNewRun();
            int serial = f.LastSheetChange.Serial;
            f.ConfirmNewRun();
            Assert.AreEqual(Overlay.None, f.Overlay);
            Assert.AreEqual(1, f.Snapshot.Day);
            Assert.AreEqual(20, f.Snapshot.Money);
            Assert.IsNull(f.Snapshot.Sheet);
            Assert.IsTrue(f.Snapshot.FreeSheetAvailable);
            Assert.AreEqual(SheetChangeKind.Reset, f.LastSheetChange.Kind);
            Assert.AreEqual(serial + 1, f.LastSheetChange.Serial);
            Assert.AreSame(sheet, f.LastSheetChange.Previous);
        }

        [Test]
        public void F011_SleepYes_ShowsSettlementThenNextDay()
        {
            var f = new ScreenFlow();
            Sit(f);
            Ref(f, ModelIds.House);
            Stand(f);
            Ok(f.Interact(InteractTargets.Bed));
            var r = Ok(f.Sleep(true));
            Assert.AreEqual(Overlay.Settlement, f.Overlay);
            Assert.AreSame(r.Settlement, f.PendingSettlement);
            Assert.AreEqual(12, f.PendingSettlement.Total);
            f.AcknowledgeSettlement();
            Assert.AreEqual(Overlay.None, f.Overlay);
            Assert.IsNull(f.PendingSettlement);
            Assert.AreEqual(2, f.Snapshot.Day);
        }

        [Test]
        public void SleepNo_DoesNotOpenSettlement()
        {
            var f = new ScreenFlow();
            Ok(f.Interact(InteractTargets.Bed));
            Ok(f.Sleep(false));
            Assert.AreEqual(Overlay.None, f.Overlay);
            Assert.IsNull(f.PendingSettlement);
        }

        [Test]
        public void F012_LastNight_SettlementThenResultThenNewRun()
        {
            var f = new ScreenFlow();
            for (int d = 0; d < 6; d++) Night(f);
            Ok(f.Interact(InteractTargets.Bed));
            Ok(f.Sleep(true));
            Assert.AreEqual(Overlay.Settlement, f.Overlay);
            Assert.IsTrue(f.Snapshot.IsEnded);
            Assert.AreEqual(EndingIds.EmptyHanded, f.Snapshot.EndingId);
            f.AcknowledgeSettlement();
            Assert.AreEqual(Overlay.Result, f.Overlay);
            Assert.IsNull(f.PendingSettlement);
            Assert.AreEqual("빈손", GameTexts.ResultLines(f.Snapshot)[0]);
            Assert.AreEqual(RejectReason.WrongMode, f.Interact(InteractTargets.Table).Reason, "the overlay rejects first (A2-11)");
            f.OpenPause();
            Assert.AreEqual(Overlay.Result, f.Overlay);
            f.ConfirmNewRun();
            Assert.AreEqual(Overlay.None, f.Overlay);
            Assert.IsFalse(f.Snapshot.IsEnded);
            Assert.AreEqual(1, f.Snapshot.Day);
        }

        [Test]
        public void F001_AC05_BankruptcyEndsThroughSettlementAndResult()
        {
            var f = new ScreenFlow();
            Sit(f);
            for (int i = 0; i < 5; i++) { Take(f); Ok(f.Discard()); }
            Night(f);
            Sit(f);
            for (int i = 0; i < 5; i++) { Take(f); Ok(f.Discard()); }
            Night(f);
            Sit(f);
            Take(f);
            Ok(f.Discard());
            Stand(f);
            Ok(f.Interact(InteractTargets.Bed));
            Ok(f.Sleep(true));
            Assert.AreEqual(Overlay.Settlement, f.Overlay);
            Assert.AreEqual(4, f.Snapshot.Day);
            Assert.AreEqual(0, f.Snapshot.Money);
            Assert.IsTrue(f.Snapshot.IsEnded);
            Assert.IsTrue(f.ProcessKeys(GameKeys.Enter, null));
            Assert.AreEqual(Overlay.Result, f.Overlay);
            Assert.IsTrue(f.ProcessKeys(GameKeys.Enter, null));
            Assert.AreEqual(Overlay.None, f.Overlay);
            Assert.AreEqual(1, f.Snapshot.Day);
        }

        // ------------------------------------------------------------------------------------------------ messages

        [Test]
        public void F013_Messages_RejectTextsAndRecognitionLines()
        {
            var f = new ScreenFlow();
            Sit(f);
            f.Finish();                                   // NoSheet: no text, no message
            Assert.AreEqual(0, f.Message.Serial);
            Take(f);
            f.Finish();                                   // NoFolds
            Assert.AreEqual(1, f.Message.Serial);
            Assert.AreEqual(MessageKind.Reject, f.Message.Kind);
            Assert.AreEqual("아직 접지 않았어요", f.Message.Text);
            f.Finish();                                   // the same message again is a new message
            Assert.AreEqual(2, f.Message.Serial);
            f.Fold(V(0, 0), V(0, 0), false, 0);
            Assert.AreEqual(3, f.Message.Serial);
            Assert.AreEqual("조금 더 끌어 주세요", f.Message.Text);
            f.Fold(V(0, 0), V(0.5, 0), false, -1);        // InvalidLayers has no text
            Assert.AreEqual(3, f.Message.Serial);
            f.Interact(InteractTargets.Bed);              // WrongMode: silent
            Assert.AreEqual(3, f.Message.Serial);

            foreach (var s in ModelCatalog.Find(ModelIds.Plane).Steps) Apply(f, s);   // accepted commands keep the message
            Assert.AreEqual(3, f.Message.Serial);
            var r = Ok(f.Finish());
            Assert.AreEqual(4, f.Message.Serial);
            Assert.AreEqual(MessageKind.Recognition, f.Message.Kind);
            Assert.AreEqual("종이비행기 ★3 (일치도 100%)", f.Message.Text);
            Assert.AreEqual(GameTexts.RecognitionLine(r.Recognition), f.Message.Text);
        }

        [Test]
        public void F013_Messages_SleepWithSheetOnTableAndAlreadyOwned()
        {
            var f = new ScreenFlow();
            Sit(f);
            Take(f);
            Stand(f);
            f.Interact(InteractTargets.Bed);
            Assert.AreEqual("테이블 위 종이를 먼저 완성하거나 버리세요", f.Message.Text);
            Ok(f.Interact(InteractTargets.Bookshelf));
            f.BuyPage(ModelIds.Plane);
            Assert.AreEqual("이미 가진 페이지예요", f.Message.Text);
            f.BuyPage(ModelIds.Dog);
            Assert.AreEqual("돈이 모자라요", f.Message.Text);
            Assert.AreEqual(MessageKind.Reject, f.Message.Kind);
        }

        // ------------------------------------------------------------------------------------------------ sheet changes

        [Test]
        public void SheetChanges_FollowEveryAcceptedSheetCommand()
        {
            var f = new ScreenFlow();
            Sit(f);
            Take(f);
            Expect(f, 1, SheetChangeKind.Taken, null);

            var s0 = f.Snapshot.Sheet.State;
            var crease = Ok(f.Crease(V(-1, 1), V(1, 1), 0));
            Expect(f, 2, SheetChangeKind.Creased, s0);
            Assert.AreSame(crease.Plan, f.LastSheetChange.Plan);

            var s1 = f.Snapshot.Sheet.State;
            var fold = Ok(f.Fold(V(-1, 1), V(0, 0), false, 0));
            Expect(f, 3, SheetChangeKind.Folded, s1);
            Assert.AreSame(fold.Plan, f.LastSheetChange.Plan);

            var s2 = f.Snapshot.Sheet.State;
            Ok(f.Flip());
            Expect(f, 4, SheetChangeKind.Flipped, s2);
            Assert.IsNull(f.LastSheetChange.Plan);

            var s3 = f.Snapshot.Sheet.State;
            Ok(f.Undo());
            Expect(f, 5, SheetChangeKind.Undone, s3);

            var s4 = f.Snapshot.Sheet.State;
            Ok(f.Unfold());
            Expect(f, 6, SheetChangeKind.Unfolded, s4);

            // rejected commands do not change it
            f.Undo();
            f.Fold(V(0, 0), V(0, 0), false, 0);
            f.TakeSheet();
            Assert.AreEqual(6, f.LastSheetChange.Serial);

            Ok(f.Fold(V(-1, 1), V(-1, -1), false, 0));
            var s5 = f.Snapshot.Sheet.State;
            Ok(f.Finish());
            Expect(f, 8, SheetChangeKind.Removed, s5);

            Take(f);
            var s6 = f.Snapshot.Sheet.State;
            Ok(f.Discard());
            Expect(f, 10, SheetChangeKind.Removed, s6);

            // non-sheet commands do not change it
            Ok(f.OpenBook());
            Ok(f.TurnPage(6));
            Stand(f);
            Assert.AreEqual(10, f.LastSheetChange.Serial);
        }

        private static void Expect(ScreenFlow f, int serial, SheetChangeKind kind, PaperState previous)
        {
            Assert.AreEqual(serial, f.LastSheetChange.Serial, kind.ToString());
            Assert.AreEqual(kind, f.LastSheetChange.Kind);
            Assert.AreSame(previous, f.LastSheetChange.Previous, kind + " previous");
            if (kind != SheetChangeKind.Folded && kind != SheetChangeKind.Creased) Assert.IsNull(f.LastSheetChange.Plan);
        }

        // ------------------------------------------------------------------------------------------------ one-time hints (F-013)

        [Test]
        public void F013_AC03_WalkHintUntilTheFirstSit()
        {
            var f = new ScreenFlow();
            Assert.IsTrue(f.WalkHintVisible);
            Ok(f.Interact(InteractTargets.Bookshelf));
            Assert.IsFalse(f.WalkHintVisible, "walking only");
            Ok(f.CloseBook());
            Assert.IsTrue(f.WalkHintVisible);
            Sit(f);
            Assert.IsFalse(f.WalkHintVisible);
            Stand(f);
            Assert.IsFalse(f.WalkHintVisible, "until the first sit of the run");
            f.OpenPause();
            f.RequestNewRun();
            f.ConfirmNewRun();
            Assert.IsTrue(f.WalkHintVisible, "new run");
        }

        [Test]
        public void F013_TakeSheetHintBeforeTheFirstSheet()
        {
            var f = new ScreenFlow();
            Assert.IsFalse(f.TakeSheetHintVisible, "walking");
            Sit(f);
            Assert.IsTrue(f.TakeSheetHintVisible);
            Ok(f.OpenBook());
            Assert.IsTrue(f.TakeSheetHintVisible);
            Take(f);
            Assert.IsFalse(f.TakeSheetHintVisible);
            Ok(f.Discard());
            Assert.IsFalse(f.TakeSheetHintVisible, "only before the run's first sheet");
        }

        [Test]
        public void F013_PostTutorialHint_AfterFinishingTheTutorialSheetUntilTakeSheet()
        {
            var f = new ScreenFlow();
            Sit(f);
            Take(f);
            Assert.IsFalse(f.PostTutorialHintVisible);
            Ref2(f);
            Assert.IsTrue(f.PostTutorialHintVisible);
            Assert.AreEqual("이제부터 접는 법은 책에서만 볼 수 있어요. 앉아서 B 또는 책장에서 E", GameTexts.PostTutorialHint);
            f.Undo();                    // rejected
            Stand(f);
            Sit(f);
            Assert.IsTrue(f.PostTutorialHintVisible, "stays until TakeSheet / OpenBook / bookshelf");
            f.Interact(InteractTargets.Bookshelf);   // rejected (WrongMode) does not clear
            Assert.IsTrue(f.PostTutorialHintVisible);
            Take(f);
            Assert.IsFalse(f.PostTutorialHintVisible);
            Ok(f.Discard());
            Assert.IsFalse(f.PostTutorialHintVisible, "only the tutorial sheet shows it");
        }

        private static void Ref2(ScreenFlow f)
        {
            foreach (var s in ModelCatalog.Find(ModelIds.Plane).Steps) Apply(f, s);
            Ok(f.Finish());
        }

        [Test]
        public void F013_PostTutorialHint_ClearedByOpeningTheBook()
        {
            var f = new ScreenFlow();
            Sit(f);
            Take(f);
            Ok(f.Discard());             // discarding the tutorial sheet also ends the tutorial
            Assert.IsTrue(f.PostTutorialHintVisible);
            Ok(f.ToggleBook());
            Assert.IsTrue(f.Snapshot.BookOpen);
            Assert.IsFalse(f.PostTutorialHintVisible);
        }

        [Test]
        public void F013_PostTutorialHint_ClearedByTheBookshelf()
        {
            var f = new ScreenFlow();
            Sit(f);
            Take(f);
            Ok(f.Discard());
            Stand(f);
            Assert.IsTrue(f.PostTutorialHintVisible);
            Ok(f.Interact(InteractTargets.Counter));
            Ok(f.CloseCounter());
            Assert.IsTrue(f.PostTutorialHintVisible, "other targets keep it");
            Assert.IsTrue(f.ProcessKeys(GameKeys.Interact, InteractTargets.Bookshelf));
            Assert.IsFalse(f.PostTutorialHintVisible);
        }

        [Test]
        public void F013_PostTutorialHint_ClearedByNewRun()
        {
            var f = new ScreenFlow();
            Sit(f);
            Take(f);
            Ok(f.Discard());
            Stand(f);
            f.OpenPause();
            f.RequestNewRun();
            f.ConfirmNewRun();
            Assert.IsFalse(f.PostTutorialHintVisible);
        }

        // ------------------------------------------------------------------------------------------------ book helpers

        [Test]
        public void ToggleBook_OpensClosesAndLeavesTheBookshelf()
        {
            var f = new ScreenFlow();
            Assert.AreEqual(RejectReason.WrongMode, f.ToggleBook().Reason, "walking");
            Sit(f);
            Ok(f.ToggleBook());
            Assert.IsTrue(f.Snapshot.BookOpen);
            Ok(f.ToggleBook());
            Assert.IsFalse(f.Snapshot.BookOpen);
            Assert.AreEqual(WorkshopMode.Seated, f.Snapshot.Mode);
            Stand(f);
            Ok(f.Interact(InteractTargets.Bookshelf));
            Ok(f.ToggleBook());
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
        }

        [Test]
        public void TurnPageBy_ClampsLikeTurnPageWithoutOverflow()
        {
            var f = new ScreenFlow();
            Assert.AreEqual(RejectReason.WrongMode, f.TurnPageBy(2).Reason);
            Ok(f.Interact(InteractTargets.Bookshelf));
            Ok(f.TurnPageBy(17));
            Assert.AreEqual(16, f.Snapshot.BookPage);
            Ok(f.TurnPageBy(-2));
            Assert.AreEqual(14, f.Snapshot.BookPage);
            Ok(f.TurnPageBy(int.MaxValue));
            Assert.AreEqual(42, f.Snapshot.BookPage);
            Ok(f.TurnPageBy(int.MinValue));
            Assert.AreEqual(0, f.Snapshot.BookPage);
        }

        [Test]
        public void F007_AC05_SeatedPageKeysNeedTheOpenBook()
        {
            var f = new ScreenFlow();
            Sit(f);
            Assert.IsFalse(f.ProcessKeys(GameKeys.PageRight, null));
            Assert.AreEqual(RejectReason.WrongMode, f.TurnPageBy(2).Reason);
            Assert.IsTrue(f.ProcessKeys(GameKeys.Book, null));
            Assert.IsTrue(f.ProcessKeys(GameKeys.PageRight, null));
            Assert.AreEqual(2, f.Snapshot.BookPage);
            Take(f);
            Assert.IsTrue(f.Fold(V(-1, 1), V(-1, -1), false, 0).Accepted, "folding with the book open");
        }

        [Test]
        public void Determinism_SameKeySequenceGivesSameState()
        {
            ScreenFlow Run()
            {
                var f = new ScreenFlow();
                f.ProcessKeys(GameKeys.Interact, InteractTargets.Table);
                f.ProcessKeys(GameKeys.TakeSheet, null);
                foreach (var s in ModelCatalog.Find(ModelIds.House).Steps) Apply(f, s);
                f.ProcessKeys(GameKeys.Enter, null);
                f.ProcessKeys(GameKeys.StandUp, null);
                f.ProcessKeys(GameKeys.Interact, InteractTargets.Bed);
                f.ProcessKeys(GameKeys.Enter, null);
                f.ProcessKeys(GameKeys.Enter, null);
                return f;
            }
            var a = Run();
            var b = Run();
            Assert.AreEqual(2, a.Snapshot.Day);
            Assert.AreEqual(32, a.Snapshot.Money);
            Assert.AreEqual(a.Snapshot.Money, b.Snapshot.Money);
            Assert.AreEqual(a.Message.Serial, b.Message.Serial);
            Assert.AreEqual(a.Message.Text, b.Message.Text);
            Assert.AreEqual(a.LastSheetChange.Serial, b.LastSheetChange.Serial);
            Assert.AreEqual(a.Overlay, b.Overlay);
        }
    }
}
