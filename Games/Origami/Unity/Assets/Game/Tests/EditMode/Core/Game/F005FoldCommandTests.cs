using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Recognition;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Game
{
    // F-005 AC-01~05, AC-07~10 at command level (R-007). Engine-level checks are ORIGAMI-009's.
    public class F005FoldCommandTests
    {
        private const double Tol = 1e-6;

        private static OrigamiGame SeatedWithSheet(bool tutorial = false)
        {
            var g = new OrigamiGame();
            Sit(g);
            if (!tutorial) Waste(g);   // use up the free tutorial sheet
            Take(g);
            return g;
        }

        private static double OutlineArea(OrigamiGame g) => Outline.FromState(g.Snapshot.Sheet.State).Area;

        [Test]
        public void F005_AC01_FoldInHalf()
        {
            var g = SeatedWithSheet();
            var r = Fold(g, V(-1, 1), V(-1, -1));
            Assert.IsNotNull(r.Plan);
            Assert.IsTrue(r.Plan.Valid);
            Assert.AreSame(r.Plan.Result, g.Snapshot.Sheet.State);
            Assert.AreEqual(1, g.Snapshot.Sheet.FoldCount);
            Assert.AreEqual(2.0, OutlineArea(g), Tol);
            Assert.AreEqual(4.0, g.Snapshot.Sheet.State.TotalArea(), Tol);
            foreach (var f in g.Snapshot.Sheet.State.Facets)
                foreach (var p in f.World)
                {
                    Assert.That(p.X, Is.InRange(-1 - Tol, 1 + Tol));
                    Assert.That(p.Y, Is.InRange(-1 - Tol, 0 + Tol));
                }
        }

        [Test]
        public void F005_AC02_UndoThenNothingToUndo()
        {
            var g = SeatedWithSheet();
            Fold(g, V(-1, 1), V(-1, -1));
            Ok(g, g.Undo());
            Assert.AreEqual(0, g.Snapshot.Sheet.FoldCount);
            Assert.AreEqual(4.0, OutlineArea(g), Tol);
            Rejected(g, x => x.Undo(), RejectReason.NothingToUndo);
        }

        [Test]
        public void F005_AC03_InvalidFolds()
        {
            var g = SeatedWithSheet();
            var a = Rejected(g, x => x.Fold(V(-1, 1), V(-1, 0.98), false, 0), RejectReason.InvalidFold);
            Assert.AreEqual(FoldError.DragTooShort, a.FoldError);
            var b = Rejected(g, x => x.Fold(V(-1, 1), V(-3, 3), false, 0), RejectReason.InvalidFold);
            Assert.AreEqual(FoldError.LineOutsidePaper, b.FoldError);
            var c = Rejected(g, x => x.Fold(V(-1, 1), V(0, 0), false, -1), RejectReason.InvalidFold);
            Assert.AreEqual(FoldError.InvalidLayers, c.FoldError);
            var d = Rejected(g, x => x.Crease(V(-1, 1), V(0, 0), -1), RejectReason.InvalidFold);
            Assert.AreEqual(FoldError.InvalidLayers, d.FoldError);
            Assert.AreEqual(0, g.Snapshot.Sheet.FoldCount);
            Assert.AreEqual(0, g.Snapshot.Sheet.ActionCount);
        }

        [Test]
        public void F005_AC03_FoldErrorIsNoneForOtherRejections()
        {
            var g = new OrigamiGame();
            Sit(g);
            var r = Rejected(g, x => x.Fold(V(-1, 1), V(-1, 0.98), false, 0), RejectReason.NoSheet);
            Assert.AreEqual(FoldError.None, r.FoldError);
            var ok = Ok(g, g.TakeSheet());
            Assert.AreEqual(FoldError.None, ok.FoldError);
        }

        [Test]
        public void F005_AC03_NonFiniteCoordinatesAreRejectedNotThrown()
        {
            var g = SeatedWithSheet();
            Rejected(g, x => x.Fold(V(double.NaN, 1), V(-1, -1), false, 0), RejectReason.InvalidFold);
            Rejected(g, x => x.Fold(V(-1, 1), V(double.PositiveInfinity, -1), false, 0), RejectReason.InvalidFold);
            Rejected(g, x => x.Crease(V(-1, double.NaN), V(1, 1), 0), RejectReason.InvalidFold);
        }

        [Test]
        public void F005_AC04_FlipThenUndoOnlyTheFlip()
        {
            var g = SeatedWithSheet();
            Fold(g, V(-1, 1), V(-1, -1));
            var folded = g.Snapshot.Sheet.State;
            Ok(g, g.Flip());
            Assert.AreEqual(1, g.Snapshot.Sheet.FoldCount);
            Assert.AreEqual(2.0, OutlineArea(g), Tol);
            Assert.AreNotSame(folded, g.Snapshot.Sheet.State);
            Ok(g, g.Undo());
            Assert.AreEqual(1, g.Snapshot.Sheet.FoldCount);
            Assert.AreSame(folded, g.Snapshot.Sheet.State, "same facet coordinates as before the flip");
        }

        [Test]
        public void F005_AC05_UnfoldResetsEverything()
        {
            var g = SeatedWithSheet();
            Fold(g, V(-1, 1), V(-1, -1));
            Fold(g, V(-1, 0), V(-0.6, -0.8));
            Fold(g, V(1, 0), V(0.6, -0.8));
            Crease(g, V(-1, -1), V(1, -1));
            Ok(g, g.Unfold());
            var s = g.Snapshot.Sheet;
            Assert.AreEqual(0, s.FoldCount);
            Assert.AreEqual(0, s.CreaseCount);
            Assert.AreEqual(1, s.State.Facets.Count);
            Assert.AreEqual(0, s.State.Creases.Count);
            Assert.AreEqual(4.0, OutlineArea(g), Tol);
            Rejected(g, x => x.Undo(), RejectReason.NothingToUndo);
            Rejected(g, x => x.Unfold(), RejectReason.NothingToUndo);
        }

        [Test]
        public void F005_AC05_UnfoldAfterFlipOnly()
        {
            var g = SeatedWithSheet();
            Rejected(g, x => x.Unfold(), RejectReason.NothingToUndo);
            Ok(g, g.Flip());
            Assert.IsTrue(g.Snapshot.Sheet.State.Facets[0].FrontUp);
            Assert.IsTrue(g.Snapshot.Sheet.CanUndo);
            Ok(g, g.Unfold());
            Assert.AreEqual(1, g.Snapshot.Sheet.State.Facets.Count);
            Assert.IsFalse(g.Snapshot.Sheet.State.Facets[0].FrontUp);
            Assert.IsFalse(g.Snapshot.Sheet.CanUndo);
            Rejected(g, x => x.Unfold(), RejectReason.NothingToUndo);
        }

        [Test]
        public void F005_AC07_StandAndSitKeepTheSheet()
        {
            var g = SeatedWithSheet();
            Fold(g, V(-1, 1), V(-1, -1));
            Fold(g, V(-1, 0), V(-0.6, -0.8));
            var state = g.Snapshot.Sheet.State;
            Stand(g);
            Sit(g);
            Assert.AreEqual(2, g.Snapshot.Sheet.FoldCount);
            Assert.AreSame(state, g.Snapshot.Sheet.State);
            Assert.IsTrue(g.Snapshot.Sheet.CanUndo);
            Ok(g, g.Undo());
            Assert.AreEqual(1, g.Snapshot.Sheet.FoldCount);
        }

        [Test]
        public void F005_AC08_NoSheet()
        {
            var g = new OrigamiGame();
            Sit(g);
            Rejected(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0), RejectReason.NoSheet);
            Rejected(g, x => x.Crease(V(-1, 1), V(1, 1), 0), RejectReason.NoSheet);
            Rejected(g, x => x.Flip(), RejectReason.NoSheet);
            Rejected(g, x => x.Undo(), RejectReason.NoSheet);
            Rejected(g, x => x.Unfold(), RejectReason.NoSheet);
            Rejected(g, x => x.Finish(), RejectReason.NoSheet);
            Rejected(g, x => x.Discard(), RejectReason.NoSheet);
            // NoSheet before InvalidFold
            Rejected(g, x => x.Fold(V(-1, 1), V(0, 0), false, -1), RejectReason.NoSheet);
        }

        [Test]
        public void F005_AC09_NoGuideOnNonTutorialSheets()
        {
            var g = SeatedWithSheet();
            Assert.IsFalse(g.Snapshot.Sheet.IsTutorial);
            Assert.IsFalse(g.Snapshot.Sheet.GuideVisible);
            var results = FoldSteps(g, "plane");
            foreach (var r in results) Assert.IsNull(r.GuideScore);
            Ok(g, g.Flip());
            Ok(g, g.Undo());
            Ok(g, g.Undo());
            Ok(g, g.Unfold());
            Crease(g, V(-1, 1), V(1, 1));
            Assert.IsFalse(g.Snapshot.Sheet.GuideVisible);
            Assert.IsNull(g.Snapshot.Sheet.Guide);
            Assert.AreEqual(0, g.Snapshot.Sheet.GuideScores.Count);
            Rejected(g, x => x.SkipGuide(), RejectReason.NoGuide);
        }

        [Test]
        public void F005_AC10_Crease()
        {
            var g = SeatedWithSheet();
            var r = Crease(g, V(-1, 1), V(1, 1));
            Assert.IsTrue(r.Plan.IsCrease);
            var s = g.Snapshot.Sheet;
            Assert.AreEqual(0, s.FoldCount);
            Assert.AreEqual(1, s.CreaseCount);
            Assert.AreEqual(1, s.ActionCount);
            Assert.AreEqual(1, s.State.Facets.Count);
            Assert.AreEqual(4.0, OutlineArea(g), Tol);
            Assert.AreEqual(1, s.State.Creases.Count);
            var c = s.State.Creases[0];
            Assert.AreEqual(2.0, c.Length, Tol);
            Assert.AreEqual(0, c.A.X, Tol);
            Assert.AreEqual(0, c.B.X, Tol);
            Assert.AreEqual(0, c.A.Y + c.B.Y, Tol);
            Assert.AreEqual(1, System.Math.Abs(c.A.Y), Tol);
            Rejected(g, x => x.Finish(), RejectReason.NoFolds);
            Ok(g, g.Undo());
            Assert.AreEqual(0, g.Snapshot.Sheet.State.Creases.Count);
            Assert.AreEqual(0, g.Snapshot.Sheet.CreaseCount);
        }

        [Test]
        public void R007_LayersArePassedThrough()
        {
            // tulip 4th step needs layers 2 → the command applies the same plan as the engine.
            var g = SeatedWithSheet();
            FoldSteps(g, "tulip", 1, 3);
            var before = g.Snapshot.Sheet.State;
            var r = Fold(g, V(-1, -1), V(0.689655, -0.275862), false, 2);
            var expected = FoldPlanner.PlanFold(before, V(-1, -1), V(0.689655, -0.275862), false, 2);
            Assert.AreEqual(expected.Result.Facets.Count, g.Snapshot.Sheet.State.Facets.Count);
            Assert.AreEqual(2, r.Plan.Layers);
            Assert.AreEqual(8, g.Snapshot.Sheet.State.Facets.Count);
        }
    }
}
