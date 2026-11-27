using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Core.Recognition;

namespace Origami.Tests.Recognition
{
    // C-110 Recognizer / RecognitionCatalog — F-009 AC-01..AC-03, AC-05, AC-06 (owned by ORIGAMI-009).
    // Expected values: CONTENT_SPECS §4 (J-01..J-18, model matrix), tolerance ±0.01 (FEATURE_SPECS common notation).
    // Per review W-6 / ORIGAMI-008 W-3: no test asserts the dog-without-chin result or a J-case percent value;
    // shortcut results are only checked with "< 0.91" (CODING_CONVENTION).
    public class RecognizerTests
    {
        private const double Tol = 0.01;
        private static readonly string[] All = { ModelIds.Plane, ModelIds.House, ModelIds.Tulip, ModelIds.Dog, ModelIds.Cicada };
        private static readonly double X0 = 2 * Math.Tan(Math.PI / 8);

        private static Vec2 V(double x, double y) => new Vec2(x, y);
        private static FoldStep F(Vec2 g, Vec2 d, bool m = false, int l = 0) => FoldStep.Fold(g, d, m, l);
        private static IReadOnlyList<FoldStep> Ref(string id) => ModelCatalog.Find(id).Steps;

        private static FoldStep WithLayers(FoldStep s, int layers) => FoldStep.Fold(s.Grab, s.Drop, s.Mountain, layers);

        /// <summary>FEATURE_SPECS Var(dy): house roof by hand, no creases.</summary>
        private static FoldStep[] Var(double dy) =>
            new[] { F(V(-1, 1), V(-1, -1)), F(V(-1, 0), V(-0.6, -0.8 + dy)), F(V(1, 0), V(0.6, -0.8 + dy)) };

        private static RecognitionResult Judge(IEnumerable<FoldStep> steps, IEnumerable<string> owned = null) =>
            Recognizer.Recognize(FoldPlanner.ApplyAll(steps), owned ?? All, ModelCatalog.Recognition);

        private static void AssertPiece(RecognitionResult r, string model, Grade grade, double score, string id)
        {
            Assert.IsTrue(r.IsPiece, id);
            Assert.AreEqual(model, r.ModelId, id);
            Assert.AreEqual(model, r.BestModelId, id);
            Assert.AreEqual(grade, r.Grade, id);
            Assert.That(r.Score, Is.EqualTo(score).Within(Tol), id);
            Assert.AreEqual(r.BestScore, r.Score, id);
        }

        private static void AssertCrumpled(RecognitionResult r, string best, double score, string id)
        {
            Assert.IsFalse(r.IsPiece, id);
            Assert.IsNull(r.ModelId, id);
            Assert.AreEqual(Grade.None, r.Grade, id);
            Assert.AreEqual(best, r.BestModelId, id);
            Assert.That(r.BestScore, Is.EqualTo(score).Within(Tol), id);
        }

        [TestCase(ModelIds.Plane)]
        [TestCase(ModelIds.House)]
        [TestCase(ModelIds.Tulip)]
        [TestCase(ModelIds.Dog)]
        [TestCase(ModelIds.Cicada)]
        public void J01_ReferenceSequenceIsThatModelStar3(string id)
        {
            AssertPiece(Judge(Ref(id)), id, Grade.Star3, 1.0, "J-01 " + id);
        }

        [Test]
        public void J02_HouseFoldedBottomUp()
        {
            AssertPiece(Judge(new[] { F(V(-1, -1), V(-1, 1)), F(V(-1, 0), V(-0.6, 0.8)), F(V(1, 0), V(0.6, 0.8)) }),
                ModelIds.House, Grade.Star3, 1.0, "J-02");
        }

        [Test]
        public void J03_PlaneRightFirstWithoutCrease()
        {
            var p = Ref(ModelIds.Plane);
            AssertPiece(Judge(new[] { p[2], p[1], p[4], p[3], F(V(X0, -1), V(-X0, -1)) }), ModelIds.Plane, Grade.Star3, 1.0, "J-03");
        }

