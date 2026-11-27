using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Game;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Game
{
    // F-003 interaction and workshop modes (R-003, R-019).
    public class F003ModeTests
    {
        [Test]
        public void F003_AC01_ModeTransitions()
        {
            var g = new OrigamiGame();
            Ok(g, g.Interact(InteractTargets.Table));
            Assert.AreEqual(WorkshopMode.Seated, g.Snapshot.Mode);
            Assert.IsFalse(g.Snapshot.BookOpen);
            Assert.IsTrue(g.Snapshot.HasSat);
            Ok(g, g.StandUp());
            Assert.AreEqual(WorkshopMode.Walking, g.Snapshot.Mode);
            Assert.IsTrue(g.Snapshot.HasSat, "HasSat stays");

            Ok(g, g.Interact(InteractTargets.Bookshelf));
            Assert.AreEqual(WorkshopMode.Bookshelf, g.Snapshot.Mode);
            Assert.IsTrue(g.Snapshot.BookVisible);
            Ok(g, g.CloseBook());
            Assert.AreEqual(WorkshopMode.Walking, g.Snapshot.Mode);
            Assert.IsFalse(g.Snapshot.BookVisible);

            Ok(g, g.Interact(InteractTargets.Counter));
            Assert.AreEqual(WorkshopMode.Counter, g.Snapshot.Mode);
            Ok(g, g.CloseCounter());
            Assert.AreEqual(WorkshopMode.Walking, g.Snapshot.Mode);

            Ok(g, g.Interact(InteractTargets.Bed));
            Assert.AreEqual(WorkshopMode.SleepConfirm, g.Snapshot.Mode);
            var r = Ok(g, g.Sleep(false));
            Assert.IsNull(r.Settlement);
            Assert.AreEqual(WorkshopMode.Walking, g.Snapshot.Mode);
            Assert.AreEqual(1, g.Snapshot.Day);
        }

        [Test]
        public void F003_AC01_HasSatOnlyFromTable()
        {
            var g = new OrigamiGame();
            Ok(g, g.Interact(InteractTargets.Bookshelf));
            Ok(g, g.CloseBook());
            Assert.IsFalse(g.Snapshot.HasSat);
        }

        [Test]
        public void F003_AC02_WrongMode()
        {
            var g = new OrigamiGame();
            Sit(g);
            Rejected(g, x => x.Interact(InteractTargets.Bookshelf), RejectReason.WrongMode);
            Stand(g);
            Rejected(g, x => x.TakeSheet(), RejectReason.WrongMode);
            Rejected(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0), RejectReason.WrongMode);
            Rejected(g, x => x.StandUp(), RejectReason.WrongMode);
            Rejected(g, x => x.OpenBook(), RejectReason.WrongMode);
            Ok(g, g.Interact(InteractTargets.Bookshelf));
            Rejected(g, x => x.Interact(InteractTargets.Bed), RejectReason.WrongMode);
        }

        [Test]
        public void F003_AC02_WrongModeMatrix()
        {
            // Commands outside their mode (R-003 table) in every non-ended mode.
            var walking = new OrigamiGame();
            Rejected(walking, x => x.CloseBook(), RejectReason.WrongMode);
            Rejected(walking, x => x.CloseCounter(), RejectReason.WrongMode);
            Rejected(walking, x => x.Sleep(true), RejectReason.WrongMode);
            Rejected(walking, x => x.Sleep(false), RejectReason.WrongMode);
            Rejected(walking, x => x.TurnPage(2), RejectReason.WrongMode);
            Rejected(walking, x => x.BuyPage(ModelIds.Tulip), RejectReason.WrongMode);
            Rejected(walking, x => x.Crease(V(-1, 1), V(1, 1), 0), RejectReason.WrongMode);
            Rejected(walking, x => x.Flip(), RejectReason.WrongMode);
            Rejected(walking, x => x.Undo(), RejectReason.WrongMode);
            Rejected(walking, x => x.Unfold(), RejectReason.WrongMode);
            Rejected(walking, x => x.Finish(), RejectReason.WrongMode);
            Rejected(walking, x => x.Discard(), RejectReason.WrongMode);
            Rejected(walking, x => x.SkipGuide(), RejectReason.WrongMode);

            var shelf = new OrigamiGame();
            Ok(shelf, shelf.Interact(InteractTargets.Bookshelf));
            Rejected(shelf, x => x.Fold(V(-1, 1), V(-1, -1), false, 0), RejectReason.WrongMode);
            Rejected(shelf, x => x.TakeSheet(), RejectReason.WrongMode);
            Rejected(shelf, x => x.OpenBook(), RejectReason.WrongMode);
            Rejected(shelf, x => x.StandUp(), RejectReason.WrongMode);
            Rejected(shelf, x => x.CloseCounter(), RejectReason.WrongMode);
            Rejected(shelf, x => x.Sleep(true), RejectReason.WrongMode);

            var counter = new OrigamiGame();
            Ok(counter, counter.Interact(InteractTargets.Counter));
            Rejected(counter, x => x.CloseBook(), RejectReason.WrongMode);
            Rejected(counter, x => x.TurnPage(2), RejectReason.WrongMode);
            Rejected(counter, x => x.BuyPage(ModelIds.Tulip), RejectReason.WrongMode);
            Rejected(counter, x => x.Interact(InteractTargets.Table), RejectReason.WrongMode);
            Rejected(counter, x => x.Sleep(true), RejectReason.WrongMode);

            var bed = new OrigamiGame();
            Ok(bed, bed.Interact(InteractTargets.Bed));
            Rejected(bed, x => x.Interact(InteractTargets.Table), RejectReason.WrongMode);
            Rejected(bed, x => x.CloseCounter(), RejectReason.WrongMode);
            Rejected(bed, x => x.CloseBook(), RejectReason.WrongMode);
            Rejected(bed, x => x.StandUp(), RejectReason.WrongMode);

            var seated = new OrigamiGame();
            Sit(seated);
            Rejected(seated, x => x.CloseCounter(), RejectReason.WrongMode);
            Rejected(seated, x => x.Sleep(true), RejectReason.WrongMode);
            Rejected(seated, x => x.Interact(InteractTargets.Table), RejectReason.WrongMode);
        }

        [Test]
        public void F003_AC03_UnknownTarget()
        {
            var g = new OrigamiGame();
            Rejected(g, x => x.Interact(null), RejectReason.UnknownTarget);
            Rejected(g, x => x.Interact("door"), RejectReason.UnknownTarget);
            Rejected(g, x => x.Interact(""), RejectReason.UnknownTarget);
            Rejected(g, x => x.Interact("Table"), RejectReason.UnknownTarget);   // Ordinal
            Sit(g);
            Rejected(g, x => x.Interact(null), RejectReason.WrongMode);
        }

        [Test]
        public void F003_AC04_OpenBookIsIdempotentAndStandClosesIt()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, g.OpenBook());
            Assert.IsTrue(g.Snapshot.BookOpen);
            Ok(g, g.OpenBook());
            Assert.IsTrue(g.Snapshot.BookOpen);
            Assert.IsTrue(g.Snapshot.BookVisible);
            Ok(g, g.CloseBook());
            Ok(g, g.CloseBook());   // idempotent
            Assert.IsFalse(g.Snapshot.BookOpen);
            Ok(g, g.OpenBook());
            Stand(g);
            Assert.AreEqual(WorkshopMode.Walking, g.Snapshot.Mode);
            Assert.IsFalse(g.Snapshot.BookOpen);
            Sit(g);
            Assert.IsFalse(g.Snapshot.BookOpen);
        }

        [Test]
        public void R003_StandUpKeepsTheSheet()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            Stand(g);
            Assert.IsNotNull(g.Snapshot.Sheet);
            Rejected(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0), RejectReason.WrongMode);
            Sit(g);
            Assert.IsNotNull(g.Snapshot.Sheet);
        }
    }
}
