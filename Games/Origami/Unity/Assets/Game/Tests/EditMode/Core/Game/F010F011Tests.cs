using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Game;
using Origami.Core.Recognition;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Game
{
    // F-010 finish / discard / display (R-009, R-012) and F-011 sale / settlement (R-015, R-016).
    public class F010F011Tests
    {
        [Test]
        public void F010_AC01_RefHouse()
        {
            var g = new OrigamiGame();
            Sit(g);
            var r = Ref(g, ModelIds.House);
            Assert.IsTrue(r.Recognition.IsPiece);
            var s = g.Snapshot;
            Assert.AreEqual(1, s.Display.Count);
            var d = s.Display[0];
            Assert.AreEqual(ModelIds.House, d.ModelId);
            Assert.AreEqual(Grade.Star3, d.Grade);
            Assert.AreEqual(100, d.Percent);   // exact reference state
            Assert.AreEqual(r.Recognition.Score, d.Score);
            Assert.AreEqual(Grading.Percent(d.Score), d.Percent);
            Assert.IsNotNull(d.Shape);
            Assert.AreEqual(1, s.Completions.Get(ModelIds.House, Grade.Star3));
            Assert.IsNull(s.Sheet);
            Assert.AreEqual(20, s.Money);
        }

        [Test]
        public void F010_AC01_DisplayShapeIsThePlayersState()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            FoldSteps(g, ModelIds.House);
            var state = g.Snapshot.Sheet.State;
            Finish(g);
            Assert.AreSame(state, g.Snapshot.Display[0].Shape);
        }

        [Test]
        public void F010_AC02_NoFolds()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            Rejected(g, x => x.Finish(), RejectReason.NoFolds);
            Fold(g, V(-1, 1), V(-1, -1));
            Ok(g, g.Undo());
            Rejected(g, x => x.Finish(), RejectReason.NoFolds);
            Ok(g, g.Flip());
            Rejected(g, x => x.Finish(), RejectReason.NoFolds);
        }

        [Test]
        public void F010_AC03_CrumpledPaper()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            Fold(g, V(-1, 1), V(-1, -1));
            var r = Finish(g);
            Assert.IsFalse(r.Recognition.IsPiece);
            Assert.IsNull(r.Recognition.ModelId);
            Assert.AreEqual(Grade.None, r.Recognition.Grade);
            Assert.AreEqual(ModelIds.House, r.Recognition.BestModelId);
            Assert.AreEqual(0.728, r.Recognition.BestScore, 0.01);   // J-09 score ±0.01 (percent not asserted, W-3)
            var s = g.Snapshot;
            Assert.AreEqual(1, s.FailureCount);
            Assert.AreEqual(0, s.Display.Count);
            Assert.IsTrue(s.Completions.Equals(CompletionCounts.Empty));
            Assert.IsNull(s.Sheet);
        }

        [Test]
        public void F010_AC04_Waste()
        {
            var g = new OrigamiGame();
            Sit(g);
            Waste(g);
            Assert.AreEqual(1, g.Snapshot.FailureCount);
            Assert.AreEqual(0, g.Snapshot.Display.Count);
            Assert.IsNull(g.Snapshot.Sheet);
        }

        [Test]
        public void F010_AC05_FiveHousesInOneDay()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Ref(g, ModelIds.House));
            Assert.AreEqual(5, g.Snapshot.Display.Count);
            Assert.AreEqual(0, g.Snapshot.SheetsLeft);
            Assert.AreEqual(5, g.Snapshot.Completions.Get(ModelIds.House, Grade.Star3));
        }

        [Test]
        public void F011_AC01_PopularPlaneDay1()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Ref(g, ModelIds.Plane));
            Assert.AreEqual(12, g.Snapshot.Money);
            var r = Night(g);
            Assert.AreEqual(5, r.Settlement.Lines.Count);
            foreach (var l in r.Settlement.Lines)
            {
                Assert.AreEqual(ModelIds.Plane, l.ModelId);
                Assert.AreEqual(Grade.Star3, l.Grade);
                Assert.IsTrue(l.Popular);
                Assert.AreEqual(13, l.Price);
            }
            Assert.AreEqual(65, r.Settlement.Total);
            Assert.AreEqual(77, r.Settlement.MoneyAfter);
            Assert.AreEqual(77, g.Snapshot.Money);
            Assert.AreEqual(65, g.Snapshot.TotalSales);
            Assert.AreEqual(2, g.Snapshot.Day);
            Assert.AreEqual(0, g.Snapshot.Display.Count);
        }

        [Test]
        public void F011_AC02_DogDay2()
        {
            var g = F001RunTests.F011Run();
            Assert.AreEqual(152, g.Snapshot.Money);
            Assert.AreEqual(3, g.Snapshot.Day);
            Assert.AreEqual(50, g.Snapshot.TotalBookCost);
            Assert.AreEqual(5, g.Snapshot.Completions.Get(ModelIds.Dog, Grade.Star3));
        }

        [Test]
        public void F011_AC02_IntermediateMoney()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Ref(g, ModelIds.Plane));
            Night(g);
            Sit(g);
            Ok(g, g.OpenBook());
            Ok(g, g.BuyPage(ModelIds.Dog));
            Assert.AreEqual(27, g.Snapshot.Money);
            Times(5, () => Ref(g, ModelIds.Dog));
            Assert.AreEqual(17, g.Snapshot.Money);
            var r = Night(g);
            Assert.AreEqual(135, r.Settlement.Total);
            foreach (var l in r.Settlement.Lines)
            {
                Assert.IsFalse(l.Popular);
                Assert.AreEqual(27, l.Price);
            }
        }

        [Test]
        public void F011_AC03_AC06_MixedGradesSettlement()
        {
            var g = new OrigamiGame();
            Sit(g);
            Assert.AreEqual(Grade.Star2, Var(g, 0.06).Recognition.Grade);
            Assert.AreEqual(Grade.Star1, Var(g, 0.15).Recognition.Grade);
            Assert.AreEqual(Grade.Star3, Ref(g, ModelIds.House).Recognition.Grade);
            Assert.AreEqual(16, g.Snapshot.Money);
            var r = Night(g);
            var lines = r.Settlement.Lines;
            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual((ModelIds.House, Grade.Star2, false, 8), (lines[0].ModelId, lines[0].Grade, lines[0].Popular, lines[0].Price));
            Assert.AreEqual((ModelIds.House, Grade.Star1, false, 4), (lines[1].ModelId, lines[1].Grade, lines[1].Popular, lines[1].Price));
            Assert.AreEqual((ModelIds.House, Grade.Star3, false, 12), (lines[2].ModelId, lines[2].Grade, lines[2].Popular, lines[2].Price));
            Assert.AreEqual(24, r.Settlement.Total);
            Assert.AreEqual(40, r.Settlement.MoneyAfter);
            Assert.AreEqual(40, g.Snapshot.Money);
            Assert.AreEqual(1, r.Settlement.SettledDay);
        }

        [Test]
        public void F011_AC05_EmptyDisplay()
        {
            var g = new OrigamiGame();
            Sit(g);
            Waste(g);
            var before = g.Snapshot.Money;
            var r = Night(g);
            Assert.AreEqual(0, r.Settlement.Lines.Count);
            Assert.AreEqual(0, r.Settlement.Total);
            Assert.AreEqual(before, g.Snapshot.Money);
            Assert.AreEqual(before, r.Settlement.MoneyAfter);
        }

        [Test]
        public void R016_SettlementUsesTheSaleDayPrice()
        {
            // Day 2 popular = house: house ★3 → 18.
            var g = new OrigamiGame();
            Night(g);
            Sit(g);
            Ref(g, ModelIds.House);
            var r = Night(g);
            Assert.AreEqual(1, r.Settlement.Lines.Count);
            Assert.IsTrue(r.Settlement.Lines[0].Popular);
            Assert.AreEqual(Pricing.SalePrice(ModelIds.House, Grade.Star3, 2), r.Settlement.Lines[0].Price);
            Assert.AreEqual(18, r.Settlement.Lines[0].Price);
            Assert.AreEqual(ModelIds.Tulip, r.Settlement.NextPopularModelId);
            Assert.AreEqual(ModelIds.Tulip, g.Snapshot.PopularModelId);
        }

        [Test]
        public void R016_Day7SalesAreCountedBeforeTheEnd()
        {
            var g = new OrigamiGame();
            Times(6, () => Night(g));
            Assert.AreEqual(7, g.Snapshot.Day);
            Sit(g);
            Ref(g, ModelIds.Plane);   // free tutorial sheet, plane ★3 normal on day 7 (cicada popular) = 9
            var r = Night(g);
            Assert.AreEqual(9, r.Settlement.Total);
            Assert.AreEqual(RunEndReason.Completed, r.Settlement.EndReason);
            Assert.AreEqual(29, g.Snapshot.Money);
            Assert.AreEqual(7, g.Snapshot.Day);
            Assert.AreEqual(EndingIds.LocalWorkshop, g.Snapshot.EndingId);
        }
    }
}