        [Test]
        public void J04_J05_J06_HouseVariations()
        {
            AssertPiece(Judge(Var(0.01)), ModelIds.House, Grade.Star3, 0.992, "J-04");
            AssertPiece(Judge(Var(0.06)), ModelIds.House, Grade.Star2, 0.951, "J-05");
            AssertPiece(Judge(Var(0.15)), ModelIds.House, Grade.Star1, 0.892, "J-06");
        }

        [Test]
        public void J07_PlaneWrongFlapPointIsCrumpled()
        {
            var p = Ref(ModelIds.Plane);
            AssertCrumpled(Judge(p.Take(3).Concat(new[] { F(V(-1, 0), V(0, -0.3)), F(V(1, 0), V(0, -0.3)), p[5] })),
                ModelIds.Plane, 0.804, "J-07");
        }

        [Test]
        public void J08_J09_IncompleteHouse()
        {
            AssertCrumpled(Judge(Var(0).Take(2)), ModelIds.House, 0.783, "J-08");
            AssertCrumpled(Judge(Var(0).Take(1)), ModelIds.House, 0.728, "J-09");
        }

        [Test]
        public void J10_DogWithoutEars()
        {
            var d = Ref(ModelIds.Dog);
            AssertCrumpled(Judge(d.Take(3).Concat(d.Skip(5))), ModelIds.Dog, 0.693, "J-10");
        }

        [Test]
        public void J11_CicadaWithoutSideFolds()
        {
            AssertCrumpled(Judge(Ref(ModelIds.Cicada).Take(8)), ModelIds.Tulip, 0.677, "J-11");
        }

        [Test]
        public void J12_DogWithoutOwningTheBook()
        {
            AssertCrumpled(Judge(Ref(ModelIds.Dog), new[] { ModelIds.Plane, ModelIds.House }), ModelIds.House, 0.477, "J-12");
        }

        [TestCase(ModelIds.Plane)]
        [TestCase(ModelIds.House)]
        [TestCase(ModelIds.Tulip)]
        [TestCase(ModelIds.Dog)]
        [TestCase(ModelIds.Cicada)]
        public void J13_ReferencePlusFlipIsStillStar3(string id)
        {
            var r = Judge(Ref(id).Concat(new[] { FoldStep.Flip() }));
            Assert.AreEqual(id, r.ModelId, "J-13 " + id);
            Assert.AreEqual(Grade.Star3, r.Grade, "J-13 " + id);
            Assert.That(r.Score, Is.InRange(0.994 - Tol, 1.0 + Tol), "J-13 " + id);
        }

        [Test]
        public void J14_TulipPetalsWithAllLayers()
        {
            AssertCrumpled(Judge(Ref(ModelIds.Tulip).Select(s => WithLayers(s, 0))), ModelIds.Cicada, 0.714, "J-14");
        }

        [Test]
        public void J15_CicadaWingWithAllLayers()
        {
            var c = Ref(ModelIds.Cicada);
            AssertCrumpled(Judge(c.Take(5).Concat(new[] { WithLayers(c[5], 0) }).Concat(c.Skip(8))), ModelIds.Cicada, 0.648, "J-15");
        }

        [Test]
        public void J16_HouseWithoutCreases()
        {
            AssertPiece(Judge(Ref(ModelIds.House).Where(s => s.Kind == FoldStepKind.Fold)), ModelIds.House, Grade.Star3, 1.0, "J-16");
        }

        [Test]
        public void J17_CicadaWingsTop1FoldBothHingedLayers()
        {
            var c = Ref(ModelIds.Cicada);
            var steps = c.Take(5).Concat(new[] { WithLayers(c[5], 1), WithLayers(c[6], 1) }).Concat(c.Skip(7));
            AssertPiece(Judge(steps), ModelIds.Cicada, Grade.Star3, 1.0, "J-17");
        }

        [Test]
        public void J18_CicadaWithoutHead()
        {
            var c = Ref(ModelIds.Cicada);
            AssertCrumpled(Judge(c.Take(7).Concat(c.Skip(8))), ModelIds.Cicada, 0.782, "J-18");
        }

