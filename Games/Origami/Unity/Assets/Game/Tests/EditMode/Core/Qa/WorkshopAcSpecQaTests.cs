using System.Linq;
using NUnit.Framework;
using Origami.Core.Economy;
using Origami.Core.Game;
using Origami.Core.Recognition;
using static Origami.Tests.Qa.WsDriver;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-010: black-box checks of the owned [Core] acceptance criteria not already covered by the scripted runs,
    /// matrix, tutorial and fuzz fixtures. Expected values are typed from FEATURE_SPECS / CONTENT.
    /// </summary>
    [TestFixture]
    public class WorkshopAcSpecQaTests
    {
        private static void AssertFreshRun(GameSnapshot s)
        {
            Assert.That(s.Day, Is.EqualTo(1));
            Assert.That(s.Money, Is.EqualTo(20));
            Assert.That(s.SheetsLeft, Is.EqualTo(5));
            CollectionAssert.AreEqual(new[] { "plane", "house" }, s.OwnedModelIds);
            Assert.That(s.FreeSheetAvailable, Is.True);
            Assert.That(s.TutorialFinished, Is.False);
            Assert.That(s.HasSat, Is.False);
            Assert.That(s.Mode, Is.EqualTo(WorkshopMode.Walking));
            Assert.That(s.Sheet, Is.Null);
            Assert.That(s.BookOpen, Is.False);
            Assert.That(s.BookPage, Is.EqualTo(0));
            Assert.That(s.Display.Count, Is.EqualTo(0));
            Assert.That(s.Completions, Is.EqualTo(CompletionCounts.Empty));
            Assert.That(s.FailureCount + s.TotalSales + s.TotalPaperCost + s.TotalBookCost, Is.EqualTo(0));
            Assert.That(s.IsEnded, Is.False);
            Assert.That(s.EndReason, Is.EqualTo(RunEndReason.None));
            Assert.That(s.EndingId, Is.Null);
            Assert.That(s.PopularModelId, Is.EqualTo("plane"));
        }

        [Test]
        public void F001_AC01_AC07_NewRunState_AndNewRunMidDay2()
        {
            var g = new OrigamiGame();
            AssertFreshRun(g.Snapshot);
            Night(g);
            Sit(g);
            Buy(g, "tulip");
            Ok(g, x => x.TakeSheet());
            Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0));
            Ok(g, x => x.NewRun());
            AssertFreshRun(g.Snapshot);
            Ok(g, x => x.NewRun());   // NewRun from a fresh run is accepted too
            AssertFreshRun(g.Snapshot);
        }

        [Test]
        public void F001_AC02_AC04_OneNight_AndSheetOnTableBlocksBed()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, x => x.TakeSheet());
            Stand(g);
            Rej(g, x => x.Interact(Bed), RejectReason.SheetOnTable);
            Assert.That(g.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Sit(g);
            Ok(g, x => x.Discard());
            Night(g);
            Assert.That(g.Snapshot.Day, Is.EqualTo(2));
            Assert.That(g.Snapshot.Money, Is.EqualTo(20));
            Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(5));
            Assert.That(g.Snapshot.FreeSheetAvailable, Is.False, "the free first sheet was used on day 1");
        }

        [Test]
        public void F001_AC06_MoneyZeroDuringTheDayIsNotBankruptcy()
        {
            var g = new OrigamiGame();
            Sit(g); Times(5, () => Waste(g)); Night(g);
            Sit(g); Times(5, () => Waste(g)); Night(g);
            Sit(g);
            Ref3(g, "house");
            Assert.That(g.Snapshot.Money, Is.EqualTo(0));
            Assert.That(g.Snapshot.IsEnded, Is.False);
            var st = Night(g);
            Assert.That(st.Total, Is.EqualTo(12), "day 3 house ★3 normal");
            Assert.That(g.Snapshot.Day, Is.EqualTo(4));
            Assert.That(g.Snapshot.Money, Is.EqualTo(12));
            Assert.That(g.Snapshot.IsEnded, Is.False);
        }

        [Test]
        public void F003_AC01_AC04_ModeRoundTrips()
        {
            var g = new OrigamiGame();
            Sit(g);
            Assert.That(g.Snapshot.Mode, Is.EqualTo(WorkshopMode.Seated));
            Assert.That(g.Snapshot.HasSat, Is.True);
            Ok(g, x => x.OpenBook());
            Ok(g, x => x.OpenBook());
            Assert.That(g.Snapshot.BookOpen, Is.True);
            Assert.That(g.Snapshot.BookVisible, Is.True);
            Stand(g);
            Sit(g);
            Assert.That(g.Snapshot.BookOpen, Is.False);
            Stand(g);
            Ok(g, x => x.Interact(Bookshelf));
            Assert.That(g.Snapshot.BookVisible, Is.True);
            Ok(g, x => x.CloseBook());
            Ok(g, x => x.Interact(Counter));
            Assert.That(g.Snapshot.Mode, Is.EqualTo(WorkshopMode.Counter));
            Ok(g, x => x.CloseCounter());
            Ok(g, x => x.Interact(Bed));
            Ok(g, x => x.Sleep(false));
            Assert.That(g.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Assert.That(g.Snapshot.Day, Is.EqualTo(1));
            Assert.That(g.Snapshot.HasSat, Is.True, "HasSat persists for the run");
        }

        [Test]
        public void F004_AC01_AC02_FirstSheetFreeThenPaid()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, x => x.TakeSheet());
            var sh = g.Snapshot.Sheet;
            Assert.That(sh.State.Facets.Count, Is.EqualTo(1));
            Assert.That(sh.FoldCount, Is.EqualTo(0));
            Assert.That(sh.State.TotalArea(), Is.EqualTo(4.0).Within(1e-6));
            Assert.That(sh.State.Facets[0].FrontUp, Is.False, "white side up (FrontUp = coloured side)");
            Assert.That(g.Snapshot.Money, Is.EqualTo(20));
            Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(4));
            Ok(g, x => x.Discard());
            Ok(g, x => x.TakeSheet());
            Assert.That(g.Snapshot.Money, Is.EqualTo(18));
            Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(3));
            Assert.That(g.Snapshot.TotalPaperCost, Is.EqualTo(2));
            Assert.That(g.Snapshot.Sheet.IsTutorial, Is.False);
        }

        [Test]
        public void F005_CommandLevel_UndoFlipUnfoldStandSit_AndCreases()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, x => x.TakeSheet());
            var r = Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0));
            Assert.That(r.Plan, Is.Not.Null);
            Assert.That(g.Snapshot.Sheet.FoldCount, Is.EqualTo(1));
            Assert.That(g.Snapshot.Sheet.State.TotalArea(), Is.EqualTo(4.0).Within(1e-6));
            var afterFold = g.Snapshot.Sheet.State;
            Ok(g, x => x.Flip());
            Assert.That(g.Snapshot.Sheet.FoldCount, Is.EqualTo(1));
            Ok(g, x => x.Undo());
            V2SpecBook.AssertSameState(afterFold, g.Snapshot.Sheet.State, 1e-6, "undo flip");
            // AC-11: crease across two layers leaves two crease segments; facets and folds unchanged.
            Ok(g, x => x.Crease(V(-1, 0), V(1, 0), 0));
            Assert.That(g.Snapshot.Sheet.State.Creases.Count, Is.EqualTo(2));
            Assert.That(g.Snapshot.Sheet.State.Creases.Sum(c => c.Length), Is.EqualTo(2.0).Within(1e-6));
            Assert.That(g.Snapshot.Sheet.State.Facets.Count, Is.EqualTo(2));
            Assert.That(g.Snapshot.Sheet.FoldCount, Is.EqualTo(1));
            Assert.That(g.Snapshot.Sheet.CreaseCount, Is.EqualTo(1));
            // AC-07: standing up keeps the sheet.
            var keep = g.Snapshot.Sheet.State;
            Stand(g);
            Sit(g);
            V2SpecBook.AssertSameState(keep, g.Snapshot.Sheet.State, 1e-9, "stand/sit");
            Assert.That(g.Snapshot.Sheet.CanUndo, Is.True);
            // AC-05: Unfold resets to the flat square and clears history.
            Ok(g, x => x.Fold(V(1, 0), V(0.6, -0.8), false, 0));
            Ok(g, x => x.Unfold());
            Assert.That(g.Snapshot.Sheet.FoldCount, Is.EqualTo(0));
            Assert.That(g.Snapshot.Sheet.State.Facets.Count, Is.EqualTo(1));
            Assert.That(g.Snapshot.Sheet.State.Creases.Count, Is.EqualTo(0));
            Assert.That(g.Snapshot.Sheet.CanUndo, Is.False);
            Rej(g, x => x.Undo(), RejectReason.NothingToUndo);
            // AC-10
            Ok(g, x => x.Crease(V(-1, 1), V(1, 1), 0));
            Assert.That(g.Snapshot.Sheet.State.Creases.Count, Is.EqualTo(1));
            Assert.That(g.Snapshot.Sheet.State.Creases[0].Length, Is.EqualTo(2.0).Within(1e-6));
            Rej(g, x => x.Finish(), RejectReason.NoFolds);
            Ok(g, x => x.Undo());
            Assert.That(g.Snapshot.Sheet.State.Creases.Count, Is.EqualTo(0));
        }

        [Test]
        public void F005_AC09_NonTutorialSheetNeverShowsGuide()
        {
            var g = new OrigamiGame();
            Sit(g);
            Waste(g);
            Ok(g, x => x.TakeSheet());
            foreach (var row in V2SpecBook.Book("plane"))
            {
                var r = Apply(g, row, "plane");
                Assert.That(r.GuideScore, Is.Null);
                Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False);
                Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void F007_AC05_FoldWithBookOpen()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, x => x.TakeSheet());
            Ok(g, x => x.OpenBook());
            Ok(g, x => x.TurnPage(6));
            Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0));
            Assert.That(g.Snapshot.BookOpen, Is.True);
            Assert.That(g.Snapshot.BookPage, Is.EqualTo(6));
            Ok(g, x => x.CloseBook());
            Rej(g, x => x.TurnPage(8), RejectReason.WrongMode);
        }

        [Test]
        public void F008_AC05_DogWithoutPageIsCrumpled_ThenBoughtIsStar3()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Ref3(g, "plane"));
            Assert.That(Night(g).MoneyAfter, Is.EqualTo(77));
            Sit(g);
            var crumpled = Ref(g, "dog");
            Assert.That(crumpled.IsPiece, Is.False, "dog not owned → crumpled (J-12)");
            Assert.That(g.Snapshot.Display.Count, Is.EqualTo(0));
            Ok(g, x => x.TakeSheet());
            Ok(g, x => x.OpenBook());
            Ok(g, x => x.BuyPage("dog"));
            Assert.That(g.Snapshot.Money, Is.EqualTo(23));
            foreach (var row in V2SpecBook.Book("dog")) Apply(g, row, "dog");
            var fin = Ok(g, x => x.Finish());
            Assert.That(fin.Recognition.ModelId, Is.EqualTo("dog"));
            Assert.That(fin.Recognition.Grade, Is.EqualTo(Grade.Star3));
            Assert.That(g.Snapshot.Money, Is.EqualTo(23));
            Assert.That(g.Snapshot.FailureCount, Is.EqualTo(1));
        }

        [Test]
        public void F010_AC01_AC05_FinishDisplayFailures()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ref3(g, "house");
            var d = g.Snapshot.Display.Single();
            Assert.That((d.ModelId, d.Grade, d.Percent), Is.EqualTo(("house", Grade.Star3, 100)));
            Assert.That(d.Shape, Is.Not.Null);
            Assert.That(g.Snapshot.Completions.Get("house", Grade.Star3), Is.EqualTo(1));
            Assert.That(g.Snapshot.Sheet, Is.Null);
            Assert.That(g.Snapshot.Money, Is.EqualTo(20));
            Ok(g, x => x.TakeSheet());
            Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0));
            var half = Ok(g, x => x.Finish()).Recognition;
            Assert.That(half.IsPiece, Is.False);
            Assert.That(half.BestModelId, Is.EqualTo("house"));
            Assert.That(g.Snapshot.FailureCount, Is.EqualTo(1));
            Assert.That(g.Snapshot.Display.Count, Is.EqualTo(1));
            Assert.That(g.Snapshot.Completions.Total("house"), Is.EqualTo(1));
            Times(3, () => Ref3(g, "house"));
            Assert.That(g.Snapshot.Display.Count, Is.EqualTo(4));
            Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(0));
        }

        [Test]
        public void F011_AC03_AC05_AC06_SettlementObject()
        {
            var g = new OrigamiGame();
            Sit(g);
            Assert.That(Var(g, 0.06).Grade, Is.EqualTo(Grade.Star2));
            Assert.That(Var(g, 0.15).Grade, Is.EqualTo(Grade.Star1));
            Ref3(g, "house");
            Assert.That(g.Snapshot.Money, Is.EqualTo(16));
            var st = Night(g);
            Assert.That(st.Lines.Select(l => (l.ModelId, l.Grade, l.Price)).ToArray(),
                Is.EqualTo(new[] { ("house", Grade.Star2, 8), ("house", Grade.Star1, 4), ("house", Grade.Star3, 12) }));
            Assert.That(st.Total, Is.EqualTo(24));
            Assert.That(st.MoneyAfter, Is.EqualTo(40));
            var empty = Night(g);
            Assert.That(empty.Lines.Count, Is.EqualTo(0));
            Assert.That(empty.Total, Is.EqualTo(0));
            Assert.That(empty.MoneyAfter, Is.EqualTo(40));
        }

        [Test]
        public void F011_AC04_PriceTable30Cells_AndPopularTable()
        {
            string[] ids = { "plane", "house", "tulip", "dog", "cicada" };
            int[,] table = { { 9, 6, 3, 13, 9, 5 }, { 12, 8, 4, 18, 12, 7 }, { 18, 12, 7, 27, 18, 10 }, { 27, 18, 10, 40, 27, 16 }, { 39, 26, 15, 58, 39, 23 } };
            int[] bases = { 6, 8, 12, 18, 26 };
            var grades = new[] { Grade.Star3, Grade.Star2, Grade.Star1 };
            string[] popular = { "plane", "house", "tulip", "house", "dog", "tulip", "cicada" };
            for (int m = 0; m < 5; m++)
                for (int c = 0; c < 6; c++)
                {
                    Assert.That(Pricing.SalePrice(bases[m], grades[c % 3], c >= 3), Is.EqualTo(table[m, c]), $"{ids[m]} col {c}");
                    int popDay = System.Array.IndexOf(popular, ids[m]) + 1;           // first popular day (plane 1, house 2, tulip 3, dog 5, cicada 7)
                    int normalDay = popular[0] == ids[m] ? 2 : 1;
                    Assert.That(Pricing.SalePrice(ids[m], grades[c % 3], c >= 3 ? popDay : normalDay), Is.EqualTo(table[m, c]), $"{ids[m]} by day col {c}");
                }
            var g = new OrigamiGame();
            for (int d = 1; d <= 7; d++)
            {
                Assert.That(g.Snapshot.PopularModelId, Is.EqualTo(popular[d - 1]), "day " + d);
                Night(g);
            }
        }

        [Test]
        public void F012_EndingTable_AC01_AC07()
        {
            CompletionCounts Each3(params string[] ids) => ids.Aggregate(CompletionCounts.Empty, (c, id) => c.With(id, Grade.Star3, 1));
            var all5 = Each3("plane", "house", "tulip", "dog", "cicada");
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 20, CompletionCounts.Empty), Is.EqualTo("E-01"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 21, CompletionCounts.Empty), Is.EqualTo("E-05"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Bankrupt, 0, CompletionCounts.Empty), Is.EqualTo("E-01"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 700, all5.With("tulip", Grade.Star1, 12)), Is.EqualTo("E-02"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 700, all5), Is.EqualTo("E-03"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 600, CompletionCounts.Empty), Is.EqualTo("E-04"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 599, CompletionCounts.Empty), Is.EqualTo("E-05"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 20, CompletionCounts.Empty.With("tulip", Grade.Star3, 12)), Is.EqualTo("E-01"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Bankrupt, 0, all5), Is.EqualTo("E-01"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 650,
                Each3("plane", "house", "dog").With("tulip", Grade.Star3, 11)), Is.EqualTo("E-04"));
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 500,
                Each3("plane", "house", "tulip", "dog").With("cicada", Grade.Star2, 1)), Is.EqualTo("E-05"));
            // Tulip garden counts all grades: 4 + 4 + 4.
            var mixed = CompletionCounts.Empty.With("tulip", Grade.Star1, 4).With("tulip", Grade.Star2, 4).With("tulip", Grade.Star3, 4);
            Assert.That(EndingRules.Evaluate(RunEndReason.Completed, 21, mixed), Is.EqualTo("E-02"));
        }

        [Test]
        public void F012_AC08_IdleRunResult_ThenNewRun()
        {
            var g = new OrigamiGame();
            Times(7, () => Night(g));
            Assert.That(g.Snapshot.EndingId, Is.EqualTo("E-01"));
            Assert.That(g.Snapshot.Money, Is.EqualTo(20));
            Assert.That(g.Snapshot.FailureCount, Is.EqualTo(0));
            Ok(g, x => x.NewRun());
            AssertFreshRun(g.Snapshot);
        }
    }
}
