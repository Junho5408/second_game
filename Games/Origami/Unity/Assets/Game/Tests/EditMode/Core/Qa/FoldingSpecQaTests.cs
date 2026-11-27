using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Core.Recognition;

namespace Origami.Tests.Qa
{
    // QA ORIGAMI-009: black-box checks of the v2 folding engine against FEATURE_SPECS F-005,
    // CONTENT_SPECS §2 and DATA_STRUCTURE D-103. Expected values are typed from the spec documents (V2SpecBook),
    // not taken from the developer's tests or from ModelCatalog.
    public class FoldingSpecQaTests
    {
        static Vec2 V(double x, double y) => new Vec2(x, y);
        static readonly IEnumerable<string> AllIds = V2SpecBook.Ids;

        static PaperState Diamond() =>
            FoldPlanner.ApplyAll(V2SpecBook.Steps("tulip").Take(3));

        static double OutlineArea(PaperState s) => Outline.FromState(s).Area;

        // ---- CONTENT §2 transcription vs the catalogue ------------------------------------------------

        [TestCaseSource(nameof(AllIds))]
        public void Content2_CatalogueStepsMatchQaTranscription(string id)
        {
            var book = V2SpecBook.Book(id);
            var steps = ModelCatalog.Find(id).Steps;
            Assert.That(steps.Count, Is.EqualTo(book.Count));
            for (int i = 0; i < book.Count; i++)
            {
                var r = book[i];
                var s = steps[i];
                string m = $"{id} step {i + 1}";
                Assert.That(s.Kind, Is.EqualTo(r.Kind), m);
                Assert.That(s.Mountain, Is.EqualTo(r.Mountain), m);
                Assert.That(s.Layers, Is.EqualTo(r.Layers), m);
                Assert.That(s.Grab.X, Is.EqualTo(r.Grab.X).Within(1e-12), m + " grab");
                Assert.That(s.Grab.Y, Is.EqualTo(r.Grab.Y).Within(1e-12), m + " grab");
                Assert.That(s.Drop.X, Is.EqualTo(r.Drop.X).Within(1e-12), m + " drop");
                Assert.That(s.Drop.Y, Is.EqualTo(r.Drop.Y).Within(1e-12), m + " drop");
            }
        }

        // ---- D-103 per-step counts via FoldSession ---------------------------------------------------

        [TestCaseSource(nameof(AllIds))]
        public void D103_SessionCountsPerStep(string id)
        {
            var book = V2SpecBook.Book(id);
            var exp = V2SpecBook.D103(id);
            var s = new FoldSession();
            int creases = 0;
            for (int i = 0; i < book.Count; i++)
            {
                var r = book[i];
                var plan = r.Kind == FoldStepKind.Crease ? s.Crease(r.Grab, r.Drop, r.Layers) : s.Fold(r.Grab, r.Drop, r.Mountain, r.Layers);
                Assert.That(plan.Valid, Is.True, $"{id} step {i + 1}: {plan.Error}");
                if (r.Kind == FoldStepKind.Crease) creases++;
                string m = $"{id} after step {i + 1}";
                Assert.That(s.State.Facets.Count, Is.EqualTo(exp[i].facets), m + " facets");
                Assert.That(s.State.Creases.Count, Is.EqualTo(exp[i].segs), m + " crease segments");
                Assert.That(s.FoldCount, Is.EqualTo(exp[i].folds), m + " folds");
                Assert.That(s.CreaseCount, Is.EqualTo(creases), m + " CreaseCount");
                Assert.That(s.ActionCount, Is.EqualTo(i + 1), m + " ActionCount");
                Assert.That(s.State.TotalArea(), Is.EqualTo(4.0).Within(1e-6), m + " total area");
                Assert.That(s.AppliedSteps.Count, Is.EqualTo(i + 1), m);
            }
        }

