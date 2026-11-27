using NUnit.Framework;
using Fold3DSpike.Core.Folding;

namespace Fold3DSpike.Tests
{
    // Engine-free: runs under both `dotnet test` (Core.Tests) and Unity EditMode.
    public class FoldingTests
    {
        [Test]
        public void FoldInHalf_SplitsIntoTwoLayersAndKeepsArea()
        {
            var s = new FoldSession();
            var plan = s.Fold(new Vec2(-1, -1), new Vec2(1, -1), false);
            Assert.IsTrue(plan.Valid, plan.Reason);
            Assert.AreEqual(2, s.State.Facets.Count);
            Assert.AreEqual(4.0, s.State.TotalArea(), 1e-9);
            Assert.AreEqual(1, s.State.MaxLayer());
            foreach (var f in s.State.Facets)
            {
                Assert.Greater(f.SignedArea(), 0, "facets stay counter-clockwise");
                foreach (var v in f.World) Assert.GreaterOrEqual(v.X, -1e-9, "all paper now on the right half");
            }
        }

        [Test]
        public void MountainFold_PutsFlapUnderneath()
        {
            var s = new FoldSession();
            s.Fold(new Vec2(-1, 1), new Vec2(0, 0), true);
            Facet flap = null, body = null;
            foreach (var f in s.State.Facets)
            {
                if (f.FrontUp) flap = f;
                else body = f;
            }
            Assert.IsNotNull(flap);
            Assert.IsNotNull(body);
            Assert.Less(flap.Layer, body.Layer);
        }

        [Test]
        public void PerfectPaperPlane_ScoresHundredEveryStep()
        {
            var s = new FoldSession();
            foreach (var step in s.Steps)
            {
                var plan = s.Fold(step.Grab, step.Drop, false);
                Assert.IsTrue(plan.Valid, step.Title + ": " + plan.Reason);
                Assert.AreEqual(100, s.LastScore.Score, step.Title);
            }
            Assert.IsTrue(s.GuideComplete);
            Assert.AreEqual(100, s.TotalScore);
            Assert.AreEqual(4.0, s.State.TotalArea(), 1e-6);
            // Folded in half along x = 0: everything on the right half.
            foreach (var f in s.State.Facets)
            foreach (var v in f.World)
                Assert.GreaterOrEqual(v.X, -1e-6);
        }

        [Test]
        public void SloppyFold_ScoresLowerButIsAccepted()
        {
            var s = new FoldSession();
            var plan = s.Fold(new Vec2(-1, 1), new Vec2(0.2, -0.3), false);
            Assert.IsTrue(plan.Valid);
            Assert.That(s.LastScore.Score, Is.InRange(0, 99));
            Assert.AreEqual(1, s.StepIndex);
        }

        [Test]
        public void WrongSide_HalvesScore()
        {
            var s = new FoldSession();
            // Same crease as step 1, but the big body is folded over the corner instead.
            s.Fold(new Vec2(0, 0), new Vec2(-1, 1), false);
            Assert.IsTrue(s.LastScore.WrongSide);
            Assert.LessOrEqual(s.LastScore.Score, 50);
        }

        [Test]
        public void Undo_RestoresStateAndStep()
        {
            var s = new FoldSession();
            s.Fold(new Vec2(-1, 1), new Vec2(0, 0), false);
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(1, s.State.Facets.Count);
            Assert.AreEqual(0, s.StepIndex);
            Assert.AreEqual(0, s.Scores.Count);
            Assert.IsFalse(s.Undo());
        }

        [Test]
        public void TinyDragOrCreaseOutsidePaper_IsRejected()
        {
            var s = new FoldSession();
            Assert.IsFalse(s.Fold(new Vec2(0, 0), new Vec2(0.01, 0), false).Valid);
            Assert.IsFalse(s.Fold(new Vec2(0, 0), new Vec2(5, 0), false).Valid, "crease at x=2.5 moves everything");
            Assert.AreEqual(0, s.FoldCount);
        }

        [Test]
        public void Flip_TogglesSidesAndReversesLayers()
        {
            var s = new FoldSession();
            s.Fold(new Vec2(-1, 1), new Vec2(0, 0), false);
            s.Flip();
            int frontUp = 0;
            foreach (var f in s.State.Facets) if (f.FrontUp) frontUp++;
            Assert.AreEqual(s.State.Facets.Count - 1, frontUp);
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(1, s.FoldCount);
        }

        [Test]
        public void SnapGrab_PrefersCorners()
        {
            var st = PaperState.CreateSquare(1, false);
            Assert.IsTrue(st.TrySnapGrab(new Vec2(-0.95, 0.93), 0.15, 0.08, out var g, out var corner));
            Assert.IsTrue(corner);
            Assert.AreEqual(-1.0, g.X, 1e-12);
            Assert.AreEqual(1.0, g.Y, 1e-12);
            Assert.IsFalse(st.TrySnapGrab(new Vec2(3, 3), 0.15, 0.08, out _, out _));
        }
    }
}