        [Test]
        public void ShortcutPolicy_FewerFoldsStayBelow091()
        {
            // CONTENT §4.2: dog without step 1 (0.867) and cicada without steps 2 and 7 (0.892) — policy check only.
            var dog = Ref(ModelIds.Dog).Skip(1);
            Assert.That(ModelCatalog.Recognition.Score(FoldPlanner.ApplyAll(dog), ModelIds.Dog), Is.LessThan(0.91));
            var c = Ref(ModelIds.Cicada);
            var cicada = c.Where((s, i) => i != 1 && i != 6);
            Assert.That(ModelCatalog.Recognition.Score(FoldPlanner.ApplyAll(cicada), ModelIds.Cicada), Is.LessThan(0.91));
        }

        private static readonly double[,] Matrix =
        {
            { 1, 0.487, 0.453, 0.417, 0.483 },
            { 0.487, 1, 0.561, 0.477, 0.411 },
            { 0.453, 0.561, 1, 0.611, 0.453 },
            { 0.417, 0.477, 0.611, 1, 0.451 },
            { 0.483, 0.411, 0.453, 0.451, 1 },
        };

        [Test]
        public void F009_AC02_ModelMatrixMatchesContentAndStaysBelowMatch()
        {
            var cat = ModelCatalog.Recognition;
            for (int a = 0; a < All.Length; a++)
            for (int b = 0; b < All.Length; b++)
            {
                double s = cat.Score(ModelCatalog.ReferenceState(All[a]), All[b]);
                Assert.That(s, Is.EqualTo(Matrix[a, b]).Within(Tol), All[a] + " vs " + All[b]);
                if (a != b) Assert.That(s, Is.LessThan(0.85), All[a] + " vs " + All[b]);
                Assert.That(Similarity.Centered(ModelCatalog.ReferenceState(All[a]), ModelCatalog.ReferenceState(All[b])),
                    Is.EqualTo(s).Within(1e-12), "Similarity.Centered = catalogue score");
            }
        }

        private static PaperState Transform(PaperState s, Func<Vec2, Vec2> tf) =>
            new PaperState(s.Facets.Select(f => new Facet(f.World.Select(tf), f.Paper, f.Layer, f.FrontUp)), s.Creases);

        [Test]
        public void F009_AC03_TranslatedRotatedMirroredRawStatesScoreOne()
        {
            var tfs = new Dictionary<string, Func<Vec2, Vec2>>
            {
                ["translate"] = q => V(q.X + 0.3, q.Y - 0.2),
                ["rotate90"] = q => V(-q.Y, q.X),
                ["mirror"] = q => V(-q.X, q.Y),   // vertex order left clockwise on purpose (orientation-free)
            };
            foreach (var id in All)
            foreach (var kv in tfs)
            {
                var raw = Transform(ModelCatalog.ReferenceState(id), kv.Value);
                double s = ModelCatalog.Recognition.Score(raw, id);
                Assert.That(s, Is.EqualTo(1.0).Within(Tol), id + " " + kv.Key);
                var r = Recognizer.Recognize(raw, All, ModelCatalog.Recognition);
                Assert.AreEqual(id, r.ModelId, id + " " + kv.Key);
                Assert.AreEqual(Grade.Star3, r.Grade, id + " " + kv.Key);
            }
        }

        [Test]
        public void F009_AC05_TieGoesToEarlierCatalogueModel()
        {
            var house = ModelCatalog.ReferenceState(ModelIds.House);
            var ab = new RecognitionCatalog(new[]
            {
                new KeyValuePair<string, PaperState>("A", house), new KeyValuePair<string, PaperState>("B", house),
            });
            var r = Recognizer.Recognize(house, new[] { "B", "A" }, ab);
            Assert.AreEqual("A", r.ModelId);
            Assert.AreEqual("A", r.BestModelId);
            var ba = new RecognitionCatalog(new[]
            {
                new KeyValuePair<string, PaperState>("B", house), new KeyValuePair<string, PaperState>("A", house),
            });
            Assert.AreEqual("B", Recognizer.Recognize(house, new[] { "A", "B" }, ba).ModelId);
            CollectionAssert.AreEqual(new[] { "B", "A" }, ba.ModelIds);
            // only owned models compete
            Assert.AreEqual("B", Recognizer.Recognize(house, new[] { "B" }, ab).ModelId);
        }

