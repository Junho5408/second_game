using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Texts;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-016: pins the C-150 / C-170 wording added to INTERFACE_CONTRACTS — non-finite Fold/Crease coordinates are
    /// InvalidFold + FoldError.NothingToFold (after the layers check, no state change, no guide score), their toast is the
    /// NothingToFold text, and NoSheet / InvalidLayers are silent.
    /// </summary>
    [TestFixture]
    public class ContractDocsV2QaTests
    {
        private static OrigamiGame SeatedWithTutorialSheet()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);   // first sheet = tutorial sheet, guide visible
            return g;
        }

        [TestCase(double.NaN, 1.0, -1.0, -1.0)]
        [TestCase(-1.0, double.NaN, -1.0, -1.0)]
        [TestCase(-1.0, 1.0, double.PositiveInfinity, -1.0)]
        [TestCase(-1.0, 1.0, -1.0, double.NegativeInfinity)]
        public void C150_NonFiniteFoldAndCrease_AreNothingToFold(double gx, double gy, double dx, double dy)
        {
            var g = SeatedWithTutorialSheet();
            var f = Rejected(g, x => x.Fold(V(gx, gy), V(dx, dy), false, 0), RejectReason.InvalidFold, "fold");
            Assert.AreEqual(FoldError.NothingToFold, f.FoldError);
            var c = Rejected(g, x => x.Crease(V(gx, gy), V(dx, dy), 0), RejectReason.InvalidFold, "crease");
            Assert.AreEqual(FoldError.NothingToFold, c.FoldError);
            Assert.AreEqual(0, g.Snapshot.Sheet.ActionCount);
            Assert.AreEqual(0, g.Snapshot.Sheet.GuideScores.Count, "no guide score recorded");
            Assert.AreEqual("접히는 부분이 없어요", GameTexts.RejectMessage(f.Reason, f.FoldError));
        }

        [Test]
        public void C150_NegativeLayersCheckedBeforeNonFiniteCoordinates()
        {
            var g = SeatedWithTutorialSheet();
            var f = Rejected(g, x => x.Fold(V(double.NaN, 1), V(-1, -1), false, -1), RejectReason.InvalidFold);
            Assert.AreEqual(FoldError.InvalidLayers, f.FoldError);
            var c = Rejected(g, x => x.Crease(V(double.NaN, 1), V(-1, -1), -1), RejectReason.InvalidFold);
            Assert.AreEqual(FoldError.InvalidLayers, c.FoldError);
            Assert.AreEqual("", GameTexts.RejectMessage(f.Reason, f.FoldError), "InvalidLayers is silent");
        }

        [Test]
        public void C150_NoSheetCheckedBeforeNonFiniteCoordinates()
        {
            var g = new OrigamiGame();
            Sit(g);
            var r = Rejected(g, x => x.Fold(V(double.NaN, 1), V(-1, -1), false, 0), RejectReason.NoSheet);
            Assert.AreEqual(FoldError.None, r.FoldError);
            Assert.AreEqual("", GameTexts.RejectMessage(r.Reason, r.FoldError), "C-170: NoSheet is silent");
        }
    }
}
