using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Run;
using static Origami.Tests.Run.RunScenarios;

namespace Origami.Tests.Run
{
    // ORIGAMI-003: every rejection reason in the documented check order (R-004, R-005, R-015, C-010 table).
    // AssertRejected also proves that a rejected command leaves GetSnapshot() unchanged and never uses None.
    public class RejectionOrderTests
    {
        private static OrigamiRun EndedRun()
        {
            var run = new OrigamiRun();
            for (int i = 0; i < 7; i++) EndDay(run);
            Assert.IsTrue(run.GetSnapshot().IsRunEnded);
            return run;
        }

        private static OrigamiRun BankruptRun()
        {
            var run = new OrigamiRun();
            WastedDays(run, 4);
            Assert.IsTrue(run.GetSnapshot().IsRunEnded);
            return run;
        }

        [Test]
        public void R015_RunEnded_FirstForEveryCommand()
        {
            foreach (var run in new[] { EndedRun(), BankruptRun() })
            {
                AssertRejected(run, x => x.Start(ModelIds.Plane), RejectReason.RunEnded, "Start(plane)");
                AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.RunEnded, "Start(locked)");
                AssertRejected(run, x => x.Start("unicorn"), RejectReason.RunEnded, "Start(unknown)");
                AssertRejected(run, x => x.Start(null), RejectReason.RunEnded, "Start(null)");
                AssertRejected(run, x => x.Fold(FoldAction.U), RejectReason.RunEnded, "Fold");
                AssertRejected(run, x => x.Fold((FoldAction)0), RejectReason.RunEnded, "Fold(invalid)");
                AssertRejected(run, x => x.Hint(), RejectReason.RunEnded, "Hint");
                AssertRejected(run, x => x.Abandon(), RejectReason.RunEnded, "Abandon");
                AssertRejected(run, x => x.EndDay(), RejectReason.RunEnded, "EndDay");
            }
        }

        [Test]
        public void R004_StartRejectOrder()
        {
            var run = new OrigamiRun();
            // FoldInProgress before UnknownModel / Locked.
            run.Start(ModelIds.Plane);
            AssertRejected(run, x => x.Start("unicorn"), RejectReason.FoldInProgress, "folding + unknown");
            AssertRejected(run, x => x.Start(""), RejectReason.FoldInProgress, "folding + empty");
            AssertRejected(run, x => x.Start(ModelIds.Dragon), RejectReason.FoldInProgress, "folding + locked");
            run.Abandon();

            // UnknownModel before Locked/NoAttemptsLeft/NotEnoughMoney; Locked before NoAttemptsLeft.
            Waste(run, ModelIds.Plane, 4);           // attempts 0, money 15
            AssertRejected(run, x => x.Start("unicorn"), RejectReason.UnknownModel, "unknown + no attempts");
            AssertRejected(run, x => x.Start(null), RejectReason.UnknownModel, "null + no attempts");
            AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.Locked, "locked + no attempts");
            AssertRejected(run, x => x.Start(ModelIds.Plane), RejectReason.NoAttemptsLeft, "no attempts");
        }

        [Test]
        public void R004_NoAttemptsLeftBeforeNotEnoughMoney_ThenNotEnoughMoney()
        {
            var run = new OrigamiRun();
            WastedDays(run, 3);                  // day 4, money 5
            Make(run, ModelIds.Boat, 3);         // money 2, frog unlocked
            Waste(run, ModelIds.Plane);          // money 1, attempts 1
            AssertRejected(run, x => x.Start("unicorn"), RejectReason.UnknownModel, "unknown + poor");
            AssertRejected(run, x => x.Start(ModelIds.Crane), RejectReason.Locked, "locked + poor");
            AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.NotEnoughMoney, "frog costs 2");
            Waste(run, ModelIds.Plane);          // money 0, attempts 0
            AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.NoAttemptsLeft, "no attempts + poor");
            AssertRejected(run, x => x.Start(ModelIds.Plane), RejectReason.NoAttemptsLeft, "no attempts + money 0");
            Assert.AreEqual(0, run.GetSnapshot().Money);
        }

        [Test]
        public void R005_FoldRejectOrder()
        {
            var run = new OrigamiRun();
            AssertRejected(run, x => x.Fold((FoldAction)0), RejectReason.NoFold, "not folding + invalid");
            AssertRejected(run, x => x.Fold((FoldAction)6), RejectReason.NoFold, "not folding + 6");
            AssertRejected(run, x => x.Fold(FoldAction.U), RejectReason.NoFold, "not folding");
            run.Start(ModelIds.Plane);
            run.Fold(FoldAction.D); // 1 mistake
            run.Hint();
            AssertRejected(run, x => x.Fold((FoldAction)0), RejectReason.InvalidAction, "folding + 0");
            AssertRejected(run, x => x.Fold((FoldAction)6), RejectReason.InvalidAction, "folding + 6");
            Assert.AreEqual(1, run.GetSnapshot().Fold.Mistakes);
        }

        [Test]
        public void R015_HintAbandonRejectOrder()
        {
            var run = new OrigamiRun();
            AssertRejected(run, x => x.Hint(), RejectReason.NoFold);
            AssertRejected(run, x => x.Abandon(), RejectReason.NoFold);
        }

        [Test]
        public void R015_EndDayRejectOrder()
        {
            var run = new OrigamiRun();
            run.Start(ModelIds.Boat);
            run.Fold(FoldAction.D);
            run.Hint();
            AssertRejected(run, x => x.EndDay(), RejectReason.FoldInProgress, "mid fold with hint");
        }

        [Test]
        public void R015_RejectedCommandsLeaveSnapshotUnchanged_RichState()
        {
            // A state with pieces today, a fold with progress, mistakes, hint reveal, unlocks and failures.
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 3);
            Waste(run, ModelIds.Plane);
            run.Start(ModelIds.Frog);
            run.Fold(FoldAction.L);
            run.Fold(FoldAction.U); // mistake
            run.Hint();
            AssertRejected(run, x => x.Start(ModelIds.Plane), RejectReason.FoldInProgress);
            AssertRejected(run, x => x.Start(null), RejectReason.FoldInProgress);
            AssertRejected(run, x => x.Fold((FoldAction)42), RejectReason.InvalidAction);
            AssertRejected(run, x => x.EndDay(), RejectReason.FoldInProgress);
            var s = run.GetSnapshot();
            Assert.AreEqual(1, s.Fold.Progress);
            Assert.AreEqual(1, s.Fold.Mistakes);
            Assert.AreEqual(FoldAction.R, s.Fold.RevealedAction);
            Assert.AreEqual(3, s.TodayPieces.Count);
        }
    }
}
