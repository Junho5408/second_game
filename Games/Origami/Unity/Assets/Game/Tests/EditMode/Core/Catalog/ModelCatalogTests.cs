using System;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Core.Recognition;

namespace Origami.Tests.Catalog
{
    // C-120 ModelCatalog vs CONTENT_SPECS §1 (D-110), §2 (D-111: steps, "선" ±1e-4, "자석"), §5 popular table (D-113),
    // D-103 per-step engine counts, F-005 AC-16 last clause (reference inputs keep their line after the magnet, ±1e-5).
    public class ModelCatalogTests
    {
        private const double LitTol = 1e-4;   // CONTENT 6-digit literals
        private static readonly double K = 1 - Math.Sqrt(2);
        private static readonly double X0 = 2 * Math.Tan(Math.PI / 8);

        private static Vec2 V(double x, double y) => new Vec2(x, y);

        [Test]
        public void Content1_ModelTable()
        {
            var expected = new[]
            {
                (1, "plane", "종이비행기", 6, 0, true, 2, 6, 5, 0.0),
                (2, "house", "집", 8, 0, true, 1, 5, 3, 0.0),
                (3, "tulip", "튤립", 12, 20, false, 2, 5, 5, 45.0),
                (4, "dog", "강아지 얼굴", 18, 50, false, 3, 6, 4, 0.0),
                (5, "cicada", "매미", 26, 150, false, 4, 10, 8, 45.0),
            };
            var models = ModelCatalog.Models;
            Assert.AreEqual(expected.Length, models.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                var (order, id, name, basePrice, page, owned, difficulty, actions, folds, rot) = expected[i];
                var m = models[i];
                Assert.AreEqual(order, m.Order, id);
                Assert.AreEqual(id, m.Id);
                Assert.AreEqual(name, m.DisplayName, id);
                Assert.AreEqual(basePrice, m.BasePrice, id);
                Assert.AreEqual(page, m.PagePrice, id);
                Assert.AreEqual(owned, m.InitiallyOwned, id);
                Assert.AreEqual(difficulty, m.Difficulty, id);
                Assert.AreEqual(actions, m.ActionCount, id);
                Assert.AreEqual(actions, m.Steps.Count, id);
                Assert.AreEqual(folds, m.FoldCount, id);
                Assert.AreEqual(actions - folds, m.Steps.Count(s => s.Kind == FoldStepKind.Crease), id + " creases");
                Assert.IsFalse(m.Steps.Any(s => s.Kind == FoldStepKind.Flip), id);
                Assert.AreEqual(rot, m.CoverRotationDeg, id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.Techniques), id);
                Assert.AreEqual(m.Steps.Count, m.StepTexts.Count, id);
                Assert.IsTrue(m.StepTexts.All(t => !string.IsNullOrWhiteSpace(t)), id);
            }
            CollectionAssert.AreEqual(new[] { "plane", "house", "tulip", "dog", "cicada" },
                new[] { ModelIds.Plane, ModelIds.House, ModelIds.Tulip, ModelIds.Dog, ModelIds.Cicada });
        }

        [TestCase("plane", 0.845)]
        [TestCase("house", 1.500)]
        [TestCase("tulip", 1.488)]
        [TestCase("dog", 1.477)]
        [TestCase("cicada", 0.690)]
        public void Content1_ReferenceOutlineArea(string id, double area)
        {
            Assert.That(Outline.FromState(ModelCatalog.ReferenceState(id)).Area, Is.EqualTo(area).Within(0.01));
        }

