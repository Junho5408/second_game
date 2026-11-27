using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Game;
using Origami.Core.Recognition;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Game
{
    // F-007 AC-04~05 (book page / visibility, R-010) and F-008 AC-01~05 (buying pages, R-013/R-014).
    public class F007F008BookTests
    {
        [Test]
        public void F007_AC04_TurnPageClampsToEvenSpread()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, g.OpenBook());
            Assert.AreEqual(0, g.Snapshot.BookPage);
            var expected = new[] { (-1, 0), (17, 16), (31, 30), (43, 42), (99, 42), (int.MinValue, 0), (int.MaxValue, 42), (2, 2), (1, 0) };
            foreach (var (p, e) in expected)
            {
                Ok(g, g.TurnPage(p));
                Assert.AreEqual(e, g.Snapshot.BookPage, "TurnPage(" + p + ")");
            }
            Ok(g, g.TurnPage(20));
            Ok(g, g.CloseBook());
            Ok(g, g.OpenBook());
            Assert.AreEqual(20, g.Snapshot.BookPage);
            Stand(g);
            Ok(g, g.Interact(InteractTargets.Bookshelf));
            Assert.AreEqual(20, g.Snapshot.BookPage, "same book on the shelf");
            Ok(g, g.TurnPage(9));
            Assert.AreEqual(8, g.Snapshot.BookPage);
            Ok(g, g.NewRun());
            Assert.AreEqual(0, g.Snapshot.BookPage);
        }

        [Test]
        public void F007_AC05_FoldWithOpenBook_TurnPageNeedsVisibleBook()
        {
            var g = new OrigamiGame();
            Sit(g);
            Rejected(g, x => x.TurnPage(4), RejectReason.WrongMode);
            Assert.IsFalse(g.Snapshot.BookVisible);
            Take(g);
            Ok(g, g.OpenBook());
            Assert.IsTrue(g.Snapshot.BookVisible);
            Fold(g, V(-1, 1), V(-1, -1));
            Assert.AreEqual(1, g.Snapshot.Sheet.FoldCount);
            Ok(g, g.CloseBook());
            Rejected(g, x => x.TurnPage(4), RejectReason.WrongMode);
        }

        [Test]
        public void F008_AC01_BuyTulipOnTheShelf()
        {
            var g = new OrigamiGame();
            Ok(g, g.Interact(InteractTargets.Bookshelf));
            Ok(g, g.BuyPage(ModelIds.Tulip));
            var s = g.Snapshot;
            Assert.AreEqual(0, s.Money);
            Assert.AreEqual(20, s.TotalBookCost);
            CollectionAssert.AreEqual(new[] { ModelIds.Plane, ModelIds.House, ModelIds.Tulip }, s.OwnedModelIds);
        }

        [Test]
        public void F008_AC02_AlreadyOwnedAndUnknown()
        {
            var g = new OrigamiGame();
            Ok(g, g.Interact(InteractTargets.Bookshelf));
            Ok(g, g.BuyPage(ModelIds.Tulip));
            Rejected(g, x => x.BuyPage(ModelIds.Tulip), RejectReason.AlreadyOwned);
            Rejected(g, x => x.BuyPage(ModelIds.Plane), RejectReason.AlreadyOwned);
            Rejected(g, x => x.BuyPage("unicorn"), RejectReason.UnknownModel);
            Rejected(g, x => x.BuyPage(""), RejectReason.UnknownModel);
            Rejected(g, x => x.BuyPage(null), RejectReason.UnknownModel);
            Rejected(g, x => x.BuyPage("Dog"), RejectReason.UnknownModel);   // Ordinal
        }

        [Test]
        public void F008_AC03_NotEnoughMoney()
        {
            var g = new OrigamiGame();
            Ok(g, g.Interact(InteractTargets.Bookshelf));
            Rejected(g, x => x.BuyPage(ModelIds.Dog), RejectReason.NotEnoughMoney);
            Assert.AreEqual(20, g.Snapshot.Money);
            Rejected(g, x => x.BuyPage(ModelIds.Cicada), RejectReason.NotEnoughMoney);
            // AlreadyOwned is checked before NotEnoughMoney
            Ok(g, g.BuyPage(ModelIds.Tulip));
            Rejected(g, x => x.BuyPage(ModelIds.Tulip), RejectReason.AlreadyOwned);
        }

        [Test]
        public void F008_AC04_BookMustBeVisible()
        {
            var g = new OrigamiGame();
            Rejected(g, x => x.BuyPage(ModelIds.Tulip), RejectReason.WrongMode);
            Rejected(g, x => x.BuyPage("unicorn"), RejectReason.WrongMode);   // WrongMode before UnknownModel
            Sit(g);
            Rejected(g, x => x.BuyPage(ModelIds.Tulip), RejectReason.WrongMode);
            Ok(g, g.OpenBook());
            Ok(g, g.BuyPage(ModelIds.Tulip));
            Assert.AreEqual(0, g.Snapshot.Money);
        }

        [Test]
        public void F008_AC05_BuyingMakesTheModelACandidate()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Ref(g, ModelIds.Plane));
            Night(g);
            Assert.AreEqual(77, g.Snapshot.Money);
            Sit(g);
            Take(g);
            FoldSteps(g, ModelIds.Dog);
            var crumpled = Finish(g);
            Assert.IsFalse(crumpled.Recognition.IsPiece, "J-12: dog not owned");
            Assert.AreEqual(ModelIds.House, crumpled.Recognition.BestModelId);
            Take(g);
            Ok(g, g.OpenBook());
            Ok(g, g.BuyPage(ModelIds.Dog));   // while a sheet is on the table
            Assert.AreEqual(23, g.Snapshot.Money);
            FoldSteps(g, ModelIds.Dog);
            var dog = Finish(g);
            Assert.AreEqual(ModelIds.Dog, dog.Recognition.ModelId);
            Assert.AreEqual(Grade.Star3, dog.Recognition.Grade);
            Assert.AreEqual(23, g.Snapshot.Money);
            Assert.AreEqual(1, g.Snapshot.FailureCount);
        }
    }
}
