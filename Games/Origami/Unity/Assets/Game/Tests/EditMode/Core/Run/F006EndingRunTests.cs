using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;
using Origami.Core.Run;
using static Origami.Tests.Run.RunScenarios;

namespace Origami.Tests.Run
{
    // ORIGAMI-003: F-006 ending through a whole run (AC-10, AC-11). AC-01..09 are in Rules/EndingRulesTests.
    public class F006EndingRunTests
    {
        [Test]
        public void F006_AC10_PerfectRun_MoneyPerDay_EndsTycoon()
        {
            var run = new OrigamiRun();
            var moneyAfter = new int[7];

            Make(run, ModelIds.Boat, 3);
            Make(run, ModelIds.Frog, 2);
            moneyAfter[0] = EndDay(run).Settlement.MoneyAfter;

            Make(run, ModelIds.Frog);
            Make(run, ModelIds.Crane, 3);
            Make(run, ModelIds.Dragon);
            moneyAfter[1] = EndDay(run).Settlement.MoneyAfter;

            CommandResult last = default;
            for (int day = 3; day <= 7; day++)
            {
                Make(run, ModelIds.Dragon, 5);
                last = EndDay(run);
                moneyAfter[day - 1] = last.Settlement.MoneyAfter;
                Assert.AreEqual(moneyAfter[day - 1], run.GetSnapshot().Money, "day " + day);
            }

            CollectionAssert.AreEqual(new[] { 70, 209, 479, 749, 1019, 1289, 1709 }, moneyAfter);
            Assert.AreEqual(CommandOutcome.RunFinished, last.Outcome);
            Assert.AreEqual(450, last.Settlement.Total, "day 7: popular dragon 90 × 5");

            var s = run.GetSnapshot();
            Assert.AreEqual(RunEndReason.Completed, s.EndReason);
            Assert.AreEqual(7, s.Day);
            Assert.AreEqual(1709, s.Money);
            Assert.AreEqual(EndingIds.Tycoon, s.EndingId);
            Assert.AreEqual(0, s.Completions.Get(ModelIds.Plane, Grade.Star3), "E-04 not met");
            Assert.AreEqual(3, s.Completions.Total(ModelIds.Crane), "E-02 not met");
            Assert.AreEqual(26, s.Completions.Total(ModelIds.Dragon));
            Assert.AreEqual(0, s.FailureCount);
            Assert.AreEqual(1709 - 20 + s.TotalPaperCost, s.TotalSales, "money = start − paper + sales");
        }

        [Test]
        public void F006_AC11_ResultData_ThenRestart()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            run.Start(ModelIds.Boat);
            Input(run, FoldAction.L, FoldAction.D, FoldAction.U, FoldAction.F, FoldAction.U); // ★2
            Waste(run, ModelIds.Plane);
            for (int i = 0; i < 7; i++) EndDay(run);
            var s = run.GetSnapshot();
            Assert.IsTrue(s.IsRunEnded);
            Assert.AreEqual(EndingIds.LocalShop, s.EndingId, "money 32 > 20");
            Assert.IsNotNull(OrigamiCatalog.FindEnding(s.EndingId));
            Assert.AreEqual(20 - 3 + 9 + 6, s.Money);
            Assert.AreEqual(1, s.Completions.Get(ModelIds.Boat, Grade.Star3));
            Assert.AreEqual(1, s.Completions.Get(ModelIds.Boat, Grade.Star2));
            Assert.AreEqual(0, s.Completions.Get(ModelIds.Boat, Grade.Star1));
            Assert.AreEqual(1, s.FailureCount);
            Assert.AreEqual(15, s.TotalSales);
            Assert.AreEqual(3, s.TotalPaperCost);

            Accept(run.NewRun(), CommandOutcome.RunStarted, "다시 시작");
            AssertStartState(run.GetSnapshot());
        }

        [Test]
        public void R014_EndingFixedOnceRunEnds()
        {
            var run = new OrigamiRun();
            for (int i = 0; i < 7; i++) EndDay(run);
            Assert.AreEqual(EndingIds.EmptyHanded, run.GetSnapshot().EndingId);
            AssertRejected(run, x => x.EndDay(), RejectReason.RunEnded);
            Assert.AreEqual(EndingIds.EmptyHanded, run.GetSnapshot().EndingId);
        }

        [Test]
        public void R014_NoEndingWhileRunning()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            EndDay(run);
            Assert.IsNull(run.GetSnapshot().EndingId);
        }
    }
}