        [TestCaseSource(nameof(AllIds))]
        public void Session_MatchesReferenceAndStateBeforeStep(string id)
        {
            var steps = V2SpecBook.Steps(id);
            var s = new FoldSession();
            for (int i = 0; i < steps.Count; i++)
            {
                V2SpecBook.AssertSameState(s.State, ModelCatalog.StateBeforeStep(id, i + 1), 0, $"{id} before step {i + 1}");
                var st = steps[i];
                if (st.Kind == FoldStepKind.Crease) s.Crease(st.Grab, st.Drop, st.Layers);
                else s.Fold(st.Grab, st.Drop, st.Mountain, st.Layers);
            }
            V2SpecBook.AssertSameState(ModelCatalog.ReferenceState(id), s.State, 0, id + " reference");
            V2SpecBook.AssertSameState(s.State, ModelCatalog.StateBeforeStep(id, steps.Count + 1), 0, id + " N+1");
            // Determinism (D-104): replaying AppliedSteps from the initial paper gives the same state bit for bit.
            V2SpecBook.AssertSameState(s.State, FoldPlanner.ApplyAll(s.AppliedSteps), 0, id + " replay");
        }

        [TestCaseSource(nameof(AllIds))]
        public void Session_UndoWalksBackThroughEveryReferenceStep(string id)
        {
            var steps = V2SpecBook.Steps(id);
            var s = V2SpecBook.Play(steps);
            for (int n = steps.Count; n >= 1; n--)
            {
                Assert.That(s.Undo(), Is.True);
                V2SpecBook.AssertSameState(ModelCatalog.StateBeforeStep(id, n), s.State, 0, $"{id} undo to before step {n}");
            }
            Assert.That(s.Undo(), Is.False);
            Assert.That(s.FoldCount + s.CreaseCount, Is.EqualTo(0));
        }

        // ---- CONTENT §2 diagram lines and magnets -----------------------------------------------------

        [TestCaseSource(nameof(AllIds))]
        public void Content2_DiagramLineEndsWithin1e4(string id)
        {
            var book = V2SpecBook.Book(id);
            for (int i = 0; i < book.Count; i++)
            {
                var before = ModelCatalog.StateBeforeStep(id, i + 1);
                Assert.That(LineOnPaper.TryGet(before, book[i].Grab, book[i].Drop, out var a, out var b), Is.True);
                bool same = Near(a, book[i].LineA) && Near(b, book[i].LineB);
                bool swapped = Near(a, book[i].LineB) && Near(b, book[i].LineA);
                Assert.That(same || swapped, Is.True, $"{id} step {i + 1}: got {a}-{b}");
            }

            bool Near(Vec2 p, Vec2 q) => Math.Abs(p.X - q.X) <= 1e-4 && Math.Abs(p.Y - q.Y) <= 1e-4;
        }

        [TestCaseSource(nameof(AllIds))]
        public void Content2_ExactDropHitsListedMagnetAndKeepsFoldLine(string id)
        {
            var book = V2SpecBook.Book(id);
            for (int i = 0; i < book.Count; i++)
            {
                var r = book[i];
                var before = ModelCatalog.StateBeforeStep(id, i + 1);
                var snap = DropSnap.Snap(before, r.Grab, r.Drop);
                var expected = r.Magnet == 'P' ? SnapKind.Point : r.Magnet == 'L' ? SnapKind.Line : r.Magnet == 'M' ? SnapKind.Mark : SnapKind.None;
                string m = $"{id} step {i + 1}";
                Assert.That(snap.Kind, Is.EqualTo(expected), m);
                var l0 = FoldLine.FromGrabDrop(r.Grab, r.Drop);
                var l1 = FoldLine.FromGrabDrop(r.Grab, snap.Point);
                Assert.That(l1.Normal.X, Is.EqualTo(l0.Normal.X).Within(1e-5), m + " normal");
                Assert.That(l1.Normal.Y, Is.EqualTo(l0.Normal.Y).Within(1e-5), m + " normal");
                Assert.That(l0.Distance(l1.Point), Is.LessThanOrEqualTo(1e-5), m + " line offset");
            }
        }

