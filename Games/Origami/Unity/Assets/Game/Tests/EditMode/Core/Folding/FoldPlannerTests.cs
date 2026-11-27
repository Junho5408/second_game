using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using static Origami.Tests.Folding.FoldTestUtil;

namespace Origami.Tests.Folding
{
    // C-103 FoldPlanner, F-005 AC-01/03/04/06/10..15 at engine level (AC-06, AC-11..15 owned by ORIGAMI-009;
    // AC-01..05/10 are owned by ORIGAMI-010 as game commands — these are the supporting engine tests).
    // Detailed expected values (layer lists, exact areas) come from balance_check.py plan/fold/crease on the same inputs.
    public class FoldPlannerTests
    {
        private static readonly Vec2 TulipTip4 = V(0.689655, -0.275862);

        private static PaperState Apply(params FoldStep[] steps) => FoldPlanner.ApplyAll(steps);

        /// <summary>Tulip steps 1–3 = diamond base (CONTENT §2).</summary>
        internal static PaperState Diamond() =>
            Apply(FoldStep.Fold(V(1, 1), C), FoldStep.Fold(V(-1, 1), C), FoldStep.Fold(V(1, -1), C));

        private static void AssertSameFacets(PaperState expected, PaperState actual)
        {
            Assert.AreEqual(expected.Facets.Count, actual.Facets.Count, "facet count");
            for (int i = 0; i < expected.Facets.Count; i++)
            {
                var e = expected.Facets[i];
                var a = actual.Facets[i];
                Assert.AreEqual(e.Layer, a.Layer, "layer " + i);
                Assert.AreEqual(e.FrontUp, a.FrontUp, "front " + i);
                CollectionAssert.AreEqual(e.World, a.World, "world " + i);
                CollectionAssert.AreEqual(e.Paper, a.Paper, "paper " + i);
            }
        }

