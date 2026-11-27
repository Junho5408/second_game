using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Run;
using static Origami.Tests.Run.RunScenarios;

namespace Origami.Tests.Run
{
    // ORIGAMI-003: F-002 starting a fold / buying paper (R-004, R-015).
    public class F002StartTests
    {
        [Test]
        public void F002_AC01_StartBoat_PaysAndFolds()
        {
            var run = new OrigamiRun();
            Accept(run.Start(ModelIds.Boat), CommandOutcome.FoldStarted, "Start(boat)");
            var s = run.GetSnapshot();
            Assert.AreEqual(19, s.Money);
            Assert.AreEqual(4, s.AttemptsLeft);
            Assert.AreEqual(1, s.TotalPaperCost);
            Assert.IsTrue(s.IsFolding);
            Assert.AreEqual(ModelIds.Boat, s.Fold.ModelId);
            Assert.AreEqual(0, s.Fold.Progress);
            Assert.AreEqual(4, s.Fold.StepCount);
            Assert.AreEqual(0, s.Fold.CorrectSteps.Count);
            Assert.AreEqual(0, s.Fold.Mistakes);
            Assert.AreEqual(0, s.Fold.Hints);
            Assert.IsFalse(s.Fold.IsCurrentStepRevealed);
            Assert.AreEqual((FoldAction)0, s.Fold.RevealedAction);
            Assert.AreEqual(0, s.Fold.Penalty);
            Assert.AreEqual(Grade.Star3, s.Fold.ExpectedGrade);
            Assert.AreEqual(0, s.TodayPieces.Count, "no price/piece at start");
        }

        [Test]
        public void F002_AC02_StartLockedFrog_Locked()
        {
            var run = new OrigamiRun();
            AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.Locked);
            AssertRejected(run, x => x.Start(ModelIds.Crane), RejectReason.Locked);
            AssertRejected(run, x => x.Start(ModelIds.Dragon), RejectReason.Locked);
            Assert.AreEqual(20, run.GetSnapshot().Money);
            Assert.AreEqual(5, run.GetSnapshot().AttemptsLeft);
        }

        [Test]
        public void F002_AC03_UnknownModel_IncludingEmptyAndNull()
        {
            var run = new OrigamiRun();
            AssertRejected(run, x => x.Start("unicorn"), RejectReason.UnknownModel, "unicorn");
            AssertRejected(run, x => x.Start(""), RejectReason.UnknownModel, "empty");
            AssertRejected(run, x => x.Start(null), RejectReason.UnknownModel, "null");
            AssertRejected(run, x => x.Start("Boat"), RejectReason.UnknownModel, "case-sensitive");
            Assert.AreEqual(20, run.GetSnapshot().Money);
            Assert.AreEqual(5, run.GetSnapshot().AttemptsLeft);
            run.Start(ModelIds.Boat);
            AssertRejected(run, x => x.Start(null), RejectReason.FoldInProgress, "null while folding");
        }

        [Test]
        public void F002_AC04_FoldInProgressBeforeLocked()
        {
            var run = new OrigamiRun();
            run.Start(ModelIds.Boat);
            AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.FoldInProgress);
            AssertRejected(run, x => x.Start(ModelIds.Boat), RejectReason.FoldInProgress);
        }

        [Test]
        public void F002_AC05_LockedBeforeNoAttemptsLeft()
        {
            var run = new OrigamiRun();
            Waste(run, ModelIds.Plane, 5);
            AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.Locked);
            AssertRejected(run, x => x.Start(ModelIds.Plane), RejectReason.NoAttemptsLeft);
        }

        [Test]
        public void F002_AC06_NotEnoughMoney_ThenExactMoneyAllowed()
        {
            var run = new OrigamiRun();
            WastedDays(run, 3);
            Make(run, ModelIds.Boat, 3);
            Assert.AreEqual(2, run.GetSnapshot().Money);
            Assert.IsTrue(run.GetSnapshot().IsUnlocked(ModelIds.Frog));
            Waste(run, ModelIds.Plane);
            Assert.AreEqual(1, run.GetSnapshot().Money);
            AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.NotEnoughMoney);
            Assert.AreEqual(1, run.GetSnapshot().Money);
            Assert.AreEqual(1, run.GetSnapshot().AttemptsLeft);
            Accept(run.Start(ModelIds.Plane), CommandOutcome.FoldStarted, "money == cost");
            Assert.AreEqual(0, run.GetSnapshot().Money);
            Assert.AreEqual(0, run.GetSnapshot().AttemptsLeft);
        }

        [Test]
        public void F002_AC07_MoneyNeverNegative()
        {
            var run = new OrigamiRun();
            WastedDays(run, 3);
            Make(run, ModelIds.Boat, 3);
            Waste(run, ModelIds.Plane);
            run.Start(ModelIds.Plane);
            Accept(run.Abandon(), CommandOutcome.FoldAbandoned, "Abandon");
            AssertRejected(run, x => x.Start(ModelIds.Plane), RejectReason.NoAttemptsLeft);
            Assert.AreEqual(0, run.GetSnapshot().Money);
        }
    }
}