        // End-to-end through the input layer: grab by pointer, cycle layers with the wheel, move to the
        // book drop (magnets on), preview, commit with the same arguments (C-107) -> ★3 of that model.
        [TestCaseSource(nameof(AllIds))]
        public void Gesture_PlayingTheBookThroughFoldGestureGivesThreeStars(string id)
        {
            var s = new FoldSession();
            foreach (var r in V2SpecBook.Book(id))
            {
                var g = FoldGesture.TryBegin(s.State, r.Grab);
                Assert.That(g, Is.Not.Null, id);
                Assert.That(Vec2.Distance(g.Grab, r.Grab), Is.LessThan(1e-9), id + " grab snapped elsewhere");
                Assert.That(g.Layers, Is.EqualTo(0));
                if (r.Layers > 0)
                {
                    Assert.That(g.StackCount, Is.GreaterThan(r.Layers), id + " not enough layers to choose from");
                    for (int n = 0; n < r.Layers; n++) g.CycleLayers(1);
                    Assert.That(g.Layers, Is.EqualTo(r.Layers));
                }
                g.MoveTo(s.State, r.Drop);
                bool crease = r.Kind == FoldStepKind.Crease;
                var preview = g.Preview(s.State, r.Mountain, crease);
                Assert.That(preview.Valid, Is.True, id + " preview " + preview.Error);
                var commit = crease ? s.Crease(g.Grab, g.Drop.Point, g.Layers) : s.Fold(g.Grab, g.Drop.Point, r.Mountain, g.Layers);
                Assert.That(commit.Valid, Is.True);
                V2SpecBook.AssertSameState(preview.Result, s.State, 0, id + " preview == commit");
            }
            var res = Recognizer.Recognize(s.State, V2SpecBook.Ids, ModelCatalog.Recognition);
            Assert.That(res.ModelId, Is.EqualTo(id));
            Assert.That(res.Grade, Is.EqualTo(Grade.Star3));
            Assert.That(res.Score, Is.EqualTo(1.0).Within(0.01));
        }

        // ---- F-005 owned acceptance criteria ---------------------------------------------------------

        static IEnumerable<TestCaseData> MountainValleyCases()
        {
            yield return new TestCaseData("initial", 0, V(-1, 1), V(-1, -1));
            yield return new TestCaseData("initial-diag", 0, V(1, 1), V(-0.2, -0.4));
            yield return new TestCaseData("house1", 1, V(-1, 0), V(-0.6, -0.8));
            yield return new TestCaseData("diamond", 3, V(-1, -1), V(0.689655, -0.275862));
        }

        [TestCaseSource(nameof(MountainValleyCases))]
        public void F005_AC06_MountainAndValleySameOutlineOppositeStackEnd(string name, int prefix, Vec2 g, Vec2 d)
        {
            var start = name == "house1"
                ? FoldPlanner.ApplyAll(V2SpecBook.Steps("house").Take(prefix))
                : FoldPlanner.ApplyAll(V2SpecBook.Steps("tulip").Take(prefix));
            var valley = FoldPlanner.PlanFold(start, g, d, false, 0);
            var mountain = FoldPlanner.PlanFold(start, g, d, true, 0);
            Assert.That(valley.Valid && mountain.Valid, Is.True);
            var ov = Outline.FromState(valley.Result);
            var om = Outline.FromState(mountain.Result);
            Assert.That(om.Area, Is.EqualTo(ov.Area).Within(1e-9));
            Assert.That(Outline.IntersectionArea(ov, om), Is.EqualTo(ov.Area).Within(1e-9), "outlines differ");

            int staticCount = valley.Result.Facets.Count - valley.Moved.Count;
            Assert.That(valley.Moved.Count, Is.GreaterThan(0));
            var vStatic = valley.Result.Facets.Take(staticCount).Select(f => f.Layer).ToList();
            var mStatic = mountain.Result.Facets.Take(staticCount).Select(f => f.Layer).ToList();
            Assert.That(valley.Moved.Min(f => f.Layer), Is.GreaterThan(vStatic.Max()), "valley: moved must be on top");
            Assert.That(mountain.Moved.Max(f => f.Layer), Is.LessThan(mStatic.Min()), "mountain: moved must be at the bottom");
        }

