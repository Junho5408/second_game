using System;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Game;
using static Origami.Tests.Qa.WsDriver;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-010: rejection-order matrix from the C-150 table. Each case builds a state in which two or more rejection
    /// causes hold at once and checks that the first one in the table wins, that the snapshot reference is unchanged (R-019),
    /// and the boundary that is accepted.
    /// </summary>
    [TestFixture]
    public class WorkshopRejectionOrderQaTests
    {
        private static readonly Func<OrigamiGame, CommandResult>[] AllCommands =
        {
            x => x.Interact(Table), x => x.Interact(null), x => x.Interact("nowhere"), x => x.StandUp(), x => x.OpenBook(),
            x => x.CloseBook(), x => x.TurnPage(4), x => x.BuyPage("tulip"), x => x.BuyPage(null), x => x.CloseCounter(),
            x => x.Sleep(true), x => x.Sleep(false), x => x.TakeSheet(), x => x.Fold(V(-1, 1), V(-1, -1), false, 0),
            x => x.Fold(V(-1, 1), V(-1, -1), false, -1), x => x.Crease(V(-1, 1), V(1, 1), 0), x => x.Flip(), x => x.Undo(),
            x => x.Unfold(), x => x.Finish(), x => x.Discard(), x => x.SkipGuide(),
        };

        private static OrigamiGame Mode(WorkshopMode m)
        {
            var g = new OrigamiGame();
            switch (m)
            {
                case WorkshopMode.Walking: break;
                case WorkshopMode.Seated: Sit(g); break;
                case WorkshopMode.Bookshelf: Ok(g, x => x.Interact(Bookshelf)); break;
                case WorkshopMode.Counter: Ok(g, x => x.Interact(Counter)); break;
                case WorkshopMode.SleepConfirm: Ok(g, x => x.Interact(Bed)); break;
                case WorkshopMode.Ended: Times(7, () => Night(g)); break;
            }
            Assert.That(g.Snapshot.Mode, Is.EqualTo(m));
            return g;
        }

        [Test]
        public void RunEnded_IsFirstForEveryCommandAndArgument()
        {
            var g = Mode(WorkshopMode.Ended);
            for (int i = 0; i < AllCommands.Length; i++) Rej(g, AllCommands[i], RejectReason.RunEnded, "cmd #" + i);
            Rej(g, x => x.TurnPage(-5), RejectReason.RunEnded);
            Rej(g, x => x.BuyPage("unicorn"), RejectReason.RunEnded);
            Ok(g, x => x.NewRun());
            Assert.That(g.Snapshot.Day, Is.EqualTo(1));
        }

        [Test]
        public void RunEnded_ByBankruptcy_AlsoRejectsEverything()
        {
            var g = new OrigamiGame();
            Sit(g); Times(5, () => Waste(g)); Night(g);
            Sit(g); Times(5, () => Waste(g)); Night(g);
            Sit(g); Waste(g); Night(g);
            Assert.That(g.Snapshot.IsEnded, Is.True);
            for (int i = 0; i < AllCommands.Length; i++) Rej(g, AllCommands[i], RejectReason.RunEnded, "cmd #" + i);
        }

        // Which commands each mode accepts (R-003 table); everything else is WrongMode (before any argument check).
        [TestCase(WorkshopMode.Walking)]
        [TestCase(WorkshopMode.Bookshelf)]
        [TestCase(WorkshopMode.Counter)]
        [TestCase(WorkshopMode.SleepConfirm)]
        public void NonSeatedModes_RejectSeatedCommandsWithWrongMode_EvenWithBadArguments(WorkshopMode m)
        {
            var g = Mode(m);
            Rej(g, x => x.StandUp(), RejectReason.WrongMode);
            Rej(g, x => x.OpenBook(), RejectReason.WrongMode);
            Rej(g, x => x.TakeSheet(), RejectReason.WrongMode);
            Rej(g, x => x.Fold(V(-1, 1), V(-1, -1), false, -1), RejectReason.WrongMode, "WrongMode before NoSheet/InvalidFold");
            Rej(g, x => x.Crease(V(double.NaN, 0), V(0, 0), -3), RejectReason.WrongMode);
            Rej(g, x => x.Flip(), RejectReason.WrongMode);
            Rej(g, x => x.Undo(), RejectReason.WrongMode);
            Rej(g, x => x.Unfold(), RejectReason.WrongMode);
            Rej(g, x => x.Finish(), RejectReason.WrongMode);
            Rej(g, x => x.Discard(), RejectReason.WrongMode);
            Rej(g, x => x.SkipGuide(), RejectReason.WrongMode, "WrongMode before NoGuide");
            if (m != WorkshopMode.Walking)
            {
                Rej(g, x => x.Interact(null), RejectReason.WrongMode, "WrongMode before UnknownTarget");
                Rej(g, x => x.Interact(Table), RejectReason.WrongMode);
            }
            if (m != WorkshopMode.Counter) Rej(g, x => x.CloseCounter(), RejectReason.WrongMode);
            if (m != WorkshopMode.SleepConfirm)
            {
                Rej(g, x => x.Sleep(true), RejectReason.WrongMode);
                Rej(g, x => x.Sleep(false), RejectReason.WrongMode);
            }
            if (m != WorkshopMode.Bookshelf)
            {
                Rej(g, x => x.TurnPage(2), RejectReason.WrongMode);
                Rej(g, x => x.BuyPage(null), RejectReason.WrongMode, "WrongMode before UnknownModel");
                Rej(g, x => x.BuyPage("cicada"), RejectReason.WrongMode, "WrongMode before NotEnoughMoney");
                Rej(g, x => x.BuyPage("plane"), RejectReason.WrongMode, "WrongMode before AlreadyOwned");
                Rej(g, x => x.CloseBook(), RejectReason.WrongMode);
            }
        }

        [Test]
        public void Seated_RejectsNonSeatedCommandsWithWrongMode()
        {
            var g = Mode(WorkshopMode.Seated);
            Rej(g, x => x.Interact(Bookshelf), RejectReason.WrongMode);
            Rej(g, x => x.Interact("garden"), RejectReason.WrongMode);
            Rej(g, x => x.CloseCounter(), RejectReason.WrongMode);
            Rej(g, x => x.Sleep(true), RejectReason.WrongMode);
            Rej(g, x => x.TurnPage(10), RejectReason.WrongMode, "book closed → not visible");
            Rej(g, x => x.BuyPage("unicorn"), RejectReason.WrongMode, "book closed → WrongMode before UnknownModel");
            Ok(g, x => x.CloseBook(), "CloseBook while closed is idempotent");
            Assert.That(g.Snapshot.Mode, Is.EqualTo(WorkshopMode.Seated));
        }

        [Test]
        public void Interact_UnknownTargetThenSheetOnTable()
        {
            var g = new OrigamiGame();
            Rej(g, x => x.Interact(null), RejectReason.UnknownTarget);
            Rej(g, x => x.Interact(""), RejectReason.UnknownTarget);
            Rej(g, x => x.Interact("Table"), RejectReason.UnknownTarget, "ordinal comparison");
            Rej(g, x => x.Interact(" bed"), RejectReason.UnknownTarget);
            Sit(g);
            Ok(g, x => x.TakeSheet());
            Stand(g);
            Rej(g, x => x.Interact("sofa"), RejectReason.UnknownTarget, "UnknownTarget before SheetOnTable");
            Rej(g, x => x.Interact(Bed), RejectReason.SheetOnTable);
            Ok(g, x => x.Interact(Counter), "only bed checks the sheet");
            Ok(g, x => x.CloseCounter());
            Ok(g, x => x.Interact(Bookshelf));
            Ok(g, x => x.CloseBook());
            Sit(g);
            Assert.That(g.Snapshot.Sheet, Is.Not.Null, "sheet stays on the table while walking");
        }

        [Test]
        public void TurnPage_AcceptedOnlyWhenBookVisible_AndClamps()
        {
            var g = Mode(WorkshopMode.Bookshelf);
            Ok(g, x => x.TurnPage(int.MinValue));
            Assert.That(g.Snapshot.BookPage, Is.EqualTo(0));
            Ok(g, x => x.TurnPage(int.MaxValue));
            Assert.That(g.Snapshot.BookPage, Is.EqualTo(42));
            Ok(g, x => x.TurnPage(21));
            Assert.That(g.Snapshot.BookPage, Is.EqualTo(20));
            Ok(g, x => x.CloseBook());
            Assert.That(g.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Sit(g);
            Rej(g, x => x.TurnPage(4), RejectReason.WrongMode);
            Ok(g, x => x.OpenBook());
            Assert.That(g.Snapshot.BookPage, Is.EqualTo(20), "page kept across close/open (F-007 AC-04)");
            Ok(g, x => x.TurnPage(33));
            Assert.That(g.Snapshot.BookPage, Is.EqualTo(32));
            Stand(g);
            Assert.That(g.Snapshot.BookOpen, Is.False, "standing closes the book");
            Ok(g, x => x.NewRun());
            Assert.That(g.Snapshot.BookPage, Is.EqualTo(0));
        }

        [Test]
        public void BuyPage_UnknownModelThenAlreadyOwnedThenNotEnoughMoney()
        {
            var g = Mode(WorkshopMode.Bookshelf);
            Rej(g, x => x.BuyPage(null), RejectReason.UnknownModel);
            Rej(g, x => x.BuyPage(""), RejectReason.UnknownModel);
            Rej(g, x => x.BuyPage("Tulip"), RejectReason.UnknownModel, "ordinal IDs");
            Rej(g, x => x.BuyPage("dog"), RejectReason.NotEnoughMoney, "50 > 20");
            Rej(g, x => x.BuyPage("cicada"), RejectReason.NotEnoughMoney);
            Ok(g, x => x.BuyPage("tulip"), "money = price is allowed");
            Assert.That(g.Snapshot.Money, Is.EqualTo(0));
            Assert.That(g.Snapshot.TotalBookCost, Is.EqualTo(20));
            CollectionAssert.AreEqual(new[] { "plane", "house", "tulip" }, g.Snapshot.OwnedModelIds);
            Rej(g, x => x.BuyPage("tulip"), RejectReason.AlreadyOwned, "AlreadyOwned with money 0 < 20");
            Rej(g, x => x.BuyPage("plane"), RejectReason.AlreadyOwned, "free owned page");
            Rej(g, x => x.BuyPage("house"), RejectReason.AlreadyOwned);
            Rej(g, x => x.BuyPage("unicorn"), RejectReason.UnknownModel);
            Rej(g, x => x.BuyPage("dog"), RejectReason.NotEnoughMoney);
        }

        [Test]
        public void BuyPage_WhileFoldingWithBookOpen_IsAccepted_AndKeepsTheSheet()
        {
            var g = Mode(WorkshopMode.Seated);
            Ok(g, x => x.TakeSheet());
            Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0));
            Ok(g, x => x.OpenBook());
            Ok(g, x => x.BuyPage("tulip"));
            Assert.That(g.Snapshot.Sheet.FoldCount, Is.EqualTo(1));
            Assert.That(g.Snapshot.Money, Is.EqualTo(0));
        }

        [Test]
        public void TakeSheet_SheetOnTableBeforeNoSheetsLeft()
        {
            var g = Mode(WorkshopMode.Seated);
            Times(4, () => Waste(g));
            Ok(g, x => x.TakeSheet());          // 5th sheet stays on the table
            Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(0));
            Rej(g, x => x.TakeSheet(), RejectReason.SheetOnTable);
            Ok(g, x => x.Discard());
            Rej(g, x => x.TakeSheet(), RejectReason.NoSheetsLeft);
        }

        [Test]
        public void TakeSheet_FreeFirstSheetIgnoresMoney_ThenNotEnoughMoney()
        {
            var g = Mode(WorkshopMode.Seated);
            Buy(g, "tulip");
            Assert.That(g.Snapshot.Money, Is.EqualTo(0));
            Ok(g, x => x.TakeSheet(), "the free first sheet needs no money");
            Assert.That(g.Snapshot.Money, Is.EqualTo(0));
            Assert.That(g.Snapshot.TotalPaperCost, Is.EqualTo(0));
            Ok(g, x => x.Discard());
            Rej(g, x => x.TakeSheet(), RejectReason.NotEnoughMoney);
            Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(4));
        }

        [Test]
        public void FoldAndCrease_NoSheetBeforeInvalidFold_ThenInvalidLayers()
        {
            var g = Mode(WorkshopMode.Seated);
            Rej(g, x => x.Fold(V(-1, 1), V(-1, -1), false, -1), RejectReason.NoSheet, "NoSheet before InvalidFold");
            Rej(g, x => x.Crease(V(-1, 1), V(-1, 0.99), -1), RejectReason.NoSheet);
            Ok(g, x => x.TakeSheet());
            var r = Rej(g, x => x.Fold(V(-1, 1), V(-1, -1), true, -1), RejectReason.InvalidFold);
            Assert.That(r.FoldError, Is.EqualTo(FoldError.InvalidLayers));
            r = Rej(g, x => x.Crease(V(-1, 1), V(1, 1), int.MinValue), RejectReason.InvalidFold);
            Assert.That(r.FoldError, Is.EqualTo(FoldError.InvalidLayers));
            r = Rej(g, x => x.Fold(V(-1, 1), V(-1, 0.98), false, 0), RejectReason.InvalidFold, "drag 0.02");
            Assert.That(r.FoldError, Is.Not.EqualTo(FoldError.None));
            r = Rej(g, x => x.Crease(V(-1, 1), V(-3, 3), 0), RejectReason.InvalidFold, "line outside");
            Assert.That(r.FoldError, Is.Not.EqualTo(FoldError.None));
            Assert.That(g.Snapshot.Sheet.ActionCount, Is.EqualTo(0));
        }

        [Test]
        public void FlipUndoUnfoldFinishDiscard_NoSheetThenOwnReason()
        {
            var g = Mode(WorkshopMode.Seated);
            Rej(g, x => x.Flip(), RejectReason.NoSheet);
            Rej(g, x => x.Undo(), RejectReason.NoSheet, "NoSheet before NothingToUndo");
            Rej(g, x => x.Unfold(), RejectReason.NoSheet);
            Rej(g, x => x.Finish(), RejectReason.NoSheet, "NoSheet before NoFolds");
            Rej(g, x => x.Discard(), RejectReason.NoSheet);
            Ok(g, x => x.TakeSheet());
            Rej(g, x => x.Undo(), RejectReason.NothingToUndo);
            Rej(g, x => x.Unfold(), RejectReason.NothingToUndo);
            Rej(g, x => x.Finish(), RejectReason.NoFolds);
            Ok(g, x => x.Crease(V(-1, 1), V(1, 1), 0));
            Rej(g, x => x.Finish(), RejectReason.NoFolds, "crease only");
            Ok(g, x => x.Flip());
            Rej(g, x => x.Finish(), RejectReason.NoFolds, "crease + flip");
            Ok(g, x => x.Undo());
            Ok(g, x => x.Undo());
            Rej(g, x => x.Undo(), RejectReason.NothingToUndo);
            Ok(g, x => x.Flip());
            Ok(g, x => x.Unfold(), "flip-only sheet can be unfolded");
            Rej(g, x => x.Unfold(), RejectReason.NothingToUndo);
        }

        [Test]
        public void SeatedCommandsAfterStandingUp_AreWrongModeEvenWithASheet()
        {
            var g = Mode(WorkshopMode.Seated);
            Ok(g, x => x.TakeSheet());
            Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0));
            Stand(g);
            Rej(g, x => x.Finish(), RejectReason.WrongMode);
            Rej(g, x => x.Discard(), RejectReason.WrongMode);
            Rej(g, x => x.Undo(), RejectReason.WrongMode);
            Rej(g, x => x.SkipGuide(), RejectReason.WrongMode, "guide visible but not seated");
            Sit(g);
            Assert.That(g.Snapshot.Sheet.FoldCount, Is.EqualTo(1));
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.True);
        }

        [Test]
        public void SkipGuide_NoGuideWithoutSheet_OnNonTutorialSheet_AndAfterSixActions()
        {
            var g = Mode(WorkshopMode.Seated);
            Rej(g, x => x.SkipGuide(), RejectReason.NoGuide, "no sheet (A2-10)");
            Ok(g, x => x.TakeSheet());
            int i = 0;
            foreach (var row in V2SpecBook.Book("plane")) Apply(g, row, "plane " + ++i);
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False);
            Rej(g, x => x.SkipGuide(), RejectReason.NoGuide, "6 actions done");
            Ok(g, x => x.Discard());
            Ok(g, x => x.TakeSheet());
            Rej(g, x => x.SkipGuide(), RejectReason.NoGuide, "non-tutorial sheet");
        }

        [Test]
        public void SleepNo_ReturnsToWalkingWithoutSettlement()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ref3(g, "house");
            Stand(g);
            Ok(g, x => x.Interact(Bed));
            var r = Ok(g, x => x.Sleep(false));
            Assert.That(r.Settlement, Is.Null);
            Assert.That(g.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Assert.That(g.Snapshot.Day, Is.EqualTo(1));
            Assert.That(g.Snapshot.Display.Count, Is.EqualTo(1));
        }
    }
}