        [Test]
        public void F005_AC01_HalfFoldGivesLowerRectangle()
        {
            var plan = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, -1), false, 0);
            Assert.IsTrue(plan.Valid);
            Assert.AreEqual(FoldError.None, plan.Error);
            var s = plan.Result;
            Assert.AreEqual(2, s.Facets.Count);
            Assert.That(s.TotalArea(), Is.EqualTo(4.0).Within(Eps));
            Assert.That(OutlineArea(s), Is.EqualTo(2.0).Within(Eps));
            foreach (var f in s.Facets)
            foreach (var q in f.World)
            {
                Assert.That(q.X, Is.InRange(-1 - Eps, 1 + Eps));
                Assert.That(q.Y, Is.InRange(-1 - Eps, 0 + Eps));
            }
            // D-101 example: staying lower half layer 0 (white up), moved upper half layer 1 (front up).
            Assert.AreEqual(0, s.Facets[0].Layer);
            Assert.IsFalse(s.Facets[0].FrontUp);
            Assert.AreEqual(1, s.Facets[1].Layer);
            Assert.IsTrue(s.Facets[1].FrontUp);
            Assert.AreSame(s.Facets[1], plan.Moved[0]);
            Assert.AreEqual(1, plan.Moving.Count);
            Assert.AreEqual(1, plan.Static.Count);
            Assert.IsFalse(plan.Partial);
            Assert.IsFalse(plan.IsCrease);
            Assert.AreEqual(0, plan.NewCreases.Count);
        }

        [Test]
        public void F005_AC03_InvalidFoldsAreRejectedWithoutResult()
        {
            var shortDrag = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, 0.98), false, 0);
            Assert.IsFalse(shortDrag.Valid);
            Assert.AreEqual(FoldError.DragTooShort, shortDrag.Error);
            Assert.IsNull(shortDrag.Result);
            Assert.AreEqual(0, shortDrag.Moving.Count + shortDrag.Static.Count + shortDrag.Moved.Count + shortDrag.NewCreases.Count);

            var outside = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-3, 3), false, 0);
            Assert.IsFalse(outside.Valid);
            Assert.AreEqual(FoldError.LineOutsidePaper, outside.Error);
            Assert.IsNull(outside.Result);

            var negative = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(0, 0), false, -1);
            Assert.IsFalse(negative.Valid);
            Assert.AreEqual(FoldError.InvalidLayers, negative.Error);

            // layers < 0 is checked before the drag length (C-103 step 0).
            Assert.AreEqual(FoldError.InvalidLayers, FoldPlanner.PlanCrease(PaperState.Initial, V(0, 0), V(0, 0), -2).Error);
        }

        [Test]
        public void MinDragBoundary_IsInclusive()
        {
            // Distance < MinDrag rejects; exactly MinDrag (0.03) is allowed (Python `<`).
            Assert.AreEqual(FoldError.DragTooShort, FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, 0.9701), false, 0).Error);
            Assert.IsTrue(FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, 0.96), false, 0).Valid);
        }

        [Test]
        public void F005_AC04_FlipKeepsOutlineAndReversesLayers()
        {
            var folded = Apply(FoldStep.Fold(V(-1, 1), V(-1, -1)));
            var flipped = FoldPlanner.Flip(folded);
            Assert.That(OutlineArea(flipped), Is.EqualTo(2.0).Within(Eps));
            Assert.AreEqual(1, flipped.Facets[0].Layer);
            Assert.IsTrue(flipped.Facets[0].FrontUp);
            Assert.AreEqual(0, flipped.Facets[1].Layer);
            Assert.IsFalse(flipped.Facets[1].FrontUp);
            Assert.That(flipped.Facets[0].SignedArea(), Is.GreaterThan(0), "flip keeps CCW");
            // flipping twice restores the exact coordinates
            AssertSameFacets(folded, FoldPlanner.Flip(flipped));
        }

        [Test]
        public void Flip_MirrorsAroundOutlineCentreAndCarriesCreases()
        {
            var s = Apply(FoldStep.Fold(V(1, 1), V(0, 1)), FoldStep.Crease(V(-1, 1), V(-1, -1)));   // outline x ∈ [−1, 0.5]
            Assert.That(s.FlipAxisX(), Is.EqualTo(-0.25).Within(1e-12));
            var f = FoldPlanner.Flip(s);
            CollectionAssert.AreEqual(s.Creases, f.Creases, "creases are paper coordinates");
            double minX = f.Facets.SelectMany(x => x.World).Min(q => q.X);
            double maxX = f.Facets.SelectMany(x => x.World).Max(q => q.X);
            Assert.That(minX, Is.EqualTo(-1).Within(Eps));
            Assert.That(maxX, Is.EqualTo(0.5).Within(Eps));
        }

        [Test]
        public void F005_AC06_MountainAndValleyShareOutlineButNotLayerOrder()
        {
            var valley = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, -1), false, 0).Result;
            var mountain = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, -1), true, 0).Result;
            Assert.AreEqual(valley.Facets.Count, mountain.Facets.Count);
            for (int i = 0; i < valley.Facets.Count; i++)
                CollectionAssert.AreEqual(valley.Facets[i].World, mountain.Facets[i].World, "same outline " + i);
            // moved facet is the last one: valley = top, mountain = bottom
            Assert.AreEqual(valley.MaxLayer(), valley.Facets[1].Layer);
            Assert.AreEqual(0, mountain.Facets[1].Layer);
            Assert.AreEqual(1, mountain.Facets[0].Layer);

            // same on a multi-layer state (diamond, all layers)
            var d = Diamond();
            var v2 = FoldPlanner.PlanFold(d, C, TulipTip4, false, 0);
            var m2 = FoldPlanner.PlanFold(d, C, TulipTip4, true, 0);
            Assert.That(OutlineArea(m2.Result), Is.EqualTo(OutlineArea(v2.Result)).Within(Eps));
            int maxLayer = v2.Result.MaxLayer();
            foreach (var f in v2.Moved) Assert.That(f.Layer, Is.GreaterThan(maxLayer - v2.Moved.Count));
            foreach (var f in m2.Moved) Assert.That(f.Layer, Is.LessThan(m2.Moved.Count));
        }

        [Test]
        public void F005_AC10_CreaseLeavesMarkAndKeepsShape()
        {
            var plan = FoldPlanner.PlanCrease(PaperState.Initial, V(-1, 1), V(1, 1), 0);
            Assert.IsTrue(plan.Valid);
            Assert.IsTrue(plan.IsCrease);
            var s = plan.Result;
            Assert.AreEqual(1, s.Facets.Count);
            Assert.AreSame(PaperState.Initial.Facets[0], s.Facets[0], "crease keeps the facet objects");
            Assert.That(OutlineArea(s), Is.EqualTo(4.0).Within(Eps));
            Assert.AreEqual(1, s.Creases.Count);
            AreClose(V(0, -1), s.Creases[0].A, Eps);
            AreClose(V(0, 1), s.Creases[0].B, Eps);
            Assert.That(s.Creases[0].Length, Is.EqualTo(2.0).Within(Eps));
            Assert.AreEqual(0, plan.Moved.Count);
            CollectionAssert.AreEqual(s.Creases, plan.NewCreases);
            Assert.AreEqual(0, PaperState.Initial.Creases.Count, "input state unchanged");
        }

        [Test]
        public void F005_AC11_CreaseThroughTwoLayersLeavesTwoSegments()
        {
            var folded = Apply(FoldStep.Fold(V(-1, 1), V(-1, -1)));
            var s = FoldPlanner.PlanCrease(folded, V(-1, 0), V(1, 0), 0).Result;
            Assert.AreEqual(2, s.Facets.Count, "crease does not split facets");
            Assert.AreEqual(2, s.Creases.Count);
            // balance_check.py crease(): ((0,-1),(0,0)), ((0,1),(0,0))
            AreClose(V(0, -1), s.Creases[0].A, Eps);
            AreClose(V(0, 0), s.Creases[0].B, Eps);
            AreClose(V(0, 1), s.Creases[1].A, Eps);
            AreClose(V(0, 0), s.Creases[1].B, Eps);
            Assert.That(s.Creases.Sum(c => c.Length), Is.EqualTo(2.0).Within(Eps));
        }

        [Test]
        public void F005_AC12_TopTwoLayersTulipPetal()
        {
            var d = Diamond();
            Assert.AreEqual(6, d.Facets.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5 }, d.Facets.Select(f => f.Layer));
            CollectionAssert.AreEqual(new[] { 0.5, 0.5, 0.5, 0.5, 1.0, 1.0 },
                d.Facets.Select(f => Math.Round(f.Area, 9)).OrderBy(a => a));
            Assert.That(OutlineArea(d), Is.EqualTo(1.0).Within(Eps));

            var top2 = FoldPlanner.PlanFold(d, C, TulipTip4, false, 2);
            Assert.IsTrue(top2.Valid);
            Assert.AreEqual(2, top2.Moving.Count);
            Assert.IsTrue(top2.Partial);
            Assert.That(SumArea(top2.Moving), Is.EqualTo(0.70).Within(0.005));
            Assert.That(SumArea(top2.Moving), Is.EqualTo(0.6999999014285826).Within(Eps));   // balance_check plan()
            Assert.AreEqual(8, top2.Result.Facets.Count);
            Assert.That(OutlineArea(top2.Result), Is.EqualTo(1.244).Within(0.01));

            var all = FoldPlanner.PlanFold(d, C, TulipTip4, false, 0);
            Assert.AreEqual(6, all.Moving.Count);
            Assert.IsFalse(all.Partial);
            Assert.That(SumArea(all.Moving), Is.EqualTo(3.143).Within(0.0005));
            Assert.That(SumArea(all.Moving), Is.EqualTo(3.142856810495633).Within(Eps));
            Assert.AreEqual(12, all.Result.Facets.Count);
            Assert.That(OutlineArea(all.Result), Is.EqualTo(0.786).Within(0.01));
        }

        [Test]
        public void F005_AC13_HingedLayersFoldTogether()
        {
            var triangle = Apply(FoldStep.Fold(V(1, 1), C));
            var top1 = FoldPlanner.PlanFold(triangle, V(-1, 1), V(-1.4, 0.0), false, 1);
            var all = FoldPlanner.PlanFold(triangle, V(-1, 1), V(-1.4, 0.0), false, 0);
            Assert.IsTrue(top1.Valid);
            Assert.AreEqual(2, top1.Moving.Count, "the diagonal fold edge hinges both layers");
            Assert.IsFalse(top1.Partial);
            AssertSameFacets(all.Result, top1.Result);
            Assert.That(OutlineArea(top1.Result), Is.EqualTo(1.804).Within(0.01));
        }

        [Test]
        public void F005_AC14_LayersAboveStackEqualAllLayers()
        {
            var d = Diamond();
            Assert.AreEqual(6, d.StackCountAt(C));
            AssertSameFacets(FoldPlanner.PlanFold(d, C, TulipTip4, false, 0).Result,
                FoldPlanner.PlanFold(d, C, TulipTip4, false, 9).Result);
            AssertSameFacets(FoldPlanner.PlanFold(d, C, TulipTip4, false, 0).Result,
                FoldPlanner.PlanFold(d, C, TulipTip4, false, 6).Result);
        }

        [Test]
        public void F005_AC15_MountainTopTwoInsertsBelowMovedLayers()
        {
            var d = Diamond();
            var plan = FoldPlanner.PlanFold(d, C, TulipTip4, true, 2);
            Assert.IsTrue(plan.Valid);
            Assert.IsTrue(plan.Partial);
            var s = plan.Result;
            // balance_check.py fold(diamond, C, tip, True, 2) layers: static [0,1,3,2,7,6] + moved [4,5]
            CollectionAssert.AreEqual(new[] { 0, 1, 3, 2, 7, 6, 4, 5 }, s.Facets.Select(f => f.Layer));
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, plan.Moved.Select(f => f.Layer), "moved flap");
            // the staying parts of the moved flap (pre-fold layers 4, 5) are now 6, 7
            var movedOriginalLayers = plan.Moving.Select(f => f.Layer).ToList();
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, movedOriginalLayers);
            var sameFlapRest = s.Facets.Take(plan.Static.Count).Where((f, i) => movedOriginalLayers.Contains(plan.Static[i].Layer));
            CollectionAssert.AreEquivalent(new[] { 6, 7 }, sameFlapRest.Select(f => f.Layer));
            // other flap 2, 3 and bottom 0, 1 unchanged
            var others = s.Facets.Take(plan.Static.Count).Where((f, i) => !movedOriginalLayers.Contains(plan.Static[i].Layer));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, others.Select(f => f.Layer));

            // valley top-2 for comparison: moved on top (6, 7)
            var valley = FoldPlanner.PlanFold(d, C, TulipTip4, false, 2).Result;
            CollectionAssert.AreEqual(new[] { 0, 1, 3, 2, 5, 4, 6, 7 }, valley.Facets.Select(f => f.Layer));
        }

        [Test]
        public void TopLayers_NothingGrabbedAndNothingToFold()
        {
            // J-15 path: cicada 1–5 then wing with all layers takes every layer at C (CONTENT §4 J-15).
            var s = Apply(FoldStep.Fold(V(1, 1), C), FoldStep.Fold(V(-1, 1), C), FoldStep.Fold(V(1, -1), C),
                FoldStep.Crease(V(-1, 0), V(0, -1)), FoldStep.Crease(V(-1, 0), V(0, 0)),
                FoldStep.Fold(C, V(0.453858, -0.108926)));
            var step7 = FoldPlanner.PlanFold(s, C, V(-0.108926, 0.453858), false, 2);
            Assert.AreEqual(FoldError.NothingGrabbed, step7.Error);
            var step8 = FoldPlanner.PlanFold(s, C, V(-0.38, -0.38), false, 0);
            Assert.AreEqual(FoldError.NothingToFold, step8.Error);
            // a grab off the paper with layers ≥ 1 is NothingGrabbed; with layers 0 the line decides
            Assert.AreEqual(FoldError.NothingGrabbed, FoldPlanner.PlanFold(PaperState.Initial, V(-1.5, 1.5), V(0, 0), false, 1).Error);
        }

        [Test]
        public void Planner_DoesNotChangeInputState()
        {
            var d = Diamond();
            var before = d.Facets.ToList();
            FoldPlanner.PlanFold(d, C, TulipTip4, true, 2);
            FoldPlanner.PlanCrease(d, C, TulipTip4, 2);
            FoldPlanner.Flip(d);
            CollectionAssert.AreEqual(before, d.Facets);
        }

        [Test]
        public void Apply_InvalidStepThrows_ValidStepsMatchPlans()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FoldPlanner.Apply(PaperState.Initial, FoldStep.Fold(V(-1, 1), V(-1, 0.99))));
            StringAssert.Contains("DragTooShort", ex.Message);
            Assert.Throws<InvalidOperationException>(() => FoldPlanner.Apply(PaperState.Initial, FoldStep.Crease(V(-1, 1), V(-3, 3))));
            Assert.AreSame(PaperState.Initial, FoldPlanner.ApplyAll(new FoldStep[0]));
            var start = Apply(FoldStep.Fold(V(-1, 1), V(-1, -1)));
            var s = FoldPlanner.ApplyAll(new[] { FoldStep.Flip() }, start);
            AssertSameFacets(FoldPlanner.Flip(start), s);
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => FoldPlanner.PlanFold(null, V(0, 0), V(1, 1), false, 0));
            Assert.Throws<ArgumentNullException>(() => FoldPlanner.PlanCrease(null, V(0, 0), V(1, 1), 0));
            Assert.Throws<ArgumentNullException>(() => FoldPlanner.Flip(null));
            Assert.Throws<ArgumentNullException>(() => FoldPlanner.Apply(null, FoldStep.Flip()));
            Assert.Throws<ArgumentNullException>(() => FoldPlanner.Apply(PaperState.Initial, null));
            Assert.Throws<ArgumentNullException>(() => FoldPlanner.ApplyAll(null));
            Assert.Throws<ArgumentNullException>(() => FoldPlanner.ApplyAll(new FoldStep[] { null }));
        }

        [Test]
        public void Constants_MatchGameRules()
        {
            Assert.AreEqual(0.03, FoldPlanner.MinDrag);
            Assert.AreEqual(0.02, FoldPlanner.HingeMin);
            Assert.AreEqual(1e-9, FoldPlanner.SideEps);
            Assert.AreEqual(1e-7, FoldPlanner.MinArea);
            Assert.AreEqual(1e-6, FoldPlanner.ContainsEps);
        }

        [Test]
        public void FoldStep_Factories()
        {
            var f = FoldStep.Fold(V(1, 2), V(3, 4), true, 2);
            Assert.AreEqual(FoldStepKind.Fold, f.Kind);
            Assert.AreEqual(V(1, 2), f.Grab);
            Assert.AreEqual(V(3, 4), f.Drop);
            Assert.IsTrue(f.Mountain);
            Assert.AreEqual(2, f.Layers);
            var c = FoldStep.Crease(V(1, 2), V(3, 4), 1);
            Assert.AreEqual(FoldStepKind.Crease, c.Kind);
            Assert.IsFalse(c.Mountain);
            Assert.AreEqual(1, c.Layers);
            var x = FoldStep.Flip();
            Assert.AreEqual(FoldStepKind.Flip, x.Kind);
            Assert.AreEqual(V(0, 0), x.Grab);
            Assert.AreEqual(V(0, 0), x.Drop);
            Assert.AreEqual(0, x.Layers);
            var d = FoldStep.Fold(V(0, 0), V(1, 0));
            Assert.IsFalse(d.Mountain);
            Assert.AreEqual(0, d.Layers);
        }

        [Test]
        public void FoldLine_SideReflectDistance()
        {
            var L = FoldLine.FromGrabDrop(V(-1, 1), V(-1, -1));
            AreClose(V(-1, 0), L.Point, 1e-15);
            AreClose(V(0, -1), L.Normal, 1e-15);
            AreClose(V(1, 0), L.Direction, 1e-15);
            Assert.That(L.Side(V(-1, 1)), Is.LessThan(0), "grab side is negative");
            Assert.That(L.Side(V(0, -0.5)), Is.GreaterThan(0));
            Assert.AreEqual(0.0, L.Side(V(0.3, 1e-10)), "|side| < 1e-9 → 0");
            Assert.AreNotEqual(0.0, L.RawSide(V(0.3, 1e-10)));
            AreClose(V(0.5, -0.7), L.Reflect(V(0.5, 0.7)), 1e-15);
            Assert.That(L.Distance(V(3, 0.25)), Is.EqualTo(0.25).Within(1e-15));
            var n = new FoldLine(V(0, 0), V(3, 4));
            AreClose(V(0.6, 0.8), n.Normal, 1e-15);
        }
    }
}