        [Test]
        public void D111_StepsAreTheContentSequences()
        {
            var c = V(-1, -1);
            void Is(FoldStep s, FoldStepKind kind, Vec2 g, Vec2 d, bool mountain = false, int layers = 0)
            {
                Assert.AreEqual(kind, s.Kind);
                Assert.AreEqual(g, s.Grab);
                Assert.AreEqual(d, s.Drop);
                Assert.AreEqual(mountain, s.Mountain);
                Assert.AreEqual(layers, s.Layers);
            }
            const FoldStepKind F = FoldStepKind.Fold, C = FoldStepKind.Crease;
            var p = ModelCatalog.Find("plane").Steps;
            Is(p[0], C, V(-1, 1), V(1, 1));
            Is(p[1], F, V(-1, 1), V(0, 0));
            Is(p[2], F, V(1, 1), V(0, 0));
            Is(p[3], F, V(-1, 0), V(0, K));
            Is(p[4], F, V(1, 0), V(0, K));
            Is(p[5], F, V(-X0, -1), V(X0, -1));
            var h = ModelCatalog.Find("house").Steps;
            Is(h[0], F, V(-1, 1), V(-1, -1));
            Is(h[1], C, V(-1, 0), V(1, 0));
            Is(h[2], C, V(-1, -1), V(-1, 0));
            Is(h[3], F, V(-1, 0), V(-0.6, -0.8));
            Is(h[4], F, V(1, 0), V(0.6, -0.8));
            var t = ModelCatalog.Find("tulip").Steps;
            Is(t[0], F, V(1, 1), c);
            Is(t[1], F, V(-1, 1), c);
            Is(t[2], F, V(1, -1), c);
            Is(t[3], F, c, V(0.689655, -0.275862), false, 2);
            Is(t[4], F, c, V(-0.275862, 0.689655), false, 2);
            var d = ModelCatalog.Find("dog").Steps;
            Is(d[0], F, V(1, 1), c);
            Is(d[1], C, V(-1, 1), V(1, -1));
            Is(d[2], C, c, V(0, 0));
            Is(d[3], F, V(-1, 1), V(-1.4, 0.0));
            Is(d[4], F, V(1, -1), V(0.0, -1.4));
            Is(d[5], F, c, V(-0.5, -0.5));
            var m = ModelCatalog.Find("cicada").Steps;
            Is(m[0], F, V(1, 1), c);
            Is(m[1], F, V(-1, 1), c);
            Is(m[2], F, V(1, -1), c);
            Is(m[3], C, V(-1, 0), V(0, -1));
            Is(m[4], C, V(-1, 0), V(0, 0));
            Is(m[5], F, c, V(0.453858, -0.108926), false, 2);
            Is(m[6], F, c, V(-0.108926, 0.453858), false, 2);
            Is(m[7], F, c, V(-0.38, -0.38));
            Is(m[8], F, V(-1, 0), V(-0.5, -0.5), true);
            Is(m[9], F, V(0, -1), V(-0.5, -0.5), true);
        }

        [Test]
        public void D111_StepTextsSpotCheck()
        {
            Assert.AreEqual("왼쪽 변을 오른쪽 변에 맞춰 반으로 접었다가 펴요. 가운데 세로 자국이 생겨요. (Ctrl을 누른 채 놓기)",
                ModelCatalog.Find("plane").StepTexts[0]);
            Assert.AreEqual("오른쪽도 똑같이. 지붕 완성!", ModelCatalog.Find("house").StepTexts[4]);
            Assert.AreEqual("오른쪽 옆도 뒤로 같은 점에 맞춰요. 매미 완성!", ModelCatalog.Find("cicada").StepTexts[9]);
        }

        // D-103: (facets, crease segments, folds) after each step (balance_check.py apply).
        private static readonly object[] D103 =
        {
            new object[] { "plane", new[] { 1, 1, 0, 2, 1, 1, 3, 1, 2, 5, 1, 3, 7, 1, 4, 8, 1, 5 } },
            new object[] { "house", new[] { 2, 0, 1, 2, 2, 1, 2, 4, 1, 4, 4, 2, 6, 4, 3 } },
            new object[] { "tulip", new[] { 2, 0, 1, 4, 0, 2, 6, 0, 3, 8, 0, 4, 10, 0, 5 } },
            new object[] { "dog", new[] { 2, 0, 1, 2, 2, 1, 2, 4, 1, 4, 4, 2, 6, 4, 3, 8, 4, 4 } },
            new object[]
            {
                "cicada",
                new[] { 2, 0, 1, 4, 0, 2, 6, 0, 3, 6, 4, 3, 6, 10, 3, 8, 10, 4, 10, 10, 5, 12, 10, 6, 20, 10, 7, 28, 10, 8 },
            },
        };

        [TestCaseSource(nameof(D103))]
        public void D103_StepCountsMatchReferenceEngine(string id, int[] triples)
        {
            var m = ModelCatalog.Find(id);
            Assert.AreEqual(m.Steps.Count * 3, triples.Length);
            var session = new FoldSession();
            for (int i = 0; i < m.Steps.Count; i++)
            {
                var s = m.Steps[i];
                var plan = s.Kind == FoldStepKind.Fold
                    ? session.Fold(s.Grab, s.Drop, s.Mountain, s.Layers)
                    : session.Crease(s.Grab, s.Drop, s.Layers);
                Assert.IsTrue(plan.Valid, id + " step " + (i + 1));
                var st = ModelCatalog.StateBeforeStep(id, i + 2);
                Assert.AreEqual(triples[3 * i], st.Facets.Count, id + " step " + (i + 1) + " facets");
                Assert.AreEqual(triples[3 * i + 1], st.Creases.Count, id + " step " + (i + 1) + " crease segments");
                Assert.AreEqual(triples[3 * i + 2], session.FoldCount, id + " step " + (i + 1) + " folds");
                Assert.AreEqual(st.Facets.Count, session.State.Facets.Count);
                Assert.That(st.TotalArea(), Is.EqualTo(4.0).Within(1e-6), id + " area");
            }
            Assert.AreEqual(m.FoldCount, session.FoldCount);
            Assert.AreEqual(m.ActionCount, session.ActionCount);
        }