        [Test]
        public void F005_AC11_CreaseThroughTwoLayers()
        {
            var s = new FoldSession();
            Assert.That(s.Fold(V(-1, 1), V(-1, -1), false, 0).Valid);
            Assert.That(s.Crease(V(-1, 0), V(1, 0), 0).Valid);
            Assert.That(s.State.Facets.Count, Is.EqualTo(2));
            Assert.That(s.FoldCount, Is.EqualTo(1));
            Assert.That(s.CreaseCount, Is.EqualTo(1));
            var cr = s.State.Creases;
            Assert.That(cr.Count, Is.EqualTo(2));
            Assert.That(cr.Sum(c => c.Length), Is.EqualTo(2.0).Within(1e-6));
            Assert.That(cr.Any(c => Seg(c, V(0, -1), V(0, 0))), Is.True, "missing (0,-1)-(0,0)");
            Assert.That(cr.Any(c => Seg(c, V(0, 1), V(0, 0))), Is.True, "missing (0,1)-(0,0)");
        }

        static bool Seg(CreaseSegment c, Vec2 p, Vec2 q) =>
            (Vec2.Distance(c.A, p) < 1e-6 && Vec2.Distance(c.B, q) < 1e-6) || (Vec2.Distance(c.A, q) < 1e-6 && Vec2.Distance(c.B, p) < 1e-6);

