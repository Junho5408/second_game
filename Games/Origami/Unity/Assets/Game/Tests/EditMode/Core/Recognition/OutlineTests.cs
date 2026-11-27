using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Core.Recognition;

namespace Origami.Tests.Recognition
{
    // C-110 Outline (row scan, A2-3) and Similarity.
    public class OutlineTests
    {
        private static Vec2 V(double x, double y) => new Vec2(x, y);

        private static Outline Of(params Vec2[][] polys) => Outline.FromPolygons(polys);

        [Test]
        public void Square_AreaAndCentroid()
        {
            var o = Of(new[] { V(0, 0), V(1, 0), V(1, 1), V(0, 1) });
            Assert.That(o.Area, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(o.Centroid.X, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(o.Centroid.Y, Is.EqualTo(0.5).Within(1e-9));
            Assert.AreEqual(0.001, Outline.RowStep);
        }

        [Test]
        public void Triangle_IsExactUnderMidpointRows_AnyOrientation()
        {
            var ccw = Of(new[] { V(0, 0), V(1, 0), V(0, 1) });
            var cw = Of(new[] { V(0, 1), V(1, 0), V(0, 0) });
            Assert.That(ccw.Area, Is.EqualTo(0.5).Within(1e-9));
            Assert.AreEqual(ccw.Area, cw.Area);
            Assert.That(ccw.Centroid.X, Is.EqualTo(1.0 / 3).Within(1e-6));
            Assert.That(ccw.Centroid.Y, Is.EqualTo(1.0 / 3).Within(1e-6));
        }

        [Test]
        public void Union_MergesOverlaps_IntersectionCountsSharedRows()
        {
            var a = new[] { V(0, 0), V(2, 0), V(2, 1), V(0, 1) };
            var b = new[] { V(1, 0), V(3, 0), V(3, 1), V(1, 1) };
            var u = Of(a, b);
            Assert.That(u.Area, Is.EqualTo(3.0).Within(1e-9), "union, not sum");
            Assert.That(u.Centroid.X, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(Outline.IntersectionArea(Of(a), Of(b)), Is.EqualTo(1.0).Within(1e-9));
            var far = Of(new[] { V(10, 10), V(11, 10), V(11, 11), V(10, 11) });
            Assert.AreEqual(0.0, Outline.IntersectionArea(Of(a), far));
            var gap = Of(a, new[] { V(5, 0), V(6, 0), V(6, 1), V(5, 1) });
            Assert.That(gap.Area, Is.EqualTo(3.0).Within(1e-9), "disjoint intervals in one row");
            Assert.That(Outline.IntersectionArea(gap, u), Is.EqualTo(2.0).Within(1e-9));
        }

        [Test]
        public void Empty_AndArgumentChecks()
        {
            var empty = Outline.FromPolygons(new List<IReadOnlyList<Vec2>>());
            Assert.AreEqual(0.0, empty.Area);
            Assert.AreEqual(V(0, 0), empty.Centroid);
            Assert.AreEqual(0.0, Outline.IntersectionArea(empty, empty));
            Assert.Throws<ArgumentNullException>(() => Outline.FromPolygons(null));
            Assert.Throws<ArgumentException>(() => Outline.FromPolygons(new IReadOnlyList<Vec2>[] { null }));
            Assert.Throws<ArgumentNullException>(() => Outline.FromState(null));
            Assert.Throws<ArgumentNullException>(() => Outline.IntersectionArea(null, empty));
            Assert.Throws<ArgumentNullException>(() => Outline.IntersectionArea(empty, null));
        }

        [Test]
        public void FromState_UsesAllFacets()
        {
            Assert.That(Outline.FromState(PaperState.Initial).Area, Is.EqualTo(4.0).Within(1e-9));
            var half = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, -1), false, 0).Result;
            var o = Outline.FromState(half);
            Assert.That(o.Area, Is.EqualTo(2.0).Within(1e-9));
            Assert.That(o.Centroid.Y, Is.EqualTo(-0.5).Within(1e-9));
        }

        [Test]
        public void Similarity_IsInvariantUnderD4AndTranslation()
        {
            var half = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, -1), false, 0).Result;
            Assert.That(Similarity.Centered(half, half), Is.EqualTo(1.0).Within(1e-9));
            var rotated = FoldPlanner.PlanFold(PaperState.Initial, V(1, 1), V(-1, 1), false, 0).Result;   // vertical half
            Assert.That(Similarity.Centered(half, rotated), Is.EqualTo(1.0).Within(1e-6));
            Assert.That(Similarity.Centered(PaperState.Initial, half), Is.EqualTo(0.5).Within(1e-6));
            Assert.Throws<ArgumentNullException>(() => Similarity.Centered(null, half));
            Assert.Throws<ArgumentNullException>(() => Similarity.Centered(half, null));
        }
    }
}
