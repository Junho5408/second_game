using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Recognition;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Game
{
    // F-004 taking a sheet (R-006, R-008).
    public class F004SheetTests
    {
        [Test]
        public void F004_AC01_FirstSheetIsFreeTutorial()
        {
            var g = new OrigamiGame();
            Sit(g);
            var before = g.Snapshot;
            Take(g);
            var s = g.Snapshot;
            Assert.AreNotSame(before, s);
            Assert.AreEqual(20, s.Money);
            Assert.AreEqual(4, s.SheetsLeft);
            Assert.AreEqual(0, s.TotalPaperCost);
            Assert.IsFalse(s.FreeSheetAvailable);
            Assert.IsNotNull(s.Sheet);
            Assert.AreSame(PaperState.Initial, s.Sheet.State);
            Assert.AreEqual(1, s.Sheet.State.Facets.Count);
            Assert.IsFalse(s.Sheet.State.Facets[0].FrontUp);
            Assert.AreEqual(0, s.Sheet.FoldCount);
            Assert.AreEqual(0, s.Sheet.CreaseCount);
            Assert.AreEqual(0, s.Sheet.ActionCount);
            Assert.IsFalse(s.Sheet.CanUndo);
            Assert.AreEqual(4.0, Outline.FromState(s.Sheet.State).Area, 1e-6);
            Assert.IsTrue(s.Sheet.IsTutorial);
            Assert.IsTrue(s.Sheet.GuideVisible);
            Assert.IsNotNull(s.Sheet.Guide);
            Assert.AreEqual(0, s.Sheet.GuideScores.Count);
        }

        [Test]
        public void F004_AC02_SecondSheetCostsTwo()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            Ok(g, g.Discard());
            Take(g);
            var s = g.Snapshot;
            Assert.AreEqual(18, s.Money);
            Assert.AreEqual(3, s.SheetsLeft);
            Assert.AreEqual(2, s.TotalPaperCost);
            Assert.IsFalse(s.Sheet.IsTutorial);
            Assert.IsFalse(s.Sheet.GuideVisible);
            Assert.IsNull(s.Sheet.Guide);
        }

        [Test]
        public void F004_AC03_SheetOnTable()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            Rejected(g, x => x.TakeSheet(), RejectReason.SheetOnTable);
        }

        [Test]
        public void F004_AC04_NoSheetsLeft()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Waste(g));
            Rejected(g, x => x.TakeSheet(), RejectReason.NoSheetsLeft);
            Assert.AreEqual(12, g.Snapshot.Money);
        }

        [Test]
        public void F004_AC05_FirstSheetOnDay2IsStillTheTutorial()
        {
            var g = new OrigamiGame();
            Night(g);
            Sit(g);
            Take(g);
            Assert.AreEqual(20, g.Snapshot.Money);
            Assert.AreEqual(4, g.Snapshot.SheetsLeft);
            Assert.IsTrue(g.Snapshot.Sheet.IsTutorial);
            Assert.IsTrue(g.Snapshot.Sheet.GuideVisible);
        }

        [Test]
        public void F004_AC06_MoneyTwoAllowed_MoneyZeroRejected()
        {
            var g = F001RunTests.ToDay3Seated();
            Take(g);
            Assert.AreEqual(0, g.Snapshot.Money);
            Assert.AreEqual(4, g.Snapshot.SheetsLeft);
            Ok(g, g.Discard());
            Rejected(g, x => x.TakeSheet(), RejectReason.NotEnoughMoney);
            Assert.AreEqual(0, g.Snapshot.Money);
            Assert.AreEqual(4, g.Snapshot.SheetsLeft);
        }

        [Test]
        public void R006_OrderSheetOnTableBeforeNoSheetsLeft()
        {
            // Last sheet of the day on the table: SheetOnTable wins over NoSheetsLeft.
            var g = new OrigamiGame();
            Sit(g);
            Times(4, () => Waste(g));
            Take(g);
            Assert.AreEqual(0, g.Snapshot.SheetsLeft);
            Rejected(g, x => x.TakeSheet(), RejectReason.SheetOnTable);
        }

        [Test]
        public void R006_OrderNoSheetsLeftBeforeNotEnoughMoney()
        {
            // NotEnoughMoney only when sheets remain; with 0 sheets the reason is NoSheetsLeft (money 2 would be enough anyway).
            var g = F001RunTests.ToDay3Seated();
            Waste(g);                      // money 0, sheets 4
            Assert.AreEqual(0, g.Snapshot.Money);
            Rejected(g, x => x.TakeSheet(), RejectReason.NotEnoughMoney);
            var h = new OrigamiGame();
            Sit(h);
            Times(5, () => Waste(h));      // money 12
            Night(h);
            Sit(h);
            Times(5, () => Waste(h));      // money 2, sheets 0
            Rejected(h, x => x.TakeSheet(), RejectReason.NoSheetsLeft);
        }

        [Test]
        public void R006_FreeSheetNeedsNoMoney()
        {
            var g = new OrigamiGame();
            Ok(g, g.Interact(Origami.Core.Catalog.InteractTargets.Bookshelf));
            Ok(g, g.BuyPage(Origami.Core.Catalog.ModelIds.Tulip));   // money 0
            Ok(g, g.CloseBook());
            Sit(g);
            Take(g);
            Assert.AreEqual(0, g.Snapshot.Money);
            Assert.IsTrue(g.Snapshot.Sheet.IsTutorial);
            Ok(g, g.Discard());
            Rejected(g, x => x.TakeSheet(), RejectReason.NotEnoughMoney);
        }

        [Test]
        public void R005_BedOrder_UnknownTargetBeforeSheetOnTable()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            Stand(g);
            Rejected(g, x => x.Interact("sofa"), RejectReason.UnknownTarget);
            Ok(g, g.Interact(Origami.Core.Catalog.InteractTargets.Counter));   // other targets still usable with a sheet on the table
        }
    }
}
