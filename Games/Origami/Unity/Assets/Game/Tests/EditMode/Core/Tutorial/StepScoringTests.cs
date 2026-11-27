using System;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Core.Tutorial;

namespace Origami.Tests.Tutorial
{
    // C-140 StepScoring — R-008, F-006 AC-02 / AC-08 (Fold3DSpike FoldScoring.Evaluate formula).
    public class StepScoringTests
    {
        private static readonly double X0 = 2 * Math.Tan(Math.PI / 8);
        private static GuideStep Step(int n) => PlaneGuide.Steps[n - 1];

        [Test]
        public void Constants()
        {
            Assert.AreEqual(0.25, StepScoring.OffsetTolerance);
            Assert.AreEqual(20.0, StepScoring.AngleTolerance);
        }

        [Test]
        public void F006_AC08_ExactInputOfEveryStepIs100()
        {
            foreach (var g in PlaneGuide.Steps)
            {
                var s = StepScoring.Evaluate(g, g.Kind, g.Grab, g.Drop);
                Assert.AreEqual(100, s.Score, "step " + g.Number);
                Assert.AreEqual(0, s.CreaseOffset, 1e-9, "offset " + g.Number);
                Assert.AreEqual(0, s.AngleErrorDeg, 1e-5, "angle " + g.Number);   // acos near 1 amplifies rounding (~sqrt(eps))
                Assert.AreEqual(0, s.GrabError, 1e-12);
                Assert.AreEqual(0, s.DropError, 1e-12);
                Assert.IsFalse(s.WrongSide);
                Assert.IsFalse(s.KindMismatch);
            }
        }

        [Test]
        public void F006_AC02_KindMismatchScoresZero()
        {
            var g = Step(1);
            var s = StepScoring.Evaluate(g, FoldStepKind.Fold, g.Grab, g.Drop);
            Assert.AreEqual(0, s.Score);
            Assert.IsTrue(s.KindMismatch);
            var t = StepScoring.Evaluate(Step(2), FoldStepKind.Crease, Step(2).Grab, Step(2).Drop);
            Assert.AreEqual(0, t.Score);
            Assert.IsTrue(t.KindMismatch);
            Assert.AreEqual(0, StepScoring.Evaluate(Step(2), FoldStepKind.Flip, Step(2).Grab, Step(2).Drop).Score);
        }

        [Test]
        public void F006_AC08_CreaseStepUsesSameFormula_ParallelOffsetHalvesPosition()
        {
            // Step 1 ideal line x = 0; drop (1.25, 1) puts the line at x = 0.125 → offset 0.125, pos 0.5, angle 0 → 75.
            var s = StepScoring.Evaluate(Step(1), FoldStepKind.Crease, new Vec2(-1, 1), new Vec2(1.25, 1));
            Assert.AreEqual(0.125, s.CreaseOffset, 1e-9);
            Assert.AreEqual(0, s.AngleErrorDeg, 1e-6);
            Assert.AreEqual(0.25, s.DropError, 1e-9);
            Assert.AreEqual(75, s.Score);
        }

        [Test]
        public void F006_AC08_AngleError()
        {
            // Line through the origin rotated 10°: offset = sin 10°, score = round(100·(0.5·(1 − sin10/0.25) + 0.5·0.5)) = 40.
            double r = 10 * Math.PI / 180;
            var n = new Vec2(Math.Cos(r), Math.Sin(r));
            var s = StepScoring.Evaluate(Step(1), FoldStepKind.Crease, -1 * n, n);
            Assert.AreEqual(10, s.AngleErrorDeg, 1e-6);
            Assert.AreEqual(Math.Sin(r), s.CreaseOffset, 1e-9);
            Assert.AreEqual(40, s.Score);
        }

        [Test]
        public void F006_AC08_WrongSideHalvesTheScore()
        {
            // Step 2 reversed: same line, but the other half moves.
            var s = StepScoring.Evaluate(Step(2), FoldStepKind.Fold, new Vec2(0, 0), new Vec2(-1, 1));
            Assert.IsTrue(s.WrongSide);
            Assert.AreEqual(50, s.Score);
        }

        [Test]
        public void F006_AC08_EitherSideAcceptsTheMirroredFold()
        {
            var s6 = StepScoring.Evaluate(Step(6), FoldStepKind.Fold, new Vec2(X0, -1), new Vec2(-X0, -1));
            Assert.AreEqual(100, s6.Score);
            Assert.IsFalse(s6.WrongSide);
            Assert.AreEqual(0, s6.GrabError, 1e-12);
            Assert.AreEqual(0, s6.DropError, 1e-12);
            var s1 = StepScoring.Evaluate(Step(1), FoldStepKind.Crease, new Vec2(1, 1), new Vec2(-1, 1));
            Assert.AreEqual(100, s1.Score);
            Assert.IsFalse(s1.WrongSide);
        }

        [Test]
        public void FarOffInput_ClampsToZero()
        {
            var s = StepScoring.Evaluate(Step(2), FoldStepKind.Fold, new Vec2(5, 5), new Vec2(5.5, -3));
            Assert.GreaterOrEqual(s.Score, 0);
            Assert.LessOrEqual(s.Score, 100);
            var far = StepScoring.Evaluate(Step(1), FoldStepKind.Crease, new Vec2(3, -1), new Vec2(3, 1));
            Assert.AreEqual(0, far.Score);
        }

        [Test]
        public void Deterministic()
        {
            var a = StepScoring.Evaluate(Step(4), FoldStepKind.Fold, new Vec2(-0.97, 0.02), new Vec2(0.01, -0.43));
            for (int i = 0; i < 20; i++)
            {
                var b = StepScoring.Evaluate(Step(4), FoldStepKind.Fold, new Vec2(-0.97, 0.02), new Vec2(0.01, -0.43));
                Assert.AreEqual(a.Score, b.Score);
                Assert.AreEqual(a.CreaseOffset, b.CreaseOffset);
                Assert.AreEqual(a.AngleErrorDeg, b.AngleErrorDeg);
            }
        }

        [Test]
        public void NullTarget_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StepScoring.Evaluate(null, FoldStepKind.Fold, new Vec2(0, 0), new Vec2(1, 0)));
        }
    }
}