        [Test]
        public void F005_AC12_DiamondBaseShape()
        {
            var d = Diamond();
            Assert.That(d.Facets.Count, Is.EqualTo(6));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5 }, d.Facets.Select(f => f.Layer));
            var areas = d.Facets.Select(f => Math.Round(f.Area, 6)).OrderBy(a => a).ToArray();
            CollectionAssert.AreEqual(new[] { 0.5, 0.5, 0.5, 0.5, 1.0, 1.0 }, areas);
            Assert.That(OutlineArea(d), Is.EqualTo(1.0).Within(0.01));
            Assert.That(d.StackCountAt(V(-1, -1)), Is.EqualTo(6));
        }

        [Test]
        public void F005_AC12_TulipPetalTopTwoVersusAllLayers()
        {
            var d = Diamond();
            var top2 = FoldPlanner.PlanFold(d, V(-1, -1), V(0.689655, -0.275862), false, 2);
            Assert.That(top2.Valid, Is.True);
            Assert.That(top2.Moving.Count, Is.EqualTo(2));
            Assert.That(top2.Moving.Sum(f => f.Area), Is.EqualTo(0.70).Within(0.005));
            Assert.That(top2.Partial, Is.True);
            Assert.That(top2.Result.Facets.Count, Is.EqualTo(8));
            Assert.That(OutlineArea(top2.Result), Is.EqualTo(1.244).Within(0.01));

            var all = FoldPlanner.PlanFold(d, V(-1, -1), V(0.689655, -0.275862), false, 0);
            Assert.That(all.Valid, Is.True);
            Assert.That(all.Moving.Sum(f => f.Area), Is.EqualTo(3.143).Within(0.0005));
            Assert.That(all.Partial, Is.False);
            Assert.That(all.Result.Facets.Count, Is.EqualTo(12));
            Assert.That(OutlineArea(all.Result), Is.EqualTo(0.786).Within(0.01));
            Assert.That(all.Result.TotalArea(), Is.EqualTo(4.0).Within(1e-6));
            Assert.That(top2.Result.TotalArea(), Is.EqualTo(4.0).Within(1e-6));
        }

        [Test]
        public void F005_AC13_HingedLayersFoldTogether()
        {
            var tri = FoldPlanner.ApplyAll(V2SpecBook.Steps("tulip").Take(1));
            Assert.That(tri.StackCountAt(V(-1, 1)), Is.EqualTo(2));
            var one = FoldPlanner.PlanFold(tri, V(-1, 1), V(-1.4, 0.0), false, 1);
            var all = FoldPlanner.PlanFold(tri, V(-1, 1), V(-1.4, 0.0), false, 0);
            Assert.That(one.Valid && all.Valid, Is.True);
            V2SpecBook.AssertSameState(all.Result, one.Result, 1e-12, "layers 1 vs 0");
            Assert.That(one.Partial, Is.False);
            Assert.That(OutlineArea(one.Result), Is.EqualTo(1.804).Within(0.01));
        }

        [TestCase(6)]
        [TestCase(9)]
        [TestCase(1000)]
        public void F005_AC14_LayersAtOrAboveStackEqualsAll(int layers)
        {
            var d = Diamond();
            var all = FoldPlanner.PlanFold(d, V(-1, -1), V(0.689655, -0.275862), false, 0);
            var big = FoldPlanner.PlanFold(d, V(-1, -1), V(0.689655, -0.275862), false, layers);
            Assert.That(big.Valid, Is.True);
            V2SpecBook.AssertSameState(all.Result, big.Result, 0, "layers " + layers);
        }

        [Test]
        public void F005_AC15_MountainTopTwoLayerOrder()
        {
            var d = Diamond();
            var plan = FoldPlanner.PlanFold(d, V(-1, -1), V(0.689655, -0.275862), true, 2);
            Assert.That(plan.Valid, Is.True);
            Assert.That(plan.Partial, Is.True);
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, plan.Moved.Select(f => f.Layer), "moved");
            // C-103: Result = Static (same order, same objects' geometry) followed by the moved parts.
            var res = plan.Result.Facets;
            Assert.That(res.Count, Is.EqualTo(plan.Static.Count + plan.Moved.Count));
            var byOldLayer = new Dictionary<int, List<int>>();
            for (int i = 0; i < plan.Static.Count; i++)
            {
                int old = plan.Static[i].Layer;
                if (!byOldLayer.TryGetValue(old, out var l)) byOldLayer[old] = l = new List<int>();
                l.Add(res[i].Layer);
            }
            // before: bottom 0,1 · other flap 2,3 · grabbed (top) flap 4,5
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, byOldLayer[0].Concat(byOldLayer[1]), "bottom");
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, byOldLayer[2].Concat(byOldLayer[3]), "other flap");
            CollectionAssert.AreEquivalent(new[] { 6, 7 }, byOldLayer[4].Concat(byOldLayer[5]), "rest of the grabbed flap");
        }

        [Test]
        public void F005_AC16_DropMagnets()
        {
            var plane1 = FoldPlanner.ApplyAll(V2SpecBook.Steps("plane").Take(1));
            var m = DropSnap.Snap(plane1, V(-1, 1), V(0.03, 0.2));
            Assert.That(m.Kind, Is.EqualTo(SnapKind.Mark));
            Assert.That(Vec2.Distance(m.Point, V(0, 0.2)), Is.LessThan(1e-6));
            Assert.That(m.AnchorCount, Is.EqualTo(2));

            var plane2 = FoldPlanner.ApplyAll(V2SpecBook.Steps("plane").Take(2));
            var p = DropSnap.Snap(plane2, V(1, 1), V(0.04, -0.03));
            Assert.That(p.Kind, Is.EqualTo(SnapKind.Point));
            Assert.That(Vec2.Distance(p.Point, V(0, 0)), Is.LessThan(1e-6));
            Assert.That(p.AnchorCount, Is.EqualTo(1));

            var house3 = FoldPlanner.ApplyAll(V2SpecBook.Steps("house").Take(3));
            var l = DropSnap.Snap(house3, V(-1, 0), V(-0.62, -0.78));
            Assert.That(l.Kind, Is.EqualTo(SnapKind.Line));
            Assert.That(Vec2.Distance(l.Point, V(-0.6, -0.8)), Is.LessThan(1e-6));
            Assert.That(l.AnchorCount, Is.EqualTo(2));

            var house1 = FoldPlanner.ApplyAll(V2SpecBook.Steps("house").Take(1));
            var n = DropSnap.Snap(house1, V(-1, 0), V(-0.62, -0.78));
            Assert.That(n.Kind, Is.EqualTo(SnapKind.None));
            Assert.That(n.Point, Is.EqualTo(V(-0.62, -0.78)));
            Assert.That(n.AnchorCount, Is.EqualTo(0));
        }

        [Test]
        public void DropSnap_GuideWinsWithinRadiusOnly()
        {
            var s = PaperState.Initial;
            var g = DropSnap.Snap(s, V(-1, 1), V(0.05, 0.05), V(0, 0));
            Assert.That(g.Kind, Is.EqualTo(SnapKind.Guide));
            Assert.That(g.Point, Is.EqualTo(V(0, 0)));
            var far = DropSnap.Snap(s, V(-1, 1), V(0.3, 0.3), V(0, 0));
            Assert.That(far.Kind, Is.Not.EqualTo(SnapKind.Guide));
        }

        // ---- layer-choice cases from CONTENT §2 / §4 -------------------------------------------------

        [Test]
        public void Cicada_WingsWithAllLayersBlockSteps7And8()
        {
            var steps = V2SpecBook.Steps("cicada");
            var s = V2SpecBook.Play(steps.Take(5));
            Assert.That(s.Fold(steps[5].Grab, steps[5].Drop, false, 0).Valid, Is.True, "step 6 all layers");
            var before = s.State;
            var p7 = s.Fold(steps[6].Grab, steps[6].Drop, false, 2);
            Assert.That(p7.Valid, Is.False);
            Assert.That(p7.Error, Is.EqualTo(FoldError.NothingGrabbed));
            Assert.That(FoldTexts.ErrorMessage(p7.Error), Is.EqualTo("잡은 곳에 종이가 없어요"));
            var p8 = s.Fold(steps[7].Grab, steps[7].Drop, false, 0);
            Assert.That(p8.Valid, Is.False);
            Assert.That(p8.Error, Is.EqualTo(FoldError.NothingToFold));
            Assert.That(FoldTexts.ErrorMessage(p8.Error), Is.EqualTo("접히는 부분이 없어요"));
            Assert.That(ReferenceEquals(before, s.State), Is.True, "rejected folds must not change the state");
            Assert.That(s.FoldCount, Is.EqualTo(4));
        }

        [Test]
        public void Cicada_WingTopOneEqualsTopTwo_Hinged()
        {
            var steps = V2SpecBook.Steps("cicada");
            var before6 = ModelCatalog.StateBeforeStep("cicada", 6);
            var one = FoldPlanner.PlanFold(before6, steps[5].Grab, steps[5].Drop, false, 1);
            var two = FoldPlanner.PlanFold(before6, steps[5].Grab, steps[5].Drop, false, 2);
            V2SpecBook.AssertSameState(two.Result, one.Result, 0, "cicada 6 top1 vs top2");
        }

        [Test]
        public void Rejections_DoNotChangeSession()
        {
            var s = V2SpecBook.Play(V2SpecBook.Steps("house").Take(2));
            var st = s.State;
            int folds = s.FoldCount, creases = s.CreaseCount, n = s.AppliedSteps.Count;
            Assert.That(s.Fold(V(-1, 0), V(-1, 0.02), false, 0).Error, Is.EqualTo(FoldError.DragTooShort));
            Assert.That(s.Fold(V(-1, 0), V(0, 0), false, -1).Error, Is.EqualTo(FoldError.InvalidLayers));
            Assert.That(s.Fold(V(-1, 0), V(-5, 5), false, 0).Error, Is.EqualTo(FoldError.LineOutsidePaper).Or.EqualTo(FoldError.NothingToFold));
            Assert.That(s.Crease(V(5, 5), V(5, 4), 1).Error, Is.EqualTo(FoldError.NothingGrabbed));
            Assert.That(ReferenceEquals(st, s.State), Is.True);
            Assert.That(s.FoldCount, Is.EqualTo(folds));
            Assert.That(s.CreaseCount, Is.EqualTo(creases));
            Assert.That(s.AppliedSteps.Count, Is.EqualTo(n));
        }

        [Test]
        public void Gesture_LayerCycleOnDiamond()
        {
            var g = FoldGesture.TryBegin(Diamond(), V(-0.95, -0.97));
            Assert.That(g, Is.Not.Null);
            Assert.That(g.GrabbedCorner, Is.True);
            Assert.That(Vec2.Distance(g.Grab, V(-1, -1)), Is.LessThan(1e-9));
            Assert.That(g.StackCount, Is.EqualTo(6));
            g.CycleLayers(1); g.CycleLayers(1);
            Assert.That(g.Layers, Is.EqualTo(2));
            Assert.That(FoldTexts.LayerLabel(g.Layers, g.StackCount), Is.EqualTo("겹: 위 2 / 6"));
            g.CycleLayers(4);
            Assert.That(g.Layers, Is.EqualTo(0), "wraps to all after M-1");
            Assert.That(FoldTexts.LayerLabel(0, 6), Is.EqualTo("겹: 전체"));
            g.CycleLayers(-1);
            Assert.That(g.Layers, Is.EqualTo(5));
            g.CycleLayers(-13);
            Assert.That(g.Layers, Is.EqualTo(4));

            var single = FoldGesture.TryBegin(PaperState.Initial, V(0.3, 0.2));
            Assert.That(single.StackCount, Is.EqualTo(1));
            single.CycleLayers(3);
            Assert.That(single.Layers, Is.EqualTo(0), "M = 1 ignores the wheel");
            Assert.That(FoldGesture.TryBegin(PaperState.Initial, V(3, 3)), Is.Null);
        }

        // ---- determinism / fuzz ----------------------------------------------------------------------

        [Test]
        public void Fuzz_RandomSessionsPreserveAreaAndReplayDeterministically()
        {
            var rng = new Random(9009);
            for (int run = 0; run < 60; run++)
            {
                var s = new FoldSession();
                for (int a = 0; a < 6; a++)
                {
                    var g = V(rng.NextDouble() * 2.4 - 1.2, rng.NextDouble() * 2.4 - 1.2);
                    var d = V(rng.NextDouble() * 2.4 - 1.2, rng.NextDouble() * 2.4 - 1.2);
                    int layers = rng.Next(4);
                    double k = rng.NextDouble();
                    var before = s.State;
                    FoldPlan p = null;
                    if (k < 0.15) s.Flip();
                    else if (k < 0.35) p = s.Crease(g, d, layers);
                    else p = s.Fold(g, d, rng.Next(2) == 0, layers);
                    if (p != null && !p.Valid) Assert.That(ReferenceEquals(before, s.State), Is.True);
                    Assert.That(s.State.TotalArea(), Is.EqualTo(4.0).Within(1e-6), $"run {run} action {a}");
                    var layersSeen = s.State.Facets.Select(f => f.Layer).Distinct().OrderBy(x => x).ToList();
                    Assert.That(layersSeen.First(), Is.EqualTo(0));
                    Assert.That(layersSeen.Last(), Is.EqualTo(layersSeen.Count - 1), "layers must be ranked 0..n-1");
                }
                V2SpecBook.AssertSameState(s.State, FoldPlanner.ApplyAll(s.AppliedSteps), 0, "replay run " + run);
            }
        }
    }
}
