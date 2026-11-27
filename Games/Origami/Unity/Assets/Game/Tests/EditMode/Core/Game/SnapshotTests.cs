using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Game;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Game
{
    // C-150/C-151 snapshot rules: immutable, new object only on accepted commands, read-only lists.
    public class SnapshotTests
    {
        [Test]
        public void AcceptedCommandsReplaceTheSnapshot_OldOnesDoNotChange()
        {
            var g = new OrigamiGame();
            var s0 = g.Snapshot;
            Sit(g);
            var s1 = g.Snapshot;
            Assert.AreNotSame(s0, s1);
            Assert.AreEqual(WorkshopMode.Walking, s0.Mode);
            Take(g);
            var s2 = g.Snapshot;
            Fold(g, V(-1, 1), V(-1, -1));
            Assert.AreEqual(0, s2.Sheet.FoldCount, "old sheet snapshot untouched");
            Assert.IsNull(s1.Sheet);
            Assert.AreEqual(1, g.Snapshot.Sheet.FoldCount);
            Assert.AreSame(g.Snapshot, g.Snapshot, "stable between commands");
        }

        [Test]
        public void ListsAreReadOnly()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ref(g, ModelIds.House);
            Take(g);
            Crease(g, V(-1, 1), V(1, 1));
            var s = g.Snapshot;
            Assert.IsTrue(((IList<string>)s.OwnedModelIds).IsReadOnly);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)s.OwnedModelIds).Add("dog"));
            Assert.IsTrue(((IList<DisplayedPiece>)s.Display).IsReadOnly);
            Assert.Throws<NotSupportedException>(() => ((IList<DisplayedPiece>)s.Display).Clear());
            Assert.IsTrue(((IList<Origami.Core.Tutorial.StepScore>)s.Sheet.GuideScores).IsReadOnly);
            Ok(g, g.Discard());
        }

        [Test]
        public void SettlementLinesAreReadOnly()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ref(g, ModelIds.House);
            var r = Night(g);
            Assert.IsTrue(((IList<SaleLine>)r.Settlement.Lines).IsReadOnly);
        }

        [Test]
        public void BookVisibleFollowsModeAndOpenFlag()
        {
            var g = new OrigamiGame();
            Assert.IsFalse(g.Snapshot.BookVisible);
            Ok(g, g.Interact(InteractTargets.Bookshelf));
            Assert.IsTrue(g.Snapshot.BookVisible);
            Assert.IsFalse(g.Snapshot.BookOpen, "BookOpen is the seated flag");
            Ok(g, g.CloseBook());
            Sit(g);
            Assert.IsFalse(g.Snapshot.BookVisible);
            Ok(g, g.OpenBook());
            Assert.IsTrue(g.Snapshot.BookVisible);
        }

        [Test]
        public void CommandsNeverThrow_OnOddInput()
        {
            var g = new OrigamiGame();
            Assert.DoesNotThrow(() =>
            {
                g.Interact("  ");
                g.TurnPage(int.MinValue);
                g.BuyPage(null);
                g.Interact(InteractTargets.Table);
                g.TakeSheet();
                g.Fold(V(double.NaN, double.NaN), V(0, 0), true, int.MaxValue);
                g.Fold(V(-1, 1), V(-1, -1), true, int.MaxValue);
                g.Crease(V(0, 0), V(0, 0), 0);
                g.Fold(V(1e300, 1e300), V(-1e300, -1e300), false, 0);
                g.SkipGuide();
                g.SkipGuide();
                g.Finish();
            });
            CheckInvariants(g);
        }
    }
}
