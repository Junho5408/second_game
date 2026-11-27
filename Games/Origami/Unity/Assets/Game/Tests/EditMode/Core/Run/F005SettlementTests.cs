using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Run;
using static Origami.Core.Content.FoldAction;
using static Origami.Tests.Run.RunScenarios;

namespace Origami.Tests.Run
{
    // ORIGAMI-003: F-005 selling and daily settlement (R-011, R-012).
    public class F005SettlementTests
    {
        [Test]
        public void F005_AC01_BoatSettlement()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            var r = EndDay(run);
            Assert.AreEqual(9, r.Settlement.Total);
            Assert.AreEqual(28, r.Settlement.MoneyAfter);
            Assert.AreEqual(1, r.Settlement.Lines.Count);
            var line = r.Settlement.Lines[0];
            Assert.AreEqual(ModelIds.Boat, line.ModelId);
            Assert.AreEqual(Grade.Star3, line.Grade);
            Assert.IsFalse(line.Popular);
            Assert.AreEqual(9, line.Price);
            Assert.AreEqual(28, run.GetSnapshot().Money);
        }

        [Test]
        public void F005_AC02_PopularPlaneDay1()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Plane);
            var r = EndDay(run);
            Assert.IsTrue(r.Settlement.Lines[0].Popular);
            Assert.AreEqual(9, r.Settlement.Total);
            Assert.AreEqual(28, run.GetSnapshot().Money);
        }

        [Test]
        public void F005_AC03_MixedGrades()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Plane);
            run.Start(ModelIds.Boat);
            Input(run, L, D, U, F, U);       // ★2
            run.Start(ModelIds.Boat);
            Input(run, D, L, U, R, F, U);    // ★1
            CollectionAssert.AreEqual(
                Pieces(ModelIds.Plane, Grade.Star3, ModelIds.Boat, Grade.Star2, ModelIds.Boat, Grade.Star1),
                run.GetSnapshot().TodayPieces);
            var r = EndDay(run);
            CollectionAssert.AreEqual(new[] { 9, 6, 3 }, Prices(r.Settlement));
            Assert.AreEqual(18, r.Settlement.Total);
            Assert.AreEqual(35, run.GetSnapshot().Money);
        }

        [Test]
        public void F005_AC04_SettlementUsesPriceTable()
        {
            // Pricing covers all 30 values (Rules/PricingTests); here: settlement prices = Pricing for the settled day.
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 3);
            EndDay(run);                    // day 1: boat normal
            Make(run, ModelIds.Boat);       // day 2: boat popular
            Make(run, ModelIds.Frog);
            var r = EndDay(run);
            Assert.AreEqual(2, r.Settlement.SettledDay);
            Assert.IsTrue(r.Settlement.Lines[0].Popular);
            Assert.AreEqual(13, r.Settlement.Lines[0].Price);
            Assert.IsFalse(r.Settlement.Lines[1].Popular);
            Assert.AreEqual(15, r.Settlement.Lines[1].Price);
        }

        [Test]
        public void F005_AC05_ListClearedAndEmptySettlementIsZero()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            EndDay(run);
            Assert.AreEqual(0, run.GetSnapshot().TodayPieces.Count);
            int money = run.GetSnapshot().Money;
            var r = EndDay(run);
            Assert.AreEqual(0, r.Settlement.Total);
            Assert.AreEqual(0, r.Settlement.Lines.Count);
            Assert.AreEqual(money, run.GetSnapshot().Money);
        }

        [Test]
        public void F005_AC06_SoldAtCompletionDayPrice()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            var r = EndDay(run);
            Assert.AreEqual(1, r.Settlement.SettledDay);
            Assert.AreEqual(9, r.Settlement.Lines[0].Price, "day-1 price, not day-2 popular 13");
            Assert.AreEqual(ModelIds.Boat, r.Settlement.NextPopularModelId);
        }

        [Test]
        public void F005_AC07_TotalSalesAndPaperCost()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            Waste(run, ModelIds.Plane);
            EndDay(run);
            var s = run.GetSnapshot();
            Assert.AreEqual(9, s.TotalSales);
            Assert.AreEqual(2, s.TotalPaperCost);
            Assert.AreEqual(27, s.Money);
        }

        [Test]
        public void F005_NewRunDiscardsUnsoldPieces()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            run.NewRun();
            var r = EndDay(run);
            Assert.AreEqual(0, r.Settlement.Total);
            Assert.AreEqual(0, run.GetSnapshot().TotalSales);
        }
    }
}