        private static readonly double[][] Lines =
        {
            new[] { 0, -1, 0, 1, -1, 0, 0, 1, 0, 1, 1, 0, -0.828427, -1, 0, 1, 0, 1, 0.828427, -1, 0, -1, 0, 1 },
            new[] { -1, 0, 1, 0, 0, -1, 0, 0, -1, -0.5, 1, -0.5, -1, -0.5, 0, 0, 0, 0, 1, -0.5 },
            new[] { -1, 1, 1, -1, -1, 0, 0, 0, 0, 0, 0, -1, 0, -1, -0.428572, 0, 0.386473, -0.594203, -1, 0 },
            new[] { -1, 1, 1, -1, -1, -1, 0, 0, 0, -1, -1, 0, -1, 0.42, -0.033333, 0.033333, 0.033333, -0.033333, 0.42, -1, -0.5, -1, -1, -0.5 },
            new[]
            {
                -1, 1, 1, -1, -1, 0, 0, 0, 0, 0, 0, -1, -1, -1, 0, 0, -0.5, -1, -0.5, 0, 0, -1, -0.612903, 0,
                0.150257, -0.704996, -1, 0, -0.38, -1, -1, -0.38, -0.94, -0.44, -0.139167, 0.360833, 0.360833, -0.139167, -0.44, -0.94,
            },
        };

        private static readonly string[] Magnets = { "PMPMPP", "PPPLL", "PPP-L", "PPP--P", "PPPPP-LPPP" };

        [Test]
        public void Content2_DiagramLinesMatchWithin1e4()
        {
            for (int mi = 0; mi < ModelCatalog.Models.Count; mi++)
            {
                var m = ModelCatalog.Models[mi];
                for (int i = 0; i < m.Steps.Count; i++)
                {
                    var st = ModelCatalog.StateBeforeStep(m.Id, i + 1);
                    Assert.IsTrue(LineOnPaper.TryGet(st, m.Steps[i].Grab, m.Steps[i].Drop, out var a, out var b), m.Id + (i + 1));
                    var e = Lines[mi];
                    string c = m.Id + " step " + (i + 1) + ": got " + a + "–" + b;
                    var e1 = V(e[4 * i], e[4 * i + 1]);
                    var e2 = V(e[4 * i + 2], e[4 * i + 3]);
                    // The CONTENT table is a segment (house step 3 lists its ends in the other order than
                    // balance_check.py `steps`), so the ends are compared as an unordered pair. The ordered a/b
                    // (min/max parameter, C-105) is checked against Python in Folding/PythonParityTests.
                    bool same = Near(a, e1) && Near(b, e2);
                    bool swapped = Near(a, e2) && Near(b, e1);
                    Assert.IsTrue(same || swapped, c);
                }
            }
        }

        private static bool Near(Vec2 p, Vec2 q) => Math.Abs(p.X - q.X) <= LitTol && Math.Abs(p.Y - q.Y) <= LitTol;

        [Test]
        public void Content2_LineEndOrderFollowsPython()
        {
            // balance_check.py `steps`: house 3 = (1,−0.5)–(−1,−0.5), plane 4 = (−0.828427,−1)–(0,1)
            Assert.IsTrue(LineOnPaper.TryGet(ModelCatalog.StateBeforeStep("house", 3), V(-1, -1), V(-1, 0), out var a, out var b));
            Assert.IsTrue(Near(a, V(1, -0.5)) && Near(b, V(-1, -0.5)), a + "–" + b);
            var p4 = ModelCatalog.Find("plane").Steps[3];
            Assert.IsTrue(LineOnPaper.TryGet(ModelCatalog.StateBeforeStep("plane", 4), p4.Grab, p4.Drop, out a, out b));
            Assert.IsTrue(Near(a, V(-X0, -1)) && Near(b, V(0, 1)), a + "–" + b);
        }

