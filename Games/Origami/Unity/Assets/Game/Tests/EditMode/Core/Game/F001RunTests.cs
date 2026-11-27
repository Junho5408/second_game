using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Game;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Game
{
    // F-001 run progression (R-002, R-005, R-016, R-017, R-019, R-020). Invariants (AC-09) are checked after every helper command.
    public class F001RunTests
    {
        internal static void AssertStartState(GameSnapshot s)
        {
            Assert.AreEqual(1, s.Day);
            Assert.AreEqual(20, s.Money);
            Assert.AreEqual(5, s.SheetsLeft);
            CollectionAssert.AreEqual(new[] { ModelIds.Plane, ModelIds.House }, s.OwnedModelIds);
            Assert.IsTrue(s.FreeSheetAvailable);
            Assert.IsFalse(s.TutorialFinished);
            Assert.IsFalse(s.HasSat);
            Assert.AreEqual(WorkshopMode.Walking, s.Mode);
            Assert.IsNull(s.Sheet);
            Assert.IsFalse(s.BookOpen);
            Assert.IsFalse(s.BookVisible);
            Assert.AreEqual(0, s.BookPage);
            Assert.AreEqual(0, s.Display.Count);
            Assert.IsTrue(s.Completions.Equals(CompletionCounts.Empty));
            Assert.AreEqual(0, s.FailureCount);
            Assert.AreEqual(0, s.TotalSales);
            Assert.AreEqual(0, s.TotalPaperCost);
            Assert.AreEqual(0, s.TotalBookCost);
            Assert.AreEqual(ModelIds.Plane, s.PopularModelId);
            Assert.AreEqual(RunEndReason.None, s.EndReason);
            Assert.IsNull(s.EndingId);
            Assert.IsFalse(s.IsEnded);
        }

        [Test]
        public void F001_AC01_NewRunState()
        {
            var g = new OrigamiGame();
            AssertStartState(g.Snapshot);
            CheckInvariants(g);
            var before = g.Snapshot;
            Ok(g, g.NewRun());
            Assert.AreNotSame(before, g.Snapshot);
            AssertStartState(g.Snapshot);
        }

        [Test]
        public void F001_AC02_OneNight()
        {
            var g = new OrigamiGame();
            var r = Night(g);
            var s = g.Snapshot;
            Assert.AreEqual(2, s.Day);
            Assert.AreEqual(20, s.Money);
            Assert.AreEqual(5, s.SheetsLeft);
            Assert.AreEqual(WorkshopMode.Walking, s.Mode);
            Assert.IsTrue(s.FreeSheetAvailable);
            Assert.IsFalse(s.IsEnded);
            Assert.AreEqual(ModelIds.House, s.PopularModelId);
            Assert.AreEqual(1, r.Settlement.SettledDay);
            Assert.AreEqual(RunEndReason.None, r.Settlement.EndReason);
            Assert.AreEqual(ModelIds.House, r.Settlement.NextPopularModelId);
        }

        [Test]
        public void F001_AC03_SevenNightsCompletesTheRun()
        {
            var g = new OrigamiGame();
            CommandResult last = null;
            Times(7, () => last = Night(g));
            var s = g.Snapshot;
            Assert.IsTrue(s.IsEnded);
            Assert.AreEqual(RunEndReason.Completed, s.EndReason);
            Assert.AreEqual(WorkshopMode.Ended, s.Mode);
            Assert.AreEqual(7, s.Day);
            Assert.AreEqual(20, s.Money);
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);
            Assert.AreEqual(7, last.Settlement.SettledDay);
            Assert.AreEqual(RunEndReason.Completed, last.Settlement.EndReason);
            Assert.IsNull(last.Settlement.NextPopularModelId);
            Rejected(g, x => x.Interact(InteractTargets.Table), RejectReason.RunEnded);
            Rejected(g, x => x.TakeSheet(), RejectReason.RunEnded);
            Rejected(g, x => x.Interact(InteractTargets.Bed), RejectReason.RunEnded);
        }

        [Test]
        public void F001_AC04_CannotSleepWithSheetOnTable()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            Stand(g);
            Rejected(g, x => x.Interact(InteractTargets.Bed), RejectReason.SheetOnTable);
            Assert.AreEqual(WorkshopMode.Walking, g.Snapshot.Mode);
            Assert.AreEqual(1, g.Snapshot.Day);
        }

        [Test]
        public void F001_AC05_Bankruptcy()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Waste(g));
            Assert.AreEqual(12, g.Snapshot.Money);
            Assert.AreEqual(0, g.Snapshot.SheetsLeft);
            Rejected(g, x => x.TakeSheet(), RejectReason.NoSheetsLeft);
            Night(g);
            Assert.AreEqual(2, g.Snapshot.Day);
            Assert.AreEqual(12, g.Snapshot.Money);
            Sit(g);
            Times(5, () => Waste(g));
            Assert.AreEqual(2, g.Snapshot.Money);
            Night(g);
            Assert.AreEqual(3, g.Snapshot.Day);
            Assert.IsFalse(g.Snapshot.IsEnded, "money = 2 is not bankrupt");
            Sit(g);
            Waste(g);
            Assert.AreEqual(0, g.Snapshot.Money);
            Rejected(g, x => x.TakeSheet(), RejectReason.NotEnoughMoney);
            Assert.AreEqual(4, g.Snapshot.SheetsLeft);
            var r = Night(g);
            var s = g.Snapshot;
            Assert.IsTrue(s.IsEnded);
            Assert.AreEqual(RunEndReason.Bankrupt, s.EndReason);
            Assert.AreEqual(4, s.Day);
            Assert.AreEqual(0, s.Money);
            Assert.AreEqual(11, s.FailureCount);
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);
            Assert.AreEqual(3, r.Settlement.SettledDay);
            Assert.AreEqual(RunEndReason.Bankrupt, r.Settlement.EndReason);
            Assert.IsNull(r.Settlement.NextPopularModelId);
            Assert.AreEqual(0, r.Settlement.MoneyAfter);
        }

        internal static OrigamiGame ToDay3Seated()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Waste(g));
            Night(g);
            Sit(g);
            Times(5, () => Waste(g));
            Night(g);
            Sit(g);
            Assert.AreEqual(3, g.Snapshot.Day);
            Assert.AreEqual(2, g.Snapshot.Money);
            return g;
        }

        [Test]
        public void F001_AC06_ZeroMoneyDuringTheDayIsNotBankrupt()
        {
            var g = ToDay3Seated();
            var r = Ref(g, ModelIds.House);
            Assert.AreEqual(ModelIds.House, r.Recognition.ModelId);
            Assert.AreEqual(0, g.Snapshot.Money);
            Assert.IsFalse(g.Snapshot.IsEnded);
            var n = Night(g);
            Assert.AreEqual(12, n.Settlement.Total);
            Assert.AreEqual(4, g.Snapshot.Day);
            Assert.AreEqual(12, g.Snapshot.Money);
            Assert.IsFalse(g.Snapshot.IsEnded);
        }

        [Test]
        public void F001_AC07_NewRunMidRunDiscardsEverything()
        {
            var g = new OrigamiGame();
            Night(g);
            Sit(g);
            Take(g);
            Fold(g, V(-1, 1), V(-1, -1));
            Ok(g, g.NewRun());
            AssertStartState(g.Snapshot);
        }

        [Test]
        public void F001_AC07_NewRunAfterEndAndMidTutorial()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, g.OpenBook());
            Ok(g, g.TurnPage(20));
            Ok(g, g.BuyPage(ModelIds.Tulip));
            Ref(g, ModelIds.House);
            Ok(g, g.NewRun());
            AssertStartState(g.Snapshot);
            Times(7, () => Night(g));
            Ok(g, g.NewRun());
            AssertStartState(g.Snapshot);
        }

        // F-011 AC-01~02 command sequence (also used for determinism).
        internal static OrigamiGame F011Run()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Ref(g, ModelIds.Plane));
            Night(g);
            Sit(g);
            Ok(g, g.OpenBook());
            Ok(g, g.BuyPage(ModelIds.Dog));
            Times(5, () => Ref(g, ModelIds.Dog));
            Night(g);
            return g;
        }

        [Test]
        public void F001_AC08_Determinism()
        {
            var a = F011Run().Snapshot;
            var b = F011Run().Snapshot;
            Assert.AreEqual(a.Day, b.Day);
            Assert.AreEqual(a.Money, b.Money);
            Assert.AreEqual(a.SheetsLeft, b.SheetsLeft);
            CollectionAssert.AreEqual(a.OwnedModelIds, b.OwnedModelIds);
            Assert.IsTrue(a.Completions.Equals(b.Completions));
            Assert.AreEqual(a.FailureCount, b.FailureCount);
            Assert.AreEqual(a.Display.Count, b.Display.Count);
            Assert.AreEqual(a.IsEnded, b.IsEnded);
            Assert.AreEqual(a.EndingId, b.EndingId);
            Assert.AreEqual(a.TotalSales, b.TotalSales);

            // Mid-day display comparison (piece by piece, bitwise scores).
            var x = new OrigamiGame();
            var y = new OrigamiGame();
            foreach (var g in new[] { x, y })
            {
                Sit(g);
                Var(g, 0.06);
                Var(g, 0.15);
                Ref(g, ModelIds.House);
            }
            Assert.AreEqual(3, x.Snapshot.Display.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(x.Snapshot.Display[i].ModelId, y.Snapshot.Display[i].ModelId);
                Assert.AreEqual(x.Snapshot.Display[i].Grade, y.Snapshot.Display[i].Grade);
                Assert.AreEqual(x.Snapshot.Display[i].Score, y.Snapshot.Display[i].Score);
                Assert.AreEqual(x.Snapshot.Display[i].Percent, y.Snapshot.Display[i].Percent);
            }
        }

        [Test]
        public void F001_AC09_InvariantsAlongARichRun()
        {
            // Every helper checks the invariants; this run mixes buying, folding, failures, settlement and bankruptcy checks.
            var g = F011Run();
            Assert.AreEqual(3, g.Snapshot.Day);
            Assert.AreEqual(152, g.Snapshot.Money);
            Sit(g);
            Waste(g);
            Var(g, 0.06);
            Ok(g, g.OpenBook());
            Ok(g, g.BuyPage(ModelIds.Tulip));
            Ref(g, ModelIds.Tulip);
            Times(5, () => Night(g));   // settles days 3..7
            Assert.IsTrue(g.Snapshot.IsEnded);
            Assert.AreEqual(RunEndReason.Completed, g.Snapshot.EndReason);
            Assert.AreEqual(EndingRules.Evaluate(RunEndReason.Completed, g.Snapshot.Money, g.Snapshot.Completions), g.Snapshot.EndingId);
        }
    }
}
