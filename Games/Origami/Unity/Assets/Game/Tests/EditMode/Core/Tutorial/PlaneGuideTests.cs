using System;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Core.Tutorial;

namespace Origami.Tests.Tutorial
{
    // C-140 PlaneGuide / GuideStep — D-115, R-008, A2-9.
    public class PlaneGuideTests
    {
        private const double Tol = 1e-6;

        private static void Near(Vec2 expected, Vec2 actual, double tol, string msg)
        {
            Assert.AreEqual(expected.X, actual.X, tol, msg + " x");
            Assert.AreEqual(expected.Y, actual.Y, tol, msg + " y");
        }

        [Test]
        public void D115_SixStepsFromPlaneCatalogue()
        {
            Assert.AreEqual(6, PlaneGuide.StepCount);
            var plane = ModelCatalog.Find(ModelIds.Plane);
            var steps = PlaneGuide.Steps;
            Assert.AreEqual(PlaneGuide.StepCount, steps.Count);
            Assert.AreEqual(plane.ActionCount, steps.Count);
            for (int i = 0; i < steps.Count; i++)
            {
                var g = steps[i];
                var s = plane.Steps[i];
                Assert.AreEqual(i + 1, g.Number);
                Assert.AreEqual(s.Kind, g.Kind, "kind " + g.Number);
                Assert.AreEqual(s.Grab, g.Grab, "grab " + g.Number);
                Assert.AreEqual(s.Drop, g.Drop, "drop " + g.Number);
                Assert.AreEqual(plane.StepTexts[i], g.Text, "text " + g.Number);
                Assert.AreEqual(g.Number == 1 || g.Number == 6, g.EitherSide, "either " + g.Number);
                Assert.IsTrue(LineOnPaper.TryGet(ModelCatalog.StateBeforeStep(ModelIds.Plane, g.Number), g.Grab, g.Drop, out var a, out var b));
                Assert.AreEqual(a, g.LineA, "lineA " + g.Number);
                Assert.AreEqual(b, g.LineB, "lineB " + g.Number);
            }
        }

        [Test]
        public void D115_Examples()
        {
            var s1 = PlaneGuide.Steps[0];
            Assert.AreEqual(FoldStepKind.Crease, s1.Kind);
            Near(new Vec2(-1, 1), s1.Grab, Tol, "s1 grab");
            Near(new Vec2(1, 1), s1.Drop, Tol, "s1 drop");
            Near(new Vec2(0, -1), s1.LineA, Tol, "s1 A");
            Near(new Vec2(0, 1), s1.LineB, Tol, "s1 B");
            var s2 = PlaneGuide.Steps[1];
            Assert.AreEqual(FoldStepKind.Fold, s2.Kind);
            Near(new Vec2(-1, 1), s2.Grab, Tol, "s2 grab");
            Near(new Vec2(0, 0), s2.Drop, Tol, "s2 drop");
            for (int i = 1; i < 6; i++) Assert.AreEqual(FoldStepKind.Fold, PlaneGuide.Steps[i].Kind);
        }

        [Test]
        public void Steps_AreSharedReadOnly()
        {
            Assert.AreSame(PlaneGuide.Steps, PlaneGuide.Steps);
            Assert.AreSame(PlaneGuide.Steps[2], PlaneGuide.Steps[2]);
            var list = (System.Collections.Generic.IList<GuideStep>)PlaneGuide.Steps;
            Assert.IsTrue(list.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => list[0] = null);
        }
    }
}
