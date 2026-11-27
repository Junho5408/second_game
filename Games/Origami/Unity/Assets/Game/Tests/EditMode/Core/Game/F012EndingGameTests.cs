using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Game;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Game
{
    // F-012 AC-08 (run result data, NewRun after the end). AC-01~07 are EndingRules tests (Economy/EndingRulesTests).
    public class F012EndingGameTests
    {
        [Test]
        public void F012_AC08_SevenIdleNights()
        {
            var g = new OrigamiGame();
            Times(7, () => Night(g));
            var s = g.Snapshot;
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);
            Assert.AreEqual(20, s.Money);
            Assert.AreEqual(0, s.FailureCount);
            Assert.AreEqual(0, s.TotalSales);
            Assert.AreEqual(0, s.TotalPaperCost);
            Assert.AreEqual(0, s.TotalBookCost);
            Ok(g, g.NewRun());
            F001RunTests.AssertStartState(g.Snapshot);
        }

        [Test]
        public void F012_EndingMatchesEndingRules_ForBankruptRun()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Waste(g));
            Night(g);
            Sit(g);
            Times(5, () => Waste(g));
            Night(g);
            Sit(g);
            Waste(g);
            Night(g);
            Assert.AreEqual(RunEndReason.Bankrupt, g.Snapshot.EndReason);
            Assert.AreEqual(EndingRules.Evaluate(RunEndReason.Bankrupt, 0, g.Snapshot.Completions), g.Snapshot.EndingId);
        }

        [Test]
        public void R019_EveryCommandButNewRunIsRejectedAfterTheEnd()
        {
            var g = new OrigamiGame();
            Times(7, () => Night(g));
            Rejected(g, x => x.Interact(InteractTargets.Table), RejectReason.RunEnded);
            Rejected(g, x => x.Interact(null), RejectReason.RunEnded);
            Rejected(g, x => x.StandUp(), RejectReason.RunEnded);
            Rejected(g, x => x.OpenBook(), RejectReason.RunEnded);
            Rejected(g, x => x.CloseBook(), RejectReason.RunEnded);
            Rejected(g, x => x.TurnPage(2), RejectReason.RunEnded);
            Rejected(g, x => x.BuyPage(ModelIds.Tulip), RejectReason.RunEnded);
            Rejected(g, x => x.BuyPage(null), RejectReason.RunEnded);
            Rejected(g, x => x.CloseCounter(), RejectReason.RunEnded);
            Rejected(g, x => x.Sleep(true), RejectReason.RunEnded);
            Rejected(g, x => x.Sleep(false), RejectReason.RunEnded);
            Rejected(g, x => x.TakeSheet(), RejectReason.RunEnded);
            Rejected(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0), RejectReason.RunEnded);
            Rejected(g, x => x.Fold(V(-1, 1), V(-1, -1), false, -1), RejectReason.RunEnded);
            Rejected(g, x => x.Crease(V(-1, 1), V(1, 1), 0), RejectReason.RunEnded);
            Rejected(g, x => x.Flip(), RejectReason.RunEnded);
            Rejected(g, x => x.Undo(), RejectReason.RunEnded);
            Rejected(g, x => x.Unfold(), RejectReason.RunEnded);
            Rejected(g, x => x.Finish(), RejectReason.RunEnded);
            Rejected(g, x => x.Discard(), RejectReason.RunEnded);
            Rejected(g, x => x.SkipGuide(), RejectReason.RunEnded);
            Ok(g, g.NewRun());
        }
    }
}
