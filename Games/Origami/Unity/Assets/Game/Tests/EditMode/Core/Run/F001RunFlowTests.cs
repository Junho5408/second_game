using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;
using Origami.Core.Run;
using static Origami.Tests.Run.RunScenarios;

namespace Origami.Tests.Run
{
    // ORIGAMI-003: F-001 run flow (R-001, R-002, R-003, R-012, R-013, R-015).
    public class F001RunFlowTests
    {
        [Test]
        public void F001_AC01_NewRunStartState()
        {
            var run = new OrigamiRun();
            AssertStartState(run.GetSnapshot());
            Accept(run.NewRun(), CommandOutcome.RunStarted, "NewRun");
            AssertStartState(run.GetSnapshot());
        }

        [Test]
        public void F001_AC02_EndDayOnce_Day2()
        {
            var run = new OrigamiRun();
            var r = EndDay(run);
            Assert.AreEqual(CommandOutcome.DayEnded, r.Outcome);
            var s = run.GetSnapshot();
            Assert.AreEqual(2, s.Day);
            Assert.AreEqual(20, s.Money);
            Assert.AreEqual(5, s.AttemptsLeft);
            Assert.IsFalse(s.IsRunEnded);
            Assert.AreEqual(ModelIds.Boat, s.PopularModelId);
            Assert.AreEqual(1, r.Settlement.SettledDay);
            Assert.AreEqual(ModelIds.Boat, r.Settlement.NextPopularModelId);
            Assert.AreEqual(RunEndReason.None, r.Settlement.EndReason);
        }

        [Test]
        public void F001_AC03_SevenEndDays_CompletedE01_ThenRunEnded()
        {
            var run = new OrigamiRun();
            for (int i = 0; i < 6; i++) Assert.AreEqual(CommandOutcome.DayEnded, EndDay(run).Outcome, "day " + (i + 1));
            var last = EndDay(run);
            Assert.AreEqual(CommandOutcome.RunFinished, last.Outcome);
            Assert.AreEqual(7, last.Settlement.SettledDay);
            Assert.AreEqual(RunEndReason.Completed, last.Settlement.EndReason);
            Assert.IsTrue(last.Settlement.RunEnded);
            Assert.IsNull(last.Settlement.NextPopularModelId);
            Assert.AreEqual(20, last.Settlement.MoneyAfter);

            var s = run.GetSnapshot();
            Assert.IsTrue(s.IsRunEnded);
            Assert.AreEqual(RunEndReason.Completed, s.EndReason);
            Assert.AreEqual(7, s.Day);
            Assert.AreEqual(20, s.Money);
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);

            AssertRejected(run, x => x.EndDay(), RejectReason.RunEnded, "EndDay after end");
            AssertRejected(run, x => x.Start(ModelIds.Plane), RejectReason.RunEnded, "Start after end");
            Assert.AreEqual(7, run.GetSnapshot().Day);
            Assert.AreEqual(20, run.GetSnapshot().Money);
        }

        [Test]
        public void F001_AC04_EndDayWhileFolding_FoldInProgress()
        {
            var run = new OrigamiRun();
            run.Start(ModelIds.Plane);
            AssertRejected(run, x => x.EndDay(), RejectReason.FoldInProgress);
            var s = run.GetSnapshot();
            Assert.AreEqual(1, s.Day);
            Assert.AreEqual(4, s.AttemptsLeft);
            Assert.IsTrue(s.IsFolding);
        }

        [Test]
        public void F001_AC05_FiveWastes_NoAttemptsLeft_ThenNextDayResets()
        {
            var run = new OrigamiRun();
            Waste(run, ModelIds.Plane, 5);
            AssertRejected(run, x => x.Start(ModelIds.Plane), RejectReason.NoAttemptsLeft);
            Assert.AreEqual(15, run.GetSnapshot().Money);
            Assert.AreEqual(0, run.GetSnapshot().AttemptsLeft);
            EndDay(run);
            Assert.AreEqual(2, run.GetSnapshot().Day);
            Assert.AreEqual(5, run.GetSnapshot().AttemptsLeft);
        }