        [Test]
        public void Content2_MagnetKinds_F005_AC16_ReferenceInputsKeepTheirLine()
        {
            for (int mi = 0; mi < ModelCatalog.Models.Count; mi++)
            {
                var m = ModelCatalog.Models[mi];
                Assert.AreEqual(m.Steps.Count, Magnets[mi].Length, m.Id);
                for (int i = 0; i < m.Steps.Count; i++)
                {
                    var s = m.Steps[i];
                    var st = ModelCatalog.StateBeforeStep(m.Id, i + 1);
                    var r = DropSnap.Snap(st, s.Grab, s.Drop);
                    string c = m.Id + " step " + (i + 1);
                    char k = Magnets[mi][i];
                    var expected = k == 'P' ? SnapKind.Point : k == 'L' ? SnapKind.Line : k == 'M' ? SnapKind.Mark : SnapKind.None;
                    Assert.AreEqual(expected, r.Kind, c);
                    // same fold line (balance_check.py recog "자석" check)
                    var l1 = FoldLine.FromGrabDrop(s.Grab, s.Drop);
                    var l2 = FoldLine.FromGrabDrop(s.Grab, r.Point);
                    Assert.That(Math.Abs(Vec2.Dot(l1.Normal, l2.Normal)), Is.EqualTo(1.0).Within(1e-9), c + " direction");
                    Assert.That(Math.Abs(Vec2.Dot(l2.Point - l1.Point, l1.Normal)), Is.LessThan(1e-5), c + " offset");
                }
            }
        }

        [Test]
        public void ReferenceStates_AreSharedAndConsistent()
        {
            foreach (var m in ModelCatalog.Models)
            {
                var r = ModelCatalog.ReferenceState(m.Id);
                Assert.AreSame(r, ModelCatalog.ReferenceState(m.Id));
                Assert.AreSame(r, ModelCatalog.StateBeforeStep(m.Id, m.ActionCount + 1));
                Assert.AreSame(PaperState.Initial, ModelCatalog.StateBeforeStep(m.Id, 1));
                var replay = FoldPlanner.ApplyAll(m.Steps);
                Assert.AreEqual(r.Facets.Count, replay.Facets.Count);
                for (int i = 0; i < r.Facets.Count; i++) CollectionAssert.AreEqual(r.Facets[i].World, replay.Facets[i].World);
            }
            Assert.AreSame(ModelCatalog.Recognition, ModelCatalog.Recognition);
            CollectionAssert.AreEqual(ModelCatalog.Models.Select(m => m.Id), ModelCatalog.Recognition.ModelIds);
        }

        [Test]
        public void Lookup_AndArgumentChecks()
        {
            Assert.AreEqual(0, ModelCatalog.IndexOf("plane"));
            Assert.AreEqual(4, ModelCatalog.IndexOf("cicada"));
            Assert.AreEqual(-1, ModelCatalog.IndexOf("Plane"), "ordinal");
            Assert.AreEqual(-1, ModelCatalog.IndexOf(null));
            Assert.AreEqual(-1, ModelCatalog.IndexOf(""));
            Assert.AreEqual("dog", ModelCatalog.Find("dog").Id);
            Assert.IsNull(ModelCatalog.Find("unicorn"));
            Assert.IsNull(ModelCatalog.Find(null));
            Assert.Throws<ArgumentException>(() => ModelCatalog.ReferenceState("unicorn"));
            Assert.Throws<ArgumentException>(() => ModelCatalog.ReferenceState(null));
            Assert.Throws<ArgumentException>(() => ModelCatalog.StateBeforeStep("unicorn", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelCatalog.StateBeforeStep("plane", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelCatalog.StateBeforeStep("plane", 8));
            Assert.DoesNotThrow(() => ModelCatalog.StateBeforeStep("plane", 7));
        }

        [Test]
        public void Content5_PopularByDay()
        {
            CollectionAssert.AreEqual(new[] { "plane", "house", "tulip", "house", "dog", "tulip", "cicada" }, ModelCatalog.PopularByDay);
            for (int day = 1; day <= 7; day++) Assert.AreEqual(ModelCatalog.PopularByDay[day - 1], ModelCatalog.PopularModelId(day));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelCatalog.PopularModelId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelCatalog.PopularModelId(8));
        }

        [Test]
        public void Lists_AreReadOnly()
        {
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<ModelDefinition>)ModelCatalog.Models).Clear());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<string>)ModelCatalog.PopularByDay)[0] = "x");
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<FoldStep>)ModelCatalog.Models[0].Steps).Clear());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<string>)ModelCatalog.Models[0].StepTexts).Clear());
        }
    }
}