        [Test]
        public void F009_AC06_RecognitionIsDeterministic()
        {
            var state = FoldPlanner.ApplyAll(Var(0.06));
            var first = Recognizer.Recognize(state, All, ModelCatalog.Recognition);
            for (int i = 0; i < 100; i++)
            {
                var r = Recognizer.Recognize(state, All, ModelCatalog.Recognition);
                Assert.AreEqual(first.ModelId, r.ModelId);
                Assert.AreEqual(first.Grade, r.Grade);
                Assert.AreEqual(first.BestModelId, r.BestModelId);
                Assert.IsTrue(first.BestScore == r.BestScore, "bitwise identical score");
            }
            // a freshly built catalogue gives the identical score as well
            var fresh = new RecognitionCatalog(All.Select(id => new KeyValuePair<string, PaperState>(id, ModelCatalog.ReferenceState(id))));
            Assert.IsTrue(first.BestScore == Recognizer.Recognize(state, All, fresh).BestScore);
        }

        [Test]
        public void Result_FieldsAreConsistent()
        {
            var r = Judge(Var(0.15));
            Assert.AreEqual(Grading.Percent(r.BestScore), r.Percent);
            Assert.AreEqual(Grading.FromScore(r.BestScore), r.Grade);
            Assert.AreEqual(r.IsPiece, r.ModelId != null);
        }

        [Test]
        public void NoCandidates_IsCrumpledWithoutBest()
        {
            var state = FoldPlanner.ApplyAll(Ref(ModelIds.House));
            foreach (var owned in new[] { new string[0], new[] { "unicorn", null, "" } })
            {
                var r = Recognizer.Recognize(state, owned, ModelCatalog.Recognition);
                Assert.IsNull(r.ModelId);
                Assert.IsNull(r.BestModelId);
                Assert.AreEqual(0.0, r.BestScore);
                Assert.AreEqual(0.0, r.Score);
                Assert.AreEqual(0, r.Percent);
                Assert.AreEqual(Grade.None, r.Grade);
                Assert.IsFalse(r.IsPiece);
            }
            // duplicates and unknown IDs are ignored
            var dup = Recognizer.Recognize(state, new[] { ModelIds.House, "x", ModelIds.House }, ModelCatalog.Recognition);
            Assert.AreEqual(ModelIds.House, dup.ModelId);
        }

        [Test]
        public void ArgumentChecks()
        {
            var cat = ModelCatalog.Recognition;
            Assert.Throws<ArgumentNullException>(() => Recognizer.Recognize(null, All, cat));
            Assert.Throws<ArgumentNullException>(() => Recognizer.Recognize(PaperState.Initial, null, cat));
            Assert.Throws<ArgumentNullException>(() => Recognizer.Recognize(PaperState.Initial, All, null));
            Assert.Throws<ArgumentException>(() => cat.Score(PaperState.Initial, "unicorn"));
            Assert.Throws<ArgumentNullException>(() => cat.Score(PaperState.Initial, null));
            Assert.Throws<ArgumentNullException>(() => cat.Score(null, ModelIds.House));
            Assert.Throws<ArgumentNullException>(() => new RecognitionCatalog(null));
            var s = PaperState.Initial;
            Assert.Throws<ArgumentException>(() => new RecognitionCatalog(new[] { new KeyValuePair<string, PaperState>(null, s) }));
            Assert.Throws<ArgumentException>(() => new RecognitionCatalog(new[] { new KeyValuePair<string, PaperState>("", s) }));
            Assert.Throws<ArgumentException>(() => new RecognitionCatalog(new[] { new KeyValuePair<string, PaperState>("a", null) }));
            Assert.Throws<ArgumentException>(() => new RecognitionCatalog(new[]
            {
                new KeyValuePair<string, PaperState>("a", s), new KeyValuePair<string, PaperState>("a", s),
            }));
            CollectionAssert.AreEqual(All, cat.ModelIds);
        }
    }
}