        [Test]
        public void F001_AC06_Bankrupt_AfterFourWastedDays()
        {
            var run = new OrigamiRun();
            WastedDays(run, 3);
            Assert.AreEqual(5, run.GetSnapshot().Money);
            Waste(run, ModelIds.Plane, 5);
            Assert.AreEqual(0, run.GetSnapshot().Money);
            Assert.IsFalse(run.GetSnapshot().IsRunEnded, "money 0 during the day is not bankruptcy");
            var r = EndDay(run);
            Assert.AreEqual(CommandOutcome.RunFinished, r.Outcome);
            Assert.AreEqual(RunEndReason.Bankrupt, r.Settlement.EndReason);
            Assert.AreEqual(4, r.Settlement.SettledDay);
            Assert.AreEqual(0, r.Settlement.Total);
            Assert.AreEqual(0, r.Settlement.MoneyAfter);
            Assert.IsNull(r.Settlement.NextPopularModelId);

            var s = run.GetSnapshot();
            Assert.IsTrue(s.IsRunEnded);
            Assert.AreEqual(RunEndReason.Bankrupt, s.EndReason);
            Assert.AreEqual(5, s.Day);
            Assert.AreEqual(0, s.Money);
            Assert.AreEqual(20, s.FailureCount);
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);
        }

        [Test]
        public void F001_AC07_MoneyZeroDuringDay_NotBankrupt()
        {
            var run = new OrigamiRun();
            WastedDays(run, 3);
            Assert.AreEqual(4, run.GetSnapshot().Day);
            Assert.AreEqual(5, run.GetSnapshot().Money);
            Make(run, ModelIds.Boat, 3);
            Assert.AreEqual(2, run.GetSnapshot().Money);
            Waste(run, ModelIds.Plane);
            Assert.AreEqual(1, run.GetSnapshot().Money);
            Make(run, ModelIds.Plane);
            Assert.AreEqual(0, run.GetSnapshot().Money);
            Assert.IsFalse(run.GetSnapshot().IsRunEnded);
            var r = EndDay(run);
            Assert.AreEqual(CommandOutcome.DayEnded, r.Outcome);
            CollectionAssert.AreEqual(new[] { 13, 13, 13, 6 }, Prices(r.Settlement));
            Assert.AreEqual(45, r.Settlement.Total);
            var s = run.GetSnapshot();
            Assert.AreEqual(45, s.Money);
            Assert.AreEqual(5, s.Day);
            Assert.IsFalse(s.IsRunEnded);
        }

        [Test]
        public void F001_AC08_CompletedWithLowMoney_E01NotBankrupt()
        {
            var run = new OrigamiRun();
            WastedDays(run, 3);
            for (int i = 0; i < 4; i++) EndDay(run);
            var s = run.GetSnapshot();
            Assert.IsTrue(s.IsRunEnded);
            Assert.AreEqual(RunEndReason.Completed, s.EndReason);
            Assert.AreEqual(5, s.Money);
            Assert.AreEqual(7, s.Day);
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);
        }

        [Test]
        public void F001_AC09_NewRunMidRun_ResetsToStartState()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            EndDay(run);
            Make(run, ModelIds.Boat);
            run.Start(ModelIds.Boat);
            Assert.IsTrue(run.GetSnapshot().IsFolding);
            var r = run.NewRun();
            Accept(r, CommandOutcome.RunStarted, "NewRun");
            Assert.IsNull(r.Settlement, "today's pieces are discarded, not sold");
            AssertStartState(run.GetSnapshot());
        }

        [Test]
        public void F001_AC10_Determinism_SameCommandsSameState()
        {
            var a = new OrigamiRun();
            var b = new OrigamiRun();
            foreach (var run in new[] { a, b })
            {
                WastedDays(run, 3);
                Make(run, ModelIds.Boat, 3);
                Waste(run, ModelIds.Plane);
                Make(run, ModelIds.Plane);
                EndDay(run);
            }
            AssertSameState(a.GetSnapshot(), b.GetSnapshot(), "AC-07 sequence");

            // A reused run object after NewRun behaves the same as a fresh one.
            a.NewRun();
            var c = new OrigamiRun();
            foreach (var run in new[] { a, c })
            {
                Make(run, ModelIds.Boat, 3);
                Make(run, ModelIds.Frog);
                EndDay(run);
            }
            AssertSameState(a.GetSnapshot(), c.GetSnapshot(), "after NewRun");
        }

        [Test]
        public void R013_BankruptcyOnlyAtStartOfNewDay_NotAfterDay7()
        {
            // Days 1-3 wasted (money 5), days 4-6 idle, day 7: spend the last 5 → money 0 → EndDay completes (no bankruptcy check).
            var run = new OrigamiRun();
            WastedDays(run, 3);
            for (int i = 0; i < 3; i++) EndDay(run);
            Assert.AreEqual(7, run.GetSnapshot().Day);
            Waste(run, ModelIds.Plane, 5);
            Assert.AreEqual(0, run.GetSnapshot().Money);
            Assert.IsFalse(run.GetSnapshot().IsRunEnded, "no bankruptcy during the day");
            var r = EndDay(run);
            Assert.AreEqual(RunEndReason.Completed, r.Settlement.EndReason);
            var s = run.GetSnapshot();
            Assert.AreEqual(RunEndReason.Completed, s.EndReason);
            Assert.AreEqual(7, s.Day);
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);
        }

        [Test]
        public void R013_MoneyZeroDuringDay_CommandsStillAccepted()
        {
            var run = new OrigamiRun();
            WastedDays(run, 3);
            Waste(run, ModelIds.Plane, 5);
            Assert.AreEqual(0, run.GetSnapshot().Money);
            Assert.AreEqual(RunEndReason.None, run.GetSnapshot().EndReason);
            AssertRejected(run, x => x.Start(ModelIds.Plane), RejectReason.NoAttemptsLeft, "not RunEnded");
            AssertRejected(run, x => x.Hint(), RejectReason.NoFold, "not RunEnded");
        }

        [Test]
        public void R013_SettlementSavesFromBankruptcy()
        {
            // Day 4: spend down to 0 but complete a piece → evening sale prevents bankruptcy.
            var run = new OrigamiRun();
            WastedDays(run, 3);
            Waste(run, ModelIds.Plane, 4);
            Make(run, ModelIds.Plane);
            Assert.AreEqual(0, run.GetSnapshot().Money);
            var r = EndDay(run);
            Assert.AreEqual(CommandOutcome.DayEnded, r.Outcome);
            Assert.AreEqual(6, run.GetSnapshot().Money, "plane ★3 normal on day 4");
            Assert.AreEqual(5, run.GetSnapshot().Day);
        }

        [Test]
        public void R002_NewRunAfterEnd_IsAccepted()
        {
            var run = new OrigamiRun();
            WastedDays(run, 4);
            Assert.IsTrue(run.GetSnapshot().IsRunEnded);
            Accept(run.NewRun(), CommandOutcome.RunStarted, "NewRun after bankruptcy");
            AssertStartState(run.GetSnapshot());
        }

        [Test]
        public void R003_EndDayWithoutAttempts_SettlesZero()
        {
            var run = new OrigamiRun();
            var r = EndDay(run);
            Assert.AreEqual(0, r.Settlement.Total);
            Assert.AreEqual(0, r.Settlement.Lines.Count);
            Assert.AreEqual(20, r.Settlement.MoneyAfter);
        }

        [Test]
        public void C010_SnapshotsAreIndependentCopies()
        {
            var run = new OrigamiRun();
            var before = run.GetSnapshot();
            Assert.AreNotSame(before, run.GetSnapshot());
            Make(run, ModelIds.Boat);
            run.Start(ModelIds.Plane);
            Assert.AreEqual(20, before.Money);
            Assert.AreEqual(0, before.TodayPieces.Count);
            Assert.IsNull(before.Fold);
            var mid = run.GetSnapshot();
            run.Fold(FoldAction.U);
            Assert.AreEqual(0, mid.Fold.Progress, "fold snapshot does not track later input");
        }
    }
}
