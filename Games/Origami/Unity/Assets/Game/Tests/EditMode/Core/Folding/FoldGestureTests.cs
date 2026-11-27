using System;
using NUnit.Framework;
using Origami.Core.Folding;
using static Origami.Tests.Folding.FoldTestUtil;

namespace Origami.Tests.Folding
{
    // C-107 FoldGesture, C-108 FoldTexts — F-005 AC-18 Core values (supporting; AC-18 owned by ORIGAMI-012).
    public class FoldGestureTests
    {
        [Test]
        public void F005_AC18_DiamondGrabTwoNotchesIsTop2Of6()
        {
            var d = FoldPlannerTests.Diamond();
            var g = FoldGesture.TryBegin(d, V(-0.97, -0.98));
            Assert.IsNotNull(g);
            Assert.IsTrue(g.GrabbedCorner);
            AreClose(C, g.Grab, Eps);
            Assert.AreEqual(6, g.StackCount);
            Assert.AreEqual(0, g.Layers);
            Assert.AreEqual("겹: 전체", FoldTexts.LayerLabel(g.Layers, g.StackCount));
            g.CycleLayers(1);
            g.CycleLayers(1);
            Assert.AreEqual(2, g.Layers);
            Assert.AreEqual("겹: 위 2 / 6", FoldTexts.LayerLabel(g.Layers, g.StackCount));

            g.MoveTo(d, V(0.689655, -0.275862));
            var preview = g.Preview(d, false, false);
            Assert.IsTrue(preview.Valid);
            Assert.AreEqual(2, preview.Layers);
            Assert.AreEqual(2, preview.Moving.Count);
            var expected = FoldPlanner.PlanFold(d, g.Grab, g.Drop.Point, false, 2);
            Assert.AreEqual(expected.Result.Facets.Count, preview.Result.Facets.Count);
            for (int i = 0; i < expected.Result.Facets.Count; i++)
                CollectionAssert.AreEqual(expected.Result.Facets[i].World, preview.Result.Facets[i].World);

            var crease = g.Preview(d, false, true);
            Assert.IsTrue(crease.IsCrease);
            Assert.AreEqual(d.Facets.Count, crease.Result.Facets.Count);
        }

        [Test]
        public void CycleLayers_WrapsBothWays()
        {
            var g = FoldGesture.TryBegin(FoldPlannerTests.Diamond(), V(-1, -1));
            g.CycleLayers(6);
            Assert.AreEqual(0, g.Layers, "all → top1 … top5 → all");
            g.CycleLayers(-1);
            Assert.AreEqual(5, g.Layers);
            g.CycleLayers(-13);
            Assert.AreEqual(4, g.Layers);
            g.CycleLayers(3);
            Assert.AreEqual(1, g.Layers);
        }

        [Test]
        public void CycleLayers_SingleLayerStaysAll()
        {
            var g = FoldGesture.TryBegin(PaperState.Initial, V(0.2, 0.2));
            Assert.AreEqual(1, g.StackCount);
            g.CycleLayers(1);
            Assert.AreEqual(0, g.Layers);
            g.CycleLayers(-3);
            Assert.AreEqual(0, g.Layers);
            Assert.AreEqual("겹: 전체", FoldTexts.LayerLabel(g.Layers, g.StackCount));
        }

        [Test]
        public void TryBegin_GrabMagnetAndMiss()
        {
            var corner = FoldGesture.TryBegin(PaperState.Initial, V(-0.9, 0.95));
            Assert.IsTrue(corner.GrabbedCorner);
            Assert.AreEqual(V(-1, 1), corner.Grab);
            Assert.AreEqual(SnapKind.None, corner.Drop.Kind);
            Assert.AreEqual(corner.Grab, corner.Drop.Point);

            var edge = FoldGesture.TryBegin(PaperState.Initial, V(0.5, 1.05));
            Assert.IsFalse(edge.GrabbedCorner);
            AreClose(V(0.5, 1), edge.Grab, 1e-12);

            Assert.IsNull(FoldGesture.TryBegin(PaperState.Initial, V(3, 3)));
            Assert.Throws<ArgumentNullException>(() => FoldGesture.TryBegin(null, V(0, 0)));
            Assert.AreEqual(0.16, FoldGesture.GrabCornerRadius);
            Assert.AreEqual(0.08, FoldGesture.GrabEdgeRadius);
        }

        [Test]
        public void MoveTo_UsesDropSnap_AndGuideVariant()
        {
            var st = FoldPlanner.ApplyAll(new[] { FoldStep.Crease(V(-1, 1), V(1, 1)) });
            var g = FoldGesture.TryBegin(st, V(-1, 1));
            var r = g.MoveTo(st, V(0.03, 0.2));
            Assert.AreEqual(SnapKind.Mark, r.Kind);
            Assert.AreEqual(r.Point, g.Drop.Point);
            var guided = g.MoveTo(st, V(0.03, 0.03), V(0, 0));
            Assert.AreEqual(SnapKind.Guide, guided.Kind);
            Assert.AreEqual(V(0, 0), g.Drop.Point);
            var plan = g.Preview(st, true, false);
            Assert.IsTrue(plan.Valid);
            Assert.IsTrue(plan.Mountain);
            Assert.AreEqual(V(0, 0), plan.Drop);
        }

        [Test]
        public void Preview_ShortDragIsInvalid()
        {
            var g = FoldGesture.TryBegin(PaperState.Initial, V(-1, 1));
            var plan = g.Preview(PaperState.Initial, false, false);
            Assert.IsFalse(plan.Valid);
            Assert.AreEqual(FoldError.DragTooShort, plan.Error);
        }

        [Test]
        public void FoldTexts_Messages()
        {
            Assert.AreEqual("조금 더 끌어 주세요", FoldTexts.ErrorMessage(FoldError.DragTooShort));
            Assert.AreEqual("잡은 곳에 종이가 없어요", FoldTexts.ErrorMessage(FoldError.NothingGrabbed));
            Assert.AreEqual("접히는 부분이 없어요", FoldTexts.ErrorMessage(FoldError.NothingToFold));
            Assert.AreEqual("접는 선이 종이 밖이에요", FoldTexts.ErrorMessage(FoldError.LineOutsidePaper));
            Assert.AreEqual("", FoldTexts.ErrorMessage(FoldError.InvalidLayers));
            Assert.AreEqual("", FoldTexts.ErrorMessage(FoldError.None));
            Assert.AreEqual("겹: 전체", FoldTexts.LayerLabel(0, 6));
            Assert.AreEqual("겹: 위 1 / 3", FoldTexts.LayerLabel(1, 3));
            Assert.AreEqual("접었다 펴기", FoldTexts.CreaseLabel);
        }
    }
}
