using System;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using static Origami.Tests.Folding.FoldTestUtil;

namespace Origami.Tests.Folding
{
    // C-105 LineOnPaper (CONTENT §2 "선" for all reference steps is checked in Catalog/ModelCatalogTests).
    public class LineOnPaperTests
    {
        [Test]
        public void VerticalCentreLineOnInitialSheet()
        {
            Assert.IsTrue(LineOnPaper.TryGet(PaperState.Initial, V(-1, 1), V(1, 1), out var a, out var b));
            AreClose(V(0, -1), a, 1e-12);
            AreClose(V(0, 1), b, 1e-12);
        }

        [Test]
        public void LineSpansAllFacets()
        {
            // after a half fold the vertical line runs over the lower rectangle only
            var s = FoldPlanner.ApplyAll(new[] { FoldStep.Fold(V(-1, 1), V(-1, -1)) });
            Assert.IsTrue(LineOnPaper.TryGet(s, V(-1, 0), V(1, 0), out var a, out var b));
            AreClose(V(0, -1), a, 1e-12);
            AreClose(V(0, 0), b, 1e-12);
        }

        [Test]
        public void LineOutsidePaper_ReturnsFalse()
        {
            Assert.IsFalse(LineOnPaper.TryGet(PaperState.Initial, V(-1, 1), V(-3, 3), out _, out _));
        }

        [Test]
        public void QA2_ZeroLengthDrag_ReturnsFalse()
        {
            // ORIGAMI-008 QA-2: grab == drop has no fold line (FromGrabDrop normal (0,0)) → rejected, not a point.
            Assert.IsFalse(LineOnPaper.TryGet(PaperState.Initial, V(0.2, 0.3), V(0.2, 0.3), out var a, out var b));
            Assert.AreEqual(default(Vec2), a);
            Assert.AreEqual(default(Vec2), b);
            Assert.IsFalse(LineOnPaper.TryGet(PaperState.Initial, V(0.2, 0.3), V(0.2, 0.3 + 5e-10), out _, out _));
            Assert.AreEqual(V(0, 0), FoldLine.FromGrabDrop(V(0.2, 0.3), V(0.2, 0.3)).Normal);
            // the planner rejects the same input as DragTooShort
            Assert.AreEqual(FoldError.DragTooShort, FoldPlanner.PlanFold(PaperState.Initial, V(0.2, 0.3), V(0.2, 0.3), false, 0).Error);
        }

        [Test]
        public void NullState_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LineOnPaper.TryGet(null, V(0, 0), V(1, 0), out _, out _));
        }
    }
}
