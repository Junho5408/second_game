using System;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using static Origami.Tests.Folding.FoldTestUtil;

namespace Origami.Tests.Folding
{
    // C-106 DropSnap, R-021, F-005 AC-16 (owned by ORIGAMI-009; the "reference inputs keep their line" clause is in
    // Catalog/ModelCatalogTests because it needs the reference sequences).
    public class DropSnapTests
    {
        private static readonly double K = 1 - Math.Sqrt(2);

        private static PaperState Plane(int steps)
        {
            var all = new[]
            {
                FoldStep.Crease(V(-1, 1), V(1, 1)), FoldStep.Fold(V(-1, 1), V(0, 0)), FoldStep.Fold(V(1, 1), V(0, 0)),
                FoldStep.Fold(V(-1, 0), V(0, K)),
            };
            return FoldPlanner.ApplyAll(all.Take(steps));
        }

        private static PaperState House(int steps)
        {
            var all = new[]
            {
                FoldStep.Fold(V(-1, 1), V(-1, -1)), FoldStep.Crease(V(-1, 0), V(1, 0)), FoldStep.Crease(V(-1, -1), V(-1, 0)),
            };
            return FoldPlanner.ApplyAll(all.Take(steps));
        }

        [Test]
        public void F005_AC16_MarkMagnetAfterPlaneStep1()
        {
            var r = DropSnap.Snap(Plane(1), V(-1, 1), V(0.03, 0.2));
            Assert.AreEqual(SnapKind.Mark, r.Kind);
            AreClose(V(0, 0.2), r.Point, Eps);
            Assert.AreEqual(2, r.AnchorCount);
            AreClose(V(0, -1), r.AnchorA, Eps);
            AreClose(V(0, 1), r.AnchorB, Eps);
        }

        [Test]
        public void F005_AC16_PointMagnetAfterPlaneSteps1And2()
        {
            var r = DropSnap.Snap(Plane(2), V(1, 1), V(0.04, -0.03));
            Assert.AreEqual(SnapKind.Point, r.Kind);
            AreClose(V(0, 0), r.Point, Eps);
            Assert.AreEqual(1, r.AnchorCount);
            Assert.AreEqual(r.Point, r.AnchorA);
        }

        [Test]
        public void F005_AC16_LineMagnetAfterHouseSteps1To3()
        {
            var r = DropSnap.Snap(House(3), V(-1, 0), V(-0.62, -0.78));
            Assert.AreEqual(SnapKind.Line, r.Kind);
            AreClose(V(-0.6, -0.8), r.Point, Eps);
            Assert.AreEqual(2, r.AnchorCount);
            // the roof line passes the left horizontal-mark end and the top vertical-mark end, sorted by (x, y)
            AreClose(V(-1, -0.5), r.AnchorA, Eps);
            AreClose(V(0, 0), r.AnchorB, Eps);
        }

        [Test]
        public void F005_AC16_NoMagnetAfterHouseStep1Only()
        {
            var r = DropSnap.Snap(House(1), V(-1, 0), V(-0.62, -0.78));
            Assert.AreEqual(SnapKind.None, r.Kind);
            Assert.AreEqual(V(-0.62, -0.78), r.Point);
            Assert.AreEqual(0, r.AnchorCount);
        }

        [Test]
        public void ZeroDrag_IsFreeUnlessAPointIsNear()
        {
            var r = DropSnap.Snap(PaperState.Initial, V(0.3, 0.3), V(0.3, 0.3));
            Assert.AreEqual(SnapKind.None, r.Kind);
            Assert.AreEqual(V(0.3, 0.3), r.Point);
            // the grabbed point itself is never a landmark target
            var g = DropSnap.Snap(PaperState.Initial, V(1, 1), V(1.01, 0.99));
            Assert.AreNotEqual(SnapKind.Point, g.Kind);
        }

        [Test]
        public void Guide_TargetMagnetWinsWithin008()
        {
            var st = Plane(1);
            var guide = V(0, 0);
            var r = DropSnap.Snap(st, V(-1, 1), V(0.05, 0.06), guide);
            Assert.AreEqual(SnapKind.Guide, r.Kind);
            Assert.AreEqual(guide, r.Point);
            Assert.AreEqual(1, r.AnchorCount);
            Assert.AreEqual(guide, r.AnchorA);
            // outside 0.08 the R-021 magnets apply as usual
            var far = DropSnap.Snap(st, V(-1, 1), V(0.03, 0.2), guide);
            Assert.AreEqual(SnapKind.Mark, far.Kind);
            Assert.AreEqual(DropSnap.Snap(st, V(-1, 1), V(0.03, 0.2)).Point, far.Point);
        }

        [Test]
        public void R021_TieBreakIsOrderIndependent()
        {
            // two landmarks at the same distance from the drop: the smaller x wins whatever the facet order
            var a = new Facet(new[] { V(-1, -1), V(0, 0), V(-1, 0) }, new[] { V(-1, -1), V(0, 0), V(-1, 0) }, 0, false);
            var b = new Facet(new[] { V(0.1, 0), V(1, -1), V(1, 0) }, new[] { V(0.1, 0), V(1, -1), V(1, 0) }, 1, false);
            var s1 = new PaperState(new[] { a, b }, null);
            var s2 = new PaperState(new[] { b, a }, null);
            var r1 = DropSnap.Snap(s1, V(-1, -1), V(0.05, 0));
            var r2 = DropSnap.Snap(s2, V(-1, -1), V(0.05, 0));
            Assert.AreEqual(SnapKind.Point, r1.Kind);
            Assert.AreEqual(V(0, 0), r1.Point);
            Assert.AreEqual(r1.Point, r2.Point);
            Assert.AreEqual(r1.Kind, r2.Kind);
        }

