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
    // QA ORIGAMI-009: black-box checks of v2 recognition against FEATURE_SPECS F-009 and CONTENT_SPECS §4.
    // Inputs are built from the QA transcription of CONTENT §2 (V2SpecBook) and the §4 "입력" column.
    // Per the task (review W-6 / ORIGAMI-008 W-3) no test asserts the dog-without-chin case or a J-case Percent value;
    // shortcut results are only asserted "< 0.91".
    public class RecognitionSpecQaTests
    {
        static readonly double K = V2SpecBook.K, X0 = V2SpecBook.X0;
        static readonly string[] All = V2SpecBook.Ids;
        static readonly IEnumerable<string> AllIds = V2SpecBook.Ids;

        static FoldStep F(double gx, double gy, double dx, double dy, bool m = false, int l = 0) => V2SpecBook.Fold(gx, gy, dx, dy, m, l);
        static List<FoldStep> Book(string id) => V2SpecBook.Steps(id);
        static List<FoldStep> Pick(string id, params int[] oneBased) => oneBased.Select(i => Book(id)[i - 1]).ToList();

        static List<FoldStep> Var(double dy) => new List<FoldStep>
        {
            F(-1, 1, -1, -1), F(-1, 0, -0.6, -0.8 + dy), F(1, 0, 0.6, -0.8 + dy),
        };

        static List<FoldStep> WithLayers(string id, int[] steps, int layers) =>
            Book(id).Select((s, i) => steps.Contains(i + 1) ? FoldStep.Fold(s.Grab, s.Drop, s.Mountain, layers) : s).ToList();

        static RecognitionResult Judge(IEnumerable<FoldStep> steps, IEnumerable<string> owned = null) =>
            Recognizer.Recognize(V2SpecBook.Play(steps).State, owned ?? All, ModelCatalog.Recognition);

        static void Expect(RecognitionResult r, string best, double score, string piece, Grade grade)
        {
            Assert.That(r.BestModelId, Is.EqualTo(best), "best candidate");
            Assert.That(r.BestScore, Is.EqualTo(score).Within(0.01), "score");
            Assert.That(r.Score, Is.EqualTo(r.BestScore));
            Assert.That(r.ModelId, Is.EqualTo(piece), "piece");
            Assert.That(r.IsPiece, Is.EqualTo(piece != null));
            Assert.That(r.Grade, Is.EqualTo(grade), "grade");
            Assert.That(r.Percent, Is.EqualTo(Grading.Percent(r.BestScore)));
        }

        // ---- F-009 AC-01: CONTENT §4 J-01 .. J-18 ------------------------------------------------------

        [TestCaseSource(nameof(AllIds))]
        public void J01_BookSequenceIsThreeStarsHundred(string id)
        {
            var r = Judge(Book(id));
            Expect(r, id, 1.0, id, Grade.Star3);
            Assert.That(r.Percent, Is.EqualTo(100), "J-01 is shown as 100% in the table");
        }

        [Test] public void J02_HouseBottomUp() =>
            Expect(Judge(new[] { F(-1, -1, -1, 1), F(-1, 0, -0.6, 0.8), F(1, 0, 0.6, 0.8) }), "house", 1.0, "house", Grade.Star3);

        [Test] public void J03_PlaneNoCreaseRightFirst() =>
            Expect(Judge(new[] { F(1, 1, 0, 0), F(-1, 1, 0, 0), F(1, 0, 0, K), F(-1, 0, 0, K), F(X0, -1, -X0, -1) }), "plane", 1.0, "plane", Grade.Star3);

        [Test] public void J04_Var001() => Expect(Judge(Var(0.01)), "house", 0.992, "house", Grade.Star3);
        [Test] public void J05_Var006() => Expect(Judge(Var(0.06)), "house", 0.951, "house", Grade.Star2);
        [Test] public void J06_Var015() => Expect(Judge(Var(0.15)), "house", 0.892, "house", Grade.Star1);

        [Test] public void J07_PlaneWrongSides()
        {
            var seq = Pick("plane", 1, 2, 3);
            seq.Add(F(-1, 0, 0, -0.3)); seq.Add(F(1, 0, 0, -0.3)); seq.Add(Book("plane")[5]);
            Expect(Judge(seq), "plane", 0.804, null, Grade.None);
        }

        [Test] public void J08_HouseOneRoofSide() =>
            Expect(Judge(new[] { F(-1, 1, -1, -1), F(-1, 0, -0.6, -0.8) }), "house", 0.783, null, Grade.None);

        [Test] public void J09_HalfOnce() => Expect(Judge(new[] { F(-1, 1, -1, -1) }), "house", 0.728, null, Grade.None);

        [Test] public void J10_DogChinWithoutEars() => Expect(Judge(Pick("dog", 1, 2, 3, 6)), "dog", 0.693, null, Grade.None);

        [Test] public void J11_CicadaWithoutSideFolds() => Expect(Judge(Book("cicada").Take(8)), "tulip", 0.677, null, Grade.None);

        [Test] public void J12_DogWithoutBook() =>
            Expect(Judge(Book("dog"), new[] { "plane", "house" }), "house", 0.477, null, Grade.None);

        [TestCaseSource(nameof(AllIds))]
        public void J13_FlippedReferenceStillThreeStars(string id)
        {
            var seq = Book(id);
            seq.Add(FoldStep.Flip());
            var r = Judge(seq);
            Assert.That(r.BestModelId, Is.EqualTo(id));
            Assert.That(r.BestScore, Is.InRange(0.994 - 0.01, 1.0 + 1e-9));
            Assert.That(r.Grade, Is.EqualTo(Grade.Star3));
            Assert.That(r.ModelId, Is.EqualTo(id));
        }

        [Test] public void J14_TulipPetalsWithAllLayers() =>
            Expect(Judge(WithLayers("tulip", new[] { 4, 5 }, 0)), "cicada", 0.714, null, Grade.None);

        [Test] public void J15_CicadaWingAllLayersThenSides()
        {
            var seq = Pick("cicada", 1, 2, 3, 4, 5);
            var w = Book("cicada")[5];
            seq.Add(FoldStep.Fold(w.Grab, w.Drop, false, 0));
            seq.Add(Book("cicada")[8]);
            seq.Add(Book("cicada")[9]);
            Expect(Judge(seq), "cicada", 0.648, null, Grade.None);
        }

        [Test] public void J16_HouseFoldsOnly() => Expect(Judge(Pick("house", 1, 4, 5)), "house", 1.0, "house", Grade.Star3);

        [Test] public void J17_CicadaWingsTopOne() =>
            Expect(Judge(WithLayers("cicada", new[] { 6, 7 }, 1)), "cicada", 1.0, "cicada", Grade.Star3);

        [Test] public void J18_CicadaWithoutHead() =>
            Expect(Judge(Pick("cicada", 1, 2, 3, 4, 5, 6, 7, 9, 10)), "cicada", 0.782, null, Grade.None);

        // ---- F-009 AC-02: §4 cross table ---------------------------------------------------------------

        static readonly double[,] Matrix =
        {
            { 1, 0.487, 0.453, 0.417, 0.483 },
            { 0.487, 1, 0.561, 0.477, 0.411 },
            { 0.453, 0.561, 1, 0.611, 0.453 },
            { 0.417, 0.477, 0.611, 1, 0.451 },
            { 0.483, 0.411, 0.453, 0.451, 1 },
        };

        [Test]
        public void AC02_CrossTableBothDirections()
        {
            var refs = All.Select(id => V2SpecBook.Play(Book(id)).State).ToArray();
            for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
            {
                double s = ModelCatalog.Recognition.Score(refs[i], All[j]);
                Assert.That(s, Is.EqualTo(Matrix[i, j]).Within(0.01), $"{All[i]} vs {All[j]}");
                if (i != j) Assert.That(s, Is.LessThan(0.85));
                Assert.That(Similarity.Centered(refs[i], refs[j]), Is.EqualTo(s).Within(1e-9), "Similarity vs catalogue");
            }
        }

        // ---- F-009 AC-03: invariance on raw states (fresh random transforms) ---------------------------

        static PaperState Transform(PaperState s, Func<Vec2, Vec2> tf, Random rng)
        {
            // raw state: transformed world points, scrambled layers/frontUp, random facet order, random vertex direction
            var facets = s.Facets.Select(f =>
            {
                var w = f.World.Select(tf).ToList();
                var p = f.Paper.ToList();
                if (rng.Next(2) == 0) { w.Reverse(); p.Reverse(); }
                return new Facet(w, p, rng.Next(50), rng.Next(2) == 0);
            }).OrderBy(_ => rng.Next()).ToList();
            return new PaperState(facets, null);
        }

        [TestCaseSource(nameof(AllIds))]
        public void AC03_SpecTransformsScoreOne(string id)
        {
            var rng = new Random(3);
            var refState = ModelCatalog.ReferenceState(id);
            var cases = new Func<Vec2, Vec2>[]
            {
                p => new Vec2(p.X + 0.3, p.Y - 0.2),
                p => new Vec2(-p.Y, p.X),
                p => new Vec2(-p.X, p.Y),
            };
            foreach (var tf in cases)
            {
                var r = Recognizer.Recognize(Transform(refState, tf, rng), All, ModelCatalog.Recognition);
                Assert.That(r.ModelId, Is.EqualTo(id));
                Assert.That(ModelCatalog.Recognition.Score(Transform(refState, tf, rng), id), Is.EqualTo(1.0).Within(0.01));
            }
        }

        [TestCaseSource(nameof(AllIds))]
        public void AC03_RandomD4PlusTranslationScoreOne(string id)
        {
            var rng = new Random(4100 + Array.IndexOf(All, id));   // stable seed (string.GetHashCode is randomised on .NET)
            var refState = ModelCatalog.ReferenceState(id);
            for (int t = 0; t < 12; t++)
            {
                int g = rng.Next(8);
                double dx = rng.NextDouble() * 6 - 3, dy = rng.NextDouble() * 6 - 3;
                var raw = Transform(refState, p => { var q = D4.Apply(g, p); return new Vec2(q.X + dx, q.Y + dy); }, rng);
                double s = ModelCatalog.Recognition.Score(raw, id);
                Assert.That(s, Is.EqualTo(1.0).Within(0.01), $"g{g} ({dx:F2},{dy:F2})");
                var r = Recognizer.Recognize(raw, All, ModelCatalog.Recognition);
                Assert.That(r.ModelId, Is.EqualTo(id));
                Assert.That(r.Grade, Is.EqualTo(Grade.Star3));
            }
        }

        [Test]
        public void AC03_FacetOrderAndCreasesDoNotMatter()
        {
            var house = V2SpecBook.Play(Book("house")).State;   // has 4 crease segments
            var noCreases = new PaperState(house.Facets.Reverse(), null);
            Assert.That(ModelCatalog.Recognition.Score(noCreases, "house"),
                Is.EqualTo(ModelCatalog.Recognition.Score(house, "house")).Within(1e-12));
        }

        // ---- F-009 AC-04: grade boundaries --------------------------------------------------------------

        [TestCase(1.0, 100, Grade.Star3)]
        [TestCase(0.97, 97, Grade.Star3)]
        [TestCase(0.969, 96, Grade.Star2)]
        [TestCase(0.93, 93, Grade.Star2)]
        [TestCase(0.929, 92, Grade.Star1)]
        [TestCase(0.85, 85, Grade.Star1)]
        [TestCase(0.849, 84, Grade.None)]
        [TestCase(0.57, 57, Grade.None)]
        [TestCase(0.29, 29, Grade.None)]
        [TestCase(0.0, 0, Grade.None)]
        public void AC04_PercentAndGrade(double s, int percent, Grade grade)
        {
            Assert.That(Grading.Percent(s), Is.EqualTo(percent));
            Assert.That(Grading.FromScore(s), Is.EqualTo(grade));
            Assert.That(Grading.FromPercent(percent), Is.EqualTo(grade));
        }

        [Test]
        public void AC04_FromPercentThresholds()
        {
            for (int p = 0; p <= 100; p++)
            {
                var e = p >= 97 ? Grade.Star3 : p >= 93 ? Grade.Star2 : p >= 85 ? Grade.Star1 : Grade.None;
                Assert.That(Grading.FromPercent(p), Is.EqualTo(e), "percent " + p);
            }
            Assert.That(Grading.MatchMin, Is.EqualTo(85));
            Assert.That(Grading.Grade2Min, Is.EqualTo(93));
            Assert.That(Grading.Grade3Min, Is.EqualTo(97));
        }

        // ---- F-009 AC-05: tie rule / owned filter --------------------------------------------------------

        [Test]
        public void AC05_TieGoesToEarlierCatalogueEntry()
        {
            var house = ModelCatalog.ReferenceState("house");
            var player = V2SpecBook.Play(Var(0.06)).State;
            var ab = new RecognitionCatalog(new[] { Kv("A", house), Kv("B", house) });
            var ba = new RecognitionCatalog(new[] { Kv("B", house), Kv("A", house) });
            Assert.That(Recognizer.Recognize(player, new[] { "A", "B" }, ab).ModelId, Is.EqualTo("A"));
            Assert.That(Recognizer.Recognize(player, new[] { "B", "A" }, ab).ModelId, Is.EqualTo("A"), "owned order is irrelevant");
            Assert.That(Recognizer.Recognize(player, new[] { "A", "B" }, ba).ModelId, Is.EqualTo("B"));
            Assert.That(Recognizer.Recognize(player, new[] { "B" }, ab).ModelId, Is.EqualTo("B"), "only owned candidates");
        }

        [Test]
        public void AC05_OwnedFilterAndEmptyCandidates()
        {
            var tulip = ModelCatalog.ReferenceState("tulip");
            var r = Recognizer.Recognize(tulip, new[] { "plane", "house", "unicorn", "house", null }, ModelCatalog.Recognition);
            Assert.That(r.BestModelId, Is.EqualTo("house"));
            Assert.That(r.IsPiece, Is.False);
            var none = Recognizer.Recognize(tulip, new string[0], ModelCatalog.Recognition);
            Assert.That(none.BestModelId, Is.Null);
            Assert.That(none.BestScore, Is.EqualTo(0));
            Assert.That(none.ModelId, Is.Null);
            Assert.That(none.Grade, Is.EqualTo(Grade.None));
            Assert.Throws<ArgumentNullException>(() => Recognizer.Recognize(null, All, ModelCatalog.Recognition));
            Assert.Throws<ArgumentNullException>(() => Recognizer.Recognize(tulip, null, ModelCatalog.Recognition));
            Assert.Throws<ArgumentNullException>(() => Recognizer.Recognize(tulip, All, null));
            Assert.Throws<ArgumentException>(() => ModelCatalog.Recognition.Score(tulip, "unicorn"));
            Assert.Throws<ArgumentException>(() => new RecognitionCatalog(new[] { Kv("A", tulip), Kv("A", tulip) }));
            Assert.Throws<ArgumentException>(() => new RecognitionCatalog(new[] { Kv("", tulip) }));
        }

        static KeyValuePair<string, PaperState> Kv(string k, PaperState v) => new KeyValuePair<string, PaperState>(k, v);

        // ---- F-009 AC-06: determinism ---------------------------------------------------------------------

        [Test]
        public void AC06_HundredRepeatsBitIdentical()
        {
            var state = V2SpecBook.Play(Var(0.06)).State;
            var first = Recognizer.Recognize(state, All, ModelCatalog.Recognition);
            for (int i = 0; i < 100; i++)
            {
                var r = Recognizer.Recognize(state, All, ModelCatalog.Recognition);
                Assert.That(BitConverter.DoubleToInt64Bits(r.BestScore), Is.EqualTo(BitConverter.DoubleToInt64Bits(first.BestScore)));
                Assert.That(r.BestModelId, Is.EqualTo(first.BestModelId));
                Assert.That(r.Grade, Is.EqualTo(first.Grade));
                Assert.That(r.Percent, Is.EqualTo(first.Percent));
            }
            // A freshly built catalogue from the same references gives the same bits.
            var fresh = new RecognitionCatalog(All.Select(id => Kv(id, ModelCatalog.ReferenceState(id))));
            var again = Recognizer.Recognize(state, All, fresh);
            Assert.That(BitConverter.DoubleToInt64Bits(again.BestScore), Is.EqualTo(BitConverter.DoubleToInt64Bits(first.BestScore)));
        }

        // ---- CONTENT §4.2 [1] exhaustive sub-sequence shortcut policy (paid models: < 0.91) -------------

        [Test]
        public void Shortcut_AllBookSubsequencesWithFewerFoldsStayBelowPolicy()
        {
            var paid = new[] { "tulip", "dog", "cicada" };
            var bookFolds = All.ToDictionary(id => id, id => Book(id).Count(s => s.Kind == FoldStepKind.Fold));
            var expectValid = new Dictionary<string, (int valid, int total)>
            {
                ["plane"] = (30, 31), ["house"] = (6, 7), ["tulip"] = (25, 31), ["dog"] = (14, 15), ["cicada"] = (158, 255),
            };
            var best = paid.ToDictionary(p => p, p => 0.0);
            foreach (var id in All)
            {
                var steps = Book(id);
                var foldIdx = Enumerable.Range(0, steps.Count).Where(i => steps[i].Kind == FoldStepKind.Fold).ToList();
                int n = foldIdx.Count, valid = 0, total = 0;
                for (int mask = 1; mask < (1 << n); mask++)   // mask = folds removed (at least one)
                {
                    total++;
                    var removed = new HashSet<int>(Enumerable.Range(0, n).Where(b => (mask & (1 << b)) != 0).Select(b => foldIdx[b]));
                    var seq = steps.Where((s, i) => !removed.Contains(i)).ToList();
                    PaperState st;
                    try { st = FoldPlanner.ApplyAll(seq); }
                    catch (InvalidOperationException) { continue; }
                    int folds = n - removed.Count;
                    if (folds == 0) continue;   // balance_check.py subsequences(): states with no fold are not counted
                    valid++;
                    foreach (var p in paid)
                        if (folds < bookFolds[p]) best[p] = Math.Max(best[p], ModelCatalog.Recognition.Score(st, p));
                }
                Assert.That((valid, total), Is.EqualTo(expectValid[id]), id + " valid / total sub-sequences");
            }
            foreach (var p in paid)
            {
                TestContext.WriteLine($"{p}: best shortcut {best[p]:F4}");
                Assert.That(best[p], Is.LessThan(0.91), p + " shortcut policy");
            }
        }
    }
}