        [Test]
        public void Visibility_HiddenMarkCrossingIsNotALandmark()
        {
            var square = new[] { V(-1, -1), V(1, -1), V(1, 1), V(-1, 1) };
            var patchW = new[] { V(-0.1, -0.1), V(0.1, -0.1), V(0.1, 0.1), V(-0.1, 0.1) };
            var patchP = new[] { V(2, 2), V(2.2, 2), V(2.2, 2.2), V(2, 2.2) };   // paper far away: no marks on the patch
            var s = new PaperState(new[] { new Facet(square, square, 0, false), new Facet(patchW, patchP, 1, true) },
                new[] { new CreaseSegment(V(-1, 0), V(1, 0)), new CreaseSegment(V(0, -1), V(0, 1)) });
            Assert.IsFalse(DropSnap.IsVisible(s, V(0, 0), 0));
            Assert.IsTrue(DropSnap.IsVisible(s, V(0, 0), 1));
            Assert.IsTrue(DropSnap.IsVisible(s, V(0.5, 0), 0));
            Assert.IsTrue(DropSnap.IsVisible(s, V(0.1, 0), 0), "patch border is not strictly inside");
            var marks = DropSnap.MarkPieces(s);
            Assert.AreEqual(2, marks.Count);
            Assert.IsTrue(marks.All(m => m.FacetIndex == 0));
            var lms = DropSnap.Landmarks(s);
            Assert.IsFalse(lms.Any(q => Vec2.Distance(q, V(0, 0)) < 1e-6), "hidden crossing");
            Assert.IsTrue(lms.Any(q => Vec2.Distance(q, V(1, 0)) < 1e-6), "visible mark end");
            Assert.AreEqual(4 + 4 + 4, lms.Count, "8 vertices + 4 mark ends (crossing hidden)");

            // without the patch the crossing is a landmark
            var open = new PaperState(new[] { new Facet(square, square, 0, false) }, s.Creases);
            Assert.IsTrue(DropSnap.Landmarks(open).Any(q => Vec2.Distance(q, V(0, 0)) < 1e-6));
        }

        [Test]
        public void Landmarks_DropDuplicates_FirstKept()
        {
            var lms = DropSnap.Landmarks(House(1));
            for (int i = 0; i < lms.Count; i++)
            for (int j = i + 1; j < lms.Count; j++)
                Assert.That(Vec2.Distance(lms[i], lms[j]), Is.GreaterThan(1e-6));
            Assert.AreEqual(4, lms.Count, "half fold: both layers share the 4 rectangle corners");
        }

        [Test]
        public void Key_RoundsTo9Decimals()
        {
            Assert.AreEqual(1.0, DropSnap.Key(1.0000000004));
            Assert.AreEqual(1.000000001, DropSnap.Key(1.0000000006));
            Assert.AreEqual(-0.123456789, DropSnap.Key(-0.1234567891));
        }

        [Test]
        public void Constants_MatchGameRules()
        {
            Assert.AreEqual(0.06, DropSnap.SnapPoint);
            Assert.AreEqual(0.04, DropSnap.SnapLine);
            Assert.AreEqual(0.04, DropSnap.SnapMark);
            Assert.AreEqual(0.2, DropSnap.LinePairMin);
            Assert.AreEqual(0.08, DropSnap.GuideSnap);
        }

        [Test]
        public void ArgumentChecks()
        {
            Assert.Throws<ArgumentNullException>(() => DropSnap.Snap(null, V(0, 0), V(1, 1)));
            Assert.Throws<ArgumentNullException>(() => DropSnap.Snap(null, V(0, 0), V(1, 1), V(0, 0)));
            Assert.Throws<ArgumentNullException>(() => DropSnap.Landmarks(null));
            Assert.Throws<ArgumentNullException>(() => DropSnap.MarkPieces(null));
            Assert.Throws<ArgumentNullException>(() => DropSnap.IsVisible(null, V(0, 0), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DropSnap.IsVisible(PaperState.Initial, V(0, 0), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => DropSnap.IsVisible(PaperState.Initial, V(0, 0), -1));
        }

        [Test]
        public void Snap_IsDeterministic()
        {
            var st = House(3);
            var first = DropSnap.Snap(st, V(-1, 0), V(-0.62, -0.78));
            for (int i = 0; i < 50; i++)
            {
                var r = DropSnap.Snap(st, V(-1, 0), V(-0.62, -0.78));
                Assert.AreEqual(first.Point, r.Point);
                Assert.AreEqual(first.Kind, r.Kind);
                Assert.AreEqual(first.AnchorA, r.AnchorA);
                Assert.AreEqual(first.AnchorB, r.AnchorB);
            }
        }
    }
}
